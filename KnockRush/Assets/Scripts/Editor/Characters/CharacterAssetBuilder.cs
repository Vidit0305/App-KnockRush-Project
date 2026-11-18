using System.Collections.Generic;
using System.Linq;
using KnockRush.CameraRig;
using KnockRush.Core;
using KnockRush.EditorTools.Common;
using KnockRush.Movement;
using KnockRush.Player;
using KnockRush.Player.Controls;
using KnockRush.Player.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KnockRush.EditorTools.Characters
{
    /// <summary>
    /// Builds the Phase 2 character content from the generated Bean.fbx:
    /// movement/camera settings, toon material, animator controller and the PlayerCharacter prefab.
    /// Idempotent: existing assets are updated in place (GUIDs stay stable).
    /// </summary>
    public static class CharacterAssetBuilder
    {
        public const string BeanModelPath = "Assets/Art/Characters/Bean/Bean.fbx";
        public const string BeanMaterialPath = "Assets/Art/Characters/Bean/Bean_Toon.mat";
        public const string AnimatorPath = "Assets/Animations/Characters/Bean.controller";
        public const string MovementSettingsPath = "Assets/ScriptableObjects/Movement/DefaultMovementSettings.asset";
        public const string CameraSettingsPath = "Assets/ScriptableObjects/Camera/DefaultCameraSettings.asset";
        public const string PlayerPrefabPath = "Assets/Prefabs/Characters/PlayerCharacter.prefab";

        private static readonly string[] RequiredClips =
        {
            "Idle", "Walk", "Run", "Jump", "Fall", "Land", "Dive", "Hit", "Stumble", "Grab",
            "Eliminated", "Celebrate", "VictoryDance",
            "Wave", "Bounce", "Spin", "Clap", "Bow", "Flex", "Laugh", "RobotDance"
        };

        public static MovementSettings EnsureMovementSettings()
        {
            var settings = EditorAssetUtility.LoadOrCreate(MovementSettingsPath, ScriptableObject.CreateInstance<MovementSettings>);
            settings.SetCollisionMask(GameLayers.EnvironmentMask);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        public static CameraSettings EnsureCameraSettings()
        {
            return EditorAssetUtility.LoadOrCreate(CameraSettingsPath, ScriptableObject.CreateInstance<CameraSettings>);
        }

        public static Dictionary<string, AnimationClip> LoadClips()
        {
            AssetDatabase.ImportAsset(BeanModelPath, ImportAssetOptions.ForceUpdate);
            var clips = AssetDatabase.LoadAllAssetsAtPath(BeanModelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .ToDictionary(clip => clip.name);

            var missing = RequiredClips.Where(name => !clips.ContainsKey(name)).ToArray();
            if (missing.Length > 0)
            {
                throw new System.InvalidOperationException(
                    $"Bean.fbx is missing clips: {string.Join(", ", missing)}. Found: {string.Join(", ", clips.Keys)}");
            }

            return clips;
        }

        public static AnimatorController BuildAnimatorController()
        {
            var clips = LoadClips();
            KnockRushProjectSetupFacade.EnsureFolder("Assets/Animations/Characters");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath);
            if (controller != null)
            {
                AssetDatabase.DeleteAsset(AnimatorPath);
            }

            controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var locomotionState = controller.CreateBlendTreeInController(CharacterAnimationDriver.Locomotion, out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle"], 0f);
            tree.AddChild(clips["Walk"], 0.35f);
            tree.AddChild(clips["Run"], 1f);

            var stateMachine = controller.layers[0].stateMachine;
            stateMachine.defaultState = locomotionState;

            var position = new Vector3(300f, 0f, 0f);
            foreach (var name in new[]
                     {
                         CharacterAnimationDriver.Jump, CharacterAnimationDriver.Fall, CharacterAnimationDriver.Land,
                         CharacterAnimationDriver.Dive, CharacterAnimationDriver.Hit, CharacterAnimationDriver.Stumble,
                         CharacterAnimationDriver.Grab, CharacterAnimationDriver.Eliminated,
                         CharacterAnimationDriver.Celebrate, CharacterAnimationDriver.VictoryDance,
                         "Wave", "Bounce", "Spin", "Clap", "Bow", "Flex", "Laugh", "RobotDance"
                     })
            {
                var state = stateMachine.AddState(name, position);
                state.motion = clips[name];
                state.writeDefaultValues = true;
                position += new Vector3(0f, 60f, 0f);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[KnockRush Build] Animator controller built with {clips.Count} clips: {AnimatorPath}");
            return controller;
        }

        public static GameObject BuildPlayerPrefab(MovementSettings movementSettings, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BeanModelPath);
            var material = EditorAssetUtility.CreateToonMaterial(BeanMaterialPath, new Color32(0xFF, 0x6A, 0x1A, 0xFF), true);
            KnockRushProjectSetupFacade.EnsureFolder("Assets/Prefabs/Characters");

            var root = new GameObject("PlayerCharacter");
            try
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = movementSettings.Radius;
                capsule.height = movementSettings.Height;
                capsule.center = new Vector3(0f, movementSettings.Height * 0.5f, 0f);

                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;

                var character = root.AddComponent<PlayerCharacter>();
                var reader = root.AddComponent<PlayerInputReader>();

                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);

                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                modelInstance.name = "Bean";
                modelInstance.transform.SetParent(visual, false);

                // Blender's front (-Y) imports as Unity -Z; turn the model to face +Z (transform.forward).
                modelInstance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var renderer = modelInstance.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.updateWhenOffscreen = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                var animator = modelInstance.GetComponent<Animator>() ?? modelInstance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var driver = visual.gameObject.AddComponent<CharacterAnimationDriver>();
                var appearance = visual.gameObject.AddComponent<BeanAppearance>();

                EditorAssetUtility.SetField(character, "settings", movementSettings);
                EditorAssetUtility.SetField(character, "visualRoot", visual);
                EditorAssetUtility.SetField(character, "inputReader", reader);
                EditorAssetUtility.SetField(driver, "animator", animator);
                EditorAssetUtility.SetField(appearance, "bodyRenderer", renderer);
                var cosmetics = visual.gameObject.AddComponent<KnockRush.Inventory.CosmeticApplier>();
                EditorAssetUtility.SetField(cosmetics, "bodyRenderer", renderer);
                EditorAssetUtility.SetField(cosmetics, "animationDriver", driver);

                EditorAssetUtility.SetLayerRecursively(root, GameLayers.Player);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"[KnockRush Build] Player prefab saved: {PlayerPrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    /// <summary>Thin indirection so builders don't depend on setup internals beyond folder creation.</summary>
    internal static class KnockRushProjectSetupFacade
    {
        public static void EnsureFolder(string path)
        {
            Setup.KnockRushProjectSetup.EnsureFolder(path);
        }
    }
}
