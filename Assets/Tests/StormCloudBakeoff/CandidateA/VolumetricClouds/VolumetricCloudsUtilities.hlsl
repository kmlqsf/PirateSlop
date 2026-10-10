#ifndef URP_VOLUMETRIC_CLOUDS_UTILITIES_HLSL
#define URP_VOLUMETRIC_CLOUDS_UTILITIES_HLSL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

half3 EvaluateVolumetricCloudsAmbientProbe(half3 normalWS)
{
    // Linear + constant polynomial terms
    half3 res = SHEvalLinearL0L1(normalWS, clouds_SHAr, clouds_SHAg, clouds_SHAb);

    // Quadratic polynomials
    res += SHEvalLinearL2(normalWS, clouds_SHBr, clouds_SHBg, clouds_SHBb, clouds_SHC);

    return res;
}

// From HDRP: VolumetricCloudsUtilities.hlsl

// The number of octaves for the multi-scattering
#define NUM_MULTI_SCATTERING_OCTAVES 2
#define PHASE_FUNCTION_STRUCTURE half2
// Global offset to the high frequency noise
#define CLOUD_DETAIL_MIP_OFFSET 0.0
// Global offset for reaching the LUT/AO
#define CLOUD_LUT_MIP_OFFSET 1.0
// Size of Preset LUT (unused since it's not a compute shader)
#define CLOUD_MAP_LUT_PRESET_SIZE 64.0
// Density below wich we consider the density is zero (optimization reasons)
#define CLOUD_DENSITY_TRESHOLD 0.001
// Number of steps before we start the large steps
#define EMPTY_STEPS_BEFORE_LARGE_STEPS 8
// Forward eccentricity
#define FORWARD_ECCENTRICITY 0.7
// Forward eccentricity
#define BACKWARD_ECCENTRICITY 0.7
// Distance until which the erosion texture is used
#define MIN_EROSION_DISTANCE 3000.0
#define MAX_EROSION_DISTANCE 100000.0
// Value that is used to normalize the noise textures
#define NOISE_TEXTURE_NORMALIZATION_FACTOR 100000.0
// Maximal distance until which the "skybox"
#define MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE 200000.0 //FLT_MAX
// Maximal size of a light step
#define LIGHT_STEP_MAXIMAL_SIZE 1000.0

// The planet center position
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
#define _PlanetCenterPosition float3(0.0, -_EarthRadius, 0.0)
#include "Assets/Game/BRZoneVolumetric/Shaders/StormCloudBoundary.hlsl"
float4 _ClearCloudLayer;
#else
#define _PlanetCenterPosition _PlanetCenterRadius.xyz
#endif
#define ConvertToPS(x) (x - _PlanetCenterPosition)

// Structure that holds all the data required for the cloud ray marching
struct CloudRay
{
    // Origin of the ray in camera-relative space
    float3 originWS;
    // Direction of the ray in world space
    float3 direction;
    // Maximal ray length before hitting the far plane or an occluder
    float maxRayLength;
    // Integration Noise
    float integrationNoise;
    float pixelConeAngle;
};

// Structure that holds the result of our volumetric ray
struct VolumetricRayResult
{
    // Amount of lighting that reach the clouds
    // We keep track of sun light and ambient light separately for optimization
    // They are combine at the end of tracing
    half3 scattering;
    half ambient;
    // Transmittance through the clouds
    half transmittance;
    // Mean distance of the clouds
    float meanDistance;
    // Flag that defines if the ray is valid or not
    bool invalidRay;
};

// Perceptual blending
half EvaluateFinalTransmittance(half3 sceneColor, half transmittance)
{
    // Due to the high intensity of the sun, we often need apply the transmittance in a tonemapped space
    // As we only produce one transmittance, we evaluate the approximation on the luminance of the color
    half luminance = Luminance(sceneColor * _PostExposure);

    if (luminance > 0.0)
    {
        // Apply the transmittance in tonemapped space
        half resultLuminance = luminance * rcp(1.0 + luminance) * transmittance;
        resultLuminance = resultLuminance * rcp(1.0 - resultLuminance);

        // By softening the transmittance attenuation curve for pixels adjacent to cloud boundaries when the luminance is super high,  
        // We can prevent sun flicker and improve perceptual blending. (https://www.desmos.com/calculator/vmly6erwdo)
        half finalTransmittance = max(resultLuminance * rcp(luminance), pow(transmittance, 6));

        // This approach only makes sense if the color is not black
        transmittance = lerp(transmittance, finalTransmittance, _ImprovedTransmittanceBlend);
    }
    return saturate(transmittance);
}

// These 2 functions were moved to the Core RP package by the commit below:
// "[HDRP] Optimizations and quality improvements to PBR sky"
// https://github.com/Unity-Technologies/Graphics/commit/9f7464a87cb8a09f23869dc178560bb8b072d4ca
#if UNITY_VERSION < 202330

// Use an infinite far plane
// https://chaosinmotion.com/2010/09/06/goodbye-far-clipping-plane/
// 'depth' is the linear depth (view-space Z position)
float EncodeInfiniteDepth(float depth, float near)
{
    return saturate(near / depth);
}

// 'z' is the depth encoded in the depth buffer (1 at near plane, 0 at far plane)
float DecodeInfiniteDepth(float z, float near)
{
    return near / max(z, FLT_EPS);
}

#endif

// Function that takes a world space position and converts it to a depth value
float ConvertCloudDepth(float3 position)
{
    float4 hClip = TransformWorldToHClip(position);
    return hClip.z / hClip.w;
}

float GenerateRandomFloat(float2 screenUV)
{
    float time = unity_DeltaTime.y * _Time.y + _Seed;
    _Seed += 1.0;
    return GenerateHashedRandomFloat(uint3(screenUV * _ScreenSize.xy, time));
}

