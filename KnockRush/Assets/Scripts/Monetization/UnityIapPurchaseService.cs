using System;
using System.Threading;
using System.Threading.Tasks;
using KnockRush.Backend;
using KnockRush.Core;
using KnockRush.Core.Services;
using KnockRush.Utilities;
using Newtonsoft.Json.Linq;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

namespace KnockRush.Monetization
{
    /// <summary>
    /// Unity IAP (Google Play Billing / StoreKit) for gem packs. Purchases stay pending until the
    /// backend verifies the receipt with the store and grants the gems exactly once
    /// (validatePurchase), then they are consumed. Editor/dev builds use Unity's fake store, which the
    /// Firebase emulator accepts as the "test" store.
    /// </summary>
    public sealed class UnityIapPurchaseService : IPurchaseService, IDetailedStoreListener
    {
        private const string Tag = "IAP";

        private IStoreController controller;
        private TaskCompletionSource<bool> initialization;
        private TaskCompletionSource<int> pendingBuy;
        private string pendingProductId;

        public bool IsReady => controller != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            AppRoot.RegisterInstaller(registry =>
            {
                if (!AppRoot.IsDedicatedServer)
                {
                    registry.Add<IPurchaseService>(new UnityIapPurchaseService());
                    registry.Add<IRewardedAdService>(new UnconfiguredRewardedAds());
                }
            });
        }

        /// <summary>Lazy: the store connects the first time the shop needs prices.</summary>
        public async Task<bool> InitializeAsync()
        {
            if (initialization != null)
            {
                return await initialization.Task;
            }

            initialization = new TaskCompletionSource<bool>();
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
                foreach (var product in GemProducts.All)
                {
                    builder.AddProduct(product.ProductId, ProductType.Consumable);
                }

                UnityPurchasing.Initialize(this, builder);
            }
            catch (Exception exception)
            {
                Log.Warning(Tag, $"Store unavailable: {exception.Message}");
                initialization.TrySetResult(false);
            }

            var timeout = Task.Delay(TimeSpan.FromSeconds(15));
            var finished = await Task.WhenAny(initialization.Task, timeout);
            if (finished == timeout)
            {
                initialization.TrySetResult(false);
            }

            var ok = await initialization.Task;
            if (!ok)
            {
                initialization = null; // allow a retry next time the shop opens
            }

            return ok;
        }

        public string PriceText(string productId)
        {
            var product = controller?.products.WithID(productId);
            if (product != null && product.availableToPurchase && !string.IsNullOrEmpty(product.metadata.localizedPriceString))
            {
                return product.metadata.localizedPriceString;
            }

            foreach (var gemProduct in GemProducts.All)
            {
                if (gemProduct.ProductId == productId)
                {
                    return gemProduct.FallbackPrice;
                }
            }

            return "-";
        }

        public async Task<int> BuyAsync(string productId)
        {
            if (!await InitializeAsync())
            {
                throw new InvalidOperationException("The store is not available right now.");
            }

            if (pendingBuy != null)
            {
                throw new InvalidOperationException("A purchase is already in progress.");
            }

            pendingBuy = new TaskCompletionSource<int>();
            pendingProductId = productId;
            controller.InitiatePurchase(productId);
            try
            {
                return await pendingBuy.Task;
            }
            finally
            {
                pendingBuy = null;
                pendingProductId = null;
            }
        }

        // ------------------------------------------------------------------------------------
        // IDetailedStoreListener
        // ------------------------------------------------------------------------------------
        public void OnInitialized(IStoreController storeController, IExtensionProvider extensions)
        {
            controller = storeController;
            Log.Info(Tag, $"Store ready ({controller.products.all.Length} products).");
            initialization?.TrySetResult(true);
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            OnInitializeFailed(error, null);
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Log.Warning(Tag, $"Store init failed: {error} {message}");
            initialization?.TrySetResult(false);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            _ = ValidateAndConsume(args.purchasedProduct);
            return PurchaseProcessingResult.Pending; // confirmed after the backend grants the gems
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Fail(product, failureReason.ToString());
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            Fail(product, failureDescription.reason == PurchaseFailureReason.UserCancelled ? null : failureDescription.message);
        }

        private void Fail(Product product, string reason)
        {
            Log.Warning(Tag, $"Purchase failed: {product?.definition.id} {reason}");
            if (reason == null)
            {
                pendingBuy?.TrySetResult(0); // cancelled
            }
            else
            {
                pendingBuy?.TrySetException(new InvalidOperationException($"Purchase failed: {reason}"));
            }
        }

        private async Task ValidateAndConsume(Product product)
        {
            try
            {
                if (!ServiceLocator.TryGet<IBackendService>(out var backend) || backend.State != BackendState.Online)
                {
                    // Left pending: the store re-delivers it on the next launch when we're online.
                    throw new InvalidOperationException("Purchase saved: it will be credited when you're back online.");
                }

                var (store, token) = ReceiptToken(product);
                var result = await backend.ValidatePurchaseAsync(store, product.definition.id, token);
                controller.ConfirmPendingPurchase(product);
                Log.Info(Tag, $"Granted {result.Gems} gems for {product.definition.id} (duplicate: {result.Duplicate}).");
                if (pendingProductId == product.definition.id)
                {
                    pendingBuy?.TrySetResult(result.Gems);
                }
            }
            catch (Exception exception)
            {
                Log.Warning(Tag, $"Validation failed for {product.definition.id}: {exception.Message}");
                if (pendingProductId == product.definition.id)
                {
                    pendingBuy?.TrySetException(exception);
                }
            }
        }

        /// <summary>Extracts the store and the token the backend verifies with that store.</summary>
        internal static (string store, string token) ReceiptToken(Product product)
        {
            var receipt = string.IsNullOrEmpty(product.receipt) ? null : JObject.Parse(product.receipt);
            var storeName = receipt?.Value<string>("Store") ?? string.Empty;
            switch (storeName)
            {
                case "GooglePlay":
                    var payload = JObject.Parse(receipt.Value<string>("Payload"));
                    var json = JObject.Parse(payload.Value<string>("json"));
                    return ("google", json.Value<string>("purchaseToken"));
                case "AppleAppStore":
                case "MacAppStore":
                    return ("apple", product.appleOriginalTransactionID ?? product.transactionID);
                default:
                    // Unity's fake store (editor, development builds): accepted by the emulator only.
                    return ("test", "test-" + (string.IsNullOrEmpty(product.transactionID) ? Guid.NewGuid().ToString("N") : product.transactionID));
            }
        }
    }
}
