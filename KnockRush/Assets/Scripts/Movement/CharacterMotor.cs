using UnityEngine;

namespace KnockRush.Movement
{
    /// <summary>
    /// The deterministic KnockRush character simulation. <see cref="Step"/> is a pure function of
    /// (state, input, settings, dt, physics scene); it holds no state of its own. That lets the
    /// same code run offline, inside Fusion's predicted FixedUpdateNetwork, and on the server.
    /// </summary>
    public static class CharacterMotor
    {
        private const float MinMoveSqr = 0.0001f;
        private const float LeaveGroundVelocity = 0.01f;

        /// <summary>Advances <paramref name="state"/> by <paramref name="deltaTime"/> seconds.</summary>
        /// <param name="shape">The character's own capsule collider (used for depenetration only).</param>
        public static void Step(
            ref MotorState state,
            in MotorInput input,
            MovementSettings settings,
            float deltaTime,
            PhysicsScene physics,
            Collider shape)
        {
            // NOTE: Events accumulate (impulses can be applied between steps); the driver
            // reads and clears them with ConsumeEvents after each step.
            if (state.Mode == MotorMode.Disabled || deltaTime <= 0f)
            {
                return;
            }

            UpdateTimers(ref state, input, settings, deltaTime);
            HandleModeTransitions(ref state, input, settings);
            ApplyHorizontalMovement(ref state, input, settings, deltaTime);
            ApplyVerticalMovement(ref state, input, settings, deltaTime);
            Move(ref state, settings, deltaTime, physics, shape);
            UpdateGrounding(ref state, settings, physics);
        }

        /// <summary>Adds an external velocity change (obstacle hit, bumper, explosion, player push).</summary>
        public static void ApplyImpulse(ref MotorState state, Vector3 impulse, MovementSettings settings)
        {
            if (state.Mode == MotorMode.Disabled)
            {
                return;
            }

            state.Velocity += impulse;
            state.Events |= MotorEvents.Impulse;

            if (impulse.y > 0.5f)
            {
                state.IsGrounded = false;
                state.IsJumpRising = false;
            }

            if (impulse.magnitude >= settings.StumbleImpulseThreshold)
            {
                SetMode(ref state, MotorMode.Stumbled);
                state.Events |= MotorEvents.Stumbled;
            }
        }

        /// <summary>Returns the events raised since the last call and clears them.</summary>
        public static MotorEvents ConsumeEvents(ref MotorState state)
        {
            var events = state.Events;
            state.Events = MotorEvents.None;
            return events;
        }

        /// <summary>Teleports the character and resets its dynamic state.</summary>
        public static void Respawn(ref MotorState state, Vector3 position, float yaw)
        {
            state = MotorState.Spawn(position, yaw);
        }

        public static void SetDisabled(ref MotorState state, bool disabled)
        {
            if (disabled)
            {
                state.Velocity = Vector3.zero;
                SetMode(ref state, MotorMode.Disabled);
            }
            else if (state.Mode == MotorMode.Disabled)
            {
                SetMode(ref state, MotorMode.Airborne);
            }
        }

        private static void UpdateTimers(ref MotorState state, in MotorInput input, MovementSettings settings, float dt)
        {
            state.ModeTime += dt;
            state.JumpBufferTimer = input.JumpPressed ? settings.JumpBufferTime : Mathf.Max(0f, state.JumpBufferTimer - dt);
            state.CoyoteTimer = state.IsGrounded ? settings.CoyoteTime : Mathf.Max(0f, state.CoyoteTimer - dt);
            if (state.GroundFriction <= 0f)
            {
                state.GroundFriction = 1f;
            }
        }

        private static void HandleModeTransitions(ref MotorState state, in MotorInput input, MovementSettings settings)
        {
            switch (state.Mode)
            {
                case MotorMode.Grounded:
                case MotorMode.Airborne:
                {
                    var canDive = input.DivePressed && (settings.AllowGroundDive || !state.IsGrounded);
                    if (canDive)
                    {
                        StartDive(ref state, input, settings);
                    }
                    else if (state.JumpBufferTimer > 0f && state.CoyoteTimer > 0f)
                    {
                        Jump(ref state, settings);
                    }

                    break;
                }

                case MotorMode.Sliding:
                {
                    var speed = state.HorizontalVelocity.magnitude;
                    var slowEnough = speed <= settings.SlideStopSpeed && state.ModeTime >= settings.SlideMinDuration;
                    if (slowEnough || state.ModeTime >= settings.SlideMaxDuration)
                    {
                        SetMode(ref state, MotorMode.Recovering);
                    }

                    break;
                }

                case MotorMode.Recovering:
                    if (state.ModeTime >= settings.RecoverDuration)
                    {
                        SetMode(ref state, state.IsGrounded ? MotorMode.Grounded : MotorMode.Airborne);
                        state.Events |= MotorEvents.Recovered;
                    }

                    break;

                case MotorMode.Stumbled:
                    if (state.ModeTime >= settings.StumbleDuration)
                    {
                        SetMode(ref state, state.IsGrounded ? MotorMode.Grounded : MotorMode.Airborne);
                        state.Events |= MotorEvents.Recovered;
                    }

                    break;
            }

            state.IsGrabbing = input.GrabHeld && (state.Mode == MotorMode.Grounded || state.Mode == MotorMode.Airborne);
        }

