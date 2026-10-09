using System.IO;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class StormBillowHybridSetup
    {
        [MenuItem("PirateSlop/VFX/Storm Billows/Enable Hybrid")]
        public static void EnableHybrid()
        {
            const string boundaryPath = "Assets/Game/BRZoneVolumetric/Shaders/StormCloudBoundary.hlsl";
            var boundary = File.ReadAllText(boundaryPath);
            if (!boundary.Contains("float _PirateStormBillows;"))
                File.WriteAllText(boundaryPath, boundary.Replace("float4 _PirateStormCloudBand;", "float4 _PirateStormCloudBand;\nfloat _PirateStormBillows;"));
            const string utilitiesPath = "Assets/Game/BRZoneVolumetric/Shaders/VolumetricCloudsUtilities.hlsl";
            var utilities = File.ReadAllText(utilitiesPath);
            if (!utilities.Contains("float hybridFade="))
                utilities = utilities.Replace("    properties.body = body * stormMask;", "    float hybridFade=_PirateStormBackdrop.x<.5&&_PirateStormBillows>.5?1.0-smoothstep(220.0,420.0,distance(positionPS,GetCameraPositionWS())):1.0;\n    body*=hybridFade;\n    properties.body = body * stormMask;");
            if (!utilities.Contains("distance(positionPS, GetCameraPositionWS()) >= 420.0"))
                utilities = utilities.Replace("    ZERO_INITIALIZE(CloudProperties, properties);", "    ZERO_INITIALIZE(CloudProperties, properties);\n    if (_PirateStormBackdrop.x < .5 && _PirateStormBillows > .5 && distance(positionPS, GetCameraPositionWS()) >= 420.0) return;");
            File.WriteAllText(utilitiesPath, utilities);
            const string tracePath = "Assets/Game/BRZoneVolumetric/Shaders/VolumetricClouds.hlsl";
            var trace = File.ReadAllText(tracePath);
            if (!trace.Contains("ray.maxRayLength = min(ray.maxRayLength, 420.0);"))
                trace = trace.Replace("    ray.integrationNoise = GenerateRandomFloat(screenUV);", "    if (_PirateStormBackdrop.x < .5 && _PirateStormBillows > .5) ray.maxRayLength = min(ray.maxRayLength, 420.0);\n    ray.integrationNoise = GenerateRandomFloat(screenUV);");
            File.WriteAllText(tracePath, trace);
            AssetDatabase.ImportAsset(boundaryPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(utilitiesPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(tracePath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("STORM_BILLOW_HYBRID_ENABLED");
        }
    }
}