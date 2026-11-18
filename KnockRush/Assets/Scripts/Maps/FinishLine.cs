using KnockRush.Core.Events;
using KnockRush.Interaction;
using UnityEngine;

namespace KnockRush.Maps
{
    /// <summary>Trigger volume at the end of a race course. The round logic decides qualification.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FinishLine : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var participant = other.GetComponentInParent<ICourseParticipant>();
            if (participant != null && !participant.HasFinished)
            {
                EventBus.Publish(new FinishLineCrossedEvent(participant));
            }
        }
    }
}
