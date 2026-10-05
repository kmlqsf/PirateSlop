#ifndef PIRATESLOP_WATER_SHIP_FOAM_INCLUDED
#define PIRATESLOP_WATER_SHIP_FOAM_INCLUDED
int _WaterShipCount;
float4 _WaterShipPositions[32];
float4 _WaterShipShapes[32];
float4 _WaterShipTilts[32];
float4 _WaterShipContour[5];
float4 _WaterShipFoamParams;
float WaterShipWidth(float normalized)
{
    float index = saturate(normalized) * 17;
    uint first = min((uint)index, 17u);
    uint second = min(first + 1u, 17u);
    float a = _WaterShipContour[first >> 2][first & 3u];
    float b = _WaterShipContour[second >> 2][second & 3u];
    return lerp(a, b, frac(index));
}
float WaterShipFoamMask(float3 positionWS, float noise)
{
    float contactFoam = 0, wakeFoam = 0;
    [loop]
    for (int i = 0; i < min(_WaterShipCount, 32); i++)
    {
        float4 ship = _WaterShipPositions[i];
        float4 shape = _WaterShipShapes[i];
        float2 delta = positionWS.xz - ship.xy;
        if (dot(delta, delta) > 12100) continue;
        float sine, cosine;
        sincos(ship.z, sine, cosine);
        float side = delta.x * cosine - delta.y * sine;
        float ahead = delta.x * sine + delta.y * cosine;
        float sideways = abs(side);
        float localZ = saturate((ahead + shape.y) / max(.1, shape.y + shape.z));
        float width = WaterShipWidth(localZ) * shape.x;
        float boundary = max(sideways - width, max(-ahead - shape.y, ahead - shape.z));
        float waterline = shape.w + side * _WaterShipTilts[i].x + ahead * _WaterShipTilts[i].y;
        float heightMask = 1 - smoothstep(_WaterShipFoamParams.z * .4, _WaterShipFoamParams.z, abs(positionWS.y - waterline));
        float contact = smoothstep(-.2, .05, boundary) * (1 - smoothstep(.15, max(.16, _WaterShipFoamParams.x), boundary));
        contactFoam = max(contactFoam, contact * heightMask * _WaterShipFoamParams.y * smoothstep(.25, .70, noise));
        float behind = max(0, -ahead - shape.y);
        float sternWidth = WaterShipWidth(0) * shape.x;
        float lengthMask = smoothstep(0, 4, behind) * (1 - smoothstep(18, 65, behind));
        float edge = 1 - smoothstep(.2, 1.8, abs(sideways - (sternWidth + behind * .16)));
        float center = (1 - smoothstep(0, sternWidth + behind * .12, sideways)) * .25;
        float bow = (1 - smoothstep(.2, 1.1, abs(sideways - max(0, (shape.z + 2 - ahead) * .35)))) * smoothstep(shape.z - 6, shape.z - 1, ahead) * (1 - smoothstep(shape.z + 1, shape.z + 4, ahead));
        wakeFoam = max(wakeFoam, saturate(ship.w) * (lengthMask * (edge + center) + bow));
    }
    return saturate(max(contactFoam, wakeFoam * smoothstep(.12, .75, noise) * _WaterShipFoamParams.w));
}
#endif
