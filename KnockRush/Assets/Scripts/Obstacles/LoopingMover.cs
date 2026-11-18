using UnityEngine;

namespace KnockRush.Obstacles
{
    /// <summary>
    /// Travels from its start position along an offset at constant speed, then instantly restarts
    /// (rolling balls down a slope, conveyor crates, falling logs). Optional spin for rolling.
    /// Deterministic: position = f(time).
    /// </summary>
    public sealed class LoopingMover : TimeDrivenObstacle
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, -10f, -40f);
        [SerializeField, Min(0.2f)] private float travelSeconds = 6f;

        [Tooltip("Spin around this local axis while travelling (degrees per metre), for rolling balls.")]
        [SerializeField] private Vector3 spinAxis = Vector3.right;
        [SerializeField] private float spinDegreesPerMetre = 0f;

        public void Configure(Vector3 travel, float seconds, float spinPerMetre)
        {
            offset = travel;
            travelSeconds = seconds;
            spinDegreesPerMetre = spinPerMetre;
        }

        protected override void Evaluate(double time, out Vector3 localPosition, out Quaternion localRotation)
        {
            var progress = (float)(time / travelSeconds % 1d);
            if (progress < 0f)
            {
                progress += 1f;
            }

            localPosition = BasePosition + offset * progress;
            var distance = offset.magnitude * progress;
            localRotation = BaseRotation * Quaternion.AngleAxis(distance * spinDegreesPerMetre, spinAxis.normalized);
        }
    }
}
