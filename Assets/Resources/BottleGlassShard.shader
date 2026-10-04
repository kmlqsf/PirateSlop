Shader "PirateSlop/GlassShard"
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
                float width=.55*(1-p.y);
                float edge=min(width-abs(p.x),p.y+.85);
                float alpha=smoothstep(0,.04,edge);
                float highlight=1-smoothstep(.02,.12,edge);
                return half4(lerp(i.color.rgb,half3(.95,1,1),highlight*.7),i.color.a*alpha);
            }
            ENDHLSL
        }
    }
}
