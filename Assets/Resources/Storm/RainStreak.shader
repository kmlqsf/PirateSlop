Shader "PirateSlop/Storm Rain Streak"
{
    Properties
    {
        [HideInInspector] _ShelterHeight("Shelter height", 2D) = "white" {}
        _RainFade("Rain fade", Float) = 1
        _RainDownpour("Downpour", Float) = 0
        _RainWeather("Weather", Vector) = (0,0,0,40)
        _SeaLevel("Water", Float) = 0
        _ShelterField("Shelter field", Vector) = (0,0,40,40)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+25" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_ShelterHeight); SAMPLER(sampler_ShelterHeight);
            CBUFFER_START(UnityPerMaterial)
                float4 _ShelterField, _RainWeather;
                float _RainFade, _SeaLevel, _RainDownpour;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.world);
                output.uv = input.uv; output.color = input.color;
                return output;
            }
            half4 RainFragment(Varyings input)
            {
                clip(_RainWeather.z - .1);
                float distanceToZone = distance(input.world.xz, _RainWeather.xy) - _RainWeather.z;
                float halfWidth = max(.5, _RainWeather.w * .5);
                float weather = smoothstep(-halfWidth, halfWidth, distanceToZone);
                float2 fieldUV = (input.world.xz - _ShelterField.xy) / _ShelterField.zw;
                float inField = all(fieldUV >= 0) && all(fieldUV <= 1);
                float height = inField > .5 ? SAMPLE_TEXTURE2D_LOD(_ShelterHeight, sampler_ShelterHeight, fieldUV, 0).r : _SeaLevel - .15;
                clip(input.world.y - height - .01);
                float2 p = abs(input.uv * 2 - 1);
                float shape = pow(saturate(1 - p.x), 1.3) * saturate(1 - p.y * p.y);
                float scene = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS)), _ZBufferParams);
                float eye = -TransformWorldToView(input.world).z;
                float soft = saturate((scene - eye) / .25) * smoothstep(.2, .65, eye);
                half alpha = min(lerp(.36,.65,_RainDownpour), input.color.a * shape * lerp(1.25,2,_RainDownpour) * weather * _RainFade * soft);
                clip(alpha - .001);
                Light light = GetMainLight();
                half3 color = max(SampleSH(float3(0,1,0)), half3(.14,.17,.21)) * 1.1 + light.color * saturate(light.direction.y) * .13;
                color *= input.color.rgb;
                return half4(color * alpha, alpha);
            }
            half4 Frag(Varyings input):SV_Target { clip(.5-_RainDownpour); return RainFragment(input); }
            half4 FragHeavy(Varyings input):SV_Target { clip(_RainDownpour-.5); return RainFragment(input); }
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
