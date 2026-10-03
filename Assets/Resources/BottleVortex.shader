Shader "PirateSlop/BottleVortex"
{
    Properties
    {
        _BaseColor ("Color", Color) = (.55, 1.1, 1.35, .65)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float edge = 1.0 - abs(input.uv.x * 2.0 - 1.0);
                float halo = smoothstep(0.0, .65, edge);
                float core = pow(saturate(edge), 5.0);
                float flow = .74 + .26 * sin(input.uv.y * 74.0 - _Time.y * 3.2);
                float alpha = input.color.a * _BaseColor.a * halo * flow;
                half3 color = input.color.rgb * _BaseColor.rgb + core * half3(.28, .38, .42);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
