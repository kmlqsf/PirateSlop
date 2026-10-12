Shader "PirateSlop/FrozenWaterIce"
{
    Properties { _Formation("Formation", Float)=1 _Thaw("Thaw", Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/FrozenWaterSurface.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _Formation;
            float _Thaw;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float3 local:TEXCOORD2; float fog:TEXCOORD3; };
        Varyings Vert(Attributes i)
        {
            Varyings o;
            o.local=i.positionOS.xyz;
            o.world=TransformObjectToWorld(i.positionOS.xyz);
            o.world.y -= _Thaw * .16;
            o.positionCS=TransformWorldToHClip(o.world);
            o.normal=TransformObjectToWorldNormal(i.normalOS);
            o.fog=ComputeFogFactor(o.positionCS.z);
            return o;
        }
        float2 Hash(float2 p) { return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453); }
        float Fracture(float2 uv)
        {
            float2 cell=floor(uv), f=frac(uv);
            float a=20,b=20;
            for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
            {
                float2 offset=float2(x,y);
                float d=length(offset+Hash(cell+offset)-f);
                if(d<a){b=a;a=d;}else b=min(b,d);
            }
            return 1-smoothstep(.004,.028,b-a);
        }
        void Cut(Varyings i)
        {
            if (_FrozenWaterMapping.z > 0)
            {
                float edge=FrozenWaterSample(i.world.xz).b;
                float breakup=.58+.13*sin(i.world.x*5.7+sin(i.world.z*4.3))*sin(i.world.z*6.1);
                clip(edge-breakup);
            }
            float radius=length(i.local.xz)/1.6;
            clip(_Formation*1.4-radius-.02);
            clip(1-_Thaw-radius*.2);
        }
        ENDHLSL
        Pass
        {
            Name "IceSurface"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            half4 Frag(Varyings i):SV_Target
            {
                Cut(i);
                float2 uv=i.world.xz;
                float cracks=Fracture(uv*.95);
                float fine=Fracture(uv*3.7+17)*.25;
                float cloud=sin(uv.x*2.6+sin(uv.y*1.7))*sin(uv.y*3.1-uv.x*.8)*.5+.5;
                float rim=saturate(1-FrozenWaterSample(uv).b);
                float frost=saturate(cracks*.58+fine+rim*.6+cloud*.17);
                half3 base=lerp(half3(.045,.16,.22),half3(.57,.8,.85),frost);
                float3 n=normalize(i.normal+float3(ddx(cloud)*.2,0,ddy(cloud)*.2));
                float3 view=normalize(GetWorldSpaceViewDir(i.world));
                Light sun=GetMainLight();
                float fresnel=pow(1-saturate(dot(n,view)),4);
                float gloss=pow(saturate(dot(n,normalize(sun.direction+view))),100)*(1-frost*.7);
                half3 lit=base*(SampleSH(n)+sun.color*(.25+.75*saturate(dot(n,sun.direction))))+sun.color*gloss*.8;
                float front=1-smoothstep(.025,.12,abs(length(i.local.xz)/1.6-_Formation*1.4));
                lit+=half3(.22,.54,.64)*(front*.5+cracks*.04+fresnel*.16);
                lit=lerp(lit,lit*.45,_Thaw);
                return half4(MixFog(lit,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            half4 Depth(Varyings i):SV_Target { Cut(i); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Normals
            half4 Normals(Varyings i):SV_Target { Cut(i); return half4(normalize(i.normal),0); }
            ENDHLSL
        }
    }
}
