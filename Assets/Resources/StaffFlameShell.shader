Shader "PirateSlop/StaffFlameShell"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; float3 n:NORMAL; };
            struct V { float4 p:SV_POSITION; float3 local:TEXCOORD0; float3 n:TEXCOORD1; float3 w:TEXCOORD2; };
            float h(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }
            float n(float3 p)
            {
                float3 a=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(h(a),h(a+float3(1,0,0)),f.x),lerp(h(a+float3(0,1,0)),h(a+float3(1,1,0)),f.x),f.y),lerp(lerp(h(a+float3(0,0,1)),h(a+float3(1,0,1)),f.x),lerp(h(a+float3(0,1,1)),h(a+1),f.x),f.y),f.z);
            }
            V vert(A i)
            {
                V o; float3 p=i.p.xyz;
                float heat=n(p*9+float3(0,-_Time.y*3,0));
                p+=i.n*(.03+heat*.11); o.p=TransformObjectToHClip(p); o.local=p; o.w=TransformObjectToWorld(p); o.n=TransformObjectToWorldNormal(i.n); return o;
            }
            half4 frag(V i):SV_Target
            {
                float3 flow=i.local*10+float3(0,-_Time.y*3,0);
                float field=n(flow)*.65+n(flow*2.1)*.35;
                float rim=pow(1-saturate(dot(normalize(i.n),normalize(GetWorldSpaceViewDir(i.w)))),.7);
                float alpha=smoothstep(.24,.72,field)*(.2+rim*.65);
                float3 color=lerp(float3(1.6,.08,.001),float3(4,1.9,.25),smoothstep(.35,.7,field));
                return half4(color*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
