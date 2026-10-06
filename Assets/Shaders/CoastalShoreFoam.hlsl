#ifndef COASTAL_SHORE_FOAM_INCLUDED
#define COASTAL_SHORE_FOAM_INCLUDED
TEXTURE2D(_CoastalShoreMap);
SAMPLER(sampler_CoastalShoreMap);
float4 _CoastalShoreRect;
float _CoastalShoreActive;
float2 CoastalShoreMask(float3 positionWS)
{
    if (_CoastalShoreActive < .5) return 0;
    float2 uv = (positionWS.xz - _CoastalShoreRect.xy) * _CoastalShoreRect.zw;
    float edge = min(min(uv.x, uv.y), min(1 - uv.x, 1 - uv.y));
    return SAMPLE_TEXTURE2D(_CoastalShoreMap, sampler_CoastalShoreMap, uv).rg * saturate(edge * 40);
}
#endif
