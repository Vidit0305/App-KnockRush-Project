using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Scenes;
using KnockRush.EditorTools.Setup;
using KnockRush.Inventory;
using KnockRush.Maps;
using KnockRush.UI.Stage;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KnockRush.EditorTools.UI
{
    /// <summary>
    /// The Home lobby set behind the player's bean: a festival castle arena (lavender tiled floor,
    /// inflatable bumpers, a striped ramp, castle towers with cone roofs, a giant crown arch, a logo
    /// sign, hot-air balloons), a glowing podium and a crowd of costumed beans idling and jogging.
    /// </summary>
    internal static class MenuLobbyBuilder
    {
        public const float FloorY = -0.45f;
        private const string MeshFolder = "Assets/Art/World/Meshes";
        private const string ConeMeshPath = MeshFolder + "/Cone.asset";
        public const string GlowPath = "Assets/Art/UI/Home/PodiumGlow.png";

        private static Mesh cone;

        public struct BeanAssets
        {
            public GameObject Model;
            public Material Material;
            public RuntimeAnimatorController Controller;
        }

        public static void Build(Transform root, BeanAssets bean)
        {
            LobbySky();
            cone = EnsureCone();

            // The stage camera looks down −Z, so +X is screen-left. The set is authored with the
            // fortress at +X and mirrored, putting the castles and crown arch on the right.
            var set = new GameObject("Set").transform;
            set.SetParent(root, false);
            set.localScale = new Vector3(-1f, 1f, 1f);
            Floor(set);
            Podium(set);
            Bumpers(set);
            Castles(set);
            CrownArch(set, new Vector3(7.4f, FloorY, -21f));
            Sky(set);
            Crowd(root, bean);
        }

        // -----------------------------------------------------------------------------------------
        private static Material M(string name, Color32 color, int pattern = 0, Color32? patternColor = null, float scale = 1f)
        {
            return WorldLookBuilder.Mat("Lobby_" + name, color, pattern, patternColor, scale);
        }

        private static GameObject Piece(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
        {
            return SceneBuildKit.Decor(parent, type, name, position, scale, material, rotation);
        }

        private static GameObject Cone(Transform parent, string name, Vector3 basePosition, float radius, float height, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = basePosition;
            go.transform.localScale = new Vector3(radius, height, radius);
            go.AddComponent<MeshFilter>().sharedMesh = cone;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;
            return go;
        }

        // -----------------------------------------------------------------------------------------
        private static void Floor(Transform root)
        {
            var lavender = EditorAssetUtility.CreateLevelMaterial(UiContentBuilder.MenuMaterialsFolder + "/Floor.mat", new Color32(178, 160, 255, 255));
            var lane = EditorAssetUtility.CreateLevelMaterial(UiContentBuilder.MenuMaterialsFolder + "/Lane.mat", new Color32(255, 200, 60, 255));
            var laneBlue = EditorAssetUtility.CreateLevelMaterial(UiContentBuilder.MenuMaterialsFolder + "/LaneBlue.mat", new Color32(80, 170, 255, 255));
            var floor = Piece(root, PrimitiveType.Cylinder, "Floor", new Vector3(0f, FloorY - 0.5f, -14f), new Vector3(110f, 0.5f, 110f), lavender);
            floor.GetComponent<MeshRenderer>().receiveShadows = true;
            Piece(root, PrimitiveType.Cube, "LaneCenter", new Vector3(0.5f, FloorY + 0.005f, -16f), new Vector3(5f, 0.02f, 22f), lane, Quaternion.Euler(0f, 8f, 0f));
            Piece(root, PrimitiveType.Cube, "LaneLeft", new Vector3(-5.5f, FloorY + 0.004f, -15f), new Vector3(3f, 0.02f, 20f), laneBlue, Quaternion.Euler(0f, -6f, 0f));
        }

        private static void Podium(Transform root)
        {
            var purple = M("PodiumPurple", new Color32(140, 80, 240, 255));
            var gold = M("PodiumGold", new Color32(255, 196, 46, 255));
            var top = M("PodiumTop", new Color32(170, 110, 255, 255), 6, new Color32(150, 90, 245, 255), 1.5f);
            var podium = new GameObject("Podium").transform;
            podium.SetParent(root, false);
            Piece(podium, PrimitiveType.Cylinder, "BaseRing", new Vector3(0f, FloorY + 0.05f, 0f), new Vector3(3f, 0.05f, 3f), gold);
            Piece(podium, PrimitiveType.Cylinder, "Drum", new Vector3(0f, -0.24f, 0f), new Vector3(2.5f, 0.2f, 2.5f), purple);
            Piece(podium, PrimitiveType.Cylinder, "TopRim", new Vector3(0f, -0.02f, 0f), new Vector3(2.62f, 0.03f, 2.62f), gold);
            Piece(podium, PrimitiveType.Cylinder, "Top", new Vector3(0f, -0.005f, 0f), new Vector3(2.2f, 0.02f, 2.2f), top);
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI * 2f / 12f;
                Piece(podium, PrimitiveType.Sphere, $"Stud{i}", new Vector3(Mathf.Sin(angle) * 1.26f, -0.24f, Mathf.Cos(angle) * 1.26f), Vector3.one * 0.14f, gold);
            }

            var glowTexture = ImportTexture(GlowPath, true, true);
            var glow = Unlit("PodiumGlow", glowTexture, new Color(1.9f, 1.4f, 0.6f, 1f));
            var quad = Piece(podium, PrimitiveType.Quad, "Glow", new Vector3(0f, FloorY + 0.012f, 0f), new Vector3(5f, 5f, 1f), glow, Quaternion.Euler(90f, 0f, 0f));
            quad.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        private static void Bumpers(Transform root)
        {
            var yellow = M("BumperYellow", new Color32(255, 200, 40, 255));
            var purple = M("BumperPurple", new Color32(150, 90, 245, 255));
            var pink = M("BumperPink", new Color32(255, 100, 190, 255));
            var group = new GameObject("Bumpers").transform;
            group.SetParent(root, false);
            for (var i = 0; i < 6; i++)
            {
                var z = -1.5f - i * 2.9f;
                Piece(group, PrimitiveType.Capsule, $"Left{i}", new Vector3(-6.2f - i * 0.25f, FloorY + 0.55f, z), new Vector3(1.1f, 1.45f, 1.1f), i % 2 == 0 ? yellow : purple, Quaternion.Euler(90f, 0f, 0f));
            }

            for (var i = 0; i < 4; i++)
            {
                var z = -8f - i * 2.9f;
                Piece(group, PrimitiveType.Capsule, $"Right{i}", new Vector3(5.6f + i * 0.2f, FloorY + 0.5f, z), new Vector3(1f, 1.4f, 1f), i % 2 == 0 ? pink : yellow, Quaternion.Euler(90f, 0f, 0f));
            }

            // Bouncy drums dotted around the arena.
            var drums = new[] { new Vector3(-3.8f, 0f, -6.5f), new Vector3(3.6f, 0f, -9f), new Vector3(-8.5f, 0f, -12f) };
            for (var i = 0; i < drums.Length; i++)
            {
                var p = drums[i];
                Piece(group, PrimitiveType.Cylinder, $"Drum{i}", new Vector3(p.x, FloorY + 0.35f, p.z), new Vector3(1.6f, 0.35f, 1.6f), i % 2 == 0 ? purple : pink);
                Piece(group, PrimitiveType.Cylinder, $"DrumTop{i}", new Vector3(p.x, FloorY + 0.72f, p.z), new Vector3(1.7f, 0.04f, 1.7f), yellow);
            }
        }

        private static void Castles(Transform root)
        {
            var wall = M("CastleWall", new Color32(110, 90, 235, 255));
            var wallDark = M("CastleWallDark", new Color32(80, 70, 210, 255));
            var roof = M("CastleRoof", new Color32(50, 140, 255, 255));
            var roofPink = M("CastleRoofPink", new Color32(255, 100, 190, 255));
            var trim = M("CastleTrim", new Color32(255, 196, 46, 255));
            var flag = M("CastleFlag", new Color32(255, 70, 120, 255));
            var group = new GameObject("Castles").transform;
            group.SetParent(root, false);

            // Right-hand fortress.
            Wall(group, new Vector3(17f, FloorY, -52f), 14f, 3.6f, wallDark, trim);
            Tower(group, new Vector3(10.6f, FloorY, -51f), 1.4f, 5.5f, wall, roof, trim, flag);
            Tower(group, new Vector3(23f, FloorY, -51.5f), 1.6f, 7f, wall, roof, trim, flag);
            Tower(group, new Vector3(17f, FloorY, -55f), 2.1f, 9f, wall, roofPink, trim, flag);
            Tower(group, new Vector3(13.5f, FloorY, -42f), 1.1f, 4.5f, wall, roof, trim, flag);

            // Left-hand keep.
            Wall(group, new Vector3(-19f, FloorY, -50f), 9f, 3f, wallDark, trim);
            Tower(group, new Vector3(-15f, FloorY, -49f), 1.3f, 5f, wall, roofPink, trim, flag);
            Tower(group, new Vector3(-22.5f, FloorY, -50.5f), 1.7f, 7f, wall, roof, trim, flag);
            Tower(group, new Vector3(-18.5f, FloorY, -54f), 1.2f, 8f, wall, roof, trim, flag);

            // Distant skyline.
            for (var i = 0; i < 9; i++)
            {
                var x = -48f + i * 12f;
                var height = 6f + (i * 37 % 6);
                Tower(group, new Vector3(x, FloorY, -78f - (i % 3) * 5f), 1.8f, height, wallDark, i % 2 == 0 ? roof : roofPink, trim, null);
            }
        }

        private static void Wall(Transform parent, Vector3 basePosition, float width, float height, Material body, Material trim)
        {
            var wall = new GameObject("Wall").transform;
            wall.SetParent(parent, false);
            wall.localPosition = basePosition;
            Piece(wall, PrimitiveType.Cube, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 1.4f), body);
            Piece(wall, PrimitiveType.Cube, "Trim", new Vector3(0f, height + 0.1f, 0f), new Vector3(width + 0.2f, 0.25f, 1.6f), trim);
            var count = Mathf.FloorToInt(width / 1.3f);
            for (var i = 0; i < count; i++)
            {
                var x = -width * 0.5f + 0.65f + i * (width - 1.3f) / Mathf.Max(1, count - 1);
                Piece(wall, PrimitiveType.Cube, $"Merlon{i}", new Vector3(x, height + 0.6f, 0f), new Vector3(0.7f, 0.8f, 1.4f), body);
            }
        }

        private static void Tower(Transform parent, Vector3 basePosition, float radius, float height, Material body, Material roof, Material trim, Material flag)
        {
            var tower = new GameObject("CastleTower").transform;
            tower.SetParent(parent, false);
            tower.localPosition = basePosition;
            Piece(tower, PrimitiveType.Cylinder, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(radius * 2f, height * 0.5f, radius * 2f), body);
            Piece(tower, PrimitiveType.Cylinder, "Band", new Vector3(0f, height * 0.45f, 0f), new Vector3(radius * 2.08f, 0.12f, radius * 2.08f), trim);
            Piece(tower, PrimitiveType.Cylinder, "Rim", new Vector3(0f, height, 0f), new Vector3(radius * 2.4f, 0.2f, radius * 2.4f), trim);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI / 4f;
                Piece(tower, PrimitiveType.Cube, $"Merlon{i}", new Vector3(Mathf.Sin(angle) * radius * 1.05f, height + 0.45f, Mathf.Cos(angle) * radius * 1.05f), new Vector3(radius * 0.5f, 0.6f, radius * 0.5f), body, Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f));
            }

            Cone(tower, "Roof", new Vector3(0f, height + 0.2f, 0f), radius * 1.25f, radius * 2.4f, roof);
            Piece(tower, PrimitiveType.Sphere, "Finial", new Vector3(0f, height + 0.2f + radius * 2.4f, 0f), Vector3.one * radius * 0.3f, trim);
            Piece(tower, PrimitiveType.Cube, "Window", new Vector3(0f, height * 0.7f, radius * 0.98f), new Vector3(radius * 0.5f, radius * 0.8f, 0.1f), M("Window", new Color32(40, 40, 110, 255)));
            if (flag != null)
            {
                var top = height + 0.2f + radius * 2.4f;
                Piece(tower, PrimitiveType.Cylinder, "Pole", new Vector3(0f, top + 0.7f, 0f), new Vector3(0.08f, 0.7f, 0.08f), trim);
                Piece(tower, PrimitiveType.Cube, "Flag", new Vector3(0.45f, top + 1.15f, 0f), new Vector3(0.9f, 0.5f, 0.05f), flag);
            }
        }

        private static void CrownArch(Transform root, Vector3 basePosition)
        {
            var purple = M("ArchPurple", new Color32(150, 80, 240, 255));
            var gold = M("CrownGold", new Color32(255, 200, 45, 255));
            var ruby = M("CrownRuby", new Color32(255, 60, 100, 255));
            var sapphire = M("CrownSapphire", new Color32(70, 160, 255, 255));
            var arch = new GameObject("CrownArch").transform;
            arch.SetParent(root, false);
            arch.localPosition = basePosition;
            arch.localRotation = Quaternion.Euler(0f, -12f, 0f);
            const float span = 2.6f;
            const float pillarHeight = 2.8f;
            Piece(arch, PrimitiveType.Cube, "PillarL", new Vector3(-span, pillarHeight * 0.5f, 0f), new Vector3(0.9f, pillarHeight, 0.9f), purple);
            Piece(arch, PrimitiveType.Cube, "PillarR", new Vector3(span, pillarHeight * 0.5f, 0f), new Vector3(0.9f, pillarHeight, 0.9f), purple);
            Piece(arch, PrimitiveType.Cube, "CapL", new Vector3(-span, pillarHeight + 0.05f, 0f), new Vector3(1.1f, 0.2f, 1.1f), gold);
            Piece(arch, PrimitiveType.Cube, "CapR", new Vector3(span, pillarHeight + 0.05f, 0f), new Vector3(1.1f, 0.2f, 1.1f), gold);
            for (var i = 0; i <= 18; i++)
            {
                var a = Mathf.PI * i / 18f;
                var p = new Vector3(-Mathf.Cos(a) * span, pillarHeight + Mathf.Sin(a) * span * 0.75f, 0f);
                var tangent = Quaternion.Euler(0f, 0f, Mathf.Atan2(Mathf.Cos(a) * 0.75f, Mathf.Sin(a)) * Mathf.Rad2Deg);
                Piece(arch, PrimitiveType.Cube, $"Arc{i}", p, new Vector3(0.62f, 0.9f, 0.9f), purple, tangent);
                if (i % 3 == 1)
                {
                    Piece(arch, PrimitiveType.Cube, $"Band{i}", p, new Vector3(0.2f, 0.96f, 0.96f), gold, tangent);
                }
            }

            // Giant crown on top.
            var crown = new GameObject("GiantCrown").transform;
            crown.SetParent(arch, false);
            crown.localPosition = new Vector3(0f, pillarHeight + span * 0.75f + 0.55f, 0f);
            const float r = 1.25f;
            Piece(crown, PrimitiveType.Cylinder, "Band", new Vector3(0f, 0.35f, 0f), new Vector3(r * 2f, 0.32f, r * 2f), gold);
            for (var i = 0; i < 7; i++)
            {
                var a = i * Mathf.PI * 2f / 7f;
                var p = new Vector3(Mathf.Sin(a) * r * 0.86f, 0.7f, Mathf.Cos(a) * r * 0.86f);
                Cone(crown, $"Spike{i}", p, 0.34f, 0.95f, gold);
                Piece(crown, PrimitiveType.Sphere, $"Pearl{i}", p + Vector3.up * 1f, Vector3.one * 0.27f, gold);
                Piece(crown, PrimitiveType.Sphere, $"Gem{i}", new Vector3(Mathf.Sin(a) * r * 1.0f, 0.35f, Mathf.Cos(a) * r * 1.0f), Vector3.one * 0.28f, i % 2 == 0 ? ruby : sapphire);
            }

            crown.gameObject.AddComponent<FloatingDecor>();
        }

        /// <summary>A bluer sky and haze for the lobby (the shared look stays for the maps).</summary>
        private static void LobbySky()
        {
            var shader = Shader.Find("KnockRush/Sky");
            var sky = EditorAssetUtility.LoadOrCreate($"{UiContentBuilder.MenuMaterialsFolder}/LobbySky.mat", () => new Material(shader));
            sky.shader = shader;
            sky.SetColor("_ZenithColor", new Color(0.09f, 0.42f, 0.98f));
            sky.SetColor("_HorizonColor", new Color(0.6f, 0.84f, 1f));
            sky.SetColor("_GroundColor", new Color(0.55f, 0.8f, 1f));
            sky.SetFloat("_HorizonSharpness", 3.2f);
            sky.SetFloat("_CloudAmount", 0.6f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.fogColor = new Color(0.66f, 0.84f, 1f);
        }

        private static void Sky(Transform root)
        {
            var group = new GameObject("Sky").transform;
            group.SetParent(root, false);
            WorldLookBuilder.Balloon(group, new Vector3(-5.2f, 6.4f, -22f), 2f, 1);
            WorldLookBuilder.Balloon(group, new Vector3(-10.5f, 9.5f, -36f), 3f, 0);
            WorldLookBuilder.Balloon(group, new Vector3(-2.5f, 12f, -46f), 2.8f, 2);
            WorldLookBuilder.Balloon(group, new Vector3(6.5f, 9f, -32f), 2.4f, 0);
            WorldLookBuilder.Balloon(group, new Vector3(14f, 12.5f, -46f), 3.4f, 2);
            WorldLookBuilder.Balloon(group, new Vector3(-19f, 14f, -56f), 3.8f, 1);
            WorldLookBuilder.Balloon(group, new Vector3(3f, 15f, -60f), 3.2f, 0);
        }

        // -----------------------------------------------------------------------------------------
        private static void Crowd(Transform root, BeanAssets bean)
        {
            var group = new GameObject("Crowd").transform;
            group.SetParent(root, false);
            Loadout Outfit(string body, string color, string pattern, string hat) => new Loadout { Body = body, Color = color, Pattern = pattern, Hat = hat, Emote = "emote_wave" };

            // Positions in camera terms: −X is screen-right (the mirrored fortress side).
            CrowdBean(group, bean, "BlueCrown", new Vector3(-2.75f, FloorY, -3.2f), 16f, Outfit("default_bean", "color_ocean", "pattern_none", "hat_crown"), MenuCrowdBean.Behaviour.Idle, Vector3.zero, 1);
            CrowdBean(group, bean, "PinkBunny", new Vector3(2.85f, FloorY, -4.4f), -16f, Outfit("body_bunny", "color_bubblegum", "pattern_hearts", "hat_none"), MenuCrowdBean.Behaviour.Idle, Vector3.zero, 2);
            CrowdBean(group, bean, "SunnyProp", new Vector3(-1.6f, FloorY, -8.2f), 8f, Outfit("default_bean", "color_sunny", "pattern_none", "hat_propeller"), MenuCrowdBean.Behaviour.Idle, Vector3.zero, 3);
            CrowdBean(group, bean, "Dino", new Vector3(1.6f, FloorY, -10f), 10f, Outfit("body_dino", "color_lime", "pattern_none", "hat_none"), MenuCrowdBean.Behaviour.Jog, new Vector3(4.4f, FloorY, -12.5f), 4);
            CrowdBean(group, bean, "RunnerA", new Vector3(-6.2f, FloorY, -11f), 180f, Outfit("default_bean", "color_cherry", "pattern_stripes", "hat_cap"), MenuCrowdBean.Behaviour.Jog, new Vector3(-8.4f, FloorY, -16.5f), 5);
            CrowdBean(group, bean, "RunnerB", new Vector3(-9.5f, FloorY, -15f), 0f, Outfit("body_robot", "color_snow", "pattern_none", "hat_none"), MenuCrowdBean.Behaviour.Jog, new Vector3(-6.8f, FloorY, -9.5f), 6);
            CrowdBean(group, bean, "ArchFan", new Vector3(-5.4f, FloorY, -19.5f), -20f, Outfit("body_cat", "color_lavender", "pattern_dots", "hat_none"), MenuCrowdBean.Behaviour.Idle, Vector3.zero, 7);
            CrowdBean(group, bean, "ArchRunner", new Vector3(-10f, FloorY, -18.5f), 40f, Outfit("default_bean", "color_mint", "pattern_zigzag", "hat_party"), MenuCrowdBean.Behaviour.Jog, new Vector3(-12.8f, FloorY, -15f), 8);
        }

        public static (Transform root, Animator animator, CosmeticApplier cosmetics) SpawnBean(Transform parent, BeanAssets assets, string name, Vector3 position, float yaw)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root, false);
            var bean = (GameObject)PrefabUtility.InstantiatePrefab(assets.Model);
            bean.name = "Bean";
            bean.transform.SetParent(visual.transform, false);
            bean.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // model faces −Z after import
            var renderer = bean.GetComponentInChildren<SkinnedMeshRenderer>();
            renderer.sharedMaterial = assets.Material;
            renderer.updateWhenOffscreen = true;
            var animator = bean.GetComponent<Animator>() ?? bean.AddComponent<Animator>();
            animator.runtimeAnimatorController = assets.Controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var cosmetics = visual.AddComponent<CosmeticApplier>();
            EditorAssetUtility.SetField(cosmetics, "bodyRenderer", renderer);
            return (root, animator, cosmetics);
        }

        private static void CrowdBean(Transform parent, BeanAssets assets, string name, Vector3 position, float yaw, Loadout outfit, MenuCrowdBean.Behaviour behaviour, Vector3 pathEnd, int seed)
        {
            var (root, animator, cosmetics) = SpawnBean(parent, assets, name, position, yaw);
            var crowd = root.gameObject.AddComponent<MenuCrowdBean>();
            crowd.Configure(animator, cosmetics, behaviour, outfit, pathEnd, seed);
            cosmetics.Apply(outfit); // editor preview
        }

        // -----------------------------------------------------------------------------------------
        public static Texture2D ImportTexture(string path, bool mipmaps, bool compress)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                return null;
            }

            var compression = compress ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            if (importer.mipmapEnabled != mipmaps || !importer.alphaIsTransparency || importer.textureCompression != compression || importer.wrapMode != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = mipmaps;
                importer.alphaIsTransparency = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = compression;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material Unlit(string name, Texture texture, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = EditorAssetUtility.LoadOrCreate($"{UiContentBuilder.MenuMaterialsFolder}/{name}.mat", () => new Material(shader));
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh EnsureCone()
        {
            KnockRushProjectSetup.EnsureFolder(MeshFolder);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ConeMeshPath);
            if (mesh != null)
            {
                return mesh;
            }

            // Unit cone: base radius 1 at y = 0, apex at y = 1, flat-shaded-ish smooth sides + cap.
            const int segments = 24;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            var slope = new Vector2(1f, 1f).normalized;
            for (var i = 0; i < segments; i++)
            {
                var a0 = i * Mathf.PI * 2f / segments;
                var a1 = (i + 1) * Mathf.PI * 2f / segments;
                var am = (a0 + a1) * 0.5f;
                var start = vertices.Count;
                vertices.Add(new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)));
                vertices.Add(new Vector3(0f, 1f, 0f));
                vertices.Add(new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)));
                normals.Add(new Vector3(Mathf.Sin(a0) * slope.x, slope.y, Mathf.Cos(a0) * slope.x));
                normals.Add(new Vector3(Mathf.Sin(am) * slope.x, slope.y, Mathf.Cos(am) * slope.x));
                normals.Add(new Vector3(Mathf.Sin(a1) * slope.x, slope.y, Mathf.Cos(a1) * slope.x));
                triangles.Add(start); // clockwise seen from outside (Unity front face)
                triangles.Add(start + 2);
                triangles.Add(start + 1);
            }

            var center = vertices.Count;
            vertices.Add(Vector3.zero);
            normals.Add(Vector3.down);
            for (var i = 0; i < segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)));
                normals.Add(Vector3.down);
            }

            for (var i = 0; i < segments; i++)
            {
                triangles.Add(center);
                triangles.Add(center + 1 + (i + 1) % segments);
                triangles.Add(center + 1 + i);
            }

            mesh = new Mesh { name = "Cone" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, ConeMeshPath);
            return mesh;
        }
    }
}
