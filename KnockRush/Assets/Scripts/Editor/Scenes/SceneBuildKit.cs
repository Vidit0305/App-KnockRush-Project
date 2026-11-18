using System.Collections.Generic;
using KnockRush.CameraRig;
using KnockRush.Core;
using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.UI;
using KnockRush.Maps;
using KnockRush.Player;
using KnockRush.Player.Controls;
using KnockRush.UI.Placeholders;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace KnockRush.EditorTools.Scenes
{
    /// <summary>Shared helpers for generating map scenes: geometry, gameplay volumes, player rig, decor.</summary>
    public static class SceneBuildKit
    {
        public static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material material, int layer = GameLayers.Default)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.layer = layer;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = center;
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            block.isStatic = true;
            return block;
        }

        /// <summary>Ramp whose top surface starts at <paramref name="bottomFront"/> and rises along +Z.</summary>
        public static GameObject Ramp(Transform parent, string name, Vector3 bottomFront, float width, float length, float angle, Material material)
        {
            const float thickness = 0.6f;
            var rotation = Quaternion.Euler(-angle, 0f, 0f);
            var center = bottomFront + rotation * new Vector3(0f, -thickness * 0.5f, length * 0.5f);
            var ramp = Block(parent, name, center, new Vector3(width, thickness, length), material);
            ramp.transform.localRotation = rotation;
            return ramp;
        }

        public static GameObject Decor(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation ?? Quaternion.identity);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        public static GameObject PlacePrefab(string path, Transform parent, Vector3 position, Quaternion rotation, string name = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetLocalPositionAndRotation(position, rotation);
            if (name != null)
            {
                instance.name = name;
            }

            return instance;
        }

        public static void ConfigureLighting(Quaternion sunRotation, Color sky, Color equator, Color ground, float sunIntensity = 1.25f)
        {
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = sunRotation;
                light.intensity = sunIntensity;
                light.color = new Color(1f, 0.96f, 0.9f);
                light.shadows = LightShadows.Soft;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
        }

        public static Transform[] SpawnGrid(Transform parent, Vector3 center, int columns, int rows, float spacingX, float spacingZ, float yaw)
        {
            var root = new GameObject("SpawnPoints").transform;
            root.SetParent(parent, false);
            var points = new List<Transform>();
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var point = new GameObject($"Spawn_{points.Count:00}").transform;
                    point.SetParent(root, false);
                    var x = (column - (columns - 1) * 0.5f) * spacingX;
                    var z = -row * spacingZ;
                    point.SetLocalPositionAndRotation(center + Quaternion.Euler(0f, yaw, 0f) * new Vector3(x, 0.05f, z), Quaternion.Euler(0f, yaw, 0f));
                    points.Add(point);
                }
            }

            return points.ToArray();
        }

        public static Checkpoint AddCheckpoint(Transform parent, int order, Vector3 center, Vector3 size, float respawnWidth)
        {
            var go = new GameObject($"Checkpoint_{order}") { layer = GameLayers.Trigger };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;

            var points = new Transform[8];
            for (var i = 0; i < points.Length; i++)
            {
                var point = new GameObject($"Respawn_{i}").transform;
                point.SetParent(go.transform, false);
                point.localPosition = new Vector3((i - 3.5f) / 3.5f * (respawnWidth * 0.5f), 0.1f - size.y * 0.5f + 0.5f, 0f);
                points[i] = point;
            }

            var checkpoint = go.AddComponent<Checkpoint>();
            checkpoint.Configure(order, points);
            return checkpoint;
        }

        public static void AddFinishLine(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("FinishLine") { layer = GameLayers.Trigger };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            go.AddComponent<FinishLine>();
        }

        public static void AddKillVolume(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("KillVolume") { layer = GameLayers.Trigger };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            go.AddComponent<KillVolume>();
        }

        /// <summary>Spawns the local player, wires the follow camera, mobile controls, HUD and dev tools.</summary>
        public static PlayerCharacter AddLocalPlayerRig(Transform spawn, float killHeight, CameraSettings cameraSettings, bool withRaceHud)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Characters.CharacterAssetBuilder.PlayerPrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var character = player.GetComponent<PlayerCharacter>();
            var reader = player.GetComponent<PlayerInputReader>();
            EditorAssetUtility.SetField(character, "killHeight", killHeight);

            var cameraObject = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            var rig = cameraObject.AddComponent<ThirdPersonCamera>();
            EditorAssetUtility.SetField(rig, "settings", cameraSettings);
            EditorAssetUtility.SetField(rig, "target", player.transform);
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = cameraSettings.FieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            cameraObject.transform.SetPositionAndRotation(spawn.position + spawn.rotation * new Vector3(0f, 3f, -6.5f), spawn.rotation * Quaternion.Euler(16f, 0f, 0f));

            EditorAssetUtility.SetField(character, "cameraRig", rig);
            EditorAssetUtility.SetField(reader, "cameraRig", rig);

            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MobileControlsBuilder.PrefabPath));
            if (withRaceHud)
            {
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudBuilder.RaceHudPath));
            }

            MobileControlsBuilder.CreateEventSystem();

            var devTools = new GameObject("DevTools").AddComponent<PlaygroundDevTools>();
            EditorAssetUtility.SetField(devTools, "character", character);
            return character;
        }

        /// <summary>Scatters floating islands and clouds around a course for depth and theme.</summary>
        public static void Scenery(Transform parent, Bounds course, Material islandTop, Material islandRock, Material cloud, int seed)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(parent, false);
            var random = new System.Random(seed);
            float Range(float min, float max) => (float)(min + random.NextDouble() * (max - min));

            for (var i = 0; i < 14; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var position = new Vector3(
                    course.center.x + side * Range(course.extents.x + 25f, course.extents.x + 90f),
                    Range(-25f, 15f),
                    Range(course.min.z - 20f, course.max.z + 20f));
                var radius = Range(6f, 16f);
                var island = new GameObject($"Island_{i}").transform;
                island.SetParent(root, false);
                island.localPosition = position;
                Decor(island, PrimitiveType.Cylinder, "Top", Vector3.zero, new Vector3(radius * 2f, 1f, radius * 2f), islandTop);
                Decor(island, PrimitiveType.Sphere, "Rock", new Vector3(0f, -radius * 0.45f, 0f), new Vector3(radius * 1.8f, radius * 1.1f, radius * 1.8f), islandRock);
            }

            for (var i = 0; i < 18; i++)
            {
                var position = new Vector3(
                    course.center.x + Range(-140f, 140f),
                    Range(35f, 70f),
                    Range(course.min.z - 60f, course.max.z + 60f));
                var cloudRoot = new GameObject($"Cloud_{i}").transform;
                cloudRoot.SetParent(root, false);
                cloudRoot.localPosition = position;
                for (var puff = 0; puff < 4; puff++)
                {
                    var size = Range(6f, 12f);
                    Decor(cloudRoot, PrimitiveType.Sphere, $"Puff{puff}", new Vector3(puff * size * 0.6f - size, Range(-1f, 2f), Range(-2f, 2f)), new Vector3(size, size * 0.6f, size), cloud);
                }
            }
        }
    }
}
