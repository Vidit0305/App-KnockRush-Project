using System.Collections.Generic;
using KnockRush.Player.Presentation;
using UnityEngine;

namespace KnockRush.Inventory
{
    /// <summary>
    /// Dresses a bean: body colour + pattern on the toon material, accessory meshes attached to
    /// skeleton bones, and the equipped emote. Accessory prefabs are authored in the bean's mesh
    /// space; each is placed on its bone with the bone's bind pose, so it follows animation exactly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CosmeticApplier : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int PatternTypeId = Shader.PropertyToID("_PatternType");
        private static readonly int PatternColorId = Shader.PropertyToID("_PatternColor");

        [SerializeField] private SkinnedMeshRenderer bodyRenderer;
        [SerializeField] private CharacterAnimationDriver animationDriver;

        private readonly List<GameObject> spawnedAccessories = new List<GameObject>();
        private Material material;

        public Loadout Current { get; private set; }

        public bool HasApplied { get; private set; }

        public IReadOnlyList<GameObject> SpawnedAccessories => spawnedAccessories;

        public void Apply(Loadout loadout)
        {
            var catalog = CosmeticCatalog.Load();
            if (catalog == null || bodyRenderer == null)
            {
                return;
            }

            Current = loadout;
            HasApplied = true;

            var body = catalog.Get(loadout.Body);
            var color = catalog.Get(loadout.Color);
            var pattern = catalog.Get(loadout.Pattern);
            var hat = catalog.Get(loadout.Hat);
            var emote = catalog.Get(loadout.Emote);

            // Colour: costume override > chosen colour.
            var bodyColor = body != null && body.OverridesBodyColor ? body.Color : color != null ? color.Color : Color.white;
            var patternType = pattern != null && pattern.PatternType > 0 ? pattern.PatternType : body != null && body.PatternType > 0 ? body.PatternType : 0;
            var patternColor = pattern != null && pattern.PatternType > 0 ? pattern.PatternColor : body != null ? body.PatternColor : Color.white;

            if (material == null && Application.isPlaying)
            {
                material = bodyRenderer.material; // per-character instance (SRP Batcher friendly)
            }

            if (material != null)
            {
                material.SetColor(BaseColorId, bodyColor);
                material.SetFloat(PatternTypeId, patternType);
                material.SetColor(PatternColorId, patternColor);
            }
            else
            {
                // Edit mode (previews, captures): never modify the shared material asset.
                var block = new MaterialPropertyBlock();
                bodyRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, bodyColor);
                block.SetFloat(PatternTypeId, patternType);
                block.SetColor(PatternColorId, patternColor);
                bodyRenderer.SetPropertyBlock(block);
            }

            ClearAccessories();
            Attach(body);
            Attach(hat);

            if (animationDriver != null && emote != null && !string.IsNullOrEmpty(emote.EmoteState))
            {
                animationDriver.EquippedEmoteState = emote.EmoteState;
            }
        }

        private void Attach(CosmeticDefinition definition)
        {
            if (definition == null || definition.AccessoryPrefab == null)
            {
                return;
            }

            var bones = bodyRenderer.bones;
            var bindposes = bodyRenderer.sharedMesh.bindposes;
            for (var i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null || bones[i].name != definition.AttachBone)
                {
                    continue;
                }

                // Accessories are authored in the model root's space (+Y up, face −Z, feet at 0).
                // root → bone (at bind) = bindpose (mesh → bone) × (mesh → root)⁻¹.
                var modelRoot = bodyRenderer.GetComponentInParent<Animator>() != null
                    ? bodyRenderer.GetComponentInParent<Animator>().transform
                    : bodyRenderer.transform.parent;
                var meshToRoot = modelRoot.worldToLocalMatrix * bodyRenderer.transform.localToWorldMatrix;
                var rootToBone = bindposes[i] * meshToRoot.inverse;

                var instance = Instantiate(definition.AccessoryPrefab, bones[i], false);
                instance.transform.localPosition = rootToBone.GetColumn(3);
                instance.transform.localRotation = rootToBone.rotation;
                instance.transform.localScale = rootToBone.lossyScale;
                instance.layer = gameObject.layer;
                foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = gameObject.layer;
                }

                spawnedAccessories.Add(instance);
                return;
            }

            Debug.LogWarning($"[Cosmetics] Bone '{definition.AttachBone}' not found for {definition.ItemId}.");
        }

        private void ClearAccessories()
        {
            foreach (var accessory in spawnedAccessories)
            {
                if (accessory != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(accessory);
                    }
                    else
                    {
                        DestroyImmediate(accessory);
                    }
                }
            }

            spawnedAccessories.Clear();
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