// Returns the closest hit in X and the farthest hit in Y.
// Returns a negative number if there's no intersection.
// (result.y >= 0) indicates success.
// (result.x < 0) indicates that we are inside the sphere.
float2 IntersectSphere(float sphereRadius, float cosChi,
                       float radialDistance, float rcpRadialDistance)
{
    // r_o = float2(0, r)
    // r_d = float2(sinChi, cosChi)
    // p_s = r_o + t * r_d
    //
    // R^2 = dot(r_o + t * r_d, r_o + t * r_d)
    // R^2 = ((r_o + t * r_d).x)^2 + ((r_o + t * r_d).y)^2
    // R^2 = t^2 + 2 * dot(r_o, r_d) + dot(r_o, r_o)
    //
    // t^2 + 2 * dot(r_o, r_d) + dot(r_o, r_o) - R^2 = 0
    //
    // Solve: t^2 + (2 * b) * t + c = 0, where
    // b = r * cosChi,
    // c = r^2 - R^2.
    //
    // t = (-2 * b + sqrt((2 * b)^2 - 4 * c)) / 2
    // t = -b + sqrt(b^2 - c)
    // t = -b + sqrt((r * cosChi)^2 - (r^2 - R^2))
    // t = -b + r * sqrt((cosChi)^2 - 1 + (R/r)^2)
    // t = -b + r * sqrt(d)
    // t = r * (-cosChi + sqrt(d))
    //
    // Why do we do this? Because it is more numerically robust.

    float d = Sq(sphereRadius * rcpRadialDistance) - saturate(1 - cosChi * cosChi);

    // Return the value of 'd' for debugging purposes.
    return (d < 0) ? d : (radialDistance * float2(-cosChi - sqrt(d),
                                                  -cosChi + sqrt(d)));
}

// TODO: remove.
float2 IntersectSphere(float sphereRadius, float cosChi, float radialDistance)
{
    return IntersectSphere(sphereRadius, cosChi, radialDistance, rcp(radialDistance));
}

float ComputeCosineOfHorizonAngle(float r)
{
    float R = _EarthRadius;
    float sinHor = R * rcp(r);
    return -sqrt(saturate(1 - sinHor * sinHor));
}

// Function that interects a ray with a sphere (optimized for very large sphere), returns up to two positives distances.

// numSolutions: 0, 1 or 2 positive solves
// startWS: rayOriginWS, might be camera positionWS
// dir: normalized ray direction
// radius: planet radius
// result: the distance of hitPos, which means the value of solves
int RaySphereIntersection(float3 startWS, float3 dir, float radius, out float2 result)
{
    float3 startPS = startWS + float3(0, _EarthRadius, 0);
    float a = dot(dir, dir);
    float b = 2.0 * dot(dir, startPS);
    float c = dot(startPS, startPS) - (radius * radius);
    float d = (b * b) - 4.0 * a * c;
    result = 0.0;
    int numSolutions = 0;
    if (d >= 0.0)
    {
        // Compute the values required for the solution eval
        float sqrtD = sqrt(d);
        float q = -0.5 * (b + FastSign(b) * sqrtD);
        result = float2(c / q, q / a);
        // Remove the solutions we do not want
        numSolutions = 2;
        if (result.x < 0.0)
        {
            numSolutions--;
            result.x = result.y;
        }
        if (result.y < 0.0)
            numSolutions--;
    }
    // Return the number of solutions
    return numSolutions;
}

// Returns true if the ray exits the cloud volume (doesn't intersect earth)
// The ray is supposed to start inside the volume
bool ExitCloudVolume(float3 originPS, float3 dir, float higherBoundPS, out float tExit)
{
    // Given that we are inside the volume, we are guaranteed to exit at the outer bound
    float radialDistance = length(originPS);
    float cosChi = dot(originPS, dir) * rcp(radialDistance);
    tExit = IntersectSphere(higherBoundPS, cosChi, radialDistance, rcp(radialDistance)).y;

    // If the ray intersects the earth, then the sun is occluded by the earth
    return cosChi >= ComputeCosineOfHorizonAngle(radialDistance);
}

struct RayMarchRange
{
    // The start of the range
    float start;
    // The length of the range
    float end;
};

// Returns true if the ray intersects the cloud volume
// Outputs the entry and exit distance from the volume
bool IntersectCloudVolume(float3 originPS, float3 dir, float lowerBoundPS, float higherBoundPS, out float tEntry, out float tExit)
{
    bool intersect;
    float radialDistance = length(originPS);
    float rcpRadialDistance = rcp(radialDistance);
    float cosChi = dot(originPS, dir) * rcpRadialDistance;
    float2 tInner = IntersectSphere(lowerBoundPS, cosChi, radialDistance, rcpRadialDistance);
    float2 tOuter = IntersectSphere(higherBoundPS, cosChi, radialDistance, rcpRadialDistance);

    if (tInner.x < 0.0 && tInner.y >= 0.0) // Below the lower bound
    {
        // The ray starts at the intersection with the lower bound and ends at the intersection with the outer bound
        tEntry = tInner.y;
        tExit = tOuter.y;
        // We don't see the clouds if they are behind Earth
        intersect = cosChi >= ComputeCosineOfHorizonAngle(radialDistance);
    }
    else // Inside or above the cloud volume
    {
        // The ray starts at the intersection with the outer bound, or at 0 if we are inside
        // The ray ends at the lower bound if we hit it, at the outer bound otherwise
        tEntry = max(tOuter.x, 0.0f);
        tExit = tInner.x >= 0.0 ? tInner.x : tOuter.y;
        // We don't see the clouds if we don't hit the outer bound
        intersect = tOuter.y >= 0.0f;
    }

    return intersect;
}

