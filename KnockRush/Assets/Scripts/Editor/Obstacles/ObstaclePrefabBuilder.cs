using KnockRush.Core;
using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Setup;
using KnockRush.Interaction;
using KnockRush.Obstacles;
using UnityEditor;
using UnityEngine;

namespace KnockRush.EditorTools.Obstacles
{
    /// <summary>
    /// Builds the reusable obstacle prefab library in Assets/Prefabs/Obstacles from primitives and
    /// toon materials (placeholder art with final gameplay colliders; meshes can be swapped for
    /// Blender art later without touching gameplay).
    /// </summary>
    public static class ObstaclePrefabBuilder
    {
        public const string Folder = "Assets/Prefabs/Obstacles";
        private const string MaterialFolder = "Assets/Materials/Obstacles";

        public static string PathOf(string name) => $"{Folder}/{name}.prefab";

        private static Material hammerHead;
        private static Material wood;
        private static Material metal;
        private static Material bumper;
        private static Material bounce;
        private static Material platform;
        private static Material tile;
        private static Material hazard;

        public static void BuildAll()
        {
            KnockRushProjectSetup.EnsureFolder(Folder);
            KnockRushProjectSetup.EnsureFolder(MaterialFolder);

            hammerHead = Mat("Obs_HammerHead", new Color32(0xE8, 0x3A, 0x3A, 0xFF));
            wood = Mat("Obs_Wood", new Color32(0xB7, 0x7A, 0x4A, 0xFF));
            metal = Mat("Obs_Metal", new Color32(0x9A, 0xA4, 0xB8, 0xFF));
            bumper = Mat("Obs_Bumper", new Color32(0xFF, 0x4F, 0x9A, 0xFF));
            bounce = Mat("Obs_Bounce", new Color32(0x22, 0xC5, 0x8B, 0xFF));
            platform = Mat("Obs_Platform", new Color32(0x2D, 0x9C, 0xFF, 0xFF));
            tile = Mat("Obs_Tile", new Color32(0xFF, 0xD1, 0x2B, 0xFF));
            hazard = Mat("Obs_Hazard", new Color32(0x2B, 0x2B, 0x3A, 0xFF));

            SwingingHammer();
            SweeperBar();
            MovingPlatform();
            Bumper();
            BouncePad();
            FallingTilePrefab();
            CyclingTile();
            RotatingDisc();
            WindmillHammer();
            Hurdle();
            RollingBall();
            CollapseTile();
            HangingHammer();
            ArenaSweeper();
            AssetDatabase.SaveAssets();
            Debug.Log("[KnockRush Build] Obstacle prefabs built.");
        }

        // --------------------------------------------------------------------------------

        /// <summary>Pendulum hammer swinging across a 12 m path; pivot 6.8 m up, head sweeps 0.35–2.05 m.</summary>
        private static void SwingingHammer()
        {
            var root = new GameObject("SwingingHammer");
            Part(root.transform, PrimitiveType.Cube, "PostL", new Vector3(-7f, 3.6f, 0f), new Vector3(0.5f, 7.2f, 0.5f), Quaternion.identity, metal, GameLayers.Default);
            Part(root.transform, PrimitiveType.Cube, "PostR", new Vector3(7f, 3.6f, 0f), new Vector3(0.5f, 7.2f, 0.5f), Quaternion.identity, metal, GameLayers.Default);
            Part(root.transform, PrimitiveType.Cube, "Beam", new Vector3(0f, 7.2f, 0f), new Vector3(14.5f, 0.6f, 0.6f), Quaternion.identity, metal, GameLayers.Default);

            var arm = Pivot(root.transform, "Arm", new Vector3(0f, 6.8f, 0f));
            var pendulum = arm.gameObject.AddComponent<PendulumObstacle>();
            pendulum.Configure(Vector3.forward, 70f, 2.8f);
            Part(arm, PrimitiveType.Cylinder, "Handle", new Vector3(0f, -2.75f, 0f), new Vector3(0.35f, 2.75f, 0.35f), Quaternion.identity, wood, GameLayers.Obstacle);
            Part(arm, PrimitiveType.Cylinder, "Head", new Vector3(0f, -5.6f, 0f), new Vector3(1.7f, 1.5f, 1.7f), Quaternion.Euler(0f, 0f, 90f), hammerHead, GameLayers.Obstacle);
            Part(arm, PrimitiveType.Cylinder, "BandL", new Vector3(-1.1f, -5.6f, 0f), new Vector3(1.78f, 0.12f, 1.78f), Quaternion.Euler(0f, 0f, 90f), metal, GameLayers.Obstacle, keepCollider: false);
            Part(arm, PrimitiveType.Cylinder, "BandR", new Vector3(1.1f, -5.6f, 0f), new Vector3(1.78f, 0.12f, 1.78f), Quaternion.Euler(0f, 0f, 90f), metal, GameLayers.Obstacle, keepCollider: false);
            Hitbox(arm, new Vector3(0f, -5.6f, 0f), new Vector3(3.5f, 2.2f, 2.2f), ImpulseMode.ObstacleVelocity, 11f, 5f, Vector3.up);
            Save(root);
        }

