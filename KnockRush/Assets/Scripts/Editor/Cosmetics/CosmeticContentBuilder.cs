using System.Collections.Generic;
using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Setup;
using KnockRush.Inventory;
using UnityEditor;
using UnityEngine;

namespace KnockRush.EditorTools.Cosmetics
{
    /// <summary>
    /// Generates the cosmetic content: procedural accessory meshes, accessory prefabs (authored in
    /// the bean's mesh space: +Y up, face towards −Z, head top ≈ 1.52 m), and one CosmeticDefinition
    /// per catalogue item (20 costumes, 15 colours, 8 patterns, 10 hats, 10 emotes).
    /// Item ids mirror Backend/firebase/functions/src/catalog.ts.
    /// </summary>
    public static class CosmeticContentBuilder
    {
        private const string MeshFolder = "Assets/Art/Cosmetics/Meshes";
        private const string MaterialFolder = "Assets/Materials/Cosmetics";
        private const string PrefabFolder = "Assets/Prefabs/Cosmetics";
        private const string DefinitionFolder = "Assets/ScriptableObjects/Cosmetics";
        public const string CatalogPath = DefinitionFolder + "/Resources/CosmeticCatalog.asset";

        // Mesh-space landmarks of the bean.
        private const float HeadTop = 1.5f;
        private const float EyeY = 1.14f;
        private const float FaceZ = -0.38f;
        private const float BackZ = 0.38f;

        // The character's small body under the big head (outfit torso, neck, shoulders).
        private const float NeckY = 0.78f;
        private const float ChestY = 0.6f;
        private const float ChestFrontZ = -0.21f;
        private const float ChestBackZ = 0.2f;
        private const float ShoulderX = 0.27f;
        private const float ChinY = 0.86f;
        private const float ChinZ = -0.28f;

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static Mesh cone;
        private static Mesh torus;
        private static readonly List<CosmeticDefinition> Definitions = new List<CosmeticDefinition>();

