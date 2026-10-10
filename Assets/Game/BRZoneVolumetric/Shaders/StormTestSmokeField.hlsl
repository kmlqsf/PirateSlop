#ifndef PIRATESLOP_STORM_TEST_SMOKE_FIELD
#define PIRATESLOP_STORM_TEST_SMOKE_FIELD
float4 _StormTestSmokeMap;
float4 _StormTestSmokeMotion;
float4 _StormSmokeVisibilityRange;
float4 _StormSmokeWaterPatch;
TEXTURE3D(_StormSmokeNoise);
SAMPLER(sampler_StormSmokeNoise);
TEXTURE2D(_StormSmokeWaterHeight);
SAMPLER(sampler_StormSmokeWaterHeight);
static const float STORM_SMOKE_FRINGE = 64.0;

float StormSmokeDistanceVisibility(float distanceWS)
{
    return 1.0 - smoothstep(_StormSmokeVisibilityRange.x, _StormSmokeVisibilityRange.y, distanceWS);
}

float4 StormSmokeNoise(float3 uv, float pixelWidth, float scale)
{
    float mip = clamp(log2(max(1.0, pixelWidth * 128.0 / scale)), 0.0, 6.0);
    return SAMPLE_TEXTURE3D_LOD(_StormSmokeNoise, sampler_StormSmokeNoise, uv, mip);
}
float StormSmokeWater(float3 p)
{
    float2 uv = (p.xz - _StormSmokeWaterPatch.xy) * _StormSmokeWaterPatch.z;
    float edge = min(min(uv.x, uv.y), min(1.0 - uv.x, 1.0 - uv.y));
    float wave = SAMPLE_TEXTURE2D_LOD(_StormSmokeWaterHeight, sampler_StormSmokeWaterHeight, saturate(uv), 0).r;
    return lerp(_StormCenterWater.y, wave, smoothstep(0.0, .04, edge) * _StormSmokeWaterPatch.w);
}
float StormTestSmokeBottom() { return _StormCenterWater.y - 12.0; }

