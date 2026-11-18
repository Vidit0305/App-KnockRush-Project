using System;
using KnockRush.Core.Config;
using KnockRush.Core.Services;
using KnockRush.Utilities;
using UnityEngine;

namespace KnockRush.Core.SceneManagement
{
    /// <summary>
    /// Lives in the Boot scene (Build index 0). Waits for <see cref="AppRoot"/> to finish
    /// initialising, keeps the splash visible for a minimum time, then loads the first scene.
    ///
    /// The branded splash itself is drawn by the UI layer (KnockRush.UI SplashScreen).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BootSceneController : MonoBehaviour
    {
        private const string Tag = nameof(BootSceneController);

        private string status = "Loading...";
        private bool failed;

        /// <summary>Boot progress text (shown by the splash screen; useful in logs and tests).</summary>
        public string Status => status;

        public bool Failed => failed;

        private async void Start()
        {
            var startTime = Time.realtimeSinceStartup;

            try
            {
                await AppRoot.InitializationTask;

                // Headless roles (dedicated server, bot clients) are launched by the network layer.
                if (AppRoot.IsDedicatedServer || CommandLine.Has("-server") || CommandLine.Has("-botclient") || CommandLine.Has("-single"))
                {
                    status = "Headless launch";
                    return;
                }

                var config = ServiceLocator.Get<GameConfig>();
                var remaining = config.MinimumSplashDuration - (Time.realtimeSinceStartup - startTime);
                if (remaining > 0f)
                {
                    await Awaitable.WaitForSecondsAsync(remaining, destroyCancellationToken);
                }

                status = "Starting...";
                await ServiceLocator.Get<ISceneLoader>().LoadSceneAsync(config.FirstSceneAfterBoot);
            }
            catch (OperationCanceledException)
            {
                // Boot scene destroyed or play mode exited during boot. Nothing to do.
            }
            catch (Exception exception)
            {
                failed = true;
                status = "Startup failed. Check the Console / device log.";
                Log.Exception(exception, this);
            }
        }
    }
}