bool GetCloudVolumeIntersection(float3 originWS, float3 dir, out RayMarchRange rayMarchRange)
{
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
    if(_PirateStormTestSkyWeather.w>0.0 && _PirateStormBackdrop.x<.5)
    {
        ZERO_INITIALIZE(RayMarchRange,rayMarchRange);
        float bottom=_PirateStormCloudBoundary.x+360.0;
        float top=_PirateStormCloudBoundary.x+2200.0;
        if(abs(dir.y)<.00001)
        {
            rayMarchRange.end=MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE;
            return originWS.y>bottom && originWS.y<top;
        }
        float2 span=(float2(bottom,top)-originWS.y)/dir.y;
        rayMarchRange.start=max(0.0,min(span.x,span.y));
        rayMarchRange.end=max(span.x,span.y);
        return rayMarchRange.end>rayMarchRange.start;
    }
#endif
#if defined(_LOCAL_VOLUMETRIC_CLOUDS) || defined(_PIRATESLOP_CLEAR_CLOUDS)
    return IntersectCloudVolume(ConvertToPS(originWS), dir, _LowestCloudAltitude, _HighestCloudAltitude, rayMarchRange.start, rayMarchRange.end);
#else
    {
        ZERO_INITIALIZE(RayMarchRange, rayMarchRange);

        // intersect with all three spheres
        float2 intersectionInter, intersectionOuter;
        int numInterInner = RaySphereIntersection(originWS, dir, _LowestCloudAltitude, intersectionInter);
        int numInterOuter = RaySphereIntersection(originWS, dir, _HighestCloudAltitude, intersectionOuter);

        // The ray starts at the first intersection with the lower bound and goes up to the first intersection with the outer bound
        rayMarchRange.start = intersectionInter.x;
        rayMarchRange.end = intersectionOuter.x;

        // Return if we have an intersection
        return true;
    }
#endif
}

struct CloudProperties
{
    // Normalized float that tells the "amount" of clouds that is at a given location
    half density;
    // Ambient occlusion for the ambient probe
    half ambientOcclusion;
    // Normalized value that tells us the height within the cloud volume (vertically)
    float height;
    // Extinction over the interval
    half sigmaT;
    half stormShading;
};

// Global attenuation of the density based on the camera distance
half DensityFadeValue(float distanceToCamera)
{
    return saturate((distanceToCamera - _FadeInStart) * rcp(_FadeInStart + _FadeInDistance));
}

// Evaluate the erosion mip offset based on the camera distance
float ErosionMipOffset(float distanceToCamera)
{
    return lerp(0.0, 4.0, saturate((distanceToCamera - MIN_EROSION_DISTANCE) * rcp(MAX_EROSION_DISTANCE - MIN_EROSION_DISTANCE)));
}

// Function that returns the normalized height inside the cloud layer
float EvaluateNormalizedCloudHeight(float3 positionPS)
{
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
    if(_PirateStormTestSkyWeather.w>0.0 && _PirateStormBackdrop.x<.5)
        return RangeRemap(_LowestCloudAltitude,_HighestCloudAltitude,positionPS.y);
#endif
    return RangeRemap(_LowestCloudAltitude, _HighestCloudAltitude, length(positionPS));
}

// Animation of the cloud shape position
float3 AnimateShapeNoisePosition(float3 positionPS)
{
    // We reduce the top-view repetition of the pattern
    positionPS.y += (positionPS.x / 3.0 + positionPS.z / 7.0);
    // We add the contribution of the wind displacements
    return positionPS + float3(_WindVector.x, 0.0, _WindVector.y) * _MediumWindSpeed + float3(0.0, _VerticalShapeWindDisplacement, 0.0);
    //return positionPS;
}

// Animation of the cloud erosion position
float3 AnimateErosionNoisePosition(float3 positionPS)
{
    return positionPS + float3(_WindVector.x, 0.0, _WindVector.y) * _SmallWindSpeed + float3(0.0, _VerticalErosionWindDisplacement, 0.0);
    //return positionPS;
}

// Structure that holds all the data used to define the cloud density of a point in space
struct CloudCoverageData
{
    // From a top down view, in what proportions this pixel has clouds
    half coverage;
    // From a top down view, in what proportions this pixel has clouds
    half rainClouds;
    // Value that allows us to request the cloudtype using the density
    half cloudType;
    // Maximal cloud height
    half maxCloudHeight;
    half minCloudHeight;
};

#if defined(_PIRATESLOP_CLEAR_CLOUDS)
float3 PirateCloudHash(float2 cell)
{
    float3 value = frac(float3(cell.xyx) * float3(0.1031, 0.1030, 0.0973));
    value += dot(value, value.yxz + 33.33);
    return frac((value.xxy + value.yzz) * value.zyx);
}

float2 PirateCloudNoise2(float2 position)
{
    float2 cell = floor(position);
    float2 blend = frac(position);
    blend = blend * blend * (3.0 - 2.0 * blend);
    return lerp(lerp(PirateCloudHash(cell).xy, PirateCloudHash(cell + float2(1, 0)).xy, blend.x),
                lerp(PirateCloudHash(cell + float2(0, 1)).xy, PirateCloudHash(cell + float2(1, 1)).xy, blend.x), blend.y);
}

