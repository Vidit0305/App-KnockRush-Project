using System.Collections.Generic;
using Newtonsoft.Json;

namespace KnockRush.Backend
{
    /// <summary>One season-pass tier reward (coins, gems or a cosmetic item).</summary>
    public sealed class TierReward
    {
        [JsonProperty("coins")] public int Coins;
        [JsonProperty("gems")] public int Gems;
        [JsonProperty("itemId")] public string ItemId;
    }

    public sealed class SeasonInfo
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("tiers")] public int Tiers;
        [JsonProperty("xpPerTier")] public int XpPerTier;
        [JsonProperty("premiumPriceGems")] public int PremiumPriceGems;
        [JsonProperty("endsAt")] public string EndsAt;
    }

    public sealed class SeasonPlayerState
    {
        [JsonProperty("seasonXp")] public int SeasonXp;
        [JsonProperty("tier")] public int Tier;
        [JsonProperty("premium")] public bool Premium;
        [JsonProperty("claimedFree")] public List<int> ClaimedFree = new List<int>();
        [JsonProperty("claimedPremium")] public List<int> ClaimedPremium = new List<int>();
    }

    public sealed class SeasonResponse
    {
        [JsonProperty("season")] public SeasonInfo Season = new SeasonInfo();
        [JsonProperty("freeTrack")] public List<TierReward> FreeTrack = new List<TierReward>();
        [JsonProperty("premiumTrack")] public List<TierReward> PremiumTrack = new List<TierReward>();
        [JsonProperty("player")] public SeasonPlayerState Player = new SeasonPlayerState();
    }

    public sealed class Mission
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("title")] public string Title;
        [JsonProperty("stat")] public string Stat;
        [JsonProperty("target")] public int Target;
        [JsonProperty("coins")] public int Coins;
        [JsonProperty("gems")] public int Gems;
        [JsonProperty("progress")] public int Progress;
        [JsonProperty("claimed")] public bool Claimed;

        public bool Complete => Progress >= Target;
    }

    public sealed class MissionsResponse
    {
        [JsonProperty("day")] public int Day;
        [JsonProperty("missions")] public List<Mission> Missions = new List<Mission>();
    }

    public sealed class ShopBundle
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("items")] public List<string> Items = new List<string>();
        [JsonProperty("price")] public int Price;
        [JsonProperty("currency")] public string Currency;
    }

    public sealed class ShopResponse
    {
        [JsonProperty("day")] public int Day;
        [JsonProperty("resetsInSeconds")] public int ResetsInSeconds;
        [JsonProperty("featured")] public List<CatalogEntry> Featured = new List<CatalogEntry>();
        [JsonProperty("bundles")] public List<ShopBundle> Bundles = new List<ShopBundle>();
    }

    public sealed class PurchaseValidation
    {
        [JsonProperty("duplicate")] public bool Duplicate;
        [JsonProperty("gems")] public int Gems;
    }

    public sealed class FriendEntry
    {
        [JsonProperty("uid")] public string Uid;
        [JsonProperty("displayName")] public string DisplayName;
    }

    public sealed class FriendsResponse
    {
        [JsonProperty("friends")] public List<FriendEntry> Friends = new List<FriendEntry>();
        [JsonProperty("requests")] public List<FriendEntry> Requests = new List<FriendEntry>();
    }

    public sealed class AchievementEntry
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("title")] public string Title;
        [JsonProperty("description")] public string Description;
        [JsonProperty("target")] public int Target;
        [JsonProperty("progress")] public int Progress;
        [JsonProperty("unlocked")] public bool Unlocked;
    }

    public sealed class AchievementsResponse
    {
        [JsonProperty("achievements")] public List<AchievementEntry> Achievements = new List<AchievementEntry>();
    }
}
