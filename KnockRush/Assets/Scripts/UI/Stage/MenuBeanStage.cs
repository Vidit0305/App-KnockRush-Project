using KnockRush.Inventory;
using KnockRush.Player.Presentation;
using KnockRush.UI.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KnockRush.UI.Stage
{
    /// <summary>
    /// The 3D bean behind the menus (MainMenu scene): wears the equipped loadout (or a locker
    /// preview), idles, plays emotes, turns when dragged, and slides aside for locker screens.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuBeanStage : MonoBehaviour
    {
        private const float EmoteSeconds = 3.2f;
        private const float IdleEmoteEvery = 11f;

        [SerializeField] private Transform turntable;
        [SerializeField] private Animator animator;
        [SerializeField] private CosmeticApplier cosmetics;
        [SerializeField] private Camera stageCamera;
        [SerializeField] private float lockerOffset = 1.1f;

        private Loadout shown;
        private bool previewing;
        private float emoteEndsAt;
        private float nextIdleEmote;
        private float spinVelocity;
        private bool dragging;
        private Vector3 cameraHome;

        public static MenuBeanStage Current { get; private set; }

        public Loadout Shown => shown;

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void Start()
        {
            if (stageCamera != null)
            {
                cameraHome = stageCamera.transform.localPosition;
            }

            Show(LocalLoadoutStore.Current);
            nextIdleEmote = Time.time + 4f;
        }

        /// <summary>Dresses the stage bean (pass the stored loadout to end a preview).</summary>
        public void Preview(Loadout loadout)
        {
            previewing = !loadout.Equals(LocalLoadoutStore.Current);
            Show(loadout);
        }

        public void PlayEquippedEmote() => PlayEmote(shown.Emote);

        public void PlayEmote(string emoteItemId)
        {
            var state = EmoteStates.ForItem(emoteItemId) ?? CharacterAnimationDriver.Celebrate;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.CrossFadeInFixedTime(state, 0.15f);
                emoteEndsAt = Time.time + EmoteSeconds;
            }
        }

        private void Show(Loadout loadout)
        {
            if (cosmetics != null && (!cosmetics.HasApplied || !loadout.Equals(shown)))
            {
                shown = loadout;
                cosmetics.Apply(loadout);
            }
        }

        private void Update()
        {
            // Follow loadout changes made elsewhere (backend profile sync, equip).
            if (!previewing && !shown.Equals(LocalLoadoutStore.Current))
            {
                Show(LocalLoadoutStore.Current);
            }

            if (emoteEndsAt > 0f && Time.time > emoteEndsAt)
            {
                emoteEndsAt = 0f;
                animator.CrossFadeInFixedTime(CharacterAnimationDriver.Locomotion, 0.25f);
            }
            else if (emoteEndsAt <= 0f && Time.time > nextIdleEmote)
            {
                nextIdleEmote = Time.time + IdleEmoteEvery;
                PlayEquippedEmote();
            }

            UpdateDrag();
            UpdateFraming();
        }

        private void UpdateDrag()
        {
            var pointer = Pointer.current;
            if (pointer == null || turntable == null)
            {
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                dragging = !IsOverUi(pointer.position.ReadValue());
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                dragging = false;
            }

            if (dragging && pointer.press.isPressed)
            {
                var degrees = -pointer.delta.ReadValue().x * 0.4f;
                turntable.Rotate(0f, degrees, 0f, Space.Self);
                spinVelocity = degrees / Mathf.Max(Time.deltaTime, 0.001f);
                return;
            }

            // Coast, then ease back to facing the camera.
            var angle = Mathf.DeltaAngle(turntable.localEulerAngles.y, 0f);
            spinVelocity = Mathf.Lerp(spinVelocity, angle * 2f, Time.deltaTime * 3f);
            turntable.Rotate(0f, spinVelocity * Time.deltaTime, 0f, Space.Self);
        }

        private static bool IsOverUi(Vector2 screenPosition)
        {
            var app = AppUI.Instance;
            if (app == null || app.Root?.panel == null)
            {
                return false;
            }

            var panelPosition = UnityEngine.UIElements.RuntimePanelUtils.ScreenToPanel(app.Root.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            var picked = app.Root.panel.Pick(panelPosition);
            // Screen backgrounds don't count: only real controls (buttons, panels, lists) block the drag.
            return picked != null && picked != app.Root && !picked.ClassListContains("kr-screen");
        }

        private void UpdateFraming()
        {
            if (stageCamera == null)
            {
                return;
            }

            var top = AppUI.Instance != null ? AppUI.Instance.Navigator?.Top : null;
            var offset = top != null && top.ShowsStage && !(top is KnockRush.UI.Screens.HomeScreen) ? lockerOffset : 0f;
            var target = cameraHome + stageCamera.transform.localRotation * new Vector3(offset, 0f, 0f);
            stageCamera.transform.localPosition = Vector3.Lerp(stageCamera.transform.localPosition, target, Time.deltaTime * 6f);
        }
    }
}
