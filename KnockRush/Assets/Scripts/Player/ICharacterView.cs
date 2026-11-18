using System;
using KnockRush.Movement;

namespace KnockRush.Player
{
    /// <summary>
    /// Read-only view of a simulated character for presentation (animation, audio, VFX).
    /// Implemented by the offline <see cref="PlayerCharacter"/> and the networked character, so
    /// the same visuals work in every mode.
    /// </summary>
    public interface ICharacterView
    {
        MotorState State { get; }

        MovementSettings Settings { get; }

        /// <summary>True for the character controlled on this device.</summary>
        bool IsLocallyControlled { get; }

        bool HasFinished { get; }

        event Action<MotorEvents> MotorEventsRaised;

        event Action Finished;
    }
}