        public static void Build()
        {
            foreach (var folder in new[] { MeshFolder, MaterialFolder, PrefabFolder, DefinitionFolder + "/Resources" })
            {
                KnockRushProjectSetup.EnsureFolder(folder);
            }

            Materials.Clear();
            Definitions.Clear();
            cone = SaveMesh("Cone", ProceduralMeshes.Cone(0.5f, 1f, 24));
            torus = SaveMesh("Torus", ProceduralMeshes.Torus(0.5f, 0.12f, 32, 12));

            BuildCostumes();
            BuildColors();
            BuildPatterns();
            BuildHats();
            BuildEmotes();

            var catalog = EditorAssetUtility.LoadOrCreate(CatalogPath, ScriptableObject.CreateInstance<CosmeticCatalog>);
            catalog.Configure(Definitions.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[KnockRush Build] Cosmetics: {Definitions.Count} items.");
        }

        // ---------------------------------------------------------------------------------------
        // Costumes (20)
        // ---------------------------------------------------------------------------------------
        private static void BuildCostumes()
        {
            Costume("default_bean", "Classic Rusher", CosmeticRarity.Common, null, null);
            Costume("body_astronaut", "Astro Rusher", CosmeticRarity.Epic, C(0xF2F4F8), root =>
            {
                Sphere(root, new Vector3(0f, EyeY, FaceZ + 0.03f), new Vector3(0.62f, 0.34f, 0.14f), C(0x1E2A5A));
                Box(root, new Vector3(0f, ChestY + 0.04f, ChestBackZ + 0.08f), new Vector3(0.36f, 0.36f, 0.16f), C(0xB8C0CC));
                Cyl(root, new Vector3(0.12f, ChestY + 0.26f, ChestBackZ + 0.12f), new Vector3(0.06f, 0.08f, 0.06f), C(0xE83A3A));
            });
            Costume("body_dino", "Dino Rusher", CosmeticRarity.Legendary, C(0x4CC46A), root =>
            {
                for (var i = 0; i < 5; i++)
                {
                    var y = 0.5f + i * 0.22f;
                    Cone(root, new Vector3(0f, y, (y < 0.8f ? ChestBackZ : BackZ) + 0.02f - Mathf.Abs(i - 2) * 0.01f), Quaternion.Euler(-80f, 0f, 0f), new Vector3(0.16f, 0.2f, 0.16f), C(0xFFD12B));
                }
                Cone(root, new Vector3(0f, 0.4f, ChestBackZ + 0.08f), Quaternion.Euler(-100f, 0f, 0f), new Vector3(0.22f, 0.48f, 0.22f), C(0x3BA458));
            }, "Hips");
            Costume("body_chef", "Chef Rusher", CosmeticRarity.Rare, C(0xFFFFFF), root =>
            {
                Cyl(root, new Vector3(0f, HeadTop + 0.1f, 0f), new Vector3(0.42f, 0.14f, 0.42f), C(0xFFFFFF));
                for (var i = 0; i < 4; i++)
                {
                    Sphere(root, new Vector3(Mathf.Cos(i * 1.57f) * 0.12f, HeadTop + 0.3f, Mathf.Sin(i * 1.57f) * 0.12f), Vector3.one * 0.26f, C(0xFFFFFF));
                }
                Torus(root, new Vector3(0f, NeckY, 0f), Quaternion.identity, new Vector3(0.42f, 0.45f, 0.4f), C(0xE83A3A));
            });
            Costume("body_pirate", "Pirate Rusher", CosmeticRarity.Rare, null, root =>
            {
                Sphere(root, new Vector3(0f, HeadTop - 0.08f, 0f), new Vector3(0.82f, 0.3f, 0.76f), C(0xE83A3A));
                Cyl(root, new Vector3(0.15f, EyeY, FaceZ - 0.04f), new Vector3(0.16f, 0.02f, 0.16f), C(0x111111), Quaternion.Euler(90f, 0f, 0f));
                Torus(root, new Vector3(-0.42f, 1.0f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one * 0.12f, C(0xFFD12B));
            });
            Costume("body_ninja", "Ninja Rusher", CosmeticRarity.Epic, C(0x2B2B3A), root =>
            {
                Torus(root, new Vector3(0f, 1.3f, 0f), Quaternion.identity, new Vector3(0.74f, 0.6f, 0.7f), C(0xE83A3A));
                Box(root, new Vector3(0.08f, 1.25f, BackZ + 0.08f), new Vector3(0.06f, 0.25f, 0.04f), C(0xE83A3A), Quaternion.Euler(20f, 0f, 20f));
                Box(root, new Vector3(-0.08f, 1.22f, BackZ + 0.08f), new Vector3(0.06f, 0.25f, 0.04f), C(0xE83A3A), Quaternion.Euler(20f, 0f, -20f));
            });
            Costume("body_robot", "Robo Rusher", CosmeticRarity.Epic, C(0x9AA4B8), root =>
            {
                Cyl(root, new Vector3(0f, HeadTop + 0.12f, 0f), new Vector3(0.04f, 0.14f, 0.04f), C(0x2B2B3A));
                Sphere(root, new Vector3(0f, HeadTop + 0.3f, 0f), Vector3.one * 0.12f, C(0xE83A3A));
                Box(root, new Vector3(0f, ChestY, ChestFrontZ - 0.01f), new Vector3(0.24f, 0.18f, 0.05f), C(0x2D9CFF));
                Sphere(root, new Vector3(0.44f, 1.0f, 0f), Vector3.one * 0.1f, C(0xFFD12B));
                Sphere(root, new Vector3(-0.44f, 1.0f, 0f), Vector3.one * 0.1f, C(0xFFD12B));
            });
            Costume("body_knight", "Knight Rusher", CosmeticRarity.Legendary, C(0xC9D1DE), root =>
            {
                Box(root, new Vector3(0f, EyeY + 0.12f, FaceZ - 0.02f), new Vector3(0.62f, 0.05f, 0.08f), C(0x2B2B3A));
                Cone(root, new Vector3(0f, HeadTop + 0.15f, 0.05f), Quaternion.Euler(-20f, 0f, 0f), new Vector3(0.14f, 0.4f, 0.14f), C(0xE83A3A));
                Sphere(root, new Vector3(ShoulderX, NeckY - 0.02f, 0f), new Vector3(0.2f, 0.14f, 0.2f), C(0x9AA4B8));
                Sphere(root, new Vector3(-ShoulderX, NeckY - 0.02f, 0f), new Vector3(0.2f, 0.14f, 0.2f), C(0x9AA4B8));
            });
            Costume("body_wizard", "Wizard Rusher", CosmeticRarity.Epic, C(0x6A4CE0), root =>
            {
                Cyl(root, new Vector3(0f, HeadTop - 0.02f, 0f), new Vector3(0.95f, 0.02f, 0.95f), C(0x3B2A8A));
                Cone(root, new Vector3(0f, HeadTop, 0f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(0.6f, 0.75f, 0.6f), C(0x3B2A8A));
                Sphere(root, new Vector3(0f, ChinY - 0.04f, ChinZ), new Vector3(0.3f, 0.34f, 0.14f), C(0xFFFFFF));
            });
            Costume("body_bunny", "Bunny Rusher", CosmeticRarity.Rare, C(0xFFC2DA), root =>
            {
                BunnyEars(root);
                Sphere(root, new Vector3(0f, 0.46f, ChestBackZ + 0.04f), Vector3.one * 0.16f, C(0xFFFFFF));
            });
            Costume("body_cat", "Kitty Rusher", CosmeticRarity.Rare, C(0xFF9A3C), root =>
            {
                Cone(root, new Vector3(0.2f, HeadTop - 0.06f, 0f), Quaternion.Euler(0f, 0f, -15f), new Vector3(0.18f, 0.22f, 0.12f), C(0xFF9A3C));
                Cone(root, new Vector3(-0.2f, HeadTop - 0.06f, 0f), Quaternion.Euler(0f, 0f, 15f), new Vector3(0.18f, 0.22f, 0.12f), C(0xFF9A3C));
                Sphere(root, new Vector3(0f, 1.03f, FaceZ + 0.03f), Vector3.one * 0.06f, C(0xFF4F9A));
                for (var side = -1; side <= 1; side += 2)
                {
                    Box(root, new Vector3(side * 0.16f, 1.02f, FaceZ + 0.04f), new Vector3(0.18f, 0.01f, 0.01f), C(0x2B2B3A), Quaternion.Euler(0f, 0f, side * 8f));
                }
            });
            Costume("body_pumpkin", "Pumpkin Rusher", CosmeticRarity.Rare, C(0xFF7A1A), root =>
            {
                Cyl(root, new Vector3(0f, HeadTop + 0.06f, 0f), new Vector3(0.08f, 0.1f, 0.08f), C(0x4C8A3A));
                Sphere(root, new Vector3(0.08f, HeadTop + 0.1f, 0f), new Vector3(0.18f, 0.04f, 0.1f), C(0x4CC46A));
            }, pattern: 1, patternColor: C(0xE8601A));
            Costume("body_snowman", "Snow Rusher", CosmeticRarity.Rare, C(0xF6FAFF), root =>
            {
                Cone(root, new Vector3(0f, 1.0f, FaceZ - 0.02f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.1f, 0.24f, 0.1f), C(0xFF7A1A));
                TopHat(root, C(0x1E1E28), C(0xE83A3A));
                Torus(root, new Vector3(0f, NeckY, 0f), Quaternion.identity, new Vector3(0.42f, 0.5f, 0.4f), C(0xE83A3A));
            });
            Costume("body_superhero", "Super Rusher", CosmeticRarity.Legendary, C(0x2D6BFF), root =>
            {
                Box(root, new Vector3(0f, ChestY - 0.06f, ChestBackZ + 0.05f), new Vector3(0.46f, 0.6f, 0.03f), C(0xE83A3A), Quaternion.Euler(12f, 0f, 0f));
                Torus(root, new Vector3(0f, EyeY, 0f), Quaternion.identity, new Vector3(0.82f, 0.5f, 0.78f), C(0x1E1E28));
            }, pattern: 4, patternColor: C(0xFFD12B));
            Costume("body_viking", "Viking Rusher", CosmeticRarity.Epic, C(0xE0B080), root =>
            {
                Sphere(root, new Vector3(0f, HeadTop - 0.08f, 0f), new Vector3(0.84f, 0.36f, 0.78f), C(0x9AA4B8));
                Horns(root, C(0xFFF4E0));
                Sphere(root, new Vector3(0f, ChinY - 0.04f, ChinZ), new Vector3(0.3f, 0.28f, 0.14f), C(0xE8A23A));
            });
            Costume("body_cowboy", "Cowboy Rusher", CosmeticRarity.Common, null, root =>
            {
                Cyl(root, new Vector3(0f, HeadTop - 0.02f, 0f), new Vector3(1.0f, 0.02f, 1.0f), C(0x8A5A3B));
                Cyl(root, new Vector3(0f, HeadTop + 0.1f, 0f), new Vector3(0.42f, 0.12f, 0.42f), C(0x8A5A3B));
                Torus(root, new Vector3(0f, NeckY, 0f), Quaternion.identity, new Vector3(0.42f, 0.5f, 0.4f), C(0xE83A3A));
            });
            Costume("body_clown", "Clown Rusher", CosmeticRarity.Common, C(0xFFFFFF), root =>
            {
                Sphere(root, new Vector3(0f, 1.0f, FaceZ - 0.04f), Vector3.one * 0.14f, C(0xE83A3A));
                var wig = new[] { C(0xFF4F9A), C(0x2D9CFF), C(0xFFD12B), C(0x22C58B) };
                for (var i = 0; i < 6; i++)
                {
                    var a = i * 60f * Mathf.Deg2Rad;
                    Sphere(root, new Vector3(Mathf.Cos(a) * 0.28f, HeadTop - 0.08f, Mathf.Sin(a) * 0.26f), Vector3.one * 0.22f, wig[i % wig.Length]);
                }
            }, pattern: 2, patternColor: C(0xFF4F9A));
            Costume("body_alien", "Alien Rusher", CosmeticRarity.Epic, C(0x7FE07A), root =>
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    Cyl(root, new Vector3(side * 0.14f, HeadTop + 0.12f, 0f), new Vector3(0.03f, 0.16f, 0.03f), C(0x3BA458), Quaternion.Euler(0f, 0f, side * -15f));
                    Sphere(root, new Vector3(side * 0.22f, HeadTop + 0.3f, 0f), Vector3.one * 0.1f, C(0xFF4F9A));
                }
            });
            Costume("body_dragon", "Dragon Rusher", CosmeticRarity.Legendary, C(0xD8343A), root =>
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    Box(root, new Vector3(side * 0.24f, ChestY + 0.12f, ChestBackZ + 0.08f), new Vector3(0.42f, 0.26f, 0.03f), C(0x8A1A2A), Quaternion.Euler(10f, side * -30f, side * 25f));
                }
                Horns(root, C(0xFFD12B));
                for (var i = 0; i < 4; i++)
                {
                    Cone(root, new Vector3(0f, 0.5f + i * 0.22f, (i < 2 ? ChestBackZ : BackZ) + 0.02f), Quaternion.Euler(-80f, 0f, 0f), new Vector3(0.12f, 0.16f, 0.12f), C(0xFFD12B));
                }
            }, pattern: 7, patternColor: C(0xFFB23F));
            Costume("body_golden", "Golden Rusher", CosmeticRarity.Legendary, C(0xFFC83A), root => Crown(root), pattern: 6, patternColor: C(0xFFE08A));
        }

