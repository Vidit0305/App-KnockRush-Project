using UnityEngine;

namespace KnockRush.Obstacles
{
    /// <summary>
    /// Appears and disappears on a fixed cycle with a blinking warning (disappearing bridges,
    /// "now you see it" platforms). Collision is removed while hidden.
    /// </summary>
    public sealed class CyclingPlatform : TimeDrivenObstacle
    {
        [SerializeField, Min(0.5f)] private float period = 5f;
        [SerializeField, Range(0.1f, 0.95f)] private float solidFraction = 0.6f;
        [SerializeField, Min(0f)] private float warningSeconds = 1f;

        private Collider[] colliders;
        private Renderer[] renderers;
        private bool lastSolid = true;
        private bool lastVisible = true;

        public void Configure(float periodSeconds, float solid, float warning)
        {
            period = periodSeconds;
            solidFraction = solid;
            warningSeconds = warning;
        }

        public static bool IsSolidAt(double time, float periodSeconds, float solid)
        {
            var cycle = time / periodSeconds % 1d;
            if (cycle < 0d)
            {
                cycle += 1d;
            }

            return cycle < solid;
        }

        protected override void Awake()
        {
            base.Awake();
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        protected override void Evaluate(double time, out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = BasePosition;
            localRotation = BaseRotation;
        }

        protected override void OnTimeApplied(double time)
        {
            var solid = IsSolidAt(time, period, solidFraction);
            var cycle = (float)(time / period % 1d);
            var untilHidden = (solidFraction - cycle) * period;
            var blinking = solid && untilHidden < warningSeconds && Mathf.Repeat((float)time * 8f, 1f) < 0.5f;
            var visible = solid && !blinking;

            if (solid != lastSolid)
            {
                foreach (var c in colliders)
                {
                    if (!c.isTrigger)
                    {
                        c.enabled = solid;
                    }
                }

                lastSolid = solid;
            }

            if (visible != lastVisible)
            {
                foreach (var r in renderers)
                {
                    r.enabled = visible;
                }

                lastVisible = visible;
            }
        }
    }
}
