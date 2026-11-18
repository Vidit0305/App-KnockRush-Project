using UnityEngine;

namespace KnockRush.Inventory
{
    public enum CosmeticSlot
    {
        Body = 0,
        Color = 1,
        Pattern = 2,
        Hat = 3,
        Emote = 4
    }

    public enum CosmeticRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }

    /// <summary>
    /// Client-side visuals for one cosmetic item. <see cref="ItemId"/> matches the server catalogue
    /// (Backend/firebase/functions/src/catalog.ts), which owns prices and availability.
    /// </summary>
    [CreateAssetMenu(menuName = "KnockRush/Cosmetics/Cosmetic", fileName = "Cosmetic", order = 0)]
    public sealed class CosmeticDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private CosmeticSlot slot;
        [SerializeField] private CosmeticRarity rarity;

        [Header("Color (Color slot, or costume override)")]
        [SerializeField] private Color color = Color.white;
        [SerializeField] private bool overridesBodyColor;

        [Header("Pattern (Pattern slot, or costume override; -1 = none)")]
        [SerializeField, Range(-1, 7)] private int patternType = -1;
        [SerializeField] private Color patternColor = Color.white;

        [Header("Accessory (hats, costume pieces)")]
        [SerializeField] private GameObject accessoryPrefab;
        [Tooltip("Bone the accessory follows (Chest for head/back items, Hips for tails).")]
        [SerializeField] private string attachBone = "Chest";

        [Header("Emote")]
        [SerializeField] private string emoteState;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public CosmeticSlot Slot => slot;
        public CosmeticRarity Rarity => rarity;
        public Color Color => color;
        public bool OverridesBodyColor => overridesBodyColor;
        public int PatternType => patternType;
        public Color PatternColor => patternColor;
        public GameObject AccessoryPrefab => accessoryPrefab;
        public string AttachBone => attachBone;
        public string EmoteState => emoteState;

        public void Configure(string id, string name, CosmeticSlot cosmeticSlot, CosmeticRarity cosmeticRarity)
        {
            itemId = id;
            displayName = name;
            slot = cosmeticSlot;
            rarity = cosmeticRarity;
        }

        public void ConfigureLook(Color bodyColor, bool overrideColor, int pattern, Color patternTint, GameObject accessory, string bone, string emote)
        {
            color = bodyColor;
            overridesBodyColor = overrideColor;
            patternType = pattern;
            patternColor = patternTint;
            accessoryPrefab = accessory;
            attachBone = bone;
            emoteState = emote;
        }
    }
}
