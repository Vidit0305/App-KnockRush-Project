using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Setup;
using KnockRush.Maps;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KnockRush.EditorTools.Scenes
{
    /// <summary>
    /// The shared KnockRush "look" applied to every map, the playground and the lobby: bright gradient
    /// sky with clouds, haze, a global post-processing volume (bloom, tone mapping, colour boost,
    /// vignette) and festival decor (hot-air balloons, banner towers, a checkered finish arch).
    /// </summary>
    public static class WorldLookBuilder
    {
        public const string Folder = "Assets/Art/World";
        public const string SkyMaterialPath = Folder + "/Sky.mat";
        public const string VolumeProfilePath = Folder + "/KnockRushLook.asset";

        private static readonly Color Horizon = new Color(0.78f, 0.93f, 1f);

        /// <summary>Sky, fog and the global post-processing volume for the open scene.</summary>
        public static void Apply()
        {
            KnockRushProjectSetup.EnsureFolder(Folder);

            var sky = EditorAssetUtility.LoadOrCreate(SkyMaterialPath, () => new Material(Shader.Find("KnockRush/Sky")));
            sky.shader = Shader.Find("KnockRush/Sky");
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Horizon;
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 520f;

            var volume = new GameObject("GlobalVolume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = EnsureProfile();
        }

        public static VolumeProfile EnsureProfile()
        {
            var profile = EditorAssetUtility.LoadOrCreate(VolumeProfilePath, ScriptableObject.CreateInstance<VolumeProfile>);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(false);

            Get<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);

            var color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(0.18f);
            color.contrast.Override(14f);
            color.saturation.Override(24f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            component.active = true;
            return component;
        }

        /// <summary>Enables post-processing (and FXAA) on a camera.</summary>
        public static void ConfigureCamera(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        // -------------------------------------------------------------------------------------
        // Festival decor
        // -------------------------------------------------------------------------------------
        internal static Material Mat(string name, Color color, int pattern = 0, Color? patternColor = null, float patternScale = 1f)
        {
            var material = EditorAssetUtility.CreateToonMaterial($"{Folder}/{name}.mat", color, false);
            material.SetFloat("_PatternType", pattern);
            material.SetColor("_PatternColor", patternColor ?? Color.white);
            material.SetFloat("_PatternScale", patternScale);
            material.SetFloat("_RimStrength", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Hot-air balloons drifting around the course and crown banner towers along it.</summary>
        public static void Festival(Transform parent, Bounds course, int seed, float? towerBaseY = null)
        {
            var root = new GameObject("Festival").transform;
            root.SetParent(parent, false);
            var random = new System.Random(seed * 31 + 5);
            float Range(float min, float max) => (float)(min + random.NextDouble() * (max - min));

            for (var i = 0; i < 7; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var position = new Vector3(
                    course.center.x + side * Range(course.extents.x + 18f, course.extents.x + 70f),
                    Range(22f, 48f),
                    Range(course.min.z - 30f, course.max.z + 30f));
                Balloon(root, position, Range(7f, 11f), i);
            }

            var towerCount = Mathf.Clamp(Mathf.RoundToInt(course.size.z / 45f), 2, 6);
            for (var i = 0; i < towerCount; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var z = Mathf.Lerp(course.min.z + 10f, course.max.z - 10f, towerCount == 1 ? 0.5f : i / (towerCount - 1f));
                Tower(root, new Vector3(course.center.x + side * (course.extents.x + Range(12f, 22f)), towerBaseY ?? course.min.y - 18f, z), Range(26f, 38f), side, 1f);
            }
        }

        /// <summary>A striped hot-air balloon that bobs gently.</summary>
        public static Transform Balloon(Transform parent, Vector3 position, float size, int variant)
        {
            Material[] envelopes =
            {
                Mat("Balloon_Orange", new Color32(255, 140, 40, 255), 1, new Color32(255, 214, 60, 255), 2f),
                Mat("Balloon_Purple", new Color32(120, 80, 230, 255), 1, new Color32(255, 160, 60, 255), 2f),
                Mat("Balloon_Pink", new Color32(255, 90, 170, 255), 1, new Color32(255, 240, 250, 255), 2f)
            };
            var basket = Mat("Balloon_Basket", new Color32(150, 95, 55, 255));
            var balloon = new GameObject($"Balloon_{variant}").transform;
            balloon.SetParent(parent, false);
            balloon.localPosition = position;
            SceneBuildKit.Decor(balloon, PrimitiveType.Sphere, "Envelope", Vector3.zero, new Vector3(size, size * 1.15f, size), envelopes[Mathf.Abs(variant) % envelopes.Length]);
            SceneBuildKit.Decor(balloon, PrimitiveType.Cylinder, "Basket", new Vector3(0f, -size * 0.85f, 0f), new Vector3(size * 0.25f, size * 0.12f, size * 0.25f), basket);
            balloon.gameObject.AddComponent<FloatingDecor>();
            return balloon;
        }

        /// <summary>A purple banner tower with a gold rim, striped pole and crown flag.</summary>
        public static Transform Tower(Transform parent, Vector3 basePosition, float height, float side, float scale)
        {
            var towerBody = Mat("Tower_Body", new Color32(92, 64, 220, 255));
            var towerRim = Mat("Tower_Rim", new Color32(255, 170, 30, 255));
            var pole = Mat("Tower_Pole", new Color32(240, 240, 255, 255), 1, new Color32(60, 110, 230, 255), 3f);
            var banner = Mat("Tower_Banner", new Color32(60, 90, 220, 255));
            var crown = Mat("Crown_Gold", new Color32(255, 205, 40, 255));

            var tower = new GameObject("BannerTower").transform;
            tower.SetParent(parent, false);
            tower.localPosition = basePosition;
            tower.localScale = Vector3.one * scale;
            SceneBuildKit.Decor(tower, PrimitiveType.Cylinder, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(7f, height * 0.5f, 7f), towerBody);
            SceneBuildKit.Decor(tower, PrimitiveType.Cylinder, "Rim", new Vector3(0f, height, 0f), new Vector3(8.4f, 0.9f, 8.4f), towerRim);
            SceneBuildKit.Decor(tower, PrimitiveType.Cylinder, "Pole", new Vector3(0f, height + 5f, 0f), new Vector3(0.6f, 5f, 0.6f), pole);
            SceneBuildKit.Decor(tower, PrimitiveType.Cube, "Banner", new Vector3(side * -2.2f, height + 7f, 0f), new Vector3(4f, 3.4f, 0.25f), banner);
            AddCrown(tower, new Vector3(side * -2.2f, height + 7.1f, -0.25f), 1.1f, crown);
            SceneBuildKit.Decor(tower, PrimitiveType.Cube, "Band", new Vector3(0f, height * 0.62f, 0f), new Vector3(7.2f, 1.2f, 7.2f), towerRim, Quaternion.Euler(0f, 45f, 0f));
            return tower;
        }

        /// <summary>A simple three-point crown made of cubes (logo motif).</summary>
        public static void AddCrown(Transform parent, Vector3 position, float size, Material material)
        {
            var crown = new GameObject("Crown").transform;
            crown.SetParent(parent, false);
            crown.localPosition = position;
            SceneBuildKit.Decor(crown, PrimitiveType.Cube, "Base", new Vector3(0f, -0.35f * size, 0f), new Vector3(1.6f * size, 0.5f * size, 0.2f * size), material);
            for (var i = -1; i <= 1; i++)
            {
                SceneBuildKit.Decor(crown, PrimitiveType.Cube, $"Point{i + 1}", new Vector3(i * 0.62f * size, 0.15f * size, 0f), new Vector3(0.42f * size, 0.42f * size, 0.2f * size), material, Quaternion.Euler(0f, 0f, 45f));
            }
        }

        /// <summary>Checkered finish arch over a finish line (pillars, chequered beam, gold crown).</summary>
        public static void FinishArch(Transform parent, Vector3 center, Vector3 size)
        {
            var pillar = Mat("Arch_Pillar", new Color32(70, 100, 235, 255), 1, new Color32(255, 255, 255, 255), 2f);
            var checker = Mat("Arch_Checker", new Color32(30, 30, 40, 255), 6, Color.white, 2.5f);
            var crown = Mat("Crown_Gold", new Color32(255, 205, 40, 255));

            var arch = new GameObject("FinishArch").transform;
            arch.SetParent(parent, false);
            var floorY = center.y - size.y * 0.5f;
            arch.localPosition = new Vector3(center.x, floorY, center.z);
            var halfWidth = size.x * 0.5f + 1f;
            const float height = 8f;
            SceneBuildKit.Decor(arch, PrimitiveType.Cube, "PillarL", new Vector3(-halfWidth, height * 0.5f, 0f), new Vector3(1.6f, height, 1.6f), pillar);
            SceneBuildKit.Decor(arch, PrimitiveType.Cube, "PillarR", new Vector3(halfWidth, height * 0.5f, 0f), new Vector3(1.6f, height, 1.6f), pillar);
            SceneBuildKit.Decor(arch, PrimitiveType.Cube, "Beam", new Vector3(0f, height + 0.9f, 0f), new Vector3(halfWidth * 2f + 1.6f, 1.8f, 1.2f), checker);
            AddCrown(arch, new Vector3(0f, height + 2.9f, 0f), 1.6f, crown);
        }
    }
}
