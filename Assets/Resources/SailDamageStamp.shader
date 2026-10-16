Shader "Hidden/PirateSlop/SailDamageStamp"
{
    Properties
    {
        _Seed ("Seed", Float) = 0
        _Tear ("Tear", Float) = 0
        _SailShape ("Sail Shape", Vector) = (0, 0, 0, 0)
        _SourceTex ("Source", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One
            BlendOp Max
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float _Seed, _Tear;
            float4 _SailShape;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 sailUV : TEXCOORD1; };
            Output Vert(Input input)
            {
                Output output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.sailUV = input.vertex.xy;
                return output;
            }
            half4 Frag(Output input) : SV_Target
            {
                float angle = atan2(input.uv.y, input.uv.x);
                float seed = _Seed * .0617;
                float radial = .94 + .09 * sin(angle * 5 + seed) + .07 * sin(angle * 9 - seed * 1.7);
                radial += .14 * pow(saturate(sin(angle * 3 + seed * .7)), 12);
                radial -= .09 * pow(saturate(sin(angle * 37 + seed * 2.3)), 14);
                radial += _Tear * pow(saturate(cos(angle * 2 + seed * .4)), 20);
                float torn = length(input.uv) - radial;
                float fibers = sin(angle * 113 + seed) * sin(angle * 61 - seed);
                float edge = .075 + .045 * saturate(fibers);
                float coverage = saturate(.5 - torn / edge);
                float2 sail = input.sailUV;
                sail.y /= lerp(1, max(.001, 1 - sail.x), _SailShape.x);
                float phase = _SailShape.y;
                float center = .27 + .05 * sin(sail.y * 5.7 + phase);
                float grain = .006 * sin(sail.y * 97 + phase) + .004 * sin(sail.y * 173 - phase);
                float trunk = abs(sail.x - center) - (.06 + .012 * sin(sail.y * 13 + phase)) + grain;
                float branch = saturate((sail.y - .56) / .44);
                float waviness = .025 * sin(branch * 7 + phase) * branch;
                float branches = min(abs(sail.x - center - .59 * branch - waviness), abs(sail.x - center + .23 * branch + waviness)) - .043 + grain;
                branches = max(branches, .56 - sail.y);
                float remnant = min(trunk, branches);
                coverage *= smoothstep(0, .008, remnant);
                return coverage.xxxx;
            }
            ENDHLSL
        }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _SourceTex;
            float4 _SourceStep;
            float _SourceMip, _ReduceMinimum;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Output Vert(Input input)
            {
                Output output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Output input) : SV_Target
            {
                float2 step = _SourceStep.xy;
                half a = tex2Dlod(_SourceTex, float4(input.uv + float2(-step.x, -step.y), 0, _SourceMip)).r;
                half b = tex2Dlod(_SourceTex, float4(input.uv + float2(step.x, -step.y), 0, _SourceMip)).r;
                half c = tex2Dlod(_SourceTex, float4(input.uv + float2(-step.x, step.y), 0, _SourceMip)).r;
                half d = tex2Dlod(_SourceTex, float4(input.uv + step, 0, _SourceMip)).r;
                half maximum = max(max(a, b), max(c, d));
                half minimum = min(min(a, b), min(c, d));
                return lerp(maximum, minimum, _ReduceMinimum).xxxx;
            }
            ENDHLSL
        }
    }
}
