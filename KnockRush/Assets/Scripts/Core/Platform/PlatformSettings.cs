using KnockRush.Core.Config;
using KnockRush.Utilities;
using UnityEngine;

namespace KnockRush.Core.Platform
{
    /// <summary>
    /// Applies runtime platform settings at boot: frame rate, orientation and screen sleep.
    /// (Build-time Player Settings are applied by the editor tool KnockRush > Setup.)
    /// </summary>
    public static class PlatformSettings
    {
        private const string Tag = nameof(PlatformSettings);

        public static void Apply(GameConfig config, bool isDedicatedServer)
        {
            if (isDedicatedServer)
            {
                // Headless server: simulation rate is driven by the network tick (Phase 4).
                // Cap the render loop so an idle server does not burn a full CPU core.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
                Log.Info(Tag, "Applied dedicated server platform settings.");
                return;
            }

            // On mobile, vSync is ignored and targetFrameRate controls pacing.
            // In the Editor / desktop dev builds we disable vSync so targetFrameRate is honoured too.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = config.TargetFrameRate;

            // Landscape only (both directions), matching the Player Settings.
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            // Menus follow the OS sleep setting. Matches switch to NeverSleep (Phase 5).
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            Log.Info(Tag, $"Target frame rate {config.TargetFrameRate}, landscape orientation.");
        }
    }
}