float4 StormTestSmokeField(float3 p, float pixelWidth, bool sampleNormal, out float3 normal)
{
    normal = float3(0, 1, 0);
    float3 local = p - _StormCenterWater.xyz;
    float radius = length(local.xz);
    float sd = radius - _StormBand.x;
    float mapDistance = length(p.xz - _StormTestSmokeMap.xy);
    float top = _PirateStormTestClouds.z;
    if (sd <= -STORM_SMOKE_FRINGE || mapDistance >= _StormTestSmokeMap.z || p.y < StormTestSmokeBottom() || p.y > top) return 0.0;
    float2 radial = local.xz / max(.001, radius);
    float time = _StormTestSmokeMap.w;
    float advance = _StormTestSmokeMotion.x;
    float travel = _StormTestSmokeMotion.y;
    float fall = _StormTestSmokeMotion.z;
    float water = _StormCenterWater.y;
    if (local.y < 12.0) water += (StormSmokeWater(p) - water) * (1.0 - smoothstep(6.0, 12.0, local.y));
    float height = p.y - water;
    float ground = 1.0 - smoothstep(0.0, 12.0, height);
    float3 flow = local;
    flow.xz += radial * travel;
    flow.y += fall;
    float3 curl = sin(flow.zxy * float3(.093, .077, .085) + float3(time * .31, 1.7 - time * .24, time * .28 + 3.1));
    flow += curl * float3(3.2, 2.4, 3.2);
    float4 broad = StormSmokeNoise(flow / 64.0, pixelWidth, 64.0);
    float3 foldFlow = float3(flow.x * .8 + flow.z * .6, flow.y, flow.z * .8 - flow.x * .6);
    float4 fold = StormSmokeNoise(foldFlow / 29.0 + float3(.37, .71, .19), pixelWidth, 29.0);
    float4 fine = StormSmokeNoise((flow + curl * 1.4) / 9.0 + float3(time * -.04, 0, time * .023), pixelWidth, 9.0);
    float shape = broad.r * .68 + fold.r * .32;
    float body = smoothstep(.34, .68, shape);
    float threads = smoothstep(.38, .64, fine.r);
    float2 frontFlow = local.xz + radial * travel + curl.xz * 3.0;
    float front = StormBoundaryNoise(frontFlow / 47.0) * .6 + shape * .4;
    float offset = lerp(-18.0, 9.0, front) + (body - .5) * 13.0;
    offset += ground * advance * (9.0 + 5.0 * fold.r);
    float feather = 12.0 + 5.0 * broad.r + min(3.0, pixelWidth);
    float edge = smoothstep(offset - feather, offset + feather * .6, sd);
    float outer = 1.0 - smoothstep(_StormTestSmokeMap.z - 7.0, _StormTestSmokeMap.z, mapDistance);
    float crown = top - 3.0 - (1.0 - body) * 9.0 - (1.0 - fold.r) * 3.0;
    float upper = smoothstep(0.0, 6.0, crown - p.y);
    float lower = smoothstep(StormTestSmokeBottom(), StormTestSmokeBottom() + 3.0, p.y);
    float deep = smoothstep(0.0, 28.0, sd);
    float surface = .145 + body * .75 + threads * .06;
    float interior = .445 + body * .42 + threads * .06;
    if (advance > .001 && ground > .04)
    {
        float3 underFlow = local;
        underFlow.xz -= radial * travel;
        underFlow.y += fall;
        float under = StormSmokeNoise(underFlow / 29.0 + float3(.17, .43, .79), pixelWidth, 29.0).r;
        surface = lerp(surface, .28 + under * .7, ground * advance);
    }
    float density = lerp(surface, interior, deep) * edge;
    float3 foldGradient = float3(fold.g * .8 - fold.a * .6, fold.b, fold.g * .6 + fold.a * .8);
    float3 gradient = broad.gba * (.68 / 64.0) + foldGradient * (.32 / 29.0);
    float3 outward = float3(-radial.x, .08, -radial.y);
    float3 fieldNormal = normalize(-gradient + outward * (.025 * (1.0 - deep)) + float3(0, .025 * (1.0 - upper), 0) + .00001);
    float eligibility = smoothstep(-49.0, -35.0, sd) * (1.0 - smoothstep(-9.0, 5.0, sd)) * (1.0 - advance);
    if (eligibility > .001)
    {
        float2 cellBase = floor(local.xz / 18.0 - .5);
        [unroll] for (int cellIndex = 0; cellIndex < 4; cellIndex++)
        {
            float2 cell = cellBase + float2(cellIndex % 2, cellIndex / 2);
            float3 seed = StormBoundaryHash(cell + float2(13.17, 73.91));
            float2 centerXZ = (cell + .5) * 18.0 + (seed.xz - .5) * 4.0;
            float centerDistance = length(centerXZ) - _StormBand.x;
            float centerWeight = smoothstep(-29.0, -20.0, centerDistance) * (1.0 - smoothstep(-4.0, 7.0, centerDistance));
            if (centerWeight <= .001) continue;
            float lifetime = lerp(12.0, 16.0, seed.y);
            float age = frac(time / lifetime + seed.x) * lifetime;
            float falling = saturate(age / (lifetime - 3.0));
            float spreading = saturate((age - lifetime + 3.0) / 3.0);
            float2 centerRadial = normalize(centerXZ + .001);
            centerXZ -= centerRadial * spreading * 3.0;
            float puffRadius = lerp(6.0, 8.5, seed.z);
            float radiusY = lerp(puffRadius * .85, 1.0, smoothstep(0.0, 1.0, spreading));
            float centerY = lerp(top - _StormCenterWater.y - puffRadius, water - _StormCenterWater.y + puffRadius * .28, falling);
            centerY = lerp(centerY, water - _StormCenterWater.y + .6, spreading);
            float3 extent = float3(puffRadius * (1.0 + spreading * .45), radiusY, puffRadius * (1.0 + spreading * .45));
            float horizontalDistance = dot(local.xz - centerXZ, local.xz - centerXZ) / (extent.x * extent.x);
            if (horizontalDistance >= 1.0) continue;
            float3 q = (local - float3(centerXZ.x, centerY, centerXZ.y)) / extent;
            q += (float3(broad.r, fold.r, fine.r) - .5) * 1.25;
            float lobe = 1.0 - dot(q, q) + (fold.r - .5) * .8 + (fine.r - .5) * .5;
            float3 trail = q - float3(.18 * curl.x, .8, .18 * curl.z);
            float trailLobe = .52 - dot(trail, trail) + (fine.r - .5) * .5;
            lobe = lerp(max(lobe, trailLobe), lobe, spreading);
            float life = smoothstep(0.0, 1.5, age) * (1.0 - smoothstep(.25, 1.0, spreading));
            float puff = smoothstep(-.25, .55, lobe) * life * eligibility * centerWeight * (.55 + threads * .37)
                * (1.0 - smoothstep(.65, 1.0, horizontalDistance));
            if (puff > density)
            {
                density = puff;
                body = saturate(lobe);
                fieldNormal = normalize(q / extent - gradient * .4 + .00001);
            }
        }
    }
    normal = sampleNormal ? fieldNormal : float3(0, 1, 0);
    float exposure = saturate(.3225 + (1.0 - body) * .34 + threads * .035 + (1.0 - upper) * .2);
    return float4(min(1.0, density) * outer * upper * lower, body, threads, exposure);
}
float4 StormTestSmokeField(float3 p, float pixelWidth, out float3 normal) { return StormTestSmokeField(p, pixelWidth, true, normal); }
#endif