        // ---------------------------------------------------------------------------------------
        // Colours (15), patterns (8), hats (10), emotes (10)
        // ---------------------------------------------------------------------------------------
        private static void BuildColors()
        {
            ColorItem("default_orange", "Rush Orange", 0xFF6A1A, CosmeticRarity.Common);
            ColorItem("color_purple", "Knock Purple", 0x5B2EFF, CosmeticRarity.Common);
            ColorItem("color_mint", "Mint", 0x22C58B, CosmeticRarity.Common);
            ColorItem("color_bubblegum", "Bubblegum", 0xFF4F9A, CosmeticRarity.Common);
            ColorItem("color_sky", "Sky", 0x2D9CFF, CosmeticRarity.Common);
            ColorItem("color_sunny", "Sunny", 0xFFD12B, CosmeticRarity.Common);
            ColorItem("color_cherry", "Cherry", 0xE83A3A, CosmeticRarity.Rare);
            ColorItem("color_lime", "Lime", 0x8BE03C, CosmeticRarity.Rare);
            ColorItem("color_midnight", "Midnight", 0x232A5C, CosmeticRarity.Rare);
            ColorItem("color_coral", "Coral", 0xFF7F6A, CosmeticRarity.Common);
            ColorItem("color_lavender", "Lavender", 0xB59CFF, CosmeticRarity.Common);
            ColorItem("color_ocean", "Ocean", 0x0F7C9C, CosmeticRarity.Rare);
            ColorItem("color_cocoa", "Cocoa", 0x8A5A3B, CosmeticRarity.Common);
            ColorItem("color_snow", "Snow", 0xF2F6FF, CosmeticRarity.Rare);
            ColorItem("color_rainbow", "Rainbow Shift", 0xFF4FD8, CosmeticRarity.Legendary);
        }

