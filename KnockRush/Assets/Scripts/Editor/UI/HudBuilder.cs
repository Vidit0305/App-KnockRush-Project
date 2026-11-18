using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Setup;
using KnockRush.UI.Common;
using KnockRush.UI.Hud;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KnockRush.EditorTools.UI
{
    /// <summary>Builds the race HUD prefab (countdown, timer, banner, toast). Placeholder styling until Phase 8.</summary>
    public static class HudBuilder
    {
        public const string RaceHudPath = "Assets/Prefabs/UI/RaceHud.prefab";

        public static GameObject BuildRaceHud()
        {
            KnockRushProjectSetup.EnsureFolder("Assets/Prefabs/UI");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("RaceHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                var safe = Rect("SafeArea", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                safe.gameObject.AddComponent<SafeAreaFitter>();

                var countdown = Label(safe, "Countdown", font, 220, new Vector2(0.5f, 0.55f), new Vector2(800f, 300f), new Color32(0xFF, 0xD1, 0x2B, 0xFF));
                var timer = Label(safe, "Timer", font, 54, new Vector2(0.5f, 0.94f), new Vector2(900f, 90f), Color.white);
                var banner = Label(safe, "Banner", font, 120, new Vector2(0.5f, 0.6f), new Vector2(1400f, 360f), new Color32(0x22, 0xC5, 0x8B, 0xFF));
                var toast = Label(safe, "Toast", font, 48, new Vector2(0.5f, 0.78f), new Vector2(900f, 80f), Color.white);

                var hud = root.AddComponent<RaceHud>();
                EditorAssetUtility.SetField(hud, "countdownText", countdown);
                EditorAssetUtility.SetField(hud, "timerText", timer);
                EditorAssetUtility.SetField(hud, "bannerText", banner);
                EditorAssetUtility.SetField(hud, "toastText", toast);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, RaceHudPath);
                Debug.Log($"[KnockRush Build] Race HUD prefab saved: {RaceHudPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static Text Label(RectTransform parent, string name, Font font, int size, Vector2 anchor, Vector2 dimensions, Color color)
        {
            var rect = Rect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
            rect.sizeDelta = dimensions;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(3f, -3f);
            text.text = string.Empty;
            return text;
        }
    }
}
