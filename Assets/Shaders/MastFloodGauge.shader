Shader "PirateSlop/Mast Flood Gauge"
{
    Properties
    {
        [HideInInspector] _Cull("Cull", Float) = 2
        [PerRendererData] _FloodLevels("Water and death progress", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "MastGlassAndBrass"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            UNITY_INSTANCING_BUFFER_START(Gauge)
                UNITY_DEFINE_INSTANCED_PROP(float4, _FloodLevels)
            UNITY_INSTANCING_BUFFER_END(Gauge)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                output.fog = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            float Ripple(float x, float time)
            {
                return .0028 * sin(x * 5.8 + time * 2.3) + .0013 * sin(x * 12.5 - time * 1.7);
            }

            float Bubbles(float2 uv, float time)
            {
                float2 cells = uv * float2(3.8, 38.0) - float2(0, time * .45);
                float2 id = floor(cells);
                float seed = frac(52.9829189 * frac(dot(id, float2(.06711056, .00583715))));
                float2 center = float2(.18 + .64 * seed, .27 + .46 * frac(seed * 11.3));
                float2 delta = frac(cells) - center;
                delta.x += .045 * sin(time + seed * 18);
                float radius = .026 + seed * .056;
                float dist = length(delta);
                float aa = max(.017, max(fwidth(cells.x), fwidth(cells.y)) * .55);
                float rim = 1.0 - smoothstep(0, aa, abs(dist - radius));
                return rim * smoothstep(.48, .84, seed) * saturate(1.0 - aa * 4.0);
            }

            half3 Chamber(float2 uv, float2 levels, half light, half fresnel, half highlight)
            {
                float water = saturate(levels.x);
                float death = saturate(levels.y);
                float time = _Time.y;
                float wave = Ripple(uv.x, time);
                float surface = water + wave * saturate(min(water, 1.0 - water) * 80.0);
                float redLine = (death + wave * .58 * saturate(min(death, 1.0 - death) * 80.0)) * water;
                float aa = max(.00025, fwidth(uv.y) * .65);
                float wet = water <= 0 ? 0 : water >= 1 ? 1 : 1 - smoothstep(surface - aa, surface + aa, uv.y);
                float red = death <= 0 ? 0 : death >= 1 ? 1 : 1 - smoothstep(redLine - aa, redLine + aa, uv.y);
                float edge = abs(uv.x * 2.0 - 1.0);
                float body = 1.0 - .65 * edge * edge;
                half3 interior = lerp(half3(.012,.013,.012), half3(.067,.053,.035), body * (.55 + .45 * uv.y));
                half3 blue = lerp(half3(.008,.12,.17), half3(.027,.48,.57), body * (.48 + .52 * uv.y));
                half3 crimson = lerp(half3(.19,.008,.016), half3(.66,.033,.042), body * (.58 + .42 * uv.y));
                half3 fluid = lerp(blue, crimson, red);
                fluid += Bubbles(uv, time) * lerp(half3(.17,.30,.29), half3(.24,.09,.075), red);
                half3 color = lerp(interior, fluid, wet);
                float waterLip = 1.0 - smoothstep(aa, aa + .0017, abs(uv.y - surface));
                color += waterLip * saturate(min(water, 1.0-water) * 100.0) * half3(.20,.52,.51);
                float redLip = 1.0 - smoothstep(aa, aa + .0012, abs(uv.y - redLine));
                color += redLip * wet * saturate(min(death, 1.0-death) * 90.0) * half3(.44,.13,.105);
                float graduation = abs(frac(uv.y * 8.0 + .5) - .5) / 8.0;
                float marks = (1.0 - smoothstep(.0005, aa + .0012, graduation)) * smoothstep(.76,.81,edge);
                color = lerp(color, half3(.47,.43,.27), marks * .56);
                float seam = 1.0 - smoothstep(.008,.045,abs(uv.x - .11));
                return color * light + half3(.070,.083,.083) * fresnel + half3(.48,.56,.52) * highlight + seam * half3(.023,.037,.039);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight();
                half diffuse = saturate(dot(n, sun.direction));
                half3 ambient = max(SampleSH(n), half3(.12,.13,.14));
                half fresnel = pow(1.0h-saturate(dot(n,view)), 3.0h);
                half spec = saturate(dot(n, SafeNormalize(sun.direction + view)));
                half3 color;
                if (input.color.a > .5h)
                {
                    half3 bronze = input.color.rgb;
                    #ifndef UNITY_COLORSPACE_GAMMA
                        bronze = SRGBToLinear(bronze);
                    #endif
                    color = bronze * (ambient + sun.color * (.17h + .87h * diffuse));
                    color += half3(.66,.47,.24) * sun.color * pow(spec, 37.0h) * diffuse;
                    color += half3(.08,.073,.05) * fresnel;
                }
                else
                {
                    float2 levels = UNITY_ACCESS_INSTANCED_PROP(Gauge, _FloodLevels).xy;
                    half light = clamp(.57h + Luminance(ambient) * .35h + Luminance(sun.color) * diffuse * .43h, .53h, 1.32h);
                    color = Chamber(input.uv, levels, light, fresnel, pow(spec, 72.0h) * diffuse);
                }
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