        private static void BuildPatterns()
        {
            PatternItem("pattern_none", "Plain", 0, 0xFFFFFF, CosmeticRarity.Common);
            PatternItem("pattern_stripes", "Stripes", 1, 0xFFFFFF, CosmeticRarity.Common);
            PatternItem("pattern_dots", "Polka Dots", 2, 0xFFFFFF, CosmeticRarity.Common);
            PatternItem("pattern_zigzag", "Zigzag", 3, 0x2B2B3A, CosmeticRarity.Rare);
            PatternItem("pattern_stars", "Stars", 4, 0xFFD12B, CosmeticRarity.Rare);
            PatternItem("pattern_hearts", "Hearts", 5, 0xFF4F9A, CosmeticRarity.Rare);
            PatternItem("pattern_checker", "Checkers", 6, 0x2B2B3A, CosmeticRarity.Epic);
            PatternItem("pattern_flames", "Flames", 7, 0xFFB23F, CosmeticRarity.Epic);
        }

        private static void BuildHats()
        {
            Hat("hat_none", "No Hat", CosmeticRarity.Common, null);
            Hat("hat_cap", "Rush Cap", CosmeticRarity.Common, root =>
            {
                Sphere(root, new Vector3(0f, HeadTop - 0.06f, 0f), new Vector3(0.76f, 0.34f, 0.72f), C(0xE83A3A));
                Box(root, new Vector3(0f, HeadTop - 0.1f, FaceZ + 0.02f), new Vector3(0.44f, 0.03f, 0.3f), C(0xE83A3A));
            });
            Hat("hat_crown", "Tiny Crown", CosmeticRarity.Epic, Crown);
            Hat("hat_party", "Party Cone", CosmeticRarity.Common, root =>
            {
                Cone(root, new Vector3(0f, HeadTop - 0.04f, 0f), Quaternion.Euler(0f, 0f, 10f), new Vector3(0.36f, 0.5f, 0.36f), C(0xFF4F9A));
                Sphere(root, new Vector3(-0.08f, HeadTop + 0.46f, 0f), Vector3.one * 0.1f, C(0xFFD12B));
            });
            Hat("hat_tophat", "Top Hat", CosmeticRarity.Rare, root => TopHat(root, C(0x1E1E28), C(0x5B2EFF)));
            Hat("hat_propeller", "Propeller Cap", CosmeticRarity.Rare, root =>
            {
                Sphere(root, new Vector3(0f, HeadTop - 0.06f, 0f), new Vector3(0.74f, 0.32f, 0.7f), C(0x2D9CFF));
                Cyl(root, new Vector3(0f, HeadTop + 0.12f, 0f), new Vector3(0.03f, 0.08f, 0.03f), C(0x2B2B3A));
                Box(root, new Vector3(0f, HeadTop + 0.2f, 0f), new Vector3(0.6f, 0.02f, 0.08f), C(0xFFD12B), Quaternion.Euler(0f, 30f, 0f));
            });
            Hat("hat_horns", "Horns", CosmeticRarity.Rare, root => Horns(root, C(0xFFF4E0)));
            Hat("hat_halo", "Halo", CosmeticRarity.Epic, root =>
                Torus(root, new Vector3(0f, HeadTop + 0.22f, 0f), Quaternion.identity, new Vector3(0.5f, 0.6f, 0.5f), C(0xFFE04A)));
            Hat("hat_bunnyears", "Bunny Ears", CosmeticRarity.Common, BunnyEars);
            Hat("hat_headphones", "Headphones", CosmeticRarity.Rare, root =>
            {
                Torus(root, new Vector3(0f, EyeY + 0.05f, 0f), Quaternion.Euler(90f, 0f, 90f), new Vector3(0.9f, 0.9f, 0.5f), C(0x2B2B3A));
                for (var side = -1; side <= 1; side += 2)
                {
                    Cyl(root, new Vector3(side * 0.44f, EyeY - 0.05f, 0f), new Vector3(0.2f, 0.05f, 0.2f), C(0xFF4F9A), Quaternion.Euler(0f, 0f, 90f));
                }
            });
        }

