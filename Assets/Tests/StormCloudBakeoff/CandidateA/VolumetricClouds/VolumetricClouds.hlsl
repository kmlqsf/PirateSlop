#ifndef URP_VOLUMETRIC_CLOUDS_HLSL
#define URP_VOLUMETRIC_CLOUDS_HLSL

#include "./VolumetricCloudsDefs.hlsl"
#include "./VolumetricCloudsUtilities.hlsl"

CloudRay BuildCloudsRay(float2 screenUV, float depth, float3 invViewDirWS, bool isOccluded)
{
    CloudRay ray;

#if defined(_LOCAL_VOLUMETRIC_CLOUDS) || defined(_PIRATESLOP_CLEAR_CLOUDS)
    ray.originWS = GetCameraPositionWS();
#else
    ray.originWS = float3(0.0, 0.0, 0.0);
#endif

#if defined(PIRATESLOP_CLOUD_REFLECTION) && defined(_PIRATESLOP_CLEAR_CLOUDS)
    if ((_PirateStormWeather.w > 0.0 || _PirateStormTestSkyWeather.w > 0.0) && _PirateStormBackdrop.x < 0.5)
        ray.originWS = float3(_ClearCloudWorldOffset.x, _ClearCloudWorldOffset.z, _ClearCloudWorldOffset.y);
#endif

    ray.direction = invViewDirWS;
    ray.pixelConeAngle = max(length(ddx(invViewDirWS)), length(ddy(invViewDirWS)));

    // Compute the max cloud ray length
    // For opaque objects, we only care about clouds in front of them.
#if defined(_LOCAL_VOLUMETRIC_CLOUDS) || defined(_PIRATESLOP_CLEAR_CLOUDS)
    // The depth may from a high-res texture which isn't ideal but can save performance.
    float distance = LinearEyeDepth(depth, _ZBufferParams) * rcp(dot(ray.direction, -UNITY_MATRIX_V[2].xyz));
    ray.maxRayLength = lerp(MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE, distance, isOccluded);
#else
    ray.maxRayLength = MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE;
#endif

    ray.integrationNoise = GenerateRandomFloat(screenUV);
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
    if(_PirateStormTestSkyWeather.w>0.0)
    {
        ray.integrationNoise=lerp(.2,.8,ray.integrationNoise);
        ray.maxRayLength=min(ray.maxRayLength,max(1000.0,_ClearCloudFarFadeEnd)/max(.001,length(ray.direction.xz)));
    }
#endif

    return ray;
}

