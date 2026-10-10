// SimpleWaterURP.shader
// Simple, lightweight stylized water shader for URP. Built for a free release.
//
// FEATURES: animated waves, world-position-based shallow/deep color + transparency,
// panning normal-map ripples, fresnel reflection, shoreline foam.
//
// HOW DEPTH WORKS (world-position based, not a raw depth subtraction):
// The camera depth texture tells us how far the camera is from whatever is behind
// the water (the lake bed, a submerged rock, etc). We use that to reconstruct the
// actual WORLD POSITION of that point, then compare its height (Y) to the water
// surface's own height. The vertical gap between them is the "depth" of the water
// at that pixel. This stays accurate at any camera angle, unlike simply subtracting
// screen-space depth values, which stretches near the shore when viewed at a
// grazing angle.
//
// SETUP:
// 1. In your URP Asset, enable "Depth Texture" (Rendering section). That's the
//    only setting needed — no Opaque Texture required, and the water mesh can be
//    a plain flat plane (no special shaping needed).
// 2. Assign a tileable normal map + a soft noise texture for foam.
// 3. Set _WaterDepth to roughly how many world units deep your water body is
//    before it should read as "fully deep" colored.

Shader "Custom/SimpleWaterURP"
{
    Properties
    {
        [Header(Waves)]
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1.0
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.15
        _WaveScale ("Wave Scale", Range(0.1, 10)) = 1.0
        [HideInInspector] _WaveTime ("Network Wave Time", Float) = 0
        [HideInInspector] _UseWaveTime ("Use Network Wave Time", Float) = 0
        [Toggle] _WorldSpaceUV ("World Space Ripples", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Water Color)]
        _ShallowColor ("Shallow Color", Color) = (0.42, 0.75, 0.75, 0.55)
        _DeepColor ("Deep Color", Color) = (0.02, 0.18, 0.32, 0.95)
        _WaterDepth ("Water Depth (max, world units)", Range(0.1, 20)) = 3.0

        [Header(Ripples)]
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalTiling ("Normal Tiling", Float) = 1.0
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.5
        _NormalSpeed ("Normal Speed", Range(0, 2)) = 0.1

                [Header(Whirlpool)]
        _WhirlpoolCenter ("Whirlpool Center", Vector) = (0,0,0,0)
        _WhirlpoolRadius ("Whirlpool Radius", Float) = 100.0
        _WhirlpoolDepth ("Whirlpool Depth", Float) = 0.0
        _WhirlpoolTwist ("Whirlpool Twist", Float) = 2.0

        [Header(Reflection)]
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 3.0
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.6
        _OceanReflection ("Ocean Sky Reflection", Cube) = "black" {}
        [HideInInspector] _UseOceanReflection ("Use Ocean Reflection", Float) = 0
        _SunStrength ("Sun Highlights", Range(0, 3)) = 1
        _SunSharpness ("Sun Sharpness", Range(16, 512)) = 128

        [Header(Foam)]
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamDistance ("Foam Distance (world units)", Range(0.01, 3)) = 0.4
        _FoamNoiseTex ("Foam Noise Texture", 2D) = "white" {}
        _FoamTiling ("Foam Tiling", Float) = 1.0
        _FoamSpeed ("Foam Speed", Range(0, 2)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
            #include "Assets/Shaders/CoastalShoreFoam.hlsl"
            #include "Assets/Shaders/ShipWaterInterior.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float _WaveSpeed;
                float _WaveStrength;
                float _WaveScale;
                float _WaveTime;
                float _UseWaveTime;
                float _WorldSpaceUV;

                float4 _ShallowColor;
                float4 _DeepColor;
                float _WaterDepth;

                float _NormalTiling;
                float _NormalStrength;
                float _NormalSpeed;

                float _FresnelPower;
                float _ReflectionStrength;
                float _UseOceanReflection;
                float _SunStrength;
                float _SunSharpness;

                float4 _FoamColor;
                float _FoamDistance;
                float _FoamTiling;
                float _FoamSpeed;
                float4 _WhirlpoolCenter;
                float _WhirlpoolRadius;
                float _WhirlpoolDepth;
                float _WhirlpoolTwist;
            CBUFFER_END
            float4 _SwellWaves[4];
            float _SeaChop;
            float3 SwellWave(float2 oceanPosition)
            {
                float3 value = 0;
                for (int i = 0; i < 4; i++)
                {
                    float4 wave = _SwellWaves[i];
                    float k = 6.2831853 / max(1.0, wave.w);
                    float phase = k * dot(wave.xy, oceanPosition) - sqrt(9.81 * k) * _WaveTime;
                    value.x += wave.z * sin(phase);
                    value.yz += wave.z * k * wave.xy * cos(phase);
                }
                return value;
            }
            float4 _Wakes[32];
            int _WakeCount;

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamNoiseTex); SAMPLER(sampler_FoamNoiseTex);
            TEXTURECUBE(_OceanReflection); SAMPLER(sampler_OceanReflection);

            // Local version of Unity's classic ComputeScreenPos, kept in-shader
            // so it doesn't depend on which URP version's helper macros exist.
            float4 ComputeScreenPosition(float4 positionCS)
            {
                float4 o = positionCS * 0.5;
                o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
                o.zw = positionCS.zw;
                return o;
            }


            float GetWhirlpoolHeight(float3 positionWS)
            {
                if (_WhirlpoolRadius <= 0.0) return 0.0;
                float dist = distance(positionWS.xz, _WhirlpoolCenter.xz);
                float t = saturate(dist / _WhirlpoolRadius);
                float falloff = 1.0 - t;
                return -_WhirlpoolDepth * (falloff * falloff);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                // Two simple sine waves summed = cheap, natural-looking motion
                float t = lerp(_Time.y, _WaveTime, _UseWaveTime) * _WaveSpeed;
                float wave1 = sin(positionWS.x * _WaveScale + t);
                float wave2 = sin((positionWS.z + positionWS.x * 0.5) * _WaveScale * 0.8 - t * 1.3);
                float chop = lerp(1.0, _SeaChop, _UseWaveTime);
                positionWS.y += (wave1 + wave2) * 0.5 * _WaveStrength * chop + SwellWave(positionWS.xz).x;
                
                positionWS.y += GetWhirlpoolHeight(positionWS);

                OUT.positionWS = positionWS;

                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.uv = lerp(IN.uv, positionWS.xz, _WorldSpaceUV);
                OUT.screenPos = ComputeScreenPosition(OUT.positionHCS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                ClipShipWaterInterior(IN.positionWS);
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float surfaceEyeDepth = IN.screenPos.w;

                // ---- Reconstruct the world position behind the water from the depth ----
                // ---- texture, then compare its height to the water surface's height. ----
                float rawSceneDepth = SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    float hasSceneGeometry = step(0.00001, rawSceneDepth);
                #else
                    float hasSceneGeometry = 1.0 - step(0.99999, rawSceneDepth);
                #endif
                float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);

                float3 viewVector = GetCameraPositionWS() - IN.positionWS;
                float3 viewDir = normalize(viewVector);
                float3 scenePositionWS = GetCameraPositionWS() - viewVector * (sceneEyeDepth / surfaceEyeDepth);
                float depthY = lerp(_WaterDepth * 20.0, max(IN.positionWS.y - scenePositionWS.y, 0.0), hasSceneGeometry);

                // ---- Ripple normal: one texture, panned in two directions ----
                float2 uvA = IN.uv * _NormalTiling + float2(1.0, 0.5) * _NormalSpeed * _Time.y;
                float2 uvB = IN.uv * _NormalTiling * 1.5 - float2(0.5, 1.0) * _NormalSpeed * _Time.y;
                float3 rippleA = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvA), 1.0);
                float3 rippleB = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB), 1.0);
                float distanceFade = lerp(1.0, .25, saturate(length(viewVector) / 500.0));
                float2 rippleXY = (rippleA.xy + rippleB.xy) * _NormalStrength * distanceFade;
                float oceanTime = lerp(_Time.y, _WaveTime, _UseWaveTime);
                float2 vortexDelta = IN.positionWS.xz - _WhirlpoolCenter.xz;
                float vortexRadius = length(vortexDelta);
                float vortexT = saturate(vortexRadius / max(1.0, _WhirlpoolRadius));
                float vortexActive = step(1.0, _WhirlpoolRadius) * saturate(_WhirlpoolDepth * .1);
                float vortexMask = smoothstep(.02, .10, vortexT) * (1.0 - smoothstep(.72, .97, vortexT)) * vortexActive;
                float flowAngle = vortexT * _WhirlpoolTwist * 6.283185 + oceanTime * .16;
                float flowSine, flowCosine;
                sincos(flowAngle, flowSine, flowCosine);
                float2 flowPosition = float2(vortexDelta.x * flowCosine - vortexDelta.y * flowSine, vortexDelta.x * flowSine + vortexDelta.y * flowCosine);
                float2 flowUV = flowPosition * _NormalTiling + float2(oceanTime * .025, 0.0);
                float2 flowNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, flowUV), 1.0).xy;
                float2 flowNormalWS = float2(flowNormal.x * flowCosine + flowNormal.y * flowSine, -flowNormal.x * flowSine + flowNormal.y * flowCosine);
                rippleXY = lerp(rippleXY, flowNormalWS * _NormalStrength * distanceFade * 1.5, vortexMask);
                float waveTime = lerp(_Time.y, _WaveTime, _UseWaveTime) * _WaveSpeed;
                float phaseA = IN.positionWS.x * _WaveScale + waveTime;
                float phaseB = (IN.positionWS.z + IN.positionWS.x * .5) * _WaveScale * .8 - waveTime * 1.3;
                float chop = lerp(1.0, _SeaChop, _UseWaveTime);
                float2 slope = float2(cos(phaseA) + .4 * cos(phaseB), .8 * cos(phaseB)) * _WaveScale * _WaveStrength * .5 * chop + SwellWave(IN.positionWS.xz).yz;
                float vortexFalloff = 1.0 - vortexT * vortexT * (3.0 - 2.0 * vortexT);
                float vortexSlope = 12.0 * _WhirlpoolDepth * vortexFalloff * vortexT * (1.0 - vortexT) / max(1.0, _WhirlpoolRadius);
                slope += vortexDelta / max(.01, vortexRadius) * vortexSlope * vortexActive;
                float3 normalWS = normalize(float3(rippleXY.x - slope.x, 1.0, rippleXY.y - slope.y));

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower);

                // ---- Depth-based color and transparency ----
                // Exponential falloff (like light absorption through water) instead of a
                // linear ramp — fades in quickly then eases off, reading as more natural.
                float depthFactor = 1.0 - saturate(exp(-depthY / max(_WaterDepth, 0.0001)));
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor);
                float alpha = lerp(_ShallowColor.a, _DeepColor.a, depthFactor);

                // ---- Reflection (nearest reflection probe / skybox) ----
                float3 reflectDir = reflect(-viewDir, normalWS);
                half4 reflectionRaw = SAMPLE_TEXTURECUBE(unity_SpecCube0, samplerunity_SpecCube0, reflectDir);
                half3 reflectionColor = DecodeHDREnvironment(reflectionRaw, unity_SpecCube0_HDR);
                reflectionColor = lerp(reflectionColor, SAMPLE_TEXTURECUBE_LOD(_OceanReflection, sampler_OceanReflection, reflectDir, 1).rgb, _UseOceanReflection);
                Light sun = GetMainLight();
                float diffuse = saturate(dot(normalWS, sun.direction));
                float3 halfDirection = normalize(sun.direction + viewDir);
                float specular = pow(saturate(dot(normalWS, halfDirection)), _SunSharpness);
                float broadHighlight = pow(saturate(dot(normalWS, halfDirection)), 24.0) * .08;
                waterColor *= .78 + diffuse * .22;
                float3 colorWithReflection = lerp(waterColor, reflectionColor, (.12 + .88 * fresnel) * _ReflectionStrength);
                colorWithReflection += sun.color * (specular + broadHighlight) * _SunStrength;

                // ---- Foam near shorelines / underwater objects ----
                float2 foamUV = IN.uv * _FoamTiling + float2(1.0, 1.0) * _FoamSpeed * _Time.y;
                  float foamNoise = SAMPLE_TEXTURE2D(_FoamNoiseTex, sampler_FoamNoiseTex, foamUV).r;
                float foamBreakup = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, foamUV * .17).g;
                float contactDepth = max(depthY, max(0.0, sceneEyeDepth - surfaceEyeDepth));
                float whirlpoolDistance = distance(IN.positionWS.xz, _WhirlpoolCenter.xz) / max(1.0, _WhirlpoolRadius);
                float whirlpoolFoamMask = 1.0 - (1.0 - smoothstep(0.95, 1.15, whirlpoolDistance)) * step(0.01, _WhirlpoolDepth);
                float foamEdge = (1.0 - smoothstep(0.0, _FoamDistance, contactDepth)) * hasSceneGeometry * whirlpoolFoamMask;
                float foamLine = foamEdge * smoothstep(.22, .68, foamBreakup + foamEdge * .25) * foamNoise;
                float2 coastalFoam = CoastalShoreMask(IN.positionWS);
                foamLine *= 1 - coastalFoam.r;
                foamLine = max(foamLine, coastalFoam.g * coastalFoam.r * smoothstep(.2, .72, foamNoise + foamBreakup * .3) * whirlpoolFoamMask);
                float vortexAngle = atan2(vortexDelta.y, vortexDelta.x + .00001);
                float spiralPhase = vortexAngle * 3.0 - vortexT * _WhirlpoolTwist * 6.283185 + oceanTime * .48;
                float spiralStripe = smoothstep(.80, .98, sin(spiralPhase));
                float spiralNoise = SAMPLE_TEXTURE2D(_FoamNoiseTex, sampler_FoamNoiseTex, flowPosition * .035 + float2(oceanTime * .018, 0.0)).r;
                float spiralFoam = spiralStripe * lerp(.35, 1.0, smoothstep(.2, .8, spiralNoise)) * vortexMask * .32;
                foamLine = max(foamLine, spiralFoam);
                float shipFoam = 0;
                for (int wakeIndex = 0; wakeIndex < min(_WakeCount, 32); wakeIndex++)
                {
                    float4 wake = _Wakes[wakeIndex];
                    float2 delta = IN.positionWS.xz - wake.xy;
                    float sine = sin(wake.z), cosine = cos(wake.z);
                    float sideways = abs(delta.x * cosine - delta.y * sine);
                    float ahead = delta.x * sine + delta.y * cosine;
                    float behind = max(0, -ahead - 15);
                    float lengthMask = smoothstep(0, 4, behind) * (1 - smoothstep(12, 45, behind));
                    float edge = 1 - smoothstep(.2, 1.8, abs(sideways - (4.5 + behind * .16)));
                    float center = (1 - smoothstep(0, 5.5 + behind * .12, sideways)) * .25;
                    float bow = (1 - smoothstep(.2, 1.1, abs(sideways - (6.2 - max(0, ahead - 12) * .35)))) * smoothstep(5, 12, ahead) * (1 - smoothstep(17, 20, ahead));
                    shipFoam = max(shipFoam, saturate(wake.w) * (lengthMask * (edge + center) + bow));
                }
                foamLine = max(foamLine, shipFoam * smoothstep(.12, .75, foamBreakup + foamNoise * .3));
                float3 finalColor = lerp(colorWithReflection, _FoamColor.rgb, saturate(foamLine));
                alpha = lerp(alpha, 1.0, saturate(foamLine));

                finalColor = MixFog(finalColor, IN.fogFactor);
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}