        /// <summary>Ground-level rotating bar (jump over it). Bar top at ~0.83 m.</summary>
        private static void SweeperBar()
        {
            var root = new GameObject("SweeperBar");
            Kinematic(root);
            root.layer = GameLayers.Obstacle;
            var rotor = root.AddComponent<RotatingObstacle>();
            rotor.Configure(Vector3.up, 95f);
            Part(root.transform, PrimitiveType.Cylinder, "Hub", new Vector3(0f, 0.6f, 0f), new Vector3(1.3f, 0.6f, 1.3f), Quaternion.identity, hazard, GameLayers.Obstacle);
            Part(root.transform, PrimitiveType.Cube, "Bar", new Vector3(0f, 0.55f, 0f), new Vector3(14f, 0.55f, 0.55f), Quaternion.identity, hammerHead, GameLayers.Obstacle);
            Hitbox(root.transform, new Vector3(0f, 0.7f, 0f), new Vector3(14.4f, 1.1f, 1.3f), ImpulseMode.ObstacleVelocity, 8f, 6f, Vector3.up);
            Save(root);
        }

        /// <summary>4×4 m platform that slides back and forth (top surface at the root's y).</summary>
        private static void MovingPlatform()
        {
            var root = new GameObject("MovingPlatform");
            Kinematic(root);
            root.layer = GameLayers.Obstacle;
            var mover = root.AddComponent<OscillatingObstacle>();
            mover.Configure(new Vector3(8f, 0f, 0f), 4.5f, 0.2f);
            Part(root.transform, PrimitiveType.Cube, "Deck", new Vector3(0f, -0.25f, 0f), new Vector3(4f, 0.5f, 4f), Quaternion.identity, platform, GameLayers.Obstacle);
            Part(root.transform, PrimitiveType.Cube, "Trim", new Vector3(0f, -0.55f, 0f), new Vector3(3.6f, 0.2f, 3.6f), Quaternion.identity, metal, GameLayers.Obstacle, keepCollider: false);
            Save(root);
        }

        /// <summary>Pillar that bounces players away radially.</summary>
        private static void Bumper()
        {
            var root = new GameObject("Bumper");
            Part(root.transform, PrimitiveType.Cylinder, "Pillar", new Vector3(0f, 0.75f, 0f), new Vector3(1.4f, 0.75f, 1.4f), Quaternion.identity, bumper, GameLayers.Default);
            Part(root.transform, PrimitiveType.Sphere, "Cap", new Vector3(0f, 1.55f, 0f), new Vector3(1.1f, 0.5f, 1.1f), Quaternion.identity, tile, GameLayers.Default, keepCollider: false);
            Hitbox(root.transform, new Vector3(0f, 0.8f, 0f), new Vector3(2.1f, 1.6f, 2.1f), ImpulseMode.Radial, 12f, 4f, Vector3.up);
            Save(root);
        }

        /// <summary>Trampoline pad: launches straight up.</summary>
        private static void BouncePad()
        {
            var root = new GameObject("BouncePad");
            var pad = Part(root.transform, PrimitiveType.Cylinder, "Pad", new Vector3(0f, 0.08f, 0f), new Vector3(2.4f, 0.08f, 2.4f), Quaternion.identity, bounce, GameLayers.Default, keepCollider: false);
            pad.AddComponent<MeshCollider>();
            Part(root.transform, PrimitiveType.Cylinder, "Rim", new Vector3(0f, 0.05f, 0f), new Vector3(2.7f, 0.05f, 2.7f), Quaternion.identity, hazard, GameLayers.Default, keepCollider: false);
            Hitbox(root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.6f, 2.2f), ImpulseMode.Directional, 15f, 0f, Vector3.up);
            Save(root);
        }

