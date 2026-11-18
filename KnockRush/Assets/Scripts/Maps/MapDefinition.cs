using UnityEngine;

namespace KnockRush.Maps
{
    public enum MapCategory
    {
        /// <summary>Reach the finish line; the first N qualify.</summary>
        Race = 0,

        /// <summary>Stay alive until the timer ends or only N remain.</summary>
        Survival = 1,

        /// <summary>Championship round with a single winner.</summary>
        Final = 2
    }

    public enum FallRule
    {
        /// <summary>Falling respawns at the last checkpoint (race maps).</summary>
        RespawnAtCheckpoint = 0,

        /// <summary>Falling eliminates the player (survival and final maps).</summary>
        Eliminate = 1
    }

    /// <summary>
    /// Static description of a map. Game modes (Phase 5–6) pick maps by category and use these
    /// values for round rules, UI and matchmaking.
    /// </summary>
    [CreateAssetMenu(menuName = "KnockRush/Maps/Map Definition", fileName = "MapDefinition", order = 0)]
    public sealed class MapDefinition : ScriptableObject
    {
        [SerializeField] private string mapId = "hammer_rush";
        [SerializeField] private string displayName = "Hammer Rush";
        [SerializeField, TextArea] private string description;
        [SerializeField] private MapCategory category = MapCategory.Race;
        [SerializeField] private FallRule fallRule = FallRule.RespawnAtCheckpoint;
        [Tooltip("Scene name in Build Settings / Addressables.")]
        [SerializeField] private string sceneName = "HammerRush";
        [SerializeField, Range(1, 5)] private int difficulty = 2;
        [SerializeField, Min(1)] private int minPlayers = 2;
        [SerializeField, Range(1, 32)] private int maxPlayers = 32;
        [Tooltip("Round time limit in seconds.")]
        [SerializeField, Min(10f)] private float timeLimit = 180f;
        [SerializeField] private Color themeColor = new Color32(0xFF, 0x6A, 0x1A, 0xFF);

        public string MapId => mapId;
        public string DisplayName => displayName;
        public string Description => description;
        public MapCategory Category => category;
        public FallRule FallRule => fallRule;
        public string SceneName => sceneName;
        public int Difficulty => difficulty;
        public int MinPlayers => minPlayers;
        public int MaxPlayers => maxPlayers;
        public float TimeLimit => timeLimit;
        public Color ThemeColor => themeColor;

        public void Configure(string id, string name, string text, MapCategory mapCategory, FallRule rule,
            string scene, int mapDifficulty, float limit, Color theme)
        {
            mapId = id;
            displayName = name;
            description = text;
            category = mapCategory;
            fallRule = rule;
            sceneName = scene;
            difficulty = mapDifficulty;
            timeLimit = limit;
            themeColor = theme;
        }
    }
}
