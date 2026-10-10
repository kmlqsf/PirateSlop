Shader "Hidden/PirateSlop/Storm Rain Screen"
{
    Properties { [HideInInspector] _RainLensCount("Lens count", Integer) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X_FLOAT(_RainSceneDepth);
            float4 _RainWeather, _RainLens[24], _RainLensMotion[24], _RainDownpour;
            float4 _PirateStormTestClouds;
            float _RainSeaLevel;
            int _RainLensCount;

            float Outside(float2 start, float2 delta, float radius)
            {
                float a = dot(delta, delta);
                if (a < .0001) return dot(start,start) > radius * radius ? 1 : 0;
                float b = dot(start,delta), c = dot(start,start) - radius * radius;
                float discriminant = b*b - a*c;
                if (discriminant <= 0) return 1;
                float near = (-b - sqrt(discriminant)) / a;
                float far = (-b + sqrt(discriminant)) / a;
                return 1 - saturate(min(1,far) - max(0,near));
            }

            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 colorUV = input.texcoord;
                float2 bend = 0;
                float shine = 0, wet = 0;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                [loop] for (int i = 0; i < min(_RainLensCount,24); i++)
                {
                    float4 drop = _RainLens[i];
                    float2 p = (screenUV - drop.xy) * float2(aspect, 1) / max(.001, drop.z);
                    float age=_RainLensMotion[i].x;
                    p.y /= lerp(1.35,lerp(1.2,1.65,saturate(age*.4)),_RainDownpour.x);
                    if(abs(p.x)>1.3 || p.y< -1.2 || p.y>lerp(1.2,2.8,_RainDownpour.x))continue;
                    p.x+=sin(p.y*2.8+_RainLensMotion[i].y*6.283)*.1*_RainDownpour.x;
                    float radius2 = dot(p,p);
                    if (radius2 > 1.2 && (_RainDownpour.x<.5 || abs(p.x)>.22 || p.y<0 || p.y>2.8)) continue;
                    float body = 1 - smoothstep(.68, 1, radius2);
                    float rim = exp(-pow((sqrt(radius2) - .82) * 16, 2));
                    float trail=exp(-p.x*p.x*95)*smoothstep(.2,.9,p.y)*(1-smoothstep(1.5,2.8,p.y))*min(.22,age*.12)*_RainDownpour.x;
                    bend += p * body * drop.z * lerp(.07,.32,_RainDownpour.x) * drop.w / float2(aspect,1);
                    bend.x+=trail*p.x*drop.z*.3*drop.w/aspect;
                    shine += (rim * lerp(.035,.09,_RainDownpour.x) + exp(-dot(p - float2(-.3,.42),p - float2(-.3,.42)) * 32) * lerp(.045,.10,_RainDownpour.x) + trail*.12) * drop.w;
                    wet = max(wet, body * drop.w * .055);
                }
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, clamp(colorUV + bend, .001, .999));
                if (_RainWeather.z > 0 && _PirateStormTestClouds.x < .5)
                {
                    float rawDepth = SAMPLE_TEXTURE2D_X(_RainSceneDepth, sampler_PointClamp, screenUV).r;
                    #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                    #endif
                    float3 world = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                    float3 ray = world - GetCameraPositionWS();
                    float rayLength = length(ray);
                    float3 direction = ray / max(rayLength,.001);
                    float distance = min(rayLength,700);
                    float2 start = GetCameraPositionWS().xz - _RainWeather.xy;
                    float2 delta = direction.xz * distance;
                    float halfWidth = max(1,_RainWeather.w) * .5;
                    float outside = (Outside(start,delta,max(1,_RainWeather.z-halfWidth)) +
                        2 * Outside(start,delta,_RainWeather.z) + Outside(start,delta,_RainWeather.z+halfWidth)) * .25;
                    float midHeight = GetCameraPositionWS().y + direction.y * distance * .5;
                    float vertical = exp(-max(0,midHeight-_RainSeaLevel-35) / 75);
                    float exteriorWeight=smoothstep(10.0,50.0,length(start)-_RainWeather.z);
                    float opacity = min(.45,1-exp(-outside * distance * vertical / 550))*exteriorWeight;
                    if(_RainDownpour.x>.5)
                        opacity=min(_RainDownpour.w,1-exp(-outside*max(0,distance-4)*vertical*_RainDownpour.z))*_RainDownpour.y;
                    Light light = GetMainLight();
                    half3 fog = max(SampleSH(float3(0,1,0)),.018) * half3(.66,.76,.83) + light.color * saturate(light.direction.y) * .08;
                    color.rgb = lerp(color.rgb,fog,opacity);

                }
                color.rgb = color.rgb * (1-wet) + shine * max(SampleSH(float3(0,1,0)),.1);
                return color;
            }
            ENDHLSL
        }
    }
}


