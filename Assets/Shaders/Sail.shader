Shader "PirateSlop/Sail"
{
    Properties
    {
        _BaseColor ("Sail Color", Color) = (1, 1, 1, 1)
        _FabricTint ("Fabric Tint", Color) = (1, 1, 1, 1)
        _UseFaceOrientation ("Use Face Orientation", Float) = 0
        _UseDecalUV2 ("Use Decal UV2", Float) = 0
        _DecalCanvasScale ("Decal Canvas Proportions", Vector) = (1, 1, 0, 0)
        _PreserveDecalColor ("Preserve Decal Color", Float) = 0
        _OcclusionMap ("Ambient Occlusion (Folds)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 0.45
        _Weathering ("Weathering / Wear", Range(0, 1)) = 0.0
        _Grime ("Grime / Dirt", Range(0, 1)) = 0.0

        _DecalTex ("Decal Texture 1", 2D) = "black" {}
        _DecalColor ("Decal Color Tint 1", Color) = (1, 1, 1, 1)
        _DecalTransform ("Decal Scale 1 (XY) and Offset (ZW)", Vector) = (1, 1, 0, 0)
        _DecalRotation ("Decal Rotation 1 (Degrees)", Float) = 0
        _DecalImageScale ("Decal Image Proportions 1", Vector) = (1, 1, 0, 0)
        _BlendMode ("Blend Mode 1", Float) = 0.0

        _DecalTex2 ("Decal Texture 2", 2D) = "black" {}
        _DecalColor2 ("Decal Color Tint 2", Color) = (1, 1, 1, 1)
        _DecalTransform2 ("Decal Scale 2 (XY) and Offset (ZW)", Vector) = (1, 1, 0, 0)
        _DecalRotation2 ("Decal Rotation 2 (Degrees)", Float) = 0
        _DecalImageScale2 ("Decal Image Proportions 2", Vector) = (1, 1, 0, 0)
        _BlendMode2 ("Blend Mode 2", Float) = 0.0

        _SailUVBoundsFront ("Front Sail UV Bounds (Min XY, Size ZW)", Vector) = (0, 0, 1, 1)
        _SailUVBoundsBack ("Back Sail UV Bounds (Min XY, Size ZW)", Vector) = (0, 0, 1, 1)
        _MirrorBack ("Mirror Decal on Back", Float) = 1.0
        _HighlightColor ("Highlight / Emission", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_DecalTex);
            SAMPLER(sampler_DecalTex);
            TEXTURE2D(_DecalTex2);
            SAMPLER(sampler_DecalTex2);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FabricTint;
                float _UseFaceOrientation;
                float _UseDecalUV2;
                float4 _DecalCanvasScale;
                float _PreserveDecalColor;
                half4 _DecalColor;
                float4 _DecalTransform;
                float _DecalRotation;
                float4 _DecalImageScale;
                float _BlendMode;

                half4 _DecalColor2;
                float4 _DecalTransform2;
                float _DecalRotation2;
                float4 _DecalImageScale2;
                float _BlendMode2;

                float4 _SailUVBoundsFront;
                float4 _SailUVBoundsBack;
                float _MirrorBack;
                half4 _HighlightColor;
                half _OcclusionStrength;
                half _Weathering;
                half _Grime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 decalUV : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fog : TEXCOORD3;
                float isBack : TEXCOORD4;
                float2 decalUV : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.worldPos);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.decalUV = input.decalUV;
                output.fog = ComputeFogFactor(output.positionCS.z);
                float dominantNormal = abs(input.normalOS.z) >= abs(input.normalOS.x) ? input.normalOS.z : input.normalOS.x;
                output.isBack = dominantNormal < 0.0 ? 1.0 : 0.0;
                return output;
            }

            void EvaluateDecal(
                inout half3 colorAccum,
                inout half preservedCoverage,
                float2 normUV,
                bool isBack,
                float mirrorBack,
                Texture2D dTex,
                SamplerState dSampler,
                float4 dTransform,
                float2 imageScale,
                float dRotation,
                half4 dColor,
                float dBlendMode,
                half weathering,
                half3 ao)
            {
                if (dColor.a <= 0.001) return;

                float2 centered = (normUV - 0.5) * _DecalCanvasScale.xy;
                if (isBack && mirrorBack > 0.5) centered.x = -centered.x;

                float rad = dRotation * (3.14159265359 / 180.0);
                if (isBack && mirrorBack > 0.5) rad = -rad;

                float c = cos(rad);
                float s = sin(rad);
                float2 rotated = float2(c * centered.x - s * centered.y, s * centered.x + c * centered.y);

                float2 scale = max(dTransform.xy * imageScale, float2(0.0001, 0.0001));
                float2 offset = dTransform.zw;
                if (isBack && mirrorBack > 0.5) offset.x = -offset.x;

                float2 decalUV = (rotated / scale) - offset + 0.5;

                if (decalUV.x >= 0.0 && decalUV.x <= 1.0 && decalUV.y >= 0.0 && decalUV.y <= 1.0)
                {
                    half4 decalSample = SAMPLE_TEXTURE2D(dTex, dSampler, decalUV);
                    if (decalSample.a > 0.001)
                    {
                        half effectiveAlpha = decalSample.a * dColor.a;

                        if (weathering > 0.005)
                        {
                            half foldErosion = lerp(1.0, ao.r, 0.65);
                            half hashNoise = frac(sin(dot(normUV * 311.7, float2(12.9898, 78.233))) * 43758.5453);
                            half wearMask = foldErosion * lerp(0.75, 1.25, hashNoise);
                            effectiveAlpha *= saturate(1.0 - weathering * (1.6 - wearMask * 1.3));
                        }

                        if (dBlendMode > 2.5 && dBlendMode < 3.5)
                        {
                            half brightness = max(decalSample.r, max(decalSample.g, decalSample.b));
                            effectiveAlpha *= saturate((0.92 - brightness) * 6.0);
                        }
                        else if (dBlendMode > 3.5)
                        {
                            half brightness = max(decalSample.r, max(decalSample.g, decalSample.b));
                            effectiveAlpha *= saturate((brightness - 0.08) * 6.0);
                        }

                        half3 tinted = decalSample.rgb * dColor.rgb;
                        if (weathering > 0.005)
                        {
                            tinted = lerp(tinted, tinted * half3(0.85, 0.82, 0.75), weathering * 0.5);
                        }

                        if (dBlendMode > 0.5 && dBlendMode < 1.5)
                        {
                            preservedCoverage *= 1.0 - effectiveAlpha;
                            half3 mult = colorAccum * tinted;
                            colorAccum = lerp(colorAccum, mult, effectiveAlpha);
                        }
                        else if (dBlendMode > 1.5 && dBlendMode < 2.5)
                        {
                            preservedCoverage *= 1.0 - effectiveAlpha;
                            half3 scr = 1.0 - (1.0 - colorAccum) * (1.0 - tinted);
                            colorAccum = lerp(colorAccum, scr, effectiveAlpha);
                        }
                        else
                        {
                            preservedCoverage = lerp(preservedCoverage, 1.0, effectiveAlpha);
                            colorAccum = lerp(colorAccum, tinted, effectiveAlpha);
                        }
                    }
                }
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                float3 normal = normalize(isFrontFace ? input.normalWS : -input.normalWS);

                half3 ao = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).rgb;
                half3 clothColor = _BaseColor.rgb * _FabricTint.rgb * lerp(half3(1.0, 1.0, 1.0), ao, _OcclusionStrength);

                float4 uvBounds = input.isBack > 0.5 ? _SailUVBoundsBack : _SailUVBoundsFront;
                float2 uvSize = uvBounds.zw;
                if (abs(uvSize.x) < 0.0001) uvSize.x = 1.0;
                if (abs(uvSize.y) < 0.0001) uvSize.y = 1.0;
                float2 normUV = (input.uv - uvBounds.xy) / uvSize;
                if (_UseDecalUV2 > 0.5) normUV = input.decalUV;
                bool isBack = _UseFaceOrientation > 0.5 ? !isFrontFace : input.isBack > 0.5;
                half preservedCoverage = 0.0;

                // Layer 1
                EvaluateDecal(clothColor, preservedCoverage, normUV, isBack, _MirrorBack, _DecalTex, sampler_DecalTex, _DecalTransform, _DecalImageScale.xy, _DecalRotation, _DecalColor, _BlendMode, _Weathering, ao);

                // Layer 2
                EvaluateDecal(clothColor, preservedCoverage, normUV, isBack, _MirrorBack, _DecalTex2, sampler_DecalTex2, _DecalTransform2, _DecalImageScale2.xy, _DecalRotation2, _DecalColor2, _BlendMode2, _Weathering, ao);

                // Grime & Dirt
                if (_Grime > 0.005)
                {
                    half grimeMask = (1.0 - ao.r) * 1.7;
                    half3 dirtTint = half3(0.24, 0.18, 0.12);
                    clothColor = lerp(clothColor, clothColor * dirtTint * 1.6, saturate(_Grime * grimeMask));
                }

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.worldPos));
                half NdotL = saturate(dot(normal, mainLight.direction));
                half3 diffuse = mainLight.color * (NdotL * mainLight.shadowAttenuation);
                half3 ambient = SampleSH(normal) * 1.2;

                half3 lighting = diffuse + ambient;
                half brightness = dot(lighting, half3(0.2126, 0.7152, 0.0722));
                half decalBrightness = clamp(brightness, 0.82, 1.0);
                half3 shaded = lerp(clothColor * lighting, clothColor * decalBrightness, preservedCoverage * _PreserveDecalColor) + _HighlightColor.rgb * 1.5;
                return half4(MixFog(shaded, input.fog), 1.0);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