float PirateStormWeather(float3 positionWS)
{
    float weather=0.0;
    if(_PirateStormTestSkyWeather.w>0.0)weather=StormTestSkyWeather(positionWS);
    else if(_PirateStormWeather.w>0.0)
    {
        float radius=max(1.0,_PirateStormWeather.z);
        float sd=length(positionWS.xz-_PirateStormWeather.xy)-radius;
        if(_PirateStormBackdrop.x<.5)weather=smoothstep(-80.0,120.0,sd);
        else
        {
            float width=StormCloudTransitionWidth(radius,_PirateStormWeather.w);
            weather=smoothstep(-width*.5,width*.5,sd-StormBoundaryOffset(positionWS,radius));
        }
    }
    return weather;
}
void PirateCloudCoverage(float3 positionPS,out CloudCoverageData data)
{
    ZERO_INITIALIZE(CloudCoverageData,data);
    bool gameStorm=(_PirateStormWeather.w>0.0 || _PirateStormTestSkyWeather.w>0.0) && _PirateStormBackdrop.x<.5;
    float cellSize=max(gameStorm?min(4200.0,_ClearCloudCellSize):_ClearCloudCellSize,1000.0);
    float2 position=AnimateShapeNoisePosition(positionPS).xz/cellSize+float2(.2381,.4044);
    float2 warp=PirateCloudNoise2(position*.18+float2(_ClearCloudSeed,_ClearCloudSeed+46.2));
    position+=(warp-.5)*1.8;
    float2 weather=PirateCloudNoise2(position+float2(_ClearCloudSeed+83.1,_ClearCloudSeed+29.7));
    float coverageStart=clamp(_ClearCloudCoverageStart,0.0,.95);
    float coverageEnd=clamp(_ClearCloudCoverageEnd,coverageStart+.01,1.0);
    float coverage=smoothstep(coverageStart,coverageEnd,weather.x);
    float3 world=positionPS+_PlanetCenterPosition;
    bool testWeather=_PirateStormTestSkyWeather.w>0.0;
    float stormWeather=testWeather?StormTestSkyWeather(float3(world.x,_PirateStormTestClouds.z+600.0,world.z)):PirateStormWeather(world);
    float2 stormPattern=PirateCloudNoise2(AnimateShapeNoisePosition(positionPS).xz/(testWeather?2200.0:1350.0)+float2(_ClearCloudSeed+151.3,_ClearCloudSeed+69.4));
    float stormCoverage=gameStorm?lerp(.28,.92,smoothstep(.26,.58,stormPattern.x)):lerp(.75,.96,weather.y);
    if(testWeather)stormCoverage=lerp(.12,.65,smoothstep(.37,.73,stormPattern.x));
    if(testWeather && gameStorm)
    {
        float height=world.y-_PirateStormCloudBoundary.x;
        float clearBottom=1200.0+warp.y*120.0;
        float clearTop=2000.0+weather.y*180.0;
        float stormBottom=400.0+warp.y*150.0;
        float stormTop=1150.0+stormPattern.y*350.0;
        float clearWindow=smoothstep(clearBottom,clearBottom+120.0,height)*(1.0-smoothstep(clearTop-180.0,clearTop,height));
        float stormWindow=smoothstep(stormBottom,stormBottom+140.0,height)*(1.0-smoothstep(stormTop-220.0,stormTop,height));
        float clearWeight=coverage*(1.0-stormWeather)*clearWindow;
        float stormWeight=stormCoverage*stormWeather*stormWindow;
        float totalWeight=clearWeight+stormWeight;
        float testFadeStart=max(_ClearCloudFarFadeStart,1000.0);
        float testFadeEnd=max(_ClearCloudFarFadeEnd,testFadeStart+1000.0);
        data.coverage=totalWeight*(1.0-smoothstep(testFadeStart,testFadeEnd,length(world.xz-_ClearCloudWorldOffset.xy)))*.94;
        data.rainClouds=stormWeight/max(.001,totalWeight);
        data.cloudType=.25;
        data.minCloudHeight=0.0;
        data.maxCloudHeight=1.0;
        return;
    }
    float stormCrown=1.0;
    float layerBottom=_LowestCloudAltitude-_EarthRadius-_PirateStormCloudBoundary.x;
    float layerRange=max(1.0,_HighestCloudAltitude-_LowestCloudAltitude);
    if(_PirateStormBackdrop.x>.5)
    {
        stormCrown=saturate((StormBackdropCrownHeight(world)-layerBottom)/layerRange);
        stormCoverage=lerp(.60,.93,StormBackdropMacro(world))*smoothstep(0.0,.07,stormCrown);
    }
    coverage=lerp(coverage,stormCoverage,stormWeather);
    if(coverage<=CLOUD_DENSITY_TRESHOLD)return;
    float baseHeight=lerp(.02,.16,warp.y);
    float cloudHeight=lerp(.58,.82,weather.y);
    baseHeight=lerp(baseHeight,lerp(.02,gameStorm?.22:.06,warp.y),stormWeather);
    cloudHeight=lerp(cloudHeight,lerp(.84,.94,weather.y),stormWeather);
    if(_PirateStormCloudBand.w>0.0)cloudHeight=lerp(cloudHeight,max(.001,stormCrown-baseHeight),stormWeather);
    float fadeStart=max(_ClearCloudFarFadeStart,1000.0);
    float fadeEnd=max(_ClearCloudFarFadeEnd,fadeStart+1000.0);
    data.coverage=coverage*(1.0-smoothstep(fadeStart,fadeEnd,length(positionPS.xz-_ClearCloudWorldOffset.xy)))*.94;
    data.rainClouds=stormWeather;
    data.cloudType=.25;
    data.minCloudHeight=baseHeight;
    data.maxCloudHeight=baseHeight+cloudHeight;
    if(gameStorm)
    {
        float bottom=_LowestCloudAltitude-_EarthRadius;
        float clearMin=(1200.0+lerp(.02,.16,warp.y)*1000.0-bottom)/layerRange;
        float clearMax=(1200.0+(lerp(.02,.16,warp.y)+lerp(.58,.82,weather.y))*1000.0-bottom)/layerRange;
        float stormBottom=testWeather?400.0+warp.y*150.0:850.0+warp.y*170.0;
        float stormTop=testWeather?1150.0+stormPattern.y*350.0:1800.0+stormPattern.y*550.0;
        data.minCloudHeight=lerp(clearMin,(stormBottom-bottom)/layerRange,stormWeather);
        data.maxCloudHeight=lerp(clearMax,(stormTop-bottom)/layerRange,stormWeather);
    }
}
#endif

// Function that evaluates the coverage data for a given point in planet space
void GetCloudCoverageData(float3 positionPS, out CloudCoverageData data)
{
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
    PirateCloudCoverage(positionPS, data);
#else
    // Convert the position into dome space and center the texture is centered above (0, 0, 0)
    //float2 normalizedPosition = AnimateCloudMapPosition(positionPS).xz / _NormalizationFactor * _CloudMapTiling.xy + _CloudMapTiling.zw - 0.5;
//#if defined(CLOUDS_SIMPLE_PRESET)
    half4 cloudMapData = half4(0.9, 0.0, 0.25, 1.0);
//#else
    //float4 cloudMapData = SAMPLE_TEXTURE2D_LOD(_CloudMapTexture, s_linear_repeat_sampler, float2(normalizedPosition), 0);
//#endif
    data.coverage = cloudMapData.x;
    data.rainClouds = cloudMapData.y;
    data.cloudType = cloudMapData.z;
    data.maxCloudHeight = cloudMapData.w;
    data.minCloudHeight = 0.0;
#endif
}

// Density remapping function
half DensityRemap(half x, half a, half b, half c, half d)
{
    return (((x - a) * rcp(b - a)) * (d - c)) + c;
}

