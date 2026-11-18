using System.Collections.Generic;
using System.Threading.Tasks;
using KnockRush.Backend;
using KnockRush.Core.Services;
using KnockRush.Inventory;
using UnityEngine;

namespace KnockRush.UI.Framework
{
    /// <summary>
    /// Read-side helpers over the backend profile for screens. When offline, ownership falls back to
    /// the starter items plus the last inventory seen online (cached), so the locker still works.
    /// </summary>
    public static class UiData
    {
        private const string OwnedCacheKey = "knockrush.ownedCache";
        private const string ModeKey = "knockrush.selectedMode";

        private static readonly string[] StarterItems =
        {
            "default_bean", "default_orange", "pattern_none", "hat_none", "emote_celebrate", "emote_victory"
        };

        private static Dictionary<string, CatalogEntry> prices;

        public static IBackendService Backend => ServiceLocator.TryGet<IBackendService>(out var backend) ? backend : null;

        public static bool Online => Backend != null && Backend.State == BackendState.Online && Backend.Profile != null;

        public static PlayerProfile Profile => Online ? Backend.Profile : null;

        public static string DisplayName => Profile != null ? Profile.DisplayName : "Player";

        /// <summary>The game mode picked on the mode-selection screen ("classic", "quickrace", ...).</summary>
        public static string SelectedModeId
        {
            get => PlayerPrefs.GetString(ModeKey, "classic");
            set => PlayerPrefs.SetString(ModeKey, value);
        }

        /// <summary>Code of the private room the player created or joined (shown in the lobby).</summary>
        public static string RoomCode { get; set; }

        /// <summary>Play against bots on this device instead of matchmaking online.</summary>
        public static bool PracticeMode
        {
            get => PlayerPrefs.GetInt(ModeKey + ".practice", 0) == 1;
            set => PlayerPrefs.SetInt(ModeKey + ".practice", value ? 1 : 0);
        }

        public static bool Owns(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            if (System.Array.IndexOf(StarterItems, itemId) >= 0)
            {
                return true;
            }

            if (Profile != null)
            {
                CacheOwned(Profile.Inventory);
                return Profile.Owns(itemId);
            }

            return PlayerPrefs.GetString(OwnedCacheKey, string.Empty).Split(',').Contains(itemId);
        }

        private static bool Contains(this string[] values, string value) => System.Array.IndexOf(values, value) >= 0;

        private static void CacheOwned(List<string> inventory)
        {
            if (inventory != null)
            {
                PlayerPrefs.SetString(OwnedCacheKey, string.Join(",", inventory));
            }
        }

        /// <summary>Shop price for an item (loaded once per session from the backend catalogue).</summary>
        public static CatalogEntry Price(string itemId)
        {
            return prices != null && itemId != null && prices.TryGetValue(itemId, out var entry) ? entry : null;
        }

        public static async Task EnsurePricesAsync()
        {
            if (prices != null || !Online)
            {
                return;
            }

            var entries = await Backend.GetCatalogAsync();
            var map = new Dictionary<string, CatalogEntry>();
            foreach (var entry in entries)
            {
                map[entry.Id] = entry;
            }

            prices = map;
        }

        public static string RarityClass(CosmeticRarity rarity) => rarity switch
        {
            CosmeticRarity.Rare => "kr-rarity-rare",
            CosmeticRarity.Epic => "kr-rarity-epic",
            CosmeticRarity.Legendary => "kr-rarity-legendary",
            _ => "kr-rarity-common"
        };

        public static Color RarityColor(CosmeticRarity rarity) => rarity switch
        {
            CosmeticRarity.Rare => new Color32(45, 156, 255, 255),
            CosmeticRarity.Epic => new Color32(181, 94, 255, 255),
            CosmeticRarity.Legendary => new Color32(255, 209, 43, 255),
            _ => new Color32(180, 190, 210, 255)
        };

        /// <summary>Swatch colour that best represents an item in a grid.</summary>
        public static Color PreviewColor(CosmeticDefinition item)
        {
            switch (item.Slot)
            {
                case CosmeticSlot.Color:
                    return item.Color;
                case CosmeticSlot.Pattern:
                    return item.PatternType > 0 ? item.PatternColor : new Color(0.9f, 0.9f, 0.95f);
                case CosmeticSlot.Body:
                    return item.OverridesBodyColor ? item.Color : new Color32(255, 106, 26, 255);
                default:
                    return RarityColor(item.Rarity);
            }
        }

        public static string SlotLabel(CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Body => "Costumes",
            CosmeticSlot.Color => "Colours",
            CosmeticSlot.Pattern => "Patterns",
            CosmeticSlot.Hat => "Hats",
            _ => "Emotes"
        };

        public static string SlotKey(CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Body => "body",
            CosmeticSlot.Color => "color",
            CosmeticSlot.Pattern => "pattern",
            CosmeticSlot.Hat => "hat",
            _ => "emote"
        };

        public static string Equipped(Loadout loadout, CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Body => loadout.Body,
            CosmeticSlot.Color => loadout.Color,
            CosmeticSlot.Pattern => loadout.Pattern,
            CosmeticSlot.Hat => loadout.Hat,
            _ => loadout.Emote
        };

        public static Loadout With(Loadout loadout, CosmeticSlot slot, string itemId)
        {
            switch (slot)
            {
                case CosmeticSlot.Body: loadout.Body = itemId; break;
                case CosmeticSlot.Color: loadout.Color = itemId; break;
                case CosmeticSlot.Pattern: loadout.Pattern = itemId; break;
                case CosmeticSlot.Hat: loadout.Hat = itemId; break;
                default: loadout.Emote = itemId; break;
            }

            return loadout;
        }

        /// <summary>Equips a loadout locally at once and on the backend when online.</summary>
        public static async Task EquipAsync(Loadout loadout)
        {
            LocalLoadoutStore.Set(loadout);
            if (Online)
            {
                await Backend.EquipAsync(loadout.ToEquipped());
            }
        }
    }
}
