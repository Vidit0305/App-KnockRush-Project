using System;
using System.Collections.Generic;
using System.Linq;
using KnockRush.Inventory;
using KnockRush.UI.Framework;
using KnockRush.UI.Stage;
using UnityEngine.UIElements;

namespace KnockRush.UI.Screens
{
    /// <summary>Shared item card for locker, inventory and shop grids.</summary>
    internal static class ItemCards
    {
        public static VisualElement Create(CosmeticDefinition item, bool owned, bool equipped, bool selected, Action onClick, string footer = null)
        {
            var card = new Button(onClick);
            card.AddToClassList("kr-card");
            card.AddToClassList("kr-item");
            card.AddToClassList(UiData.RarityClass(item.Rarity));
            card.EnableInClassList("kr-card--selected", selected);
            card.EnableInClassList("kr-card--locked", !owned);

            var accent = item.Slot == CosmeticSlot.Pattern && item.PatternType > 0 ? UnityEngine.Color.white : (UnityEngine.Color?)null;
            var swatch = Ui.Swatch(UiData.PreviewColor(item), accent);
            if (item.Slot == CosmeticSlot.Hat || item.Slot == CosmeticSlot.Emote || item.Slot == CosmeticSlot.Body && !item.OverridesBodyColor)
            {
                swatch.Add(Ui.Label(Initials(item.DisplayName), "kr-swatch__text", "kr-display"));
            }

            card.Add(swatch);
            card.Add(Ui.Label(item.DisplayName, "kr-item__name"));
            var price = UiData.Price(item.ItemId);
            var status = footer ?? (equipped ? "EQUIPPED" : owned ? "OWNED" : price != null ? $"{price.Price} {(price.Currency == "gems" ? "GEMS" : "COINS")}" : "SEASON");
            var label = Ui.Label(status, "kr-item__status", "kr-display");
            label.EnableInClassList("kr-item__status--equipped", equipped);
            card.Add(label);
            return card;
        }

        private static string Initials(string name)
        {
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant() : name.Substring(0, Math.Min(2, name.Length)).ToUpperInvariant();
        }

        public static IEnumerable<CosmeticDefinition> Sorted(CosmeticCatalog catalog, CosmeticSlot slot)
        {
            return catalog.InSlot(slot).OrderByDescending(i => UiData.Owns(i.ItemId)).ThenBy(i => i.Rarity).ThenBy(i => i.DisplayName);
        }
    }

    /// <summary>10 · Character Customization (Locker): colours, patterns, hats and emotes with live preview.</summary>
    public sealed class CustomizeScreen : UIScreen
    {
        private static readonly CosmeticSlot[] Tabs = { CosmeticSlot.Body, CosmeticSlot.Color, CosmeticSlot.Pattern, CosmeticSlot.Hat, CosmeticSlot.Emote };

        private readonly Dictionary<CosmeticSlot, Button> tabButtons = new Dictionary<CosmeticSlot, Button>();
        private VisualElement grid;
        private Label selectedName;
        private Button action;
        private CosmeticSlot tab = CosmeticSlot.Color;
        private Loadout preview;
        private CosmeticDefinition selected;

        public override bool ShowsStage => true;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-locker");
            root.Add(Ui.Header("LOCKER", () => Nav.Pop()));
            var body = Ui.Row("kr-grow");
            body.style.alignItems = Align.Stretch;

            var left = Ui.Col("kr-grow");
            left.pickingMode = PickingMode.Ignore;
            left.Add(Ui.Spacer());
            selectedName = Ui.Heading(string.Empty);
            selectedName.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
            left.Add(selectedName);
            action = Ui.Button("EQUIP", OnAction, "kr-btn--good");
            action.style.alignSelf = Align.Center;
            left.Add(action);
            body.Add(left);

            var right = Ui.Panel("kr-locker-panel");
            var tabs = Ui.Row();
            foreach (var slot in Tabs)
            {
                var s = slot;
                var button = new Button(() => SelectTab(s)) { text = UiData.SlotLabel(slot).ToUpperInvariant() };
                button.AddToClassList("kr-tab");
                button.AddToClassList("kr-display");
                tabs.Add(button);
                tabButtons[slot] = button;
            }

            right.Add(tabs);
            var scroll = Ui.Scroll();
            grid = Ui.Element("kr-wrap");
            scroll.Add(grid);
            right.Add(scroll);
            body.Add(right);
            root.Add(body);
        }

