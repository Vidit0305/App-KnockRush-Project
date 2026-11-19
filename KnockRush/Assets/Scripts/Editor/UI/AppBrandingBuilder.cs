using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace KnockRush.EditorTools.UI
{
    /// <summary>
    /// App branding from the key art: launcher icons (default, Android legacy/round/adaptive, iOS)
    /// and the native startup splash background, so the very first frame already shows KnockRush.
    /// </summary>
    public static class AppBrandingBuilder
    {
        public static Sprite ImportLoadingBackground(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                throw new System.InvalidOperationException($"Missing key art at {path}.");
            }

            var changed = importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize != 2048 || importer.mipmapEnabled;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.alphaSource = TextureImporterAlphaSource.None;
            if (changed)
            {
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void ApplyIconsAndSplash(string iconPath, string adaptiveBackgroundPath, string adaptiveForegroundPath, Sprite splashBackground)
        {
            var icon = ImportIcon(iconPath);
            var adaptiveBackground = ImportIcon(adaptiveBackgroundPath);
            var adaptiveForeground = ImportIcon(adaptiveForegroundPath);

            // Default icon: used by every platform/size without an override (iOS, Android legacy, Windows).
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            // Android: legacy + round use the square art; adaptive uses art background + empty foreground.
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var platformIcon in icons)
                {
                    if (platformIcon.minLayerCount >= 2)
                    {
                        platformIcon.SetTextures(adaptiveBackground, adaptiveForeground);
                    }
                    else
                    {
                        platformIcon.SetTextures(icon);
                    }
                }

                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }

            // Native startup splash: the key art (no logos), handing over seamlessly to the in-game splash.
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[0];
            PlayerSettings.SplashScreen.background = splashBackground;
            PlayerSettings.SplashScreen.backgroundPortrait = splashBackground;
            PlayerSettings.SplashScreen.backgroundColor = new Color32(0x2D, 0x8B, 0xF0, 0xFF);
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            AssetDatabase.SaveAssets();
            Debug.Log($"[KnockRush Build] App icons ({PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android).Length} Android kinds) and splash applied.");
        }

        private static Texture2D ImportIcon(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                throw new System.InvalidOperationException($"Missing icon at {path}.");
            }

            if (importer.textureType != TextureImporterType.Default || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
