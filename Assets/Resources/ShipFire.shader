Shader "PirateSlop/ShipFire"
{
    Properties
    {
        _MainTex ("Particle texture", 2D) = "white" {}
        _AlphaMask ("Use alpha mask", Range(0,1)) = 0
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0,1)) = 1
        _SoftDistance ("Soft distance", Float) = .1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _AlphaMask, _Intensity, _Fade, _SoftDistance;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float4 screen : TEXCOORD1; float eyeDepth : TEXCOORD2; half fog : TEXCOORD3; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.screen = ComputeScreenPos(position.positionCS);
                output.eyeDepth = -position.positionVS.z;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float depth = LinearEyeDepth(SampleSceneDepth(input.screen.xy / input.screen.w), _ZBufferParams);
                half softness = saturate((depth - input.eyeDepth) / max(.001, _SoftDistance));
                half4 sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half alphaMask = saturate(_AlphaMask);
                half mask = lerp(sample.r, sample.a, alphaMask);
                half alpha = mask * input.color.a * _Fade * softness;
                half3 textureColor = lerp(half3(1,1,1), sample.rgb, alphaMask);
                half3 color = input.color.rgb * textureColor * _Intensity;
                color = MixFog(color, input.fog);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
