using KnockRush.Interaction;
using KnockRush.Movement;
using UnityEngine;

namespace KnockRush.Obstacles
{
    /// <summary>
    /// Base class for "category A" obstacles whose pose is a pure function of time
    /// (see ARCHITECTURE §4.2). Because every machine evaluates the same function of the same
    /// clock, these obstacles cost zero network bandwidth and are predicted perfectly.
    ///
    /// The component also acts as a movement surface (characters standing on it are carried)
    /// and as a velocity provider (ImpulseZones knock players along the obstacle's motion).
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class TimeDrivenObstacle : MonoBehaviour, IMovementSurface, IPointVelocityProvider
    {
        private const double VelocitySampleStep = 0.02d;

        [Tooltip("Seconds added to the shared clock for this obstacle (staggers identical obstacles).")]
        [SerializeField] private float phaseOffset;

        [Tooltip("Characters standing on this obstacle move with it.")]
        [SerializeField] private bool carriesCharacters = true;

        [Tooltip("Ground friction multiplier for characters standing on it.")]
        [SerializeField, Range(0.05f, 3f)] private float friction = 1f;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private bool initialised;
        private double currentTime;

        public float PhaseOffset
        {
            get => phaseOffset;
            set => phaseOffset = value;
        }

        public double CurrentTime => currentTime;

        protected Vector3 BasePosition => basePosition;

        protected Quaternion BaseRotation => baseRotation;

        public float FrictionMultiplier => friction;

        /// <summary>Local pose (relative to the parent) at <paramref name="time"/> seconds.</summary>
        protected abstract void Evaluate(double time, out Vector3 localPosition, out Quaternion localRotation);

        /// <summary>Hook for non-pose behaviour driven by time (e.g. visibility cycles).</summary>
        protected virtual void OnTimeApplied(double time)
        {
        }

        public void ApplyTime(double sharedTime)
        {
            EnsureInitialised();
            currentTime = sharedTime + phaseOffset;
            Evaluate(currentTime, out var position, out var rotation);
            transform.SetLocalPositionAndRotation(position, rotation);
            OnTimeApplied(currentTime);
        }

        public Vector3 GetPointVelocity(Vector3 worldPoint)
        {
            EnsureInitialised();
            var parent = transform.parent;
            var parentMatrix = parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;
            var scale = transform.localScale;

            Evaluate(currentTime, out var p0, out var r0);
            Evaluate(currentTime + VelocitySampleStep, out var p1, out var r1);

            var m0 = parentMatrix * Matrix4x4.TRS(p0, r0, scale);
            var m1 = parentMatrix * Matrix4x4.TRS(p1, r1, scale);
            var local = m0.inverse.MultiplyPoint3x4(worldPoint);
            return (m1.MultiplyPoint3x4(local) - worldPoint) / (float)VelocitySampleStep;
        }

        public Vector3 GetSurfaceVelocity(Vector3 worldPoint)
        {
            return carriesCharacters ? GetPointVelocity(worldPoint) : Vector3.zero;
        }

        protected virtual void Awake()
        {
            EnsureInitialised();
        }

        protected virtual void OnEnable()
        {
            ObstacleSimulation.Register(this);
        }

        protected virtual void OnDisable()
        {
            ObstacleSimulation.Unregister(this);
        }

        private void EnsureInitialised()
        {
            if (initialised)
            {
                return;
            }

            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            initialised = true;
        }
    }
}
