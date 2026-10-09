using System.IO;
using UnityEditor;
using UnityEngine;
namespace PirateSlop.EditorTools
{
    public static class StormBillowLightingSetup
    {
        [MenuItem("PirateSlop/VFX/Storm Billows/Prepare Lighting")]
        public static void PrepareLighting()
        {
            const string skyPath = "Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsUtilities.hlsl";
            var sky = File.ReadAllText(skyPath);
            sky = sky.Replace("smoothstep(150.0, 1000.0, outsideDistance)", "smoothstep(3000.0, 6000.0, outsideDistance)");
            if (!sky.Contains("half stormShading;")) sky = sky.Replace("    half sigmaT;", "    half sigmaT;\n    half stormShading;");
            if (!sky.Contains("properties.stormShading"))
                sky = sky.Replace("    half shapeThreshold =", "    properties.stormShading = _PirateStormBackdrop.x < .5 ? cloudCoverageData.rainClouds : 0.0;\n    half shapeThreshold =");
            if (!sky.Contains("sunLuminance *= lerp"))
                sky = sky.Replace("    half ambientLuminance = 1.0 * cloudProperties.ambientOcclusion;", "    half ambientLuminance = 1.0 * cloudProperties.ambientOcclusion;\n    sunLuminance *= lerp(half3(1,1,1),half3(.24,.34,.48),cloudProperties.stormShading);\n    ambientLuminance *= lerp(1.0,.20,cloudProperties.stormShading);");
            File.WriteAllText(skyPath, sky);
            AssetDatabase.ImportAsset(skyPath, ImportAssetOptions.ForceSynchronousImport);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/BRZoneVolumetric/Resources/StormBillows.mat");
            material.SetColor("_ShadowColor", new Color(.055f,.075f,.10f));
            material.SetColor("_LightColor", new Color(.55f,.66f,.78f));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
    }
}