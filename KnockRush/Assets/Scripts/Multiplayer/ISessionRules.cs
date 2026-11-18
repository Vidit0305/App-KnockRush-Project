using Fusion;

namespace KnockRush.Multiplayer
{
    /// <summary>
    /// Implemented by the server-side round controller (GameModes). When present, the session
    /// delegates player lifecycle to it instead of free-roam spawning.
    /// </summary>
    public interface ISessionRules
    {
        void OnPlayerJoined(PlayerRef player);

        void OnPlayerLeft(PlayerRef player);

        void OnSceneLoadDone();
    }

    /// <summary>Persistent per-device player id (Phase 7 replaces it with the Firebase UID).</summary>
    public static class LocalPlayerId
    {
        private const string PrefsKey = "knockrush.playerid";

        public static string Get()
        {
            var id = UnityEngine.PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = System.Guid.NewGuid().ToString("N");
                UnityEngine.PlayerPrefs.SetString(PrefsKey, id);
                UnityEngine.PlayerPrefs.Save();
            }

            return id;
        }
    }
}
