using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Setup;
using KnockRush.UI.Common;
using KnockRush.UI.Controls;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace KnockRush.EditorTools.UI
{
    /// <summary>
    /// Builds the landscape mobile controls prefab: floating-style joystick on the left,
    /// Jump / Dive / Grab buttons on the right, and a touch-look drag area. Placeholder
    /// visuals use Unity's built-in UI sprites until the Phase 8 UI art pass.
    /// </summary>
    public static class MobileControlsBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/MobileControls.prefab";

        private static readonly Color StickBase = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color StickKnob = new Color(1f, 1f, 1f, 0.75f);
        private static readonly Color JumpColor = new Color32(0xFF, 0xD1, 0x2B, 0xD0);
        private static readonly Color DiveColor = new Color32(0x2D, 0x9C, 0xFF, 0xD0);
        private static readonly Color GrabColor = new Color32(0xFF, 0x4F, 0x9A, 0xD0);

        public static GameObject Build()
        {
            KnockRushProjectSetup.EnsureFolder("Assets/Prefabs/UI");
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("MobileControls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;

                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                var safeArea = CreateRect("SafeArea", root.transform);
                Stretch(safeArea);
                safeArea.gameObject.AddComponent<SafeAreaFitter>();

                var controls = root.AddComponent<MobileControlsRoot>();
                EditorAssetUtility.SetField(controls, "controlsContainer", safeArea.gameObject);

                // Look area: right 60% of the screen, behind the buttons.
                var look = CreateRect("TouchLookArea", safeArea);
                look.anchorMin = new Vector2(0.4f, 0f);
                look.anchorMax = new Vector2(1f, 1f);
                look.offsetMin = look.offsetMax = Vector2.zero;
                var lookImage = look.gameObject.AddComponent<Image>();
                lookImage.color = new Color(0f, 0f, 0f, 0f);
                look.gameObject.AddComponent<TouchLookArea>();

                // Joystick (bottom-left).
                var stickBase = CreateRect("StickBase", safeArea);
                stickBase.anchorMin = stickBase.anchorMax = new Vector2(0f, 0f);
                stickBase.pivot = new Vector2(0.5f, 0.5f);
                stickBase.anchoredPosition = new Vector2(260f, 240f);
                stickBase.sizeDelta = new Vector2(300f, 300f);
                var baseImage = stickBase.gameObject.AddComponent<Image>();
                baseImage.sprite = circle;
                baseImage.color = StickBase;
                baseImage.raycastTarget = false;

                var knob = CreateRect("StickKnob", stickBase);
                knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
                knob.sizeDelta = new Vector2(140f, 140f);
                var knobImage = knob.gameObject.AddComponent<Image>();
                knobImage.sprite = circle;
                knobImage.color = StickKnob;
                var stick = knob.gameObject.AddComponent<OnScreenStick>();
                stick.controlPath = "<Gamepad>/leftStick";
                stick.movementRange = 110f;

                // Buttons (bottom-right).
                CreateButton(safeArea, "JumpButton", "JUMP", "<Gamepad>/buttonSouth", new Vector2(-200f, 210f), 220f, JumpColor, circle, font);
                CreateButton(safeArea, "DiveButton", "DIVE", "<Gamepad>/buttonWest", new Vector2(-430f, 140f), 170f, DiveColor, circle, font);
                CreateButton(safeArea, "GrabButton", "GRAB", "<Gamepad>/buttonEast", new Vector2(-180f, 450f), 150f, GrabColor, circle, font);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[KnockRush Build] Mobile controls prefab saved: {PrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Creates an EventSystem using the Input System UI module (required for touch UI).</summary>
        public static GameObject CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return eventSystem;
        }

        private static void CreateButton(Transform parent, string name, string label, string controlPath,
            Vector2 offsetFromBottomRight, float size, Color color, Sprite sprite, Font font)
        {
            var rect = CreateRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offsetFromBottomRight;
            rect.sizeDelta = new Vector2(size, size);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;

            var button = rect.gameObject.AddComponent<OnScreenButton>();
            button.controlPath = controlPath;

            var text = CreateRect("Label", rect);
            Stretch(text);
            var uiText = text.gameObject.AddComponent<Text>();
            uiText.text = label;
            uiText.font = font;
            uiText.fontSize = Mathf.RoundToInt(size * 0.2f);
            uiText.fontStyle = FontStyle.Bold;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.color = Color.white;
            uiText.raycastTarget = false;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
