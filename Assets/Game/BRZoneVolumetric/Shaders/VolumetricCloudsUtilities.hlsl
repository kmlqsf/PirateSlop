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
#define _PlanetCenterPosition float3(0.0, 0.0, 0.0)
#include "StormAnnulus.hlsl"
#define ConvertToPS(x) (x - _PlanetCenterPosition)

// Structure that holds all the data required for the cloud ray marching
struct CloudRay
{
    // Origin of the ray in camera-relative space
    float3 originWS;
    // Direction of the ray in world space
    half3 direction;
    // Maximal ray length before hitting the far plane or an occluder
    float maxRayLength;
    // Integration Noise
    float integrationNoise;
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
    return GenerateHashedRandomFloat(uint3(screenUV * _ScreenSize.xy, 17u));
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
bool ExitCloudVolume(float3 originPS, half3 dir, float higherBoundPS, out float tExit)
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
bool IntersectCloudVolume(float3 originPS, half3 dir, float lowerBoundPS, float higherBoundPS, out float tEntry, out float tExit)
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

bool GetCloudVolumeIntersection(float3 originWS, half3 dir, out RayMarchRange rayMarchRange)
{
#ifdef _LOCAL_VOLUMETRIC_CLOUDS
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
    half body;
    half magic;
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
    return (positionPS.y - _StormCenterWater.y) / max(1.0, _StormShape.x);
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
};

// Function that evaluates the coverage data for a given point in planet space
void GetCloudCoverageData(float3 positionPS, out CloudCoverageData data)
{
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

// Function that evaluates the cloud properties at a given absolute world space position
void EvaluateCloudProperties(float3 positionPS, float noiseMipOffset, float erosionMipOffset, bool cheapVersion, bool lightSampling,
                            out CloudProperties properties)
{
    ZERO_INITIALIZE(CloudProperties, properties);
    float macro, billow;
    float3 flow;
    float stormMask = StormDensityMask(positionPS, min(noiseMipOffset, 1.0), lightSampling, macro, billow, flow);
    if (stormMask <= 0.0) return;

    properties.height = EvaluateNormalizedCloudHeight(positionPS);
    float cavities = (1.0 - billow) * (1.0 - macro * 0.65);
    float mass = macro - cavities * 0.30;
    float shape = smoothstep(0.14, 0.90, mass);
    float erosion = 0.0;
    if (!cheapVersion)
    {
        float t = _StormShape.z;
        float3 detailCurl = sin(flow.zxy * 4.1 + float3(t * 1.17, -t * 0.91 + 2.3, t * 1.43 + 4.1));
        float3 erosionCoords = flow.yzx * 2.4 + detailCurl * 0.085 + float3(t * 0.021, t * 0.015, -t * 0.027);
        erosion = 1.0 - SAMPLE_TEXTURE3D_LOD(_ErosionNoise, s_linear_repeat_sampler, erosionCoords, erosionMipOffset).r;
        erosion *= lerp(0.06, 0.15, _StormStyle.x) * (1.0 - shape * 0.8);
    }
    float body = saturate((shape - erosion) / max(0.01, 1.0 - erosion));
    body = smoothstep(lerp(0.04, 0.14, _StormStyle.x), lerp(0.88, 0.66, _StormStyle.x), body);
    float lowerBank = (1.0 - smoothstep(0.10, 0.38, properties.height)) * (0.035 + 0.10 * billow);
    body = max(body, lowerBank);
    properties.body = body * stormMask;
    properties.density = body * _DensityMultiplier * stormMask;
    properties.sigmaT = 0.055;
    properties.ambientOcclusion = saturate(0.25 + billow * 0.50 + (1.0 - macro) * 0.25);
    float pulse = 0.5 + 0.5 * sin(flow.y * 2.1 - flow.x * 1.7 + _StormShape.z * 0.29);
    float selection = smoothstep(0.55, 0.82, billow) * (1.0 - smoothstep(0.55, 0.9, macro));
    properties.magic = selection * pulse * pulse;
}
// Function that evaluates the transmittance to the sun at a given cloud position
half3 EvaluateSunTransmittance(float3 positionPS, half3 sunDirection, PHASE_FUNCTION_STRUCTURE phaseFunction)
{
    float4 lightIntervals;
    if (!StormRayIntervals(positionPS, sunDirection, 100000.0, lightIntervals)) return 1.0;
    float distanceLimit = min(lightIntervals.y, max(32.0, min(_StormShape.x, (_StormBand.y + _StormBand.z) * 0.8)));
    float opticalDepth = 0.0;
    float previousDistance = 0.0;
    int count = max(1, (int)_NumLightSteps);
    for (int j = 0; j < count; j++)
    {
        float fraction = (j + 1.0) / count;
        float endDistance = fraction * fraction * distanceLimit;
        float interval = endDistance - previousDistance;
        float sampleDistance = previousDistance + interval * 0.5;
        CloudProperties sampleCloud;
        EvaluateCloudProperties(positionPS + sunDirection * sampleDistance, 0.0, 0.0, true, true, sampleCloud);
        opticalDepth += sampleCloud.density * sampleCloud.sigmaT * interval;
        previousDistance = endDistance;
    }
    return exp(-opticalDepth * 0.65);
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
    half extinction = cloudProperties.density * cloudProperties.sigmaT;
    half transmittance = exp(-extinction * stepSize);
    Light sun = GetMainLight();
    half3 lightDirection = normalize(sun.direction + half3(0.0, 0.55, 0.0));
    half visibility = EvaluateSunTransmittance(currentPositionPS, lightDirection, half2(1.0, 0.0)).r;
    half bands = 0.20h * smoothstep(0.035h, 0.10h, visibility)
               + 0.46h * smoothstep(0.18h, 0.34h, visibility)
               + 0.34h * smoothstep(0.58h, 0.78h, visibility);
    half shading = lerp(visibility, bands, _StormStyle.x);
    half diffuseFill = smoothstep(0.22h, 0.78h, cloudProperties.ambientOcclusion);
    shading = max(shading, 0.10h + diffuseFill * 0.55h);
    half edge = (1.0h - smoothstep(0.12h, 0.68h, cloudProperties.body));
    half fill = 0.45h + 0.45h * diffuseFill;
    half3 interior = lerp(_StormCoreColor.rgb, _StormBodyColor.rgb, fill);
    half3 color = lerp(interior, _StormLitColor.rgb, shading * 0.88h);
    half softEdge = edge * smoothstep(0.15h, 0.85h, visibility);
    color = lerp(color, _StormHighlightColor.rgb, softEdge * 0.28h);
    color += _StormRimColor.rgb * _StormStyle.y * cloudProperties.magic * 0.025h * (0.4h + 0.6h * edge);
    color *= lerp(0.96h, 1.04h, saturate(Luminance(sun.color)));
    half contribution = volumetricRay.transmittance * (1.0h - transmittance);
    volumetricRay.scattering += color * contribution;
    volumetricRay.transmittance *= transmittance;
}
#endif
