using Fusion;
using KnockRush.Movement;
using UnityEngine;

namespace KnockRush.Multiplayer
{
    /// <summary>
    /// Replicated part of <see cref="MotorState"/>. Position and yaw are replicated by the
    /// NetworkTransform on the same object, so they are not duplicated here.
    /// </summary>
    public struct NetworkMotorState : INetworkStruct
    {
        public Vector3 Velocity;
        public Vector3 GroundNormal;
        public Vector3 GroundVelocity;
        public float GroundFriction;
        public float ModeTime;
        public float CoyoteTimer;
        public float JumpBufferTimer;
        public float LastLandingSpeed;
        public byte Mode;
        public NetworkBool IsGrounded;
        public NetworkBool IsJumpRising;
        public NetworkBool IsGrabbing;

        public static NetworkMotorState From(in MotorState state)
        {
            return new NetworkMotorState
            {
                Velocity = state.Velocity,
                GroundNormal = state.GroundNormal,
                GroundVelocity = state.GroundVelocity,
                GroundFriction = state.GroundFriction,
                ModeTime = state.ModeTime,
                CoyoteTimer = state.CoyoteTimer,
                JumpBufferTimer = state.JumpBufferTimer,
                LastLandingSpeed = state.LastLandingSpeed,
                Mode = (byte)state.Mode,
                IsGrounded = state.IsGrounded,
                IsJumpRising = state.IsJumpRising,
                IsGrabbing = state.IsGrabbing
            };
        }

        public MotorState ToMotorState(Vector3 position, float yaw)
        {
            return new MotorState
            {
                Position = position,
                Yaw = yaw,
                Velocity = Velocity,
                GroundNormal = GroundNormal == Vector3.zero ? Vector3.up : GroundNormal,
                GroundVelocity = GroundVelocity,
                GroundFriction = GroundFriction <= 0f ? 1f : GroundFriction,
                ModeTime = ModeTime,
                CoyoteTimer = CoyoteTimer,
                JumpBufferTimer = JumpBufferTimer,
                LastLandingSpeed = LastLandingSpeed,
                Mode = (MotorMode)Mode,
                IsGrounded = IsGrounded,
                IsJumpRising = IsJumpRising,
                IsGrabbing = IsGrabbing
            };
        }
    }
}
