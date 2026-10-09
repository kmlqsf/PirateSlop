Shader "PirateSlop/Storm Wet Wood"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+22" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(input.normalOS);
                o.uv=input.uv; o.color=input.color;
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1;
                float seed=input.color.r*6.2831853;
                float angle=atan2(p.y,p.x);
                float edge=.79+.09*sin(angle*3+seed)+.055*sin(angle*7-seed*2);
                float alpha=(1-smoothstep(edge-.16,edge+.1,length(p)))*input.color.a;
                float scene=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS)),_ZBufferParams);
                float eye=-TransformWorldToView(input.world).z;
                alpha*=1-smoothstep(.035,.12,abs(scene-eye));
                clip(alpha-.002);
                float3 normal=normalize(input.normal);
                float3 view=GetWorldSpaceNormalizeViewDir(input.world);
                Light sun=GetMainLight();
                float spec=pow(saturate(dot(normal,SafeNormalize(view+sun.direction))),128);
                float fresnel=.02+.12*pow(1-saturate(dot(normal,view)),5);
                half3 reflection=GlossyEnvironmentReflection(reflect(-view,normal),input.world,.12,1,GetNormalizedScreenSpaceUV(input.positionCS));
                half3 sheen=reflection*fresnel+sun.color*spec*.42;
                return half4(sheen*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
