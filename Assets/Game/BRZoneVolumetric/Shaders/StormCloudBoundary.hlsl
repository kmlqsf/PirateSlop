#ifndef PIRATESLOP_STORM_CLOUD_BOUNDARY
#define PIRATESLOP_STORM_CLOUD_BOUNDARY
float4 _PirateStormCloudBoundary;
float4 _PirateStormBackdrop;
float4 _PirateStormCloudBand;
float _PirateStormBillows;
float _PirateStormVolume3D;
float4 _PirateStormTestClouds;
float _PirateStormTestCloudBase;
float4 _PirateStormTestSkyWeather;
float3 StormBoundaryHash(float2 cell)
{
    float3 value = frac(float3(cell.xyx) * float3(0.1031, 0.1030, 0.0973));
    value += dot(value, value.yxz + 33.33);
    return frac((value.xxy + value.yzz) * value.zyx);
}
float StormBoundaryNoise(float2 position)
{
    float2 cell = floor(position);
    float2 blend = frac(position);
    blend = blend * blend * (3.0 - 2.0 * blend);
    return lerp(lerp(StormBoundaryHash(cell).x, StormBoundaryHash(cell + float2(1, 0)).x, blend.x),
                lerp(StormBoundaryHash(cell + float2(0, 1)).x, StormBoundaryHash(cell + float2(1, 1)).x, blend.x), blend.y);
}
float StormTestSkyWeatherAtWidth(float3 positionWS,float projectedWidth)
{
    if(_PirateStormTestSkyWeather.w<=0.0)return 0.0;
    float height=max(0.0,positionWS.y-_PirateStormTestClouds.z);
    float width=_PirateStormTestSkyWeather.w+sqrt(height)*8.0;
    float macro=StormBoundaryNoise(positionWS.xz/520.0+float2(37.13,83.71))*.65
        +StormBoundaryNoise(positionWS.xz/180.0-float2(19.23,57.31))*.35;
    float sd=length(positionWS.xz-_PirateStormTestSkyWeather.xy)-_PirateStormTestSkyWeather.z;
    sd-=height*.4+(macro-.5)*width*.6;
    width*=lerp(.85,1.2,StormBoundaryNoise(positionWS.xz/950.0+71.39));
    width=sqrt(width*width+projectedWidth*projectedWidth);
    return smoothstep(-width*.5,width*.5,sd);
}
float StormTestSkyWeather(float3 positionWS)
{
    return StormTestSkyWeatherAtWidth(positionWS,0.0);
}
float3 StormTestSkyColor(float3 color,float3 viewDirection,float3 cameraWS)
{
    if(_PirateStormTestSkyWeather.w<=0.0)return color;
    float distance=clamp((850.0+_PirateStormTestCloudBase-cameraWS.y)/max(.001,viewDirection.y),0.0,100000.0);
    float projectedWidth=1000.0*length(viewDirection.xz)/max(.025,viewDirection.y);
    float weather=StormTestSkyWeatherAtWidth(cameraWS+viewDirection*distance,projectedWidth);
    float luminance=dot(color,float3(.2126,.7152,.0722));
    float daylight=smoothstep(.001,.05,luminance);
    float3 overcast=float3(.035,.043,.056)*daylight+luminance*float3(.60,.65,.73);
    return lerp(color,overcast,weather*.85);
}
float StormBoundaryAmplitude(float radius)
{
    float amplitude = min(max(0.0, _PirateStormCloudBoundary.z), max(0.0, radius) * 0.12);
    return _PirateStormBackdrop.x > .5 ? amplitude : min(amplitude, (_PirateStormCloudBand.x + _PirateStormCloudBand.y) * .35);
}
float StormBackdropMacro(float3 positionWS)
{
    float2 position = positionWS.xz + float2(1.0, 0.35) * _PirateStormBackdrop.w * 0.18;
    return StormBoundaryNoise(position / 2200.0 + float2(17.31, 46.72)) * 0.78
         + StormBoundaryNoise(position / 900.0 + float2(81.19, 23.53)) * 0.22;
}
float StormBackdropCrownHeight(float3 positionWS)
{
    return lerp(0.60, 1.0, smoothstep(0.22, 0.78, StormBackdropMacro(positionWS))) * _PirateStormBackdrop.z;
}
float StormBoundaryMacro(float3 positionWS)
{
    float macro = 0.0;
    if (_PirateStormBackdrop.x > 0.5) macro = StormBackdropMacro(positionWS);
    else macro = StormBoundaryNoise(positionWS.xz / 900.0 + float2(17.31, 46.72)) * 0.78
               + StormBoundaryNoise(positionWS.xz / 350.0 + float2(81.19, 23.53)) * 0.22;
    return macro;
}
float StormWallSpread(float heightWS)
{
    return _PirateStormBackdrop.x > .5 ? 0.0 : smoothstep(55.0,180.0,heightWS-_PirateStormCloudBoundary.x);
}
float StormWallInner(float heightWS,float width)
{
    return width + StormWallSpread(heightWS)*100.0;
}
float StormWallOuter(float heightWS,float width)
{
    return width + StormWallSpread(heightWS)*420.0;
}
float StormBoundaryInward(float h, float distance)
{
    float offset;
    if (h < 1.0 / 6.0) offset = lerp(0.0, 0.05, h * 6.0);
    else if (h < 1.0 / 3.0) offset = lerp(0.05, 0.15, (h - 1.0 / 6.0) * 6.0);
    else if (h < 5.0 / 9.0) offset = lerp(0.15, 0.35, (h - 1.0 / 3.0) * 4.5);
    else if (h < 7.0 / 9.0) offset = lerp(0.35, 0.65, (h - 5.0 / 9.0) * 4.5);
    else offset = lerp(0.65, 1.0, (h - 7.0 / 9.0) * 4.5);
    return saturate(offset) * distance - StormWallSpread(_PirateStormCloudBoundary.x+h*_PirateStormCloudBand.w)*220.0;
}
float StormBoundaryCrownHeight(float3 positionWS, float height)
{
    float crownHeight = height;
    if (_PirateStormBackdrop.x > 0.5) crownHeight = StormBackdropCrownHeight(positionWS);
    else crownHeight = height * lerp(0.60, 1.0, smoothstep(0.22, 0.78, StormBoundaryMacro(positionWS)));
    return crownHeight;
}
float StormBoundaryOffset(float3 positionWS, float radius)
{
    float amplitude = StormBoundaryAmplitude(radius);
    float rise = smoothstep(80.0, max(81.0, _PirateStormCloudBoundary.w), positionWS.y - _PirateStormCloudBoundary.x);
    if (amplitude <= 0.0 || rise <= 0.0) return 0.0;
    float noise = StormBoundaryMacro(positionWS);
    float signedNoise = noise * 2.0 - 1.0;
    return signedNoise * (1.65 - 0.65 * abs(signedNoise)) * amplitude * rise;
}
float StormCloudTransitionWidth(float radius, float physicalWidth)
{
    return min(max(physicalWidth, _PirateStormCloudBoundary.y), max(1.0, radius) * 0.3);
}
#endif
