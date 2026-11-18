using Fusion;
using KnockRush.Core;
using KnockRush.EditorTools.Characters;
using KnockRush.EditorTools.Common;
using KnockRush.EditorTools.Setup;
using KnockRush.Movement;
using KnockRush.Multiplayer;
using KnockRush.Player.Controls;
using KnockRush.Player.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KnockRush.EditorTools.Multiplayer
{
    /// <summary>Builds the networked player prefab and the NetworkSettings asset.</summary>
    public static class NetworkContentBuilder
    {
        public const string NetworkPlayerPath = "Assets/Prefabs/Characters/NetworkPlayer.prefab";
        public const string NetworkSettingsPath = "Assets/ScriptableObjects/Config/Resources/NetworkSettings.asset";

        public static void Build(MovementSettings movementSettings)
        {
            // Reload: scene operations in the content build unload unused assets, which leaves
            // references held across them dangling (they then serialize as null).
            movementSettings = AssetDatabase.LoadAssetAtPath<MovementSettings>(CharacterAssetBuilder.MovementSettingsPath);
            var prefab = BuildNetworkPlayer(movementSettings);
            var settings = EditorAssetUtility.LoadOrCreate(NetworkSettingsPath, ScriptableObject.CreateInstance<NetworkSettings>);
            settings.Configure(prefab.GetComponent<NetworkObject>());
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildNetworkPlayer(MovementSettings movementSettings)
        {
            KnockRushProjectSetup.EnsureFolder("Assets/Prefabs/Characters");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetBuilder.BeanModelPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetBuilder.AnimatorPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(CharacterAssetBuilder.BeanMaterialPath);

            var root = new GameObject("NetworkPlayer");
            try
            {
                root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkTransform>();

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = movementSettings.Radius;
                capsule.height = movementSettings.Height;
                capsule.center = new Vector3(0f, movementSettings.Height * 0.5f, 0f);

                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                var reader = root.AddComponent<PlayerInputReader>();
                reader.enabled = false; // enabled only on the owning client
                var character = root.AddComponent<NetworkCharacter>();
                EditorAssetUtility.SetField(character, "settings", movementSettings);
                EditorAssetUtility.SetField(character, "inputReader", reader);

                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                var bean = (GameObject)PrefabUtility.InstantiatePrefab(model);
                bean.name = "Bean";
                bean.transform.SetParent(visual, false);
                bean.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var renderer = bean.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.sharedMaterial = material;
                var animator = bean.GetComponent<Animator>() ?? bean.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var driver = visual.gameObject.AddComponent<CharacterAnimationDriver>();
                EditorAssetUtility.SetField(driver, "animator", animator);
                var appearance = visual.gameObject.AddComponent<BeanAppearance>();
                EditorAssetUtility.SetField(appearance, "bodyRenderer", renderer);

                EditorAssetUtility.SetLayerRecursively(root, GameLayers.Player);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, NetworkPlayerPath);
                Debug.Log($"[KnockRush Build] Network player prefab saved: {NetworkPlayerPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
