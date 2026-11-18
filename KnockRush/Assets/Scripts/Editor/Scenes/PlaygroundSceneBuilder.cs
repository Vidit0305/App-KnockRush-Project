using System.Collections.Generic;
using System.Linq;
using KnockRush.CameraRig;
using KnockRush.Core.SceneManagement;
using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Setup;
using KnockRush.EditorTools.UI;
using KnockRush.Player;
using KnockRush.Player.Controls;
using KnockRush.UI.Placeholders;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace KnockRush.EditorTools.Scenes
{
    /// <summary>
    /// Builds Assets/Scenes/Dev/Playground.unity: a movement test course with ramps (walkable and
    /// too steep), small steps, jump platforms, dive gaps, walls, pillars and a ledge to fall off.
    /// </summary>
    public static class PlaygroundSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Dev/Playground.unity";
        private const string MaterialFolder = "Assets/Materials/Dev";

        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -24f);

        public static void Build(GameObject playerPrefab, GameObject mobileControlsPrefab, CameraSettings cameraSettings)
        {
            KnockRushProjectSetup.EnsureFolder("Assets/Scenes/Dev");
            KnockRushProjectSetup.EnsureFolder(MaterialFolder);

            var floor = EditorAssetUtility.CreateToonMaterial($"{MaterialFolder}/Dev_Floor.mat", new Color32(0x8E, 0xD3, 0xF5, 0xFF), false);
            var ramp = EditorAssetUtility.CreateToonMaterial($"{MaterialFolder}/Dev_Ramp.mat", new Color32(0xFF, 0xD1, 0x2B, 0xFF), false);
            var steep = EditorAssetUtility.CreateToonMaterial($"{MaterialFolder}/Dev_Steep.mat", new Color32(0xE8, 0x3A, 0x3A, 0xFF), false);
            var platform = EditorAssetUtility.CreateToonMaterial($"{MaterialFolder}/Dev_Platform.mat", new Color32(0xFF, 0x4F, 0x9A, 0xFF), false);
            var wall = EditorAssetUtility.CreateToonMaterial($"{MaterialFolder}/Dev_Wall.mat", new Color32(0x7A, 0x5C, 0xFF, 0xFF), false);
            var accent = EditorAssetUtility.CreateToonMaterial($"{MaterialFolder}/Dev_Accent.mat", new Color32(0x22, 0xC5, 0x8B, 0xFF), false);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ConfigureLighting();

            var level = new GameObject("Level").transform;

            // Main floor: top surface at y = 0, x -20..20, z -30..30.
            Block(level, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 60f), floor);

            // Ramps (walkable 15° / 30°, too steep 60°).
            Ramp(level, "Ramp15", new Vector3(-12f, 0f, -16f), 4f, 10f, 15f, ramp);
            Block(level, "Ramp15_Top", new Vector3(-12f, 10f * Mathf.Sin(15f * Mathf.Deg2Rad) - 0.25f, -16f + 10f * Mathf.Cos(15f * Mathf.Deg2Rad) + 3f), new Vector3(4f, 0.5f, 6f), platform);
            Ramp(level, "Ramp30", new Vector3(-6f, 0f, -16f), 3f, 7f, 30f, ramp);
            Ramp(level, "Ramp60_TooSteep", new Vector3(-17.5f, 0f, -16f), 3f, 5f, 60f, steep);

            // Small steps (0.12 m) the capsule should walk up.
            for (var i = 0; i < 6; i++)
            {
                var height = 0.12f * (i + 1);
                Block(level, $"Step_{i}", new Vector3(0f, height * 0.5f, -16f + 0.6f * i + 0.3f), new Vector3(4f, height, 0.6f), accent);
            }

            Block(level, "Step_Top", new Vector3(0f, 0.36f, -10.5f), new Vector3(4f, 0.72f, 3f), accent);

            // Jump platforms (1.0 m / 1.4 m / 1.8 m).
            Block(level, "Jump_1.0", new Vector3(7f, 0.5f, -15f), new Vector3(3f, 1f, 3f), platform);
            Block(level, "Jump_1.4", new Vector3(7f, 0.7f, -11f), new Vector3(3f, 1.4f, 3f), platform);
            Block(level, "Jump_1.8", new Vector3(7f, 0.9f, -7f), new Vector3(3f, 1.8f, 3f), platform);

            // Dive gaps: 3 m (jump) and 5 m (jump + dive) between 1 m high platforms.
            Block(level, "Gap_A", new Vector3(15f, 0.5f, -14f), new Vector3(5f, 1f, 6f), platform);
            Block(level, "Gap_B", new Vector3(15f, 0.5f, -5f), new Vector3(5f, 1f, 6f), platform);
            Block(level, "Gap_C", new Vector3(15f, 0.5f, 6f), new Vector3(5f, 1f, 6f), platform);

            // Wall and pillars.
            Block(level, "Wall", new Vector3(0f, 1.5f, 2f), new Vector3(8f, 3f, 0.5f), wall);
            for (var i = 0; i < 5; i++)
            {
                Block(level, $"Pillar_{i}", new Vector3(-8f + i * 4f, 1.5f, 12f), new Vector3(1f, 3f, 1f), wall);
            }

            // Island past the end of the floor (fall off the edge to test respawn).
            Block(level, "Island", new Vector3(0f, -0.5f, 36f), new Vector3(8f, 1f, 6f), accent);

            var spawn = new GameObject("SpawnPoint").transform;
            spawn.SetPositionAndRotation(SpawnPosition, Quaternion.identity);

            // Player.
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);
            var character = player.GetComponent<PlayerCharacter>();
            var reader = player.GetComponent<PlayerInputReader>();

            // Camera.
            var cameraObject = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            var cameraRig = cameraObject.AddComponent<ThirdPersonCamera>();
            EditorAssetUtility.SetField(cameraRig, "settings", cameraSettings);
            EditorAssetUtility.SetField(cameraRig, "target", player.transform);
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = cameraSettings.FieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;
            cameraObject.transform.SetPositionAndRotation(SpawnPosition + new Vector3(0f, 3f, -6.5f), Quaternion.Euler(16f, 0f, 0f));

            EditorAssetUtility.SetField(character, "cameraRig", cameraRig);
            EditorAssetUtility.SetField(reader, "cameraRig", cameraRig);

            // Mobile controls + event system.
            PrefabUtility.InstantiatePrefab(mobileControlsPrefab);
            MobileControlsBuilder.CreateEventSystem();

            var devTools = new GameObject("PlaygroundDevTools").AddComponent<PlaygroundDevTools>();
            EditorAssetUtility.SetField(devTools, "character", character);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[KnockRush Build] Playground scene saved: {ScenePath}");
        }

        private static void ConfigureLighting()
        {
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1.2f;
                light.color = new Color(1f, 0.96f, 0.9f);
                light.shadows = LightShadows.Soft;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.82f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.6f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.3f, 0.42f);
        }

        private static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = center;
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            block.isStatic = true;
            return block;
        }

        /// <summary>Creates a ramp whose top surface starts at <paramref name="bottomFront"/> and rises along +Z.</summary>
        private static void Ramp(Transform parent, string name, Vector3 bottomFront, float width, float length, float angle, Material material)
        {
            const float thickness = 0.5f;
            var rotation = Quaternion.Euler(-angle, 0f, 0f);
            var center = bottomFront + rotation * new Vector3(0f, -thickness * 0.5f, length * 0.5f);
            var ramp = Block(parent, name, center, new Vector3(width, thickness, length), material);
            ramp.transform.localRotation = rotation;
        }

        public static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(scene => scene.path == path))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
