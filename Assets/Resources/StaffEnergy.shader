Shader "PirateSlop/StaffEnergy"
{
    Properties { _Mode("Mode", Float)=0 _BaseMap("Texture",2D)="white" {} _BaseColor("Color",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Mode; float4 _BaseColor;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; float eye:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            float h(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float n(float2 p) { float2 a=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(h(a),h(a+float2(1,0)),f.x),lerp(h(a+float2(0,1)),h(a+1),f.x),f.y); }
            float fbm(float2 p) { return n(p)*.57+n(p*2.13)*.28+n(p*4.47)*.15; }
            V vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); float3 w=TransformObjectToWorld(i.p.xyz); o.p=TransformWorldToHClip(w); o.eye=-TransformWorldToView(w).z; o.uv=i.uv; o.c=i.c*_BaseColor; return o; }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=i.uv, q=(uv-.5)*2;
                float field=fbm(uv*5+float2(0,-_Time.y*1.3));
                float warp=fbm(uv*9+field*2+_Time.y*.22);
                float density=smoothstep(.16,.8,field*.75+warp*.25);
                float mask=pow(saturate(1-dot(q,q)),1.4);
                float3 color=i.c.rgb;
                if (_Mode<.5)
                {
                    float taper=lerp(.9,.12,uv.y);
                    float tongue=saturate(1-abs(q.x+sin(uv.y*9+_Time.y*4)*.12)/taper);
                    mask=pow(tongue,1.3)*sin(saturate(uv.y)*3.14159)*smoothstep(.18,.65,field);
                    color=lerp(float3(.7,.035,.001),float3(3.4,1.25,.08),density)*i.c.rgb;
                }
                else if (_Mode<1.5) { mask*=smoothstep(.13,.75,field); color*=lerp(.6,1.4,density); }
                else { mask*=smoothstep(.12,.7,field); color*=lerp(.6,1.1,density); }
                float scene=LinearEyeDepth(SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy),_ZBufferParams);
                float soft=saturate((scene-i.eye)*3);
                float alpha=saturate(mask*i.c.a*soft);
                return half4(color*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