// Horizon zero dawn technique to darken the clouds
half PowderEffect(half cloudDensity, half cosAngle, half intensity)
{
    half powderEffect = 1.0 - exp(-cloudDensity * 4.0);
    powderEffect = saturate(powderEffect * 2.0);
    return lerp(1.0, lerp(1.0, powderEffect, smoothstep(0.5, -0.5, cosAngle)), intensity);
}

#if defined(_PIRATESLOP_CLEAR_CLOUDS)
void EvaluateTestSkyProperties(float3 positionPS,float footprint,out CloudProperties properties)
{
    ZERO_INITIALIZE(CloudProperties,properties);
    float3 world=positionPS+_PlanetCenterPosition;
    float height=world.y-_PirateStormCloudBoundary.x;
    if(height<=360.0 || height>=2200.0)return;
    CloudCoverageData coverage;
    PirateCloudCoverage(positionPS,coverage);
    if(coverage.coverage<=CLOUD_DENSITY_TRESHOLD)return;
    float3 flow=world+float3(_WindVector.x,0,_WindVector.y)*_MediumWindSpeed;
    flow.y=height;
    float3 coordinates=flow*float3(1.0,.7,1.0);
    float broadMip=clamp(log2(max(1.0,footprint*32.0/2600.0)),0.0,4.0);
    float detailMip=clamp(log2(max(1.0,footprint*32.0/1100.0)),0.0,4.0);
    float broad=SAMPLE_TEXTURE3D_LOD(_ErosionNoise,s_trilinear_repeat_sampler,coordinates/2600.0,broadMip).r;
    float detail=SAMPLE_TEXTURE3D_LOD(_ErosionNoise,s_trilinear_repeat_sampler,coordinates/1100.0+float3(.31,.57,.83),detailMip).r;
    float shape=smoothstep(.28,.65,broad*.72+detail*.28);
    properties.height=saturate((height-360.0)/1840.0);
    properties.stormShading=coverage.rainClouds;
    properties.density=shape*coverage.coverage*_DensityMultiplier;
    properties.sigmaT=lerp(.012,.018,coverage.rainClouds);
    properties.ambientOcclusion=lerp(.65,.36,coverage.rainClouds)*lerp(.65,1.0,shape);
}
#endif

