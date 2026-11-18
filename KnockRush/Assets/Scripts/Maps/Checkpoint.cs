using KnockRush.Core.Events;
using KnockRush.Interaction;
using UnityEngine;

namespace KnockRush.Maps
{
    /// <summary>
    /// Trigger volume that moves a participant's respawn point forward. Orders must increase
    /// along the course; reaching a lower-order checkpoint never moves the respawn point back.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Checkpoint : MonoBehaviour
    {
        [SerializeField, Min(0)] private int order;

        [Tooltip("Respawn locations (spread so respawning players don't stack). Defaults to this transform.")]
        [SerializeField] private Transform[] respawnPoints = new Transform[0];

        public int Order => order;

        public void Configure(int checkpointOrder, Transform[] points)
        {
            order = checkpointOrder;
            respawnPoints = points;
        }

        public Transform GetRespawnPoint(int participantId)
        {
            return respawnPoints.Length == 0 ? transform : respawnPoints[Mathf.Abs(participantId) % respawnPoints.Length];
        }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var participant = other.GetComponentInParent<ICourseParticipant>();
            if (participant == null || participant.CheckpointOrder >= order)
            {
                return;
            }

            participant.CheckpointOrder = order;
            var point = GetRespawnPoint(participant.ParticipantId);
            participant.SetRespawnPoint(point.position, point.eulerAngles.y);
            EventBus.Publish(new CheckpointReachedEvent(participant, order));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.DrawCube(box.center, box.size);
            }
        }
    }
}
