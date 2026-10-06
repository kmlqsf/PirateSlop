Shader "PirateSlop/Player Freeze Screen"
{
    Properties { _MainTex("Texture",2D)="white" {} _FreezeFade("Fade",Range(0,1))=1 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float _FreezeFade;
            struct Attributes { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input) { Varyings output; output.vertex=UnityObjectToClipPos(input.vertex); output.uv=input.uv; return output; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 Frag(Varyings input):SV_Target
            {
                float2 edgeUV=min(input.uv,1-input.uv);
                float edge=min(edgeUV.x,edgeUV.y);
                float2 p=input.uv*float2(_ScreenParams.x/_ScreenParams.y,1);
                float irregular=sin(p.x*43+sin(p.y*27))*sin(p.y*37+sin(p.x*29));
                float border=1-smoothstep(.035,.15+irregular*.018,edge);
                float2 cell=floor(p*36), local=frac(p*36)-.5;
                float angle=Hash(cell)*6.2831853;
                float2 branch=float2(cos(angle),sin(angle));
                float a=abs(dot(local,branch));
                float b=abs(dot(local,float2(branch.y,-branch.x)));
                float veins=(1-smoothstep(.015,.015+fwidth(a),a))*smoothstep(.45,.08,b);
                float forks=(1-smoothstep(.02,.02+fwidth(b),abs(b-a*.55)))*smoothstep(.35,.05,a);
                float crystal=saturate(veins+forks*.65);
                float grain=Hash(floor(p*_ScreenParams.y*.4));
                float opacity=border*(.14+crystal*.35+grain*.08)*_FreezeFade;
                return half4(lerp(half3(.53,.68,.76),half3(.87,.94,.96),crystal),opacity);
            }
            ENDHLSL
        }
    }
}
