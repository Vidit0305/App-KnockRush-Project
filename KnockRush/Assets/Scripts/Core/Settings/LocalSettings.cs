using System;
using UnityEngine;

namespace KnockRush.Core.Settings
{
    /// <summary>Read-only view of the player's device-local settings.</summary>
    public interface IReadOnlyLocalSettings
    {
        float MusicVolume { get; }
        float SfxVolume { get; }
        float CameraSensitivity { get; }
        bool InvertCameraY { get; }

        /// <summary>-1 = automatic (device tier detection, Phase 9); otherwise a quality level index.</summary>
        int GraphicsQuality { get; }

        bool HapticsEnabled { get; }
        bool ShowFpsCounter { get; }
    }

    /// <summary>
    /// Device-local preferences (audio, camera, graphics). Stored in PlayerPrefs as JSON.
    /// Anything that affects progression or economy is NOT stored here; that lives on the
    /// backend (Phase 7) and is server-authoritative.
    /// </summary>
    [Serializable]
    public sealed class LocalSettings : IReadOnlyLocalSettings
    {
        public const int CurrentVersion = 1;

        public const float MinSensitivity = 0.1f;
        public const float MaxSensitivity = 3f;

        [SerializeField] private int version = CurrentVersion;
        [SerializeField] private float musicVolume = 0.8f;
        [SerializeField] private float sfxVolume = 1f;
        [SerializeField] private float cameraSensitivity = 1f;
        [SerializeField] private bool invertCameraY;
        [SerializeField] private int graphicsQuality = -1;
        [SerializeField] private bool hapticsEnabled = true;
        [SerializeField] private bool showFpsCounter;

        public int Version => version;

        public float MusicVolume
        {
            get => musicVolume;
            set => musicVolume = Mathf.Clamp01(value);
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        public float CameraSensitivity
        {
            get => cameraSensitivity;
            set => cameraSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        }

        public bool InvertCameraY
        {
            get => invertCameraY;
            set => invertCameraY = value;
        }

        public int GraphicsQuality
        {
            get => graphicsQuality;
            set => graphicsQuality = Mathf.Max(-1, value);
        }

        public bool HapticsEnabled
        {
            get => hapticsEnabled;
            set => hapticsEnabled = value;
        }

        public bool ShowFpsCounter
        {
            get => showFpsCounter;
            set => showFpsCounter = value;
        }

        /// <summary>
        /// Clamps every value into its legal range and upgrades old data versions.
        /// Always called after deserialising, because PlayerPrefs data can be stale or edited.
        /// </summary>
        public void Sanitize()
        {
            if (float.IsNaN(musicVolume)) musicVolume = 0.8f;
            if (float.IsNaN(sfxVolume)) sfxVolume = 1f;
            if (float.IsNaN(cameraSensitivity)) cameraSensitivity = 1f;

            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            cameraSensitivity = Mathf.Clamp(cameraSensitivity, MinSensitivity, MaxSensitivity);
            graphicsQuality = Mathf.Max(-1, graphicsQuality);

            // Future data migrations go here: if (version < 2) { ... }
            version = CurrentVersion;
        }
    }
}
