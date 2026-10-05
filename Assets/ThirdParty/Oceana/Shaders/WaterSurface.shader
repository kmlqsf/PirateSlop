Shader "Oceana/WaterSurface" {
    Properties {
        [HideInInspector] _ScrollMap ("Scroll", 2D) = "gray" {}
        [HideInInspector] _SourceColor ("Scene Color", 2D) = "black" {}
        [HideInInspector] _SourceDepth ("Scene Depth", 2D) = "black" {}
        [HideInInspector] _WaterShipCount ("Ship Foam Count", Integer) = 0
        [HideInInspector] _WaterShipFoamParams ("Ship Foam Parameters", Vector) = (.9,.55,2.5,.9)
        [HideInInspector] _CameraAboveWater ("Camera Above Water", Float) = 1
        [HideInInspector] _SeaLevel ("Sea Level", Float) = 0
        [HideInInspector] _DisplaceHeight ("Wave Height", Float) = 10
        [HideInInspector] _OceanWaveTime ("Wave Time", Float) = 0
        [HideInInspector] _Whirlpool ("Whirlpool", Vector) = (500,120,2,0)
        [HideInInspector] _WhirlpoolCenter ("Whirlpool Center", Vector) = (0,0,550,0)
        _Color ("Color", color) = (0.2, 0.6, 0.8, 1)
        _ColorFade ("Color Fade", color) = (0.2, 0.6, 0.8, 1)

        _GlareSpecular ("Glare Specular Edge", range(0, 1)) = 0.6
        _GlareEdgeLow ("Glare Low Edge", range(0, 1)) = 0.8
        _GlareEdgeHigh ("Glare High Edge", range(0, 1)) = 0.9
        _GlareIntensity ("Glare Intensity", range(0, 1)) = 0.8

        _FresnelFade ("Fresnel Fade", float) = 100
        _FadeIntensity ("Fade Intensity", range(0, 1)) = 0.9
        _FresnelWater ("Fresnel Water", float) = 10

        _WindowEdgeLow ("Window Low Edge", range(0, 1)) = 0.5
        _WindowEdgeHigh ("Window High Edge", range(0, 1)) = 0.6

        _DisplaceEdgeClose ("Displace Edge Close", float) = 10
        _DisplaceEdgeFar ("Displace Edge Far", float) = 80

        _VisibleDepth ("Visible Depth", float) = 10
        _Refraction ("Refraction", float) = 0.1

        _FoamEdgeLow ("Foam Edge Low", float) = 0.4
        _FoamEdgeHigh ("Foam Edge High", float) = 0.4
        _FoamMask ("Foam Mask", 2D) = "white"{}
    }
    SubShader {
        Tags {"RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque"}

        HLSLINCLUDE
        #define PIRATESLOP_MODIFIED_OCEANA_SURFACE 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        cbuffer UnityPerMaterial {
            uniform float4 _Color;
            uniform float4 _ColorFade;

            uniform float _GlareSpecular;
            uniform float _GlareEdgeLow;
            uniform float _GlareEdgeHigh;
            uniform float _GlareIntensity;

            uniform float _FresnelFade;
            uniform float _FadeIntensity;
            uniform float _FresnelWater;

            uniform float _WindowEdgeLow;
            uniform float _WindowEdgeHigh;
 
            uniform float _DisplaceEdgeClose;
            uniform float _DisplaceEdgeFar;

            uniform float _VisibleDepth;
            uniform float _Refraction;

            uniform float _FoamEdgeLow;
            uniform float _FoamEdgeHigh;
            uniform float4 _FoamMask_ST;
        }

        cbuffer FromDynamic {
            uniform float4 _ScrollMap_ST;
            uniform uint _VertexMipLevel;

            uniform float _SeaLevel;
            uniform float _DisplaceHeight;

            uniform SamplerState _bilinearRepeatSampler;
            uniform SamplerState _pointRepeatSampler;
            uniform SamplerState _pointClampSampler;
        }

        tbuffer MapBuffer {
            uniform Texture2D _ScrollMap;
            uniform Texture2D _SourceColor;
            uniform Texture2D _SourceDepth;

            uniform Texture2D _FoamMask;
        }
        
        
        #include "include/MapPacking.hlsl"
        #include "Assets/Shaders/WaterShipFoam.hlsl"

        float _CameraAboveWater;
        float4 _Whirlpool;
        float3 _WhirlpoolCenter;
        float _OceanWaveTime;

        float WhirlpoolHeight(float2 position)
        {
            float radius = max(_Whirlpool.x, .001);
            float falloff = 1 - saturate(distance(position, _WhirlpoolCenter.xz) / radius);
            return -_Whirlpool.y * falloff * falloff;
        }
        float3 WhirlpoolNormal(float3 normal, float2 position)
        {
            float2 delta = position - _WhirlpoolCenter.xz;
            float radius = max(_Whirlpool.x, .001);
            float dist = max(length(delta), .001);
            float slope = 2 * _Whirlpool.y * (1 - saturate(dist / radius)) / radius;
            return normalize(normal + float3(-delta.x / dist * slope * normal.y, 0, -delta.y / dist * slope * normal.y));
        }

        float SpecularBRF(float3 viewDir, float3 normal, float3 lightDir) {
            float3 halfVector = normalize(viewDir + lightDir);
            return saturate(dot(halfVector, normal)) * saturate(sign(lightDir.y));
        }

        ENDHLSL

        Pass {
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex VertFunc
            #pragma target 4.5
            #pragma multi_compile_fog
            #pragma fragment FragFunc

            struct Varyings{
                float4 positionWS : TEXCOORD0;
                float4 positionSS : TEXCOORD1;
                float4 positionCS : SV_POSITION;
            };

            Varyings VertFunc(float3 positionOS : POSITION) {
                Varyings output = (Varyings)0;

                float3 directionWS = positionOS.xyz;
                output.positionWS = float4(directionWS, 1);
                float heightValue = _ScrollMap.SampleLevel(_bilinearRepeatSampler, output.positionWS.xz * _ScrollMap_ST.xy + _ScrollMap_ST.zw, 0).a;
                
                output.positionWS.y = _SeaLevel + (heightValue - .5) * _DisplaceHeight + WhirlpoolHeight(output.positionWS.xz);

                output.positionCS = mul(unity_MatrixVP, output.positionWS);
                output.positionSS = ComputeScreenPos(output.positionCS);

                return output;
            }

            float4 FragFunc(Varyings input) : SV_TARGET {
                
                float4 sample = _ScrollMap.Sample(_bilinearRepeatSampler, input.positionWS.xz * _ScrollMap_ST.xy + _ScrollMap_ST.zw);
                float2 uvSS = GetNormalizedScreenSpaceUV(input.positionCS);

                float3 viewVector = _WorldSpaceCameraPos.xyz - input.positionWS.xyz;
                float3 normal = WhirlpoolNormal(normalize(UnpackRGBNormal(sample.rgb)), input.positionWS.xz);
                bool aboveSurface = _CameraAboveWater > .5;
                normal = dot(normal, viewVector) >= 0 ? normal : -normal;

                float2 uvRefracted = uvSS + normal.xz * _Refraction;
                float sceneDepth = _SourceDepth.SampleLevel(_pointClampSampler, uvSS, 0).r;
                float refractedDepth = _SourceDepth.SampleLevel(_pointClampSampler, uvRefracted, 0).r;

                float invFoamMask = smoothstep(_FoamEdgeLow, _FoamEdgeHigh, LinearEyeDepth(sceneDepth, _ZBufferParams) - input.positionSS.w);
                float3 foamColor = _FoamMask.Sample(_bilinearRepeatSampler, input.positionWS.xz * _FoamMask_ST.xy + _FoamMask_ST.zw * _OceanWaveTime).rgb;

                float depthOptions[2] = {sceneDepth, refractedDepth};
                float2 uvOptions[2] = {uvSS, uvRefracted};

                bool isRefraction = LinearEyeDepth(refractedDepth, _ZBufferParams) > input.positionSS.w;
                sceneDepth = depthOptions[isRefraction];
                uvSS = uvOptions[isRefraction];

                float waterDepth = _ProjectionParams.y + LinearEyeDepth(sceneDepth, _ZBufferParams) - input.positionSS.w;
                float3 sceneColor = _SourceColor.SampleLevel(_pointClampSampler, uvSS, 0).rgb;

                float3 viewDir = normalize(viewVector);

                float frensel = 1 - saturate(dot(viewDir, normal));
                float fadeMask = pow(frensel, _FresnelFade) * _FadeIntensity;
                float waterMask = pow(frensel, _FresnelWater);
                float windowMask = smoothstep(_WindowEdgeLow, _WindowEdgeHigh, 1 - waterMask);

                float3 surfaceColor = lerp(_Color.rgb * _Color.rgb, _Color.rgb, waterMask);
                float specularMask = pow(SpecularBRF(viewDir, normal, _MainLightPosition.xyz), pow(2, _GlareSpecular * 8)) * _GlareIntensity * (1 - fadeMask);
                specularMask = smoothstep(_GlareEdgeLow, _GlareEdgeHigh, specularMask);

                float depthMask = 1 - saturate(waterDepth);
                surfaceColor = lerp(surfaceColor, sceneColor, windowMask * (1 - aboveSurface));
                surfaceColor = lerp(surfaceColor, sceneColor, depthMask * aboveSurface);
                float crest = smoothstep(.60, .66, sample.a) * .9;
                float2 vortexDelta = input.positionWS.xz - _WhirlpoolCenter.xz;
                float vortexDistance = length(vortexDelta);
                float phase = atan2(vortexDelta.y, vortexDelta.x) * 3 - vortexDistance / max(_Whirlpool.x, .001) * _Whirlpool.z * 6.283185 + _OceanWaveTime * .48;
                float vortexRatio = vortexDistance / max(_Whirlpool.x, .001);
                float vortexMask = smoothstep(.015, .06, vortexRatio) * (1 - smoothstep(.97, 1, vortexRatio)) * step(.01, _Whirlpool.y);
                float spiral = smoothstep(.92, .99, saturate(cos(phase) * .5 + .5)) * vortexMask;
                float foamNoiseRaw = _FoamMask.Sample(_bilinearRepeatSampler, input.positionWS.xz * _FoamMask_ST.xy * .18 + _FoamMask_ST.zw * _OceanWaveTime * .5).r;
                float foamNoiseMask = smoothstep(.04, .26, foamNoiseRaw);
                float spiralNoise = smoothstep(.12, .78, foamNoiseMask);
                float rim = (1 - smoothstep(1.5, 5.0, abs(vortexDistance - _Whirlpool.x))) * step(.01, _Whirlpool.y);
                float foam = saturate(max(max(max(1 - invFoamMask, crest) * foamColor.r, spiral * .6 * spiralNoise), max(rim * .22 * spiralNoise, WaterShipFoamMask(input.positionWS.xyz, foamNoiseMask))));
                surfaceColor = saturate(lerp(surfaceColor, _ColorFade.rgb, fadeMask) + specularMask * aboveSurface);
                surfaceColor = lerp(surfaceColor, float3(.87, .95, .94), foam);

                surfaceColor = MixFog(surfaceColor, ComputeFogFactor(TransformWorldToHClip(input.positionWS.xyz).z));
                return float4(surfaceColor, 1);
            }
            ENDHLSL
        }
    }
}