        private static void BuildEmotes()
        {
            Emote("emote_celebrate", "Celebrate", "Celebrate", CosmeticRarity.Common);
            Emote("emote_victory", "Victory Dance", "VictoryDance", CosmeticRarity.Common);
            Emote("emote_wave", "Big Wave", "Wave", CosmeticRarity.Common);
            Emote("emote_bounce", "Bounce", "Bounce", CosmeticRarity.Common);
            Emote("emote_spin", "Spin", "Spin", CosmeticRarity.Rare);
            Emote("emote_clap", "Clap", "Clap", CosmeticRarity.Common);
            Emote("emote_bow", "Bow", "Bow", CosmeticRarity.Rare);
            Emote("emote_flex", "Flex", "Flex", CosmeticRarity.Rare);
            Emote("emote_laugh", "Laugh", "Laugh", CosmeticRarity.Epic);
            Emote("emote_robotdance", "Robot Dance", "RobotDance", CosmeticRarity.Legendary);
        }

        // ---------------------------------------------------------------------------------------
        // Shared accessory pieces
        // ---------------------------------------------------------------------------------------
        private static void Crown(Transform root)
        {
            Torus(root, new Vector3(0f, HeadTop + 0.02f, 0f), Quaternion.identity, new Vector3(0.5f, 1.2f, 0.5f), C(0xFFD12B));
            for (var i = 0; i < 5; i++)
            {
                var a = i * 72f * Mathf.Deg2Rad;
                Cone(root, new Vector3(Mathf.Sin(a) * 0.22f, HeadTop + 0.06f, Mathf.Cos(a) * 0.22f), Quaternion.identity, new Vector3(0.1f, 0.16f, 0.1f), C(0xFFD12B));
            }
            Sphere(root, new Vector3(0f, HeadTop + 0.04f, -0.23f), Vector3.one * 0.06f, C(0xE83A3A));
        }

