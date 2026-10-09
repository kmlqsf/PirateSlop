using System.IO;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class StormBillowSetup
    {
        [MenuItem("PirateSlop/VFX/Storm Billows/Prepare")]
        public static void Prepare()
        {
            const string folder = "Assets/Game/BRZoneVolumetric/Resources";
            const string texturePath = folder + "/StormBillowsAtlas.png";
            File.Copy("Art/Source/StormClouds/Generated/StormCumulusAtlas.png", texturePath, true);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            const string materialPath = folder + "/StormBillows.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/BRZoneVolumetric/Shaders/StormBillows.shader");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            material.shader = shader;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            EditorUtility.SetDirty(material);
            var prefab = Resources.Load<GameObject>("BRStormVolume");
            var path = AssetDatabase.GetAssetPath(prefab);
            var content = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var billows = content.GetComponent<StormBillowController>();
                if (billows == null) billows = content.AddComponent<StormBillowController>();
                billows.Material = material;
                PrefabUtility.SaveAsPrefabAsset(content, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(content); }
            AssetDatabase.SaveAssets();
            Debug.Log("STORM_BILLOWS_PREPARED shaderMessages=" + ShaderUtil.GetShaderMessages(shader).Length);
        }
    }
}