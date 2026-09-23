Shader "PirateSlop/StormWeather"
{
    Properties
    {
        _Mode("Rain / Lightning", Float) = 0
        _Color("Weather Color", Color) = (.48,.56,.62,1)
        _WeatherCenter("Center / Radius", Vector) = (0,0,0,1500)
        _RainSettings("Opacity / Speed / Height / Inner Thickness", Vector) = (.34,24,187,80)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+18" "RenderType"="Transparent" }
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
            float _Mode;
            half4 _Color;
            float4 _WeatherCenter;
            float4 _RainSettings;
            CBUFFER_END
            float4 _StormLightning;
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 kind:TEXCOORD1; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; float2 kind:TEXCOORD2; half4 color:COLOR; };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);
                o.uv=input.uv; o.kind=input.kind; o.color=input.color;
                return o;
            }
            float RainStrand(float2 p, float width)
            {
                float cell=floor(p.x);
                float seed=Hash(float2(cell,17.7));
                float center=.22+seed*.56;
                float dx=abs(frac(p.x)-center);
                float aa=max(.015,fwidth(p.x));
                float strand=1-smoothstep(width-aa,width+aa,dx);
                strand*=saturate(width/max(width,aa));
                float segment=frac(p.y+seed*5.37);
                float trail=smoothstep(0,.14,segment)*(1-smoothstep(.48,.9,segment));
                return strand*trail;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float scene=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)),_ZBufferParams);
                float eye=-TransformWorldToView(i.world).z;
                float depthFade=saturate((scene-eye)/1.5);
                float2 cameraOffset=_WorldSpaceCameraPos.xz-_WeatherCenter.xz;
                float cameraRadius=length(cameraOffset);
                float visibility=1;
                if (cameraRadius>_WeatherCenter.w+20)
                {
                    float2 radial=normalize(i.world.xz-_WeatherCenter.xz);
                    float facing=dot(radial,cameraOffset/max(1,cameraRadius));
                    visibility=smoothstep(_WeatherCenter.w/cameraRadius-.08,_WeatherCenter.w/cameraRadius+.08,facing);
                }
                if (_Mode > .5)
                {
                    float side=abs(i.uv.y*2-1);
                    float core=1-smoothstep(.05,.4,side);
                    float glow=pow(saturate(1-side),1.4);
                    half3 color=lerp(i.color.rgb,i.color.rgb*1.65,core);
                    return half4(color, saturate((core*.65+glow*.55)*i.color.a)*depthFade*visibility);
                }
                float t=_Time.y;
                float layer=i.kind.x;
                float2 drift=float2(t*.12,-t*.075);
                float broad=Noise(i.world.xz*.009+drift+layer*9.7);
                float medium=Noise(i.world.xz*.034-float2(t*.23,t*.09)+layer*14.1);
                float cluster=smoothstep(.25,.7,broad*.8+medium*.2);
                float y=i.uv.y;
                float fall=y+t*_RainSettings.y;
                float x=i.uv.x+y*.14+t*(2.4+layer*.65);
                float streak=RainStrand(float2(x*.68,fall*.25),.055);
                float fine=RainStrand(float2(x*1.37+11.3,fall*.37-t*.17),.037);
                float distanceFade=smoothstep(2,14,distance(_WorldSpaceCameraPos,i.world));
                float heightFade=smoothstep(0,.018,i.kind.y)*(1-smoothstep(.7,1,i.kind.y));
                float curtain=cluster*(.018+medium*.035);
                float alpha=(curtain+cluster*(streak*.9+fine*.45))*heightFade*distanceFade*_RainSettings.x;
                float3 curtainNormal=normalize(cross(ddx(i.world),ddy(i.world)));
                float silhouette=abs(dot(curtainNormal,normalize(_WorldSpaceCameraPos-i.world)));
                alpha*=visibility*smoothstep(.06,.36,silhouette);
                float flash=_StormLightning.w*exp(-dot(i.world-_StormLightning.xyz,i.world-_StormLightning.xyz)/8000);
                half3 color=lerp(_Color.rgb*.65,_Color.rgb,saturate(streak+fine*.5));
                color+=half3(.16,.19,.24)*flash;
                return half4(color,saturate(alpha)*depthFade);
            }
            ENDHLSL
        }
    }
}
