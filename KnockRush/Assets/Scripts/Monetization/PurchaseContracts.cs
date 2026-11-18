using System.Collections.Generic;
using System.Threading.Tasks;

namespace KnockRush.Monetization
{
    /// <summary>A real-money gem pack. Ids must match GEM_PACKS in Backend/.../monetization.ts and the store listings.</summary>
    public readonly struct GemProduct
    {
        public readonly string ProductId;
        public readonly int Gems;
        public readonly string FallbackPrice;

        public GemProduct(string productId, int gems, string fallbackPrice)
        {
            ProductId = productId;
            Gems = gems;
            FallbackPrice = fallbackPrice;
        }
    }

    public static class GemProducts
    {
        public static readonly IReadOnlyList<GemProduct> All = new[]
        {
            new GemProduct("knockrush.gems.100", 100, "$0.99"),
            new GemProduct("knockrush.gems.550", 550, "$4.99"),
            new GemProduct("knockrush.gems.1200", 1200, "$9.99"),
            new GemProduct("knockrush.gems.2500", 2500, "$19.99")
        };
    }

    /// <summary>Real-money purchases. Every purchase is verified and granted by the backend.</summary>
    public interface IPurchaseService
    {
        bool IsReady { get; }

        Task<bool> InitializeAsync();

        /// <summary>Localised price from the store (or a fallback before the store answers).</summary>
        string PriceText(string productId);

        /// <summary>Buys a gem pack; returns the gems granted (0 if cancelled or a duplicate).</summary>
        Task<int> BuyAsync(string productId);
    }

    /// <summary>Rewarded video ads. Rewards are granted server side via AdMob's signed callback.</summary>
    public interface IRewardedAdService
    {
        /// <summary>False until an ad network SDK and ad unit ids are configured.</summary>
        bool IsAvailable { get; }

        /// <summary>Shows an ad; true if the player watched it to the end (reward arrives server side).</summary>
        Task<bool> ShowAsync();
    }

    /// <summary>Default ad service: no ad network bundled yet (see Docs/PHASE_8_UI_MONETIZATION.md).</summary>
    public sealed class UnconfiguredRewardedAds : IRewardedAdService
    {
        public bool IsAvailable => false;

        public Task<bool> ShowAsync() => Task.FromResult(false);
    }
}
