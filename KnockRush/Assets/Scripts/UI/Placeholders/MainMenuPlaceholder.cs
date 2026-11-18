using System;
using KnockRush.Core;
using KnockRush.Core.Config;
using KnockRush.Core.SceneManagement;
using KnockRush.Core.Services;
using KnockRush.Core.Settings;
using KnockRush.Utilities;
using UnityEngine;

namespace KnockRush.UI.Placeholders
{
    /// <summary>
    /// PHASE 1 PLACEHOLDER for the main menu. Proves the core architecture works end to end:
    ///  - services resolve through the ServiceLocator,
    ///  - local settings persist between play sessions,
    ///  - the SceneLoader can round-trip Boot -> MainMenu.
    /// Replaced by the real home screen in Phase 8.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuPlaceholder : MonoBehaviour
    {
        private const string Tag = nameof(MainMenuPlaceholder);

        private ILocalSettingsService settingsService;
        private ISceneLoader sceneLoader;
        private GameConfig config;
        private float pendingSensitivity;
        private bool ready;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;

        private async void Start()
        {
            try
            {
                await AppRoot.InitializationTask;
            }
            catch (Exception exception)
            {
                Log.Exception(exception, this);
                return;
            }

            settingsService = ServiceLocator.Get<ILocalSettingsService>();
            sceneLoader = ServiceLocator.Get<ISceneLoader>();
            config = ServiceLocator.Get<GameConfig>();
            pendingSensitivity = settingsService.Current.CameraSensitivity;
            ready = true;
            Log.Info(Tag, "Main menu placeholder ready. Phase 1 core architecture is working.");
        }

        private void OnGUI()
        {
            if (!ready)
            {
                return;
            }

            EnsureStyles();

            var scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var panel = new Rect((width - 520f) * 0.5f, 80f, 520f, 620f);

            GUILayout.BeginArea(panel);
            GUILayout.Label("KNOCKRUSH", titleStyle);
            GUILayout.Label($"Main Menu (Phase 1 placeholder) | {config.Environment}", labelStyle);
            GUILayout.Space(24f);

            GUILayout.Label($"Camera sensitivity: {pendingSensitivity:0.00}  (saved: {settingsService.Current.CameraSensitivity:0.00})", labelStyle);
            pendingSensitivity = GUILayout.HorizontalSlider(pendingSensitivity, LocalSettings.MinSensitivity, LocalSettings.MaxSensitivity);
            GUILayout.Space(12f);

            if (GUILayout.Button("Save Settings", buttonStyle, GUILayout.Height(56f)))
            {
                var value = pendingSensitivity;
                settingsService.Modify(s => s.CameraSensitivity = value);
                Log.Info(Tag, $"Saved camera sensitivity {value:0.00}.");
            }

            GUILayout.Space(8f);

            GUI.enabled = !sceneLoader.IsLoading;
            if (GUILayout.Button("Play Hammer Rush (offline)", buttonStyle, GUILayout.Height(56f)))
            {
                Load(SceneNames.HammerRush);
            }

            GUILayout.Space(8f);

            if (GUILayout.Button("Movement Playground", buttonStyle, GUILayout.Height(56f)))
            {
                Load(SceneNames.Playground);
            }

            GUILayout.Space(8f);

            if (GUILayout.Button("Reload via Boot Scene", buttonStyle, GUILayout.Height(56f)))
            {
                Load(SceneNames.Boot);
            }

            GUI.enabled = true;
            GUILayout.EndArea();

            GUI.matrix = Matrix4x4.identity;
        }

        private async void Load(string sceneName)
        {
            try
            {
                await sceneLoader.LoadSceneAsync(sceneName);
            }
            catch (OperationCanceledException)
            {
                // Play mode exited during load.
            }
            catch (Exception exception)
            {
                Log.Exception(exception);
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            labelStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        }
    }
}
