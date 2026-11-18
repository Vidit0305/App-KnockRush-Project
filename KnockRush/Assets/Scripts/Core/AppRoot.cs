using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnockRush.Core.Config;
using KnockRush.Core.Diagnostics;
using KnockRush.Core.Events;
using KnockRush.Core.Platform;
using KnockRush.Core.SceneManagement;
using KnockRush.Core.Services;
using KnockRush.Core.Settings;
using KnockRush.Utilities;
using UnityEngine;

namespace KnockRush.Core
{
    /// <summary>
    /// The application composition root.
    ///
    /// Created automatically before the first scene loads (no scene setup required), so
    /// pressing Play in ANY scene gives a fully initialised game. It:
    ///   1. Loads <see cref="GameConfig"/> from Resources.
    ///   2. Applies runtime platform settings.
    ///   3. Creates, registers and initialises every core service in a fixed order.
    ///   4. Publishes <see cref="AppInitializedEvent"/>.
    ///
    /// Await <see cref="InitializationTask"/> before resolving services.
    /// Later phases add services here (audio, auth, profile, economy, networking).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class AppRoot : MonoBehaviour
    {
        private const string Tag = nameof(AppRoot);

        private static readonly List<Action<ServiceRegistry>> Installers = new List<Action<ServiceRegistry>>();
        private readonly List<IGameService> orderedServices = new List<IGameService>();

        /// <summary>
        /// Lets feature assemblies (backend, audio, ...) add services without Core depending on
        /// them. Call from [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)].
        /// Installers run after the core services, in registration order.
        /// </summary>
        public static void RegisterInstaller(Action<ServiceRegistry> installer)
        {
            if (installer != null && !Installers.Contains(installer))
            {
                Installers.Add(installer);
            }
        }

        /// <summary>Handed to installers to register services with the app.</summary>
        public sealed class ServiceRegistry
        {
            private readonly AppRoot root;

            internal ServiceRegistry(AppRoot root)
            {
                this.root = root;
            }

            public GameConfig Config => root.Config;

            public void Add<TService>(TService service) where TService : class
            {
                root.Add(service);
            }
        }

        public static AppRoot Instance { get; private set; }

        /// <summary>Completes when every core service is initialised. Faults if startup failed.</summary>
        public static Task InitializationTask { get; private set; } = Task.CompletedTask;

        public static bool IsInitialized { get; private set; }

        /// <summary>True in headless dedicated server builds (Linux Dedicated Server build target).</summary>
        public static bool IsDedicatedServer
        {
            get
            {
#if UNITY_SERVER
                return true;
#else
                return false;
#endif
            }
        }

        public GameConfig Config { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Installers.Clear();
            Instance = null;
            InitializationTask = Task.CompletedTask;
            IsInitialized = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            var config = GameConfig.LoadOrDefault();

            var root = new GameObject("[KnockRush App]");
            DontDestroyOnLoad(root);

            Instance = root.AddComponent<AppRoot>();
            Instance.Config = config;
            root.AddComponent<ApplicationLifecycleRelay>();

            if (Debug.isDebugBuild && config.ShowDebugOverlay && !IsDedicatedServer)
            {
                root.AddComponent<DebugOverlay>().Initialize(config);
            }

            InitializationTask = Instance.InitializeAsync(Instance.destroyCancellationToken);
        }

        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            Log.Info(Tag, $"Booting KnockRush v{Application.version} | env={Config.Environment} | server={IsDedicatedServer}");

            try
            {
                PlatformSettings.Apply(Config, IsDedicatedServer);

                ServiceLocator.Register(Config);

                // ---- Core services (order matters: dependencies first) ----
                Add<ILocalSettingsService>(new LocalSettingsService());
                Add<ISceneLoader>(new SceneLoader(cancellationToken));

                // Feature services (backend, ...) registered by their assemblies.
                var registry = new ServiceRegistry(this);
                foreach (var installer in Installers)
                {
                    installer(registry);
                }

                foreach (var service in orderedServices)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await service.InitializeAsync(cancellationToken);
                }

                IsInitialized = true;
                Log.Info(Tag, $"Initialised {orderedServices.Count} services.");
                EventBus.Publish(new AppInitializedEvent());
            }
            catch (OperationCanceledException)
            {
                Log.Info(Tag, "Initialisation cancelled (application shutting down).");
                throw;
            }
            catch (Exception exception)
            {
                Log.Error(Tag, "Startup failed. See the following exception.");
                Log.Exception(exception, this);
                throw;
            }
        }

        private void Add<TService>(TService service) where TService : class
        {
            ServiceLocator.Register(service);
            if (service is IGameService gameService)
            {
                orderedServices.Add(gameService);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            // Dispose in reverse initialisation order.
            for (var i = orderedServices.Count - 1; i >= 0; i--)
            {
                if (orderedServices[i] is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception exception)
                    {
                        Log.Exception(exception);
                    }
                }
            }

            orderedServices.Clear();
            ServiceLocator.Reset();
            EventBus.Reset();
            IsInitialized = false;
            Instance = null;
        }
    }
}
