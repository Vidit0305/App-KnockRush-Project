using System;
using System.Threading.Tasks;
using Fusion;
using KnockRush.Backend;
using KnockRush.Core.Services;
using KnockRush.Core.SceneManagement;
using KnockRush.Multiplayer;

namespace KnockRush.Matchmaking
{
    public enum MatchmakingBackend
    {
        /// <summary>Production: join a dedicated server session via Photon matchmaking.</summary>
        DedicatedServers,

        /// <summary>Development / peer play: join a matching session or become its host.</summary>
        PeerHosted
    }

    /// <summary>
    /// Entry points used by the menus: quick play per mode, private rooms by code, and local
    /// practice against bots. Sessions are filtered by game mode and protocol version, so players
    /// only meet compatible builds in the same mode.
    /// </summary>
    public static class MatchmakingService
    {
        public static MatchmakingBackend Backend { get; set; } = MatchmakingBackend.PeerHosted;

        /// <summary>
        /// Player identity for sessions: the Firebase UID + ID token when signed in (verified by
        /// Photon custom auth), otherwise the local device id (practice / offline development).
        /// </summary>
        public static async Task<(string userId, string token)> GetIdentityAsync()
        {
            if (ServiceLocator.TryGet<IBackendService>(out var backend) && backend.State == BackendState.Online && backend is BackendService service)
            {
                try
                {
                    return (service.Uid, await service.Client.GetIdTokenAsync());
                }
                catch (BackendException)
                {
                    // fall through to the device id
                }
            }

            return (LocalPlayerId.Get(), null);
        }

        /// <summary>Joins a public match of <paramref name="modeId"/> ("classic", "quickrace").</summary>
        public static async Task<StartGameResult> QuickPlayAsync(string modeId)
        {
            var (userId, token) = await GetIdentityAsync();
            return await NetworkSession.StartAsync(new NetworkLaunchOptions
            {
                Role = Backend == MatchmakingBackend.DedicatedServers ? SessionRole.Client : SessionRole.AutoHostOrClient,
                SessionName = null, // random matching session
                GameModeId = modeId,
                MapScene = SceneNames.HammerRush,
                UserId = userId,
                AuthToken = token,
                FillWithBots = true
            });
        }

        /// <summary>Creates a hidden private room and returns its code (the creator hosts it).</summary>
        public static async Task<(StartGameResult result, string code)> CreatePrivateRoomAsync(string modeId = "private", bool bots = false)
        {
            var code = RoomCodes.Generate(new Random());
            var (userId, token) = await GetIdentityAsync();
            var result = await NetworkSession.StartAsync(new NetworkLaunchOptions
            {
                AuthToken = token,
                Role = SessionRole.Host,
                SessionName = RoomCodes.ToSessionName(code),
                GameModeId = modeId,
                MapScene = SceneNames.HammerRush,
                UserId = userId,
                IsPrivate = true,
                FillWithBots = bots,
                LobbySeconds = 600f // the owner starts the match
            });
            return (result, code);
        }

        public static async Task<StartGameResult> JoinPrivateRoomAsync(string typedCode)
        {
            var code = RoomCodes.Normalize(typedCode);
            if (!RoomCodes.IsValid(code))
            {
                throw new ArgumentException($"'{typedCode}' is not a valid room code.");
            }

            var (userId, token) = await GetIdentityAsync();
            return await NetworkSession.StartAsync(new NetworkLaunchOptions
            {
                Role = SessionRole.Client,
                SessionName = RoomCodes.ToSessionName(code),
                GameModeId = "private",
                UserId = userId,
                AuthToken = token
            });
        }

        /// <summary>Full knockout match on this device against bots (no Photon connection needed).</summary>
        public static Task<StartGameResult> PracticeAsync(string modeId, int lobbySeconds = 3)
        {
            return NetworkSession.StartAsync(new NetworkLaunchOptions
            {
                Role = SessionRole.Single,
                SessionName = "practice",
                GameModeId = modeId,
                MapScene = SceneNames.HammerRush,
                UserId = LocalPlayerId.Get(),
                FillWithBots = true,
                LobbySeconds = lobbySeconds
            });
        }
    }
}
