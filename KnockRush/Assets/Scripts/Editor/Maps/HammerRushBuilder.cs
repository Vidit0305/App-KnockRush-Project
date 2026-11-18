using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Obstacles;
using KnockRush.EditorTools.Scenes;
using KnockRush.EditorTools.Setup;
using KnockRush.Maps;
using KnockRush.Obstacles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static KnockRush.EditorTools.Scenes.SceneBuildKit;

namespace KnockRush.EditorTools.Maps
{
    /// <summary>
    /// Generates HAMMER RUSH (race map 1): start grid → Hammer Alley (swinging hammers) →
    /// Sweeper Bridges → moving-platform gap → narrow bridge with hurdles, hammers and bumper →
    /// ramp past the windmill hammer → finish. About 190 m long; 5 checkpoints.
    /// </summary>
    public static class HammerRushBuilder
    {
        public const string ScenePath = "Assets/Scenes/Maps/HammerRush.unity";
        public const string DefinitionPath = "Assets/ScriptableObjects/Maps/HammerRush.asset";
        private const string MaterialFolder = "Assets/Materials/Maps/HammerRush";
        private const float KillHeight = -10f;

        public static MapDefinition EnsureDefinition()
        {
            var definition = EditorAssetUtility.LoadOrCreate(DefinitionPath, ScriptableObject.CreateInstance<MapDefinition>);
            definition.Configure("hammer_rush", "Hammer Rush",
                "Dodge giant swinging hammers, leap the sweepers and ride the moving platforms to the finish.",
                MapCategory.Race, FallRule.RespawnAtCheckpoint, "HammerRush", 2, 180f, new Color32(0xFF, 0xB2, 0x3F, 0xFF));
            EditorUtility.SetDirty(definition);
            return definition;
        }

