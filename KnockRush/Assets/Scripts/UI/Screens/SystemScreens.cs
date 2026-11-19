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
