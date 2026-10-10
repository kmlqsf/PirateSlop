#ifndef PIRATESLOP_WATER_SHIP_FOAM_INCLUDED
#define PIRATESLOP_WATER_SHIP_FOAM_INCLUDED
#include "Assets/Shaders/ShipWaterInterior.hlsl"
int _WaterShipCount;
TEXTURE2D(_WaterShipFoamAtlas);
SAMPLER(sampler_WaterShipFoamAtlas);
float4 _WaterShipFoamMapping;
float2 WaterShipFoamMask(float3 positionWS)
{
    if (_WaterShipCount <= 0) return 0;
    float2 uv = (positionWS.xz - _WaterShipFoamMapping.xy) * _WaterShipFoamMapping.zw;
    float2 edge = min(uv, 1 - uv);
    float fade = saturate(min(edge.x, edge.y) * 64);
    float2 foam = SAMPLE_TEXTURE2D(_WaterShipFoamAtlas, sampler_WaterShipFoamAtlas, uv).rg * fade;
    return saturate(foam);
}

float WaterBowHeight(float3 positionWS)
{
    if (_WaterShipCount <= 0) return 0;
    float2 uv = (positionWS.xz - _WaterShipFoamMapping.xy) * _WaterShipFoamMapping.zw;
    float2 edge = min(uv, 1 - uv);
    float fade = saturate(min(edge.x, edge.y) * 64);
    return SAMPLE_TEXTURE2D_LOD(_WaterShipFoamAtlas, sampler_WaterShipFoamAtlas, uv, 0).b * fade;
}
float2 WaterBowGradient(float3 positionWS)
{
    return float2(WaterBowHeight(positionWS + float3(.25, 0, 0)) - WaterBowHeight(positionWS - float3(.25, 0, 0)),
        WaterBowHeight(positionWS + float3(0, 0, .25)) - WaterBowHeight(positionWS - float3(0, 0, .25))) * 2;
}
#endif
