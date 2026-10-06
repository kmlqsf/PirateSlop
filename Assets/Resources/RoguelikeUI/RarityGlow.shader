Shader "PirateSlop/UI/RarityGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CardMask ("Paper Mask", 2D) = "white" {}
        _GlowColor ("Glow Color", Color) = (0.15, 0.55, 1, 1)
        _Rarity ("Rarity", Float) = 1
        _EffectTime ("Effect Time", Float) = 0
        _Phase ("Phase", Float) = 0
        _Focus ("Focus", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 position : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _CardMask;
            float4 _GlowColor, _ClipRect;
            float _Rarity, _EffectTime, _Phase, _Focus;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float hash(float n) { return frac(sin(n * 127.1) * 43758.5453); }
            float noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n = dot(cell, float2(1.0, 57.0));
                return lerp(lerp(hash(n), hash(n + 1.0), f.x), lerp(hash(n + 57.0), hash(n + 58.0), f.x), f.y);
            }

            float roundedDistance(float2 p)
            {
                float2 q = abs(p - float2(0.0, 0.7)) - float2(140.5, 255.0);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - 20.5;
            }

            float3 sparkLight(float2 p, float time)
            {
                float3 light = 0.0;
                int count = _Rarity > 2.5 ? 8 : 4;
                if (_Rarity < 1.5) return light;
                for (int i = 0; i < 8; i++)
                {
                    if (i >= count) break;
                    float seed = i * 7.31 + _Phase * 11.0 + 13.0;
                    float life = frac(time * (0.18 + hash(seed) * 0.07) + hash(seed + 5.0));
                    float cycle = floor(time * (0.18 + hash(seed) * 0.07) + hash(seed + 5.0));
                    float location = hash(seed + cycle * 3.17);
                    float side = floor(hash(seed + 1.0) * 4.0);
                    float2 origin, normal;
                    if (side < 1.0) { origin = float2(lerp(-132.0, 132.0, location), 276.0); normal = float2(0.0, 1.0); }
                    else if (side < 2.0) { origin = float2(161.0, lerp(-242.0, 242.0, location)); normal = float2(1.0, 0.0); }
                    else if (side < 3.0) { origin = float2(-161.0, lerp(-242.0, 242.0, location)); normal = float2(-1.0, 0.0); }
                    else { origin = float2(lerp(-132.0, 132.0, location), -274.0); normal = float2(0.0, -1.0); }
                    origin += normal * (4.0 + life * 16.0) + float2(sin(seed + time * 0.8) * 2.0, life * 7.0);
                    float2 delta = abs(p - origin);
                    float sparkle = exp2(-length(delta) * 1.5);
                    sparkle += (exp2(-delta.x * 0.8 - delta.y * 4.0) + exp2(-delta.y * 0.8 - delta.x * 4.0)) * 0.45;
                    sparkle += exp2(-length(delta) * 0.4) * 0.12;
                    float envelope = pow(sin(life * 3.14159265), 2.0);
                    light += lerp(_GlowColor.rgb, 1.0, 0.8) * sparkle * envelope * 1.6;
                }
                return light;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * float2(430.0, 658.0);
                float2 cardUV = p / float2(342.0, 570.0) + 0.5;
                float inCard = step(0.0, cardUV.x) * step(cardUV.x, 1.0) * step(0.0, cardUV.y) * step(cardUV.y, 1.0);
                float paper = tex2D(_CardMask, saturate(cardUV)).a * inCard;
                float outside = 1.0 - paper;
                clip(outside - 0.002);
                float distance = roundedDistance(p);
                float edgeFade = 1.0 - smoothstep(30.0, 49.0, distance);
                clip(edgeFade - 0.001);
                float time = _EffectTime + _Phase * 3.7;
                float flow = noise(p * float2(0.036, 0.025) + float2(time * 0.16, -time * 0.22));
                float ripple = noise(p * 0.083 + float2(-time * 0.11, time * 0.13));
                float energy = 0.7 + 0.3 * flow;
                float core = exp2(-abs(distance - 1.0) * 1.25);
                float bloom = exp2(-abs(distance) / 4.4);
                float wisps = pow(saturate(flow * 1.5 + ripple * 0.45 - 0.8), 2.0) * exp2(-max(distance, 0.0) / 13.0);
                float accent = pow(0.5 + 0.5 * sin(p.x * 0.017 + p.y * 0.009 - time * 0.65), 12.0);
                float intensity = lerp(0.75, 1.25, saturate((_Rarity - 1.0) * 0.5));
                float3 whiteHot = lerp(_GlowColor.rgb, 1.0, 0.72);
                float3 color = whiteHot * core * (0.65 + accent * 0.45);
                color += _GlowColor.rgb * bloom * (0.37 + accent * 0.12) * energy;
                color += _GlowColor.rgb * wisps * (0.12 + _Rarity * 0.055);
                color *= intensity * (1.0 + _Focus * 0.28);
                color += sparkLight(p, time);
                float alpha = outside * edgeFade * i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(i.position.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif
                return float4(color * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
