using System;
using KnockRush.CameraRig;
using KnockRush.Core.Events;
using KnockRush.Movement;
using KnockRush.Player.Controls;
using UnityEngine;

namespace KnockRush.Player
{
    /// <summary>
    /// Offline driver for one character: samples input, steps the <see cref="CharacterMotor"/>
    /// at the fixed timestep, applies the result to the transform, interpolates the visual
    /// model between ticks, and handles falling out of the world.
    ///
    /// Phase 4 adds a networked driver that runs the same motor inside Fusion's
    /// FixedUpdateNetwork; this component stays for offline modes and tests.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        [SerializeField] private MovementSettings settings;

        [Tooltip("Child transform holding the visual model; interpolated between simulation ticks.")]
        [SerializeField] private Transform visualRoot;

        [Tooltip("Input source for the locally controlled player. Leave empty for bots / remote players.")]
        [SerializeField] private PlayerInputReader inputReader;

        [Tooltip("Camera that follows this player (found automatically for the local player if empty).")]
        [SerializeField] private ThirdPersonCamera cameraRig;

        [Tooltip("Falling below this height respawns the character.")]
        [SerializeField] private float killHeight = -25f;

        private CapsuleCollider shape;
        private MotorState state;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private Vector3 spawnPosition;
        private float spawnYaw;
        private Func<MotorInput> externalInput;

        /// <summary>Raised after every simulation tick that produced events.</summary>
        public event Action<MotorEvents> MotorEventsRaised;

        public MotorState State => state;

        public MovementSettings Settings => settings;

        public bool IsLocallyControlled => inputReader != null;

        public float KillHeight
        {
            get => killHeight;
            set => killHeight = value;
        }

        /// <summary>Supplies input from code (bots, tests) instead of the local input reader.</summary>
        public void SetInputSource(Func<MotorInput> source)
        {
            externalInput = source;
        }

        public void SetSpawnPoint(Vector3 position, float yaw)
        {
            spawnPosition = position;
            spawnYaw = yaw;
        }

        public void Respawn()
        {
            CharacterMotor.Respawn(ref state, spawnPosition, spawnYaw);
            ApplyTransform(snapVisual: true);
            if (cameraRig != null && inputReader != null)
            {
                cameraRig.SnapToTarget();
            }

            DispatchEvents();
        }

        public void ApplyImpulse(Vector3 impulse)
        {
            CharacterMotor.ApplyImpulse(ref state, impulse, settings);
        }

        public void SetDisabled(bool disabled)
        {
            CharacterMotor.SetDisabled(ref state, disabled);
        }

        private void Awake()
        {
            shape = GetComponent<CapsuleCollider>();
            ConfigureShape();

            spawnPosition = transform.position;
            spawnYaw = transform.eulerAngles.y;
            state = MotorState.Spawn(spawnPosition, spawnYaw);
            previousPosition = spawnPosition;
            previousRotation = transform.rotation;
        }

        private void Start()
        {
            if (inputReader != null)
            {
                if (cameraRig == null)
                {
                    cameraRig = FindFirstObjectByType<ThirdPersonCamera>();
                }

                if (cameraRig != null)
                {
                    inputReader.CameraRig = cameraRig;
                    if (cameraRig.Target != transform)
                    {
                        cameraRig.Target = transform;
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (settings == null)
            {
                return;
            }

            previousPosition = state.Position;
            previousRotation = Quaternion.Euler(0f, state.Yaw, 0f);

            var input = externalInput != null
                ? externalInput()
                : inputReader != null ? inputReader.ConsumeInput() : MotorInput.None;

            CharacterMotor.Step(ref state, input, settings, Time.fixedDeltaTime, gameObject.scene.GetPhysicsScene(), shape);

            if (state.Position.y < killHeight)
            {
                Respawn();
                return;
            }

            ApplyTransform(snapVisual: false);
            DispatchEvents();
        }

        private void Update()
        {
            if (visualRoot == null)
            {
                return;
            }

            var alpha = Time.fixedDeltaTime > 0f
                ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime)
                : 1f;
            var current = Quaternion.Euler(0f, state.Yaw, 0f);
            visualRoot.SetPositionAndRotation(
                Vector3.Lerp(previousPosition, state.Position, alpha),
                Quaternion.Slerp(previousRotation, current, alpha));
        }

        private void ApplyTransform(bool snapVisual)
        {
            var rotation = Quaternion.Euler(0f, state.Yaw, 0f);
            transform.SetPositionAndRotation(state.Position, rotation);

            if (snapVisual)
            {
                previousPosition = state.Position;
                previousRotation = rotation;
                if (visualRoot != null)
                {
                    visualRoot.SetPositionAndRotation(state.Position, rotation);
                }
            }
        }

        private void DispatchEvents()
        {
            var events = CharacterMotor.ConsumeEvents(ref state);
            if (events == MotorEvents.None)
            {
                return;
            }

            MotorEventsRaised?.Invoke(events);
            EventBus.Publish(new CharacterMotorEvent(this, events, state.Position));
        }

        private void ConfigureShape()
        {
            if (settings == null || shape == null)
            {
                return;
            }

            shape.direction = 1;
            shape.radius = settings.Radius;
            shape.height = settings.Height;
            shape.center = new Vector3(0f, settings.Height * 0.5f, 0f);
        }

        private void OnValidate()
        {
            if (shape == null)
            {
                shape = GetComponent<CapsuleCollider>();
            }

            ConfigureShape();
        }
    }
}
