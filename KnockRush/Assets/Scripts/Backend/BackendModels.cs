using System.Collections.Generic;
using Newtonsoft.Json;

namespace KnockRush.Backend
{
    /// <summary>Player profile as returned by getProfile / ensureProfile (read-only on the client).</summary>
    public sealed class PlayerProfile
    {
        [JsonProperty("uid")] public string Uid;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("username")] public string Username;
        [JsonProperty("coins")] public int Coins;
        [JsonProperty("gems")] public int Gems;
        [JsonProperty("xp")] public int Xp;
        [JsonProperty("level")] public int Level;
        [JsonProperty("xpForNextLevel")] public int XpForNextLevel;
        [JsonProperty("equipped")] public Dictionary<string, string> Equipped = new Dictionary<string, string>();
        [JsonProperty("stats")] public PlayerStats Stats = new PlayerStats();
        [JsonProperty("inventory")] public List<string> Inventory = new List<string>();
        [JsonProperty("achievements")] public List<string> Achievements = new List<string>();
        [JsonProperty("dailyStreak")] public int DailyStreak;
        [JsonProperty("dailyClaimable")] public bool DailyClaimable;

        public bool Owns(string itemId) => Inventory.Contains(itemId);
    }

    public sealed class PlayerStats
    {
        [JsonProperty("matches")] public int Matches;
        [JsonProperty("wins")] public int Wins;
        [JsonProperty("roundsCleared")] public int RoundsCleared;
        [JsonProperty("crowns")] public int Crowns;
    }

    public sealed class CatalogEntry
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("slot")] public string Slot;
        [JsonProperty("rarity")] public string Rarity;
        [JsonProperty("price")] public int Price;
        [JsonProperty("currency")] public string Currency;
    }

    public sealed class CatalogResponse
    {
        [JsonProperty("items")] public List<CatalogEntry> Items = new List<CatalogEntry>();
    }

    public sealed class DailyRewardResult
    {
        [JsonProperty("streak")] public int Streak;
        [JsonProperty("coins")] public int Coins;
        [JsonProperty("gems")] public int Gems;
    }

    public sealed class LeaderboardEntry
    {
        [JsonProperty("rank")] public int Rank;
        [JsonProperty("uid")] public string Uid;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("wins")] public int Wins;
        [JsonProperty("crowns")] public int Crowns;
        [JsonProperty("level")] public int Level;
    }

    public sealed class LeaderboardResponse
    {
        [JsonProperty("entries")] public List<LeaderboardEntry> Entries = new List<LeaderboardEntry>();
    }

    /// <summary>Result of one match for one participant (dedicated server → reportMatchResult).</summary>
    public sealed class MatchReportPlayer
    {
        [JsonProperty("uid", NullValueHandling = NullValueHandling.Ignore)] public string Uid;
        [JsonProperty("isBot")] public bool IsBot;
        [JsonProperty("placement")] public int Placement;
        [JsonProperty("roundsCleared")] public int RoundsCleared;
        [JsonProperty("won")] public bool Won;
    }

    public sealed class MatchReport
    {
        [JsonProperty("matchId")] public string MatchId;
        [JsonProperty("modeId")] public string ModeId;
        [JsonProperty("players")] public List<MatchReportPlayer> Players = new List<MatchReportPlayer>();
    }
}
