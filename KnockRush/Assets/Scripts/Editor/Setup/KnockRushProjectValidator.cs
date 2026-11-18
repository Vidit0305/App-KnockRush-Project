using System.Collections.Generic;
using System.Linq;
using System.Text;
using KnockRush.Core.Config;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace KnockRush.EditorTools.Setup
{
    /// <summary>
    /// Checks that the project matches the Phase 1 architecture.
    /// Menu: KnockRush > Setup > Validate Project. Safe to run at any time.
    /// </summary>
    public static class KnockRushProjectValidator
    {
        public enum Severity
        {
            Pass,
            Warning,
            Fail
        }

        public readonly struct Result
        {
            public readonly Severity Severity;
            public readonly string Message;

            public Result(Severity severity, string message)
            {
                Severity = severity;
                Message = message;
            }
        }

        private const string ExpectedUnityLine = "6000.3";

        [MenuItem("KnockRush/Setup/Validate Project", priority = 20)]
        public static void ValidateFromMenu()
        {
            var results = Run();
            var report = new StringBuilder();
            var failures = 0;
            var warnings = 0;

            foreach (var result in results)
            {
                switch (result.Severity)
                {
                    case Severity.Pass:
                        Debug.Log($"[Validate] PASS  {result.Message}");
                        break;
                    case Severity.Warning:
                        warnings++;
                        Debug.LogWarning($"[Validate] WARN  {result.Message}");
                        report.AppendLine("WARN: " + result.Message);
                        break;
                    default:
                        failures++;
                        Debug.LogError($"[Validate] FAIL  {result.Message}");
                        report.AppendLine("FAIL: " + result.Message);
                        break;
                }
            }

            var summary = failures == 0 && warnings == 0
                ? $"All {results.Count} checks passed. Phase 1 project setup is valid."
                : $"{failures} failure(s), {warnings} warning(s) out of {results.Count} checks.\n\n{report}";

            EditorUtility.DisplayDialog("KnockRush Project Validation", summary, "OK");
        }

        public static List<Result> Run()
        {
            var results = new List<Result>();

            CheckUnityVersion(results);
            CheckRenderPipeline(results);
            CheckInputHandling(results);
            CheckPackages(results);
            CheckFolders(results);
            CheckGameConfig(results);
            CheckBuildScenes(results);
            CheckPlayerSettings(results);
            CheckVersionControlSettings(results);

            return results;
        }

        private static void CheckUnityVersion(List<Result> results)
        {
            var version = Application.unityVersion;
            results.Add(version.StartsWith(ExpectedUnityLine)
                ? new Result(Severity.Pass, $"Unity {version} (6.3 LTS line).")
                : new Result(Severity.Warning, $"Unity {version}: the project standard is the latest {ExpectedUnityLine}.x LTS patch."));
        }

        private static void CheckRenderPipeline(List<Result> results)
        {
            var defaultPipeline = GraphicsSettings.defaultRenderPipeline;
            if (defaultPipeline == null || !IsUrp(defaultPipeline))
            {
                results.Add(new Result(Severity.Fail, "URP is not the default render pipeline (Project Settings > Graphics)."));
                return;
            }

            for (var i = 0; i < QualitySettings.count; i++)
            {
                var qualityPipeline = QualitySettings.GetRenderPipelineAssetAt(i);
                if (qualityPipeline != null && !IsUrp(qualityPipeline))
                {
                    results.Add(new Result(Severity.Fail, $"Quality level '{QualitySettings.names[i]}' uses a non-URP pipeline asset."));
                    return;
                }
            }

            results.Add(new Result(Severity.Pass, $"URP active ({defaultPipeline.name})."));
        }

        private static bool IsUrp(RenderPipelineAsset asset)
        {
            return asset.GetType().Name == "UniversalRenderPipelineAsset";
        }

        private static void CheckInputHandling(List<Result> results)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            results.Add(new Result(Severity.Pass, "Active Input Handling = Input System Package (New)."));
#elif ENABLE_INPUT_SYSTEM
            results.Add(new Result(Severity.Warning, "Active Input Handling = Both. Set it to 'Input System Package (New)' in Player > Other Settings."));
#else
            results.Add(new Result(Severity.Fail, "Input System is not enabled. Set Active Input Handling to 'Input System Package (New)'."));
#endif
        }

        private static void CheckPackages(List<Result> results)
        {
            var missing = KnockRushProjectSetup.RequiredPackages
                .Where(name => !KnockRushProjectSetup.IsPackageInstalled(name))
                .ToArray();

            results.Add(missing.Length == 0
                ? new Result(Severity.Pass, "All required packages installed.")
                : new Result(Severity.Fail, $"Missing packages: {string.Join(", ", missing)}. Run Setup step 1."));
        }

        private static void CheckFolders(List<Result> results)
        {
            var missing = KnockRushProjectSetup.RequiredFolders
                .Where(folder => !AssetDatabase.IsValidFolder(folder))
                .ToArray();

            results.Add(missing.Length == 0
                ? new Result(Severity.Pass, "Folder structure complete.")
                : new Result(Severity.Fail, $"{missing.Length} folder(s) missing (e.g. {missing[0]}). Run Setup step 2."));
        }

        private static void CheckGameConfig(List<Result> results)
        {
            var paths = AssetDatabase.FindAssets("t:" + nameof(GameConfig))
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();

            if (paths.Length == 0)
            {
                results.Add(new Result(Severity.Fail, "No GameConfig asset. Run Setup step 4."));
            }
            else if (paths.Length > 1)
            {
                results.Add(new Result(Severity.Fail, $"Multiple GameConfig assets found: {string.Join(", ", paths)}."));
            }
            else if (!paths[0].Contains("/Resources/") || !paths[0].EndsWith("/GameConfig.asset"))
            {
                results.Add(new Result(Severity.Fail, $"GameConfig must be named GameConfig.asset inside a Resources folder (found {paths[0]})."));
            }
            else
            {
                results.Add(new Result(Severity.Pass, $"GameConfig found at {paths[0]}."));
            }
        }

        private static void CheckBuildScenes(List<Result> results)
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != KnockRushProjectSetup.BootScenePath || !scenes[0].enabled)
            {
                results.Add(new Result(Severity.Fail, "Boot scene must be the first enabled scene in the scene list. Run Setup step 4."));
                return;
            }

            if (!scenes.Any(scene => scene.path == KnockRushProjectSetup.MainMenuScenePath && scene.enabled))
            {
                results.Add(new Result(Severity.Fail, "MainMenu scene missing from the scene list."));
                return;
            }

            var extras = scenes.Where(scene => scene.enabled && scene.path.EndsWith("SampleScene.unity")).ToArray();
            results.Add(extras.Length > 0
                ? new Result(Severity.Warning, "Template SampleScene is still in the scene list; remove it.")
                : new Result(Severity.Pass, "Scene list: Boot (0), MainMenu (1)."));
        }

        private static void CheckPlayerSettings(List<Result> results)
        {
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                var backend = PlayerSettings.GetScriptingBackend(target);
                results.Add(backend == ScriptingImplementation.IL2CPP
                    ? new Result(Severity.Pass, $"{target.TargetName}: IL2CPP.")
                    : new Result(Severity.Fail, $"{target.TargetName}: scripting backend is {backend}, expected IL2CPP."));

                var id = PlayerSettings.GetApplicationIdentifier(target);
                results.Add(KnockRushProjectSetup.IsPlaceholderIdentifier(id)
                    ? new Result(Severity.Fail, $"{target.TargetName}: application identifier is not set ('{id}').")
                    : new Result(Severity.Pass, $"{target.TargetName}: identifier {id}."));
            }

            results.Add(PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64
                ? new Result(Severity.Pass, "Android: ARM64 only.")
                : new Result(Severity.Warning, $"Android architectures = {PlayerSettings.Android.targetArchitectures}; expected ARM64 only."));

            var landscapeOnly =
                PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation &&
                !PlayerSettings.allowedAutorotateToPortrait &&
                !PlayerSettings.allowedAutorotateToPortraitUpsideDown &&
                PlayerSettings.allowedAutorotateToLandscapeLeft &&
                PlayerSettings.allowedAutorotateToLandscapeRight;
            var fixedLandscape =
                PlayerSettings.defaultInterfaceOrientation == UIOrientation.LandscapeLeft ||
                PlayerSettings.defaultInterfaceOrientation == UIOrientation.LandscapeRight;

            results.Add(landscapeOnly || fixedLandscape
                ? new Result(Severity.Pass, "Orientation: landscape only.")
                : new Result(Severity.Fail, "Orientation allows portrait. Run Setup step 3."));

            results.Add(PlayerSettings.colorSpace == ColorSpace.Linear
                ? new Result(Severity.Pass, "Colour space: Linear.")
                : new Result(Severity.Fail, "Colour space must be Linear."));
        }

        private static void CheckVersionControlSettings(List<Result> results)
        {
            results.Add(EditorSettings.serializationMode == SerializationMode.ForceText
                ? new Result(Severity.Pass, "Asset serialization: Force Text.")
                : new Result(Severity.Fail, "Asset serialization must be Force Text."));

            results.Add(VersionControlSettings.mode == "Visible Meta Files"
                ? new Result(Severity.Pass, "Version control mode: Visible Meta Files.")
                : new Result(Severity.Fail, $"Version control mode is '{VersionControlSettings.mode}', expected Visible Meta Files."));
        }
    }
}
