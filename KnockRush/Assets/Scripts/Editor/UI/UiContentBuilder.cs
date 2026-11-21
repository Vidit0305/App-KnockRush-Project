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
using UnityEngine.Rendering.Universal;
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
        public const string LoadingBackgroundPath = "Assets/Art/UI/Backgrounds/LoadingBackground.png";
        public const string AppIconPath = "Assets/Art/Branding/AppIcon.png";
        public const string AdaptiveBackgroundPath = "Assets/Art/Branding/AppIcon_AdaptiveBackground.png";
        public const string AdaptiveForegroundPath = "Assets/Art/Branding/AppIcon_AdaptiveForeground.png";
        public const string LogoPath = "Assets/Art/Branding/Logo.png";
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
            ImportUiTextures();
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
            AppBrandingBuilder.ApplyIconsAndSplash(AppIconPath, AdaptiveBackgroundPath, AdaptiveForegroundPath);
            EditorUtility.SetDirty(resources);

            if (panel.themeStyleSheet == null || resources.Theme == null || resources.DisplayFont == null || resources.BodyFont == null || resources.LoadingBackground == null)
            {
                throw new System.InvalidOperationException("UI assets missing (theme, runtime theme or fonts under Assets/Art/UI).");
            }

            return resources;
        }

        /// <summary>Icons (mipmapped, compressed) and 9-slice skins (crisp, uncompressed) used by the theme.</summary>
        private static void ImportUiTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/UI/Icons" }))
            {
                MenuStageBuilder.ImportTexture(AssetDatabase.GUIDToAssetPath(guid), true, true, 256);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/UI/Skins" }))
            {
                MenuStageBuilder.ImportTexture(AssetDatabase.GUIDToAssetPath(guid), false, false, 256);
            }

            MenuStageBuilder.ImportTexture(LogoPath, true, true, 1024);
        }

        /// <summary>The menu stage: the player's bean in front of the painted arena backdrop.</summary>
        public static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            KnockRushProjectSetup.EnsureFolder(MenuMaterialsFolder);

            // Assets reloaded by path after NewScene (it unloads unused assets).
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetBuilder.BeanModelPath);
            var beanMaterial = AssetDatabase.LoadAssetAtPath<Material>(CharacterAssetBuilder.BeanMaterialPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterAssetBuilder.AnimatorPath);
            if (model == null || beanMaterial == null || controller == null)
            {
                throw new System.InvalidOperationException("Bean model/material/controller missing: build character assets first.");
            }

            var beanAssets = new MenuStageBuilder.BeanAssets { Model = model, Material = beanMaterial, Controller = controller };

            // Camera: the hero bean centre-bottom, the festival arena filling the rest of the frame.
            var camera = Object.FindFirstObjectByType<Camera>();
            var rig = new GameObject("StageCameraRig").transform;
            camera.transform.SetParent(rig, false);
            camera.transform.localPosition = new Vector3(0f, 0.95f, 4.9f);
            camera.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 1.02f, 0f) - camera.transform.localPosition);
            camera.fieldOfView = 38f;
            Scenes.WorldLookBuilder.ConfigureCamera(camera);
            // The painted backdrop is final art: no post-processing tint, no sky behind it.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(70, 150, 245, 255);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 140f;

            SceneBuildKitFacade.ConfigureLighting(Quaternion.Euler(40f, 155f, 0f),
                new Color32(205, 225, 255, 255), new Color32(200, 205, 240, 255), new Color32(140, 120, 190, 255), 1.2f);
            RenderSettings.fog = false;

            var stageRoot = new GameObject("MenuStage");
            var backdrop = MenuStageBuilder.Build(stageRoot.transform, camera);

            // The hero bean.
            var (heroRoot, animator, cosmetics) = MenuStageBuilder.SpawnBean(stageRoot.transform, beanAssets, "Turntable", Vector3.zero, 0f);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorAssetUtility.SetLayerRecursively(heroRoot.gameObject, MenuBeanStage.PortraitLayer);

            // Live portrait: head and shoulders of the hero (Home avatar).
            var portrait = PortraitCamera("PortraitCamera", heroRoot, new Vector3(0f, 1.2f, 1.55f), new Vector3(0f, 1.12f, 0f), 30f);

            // Season showcase bean far behind the main camera, filmed for the Home season card.
            var (seasonRoot, seasonAnimator, seasonCosmetics) = MenuStageBuilder.SpawnBean(stageRoot.transform, beanAssets, "SeasonModel", new Vector3(40f, 0f, 40f), 0f);
            seasonAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorAssetUtility.SetLayerRecursively(seasonRoot.gameObject, MenuBeanStage.PortraitLayer);
            var seasonCamera = PortraitCamera("SeasonCamera", seasonRoot, new Vector3(0f, 1.05f, 3.9f), new Vector3(0f, 0.98f, 0f), 32f);

            var stage = stageRoot.AddComponent<MenuBeanStage>();
            EditorAssetUtility.SetField(stage, "turntable", heroRoot);
            EditorAssetUtility.SetField(stage, "animator", animator);
            EditorAssetUtility.SetField(stage, "cosmetics", cosmetics);
            EditorAssetUtility.SetField(stage, "stageCamera", camera);
            EditorAssetUtility.SetField(stage, "portraitCamera", portrait);
            EditorAssetUtility.SetField(stage, "seasonCamera", seasonCamera);
            EditorAssetUtility.SetField(stage, "seasonCosmetics", seasonCosmetics);
            EditorAssetUtility.SetField(stage, "seasonAnimator", seasonAnimator);
            EditorAssetUtility.SetField(stage, "backdrop", backdrop);
            EditorAssetUtility.SetField(stage, "homeBackdrop", MenuStageBuilder.HomeBackdrop);
            EditorAssetUtility.SetField(stage, "lockerBackdrop", MenuStageBuilder.LockerBackdrop);

            // Editor preview of the default outfit (play mode re-applies the stored loadout).
            cosmetics.Apply(Loadout.Default);

            EditorSceneManager.SaveScene(scene, KnockRushProjectSetup.MainMenuScenePath);
        }

        private static Camera PortraitCamera(string name, Transform parent, Vector3 position, Vector3 lookAt, float fieldOfView)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.LookRotation(lookAt - position);
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.63f, 0.88f, 1f, 0f);
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 8f;
            camera.cullingMask = 1 << MenuBeanStage.PortraitLayer;
            camera.depth = -5;
            camera.enabled = false; // MenuBeanStage turns it on (with a render texture) while Home is up
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            return camera;
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
