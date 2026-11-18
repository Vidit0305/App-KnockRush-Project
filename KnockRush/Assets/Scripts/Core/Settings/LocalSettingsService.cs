using System;
using System.Threading;
using System.Threading.Tasks;
using KnockRush.Core.Events;
using KnockRush.Core.Services;
using KnockRush.Utilities;
using UnityEngine;

namespace KnockRush.Core.Settings
{
    public interface ILocalSettingsService
    {
        IReadOnlyLocalSettings Current { get; }

        /// <summary>Applies a change, clamps values, saves to disk and publishes <see cref="LocalSettingsChangedEvent"/>.</summary>
        void Modify(Action<LocalSettings> change);

        void ResetToDefaults();
    }

    public readonly struct LocalSettingsChangedEvent
    {
        public readonly IReadOnlyLocalSettings Settings;

        public LocalSettingsChangedEvent(IReadOnlyLocalSettings settings)
        {
            Settings = settings;
        }
    }

    public sealed class LocalSettingsService : ILocalSettingsService, IGameService
    {
        private const string Tag = nameof(LocalSettingsService);
        private const string PrefsKey = "knockrush.localsettings";

        private LocalSettings settings = new LocalSettings();

        public IReadOnlyLocalSettings Current => settings;

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            settings = Load();
            Log.Info(Tag, $"Loaded local settings (sensitivity {settings.CameraSensitivity:0.00}, music {settings.MusicVolume:0.00}).");
            return Task.CompletedTask;
        }

        public void Modify(Action<LocalSettings> change)
        {
            if (change == null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            change(settings);
            settings.Sanitize();
            Save(settings);
            EventBus.Publish(new LocalSettingsChangedEvent(settings));
        }

        public void ResetToDefaults()
        {
            settings = new LocalSettings();
            Save(settings);
            EventBus.Publish(new LocalSettingsChangedEvent(settings));
        }

        private static LocalSettings Load()
        {
            var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new LocalSettings();
            }

            try
            {
                var loaded = JsonUtility.FromJson<LocalSettings>(json) ?? new LocalSettings();
                loaded.Sanitize();
                return loaded;
            }
            catch (Exception exception)
            {
                Log.Warning(Tag, $"Local settings were corrupt and have been reset. ({exception.Message})");
                return new LocalSettings();
            }
        }

        private static void Save(LocalSettings value)
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(value));
            PlayerPrefs.Save();
        }
    }
}
