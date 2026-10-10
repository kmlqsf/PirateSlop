Shader "PirateSlop/UnderwaterSuspension"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="UnderwaterSuspension" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shaders/ShipWaterInterior.hlsl"
            TEXTURE2D_X_FLOAT(_UnderwaterSceneDepth);
            float4 _BoatAttack_CameraWater, _UnderwaterExtinction;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                ClipShipWaterInterior(input.positionWS);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float raw = SAMPLE_TEXTURE2D_X(_UnderwaterSceneDepth, sampler_PointClamp, uv).r;
                float surface = LinearEyeDepth(raw, _ZBufferParams);
                float eye = -TransformWorldToView(input.positionWS).z;
                float path = distance(GetCameraPositionWS(), input.positionWS);
                float radial = saturate(1 - dot(input.uv * 2 - 1, input.uv * 2 - 1));
                half alpha = input.color.a * radial * radial * smoothstep(.8, 1.8, path) * saturate((surface - eye) / .4);
                alpha *= smoothstep(0, .18, _BoatAttack_CameraWater.z);
                half3 light = max(SampleSH(float3(0, 1, 0)), .025) + GetMainLight().color * .1;
                half3 color = input.color.rgb * light * exp(-_UnderwaterExtinction.rgb * path);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
