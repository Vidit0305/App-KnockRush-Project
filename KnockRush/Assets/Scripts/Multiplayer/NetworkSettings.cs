using System;
using Fusion;
using Fusion.Photon.Realtime;
using KnockRush.Core;
using KnockRush.Utilities;
using UnityEngine;
using Log = KnockRush.Utilities.Log;

namespace KnockRush.Multiplayer
{
    /// <summary>
    /// Networking configuration. Lives in a Resources folder (NetworkSettings.asset).
    ///
    /// Photon App ID resolution order (first non-empty wins):
    ///   1. command line  -photonAppId &lt;id&gt;            (dedicated servers, load tests, CI)
    ///   2. environment   KNOCKRUSH_PHOTON_APPID          (dedicated server containers)
    ///   3. Assets/Photon/Fusion/Resources/PhotonAppSettings.asset (client builds)
    /// </summary>
    [CreateAssetMenu(menuName = "KnockRush/Config/Network Settings", fileName = "NetworkSettings", order = 1)]
    public sealed class NetworkSettings : ScriptableObject
    {
        public const string ResourcesPath = "NetworkSettings";
        public const string AppIdArgument = "-photonAppId";
        public const string AppIdEnvironmentVariable = "KNOCKRUSH_PHOTON_APPID";

        [Tooltip("Networked player prefab (NetworkObject + NetworkCharacter).")]
        [SerializeField] private NetworkObject playerPrefab;

        [Tooltip("Networked round controller prefab (spawned by the server for game modes).")]
        [SerializeField] private NetworkObject matchControllerPrefab;

        [Tooltip("Sessions only match clients with the same protocol version. Bump on any network-breaking change.")]
        [SerializeField] private string protocolVersion = "0.4";

        [SerializeField, Range(2, 32)] private int maxPlayers = 32;

        [Tooltip("Optional fixed Photon region (e.g. 'eu', 'us', 'asia', 'in'). Empty = best ping.")]
        [SerializeField] private string fixedRegion = string.Empty;

        public NetworkObject PlayerPrefab => playerPrefab;
        public NetworkObject MatchControllerPrefab => matchControllerPrefab;
        public string ProtocolVersion => protocolVersion;
        public int MaxPlayers => maxPlayers;

        public void Configure(NetworkObject prefab)
        {
            playerPrefab = prefab;
        }

        public void ConfigureMatchController(NetworkObject prefab)
        {
            matchControllerPrefab = prefab;
        }

        public static NetworkSettings Load()
        {
            var settings = Resources.Load<NetworkSettings>(ResourcesPath);
            if (settings == null)
            {
                throw new InvalidOperationException("NetworkSettings asset missing from Resources. Run KnockRush > Build > All Generated Content.");
            }

            return settings;
        }

        public static string ResolveAppId()
        {
            var fromArgs = CommandLine.Get(AppIdArgument);
            if (!string.IsNullOrWhiteSpace(fromArgs))
            {
                return fromArgs.Trim();
            }

            var fromEnvironment = Environment.GetEnvironmentVariable(AppIdEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment.Trim();
            }

            return PhotonAppSettings.TryGetGlobal(out var global) ? global.AppSettings.AppIdFusion : null;
        }

        public static bool HasAppId => !string.IsNullOrWhiteSpace(ResolveAppId());

        /// <summary>App settings for StartGameArgs with the resolved App ID / region applied.</summary>
        public FusionAppSettings BuildAppSettings()
        {
            if (!PhotonAppSettings.TryGetGlobal(out var global))
            {
                throw new InvalidOperationException("PhotonAppSettings asset not found.");
            }

            var copy = global.AppSettings.GetCopy();
            copy.AppIdFusion = ResolveAppId();
            copy.AppVersion = protocolVersion;
            var region = CommandLine.Get("-region", fixedRegion);
            if (!string.IsNullOrWhiteSpace(region))
            {
                copy.FixedRegion = region.Trim();
            }

            if (string.IsNullOrWhiteSpace(copy.AppIdFusion))
            {
                Log.Warning(nameof(NetworkSettings), "No Photon Fusion App ID configured. Online modes will fail; see Docs/PHASE_4_NETWORKING.md.");
            }

            return copy;
        }
    }
}
