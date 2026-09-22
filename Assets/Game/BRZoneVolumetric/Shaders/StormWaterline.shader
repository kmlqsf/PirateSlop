Shader "PirateSlop/StormWaterline"
{
    Properties
    {
        _Color("Cold Foam / Mist", Color) = (.82,.87,.87,1)
        _ParticleMode("Particle Mode", Float) = 0
        _WaterlineSettings("Opacity / Noise Scale / Speed / Height", Vector) = (.52,.024,1,4)
        _WaterlineCenter("Center / Radius", Vector) = (0,0,0,1500)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="StormWaterContact" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _ParticleMode;
            float4 _WaterlineSettings;
            float4 _WaterlineCenter;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 kind:TEXCOORD1; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; float2 kind:TEXCOORD2; half4 color:COLOR; float4 screen:TEXCOORD3; };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);
                o.screen=ComputeScreenPos(o.positionCS);
                o.uv=input.uv; o.kind=input.kind; o.color=input.color;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float t=_Time.y*_WaterlineSettings.z;
                float2 p=i.world.xz*_WaterlineSettings.y;
                float broad=Noise(p*.28+float2(t*.018,-t*.011));
                float medium=Noise(p*1.9+float2(-t*.15,t*.09)+broad*.7);
                float fine=Noise(p*5.7+float2(t*.27,t*.18));
                float patches=smoothstep(.23,.6,broad*.85+medium*.15);
                float alpha;
                half3 color=_Color.rgb;
                if (_ParticleMode > .5)
                {
                    float2 uv=i.uv*2-1;
                    float body=saturate(1-dot(uv,uv));
                    alpha=smoothstep(0,.28,body)*body*smoothstep(.26,.62,medium*.4+fine*.6)*i.color.a;
                    color*=i.color.rgb;
                }
                else if (i.kind.x < .5)
                {
                    float edge=smoothstep(0,.22,i.uv.y)*(1-smoothstep(.78,1,i.uv.y));
                    float2 radial=normalize(i.world.xz-_WaterlineCenter.xz);
                    float2 tangent=float2(-radial.y,radial.x);
                    float streak=Noise(float2(dot(i.world.xz,tangent)*.045-t*.42,length(i.world.xz-_WaterlineCenter.xz)*.48+t*.17));
                    float band=1-smoothstep(4,8,abs(length(i.world.xz-_WaterlineCenter.xz)-_WaterlineCenter.w+(broad-.5)*5));
                    float foam=smoothstep(.4,.66,medium*.6+fine*.4);
                    float churn=band*smoothstep(.42,.7,streak)*smoothstep(.25,.6,broad);
                    alpha=edge*(patches*foam*1.2+churn)*_WaterlineSettings.x;
                    color=lerp(half3(.38,.5,.55),_Color.rgb,saturate(foam*.6+churn));
                }
                else
                {
                    float crown=lerp(.45,1,broad);
                    float vertical=(1-smoothstep(crown*.12,crown,i.uv.y));
                    alpha=vertical*patches*(.35+medium*.55)*_WaterlineSettings.x;
                    color=lerp(_Color.rgb*.84,half3(.4,.53,.6),i.uv.y);
                }
                float scene=LinearEyeDepth(SampleSceneDepth(i.screen.xy/i.screen.w),_ZBufferParams);
                float eye=-TransformWorldToView(i.world).z;
                alpha*=saturate((scene-eye)/(_ParticleMode < .5 && i.kind.x < .5 ? .08 : 1.2));
                alpha*=smoothstep(.4,2.5,distance(_WorldSpaceCameraPos,i.world));
                if (_ParticleMode < .5)
                {
                    float2 cameraOffset=_WorldSpaceCameraPos.xz-_WaterlineCenter.xz;
                    float cameraRadius=length(cameraOffset);
                    if (cameraRadius > _WaterlineCenter.w+20)
                    {
                        float2 radial=normalize(i.world.xz-_WaterlineCenter.xz);
                        float facing=dot(radial,cameraOffset/max(1,cameraRadius));
                        alpha*=smoothstep(_WaterlineCenter.w/cameraRadius-.12,_WaterlineCenter.w/cameraRadius+.04,facing);
                    }
                }
                return half4(color,saturate(alpha));
            }
            ENDHLSL
        }
    }
}
