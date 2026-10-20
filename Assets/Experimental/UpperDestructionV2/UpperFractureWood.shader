Shader "PirateSlop/UpperFractureWood"
{
 Properties { _BaseColor("Wood", Color) = (0.37,0.24,0.11,1) _GrainScale("Grain", Float) = 1 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;
   float _GrainScale;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 grain:TEXCOORD1; half3 normalWS:TEXCOORD2; half fog:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
   Varyings Vert(Attributes v)
   {
    Varyings o;
    UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
    o.positionCS=p.positionCS; o.positionWS=p.positionWS;
    float3 scale=float3(length(GetObjectToWorldMatrix()._m00_m10_m20),length(GetObjectToWorldMatrix()._m01_m11_m21),length(GetObjectToWorldMatrix()._m02_m12_m22));
    o.grain=v.positionOS.xyz*scale*_GrainScale;
    o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.fog=ComputeFogFactor(p.positionCS.z);
    return o;
   }
   half4 Frag(Varyings i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 p=i.grain;
    float warp=sin(p.x*19+p.z*13)*.18+sin(p.y*7+p.z*9)*.1;
    float grain=sin(p.x*170+p.z*117+warp*12);
    float fine=sin(p.x*610+p.z*431+sin(p.y*25)*.8);
    float pores=smoothstep(.82,1.,sin(p.x*241+p.z*83+sin(p.y*41)))*.12;
    half3 albedo=_BaseColor.rgb*(.91+.09*grain+.025*fine-pores);
    half3 normal=normalize(i.normalWS);
    Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 lighting=SampleSH(normal)+light.color*(saturate(dot(normal,light.direction))*.85+.15)*light.shadowAttenuation;
    return half4(MixFog(albedo*lighting,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}