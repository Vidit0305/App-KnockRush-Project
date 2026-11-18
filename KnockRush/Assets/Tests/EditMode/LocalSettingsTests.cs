using KnockRush.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace KnockRush.Tests.EditMode
{
    public sealed class LocalSettingsTests
    {
        [Test]
        public void Defaults_AreSensible()
        {
            var settings = new LocalSettings();

            Assert.AreEqual(1f, settings.CameraSensitivity);
            Assert.AreEqual(-1, settings.GraphicsQuality);
            Assert.IsTrue(settings.HapticsEnabled);
        }

        [Test]
        public void Setters_ClampValues()
        {
            var settings = new LocalSettings
            {
                MusicVolume = 5f,
                SfxVolume = -1f,
                CameraSensitivity = 100f,
                GraphicsQuality = -7
            };

            Assert.AreEqual(1f, settings.MusicVolume);
            Assert.AreEqual(0f, settings.SfxVolume);
            Assert.AreEqual(LocalSettings.MaxSensitivity, settings.CameraSensitivity);
            Assert.AreEqual(-1, settings.GraphicsQuality);
        }

        [Test]
        public void Sanitize_FixesCorruptJson()
        {
            const string corrupt = "{\"version\":0,\"musicVolume\":9.0,\"sfxVolume\":-3.0,\"cameraSensitivity\":0.0,\"graphicsQuality\":-50}";
            var settings = JsonUtility.FromJson<LocalSettings>(corrupt);

            settings.Sanitize();

            Assert.AreEqual(1f, settings.MusicVolume);
            Assert.AreEqual(0f, settings.SfxVolume);
            Assert.AreEqual(LocalSettings.MinSensitivity, settings.CameraSensitivity);
            Assert.AreEqual(-1, settings.GraphicsQuality);
            Assert.AreEqual(LocalSettings.CurrentVersion, settings.Version);
        }

        [Test]
        public void JsonRoundTrip_PreservesValues()
        {
            var original = new LocalSettings { CameraSensitivity = 2.25f, InvertCameraY = true, MusicVolume = 0.3f };

            var copy = JsonUtility.FromJson<LocalSettings>(JsonUtility.ToJson(original));

            Assert.AreEqual(2.25f, copy.CameraSensitivity, 0.0001f);
            Assert.IsTrue(copy.InvertCameraY);
            Assert.AreEqual(0.3f, copy.MusicVolume, 0.0001f);
        }
    }
}
