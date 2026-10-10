#ifndef PIRATESLOP_SHIP_WATER_INTERIOR_INCLUDED
#define PIRATESLOP_SHIP_WATER_INTERIOR_INCLUDED
int _ShipWaterInteriorCount;
int _ShipWaterInteriorCameraInside;
float4x4 _ShipWaterInteriorMatrices[16];
float4 _ShipWaterInteriorMapping;
float4 _ShipWaterInteriorSize;
TEXTURE2D(_ShipWaterInteriorProfile);
SAMPLER(sampler_ShipWaterInteriorProfile);

void ClipShipWaterInterior(float3 positionWS)
{
    [loop]
    for (int i = 0; i < min(_ShipWaterInteriorCount, 16); i++)
    {
        float3 local = mul(_ShipWaterInteriorMatrices[i], float4(positionWS, 1)).xyz;
        float2 uv = (local.zy - _ShipWaterInteriorMapping.xy) * _ShipWaterInteriorMapping.zw;
        if (any(uv < 0) || any(uv > 1) || abs(local.x) > 7) continue;
        uv = (uv * (_ShipWaterInteriorSize.zw - 1) + .5) * _ShipWaterInteriorSize.xy;
        float width = SAMPLE_TEXTURE2D_LOD(_ShipWaterInteriorProfile, sampler_ShipWaterInteriorProfile, uv, 0).r;
        if (width > 0 && abs(local.x) <= width) clip(-1);
    }
}
#endif