        private static void TopHat(Transform root, Color hat, Color band)
        {
            Cyl(root, new Vector3(0f, HeadTop - 0.02f, 0f), new Vector3(0.62f, 0.02f, 0.62f), hat);
            Cyl(root, new Vector3(0f, HeadTop + 0.17f, 0f), new Vector3(0.38f, 0.18f, 0.38f), hat);
            Cyl(root, new Vector3(0f, HeadTop + 0.04f, 0f), new Vector3(0.39f, 0.03f, 0.39f), band);
        }

        private static void Horns(Transform root, Color color)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                Cone(root, new Vector3(side * 0.24f, HeadTop - 0.12f, 0f), Quaternion.Euler(0f, 0f, side * -40f), new Vector3(0.12f, 0.32f, 0.12f), color);
            }
        }

        private static void BunnyEars(Transform root)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                Capsule(root, new Vector3(side * 0.12f, HeadTop + 0.16f, 0f), Quaternion.Euler(0f, 0f, side * -10f), new Vector3(0.12f, 0.2f, 0.06f), C(0xFFFFFF));
                Capsule(root, new Vector3(side * 0.12f, HeadTop + 0.16f, -0.03f), Quaternion.Euler(0f, 0f, side * -10f), new Vector3(0.06f, 0.15f, 0.02f), C(0xFF9AC2));
            }
        }

        // ---------------------------------------------------------------------------------------
        // Definition helpers
        // ---------------------------------------------------------------------------------------
        private delegate void Builder(Transform root);

        // Who each character is: skin tone and hair colour (hex). Skin tones span light to dark;
        // fantasy characters get fantasy skin (robot steel, alien green, golden).
        private static readonly System.Collections.Generic.Dictionary<string, (int skin, int hair)> CharacterLooks =
            new System.Collections.Generic.Dictionary<string, (int skin, int hair)>
            {
                { "default_bean", (0xFFCFAE, 0x5A3420) },
                { "body_astronaut", (0xC68A5E, 0x1E1A1A) },
                { "body_dino", (0xF2C29A, 0x2F7D3A) },
                { "body_chef", (0xFFE0C8, 0xF2D36B) },
                { "body_pirate", (0xE8B48A, 0x241A14) },
                { "body_ninja", (0xD9A27A, 0x16161E) },
                { "body_robot", (0xB9C6D8, 0x6E7C92) },
                { "body_knight", (0xFFD9BE, 0xE0702A) },
                { "body_wizard", (0xFFE0CC, 0xF4F4FA) },
                { "body_bunny", (0xFFD8D8, 0xFF8FC2) },
                { "body_cat", (0xFFD1A8, 0xFF8A2A) },
                { "body_pumpkin", (0xE3A97C, 0x7A2E12) },
                { "body_snowman", (0xF4F8FF, 0xDDE8FF) },
                { "body_superhero", (0x7A4A2E, 0x14110F) },
                { "body_viking", (0xFFD3B6, 0xC0441C) },
                { "body_cowboy", (0xC98F63, 0x6A4424) },
                { "body_clown", (0xFFF3EC, 0xE8303A) },
                { "body_alien", (0x8FE27A, 0x2C6B2E) },
                { "body_dragon", (0xE9B78C, 0x8A1A2A) },
                { "body_golden", (0xFFD86A, 0xFFB01F) },
            };

        private static void Costume(string id, string name, CosmeticRarity rarity, Color? color, Builder build, string bone = "Chest",
            int pattern = -1, Color? patternColor = null)
        {
            var prefab = build != null ? SavePrefab(id, build) : null;
            var definition = Definition(id, name, CosmeticSlot.Body, rarity);
            definition.ConfigureLook(color ?? UnityEngine.Color.white, color.HasValue, pattern, patternColor ?? UnityEngine.Color.white, prefab, bone, null);
            var look = CharacterLooks.TryGetValue(id, out var l) ? l : (0xFFCFAE, 0x5A3420);
            definition.ConfigureCharacter(C(look.Item1), C(look.Item2));
        }

        private static void ColorItem(string id, string name, int hex, CosmeticRarity rarity)
        {
            Definition(id, name, CosmeticSlot.Color, rarity).ConfigureLook(C(hex), false, -1, UnityEngine.Color.white, null, null, null);
        }

        private static void PatternItem(string id, string name, int type, int hex, CosmeticRarity rarity)
        {
            Definition(id, name, CosmeticSlot.Pattern, rarity).ConfigureLook(UnityEngine.Color.white, false, type, C(hex), null, null, null);
        }

        private static void Hat(string id, string name, CosmeticRarity rarity, Builder build)
        {
            var prefab = build != null ? SavePrefab(id, build) : null;
            Definition(id, name, CosmeticSlot.Hat, rarity).ConfigureLook(UnityEngine.Color.white, false, -1, UnityEngine.Color.white, prefab, "Chest", null);
        }

        private static void Emote(string id, string name, string state, CosmeticRarity rarity)
        {
            Definition(id, name, CosmeticSlot.Emote, rarity).ConfigureLook(UnityEngine.Color.white, false, -1, UnityEngine.Color.white, null, null, state);
        }

        private static CosmeticDefinition Definition(string id, string name, CosmeticSlot slot, CosmeticRarity rarity)
        {
            var definition = EditorAssetUtility.LoadOrCreate($"{DefinitionFolder}/{id}.asset", ScriptableObject.CreateInstance<CosmeticDefinition>);
            definition.Configure(id, name, slot, rarity);
            EditorUtility.SetDirty(definition);
            Definitions.Add(definition);
            return definition;
        }

        private static GameObject SavePrefab(string id, Builder build)
        {
            var root = new GameObject(id);
            try
            {
                build(root.transform);
                foreach (var collider in root.GetComponentsInChildren<Collider>())
                {
                    Object.DestroyImmediate(collider);
                }

                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{id}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------------------------------------------------------------------------------
        // Geometry helpers (mesh space)
        // ---------------------------------------------------------------------------------------
        private static Color C(int hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);

        private static Material Mat(Color color)
        {
            var key = ColorUtility.ToHtmlStringRGB(color);
            if (!Materials.TryGetValue(key, out var material) || material == null)
            {
                material = EditorAssetUtility.CreateToonMaterial($"{MaterialFolder}/Cos_{key}.mat", color, false);
                material.SetFloat("_SpecularStrength", 0.3f);
                material.SetFloat("_RimStrength", 0.25f);
                Materials[key] = material;
            }

            return material;
        }

        private static GameObject Piece(Transform root, Mesh mesh, PrimitiveType? primitive, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            GameObject go;
            if (primitive.HasValue)
            {
                go = GameObject.CreatePrimitive(primitive.Value);
            }
            else
            {
                go = new GameObject("Piece", typeof(MeshFilter), typeof(MeshRenderer));
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            go.transform.SetParent(root, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        private static void Sphere(Transform r, Vector3 p, Vector3 s, Color c) => Piece(r, null, PrimitiveType.Sphere, p, Quaternion.identity, s, c);
        private static void Box(Transform r, Vector3 p, Vector3 s, Color c, Quaternion? q = null) => Piece(r, null, PrimitiveType.Cube, p, q ?? Quaternion.identity, s, c);
        private static void Cyl(Transform r, Vector3 p, Vector3 s, Color c, Quaternion? q = null) => Piece(r, null, PrimitiveType.Cylinder, p, q ?? Quaternion.identity, s, c);
        private static void Capsule(Transform r, Vector3 p, Quaternion q, Vector3 s, Color c) => Piece(r, null, PrimitiveType.Capsule, p, q, s, c);
        private static void Cone(Transform r, Vector3 p, Quaternion q, Vector3 s, Color c) => Piece(r, cone, null, p, q, s, c);
        private static void Torus(Transform r, Vector3 p, Quaternion q, Vector3 s, Color c) => Piece(r, torus, null, p, q, s, c);

        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            var path = $"{MeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }

    /// <summary>Simple procedural meshes for accessories (unit sized, base at y = 0).</summary>
    public static class ProceduralMeshes
    {
        public static Mesh Cone(float radius, float height, int segments)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            var slope = radius / height;
            for (var i = 0; i < segments; i++)
            {
                var a0 = i * Mathf.PI * 2f / segments;
                var a1 = (i + 1) * Mathf.PI * 2f / segments;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                var n0 = new Vector3(Mathf.Cos(a0), slope, Mathf.Sin(a0)).normalized;
                var n1 = new Vector3(Mathf.Cos(a1), slope, Mathf.Sin(a1)).normalized;
                var nTip = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), slope, Mathf.Sin((a0 + a1) * 0.5f)).normalized;
                var start = vertices.Count;
                vertices.AddRange(new[] { p0, new Vector3(0f, height, 0f), p1 });
                normals.AddRange(new[] { n0, nTip, n1 });
                triangles.AddRange(new[] { start, start + 1, start + 2 });

                var baseStart = vertices.Count;
                vertices.AddRange(new[] { Vector3.zero, p0, p1 });
                normals.AddRange(new[] { Vector3.down, Vector3.down, Vector3.down });
                triangles.AddRange(new[] { baseStart, baseStart + 1, baseStart + 2 });
            }

            var mesh = new Mesh { name = "Cone" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Torus(float majorRadius, float minorRadius, int segments, int sides)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i <= segments; i++)
            {
                var u = i * Mathf.PI * 2f / segments;
                var center = new Vector3(Mathf.Cos(u) * majorRadius, 0f, Mathf.Sin(u) * majorRadius);
                for (var j = 0; j <= sides; j++)
                {
                    var v = j * Mathf.PI * 2f / sides;
                    var normal = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                    vertices.Add(center + normal * minorRadius);
                    normals.Add(normal);
                }
            }

            for (var i = 0; i < segments; i++)
            {
                for (var j = 0; j < sides; j++)
                {
                    var a = i * (sides + 1) + j;
                    var b = a + sides + 1;
                    triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            }

            var mesh = new Mesh { name = "Torus" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
