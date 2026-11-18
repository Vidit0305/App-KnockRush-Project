using System;
using System.Threading.Tasks;
using KnockRush.Core;
using KnockRush.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace KnockRush.UI.Framework
{
    /// <summary>
    /// The game's single UI Toolkit root. Created at startup (never on dedicated servers or headless
    /// clients), it survives scene loads and hosts every screen: splash, menus, matchmaking, match
    /// HUD and results. <see cref="UiFlow"/> decides which screens the current game state needs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppUI : MonoBehaviour
    {
        private const string Tag = "UI";

        private UIDocument document;
        private VisualElement safeArea;
        private VisualElement toastLayer;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreenSize;
        private int busyCount;

        public static AppUI Instance { get; private set; }

        public ScreenNavigator Navigator { get; private set; }

        public UiFlow Flow { get; private set; }

        public VisualElement Root { get; private set; }

        public bool IsBusy => busyCount > 0;

        /// <summary>Raised by <see cref="Run"/> so screens can grey out while a request is running.</summary>
        public event Action<bool> BusyChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Instance != null || AppRoot.IsDedicatedServer || CommandLine.Has("-botclient") || CommandLine.Has("-noui"))
            {
                return;
            }

            if (Application.isBatchMode && !Application.isEditor)
            {
                return; // headless player (server, load-test bots)
            }

            Create();
        }

        public static AppUI Create()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var resources = UiResources.Load();
            if (resources == null || resources.PanelSettings == null)
            {
                Log.Warning(Tag, "UiResources missing: run KnockRush > Build > Build All Content.");
                return null;
            }

            var go = new GameObject("[AppUI]");
            go.SetActive(false);
            DontDestroyOnLoad(go);
            var ui = go.AddComponent<AppUI>();
            ui.document = go.AddComponent<UIDocument>();
            ui.document.panelSettings = resources.PanelSettings;
            ui.document.sortingOrder = 10;
            Instance = ui;
            go.SetActive(true);
            ui.Initialize(resources);
            return ui;
        }

        private void Initialize(UiResources resources)
        {
            Root = document.rootVisualElement;
            Root.AddToClassList("kr-root");
            if (resources.Theme != null && !Root.styleSheets.Contains(resources.Theme))
            {
                Root.styleSheets.Add(resources.Theme);
            }

            if (resources.BodyFont != null)
            {
                Root.style.unityFontDefinition = FontDefinition.FromFont(resources.BodyFont);
            }

            Root.pickingMode = PickingMode.Ignore;

            safeArea = Ui.Element("kr-safe");
            safeArea.pickingMode = PickingMode.Ignore;
            Root.Add(safeArea);

            var screenLayer = new VisualElement { name = "Screens", pickingMode = PickingMode.Ignore };
            screenLayer.style.flexGrow = 1;
            safeArea.Add(screenLayer);

            toastLayer = new VisualElement { name = "Toasts", pickingMode = PickingMode.Ignore };
            toastLayer.style.position = Position.Absolute;
            toastLayer.style.left = 0;
            toastLayer.style.right = 0;
            toastLayer.style.top = 110;
            toastLayer.style.alignItems = Align.Center;
            safeArea.Add(toastLayer);

            Navigator = new ScreenNavigator(this, screenLayer);
            Flow = new UiFlow(this);
            ApplySafeArea();
            Log.Info(Tag, "App UI ready.");
        }

        private void Update()
        {
            if (Navigator == null)
            {
                return;
            }

            ApplySafeArea();
            Flow.Update();
            Navigator.Tick(Time.unscaledDeltaTime);

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Navigator.Back();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            Flow?.Dispose();
        }

        /// <summary>Shows a short message at the top of the screen.</summary>
        public void Toast(string message, float seconds = 2.6f)
        {
            if (toastLayer == null)
            {
                return;
            }

            var toast = Ui.Label(message, "kr-toast");
            toast.style.opacity = 0f;
            toast.style.marginBottom = 8;
            toast.style.transitionProperty = new StyleList<StylePropertyName>(new System.Collections.Generic.List<StylePropertyName> { new StylePropertyName("opacity") });
            toast.style.transitionDuration = new StyleList<TimeValue>(new System.Collections.Generic.List<TimeValue> { new TimeValue(0.2f) });
            toastLayer.Add(toast);
            toast.schedule.Execute(() => toast.style.opacity = 1f).StartingIn(16);
            toast.schedule.Execute(() => toast.style.opacity = 0f).StartingIn((long)(seconds * 1000));
            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).StartingIn((long)(seconds * 1000) + 260);
            Log.Info(Tag, $"Toast: {message}");
        }

        /// <summary>
        /// Runs a backend/network request: marks the UI busy, shows errors as a toast, and returns
        /// whether it succeeded. Never throws.
        /// </summary>
        public async Task<bool> Run(Func<Task> action, string successMessage = null)
        {
            SetBusy(+1);
            try
            {
                await action();
                if (!string.IsNullOrEmpty(successMessage))
                {
                    Toast(successMessage);
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.Warning(Tag, $"Request failed: {exception.GetBaseException().Message}");
                Toast(Ui.ErrorText(exception));
                return false;
            }
            finally
            {
                SetBusy(-1);
            }
        }

        private void SetBusy(int delta)
        {
            var wasBusy = IsBusy;
            busyCount = Mathf.Max(0, busyCount + delta);
            if (wasBusy != IsBusy)
            {
                BusyChanged?.Invoke(IsBusy);
            }
        }

        /// <summary>Pads the UI so nothing sits under notches, punch-holes or rounded corners.</summary>
        private void ApplySafeArea()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            var area = Screen.safeArea;
            if (area == appliedSafeArea && screenSize == appliedScreenSize || Root?.panel == null || screenSize.x <= 0 || screenSize.y <= 0)
            {
                return;
            }

            appliedSafeArea = area;
            appliedScreenSize = screenSize;

            // Convert through the panel's scaling (valid before the first layout pass, unlike worldBound).
            var panel = Root.panel;
            var topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(area.xMin, screenSize.y - area.yMax));
            var bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(area.xMax, screenSize.y - area.yMin));
            var full = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenSize.x, screenSize.y));
            if (!IsFinite(topLeft) || !IsFinite(bottomRight) || !IsFinite(full))
            {
                appliedSafeArea = default; // panel not scaled yet; retry next frame
                return;
            }

            safeArea.style.left = Mathf.Max(0f, topLeft.x);
            safeArea.style.top = Mathf.Max(0f, topLeft.y);
            safeArea.style.right = Mathf.Max(0f, full.x - bottomRight.x);
            safeArea.style.bottom = Mathf.Max(0f, full.y - bottomRight.y);
        }

        private static bool IsFinite(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
    }
}
