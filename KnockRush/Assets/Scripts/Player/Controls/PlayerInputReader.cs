using KnockRush.CameraRig;
using KnockRush.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KnockRush.Player.Controls
{
    /// <summary>
    /// Reads the local player's controls (keyboard/mouse, gamepad, on-screen touch controls)
    /// and turns them into a <see cref="MotorInput"/> for each simulation tick.
    ///
    /// On-screen controls (Phase 2 mobile UI) drive a virtual gamepad, so the gamepad bindings
    /// below cover touch too. Taps are latched until the next tick consumes them, so a quick
    /// press between fixed updates (or network ticks) is never lost.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private ThirdPersonCamera cameraRig;

        private InputActionMap map;
        private InputAction move;
        private InputAction mouseLook;
        private InputAction mouseLookHold;
        private InputAction stickLook;
        private InputAction jump;
        private InputAction dive;
        private InputAction grab;
        private InputAction emoteCelebrate;
        private InputAction emoteVictory;

        private bool jumpLatched;
        private bool diveLatched;
        private Vector2 pendingTouchLook;

        /// <summary>The reader of the player controlled on this device (null if none).</summary>
        public static PlayerInputReader Local { get; private set; }

        public ThirdPersonCamera CameraRig
        {
            get => cameraRig;
            set => cameraRig = value;
        }

        public float CameraYaw => cameraRig != null ? cameraRig.Yaw : 0f;

        public bool CelebratePressedThisFrame => emoteCelebrate != null && emoteCelebrate.WasPressedThisFrame();

        public bool VictoryPressedThisFrame => emoteVictory != null && emoteVictory.WasPressedThisFrame();

        /// <summary>Called by the on-screen look area with a drag delta in pixels.</summary>
        public void AddTouchLook(Vector2 pixels)
        {
            pendingTouchLook += pixels;
        }

        /// <summary>Builds the input for one simulation tick and clears latched presses.</summary>
        public MotorInput ConsumeInput()
        {
            var raw = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            var world = Quaternion.Euler(0f, CameraYaw, 0f) * new Vector3(raw.x, 0f, raw.y);

            var input = new MotorInput
            {
                Move = new Vector2(world.x, world.z),
                JumpPressed = jumpLatched || jump.WasPressedThisFrame(),
                JumpHeld = jump.IsPressed(),
                DivePressed = diveLatched || dive.WasPressedThisFrame(),
                GrabHeld = grab.IsPressed()
            };

            jumpLatched = false;
            diveLatched = false;
            return input;
        }

        private void Awake()
        {
            BuildActions();
        }

        private void OnEnable()
        {
            map.Enable();
            Local = this;
        }

        private void OnDisable()
        {
            map.Disable();
            if (Local == this)
            {
                Local = null;
            }
        }

        private void OnDestroy()
        {
            map?.Dispose();
        }

        private void Update()
        {
            if (jump.WasPressedThisFrame())
            {
                jumpLatched = true;
            }

            if (dive.WasPressedThisFrame())
            {
                diveLatched = true;
            }

            if (cameraRig == null)
            {
                pendingTouchLook = Vector2.zero;
                return;
            }

            if (mouseLookHold.IsPressed())
            {
                cameraRig.AddMouseDelta(mouseLook.ReadValue<Vector2>());
            }

            cameraRig.AddStick(stickLook.ReadValue<Vector2>(), Time.deltaTime);

            if (pendingTouchLook != Vector2.zero)
            {
                cameraRig.AddTouchDelta(pendingTouchLook);
                pendingTouchLook = Vector2.zero;
            }
        }

        private void BuildActions()
        {
            map = new InputActionMap("Player");

            move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            mouseLook = map.AddAction("MouseLook", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            mouseLookHold = map.AddAction("MouseLookHold", InputActionType.Button, "<Mouse>/rightButton");
            stickLook = map.AddAction("StickLook", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");

            jump = map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            dive = map.AddAction("Dive", InputActionType.Button, "<Keyboard>/e");
            dive.AddBinding("<Keyboard>/leftCtrl");
            dive.AddBinding("<Gamepad>/buttonWest");

            grab = map.AddAction("Grab", InputActionType.Button, "<Keyboard>/f");
            grab.AddBinding("<Gamepad>/buttonEast");
            grab.AddBinding("<Gamepad>/rightTrigger");

            emoteCelebrate = map.AddAction("EmoteCelebrate", InputActionType.Button, "<Keyboard>/1");
            emoteCelebrate.AddBinding("<Gamepad>/dpad/up");
            emoteVictory = map.AddAction("EmoteVictory", InputActionType.Button, "<Keyboard>/2");
            emoteVictory.AddBinding("<Gamepad>/dpad/down");
        }
    }
}
