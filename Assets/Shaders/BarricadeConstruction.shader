Shader "PirateSlop/Barricade Construction"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)
        _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        _MetallicGlossMap("Metallic Smoothness", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 1
        _Smoothness("Smoothness", Range(0,1)) = .45
        _SpecColor("Specular", Color) = (.2,.2,.2,1)
        _EmissionColor("Emission", Color) = (0,0,0,0)
        _Cutoff("Cutoff", Range(0,1)) = .5
        _OcclusionStrength("Occlusion", Range(0,1)) = 1
        _ConstructionTint("Preview Color", Color) = (.15,.9,.45,.55)
        _ConstructionProgress("Progress", Range(0,1)) = 0
        _ConstructionHeight("Height", Float) = 2.1
        _ConstructionFeather("Feather", Float) = .045
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Construction"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment ConstructionFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _METALLICSPECGLOSSMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "BarricadeWood.hlsl"
            float4x4 _ConstructionWorldToLocal;
            half4 _ConstructionTint;
            float _ConstructionProgress;
            float _ConstructionHeight;
            float _ConstructionFeather;
            void ConstructionFragment(Varyings input, out half4 color : SV_Target0)
            {
                color = BarricadeWoodFragment(input);
                float height = mul(_ConstructionWorldToLocal, float4(input.positionWS, 1)).y;
                float edge = lerp(-_ConstructionFeather, _ConstructionHeight + _ConstructionFeather, saturate(_ConstructionProgress));
                half restored = 1 - smoothstep(edge - _ConstructionFeather, edge + _ConstructionFeather, height);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half fog = input.fogFactorAndVertexLight.x;
                #else
                half fog = input.fogFactor;
                #endif
                half3 green = MixFog(_ConstructionTint.rgb, fog);
                color.rgb = lerp(green, color.rgb, restored);
                color.a = lerp(_ConstructionTint.a, color.a, restored);
            }
            ENDHLSL
        }
    }
}
