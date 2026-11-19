using KnockRush.Core;
using KnockRush.GameModes;
using KnockRush.UI.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KnockRush.UI.Screens
{
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
        private float shown;
        private float stripeOffset;

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

        public void SetTitle(string text) => title.text = text;

        public void SetTagline(string text) => tagline.text = $"—  {text}  —";

        public void SetProgressImmediate(float value)
        {
            shown = Target = Mathf.Clamp01(value);
            Apply();
        }

        /// <summary>Eases the bar towards <see cref="Target"/> and scrolls the stripes.</summary>
        public void Tick(float deltaTime)
        {
            shown = Mathf.MoveTowards(shown, Mathf.Clamp01(Target), deltaTime * 1.5f);
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
    public sealed class SplashScreen : UIScreen
    {
        private KeyArtLoadingView view;
        private float time;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-keyart-screen");
            view = new KeyArtLoadingView(root);
        }

        public override void OnShow()
        {
            time = 0f;
            view.SetProgressImmediate(0f);
            App.ShowKeyArt(true);
        }

        public override void OnHide()
        {
            App.ShowKeyArt(false);
        }

        public override void Tick(float deltaTime)
        {
            time += deltaTime;
            var boot = Object.FindFirstObjectByType<Core.SceneManagement.BootSceneController>();
            if (boot != null && boot.Failed)
            {
                view.SetTitle("SOMETHING WENT WRONG");
                view.SetTagline("Please restart the game");
                return;
            }

            // Services first (60%), then the menu scene load reported by the loader events.
            view.Target = Mathf.Max(view.Target, AppRoot.IsInitialized ? Mathf.Min(0.6f + time * 0.08f, 0.9f) : Mathf.Min(time * 0.4f, 0.55f));
            view.Tick(deltaTime);
        }

        public void SetProgress(float value)
        {
            view.Target = Mathf.Max(view.Target, 0.6f + 0.4f * Mathf.Clamp01(value));
        }

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
    public sealed class LoadingScreen : UIScreen
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
            view.SetProgressImmediate(0.05f);
            App.ShowKeyArt(true);
        }

        public override void OnHide()
        {
            App.ShowKeyArt(false);
        }

        public void ShowScene(string sceneName)
        {
            context.text = string.Empty;
            Ui.SetVisible(context, false);
        }

        public void ShowRound(MatchController match)
        {
            var map = match.CurrentMap;
            context.text = map != null ? $"ROUND {match.RoundIndex + 1}  ·  {map.DisplayName.ToUpperInvariant()}" : $"ROUND {match.RoundIndex + 1}";
            Ui.SetVisible(context, true);
        }

        public void SetProgress(float value)
        {
            view.Target = Mathf.Max(view.Target, Mathf.Clamp01(value));
        }

        public override void Tick(float deltaTime)
        {
            // Creep forward while waiting on the network so the bar never looks frozen.
            view.Target = Mathf.Max(view.Target, Mathf.Min(0.95f, view.Target + deltaTime * 0.05f));
            view.Tick(deltaTime);
        }

        public override bool OnBack() => false;
    }
}
