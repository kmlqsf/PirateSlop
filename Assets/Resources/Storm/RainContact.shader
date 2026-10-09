Shader "PirateSlop/Storm Rain Contact"
{
    Properties { _RainDownpour("Downpour", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+26" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _RainDownpour;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.world);
                output.normal = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv; output.color = input.color;
                return output;
            }
            half4 ContactFragment(Varyings input)
            {
                float2 p = input.uv * 2 - 1;
                float radius = length(p);
                float aa = clamp(fwidth(radius), .01, .25);
                float ring = (1 - smoothstep(.055, .055 + aa, abs(radius - .78)));
                float disk = 1 - smoothstep(.6, 1, radius);
                float shape = input.color.r < .5 ? disk : input.color.r > 1.5 ? disk : ring;
                half alpha = shape * input.color.a;
                clip(alpha - .002);
                float3 normal = normalize(input.normal);
                Light light = GetMainLight();
                half glint = pow(saturate(dot(normal, SafeNormalize(light.direction + GetWorldSpaceNormalizeViewDir(input.world)))), 48);
                half3 ambient = max(SampleSH(normal), lerp(.025,.12,_RainDownpour));
                half3 color = input.color.r > 1.5 ? half3(.025,.032,.035) + glint * light.color * .5 : ambient * .65 + light.color * (glint * .25 + saturate(light.direction.y) * .12);
                return half4(color * alpha, alpha);
            }
            half4 Frag(Varyings input):SV_Target { clip(.5-_RainDownpour); return ContactFragment(input); }
            half4 FragHeavy(Varyings input):SV_Target { clip(_RainDownpour-.5); return ContactFragment(input); }
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="StormRainHeavy" }
            Blend One OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragHeavy
            ENDHLSL
        }
    }
}
