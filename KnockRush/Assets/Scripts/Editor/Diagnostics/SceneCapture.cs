using System;
using System.IO;
using System.Linq;
using KnockRush.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KnockRush.EditorTools.Diagnostics
{
    /// <summary>
    /// Renders scenes to PNG files without entering Play mode, for automated visual review
    /// (CI artefacts, store-page drafts, regression checks).
    ///
    /// Command line:
    ///   Unity.exe -batchmode -projectPath KnockRush -executeMethod KnockRush.EditorTools.Diagnostics.SceneCapture.CaptureBatch
    ///     -captureScene Assets/Scenes/Dev/Playground.unity -captureOut ../.logs/captures [-captureClip Run]
    /// </summary>
    public static class SceneCapture
    {
        public const int Width = 1280;
        public const int Height = 720;

        public static void CaptureBatch()
        {
            var exitCode = 0;
            try
            {
                var scene = GetArg("-captureScene") ?? "Assets/Scenes/Dev/Playground.unity";
                var output = GetArg("-captureOut") ?? "../.logs/captures";
                var clip = GetArg("-captureClip") ?? "Run";
                CaptureScene(scene, output, clip);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>Captures a character close-up (posed with <paramref name="clipName"/>) and a wide shot.</summary>
        public static void CaptureScene(string scenePath, string outputFolder, string clipName)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(outputFolder);
            var sceneName = Path.GetFileNameWithoutExtension(scenePath);

            var camera = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException($"No camera in {scenePath}.");
            }

            var player = Object.FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                PosePlayer(player, clipName);
                var target = player.transform.position + Vector3.up * 0.8f;
                var forward = player.transform.forward;
                Render(camera, target + forward * 3.6f + Vector3.up * 0.9f + player.transform.right * 1.4f, target,
                    Path.Combine(outputFolder, $"{sceneName}_character_{clipName}.png"));
                Render(camera, player.transform.position + new Vector3(0f, 3.2f, -6.5f), target + forward * 4f,
                    Path.Combine(outputFolder, $"{sceneName}_gameplay.png"));
            }

            var bounds = ComputeLevelBounds();
            var wideFrom = bounds.center + new Vector3(-bounds.extents.x * 0.9f, bounds.extents.magnitude * 0.55f, -bounds.extents.z * 1.1f);
            Render(camera, wideFrom, bounds.center, Path.Combine(outputFolder, $"{sceneName}_overview.png"));
        }

        private static void PosePlayer(PlayerCharacter player, string clipName)
        {
            var animator = player.GetComponentInChildren<Animator>();
            var controller = animator != null ? animator.runtimeAnimatorController : null;
            var clip = controller != null ? controller.animationClips.FirstOrDefault(c => c.name == clipName) : null;
            if (clip != null)
            {
                clip.SampleAnimation(animator.gameObject, clip.length * 0.3f);
            }
        }

        private static Bounds ComputeLevelBounds()
        {
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one * 20f);
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        private static void Render(Camera camera, Vector3 from, Vector3 lookAt, string path)
        {
            camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(lookAt - from, Vector3.up));

            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render(); // warm-up: lets the editor finish preparing materials/shaders
                camera.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"[SceneCapture] Wrote {Path.GetFullPath(path)}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
