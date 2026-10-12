Shader "PirateSlop/StaffCore"
{
    Properties { _Ice("Ice", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 positionCS:SV_POSITION; float3 p:TEXCOORD0; float3 normal:TEXCOORD1; float3 world:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
        CBUFFER_START(UnityPerMaterial)
        float _Ice;
        CBUFFER_END
        float hash(float3 p) { p=frac(p*.3183099+float3(.17,.43,.71)); p*=17; return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
        float noise3(float3 p)
        {
            float3 a=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
        }
        V vert(A v)
        {
            V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 p=v.positionOS.xyz;
            p+=v.normalOS*(noise3(p*8+float3(0,-_Time.y*1.6,0))-.5)*.045*(1-saturate(_Ice));
            o.positionCS=TransformObjectToHClip(p); o.p=p; o.normal=TransformObjectToWorldNormal(v.normalOS); o.world=TransformObjectToWorld(p); return o;
        }
        half4 frag(V i):SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float3 n=normalize(i.normal), view=normalize(GetWorldSpaceViewDir(i.world));
            float edge=pow(1-saturate(dot(n,view)),2.3);
            float shade=.35+.65*saturate(dot(n,normalize(float3(-.4,.8,-.5))));
            float3 p=i.p*11;
            float flow=noise3(p+float3(0,-_Time.y*2,0));
            float detail=noise3(p*2.7+flow*2);
            float crust=smoothstep(.32,.68,flow*.72+detail*.28);
            float cracks=1-smoothstep(.015,.085,abs(noise3(p*1.7)-.5));
            float3 lava=lerp(float3(.9,.045,.003),float3(5.5,2.6,.35),crust);
            float3 fire=lerp(lava,float3(.07,.008,.003)*shade,smoothstep(.56,.78,detail)*.8);
            fire+=edge*float3(1.8,.38,.02)+cracks*float3(.5,.13,.01);
            float3 ice=lerp(float3(.035,.12,.18),float3(.23,.51,.64),shade)+float3(.5,.9,1.1)*(edge+cracks*.12);
            float glint=pow(saturate(dot(reflect(-normalize(float3(-.4,.8,-.5)),n),view)),32);
            ice+=glint*float3(2.5,3,3.2);
            return half4(lerp(fire,ice,saturate(_Ice)),1);
        }
        half4 depthFrag(V i):SV_Target { return 0; }
        half4 normalFrag(V i):SV_Target { return half4(normalize(i.normal),0); }
        ENDHLSL
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment normalFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
