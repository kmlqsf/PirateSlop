Shader "PirateSlop/Test Storm Lightning"
{
    Properties
    {
        _WeatherCenter("Center / Radius", Vector) = (0,0,0,4000)
        _WeatherTestBand("Band / Floor / Top", Vector) = (2,18,3,42)
        [HideInInspector] _StormCenterWater("Storm Center", Vector) = (0,0,0,4000)
        [HideInInspector] _StormBand("Storm Band", Vector) = (4000,2,18,.6)
        [HideInInspector] _StormShape("Storm Shape", Vector) = (42,0,0,1)
        [HideInInspector] _StormTestWallDensity("Cloud Density", 3D) = "black" {}
        [HideInInspector] _StormTestWallNormals("Cloud Normals", 3D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+18" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="StormTestLightning" }
            Blend 0 One One
            Blend 1 One One
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _WeatherCenter, _WeatherTestBand;
            float4 _StormCenterWater, _StormBand, _StormShape;
            CBUFFER_END
            #include "../../Game/BRZoneVolumetric/Shaders/StormCloudBoundary.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);
                o.uv=input.uv; o.color=input.color;
                return o;
            }
            void Frag(Varyings i,out half4 emission:SV_Target0,out float weightedDistance:SV_Target1)
            {
                clip(i.world.y-_WeatherTestBand.z);
                clip(_WeatherTestBand.w-i.world.y);
                float cloudRadius=length(i.world.xz-_WeatherCenter.xz)-_WeatherCenter.w;
                clip(cloudRadius+_WeatherTestBand.x);
                clip(_WeatherTestBand.y-cloudRadius);
                float3 delta=i.world-_WorldSpaceCameraPos;
                float distanceWS=length(delta);
                float near=1-smoothstep(80,260,distanceWS);
                float side=abs(i.uv.y*2-1);
                float core=1-smoothstep(.08,.5,side);
                float glow=pow(saturate(1-side),1.8);
                half3 color=i.color.rgb*glow*2+half3(.85,.69,1)*core*5;
                float strength=i.color.a*near*glow;
                clip(strength-.001);
                emission=half4(color*strength,strength);
                weightedDistance=distanceWS*strength;
            }
            ENDHLSL
        }
    }
}
