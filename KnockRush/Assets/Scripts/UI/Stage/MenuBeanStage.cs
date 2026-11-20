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
        [SerializeField] private Camera portraitCamera;
        [SerializeField] private Camera seasonCamera;
        [SerializeField] private CosmeticApplier seasonCosmetics;
        [SerializeField] private Animator seasonAnimator;
        [SerializeField] private string seasonBody = "body_bunny";
        [SerializeField] private string seasonColor = "color_bubblegum";
        [SerializeField] private string seasonPattern = "pattern_hearts";
        [SerializeField] private string seasonHat = "hat_none";

        private Loadout shown;
        private bool previewing;
        private float emoteEndsAt;
        private float nextIdleEmote;
        private float spinVelocity;
        private bool dragging;
        private Vector3 cameraHome;
        private Vector2 pressPosition;
        private float pressDistance;
        private float nextSeasonEmote;
        private float seasonEmoteEnds;

        /// <summary>Live head-and-shoulders render of the player's bean (Home avatar).</summary>
        public RenderTexture Portrait { get; private set; }

        /// <summary>Live render of the season's featured costume (Home season card).</summary>
        public RenderTexture SeasonPortrait { get; private set; }

        /// <summary>Layer the portrait cameras render (the stage beans live on it).</summary>
        public static int PortraitLayer => LayerMask.NameToLayer("Player") >= 0 ? LayerMask.NameToLayer("Player") : 6;

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

            Portrait = SetupPortrait(portraitCamera, 256);
            SeasonPortrait = SetupPortrait(seasonCamera, 320);
            if (seasonCosmetics != null)
            {
                seasonCosmetics.Apply(new Loadout { Body = seasonBody, Color = seasonColor, Pattern = seasonPattern, Hat = seasonHat, Emote = "emote_wave" });
                SetLayer(seasonCosmetics.gameObject, PortraitLayer);
                nextSeasonEmote = Time.time + 2f;
            }
        }

        private static RenderTexture SetupPortrait(Camera camera, int size)
        {
            if (camera == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return null; // headless (tests, servers): no portraits
            }

            var texture = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32) { name = camera.name, antiAliasing = 2 };
            texture.Create();
            camera.targetTexture = texture;
            camera.cullingMask = 1 << PortraitLayer;
            return texture;
        }

        private void OnDestroy()
        {
            ReleasePortrait(portraitCamera, Portrait);
            ReleasePortrait(seasonCamera, SeasonPortrait);
        }

        private static void ReleasePortrait(Camera camera, RenderTexture texture)
        {
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }

        private static void SetLayer(GameObject root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
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
                SetLayer(cosmetics.gameObject, PortraitLayer); // hats and costume parts too
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
            UpdatePortraits();
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
                pressPosition = pointer.position.ReadValue();
                pressDistance = 0f;
                dragging = !IsOverUi(pressPosition);
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                // A tap on the bean (not a drag) plays the equipped emote.
                if (dragging && pressDistance < 12f && emoteEndsAt <= 0f && IsOverBean(pointer.position.ReadValue()))
                {
                    PlayEquippedEmote();
                }

                dragging = false;
            }

            if (dragging && pointer.press.isPressed)
            {
                pressDistance += pointer.delta.ReadValue().magnitude;
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

        private bool IsOverBean(Vector2 screenPosition)
        {
            if (stageCamera == null || turntable == null)
            {
                return false;
            }

            var feet = stageCamera.WorldToScreenPoint(turntable.position);
            var head = stageCamera.WorldToScreenPoint(turntable.position + Vector3.up * 1.6f);
            var halfWidth = Mathf.Abs(head.y - feet.y) * 0.4f;
            return feet.z > 0f && screenPosition.y >= feet.y && screenPosition.y <= head.y && Mathf.Abs(screenPosition.x - feet.x) <= halfWidth;
        }

        /// <summary>Portrait cameras only run while the Home screen (which shows them) is up.</summary>
        private void UpdatePortraits()
        {
            var top = AppUI.Instance != null ? AppUI.Instance.Navigator?.Top : null;
            var home = top is KnockRush.UI.Screens.HomeScreen;
            if (portraitCamera != null && portraitCamera.enabled != home)
            {
                portraitCamera.enabled = home;
            }

            if (seasonCamera != null && seasonCamera.enabled != home)
            {
                seasonCamera.enabled = home;
            }

            if (!home || seasonAnimator == null || seasonAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            if (seasonEmoteEnds > 0f && Time.time > seasonEmoteEnds)
            {
                seasonEmoteEnds = 0f;
                seasonAnimator.CrossFadeInFixedTime(CharacterAnimationDriver.Locomotion, 0.25f);
            }
            else if (seasonEmoteEnds <= 0f && Time.time > nextSeasonEmote)
            {
                nextSeasonEmote = Time.time + 7f;
                seasonEmoteEnds = Time.time + EmoteSeconds;
                seasonAnimator.CrossFadeInFixedTime(EmoteStates.ForItem("emote_wave") ?? CharacterAnimationDriver.Celebrate, 0.2f);
            }
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
