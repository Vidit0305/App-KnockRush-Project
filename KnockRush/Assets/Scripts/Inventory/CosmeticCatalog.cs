using System.Collections.Generic;
using UnityEngine;

namespace KnockRush.Inventory
{
    /// <summary>
    /// All cosmetic visuals (Resources/CosmeticCatalog.asset). The list order is the network index
    /// used to replicate loadouts (same build on every peer).
    /// </summary>
    [CreateAssetMenu(menuName = "KnockRush/Cosmetics/Catalog", fileName = "CosmeticCatalog", order = 1)]
    public sealed class CosmeticCatalog : ScriptableObject
    {
        public const string ResourcesPath = "CosmeticCatalog";

        [SerializeField] private CosmeticDefinition[] items = new CosmeticDefinition[0];

        private Dictionary<string, int> indexById;
        private static CosmeticCatalog cached;

        public IReadOnlyList<CosmeticDefinition> Items => items;

        public static CosmeticCatalog Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<CosmeticCatalog>(ResourcesPath);
            }

            return cached;
        }

        public void Configure(CosmeticDefinition[] definitions)
        {
            items = definitions;
            indexById = null;
        }

        public int IndexOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return -1;
            }

            if (indexById == null)
            {
                indexById = new Dictionary<string, int>();
                for (var i = 0; i < items.Length; i++)
                {
                    if (items[i] != null)
                    {
                        indexById[items[i].ItemId] = i;
                    }
                }
            }

            return indexById.TryGetValue(itemId, out var index) ? index : -1;
        }

        public CosmeticDefinition Get(string itemId)
        {
            var index = IndexOf(itemId);
            return index >= 0 ? items[index] : null;
        }

        public CosmeticDefinition Get(int index)
        {
            return index >= 0 && index < items.Length ? items[index] : null;
        }

        public IEnumerable<CosmeticDefinition> InSlot(CosmeticSlot slot)
        {
            foreach (var item in items)
            {
                if (item != null && item.Slot == slot)
                {
                    yield return item;
                }
            }
        }
    }
}
