using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using KnockRush.Core;
using KnockRush.Maps;
using KnockRush.Movement;
using KnockRush.Player.Controls;
using KnockRush.Utilities;
using UnityEngine;
using Log = KnockRush.Utilities.Log;
using UnityEngine.SceneManagement;

namespace KnockRush.Multiplayer
{
    public enum SessionRole
    {
        /// <summary>Local only, no Photon connection (practice, automated tests).</summary>
        Single,

        /// <summary>This device is server and a player (development and private test rooms).</summary>
        Host,

        /// <summary>Player connecting to a host or dedicated server.</summary>
        Client,

        /// <summary>Headless dedicated server (production).</summary>
        Server
    }

    public sealed class NetworkLaunchOptions
    {
        public SessionRole Role = SessionRole.Single;
        public string SessionName = "knockrush-dev";
        public string MapScene = Core.SceneManagement.SceneNames.HammerRush;
        public int MaxPlayers = 32;
        public bool IsVisible = true;
        public string GameModeId = "classic";

        /// <summary>When true this peer sends bot input (load testing).</summary>
        public bool IsBotClient;
    }

    /// <summary>
    /// Owns one Fusion NetworkRunner and its callbacks: starts/stops sessions, spawns a networked
    /// character for every player once the map scene is loaded (server/host only), and collects
    /// local input each tick. Round rules come in Phase 5 (MatchController).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSession : MonoBehaviour, INetworkRunnerCallbacks
    {
        private const string Tag = nameof(NetworkSession);

        private readonly Dictionary<PlayerRef, NetworkObject> characters = new Dictionary<PlayerRef, NetworkObject>();
        private NetworkSettings settings;
        private bool sceneReady;
        private int spawnCounter;
        private float botSeed;

        public static NetworkSession Current { get; private set; }

        public NetworkRunner Runner { get; private set; }

        public NetworkLaunchOptions Options { get; private set; }

        public IReadOnlyDictionary<PlayerRef, NetworkObject> Characters => characters;

        public event Action<ShutdownReason> Stopped;

        public static async Task<StartGameResult> StartAsync(NetworkLaunchOptions options)
        {
            if (Current != null)
            {
                await Current.StopAsync();
            }

            var go = new GameObject($"[NetworkSession {options.Role}]");
            DontDestroyOnLoad(go);
            var session = go.AddComponent<NetworkSession>();
            Current = session;
            return await session.BeginAsync(options);
        }

        public async Task StopAsync()
        {
            if (Runner != null && Runner.IsRunning)
            {
                await Runner.Shutdown();
            }
        }

        private async Task<StartGameResult> BeginAsync(NetworkLaunchOptions options)
        {
            Options = options;
            settings = NetworkSettings.Load();

            // A map that is already open was set up for offline play (offline rig, offline session);
            // leave it so Fusion loads a clean copy in online mode.
            if (MapController.Current != null)
            {
                var leave = SceneManager.LoadSceneAsync(Core.SceneManagement.SceneNames.MainMenu, LoadSceneMode.Single);
                while (leave != null && !leave.isDone)
                {
                    await Awaitable.NextFrameAsync();
                }
            }
            botSeed = UnityEngine.Random.Range(0f, 100f);

            Runner = gameObject.AddComponent<NetworkRunner>();
            Runner.ProvideInput = options.Role != SessionRole.Server;
            gameObject.AddComponent<NetworkObstacleClock>();
            var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
            Runner.AddCallbacks(this);

            GameSession.Mode = GameSessionMode.Online;

            var args = new StartGameArgs
            {
                GameMode = ToGameMode(options.Role),
                SessionName = options.SessionName,
                PlayerCount = Mathf.Clamp(options.MaxPlayers, 1, settings.MaxPlayers),
                SceneManager = sceneManager,
                IsVisible = options.IsVisible,
                IsOpen = true,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    ["mode"] = options.GameModeId,
                    ["proto"] = settings.ProtocolVersion
                }
            };

            if (options.Role != SessionRole.Client)
            {
                args.Scene = SceneRef.FromIndex(BuildIndexOf(options.MapScene));
            }

            if (options.Role != SessionRole.Single)
            {
                args.CustomPhotonAppSettings = settings.BuildAppSettings();
            }

            if (options.Role == SessionRole.Server)
            {
                ConfigureServerAddress(args);
            }

            Log.Info(Tag, $"Starting {options.Role} session '{options.SessionName}' on {options.MapScene}.");
            var result = await Runner.StartGame(args);
            if (!result.Ok)
            {
                Log.Error(Tag, $"StartGame failed: {result.ShutdownReason} {result.ErrorMessage}");
                GameSession.Mode = GameSessionMode.Offline;
                Current = null;
                Destroy(gameObject);
            }

            return result;
        }

