using KnockRush.Backend;
using KnockRush.Core.Events;
using UnityEngine;

namespace KnockRush.Inventory
{
    /// <summary>
    /// The local player's equipped loadout: mirrors the backend profile when signed in and keeps
    /// a local copy (PlayerPrefs) so the right outfit shows offline and before sign-in completes.
    /// </summary>
    public static class LocalLoadoutStore
    {
        private const string PrefsKey = "knockrush.loadout";
        private static Loadout? current;

        public static Loadout Current
        {
            get
            {
                if (current == null)
                {
                    var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                    current = string.IsNullOrEmpty(json) ? Loadout.Default : JsonUtility.FromJson<Loadout>(json);
                }

                return current.Value;
            }
        }

        public static void Set(Loadout loadout)
        {
            current = loadout;
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(loadout));
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            current = null;
            EventBus.Subscribe<ProfileUpdatedEvent>(OnProfileUpdated);
        }

        private static void OnProfileUpdated(ProfileUpdatedEvent updated)
        {
            if (updated.Profile?.Equipped != null)
            {
                Set(Loadout.FromEquipped(updated.Profile.Equipped));
            }
        }
    }
}
