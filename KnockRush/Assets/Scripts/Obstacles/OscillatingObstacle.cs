using UnityEngine;

namespace KnockRush.Obstacles
{
    /// <summary>Moves smoothly between its start position and an offset (moving platforms, pistons, sliding walls).</summary>
    public sealed class OscillatingObstacle : TimeDrivenObstacle
    {
        [Tooltip("Offset in the parent's space at the far end of the motion.")]
        [SerializeField] private Vector3 offset = new Vector3(4f, 0f, 0f);
        [SerializeField, Min(0.1f)] private float period = 4f;

        [Tooltip("Fraction of each half-cycle spent paused at the ends (0 = continuous).")]
        [SerializeField, Range(0f, 0.8f)] private float endPause = 0.15f;

        public void Configure(Vector3 travel, float periodSeconds, float pause)
        {
            offset = travel;
            period = periodSeconds;
            endPause = pause;
        }

        /// <summary>0..1 position along the path at <paramref name="time"/>.</summary>
        public static float Progress(double time, float periodSeconds, float pause)
        {
            var cycle = (float)(time / periodSeconds % 1d);
            if (cycle < 0f)
            {
                cycle += 1f;
            }

            // Triangle wave 0 -> 1 -> 0 with pauses at both ends, eased with smoothstep.
            var half = cycle < 0.5f ? cycle * 2f : (1f - cycle) * 2f;
            var moving = Mathf.Clamp01((half - pause * 0.5f) / Mathf.Max(0.0001f, 1f - pause));
            return moving * moving * (3f - 2f * moving);
        }

        protected override void Evaluate(double time, out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = BasePosition + offset * Progress(time, period, endPause);
            localRotation = BaseRotation;
        }
    }
}
