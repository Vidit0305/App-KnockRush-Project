using KnockRush.Movement;
using UnityEngine;

namespace KnockRush.Player
{
    /// <summary>
    /// Local presentation event: a character's motor raised events this tick.
    /// Audio and VFX (Phase 9) subscribe to this through the EventBus.
    /// </summary>
    public readonly struct CharacterMotorEvent
    {
        public readonly PlayerCharacter Character;
        public readonly MotorEvents Events;
        public readonly Vector3 Position;

        public CharacterMotorEvent(PlayerCharacter character, MotorEvents events, Vector3 position)
        {
            Character = character;
            Events = events;
            Position = position;
        }
    }
}
