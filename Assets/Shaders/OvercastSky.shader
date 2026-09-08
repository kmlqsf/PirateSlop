Shader "PirateSlop/OvercastSky"
{
    Properties { _Exposure("Exposure", Float) = 1.1 }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float _Exposure;
            struct Varyings { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(float4 position : POSITION)
            {
                Varyings output; output.position=UnityObjectToClipPos(position); output.direction=position.xyz; return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 d=normalize(input.direction);
                float2 p=d.xz/max(.2,d.y+.3)*3+_Time.y*.002;
                float n=Noise(p)*.55+Noise(p*2.1)*.28+Noise(p*4.2)*.17;
                float3 sky=lerp(float3(.72,.8,.84),float3(.5,.65,.76),saturate(d.y));
                float3 clouds=lerp(float3(.59,.66,.71),float3(.93,.95,.96),n);
                return half4(lerp(sky,clouds,smoothstep(.18,.6,n)*smoothstep(0,.2,d.y))*_Exposure,1);
            }
            ENDHLSL
        }
    }
}
