Shader "PirateSlop/StormWall"
{
    Properties
    {
        _Color("Storm Color", Color) = (.25,.34,.43,1)
        _Layer("Cloud Layer", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Layer;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 cloud : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float angle = input.uv.x * 6.2831853;
                float height = input.uv.y;
                float billow = sin(angle * 19 + height * 13 + _Time.y * .45 + _Layer) * 9
                    + sin(angle * 37 - height * 21 - _Time.y * .3) * 5;
                float envelope = sin(height * 3.14159265);
                float3 radial = normalize(TransformObjectToWorldDir(input.positionOS.xyz));
                world += radial * billow * envelope;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.cloud = input.positionOS.xyz;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float h=input.cloud.y;
                float2 flow=float2(h*6-_Time.y*.12, h*11+_Time.y*.18);
                float2 p=input.cloud.xz*22+flow+_Layer*13.7;
                float warp=Noise(p*.37)*3;
                float n=Noise(p+warp)*.55+Noise(p*2.03-warp)*.3+Noise(p*4.07)*.15;
                float lower=Noise(p+float2(0,-.4));
                float shade=saturate(.5+(n-lower)*2);
                float density=lerp(smoothstep(.23,.68,n),.5+n*.45,smoothstep(.3,.85,h));
                float flash=pow(saturate(sin(_Time.y*1.7+floor(input.uv.x*18)*23.1)),110);
                flash*=smoothstep(.5,.8,n)*(1-h)*.65;
                float spray=(1-smoothstep(.03,.18,h))*smoothstep(.52,.85,Noise(p*5+_Time.y*.8));
                float3 dark=_Color.rgb*.65;
                float3 light=lerp(float3(.64,.71,.76),float3(.8,.85,.88),h);
                float3 color=lerp(dark,light,shade*.65+h*.22)+flash*float3(.65,.8,1)+spray*.24;
                return half4(color,saturate(density*(.63-_Layer*.06)+spray*.25));
            }
            ENDHLSL
        }
    }
}
