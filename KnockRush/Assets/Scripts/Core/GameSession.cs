using System;
using UnityEngine;

namespace KnockRush.Core
{
    public enum GameSessionMode
    {
        /// <summary>Single device, no networking (practice, Playground, tests).</summary>
        Offline = 0,

        /// <summary>Connected to a networked session (client, host or dedicated server).</summary>
        Online = 1
    }

    /// <summary>
    /// Process-wide session mode. Map scenes read it to decide whether to spawn an offline player
    /// rig or wait for the network layer to spawn networked players.
    /// </summary>
    public static class GameSession
    {
        public static GameSessionMode Mode { get; set; } = GameSessionMode.Offline;

        public static bool IsOnline => Mode == GameSessionMode.Online;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Mode = GameSessionMode.Offline;
        }
    }

    /// <summary>Minimal command-line access (dedicated server and load-test arguments).</summary>
    public static class CommandLine
    {
        public static bool Has(string flag)
        {
            return Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0;
        }

        public static string Get(string flag, string fallback = null)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        public static int GetInt(string flag, int fallback)
        {
            return int.TryParse(Get(flag), out var value) ? value : fallback;
        }
    }
}
