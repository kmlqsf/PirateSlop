Shader "PirateSlop/StormWeather"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay-10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float _Intensity, _Wetness, _Shelter;
            float3 _WeatherCamera;
            float4x4 _WeatherInverseVP;
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V Vert(A i) { V o; o.positionCS=float4(i.positionOS.xy,0,1); return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 q=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(q),Hash(q+float2(1,0)),f.x),lerp(Hash(q+float2(0,1)),Hash(q+1),f.x),f.y);
            }
            float Cloud(float2 p) { return Noise(p)*.55+Noise(p*2.03)*.28+Noise(p*4.09)*.12+Noise(p*8.17)*.05; }
            half4 Frag(V i) : SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float depth=SampleSceneDepth(uv);
                float eye=LinearEyeDepth(depth,_ZBufferParams);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 world=ComputeWorldSpacePosition(uv,depth,_WeatherInverseVP);
                float3 ray=normalize(world-_WeatherCamera);
                float2 cloudUV=ray.xz/max(.16,ray.y+.35)*2.4+_WeatherCamera.xz*.0004+float2(_Time.y*.008,-_Time.y*.012);
                float clouds=Cloud(cloudUV+Cloud(cloudUV*.6)*2);
                float sky=smoothstep(_ProjectionParams.z*.8,_ProjectionParams.z*.98,eye);
                float distanceFog=1-exp(-eye*lerp(.0003,.018,_Intensity*_Intensity));
                float haze=distanceFog*_Intensity;
                float flash=exp(-pow((frac(_Time.y/8.7)-.31)*120,2))*.18*_Intensity;
                float3 fog=lerp(float3(.085,.115,.13),float3(.28,.33,.35),clouds);
                fog+=flash;
                float alpha=saturate(haze+sky*_Intensity*.8);
                float foreground=_Intensity*.2*(1-_Shelter*.75);
                alpha=alpha+foreground*(1-alpha);
                float2 aspect=float2(_ScreenParams.x/_ScreenParams.y,1);
                float beads=0, rim=0;
                for(int layer=0;layer<2;layer++)
                {
                    float2 p=uv*aspect*(13+layer*8);
                    p.x+=sin(p.y*.5+_Time.y*.2)*.05;
                    p.y+=_Time.y*(.18+layer*.12);
                    float2 cell=floor(p), f=frac(p)-.5;
                    float seed=Hash(cell+layer*31);
                    f.x+=(seed-.5)*.55;
                    float shape=length(f*float2(5,2.1));
                    float alive=step(seed, _Wetness*.55);
                    beads+=exp(-shape*shape*5)*alive;
                    rim+=exp(-pow((shape-.6)*14,2))*alive*saturate(f.y*5+.35);
                }
                float lens=saturate((beads*.22+rim*.4)*_Wetness);
                fog=lerp(fog,float3(.48,.57,.6),saturate(rim));
                alpha=alpha+lens*(1-alpha);
                return half4(fog,alpha);
            }
            ENDHLSL
        }
    }
}
