Shader "PirateSlop/BRZoneV2/GeometryDebug"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 Vert(float4 p:POSITION):SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half4 Frag():SV_Target { return half4(1,0,1,1); }
            ENDHLSL
        }
    }
}
