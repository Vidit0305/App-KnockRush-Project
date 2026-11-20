using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KnockRush.EditorTools.Build
{
    /// <summary>
    /// Repeatable player builds, usable from the menu and from the command line / CI.
    ///
    /// Command line (Android APK, size-optimised release configuration):
    ///   Unity.exe -batchmode -projectPath KnockRush -buildTarget Android
    ///     -executeMethod KnockRush.EditorTools.Build.KnockRushBuild.BuildAndroidReleaseBatch -logFile build.log
    /// (BuildAndroidDevelopmentBatch makes the debuggable development APK.)
    ///
    /// The release APK is signed with the debug key until the store keystore is set up (Phase 10).
    /// </summary>
    public static class KnockRushBuild
    {
        public const string AndroidReleasePath = "Builds/Android/KnockRush.apk";
        public const string AndroidDevelopmentPath = "Builds/Android/KnockRush-dev.apk";

        [MenuItem("KnockRush/Build/Android APK (release, small)", priority = 99)]
        public static void BuildAndroidReleaseMenu() => ShowResult(BuildAndroid(false), AndroidReleasePath);

        [MenuItem("KnockRush/Build/Android Development APK", priority = 100)]
        public static void BuildAndroidDevelopmentMenu() => ShowResult(BuildAndroid(true), AndroidDevelopmentPath);

        public static void BuildAndroidReleaseBatch() => RunBatch(() => BuildAndroid(false));

        public static void BuildAndroidDevelopmentBatch() => RunBatch(() => BuildAndroid(true));

        public static BuildReport BuildAndroidDevelopment() => BuildAndroid(true);

        private static void ShowResult(BuildReport report, string path)
        {
            var succeeded = report.summary.result == BuildResult.Succeeded;
            EditorUtility.DisplayDialog(
                "KnockRush Build",
                succeeded ? $"Android build succeeded: {Path.GetFullPath(path)}" : $"Android build {report.summary.result}. See the Console.",
                "OK");
        }

        private static void RunBatch(Func<BuildReport> build)
        {
            var exitCode = 1;
            try
            {
                exitCode = build().summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Download-size settings: 64-bit ARM only, IL2CPP optimised for size, managed code and
        /// unused engine modules stripped (Assets/link.xml keeps reflection-reached code).
        /// </summary>
        public static void ApplyAndroidSizeSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetIl2CppCodeGeneration(android, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Release);

            // Static batching copies every static map piece into per-scene combined meshes (~17 MB of
            // duplicated vertices). The SRP Batcher already draws the shared primitives cheaply.
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.Android, false);
        }

        public static BuildReport BuildAndroid(bool development)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes in the scene list. Run KnockRush > Setup first.");
            }

            var path = development ? AndroidDevelopmentPath : AndroidReleasePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Builds");
            EditorUserBuildSettings.buildAppBundle = false;
            ApplyAndroidSizeSettings();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.CompressWithLz4HC
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            var bytes = File.Exists(path) ? new FileInfo(path).Length : 0L;
            Debug.Log($"[KnockRush Build] Android {(development ? "development" : "release")}: {summary.result}, " +
                      $"APK {bytes / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0}s, " +
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
