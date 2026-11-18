using System;
using UnityEngine;

namespace KnockRush.Movement
{
    /// <summary>High-level simulation state of a character.</summary>
    public enum MotorMode : byte
    {
        Grounded = 0,
        Airborne = 1,
        Diving = 2,
        Sliding = 3,
        Recovering = 4,
        Stumbled = 5,

        /// <summary>No simulation (eliminated, qualified, waiting to spawn). Position is frozen.</summary>
        Disabled = 6
    }

    /// <summary>One-tick events raised by <see cref="CharacterMotor.Step"/> (consumed by animation, audio, VFX).</summary>
    [Flags]
    public enum MotorEvents : ushort
    {
        None = 0,
        Jumped = 1 << 0,
        Landed = 1 << 1,
        Dived = 1 << 2,
        SlideStarted = 1 << 3,
        Recovered = 1 << 4,
        Stumbled = 1 << 5,
        LeftGround = 1 << 6,
        HitWall = 1 << 7,
        Impulse = 1 << 8,
        Respawned = 1 << 9
    }

    /// <summary>
    /// Input for one simulation tick. Move is already converted to world space (camera-relative).
    /// "Pressed" flags must be latched between ticks so a quick tap is never lost.
    /// </summary>
    [Serializable]
    public struct MotorInput
    {
        /// <summary>World-space XZ move direction; magnitude 0..1.</summary>
        public Vector2 Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool DivePressed;
        public bool GrabHeld;

        public static MotorInput None => default;
    }

    /// <summary>
    /// Complete simulation state of one character. Plain data so it can be copied, compared,
    /// rolled back and (Phase 4) replicated by the network layer.
    /// </summary>
    [Serializable]
    public struct MotorState
    {
        /// <summary>Feet position (bottom of the capsule).</summary>
        public Vector3 Position;

        /// <summary>Velocity relative to the world (excludes moving-surface carry).</summary>
        public Vector3 Velocity;

        /// <summary>Facing direction in degrees around world up (0 = +Z).</summary>
        public float Yaw;

        public MotorMode Mode;
        public bool IsGrounded;
        public Vector3 GroundNormal;

        /// <summary>Velocity of the surface the character stands on (moving platforms, conveyors).</summary>
        public Vector3 GroundVelocity;

        /// <summary>Friction multiplier of the current ground (1 = normal, lower = slippery).</summary>
        public float GroundFriction;

        /// <summary>Seconds spent in the current <see cref="Mode"/>.</summary>
        public float ModeTime;

        public float CoyoteTimer;
        public float JumpBufferTimer;

        /// <summary>True while rising from a jump (enables variable jump height).</summary>
        public bool IsJumpRising;

        public bool IsGrabbing;

        /// <summary>Downward speed at the most recent landing (m/s, positive).</summary>
        public float LastLandingSpeed;

        /// <summary>Events raised during the most recent step.</summary>
        public MotorEvents Events;

        public Vector3 Forward => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;

        public Vector3 HorizontalVelocity => new Vector3(Velocity.x, 0f, Velocity.z);

        public static MotorState Spawn(Vector3 position, float yaw)
        {
            return new MotorState
            {
                Position = position,
                Velocity = Vector3.zero,
                Yaw = yaw,
                Mode = MotorMode.Airborne,
                IsGrounded = false,
                GroundNormal = Vector3.up,
                GroundVelocity = Vector3.zero,
                GroundFriction = 1f,
                Events = MotorEvents.Respawned
            };
        }
    }

    /// <summary>
    /// Implemented by anything a character can stand on that changes movement:
    /// moving/rotating platforms, conveyors, slippery slime, sticky surfaces (Phase 3).
    /// </summary>
    public interface IMovementSurface
    {
        /// <summary>Multiplier on ground acceleration and friction. 1 = normal, ~0.15 = slime/ice.</summary>
        float FrictionMultiplier { get; }

        /// <summary>World-space velocity of the surface at <paramref name="worldPoint"/>.</summary>
        Vector3 GetSurfaceVelocity(Vector3 worldPoint);
    }
}
