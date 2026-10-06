Shader "PirateSlop/CoastalRock"
{
    Properties
    {
        _BaseMap ("Rock colour", 2D) = "white" {}
        _BumpMap ("Rock normal", 2D) = "bump" {}
        _BaseColor ("Stone tint", Color) = (.82,.79,.70,1)
        _NormalScale ("Normal strength", Range(0,2)) = .6
        _TextureScale ("Texture metres", Float) = .35
        _Smoothness ("Dry smoothness", Range(0,1)) = .12
        _MossColor ("Moss", Color) = (.21,.28,.105,1)
        _TerrainBlend ("Use terrain colours", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        float _CoastalSeaLevel;
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _MossColor;
            float _NormalScale, _TextureScale, _Smoothness, _TerrainBlend;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float4 color : TEXCOORD2;
            float fog : TEXCOORD3;
            half4 weathering : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        float Hash(float3 p)
        {
            p = frac(p * .1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }
        float Noise(float3 p)
        {
            float3 i = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            float4 a = float4(Hash(i),Hash(i+float3(1,0,0)),Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)));
            float4 b = float4(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),Hash(i+float3(0,1,1)),Hash(i+float3(1,1,1)));
            return lerp(lerp(lerp(a.x,a.y,f.x),lerp(a.z,a.w,f.x),f.y),lerp(lerp(b.x,b.y,f.x),lerp(b.z,b.w,f.x),f.y),f.z);
        }
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.color = input.color;
            output.fog = ComputeFogFactor(position.positionCS.z);
            float3 p = position.positionWS;
            output.weathering = half4(Noise(p*.04),Noise(p*.17),Noise(float3(p.x*.28,p.y*.018,p.z*.28)),Noise(p*.35));
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float3 weights = pow(abs(n), 4);
                weights /= max(dot(weights, 1), .0001);
                float3 p = input.positionWS * _TextureScale;
                half3 base = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy).rgb * weights.x + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb * weights.y + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb * weights.z;
                half luminance = dot(base,half3(.3,.59,.11));
                base = saturate(half3(.52,.51,.47)+(base-luminance)*.2+(luminance-.16)*.95);
                float3 nx = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,p.zy),_NormalScale);
                float3 ny = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,p.xz),_NormalScale);
                float3 nz = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,p.xy),_NormalScale);
                float3 detail = float3(0,nx.y,nx.x)*weights.x + float3(ny.x,0,ny.y)*weights.y + float3(nz.x,nz.y,0)*weights.z;
                n = normalize(n + detail * .8);
                float streak = smoothstep(.66,.86,input.weathering.z);
                float wet = 1 - smoothstep(.08,1.15,input.positionWS.y - _CoastalSeaLevel + (input.weathering.w-.5)*.65);
                float moss = smoothstep(.6,.84,input.weathering.y) * saturate(streak*.3 + weights.y*.45) * (1-wet);
                half3 color = base * _BaseColor.rgb * lerp(.86,1.08,input.weathering.x) * lerp(1,.8,streak);
                color = lerp(color,_MossColor.rgb,moss);
                color *= lerp(1,.57,wet);
                color = lerp(color,color*lerp(half3(.94,.87,.67),half3(.78,.80,.56),saturate(input.color.g-input.color.r+.2)),_TerrainBlend*weights.y);
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = n;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI = SampleSH(n);
                data.shadowMask = half4(1,1,1,1);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = color;
                surface.alpha = 1;
                surface.smoothness = lerp(_Smoothness,.3,wet);
                surface.occlusion = 1;
                surface.normalTS = half3(0,0,1);
                half4 result = UniversalFragmentPBR(data,surface);
                result.rgb = MixFog(result.rgb,input.fog);
                return result;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output=Vert(input);
                float3 direction=_LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                direction=normalize(_LightPosition-output.positionWS);
                #endif
                output.positionCS=TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,direction));
                #if UNITY_REVERSED_Z
                output.positionCS.z=min(output.positionCS.z,UNITY_NEAR_CLIP_VALUE*output.positionCS.w);
                #else
                output.positionCS.z=max(output.positionCS.z,UNITY_NEAR_CLIP_VALUE*output.positionCS.w);
                #endif
                return output;
            }
            half4 DepthFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFrag(Varyings input) : SV_Target
            {
                float3 normal=normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal)*.5+.5)),0);
                #else
                return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
    }
}
