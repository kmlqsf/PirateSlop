using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
namespace PirateSlop.EditorTools
{
    public static class StormDenseBankSetup
    {
        [MenuItem("PirateSlop/VFX/Storm Volume/Seal Dense Bank")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop QA before applying");
            const string annulus="Assets/Game/BRZoneVolumetric/Shaders/StormAnnulus.hlsl";
            var text=File.ReadAllText(annulus);
            const string token="    float distanceToEye=distance(p,GetCameraPositionWS());";
            var start=text.IndexOf("    float lowerTheta=",StringComparison.Ordinal);
            if(start<0) start=text.IndexOf("    float3 coreUV=",StringComparison.Ordinal);
            var end=start<0?-1:text.IndexOf(token,start,StringComparison.Ordinal);
            if(start<0||end<start)throw new InvalidOperationException("Expected storm field not found");
            text=text.Substring(0,start)+@"    float3 coreUV=(p+float3(_StormShape.z*.6,0,0))/1024.0;
    float4 coreNoise=SAMPLE_TEXTURE3D_LOD(_PirateStormNearNoise,sampler_PirateStormNearNoise,coreUV,1);
    float rise=smoothstep(0.0,60.0,height);
    float width=_StormBand.y+_StormBand.z;
    float inward=_StormBand.z-width+(coreNoise.r-.5)*lerp(1.0,5.0,rise);
    float innerFade=smoothstep(inward,inward+lerp(.7,12.0,rise),sd);
    float outerFade=1-smoothstep(_StormBand.z-1.5,_StormBand.z,sd);
    float crown=scale*(550.0+coreNoise.r*90.0);
    float core=innerFade*outerFade*(1-smoothstep(crown-35.0,crown,height))*(.82+.18*coreNoise.r)*lerp(1.0,10.0,smoothstep(20.0,100.0,height))*smoothstep(-2.0,0.0,height);
    if(core>density)
    {
        density=core;
        float3 radial=normalize(float3(local.x,0,local.z)+.00001);
        float3 curl=coreNoise.gba*2.0-1.0;
        normalWS=normalize(-radial+curl*.45+float3(0,.25,0));
    }
"+text.Substring(end);
            text=text.Replace("return float4(saturate(density),normalWS);","return float4(density,normalWS);");
            File.WriteAllText(annulus,text);
            const string trace="Assets/Game/BRZoneVolumetric/Shaders/VolumetricClouds.hlsl";
            text=File.ReadAllText(trace);
            text=Regex.Replace(text,@"ray.integrationNoise = [^;]+;","ray.integrationNoise = _PirateStormVolume3D>.5 && _PirateStormBackdrop.x<.5 ? lerp(.35,.65,GenerateRandomFloat(screenUV)) : GenerateRandomFloat(screenUV);");
            File.WriteAllText(trace,text);
            AssetDatabase.ImportAsset(annulus,ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(trace,ImportAssetOptions.ForceUpdate);
            Debug.Log("Dense bank saved: continuous 3D base, connected cloud groups, stable integration");
        }
    }
}