VolumetricRayResult TraceVolumetricRay(CloudRay cloudRay)
{
    // Initiliaze the volumetric ray
    VolumetricRayResult volumetricRay;
    volumetricRay.scattering = 0.0;
    volumetricRay.ambient = 0.0;
    volumetricRay.transmittance = 1.0;
    volumetricRay.meanDistance = FLT_MAX;
    volumetricRay.invalidRay = true;

    // Determine if ray intersects bounding volume, if the ray does not intersect the cloud volume AABB, skip right away
    RayMarchRange rayMarchRange;
    if (GetCloudVolumeIntersection(cloudRay.originWS, cloudRay.direction, rayMarchRange))
    {
        if (cloudRay.maxRayLength >= rayMarchRange.start)
        {
            // Initialize the depth for accumulation
            volumetricRay.meanDistance = 0.0;

            // Total distance that the ray must travel including empty spaces
            // Clamp the travel distance to whatever is closer
            // - Sky Occluder
            // - Volume end
            // - Far plane
            float totalDistance = min(rayMarchRange.end, cloudRay.maxRayLength) - rayMarchRange.start;

            // Evaluate our integration step
            int primaryStepCount = max(1, (int)_NumPrimarySteps);
            float maxStepSize = _MaxStepSize;
#if defined(PIRATESLOP_CLOUD_REFLECTION) && defined(_PIRATESLOP_CLEAR_CLOUDS)
            if ((_PirateStormWeather.w > 0.0 || _PirateStormTestSkyWeather.w > 0.0) && _PirateStormBackdrop.x < 0.5)
            {
                primaryStepCount = min(primaryStepCount, 24);
                maxStepSize *= max(1.0, _NumPrimarySteps / primaryStepCount);
            }
#endif
            float stepS = min(totalDistance / primaryStepCount, maxStepSize);
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
            bool testSky=_PirateStormTestSkyWeather.w>0.0 && _PirateStormBackdrop.x<.5;
            if(testSky)stepS=totalDistance/primaryStepCount;
#endif
            totalDistance = stepS * primaryStepCount;

            // Compute the environment lighting that is going to be used for the cloud evaluation
            float3 rayMarchStartPS = ConvertToPS(cloudRay.originWS) + rayMarchRange.start * cloudRay.direction;
            float3 rayMarchEndPS = rayMarchStartPS + totalDistance * cloudRay.direction;

            // Tracking the number of steps that have been made
            int currentIndex = 0;

            // Normalization value of the depth
            float meanDistanceDivider = 0.0;

            // Current position for the evaluation, apply blue noise to start position
            float currentDistance = cloudRay.integrationNoise;
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
            if ((_PirateStormWeather.w > 0.0 || _PirateStormTestSkyWeather.w > 0.0) && _PirateStormBackdrop.x < 0.5)
                currentDistance *= stepS;
#endif
            float3 currentPositionWS = cloudRay.originWS + (rayMarchRange.start + currentDistance) * cloudRay.direction;

            // Initialize the values for the optimized ray marching
            bool activeSampling = true;
            int sequentialEmptySamples = 0;

            // Do the ray march for every step that we can.
            while (currentIndex < primaryStepCount && currentDistance < totalDistance)
            {
                float integrationStep = stepS;
                float shapeMipOffset = 0.0;
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
                if(testSky)
                {
                    float jitter=frac(cloudRay.integrationNoise+currentIndex*.61803398875);
                    float intervalStart = totalDistance * ((float)currentIndex / primaryStepCount);
                    float intervalEnd = totalDistance * ((float)(currentIndex + 1) / primaryStepCount);
                    integrationStep = intervalEnd - intervalStart;
                    currentDistance = lerp(intervalStart, intervalEnd, lerp(.05, .95, jitter));
                    currentPositionWS=cloudRay.originWS+(rayMarchRange.start+currentDistance)*cloudRay.direction;
                    float footprint = max(integrationStep * .5, (rayMarchRange.start + currentDistance) * cloudRay.pixelConeAngle);
                    shapeMipOffset = footprint;
                }
#endif
                // Compute the camera-distance based attenuation
                float densityAttenuationValue = DensityFadeValue(rayMarchRange.start + currentDistance);
                // Compute the mip offset for the erosion texture
                float erosionMipOffset = ErosionMipOffset(rayMarchRange.start + currentDistance);
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
                if(testSky)
                {
                    float footprint = max(integrationStep * .5, (rayMarchRange.start + currentDistance) * cloudRay.pixelConeAngle);
                    erosionMipOffset = max(erosionMipOffset, log2(max(1.0, footprint * _ErosionScale * 32.0 / NOISE_TEXTURE_NORMALIZATION_FACTOR)));
                }
#endif

                // Accumulate in WS and convert at each iteration to avoid precision issues
                float3 currentPositionPS = ConvertToPS(currentPositionWS);

                // Should we be evaluating the clouds or just doing the large ray marching
                if (activeSampling)
                {
                    // If the density is null, we can skip as there will be no contribution
                    CloudProperties properties;
                    EvaluateCloudProperties(currentPositionPS, shapeMipOffset, erosionMipOffset, false, false, properties);

                    // Apply the fade in function to the density
                    properties.density *= densityAttenuationValue;

                    if (properties.density > CLOUD_DENSITY_TRESHOLD)
                    {
                        // Contribute to the average depth (must be done first in case we end up inside a cloud at the next step)
                        half transmitanceXdensity = volumetricRay.transmittance * properties.density;
                        volumetricRay.meanDistance += (rayMarchRange.start + currentDistance) * transmitanceXdensity;
                        meanDistanceDivider += transmitanceXdensity;

                        // Evaluate the cloud at the position
                        EvaluateCloud(properties, cloudRay.direction, currentPositionPS, integrationStep, currentDistance / totalDistance, volumetricRay);

                        // if most of the energy is absorbed, just leave.
                        if (volumetricRay.transmittance < 0.003)
                        {
                            volumetricRay.transmittance = 0.0;
                            break;
                        }

                        // Reset the empty sample counter
                        sequentialEmptySamples = 0;
                    }
                    else
                        sequentialEmptySamples++;

                    // If it has been more than EMPTY_STEPS_BEFORE_LARGE_STEPS, disable active sampling and start large steps
                    if (sequentialEmptySamples == EMPTY_STEPS_BEFORE_LARGE_STEPS)
                        activeSampling = false;
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
                    if(testSky)activeSampling=true;
#endif

                    // Do the next step
                    float relativeStepSize = lerp(cloudRay.integrationNoise, 1.0, saturate(currentIndex));
#if defined(_PIRATESLOP_CLEAR_CLOUDS)
                    if ((_PirateStormWeather.w > 0.0 || _PirateStormTestSkyWeather.w > 0.0) && _PirateStormBackdrop.x < 0.5) relativeStepSize = 1.0;
#endif
                    currentPositionWS += cloudRay.direction * stepS * relativeStepSize;
                    currentDistance += stepS * relativeStepSize;

                }
                else
                {
                    CloudProperties properties;
                    EvaluateCloudProperties(currentPositionPS, 1.0, 0.0, true, false, properties);

                    // Apply the fade in function to the density
                    properties.density *= densityAttenuationValue;

                    // If the density is lower than our tolerance,
                    if (properties.density < CLOUD_DENSITY_TRESHOLD)
                    {
                        currentPositionWS += cloudRay.direction * stepS * 2.0;
                        currentDistance += stepS * 2.0;
                    }
                    else
                    {
                        // Somewhere between this step and the previous clouds started
                        // We reset all the counters and enable active sampling
                        currentPositionWS -= cloudRay.direction * stepS;
                        currentDistance -= stepS;
                        currentIndex -= 1;
                        activeSampling = true;
                        sequentialEmptySamples = 0;
                    }
                }
                currentIndex++;
            }

            // Normalized the depth we computed
            if (volumetricRay.meanDistance != 0.0)
            {
                volumetricRay.invalidRay = false;
                volumetricRay.meanDistance /= meanDistanceDivider;
                volumetricRay.meanDistance = min(volumetricRay.meanDistance, cloudRay.maxRayLength);

                float3 currentPositionPS = ConvertToPS(cloudRay.originWS) + volumetricRay.meanDistance * cloudRay.direction;
                float relativeHeight = EvaluateNormalizedCloudHeight(currentPositionPS);

                Light sun = GetMainLight();

                // Evaluate the sun color at the position
            #ifdef _PHYSICALLY_BASED_SUN
                half3 sunColor = _SunColor * EvaluateSunColorAttenuation(currentPositionPS, sun.direction, true) * _SunLightDimmer; // _SunColor includes PI
            #else
                half3 sunColor = sun.color * PI * _SunLightDimmer;
            #endif

                // Evaluate the environement lighting contribution
            #ifdef _CLOUDS_AMBIENT_PROBE
                half3 ambientTermTop = SAMPLE_TEXTURECUBE_LOD(_VolumetricCloudsAmbientProbe, sampler_VolumetricCloudsAmbientProbe, half3(0.0, 1.0, 0.0), 4.0).rgb;
                half3 ambientTermBottom = SAMPLE_TEXTURECUBE_LOD(_VolumetricCloudsAmbientProbe, sampler_VolumetricCloudsAmbientProbe, half3(0.0, -1.0, 0.0), 4.0).rgb;
            #else
                half3 ambientTermTop = EvaluateVolumetricCloudsAmbientProbe(half3(0.0, 1.0, 0.0));
                half3 ambientTermBottom = EvaluateVolumetricCloudsAmbientProbe(half3(0.0, -1.0, 0.0));
            #endif
                half3 ambient = max(0, lerp(ambientTermBottom, ambientTermTop, relativeHeight) * _AmbientProbeDimmer);

            #if defined(_PIRATESLOP_CLEAR_CLOUDS)
                if ((_PirateStormWeather.w > 0.0 || _PirateStormTestSkyWeather.w > 0.0) && _PirateStormBackdrop.x < 0.5)
                {
                    CloudCoverageData meanCoverage;
                    GetCloudCoverageData(currentPositionPS, meanCoverage);
                    float localHeight = saturate((relativeHeight - meanCoverage.minCloudHeight) / max(.01, meanCoverage.maxCloudHeight - meanCoverage.minCloudHeight));
                    half heightLight = lerp(.32, 1.0, smoothstep(.12, .78, localHeight));
                    half sunLuma = dot(sunColor, half3(.2126,.7152,.0722));
                    sunColor = lerp(sunColor, sunLuma.xxx, .75) * heightLight * lerp(1.0, .60, meanCoverage.rainClouds);
                    sunColor *= lerp(half3(1,1,1), half3(.68,.80,1.0), meanCoverage.rainClouds);
                    ambient *= lerp(half3(.52,.68,1.0), half3(1,1,1), smoothstep(.2,.8,localHeight));
                }
            #endif
                volumetricRay.scattering = sunColor * volumetricRay.scattering;
                volumetricRay.scattering += ambient * volumetricRay.ambient;
            }
        }
    }
    return volumetricRay;
}

#endif
