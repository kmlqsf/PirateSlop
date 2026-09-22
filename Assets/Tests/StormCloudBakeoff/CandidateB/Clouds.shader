Shader "CloudsURP"
{
    Properties
    {
        _Color_1("_Color_1", Color) = (1,1,1,1)
        _Color_2("_Color_2", Color) = (1,1,1,1)
        _Color_3("_Color_3", Color) = (1,1,1,1)

        _EdgeFalloff("Edge Falloff", Range(0, 1)) = 0.4
        _DisplacementAmpl("Displacement Amplitude", Range(0, 5)) = 1.0
        _DensityScale("Density Scale", Range(0.3, 2.0)) = 0.7 
        _ExtinctionCoeff("Extinction Coefficient", Range(0.1, 10.0)) = 0.8 

        _QualityMultiplier("_QualityMultiplier", Range(1, 100.0)) = 1
        
        _StepSize("Step Size", Range(0.05, 10)) = 0.2
        //_VolumeSteps("Volume Steps", Range(1, 48)) = 24 
        //_LightSteps("LightSteps", Range(1, 6)) = 3 
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }
        LOD 100

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define MAX_SPHERE_COUNT 20    

            struct appdata 
            { 
                half4 positionOS : POSITION; 
            };

            struct v2f 
            { 
                half4 positionCS : SV_POSITION; 
                half3 positionWS : TEXCOORD0; 
                half3 positionVS : TEXCOORD1; 
                half3 viewDir : TEXCOORD2; 
                half4 screenPos : TEXCOORD3; 
            };

            half4 _Spheres[MAX_SPHERE_COUNT];    
            half _SphereCount;

            half _EdgeFalloff;
            half _DisplacementAmpl;
            half _DensityScale;
            half _ExtinctionCoeff;
            
            half _VolumeSteps;
            half _LightSteps;
            half _StepSize;
            half _QualityMultiplier;

            half4 _Color_1;
            half4 _Color_2;
            half4 _Color_3;

             v2f vert(appdata input)
            {
                v2f output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.positionVS = vertexInput.positionVS;
                output.screenPos = ComputeScreenPos(output.positionCS);  
                output.viewDir = _WorldSpaceCameraPos - output.positionWS;    

                return output;
            }

            //noise funcs from https://www.shadertoy.com/view/Xl33zH

            half hash( half n ) { return frac(sin(n)*753.5453123); }
            
            half noise( half3 x )
            {
                half3 p = floor(x);
                half3 f = frac(x);
                f = f*f*(3.0-2.0*f);
            
                half n = p.x + p.y*157.0 + 113.0*p.z;
                return lerp(lerp(lerp( hash(n+  0.0), hash(n+  1.0),f.x),
                                 lerp( hash(n+157.0), hash(n+158.0),f.x),f.y),
                            lerp(lerp( hash(n+113.0), hash(n+114.0),f.x),
                                 lerp( hash(n+270.0), hash(n+271.0),f.x),f.y),f.z);
            }

            half fbm(half3 p)
            {
                half f = 0.5 * noise(p); 
                p = p * 2.02;
                f += 0.25 * noise(p); 
                p = p * 2.03; 
                f += 0.125 * noise(p);
                return f;
            }

            half getDensity(half3 pos, half4 sphere)
            {
                half distFromCenter = length(pos - sphere.xyz);
                half radius = sphere.w;

                //half3 localPos = pos - sphere.xyz * 0.5;
                
                half distFromSurface = abs(distFromCenter - radius * (fbm(pos) * _DisplacementAmpl)); //you can use localPos in fbm() instead
                
                half baseDensity = 1.0 - smoothstep(0.0, _EdgeFalloff, distFromSurface);
                
                half detail = fbm(pos * 3.0);
                baseDensity *= 0.3 + 0.7 * detail;

                baseDensity *= smoothstep(0.0, 0.15, baseDensity);

                return saturate(baseDensity);
            }

            half getSceneDensity(half3 pos)
            {
                half combinedDensity = 0.0;

                for(int i = 0; i < _SphereCount; i++)
                {
                    half distToCenter = length(pos - _Spheres[i].xyz);
                    if(distToCenter > _Spheres[i].w + _EdgeFalloff)
                    {
                        continue; 
                    } 

                    half d = getDensity(pos, _Spheres[i]);

                    d = pow(abs(d), _DensityScale);

                    combinedDensity += d * (1.0 - combinedDensity) * 0.6;

                    if(combinedDensity > 0.95) break;
                }
            
                return saturate(combinedDensity);
            }

            half getDirectionalLight(half3 pos, half3 lightDir)
            {
                half light = 1.0;
                half lightStep = 0.4;
                
                for(int j = 0; j < _LightSteps; j++)
                {
                    half3 samplePos = pos + lightDir * j * lightStep;
                    half dens = getSceneDensity(samplePos);
                    light *= 1.0 - dens * 0.7;  
                    if(light < 0.02) break;
                }
                return light;
            }
            
            
            half3 getAdditionallLight(half3 pos, half densHere)
            {
                half light = 1.0;
                half lightStep = 0.4;
                
                int lightsCount = min(GetAdditionalLightsCount(), 3);
                
                half pixelSeed = dot(pos.xy, half2(127.1, 311.7));
                half randomOffset = hash(pixelSeed);
                
                for(int i = 0; i < lightsCount; i++) 
                {
                    half t = randomOffset * 0.5;
                    Light addLight = GetAdditionalLight(i, pos);
                    half3 lightDir = normalize(addLight.direction);
                    
                    for(int j = 0; j < _LightSteps; j++)
                    {
                        half3 samplePos = pos + lightDir * t;
                        half dens = getSceneDensity(samplePos);
                        light *= 1.0 - dens * 0.7;  
                        t += lightStep;
                        if(light < 0.02) break;
                    }
                }
                
                return light;
            }

            half getSceneEyeDepth(half4 screenPosition, half3 addition)                                                    
            {                                                                                                              
                half rawDepth = SampleSceneDepth((screenPosition.xy / screenPosition.w) + addition);                       
                half sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);                                             
                return sceneEyeDepth;                                                                                      
            }                                                                                                              
                                                                                                                           
            half getDepthDifference(half4 screenPosition, half3 positionViewSpace)                                         
            {                                                                                                              
                half sceneEyeDepth = getSceneEyeDepth(screenPosition, 0);                                                  
                half fragmentEyeDepth = -positionViewSpace.z;                                                              
                                                                                                                           
                return 1 - saturate((sceneEyeDepth - fragmentEyeDepth) * 0.1);             
            }

            half3 GetGradientColor(half t) 
            {
                half3 color;

                if (t < 0.3) {
                    half localT = t / 0.3;
                    color = lerp(_Color_1.rgb, _Color_2.rgb, localT);
                }
                else if (t < 0.6) {
                    half localT = (t - 0.3) / 0.3;
                    color = lerp(_Color_2.rgb, _Color_3.rgb, localT);
                }
                else {
                    color = _Color_3.rgb;
                }

                return color;
            }
            
            half4 frag(v2f input) : SV_Target
            {
                half3 rayOrigin = input.positionWS;
                half3 rayDir = normalize(rayOrigin - _WorldSpaceCameraPos);

                half distance = 10.0;
                _VolumeSteps = 24;
                _StepSize = 0.3;

                half qualityMaxValue = 11.0;

                _StepSize = (distance / _VolumeSteps) * (qualityMaxValue - _QualityMultiplier); 

                half pixelSeed = dot(input.positionCS.xy, half2(127.1, 311.7));
                half randomOffset = hash(pixelSeed) * _StepSize;
                rayOrigin += rayDir * randomOffset;
                
                half3 col = half3(0, 0, 0);
                half T = 1.0;
                
                Light mainLight = GetMainLight();
                half3 lightDir = normalize(mainLight.direction);

                half depthDifference = getDepthDifference(input.screenPos, input.positionVS);

                for(int i = 0; i < _VolumeSteps; i++)
                {
                    if(T < 0.01) break;
                    
                    half density = getSceneDensity(rayOrigin);
                    
                    if(density > 0.02) 
                    {
                        half extinction = density * _ExtinctionCoeff * _StepSize;
                        half alpha = 1.0 - exp(-extinction);  
                        
                        //half directionalLightValue = getDirectionalLight(rayOrigin, lightDir);
                        half lighting = getAdditionallLight(rayOrigin, density);
                        
                        half3 color = GetGradientColor(lighting);// + directionalLightValue);

                        half marchProgress = i / (half)_VolumeSteps;
                        
                        if(depthDifference > marchProgress)
                        {
                            break;
                        }

                        col += color * alpha * T;
                        T *= exp(-extinction);
                    }

                    rayOrigin += rayDir * _StepSize;
                }

                return half4(col, saturate(1.0 - T));
            }
            ENDHLSL
        }
    }
}