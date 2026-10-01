Shader "PirateSlop/LootEventBeam"
{
    Properties { [HDR] _BeamColor ("Light Color", Color) = (2.8, 2.15, 0.8, 0.45) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+110" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BeamColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float edge = smoothstep(0.0, 0.18, input.uv.x) * smoothstep(0.0, 0.18, 1.0 - input.uv.x);
                float height = smoothstep(0.0, 0.015, input.uv.y) * (1.0 - smoothstep(0.65, 1.0, input.uv.y));
                return half4(_BeamColor.rgb, _BeamColor.a * edge * height);
            }
            ENDHLSL
        }
    }
}
