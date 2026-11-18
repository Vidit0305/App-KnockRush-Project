using UnityEngine;

namespace KnockRush.Obstacles
{
    /// <summary>Spins continuously around a local axis (sweeper bars, spinners, rotating platforms, windmills).</summary>
    public sealed class RotatingObstacle : TimeDrivenObstacle
    {
        [SerializeField] private Vector3 localAxis = Vector3.up;
        [SerializeField] private float degreesPerSecond = 90f;

        public void Configure(Vector3 axis, float speed)
        {
            localAxis = axis;
            degreesPerSecond = speed;
        }

        protected override void Evaluate(double time, out Vector3 localPosition, out Quaternion localRotation)
        {
            var angle = (float)((time * degreesPerSecond) % 360d);
            localPosition = BasePosition;
            localRotation = BaseRotation * Quaternion.AngleAxis(angle, localAxis.normalized);
        }
    }
}
