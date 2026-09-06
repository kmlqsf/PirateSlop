Shader "PirateSlop/Ocean"
{
    Properties
    {
        _DeepColor("Deep Water", Color) = (.008,.07,.105,1)
        _ShallowColor("Shallow Water", Color) = (.035,.35,.32,1)
        _FoamColor("Foam", Color) = (.78,.91,.89,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            float4 _Waves[4], _Wakes[32];
            int _WakeCount;
            float _WaveTime, _WaveScale;
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor, _FoamColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float fog : TEXCOORD1; };
            float3 Wave(float2 p)
            {
                float3 value = 0;
                for(int i=0;i<4;i++)
                {
                    float4 w = _Waves[i]; float k = 6.2831853 / w.w;
                    float phase = k * dot(w.xy,p) - sqrt(9.81*k)*_WaveTime;
                    value.x += w.z*sin(phase);
                    value.yz += w.z*k*w.xy*cos(phase);
                }
                return value*_WaveScale;
            }
            Varyings Vert(Attributes input)
            {
                Varyings o; o.world = TransformObjectToWorld(input.positionOS.xyz);
                o.world.y += Wave(o.world.xz).x;
                o.positionCS = TransformWorldToHClip(o.world); o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 wave = Wave(input.world.xz);
                float2 p = input.world.xz;
                float ripple = sin(p.x*2.3+p.y*1.7+_WaveTime*2.1)*.045 + sin(p.x*4.1-p.y*2.9-_WaveTime*1.6)*.025;
                float3 n = normalize(float3(-wave.y+ripple, 1, -wave.z+ripple*.7));
                float3 view = GetWorldSpaceNormalizeViewDir(input.world);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceDepth = -TransformWorldToView(input.world).z;
                float depth = max(0, LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams)-surfaceDepth);
                float2 refractedUV = saturate(uv+n.xz*.015*saturate(depth));
                float refractedDepth = LinearEyeDepth(SampleSceneDepth(refractedUV), _ZBufferParams);
                if(refractedDepth < surfaceDepth) refractedUV = uv;
                float3 tint = lerp(_ShallowColor.rgb, _DeepColor.rgb, 1-exp(-depth*.12));
                float3 color = lerp(SampleSceneColor(refractedUV), tint, 1-exp(-depth*.35));
                float fresnel = .035+.965*pow(1-saturate(dot(n,view)),5);
                float3 reflection = GlossyEnvironmentReflection(reflect(-view,n), .16, 1);
                color = lerp(color, reflection, fresnel*.85);
                Light light = GetMainLight();
                color += light.color*pow(saturate(dot(n,normalize(view+light.direction))),350)*2;
                float noise = .5+.5*sin(p.x*6+sin(p.y*4+_WaveTime))*sin(p.y*5-_WaveTime);
                float foam = saturate(1-depth/.65)*smoothstep(.3,.7,noise);
                foam += smoothstep(.65,.95,wave.x/max(.01,_WaveScale))*noise*.45;
                for(int i=0;i<_WakeCount;i++)
                {
                    float4 w = _Wakes[i]; float2 d = p-w.xy;
                    float along = dot(d,float2(sin(w.z),cos(w.z)));
                    float across = abs(dot(d,float2(cos(w.z),-sin(w.z))));
                    float trail = saturate(-along/5)*saturate(1+along/45);
                    float edge = exp(-pow((across-2.5+along*.2)*.9,2));
                    foam += edge*trail*w.w*(.3+.7*noise);
                    float bow = exp(-pow((along-7)*.55,2)-pow((across-2)*1.4,2));
                    foam += bow*w.w*noise;
                }
                color = lerp(color,_FoamColor.rgb,saturate(foam));
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
    }
}
