using KnockRush.Interaction;
using UnityEngine;

namespace KnockRush.Maps
{
    /// <summary>Trigger volume (water, void, lava) that counts as falling out of the map.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class KillVolume : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var participant = other.GetComponentInParent<ICourseParticipant>();
            if (participant == null)
            {
                return;
            }

            if (MapController.Current != null)
            {
                MapController.Current.HandleFall(participant);
            }
            else
            {
                participant.Respawn();
            }
        }
    }
}
