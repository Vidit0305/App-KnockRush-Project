using System;
using Fusion;
using KnockRush.CameraRig;
using KnockRush.Core.Events;
using KnockRush.Interaction;
using KnockRush.Movement;
using KnockRush.Player;
using KnockRush.Player.Controls;
using KnockRush.Player.Presentation;
using UnityEngine;

namespace KnockRush.Multiplayer
{
    /// <summary>
    /// Networked KnockRush character. Runs the same <see cref="CharacterMotor"/> as offline play,
    /// inside Fusion's FixedUpdateNetwork:
    ///  - the dedicated server (state authority) simulates every character from inputs,
    ///  - the owning client (input authority) predicts its own character and is corrected by the server,
    ///  - other clients render interpolated snapshots (NetworkTransform) and animate from replicated state.
    /// Course rules (checkpoints, finish, knockback) only change state on the server.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class NetworkCharacter : NetworkBehaviour, ICourseParticipant, ICharacterView
    {
        [SerializeField] private MovementSettings settings;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private float killHeight = -25f;

        private CapsuleCollider shape;
        private MotorState presentationState;
        private bool finishedRaised;

        [Networked] public NetworkMotorState Motor { get; set; }
        [Networked] public NetworkButtons PreviousButtons { get; set; }
        [Networked] public Vector3 RespawnPosition { get; set; }
        [Networked] public float RespawnYaw { get; set; }
        [Networked] public int NetworkCheckpointOrder { get; set; }
        [Networked] public NetworkBool NetworkFinished { get; set; }
        [Networked] public NetworkBool NetworkInputLocked { get; set; }
        [Networked] public byte ColorIndex { get; set; }

        /// <summary>Server-only flag marking bot-controlled characters (excluded from rewards and stats).</summary>
        [Networked] public NetworkBool IsBot { get; set; }

        public event Action<MotorEvents> MotorEventsRaised;
        public event Action Finished;

        // ---- ICharacterView ---------------------------------------------------------------
        public MotorState State => presentationState;
        public MovementSettings Settings => settings;
        public bool IsLocallyControlled => Object != null && Object.HasInputAuthority;
        public bool HasFinished => Object != null && NetworkFinished;

        // ---- ICourseParticipant -----------------------------------------------------------
        public Vector3 Position => transform.position;
        public int ParticipantId => Object != null ? Object.InputAuthority.PlayerId : GetInstanceID();

        public int CheckpointOrder
        {
            get => NetworkCheckpointOrder;
            set
            {
                if (HasStateAuthority)
                {
                    NetworkCheckpointOrder = value;
                }
            }
        }

        public bool InputLocked
        {
            get => NetworkInputLocked;
            set
            {
                if (HasStateAuthority)
                {
                    NetworkInputLocked = value;
                }
            }
        }

        public void SetRespawnPoint(Vector3 position, float yaw)
        {
            if (HasStateAuthority)
            {
                RespawnPosition = position;
                RespawnYaw = yaw;
            }
        }

        public void Respawn()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            var state = MotorState.Spawn(RespawnPosition, RespawnYaw);
            transform.SetPositionAndRotation(state.Position, Quaternion.Euler(0f, state.Yaw, 0f));
            Motor = NetworkMotorState.From(state);
        }

        public void MarkFinished()
        {
            if (!HasStateAuthority || NetworkFinished)
            {
                return;
            }

            NetworkFinished = true;
            NetworkInputLocked = true;
        }

        /// <summary>Knockback is authoritative: only the server changes the replicated motor state.</summary>
        public void ApplyImpulse(Vector3 impulse)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            var state = CurrentState();
            CharacterMotor.ApplyImpulse(ref state, impulse, settings);
            Motor = NetworkMotorState.From(state);
        }

        // ---- Fusion lifecycle -------------------------------------------------------------
        public override void Spawned()
        {
            shape = GetComponent<CapsuleCollider>();
            shape.radius = settings.Radius;
            shape.height = settings.Height;
            shape.center = new Vector3(0f, settings.Height * 0.5f, 0f);

            if (HasStateAuthority)
            {
                RespawnPosition = transform.position;
                RespawnYaw = transform.eulerAngles.y;
                NetworkCheckpointOrder = -1;
                Motor = NetworkMotorState.From(MotorState.Spawn(transform.position, transform.eulerAngles.y));
            }

            presentationState = CurrentState();

            var appearance = GetComponentInChildren<BeanAppearance>();
            if (appearance != null)
            {
                appearance.SetPaletteIndex(ColorIndex);
            }

            if (HasInputAuthority)
            {
                BindLocalControls();
            }
            else if (inputReader != null)
            {
                inputReader.enabled = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            var state = CurrentState();
            var input = MotorInput.None;
            if (GetInput(out NetworkInputData data))
            {
                input = data.ToMotorInput(PreviousButtons);
                PreviousButtons = data.Buttons;
            }

            if (NetworkInputLocked)
            {
                input = MotorInput.None;
            }

            CharacterMotor.Step(ref state, input, settings, Runner.DeltaTime, Runner.GetPhysicsScene(), shape);

            if (state.Position.y < killHeight)
            {
                state = MotorState.Spawn(RespawnPosition, RespawnYaw);
            }

            var events = CharacterMotor.ConsumeEvents(ref state);
            transform.SetPositionAndRotation(state.Position, Quaternion.Euler(0f, state.Yaw, 0f));
            Motor = NetworkMotorState.From(state);

            // Presentation events fire once per tick, never again during resimulation.
            if (Runner.IsForward && events != MotorEvents.None)
            {
                MotorEventsRaised?.Invoke(events);
                EventBus.Publish(new NetworkCharacterEvent(this, events));
            }
        }

        public override void Render()
        {
            presentationState = CurrentState();
            if (NetworkFinished && !finishedRaised)
            {
                finishedRaised = true;
                Finished?.Invoke();
            }
        }

        private MotorState CurrentState()
        {
            return Motor.ToMotorState(transform.position, transform.eulerAngles.y);
        }

        private void BindLocalControls()
        {
            if (inputReader == null)
            {
                return;
            }

            inputReader.enabled = true;
            var cameraRig = FindFirstObjectByType<ThirdPersonCamera>();
            if (cameraRig != null)
            {
                inputReader.CameraRig = cameraRig;
                cameraRig.Target = transform;
            }
        }
    }

    /// <summary>Local presentation event for networked characters (audio / VFX hooks).</summary>
    public readonly struct NetworkCharacterEvent
    {
        public readonly NetworkCharacter Character;
        public readonly MotorEvents Events;

        public NetworkCharacterEvent(NetworkCharacter character, MotorEvents events)
        {
            Character = character;
            Events = events;
        }
    }
}
