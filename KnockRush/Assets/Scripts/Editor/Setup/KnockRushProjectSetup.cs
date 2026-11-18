using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KnockRush.Core.Config;
using KnockRush.Core.SceneManagement;
using KnockRush.UI.Placeholders;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KnockRush.EditorTools.Setup
{
    /// <summary>
    /// One-click, repeatable Phase 1 project setup. Every step is idempotent: running it
    /// again only fixes what is missing or wrong. Menu: KnockRush > Setup.
    /// </summary>
    public static class KnockRushProjectSetup
    {
        // =====================================================================
        // PROJECT IDENTITY: edit these BEFORE running step 3.
        // The application identifier is permanent once the app is on the stores
        // and is also baked into the Firebase config files in Phase 7.
        // =====================================================================
        public const string CompanyName = "KnockRush Studio";
        public const string ProductName = "KnockRush";
        public const string ApplicationIdentifier = "com.knockrushstudio.knockrush";
        public const string InitialVersion = "0.1.0";

        public const string ScenesFolder = "Assets/Scenes";
        public const string BootScenePath = ScenesFolder + "/Boot.unity";
        public const string MainMenuScenePath = ScenesFolder + "/MainMenu.unity";
        public const string ConfigResourcesFolder = "Assets/ScriptableObjects/Config/Resources";
        public const string GameConfigPath = ConfigResourcesFolder + "/GameConfig.asset";

        private const string Menu = "KnockRush/Setup/";

        // Brand colours used for placeholder scene backgrounds.
        private static readonly Color BootBackground = new Color32(0x5B, 0x2E, 0xFF, 0xFF);     // Knock Purple
        private static readonly Color MainMenuBackground = new Color32(0x1E, 0x14, 0x5C, 0xFF); // Deep Night

        /// <summary>Packages required at the end of Phase 1. Versions are resolved by the Package Manager for this editor.</summary>
        public static readonly string[] RequiredPackages =
        {
            "com.unity.render-pipelines.universal",
            "com.unity.inputsystem",
            "com.unity.addressables",
            "com.unity.test-framework"
        };

        public static readonly string[] RequiredFolders =
        {
            "Assets/AddressableAssets",
            "Assets/Animations/Characters",
            "Assets/Art/Characters",
            "Assets/Art/Environments",
            "Assets/Art/Obstacles",
            "Assets/Art/UI",
            "Assets/Art/VFX",
            "Assets/Audio/Music",
            "Assets/Audio/SFX",
            "Assets/Materials",
            "Assets/Prefabs/Characters",
            "Assets/Prefabs/Maps",
            "Assets/Prefabs/Obstacles",
            "Assets/Prefabs/UI",
            "Assets/Scenes/Maps",
            "Assets/ScriptableObjects/Config/Resources",
            "Assets/ScriptableObjects/Cosmetics",
            "Assets/ScriptableObjects/GameModes",
            "Assets/ScriptableObjects/Maps",
            "Assets/Scripts/Audio",
            "Assets/Scripts/Backend",
            "Assets/Scripts/Camera",
            "Assets/Scripts/Core",
            "Assets/Scripts/Economy",
            "Assets/Scripts/Editor",
            "Assets/Scripts/GameModes",
            "Assets/Scripts/Inventory",
            "Assets/Scripts/Maps",
            "Assets/Scripts/Matchmaking",
            "Assets/Scripts/Movement",
            "Assets/Scripts/Multiplayer",
            "Assets/Scripts/Obstacles",
            "Assets/Scripts/Physics",
            "Assets/Scripts/Player",
            "Assets/Scripts/UI",
            "Assets/Scripts/Utilities",
            "Assets/Settings",
            "Assets/Tests/EditMode",
            "Assets/Tests/PlayMode",
            "Assets/Textures"
        };

        // ------------------------------------------------------------------
        // 1. Packages
        // ------------------------------------------------------------------
        [MenuItem(Menu + "1. Install Required Packages", priority = 1)]
        public static void InstallRequiredPackages()
        {
            var missing = RequiredPackages.Where(name => !IsPackageInstalled(name)).ToArray();
            if (missing.Length == 0)
            {
                EditorUtility.DisplayDialog("KnockRush Setup", "All required packages are already installed.", "OK");
                return;
            }

            Debug.Log($"[KnockRush Setup] Installing packages: {string.Join(", ", missing)}. Unity will recompile when finished.");
            var request = Client.AddAndRemove(missing, null);

            void Poll()
            {
                if (!request.IsCompleted)
                {
                    return;
                }

                EditorApplication.update -= Poll;
                if (request.Status == StatusCode.Success)
                {
                    Debug.Log("[KnockRush Setup] Packages installed successfully.");
                }
                else
                {
                    Debug.LogError($"[KnockRush Setup] Package installation failed: {request.Error?.message}");
                }
            }

            EditorApplication.update += Poll;
        }

        public static bool IsPackageInstalled(string packageName)
        {
            return UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .Any(package => package.name == packageName);
        }

        // ------------------------------------------------------------------
        // 2. Folders
        // ------------------------------------------------------------------
        [MenuItem(Menu + "2. Create Folder Structure", priority = 2)]
        public static void CreateFolderStructure()
        {
            foreach (var folder in RequiredFolders)
            {
                EnsureFolder(folder);
            }

            AssetDatabase.Refresh();
            Debug.Log($"[KnockRush Setup] Folder structure verified ({RequiredFolders.Length} folders).");
        }

        /// <summary>
        /// Creates a folder (and parents) inside Assets. Adds a hidden .gitkeep to empty folders
        /// so Git keeps them (otherwise Unity sees orphaned .meta files after a fresh clone).
        /// </summary>
        public static void EnsureFolder(string assetPath)
        {
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }

            var absolute = Path.GetFullPath(assetPath);
            if (Directory.Exists(absolute) && !Directory.EnumerateFileSystemEntries(absolute).Any())
            {
                File.WriteAllText(Path.Combine(absolute, ".gitkeep"), string.Empty);
            }
        }

        // ------------------------------------------------------------------
        // 3. Player / Editor settings
        // ------------------------------------------------------------------
        [MenuItem(Menu + "3. Apply Recommended Player Settings", priority = 3)]
        // ------------------------------------------------------------------
        // Physics layers (see KnockRush.Core.GameLayers)
        // ------------------------------------------------------------------
        public static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            void SetLayer(int index, string name)
            {
                var property = layers.GetArrayElementAtIndex(index);
                if (property.stringValue == name)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(property.stringValue))
                {
                    Debug.LogWarning($"[KnockRush Setup] Layer {index} was '{property.stringValue}', renaming to '{name}'.");
                }

                property.stringValue = name;
            }

            SetLayer(KnockRush.Core.GameLayers.Player, KnockRush.Core.GameLayers.PlayerName);
            SetLayer(KnockRush.Core.GameLayers.Obstacle, KnockRush.Core.GameLayers.ObstacleName);
            SetLayer(KnockRush.Core.GameLayers.Trigger, KnockRush.Core.GameLayers.TriggerName);
            tagManager.ApplyModifiedPropertiesWithoutUndo();

            // Characters are kinematic and resolve their own collisions; they must not push each other via PhysX.
            Physics.IgnoreLayerCollision(KnockRush.Core.GameLayers.Player, KnockRush.Core.GameLayers.Player, true);
        }

        /// <summary>True for empty identifiers and the defaults Unity / project templates generate.</summary>
        public static bool IsPlaceholderIdentifier(string identifier)
        {
            return string.IsNullOrEmpty(identifier) ||
                   identifier.Contains("DefaultCompany") ||
                   identifier.Contains("unity.template") ||
                   identifier.Contains("UnityTechnologies") ||
                   identifier.Contains("Unity-Technologies");
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;

            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) ||
                PlayerSettings.bundleVersion == "0.1" ||
                PlayerSettings.bundleVersion == "1.0")
            {
                PlayerSettings.bundleVersion = InitialVersion;
            }

            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                var currentId = PlayerSettings.GetApplicationIdentifier(target);
                if (IsPlaceholderIdentifier(currentId))
                {
                    PlayerSettings.SetApplicationIdentifier(target, ApplicationIdentifier);
                }
                else if (currentId != ApplicationIdentifier)
                {
                    Debug.LogWarning($"[KnockRush Setup] Keeping existing {target.TargetName} identifier '{currentId}' (differs from '{ApplicationIdentifier}').");
                }

                // IL2CPP is required for ARM64 on Android and is the only option on iOS.
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            }

            // Google Play requires 64-bit; 32-bit ARMv7 devices are not worth supporting for a 32-player 3D game.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Landscape only, both directions.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // iPad: a landscape-only app must opt out of multitasking split view.
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.statusBarHidden = true;

            // URP + our lighting/shaders are authored for linear colour space.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Version-control friendly assets.
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            AssetDatabase.SaveAssets();
            Debug.Log($"[KnockRush Setup] Player settings applied ({ApplicationIdentifier}, IL2CPP, ARM64, landscape, linear).");
        }

        // ------------------------------------------------------------------
        // 4. Config assets and core scenes
        // ------------------------------------------------------------------
        [MenuItem(Menu + "4. Create Config Assets and Core Scenes", priority = 4)]
        public static void CreateConfigAndScenes()
        {
            EnsureFolder(ConfigResourcesFolder);
            EnsureFolder(ScenesFolder);
            EnsureGameConfig();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[KnockRush Setup] Cancelled: unsaved scene changes were not saved.");
                return;
            }

            // Leave the template scene before deleting it.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RemoveTemplateLeftovers();

            CreateSceneIfMissing(BootScenePath, BootBackground, () =>
            {
                new GameObject("BootSceneController").AddComponent<BootSceneController>();
            });

            CreateSceneIfMissing(MainMenuScenePath, MainMenuBackground, () =>
            {
                new GameObject("MainMenuPlaceholder").AddComponent<MainMenuPlaceholder>();
            });

            UpdateBuildSceneList();
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Debug.Log("[KnockRush Setup] Config asset and core scenes ready. Boot scene opened.");
        }

        /// <summary>Assets that ship with the Universal 3D template and are not part of KnockRush.</summary>
        public static readonly string[] TemplateLeftovers =
        {
            "Assets/Scenes/SampleScene.unity",
            "Assets/Settings/SampleSceneProfile.asset",
            "Assets/TutorialInfo",
            "Assets/Readme.asset"
        };

        public static void RemoveTemplateLeftovers()
        {
            foreach (var path in TemplateLeftovers)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"[KnockRush Setup] Removed template asset {path}.");
                }
            }
        }

        private static void EnsureGameConfig()
        {
            var existing = AssetDatabase.FindAssets("t:" + nameof(GameConfig))
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();

            if (existing.Length > 1)
            {
                Debug.LogWarning($"[KnockRush Setup] Found {existing.Length} GameConfig assets. Keep exactly one: {string.Join(", ", existing)}");
            }

            if (existing.Length > 0)
            {
                return;
            }

            var config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, GameConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[KnockRush Setup] Created {GameConfigPath}.");
        }

        private static void CreateSceneIfMissing(string path, Color background, Action populate)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                Debug.Log($"[KnockRush Setup] {path} already exists; left unchanged.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
            }

            populate();

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                Debug.LogError($"[KnockRush Setup] Failed to save {path}.");
                return;
            }

            Debug.Log($"[KnockRush Setup] Created {path}.");
        }

        private static void UpdateBuildSceneList()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(MainMenuScenePath, true)
            };

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path == BootScenePath || scene.path == MainMenuScenePath)
                {
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) != null)
                {
                    scenes.Add(scene);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------
        // Run steps 2-4 together (packages are separate because they trigger a recompile)
        // ------------------------------------------------------------------
        [MenuItem(Menu + "Run Steps 2-4", priority = 10)]
        public static void RunSteps2To4()
        {
            CreateFolderStructure();
            EnsureLayers();
            ApplyPlayerSettings();
            CreateConfigAndScenes();
            KnockRushProjectValidator.ValidateFromMenu();
        }

        // ------------------------------------------------------------------
        // Command-line / CI entry point (no dialogs). Exit code 0 = valid project.
        // Unity.exe -batchmode -projectPath KnockRush
        //   -executeMethod KnockRush.EditorTools.Setup.KnockRushProjectSetup.RunBatchSetup
        // ------------------------------------------------------------------
        public static void RunBatchSetup()
        {
            var exitCode = 0;
            try
            {
                CreateFolderStructure();
                EnsureLayers();
                ApplyPlayerSettings();
                CreateConfigAndScenes();
                AssetDatabase.SaveAssets();

                foreach (var result in KnockRushProjectValidator.Run())
                {
                    var line = $"[Validate] {result.Severity.ToString().ToUpperInvariant(),-7} {result.Message}";
                    if (result.Severity == KnockRushProjectValidator.Severity.Fail)
                    {
                        exitCode = 1;
                        Debug.LogError(line);
                    }
                    else if (result.Severity == KnockRushProjectValidator.Severity.Warning)
                    {
                        Debug.LogWarning(line);
                    }
                    else
                    {
                        Debug.Log(line);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 2;
            }

            Debug.Log($"[KnockRush Setup] Batch setup finished with exit code {exitCode}.");
            EditorApplication.Exit(exitCode);
        }
    }
}
