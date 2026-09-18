Shader "PirateSlop/HudIcon"
{
    Properties { _MainTex("Icon", 2D) = "white" {} _Edge("Edge", Float) = 0 }
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
            float _Edge;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o; }
            fixed4 frag(Output i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv);
                if (_Edge > .5)
                {
                    float2 stepUV = max(abs(ddx(i.uv)), abs(ddy(i.uv))) * .85;
                    float left = tex2D(_MainTex, i.uv - float2(stepUV.x, 0)).a;
                    float right = tex2D(_MainTex, i.uv + float2(stepUV.x, 0)).a;
                    float top = tex2D(_MainTex, i.uv + float2(0, stepUV.y)).a;
                    float bottom = tex2D(_MainTex, i.uv - float2(0, stepUV.y)).a;
                    float outer = max(max(left, right), max(top, bottom));
                    float inner = saturate(c.a - min(min(left, right), min(top, bottom)));
                    c.rgb = lerp(c.rgb, float3(.9, .86, .76), inner * .12);
                    float alpha = max(c.a, outer * .85);
                    c.rgb = lerp(float3(.025, .018, .012), c.rgb, c.a / max(alpha, .001));
                    c.a = alpha;
                }
                return c * i.color;
            }
            ENDCG
        }
    }
}
