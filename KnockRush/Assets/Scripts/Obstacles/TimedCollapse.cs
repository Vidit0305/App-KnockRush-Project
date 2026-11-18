using UnityEngine;

namespace KnockRush.Obstacles
{
    /// <summary>
    /// Platform that collapses at a scheduled round time and never returns (shrinking safe area in
    /// finals). Blinks for <see cref="warningSeconds"/> before dropping. Deterministic from round
    /// time, so every peer removes the same platform on the same tick.
    /// </summary>
    public sealed class TimedCollapse : TimeDrivenObstacle
    {
        [Tooltip("Round time (seconds after GO) at which the platform drops.")]
        [SerializeField, Min(0f)] private float collapseAt = 30f;
        [SerializeField, Min(0f)] private float warningSeconds = 2.5f;
        [SerializeField, Min(0f)] private float dropDistance = 30f;

        private Collider[] colliders;
        private Renderer[] renderers;
        private bool lastSolid = true;
        private bool lastVisible = true;

        public float CollapseAt => collapseAt;

        public void Configure(float atSeconds, float warning)
        {
            collapseAt = atSeconds;
            warningSeconds = warning;
        }

        public static bool IsSolidAt(double time, float collapseTime) => time < collapseTime;

        protected override void Awake()
        {
            base.Awake();
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        protected override void Evaluate(double time, out Vector3 localPosition, out Quaternion localRotation)
        {
            var fallTime = Mathf.Max(0f, (float)time - collapseAt);
            localPosition = BasePosition + Vector3.down * Mathf.Min(dropDistance, 0.5f * 25f * fallTime * fallTime);
            localRotation = BaseRotation;
        }

        protected override void OnTimeApplied(double time)
        {
            var solid = IsSolidAt(time, collapseAt);
            var warning = solid && time > collapseAt - warningSeconds && Mathf.Repeat((float)time * 6f, 1f) < 0.5f;
            var visible = !warning && time < collapseAt + 1.5f;

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