// Function that evaluates the cloud properties at a given absolute world space position
void EvaluateCloudProperties(float3 positionPS, float noiseMipOffset, float erosionMipOffset, bool cheapVersion, bool lightSampling,
                            out CloudProperties properties)
{
    // Initliaze all the values to 0 in case
    ZERO_INITIALIZE(CloudProperties, properties);
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
    if(_PirateStormTestSkyWeather.w>0.0 && _PirateStormBackdrop.x<.5)
    {
        EvaluateTestSkyProperties(positionPS,noiseMipOffset,properties);
        return;
    }
#endif

//#ifndef CLOUDS_SIMPLE_PRESET
    // When using a cloud map, we cannot support the full planet due to UV issues
//#endif

    // Remove global clouds below the horizon
#ifndef _LOCAL_VOLUMETRIC_CLOUDS
    if (positionPS.y < _EarthRadius)
        return;
#endif


    // By default the ambient occlusion is 1.0
    properties.ambientOcclusion = 1.0;

    // Evaluate the normalized height of the position within the cloud volume
    properties.height = EvaluateNormalizedCloudHeight(positionPS);

    // When rendering in camera space, we still want horizontal scrolling
#if !defined(_LOCAL_VOLUMETRIC_CLOUDS) && !defined(_PIRATESLOP_CLEAR_CLOUDS)
    positionPS.xz += _WorldSpaceCameraPos.xz;
#endif

    CloudCoverageData cloudCoverageData;
    GetCloudCoverageData(positionPS, cloudCoverageData);
    if (cloudCoverageData.coverage <= CLOUD_DENSITY_TRESHOLD || properties.height > cloudCoverageData.maxCloudHeight)
        return;
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
    if (properties.height < cloudCoverageData.minCloudHeight)
        return;
    properties.height = saturate((properties.height - cloudCoverageData.minCloudHeight) / max(cloudCoverageData.maxCloudHeight - cloudCoverageData.minCloudHeight, 0.01));
#endif

    // Evaluate the generic sampling coordinates
    float3 baseNoiseSamplingCoordinates = float3(AnimateShapeNoisePosition(positionPS).xzy / NOISE_TEXTURE_NORMALIZATION_FACTOR) * _ShapeScale - float3(_ShapeNoiseOffset.x, _ShapeNoiseOffset.y, _VerticalShapeNoiseOffset);

    #if defined(_PIRATESLOP_CLEAR_CLOUDS)
    if ((_PirateStormWeather.w > 0.0 || _PirateStormTestSkyWeather.w > 0.0) && _PirateStormBackdrop.x < .5) baseNoiseSamplingCoordinates *= 1.4;
#endif
    // Evaluate the coordinates at which the noise will be sampled and apply wind displacement
    baseNoiseSamplingCoordinates += properties.height * float3(_WindDirection.x, _WindDirection.y, 0.0f) * _AltitudeDistortion;

    // Read the low frequency Perlin-Worley and Worley noises
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
    bool filteredTestSky = _PirateStormTestSkyWeather.w > 0.0 && _PirateStormBackdrop.x < 0.5;
    noiseMipOffset = filteredTestSky ? clamp(noiseMipOffset, 0.0, 4.0) : min(noiseMipOffset, _PirateStormWeather.w > 0.0 && _PirateStormBackdrop.x < 0.5 ? 1.0 : 2.0);
#endif
    half lowFrequencyNoise = SAMPLE_TEXTURE3D_LOD(_Worley128RGBA, s_trilinear_repeat_sampler, baseNoiseSamplingCoordinates.xyz, noiseMipOffset).r;

#if defined(_PIRATESLOP_CLEAR_CLOUDS)
    half shapeFactor = saturate(_ShapeFactor);
    half erosionFactor = _ErosionFactor * lerp(0.65, 1.0, properties.height);
    float3 secondaryCoords = baseNoiseSamplingCoordinates.zxy * 2.07 + float3(0.173, 0.419, 0.731);
    half secondaryNoise = SAMPLE_TEXTURE3D_LOD(_Worley128RGBA, s_trilinear_repeat_sampler, secondaryCoords, filteredTestSky ? noiseMipOffset + 1.05 : noiseMipOffset).r;
    half shapeNoise = lerp(lowFrequencyNoise, secondaryNoise, lerp(0.18, 0.38, shapeFactor));
    bool gameStorm = (_PirateStormWeather.w > 0.0 || _PirateStormTestSkyWeather.w > 0.0) && _PirateStormBackdrop.x < 0.5;
    if (gameStorm)
    {
        half unionWeight = saturate(.5 + .5 * (lowFrequencyNoise - secondaryNoise) / .15);
        half rounded = lerp(secondaryNoise, lowFrequencyNoise, unionWeight) + .15 * unionWeight * (1.0 - unionWeight);
        shapeNoise = lerp(shapeNoise, rounded, .65);
    }
    half heightGradient = smoothstep(0.02, 0.14, properties.height) * (1.0 - smoothstep(0.55, 0.98, properties.height));
    if(filteredTestSky)heightGradient=1.0;
    properties.stormShading = _PirateStormBackdrop.x < .5 ? cloudCoverageData.rainClouds : 0.0;
    half shapeThreshold = lerp(lerp(0.48, 0.64, shapeFactor), 0.46, cloudCoverageData.rainClouds);
    half threshold = lerp(1.0, shapeThreshold, cloudCoverageData.coverage * heightGradient);
    half base_cloud = saturate((shapeNoise - threshold) / max(1.0 - threshold, 0.001));
    properties.ambientOcclusion = lerp(lerp(0.68, 0.35, cloudCoverageData.rainClouds), lerp(1.0, 0.8, cloudCoverageData.rainClouds), smoothstep(0.05, 0.85, properties.height));
    if (gameStorm) properties.ambientOcclusion *= lerp(.62, 1.0, smoothstep(.52,.86,shapeNoise));
    properties.sigmaT = lerp(0.04, _PirateStormBackdrop.x > 0.5 ? 0.07 : 0.045, cloudCoverageData.rainClouds);
#if defined(_CLOUDS_MICRO_EROSION)
    half microDetailFactor = _MicroErosionFactor;
#endif
#else
    // Read from the LUT
//#if defined(CLOUDS_SIMPLE_PRESET)
    half3 densityErosionAO = SAMPLE_TEXTURE2D_LOD(_CloudCurveTexture, s_linear_repeat_sampler, half2(0.0, properties.height), 0).xyz;
//#else
    //half3 densityErosionAO = SAMPLE_TEXTURE2D_LOD(_CloudLutTexture, s_linear_repeat_sampler, float2(cloudCoverageData.cloudType, properties.height), CLOUD_LUT_MIP_OFFSET).xyz;
//#endif

    // Adjust the shape and erosion factor based on the LUT and the coverage
    half shapeFactor = lerp(0.1, 1.0, _ShapeFactor) * densityErosionAO.y;
    half erosionFactor = _ErosionFactor * densityErosionAO.y;
#if defined(_CLOUDS_MICRO_EROSION)
    half microDetailFactor = _MicroErosionFactor * densityErosionAO.y;
#endif

    // Combine with the low frequency noise, we want less shaping for large clouds
    lowFrequencyNoise = lerp(1.0, lowFrequencyNoise, shapeFactor);
    half base_cloud = 1.0 - densityErosionAO.x * cloudCoverageData.coverage.x * (1.0 - shapeFactor);
    base_cloud = saturate(DensityRemap(lowFrequencyNoise, base_cloud, 1.0, 0.0, 1.0)) * cloudCoverageData.coverage.x * cloudCoverageData.coverage.x;

    // Weight the ambient occlusion's contribution
    properties.ambientOcclusion = densityErosionAO.z;

    // Change the sigma based on the rain cloud data
    properties.sigmaT = lerp(0.04, 0.12, cloudCoverageData.rainClouds);

    // The ambient occlusion value that is baked is less relevant if there is shaping or erosion, small hack to compensate that
    half ambientOcclusionBlend = saturate(1.0 - max(erosionFactor, shapeFactor) * 0.5);
    properties.ambientOcclusion = lerp(1.0, properties.ambientOcclusion, ambientOcclusionBlend);
#endif

    // Apply the erosion for nicer details
    if (!cheapVersion)
    {
        float3 erosionCoords = AnimateErosionNoisePosition(positionPS) / NOISE_TEXTURE_NORMALIZATION_FACTOR * _ErosionScale;
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
        erosionMipOffset = filteredTestSky ? clamp(erosionMipOffset, 0.0, 5.0) : min(erosionMipOffset, 1.0);
#endif
        half erosionNoise = 1.0 - SAMPLE_TEXTURE3D_LOD(_ErosionNoise, s_linear_repeat_sampler, erosionCoords, CLOUD_DETAIL_MIP_OFFSET + erosionMipOffset).x;
        erosionNoise = lerp(0.0, erosionNoise, erosionFactor * 0.75 * cloudCoverageData.coverage.x);
        properties.ambientOcclusion = saturate(properties.ambientOcclusion - sqrt(erosionNoise * _ErosionOcclusion));
        base_cloud = DensityRemap(base_cloud, erosionNoise, 1.0, 0.0, 1.0);

        #if defined(_CLOUDS_MICRO_EROSION)
        float3 fineCoords = AnimateErosionNoisePosition(positionPS) / (NOISE_TEXTURE_NORMALIZATION_FACTOR) * _MicroErosionScale;
        float fineMipOffset = erosionMipOffset;
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
        if (filteredTestSky) fineMipOffset = min(5.0, fineMipOffset + log2(max(1.0, _MicroErosionScale / max(1.0, _ErosionScale))));
#endif
        half fineNoise = 1.0 - SAMPLE_TEXTURE3D_LOD(_ErosionNoise, s_linear_repeat_sampler, fineCoords, CLOUD_DETAIL_MIP_OFFSET + fineMipOffset).x;
        fineNoise = lerp(0.0, fineNoise, microDetailFactor * 0.5 * cloudCoverageData.coverage.x);
        base_cloud = DensityRemap(base_cloud, fineNoise, 1.0, 0.0, 1.0);
        #endif
    }

    // Given that we are not sampling the erosion texture, we compensate by substracting an erosion value
    if (lightSampling)
    {
        base_cloud -= erosionFactor * 0.1;
        #if defined(_CLOUDS_MICRO_EROSION)
        base_cloud -= microDetailFactor * 0.15;
        #endif
    }

    // Make sure we do not send any negative values
    base_cloud = max(0, base_cloud);

    // Attenuate everything by the density multiplier
    properties.density = base_cloud * _DensityMultiplier;
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
    properties.density *= cloudCoverageData.coverage * lerp(1.0, _PirateStormBackdrop.x > 0.5 ? 3.65 : 1.8, cloudCoverageData.rainClouds);
#endif
}