        /// <summary>3×3 m tile that shakes and drops when stepped on (top surface at root y).</summary>
        private static void FallingTilePrefab()
        {
            var root = new GameObject("FallingTile");
            var solid = root.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, -0.25f, 0f);
            solid.size = new Vector3(3f, 0.5f, 3f);
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.2f, 0f);
            trigger.size = new Vector3(2.8f, 0.4f, 2.8f);

            var visual = Part(root.transform, PrimitiveType.Cube, "Visual", new Vector3(0f, -0.25f, 0f), new Vector3(2.92f, 0.5f, 2.92f), Quaternion.identity, tile, GameLayers.Default, keepCollider: false);
            var falling = root.AddComponent<FallingTile>();
            EditorAssetUtility.SetField(falling, "visual", visual.transform);
            Save(root);
        }

        /// <summary>3×3 m tile that appears/disappears on a timer (blinks before vanishing).</summary>
        private static void CyclingTile()
        {
            var root = new GameObject("CyclingTile");
            var cycler = root.AddComponent<CyclingPlatform>();
            cycler.Configure(5f, 0.6f, 1f);
            Part(root.transform, PrimitiveType.Cube, "Deck", new Vector3(0f, -0.25f, 0f), new Vector3(3f, 0.5f, 3f), Quaternion.identity, bumper, GameLayers.Default);
            Save(root);
        }

        /// <summary>10 m rotating disc platform that carries players.</summary>
        private static void RotatingDisc()
        {
            var root = new GameObject("RotatingDisc");
            Kinematic(root);
            root.layer = GameLayers.Obstacle;
            root.AddComponent<RotatingObstacle>().Configure(Vector3.up, 30f);
            var disc = Part(root.transform, PrimitiveType.Cylinder, "Disc", new Vector3(0f, -0.25f, 0f), new Vector3(10f, 0.25f, 10f), Quaternion.identity, platform, GameLayers.Obstacle, keepCollider: false);
            disc.AddComponent<MeshCollider>();
            Part(root.transform, PrimitiveType.Cube, "Stripe", new Vector3(0f, 0.01f, 0f), new Vector3(9.6f, 0.02f, 0.8f), Quaternion.identity, tile, GameLayers.Obstacle, keepCollider: false);
            Save(root);
        }

        /// <summary>Vertical three-armed wheel whose hammer heads sweep across the path.</summary>
        private static void WindmillHammer()
        {
            var root = new GameObject("WindmillHammer");
            Part(root.transform, PrimitiveType.Cube, "Tower", new Vector3(0f, 2.3f, 1.2f), new Vector3(0.8f, 4.6f, 0.8f), Quaternion.identity, metal, GameLayers.Default);

            var rotor = Pivot(root.transform, "Rotor", new Vector3(0f, 4.4f, 0f));
            rotor.gameObject.AddComponent<RotatingObstacle>().Configure(Vector3.forward, 60f);
            Part(rotor, PrimitiveType.Cylinder, "Hub", Vector3.zero, new Vector3(1f, 0.5f, 1f), Quaternion.Euler(90f, 0f, 0f), hazard, GameLayers.Obstacle);
            for (var i = 0; i < 3; i++)
            {
                var angle = i * 120f;
                var direction = Quaternion.Euler(0f, 0f, angle) * Vector3.down;
                var rotation = Quaternion.Euler(0f, 0f, angle);
                Part(rotor, PrimitiveType.Cube, $"Arm{i}", direction * 1.9f, new Vector3(0.4f, 3.8f, 0.4f), rotation, wood, GameLayers.Obstacle);
                Part(rotor, PrimitiveType.Cylinder, $"Head{i}", direction * 3.9f, new Vector3(1.4f, 0.9f, 1.4f), rotation * Quaternion.Euler(90f, 0f, 0f), hammerHead, GameLayers.Obstacle);
                Hitbox(rotor, direction * 3.9f, new Vector3(2f, 2f, 2.2f), ImpulseMode.ObstacleVelocity, 10f, 5f, Vector3.up, rotation);
            }

            Save(root);
        }

        /// <summary>Low wall to jump over.</summary>
        private static void Hurdle()
        {
            var root = new GameObject("Hurdle");
            Part(root.transform, PrimitiveType.Cube, "Bar", new Vector3(0f, 0.3f, 0f), new Vector3(4f, 0.6f, 0.4f), Quaternion.identity, hazard, GameLayers.Default);
            Part(root.transform, PrimitiveType.Cube, "Stripe", new Vector3(0f, 0.45f, 0f), new Vector3(4.02f, 0.12f, 0.42f), Quaternion.identity, tile, GameLayers.Default, keepCollider: false);
            Save(root);
        }

        /// <summary>3 m ball that rolls down a slope on a loop (set travel per placement).</summary>
        private static void RollingBall()
        {
            var root = new GameObject("RollingBall");
            Kinematic(root);
            root.layer = GameLayers.Obstacle;
            root.AddComponent<LoopingMover>().Configure(new Vector3(0f, -6f, -30f), 5f, 38f);
            Part(root.transform, PrimitiveType.Sphere, "Ball", Vector3.zero, Vector3.one * 3f, Quaternion.identity, bumper, GameLayers.Obstacle);
            Part(root.transform, PrimitiveType.Cube, "Stripe", Vector3.zero, new Vector3(3.05f, 0.5f, 0.5f), Quaternion.identity, tile, GameLayers.Obstacle, keepCollider: false);
            Hitbox(root.transform, Vector3.zero, new Vector3(3.6f, 3.6f, 3.6f), ImpulseMode.ObstacleVelocity, 9f, 5f, Vector3.up);
            Save(root);
        }

        /// <summary>2.6 m tile that collapses at a scheduled round time (Last Platform).</summary>
        private static void CollapseTile()
        {
            var root = new GameObject("CollapseTile");
            root.AddComponent<TimedCollapse>().Configure(30f, 2.5f);
            Part(root.transform, PrimitiveType.Cube, "Deck", new Vector3(0f, -0.25f, 0f), new Vector3(2.55f, 0.5f, 2.55f), Quaternion.identity, hazard, GameLayers.Default);
            Part(root.transform, PrimitiveType.Cube, "Top", new Vector3(0f, 0.005f, 0f), new Vector3(2.35f, 0.02f, 2.35f), Quaternion.identity, tile, GameLayers.Default, keepCollider: false);
            Save(root);
        }

        /// <summary>Pendulum hammer hanging from a floating pivot (no posts), for arenas.</summary>
        private static void HangingHammer()
        {
            var root = new GameObject("HangingHammer");
            Part(root.transform, PrimitiveType.Sphere, "Pivot", new Vector3(0f, 6.8f, 0f), Vector3.one * 0.9f, Quaternion.identity, metal, GameLayers.Default, keepCollider: false);
            var arm = Pivot(root.transform, "Arm", new Vector3(0f, 6.8f, 0f));
            arm.gameObject.AddComponent<PendulumObstacle>().Configure(Vector3.forward, 75f, 2.6f);
            Part(arm, PrimitiveType.Cylinder, "Handle", new Vector3(0f, -2.75f, 0f), new Vector3(0.35f, 2.75f, 0.35f), Quaternion.identity, wood, GameLayers.Obstacle);
            Part(arm, PrimitiveType.Cylinder, "Head", new Vector3(0f, -5.6f, 0f), new Vector3(1.7f, 1.5f, 1.7f), Quaternion.Euler(0f, 0f, 90f), hammerHead, GameLayers.Obstacle);
            Hitbox(arm, new Vector3(0f, -5.6f, 0f), new Vector3(3.5f, 2.2f, 2.2f), ImpulseMode.ObstacleVelocity, 11f, 5f, Vector3.up);
            Save(root);
        }

        /// <summary>30 m ground sweeper for circular arenas (Spin Attack, Last Platform).</summary>
        private static void ArenaSweeper()
        {
            var root = new GameObject("ArenaSweeper");
            Kinematic(root);
            root.layer = GameLayers.Obstacle;
            root.AddComponent<RotatingObstacle>().Configure(Vector3.up, 70f);
            Part(root.transform, PrimitiveType.Cylinder, "Hub", new Vector3(0f, 0.7f, 0f), new Vector3(2f, 0.7f, 2f), Quaternion.identity, hazard, GameLayers.Obstacle);
            Part(root.transform, PrimitiveType.Cube, "Bar", new Vector3(0f, 0.55f, 0f), new Vector3(30f, 0.55f, 0.6f), Quaternion.identity, hammerHead, GameLayers.Obstacle);
            Hitbox(root.transform, new Vector3(0f, 0.7f, 0f), new Vector3(30.4f, 1.1f, 1.4f), ImpulseMode.ObstacleVelocity, 8f, 6f, Vector3.up);
            Save(root);
        }

        // --------------------------------------------------------------------------------

        private static Material Mat(string name, Color color)
        {
            return EditorAssetUtility.CreateToonMaterial($"{MaterialFolder}/{name}.mat", color, false);
        }

        public static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale,
            Quaternion localRotation, Material material, int layer, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }

            return go;
        }

        private static Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = localPosition;
            pivot.gameObject.layer = GameLayers.Obstacle;
            Kinematic(pivot.gameObject);
            return pivot;
        }

        private static void Kinematic(GameObject go)
        {
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
        }

        private static void Hitbox(Transform parent, Vector3 localPosition, Vector3 size, ImpulseMode mode, float strength,
            float upward, Vector3 direction, Quaternion? localRotation = null)
        {
            var go = new GameObject("Hitbox") { layer = GameLayers.Trigger };
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation ?? Quaternion.identity);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            go.AddComponent<ImpulseZone>().Configure(mode, strength, upward, direction);
        }

        private static void Save(GameObject root)
        {
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, PathOf(root.name));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
