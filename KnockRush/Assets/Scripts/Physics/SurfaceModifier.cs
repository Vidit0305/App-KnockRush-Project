using KnockRush.Movement;
using UnityEngine;

namespace KnockRush.Interaction
{
    /// <summary>
    /// Static surface behaviour for level geometry: slippery slime/ice (low friction),
    /// sticky goo (high friction) and conveyor belts (constant surface velocity).
    /// Moving obstacles get this behaviour from TimeDrivenObstacle instead.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurfaceModifier : MonoBehaviour, IMovementSurface
    {
        [Tooltip("1 = normal grip, 0.15 = slime/ice, 2 = sticky.")]
        [SerializeField, Range(0.05f, 3f)] private float friction = 1f;

        [Tooltip("Conveyor velocity in this object's local space (m/s).")]
        [SerializeField] private Vector3 localConveyorVelocity = Vector3.zero;

        public float FrictionMultiplier => friction;

        public void Configure(float frictionMultiplier, Vector3 conveyorLocalVelocity)
        {
            friction = frictionMultiplier;
            localConveyorVelocity = conveyorLocalVelocity;
        }

        public Vector3 GetSurfaceVelocity(Vector3 worldPoint)
        {
            return localConveyorVelocity == Vector3.zero ? Vector3.zero : transform.TransformDirection(localConveyorVelocity);
        }
    }
}
