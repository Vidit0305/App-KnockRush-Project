using UnityEngine;

namespace KnockRush.Movement
{
    /// <summary>
    /// Every tunable value of the character motor. One shared asset is used by all players
    /// (and by the dedicated server), so movement is identical everywhere.
    /// </summary>
    [CreateAssetMenu(menuName = "KnockRush/Movement/Movement Settings", fileName = "MovementSettings", order = 0)]
    public sealed class MovementSettings : ScriptableObject
    {
        [Header("Capsule")]
        [Tooltip("Capsule radius in metres.")]
        [SerializeField, Min(0.1f)] private float radius = 0.42f;

        [Tooltip("Total capsule height in metres (feet to top of head).")]
        [SerializeField, Min(0.3f)] private float height = 1.5f;

        [Tooltip("Gap kept between the capsule and geometry to avoid getting stuck.")]
        [SerializeField, Range(0.001f, 0.1f)] private float skinWidth = 0.02f;

        [Tooltip("Layers the character collides with. Must NOT include the Player layer.")]
        [SerializeField] private LayerMask collisionMask = 1;

        [Tooltip("Steepest slope (degrees) that counts as ground.")]
        [SerializeField, Range(0f, 89f)] private float maxSlopeAngle = 50f;

        [Header("Ground Movement")]
        [SerializeField, Min(0f)] private float maxSpeed = 6.5f;
        [SerializeField, Min(0f)] private float groundAcceleration = 45f;
        [SerializeField, Min(0f)] private float groundDeceleration = 38f;

        [Tooltip("Degrees per second the character turns towards its move direction.")]
        [SerializeField, Min(0f)] private float turnSpeed = 900f;

        [Header("Air Movement")]
        [SerializeField, Min(0f)] private float airAcceleration = 20f;
        [SerializeField, Min(0f)] private float airDeceleration = 4f;

        [Header("Jump & Gravity")]
        [Tooltip("Apex height of a full jump in metres.")]
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.6f;
        [SerializeField, Min(0.1f)] private float gravity = 28f;

        [Tooltip("Gravity multiplier while falling (snappier landings).")]
        [SerializeField, Min(1f)] private float fallGravityMultiplier = 1.5f;

        [Tooltip("Gravity multiplier while rising after the jump button is released (variable jump height).")]
        [SerializeField, Min(1f)] private float jumpCutGravityMultiplier = 2.2f;

        [SerializeField, Min(1f)] private float maxFallSpeed = 30f;

        [Tooltip("Seconds after leaving a ledge during which a jump is still allowed.")]
        [SerializeField, Range(0f, 0.5f)] private float coyoteTime = 0.12f;

        [Tooltip("Seconds a jump press is remembered before landing.")]
        [SerializeField, Range(0f, 0.5f)] private float jumpBufferTime = 0.12f;

        [Header("Dive & Slide")]
        [SerializeField] private bool allowGroundDive = true;
        [SerializeField, Min(0f)] private float diveHorizontalSpeed = 9.5f;
        [SerializeField, Min(0f)] private float diveUpVelocity = 4.5f;
        [SerializeField, Min(0f)] private float slideDeceleration = 7f;
        [SerializeField, Min(0f)] private float slideMinDuration = 0.25f;
        [SerializeField, Min(0f)] private float slideMaxDuration = 1.0f;
        [SerializeField, Min(0f)] private float slideStopSpeed = 1.5f;
        [SerializeField, Min(0f)] private float recoverDuration = 0.35f;

        [Header("Stumble / Knockback")]
        [Tooltip("Impulses at least this strong (m/s) knock the character into a stumble.")]
        [SerializeField, Min(0f)] private float stumbleImpulseThreshold = 5f;
        [SerializeField, Min(0f)] private float stumbleDuration = 0.8f;

        [Tooltip("Fraction of normal acceleration available while stumbling.")]
        [SerializeField, Range(0f, 1f)] private float stumbleControl = 0.15f;

        [Header("Grab")]
        [SerializeField, Range(0f, 1f)] private float grabSpeedMultiplier = 0.45f;

        [Header("Ground Probe")]
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.08f;
        [Tooltip("Extra distance the character snaps down to stay glued to slopes and step-downs.")]
        [SerializeField, Min(0f)] private float groundSnapDistance = 0.35f;

        public float Radius => radius;
        public float Height => height;
        public float SkinWidth => skinWidth;
        public LayerMask CollisionMask => collisionMask;
        public float MaxSlopeAngle => maxSlopeAngle;
        public float MaxSpeed => maxSpeed;
        public float GroundAcceleration => groundAcceleration;
        public float GroundDeceleration => groundDeceleration;
        public float TurnSpeed => turnSpeed;
        public float AirAcceleration => airAcceleration;
        public float AirDeceleration => airDeceleration;
        public float JumpHeight => jumpHeight;
        public float Gravity => gravity;
        public float FallGravityMultiplier => fallGravityMultiplier;
        public float JumpCutGravityMultiplier => jumpCutGravityMultiplier;
        public float MaxFallSpeed => maxFallSpeed;
        public float CoyoteTime => coyoteTime;
        public float JumpBufferTime => jumpBufferTime;
        public bool AllowGroundDive => allowGroundDive;
        public float DiveHorizontalSpeed => diveHorizontalSpeed;
        public float DiveUpVelocity => diveUpVelocity;
        public float SlideDeceleration => slideDeceleration;
        public float SlideMinDuration => slideMinDuration;
        public float SlideMaxDuration => slideMaxDuration;
        public float SlideStopSpeed => slideStopSpeed;
        public float RecoverDuration => recoverDuration;
        public float StumbleImpulseThreshold => stumbleImpulseThreshold;
        public float StumbleDuration => stumbleDuration;
        public float StumbleControl => stumbleControl;
        public float GrabSpeedMultiplier => grabSpeedMultiplier;
        public float GroundProbeDistance => groundProbeDistance;
        public float GroundSnapDistance => groundSnapDistance;

        /// <summary>Initial upward velocity that reaches <see cref="JumpHeight"/> under <see cref="Gravity"/>.</summary>
        public float JumpVelocity => Mathf.Sqrt(2f * gravity * jumpHeight);

        public float MinGroundNormalY => Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);

        /// <summary>Used by tools and tests to configure the collision mask without an asset.</summary>
        public void SetCollisionMask(LayerMask mask)
        {
            collisionMask = mask;
        }

        private void OnValidate()
        {
            height = Mathf.Max(height, radius * 2f + 0.01f);
            slideMaxDuration = Mathf.Max(slideMaxDuration, slideMinDuration);
        }
    }
}
