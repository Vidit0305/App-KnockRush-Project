using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KnockRush.EditorTools.Build
{
    /// <summary>
    /// Repeatable player builds, usable from the menu and from the command line / CI.
    ///
    /// Command line (Android development APK):
    ///   Unity.exe -batchmode -projectPath KnockRush -buildTarget Android
    ///     -executeMethod KnockRush.EditorTools.Build.KnockRushBuild.BuildAndroidDevelopmentBatch -logFile build.log
    ///
    /// Release (signed AAB) builds are added in Phase 10 together with keystore handling.
    /// </summary>
    public static class KnockRushBuild
    {
        public const string AndroidDevelopmentPath = "Builds/Android/KnockRush-dev.apk";

        [MenuItem("KnockRush/Build/Android Development APK", priority = 100)]
        public static void BuildAndroidDevelopmentMenu()
        {
            var report = BuildAndroidDevelopment();
            var succeeded = report.summary.result == BuildResult.Succeeded;
            EditorUtility.DisplayDialog(
                "KnockRush Build",
                succeeded
                    ? $"Android development build succeeded.\n{Path.GetFullPath(AndroidDevelopmentPath)}"
                    : $"Android build {report.summary.result}. See the Console.",
                "OK");
        }

        public static void BuildAndroidDevelopmentBatch()
        {
            var exitCode = 1;
            try
            {
                var report = BuildAndroidDevelopment();
                exitCode = report.summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            EditorApplication.Exit(exitCode);
        }

        public static BuildReport BuildAndroidDevelopment()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes in the scene list. Run KnockRush > Setup first.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AndroidDevelopmentPath) ?? "Builds");
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = AndroidDevelopmentPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[KnockRush Build] Android development: {summary.result}, " +
                      $"{summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0}s, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings -> {summary.outputPath}");
            return report;
        }
    }
}
