#ifndef PIRATESLOP_FROZEN_WATER_INCLUDED
#define PIRATESLOP_FROZEN_WATER_INCLUDED
TEXTURE2D(_FrozenWaterMask);
SAMPLER(sampler_FrozenWaterMask);
float4 _FrozenWaterMapping;
float3 FrozenWaterSample(float2 position)
{
    float2 uv = (position - _FrozenWaterMapping.xy) * _FrozenWaterMapping.zw;
    if (_FrozenWaterMapping.z <= 0 || any(uv < 0) || any(uv > 1)) return 0;
    return SAMPLE_TEXTURE2D_LOD(_FrozenWaterMask, sampler_FrozenWaterMask, uv, 0).rgb;
}
float FrozenWaterHeight(float3 position)
{
    float3 ice = FrozenWaterSample(position.xz);
    return lerp(position.y, ice.r, ice.g);
}
#endif
