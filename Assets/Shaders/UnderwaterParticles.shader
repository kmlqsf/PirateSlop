Shader "PirateSlop/UnderwaterParticles"
{
    Properties { _Ring("Bubble",Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Ring;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float fog:TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color;o.uv=v.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float radius=length(i.uv*2-1);
                float disk=1-smoothstep(.1,1,radius);
                float ring=(1-smoothstep(.07,.2,abs(radius-.76)))*(.4+.6*i.uv.y);
                return half4(MixFog(i.color.rgb,i.fog),i.color.a*lerp(disk,ring,saturate(_Ring)));
            }
            ENDHLSL
        }
    }
}