        public override async void OnShow()
        {
            preview = LocalLoadoutStore.Current;
            selected = null;
            SelectTab(tab);
            await App.Run(UiData.EnsurePricesAsync);
            if (IsVisible)
            {
                RebuildGrid();
            }
        }

        public override void OnHide()
        {
            MenuBeanStage.Current?.Preview(LocalLoadoutStore.Current);
        }

        public void SelectTab(CosmeticSlot slot)
        {
            tab = slot;
            foreach (var pair in tabButtons)
            {
                pair.Value.EnableInClassList("kr-tab--active", pair.Key == slot);
            }

            var catalog = CosmeticCatalog.Load();
            selected = catalog?.Get(UiData.Equipped(preview, slot));
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            grid.Clear();
            var catalog = CosmeticCatalog.Load();
            if (catalog == null)
            {
                return;
            }

            var equippedId = UiData.Equipped(LocalLoadoutStore.Current, tab);
            foreach (var item in ItemCards.Sorted(catalog, tab))
            {
                var current = item;
                grid.Add(ItemCards.Create(item, UiData.Owns(item.ItemId), item.ItemId == equippedId, selected == item, () => Select(current)));
            }

            UpdateAction();
        }

        private void Select(CosmeticDefinition item)
        {
            selected = item;
            preview = UiData.With(preview, item.Slot, item.ItemId);
            var stage = MenuBeanStage.Current;
            stage?.Preview(preview);
            if (item.Slot == CosmeticSlot.Emote)
            {
                stage?.PlayEmote(item.ItemId);
            }

            RebuildGrid();
        }

        private void UpdateAction()
        {
            if (selected == null)
            {
                selectedName.text = string.Empty;
                Ui.SetVisible(action, false);
                return;
            }

            selectedName.text = selected.DisplayName.ToUpperInvariant();
            Ui.SetVisible(action, true);
            var owned = UiData.Owns(selected.ItemId);
            var equipped = UiData.Equipped(LocalLoadoutStore.Current, selected.Slot) == selected.ItemId;
            var price = UiData.Price(selected.ItemId);
            action.SetEnabled(!equipped && (owned || price != null));
            action.text = equipped ? "EQUIPPED" : owned ? "EQUIP" : price != null ? $"BUY · {price.Price} {(price.Currency == "gems" ? "GEMS" : "COINS")}" : "SEASON PASS REWARD";
            action.EnableInClassList("kr-btn--good", owned);
        }

