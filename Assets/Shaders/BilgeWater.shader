Shader "PirateSlop/Bilge Water"
{
    Properties
    {
        [NoScaleOffset] _SurfaceNormals("Ocean normals", 2D) = "gray" {}
        [NoScaleOffset] _FoamMap("Ocean foam", 2D) = "black" {}
        [NoScaleOffset] _CubemapTexture("Ocean reflection", Cube) = "" {}
        _AbsorptionColor("Absorption", Color) = (.1,.46,.42,1)
        _ScatteringColor("Scattering", Color) = (.008,.12,.105,1)
        _MaxDepth("Visibility", Float) = 15
        _BoatAttack_Lighting("Lighting", Vector) = (1,1,1,0)
        _BoatAttack_Water_MicroWaveIntensity("Detail", Float) = 1
        _OpticalDensity("Interior water density", Range(1,8)) = 5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "BilgeWaterOptics.hlsl"
            TEXTURE2D(_WidthProfile); SAMPLER(sampler_WidthProfile);
            CBUFFER_START(UnityPerMaterial)
            float4 _ProfileBounds,_BilgeSlope;
            float _BilgeTime,_Slosh,_OpticalDensity;
            CBUFFER_END
            struct BilgeAttributes { float4 positionOS:POSITION; };
            struct BilgeVaryings { float4 positionCS:SV_POSITION; float3 local:TEXCOORD0; float3 world:TEXCOORD1; };
            BilgeVaryings Vert(BilgeAttributes i)
            {
                BilgeVaryings o;o.local=i.positionOS.xyz;o.world=TransformObjectToWorld(o.local);o.positionCS=TransformWorldToHClip(o.world);return o;
            }
            half4 Frag(BilgeVaryings i,bool frontFace:SV_IsFrontFace):SV_Target
            {
                float2 mapping=float2((i.local.z-_ProfileBounds.x)/(_ProfileBounds.y-_ProfileBounds.x),(i.local.y-_ProfileBounds.z)/(_ProfileBounds.w-_ProfileBounds.z));
                float width=SAMPLE_TEXTURE2D(_WidthProfile,sampler_WidthProfile,saturate(mapping)).r;
                float edge=width-abs(i.local.x);
                clip(edge+.025);
                float2 detail=BilgeDetail(i.local.xz*.24,_BilgeTime)*min(.23,max(.12,_BoatAttack_Water_MicroWaveIntensity*.15));
                float3 normal=TransformObjectToWorldNormal(normalize(float3(-_BilgeSlope.x+detail.x,1,-_BilgeSlope.y+detail.y)));
                float foamTexture=BilgeFoam(i.local.xz*.5,_BilgeTime);
                float foam=(1-smoothstep(.015,.20+_Slosh*.28,edge))*smoothstep(.28,.72,foamTexture)*(.28+.72*_Slosh);
                foam+=smoothstep(.68,.91,foamTexture)*(.06+_Slosh*.1);
                float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
                return half4(BilgeShading(i.world,normal,screenUV,8,foam,!frontFace,_OpticalDensity,.08),1);
            }
            ENDHLSL
        }
    }
}
