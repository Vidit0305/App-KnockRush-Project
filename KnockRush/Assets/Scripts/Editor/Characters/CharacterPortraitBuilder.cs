using System.IO;
using KnockRush.EditorTools.Setup;
using KnockRush.EditorTools.UI;
using KnockRush.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KnockRush.EditorTools.Characters
{
    /// <summary>
    /// Renders a head-and-shoulders portrait of every character (costume) for the Characters
    /// screen cards: Assets/Art/UI/Characters/Resources/CharacterPortraits/&lt;itemId&gt;.png.
    /// Needs a graphics device: Tools/unity.sh portraits.
    /// </summary>
    public static class CharacterPortraitBuilder
    {
        public const string Folder = "Assets/Art/UI/Characters/Resources/CharacterPortraits";
        private const int Size = 256;

        [MenuItem("KnockRush/Build/Character Portraits", priority = 232)]
        public static void BuildFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Build();
            }
        }

        public static void BuildBatch()
        {
            var code = 0;
            try
            {
                Build();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        public static void Build()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogWarning("[KnockRush Build] Character portraits need a graphics device (run without -nographics). Skipped.");
                return;
            }

            var catalog = CosmeticCatalog.Load();
            if (catalog == null)
            {
                throw new System.InvalidOperationException("Cosmetic catalog missing: build cosmetics first.");
            }

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            KnockRushProjectSetup.EnsureFolder(Folder);
            var assets = new MenuStageBuilder.BeanAssets
            {
                Model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetBuilder.BeanModelPath),
                Material = AssetDatabase.LoadAssetAtPath<Material>(CharacterAssetBuilder.BeanMaterialPath),
                Controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterAssetBuilder.AnimatorPath)
            };

            var light = Object.FindFirstObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
            light.intensity = 1.25f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(215, 228, 255, 255);
            RenderSettings.ambientEquatorColor = new Color32(200, 205, 240, 255);
            RenderSettings.ambientGroundColor = new Color32(150, 130, 200, 255);

            var (root, _, cosmetics) = MenuStageBuilder.SpawnBean(null, assets, "PortraitModel", Vector3.zero, 0f);
            var camera = Object.FindFirstObjectByType<Camera>();
            camera.transform.position = new Vector3(0f, 1.12f, 2.2f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.04f, 0f) - camera.transform.position);
            camera.fieldOfView = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;

            var texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            camera.targetTexture = texture;
            var count = 0;
            foreach (var costume in catalog.InSlot(CosmeticSlot.Body))
            {
                cosmetics.Apply(new Loadout { Body = costume.ItemId, Color = "default_orange", Pattern = "pattern_none", Hat = "hat_none", Emote = "emote_wave" });
                camera.Render(); // warm-up (materials / accessories)
                camera.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                image.Apply();
                File.WriteAllBytes($"{Folder}/{costume.ItemId}.png", image.EncodeToPNG());
                Object.DestroyImmediate(image);
                count++;
            }

            RenderTexture.active = null;
            camera.targetTexture = null;
            texture.Release();
            Object.DestroyImmediate(root.gameObject);
            AssetDatabase.Refresh();
            foreach (var costume in catalog.InSlot(CosmeticSlot.Body))
            {
                MenuStageBuilder.ImportTexture($"{Folder}/{costume.ItemId}.png", true, true, 256);
            }

            Debug.Log($"[KnockRush Build] {count} character portraits -> {Folder}");
        }
    }
}