// Function that evaluates the transmittance to the sun at a given cloud position
half3 EvaluateSunTransmittance(float3 positionPS, half3 sunDirection, PHASE_FUNCTION_STRUCTURE phaseFunction)
{
    // Compute the Ray to the limits of the cloud volume in the direction of the light
    int lightStepCount = max(1, (int)_NumLightSteps);
#if defined(PIRATESLOP_CLOUD_REFLECTION) && defined(_PIRATESLOP_CLEAR_CLOUDS)
    if ((_PirateStormWeather.w > 0.0 || _PirateStormTestSkyWeather.w > 0.0) && _PirateStormBackdrop.x < 0.5) lightStepCount = min(lightStepCount, 1);
#endif
    float totalLightDistance = 0.0;
    half3 transmittance = half3(0.0, 0.0, 0.0);

    // If we early out, this means we've hit the earth itself
    if (ExitCloudVolume(positionPS, sunDirection, _HighestCloudAltitude, totalLightDistance))
    {
        // Because of the very limited numebr of light steps and the potential humongous distance to cover, we decide to potnetially cover less and make it more useful
        totalLightDistance = clamp(totalLightDistance, 0, lightStepCount * LIGHT_STEP_MAXIMAL_SIZE);

        // Apply a small bias to compensate for the imprecision in the ray-sphere intersection at world scale.
        totalLightDistance += 5.0;

        // Compute the size of the current step
        float intervalSize = totalLightDistance * rcp((float)lightStepCount);
        float opticalDepth = 0;

        // Collect total density along light ray.
        for (int j = 0; j < lightStepCount; j++)
        {
            // Here we intentionally do not take the right step size for the first step
            // as it helps with darkening the clouds a bit more than they should at low light samples
            float dist = intervalSize * (0.25 + j);

            // Evaluate the current sample point
            float3 currentSamplePointPS = positionPS + sunDirection * dist;
            // Get the cloud properties at the sample point
            CloudProperties lightRayCloudProperties;
            EvaluateCloudProperties(currentSamplePointPS, 3.0 * j / lightStepCount, 0.0, true, true, lightRayCloudProperties);

            opticalDepth += lightRayCloudProperties.density * lightRayCloudProperties.sigmaT;
        }

        // Compute the luminance for each octave
        // https://magnuswrenninge.com/wp-content/uploads/2010/03/Wrenninge-OzTheGreatAndVolumetric.pdf
        half3 extinction = intervalSize * opticalDepth * _ScatteringTint.xyz;
        for (int o = 0; o < NUM_MULTI_SCATTERING_OCTAVES; ++o)
        {
            half msFactor = PositivePow(_MultiScattering, o);
            transmittance += exp(-extinction * msFactor) * (phaseFunction[o] * msFactor);
        }
    }

    return transmittance;
}

float ChapmanUpperApprox(float z, float cosTheta)
{
    float c = cosTheta;
    float n = 0.761643 * ((1 + 2 * z) - (c * c * z));
    float d = c * z + sqrt(z * (1.47721 + 0.273828 * (c * c * z)));

    return 0.5 * c + (n * rcp(d));
}

float ChapmanHorizontal(float z)
{
    float r = rsqrt(z);
    float s = z * r; // sqrt(z)

    return 0.626657 * (r + 2 * s);
}

// Default atmosphere settings of HDRP physically based sky
#if defined(PHYSICALLY_BASED_SKY)
half _AirScaleHeight;
half _AerosolScaleHeight;
half _AirDensityFalloff;
half _AerosolDensityFalloff;
//float _AtmosphericRadius;
#define _PlanetaryRadius _EarthRadius // TODO: unify earth radius control
half3 _AirSeaLevelExtinction;
half _AerosolSeaLevelExtinction;
#else
#define _AirScaleHeight 8000.0
#define _AerosolScaleHeight 1200.0
#define _AirDensityFalloff 1.0 / _AirScaleHeight
#define _AerosolDensityFalloff 1.0 / _AerosolScaleHeight
#define _PlanetaryRadius _EarthRadius
#define _AirSeaLevelExtinction (half3(5.8, 13.5, 33.1) / 1000000.0)
#define _AerosolSeaLevelExtinction 0.00001
#endif

//#define _AlphaSaturation 1.0
//#define _AlphaMultiplier 1.0

