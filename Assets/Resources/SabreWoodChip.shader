Shader "PirateSlop/SabreWoodChip"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; o.color=v.color; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;
                float width=.28*(1-.45*abs(p.y));
                float shape=(1-smoothstep(width-.03,width+.03,abs(p.x)))*(1-smoothstep(.8,1,abs(p.y)));
                return half4(i.color.rgb*(.78+.22*saturate(p.x/width)),i.color.a*shape);
            }
            ENDHLSL
        }
    }
}
