using System;
using System.Linq;
using KnockRush.Backend;
using KnockRush.Core.Events;
using KnockRush.Core.Services;
using KnockRush.Inventory;
using KnockRush.Monetization;
using KnockRush.UI.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KnockRush.UI.Screens
{
    /// <summary>Coins/gems pills that stay in sync with the profile.</summary>
    internal sealed class WalletView
    {
        private readonly Label coins;
        private readonly Label gems;

        public WalletView(out VisualElement element)
        {
            element = Ui.Row();
            element.Add(Ui.CurrencyPill("kr-coin", out coins));
            element.Add(Ui.CurrencyPill("kr-gem", out gems));
            EventBus.Subscribe<ProfileUpdatedEvent>(_ => Refresh());
        }

        public void Refresh()
        {
            var profile = UiData.Profile;
            coins.text = profile != null ? Ui.FormatNumber(profile.Coins) : "-";
            gems.text = profile != null ? Ui.FormatNumber(profile.Gems) : "-";
        }
    }

    /// <summary>12 · Shop: daily featured items, bundles, gem packs (real money) and free coins (ads).</summary>
    public sealed class ShopScreen : UIScreen
    {
        private enum Tab
        {
            Featured,
            Bundles,
            Gems,
            Free
        }

        private WalletView wallet;
        private VisualElement tabs;
        private VisualElement content;
        private Label resetLabel;
        private Tab tab;
        private ShopResponse shop;
        private float resetsIn;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop");
            wallet = new WalletView(out var walletElement);
            root.Add(Ui.Header("SHOP", () => Nav.Pop(), walletElement));
            var bar = Ui.Row();
            tabs = Ui.Row();
            foreach (Tab t in Enum.GetValues(typeof(Tab)))
            {
                var value = t;
                var button = new Button(() => Show(value)) { text = t == Tab.Free ? "FREE COINS" : t.ToString().ToUpperInvariant(), userData = t };
                button.AddToClassList("kr-tab");
                button.AddToClassList("kr-display");
                tabs.Add(button);
            }

            bar.Add(tabs);
            bar.Add(Ui.Spacer());
            resetLabel = Ui.Label(string.Empty, "kr-subtle");
            bar.Add(resetLabel);
            root.Add(bar);
            content = Ui.Scroll();
            root.Add(content);
        }

        public override async void OnShow()
        {
            wallet.Refresh();
            shop = null;
            Show(tab);
            if (!UiData.Online)
            {
                return;
            }

            await App.Run(async () =>
            {
                await UiData.EnsurePricesAsync();
                shop = await UiData.Backend.GetShopAsync();
            });
            resetsIn = shop?.ResetsInSeconds ?? 0;
            if (IsVisible)
            {
                Show(tab);
            }
        }

        public override void Tick(float deltaTime)
        {
            if (shop == null)
            {
                resetLabel.text = string.Empty;
                return;
            }

            resetsIn = Mathf.Max(0f, resetsIn - deltaTime);
            var span = TimeSpan.FromSeconds(resetsIn);
            resetLabel.text = $"New items in {(int)span.TotalHours}h {span.Minutes:00}m";
        }

        private void Show(Tab value)
        {
            tab = value;
            foreach (var child in tabs.Children())
            {
                child.EnableInClassList("kr-tab--active", (Tab)child.userData == value);
            }

            content.Clear();
            switch (value)
            {
                case Tab.Featured:
                    ShowFeatured();
                    break;
                case Tab.Bundles:
                    ShowBundles();
                    break;
                case Tab.Gems:
                    ShowGems();
                    break;
                default:
                    ShowFree();
                    break;
            }
        }

        private void ShowFeatured()
        {
            if (shop == null)
            {
                content.Add(UiData.Online ? Ui.Label("Loading today's shop...", "kr-subtle") : OfflineNotice.Create("the shop"));
                return;
            }

            var catalog = CosmeticCatalog.Load();
            var grid = Ui.Element("kr-wrap");
            foreach (var entry in shop.Featured)
            {
                var item = catalog?.Get(entry.Id);
                if (item == null)
                {
                    continue;
                }

                var owned = UiData.Owns(entry.Id);
                var price = $"{entry.Price} {(entry.Currency == "gems" ? "GEMS" : "COINS")}";
                grid.Add(ItemCards.Create(item, true, false, false, () => Buy(entry, item), owned ? "OWNED" : price));
            }

            content.Add(grid);
        }

        private void Buy(CatalogEntry entry, CosmeticDefinition item)
        {
            if (UiData.Owns(entry.Id))
            {
                App.Toast($"You already own {item.DisplayName}.");
                return;
            }

            DialogScreen.Show(App, $"BUY {item.DisplayName.ToUpperInvariant()}?",
                $"{item.Rarity} {UiData.SlotLabel(item.Slot).TrimEnd('s').ToLowerInvariant()} for {entry.Price} {entry.Currency}.", "BUY",
                async () =>
                {
                    if (await App.Run(() => UiData.Backend.PurchaseAsync(entry.Id), $"{item.DisplayName} unlocked! Equip it in the Locker."))
                    {
                        Show(tab);
                    }
                }, "CANCEL");
        }

        private void ShowBundles()
        {
            if (shop == null)
            {
                content.Add(UiData.Online ? Ui.Label("Loading bundles...", "kr-subtle") : OfflineNotice.Create("bundles"));
                return;
            }

            var catalog = CosmeticCatalog.Load();
            var row = Ui.Element("kr-wrap");
            foreach (var bundle in shop.Bundles)
            {
                var panel = Ui.Panel("kr-bundle");
                panel.Add(Ui.Heading(bundle.Name.ToUpperInvariant()));
                var items = Ui.Element("kr-wrap");
                foreach (var id in bundle.Items)
                {
                    var item = catalog?.Get(id);
                    if (item != null)
                    {
                        items.Add(ItemCards.Create(item, UiData.Owns(id), false, false, null, UiData.Owns(id) ? "OWNED" : string.Empty));
                    }
                }

                panel.Add(items);
                var allOwned = bundle.Items.All(UiData.Owns);
                var b = bundle;
                var buy = Ui.Button(allOwned ? "OWNED" : $"BUY · {bundle.Price} {(bundle.Currency == "gems" ? "GEMS" : "COINS")}", async () =>
                {
                    if (await App.Run(() => UiData.Backend.PurchaseBundleAsync(b.Id), $"{b.Name} unlocked!"))
                    {
                        Show(tab);
                    }
                }, "kr-btn--good");
                buy.SetEnabled(!allOwned);
                panel.Add(buy);
                row.Add(panel);
            }

            content.Add(row);
        }

        private async void ShowGems()
        {
            ServiceLocator.TryGet<IPurchaseService>(out var store);
            var row = Ui.Row("kr-center");
            foreach (var product in GemProducts.All)
            {
                var card = Ui.Panel("kr-gem-pack");
                var icon = Ui.Element("kr-gem");
                icon.style.width = icon.style.height = 64;
                card.Add(icon);
                card.Add(Ui.Title(Ui.FormatNumber(product.Gems)));
                card.Add(Ui.Label("GEMS", "kr-subtle"));
                var id = product.ProductId;
                var button = Ui.Button(store != null ? store.PriceText(id) : product.FallbackPrice, () => BuyGems(store, id), "kr-btn--good");
                button.name = id;
                card.Add(button);
                row.Add(card);
            }

            content.Add(row);
            content.Add(Ui.Label("Gems buy cosmetics and the season pass only: nothing that affects gameplay.", "kr-subtle"));

            if (store != null && !store.IsReady && await store.InitializeAsync() && IsVisible && tab == Tab.Gems)
            {
                foreach (var product in GemProducts.All)
                {
                    if (content.Q<Button>(product.ProductId) is { } button)
                    {
                        button.text = store.PriceText(product.ProductId);
                    }
                }
            }
        }

        private async void BuyGems(IPurchaseService store, string productId)
        {
            if (store == null || !UiData.Online)
            {
                App.Toast("Purchases need an internet connection.");
                return;
            }

            var gems = 0;
            if (await App.Run(async () => gems = await store.BuyAsync(productId)) && gems > 0)
            {
                App.Toast($"+{gems} gems! Thank you for supporting KnockRush.");
            }
        }

        private void ShowFree()
        {
            ServiceLocator.TryGet<IRewardedAdService>(out var ads);
            var panel = Ui.Panel("kr-dialog");
            panel.Add(Ui.Heading("FREE COINS"));
            panel.Add(Ui.Label("Watch a short video for 50 coins, up to 5 times a day.", "kr-dialog__text"));
            var watch = Ui.Button(ads != null && ads.IsAvailable ? "WATCH VIDEO" : "VIDEOS UNAVAILABLE", async () =>
            {
                if (ads != null && await ads.ShowAsync())
                {
                    App.Toast("Thanks for watching! Your coins arrive in a moment.");
                    await System.Threading.Tasks.Task.Delay(3000);
                    if (UiData.Online)
                    {
                        await App.Run(() => UiData.Backend.RefreshProfileAsync());
                    }
                }
            }, "kr-btn--good");
            watch.SetEnabled(ads != null && ads.IsAvailable && UiData.Online);
            panel.Add(watch);
            panel.Add(Ui.Label("Or earn coins by playing: every match, round and win pays out, plus daily missions.", "kr-subtle"));
            var wrap = Ui.Col("kr-center");
            wrap.Add(panel);
            content.Add(wrap);
        }
    }

    /// <summary>14 · Season Pass: 30 tiers, free and premium tracks, claim rewards, buy premium.</summary>
    public sealed class SeasonPassScreen : UIScreen
    {
        private Label title;
        private Label tierLabel;
        private VisualElement xpFill;
        private Button premium;
        private ScrollView track;
        private SeasonResponse season;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop");
            root.Add(Ui.Header("SEASON PASS", () => Nav.Pop()));
            var summary = Ui.Row("kr-season-summary");
            var info = Ui.Col("kr-grow");
            title = Ui.Heading("SEASON 1");
            info.Add(title);
            tierLabel = Ui.Label(string.Empty, "kr-subtle");
            info.Add(tierLabel);
            var bar = Ui.ProgressBar(out xpFill);
            bar.style.width = 520;
            info.Add(bar);
            summary.Add(info);
            premium = Ui.Button("GET PREMIUM", BuyPremium, "kr-btn--primary");
            premium.style.fontSize = 30;
            summary.Add(premium);
            root.Add(summary);

            var labels = Ui.Row();
            track = Ui.Scroll(ScrollViewMode.Horizontal);
            track.style.flexGrow = 1;
            root.Add(track);
        }

        public override async void OnShow()
        {
            track.Clear();
            if (!UiData.Online)
            {
                track.Add(OfflineNotice.Create("the season pass"));
                return;
            }

            await Load();
        }

        private async System.Threading.Tasks.Task Load()
        {
            SeasonResponse response = null;
            if (!await App.Run(async () =>
                {
                    await UiData.EnsurePricesAsync();
                    response = await UiData.Backend.GetSeasonAsync();
                }) || response == null)
            {
                return;
            }

            season = response;
            Render();
        }

        private void Render()
        {
            var s = season.Season;
            var player = season.Player;
            title.text = s.Name.ToUpperInvariant();
            var xpIntoTier = player.SeasonXp - player.Tier * s.XpPerTier;
            tierLabel.text = player.Tier >= s.Tiers ? $"Tier {player.Tier} / {s.Tiers} · season complete!" : $"Tier {player.Tier} / {s.Tiers} · {xpIntoTier}/{s.XpPerTier} XP to the next tier";
            Ui.SetProgress(xpFill, player.Tier >= s.Tiers ? 1f : (float)xpIntoTier / Mathf.Max(1, s.XpPerTier));
            premium.text = player.Premium ? "PREMIUM ACTIVE" : $"GET PREMIUM · {s.PremiumPriceGems} GEMS";
            premium.SetEnabled(!player.Premium);

            track.Clear();
            var row = Ui.Row();
            row.style.alignItems = Align.Stretch;
            for (var tier = 1; tier <= s.Tiers; tier++)
            {
                var column = Ui.Col("kr-tier");
                column.EnableInClassList("kr-tier--reached", tier <= player.Tier);
                column.Add(Ui.Label(tier.ToString(), "kr-tier__number", "kr-display"));
                column.Add(RewardCell(tier, false, season.FreeTrack[tier - 1], player.ClaimedFree.Contains(tier), tier <= player.Tier));
                column.Add(RewardCell(tier, true, season.PremiumTrack[tier - 1], player.ClaimedPremium.Contains(tier), tier <= player.Tier && player.Premium));
                row.Add(column);
            }

            track.Add(row);
        }

        private VisualElement RewardCell(int tier, bool isPremium, TierReward reward, bool claimed, bool claimable)
        {
            var cell = Ui.Col("kr-reward");
            cell.EnableInClassList("kr-reward--premium", isPremium);
            var catalog = CosmeticCatalog.Load();
            var item = reward.ItemId != null ? catalog?.Get(reward.ItemId) : null;
            if (item != null)
            {
                cell.Add(Ui.Swatch(UiData.PreviewColor(item)));
                cell.Add(Ui.Label(item.DisplayName, "kr-reward__text"));
            }
            else
            {
                var icon = Ui.Element(reward.Gems > 0 ? "kr-gem" : "kr-coin");
                cell.Add(icon);
                cell.Add(Ui.Label(reward.Gems > 0 ? $"{reward.Gems} gems" : $"{reward.Coins} coins", "kr-reward__text"));
            }

            if (claimed)
            {
                cell.Add(Ui.Label("CLAIMED", "kr-subtle"));
            }
            else if (claimable)
            {
                cell.Add(Ui.Button("CLAIM", async () =>
                {
                    if (await App.Run(() => UiData.Backend.ClaimSeasonRewardAsync(tier, isPremium), item != null ? $"{item.DisplayName} unlocked!" : "Reward claimed!"))
                    {
                        await Load();
                    }
                }, "kr-btn--small", "kr-btn--good"));
            }
            else if (isPremium && !season.Player.Premium)
            {
                cell.Add(Ui.Label("PREMIUM", "kr-subtle"));
            }

            return cell;
        }

        private void BuyPremium()
        {
            if (season == null)
            {
                return;
            }

            var price = season.Season.PremiumPriceGems;
            DialogScreen.Show(App, "PREMIUM PASS", $"Unlock 30 premium rewards (costumes, emotes, gems) for {price} gems. Cosmetic only.", "UNLOCK", async () =>
            {
                if (UiData.Profile != null && UiData.Profile.Gems < price)
                {
                    App.Toast("Not enough gems: get more in the shop.");
                    Nav.Push<ShopScreen>();
                    return;
                }

                if (await App.Run(() => UiData.Backend.BuyPremiumPassAsync(), "Premium pass unlocked!"))
                {
                    await Load();
                }
            }, "CANCEL");
        }
    }
}
