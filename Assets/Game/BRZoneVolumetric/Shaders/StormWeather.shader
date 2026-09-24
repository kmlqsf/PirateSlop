Shader "PirateSlop/StormWeather"
{
    Properties
    {
        _WeatherCenter("Center / Radius", Vector) = (0,0,0,1500)
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
            float4 _WeatherCenter;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);
                o.uv=input.uv; o.color=input.color;
                return o;
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
                float side=abs(i.uv.y*2-1);
                float core=1-smoothstep(.05,.4,side);
                float glow=pow(saturate(1-side),1.4);
                half3 color=lerp(i.color.rgb,i.color.rgb*1.65,core);
                return half4(color, saturate((core*.65+glow*.55)*i.color.a)*depthFade*visibility);

            }
            ENDHLSL
        }
    }
}
