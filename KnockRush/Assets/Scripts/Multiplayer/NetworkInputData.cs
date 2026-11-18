using Fusion;
using KnockRush.Movement;
using UnityEngine;

namespace KnockRush.Multiplayer
{
    public enum InputButton
    {
        Jump = 0,
        Dive = 1,
        Grab = 2
    }

    /// <summary>
    /// Per-tick player input sent client → server (~12 bytes). Move is already world-space
    /// (camera-relative conversion happens on the client), so the server never needs camera data.
    /// </summary>
    public struct NetworkInputData : INetworkInput
    {
        public Vector2 Move;
        public NetworkButtons Buttons;

        public static NetworkInputData FromMotorInput(in MotorInput input)
        {
            var data = new NetworkInputData { Move = Vector2.ClampMagnitude(input.Move, 1f) };
            data.Buttons.Set(InputButton.Jump, input.JumpHeld || input.JumpPressed);
            data.Buttons.Set(InputButton.Dive, input.DivePressed);
            data.Buttons.Set(InputButton.Grab, input.GrabHeld);
            return data;
        }

        /// <summary>Rebuilds motor input; "pressed" edges come from comparing with the previous tick's buttons.</summary>
        public MotorInput ToMotorInput(NetworkButtons previous)
        {
            var pressed = Buttons.GetPressed(previous);
            return new MotorInput
            {
                // Server-side sanitising: never trust a client to send a longer vector.
                Move = Vector2.ClampMagnitude(Move, 1f),
                JumpPressed = pressed.IsSet(InputButton.Jump),
                JumpHeld = Buttons.IsSet(InputButton.Jump),
                DivePressed = pressed.IsSet(InputButton.Dive),
                GrabHeld = Buttons.IsSet(InputButton.Grab)
            };
        }
    }
}
