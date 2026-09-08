Shader "PirateSlop/HudIcon"
{
    Properties { _MainTex("Icon", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Overlay" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o; }
            fixed4 frag(Output i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv);
                float light=dot(c.rgb,float3(.3,.59,.11));
                return fixed4(lerp(.72,1,light).xxx*i.color.rgb,c.a*i.color.a);
            }
            ENDCG
        }
    }
}
