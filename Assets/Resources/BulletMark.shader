Shader "PirateSlop/BulletMark"
{
    Properties { _BaseColor("Color", Color) = (0.15,0.1,0.06,0.9) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1;
                float angle=atan2(p.y,p.x);
                float edge=0.78+0.09*sin(angle*7)+0.05*sin(angle*13+1.3);
                float radius=length(p)/edge;
                float grain=frac(sin(dot(floor(input.uv*128),float2(12.9898,78.233)))*43758.5453);
                float core=1-smoothstep(0.28,0.5,radius);
                float soot=(1-smoothstep(0.4,1,radius))*(0.22+grain*0.2);
                half alpha=saturate(core+soot)*_BaseColor.a;
                clip(alpha-0.015);
                return half4(_BaseColor.rgb*lerp(1.3,0.35,core),alpha);
            }
            ENDHLSL
        }
    }
}
