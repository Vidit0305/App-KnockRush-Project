using KnockRush.EditorTools.Common;
using KnockRush.Inventory;
using KnockRush.UI.Stage;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace KnockRush.EditorTools.UI
{
    /// <summary>
    /// The Home stage: the painted arena backdrop (camera-attached, always fills the screen),
    /// a soft contact shadow under the player's bean, and the bean spawning helper shared with the
    /// portrait models.
    /// </summary>
    internal static class MenuStageBuilder
    {
        public const string BackdropPath = "Assets/Art/UI/Backgrounds/HomeBackground.png";
        public const string ShadowPath = "Assets/Art/World/Textures/BlobShadow.png";
        public const string LockerBackdropPath = "Assets/Art/UI/Backgrounds/LockerBackground.png";
        public const string PageBackgroundPath = "Assets/Art/UI/Backgrounds/PageBackground.png";
        private const float BackdropDistance = 40f;

        public struct BeanAssets
        {
            public GameObject Model;
            public Material Material;
            public RuntimeAnimatorController Controller;
        }

        /// <summary>Builds the stage; returns the backdrop renderer (its texture swaps per screen).</summary>
        public static Renderer Build(Transform root, Camera camera)
        {
            var backdrop = Backdrop(camera);
            ContactShadow(root);
            ImportTexture(LockerBackdropPath, false, true, 2048);
            ImportTexture(PageBackgroundPath, false, true, 2048);
            return backdrop;
        }

        public static Texture2D LockerBackdrop => AssetDatabase.LoadAssetAtPath<Texture2D>(LockerBackdropPath);

        public static Texture2D HomeBackdrop => AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropPath);

        private static Renderer Backdrop(Camera camera)
        {
            var texture = ImportTexture(BackdropPath, false, true, 2048);
            if (texture == null)
            {
                throw new System.InvalidOperationException($"Missing home backdrop at {BackdropPath}.");
            }

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Backdrop";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(camera.transform, false);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Unlit("HomeBackdrop", texture, Color.white, false);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            quad.AddComponent<StageBackdrop>().Configure(camera, BackdropDistance, (float)texture.width / texture.height);
            return renderer;
        }

        private static void ContactShadow(Transform root)
        {
            var texture = ImportTexture(ShadowPath, true, false, 256);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ContactShadow";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            quad.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.01f, 0f), Quaternion.Euler(90f, 0f, 0f));
            quad.transform.localScale = new Vector3(1.9f, 1.5f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Unlit("ContactShadow", texture, new Color(0.1f, 0.12f, 0.4f, 0.8f), true);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public static (Transform root, Animator animator, CosmeticApplier cosmetics) SpawnBean(Transform parent, BeanAssets assets, string name, Vector3 position, float yaw)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root, false);
            var bean = (GameObject)PrefabUtility.InstantiatePrefab(assets.Model);
            bean.name = "Bean";
            bean.transform.SetParent(visual.transform, false);
            bean.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // model faces −Z after import
            var renderer = bean.GetComponentInChildren<SkinnedMeshRenderer>();
            renderer.sharedMaterial = assets.Material;
            renderer.updateWhenOffscreen = true;
            var animator = bean.GetComponent<Animator>() ?? bean.AddComponent<Animator>();
            animator.runtimeAnimatorController = assets.Controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var cosmetics = visual.AddComponent<CosmeticApplier>();
            EditorAssetUtility.SetField(cosmetics, "bodyRenderer", renderer);
            return (root, animator, cosmetics);
        }

        /// <summary>
        /// Imports a UI/stage texture: clamped, alpha-is-transparency, optional mips and compression.
        /// On Android large art uses ASTC 6x6 and small UI art ASTC 4x4 (much smaller than RGBA32).
        /// </summary>
        public static Texture2D ImportTexture(string path, bool mipmaps, bool compress, int maxSize = 1024)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                return null;
            }

            var compression = compress ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            var androidFormat = maxSize >= 1024 ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4;
            var android = importer.GetPlatformTextureSettings("Android");
            if (importer.mipmapEnabled != mipmaps || !importer.alphaIsTransparency || importer.textureCompression != compression
                || importer.wrapMode != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None || importer.maxTextureSize != maxSize
                || !android.overridden || android.format != androidFormat || android.maxTextureSize != maxSize)
            {
                android.overridden = true;
                android.format = androidFormat;
                android.maxTextureSize = maxSize;
                importer.SetPlatformTextureSettings(android);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = mipmaps;
                importer.alphaIsTransparency = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = compression;
                importer.maxTextureSize = maxSize;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material Unlit(string name, Texture texture, Color color, bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = EditorAssetUtility.LoadOrCreate($"{UiContentBuilder.MenuMaterialsFolder}/{name}.mat", () => new Material(shader));
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", 2f);
            material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            if (transparent)
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = (int)RenderQueue.Geometry;
            }

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