float3 ComputeAtmosphericOpticalDepth(float r, float cosTheta, bool aboveHorizon)
{
    const float2 n = float2(_AirDensityFalloff, _AerosolDensityFalloff);
    const float2 H = float2(_AirScaleHeight, _AerosolScaleHeight);
    const float  R = _PlanetaryRadius;

    float2 z = n * r;
    float2 Z = n * R;

    float sinTheta = sqrt(saturate(1 - cosTheta * cosTheta));

    float2 ch;
    ch.x = ChapmanUpperApprox(z.x, abs(cosTheta)) * exp(Z.x - z.x); // Rescaling adds 'exp'
    ch.y = ChapmanUpperApprox(z.y, abs(cosTheta)) * exp(Z.y - z.y); // Rescaling adds 'exp'

    if (!aboveHorizon) // Below horizon, intersect sphere
    {
        float sinGamma = (r / R) * sinTheta;
        float cosGamma = sqrt(saturate(1 - sinGamma * sinGamma));

        float2 ch_2;
        ch_2.x = ChapmanUpperApprox(Z.x, cosGamma); // No need to rescale
        ch_2.y = ChapmanUpperApprox(Z.y, cosGamma); // No need to rescale

        ch = ch_2 - ch;
    }
    else if (cosTheta < 0)   // Above horizon, lower hemisphere
    {
        // z_0 = n * r_0 = (n * r) * sin(theta) = z * sin(theta).
        // Ch(z, theta) = 2 * exp(z - z_0) * Ch(z_0, Pi/2) - Ch(z, Pi - theta).
        float2 z_0 = z * sinTheta;
        float2 b = exp(Z - z_0); // Rescaling cancels out 'z' and adds 'Z'
        float2 a;
        a.x = 2 * ChapmanHorizontal(z_0.x);
        a.y = 2 * ChapmanHorizontal(z_0.y);
        float2 ch_2 = a * b;

        ch = ch_2 - ch;
    }

    float2 optDepth = ch * H;

    return optDepth.x * _AirSeaLevelExtinction.xyz + optDepth.y * _AerosolSeaLevelExtinction;
}

// This function evaluates the sun color attenuation from the physically based sky
half3 EvaluateSunColorAttenuation(float3 positionPS, half3 sunDirection, bool estimatePenumbra = false)
{
    float r = length(positionPS);
    float cosTheta = dot(positionPS, sunDirection) * rcp(r); // Normalize

    // Point can be below horizon due to precision issues
    r = max(r, _PlanetaryRadius);
    float cosHoriz = ComputeCosineOfHorizonAngle(r);

    if (cosTheta >= cosHoriz) // Above horizon
    {
        float3 oDepth = ComputeAtmosphericOpticalDepth(r, cosTheta, true);
        half3 opacity = 1 - TransmittanceFromOpticalDepth(oDepth);
        half penumbra = saturate((cosTheta - cosHoriz) / 0.0019); // very scientific value
        half3 attenuation = 1 - opacity;// (Desaturate(opacity, _AlphaSaturation) * _AlphaMultiplier);
        return estimatePenumbra ? attenuation * penumbra : attenuation;
    }
    else
    {
        return 0;
    }
}

// Function that evaluates the sun color along the ray
half3 EvaluateSunColor(float3 entryEvaluationPointPS, float3 exitEvaluationPointPS, half3 sunDirection, half3 sunColor, float relativeRayDistance)
{
    // evaluate the attenuation at both points (entrance and exit of the cloud layer)
    half3 sunColor0 = sunColor * EvaluateSunColorAttenuation(entryEvaluationPointPS, sunDirection, true);
    half3 sunColor1 = sunColor * EvaluateSunColorAttenuation(exitEvaluationPointPS, sunDirection, false);

    return lerp(sunColor0, sunColor1, relativeRayDistance);
}

// Evaluates the inscattering from this position
void EvaluateCloud(CloudProperties cloudProperties, half3 rayDirection,
                float3 currentPositionPS, float stepSize, float relativeRayDistance,
                inout VolumetricRayResult volumetricRay)
{
    // Apply the extinction
    const half extinction = cloudProperties.density * cloudProperties.sigmaT;
    const half transmittance = exp(-extinction * stepSize);

    Light sun = GetMainLight();
    half cosAngle = dot(rayDirection, sun.direction);

    // Evaluate the phase function for each of the octaves
    half2 phaseFunction = half2(0.0, 0.0);
    half forwardP = HenyeyGreensteinPhaseFunction(FORWARD_ECCENTRICITY * PositivePow(_MultiScattering, 0), cosAngle);
    half backwardsP = HenyeyGreensteinPhaseFunction(-BACKWARD_ECCENTRICITY * PositivePow(_MultiScattering, 0), cosAngle);
    phaseFunction[0] = forwardP + backwardsP;

#if NUM_MULTI_SCATTERING_OCTAVES >= 2
    forwardP = HenyeyGreensteinPhaseFunction(FORWARD_ECCENTRICITY * PositivePow(_MultiScattering, 1), cosAngle);
    backwardsP = HenyeyGreensteinPhaseFunction(-BACKWARD_ECCENTRICITY * PositivePow(_MultiScattering, 1), cosAngle);
    phaseFunction[1] = forwardP + backwardsP;
#endif

#if NUM_MULTI_SCATTERING_OCTAVES >= 3
    forwardP = HenyeyGreensteinPhaseFunction(FORWARD_ECCENTRICITY * PositivePow(_MultiScattering, 2), cosAngle);
    backwardsP = HenyeyGreensteinPhaseFunction(-BACKWARD_ECCENTRICITY * PositivePow(_MultiScattering, 2), cosAngle);
    phaseFunction[2] = forwardP + backwardsP;
#endif

    // Compute the powder effect
    half powderEffect = PowderEffect(cloudProperties.density, cosAngle, _PowderEffectIntensity);

    // Evaluate the sun visibility
    half3 sunTransmittance = EvaluateSunTransmittance(currentPositionPS, sun.direction, phaseFunction);

    // Compute luminance separately to factor out color multiplication at the end of the loop
    // Use 1 as placeholder to compute the 'transfer function'
    half3 sunLuminance = 1.0 * sunTransmittance * powderEffect;
    half ambientLuminance = 1.0 * cloudProperties.ambientOcclusion;
    sunLuminance *= lerp(half3(1,1,1),half3(.24,.34,.48),cloudProperties.stormShading);
    ambientLuminance *= lerp(1.0,.20,cloudProperties.stormShading);

    // "Energy-conserving analytical integration"
    // See slide 28 at http://www.frostbite.com/2015/08/physically-based-unified-volumetric-rendering-in-frostbite/
    // No division by clamped extinction because albedo == 1 => sigma_s == sigma_e so it simplifies
    // Note: this is not true anymore when _ScatteringTint is modified, but it still looks correct
    volumetricRay.scattering += sunLuminance     * (volumetricRay.transmittance - volumetricRay.transmittance * transmittance);
    volumetricRay.ambient    += ambientLuminance * (volumetricRay.transmittance - volumetricRay.transmittance * transmittance);
    volumetricRay.transmittance *= transmittance;
}

#endif
