using System;
using System.Threading;
using System.Threading.Tasks;
using KnockRush.Core.Events;
using KnockRush.Core.Services;
using KnockRush.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnockRush.Core.SceneManagement
{
    /// <summary>
    /// Loads scenes asynchronously and broadcasts start / progress / completion events
    /// (a loading screen in Phase 8 simply listens to these).
    ///
    /// The loop is bound to the application lifetime token rather than the caller's,
    /// because the caller is usually destroyed by the very scene change it requested.
    /// </summary>
    public sealed class SceneLoader : ISceneLoader, IGameService
    {
        private const string Tag = nameof(SceneLoader);
        private readonly CancellationToken appLifetime;

        public SceneLoader(CancellationToken appLifetime)
        {
            this.appLifetime = appLifetime;
        }

        public bool IsLoading { get; private set; }

        public string CurrentSceneName => SceneManager.GetActiveScene().name;

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task LoadSceneAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name is empty.", nameof(sceneName));
            }

            if (IsLoading)
            {
                Log.Warning(Tag, $"Ignored request to load '{sceneName}' because another scene is already loading.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                throw new InvalidOperationException(
                    $"Scene '{sceneName}' is not in Build Settings (File > Build Profiles > Scene List).");
            }

            IsLoading = true;
            try
            {
                Log.Info(Tag, $"Loading scene '{sceneName}'...");
                EventBus.Publish(new SceneLoadStartedEvent(sceneName));

                var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (operation == null)
                {
                    throw new InvalidOperationException($"SceneManager.LoadSceneAsync returned null for '{sceneName}'.");
                }

                while (!operation.isDone)
                {
                    // Unity reports 0..0.9 while loading, then jumps to 1 on activation.
                    var progress = Mathf.Clamp01(operation.progress / 0.9f);
                    EventBus.Publish(new SceneLoadProgressEvent(sceneName, progress));
                    await Awaitable.NextFrameAsync(appLifetime);
                }

                EventBus.Publish(new SceneLoadProgressEvent(sceneName, 1f));
                EventBus.Publish(new SceneLoadCompletedEvent(sceneName));
                Log.Info(Tag, $"Scene '{sceneName}' loaded.");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
