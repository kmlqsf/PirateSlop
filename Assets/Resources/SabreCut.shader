Shader "PirateSlop/SabreCut"
{
    Properties
    {
        _BaseColor("Color", Color) = (.12,.055,.02,1)
        _Seed("Seed", Float) = 0
    }
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
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Seed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;
                float taper=saturate(1-p.x*p.x);
                float jagged=.06*sin(p.x*73+_Seed)+.035*sin(p.x*137-_Seed);
                float center=.06*sin(p.x*9+_Seed);
                float distance=abs(p.y-center);
                float edge=taper*(.6+jagged);
                float alpha=1-smoothstep(edge-.12,edge+.08,distance);
                float rim=smoothstep(edge*.45,edge*.8,distance);
                float3 color=lerp(_BaseColor.rgb,float3(.48,.29,.12),rim*.65);
                color*=.85+.15*sin(p.x*41+_Seed);
                return half4(color,alpha*_BaseColor.a*taper);
            }
            ENDHLSL
        }
    }
}
