using System.Collections;
using System.Threading;
using KnockRush.Core;
using KnockRush.Core.Config;
using KnockRush.Core.SceneManagement;
using KnockRush.Core.Services;
using KnockRush.Core.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KnockRush.Tests.PlayMode
{
    /// <summary>
    /// Automated version of the Phase 1 milestone test:
    /// AppRoot boots, Boot hands over to MainMenu, services resolve, settings persist.
    /// </summary>
    public sealed class BootFlowTests
    {
        private const float TimeoutSeconds = 15f;

        [UnityTest]
        public IEnumerator AppRoot_IsCreatedAndInitialised_BeforeAnyScene()
        {
            yield return WaitForAppRoot();

            Assert.IsNotNull(AppRoot.Instance, "AppRoot was not created by BeforeSceneLoad bootstrap.");
            Assert.IsTrue(AppRoot.IsInitialized);
            Assert.IsTrue(ServiceLocator.IsRegistered<GameConfig>());
            Assert.IsTrue(ServiceLocator.IsRegistered<ISceneLoader>());
            Assert.IsTrue(ServiceLocator.IsRegistered<ILocalSettingsService>());
        }

        [UnityTest]
        public IEnumerator BootScene_LoadsMainMenu()
        {
            yield return WaitForAppRoot();

            yield return SceneManager.LoadSceneAsync(SceneNames.Boot, LoadSceneMode.Single);

            var elapsed = 0f;
            while (SceneManager.GetActiveScene().name != SceneNames.MainMenu && elapsed < TimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(SceneNames.MainMenu, SceneManager.GetActiveScene().name,
                "Boot did not hand over to MainMenu in time.");
            Assert.IsFalse(ServiceLocator.Get<ISceneLoader>().IsLoading);
        }

        [UnityTest]
        public IEnumerator LocalSettings_PersistAcrossServiceInstances()
        {
            yield return WaitForAppRoot();

            var service = ServiceLocator.Get<ILocalSettingsService>();
            var original = service.Current.CameraSensitivity;
            var expected = Mathf.Approximately(original, 1.75f) ? 2.25f : 1.75f;

            try
            {
                service.Modify(settings => settings.CameraSensitivity = expected);

                // A fresh service instance simulates the next app launch reading PlayerPrefs.
                var reloaded = new LocalSettingsService();
                var init = reloaded.InitializeAsync(CancellationToken.None);
                Assert.IsTrue(init.IsCompleted);
                Assert.AreEqual(expected, reloaded.Current.CameraSensitivity, 0.0001f);
            }
            finally
            {
                service.Modify(settings => settings.CameraSensitivity = original);
            }
        }

        private static IEnumerator WaitForAppRoot()
        {
            var elapsed = 0f;
            while (!AppRoot.IsInitialized && elapsed < TimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(AppRoot.IsInitialized, "AppRoot did not finish initialising.");
        }
    }
}
