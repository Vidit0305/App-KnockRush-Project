using KnockRush.Core.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnockRush.Core.Diagnostics
{
    /// <summary>
    /// Small one-line overlay (bottom-left) with FPS, frame time, build and scene info.
    /// Added automatically by AppRoot in the Editor and Development builds only.
    /// Uses IMGUI on purpose: zero setup, no dependency on the real UI (Phase 8).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugOverlay : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        private const float SmoothingFactor = 0.1f;

        private GameConfig config;
        private float smoothedDeltaTime;
        private GUIStyle style;

        public void Initialize(GameConfig gameConfig)
        {
            config = gameConfig;
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            smoothedDeltaTime = smoothedDeltaTime <= 0f
                ? delta
                : Mathf.Lerp(smoothedDeltaTime, delta, SmoothingFactor);
        }

        private void OnGUI()
        {
            if (CommandLine.Has("-uicapture") || CommandLine.Has("-nodebugoverlay"))
            {
                return; // clean screenshots
            }

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    richText = true
                };
                style.normal.textColor = Color.white;
            }

            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            var fps = smoothedDeltaTime > 0f ? 1f / smoothedDeltaTime : 0f;
            var fpsColor = fps >= 55f ? "#7CFC7C" : fps >= 30f ? "#FFD84D" : "#FF6B6B";
            var environment = config != null ? config.Environment.ToString() : "?";
            var safeArea = Screen.safeArea;

            // One compact line at the bottom-left, clear of the game HUD (top) and touch controls.
            var text =
                $"<b>KnockRush</b> v{Application.version} ({environment})  ·  <color={fpsColor}>{fps:0} FPS</color> {smoothedDeltaTime * 1000f:0.0} ms  ·  " +
                $"{SceneManager.GetActiveScene().name}  ·  {Screen.width}x{Screen.height} safe {safeArea.width:0}x{safeArea.height:0}";

            var width = Screen.width / scale;
            var rect = new Rect(6f, ReferenceHeight - 20f, Mathf.Min(width - 12f, 560f), 16f);
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 4f, rect.y, rect.width, rect.height), text, style);

            GUI.matrix = Matrix4x4.identity;
        }
    }
}
