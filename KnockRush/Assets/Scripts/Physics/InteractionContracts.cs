using UnityEngine;

namespace KnockRush.Interaction
{
    /// <summary>Anything that obstacles, bumpers and other players can push around.</summary>
    public interface IKnockbackTarget
    {
        /// <summary>Feet position of the target.</summary>
        Vector3 Position { get; }

        /// <summary>Adds an instantaneous velocity change (m/s).</summary>
        void ApplyImpulse(Vector3 impulse);
    }

    /// <summary>
    /// A character taking part in a course: it can hit checkpoints, fall out and finish.
    /// Implemented by the player character (offline) and, from Phase 4, by networked players.
    /// </summary>
    public interface ICourseParticipant : IKnockbackTarget
    {
        /// <summary>Highest checkpoint order reached (-1 = none).</summary>
        int CheckpointOrder { get; set; }

        bool HasFinished { get; }

        /// <summary>When true, the participant ignores movement input (countdowns, results).</summary>
        bool InputLocked { get; set; }

        /// <summary>Stable id for UI and results (offline: instance id; online: player ref).</summary>
        int ParticipantId { get; }

        void SetRespawnPoint(Vector3 position, float yaw);

        void Respawn();

        void MarkFinished();
    }

    /// <summary>Provides the world velocity of a moving object at a point (used for knockback).</summary>
    public interface IPointVelocityProvider
    {
        Vector3 GetPointVelocity(Vector3 worldPoint);
    }

    /// <summary>Raised when an obstacle or zone knocks a target (for audio, VFX, camera shake).</summary>
    public readonly struct KnockbackEvent
    {
        public readonly IKnockbackTarget Target;
        public readonly Vector3 Impulse;
        public readonly Vector3 Point;

        public KnockbackEvent(IKnockbackTarget target, Vector3 impulse, Vector3 point)
        {
            Target = target;
            Impulse = impulse;
            Point = point;
        }
    }
}
