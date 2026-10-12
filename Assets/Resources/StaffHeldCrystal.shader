Shader "PirateSlop/StaffHeldCrystal"
{
    Properties
    {
        _BaseMap("Model Color", 2D) = "white" {}
        _Theme("Element", Float) = 0
        _Pulse("Pulse", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-5" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4x4 _HeadFromWorld;
                float _Theme;
                float _Pulse;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 head : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.head = mul(_HeadFromWorld, float4(world, 1)).xyz;
                o.uv = input.uv;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float shape = 1 - smoothstep(.65, 1.2, length(input.head / float3(.19, .24, .17)));
                clip(shape - .005);
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                float mask;
                half3 color;
                if (_Theme < .5)
                {
                    mask = smoothstep(.02, .16, base.r - max(base.g, base.b) * 1.35);
                    color = half3(.95, .13, .012);
                }
                else if (_Theme < 1.5)
                {
                    mask = smoothstep(.025, .13, base.b - base.r) * smoothstep(.06, .3, base.g);
                    color = half3(.12, .43, .7);
                }
                else
                {
                    mask = smoothstep(.015, .12, min(base.r, base.b) - base.g * 1.2);
                    color = half3(.43, .12, .85);
                }
                float facets = .45 + .55 * saturate(max(base.r, max(base.g, base.b)) * 2);
                return half4(color * mask * shape * facets * _Pulse * .65, 0);
            }
            ENDHLSL
        }
    }
}
