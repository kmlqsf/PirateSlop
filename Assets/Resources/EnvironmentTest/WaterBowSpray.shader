Shader "Hidden/PirateSlop/WaterBowSpray"
{
    Properties
    {
        _Tint("Water tint", Color) = (.62, .9, .94, 1)
        _SoftDistance("Soft intersection", Float) = .25
        [HideInInspector] _OceanReflection("Ocean sky", Cube) = "" {}
        [HideInInspector] _ReflectionAvailable("Reflection available", Float) = 0
        [HideInInspector] _ReflectionStrength("Reflection strength", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "WaterBowSpray"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float4 _BoatAttack_CameraWater;
            TEXTURECUBE(_OceanReflection);
            SAMPLER(sampler_OceanReflection);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _SoftDistance;
                float _ReflectionAvailable;
                float _ReflectionStrength;
            CBUFFER_END
            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
                half fog : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                output.color = input.color;
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float radius2 = dot(p, p);
                float aa = clamp(fwidth(radius2), .005, .5);
                float coverage = 1 - smoothstep(1 - aa, 1 + aa, radius2);
                float3 view = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                float3 dx = ddx(input.positionWS), dy = ddy(input.positionWS);
                float2 uvx = ddx(input.uv), uvy = ddy(input.uv);
                float3 tangent = dx * uvy.y - dy * uvx.y;
                float3 bitangent = dy * uvx.x - dx * uvy.x;
                tangent = dot(tangent, tangent) > 1e-12 ? normalize(tangent) : mul((float3x3)UNITY_MATRIX_I_V, float3(1, 0, 0));
                bitangent = dot(bitangent, bitangent) > 1e-12 ? normalize(bitangent) : SafeNormalize(cross(view, tangent));
                half3 normal = SafeNormalize(tangent * (p.x * .7) + bitangent * (p.y * .7) + view * max(.2, sqrt(saturate(1 - radius2))));
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float opaqueDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float particleDepth = -TransformWorldToView(input.positionWS).z;
                float soft = saturate((opaqueDepth - particleDepth) / max(_SoftDistance, .01));
                float nearFade = smoothstep(.18, .6, distance(GetCameraPositionWS(), input.positionWS));
                float pixels = 2 / max(length(ddx(input.uv)) + length(ddy(input.uv)), .001);
                float pixelFade = smoothstep(.35, 1.15, pixels);
                float rim = pow(1 - saturate(dot(normal, view)), 2);
                half alpha = min(.45, saturate(input.color.a * _Tint.a * coverage * soft * nearFade * pixelFade * (.28 + rim * .5)));
                clip(alpha - .002);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 ambient = max(SampleSH(normal), 0);
                half illumination = light.distanceAttenuation * light.shadowAttenuation;
                half ndotl = saturate(dot(normal, light.direction));
                half3 halfway = SafeNormalize(view + light.direction);
                half glint = pow(saturate(dot(normal, halfway)), 96) * .45 * illumination;
                half3 reflected = max(SampleSH(reflect(-view, normal)), 0);
                if (_ReflectionAvailable > .5)
                    reflected = SAMPLE_TEXTURECUBE_LOD(_OceanReflection, sampler_OceanReflection, reflect(-view, normal), 0).rgb;
                half fresnel = .02 + .98 * pow(1 - saturate(dot(normal, view)), 5);
                half3 transmittedLight = max(SampleSH(float3(0, 1, 0)), .025) + light.color * saturate(light.direction.y) * .2;
                half3 color = _Tint.rgb * transmittedLight * (1 - fresnel)
                    + reflected * (fresnel * _ReflectionStrength) + light.color * glint;
                color *= input.color.rgb;
                if (!(_BoatAttack_CameraWater.w > .5 && _BoatAttack_CameraWater.x > .5))
                    color = MixFog(color, input.fog);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