        /// <summary>
        /// Binds the dedicated server to a fixed UDP port and advertises its public address so
        /// clients connect directly (Photon relay stays as fallback). Values come from the hosting
        /// provider (Edgegap injects ARBITRIUM_* variables) or the command line.
        /// </summary>
        private static void ConfigureServerAddress(StartGameArgs args)
        {
            var internalPort = ParsePort(Environment.GetEnvironmentVariable("ARBITRIUM_PORT_GAMEPORT_INTERNAL"))
                               ?? ParsePort(CommandLine.Get("-port"));
            if (internalPort == null)
            {
                return;
            }

            args.Address = NetAddress.Any(internalPort.Value);

            var publicIp = Environment.GetEnvironmentVariable("ARBITRIUM_PUBLIC_IP") ?? CommandLine.Get("-publicIp");
            var publicPort = ParsePort(Environment.GetEnvironmentVariable("ARBITRIUM_PORT_GAMEPORT_EXTERNAL"))
                             ?? ParsePort(CommandLine.Get("-publicPort"))
                             ?? internalPort;
            if (!string.IsNullOrWhiteSpace(publicIp))
            {
                args.CustomPublicAddress = NetAddress.CreateFromIpPort(publicIp, publicPort.Value);
            }

            Log.Info(Tag, $"Server bound to UDP {internalPort}, public {publicIp ?? "(auto)"}:{publicPort}");
        }

        private static ushort? ParsePort(string value)
        {
            return ushort.TryParse(value, out var port) && port > 0 ? port : (ushort?)null;
        }

        private static GameMode ToGameMode(SessionRole role)
        {
            switch (role)
            {
                case SessionRole.Host:
                    return GameMode.Host;
                case SessionRole.Client:
                    return GameMode.Client;
                case SessionRole.Server:
                    return GameMode.Server;
                default:
                    return GameMode.Single;
            }
        }

        public static int BuildIndexOf(string sceneName)
        {
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    return i;
                }
            }

            throw new InvalidOperationException($"Scene '{sceneName}' is not in Build Settings.");
        }

        // ------------------------------------------------------------------------------------
        // Spawning (server / host / single only)
        // ------------------------------------------------------------------------------------
        private void SpawnCharacter(NetworkRunner runner, PlayerRef player)
        {
            if (characters.ContainsKey(player) || settings.PlayerPrefab == null)
            {
                return;
            }

            var index = spawnCounter++;
            var map = MapController.Current;
            var spawn = map != null ? map.GetSpawnPoint(index) : null;
            var position = spawn != null ? spawn.position : new Vector3(0f, 1f, 0f);
            var rotation = spawn != null ? spawn.rotation : Quaternion.identity;

            var character = runner.Spawn(settings.PlayerPrefab, position, rotation, player, (r, obj) =>
            {
                var networkCharacter = obj.GetComponent<NetworkCharacter>();
                networkCharacter.ColorIndex = (byte)(index % 8);
            });

            runner.SetPlayerObject(player, character);
            characters[player] = character;
            Log.Info(Tag, $"Spawned character for {player} at spawn {index}.");
        }

        private void SpawnAllPending(NetworkRunner runner)
        {
            foreach (var player in runner.ActivePlayers)
            {
                SpawnCharacter(runner, player);
            }
        }

        // ------------------------------------------------------------------------------------
        // INetworkRunnerCallbacks
        // ------------------------------------------------------------------------------------
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Log.Info(Tag, $"Player joined: {player} (active {runner.SessionInfo.PlayerCount})");
            if (runner.IsServer && sceneReady)
            {
                SpawnCharacter(runner, player);
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer && characters.TryGetValue(player, out var character))
            {
                if (character != null)
                {
                    runner.Despawn(character);
                }

                characters.Remove(player);
            }
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
            sceneReady = false;
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            sceneReady = true;
            if (runner.IsServer)
            {
                SpawnAllPending(runner);
            }
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (Options != null && Options.IsBotClient)
            {
                input.Set(BotInput(runner));
                return;
            }

            var reader = PlayerInputReader.Local;
            if (reader != null)
            {
                input.Set(NetworkInputData.FromMotorInput(reader.ConsumeInput()));
            }
        }

        private NetworkInputData BotInput(NetworkRunner runner)
        {
            // Load-test bot: runs down the course weaving left/right and jumping now and then.
            var t = runner.Tick.Raw * runner.DeltaTime + botSeed;
            var move = new Vector2(Mathf.Sin(t * 0.7f) * 0.5f, 1f).normalized;
            var jump = Mathf.Repeat(t, 2.3f) < 0.15f;
            return NetworkInputData.FromMotorInput(new MotorInput { Move = move, JumpHeld = jump, JumpPressed = jump });
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Log.Info(Tag, $"Session shut down: {shutdownReason}");
            characters.Clear();
            GameSession.Mode = GameSessionMode.Offline;
            if (Current == this)
            {
                Current = null;
            }

            Stopped?.Invoke(shutdownReason);
            Destroy(gameObject);
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnConnectedToServer(NetworkRunner runner) => Log.Info(Tag, "Connected to server.");
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) => Log.Warning(Tag, $"Disconnected: {reason}");
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) => Log.Warning(Tag, $"Connect failed: {reason}");
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    }
}
