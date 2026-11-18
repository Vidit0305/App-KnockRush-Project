using UnityEngine;

namespace KnockRush.UI.Common
{
    /// <summary>
    /// Fits this RectTransform to <see cref="Screen.safeArea"/> (notches, rounded corners,
    /// gesture bars). Put it on the root panel of every screen-space canvas.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect appliedArea;
        private Vector2Int appliedScreen;

        private void OnEnable()
        {
            rectTransform = GetComponent<RectTransform>();
            Apply(force: true);
        }

        private void Update()
        {
            Apply(force: false);
        }

        private void Apply(bool force)
        {
            var area = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && area == appliedArea && screen == appliedScreen)
            {
                return;
            }

            appliedArea = area;
            appliedScreen = screen;
            if (screen.x <= 0 || screen.y <= 0)
            {
                return;
            }

            var min = area.position;
            var max = area.position + area.size;
            min.x /= screen.x;
            min.y /= screen.y;
            max.x /= screen.x;
            max.y /= screen.y;

            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
