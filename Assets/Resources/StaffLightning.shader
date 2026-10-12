Shader "PirateSlop/StaffLightning"
{
    Properties { _CoreStrength("Core", Float)=1 _GlowStrength("Glow", Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+18" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _CoreStrength, _GlowStrength;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            V vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; o.color=i.color; return o; }
            half4 frag(V i):SV_Target
            {
                float side=abs(i.uv.y*2-1);
                float core=1-smoothstep(.035,.22,side);
                float halo=pow(saturate(1-side),2);
                half3 color=i.color.rgb*halo*2.8*_GlowStrength+half3(.85,.92,1)*core*7*_CoreStrength;
                return half4(color*i.color.a,0);
            }
            ENDHLSL
        }
    }
}