        private static void Jump(ref MotorState state, MovementSettings settings)
        {
            // Inherit the platform's vertical motion so jumping off a rising platform feels right.
            state.Velocity.y = settings.JumpVelocity + Mathf.Max(0f, state.GroundVelocity.y);
            InheritGroundVelocity(ref state);
            state.JumpBufferTimer = 0f;
            state.CoyoteTimer = 0f;
            state.IsGrounded = false;
            state.IsJumpRising = true;
            SetMode(ref state, MotorMode.Airborne);
            state.Events |= MotorEvents.Jumped;
        }

        private static void StartDive(ref MotorState state, in MotorInput input, MovementSettings settings)
        {
            var direction = input.Move.sqrMagnitude > MinMoveSqr
                ? new Vector3(input.Move.x, 0f, input.Move.y).normalized
                : state.Forward;

            var currentSpeed = state.HorizontalVelocity.magnitude;
            var diveSpeed = Mathf.Max(currentSpeed, settings.DiveHorizontalSpeed);

            if (state.IsGrounded)
            {
                InheritGroundVelocity(ref state);
            }

            state.Velocity = new Vector3(direction.x * diveSpeed, Mathf.Max(state.Velocity.y, settings.DiveUpVelocity), direction.z * diveSpeed);
            state.Yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            state.IsGrounded = false;
            state.IsJumpRising = false;
            state.JumpBufferTimer = 0f;
            state.CoyoteTimer = 0f;
            SetMode(ref state, MotorMode.Diving);
            state.Events |= MotorEvents.Dived;
        }

        private static void ApplyHorizontalMovement(ref MotorState state, in MotorInput input, MovementSettings settings, float dt)
        {
            var move = Vector2.ClampMagnitude(input.Move, 1f);
            var desired = new Vector3(move.x, 0f, move.y) * settings.MaxSpeed;
            if (state.IsGrabbing)
            {
                desired *= settings.GrabSpeedMultiplier;
            }

            var horizontal = state.HorizontalVelocity;
            var friction = state.IsGrounded ? state.GroundFriction : 1f;
            var hasInput = desired.sqrMagnitude > MinMoveSqr;
            float rate;
            var canSteer = true;

            switch (state.Mode)
            {
                case MotorMode.Grounded:
                    rate = (hasInput ? settings.GroundAcceleration : settings.GroundDeceleration) * friction;
                    break;
                case MotorMode.Airborne:
                    rate = hasInput ? settings.AirAcceleration : settings.AirDeceleration;
                    break;
                case MotorMode.Diving:
                    rate = 0f; // Committed: no steering mid-dive.
                    canSteer = false;
                    break;
                case MotorMode.Sliding:
                case MotorMode.Recovering:
                    desired = Vector3.zero;
                    rate = settings.SlideDeceleration * (state.IsGrounded ? friction : 0.2f);
                    canSteer = false;
                    break;
                case MotorMode.Stumbled:
                    rate = (state.IsGrounded ? settings.GroundDeceleration * friction : settings.AirDeceleration) * settings.StumbleControl;
                    desired *= settings.StumbleControl;
                    canSteer = false;
                    break;
                default:
                    rate = 0f;
                    canSteer = false;
                    break;
            }

            horizontal = Vector3.MoveTowards(horizontal, desired, rate * dt);
            state.Velocity.x = horizontal.x;
            state.Velocity.z = horizontal.z;

            // Facing.
            if (canSteer && hasInput)
            {
                var targetYaw = Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg;
                state.Yaw = Mathf.MoveTowardsAngle(state.Yaw, targetYaw, settings.TurnSpeed * dt);
            }
            else if (state.Mode == MotorMode.Diving && horizontal.sqrMagnitude > MinMoveSqr)
            {
                state.Yaw = Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg;
            }

            state.Yaw = Mathf.Repeat(state.Yaw, 360f);
        }

