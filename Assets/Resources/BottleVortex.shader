Shader "PirateSlop/BottleVortex"
{
    Properties
    {
        [HDR] _BaseColor ("Color", Color) = (.05, .38, 1, .85)
        _EmissionStrength ("Emission Strength", Float) = 3.5
        _Density ("Density", Float) = 14
        _SwirlAge ("Swirl Age", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Density;
                float _EmissionStrength;
                float _SwirlAge;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Hash(float3 value)
            {
                value = frac(value * .1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float Noise(float3 position)
            {
                float3 cell = floor(position);
                float3 f = frac(position);
                f = f * f * (3.0 - 2.0 * f);
                float low = lerp(lerp(Hash(cell), Hash(cell + float3(1,0,0)), f.x),
                    lerp(Hash(cell + float3(0,1,0)), Hash(cell + float3(1,1,0)), f.x), f.y);
                float high = lerp(lerp(Hash(cell + float3(0,0,1)), Hash(cell + float3(1,0,1)), f.x),
                    lerp(Hash(cell + float3(0,1,1)), Hash(cell + float3(1,1,1)), f.x), f.y);
                return lerp(low, high, f.z);
            }

            float SwirlDensity(float3 local, out float lighting)
            {
                float height = saturate(local.y + .5);
                float age = _SwirlAge;
                float3 flow = float3(age * .012, age * .13, -age * .008);
                float rolling = Noise(local * 5.2 - flow);
                float detail = Noise(local * 10.0 - flow * .75 + rolling * .35);
                local.xz += float2(sin(height * 5.2 + age * .22), cos(height * 4.4 - age * .19)) * .006;
                float radius = length(local.xz);
                float angle = atan2(local.z, local.x);
                float funnelRadius = .038 + .30 * pow(height, .8) + (rolling - .5) * .012;
                float width = lerp(.042, .095, height);
                float radial = exp(-pow((radius - funnelRadius) / width, 2.0));
                float ribbons = 0.0;
                [unroll]
                for (int arm = 0; arm < 6; arm++)
                {
                    float turn = arm * 1.04719755 + height * (5.1 + arm * .08) + age * (.58 + arm * .012);
                    turn += sin(height * 5.0 + age * (.18 + arm * .015) + arm * 2.0) * .065;
                    float strand = pow(saturate((cos(angle - turn) - .48) / .52), 1.25);
                    float pulse = .88 + .12 * sin(height * 5.0 - age * (.34 + arm * .02) + arm * 2.7);
                    ribbons += strand * pulse;
                }
                float breakup = lerp(.58, 1.0, smoothstep(.20, .80, rolling * .75 + detail * .25));
                float ends = smoothstep(.015, .11, height) * (1.0 - smoothstep(.87, .995, height));
                float hollow = smoothstep(.006, .025, radius);
                float boundary = 1.0 - smoothstep(.43, .495, radius);
                float density = radial * (.38 + ribbons * .80) * breakup * ends * hollow * boundary;
                lighting = .82 + .22 * height + .06 * detail - .13 * saturate(density);
                return density * _Density * saturate(_BaseColor.a);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 origin = TransformWorldToObject(GetCameraPositionWS());
                float3 direction = normalize(TransformWorldToObject(input.positionWS) - origin);
                float3 safeDirection = float3(direction.x >= 0.0 ? max(.00001, direction.x) : min(-.00001, direction.x),
                    direction.y >= 0.0 ? max(.00001, direction.y) : min(-.00001, direction.y),
                    direction.z >= 0.0 ? max(.00001, direction.z) : min(-.00001, direction.z));
                float3 nearPlane = (-.5 - origin) / safeDirection;
                float3 farPlane = (.5 - origin) / safeDirection;
                float3 entry = min(nearPlane, farPlane);
                float3 exitPoint = max(nearPlane, farPlane);
                float start = max(0.0, max(entry.x, max(entry.y, entry.z)));
                float finish = min(exitPoint.x, min(exitPoint.y, exitPoint.z));
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    bool hasGeometry = rawDepth > .00001;
                    float deviceDepth = rawDepth;
                #else
                    bool hasGeometry = rawDepth < .99999;
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                if (hasGeometry)
                {
                    float3 scenePosition = ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
                    finish = min(finish, dot(TransformWorldToObject(scenePosition) - origin, direction));
                }
                if (finish <= start) discard;
                const int steps = 48;
                float stepLength = (finish - start) / steps;
                float transmittance = 1.0;
                float3 accumulated = 0.0;
                float3 tint = max(_BaseColor.rgb, 0) * max(_EmissionStrength, 0);
                [loop]
                for (int index = 0; index < steps; index++)
                {
                    float3 local = origin + direction * (start + (index + .5) * stepLength);
                    float lighting;
                    float density = SwirlDensity(local, lighting);
                    float opacity = 1.0 - exp(-density * stepLength);
                    accumulated += transmittance * opacity * tint * lighting;
                    transmittance *= 1.0 - opacity;
                    if (transmittance < .01) break;
                }
                float alpha = 1.0 - transmittance;
                return half4(accumulated / max(alpha, .0001), alpha);
            }
            ENDHLSL
        }
    }
}
