Shader "Unlit/SceneDepth"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            half4 Frag(Varyings input) : SV_Target
            {
                float depth = Linear01Depth(SampleSceneDepth(input.texcoord), _ZBufferParams);
                return float4(depth.xxx, 1);
            }
            ENDHLSL
        }
    }
}
