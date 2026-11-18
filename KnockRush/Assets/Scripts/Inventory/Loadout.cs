using System;
using System.Collections.Generic;

namespace KnockRush.Inventory
{
    /// <summary>The five equipped cosmetic slots (item ids from the backend catalogue).</summary>
    [Serializable]
    public struct Loadout : IEquatable<Loadout>
    {
        public string Body;
        public string Color;
        public string Pattern;
        public string Hat;
        public string Emote;

        public static Loadout Default => new Loadout
        {
            Body = "default_bean",
            Color = "default_orange",
            Pattern = "pattern_none",
            Hat = "hat_none",
            Emote = "emote_celebrate"
        };

        /// <summary>Builds a loadout from the backend profile's "equipped" map (missing slots use defaults).</summary>
        public static Loadout FromEquipped(IReadOnlyDictionary<string, string> equipped)
        {
            var loadout = Default;
            if (equipped == null)
            {
                return loadout;
            }

            if (equipped.TryGetValue("body", out var body)) loadout.Body = body;
            if (equipped.TryGetValue("color", out var color)) loadout.Color = color;
            if (equipped.TryGetValue("pattern", out var pattern)) loadout.Pattern = pattern;
            if (equipped.TryGetValue("hat", out var hat)) loadout.Hat = hat;
            if (equipped.TryGetValue("emote", out var emote)) loadout.Emote = emote;
            return loadout;
        }

        public Dictionary<string, string> ToEquipped()
        {
            return new Dictionary<string, string>
            {
                ["body"] = Body, ["color"] = Color, ["pattern"] = Pattern, ["hat"] = Hat, ["emote"] = Emote
            };
        }

        public bool Equals(Loadout other) =>
            Body == other.Body && Color == other.Color && Pattern == other.Pattern && Hat == other.Hat && Emote == other.Emote;

        public override bool Equals(object obj) => obj is Loadout other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Body, Color, Pattern, Hat, Emote);
    }

    /// <summary>Compact network form: catalogue indices (−1 = default).</summary>
    public struct LoadoutIndices
    {
        public short Body;
        public short Color;
        public short Pattern;
        public short Hat;
        public short Emote;

        public static LoadoutIndices From(Loadout loadout, CosmeticCatalog catalog)
        {
            return new LoadoutIndices
            {
                Body = (short)catalog.IndexOf(loadout.Body),
                Color = (short)catalog.IndexOf(loadout.Color),
                Pattern = (short)catalog.IndexOf(loadout.Pattern),
                Hat = (short)catalog.IndexOf(loadout.Hat),
                Emote = (short)catalog.IndexOf(loadout.Emote)
            };
        }

        public Loadout ToLoadout(CosmeticCatalog catalog)
        {
            var fallback = Loadout.Default;
            string Id(short index, string defaultId) => catalog.Get(index)?.ItemId ?? defaultId;
            return new Loadout
            {
                Body = Id(Body, fallback.Body),
                Color = Id(Color, fallback.Color),
                Pattern = Id(Pattern, fallback.Pattern),
                Hat = Id(Hat, fallback.Hat),
                Emote = Id(Emote, fallback.Emote)
            };
        }
    }
}
