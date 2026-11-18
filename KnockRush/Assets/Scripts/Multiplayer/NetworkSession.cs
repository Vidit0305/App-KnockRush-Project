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
        Server,

        /// <summary>Join a matching session, or host one if none exists (development / peer play).</summary>
        AutoHostOrClient
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

        /// <summary>Stable player id (reconnection). Ignored when <see cref="AuthToken"/> is set.</summary>
        public string UserId;

        /// <summary>
        /// Firebase ID token. When set, Photon custom authentication verifies it with the backend
        /// (photonAuth) and the verified Firebase UID becomes the player's UserId.
        /// </summary>
        public string AuthToken;

        /// <summary>Fill empty slots with server bots when the match starts.</summary>
        public bool FillWithBots = true;

        /// <summary>Seconds the lobby waits before starting (0 = mode default).</summary>
        public float LobbySeconds;

        /// <summary>Overrides every round's time limit (testing). 0 = map default.</summary>
        public float RoundTimeLimitOverride;

        /// <summary>Private rooms are hidden from matchmaking and joined by code.</summary>
        public bool IsPrivate;
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

        /// <summary>Round rules (MatchController) installed on the server; null = free roam.</summary>
        public ISessionRules Rules { get; set; }

        public NetworkSettings Settings => settings;

        /// <summary>True when the session runs a game mode (anything except free roam).</summary>
        public bool WantsMatch => Options != null && !string.IsNullOrEmpty(Options.GameModeId) && Options.GameModeId != "free"
                                  && settings != null && settings.MatchControllerPrefab != null;

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
                IsVisible = options.IsVisible && !options.IsPrivate,
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

            if (!string.IsNullOrEmpty(options.AuthToken) && options.Role != SessionRole.Single)
            {
                var auth = new Photon.Realtime.AuthenticationValues { AuthType = Photon.Realtime.CustomAuthenticationType.Custom };
                auth.AddAuthParameter("token", options.AuthToken);
                args.AuthValues = auth;
            }
            else if (!string.IsNullOrEmpty(options.UserId))
            {
                args.AuthValues = new Photon.Realtime.AuthenticationValues(options.UserId);
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
                case SessionRole.AutoHostOrClient:
                    return GameMode.AutoHostOrClient;
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
            SpawnCharacter(player, index, false, (byte)(index % 8));
            Log.Info(Tag, $"Spawned character for {player} at spawn {index}.");
        }

        /// <summary>
        /// Spawns a networked character at a map spawn point (server only). Bots pass
        /// <see cref="PlayerRef.None"/> as owner.
        /// </summary>
        public NetworkCharacter SpawnCharacter(PlayerRef owner, int spawnIndex, bool isBot, byte colorIndex)
        {
            var map = MapController.Current;
            var spawn = map != null ? map.GetSpawnPoint(spawnIndex) : null;
            var position = spawn != null ? spawn.position : new Vector3(0f, 1f, 0f);
            var rotation = spawn != null ? spawn.rotation : Quaternion.identity;

            var obj = Runner.Spawn(settings.PlayerPrefab, position, rotation, isBot ? PlayerRef.None : owner, (r, o) =>
            {
                var networkCharacter = o.GetComponent<NetworkCharacter>();
                networkCharacter.ColorIndex = colorIndex;
                networkCharacter.IsBot = isBot;
            });

            if (!isBot)
            {
                Runner.SetPlayerObject(owner, obj);
                characters[owner] = obj;
            }

            return obj.GetComponent<NetworkCharacter>();
        }

        public void ForgetCharacter(PlayerRef player)
        {
            characters.Remove(player);
        }

        private void SpawnMatchController(NetworkRunner runner)
        {
            if (Rules == null)
            {
                runner.Spawn(settings.MatchControllerPrefab, Vector3.zero, Quaternion.identity, PlayerRef.None);
            }
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
            if (!runner.IsServer)
            {
                return;
            }

            if (Rules != null)
            {
                Rules.OnPlayerJoined(player);
            }
            else if (sceneReady && !WantsMatch)
            {
                SpawnCharacter(runner, player);
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer && Rules != null)
            {
                Rules.OnPlayerLeft(player);
                characters.Remove(player);
                return;
            }

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
            if (!runner.IsServer)
            {
                return;
            }

            if (Rules != null)
            {
                Rules.OnSceneLoadDone();
            }
            else if (WantsMatch)
            {
                SpawnMatchController(runner);
            }
            else
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
            Rules = null;
            MapController.FallInterceptor = null;
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
