Shader "PirateSlop/Ship Ice"
{
    Properties
    {
        _FreezeFade("Fade", Range(0,1)) = 1
        _FreezeAge("Age", Float) = 5
        _Solid("Crystal", Range(0,1)) = 0
        _Offset("Shell offset", Float) = .003
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-5" "RenderType"="Transparent" }
        Pass
        {
            Name "Ice"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _FreezeFade, _FreezeAge, _Solid, _Offset;
                float3 _FreezeOrigin;
                float4x4 _ShipWorldToLocal;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 local : TEXCOORD1; half3 normalWS : TEXCOORD2; half fog : TEXCOORD3; half3 vertexLight : TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            float Hash(float3 p) { p = frac(p * .1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            float Noise(float3 p)
            {
                float3 cell = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(cell), Hash(cell+float3(1,0,0)), f.x), lerp(Hash(cell+float3(0,1,0)), Hash(cell+float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash(cell+float3(0,0,1)), Hash(cell+float3(1,0,1)), f.x), lerp(Hash(cell+float3(0,1,1)), Hash(cell+1), f.x), f.y), f.z);
            }
            float Crack(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                float nearest = 8, second = 8;
                for (int y=-1;y<=1;y++) for (int x=-1;x<=1;x++)
                {
                    float2 at = float2(x,y);
                    float2 seed = float2(Hash(float3(cell+at, 1)), Hash(float3(cell+at, 9)));
                    float2 delta = at + .18 + seed*.64 - f;
                    float d = dot(delta,delta);
                    if (d < nearest) { second = nearest; nearest = d; } else second = min(second,d);
                }
                float edge = second-nearest;
                return 1-smoothstep(.012, .012+max(fwidth(edge),.002), edge);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz) + normal * _Offset;
                output.local = mul(_ShipWorldToLocal,float4(output.positionWS,1)).xyz;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = normal;
                output.vertexLight = VertexLighting(output.positionWS,normal);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 p = input.local;
                half3 normal = normalize(input.normalWS);
                float3 localNormal = normalize(mul((float3x3)_ShipWorldToLocal,normal));
                float3 weights = pow(abs(localNormal),4); weights /= max(dot(weights,1),.001);
                float islands = Noise(p*1.55)*.68 + Noise(p*4.7+7)*.32;
                float grain = Noise(p*44);
                float threshold = lerp(.61,.54,saturate(localNormal.y));
                float mask = smoothstep(threshold-.035,threshold+.045,islands);
                float growth = smoothstep(0,.12,_FreezeAge-distance(p,_FreezeOrigin)/160);
                float cracks = Crack(p.yz*5)*weights.x + Crack(p.xz*5)*weights.y + Crack(p.xy*5)*weights.z;
                half alpha = lerp(mask * (.48 + .28*grain) * growth, .86, _Solid) * _FreezeFade;
                clip(alpha-.015);
                half3 facet = normalize(normal + half3(sin(p.z*92+p.x*37),sin(p.x*81+p.y*42),sin(p.y*74+p.z*51))*.065*(1-_Solid));
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = facet;
                lighting.vertexLighting = input.vertexLight;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(facet);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1,1,1,1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(half3(.38,.52,.59),half3(.73,.81,.83),saturate(grain*.65+islands*.55));
                surface.albedo *= 1-cracks*.24*(1-_Solid);
                surface.specular = half3(.055,.07,.08);
                surface.smoothness = lerp(.69,.91,grain) - cracks*.18*(1-_Solid);
                surface.occlusion = 1;
                surface.normalTS = half3(0,0,1);
                surface.alpha = alpha;
                half4 result = UniversalFragmentPBR(lighting,surface);
                result.rgb = MixFog(result.rgb,input.fog);
                return half4(result.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
