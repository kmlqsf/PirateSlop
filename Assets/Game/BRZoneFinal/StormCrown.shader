Shader "PirateSlop/StormCrown"
{
    Properties
    {
        _Density("Density",Range(0,1))=.72
        _BroadScale("Broad scale",Float)=.009
        _MediumScale("Medium scale",Float)=.025
        _FlowSpeed("Tangential speed",Float)=1.2
        _Waterline("Waterline",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent"}
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Density,_BroadScale,_MediumScale,_FlowSpeed,_Waterline;
            CBUFFER_END
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;};
            V Vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=i.uv;return o;}
            float Hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
            float Noise(float3 p)
            {
                float3 q=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(q),Hash(q+float3(1,0,0)),f.x),lerp(Hash(q+float3(0,1,0)),Hash(q+float3(1,1,0)),f.x),f.y),lerp(lerp(Hash(q+float3(0,0,1)),Hash(q+float3(1,0,1)),f.x),lerp(Hash(q+float3(0,1,1)),Hash(q+1),f.x),f.y),f.z);
            }
            half4 Frag(V i):SV_Target
            {
                float3 p=i.world-TransformObjectToWorld(float3(0,0,0));float angle=_Time.y*_FlowSpeed/max(length(p.xz),1);
                float2 flow=float2(p.x*cos(angle)-p.z*sin(angle),p.x*sin(angle)+p.z*cos(angle));
                float3 q=float3(flow.x,p.y-_Time.y*.18,flow.y)*_BroadScale;
                float broad=Noise(q);float middle=Noise(float3(flow.x,p.y+_Time.y*.12,flow.y)*_MediumScale+float3(broad,0,broad)*1.7);
                float mass=smoothstep(.22,.72,broad*.7+middle*.3);
                float light=saturate(.5+(Noise(q+float3(-.15,.3,.1))-broad)*3);
                float3 color=lerp(float3(.013,.023,.038),float3(.16,.20,.25),light*light)*lerp(.85,1.15,middle);
                float h=i.uv.y;
                float profile=lerp(.52,1,smoothstep(0,.12,h))*(1-smoothstep(.95,1,h));
                float alpha=_Density*lerp(.68,1,mass)*profile;
                if(_Waterline>.5)
                {
                    float n=Noise(i.world*.18+float3(_Time.y*.5,0,_Time.y*.2));
                    float strip=pow(saturate(sin(i.uv.y*3.14159265)),.7);
                    alpha=strip*lerp(.15,.65,smoothstep(.25,.7,n));color=float3(.65,.74,.77);
                }
                return half4(color*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
