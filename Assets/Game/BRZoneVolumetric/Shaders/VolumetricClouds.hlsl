#ifndef URP_VOLUMETRIC_CLOUDS_HLSL
#define URP_VOLUMETRIC_CLOUDS_HLSL

#include "./VolumetricCloudsDefs.hlsl"
#include "./VolumetricCloudsUtilities.hlsl"

TEXTURE2D_X(_StormTestLightningEmission);
TEXTURE2D_X(_StormTestLightningDistance);
SAMPLER(sampler_StormTestLightningEmission);

CloudRay BuildCloudsRay(float2 screenUV, float depth, float3 invViewDirWS, bool isOccluded)
{
    CloudRay ray;
    ray.lightningUV=screenUV;

#ifdef _LOCAL_VOLUMETRIC_CLOUDS
    ray.originWS = GetCameraPositionWS();
#else
    ray.originWS = float3(0.0, 0.0, 0.0);
#endif

    ray.direction = invViewDirWS;

    // Compute the max cloud ray length
    // For opaque objects, we only care about clouds in front of them.
#ifdef _LOCAL_VOLUMETRIC_CLOUDS
    // The depth may from a high-res texture which isn't ideal but can save performance.
    float distance = LinearEyeDepth(depth, _ZBufferParams) * rcp(dot(ray.direction, -UNITY_MATRIX_V[2].xyz));
    ray.maxRayLength = lerp(MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE, distance, isOccluded);
#else
    ray.maxRayLength = MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE;
#endif

    if (_PirateStormTestClouds.x<.5 && ray.direction.y < -0.0001 && ray.originWS.y >= _StormCenterWater.y)
        ray.maxRayLength = min(ray.maxRayLength, (_StormCenterWater.y - ray.originWS.y) / ray.direction.y);
    if (_PirateStormTestClouds.x < .5 && _PirateStormBackdrop.x < .5 && _PirateStormBillows > .5 && _PirateStormVolume3D < .5) ray.maxRayLength = min(ray.maxRayLength, 420.0);
    if (_PirateStormTestClouds.x < .5 && _PirateStormBackdrop.x < .5 && _PirateStormVolume3D > .5 && _PirateStormNearMedium > .99) ray.maxRayLength=min(ray.maxRayLength,35.0);
    ray.integrationNoise = _PirateStormVolume3D>.5 && _PirateStormBackdrop.x<.5 ? lerp(.35,.65,GenerateRandomFloat(screenUV)) : GenerateRandomFloat(screenUV);
    if(_PirateStormTestClouds.x>.5)
        ray.integrationNoise=.5;

    return ray;
}

