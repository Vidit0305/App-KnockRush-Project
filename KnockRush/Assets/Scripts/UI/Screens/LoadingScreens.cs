using KnockRush.Core;
using KnockRush.GameModes;
using KnockRush.UI.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KnockRush.UI.Screens
{
    /// <summary>A screen with a progress bar that must reach 100% before the flow moves on.</summary>
    public interface ILoadingScreen
    {
        /// <summary>The work is done: run the bar to 100%.</summary>
        void Complete();

        /// <summary>The bar has shown 100% (briefly held) and the screen may be replaced.</summary>
        bool IsFinished { get; }
    }

    /// <summary>
    /// The branded loading look shared by the splash and every loading screen: full-bleed key art,
    /// "LOADING..." title, a glossy striped progress bar with percentage and the tagline.
    /// </summary>
    internal sealed class KeyArtLoadingView
    {
        public const string Tagline = "Get Ready for the Ultimate Knockout!";

        private readonly Label title;
        private readonly Label percent;
        private readonly Label tagline;
        private readonly VisualElement fill;
        private readonly VisualElement stripes;
        private const float FillSpeed = 0.55f;       // bar per second while loading (0 to 100% in at least 1.8 s)


        private float shown;
        private float stripeOffset;
        private bool completing;
        private float heldAtFull;

        public KeyArtLoadingView(VisualElement parent)
        {
            var dock = Ui.Col("kr-keyart-dock");
            dock.pickingMode = PickingMode.Ignore;
            title = Ui.Label("LOADING...", "kr-keyart-title", "kr-display");
            dock.Add(title);

            var row = Ui.Row("kr-keyart-row");
            var track = Ui.Element("kr-keyart-track");
            fill = Ui.Element("kr-keyart-fill");
            fill.Add(Ui.Element("kr-keyart-gloss"));
            stripes = Ui.Element("kr-keyart-stripes");
            for (var i = 0; i < 40; i++)
            {
                stripes.Add(Ui.Element("kr-keyart-stripe"));
            }

            fill.Add(stripes);
            track.Add(fill);
            row.Add(track);
            percent = Ui.Label("0%", "kr-keyart-percent", "kr-display");
            row.Add(percent);
            dock.Add(row);

            tagline = Ui.Label($"—  {Tagline}  —", "kr-keyart-tagline", "kr-display");
            dock.Add(tagline);
            parent.Add(dock);
            SetProgressImmediate(0f);
        }

        public float Target { get; set; }

        /// <summary>Once complete, the rest of the bar fills within this many seconds.</summary>
        public float FinishSeconds { get; set; } = 0.8f;

        /// <summary>How long "100%" stays readable before the next screen.</summary>
        public float HoldAtFull { get; set; } = 0.25f;

        public void SetTitle(string text) => title.text = text;

        public void SetTagline(string text) => tagline.text = $"—  {text}  —";

        public bool IsFinished => completing && shown >= 1f && heldAtFull >= HoldAtFull;

        public void SetProgressImmediate(float value)
        {
            shown = Target = Mathf.Clamp01(value);
            completing = false;
            heldAtFull = 0f;
            Apply();
        }

        /// <summary>Loading finished: the bar runs on to 100% (never jumps, never goes backwards).</summary>
        public void Complete()
        {
            completing = true;
            Target = 1f;
        }

        /// <summary>Eases the bar towards <see cref="Target"/> and scrolls the stripes.</summary>
        public void Tick(float deltaTime)
        {
            var speed = completing ? Mathf.Max(FillSpeed, (1f - shown) / FinishSeconds) : FillSpeed;
            shown = Mathf.MoveTowards(shown, Mathf.Clamp01(completing ? 1f : Mathf.Min(Target, 0.99f)), deltaTime * speed);
            if (completing && shown >= 1f)
            {
                heldAtFull += deltaTime;
            }

            stripeOffset = (stripeOffset + deltaTime * 60f) % 48f;
            stripes.style.translate = new Translate(stripeOffset - 48f, 0);
            Apply();
        }

        private void Apply()
        {
            fill.style.width = Length.Percent(Mathf.Max(4f, shown * 100f));
            percent.text = $"{Mathf.RoundToInt(shown * 100f)}%";
        }
    }

    /// <summary>1 · Splash: the key art while the app starts.</summary>
    public sealed class SplashScreen : UIScreen, ILoadingScreen
    {
        private const float MinSeconds = 2.2f;

        private KeyArtLoadingView view;
        private float time;
        private float sceneProgress;
        private bool completeRequested;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-keyart-screen");
            view = new KeyArtLoadingView(root);
        }

        public override void OnShow()
        {
            time = 0f;
            sceneProgress = 0f;
            completeRequested = false;
            view.SetProgressImmediate(0f);
            App.ShowKeyArt(true);
        }

        public override void OnHide()
        {
            App.ShowKeyArt(false);
        }

        public override void Tick(float deltaTime)
        {
            // Scripts already run behind Unity's own startup logo: start the bar only once it is gone,
            // so the player always sees it sweep from 0%.
            if (!UnityEngine.Rendering.SplashScreen.isFinished)
            {
                time = 0f;
                view.SetProgressImmediate(0f);
                return;
            }

            time += deltaTime;
            var boot = Object.FindFirstObjectByType<Core.SceneManagement.BootSceneController>();
            if (boot != null && boot.Failed)
            {
                view.SetTitle("SOMETHING WENT WRONG");
                view.SetTagline("Please restart the game");
                return;
            }

            // Paced (0 to 100% over a couple of seconds) but never ahead of the real work:
            // services first, then the menu scene load reported by the loader events.
            var real = AppRoot.IsInitialized ? 0.6f + 0.39f * sceneProgress : 0.55f;
            view.Target = Mathf.Max(view.Target, Mathf.Min(time / MinSeconds, real));
            if (completeRequested && time >= MinSeconds * 0.7f)
            {
                view.Complete();
            }

            view.Tick(deltaTime);
        }

        public void SetProgress(float value)
        {
            sceneProgress = Mathf.Max(sceneProgress, Mathf.Clamp01(value));
        }

        public void Complete() => completeRequested = true;

        public bool IsFinished => view.IsFinished;

        public override bool OnBack() => false;

        /// <summary>Decorative floating circles in brand colours.</summary>
        internal static VisualElement Bubbles()
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            Color[] colors =
            {
                new Color32(255, 106, 26, 90), new Color32(255, 209, 43, 80), new Color32(34, 197, 139, 80),
                new Color32(255, 79, 154, 80), new Color32(91, 46, 255, 120)
            };
            var random = new System.Random(7);
            for (var i = 0; i < 14; i++)
            {
                var size = 60 + random.Next(220);
                var bubble = new VisualElement { pickingMode = PickingMode.Ignore };
                bubble.style.position = Position.Absolute;
                bubble.style.width = bubble.style.height = size;
                bubble.style.borderTopLeftRadius = bubble.style.borderTopRightRadius = bubble.style.borderBottomLeftRadius = bubble.style.borderBottomRightRadius = size / 2f;
                bubble.style.left = Length.Percent(random.Next(100));
                bubble.style.top = Length.Percent(random.Next(100));
                bubble.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                bubble.style.backgroundColor = colors[i % colors.Length];
                layer.Add(bubble);
            }

            return layer;
        }
    }

    /// <summary>2 · Loading: every scene load and round transition shows the same key-art screen.</summary>
    public sealed class LoadingScreen : UIScreen, ILoadingScreen
    {
        private KeyArtLoadingView view;
        private Label context;

        // A full page: it hides whatever is underneath while the key art is up.
        public override ScreenLayer Layer => ScreenLayer.Page;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-keyart-screen");
            context = Ui.Label(string.Empty, "kr-keyart-context", "kr-display");
            root.Add(context);
            view = new KeyArtLoadingView(root);
        }

        public override void OnShow()
        {
            view.SetProgressImmediate(0f);
            App.ShowKeyArt(true);
        }

        public override void OnHide()
        {
            App.ShowKeyArt(false);
        }

        public void ShowScene(string sceneName)
        {
            view.FinishSeconds = 0.8f;
            view.HoldAtFull = 0.25f;
            context.text = string.Empty;
            Ui.SetVisible(context, false);
        }

        public void ShowRound(MatchController match)
        {
            // Between rounds the countdown is already running: finish the bar quickly.
            view.FinishSeconds = 0.35f;
            view.HoldAtFull = 0.12f;
            var map = match.CurrentMap;
            context.text = map != null ? $"ROUND {match.RoundIndex + 1}  ·  {map.DisplayName.ToUpperInvariant()}" : $"ROUND {match.RoundIndex + 1}";
            Ui.SetVisible(context, true);
        }

        public void SetProgress(float value)
        {
            view.Target = Mathf.Max(view.Target, Mathf.Clamp01(value));
        }

        public void Complete() => view.Complete();

        public bool IsFinished => view.IsFinished;

        public override void Tick(float deltaTime)
        {
            // Creep forward while waiting on the network so the bar never looks frozen.
            view.Target = Mathf.Max(view.Target, Mathf.Min(0.9f, view.Target + deltaTime * 0.12f));
            view.Tick(deltaTime);
        }

        public override bool OnBack() => false;
    }
}
