Shader "PirateSlop/Ship Mast Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo",2D)="white"{}
        [MainColor] _BaseColor("Color",Color)=(1,1,1,1)
        _BumpMap("Normal",2D)="bump"{}
        _BumpScale("Normal Strength",Float)=1
        _MetallicGlossMap("Metallic Smoothness",2D)="white"{}
        _Metallic("Metallic",Range(0,1))=0
        _Smoothness("Smoothness",Range(0,1))=.4
        _OcclusionMap("Occlusion",2D)="white"{}
        _OcclusionStrength("Occlusion Strength",Range(0,1))=1
        _EmissionMap("Emission",2D)="white"{}
        _EmissionColor("Emission Color",Color)=(0,0,0,1)
        _Cutoff("Cutoff",Range(0,1))=.5
        _Cull("Cull",Float)=2
        [HideInInspector] _Surface("Surface",Float)=0
        [HideInInspector] _ZWrite("ZWrite",Float)=1
        [HideInInspector] _SrcBlend("Source Blend",Float)=1
        [HideInInspector] _DstBlend("Destination Blend",Float)=0
        [HideInInspector] _SrcBlendAlpha("Source Alpha",Float)=1
        [HideInInspector] _DstBlendAlpha("Destination Alpha",Float)=0
        [HideInInspector] _AlphaToMask("Alpha To Mask",Float)=0
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit"}
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #define InitializeStandardLitSurfaceData InitializeOriginalMastSurfaceData
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #undef InitializeStandardLitSurfaceData
            void InitializeStandardLitSurfaceData(float2 uv,out SurfaceData data)
            {
                InitializeOriginalMastSurfaceData(uv,data);
                half3 broadColor=SAMPLE_TEXTURE2D_LOD(_BaseMap,sampler_BaseMap,uv,6).rgb;
                half3 correction=clamp(half3(.16,.105,.065)/max(broadColor,half3(.025,.025,.025)),.45,2.2);
                half wood=1-smoothstep(.15,.65,data.metallic);
                data.albedo*=lerp(half3(1,1,1),correction,wood*.85);
            }
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
