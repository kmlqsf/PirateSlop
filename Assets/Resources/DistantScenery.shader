Shader "PirateSlop/DistantScenery"
{
    Properties
    {
        _Stone("Stone", Color) = (.05,.08,.09,1)
        _Haze("Haze", Color) = (.3,.45,.55,1)
        _SeaLevel("Sea level", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _Stone, _Haze;
        float _SeaLevel;
        CBUFFER_END
        struct A { float4 vertex:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
        V vert(A input)
        {
            V output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.world = TransformObjectToWorld(input.vertex.xyz);
            output.positionCS = TransformWorldToHClip(output.world);
            output.normal = TransformObjectToWorldNormal(input.normal);
            return output;
        }
        float Visibility(V input)
        {
            float distance = length(input.world - GetCameraPositionWS());
            float nearFade = smoothstep(650, 950, distance);
            float farFade = 1 - smoothstep(4200, 5000, distance);
            float dither = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(.06711056, .00583715))));
            clip(nearFade * farFade - dither - .001);
            clip(input.world.y - _SeaLevel + .35);
            return distance;
        }
        half4 frag(V input):SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float distance = Visibility(input);
            return half4(lerp(_Stone.rgb, _Haze.rgb, 1 - exp(-distance / 3400)), 1);
        }
        half4 depth(V input):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); Visibility(input); return input.positionCS.z; }
        half4 normals(V input):SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            Visibility(input);
            float3 normal = normalize(input.normal);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * .5 + .5)),0);
            #else
                return half4(normal,0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "Horizon"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            ZWrite On
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment depth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment normals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
