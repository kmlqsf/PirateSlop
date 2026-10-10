Shader "PirateSlop/CoastalTripoRock"
{
    Properties
    {
        [MainTexture] _BaseMap ("Native colour", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour multiplier", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Native tangent normal", 2D) = "bump" {}
        _NormalScale ("Normal strength", Range(0,2)) = 1
        _MetallicRoughnessMap ("Native roughness G metallic B", 2D) = "white" {}
        _RoughnessFactor ("Roughness multiplier", Range(0,2)) = 1
        _MetallicFactor ("Metallic multiplier", Range(0,1)) = 1
        _WetBandHeight ("Wet height above sea", Range(0,3)) = .9
        _WetBandSoftness ("Wet edge softness", Range(.05,1.5)) = .35
        _WetIrregularity ("Wet height variation", Range(0,1)) = .25
        _WetDarkening ("Wet darkening", Range(0,.5)) = .16
        _WetSmoothness ("Wet smoothness", Range(0,1)) = .3
        _MossColor ("Additional moss", Color) = (.22,.29,.13,1)
        _MossStrength ("Additional moss strength", Range(0,.25)) = 0
        _StreakStrength ("Additional streak strength", Range(0,.2)) = 0
        _CapRepair ("Exposed underside repair", Range(0,1)) = 0
        _CapMap ("Cap stone detail", 2D) = "white" {}
        [Normal] _CapBumpMap ("Cap stone normal", 2D) = "bump" {}
        _CapUVRect ("Cap detail atlas rectangle", Vector) = (.257,.34,.061,.085)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        Cull Back

        HLSLINCLUDE
        #define _NORMALMAP 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicRoughnessMap);
        SAMPLER(sampler_MetallicRoughnessMap);
        TEXTURE2D(_CapMap);
        SAMPLER(sampler_CapMap);
        TEXTURE2D(_CapBumpMap);
        SAMPLER(sampler_CapBumpMap);

        float _CoastalSeaLevel;

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _MossColor;
            half _NormalScale;
            half _RoughnessFactor;
            half _MetallicFactor;
            half _WetBandHeight;
            half _WetBandSoftness;
            half _WetIrregularity;
            half _WetDarkening;
            half _WetSmoothness;
            half _MossStrength;
            half _StreakStrength;
            half _CapRepair;
            float4 _CapUVRect;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            float2 staticLightmapUV : TEXCOORD1;
            float2 dynamicLightmapUV : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half4 tangentWS : TEXCOORD3;
            half4 fogAndVertexLight : TEXCOORD4;
            half2 weathering : TEXCOORD5;
            DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 6);
            #if defined(DYNAMICLIGHTMAP_ON)
                float2 dynamicLightmapUV : TEXCOORD7;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD8;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
                float4 probeOcclusion : TEXCOORD9;
            #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct DepthVaryings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct NormalVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 tangentWS : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float CoastalHash(float2 p)
        {
            float3 q = frac(float3(p.x, p.y, p.x) * .1031);
            q += dot(q, q.yzx + 33.33);
            return frac((q.x + q.y) * q.z);
        }

        float CoastalNoise(float2 p)
        {
            float2 cell = floor(p);
            float2 weight = frac(p);
            weight = weight * weight * (3 - 2 * weight);
            float a = CoastalHash(cell);
            float b = CoastalHash(cell + float2(1,0));
            float c = CoastalHash(cell + float2(0,1));
            float d = CoastalHash(cell + float2(1,1));
            return lerp(lerp(a,b,weight.x),lerp(c,d,weight.x),weight.y);
        }

        half3 CoastalNormalWS(float2 uv, half3 normalWS, half4 tangentWS, out half3 normalTS)
        {
            normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _NormalScale);
            half3 bitangentWS = tangentWS.w * cross(normalWS, tangentWS.xyz);
            return NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, half3x3(tangentWS.xyz, bitangentWS, normalWS)));
        }

        Varyings CoastalForwardVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.normalWS = normal.normalWS;
            output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            #if !defined(_FOG_FRAGMENT)
                output.fogAndVertexLight.x = ComputeFogFactor(position.positionCS.z);
            #endif
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                output.fogAndVertexLight.yzw = VertexLighting(position.positionWS, normal.normalWS);
            #endif
            output.weathering = half2(CoastalNoise(position.positionWS.xz * .2), CoastalNoise(position.positionWS.xz * .65 + 17.3));
            OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
            #if defined(DYNAMICLIGHTMAP_ON)
                output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
            #endif
            OUTPUT_SH4(position.positionWS, normal.normalWS, GetWorldSpaceNormalizeViewDir(position.positionWS), output.vertexSH, output.probeOcclusion);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(position);
            #endif
            return output;
        }

        void CoastalForwardFragment(Varyings input, out half4 outColor : SV_Target0
            #if defined(_WRITE_RENDERING_LAYERS)
                , out uint outRenderingLayers : SV_Target1
            #endif
        )
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
            half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
            half4 packed = SAMPLE_TEXTURE2D(_MetallicRoughnessMap, sampler_MetallicRoughnessMap, input.uv);
            half roughness = saturate(packed.g * _RoughnessFactor);
            half3 normalTS;
            half3 normalWS = CoastalNormalWS(input.uv, input.normalWS, input.tangentWS, normalTS);
            if (_CapRepair > .001)
            {
                half brightness = dot(albedo, half3(.2126,.7152,.0722));
                half bare = (1 - smoothstep(.12,.30,brightness)) * smoothstep(.008,.028,albedo.r - albedo.b);
                half cap = _CapRepair * bare * smoothstep(.5,.85,normalize(input.normalWS).y);
                if (cap > .001)
                {
                    float2 plane = input.positionWS.xz * .09;
                    plane += float2(input.weathering.x, input.weathering.y) * .12;
                    float2 capUV = _CapUVRect.xy + (1 - abs(frac(plane) * 2 - 1)) * _CapUVRect.zw;
                    half3 stone = SAMPLE_TEXTURE2D(_CapMap, sampler_CapMap, capUV).rgb * _BaseColor.rgb;
                    half3 detail = UnpackNormalScale(SAMPLE_TEXTURE2D(_CapBumpMap, sampler_CapBumpMap, capUV), .65);
                    half3 capNormal = normalize(half3(detail.x, detail.z, detail.y));
                    albedo = lerp(albedo, stone, cap);
                    normalWS = normalize(lerp(normalWS, capNormal, cap * .65));
                    roughness = lerp(roughness, .85, cap);
                }
            }
            float relativeHeight = input.positionWS.y - _CoastalSeaLevel;
            float wetEdge = _WetBandHeight + (input.weathering.x * 2 - 1) * _WetIrregularity;
            half wet = 1 - smoothstep(wetEdge - _WetBandSoftness, wetEdge + _WetBandSoftness, relativeHeight);
            half vertical = saturate(1 - abs(input.normalWS.y));
            half darkness = saturate((.45 - dot(albedo, half3(.2126,.7152,.0722))) * 4);
            half aboveWater = smoothstep(0,.7,relativeHeight) * (1 - smoothstep(3,7,relativeHeight));
            half moss = _MossStrength * darkness * roughness * aboveWater * saturate((input.weathering.x - .5) * 4);
            half streak = _StreakStrength * darkness * vertical * aboveWater * saturate((input.weathering.y - .65) * 5);
            albedo = lerp(albedo, albedo * _MossColor.rgb, moss);
            albedo *= 1 - wet * _WetDarkening - streak;
            half drySmoothness = 1 - roughness;

            SurfaceData surface = (SurfaceData)0;
            surface.albedo = albedo;
            surface.metallic = saturate(packed.b * _MetallicFactor);
            surface.smoothness = lerp(drySmoothness, max(drySmoothness, _WetSmoothness), wet);
            surface.normalTS = normalTS;
            surface.occlusion = 1;
            surface.alpha = 1;

            InputData data = (InputData)0;
            data.positionWS = input.positionWS;
            data.normalWS = normalWS;
            data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                data.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #endif
            data.fogCoord = InitializeInputDataFog(float4(input.positionWS,1), input.fogAndVertexLight.x);
            data.vertexLighting = input.fogAndVertexLight.yzw;
            data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            #if defined(DYNAMICLIGHTMAP_ON)
                data.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, normalWS);
                data.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
            #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                data.bakedGI = SAMPLE_GI(input.vertexSH, GetAbsolutePositionWS(input.positionWS), normalWS, data.viewDirectionWS, input.positionCS.xy, input.probeOcclusion, data.shadowMask);
            #else
                data.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
                data.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
            #endif
            outColor = UniversalFragmentPBR(data, surface);
            outColor.rgb = MixFog(outColor.rgb, data.fogCoord);
            #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
        }

        DepthVaryings CoastalDepthVertex(Attributes input)
        {
            DepthVaryings output = (DepthVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            return output;
        }

        half CoastalDepthFragment(DepthVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
            return input.positionCS.z;
        }

        NormalVaryings CoastalNormalVertex(Attributes input)
        {
            NormalVaryings output = (NormalVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.normalWS = normal.normalWS;
            output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            return output;
        }

        void CoastalNormalFragment(NormalVaryings input, out half4 outNormal : SV_Target0
            #if defined(_WRITE_RENDERING_LAYERS)
                , out uint outRenderingLayers : SV_Target1
            #endif
        )
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
            half3 normalTS;
            half3 normalWS = CoastalNormalWS(input.uv, input.normalWS, input.tangentWS, normalTS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(normalWS);
                outNormal = half4(PackFloat2To888(saturate(oct * .5 + .5)),0);
            #else
                outNormal = half4(normalWS,0);
            #endif
            #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CoastalForwardVertex
            #pragma fragment CoastalForwardFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CoastalShadowVertex
            #pragma fragment CoastalShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            float3 _LightDirection;
            float3 _LightPosition;

            DepthVaryings CoastalShadowVertex(Attributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection)));
                return output;
            }

            half4 CoastalShadowFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CoastalDepthVertex
            #pragma fragment CoastalDepthFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CoastalNormalVertex
            #pragma fragment CoastalNormalFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
