Shader "PirateSlop/BottleFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (.23, .28, .29, 1)
        _Density ("Density", Float) = .18
        _VolumeCenter ("Volume Center", Vector) = (0, 15.4, 0, 0)
        _VolumeRadii ("Volume Radii", Vector) = (50, 22, 50, 0)
        _VolumeAxisX ("Volume Axis X", Vector) = (1, 0, 0, 0)
        _VolumeAxisY ("Volume Axis Y", Vector) = (0, 1, 0, 0)
        _VolumeAxisZ ("Volume Axis Z", Vector) = (0, 0, 1, 0)
        _CloudAge ("Cloud Age", Float) = 0
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = 0
        _InsideBottle ("Inside Bottle", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
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
                half4 _FogColor;
                float _Density;
                float4 _VolumeCenter;
                float4 _VolumeRadii;
                float4 _VolumeAxisX;
                float4 _VolumeAxisY;
                float4 _VolumeAxisZ;
                float _CloudAge;
                float _CloudOpacity;
                float _InsideBottle;
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

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_CloudOpacity <= .001) discard;
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 origin = GetCameraPositionWS();
                float3 direction = normalize(input.positionWS - origin);
                float3 radii = max(_VolumeRadii.xyz, float3(.0001, .0001, .0001));
                float3 centerDelta = origin - _VolumeCenter.xyz;
                float3 relativeOrigin = float3(dot(centerDelta, _VolumeAxisX.xyz),
                    dot(centerDelta, _VolumeAxisY.xyz), dot(centerDelta, _VolumeAxisZ.xyz)) / radii;
                float3 relativeDirection = float3(dot(direction, _VolumeAxisX.xyz),
                    dot(direction, _VolumeAxisY.xyz), dot(direction, _VolumeAxisZ.xyz)) / radii;
                if (_InsideBottle > .5)
                {
                    relativeOrigin = TransformWorldToObject(origin) * 2.0;
                    relativeDirection = mul((float3x3)GetWorldToObjectMatrix(), direction) * 2.0;
                }
                float a = dot(relativeDirection, relativeDirection);
                float b = dot(relativeOrigin, relativeDirection);
                float c = dot(relativeOrigin, relativeOrigin) - 1.0;
                float discriminant = b * b - a * c;
                if (discriminant <= 0.0) discard;
                float root = sqrt(discriminant);
                float start = max(0.0, (-b - root) / a);
                float finish = (-b + root) / a;
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
                    finish = min(finish, dot(scenePosition - origin, direction));
                }
                if (finish <= start) discard;
                const int steps = 40;
                float stepLength = (finish - start) / steps;
                float transmittance = 1.0;
                float3 accumulated = 0.0;
                float3 drift = float3(_CloudAge * .064, -_CloudAge * .015, _CloudAge * .038);
                [loop]
                for (int index = 0; index < steps; index++)
                {
                    float distanceAlongRay = start + (index + .5) * stepLength;
                    float3 position = origin + direction * distanceAlongRay;
                    float3 positionDelta = position - _VolumeCenter.xyz;
                    float3 local = float3(dot(positionDelta, _VolumeAxisX.xyz),
                        dot(positionDelta, _VolumeAxisY.xyz), dot(positionDelta, _VolumeAxisZ.xyz)) / radii;
                    if (_InsideBottle > .5) local = relativeOrigin + relativeDirection * distanceAlongRay;
                    float shape = 1.0 - smoothstep(.48, 1.0, length(local));
                    float rolling = Noise(local * 4.25 - drift);
                    float detail = Noise(local * 10.5 + drift * .6);
                    float density = shape * lerp(.48, 1.22, rolling) * lerp(.8, 1.1, detail) * _Density * _CloudOpacity;
                    float lighting = .86 + .24 * saturate(local.y * .5 + .5) + .09 * rolling;
                    float3 fogColor = _FogColor.rgb;
                    if (_InsideBottle > .5)
                    {
                        float3 flow = float3(_CloudAge * .013, _CloudAge * .065, _CloudAge * .009);
                        float3 coordinates = local * 2.9;
                        float twist = local.y * .55 + _CloudAge * .045;
                        float sine = sin(twist);
                        float cosine = cos(twist);
                        coordinates.xz = float2(coordinates.x * cosine - coordinates.z * sine,
                            coordinates.x * sine + coordinates.z * cosine);
                        coordinates -= flow;
                        float3 warp = float3(Noise(coordinates * .68), Noise(coordinates * .68 + float3(7.1,3.6,1.4)),
                            Noise(coordinates * .68 + float3(2.4,8.3,5.7))) - .5;
                        coordinates += warp * .48;
                        rolling = Noise(coordinates);
                        detail = Noise(coordinates * 1.9 + flow * .08);
                        float fine = Noise(coordinates * 3.3 - flow * .10);
                        float billow = smoothstep(.18, .82, rolling * .70 + detail * .25 + fine * .05);
                        float boundary = 1.0 - smoothstep(.68, 1.0, length(local));
                        density = boundary * lerp(.18, 1.0, billow) * _Density * _CloudOpacity;
                        float upperDensity = Noise(coordinates + float3(.18,.7,.12));
                        lighting = .82 + .22 * saturate(local.y * .5 + .5) + .06 * fine - .18 * upperDensity * billow;
                        fogColor = lerp(_FogColor.rgb, float3(.79,.82,.85), .85);
                    }
                    float opacity = 1.0 - exp(-density * stepLength);
                    accumulated += transmittance * opacity * fogColor * lighting;
                    transmittance *= 1.0 - opacity;
                    if (transmittance < .002) break;
                }
                float alpha = min(.999, 1.0 - transmittance);
                return half4(accumulated / max(1.0 - transmittance, .0001), alpha);
            }
            ENDHLSL
        }
    }
}
