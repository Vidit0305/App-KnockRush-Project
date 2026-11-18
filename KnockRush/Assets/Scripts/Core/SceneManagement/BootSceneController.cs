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
    /// The IMGUI splash is a PLACEHOLDER; Phase 8 replaces it with the branded splash/loading UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BootSceneController : MonoBehaviour
    {
        private const string Tag = nameof(BootSceneController);

        private string status = "Loading...";
        private bool failed;
        private GUIStyle titleStyle;
        private GUIStyle statusStyle;

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

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 72,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                titleStyle.normal.textColor = Color.white;

                statusStyle = new GUIStyle(titleStyle) { fontSize = 22, fontStyle = FontStyle.Normal };
            }

            var scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;

            GUI.Label(new Rect(0f, 250f, width, 100f), "KNOCKRUSH", titleStyle);
            statusStyle.normal.textColor = failed ? new Color(1f, 0.45f, 0.45f) : new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(0f, 360f, width, 40f), status, statusStyle);

            GUI.matrix = Matrix4x4.identity;
        }
    }
}
