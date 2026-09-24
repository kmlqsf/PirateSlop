Shader "PirateSlop/StormWaterline"
{
    Properties
    {
        _Color("Cold Foam / Mist", Color) = (.82,.87,.87,1)
        _ParticleMode("Particle Mode", Float) = 0
        _WaterlineSettings("Opacity / Noise Scale / Speed / Height", Vector) = (.78,.019,1,6.5)
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
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; float2 kind:TEXCOORD2; half4 color:COLOR; };
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
                o.uv=input.uv; o.kind=input.kind; o.color=input.color;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float t=_Time.y*_WaterlineSettings.z;
                float2 offset=i.world.xz-_WaterlineCenter.xz;
                float radialDistance=length(offset);
                float2 radial=offset/max(1,radialDistance);
                float2 tangent=float2(-radial.y,radial.x);
                float2 p=i.world.xz*_WaterlineSettings.y;
                float broad=Noise(p*.31+float2(t*.015,-t*.009));
                float medium=Noise(p*1.75-tangent*t*.09+radial*t*.032+broad*.8);
                float fine=Noise(p*6.2-tangent*t*.31-radial*t*.16);
                float patches=smoothstep(.24,.6,broad*.84+medium*.16);
                float alpha;
                half3 color=_Color.rgb;
                if (_ParticleMode > .5)
                {
                    float2 uv=i.uv*2-1;
                    float body=saturate(1-dot(uv,uv));
                    float breakup=Noise(i.uv*5.5+i.world.xz*.12+float2(t*.21,-t*.13));
                    alpha=smoothstep(0,.3,body)*body*lerp(.38,1,smoothstep(.25,.66,breakup))*i.color.a;
                    float heightFade=smoothstep(2,12,i.world.y-_WaterlineCenter.y);
                    alpha*=lerp(1,.55,heightFade);
                    color*=i.color.rgb*lerp(half3(1,1,1),half3(.67,.77,.84),heightFade);
                }
                else if (i.kind.x < .5)
                {
                    float across=i.uv.y+(medium-.5)*.14+(fine-.5)*.08;
                    float edge=smoothstep(0,.18,across)*(1-smoothstep(.76,1,across));
                    float2 advected=i.world.xz-tangent*t*5+radial*t*2;
                    float streak=Noise(float2(advected.x*.045+advected.y*.023,advected.y*.21-advected.x*.12)+medium*.9);
                    float band=1-smoothstep(5.5,10.5,abs(radialDistance-_WaterlineCenter.w+(broad-.5)*8));
                    float foam=smoothstep(.36,.65,medium*.63+fine*.37);
                    float churn=band*smoothstep(.37,.66,streak)*smoothstep(.22,.58,broad);
                    float foamVeins=1-smoothstep(.07,.19,abs(fine-.52));
                    float lace=Noise(p*22-tangent*t*.41+radial*t*.23);
                    float breakup=smoothstep(.35,.7,lace*.7+fine*.3);
                    alpha=edge*(patches*(foam*.95+foamVeins*.42)+churn*1.05)*lerp(.16,1,breakup)*_WaterlineSettings.x;
                    color=lerp(half3(.4,.52,.57),_Color.rgb,saturate(foam*.8+churn*.8+foamVeins*.25));
                }
                else
                {
                    float layer=i.kind.y;
                    float curls=Noise(p*2.8-tangent*t*.14+float2(i.world.y*.31,layer*4.7));
                    float crown=lerp(.48,1,broad*.7+curls*.3);
                    float vertical=(1-smoothstep(crown*.2,crown,i.uv.y));
                    float wisps=smoothstep(.22,.64,medium*.45+curls*.55);
                    alpha=vertical*patches*(.3+wisps*.7)*_WaterlineSettings.x*1.08;
                    color=lerp(_Color.rgb*.95,half3(.43,.56,.63),smoothstep(.02,.8,i.uv.y));
                }
                float scene=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)),_ZBufferParams);
                float eye=-TransformWorldToView(i.world).z;
                alpha*=saturate((scene-eye)/(_ParticleMode < .5 && i.kind.x < .5 ? .08 : .65));
                float cameraDistance=distance(_WorldSpaceCameraPos,i.world);
                alpha*=smoothstep(.4,2.5,cameraDistance);
                // Keep the distant waterline under the same haze as the cloud wall.
                if (_ParticleMode < .5)
                    alpha*=1-smoothstep(90,380,cameraDistance);
                if (_ParticleMode < .5)
                {
                    float2 cameraOffset=_WorldSpaceCameraPos.xz-_WaterlineCenter.xz;
                    float cameraRadius=length(cameraOffset);
                    if (cameraRadius > _WaterlineCenter.w+20)
                    {
                        float facing=dot(radial,cameraOffset/max(1,cameraRadius));
                        alpha*=smoothstep(_WaterlineCenter.w/cameraRadius-.12,_WaterlineCenter.w/cameraRadius+.04,facing);
                    }
                }
                float aerial=saturate((cameraDistance-120)/1800)*.24;
                color=lerp(color,half3(.49,.59,.64),aerial);
                return half4(color,saturate(alpha));
            }
            ENDHLSL
        }
    }
}
