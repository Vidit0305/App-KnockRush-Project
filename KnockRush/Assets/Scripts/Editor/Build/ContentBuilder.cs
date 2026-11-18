using System;
using KnockRush.EditorTools.Characters;
using KnockRush.EditorTools.GameModes;
using KnockRush.EditorTools.Maps;
using KnockRush.EditorTools.Multiplayer;
using KnockRush.EditorTools.Obstacles;
using KnockRush.EditorTools.Scenes;
using KnockRush.EditorTools.Setup;
using KnockRush.EditorTools.UI;
using UnityEditor;
using UnityEngine;

namespace KnockRush.EditorTools.Build
{
    /// <summary>
    /// Regenerates all generated content (Phase 2: character + Playground; later phases add maps): settings assets, toon material, animator controller,
    /// PlayerCharacter prefab, mobile controls prefab and the Playground scene.
    ///
    /// Command line:
    ///   Unity.exe -batchmode -projectPath KnockRush
    ///     -executeMethod KnockRush.EditorTools.Build.ContentBuilder.BuildBatch -logFile phase2.log
    /// </summary>
    public static class ContentBuilder
    {
        [MenuItem("KnockRush/Build/All Generated Content", priority = 200)]
        public static void BuildFromMenu()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildAll();
            EditorUtility.DisplayDialog("KnockRush", "Phase 2 content built. Open Assets/Scenes/Dev/Playground.unity and press Play.", "OK");
        }

        public static void BuildAll()
        {
            KnockRushProjectSetup.EnsureLayers();

            // Phase 8: cosmetic visuals (needed by the player prefabs below).
            KnockRush.EditorTools.Cosmetics.CosmeticContentBuilder.Build();

            // Addressables settings (maps/cosmetics are delivered through Addressables from Phase 6;
            // Fusion also probes Addressables at runtime, which needs a settings object to exist).
            UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(true);

            var movement = CharacterAssetBuilder.EnsureMovementSettings();
            var cameraSettings = CharacterAssetBuilder.EnsureCameraSettings();
            var controller = CharacterAssetBuilder.BuildAnimatorController();
            var player = CharacterAssetBuilder.BuildPlayerPrefab(movement, controller);
            var controls = MobileControlsBuilder.Build();
            PlaygroundSceneBuilder.Build(player, controls, cameraSettings);

            // Phase 3: obstacle library, race HUD, first playable map.
            ObstaclePrefabBuilder.BuildAll();
            HudBuilder.BuildRaceHud();

            // Phase 4: networked player + settings, offline/online rigs, maps use MapBootstrap.
            NetworkContentBuilder.Build(movement);
            var rigs = RigBuilder.BuildAll(cameraSettings);
            HammerRushBuilder.Build(rigs.offline, rigs.online);

            // Phase 6: the other seven maps.
            AdditionalMapsBuilder.BuildAll();

            // Phase 5: game modes, catalog (modes + map pool), MatchController prefab.
            GameModeContentBuilder.Build();

            // Phase 7: backend connection settings (development = Firebase Emulator Suite).
            KnockRush.EditorTools.Common.EditorAssetUtility.LoadOrCreate(
                "Assets/ScriptableObjects/Config/Resources/BackendConfig.asset",
                ScriptableObject.CreateInstance<KnockRush.Backend.BackendConfig>);
            AssetDatabase.SaveAssets();
            Debug.Log("[KnockRush Build] Content build complete.");
        }

        public static void BuildBatch()
        {
            var exitCode = 0;
            try
            {
                BuildAll();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }
    }
}
