using KnockRush.Core.Events;
using KnockRush.Core.Services;
using KnockRush.Core.Settings;
using UnityEngine;

namespace KnockRush.CameraRig
{
    /// <summary>
    /// Smooth orbiting third-person camera with wall collision. Look input arrives as raw
    /// mouse pixels, touch pixels or stick values and is scaled by <see cref="CameraSettings"/>
    /// and the player's sensitivity / invert settings.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        private const float ReferenceScreenHeight = 1080f;

        [SerializeField] private CameraSettings settings;
        [SerializeField] private Transform target;

        private Camera cachedCamera;
        private float yaw;
        private float pitch;
        private float currentDistance;
        private Vector3 pivot;
        private Vector3 pivotVelocity;
        private float sensitivity = 1f;
        private bool invertY;
        private bool hasSnapped;

        /// <summary>The enabled gameplay camera on this device (spectating retargets it).</summary>
        public static ThirdPersonCamera Active { get; private set; }

        /// <summary>Horizontal look direction in degrees; used to make movement camera-relative.</summary>
        public float Yaw => yaw;

        public float Pitch => pitch;

        public CameraSettings Settings => settings;

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                SnapToTarget();
            }
        }

        public void Configure(CameraSettings cameraSettings, Transform followTarget)
        {
            settings = cameraSettings;
            Target = followTarget;
        }

        public void AddMouseDelta(Vector2 pixels)
        {
            if (settings != null)
            {
                AddLook(pixels * settings.MouseDegreesPerPixel);
            }
        }

        public void AddTouchDelta(Vector2 pixels)
        {
            if (settings != null)
            {
                var scale = ReferenceScreenHeight / Mathf.Max(1f, Screen.height);
                AddLook(pixels * (settings.TouchDegreesPerReferencePixel * scale));
            }
        }

        public void AddStick(Vector2 stick, float deltaTime)
        {
            if (settings != null && stick.sqrMagnitude > 0.0001f)
            {
                AddLook(Vector2.Scale(stick, settings.StickDegreesPerSecond) * deltaTime);
            }
        }

        /// <summary>Places the camera behind the target immediately (spawn, respawn, scene start).</summary>
        public void SnapToTarget()
        {
            if (target == null || settings == null)
            {
                return;
            }

            yaw = target.eulerAngles.y;
            pitch = settings.DefaultPitch;
            pivot = target.position + Vector3.up * settings.PivotHeight;
            pivotVelocity = Vector3.zero;
            currentDistance = settings.Distance;
            hasSnapped = true;
            ApplyTransform();
        }

        private void Awake()
        {
            cachedCamera = GetComponent<Camera>();
            if (settings != null)
            {
                pitch = settings.DefaultPitch;
                currentDistance = settings.Distance;
                cachedCamera.fieldOfView = settings.FieldOfView;
            }
        }

        private void OnEnable()
        {
            Active = this;
            if (ServiceLocator.TryGet<ILocalSettingsService>(out var localSettings))
            {
                ApplyLocalSettings(localSettings.Current);
            }

            EventBus.Subscribe<LocalSettingsChangedEvent>(OnLocalSettingsChanged);
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }

            EventBus.Unsubscribe<LocalSettingsChangedEvent>(OnLocalSettingsChanged);
        }

        private void Start()
        {
            if (!hasSnapped)
            {
                SnapToTarget();
            }
        }

        private void LateUpdate()
        {
            if (target == null || settings == null)
            {
                return;
            }

            var desiredPivot = target.position + Vector3.up * settings.PivotHeight;
            pivot = Vector3.SmoothDamp(pivot, desiredPivot, ref pivotVelocity, settings.FollowSmoothTime);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var back = rotation * Vector3.back;

            var wanted = settings.Distance;
            if (Physics.SphereCast(pivot, settings.CollisionRadius, back, out var hit, settings.Distance,
                    settings.CollisionMask, QueryTriggerInteraction.Ignore))
            {
                wanted = Mathf.Max(settings.MinDistance, hit.distance);
            }

            // Snap in instantly (never clip through walls), ease back out smoothly.
            currentDistance = wanted < currentDistance
                ? wanted
                : Mathf.MoveTowards(currentDistance, wanted, settings.DistanceRecoverSpeed * Time.deltaTime);

            transform.SetPositionAndRotation(pivot + back * currentDistance, rotation);
        }

        private void AddLook(Vector2 degrees)
        {
            degrees *= sensitivity;
            yaw = Mathf.Repeat(yaw + degrees.x, 360f);
            pitch = Mathf.Clamp(pitch + (invertY ? degrees.y : -degrees.y), settings.MinPitch, settings.MaxPitch);
        }

        private void OnLocalSettingsChanged(LocalSettingsChangedEvent changed)
        {
            ApplyLocalSettings(changed.Settings);
        }

        private void ApplyLocalSettings(IReadOnlyLocalSettings local)
        {
            sensitivity = local.CameraSensitivity;
            invertY = local.InvertCameraY;
        }
    }
}
