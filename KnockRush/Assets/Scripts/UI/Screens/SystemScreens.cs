using System;
using System.Text.RegularExpressions;
using KnockRush.Core;
using KnockRush.Core.Services;
using KnockRush.Core.Settings;
using KnockRush.GameModes;
using KnockRush.UI.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KnockRush.UI.Screens
{
    /// <summary>1 · Splash: brand logo with bouncing bubbles while services initialise.</summary>
    public sealed class SplashScreen : UIScreen
    {
        private VisualElement fill;
        private Label logo;
        private float time;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-splash");
            root.Add(Bubbles());

            var center = Ui.Col("kr-center", "kr-grow");
            logo = Ui.Label("KNOCKRUSH", "kr-logo", "kr-display");
            center.Add(logo);
            center.Add(Ui.Label("32 players. One crown.", "kr-tagline"));
            var bar = Ui.ProgressBar(out fill);
            bar.style.width = 520;
            bar.style.marginTop = 48;
            center.Add(bar);
            root.Add(center);

            var version = Ui.Label($"v{Application.version}", "kr-subtle");
            version.style.position = Position.Absolute;
            version.style.right = 24;
            version.style.bottom = 12;
            root.Add(version);
        }

        public override void Tick(float deltaTime)
        {
            time += deltaTime;
            var initialized = AppRoot.IsInitialized ? 0.6f : 0.25f;
            Ui.SetProgress(fill, Mathf.Min(1f, initialized + time * 0.25f));
            var bounce = 1f + Mathf.Abs(Mathf.Sin(time * 3f)) * 0.04f;
            logo.style.scale = new Scale(new Vector3(bounce, bounce, 1f));
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

    /// <summary>2 · Loading: scene loads and round transitions (map name, goal, tips).</summary>
    public sealed class LoadingScreen : UIScreen
    {
        private static readonly string[] Tips =
        {
            "Dive (hold jump in the air) to cover gaps and recover from a bump.",
            "Grab other players to slow them down. Everyone can grab you too!",
            "Spinning sweepers have gaps: time your jump with their rhythm.",
            "In survival rounds, staying near the centre gives you more options.",
            "Checkpoints save your progress when you fall in race rounds.",
            "Equip a new emote in the Locker and show it off at the finish line."
        };

        private Label title;
        private Label subtitle;
        private Label tip;
        private VisualElement fill;
        private VisualElement spinner;
        private float progress;
        private float angle;

        public override ScreenLayer Layer => ScreenLayer.Overlay;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop-solid");
            root.Add(SplashScreen.Bubbles());
            var center = Ui.Col("kr-center", "kr-grow");
            spinner = Ui.Element("kr-spinner");
            center.Add(spinner);
            title = Ui.Title("LOADING");
            center.Add(title);
            subtitle = Ui.Label(string.Empty, "kr-heading");
            center.Add(subtitle);
            var bar = Ui.ProgressBar(out fill);
            bar.style.width = 640;
            bar.style.marginTop = 32;
            center.Add(bar);
            root.Add(center);
            tip = Ui.Label(string.Empty, "kr-tip");
            root.Add(tip);
        }

        public override void OnShow()
        {
            tip.text = "TIP: " + Tips[UnityEngine.Random.Range(0, Tips.Length)];
            progress = 0f;
        }

        public void ShowScene(string sceneName)
        {
            title.text = "LOADING";
            subtitle.text = sceneName == Core.SceneManagement.SceneNames.MainMenu ? "Back to the lobby" : sceneName;
        }

        public void ShowRound(MatchController match)
        {
            var map = match.CurrentMap;
            title.text = $"ROUND {match.RoundIndex + 1}";
            subtitle.text = map != null ? map.DisplayName.ToUpperInvariant() : string.Empty;
        }

        public void SetProgress(float value)
        {
            progress = Mathf.Max(progress, value);
        }

        public override void Tick(float deltaTime)
        {
            angle = (angle + deltaTime * 240f) % 360f;
            spinner.style.rotate = new Rotate(angle);
            progress = Mathf.Min(0.95f, progress + deltaTime * 0.08f);
            Ui.SetProgress(fill, progress);
        }

        public override bool OnBack() => false;
    }

    /// <summary>Reusable modal message / confirmation (overlay).</summary>
    public sealed class DialogScreen : UIScreen
    {
        private Label title;
        private Label message;
        private Button confirm;
        private Button cancel;
        private Action onConfirm;

        public override ScreenLayer Layer => ScreenLayer.Overlay;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-dim");
            root.AddToClassList("kr-center");
            var panel = Ui.Panel("kr-dialog");
            title = Ui.Heading(string.Empty);
            message = Ui.Label(string.Empty, "kr-dialog__text");
            panel.Add(title);
            panel.Add(message);
            var row = Ui.Row("kr-center");
            cancel = Ui.Button("CANCEL", () => Nav.Pop(), "kr-btn--ghost");
            confirm = Ui.Button("OK", () =>
            {
                Nav.Pop();
                onConfirm?.Invoke();
            }, "kr-btn--good");
            row.Add(cancel);
            row.Add(confirm);
            panel.Add(row);
            root.Add(panel);
        }

        public static void Show(AppUI app, string titleText, string text, string confirmText = "OK", Action confirmed = null, string cancelText = null)
        {
            var dialog = app.Navigator.Push<DialogScreen>();
            dialog.title.text = titleText;
            dialog.message.text = text;
            dialog.confirm.text = confirmText;
            dialog.onConfirm = confirmed;
            dialog.cancel.text = cancelText ?? "CANCEL";
            Ui.SetVisible(dialog.cancel, cancelText != null);
        }
    }

    /// <summary>22 · Settings: audio, controls, graphics, account and credits.</summary>
    public sealed class SettingsScreen : UIScreen
    {
        private static readonly string[] QualityNames = { "AUTO", "LOW", "MEDIUM", "HIGH" };

        private ILocalSettingsService settings;
        private Slider music;
        private Slider sfx;
        private Slider sensitivity;
        private Toggle invertY;
        private Toggle haptics;
        private VisualElement qualityRow;
        private Label account;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop");
            root.Add(Ui.Header("SETTINGS", () => Nav.Pop()));
            var scroll = Ui.Scroll();
            root.Add(scroll);

            var audio = Section(scroll, "AUDIO");
            music = AddSlider(audio, "Music", 0f, 1f, v => Modify(s => s.MusicVolume = v));
            sfx = AddSlider(audio, "Sound effects", 0f, 1f, v => Modify(s => s.SfxVolume = v));

            var controls = Section(scroll, "CONTROLS");
            sensitivity = AddSlider(controls, "Camera sensitivity", LocalSettings.MinSensitivity, LocalSettings.MaxSensitivity, v => Modify(s => s.CameraSensitivity = v));
            invertY = AddToggle(controls, "Invert camera Y", v => Modify(s => s.InvertCameraY = v));
            haptics = AddToggle(controls, "Vibration", v => Modify(s => s.HapticsEnabled = v));

            var graphics = Section(scroll, "GRAPHICS");
            qualityRow = Ui.Row();
            for (var i = 0; i < QualityNames.Length; i++)
            {
                var level = i - 1; // -1 = auto
                var tab = new Button(() => SetQuality(level)) { text = QualityNames[i] };
                tab.AddToClassList("kr-tab");
                tab.AddToClassList("kr-display");
                tab.userData = level;
                qualityRow.Add(tab);
            }

            graphics.Add(qualityRow);

            var about = Section(scroll, "ACCOUNT & ABOUT");
            account = Ui.Label(string.Empty, "kr-subtle");
            about.Add(account);
            var row = Ui.Row();
            row.Add(Ui.Button("PROFILE", () => Nav.Push<ProfileScreen>(), "kr-btn--small"));
            row.Add(Ui.Button("CREDITS", ShowCredits, "kr-btn--small", "kr-btn--ghost"));
            row.Add(Ui.Button("RESET SETTINGS", () => DialogScreen.Show(App, "Reset settings?", "Audio, controls and graphics go back to their defaults.", "RESET", () =>
            {
                settings?.ResetToDefaults();
                Refresh();
            }, "CANCEL"), "kr-btn--small", "kr-btn--danger"));
            about.Add(row);

            if (Debug.isDebugBuild)
            {
                var dev = Section(scroll, "DEVELOPER");
                var devRow = Ui.Row();
                devRow.Add(Ui.Button("MOVEMENT PLAYGROUND", () => LoadScene(Core.SceneManagement.SceneNames.Playground), "kr-btn--small", "kr-btn--ghost"));
                devRow.Add(Ui.Button("HAMMER RUSH SOLO", () => LoadScene(Core.SceneManagement.SceneNames.HammerRush), "kr-btn--small", "kr-btn--ghost"));
                dev.Add(devRow);
            }
        }

        private async void LoadScene(string sceneName)
        {
            if (ServiceLocator.TryGet<Core.SceneManagement.ISceneLoader>(out var loader))
            {
                await App.Run(() => loader.LoadSceneAsync(sceneName));
            }
        }

        public override void OnShow()
        {
            ServiceLocator.TryGet(out settings);
            Refresh();
        }

        private void Refresh()
        {
            if (settings == null)
            {
                return;
            }

            var s = settings.Current;
            music.SetValueWithoutNotify(s.MusicVolume);
            sfx.SetValueWithoutNotify(s.SfxVolume);
            sensitivity.SetValueWithoutNotify(s.CameraSensitivity);
            invertY.SetValueWithoutNotify(s.InvertCameraY);
            haptics.SetValueWithoutNotify(s.HapticsEnabled);
            foreach (var child in qualityRow.Children())
            {
                child.EnableInClassList("kr-tab--active", (int)child.userData == s.GraphicsQuality);
            }

            account.text = UiData.Online
                ? $"Signed in as {UiData.DisplayName} · ID {UiData.Backend.Uid}\nVersion {Application.version}"
                : $"Offline (practice modes available) · Version {Application.version}";
        }

        private void Modify(Action<LocalSettings> change)
        {
            settings?.Modify(change);
        }

        private void SetQuality(int level)
        {
            Modify(s => s.GraphicsQuality = level);
            Refresh();
        }

        private void ShowCredits()
        {
            DialogScreen.Show(App, "CREDITS",
                "KnockRush, an original party knockout game.\n\n" +
                "Fonts: Lilita One (Juan Montoreano) and Nunito (Vernon Adams, Cyreal), SIL Open Font License 1.1.\n" +
                "Networking: Photon Fusion. Backend: Firebase. Engine: Unity.");
        }

        private static VisualElement Section(VisualElement parent, string title)
        {
            var panel = Ui.Panel("kr-section");
            panel.Add(Ui.Label(title, "kr-section__title", "kr-display"));
            parent.Add(panel);
            return panel;
        }

        private static Slider AddSlider(VisualElement parent, string label, float min, float max, Action<float> changed)
        {
            var slider = new Slider(label, min, max);
            slider.AddToClassList("kr-slider");
            slider.RegisterValueChangedCallback(e => changed(e.newValue));
            parent.Add(slider);
            return slider;
        }

        private static Toggle AddToggle(VisualElement parent, string label, Action<bool> changed)
        {
            var toggle = new Toggle(label);
            toggle.AddToClassList("kr-toggle");
            toggle.RegisterValueChangedCallback(e => changed(e.newValue));
            parent.Add(toggle);
            return toggle;
        }
    }

    /// <summary>3 · Login / Registration: pick a unique username (guest account already exists).</summary>
    public sealed class LoginScreen : UIScreen
    {
        private static readonly Regex UsernamePattern = new Regex("^[A-Za-z][A-Za-z0-9_]{2,15}$");

        private TextField username;
        private Label hint;
        private Button submit;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop");
            root.Add(SplashScreen.Bubbles());
            var center = Ui.Col("kr-center", "kr-grow");
            var panel = Ui.Panel("kr-dialog");
            panel.Add(Ui.Title("WELCOME!"));
            panel.Add(Ui.Label("Choose a name other players will see. You can play as a guest and pick one later in your profile.", "kr-dialog__text"));
            username = Ui.Input("Username", 16);
            username.RegisterValueChangedCallback(_ => Validate());
            panel.Add(username);
            hint = Ui.Label("3–16 letters, numbers or _ , starting with a letter.", "kr-subtle");
            panel.Add(hint);

            var row = Ui.Row("kr-center");
            row.Add(Ui.Button("PLAY AS GUEST", () => Nav.Pop(), "kr-btn--ghost"));
            submit = Ui.Button("LET'S GO!", Submit, "kr-btn--good");
            row.Add(submit);
            panel.Add(row);

            var link = Ui.Row("kr-center");
            link.Add(Ui.Label("Keep your progress across devices:", "kr-subtle"));
            link.Add(Ui.Button(Application.platform == RuntimePlatform.IPhonePlayer ? "SIGN IN WITH APPLE" : "LINK GOOGLE", LinkAccount, "kr-btn--small", "kr-btn--ghost"));
            panel.Add(link);
            center.Add(panel);
            root.Add(center);
        }

        public override void OnShow()
        {
            username.SetValueWithoutNotify(UiData.Profile?.Username ?? string.Empty);
            Validate();
        }

        private void Validate()
        {
            var ok = UsernamePattern.IsMatch(username.value.Trim());
            submit.SetEnabled(ok && UiData.Online);
            hint.text = !UiData.Online ? "You're offline: connect to choose a username."
                : ok || username.value.Length == 0 ? "3–16 letters, numbers or _ , starting with a letter."
                : "That name doesn't fit the rules yet.";
        }

        private async void Submit()
        {
            var name = username.value.Trim();
            if (await App.Run(() => UiData.Backend.SetUsernameAsync(name), $"Welcome, {name}!"))
            {
                Nav.Pop();
            }
        }

        private void LinkAccount()
        {
            DialogScreen.Show(App, "LINK ACCOUNT",
                "Your guest progress is saved to this device's account. Linking Google Play Games or Sign in with Apple " +
                "uses the platform sign-in plugin included in store builds; your guest account will be upgraded, not replaced.");
        }
    }
}
