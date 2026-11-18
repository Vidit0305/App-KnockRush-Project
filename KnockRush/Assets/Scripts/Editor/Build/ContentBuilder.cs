using System;
using KnockRush.EditorTools.Characters;
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
            var movement = CharacterAssetBuilder.EnsureMovementSettings();
            var cameraSettings = CharacterAssetBuilder.EnsureCameraSettings();
            var controller = CharacterAssetBuilder.BuildAnimatorController();
            var player = CharacterAssetBuilder.BuildPlayerPrefab(movement, controller);
            var controls = MobileControlsBuilder.Build();
            PlaygroundSceneBuilder.Build(player, controls, cameraSettings);
            AssetDatabase.SaveAssets();
            Debug.Log("[KnockRush Build] Phase 2 content build complete.");
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
