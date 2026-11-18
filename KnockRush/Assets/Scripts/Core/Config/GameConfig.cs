using KnockRush.Core.SceneManagement;
using KnockRush.Utilities;
using UnityEngine;

namespace KnockRush.Core.Config
{
    /// <summary>
    /// Global, build-wide configuration. Exactly one instance must exist at
    /// <c>Assets/ScriptableObjects/Config/Resources/GameConfig.asset</c> so that
    /// <see cref="AppRoot"/> can load it before any scene is loaded.
    /// </summary>
    [CreateAssetMenu(menuName = "KnockRush/Config/Game Config", fileName = "GameConfig", order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        /// <summary>Path passed to Resources.Load (file must be named GameConfig inside any Resources folder).</summary>
        public const string ResourcesPath = "GameConfig";

        [Header("Environment")]
        [Tooltip("Which backend / server environment this build connects to.")]
        [SerializeField] private BuildEnvironment environment = BuildEnvironment.Development;

        [Header("Performance")]
        [Tooltip("Application.targetFrameRate on devices. 60 for capable devices; graphics tiers may lower it later (Phase 9).")]
        [SerializeField, Range(30, 120)] private int targetFrameRate = 60;

        [Header("Boot Flow")]
        [Tooltip("Scene loaded after the Boot scene finishes initialisation.")]
        [SerializeField] private string firstSceneAfterBoot = SceneNames.MainMenu;

        [Tooltip("Minimum time (seconds) the boot splash stays on screen, so it never just flickers.")]
        [SerializeField, Min(0f)] private float minimumSplashDuration = 1.5f;

        [Header("Diagnostics")]
        [Tooltip("Shows the FPS / build info overlay in Editor and Development builds. Never shown in release builds.")]
        [SerializeField] private bool showDebugOverlay = true;

        public BuildEnvironment Environment => environment;
        public int TargetFrameRate => targetFrameRate;
        public string FirstSceneAfterBoot => firstSceneAfterBoot;
        public float MinimumSplashDuration => minimumSplashDuration;
        public bool ShowDebugOverlay => showDebugOverlay;

        /// <summary>
        /// Loads the GameConfig from Resources. If it is missing, logs an error and returns
        /// an in-memory default so the game can still start (and the problem is obvious).
        /// </summary>
        public static GameConfig LoadOrDefault()
        {
            var config = Resources.Load<GameConfig>(ResourcesPath);
            if (config != null)
            {
                return config;
            }

            Log.Error(nameof(GameConfig),
                "GameConfig asset not found in a Resources folder. Run 'KnockRush > Setup > 4. Create Config Assets and Core Scenes'. Using defaults.");
            config = CreateInstance<GameConfig>();
            config.name = "GameConfig (Runtime Default)";
            return config;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(firstSceneAfterBoot))
            {
                firstSceneAfterBoot = SceneNames.MainMenu;
            }
        }
    }
}