        private static void ApplyVerticalMovement(ref MotorState state, in MotorInput input, MovementSettings settings, float dt)
        {
            if (state.IsGrounded && state.Velocity.y <= 0f)
            {
                state.Velocity.y = 0f;
                state.IsJumpRising = false;
                return;
            }

            var gravity = settings.Gravity;
            if (state.Velocity.y < 0f)
            {
                gravity *= settings.FallGravityMultiplier;
                state.IsJumpRising = false;
            }
            else if (state.IsJumpRising && !input.JumpHeld)
            {
                gravity *= settings.JumpCutGravityMultiplier;
            }

            state.Velocity.y = Mathf.Max(state.Velocity.y - gravity * dt, -settings.MaxFallSpeed);
        }

        private static void Move(ref MotorState state, MovementSettings settings, float dt, PhysicsScene physics, Collider shape)
        {
            var position = state.Position;
            var velocity = state.Velocity;

            KinematicCapsule.Depenetrate(physics, settings, shape, ref position, ref velocity);

            // Ride moving surfaces first (their motion is not part of our velocity).
            if (state.IsGrounded && state.GroundVelocity.sqrMagnitude > MinMoveSqr)
            {
                var carry = state.GroundVelocity * dt;
                var carryVelocity = Vector3.zero;
                KinematicCapsule.MoveAndSlide(physics, settings, true, ref position, ref carryVelocity, carry, out _);
            }

            KinematicCapsule.MoveAndSlide(physics, settings, state.IsGrounded, ref position, ref velocity, velocity * dt, out var hitWall);
            if (hitWall)
            {
                state.Events |= MotorEvents.HitWall;
            }

            state.Position = position;
            state.Velocity = velocity;
        }

        private static void UpdateGrounding(ref MotorState state, MovementSettings settings, PhysicsScene physics)
        {
            var wasGrounded = state.IsGrounded;
            var preLandingVerticalSpeed = state.Velocity.y;

            // Moving upwards (jump, launch) never counts as grounded.
            if (state.Velocity.y > LeaveGroundVelocity)
            {
                SetAirborne(ref state, wasGrounded);
                return;
            }

            var probeDistance = settings.GroundProbeDistance + (wasGrounded ? settings.GroundSnapDistance : 0f);
            var ground = KinematicCapsule.ProbeGround(physics, settings, state.Position, probeDistance);

            if (!ground.HasGround || !ground.IsWalkable)
            {
                SetAirborne(ref state, wasGrounded);
                return;
            }

            // Snap down onto the ground.
            state.Position += Vector3.down * Mathf.Max(0f, ground.Distance - settings.SkinWidth * 0.5f);
            state.IsGrounded = true;
            state.GroundNormal = ground.Normal;
            state.Velocity.y = 0f;

            var surface = ground.Collider != null ? ground.Collider.GetComponentInParent<IMovementSurface>() : null;
            state.GroundFriction = surface != null ? Mathf.Max(0.01f, surface.FrictionMultiplier) : 1f;
            state.GroundVelocity = surface != null ? surface.GetSurfaceVelocity(ground.Point) : Vector3.zero;

            if (!wasGrounded)
            {
                state.LastLandingSpeed = Mathf.Max(0f, -preLandingVerticalSpeed);
                state.Events |= MotorEvents.Landed;

                switch (state.Mode)
                {
                    case MotorMode.Diving:
                        SetMode(ref state, MotorMode.Sliding);
                        state.Events |= MotorEvents.SlideStarted;
                        break;
                    case MotorMode.Airborne:
                        SetMode(ref state, MotorMode.Grounded);
                        break;
                }
            }
        }

        private static void SetAirborne(ref MotorState state, bool wasGrounded)
        {
            if (wasGrounded)
            {
                // Keep the platform's momentum when leaving it.
                InheritGroundVelocity(ref state);
                state.Events |= MotorEvents.LeftGround;
            }

            state.IsGrounded = false;
            state.GroundNormal = Vector3.up;
            state.GroundFriction = 1f;

            if (state.Mode == MotorMode.Grounded)
            {
                SetMode(ref state, MotorMode.Airborne);
            }
        }

        private static void InheritGroundVelocity(ref MotorState state)
        {
            if (state.GroundVelocity.sqrMagnitude > MinMoveSqr)
            {
                state.Velocity.x += state.GroundVelocity.x;
                state.Velocity.z += state.GroundVelocity.z;
                state.GroundVelocity = Vector3.zero;
            }
        }

        private static void SetMode(ref MotorState state, MotorMode mode)
        {
            if (state.Mode == mode)
            {
                return;
            }

            state.Mode = mode;
            state.ModeTime = 0f;
        }
    }
}
