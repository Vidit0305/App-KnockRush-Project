using System.Linq;
using Fusion;
using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Multiplayer;
using KnockRush.EditorTools.Setup;
using KnockRush.GameModes;
using KnockRush.Maps;
using KnockRush.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace KnockRush.EditorTools.GameModes
{
    /// <summary>Builds the game modes, the catalog (modes + map pool) and the MatchController prefab.</summary>
    public static class GameModeContentBuilder
    {
        public const string Folder = "Assets/ScriptableObjects/GameModes";
        public const string CatalogPath = Folder + "/Resources/GameModeCatalog.asset";
        public const string MatchControllerPath = "Assets/Prefabs/Network/MatchController.prefab";

        public static void Build()
        {
            KnockRushProjectSetup.EnsureFolder(Folder + "/Resources");

            var classic = Mode("Classic", "classic", "Classic Knockout",
                "Up to 32 players battle through four rounds of races and survival challenges. Only one is crowned champion.",
                2, 32, true, 20f, new[]
                {
                    new RoundRule(MapCategory.Race, 0.625f, 1, false),     // 32 → 20
                    new RoundRule(MapCategory.Survival, 0.5f, 1, false),   // 20 → 10
                    new RoundRule(MapCategory.Survival, 0.5f, 1, false),   // 10 → 5 (elimination challenge)
                    new RoundRule(MapCategory.Final, 1f, 1, true)          // 5 → 1
                });

            var quickRace = Mode("QuickRace", "quickrace", "Quick Race",
                "One frantic race for 16–32 players. Finish in the top half to win.",
                2, 24, true, 15f, new[]
                {
                    new RoundRule(MapCategory.Race, 0.5f, 1, false)
                });

            var party = Mode("PrivateParty", "private", "Private Party",
                "Your room, your rules: invite friends with a code and start when everyone is ready.",
                1, 32, false, 600f, new[]
                {
                    new RoundRule(MapCategory.Race, 0.625f, 1, false),
                    new RoundRule(MapCategory.Survival, 0.5f, 1, false),
                    new RoundRule(MapCategory.Final, 1f, 1, true)
                });

            var maps = AssetDatabase.FindAssets("t:" + nameof(MapDefinition))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MapDefinition>)
                .Where(m => m != null)
                .OrderBy(m => m.MapId)
                .ToArray();

            var catalog = EditorAssetUtility.LoadOrCreate(CatalogPath, ScriptableObject.CreateInstance<GameModeCatalog>);
            catalog.Configure(new[] { classic, quickRace, party }, maps);
            EditorUtility.SetDirty(catalog);

            var prefab = BuildMatchControllerPrefab();
            var settings = AssetDatabase.LoadAssetAtPath<NetworkSettings>(NetworkContentBuilder.NetworkSettingsPath);
            settings.ConfigureMatchController(prefab.GetComponent<NetworkObject>());
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[KnockRush Build] Game modes: 3, maps in pool: {maps.Length} ({string.Join(", ", maps.Select(m => m.MapId))})");
        }

        private static GameModeDefinition Mode(string file, string id, string name, string description, int min, int target,
            bool bots, float lobby, RoundRule[] rounds)
        {
            var mode = EditorAssetUtility.LoadOrCreate($"{Folder}/{file}.asset", ScriptableObject.CreateInstance<GameModeDefinition>);
            mode.Configure(id, name, description, min, target, bots, lobby, rounds);
            EditorUtility.SetDirty(mode);
            return mode;
        }

        private static GameObject BuildMatchControllerPrefab()
        {
            KnockRushProjectSetup.EnsureFolder("Assets/Prefabs/Network");
            var root = new GameObject("MatchController");
            try
            {
                root.AddComponent<NetworkObject>();
                root.AddComponent<MatchController>();
                return PrefabUtility.SaveAsPrefabAsset(root, MatchControllerPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
