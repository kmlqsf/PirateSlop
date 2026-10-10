Shader "Hidden/PirateSlop/StormSmokePreview"
{
    Properties { _DensityMultiplier("Density", Float) = .95 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex PreviewVert
            #pragma fragment PreviewFrag
            #pragma target 4.5
            #define _LOCAL_VOLUMETRIC_CLOUDS
            #define _STORM_TEST_CLOUD_WALL
            #define STORM_SMOKE_PREVIEW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_CloudLutTexture);
            TEXTURE2D(_CloudCurveTexture);
            TEXTURE3D(_Worley128RGBA);
            TEXTURE3D(_ErosionNoise);
            TEXTURECUBE(_VolumetricCloudsAmbientProbe);
            SAMPLER(s_point_clamp_sampler);
            SAMPLER(s_linear_repeat_sampler);
            SAMPLER(s_trilinear_repeat_sampler);
            SAMPLER(sampler_VolumetricCloudsAmbientProbe);
            float _StormSmokePreviewProjectionScale;
            float4 _PreviewEye, _PreviewForward, _PreviewRight, _PreviewUp, _PreviewLens;
            #include "../../Game/BRZoneVolumetric/Shaders/VolumetricClouds.hlsl"
            struct PreviewInput { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct PreviewOutput { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            PreviewOutput PreviewVert(PreviewInput input)
            {
                PreviewOutput output;
                output.position = TransformObjectToHClip(input.vertex.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 PreviewFrag(PreviewOutput input) : SV_Target
            {
                float2 q = (input.uv * 2.0 - 1.0) * _PreviewLens.xy;
                float3 direction = normalize(_PreviewForward.xyz + _PreviewRight.xyz * q.x + _PreviewUp.xyz * q.y);
                CloudRay ray;
                ray.originWS = _PreviewEye.xyz;
                ray.direction = direction;
                ray.integrationNoise = .5;
                ray.lightningUV = input.uv;
                ray.maxRayLength = 20000.0;
                bool water = direction.y < -.0001;
                if (water)
                {
                    float distance = (_StormCenterWater.y - ray.originWS.y) / direction.y;
                    [unroll] for (int i = 0; i < 4; i++)
                    {
                        float3 positionWS = ray.originWS + direction * distance;
                        distance = (StormSmokeWater(positionWS) - ray.originWS.y) / direction.y;
                    }
                    ray.maxRayLength = max(0.0, distance);
                }
                float3 background = water ? float3(.026, .22, .23) : lerp(float3(.31, .39, .46), float3(.12, .23, .36), saturate(direction.y * 2.0));
                if (water)
                {
                    float3 hit = ray.originWS + direction * ray.maxRayLength;
                    float3 waterNormal = normalize(float3(StormSmokeWater(hit - float3(1,0,0)) - StormSmokeWater(hit + float3(1,0,0)), 2,
                        StormSmokeWater(hit - float3(0,0,1)) - StormSmokeWater(hit + float3(0,0,1))));
                    background *= .8 + max(0.0, dot(waterNormal, normalize(float3(.4,1,-.3)))) * .7;
                }
                VolumetricRayResult result = TraceTestSmoke(ray);
                float3 color = background * result.transmittance + result.scattering;
                return half4(LinearToSRGB(color), 1);
            }
            ENDHLSL
        }
    }
}
