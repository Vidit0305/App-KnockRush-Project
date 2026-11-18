using System;
using KnockRush.Core;
using KnockRush.Core.SceneManagement;
using KnockRush.Utilities;
using UnityEngine;

namespace KnockRush.Multiplayer
{
    /// <summary>
    /// Starts headless roles from the command line (bypassing the menu):
    ///
    ///   Dedicated server : KnockRush.x86_64 -batchmode -nographics -server -session eu-01 [-map HammerRush] [-maxPlayers 32] [-region eu] [-photonAppId ID]
    ///   Load-test bot    : KnockRush.exe -batchmode -nographics -botclient -session eu-01 [-photonAppId ID]
    ///   Local single     : -single  (Fusion single-player mode; no Photon connection)
    ///
    /// Linux Dedicated Server builds (UNITY_SERVER) always start as a server.
    /// </summary>
    public static class CommandLineLauncher
    {
        private const string Tag = nameof(CommandLineLauncher);

        public static bool IsHeadlessLaunch =>
            AppRoot.IsDedicatedServer || CommandLine.Has("-server") || CommandLine.Has("-botclient") || CommandLine.Has("-single");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static async void Launch()
        {
            if (!IsHeadlessLaunch)
            {
                return;
            }

            try
            {
                await AppRoot.InitializationTask;

                var role = AppRoot.IsDedicatedServer || CommandLine.Has("-server")
                    ? SessionRole.Server
                    : CommandLine.Has("-single") ? SessionRole.Single : SessionRole.Client;

                var options = new NetworkLaunchOptions
                {
                    Role = role,
                    SessionName = CommandLine.Get("-session", "knockrush-dev"),
                    MapScene = CommandLine.Get("-map", SceneNames.HammerRush),
                    MaxPlayers = CommandLine.GetInt("-maxPlayers", 32),
                    IsBotClient = CommandLine.Has("-botclient")
                };

                Log.Info(Tag, $"Headless launch as {role} (session '{options.SessionName}').");
                var result = await NetworkSession.StartAsync(options);
                if (!result.Ok)
                {
                    Log.Error(Tag, $"Could not start: {result.ShutdownReason}. Exiting.");
                    Application.Quit(2);
                    return;
                }

                var stopAfter = CommandLine.GetInt("-quitAfterSeconds", 0);
                if (stopAfter > 0)
                {
                    await Awaitable.WaitForSecondsAsync(stopAfter);
                    Log.Info(Tag, $"Quitting after {stopAfter}s as requested.");
                    await NetworkSession.Current.StopAsync();
                    Application.Quit(0);
                }
            }
            catch (Exception exception)
            {
                Log.Exception(exception);
                Application.Quit(1);
            }
        }
    }
}
