Shader "PirateSlop/StormBillows"
{
    Properties
    {
        _BaseMap("Cloud Billows", 2D) = "white" {}
        _ShadowColor("Shadow", Color) = (.055,.075,.10,1)
        _LightColor("Light", Color) = (.55,.66,.78,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="StormWaterContact" }
            Blend One OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _ShadowColor;
            half4 _LightColor;
            CBUFFER_END
            float4 _BillowCenter;
            float _BillowRadius;
            float _BillowHeight;
            float _BillowClock;
            float _BillowNearBank;
            float _BillowNearShore;
            float4 _StormLightning;
            float4 _StormLightningSecondary;
            float4 _StormLightningColor;
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float4 layout:TEXCOORD1; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; half4 color:TEXCOORD2; float2 localUV:TEXCOORD3; float nearFade:TEXCOORD4; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float2 radial = input.positionOS.xz;
                float2 tangent = float2(-radial.y,radial.x);
                float scale = min(1.0,_BillowRadius/1200.0);
                float drift = sin(_BillowClock*.018+input.color.a*6.283)*8.0*scale;
                o.world.xz = _BillowCenter.xz + radial*(_BillowRadius+input.layout.z*scale)+tangent*(input.layout.x*scale+drift);
                float visibleBottom=(input.positionOS.y+abs(input.layout.y)*2*(input.color.g-.5))*_BillowHeight;
                float shoreDrop=max(0,visibleBottom+5)*_BillowNearShore*(1-_BillowNearBank);
                o.world.y = _BillowCenter.y+(input.positionOS.y+input.layout.y)*_BillowHeight-shoreDrop;
                o.positionCS = TransformWorldToHClip(o.world);
                uint tile = (uint)input.layout.w;
                float2 cell = float2(tile&3u,3u-(tile>>2u));
                o.uv = (cell+lerp(.002,.998,input.uv))*.25;
                o.localUV=input.uv;
                float3 center=float3(_BillowCenter.x+radial.x*(_BillowRadius+input.layout.z*scale),_BillowCenter.y+input.positionOS.y*_BillowHeight,_BillowCenter.z+radial.y*(_BillowRadius+input.layout.z*scale));
                float2 footing=center.xz-radial*input.layout.z*scale;
                float columnDistance=distance(footing,_WorldSpaceCameraPos.xz);
                o.nearFade=smoothstep(lerp(120.0,320.0,input.color.b),lerp(280.0,560.0,input.color.b),columnDistance);
                if(_BillowNearBank>.5)
                {
                    float radialDistance=abs(distance(_WorldSpaceCameraPos.xz,_BillowCenter.xz)-_BillowRadius);
                    o.nearFade=smoothstep(10.0,28.0,radialDistance)*(1-smoothstep(350.0,420.0,radialDistance));
                }
                o.color=input.color;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 cloud=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                float nearFade=i.nearFade;
                float scene=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)),_ZBufferParams);
                float eye=-TransformWorldToView(i.world).z;
                float depthFade=saturate((scene-eye)/12.0);
                float height=i.world.y-_BillowCenter.y;
                float shore=.5+.5*sin(i.world.x*.021+i.world.z*.013);
                half alpha=cloud.a*nearFade*depthFade*smoothstep(-4.0,3.0+shore*14.0,height);
                half luminosity=dot(cloud.rgb,half3(.2126,.7152,.0722));
                half light=saturate(luminosity*2.4);
                half3 color=cloud.rgb*_LightColor.rgb*i.color.r;
                color=lerp(color,_ShadowColor.rgb,.16*(1.0-light));
                half lowerShade=lerp(.80,1.0,smoothstep(.05,.85,i.localUV.y));
                color*=lowerShade;
                color=lerp(color,half3(.09,.12,.15),(1.0-smoothstep(4.0,28.0,height))*(.35+.25*shore));
                float d0=distance(i.world,_StormLightning.xyz);
                float d1=distance(i.world,_StormLightningSecondary.xyz);
                color+=_StormLightningColor.rgb*saturate(exp(-d0*d0/160000.0)*_StormLightning.w+exp(-d1*d1/160000.0)*_StormLightningSecondary.w)*lerp(.10,.28,light);
                clip(alpha-.002);
                return half4(color*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
