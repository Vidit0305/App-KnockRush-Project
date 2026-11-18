using KnockRush.Interaction;
using UnityEngine;

namespace KnockRush.Maps
{
    /// <summary>
    /// Scene-level description of a map instance: definition, spawn points and fall handling.
    /// One per map scene. The round logic (offline session now, networked rounds in Phase 5)
    /// reads spawn points from here and asks it what happens when someone falls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapController : MonoBehaviour
    {
        [SerializeField] private MapDefinition definition;
        [SerializeField] private Transform[] spawnPoints = new Transform[0];

        [Tooltip("Characters below this height have fallen out of the map.")]
        [SerializeField] private float killHeight = -12f;

        public static MapController Current { get; private set; }

        public MapDefinition Definition => definition;

        public float KillHeight => killHeight;

        public int SpawnPointCount => spawnPoints.Length;

        /// <summary>Used by the map builders (editor) to wire generated scenes.</summary>
        public void Configure(MapDefinition mapDefinition, Transform[] points, float fallHeight)
        {
            definition = mapDefinition;
            spawnPoints = points;
            killHeight = fallHeight;
        }

        public Transform GetSpawnPoint(int index)
        {
            return spawnPoints.Length == 0 ? transform : spawnPoints[Mathf.Abs(index) % spawnPoints.Length];
        }

        /// <summary>Called by kill volumes / kill height. Offline: respawn. Phase 5: server eliminates on survival maps.</summary>
        public void HandleFall(ICourseParticipant participant)
        {
            if (participant == null || participant.HasFinished)
            {
                return;
            }

            participant.Respawn();
            Core.Events.EventBus.Publish(new ParticipantFellEvent(participant));
        }

        private void Awake()
        {
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            foreach (var point in spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position + Vector3.up * 0.75f, 0.45f);
                    Gizmos.DrawLine(point.position + Vector3.up * 0.75f, point.position + Vector3.up * 0.75f + point.forward);
                }
            }
        }
    }
}
