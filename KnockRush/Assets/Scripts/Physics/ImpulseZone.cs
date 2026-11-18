using System.Collections.Generic;
using KnockRush.Core.Events;
using UnityEngine;

namespace KnockRush.Interaction
{
    public enum ImpulseMode
    {
        /// <summary>Push along the obstacle's own velocity at the contact point (hammers, sweepers).</summary>
        ObstacleVelocity = 0,

        /// <summary>Push away from this zone's centre (bumpers).</summary>
        Radial = 1,

        /// <summary>Push along a fixed local direction (bounce pads, launchers, wind).</summary>
        Directional = 2
    }

    /// <summary>
    /// Trigger volume that knocks characters. Put it on a trigger collider (layer Trigger) on an
    /// obstacle. On the dedicated server (Phase 4) only the server applies impulses.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ImpulseZone : MonoBehaviour
    {
        [SerializeField] private ImpulseMode mode = ImpulseMode.ObstacleVelocity;

        [Tooltip("Base push speed (m/s).")]
        [SerializeField, Min(0f)] private float strength = 9f;

        [Tooltip("Extra push per m/s of obstacle speed (ObstacleVelocity mode).")]
        [SerializeField, Min(0f)] private float velocityScale = 0.6f;

        [Tooltip("Upward speed added to every hit (m/s), for that satisfying launch.")]
        [SerializeField, Min(0f)] private float upwardBoost = 4f;

        [Tooltip("Local direction for Directional mode.")]
        [SerializeField] private Vector3 localDirection = Vector3.up;

        [Tooltip("ObstacleVelocity mode ignores contacts slower than this (m/s).")]
        [SerializeField, Min(0f)] private float minimumObstacleSpeed = 0.75f;

        [Tooltip("Seconds before the same target can be hit again.")]
        [SerializeField, Min(0f)] private float cooldown = 0.4f;

        private readonly Dictionary<IKnockbackTarget, float> lastHitTime = new Dictionary<IKnockbackTarget, float>();
        private IPointVelocityProvider velocityProvider;

        public ImpulseMode Mode => mode;

        public void Configure(ImpulseMode impulseMode, float pushStrength, float upward, Vector3 direction)
        {
            mode = impulseMode;
            strength = pushStrength;
            upwardBoost = upward;
            localDirection = direction;
        }

        private void Awake()
        {
            velocityProvider = GetComponentInParent<IPointVelocityProvider>();
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHit(other);
        }

        private void TryHit(Collider other)
        {
            var target = other.GetComponentInParent<IKnockbackTarget>();
            if (target == null)
            {
                return;
            }

            var now = Time.time;
            if (lastHitTime.TryGetValue(target, out var last) && now - last < cooldown)
            {
                return;
            }

            if (!TryComputeImpulse(target, out var impulse))
            {
                return;
            }

            lastHitTime[target] = now;
            target.ApplyImpulse(impulse);
            EventBus.Publish(new KnockbackEvent(target, impulse, target.Position + Vector3.up));
        }

        public bool TryComputeImpulse(IKnockbackTarget target, out Vector3 impulse)
        {
            var targetCenter = target.Position + Vector3.up * 0.75f;
            switch (mode)
            {
                case ImpulseMode.ObstacleVelocity:
                {
                    var velocity = velocityProvider != null ? velocityProvider.GetPointVelocity(targetCenter) : Vector3.zero;
                    var flat = new Vector3(velocity.x, 0f, velocity.z);
                    if (flat.magnitude < minimumObstacleSpeed)
                    {
                        // Slow contact (e.g. hammer at the end of its swing): push away gently instead.
                        flat = targetCenter - transform.position;
                        flat.y = 0f;
                        impulse = flat.normalized * (strength * 0.35f) + Vector3.up * (upwardBoost * 0.5f);
                        return flat.sqrMagnitude > 0.0001f;
                    }

                    impulse = flat.normalized * (strength + flat.magnitude * velocityScale) + Vector3.up * upwardBoost;
                    return true;
                }

                case ImpulseMode.Radial:
                {
                    var away = targetCenter - transform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.0001f)
                    {
                        away = Vector3.forward;
                    }

                    impulse = away.normalized * strength + Vector3.up * upwardBoost;
                    return true;
                }

                default:
                    impulse = transform.TransformDirection(localDirection).normalized * strength + Vector3.up * upwardBoost;
                    return true;
            }
        }

        private void OnDisable()
        {
            lastHitTime.Clear();
        }
    }
}
