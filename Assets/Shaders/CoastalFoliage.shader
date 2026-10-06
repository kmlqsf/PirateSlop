Shader "PirateSlop/CoastalFoliage"
{
    Properties
    {
        _BaseMap ("Colour", 2D) = "white" {}
        _AlphaMap ("Leaf mask", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BaseColor ("Tint", Color) = (.85,.90,.73,1)
        _Cutoff ("Leaf cutoff", Range(0,1)) = .4
        _NormalScale ("Normal strength", Range(0,2)) = .5
        _LeafTintStrength ("Leaf colour balance", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor;
            float _Cutoff, _NormalScale, _LeafTintStrength;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float4 tangentWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            float fog : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,output);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS=p.positionCS;
            output.positionWS=p.positionWS;
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);
            output.tangentWS=float4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w*GetOddNegativeScale());
            output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
            output.fog=ComputeFogFactor(p.positionCS.z);
            return output;
        }
        half4 LeafSample(float2 uv)
        {
            half4 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv)*_BaseColor;
            clip(color.a*SAMPLE_TEXTURE2D(_AlphaMap,sampler_AlphaMap,uv).r-_Cutoff);
            return color;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 color=LeafSample(input.uv).rgb;
                float green=smoothstep(.015,.09,color.g-color.r);
                color=lerp(color,saturate(color*half3(1.05,1.28,.95)),green);
                float luminance=dot(color,half3(.2126,.7152,.0722));
                half3 leafColor=half3(.35,.52,.16)*clamp(luminance*3.5,.55,1.45);
                float leafMask=smoothstep(.005,.045,color.g-color.r*.85);
                color=lerp(color,leafColor,_LeafTintStrength*leafMask);
                float3 normal=normalize(input.normalWS)*IS_FRONT_VFACE(face,1,-1);
                float3 tangent=normalize(input.tangentWS.xyz);
                float3 bitangent=cross(normal,tangent)*input.tangentWS.w;
                float3 detail=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,input.uv),_NormalScale);
                normal=normalize(tangent*detail.x+bitangent*detail.y+normal*detail.z);
                InputData data=(InputData)0;
                data.positionWS=input.positionWS;
                data.normalWS=normal;
                data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI=SampleSH(normal);
                data.shadowMask=half4(1,1,1,1);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=color;
                surface.smoothness=.17;
                surface.occlusion=1;
                surface.alpha=1;
                surface.normalTS=half3(0,0,1);
                half4 result=UniversalFragmentPBR(data,surface);
                result.rgb=MixFog(result.rgb,input.fog);
                return result;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment MaskFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output=Vert(input);
                float3 direction=_LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                direction=normalize(_LightPosition-output.positionWS);
                #endif
                output.positionCS=TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,direction));
                #if UNITY_REVERSED_Z
                output.positionCS.z=min(output.positionCS.z,UNITY_NEAR_CLIP_VALUE*output.positionCS.w);
                #else
                output.positionCS.z=max(output.positionCS.z,UNITY_NEAR_CLIP_VALUE*output.positionCS.w);
                #endif
                return output;
            }
            half4 MaskFrag(Varyings input) : SV_Target { LeafSample(input.uv); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings input) : SV_Target { LeafSample(input.uv); return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_instancing
            half4 NormalFrag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                LeafSample(input.uv);
                return half4(normalize(input.normalWS)*IS_FRONT_VFACE(face,1,-1),0);
            }
            ENDHLSL
        }
    }
}
