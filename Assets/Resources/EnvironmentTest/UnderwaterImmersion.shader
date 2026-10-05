Shader "Hidden/PirateSlop/UnderwaterImmersion"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X_FLOAT(_UnderwaterSceneDepth);
            float4 _WaterMedium, _WaterExtinction, _WaterFogTint, _WaterWhirlpool, _WaterWhirlpoolCenter;
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float blend = smoothstep(0, .18, _WaterMedium.z);
                float2 distortion = float2(sin(_Time.y * .38 + uv.y * 11), cos(_Time.y * .31 + uv.x * 9)) * .0007 * blend;
                uv = saturate(uv + distortion);
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float rawDepth = SAMPLE_TEXTURE2D_X(_UnderwaterSceneDepth, sampler_PointClamp, uv).r;
                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif
                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 cameraWS = GetCameraPositionWS();
                float3 ray = normalize(positionWS - cameraWS);
                bool sky = abs(rawDepth - UNITY_RAW_FAR_CLIP_VALUE) < .000001;
                float path = min(distance(cameraWS, positionWS), 300);
                if (sky && ray.y > .001)
                {
                    path = min(max(0, _WaterMedium.z) / ray.y, 300);
                    positionWS = cameraWS + ray * path;
                }
                float ratio = length(positionWS.xz - _WaterWhirlpoolCenter.xz) / max(_WaterWhirlpool.x, .01);
                float bowl = _WaterWhirlpool.x > 0 ? -_WaterWhirlpool.y * pow(1 - saturate(ratio), 2) : 0;
                float depth = max(0, _WaterWhirlpool.z + bowl - positionWS.y);
                half depthLight = .30 + .70 * exp(-depth * .012);
                Light light = GetMainLight();
                half3 gi = max(SampleSH(float3(0, 1, 0)), 0);
                half3 sun = light.color * saturate(light.direction.y);
                half illumination = clamp(dot(gi + sun * .2, half3(.2126, .7152, .0722)), .18, 1.3);
                half3 fog = _WaterFogTint.rgb * illumination * (.45 + .55 * exp(-max(0, _WaterMedium.z) * .012));
                half3 transmission = exp(-_WaterExtinction.rgb * path);
                half3 water = scene.rgb * depthLight * transmission + fog * (1 - transmission);
                return half4(lerp(scene.rgb, water, blend), scene.a);
            }
            ENDHLSL
        }
    }
}
