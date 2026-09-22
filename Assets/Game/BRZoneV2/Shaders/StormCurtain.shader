Shader "PirateSlop/BRZoneV2/Curtain"
{
    Properties
    {
        _Large("Broad noise", 2D) = "gray" {}
        _Detail("Detail noise", 2D) = "gray" {}
        _Fine("Fine noise", 2D) = "gray" {}
        _Gradient("Vertical fade", 2D) = "white" {}
        _FoamMask("Foam", 2D) = "white" {}
        _Opacity("Opacity", Range(0,0.48)) = .38
        _Foam("Sea contact", Float) = 0
        _Thickness("Thickness", Float) = 12
        _Layer("Volume layer", Float) = 0
        _Mist("Low mist", Float) = 0
        [Enum(Flat,0,Height,1,Broad,2,Medium,3)] _SilhouettePass("Silhouette pass", Float) = 2
        _TopFadeStart("Top fade start", Range(.95,.99)) = .95
        _BodyFloor("Minimum cloud density", Range(.5,1.6)) = 1.25
        _CloudPeak("Cloud pocket density", Range(1,2.2)) = 1.8
        _BroadScale("Broad world frequency", Float) = .007
        _MediumStrength("Medium variation", Range(0,.2)) = .08
        _ShellSpacing("Shell spacing metres", Range(4,8)) = 6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_Large); SAMPLER(sampler_Large);
            TEXTURE2D(_Detail); SAMPLER(sampler_Detail);
            TEXTURE2D(_Fine); SAMPLER(sampler_Fine);
            TEXTURE2D(_Gradient); SAMPLER(sampler_Gradient);
            TEXTURE2D(_FoamMask); SAMPLER(sampler_FoamMask);
            CBUFFER_START(UnityPerMaterial)
            float _Opacity, _Foam, _Thickness, _Layer, _Mist;
            float _SilhouettePass, _TopFadeStart, _BodyFloor, _CloudPeak, _BroadScale, _MediumStrength;
            float _ShellSpacing;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 heightData:TEXCOORD1; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; float eye:TEXCOORD2; float height:TEXCOORD3; };
            V Vert(A i)
            {
                V o;
                float3 w=TransformObjectToWorld(i.positionOS.xyz);
                float3 center=TransformObjectToWorld(float3(0,0,0));
                float2 radial=normalize(w.xz-center.xz+float2(.0001,0));
                float sway=sin(i.uv.x*6.2831853*11+_Time.y*.12+i.uv.y*4)+.4*sin(i.uv.x*6.2831853*23-_Time.y*.09+i.uv.y*9);
                w.xz+=radial*(sway*_Thickness*.23*sin(i.uv.y*3.14159265)+_Layer*_ShellSpacing)*(1-_Foam);
                o.world=w; o.positionCS=TransformWorldToHClip(w); o.uv=i.uv;
                o.eye=-TransformWorldToView(w).z;
                o.height=saturate((i.positionOS.y-i.heightData.x)/max(i.heightData.y,.001));
                return o;
            }
            float Hash3(float3 p) { p=frac(p*.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
            float Noise3(float3 p)
            {
                float3 q=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash3(q),Hash3(q+float3(1,0,0)),f.x),lerp(Hash3(q+float3(0,1,0)),Hash3(q+float3(1,1,0)),f.x),f.y),lerp(lerp(Hash3(q+float3(0,0,1)),Hash3(q+float3(1,0,1)),f.x),lerp(Hash3(q+float3(0,1,1)),Hash3(q+1),f.x),f.y),f.z);
            }
            half4 Frag(V i):SV_Target
            {
                float3 center=TransformObjectToWorld(float3(0,0,0));
                float3 p=i.world-center;
                float radius=max(length(p.xz),1);
                if(_Foam<.5 && _Mist<.5)
                {
                    float h=saturate(i.height);
                    float profile=1-smoothstep(_TopFadeStart,1,h);
                    if(_SilhouettePass>.5)
                        profile*=lerp(.78,1,smoothstep(0,.1,h))*lerp(1,.92,smoothstep(.75,.92,h));
                    float cloudMass=.5;
                    float cloudLight=.5;
                    if(_SilhouettePass>1.5)
                    {
                        float angle=_Time.y*lerp(.65,-.45,saturate(_Layer))/radius;
                        float2 flow=float2(p.x*cos(angle)-p.z*sin(angle),p.x*sin(angle)+p.z*cos(angle));
                        float3 cloudPosition=float3(flow.x,(i.world.y-_Time.y*.12)*2.2,flow.y)*_BroadScale*(1+_Layer*.27)+_Layer*float3(13.7,5.3,19.1);
                        cloudMass=Noise3(cloudPosition);
                        cloudLight=saturate(.5+(Noise3(cloudPosition+float3(-.18,.28,-.12))-cloudMass)*3);
                        cloudMass+=(Noise3(cloudPosition*2.6+7.9)-.5)*.24;
                        if(_SilhouettePass>2.5)
                            cloudMass+=(Noise3(float3(flow.x,i.world.y+_Time.y*.1,flow.y)*.045)-.5)*_MediumStrength;
                    }
                    float mass=smoothstep(.30,.66,cloudMass);
                    float density=_SilhouettePass>1.5?lerp(_BodyFloor,_CloudPeak,mass):1;
                    float3 body=lerp(float3(.004,.009,.016),float3(.10,.14,.18),cloudLight*cloudLight);
                    float alpha=min(.66,_Opacity*density)*profile;
                    if(_Layer>.5) { alpha=lerp(.10,.25,mass)*profile; body=lerp(float3(.012,.022,.035),float3(.12,.16,.20),cloudLight*cloudLight); }
                    if(_Layer>1.5) { alpha=lerp(.025,.065,mass)*profile; body=float3(.20,.25,.29); }
                    return half4(body,alpha);
                }
                float a=_Time.y*2.8/radius;
                float2 rot=float2(p.x*cos(a)-p.z*sin(a),p.x*sin(a)+p.z*cos(a));
                float2 broad=rot*.006+float2(p.y*.013-_Time.y*.003,p.y*.009+_Layer*3.71);
                float rawN=SAMPLE_TEXTURE2D(_Large,sampler_Large,broad).r;
                float3 cloud=float3(rot.x,p.y-_Time.y*.65,rot.y)*.018+_Layer*17.3;
                float n=Noise3(cloud+(rawN-.5)*2);
                a=-_Time.y*5.1/radius;
                rot=float2(p.x*cos(a)-p.z*sin(a),p.x*sin(a)+p.z*cos(a));
                float2 middle=rot*.028+float2(p.y*.037+_Time.y*.009,p.y*.021)+float2(n,-n)*.42+_Layer*7.3;
                float d=Noise3(float3(rot.x,p.y+_Time.y*.9,rot.y)*.11+float3(n*1.2,0,n*.6)+_Layer*9.7);
                float f=SAMPLE_TEXTURE2D(_Fine,sampler_Fine,middle*2.7+float2(_Time.y*.021,-_Time.y*.016)).r;
                float mass=smoothstep(.31,.57,n*.66+d*.34+(f-.5)*.045);
                float top=.53+n*.35+(d-.5)*.13;
                float fade=(1-smoothstep(top-.2,top+.12,i.uv.y))*smoothstep(0,.04,i.uv.y);
                float3 radial=normalize(float3(p.x,0,p.z));
                float grazing=smoothstep(.015,.22,abs(dot(normalize(_WorldSpaceCameraPos-i.world),radial)));
                float alpha=_Opacity*lerp(.025,1,mass)*fade*grazing;
                float3 color=lerp(float3(.012,.022,.035),float3(.085,.12,.155),saturate(d*.65+(1-n)*.2));
                if(_Layer>.5) alpha*=.22;
                if(_Mist>.5)
                {
                    alpha=(.08+.16*mass)*pow(saturate(1-i.uv.y),1.8)*smoothstep(0,.08,i.uv.y)*grazing;
                    color=float3(.55,.61,.63);
                }
                if(_Foam>.5)
                {
                    float foam=SAMPLE_TEXTURE2D(_FoamMask,sampler_FoamMask,i.world.xz*.065+float2(_Time.y*.035,0)).r;
                    alpha=(.09+smoothstep(.34,.65,foam)*(.28+.16*d))*pow(sin(i.uv.y*3.14159265),.65);
                    color=float3(.66,.7,.68)+f*.05;
                }
                float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
                float depth=LinearEyeDepth(raw,_ZBufferParams);
                alpha*=saturate((depth-i.eye)/(_Foam>.5?.3:3));
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
