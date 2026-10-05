Shader "Hidden/PirateSlop/ShipFoamAtlas"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend One One
            BlendOp Max
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _AtlasWorld;
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; float4 age : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 age : TEXCOORD1; float2 world : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 clip = input.positionOS.xy * 2 - 1;
                #if UNITY_UV_STARTS_AT_TOP
                clip.y = -clip.y;
                #endif
                output.positionCS = float4(clip, 0, 1);
                output.uv = input.uv;
                output.age = input.age;
                output.world = input.positionOS.xy * _AtlasWorld.z + _AtlasWorld.xy;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x), lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (input.age.w > 3.5)
                {
                    float age = saturate(input.age.x);
                    float radius = length(input.uv);
                    float crown = exp(-pow((radius - .60) / .19, 2)) * .8;
                    float wash = (1 - smoothstep(.15, .58, radius)) * .25;
                    float breakup = smoothstep(.30, .65, Noise(input.world * .55 + input.age.y + float2(age * .12, -age * .10)));
                    float fade = smoothstep(0, .06, age + .025) * (1 - smoothstep(.25, 1, age));
                    return half4((crown + wash) * breakup * fade * input.age.z, 0, 0, 0);
                }
                if (input.age.w > 2.5)
                {
                    float s = input.uv.y;
                    float shoulder = .6 + max(0, s) * 1.4;
                    float width = 1.5 + max(0, s) * .03;
                    float cross = (abs(input.uv.x) - shoulder) / width;
                    float crest = exp(-cross * cross) * smoothstep(-.6, .4, s) * (1 - smoothstep(7, 10, s));
                    return half4(crest * input.age.z * .65, 0, crest * input.age.x, 0);
                }
                if (input.age.w > .5 && input.age.w < 1.5) return half4(0, 1, 0, 0);
                float age = saturate(input.age.x);
                float2 p = input.uv * float2(1.2, 1.8) + input.age.y + float2(age * .4, -age * .5);
                float noise = Noise(p);
                float boundary = dot(input.uv, input.uv) + (noise - .5) * .25;
                float shape = 1 - smoothstep(.1, 1, boundary);
                float erosion = lerp(.6, 1, noise) * (1 - age * .2);
                float fade = smoothstep(0, .07, age + .025) * (1 - smoothstep(.45, 1, age));
                float foam = saturate(shape * erosion * fade * input.age.z);
                if (input.age.w > 1.5) foam *= smoothstep(.38, .66, Noise(input.world * .30 + float2(_AtlasWorld.w * .035, -_AtlasWorld.w * .025)));
                return half4(foam, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
