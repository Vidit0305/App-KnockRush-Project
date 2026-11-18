using KnockRush.Core;
using UnityEngine;

namespace KnockRush.Maps
{
    /// <summary>
    /// Keeps map scenes content-only. On load it adds the right "rig" for how the map is played:
    ///  - Offline: player + camera + controls + HUD + offline race session (practice / tests)
    ///  - Online client/host: camera + controls + HUD (the network layer spawns the characters)
    ///  - Dedicated server: nothing (no camera, UI or audio on a headless server)
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class MapBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject offlineRigPrefab;
        [SerializeField] private GameObject onlineRigPrefab;

        [Tooltip("Spawn point index used by the offline player.")]
        [SerializeField, Min(0)] private int offlineSpawnIndex = 3;

        public GameObject SpawnedRig { get; private set; }

        public void Configure(GameObject offlineRig, GameObject onlineRig, int spawnIndex)
        {
            offlineRigPrefab = offlineRig;
            onlineRigPrefab = onlineRig;
            offlineSpawnIndex = spawnIndex;
        }

        private void Awake()
        {
            if (AppRoot.IsDedicatedServer)
            {
                return;
            }

            if (GameSession.IsOnline)
            {
                if (onlineRigPrefab != null)
                {
                    SpawnedRig = Instantiate(onlineRigPrefab);
                    SpawnedRig.name = onlineRigPrefab.name;
                }

                return;
            }

            if (offlineRigPrefab == null)
            {
                return;
            }

            var map = GetComponent<MapController>() ?? FindFirstObjectByType<MapController>();
            var spawn = map != null ? map.GetSpawnPoint(offlineSpawnIndex) : transform;
            SpawnedRig = Instantiate(offlineRigPrefab, spawn.position, spawn.rotation);
            SpawnedRig.name = offlineRigPrefab.name;
        }
    }
}
