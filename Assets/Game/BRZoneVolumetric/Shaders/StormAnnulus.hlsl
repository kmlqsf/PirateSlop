#ifndef PIRATESLOP_STORM_ANNULUS
#define PIRATESLOP_STORM_ANNULUS
float4 _StormCenterWater;
float4 _StormBand;
float4 _StormShape;
float4 _StormStyle;
float StormInward(float h)
{
    float offset;
    if (h < 1.0 / 6.0) offset = lerp(0.0, 0.05, h * 6.0);
    else if (h < 1.0 / 3.0) offset = lerp(0.05, 0.15, (h - 1.0 / 6.0) * 6.0);
    else if (h < 5.0 / 9.0) offset = lerp(0.15, 0.35, (h - 1.0 / 3.0) * 4.5);
    else if (h < 7.0 / 9.0) offset = lerp(0.35, 0.65, (h - 5.0 / 9.0) * 4.5);
    else offset = lerp(0.65, 1.0, (h - 7.0 / 9.0) * 4.5);
    return saturate(offset) * _StormShape.y;
}
float2 StormRotate(float2 p, float angle)
{
    float s, c;
    sincos(angle, s, c);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}
float StormDensityMask(float3 p, float mip, bool lightSampling, out float macro, out float billow, out float3 flow)
{
    macro = 0.0;
    billow = 0.0;
    flow = 0.0;
    float h = (p.y - _StormCenterWater.y) / max(1.0, _StormShape.x);
    if (h <= 0.0 || h >= 1.0 || _StormShape.w < 0.5) return 0.0;
    float radius = _StormBand.x - StormInward(h);
    float d = length(p.xz - _StormCenterWater.xz);
    if (d <= radius - _StormBand.y || d >= radius + _StormBand.z) return 0.0;

    float3 local = p - _StormCenterWater.xyz;
    float t = _StormShape.z;
    float scale = max(0.00001, _StormStyle.z);
    float3 rolling = local;
    rolling.xz = StormRotate(local.xz, -_StormStyle.w * 0.00042);
    float3 q = rolling * scale * float3(1.0, 1.18, 1.0);
    float3 phase = q.zxy * 2.7 + q.yzx * 1.1;
    float3 primaryCurl = sin(phase + float3(t * 0.137, t * 0.173 + 2.1, -t * 0.119 + 4.7));
    float3 primaryCoords = q * 0.52 + primaryCurl * 0.075;
    primaryCoords.y -= t * 0.0031;
    float primaryA = SAMPLE_TEXTURE3D_LOD(_Worley128RGBA, s_trilinear_repeat_sampler, primaryCoords, mip).r;
    float3 overlapCoords = q * 0.61 + float3(0.37, 0.63, 0.19) - primaryCurl.yzx * 0.055;
    overlapCoords.y += t * 0.0023;
    float primaryB = SAMPLE_TEXTURE3D_LOD(_Worley128RGBA, s_trilinear_repeat_sampler, overlapCoords, mip).r;
    float unionBlend = saturate(0.5 + 0.5 * (primaryA - primaryB) / 0.12);
    macro = lerp(primaryB, primaryA, unionBlend) + 0.12 * unionBlend * (1.0 - unionBlend);
    macro = smoothstep(0.58, 0.96, macro);

    billow = 0.65;
    if (!lightSampling)
    {
        float3 secondary = local;
        secondary.xz = StormRotate(local.xz, _StormStyle.w * 0.00027 + 0.71);
        float3 secondaryQ = secondary * scale;
        float3 secondaryCurl = sin(secondaryQ.yzx * 3.9 + float3(-t * 0.487 + 1.7, t * 0.373 + 3.2, t * 0.563));
        flow = secondaryQ * 2.1 + secondaryCurl * 0.10 + primaryCurl * 0.06;
        flow.y -= t * 0.011;
        billow = SAMPLE_TEXTURE3D_LOD(_Worley128RGBA, s_trilinear_repeat_sampler, flow + float3(0.19, 0.47, 0.83), mip).r;
        billow = smoothstep(0.46, 0.94, billow);
    }

    float softness = max(0.1, min(_StormBand.w, (_StormBand.y + _StormBand.z) * 0.30));
    float lobes = saturate(macro * 0.68 + billow * 0.32);
    float carve = (1.0 - lobes) * 0.90;
    float innerEdge = radius - _StormBand.y + _StormBand.y * carve;
    float outerEdge = radius + _StormBand.z - _StormBand.z * carve;
    float inner = smoothstep(innerEdge, innerEdge + softness, d);
    float outer = 1.0 - smoothstep(outerEdge - softness, outerEdge, d);
    float crownHeight = 0.66 + 0.33 * lobes;
    float crown = 1.0 - smoothstep(crownHeight - 0.085, crownHeight, h);
    return inner * outer * smoothstep(0.0, 0.018, h) * crown;
}
bool StormCylinder(float2 p, float2 d, float radius, out float2 span)
{
    float a = dot(d, d);
    float c = dot(p, p) - radius * radius;
    span = float2(-1e19, 1e19);
    if (a < 1e-8) return c <= 0.0;
    float b = dot(p, d);
    float discriminant = b * b - a * c;
    if (discriminant < 0.0) return false;
    float root = sqrt(max(0.0, discriminant));
    span = float2(-b - root, -b + root) / a;
    return true;
}
bool StormRayIntervals(float3 origin, float3 direction, float maxDistance, out float4 spans)
{
    spans = 0.0;
    if (_StormShape.w < 0.5) return false;
    float2 radial;
    float2 p = origin.xz - _StormCenterWater.xz;
    if (!StormCylinder(p, direction.xz, _StormBand.x + _StormBand.z + 2.0, radial)) return false;
    float bottom = _StormCenterWater.y;
    float top = bottom + _StormShape.x;
    float2 vertical = float2(-1e19, 1e19);
    if (abs(direction.y) < 1e-6)
    {
        if (origin.y < bottom || origin.y > top) return false;
    }
    else
    {
        vertical = (float2(bottom, top) - origin.y) / direction.y;
        vertical = float2(min(vertical.x, vertical.y), max(vertical.x, vertical.y));
    }
    float entry = max(0.0, max(radial.x, vertical.x));
    float exit = min(maxDistance, min(radial.y, vertical.y));
    if (exit <= entry) return false;
    spans = float4(entry, exit, exit, exit);
    float clearRadius = max(0.0, _StormBand.x - _StormBand.y - _StormShape.y - 2.0);
    float2 hole;
    if (clearRadius > 0.0 && StormCylinder(p, direction.xz, clearRadius, hole))
    {
        float holeStart = max(entry, hole.x);
        float holeEnd = min(exit, hole.y);
        if (holeEnd > holeStart)
        {
            spans = float4(entry, holeStart, holeEnd, exit);
            if (spans.y - spans.x < 0.001) spans = float4(holeEnd, exit, exit, exit);
        }
    }
    return (spans.y - spans.x) + (spans.w - spans.z) > 0.001;
}
float StormRayDistance(float4 spans, float compressedDistance)
{
    float firstLength = spans.y - spans.x;
    return compressedDistance < firstLength ? spans.x + compressedDistance : spans.z + compressedDistance - firstLength;
}
#endif
