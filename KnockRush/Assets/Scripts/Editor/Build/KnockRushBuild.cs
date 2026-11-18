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

        // ------------------------------------------------------------------------------------
        // Desktop / server builds (local network testing, dedicated servers)
        // ------------------------------------------------------------------------------------
        public const string WindowsDevelopmentPath = "Builds/Windows/KnockRush.exe";
        public const string LinuxServerPath = "Builds/LinuxServer/KnockRushServer.x86_64";

        /// <summary>
        /// Windows development player. Run it headless as a local dedicated server or bot client:
        ///   KnockRush.exe -batchmode -nographics -server -session test -photonAppId ID
        /// </summary>
        [MenuItem("KnockRush/Build/Windows Development (server + bot testing)", priority = 101)]
        public static void BuildWindowsDevelopmentMenu()
        {
            BuildWindowsDevelopment();
        }

        public static void BuildWindowsDevelopmentBatch()
        {
            EditorApplication.Exit(BuildWindowsDevelopment().summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static BuildReport BuildWindowsDevelopment()
        {
            return BuildDesktop(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, WindowsDevelopmentPath, BuildOptions.Development);
        }

        /// <summary>
        /// Headless Linux dedicated server (UNITY_SERVER). Requires the "Linux Dedicated Server
        /// Build Support" editor module. Deployed as a container (see Server/README.md).
        /// </summary>
        [MenuItem("KnockRush/Build/Linux Dedicated Server", priority = 102)]
        public static void BuildLinuxServerMenu()
        {
            BuildLinuxServer();
        }

        public static void BuildLinuxServerBatch()
        {
            EditorApplication.Exit(BuildLinuxServer().summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static BuildReport BuildLinuxServer()
        {
            return BuildDesktop(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server, LinuxServerPath, BuildOptions.None);
        }

        private static BuildReport BuildDesktop(BuildTarget target, StandaloneBuildSubtarget subtarget, string path, BuildOptions buildOptions)
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Builds");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)subtarget,
                options = buildOptions
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[KnockRush Build] {target}/{subtarget}: {summary.result}, {summary.totalSize / (1024f * 1024f):0.0} MB, " +
                      $"{summary.totalTime.TotalSeconds:0}s, {summary.totalErrors} errors -> {summary.outputPath}");
            return report;
        }
    }
}
