using System;
using Fusion;
using KnockRush.CameraRig;
using KnockRush.Core.Events;
using KnockRush.Interaction;
using KnockRush.Inventory;
using KnockRush.Maps;
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
        private int appliedLoadoutHash;
        private CosmeticApplier cosmetics;

        [Networked] public NetworkMotorState Motor { get; set; }
        [Networked] public NetworkButtons PreviousButtons { get; set; }
        [Networked] public Vector3 RespawnPosition { get; set; }
        [Networked] public float RespawnYaw { get; set; }
        [Networked] public int NetworkCheckpointOrder { get; set; }
        [Networked] public NetworkBool NetworkFinished { get; set; }
        [Networked] public NetworkBool NetworkInputLocked { get; set; }
        [Networked] public byte ColorIndex { get; set; }

        /// <summary>Equipped cosmetics as catalogue index + 1 (0 = not set yet).</summary>
        [Networked] public short LoadoutBody { get; set; }
        [Networked] public short LoadoutColor { get; set; }
        [Networked] public short LoadoutPattern { get; set; }
        [Networked] public short LoadoutHat { get; set; }
        [Networked] public short LoadoutEmote { get; set; }

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

            cosmetics = GetComponentInChildren<CosmeticApplier>();
            if (HasInputAuthority)
            {
                BindLocalControls();
                var catalog = CosmeticCatalog.Load();
                if (catalog != null)
                {
                    var indices = LoadoutIndices.From(LocalLoadoutStore.Current, catalog);
                    RPC_SetLoadout(indices.Body, indices.Color, indices.Pattern, indices.Hat, indices.Emote);
                }
            }
            else if (HasStateAuthority && IsBot)
            {
                AssignRandomBotLoadout();
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
            if (IsBot)
            {
                if (HasStateAuthority)
                {
                    input = BotBrain.Compute(this, Runner.Tick.Raw, Runner.DeltaTime);
                }
            }
            else if (GetInput(out NetworkInputData data))
            {
                input = data.ToMotorInput(PreviousButtons);
                PreviousButtons = data.Buttons;
            }

            if (NetworkInputLocked)
            {
                input = MotorInput.None;
            }

            CharacterMotor.Step(ref state, input, settings, Runner.DeltaTime, Runner.GetPhysicsScene(), shape);

            var fellOut = state.Position.y < killHeight;
            if (fellOut && !HasStateAuthority)
            {
                // Predicting client: hold still until the server decides (respawn or elimination).
                state.Position.y = killHeight;
                state.Velocity = Vector3.zero;
            }

            var events = CharacterMotor.ConsumeEvents(ref state);
            transform.SetPositionAndRotation(state.Position, Quaternion.Euler(0f, state.Yaw, 0f));
            Motor = NetworkMotorState.From(state);

            if (fellOut && HasStateAuthority)
            {
                if (MapController.Current != null)
                {
                    MapController.Current.HandleFall(this); // respawn (race) or eliminate (survival)
                }
                else
                {
                    Respawn();
                }

                return;
            }

            // Presentation events fire once per tick, never again during resimulation.
            if (Runner.IsForward && events != MotorEvents.None)
            {
                MotorEventsRaised?.Invoke(events);
                EventBus.Publish(new NetworkCharacterEvent(this, events));
            }
        }

        /// <summary>Owner → server: equip cosmetics (visual only; validated against the catalogue).</summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetLoadout(short body, short color, short pattern, short hat, short emote)
        {
            var catalog = CosmeticCatalog.Load();
            short Clamp(short index) => (short)(catalog != null && catalog.Get(index) != null ? index + 1 : 0);
            LoadoutBody = Clamp(body);
            LoadoutColor = Clamp(color);
            LoadoutPattern = Clamp(pattern);
            LoadoutHat = Clamp(hat);
            LoadoutEmote = Clamp(emote);
        }

        private void AssignRandomBotLoadout()
        {
            var catalog = CosmeticCatalog.Load();
            if (catalog == null)
            {
                return;
            }

            var random = new System.Random(Object.Id.Raw.GetHashCode());
            short Pick(CosmeticSlot slot)
            {
                var options = new System.Collections.Generic.List<CosmeticDefinition>(catalog.InSlot(slot));
                return options.Count == 0 ? (short)0 : (short)(catalog.IndexOf(options[random.Next(options.Count)].ItemId) + 1);
            }

            LoadoutBody = random.NextDouble() < 0.6 ? Pick(CosmeticSlot.Body) : (short)0;
            LoadoutColor = Pick(CosmeticSlot.Color);
            LoadoutPattern = random.NextDouble() < 0.3 ? Pick(CosmeticSlot.Pattern) : (short)0;
            LoadoutHat = random.NextDouble() < 0.4 ? Pick(CosmeticSlot.Hat) : (short)0;
            LoadoutEmote = Pick(CosmeticSlot.Emote);
        }

        private void ApplyReplicatedLoadout()
        {
            var hash = LoadoutBody | (LoadoutColor << 7) | (LoadoutPattern << 14) | (LoadoutHat << 19) | (LoadoutEmote << 25);
            if (cosmetics == null || hash == 0 || hash == appliedLoadoutHash)
            {
                return;
            }

            appliedLoadoutHash = hash;
            var catalog = CosmeticCatalog.Load();
            var indices = new LoadoutIndices
            {
                Body = (short)(LoadoutBody - 1),
                Color = (short)(LoadoutColor - 1),
                Pattern = (short)(LoadoutPattern - 1),
                Hat = (short)(LoadoutHat - 1),
                Emote = (short)(LoadoutEmote - 1)
            };
            cosmetics.Apply(indices.ToLoadout(catalog));
        }

        public override void Render()
        {
            ApplyReplicatedLoadout();
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
