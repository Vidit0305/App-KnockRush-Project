using KnockRush.Core;
using UnityEngine;

namespace KnockRush.CameraRig
{
    [CreateAssetMenu(menuName = "KnockRush/Camera/Camera Settings", fileName = "CameraSettings", order = 0)]
    public sealed class CameraSettings : ScriptableObject
    {
        [Header("Framing")]
        [SerializeField, Min(1f)] private float distance = 6.5f;
        [SerializeField, Min(0.3f)] private float minDistance = 1.2f;
        [Tooltip("Height of the orbit pivot above the character's feet.")]
        [SerializeField] private float pivotHeight = 1.3f;
        [SerializeField, Range(20f, 100f)] private float fieldOfView = 60f;

        [Header("Orbit")]
        [SerializeField, Range(-80f, 80f)] private float defaultPitch = 16f;
        [SerializeField, Range(-80f, 0f)] private float minPitch = -25f;
        [SerializeField, Range(0f, 85f)] private float maxPitch = 65f;

        [Header("Smoothing")]
        [SerializeField, Range(0f, 0.5f)] private float followSmoothTime = 0.06f;
        [Tooltip("Metres per second the camera moves back out after a wall pushed it closer.")]
        [SerializeField, Min(0.1f)] private float distanceRecoverSpeed = 6f;

        [Header("Collision")]
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionMask = GameLayers.EnvironmentMask;

        [Header("Sensitivity (multiplied by the player's sensitivity setting)")]
        [SerializeField] private float mouseDegreesPerPixel = 0.12f;
        [Tooltip("Degrees per pixel of touch drag at a 1080 px tall screen (scaled for other resolutions).")]
        [SerializeField] private float touchDegreesPerReferencePixel = 0.2f;
        [SerializeField] private Vector2 stickDegreesPerSecond = new Vector2(220f, 140f);

        public float Distance => distance;
        public float MinDistance => minDistance;
        public float PivotHeight => pivotHeight;
        public float FieldOfView => fieldOfView;
        public float DefaultPitch => defaultPitch;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public float FollowSmoothTime => followSmoothTime;
        public float DistanceRecoverSpeed => distanceRecoverSpeed;
        public float CollisionRadius => collisionRadius;
        public LayerMask CollisionMask => collisionMask;
        public float MouseDegreesPerPixel => mouseDegreesPerPixel;
        public float TouchDegreesPerReferencePixel => touchDegreesPerReferencePixel;
        public Vector2 StickDegreesPerSecond => stickDegreesPerSecond;
    }
}
