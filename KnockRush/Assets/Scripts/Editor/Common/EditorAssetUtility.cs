using System;
using KnockRush.EditorTools.Setup;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KnockRush.EditorTools.Common
{
    /// <summary>Small helpers shared by the content builders.</summary>
    public static class EditorAssetUtility
    {
        /// <summary>Loads the asset at <paramref name="path"/> or creates it with <paramref name="create"/>.</summary>
        public static T LoadOrCreate<T>(string path, Func<T> create) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            KnockRushProjectSetup.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var asset = create();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[KnockRush Build] Created {path}");
            return asset;
        }

        /// <summary>Sets a (private) serialized field by name. Supports object references, numbers, bools, strings, colours, vectors and layer masks.</summary>
        public static void SetField(Object target, string fieldName, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                throw new ArgumentException($"Field '{fieldName}' not found on {target.GetType().Name}.");
            }

            switch (value)
            {
                case Object reference:
                    property.objectReferenceValue = reference;
                    break;
                case null:
                    property.objectReferenceValue = null;
                    break;
                case bool flag:
                    property.boolValue = flag;
                    break;
                case int integer when property.propertyType == SerializedPropertyType.LayerMask:
                    property.intValue = integer;
                    break;
                case int integer:
                    property.intValue = integer;
                    break;
                case float number:
                    property.floatValue = number;
                    break;
                case string text:
                    property.stringValue = text;
                    break;
                case Color color:
                    property.colorValue = color;
                    break;
                case Vector2 vector2:
                    property.vector2Value = vector2;
                    break;
                case Vector3 vector3:
                    property.vector3Value = vector3;
                    break;
                default:
                    throw new ArgumentException($"Unsupported value type {value.GetType().Name} for '{fieldName}'.");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        public static Material CreateToonMaterial(string path, Color color, bool useVertexColor)
        {
            var shader = Shader.Find("KnockRush/Toon");
            if (shader == null)
            {
                throw new InvalidOperationException("Shader 'KnockRush/Toon' not found (Assets/Art/Shaders/KnockRushToon.shader).");
            }

            var material = LoadOrCreate(path, () => new Material(shader));
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_UseVertexColor", useVertexColor ? 1f : 0f);

            // Characters get the glossy cartoon highlight; level geometry stays matte.
            material.SetFloat("_SpecularStrength", useVertexColor ? 0.35f : 0f);
            material.SetFloat("_RimStrength", useVertexColor ? 0.3f : 0.12f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
