using System.Collections.Generic;
using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Obstacles;
using KnockRush.EditorTools.Scenes;
using KnockRush.EditorTools.Setup;
using KnockRush.Interaction;
using KnockRush.Maps;
using KnockRush.Obstacles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static KnockRush.EditorTools.Scenes.SceneBuildKit;

namespace KnockRush.EditorTools.Maps
{
    /// <summary>
    /// Shared scaffolding for generated maps: definition asset, theme materials, scene roots,
    /// gameplay volumes, MapController + MapBootstrap, scenery and saving.
    /// </summary>
    public sealed class MapBuildContext
    {
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly string materialFolder;

        public string SceneName { get; }
        public string ScenePath { get; }
        public MapDefinition Definition { get; }
        public Scene Scene { get; }
        public Transform Level { get; }
        public Transform Obstacles { get; }
        public Transform Gameplay { get; }
        public Transform Decor { get; }

        private MapBuildContext(string sceneName, MapDefinition definition, Scene scene)
        {
            SceneName = sceneName;
            ScenePath = $"Assets/Scenes/Maps/{sceneName}.unity";
            Definition = definition;
            Scene = scene;
            materialFolder = $"Assets/Materials/Maps/{sceneName}";
            Level = new GameObject("Level").transform;
            Obstacles = new GameObject("Obstacles").transform;
            Gameplay = new GameObject("Gameplay").transform;
            Decor = new GameObject("Decor").transform;
        }

        public static MapBuildContext Begin(string sceneName, string id, string displayName, string description,
            MapCategory category, FallRule fallRule, int difficulty, float timeLimit, Color theme, Quaternion sun)
        {
            KnockRushProjectSetup.EnsureFolder("Assets/Scenes/Maps");
            KnockRushProjectSetup.EnsureFolder($"Assets/Materials/Maps/{sceneName}");

            var definition = EditorAssetUtility.LoadOrCreate($"Assets/ScriptableObjects/Maps/{sceneName}.asset", ScriptableObject.CreateInstance<MapDefinition>);
            definition.Configure(id, displayName, description, category, fallRule, sceneName, difficulty, timeLimit, theme);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Object.DestroyImmediate(Camera.main.gameObject);
            ConfigureLighting(sun, new Color(0.74f, 0.84f, 1f), new Color(0.66f, 0.62f, 0.74f), new Color(0.38f, 0.32f, 0.44f));

            // Reload the definition: NewScene unloads unused assets (see NetworkContentBuilder).
            definition = AssetDatabase.LoadAssetAtPath<MapDefinition>($"Assets/ScriptableObjects/Maps/{sceneName}.asset");
            return new MapBuildContext(sceneName, definition, scene);
        }

        public Material Mat(string name, Color32 color)
        {
            if (!materials.TryGetValue(name, out var material) || material == null)
            {
                material = EditorAssetUtility.CreateToonMaterial($"{materialFolder}/{SceneName}_{name}.mat", color, false);
                materials[name] = material;
            }

            return material;
        }

        public GameObject Obstacle(string prefab, Vector3 position, Quaternion? rotation = null, string name = null)
        {
            return PlacePrefab(ObstaclePrefabBuilder.PathOf(prefab), Obstacles, position, rotation ?? Quaternion.identity, name);
        }

        public static void Set(Component component, string field, object value)
        {
            EditorAssetUtility.SetField(component, field, value);
        }

        /// <summary>Marks a block as slime / ice / conveyor.</summary>
        public static void Surface(GameObject block, float friction, Vector3 localConveyor)
        {
            block.AddComponent<SurfaceModifier>().Configure(friction, localConveyor);
        }

        /// <summary>Platform deck whose top surface is at <paramref name="top"/>.</summary>
        public GameObject Deck(string name, Vector3 topCenter, Vector3 size, Material material)
        {
            return Block(Level, name, topCenter - new Vector3(0f, size.y * 0.5f, 0f), size, material);
        }

        public GameObject Disc(string name, Vector3 topCenter, float radius, float thickness, Material material)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            disc.transform.SetParent(Level, false);
            disc.transform.localPosition = topCenter - new Vector3(0f, thickness * 0.5f, 0f);
            disc.transform.localScale = new Vector3(radius * 2f, thickness * 0.5f, radius * 2f);
            disc.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.AddComponent<MeshCollider>();
            disc.isStatic = true;
            return disc;
        }

        /// <summary>32 spawn points in rings around a centre (arenas).</summary>
        public Transform[] RingSpawns(Vector3 center, float innerRadius, float outerRadius)
        {
            var root = new GameObject("SpawnPoints").transform;
            root.SetParent(Gameplay, false);
            var points = new Transform[32];
            for (var i = 0; i < 32; i++)
            {
                var ring = i % 2;
                var angle = (i / 2) * (360f / 16f) + ring * 11.25f;
                var radius = ring == 0 ? innerRadius : outerRadius;
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var point = new GameObject($"Spawn_{i:00}").transform;
                point.SetParent(root, false);
                point.SetLocalPositionAndRotation(center + direction * radius + Vector3.up * 0.05f, Quaternion.LookRotation(-direction));
                points[i] = point;
            }

            return points;
        }

        public void Finish(Transform[] spawns, float killHeight, Bounds course, Material islandTop, Material islandRock, int seed)
        {
            AddKillVolume(Gameplay, new Vector3(course.center.x, killHeight - 4f, course.center.z), new Vector3(course.size.x + 200f, 4f, course.size.z + 200f));

            var mapController = new GameObject("MapController").AddComponent<MapController>();
            mapController.Configure(Definition, spawns, killHeight);
            var offlineRig = AssetDatabase.LoadAssetAtPath<GameObject>(RigBuilder.OfflineRigPath);
            var onlineRig = AssetDatabase.LoadAssetAtPath<GameObject>(RigBuilder.OnlineRigPath);
            mapController.gameObject.AddComponent<MapBootstrap>().Configure(offlineRig, onlineRig, 0);

            var cloud = Mat("Cloud", new Color32(0xFF, 0xFF, 0xFF, 0xFF));
            var sea = Mat("Sea", new Color32(0x4F, 0xB4, 0xFF, 0xFF));
            SceneBuildKit.Decor(Decor, PrimitiveType.Plane, "Sea", new Vector3(course.center.x, killHeight - 35f, course.center.z), new Vector3(400f, 1f, 400f), sea);
            Scenery(Decor, course, islandTop, islandRock, cloud, seed);

            EditorSceneManager.SaveScene(Scene, ScenePath);
            PlaygroundSceneBuilder.AddToBuildSettings(ScenePath);
            Debug.Log($"[KnockRush Build] {Definition.DisplayName} scene saved: {ScenePath}");
        }

        public void SetPhase(GameObject obstacleRoot, float phase)
        {
            var obstacle = obstacleRoot.GetComponentInChildren<TimeDrivenObstacle>();
            if (obstacle != null)
            {
                Set(obstacle, "phaseOffset", phase);
            }
        }
    }
}
