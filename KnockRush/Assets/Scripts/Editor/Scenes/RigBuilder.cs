using KnockRush.CameraRig;
using KnockRush.EditorTools.Characters;
using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Setup;
using KnockRush.EditorTools.UI;
using KnockRush.Maps;
using KnockRush.Player;
using KnockRush.Player.Controls;
using KnockRush.UI.Placeholders;
using UnityEditor;
using UnityEngine;

namespace KnockRush.EditorTools.Scenes
{
    /// <summary>
    /// Builds the two player "rigs" map scenes spawn through MapBootstrap:
    ///  - OfflineRig: player + follow camera + controls + HUD + dev tools + offline race session
    ///  - OnlineRig : follow camera + controls + HUD (networked characters are spawned by the server)
    /// </summary>
    public static class RigBuilder
    {
        public const string OfflineRigPath = "Assets/Prefabs/Rigs/OfflineRig.prefab";
        public const string OnlineRigPath = "Assets/Prefabs/Rigs/OnlineRig.prefab";

        public static (GameObject offline, GameObject online) BuildAll(CameraSettings cameraSettings)
        {
            KnockRushProjectSetup.EnsureFolder("Assets/Prefabs/Rigs");
            cameraSettings = AssetDatabase.LoadAssetAtPath<CameraSettings>(CharacterAssetBuilder.CameraSettingsPath); // see NetworkContentBuilder
            return (BuildOffline(cameraSettings), BuildOnline(cameraSettings));
        }

        private static GameObject BuildOffline(CameraSettings cameraSettings)
        {
            var root = new GameObject("OfflineRig");
            try
            {
                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetBuilder.PlayerPrefabPath);
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, root.transform);
                player.transform.localPosition = Vector3.zero;
                var character = player.GetComponent<PlayerCharacter>();

                var rig = AddCamera(root.transform, cameraSettings, player.transform);
                EditorAssetUtility.SetField(character, "cameraRig", rig);
                EditorAssetUtility.SetField(player.GetComponent<PlayerInputReader>(), "cameraRig", rig);

                AddUi(root.transform);
                var devTools = new GameObject("DevTools").AddComponent<PlaygroundDevTools>();
                devTools.transform.SetParent(root.transform, false);
                EditorAssetUtility.SetField(devTools, "character", character);
                new GameObject("RaceSession", typeof(OfflineRaceSession)).transform.SetParent(root.transform, false);

                return PrefabUtility.SaveAsPrefabAsset(root, OfflineRigPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildOnline(CameraSettings cameraSettings)
        {
            var root = new GameObject("OnlineRig");
            try
            {
                AddCamera(root.transform, cameraSettings, null);
                AddUi(root.transform);
                new GameObject("MatchHud", typeof(KnockRush.UI.Hud.MatchHud)).transform.SetParent(root.transform, false);
                return PrefabUtility.SaveAsPrefabAsset(root, OnlineRigPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ThirdPersonCamera AddCamera(Transform parent, CameraSettings cameraSettings, Transform target)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.SetLocalPositionAndRotation(new Vector3(0f, 3f, -6.5f), Quaternion.Euler(16f, 0f, 0f));
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = cameraSettings.FieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            var rig = cameraObject.AddComponent<ThirdPersonCamera>();
            EditorAssetUtility.SetField(rig, "settings", cameraSettings);
            if (target != null)
            {
                EditorAssetUtility.SetField(rig, "target", target);
            }

            return rig;
        }

        private static void AddUi(Transform parent)
        {
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MobileControlsBuilder.PrefabPath), parent);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudBuilder.RaceHudPath), parent);
            MobileControlsBuilder.CreateEventSystem().transform.SetParent(parent, false);
        }
    }
}
