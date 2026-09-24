Shader "PirateSlop/SeaMist"
{
    Properties
    {
        _MistColor("Mist Color", Color) = (.46,.54,.58,1)
        _Density("Density", Range(0,.04)) = .028
        _Height("Height", Float) = 100
        _MaxOpacity("Maximum Opacity", Range(0,1)) = .995
        _SeaLevel("Sea Level", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _MistColor;
            float _Density, _Height, _MaxOpacity, _SeaLevel;
            CBUFFER_END
            float4 _FogFlashes[16];
            int _FogFlashCount;
            float4 _FogClearCenter, _FogClearShape;
            float ClearMask(float3 p)
            {
                float2 offset=p.xz-_FogClearCenter.xz;
                float2 forward=normalize(_FogClearShape.xy);
                float2 local=float2(dot(offset,float2(forward.y,-forward.x)),dot(offset,forward));
                float distanceToHull=length(max(abs(local)-_FogClearShape.zw,0));
                return lerp(.025,1,smoothstep(5,22,distanceToHull));
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            float VolumeNoise(float3 p)
            {
                float z=floor(p.z), f=frac(p.z); f=f*f*(3-2*f);
                return lerp(Noise(p.xy+z*float2(37,17)),Noise(p.xy+(z+1)*float2(37,17)),f);
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                float depth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 world=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float3 delta=world-_WorldSpaceCameraPos;
                float distanceToSurface=length(delta);
                float3 direction=delta/max(distanceToSurface,.001);
                if(direction.y < -.0001 && _WorldSpaceCameraPos.y > _SeaLevel)
                    distanceToSurface=min(distanceToSurface,(_SeaLevel-_WorldSpaceCameraPos.y)/direction.y);
                float start=3;
                float span=max(0,min(distanceToSurface,320)-start);
                float opticalDepth=0;
                [loop] for(int s=0;s<24;s++)
                {
                    float u0=s/24.0, u1=(s+1)/24.0;
                    float stepLength=span*(u1*u1-u0*u0);
                    float3 p=_WorldSpaceCameraPos+direction*(start+span*(u0*u0+u1*u1)*.5);
                    float height=exp(-max(0,p.y-_SeaLevel)/max(1,_Height));
                    float3 q=p*float3(.026,.07,.026);
                    float a=VolumeNoise(q+float3(_Time.y*.018,-_Time.y*.008,_Time.y*.011));
                    float b=VolumeNoise(q*2.3+float3(-_Time.y*.025,_Time.y*.012,_Time.y*.017)+a*.6);
                    float wisps=smoothstep(.42,.72,a*.75+b*.25);
                    opticalDepth+=height*(.18+wisps*3.0)*stepLength*ClearMask(p);
                }
                float tail=max(0,min(distanceToSurface,1800)-320);
                [unroll] for(int j=0;j<8;j++)
                {
                    float3 p=_WorldSpaceCameraPos+direction*(320+tail*(j+.5)/8);
                    opticalDepth+=exp(-max(0,p.y-_SeaLevel)/max(1,_Height))*tail*.40/8*ClearMask(p);
                }
                float opacity=min(_MaxOpacity,1-exp(-opticalDepth*_Density));
                float3 glow=0;
                [loop] for(int k=0;k<_FogFlashCount;k++)
                {
                    float3 toFlash=_FogFlashes[k].xyz-_WorldSpaceCameraPos;
                    float along=dot(toFlash,direction);
                    float range=length(toFlash);
                    if(along <= 0 || along > distanceToSurface+3) continue;
                    float lateralSq=max(0,dot(toFlash,toFlash)-along*along);
                    float radius=lerp(7,22,saturate(range/700));
                    float halo=exp(-lateralSq/(radius*radius)*2.5);
                    float core=exp(-lateralSq/4)*.45;
                    float strength=_FogFlashes[k].w*exp(-range/1100)*smoothstep(8,35,range);
                    glow+=float3(1,.43,.12)*(halo+core)*strength*.9*saturate(opticalDepth*_Density*3);
                }
                return half4(_MistColor.rgb*opacity+glow,opacity);
            }
            ENDHLSL
        }
    }
}
