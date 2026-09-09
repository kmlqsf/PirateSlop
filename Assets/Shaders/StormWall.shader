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
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 cloud : TEXCOORD1; float height : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float angle = input.uv.x * 6.2831853;
                float height = input.uv.y;
                float billow = sin(angle * 19 + height * 13 + _Time.y * .45 + _Layer) * 9
                    + sin(angle * 37 - height * 21 - _Time.y * .3) * 5;
                float envelope = sin(height * 3.14159265) * smoothstep(.08,.2,height);
                float3 radial = normalize(float3(world.x, 0, world.z));
                world += radial * billow * envelope;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.cloud = float3(world.x, height, world.z);
                output.height = length(TransformObjectToWorldDir(float3(0, 1, 0), false)) * height;
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
                float2 p=input.cloud.xz*.012 + float2(h*5-_Time.y*.06,h*9+_Time.y*.025)+_Layer*17.3;
                float2 warp=float2(Noise(p*.43),Noise(p*.43+31.7))*2.8;
                float n=Noise(p+warp)*.57+Noise(p*2.07+warp)*.28+Noise(p*4.13)*.15;
                float lightNoise=Noise(p+warp+float2(-.28,.4));
                float shade=saturate(.4+(lightNoise-n)*1.8);
                float top=1-smoothstep(.65,.98,h+(n-.5)*.16);
                float shelf=smoothstep(.15,.42,h)*(1-smoothstep(.55,.9,h));
                float density=smoothstep(.22,.72,n+shelf*.18)*top;
                float mist=exp(-h*8)*(.16+.2*n);
                float rain=pow(saturate(Noise(input.cloud.xz*.16+float2(h*2-_Time.y*.5,h*45+_Time.y*6))),5)*(1-smoothstep(.2,.7,h));
                float eventId=floor(_Time.y/6);
                float pulse=exp(-pow((frac(_Time.y/6)-.15)*100,2))+.35*exp(-pow((frac(_Time.y/6)-.19)*160,2));
                float angle=eventId*2.39996+_Layer;
                float flash=pulse*pow(saturate(dot(normalize(input.cloud.xz),float2(cos(angle),sin(angle)))),48)*smoothstep(.08,.35,h)*top;
                float3 color=lerp(_Color.rgb*.2,float3(.36,.4,.42),shade+n*.18);
                color+=rain*.22+flash*float3(.48,.68,.9);
                float alpha=saturate(density*(.86-_Layer*.12)+mist+rain*.3)*top;
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
