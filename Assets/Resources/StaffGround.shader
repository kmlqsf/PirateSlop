Shader "PirateSlop/StaffGround"
{
    Properties { _Color("Color",Color)=(.45,.22,.85,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; return o; }
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1; float r=length(p), a=atan2(p.y,p.x);
                float ring=exp(-abs(r-.93)*180)+exp(-abs(r-.79)*250)*.28;
                float angle=frac(a*3.81972+_Time.y*.11);
                float ticks=(1-smoothstep(.035,.08,abs(angle-.5)))*smoothstep(.82,.85,r)*(1-smoothstep(.88,.9,r));
                float arc=pow(saturate(sin(a*7+_Time.y*2)*sin(r*34-a*3)),18)*smoothstep(.45,.8,r)*(1-smoothstep(.86,.92,r));
                float glow=exp(-abs(r-.9)*28)*.045;
                float strength=ring+ticks*.7+arc*.3;
                float alpha=saturate(strength*.65+glow);
                return half4((_Color.rgb*1.7+strength*.5)*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