VolumetricRayResult TraceTestSmoke(CloudRay ray)
{
    VolumetricRayResult result;
    ZERO_INITIALIZE(VolumetricRayResult,result);
    result.transmittance=1.0;
    result.meanDistance=FLT_MAX;
    result.invalidRay=true;
    float4 spans;
    if(!StormRayIntervals(ray.originWS,ray.direction,ray.maxRayLength,spans))return result;

    float weightedDistance=0.0,weight=0.0;
    float4 lightning=SAMPLE_TEXTURE2D_X_LOD(_StormTestLightningEmission,sampler_StormTestLightningEmission,ray.lightningUV,0);
    float lightningDistance=SAMPLE_TEXTURE2D_X_LOD(_StormTestLightningDistance,sampler_StormTestLightningEmission,ray.lightningUV,0).r/max(.00001,lightning.a);
    bool lightningAdded=false;
#if defined(STORM_SMOKE_PREVIEW)
    float projectionScale=_StormSmokePreviewProjectionScale;
#else
    float projectionScale=1.0/max(1.0,_ScreenParams.y*abs(UNITY_MATRIX_P[1][1]));
#endif
    Light sun=GetMainLight();
    float3 lightDirection=normalize(sun.direction+float3(0,.55,0));
    [loop] for(int segment=0;segment<2;segment++)
    {
        float spanStart=segment==0?spans.x:spans.z;
        float total=segment==0?spans.y-spans.x:spans.w-spans.z;
        if(total<=.001 || result.transmittance<.003)continue;
        float current=0.0;
        float nextStep=1.0;
        float previousDensity=0.0;
        [loop] for(int index=0;index<160;index++)
        {
            if(current>=total)break;
            float desiredStep=clamp(1.0+(spanStart+current)*projectionScale*.8,1.0,2.5);
            float step=min(total-current,max(desiredStep,nextStep));
            float jitter=ray.integrationNoise;
            float distanceWS=spanStart+current+jitter*step;
            float3 positionWS=ray.originWS+ray.direction*distanceWS;
            float3 normal;
            float pixelWidth=distanceWS*projectionScale*2.0;
            float4 field=StormTestSmokeField(positionWS,pixelWidth,normal);
            if(field.r>previousDensity*2.0+.025 && step>desiredStep*3.0)
            {
                float low=current,high=current+jitter*step;
                [unroll] for(int refine=0;refine<4;refine++)
                {
                    float middle=(low+high)*.5;
                    float3 unusedNormal;
                    float sampleDensity=StormTestSmokeField(ray.originWS+ray.direction*(spanStart+middle),pixelWidth,false,unusedNormal).r;
                    if(sampleDensity>previousDensity*2.0+.025)high=middle;
                    else low=middle;
                }
                current=low;
                step=min(desiredStep,total-current);
                distanceWS=spanStart+current+jitter*step;
                positionWS=ray.originWS+ray.direction*distanceWS;
                pixelWidth=distanceWS*projectionScale*2.0;
                field=StormTestSmokeField(positionWS,pixelWidth,normal);
            }
            float density=min(1.0,field.r*_DensityMultiplier)*smoothstep(.5,3.5,distanceWS);
            float extinction=.14;
            float segmentStart=spanStart+current;
            current+=step;
            nextStep=field.r>.003?desiredStep:min(10.0,step*1.6);
            previousDensity=field.r;
            if(!lightningAdded && lightning.a>.001 && lightningDistance>=spanStart && lightningDistance<=segmentStart+step && lightningDistance<ray.maxRayLength)
            {
                float beforeBolt=clamp(lightningDistance-segmentStart,0.0,step);
                result.scattering+=lightning.rgb*result.transmittance*exp(-density*extinction*beforeBolt);
                lightningAdded=true;
            }
            if(density<=CLOUD_DENSITY_TRESHOLD)continue;
            half selfShadow=exp(-field.r*1.6);
            half sunlight=(.35+.65*saturate(dot(normal,lightDirection)))*selfShadow*saturate(field.a);
            half3 ambient=half3(.009,.012,.016)+half3(.012,.014,.016)*field.a;
            half3 color=ambient+half3(.12,.124,.13)*pow(saturate(sunlight),1.35);
            color+=half3(.012,.013,.014)*field.b*selfShadow;
            color*=lerp(.7,1.2,field.b);
            color*=lerp(.68,1.0,smoothstep(_StormCenterWater.y,_PirateStormTestClouds.z,positionWS.y));
            half flash=0;
            [loop] for(int group=0;group<12;group++)
                flash+=StormTestLightningScatter(positionWS,field,normal,_StormSmokeFlashVolumes[group],group);
            color+=_StormLightningColor.rgb*flash*2.0;
            half transmittance=exp(-density*extinction*step);
            half contribution=result.transmittance*(1.0-transmittance);
            result.scattering+=color*contribution;
            weightedDistance+=distanceWS*contribution;
            weight+=contribution;
            result.transmittance*=transmittance;
            if(result.transmittance<.003)break;
        }
    }
    if(weight>.00001)
    {
        result.invalidRay=false;
        result.meanDistance=weightedDistance/weight;
    }
    else if(lightningAdded)
    {
        result.invalidRay=false;
        result.meanDistance=lightningDistance;
    }
    return result;
}

