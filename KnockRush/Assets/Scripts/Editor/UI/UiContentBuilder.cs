using KnockRush.EditorTools.Characters;
using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Setup;
using KnockRush.Inventory;
using KnockRush.UI.Framework;
using KnockRush.UI.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace KnockRush.EditorTools.UI
{
    /// <summary>
    /// Builds the Phase 8 UI content: PanelSettings (1920×1080 reference, scales on any phone),
    /// the UiResources asset (theme + fonts) and the MainMenu scene with the 3D bean stage.
    /// </summary>
    public static class UiContentBuilder
    {
        public const string ThemePath = "Assets/Art/UI/KnockRush.uss";
        public const string RuntimeThemePath = "Assets/Art/UI/KnockRushRuntimeTheme.tss";
        public const string DisplayFontPath = "Assets/Art/UI/Fonts/LilitaOne-Regular.ttf";
        public const string BodyFontPath = "Assets/Art/UI/Fonts/Nunito-Variable.ttf";
        public const string LoadingBackgroundPath = "Assets/Art/UI/KeyArt/LoadingBackground.png";
        public const string AppIconPath = "Assets/Art/Icons/AppIcon.png";
        public const string AdaptiveBackgroundPath = "Assets/Art/Icons/AppIcon_AdaptiveBackground.png";
        public const string AdaptiveForegroundPath = "Assets/Art/Icons/AppIcon_AdaptiveForeground.png";
        public const string PanelSettingsPath = "Assets/Settings/UI/KnockRushPanelSettings.asset";
        public const string ResourcesFolder = "Assets/ScriptableObjects/UI/Resources";
        public const string UiResourcesPath = ResourcesFolder + "/UiResources.asset";
        public const string MenuMaterialsFolder = "Assets/Art/Materials/Menu";

        [MenuItem("KnockRush/Build/UI and Main Menu", priority = 230)]
        public static void BuildFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Build();
            }
        }

        /// <summary>Batch entry: Tools/unity.sh exec KnockRush.EditorTools.UI.UiContentBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            BuildUiAssets();
            BuildMainMenuScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[KnockRush Build] UI assets and MainMenu scene built.");
        }

        public static UiResources BuildUiAssets()
        {
            KnockRushProjectSetup.EnsureFolder("Assets/Settings/UI");
            KnockRushProjectSetup.EnsureFolder(ResourcesFolder);
            AssetDatabase.ImportAsset(RuntimeThemePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceUpdate);

            var panel = EditorAssetUtility.LoadOrCreate(PanelSettingsPath, ScriptableObject.CreateInstance<PanelSettings>);
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(RuntimeThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 10;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);

            var loadingBackground = AppBrandingBuilder.ImportLoadingBackground(LoadingBackgroundPath);
            var resources = EditorAssetUtility.LoadOrCreate(UiResourcesPath, ScriptableObject.CreateInstance<UiResources>);
            resources.Configure(
                panel,
                AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemePath),
                AssetDatabase.LoadAssetAtPath<Font>(DisplayFontPath),
                AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath),
                loadingBackground);
            AppBrandingBuilder.ApplyIconsAndSplash(AppIconPath, AdaptiveBackgroundPath, AdaptiveForegroundPath, loadingBackground);
            EditorUtility.SetDirty(resources);

            if (panel.themeStyleSheet == null || resources.Theme == null || resources.DisplayFont == null || resources.BodyFont == null || resources.LoadingBackground == null)
            {
                throw new System.InvalidOperationException("UI assets missing (theme, runtime theme or fonts under Assets/Art/UI).");
            }

            return resources;
        }

        /// <summary>The menu backdrop: the player's bean on a round podium with floating shapes.</summary>
        public static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            KnockRushProjectSetup.EnsureFolder(MenuMaterialsFolder);
            var podiumMat = EditorAssetUtility.CreateToonMaterial(MenuMaterialsFolder + "/Podium.mat", new Color32(255, 106, 26, 255), false);
            var podiumRimMat = EditorAssetUtility.CreateToonMaterial(MenuMaterialsFolder + "/PodiumRim.mat", new Color32(255, 209, 43, 255), false);
            var floorMat = EditorAssetUtility.CreateLevelMaterial(MenuMaterialsFolder + "/Floor.mat", new Color32(70, 205, 140, 255));
            Color[] shapeColors =
            {
                new Color32(34, 197, 139, 255), new Color32(255, 79, 154, 255), new Color32(255, 209, 43, 255), new Color32(91, 46, 255, 255), new Color32(45, 156, 255, 255)
            };
            var shapeMats = new Material[shapeColors.Length];
            for (var i = 0; i < shapeColors.Length; i++)
            {
                shapeMats[i] = EditorAssetUtility.CreateToonMaterial($"{MenuMaterialsFolder}/Shape{i}.mat", shapeColors[i], false);
            }

            // Assets reloaded by path after NewScene (it unloads unused assets).
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetBuilder.BeanModelPath);
            var beanMaterial = AssetDatabase.LoadAssetAtPath<Material>(CharacterAssetBuilder.BeanMaterialPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterAssetBuilder.AnimatorPath);
            if (model == null || beanMaterial == null || controller == null)
            {
                throw new System.InvalidOperationException("Bean model/material/controller missing: build character assets first.");
            }

            // Camera: slightly low, looking up at the hero bean.
            var camera = Object.FindFirstObjectByType<Camera>();
            var rig = new GameObject("StageCameraRig").transform;
            camera.transform.SetParent(rig, false);
            camera.transform.localPosition = new Vector3(0f, 1.25f, 5.2f);
            camera.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0.95f, 0f) - camera.transform.localPosition);
            camera.fieldOfView = 32f;
            Scenes.WorldLookBuilder.ConfigureCamera(camera);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;

            SceneBuildKitFacade.ConfigureLighting(Quaternion.Euler(42f, 150f, 0f),
                new Color32(205, 225, 255, 255), new Color32(190, 205, 235, 255), new Color32(120, 120, 160, 255), 1.2f);
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 140f;

            var stageRoot = new GameObject("MenuStage");
            var floor = Primitive(PrimitiveType.Cylinder, "Floor", stageRoot.transform, new Vector3(0f, -0.62f, -6f), new Vector3(40f, 0.5f, 40f), floorMat);
            floor.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Primitive(PrimitiveType.Cylinder, "PodiumRim", stageRoot.transform, new Vector3(0f, -0.2f, 0f), new Vector3(2.5f, 0.2f, 2.5f), podiumRimMat);
            Primitive(PrimitiveType.Cylinder, "Podium", stageRoot.transform, new Vector3(0f, -0.08f, 0f), new Vector3(2.2f, 0.12f, 2.2f), podiumMat);

            var random = new System.Random(42);
            for (var i = 0; i < 8; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var position = new Vector3(side * (4.5f + (float)random.NextDouble() * 7f), 1f + (float)random.NextDouble() * 4f, -8f - (float)random.NextDouble() * 14f);
                var size = 0.35f + (float)random.NextDouble() * 0.6f;
                var type = PrimitiveType.Sphere;
                var shape = Primitive(type, $"Shape{i}", stageRoot.transform, position, Vector3.one * size, shapeMats[i % shapeMats.Length]);
                shape.transform.rotation = Quaternion.Euler(random.Next(360), random.Next(360), random.Next(360));
                shape.AddComponent<KnockRush.Maps.FloatingDecor>();
            }

            // Festival backdrop (balloons + crown banner towers) like the key art.
            Scenes.WorldLookBuilder.Balloon(stageRoot.transform, new Vector3(-6.2f, 3.6f, -18f), 1.7f, 0);
            Scenes.WorldLookBuilder.Balloon(stageRoot.transform, new Vector3(6.8f, 4.4f, -22f), 2.1f, 1);
            Scenes.WorldLookBuilder.Balloon(stageRoot.transform, new Vector3(-11.5f, 5.2f, -32f), 2.4f, 2);
            Scenes.WorldLookBuilder.Balloon(stageRoot.transform, new Vector3(12.5f, 5.8f, -36f), 2.6f, 3);
            Scenes.WorldLookBuilder.Tower(stageRoot.transform, new Vector3(-9.5f, -0.4f, -32f), 11f, -1f, 0.3f);
            Scenes.WorldLookBuilder.Tower(stageRoot.transform, new Vector3(10.5f, -0.4f, -36f), 13f, 1f, 0.3f);

            // The bean.
            var turntable = new GameObject("Turntable").transform;
            turntable.SetParent(stageRoot.transform, false);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(turntable, false);
            var bean = (GameObject)PrefabUtility.InstantiatePrefab(model);
            bean.name = "Bean";
            bean.transform.SetParent(visual.transform, false);
            bean.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // model faces −Z after import
            var renderer = bean.GetComponentInChildren<SkinnedMeshRenderer>();
            renderer.sharedMaterial = beanMaterial;
            renderer.updateWhenOffscreen = true;
            var animator = bean.GetComponent<Animator>() ?? bean.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var cosmetics = visual.AddComponent<CosmeticApplier>();
            EditorAssetUtility.SetField(cosmetics, "bodyRenderer", renderer);

            var stage = stageRoot.AddComponent<MenuBeanStage>();
            EditorAssetUtility.SetField(stage, "turntable", turntable);
            EditorAssetUtility.SetField(stage, "animator", animator);
            EditorAssetUtility.SetField(stage, "cosmetics", cosmetics);
            EditorAssetUtility.SetField(stage, "stageCamera", camera);

            // Editor preview of the default outfit (play mode re-applies the stored loadout).
            cosmetics.Apply(Loadout.Default);

            EditorSceneManager.SaveScene(scene, KnockRushProjectSetup.MainMenuScenePath);
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }

    internal static class SceneBuildKitFacade
    {
        public static void ConfigureLighting(Quaternion sun, Color sky, Color equator, Color ground, float intensity)
        {
            Scenes.SceneBuildKit.ConfigureLighting(sun, sky, equator, ground, intensity);
        }
    }
}
