using System;
using KnockRush.Core.SceneManagement;
using KnockRush.Core.Services;
using KnockRush.Movement;
using KnockRush.Player;
using KnockRush.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KnockRush.UI.Placeholders
{
    /// <summary>
    /// Development helpers for the movement Playground: help text, live motor state readout,
    /// respawn (R), back to menu (Esc / on-screen button). Not used in real game scenes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaygroundDevTools : MonoBehaviour
    {
        [SerializeField] private PlayerCharacter character;

        private GUIStyle style;
        private GUIStyle buttonStyle;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame && character != null)
            {
                character.Respawn();
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                BackToMenu();
            }
        }

        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                style.normal.textColor = Color.white;
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            }

            var scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;

            if (GUI.Button(new Rect(width - 130f, 10f, 120f, 36f), "Menu", buttonStyle))
            {
                BackToMenu();
            }

            if (character != null)
            {
                var s = character.State;
                var text =
                    $"<b>Playground</b>  WASD move · Space jump · E dive · F grab · 1/2 emotes · R respawn · RMB look\n" +
                    $"Mode: {s.Mode}  Grounded: {s.IsGrounded}  Speed: {s.HorizontalVelocity.magnitude:0.0} m/s  Vy: {s.Velocity.y:0.0}";
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(new Rect(8f, 112f, 640f, 44f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(14f, 114f, 640f, 44f), text, style);
            }

            GUI.matrix = Matrix4x4.identity;
        }

        private async void BackToMenu()
        {
            if (!ServiceLocator.TryGet<ISceneLoader>(out var loader) || loader.IsLoading)
            {
                return;
            }

            try
            {
                await loader.LoadSceneAsync(SceneNames.MainMenu);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Log.Exception(exception);
            }
        }
    }
}
