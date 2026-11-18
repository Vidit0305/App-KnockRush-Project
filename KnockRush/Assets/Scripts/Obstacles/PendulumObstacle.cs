using UnityEngine;

namespace KnockRush.Obstacles
{
    /// <summary>Swings back and forth like a pendulum (swinging hammers, wrecking balls).</summary>
    public sealed class PendulumObstacle : TimeDrivenObstacle
    {
        [SerializeField] private Vector3 localAxis = Vector3.forward;
        [SerializeField, Range(0f, 180f)] private float amplitude = 65f;
        [SerializeField, Min(0.1f)] private float period = 2.6f;

        public void Configure(Vector3 axis, float amplitudeDegrees, float periodSeconds)
        {
            localAxis = axis;
            amplitude = amplitudeDegrees;
            period = periodSeconds;
        }

        public static float AngleAt(double time, float amplitudeDegrees, float periodSeconds)
        {
            return amplitudeDegrees * Mathf.Sin((float)(2d * Mathf.PI * (time / periodSeconds % 1d)));
        }

        protected override void Evaluate(double time, out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = BasePosition;
            localRotation = BaseRotation * Quaternion.AngleAxis(AngleAt(time, amplitude, period), localAxis.normalized);
        }
    }
}
