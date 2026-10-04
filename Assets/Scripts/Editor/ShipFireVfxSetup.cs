using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Editor
{
    public static class ShipFireVfxSetup
    {
        const string Folder = "Assets/Resources/VFX";
        const string TextureFolder = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/";
        const string PrefabPath = Folder + "/ShipFireVfx.prefab";

        [MenuItem("PirateSlop/VFX/Configure Ship Fire")]
        public static void Configure()
        {
            var shader = Shader.Find("PirateSlop/ShipFire");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("The PirateSlop/ShipFire URP shader is unavailable.");
            EnsureFolder(Folder);
            var flame = MakeMaterial("ShipFireFlame", "cfxr fire small anim blurred.png", shader, 1.7f, .08f, false);
            var smoke = MakeMaterial("ShipFireSmoke", "cfxr smoke cloud x4 blurred.png", shader, 1f, .18f, true);
            var ember = MakeMaterial("ShipFireEmber", "cfxr ember blur.png", shader, 1.5f, .045f, false);
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = existing ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("ShipFireVfx");
            try
            {
                var effect = root.GetComponent<ShipFireVfx>();
                if (effect == null) effect = root.AddComponent<ShipFireVfx>();
                var serialized = new SerializedObject(effect);
                serialized.FindProperty("flameMaterial").objectReferenceValue = flame;
                serialized.FindProperty("smokeMaterial").objectReferenceValue = smoke;
                serialized.FindProperty("emberMaterial").objectReferenceValue = ember;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Ship fire VFX resources saved with fire 3x3, smoke 2x2 and ember textures.");
        }

        static Material MakeMaterial(string name, string textureName, Shader shader, float intensity, float softness, bool alphaMask)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + textureName);
            if (texture == null) throw new InvalidOperationException("Missing ship fire texture: " + textureName);
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_AlphaMask", alphaMask ? 1f : 0f);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_Fade", 1f);
            material.SetFloat("_SoftDistance", softness);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string child = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = child;
            }
        }
    }
}
