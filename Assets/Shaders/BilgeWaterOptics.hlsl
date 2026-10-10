#ifndef PIRATESLOP_BILGE_WATER_OPTICS
#define PIRATESLOP_BILGE_WATER_OPTICS

#define _REFLECTION_CUBEMAP 1
#include "Packages/com.unity.urp-water-system/Runtime/Shaders/WaterCommon.hlsl"

float2 BilgeDetail(float2 uv, float time)
{
    float2 a = SAMPLE_TEXTURE2D(_SurfaceNormals, sampler_SurfaceNormals, uv + float2(time * .07, -time * .13)).xy * 2 - 1;
    float2 b = SAMPLE_TEXTURE2D(_SurfaceNormals, sampler_SurfaceNormals, uv * .61 + float2(-time * .05, time * .09)).zw * 2 - 1;
    return (a + b) * .5;
}

float BilgeFoam(float2 uv, float time)
{
    float3 foam = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, uv + float2(time * .05, -time * .16)).rgb;
    return dot(foam, float3(.5, .35, .15));
}

half3 BilgeShading(float3 positionWS, float3 normalWS, float2 screenUV, float thickness, float foam, bool underwater, float density, float minimumDepth)
{
    WaterInputData input = (WaterInputData)0;
    input.positionWS = positionWS;
    input.viewDirectionWS = SafeNormalize(GetCameraPositionWS() - positionWS);
    input.normalWS = dot(normalWS, input.viewDirectionWS) < 0 ? -normalWS : normalWS;
    float sceneDepth = max(0, AdjustedDepth(screenUV, positionWS).x);
    float waterDepth = min(sceneDepth, thickness);
    input.depth = float2(max(waterDepth, minimumDepth) * density, sceneDepth);
    input.refractionUV = DistortionUVs(screenUV, input.depth.x, input.normalWS, input.viewDirectionWS, positionWS);
    if (AdjustedDepth(input.refractionUV.xy, positionWS).x < 0) input.refractionUV = screenUV.xyxy;
    input.fogCoord = ComputeFogFactor(TransformWorldToHClip(positionWS).z);
    WaterSurfaceData surface = (WaterSurfaceData)0;
    surface.foamMask = saturate(foam);
    surface.foam = 1;
    return WaterShading(input, surface, float4(0, 0, 1, 0), screenUV, underwater);
}

#endif