VolumetricRayResult TraceVolumetricRay(CloudRay cloudRay)
{
#if defined(_STORM_TEST_CLOUD_WALL)
    return TraceTestSmoke(cloudRay);
#else
    // Initiliaze the volumetric ray
    VolumetricRayResult volumetricRay;
    volumetricRay.scattering = 0.0;
    volumetricRay.ambient = 0.0;
    volumetricRay.transmittance = 1.0;
    volumetricRay.meanDistance = FLT_MAX;
    volumetricRay.invalidRay = true;

    // Determine if ray intersects bounding volume, if the ray does not intersect the cloud volume AABB, skip right away
    RayMarchRange rayMarchRange;
    float4 stormIntervals;
    if (StormRayIntervals(cloudRay.originWS, cloudRay.direction, cloudRay.maxRayLength, stormIntervals))
    {
        rayMarchRange.start = stormIntervals.x;
        rayMarchRange.end = stormIntervals.w;
        if (cloudRay.maxRayLength >= rayMarchRange.start)
        {
            // Initialize the depth for accumulation
            volumetricRay.meanDistance = 0.0;

            // Total distance that the ray must travel including empty spaces
            // Clamp the travel distance to whatever is closer
            // - Sky Occluder
            // - Volume end
            // - Far plane
            float totalDistance = (stormIntervals.y - stormIntervals.x) + (stormIntervals.w - stormIntervals.z);

            // Evaluate our integration step
            float stepS = totalDistance / (float)_NumPrimarySteps;
            totalDistance = stepS * _NumPrimarySteps;

            // Compute the environment lighting that is going to be used for the cloud evaluation
            float3 rayMarchStartPS = ConvertToPS(cloudRay.originWS) + rayMarchRange.start * cloudRay.direction;
            float3 rayMarchEndPS = rayMarchStartPS + totalDistance * cloudRay.direction;

            // Tracking the number of steps that have been made
            int currentIndex = 0;

            // Normalization value of the depth
            float meanDistanceDivider = 0.0;

            // Current position for the evaluation, apply blue noise to start position
            float currentDistance = cloudRay.integrationNoise * stepS;
            float3 currentPositionWS = cloudRay.originWS + (rayMarchRange.start + currentDistance) * cloudRay.direction;

            // Initialize the values for the optimized ray marching
            bool activeSampling = true;
            int sequentialEmptySamples = 0;

            // Do the ray march for every step that we can.
            while (currentIndex < (int)_NumPrimarySteps && currentDistance < totalDistance)
            {
                if (_PirateStormBackdrop.x < .5)
                {
                    float f0=(float)currentIndex/max(1.0,_NumPrimarySteps);
                    float f1=(float)(currentIndex+1)/max(1.0,_NumPrimarySteps);
                    stepS=(f1*f1-f0*f0)*totalDistance;
                    currentDistance=f0*f0*totalDistance+cloudRay.integrationNoise*stepS;
                    activeSampling=true;
                }

                // Compute the camera-distance based attenuation
                float sampleDistance = StormRayDistance(stormIntervals, max(0.0, currentDistance));
                currentPositionWS = cloudRay.originWS + sampleDistance * cloudRay.direction;
                float densityAttenuationValue = 1.0;
                // Compute the mip offset for the erosion texture
                float erosionMipOffset = ErosionMipOffset(sampleDistance);

                // Accumulate in WS and convert at each iteration to avoid precision issues
                float3 currentPositionPS = ConvertToPS(currentPositionWS);

                // Should we be evaluating the clouds or just doing the large ray marching
                if (activeSampling)
                {
                    // If the density is null, we can skip as there will be no contribution
                    CloudProperties properties;
                    EvaluateCloudProperties(currentPositionPS, min(2.0,log2(max(1.0,stepS/5.0))), erosionMipOffset, false, false, properties);

                    // Apply the fade in function to the density
                    properties.density *= densityAttenuationValue;

                    if (properties.density > CLOUD_DENSITY_TRESHOLD)
                    {
                        // Contribute to the average depth (must be done first in case we end up inside a cloud at the next step)
                        half transmitanceXdensity = volumetricRay.transmittance * properties.density;
                        volumetricRay.meanDistance += sampleDistance * transmitanceXdensity;
                        meanDistanceDivider += transmitanceXdensity;

                        // Evaluate the cloud at the position
                        EvaluateCloud(properties, cloudRay.direction, currentPositionPS, stepS, currentDistance / totalDistance, volumetricRay);

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

                    // Do the next step
                    float relativeStepSize = 1.0;
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
                if (_PirateStormBackdrop.x < .5)
                {
                    float f=(float)currentIndex/max(1.0,_NumPrimarySteps);
                    currentDistance=f*f*totalDistance;
                }
            }

            // Normalized the depth we computed
            if (volumetricRay.meanDistance != 0.0)
            {
                volumetricRay.invalidRay = false;
                volumetricRay.meanDistance /= meanDistanceDivider;
                volumetricRay.meanDistance = min(volumetricRay.meanDistance, cloudRay.maxRayLength);
            }
        }
    }
    return volumetricRay;
#endif
}

#endif
