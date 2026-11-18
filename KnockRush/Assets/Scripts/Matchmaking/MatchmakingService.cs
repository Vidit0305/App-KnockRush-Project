using System;
using System.Threading.Tasks;
using Fusion;
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

        /// <summary>Joins a public match of <paramref name="modeId"/> ("classic", "quickrace").</summary>
        public static Task<StartGameResult> QuickPlayAsync(string modeId)
        {
            return NetworkSession.StartAsync(new NetworkLaunchOptions
            {
                Role = Backend == MatchmakingBackend.DedicatedServers ? SessionRole.Client : SessionRole.AutoHostOrClient,
                SessionName = null, // random matching session
                GameModeId = modeId,
                MapScene = SceneNames.HammerRush,
                UserId = LocalPlayerId.Get(),
                FillWithBots = true
            });
        }

        /// <summary>Creates a hidden private room and returns its code (the creator hosts it).</summary>
        public static async Task<(StartGameResult result, string code)> CreatePrivateRoomAsync(string modeId = "private", bool bots = false)
        {
            var code = RoomCodes.Generate(new Random());
            var result = await NetworkSession.StartAsync(new NetworkLaunchOptions
            {
                Role = SessionRole.Host,
                SessionName = RoomCodes.ToSessionName(code),
                GameModeId = modeId,
                MapScene = SceneNames.HammerRush,
                UserId = LocalPlayerId.Get(),
                IsPrivate = true,
                FillWithBots = bots,
                LobbySeconds = 600f // the owner starts the match
            });
            return (result, code);
        }

        public static Task<StartGameResult> JoinPrivateRoomAsync(string typedCode)
        {
            var code = RoomCodes.Normalize(typedCode);
            if (!RoomCodes.IsValid(code))
            {
                throw new ArgumentException($"'{typedCode}' is not a valid room code.");
            }

            return NetworkSession.StartAsync(new NetworkLaunchOptions
            {
                Role = SessionRole.Client,
                SessionName = RoomCodes.ToSessionName(code),
                GameModeId = "private",
                UserId = LocalPlayerId.Get()
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
