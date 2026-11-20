using System;
using System.IO;
using System.Linq;
using KnockRush.Maps;
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

        /// <summary>
        /// Rebuilds the MainMenu lobby and renders it through its own stage camera (no UI):
        /// Tools/unity.sh exec KnockRush.EditorTools.Diagnostics.SceneCapture.CaptureMenuBatch (with graphics).
        /// </summary>
        public static void CaptureMenuBatch()
        {
            var exitCode = 0;
            try
            {
                UI.UiContentBuilder.BuildMainMenuScene();
                var output = GetArg("-captureOut") ?? "../.logs/captures";
                Directory.CreateDirectory(output);
                var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
                Render(camera, camera.transform.position, camera.transform.position + camera.transform.forward, Path.Combine(output, "MainMenu_stage.png"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>Captures every map scene in Assets/Scenes/Maps (overview + checkpoints).</summary>
        public static void CaptureAllMapsBatch()
        {
            var exitCode = 0;
            try
            {
                var output = GetArg("-captureOut") ?? "../.logs/captures";
                foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/Maps" }))
                {
                    CaptureScene(AssetDatabase.GUIDToAssetPath(guid), output, "Idle");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>Renders every costume (20) on a bean in a 5×4 grid: Tools/unity.sh exec ...CaptureCosmeticsBatch.</summary>
        public static void CaptureCosmeticsBatch()
        {
            var exitCode = 0;
            try
            {
                var output = GetArg("-captureOut") ?? "../.logs/captures";
                Directory.CreateDirectory(output);
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var catalog = KnockRush.Inventory.CosmeticCatalog.Load();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Bean/Bean.fbx");
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/Bean/Bean_Toon.mat");
                var costumes = new System.Collections.Generic.List<KnockRush.Inventory.CosmeticDefinition>(catalog.InSlot(KnockRush.Inventory.CosmeticSlot.Body));
                var colors = new System.Collections.Generic.List<KnockRush.Inventory.CosmeticDefinition>(catalog.InSlot(KnockRush.Inventory.CosmeticSlot.Color));
                var hats = new System.Collections.Generic.List<KnockRush.Inventory.CosmeticDefinition>(catalog.InSlot(KnockRush.Inventory.CosmeticSlot.Hat));
                for (var i = 0; i < costumes.Count; i++)
                {
                    var bean = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    bean.transform.SetPositionAndRotation(new Vector3((i % 5 - 2f) * 1.6f, -(i / 5) * 2.1f, 0f), Quaternion.identity);
                    var renderer = bean.GetComponentInChildren<SkinnedMeshRenderer>();
                    renderer.sharedMaterial = material;
                    var applier = bean.AddComponent<KnockRush.Inventory.CosmeticApplier>();
                    var so = new SerializedObject(applier);
                    so.FindProperty("bodyRenderer").objectReferenceValue = renderer;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    var loadout = KnockRush.Inventory.Loadout.Default;
                    loadout.Body = costumes[i].ItemId;
                    loadout.Color = colors[i % colors.Count].ItemId;
                    applier.Apply(loadout);
                    if (i == 0)
                    {
                        loadout.Hat = hats[2].ItemId;
                    }
                }

                var camera = Camera.main;
                Render(camera, new Vector3(0f, -1.3f, -11.5f), new Vector3(0f, -2.2f, 0f), Path.Combine(output, "cosmetics_costumes.png"));
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

            // Map scenes have no camera (it comes from the runtime rig): use a temporary one.
            var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                camera = new GameObject("CaptureCamera").AddComponent<Camera>();
                camera.fieldOfView = 60f;
                camera.farClipPlane = 600f;
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
            var size = Mathf.Max(bounds.size.x, bounds.size.z);
            var wideFrom = bounds.center + new Vector3(-size * 0.45f, size * 0.38f, -size * 0.05f);
            Render(camera, wideFrom, bounds.center, Path.Combine(outputFolder, $"{sceneName}_overview.png"));

            // One shot per checkpoint, looking down the course (validates every section).
            foreach (var checkpoint in Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).OrderBy(c => c.Order))
            {
                var position = checkpoint.transform.position;
                Render(camera, position + new Vector3(0f, 4.5f, -6f), position + new Vector3(0f, 0f, 12f),
                    Path.Combine(outputFolder, $"{sceneName}_checkpoint{checkpoint.Order}.png"));
            }
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
            // Gameplay geometry only (solid colliders): ignores sky decor, clouds and the sea.
            var colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => !c.isTrigger).ToArray();
            if (colliders.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one * 20f);
            }

            var bounds = colliders[0].bounds;
            foreach (var collider in colliders)
            {
                bounds.Encapsulate(collider.bounds);
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
