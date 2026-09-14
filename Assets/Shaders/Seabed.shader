Shader "PirateSlop/Seabed"
{
    Properties { _BaseColor("Sand",Color)=(.48,.43,.29,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert(float4 positionOS:POSITION):SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag(float4 positionCS:SV_POSITION):SV_Target { return positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float fog:TEXCOORD2;};
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.positionWS.xz;
                float ripple=sin(p.x*3.2+sin(p.y*.35)*2.5);
                float grain=frac(sin(dot(floor(p*55),float2(12.9898,78.233)))*43758.5453);
                float waveA=sin(p.x*.8+p.y*.55+_Time.y*.45);
                float waveB=sin(p.y*.95-p.x*.4-_Time.y*.35);
                float caustic=pow(saturate(1-abs(waveA+waveB)*2.5),5)*.14;
                Light light=GetMainLight();float3 normal=normalize(i.normalWS);
                float3 lighting=max(SampleSH(normal),float3(.18,.23,.24))+light.color*(saturate(dot(normal,light.direction))*.5+.1);
                float3 color=_BaseColor.rgb*(.9+ripple*.06+grain*.09)*lighting+caustic*float3(.4,.65,.57);
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