        private async void OnAction()
        {
            if (selected == null)
            {
                return;
            }

            var item = selected;
            if (!UiData.Owns(item.ItemId))
            {
                if (!UiData.Online)
                {
                    App.Toast("Connect to buy items.");
                    return;
                }

                if (!await App.Run(() => UiData.Backend.PurchaseAsync(item.ItemId), $"{item.DisplayName} unlocked!"))
                {
                    return;
                }
            }

            var loadout = UiData.With(LocalLoadoutStore.Current, item.Slot, item.ItemId);
            if (await App.Run(() => UiData.EquipAsync(loadout), $"{item.DisplayName} equipped"))
            {
                preview = UiData.With(preview, item.Slot, item.ItemId);
                RebuildGrid();
            }
        }
    }

    /// <summary>11 · Character Selection: browse the 20 costumes on the stage bean, one at a time.</summary>
    public sealed class CharacterSelectScreen : UIScreen
    {
        private readonly List<CosmeticDefinition> costumes = new List<CosmeticDefinition>();
        private Label name;
        private Label rarity;
        private Label counter;
        private Button select;
        private int index;

        public override bool ShowsStage => true;

        protected override void Build(VisualElement root)
        {
            root.Add(Ui.Header("CHOOSE YOUR BEAN", () => Nav.Pop(), Ui.Button("LOCKER", () => Nav.Push<CustomizeScreen>(), "kr-btn--small", "kr-btn--ghost")));
            var row = Ui.Row("kr-grow");
            row.pickingMode = PickingMode.Ignore;
            row.Add(Ui.Button("<", () => Step(-1), "kr-btn--icon", "kr-arrow"));
            row.Add(Ui.Spacer());
            row.Add(Ui.Button(">", () => Step(1), "kr-btn--icon", "kr-arrow"));
            root.Add(row);

            var info = Ui.Col("kr-center");
            name = Ui.Title(string.Empty);
            info.Add(name);
            rarity = Ui.Label(string.Empty, "kr-display");
            info.Add(rarity);
            counter = Ui.Label(string.Empty, "kr-subtle");
            info.Add(counter);
            select = Ui.Button("SELECT", Choose, "kr-btn--good");
            info.Add(select);
            root.Add(info);
        }

        public override async void OnShow()
        {
            costumes.Clear();
            var catalog = CosmeticCatalog.Load();
            if (catalog != null)
            {
                costumes.AddRange(catalog.InSlot(CosmeticSlot.Body).OrderBy(c => c.Rarity).ThenBy(c => c.DisplayName));
            }

            index = Math.Max(0, costumes.FindIndex(c => c.ItemId == LocalLoadoutStore.Current.Body));
            Show();
            await App.Run(UiData.EnsurePricesAsync);
            if (IsVisible)
            {
                Show();
            }
        }

        public override void OnHide()
        {
            MenuBeanStage.Current?.Preview(LocalLoadoutStore.Current);
        }

        private void Step(int delta)
        {
            if (costumes.Count == 0)
            {
                return;
            }

            index = (index + delta + costumes.Count) % costumes.Count;
            Show();
        }

        private void Show()
        {
            if (costumes.Count == 0)
            {
                return;
            }

            var costume = costumes[index];
            name.text = costume.DisplayName.ToUpperInvariant();
            rarity.text = costume.Rarity.ToString().ToUpperInvariant();
            rarity.style.color = UiData.RarityColor(costume.Rarity);
            counter.text = $"{index + 1} / {costumes.Count}";
            var owned = UiData.Owns(costume.ItemId);
            var equipped = LocalLoadoutStore.Current.Body == costume.ItemId;
            var price = UiData.Price(costume.ItemId);
            select.text = equipped ? "SELECTED" : owned ? "SELECT" : price != null ? $"UNLOCK IN LOCKER · {price.Price} {(price.Currency == "gems" ? "GEMS" : "COINS")}" : "SEASON PASS REWARD";
            select.SetEnabled(!equipped && (owned || price != null));
            MenuBeanStage.Current?.Preview(UiData.With(LocalLoadoutStore.Current, CosmeticSlot.Body, costume.ItemId));
        }

        private async void Choose()
        {
            var costume = costumes[index];
            if (!UiData.Owns(costume.ItemId))
            {
                var locker = Nav.Push<CustomizeScreen>();
                locker.SelectTab(CosmeticSlot.Body);
                return;
            }

            if (await App.Run(() => UiData.EquipAsync(UiData.With(LocalLoadoutStore.Current, CosmeticSlot.Body, costume.ItemId)), $"{costume.DisplayName} selected!"))
            {
                Show();
            }
        }
    }

    /// <summary>13 · Inventory: everything the player owns, by category, with collection progress.</summary>
    public sealed class InventoryScreen : UIScreen
    {
        private VisualElement content;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop");
            root.Add(Ui.Header("INVENTORY", () => Nav.Pop(), Ui.Button("OPEN LOCKER", () => Nav.Push<CustomizeScreen>(), "kr-btn--small")));
            content = Ui.Scroll();
            root.Add(content);
        }

        public override void OnShow()
        {
            content.Clear();
            var catalog = CosmeticCatalog.Load();
            if (catalog == null)
            {
                return;
            }

            if (!UiData.Online)
            {
                content.Add(Ui.Label("Offline: showing items saved on this device.", "kr-subtle"));
            }

            var loadout = LocalLoadoutStore.Current;
            foreach (CosmeticSlot slot in Enum.GetValues(typeof(CosmeticSlot)))
            {
                var all = catalog.InSlot(slot).ToList();
                var owned = all.Where(i => UiData.Owns(i.ItemId)).OrderBy(i => i.Rarity).ToList();
                content.Add(Ui.Label($"{UiData.SlotLabel(slot).ToUpperInvariant()}  {owned.Count}/{all.Count}", "kr-section__title", "kr-display"));
                var grid = Ui.Element("kr-wrap");
                foreach (var item in owned)
                {
                    grid.Add(ItemCards.Create(item, true, UiData.Equipped(loadout, slot) == item.ItemId, false, () =>
                    {
                        var locker = Nav.Push<CustomizeScreen>();
                        locker.SelectTab(item.Slot);
                    }));
                }

                content.Add(grid);
            }
        }
    }
}