        public static void Build(GameObject offlineRig, GameObject onlineRig)
        {
            KnockRushProjectSetup.EnsureFolder("Assets/Scenes/Maps");
            KnockRushProjectSetup.EnsureFolder(MaterialFolder);
            var definition = EnsureDefinition();

            Material M(string name, Color32 color) => EditorAssetUtility.CreateToonMaterial($"{MaterialFolder}/HR_{name}.mat", color, false);
            var path = M("Path", new Color32(0xFF, 0xB2, 0x3F, 0xFF));
            var edge = M("Edge", new Color32(0xFF, 0xF4, 0xE0, 0xFF));
            var start = M("Start", new Color32(0x5B, 0x2E, 0xFF, 0xFF));
            var finish = M("Finish", new Color32(0x22, 0xC5, 0x8B, 0xFF));
            var arch = M("Arch", new Color32(0x2D, 0x9C, 0xFF, 0xFF));
            var islandTop = M("IslandTop", new Color32(0x8B, 0xE0, 0x3C, 0xFF));
            var islandRock = M("IslandRock", new Color32(0xA8, 0x83, 0x62, 0xFF));
            var cloud = M("Cloud", new Color32(0xFF, 0xFF, 0xFF, 0xFF));
            var sea = M("Sea", new Color32(0x4F, 0xB4, 0xFF, 0xFF));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Object.DestroyImmediate(Camera.main.gameObject); // cameras come from the rig (none on servers)
            ConfigureLighting(Quaternion.Euler(48f, -35f, 0f), new Color(0.74f, 0.84f, 1f), new Color(0.66f, 0.62f, 0.74f), new Color(0.38f, 0.32f, 0.44f));

            var level = new GameObject("Level").transform;
            var obstacles = new GameObject("Obstacles").transform;
            var gameplay = new GameObject("Gameplay").transform;
            var decor = new GameObject("Decor").transform;

            // ---- Start (z -12..10) ----------------------------------------------------------
            Block(level, "StartPlatform", new Vector3(0f, -0.5f, -1f), new Vector3(26f, 1f, 22f), start);
            Arch(decor, new Vector3(0f, 0f, 9f), 13.5f, 6f, arch, edge);

            // ---- Section 1: Hammer Alley (z 10..50) -------------------------------------------
            Floor(level, "HammerAlley", 0f, 10f, 50f, 12f, path, edge);
            for (var i = 0; i < 4; i++)
            {
                var hammer = PlacePrefab(ObstaclePrefabBuilder.PathOf("SwingingHammer"), obstacles, new Vector3(0f, 0f, 16f + i * 8f), Quaternion.identity, $"Hammer_{i}");
                EditorAssetUtility.SetField(hammer.GetComponentInChildren<PendulumObstacle>(), "phaseOffset", i * 0.7f);
            }

            // ---- Section 2: Sweeper Bridges (z 50..80) ----------------------------------------
            Floor(level, "SweeperField", 0f, 50f, 80f, 16f, path, edge);
            var sweeperA = PlacePrefab(ObstaclePrefabBuilder.PathOf("SweeperBar"), obstacles, new Vector3(0f, 0f, 58f), Quaternion.identity, "Sweeper_A");
            var sweeperB = PlacePrefab(ObstaclePrefabBuilder.PathOf("SweeperBar"), obstacles, new Vector3(0f, 0f, 72f), Quaternion.Euler(0f, 90f, 0f), "Sweeper_B");
            EditorAssetUtility.SetField(sweeperB.GetComponent<RotatingObstacle>(), "degreesPerSecond", -80f);
            EditorAssetUtility.SetField(sweeperA.GetComponent<RotatingObstacle>(), "degreesPerSecond", 95f);

            // ---- Section 3: Moving-platform gap (z 80..102) ------------------------------------
            var platformZ = new[] { 84f, 91f, 98f };
            var platformPeriod = new[] { 4.5f, 4f, 5f };
            for (var i = 0; i < platformZ.Length; i++)
            {
                var platform = PlacePrefab(ObstaclePrefabBuilder.PathOf("MovingPlatform"), obstacles, new Vector3(-4f, 0f, platformZ[i]), Quaternion.identity, $"MovingPlatform_{i}");
                var mover = platform.GetComponent<OscillatingObstacle>();
                EditorAssetUtility.SetField(mover, "period", platformPeriod[i]);
                EditorAssetUtility.SetField(mover, "phaseOffset", i * 1.2f);
            }

            // ---- Section 4: Narrow bridge (z 102..140) ----------------------------------------
            Floor(level, "NarrowBridge", 0f, 102f, 140f, 5f, path, edge);
            PlacePrefab(ObstaclePrefabBuilder.PathOf("Hurdle"), obstacles, new Vector3(0f, 0f, 108f), Quaternion.identity, "Hurdle_A");
            PlacePrefab(ObstaclePrefabBuilder.PathOf("Hurdle"), obstacles, new Vector3(0f, 0f, 127f), Quaternion.identity, "Hurdle_B");
            PlacePrefab(ObstaclePrefabBuilder.PathOf("Bumper"), obstacles, new Vector3(0f, 0f, 120f), Quaternion.identity, "Bumper_Bridge");
            for (var i = 0; i < 2; i++)
            {
                var hammer = PlacePrefab(ObstaclePrefabBuilder.PathOf("SwingingHammer"), obstacles, new Vector3(0f, 0f, 114f + i * 19f), Quaternion.identity, $"BridgeHammer_{i}");
                var pendulum = hammer.GetComponentInChildren<PendulumObstacle>();
                EditorAssetUtility.SetField(pendulum, "period", 2.2f);
                EditorAssetUtility.SetField(pendulum, "phaseOffset", i * 1.1f);
            }

            // ---- Section 5: Final ramp (z 140..165) and finish (z 165..180) --------------------
            const float rampAngle = 10f;
            const float rampLength = 25.4f;
            var rampTop = rampLength * Mathf.Sin(rampAngle * Mathf.Deg2Rad);
            Ramp(level, "FinalRamp", new Vector3(0f, 0f, 140f), 14f, rampLength, rampAngle, path);
            float RampHeight(float z) => (z - 140f) * Mathf.Tan(rampAngle * Mathf.Deg2Rad);

            PlacePrefab(ObstaclePrefabBuilder.PathOf("WindmillHammer"), obstacles, new Vector3(0f, RampHeight(152f), 152f), Quaternion.identity, "Windmill");
            PlacePrefab(ObstaclePrefabBuilder.PathOf("Bumper"), obstacles, new Vector3(-4.5f, RampHeight(159f), 159f), Quaternion.identity, "Bumper_RampL");
            PlacePrefab(ObstaclePrefabBuilder.PathOf("Bumper"), obstacles, new Vector3(4.5f, RampHeight(159f), 159f), Quaternion.identity, "Bumper_RampR");

            Block(level, "FinishPlatform", new Vector3(0f, rampTop - 0.5f, 172.5f), new Vector3(14f, 1f, 15f), finish);
            Arch(decor, new Vector3(0f, rampTop, 172f), 7.5f, 5.5f, finish, edge);

            // ---- Gameplay volumes ----------------------------------------------------------------
            AddCheckpoint(gameplay, 1, new Vector3(0f, 1.5f, 11f), new Vector3(12f, 4f, 2f), 9f);
            AddCheckpoint(gameplay, 2, new Vector3(0f, 1.5f, 51f), new Vector3(16f, 4f, 2f), 12f);
            AddCheckpoint(gameplay, 3, new Vector3(0f, 1.5f, 78.5f), new Vector3(16f, 4f, 2f), 12f);
            AddCheckpoint(gameplay, 4, new Vector3(0f, 1.5f, 103f), new Vector3(5f, 4f, 2f), 3f);
            AddCheckpoint(gameplay, 5, new Vector3(0f, 1.6f, 141f), new Vector3(14f, 4f, 2f), 10f);
            AddFinishLine(gameplay, new Vector3(0f, rampTop + 2f, 172f), new Vector3(14f, 4f, 1f));
            AddKillVolume(gameplay, new Vector3(0f, KillHeight - 4f, 85f), new Vector3(260f, 4f, 320f));

            var spawns = SpawnGrid(gameplay, new Vector3(0f, 0f, -2f), 8, 4, 2.6f, 2.2f, 0f);
            var mapController = new GameObject("MapController").AddComponent<MapController>();
            mapController.Configure(definition, spawns, KillHeight);
            offlineRig = AssetDatabase.LoadAssetAtPath<GameObject>(RigBuilder.OfflineRigPath);
            onlineRig = AssetDatabase.LoadAssetAtPath<GameObject>(RigBuilder.OnlineRigPath);
            mapController.gameObject.AddComponent<MapBootstrap>().Configure(offlineRig, onlineRig, 3);

            // ---- Scenery ----------------------------------------------------------------------------
            Decor(decor, PrimitiveType.Plane, "Sea", new Vector3(0f, -45f, 85f), new Vector3(400f, 1f, 400f), sea);
            Scenery(decor, new Bounds(new Vector3(0f, 0f, 85f), new Vector3(30f, 10f, 200f)), islandTop, islandRock, cloud, seed: 7);
            for (var z = 14f; z < 140f; z += 12f)
            {
                var halfWidth = z < 50f ? 6f : z < 80f ? 8f : z > 102f ? 2.5f : 0f;
                if (halfWidth <= 0f)
                {
                    continue;
                }

                Flag(decor, new Vector3(-halfWidth - 0.6f, 0f, z), arch, edge);
                Flag(decor, new Vector3(halfWidth + 0.6f, 0f, z), start, edge);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            PlaygroundSceneBuilder.AddToBuildSettings(ScenePath);
            Debug.Log($"[KnockRush Build] Hammer Rush scene saved: {ScenePath}");
        }

        private static void Floor(Transform parent, string name, float x, float zStart, float zEnd, float width, Material surface, Material trim)
        {
            var length = zEnd - zStart;
            var centerZ = (zStart + zEnd) * 0.5f;
            Block(parent, name, new Vector3(x, -0.5f, centerZ), new Vector3(width, 1f, length), surface);
            Decor(parent, PrimitiveType.Cube, $"{name}_EdgeL", new Vector3(x - width * 0.5f + 0.15f, 0.01f, centerZ), new Vector3(0.3f, 0.04f, length), trim);
            Decor(parent, PrimitiveType.Cube, $"{name}_EdgeR", new Vector3(x + width * 0.5f - 0.15f, 0.01f, centerZ), new Vector3(0.3f, 0.04f, length), trim);
        }

        private static void Arch(Transform parent, Vector3 basePosition, float halfWidth, float height, Material pillars, Material banner)
        {
            var root = new GameObject("Arch").transform;
            root.SetParent(parent, false);
            root.localPosition = basePosition;
            Decor(root, PrimitiveType.Cylinder, "PillarL", new Vector3(-halfWidth, height * 0.5f, 0f), new Vector3(0.8f, height * 0.5f, 0.8f), pillars);
            Decor(root, PrimitiveType.Cylinder, "PillarR", new Vector3(halfWidth, height * 0.5f, 0f), new Vector3(0.8f, height * 0.5f, 0.8f), pillars);
            Decor(root, PrimitiveType.Cube, "Banner", new Vector3(0f, height, 0f), new Vector3(halfWidth * 2f + 0.8f, 1.2f, 0.4f), banner);
        }

        private static void Flag(Transform parent, Vector3 position, Material cloth, Material pole)
        {
            var root = new GameObject("Flag").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            Decor(root, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 1.5f, 0f), new Vector3(0.12f, 1.5f, 0.12f), pole);
            Decor(root, PrimitiveType.Cube, "Cloth", new Vector3(0f, 2.6f, 0.5f), new Vector3(0.05f, 0.7f, 1f), cloth);
        }
    }
}
