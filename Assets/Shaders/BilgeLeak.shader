Shader "PirateSlop/Bilge Leak"
{
    Properties
    {
        [NoScaleOffset] _SurfaceNormals("Ocean normals", 2D) = "gray" {}
        [NoScaleOffset] _FoamMap("Ocean foam", 2D) = "black" {}
        [NoScaleOffset] _CubemapTexture("Ocean reflection", Cube) = "" {}
        _AbsorptionColor("Absorption", Color) = (.1,.46,.42,1)
        _ScatteringColor("Scattering", Color) = (.008,.12,.105,1)
        _MaxDepth("Visibility", Float) = 15
        _BoatAttack_Lighting("Lighting", Vector) = (1,1,1,0)
        _BoatAttack_Water_MicroWaveIntensity("Detail", Float) = 1
        _OpticalDensity("Aerated water density", Range(1,8)) = 3
        _Opacity("Stream opacity", Range(0,1)) = .96
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "BilgeWaterOptics.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _SurfacePlane, _HoldBounds, _LocalGravity, _OceanPlane, _FlowState;
            float _BilgeTime, _Seed, _OpticalDensity, _Opacity;
            CBUFFER_END
            struct LeakAttributes
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float2 uv:TEXCOORD0;
                float4 flow:TEXCOORD1;
                float3 origin:TEXCOORD2;
                float4 center:TEXCOORD3;
                float4 color:COLOR;
            };
            struct LeakVaryings
            {
                float4 positionCS:SV_POSITION;
                float2 uv:TEXCOORD0;
                float3 world:TEXCOORD1;
                float4 color:TEXCOORD2;
                float4 state:TEXCOORD3;
                float localY:TEXCOORD4;
            };
            float WaterHeight(float3 p)
            {
                return _SurfacePlane.z + dot(p.xz, _SurfacePlane.xy);
            }
            float OceanHead(float3 p)
            {
                return dot(float4(p, 1), _OceanPlane);
            }
            float3 RandomDirection(float phase)
            {
                return float3(sin(phase * 831.7), sin(phase * 573.3), cos(phase * 329.1));
            }
            float3 JetPosition(float3 origin, float3 axis, float speed, float age, float3 relative, float strong)
            {
                float waterHeight = WaterHeight(origin);
                float gravity = max(.1, -_LocalGravity.y);
                float vy = axis.y * speed;
                float entry = (vy + sqrt(vy * vy + 2 * gravity * max(0, origin.y - waterHeight))) / gravity;
                if (origin.y < waterHeight && _SurfacePlane.w > .5) entry = 0;
                float wetAge = max(0, age - entry) * _SurfacePlane.w;
                float airAge = age - wetAge;
                float travel = airAge + (1 - exp(-3.5 * wetAge)) / 3.5;
                float contraction = strong * .32 * smoothstep(0, .16, age);
                float3 p = origin - relative * contraction + axis * (.009 + speed * travel);
                p += _LocalGravity.xyz * (.5 * airAge * airAge);
                p += axis * wetAge * .15 + float3(0, 1, 0) * wetAge * wetAge * .2;
                return p;
            }
            LeakVaryings Vert(LeakAttributes i)
            {
                LeakVaryings o;
                float strong = i.color.g;
                float mode = i.color.b;
                float3 axis = normalize(i.flow.xyz);
                float3 across = normalize(cross(float3(0, 1, 0), axis));
                float3 sideways = normalize(cross(axis, across));
                float3 origin = i.origin;
                float baseHead = max(0, OceanHead(origin));
                float spillDepth = baseHead / max(.35, -_OceanPlane.y);
                if (strong < .5 && mode < .5)
                    origin.y += i.center.w * min(max(0, i.flow.w - origin.y), spillDepth) * .98;
                float waterHeight = WaterHeight(origin);
                float insideHead = max(0, (waterHeight - origin.y) * _FlowState.y) * _SurfacePlane.w;
                float head = max(0, OceanHead(origin) - insideHead);
                if (strong < .5)
                {
                    float bottomInside = max(0, (WaterHeight(i.origin) - i.origin.y) * _FlowState.y) * _SurfacePlane.w;
                    head = max(0, baseHead - bottomInside) * .65;
                    axis = normalize(float3(axis.x, min(axis.y, -.12), axis.z));
                }
                float speed = sqrt(19.62 * head) * lerp(.75, .95, strong);
                float submerged = step(origin.y, waterHeight) * _SurfacePlane.w;
                float phase = i.color.a;
                float age = i.uv.y;
                float3 p;
                float visibility = lerp(1 - _FlowState.x, _FlowState.x, strong) * smoothstep(0, .018, head);
                float3 relative = origin - i.center.xyz;
                relative -= axis * dot(relative, axis);
                if (mode < .5)
                {
                    float emitted = _BilgeTime - age;
                    float2 q = float2(i.uv.x * 2.1, emitted * 8.5 + _Seed);
                    float2 a = SAMPLE_TEXTURE2D_LOD(_SurfaceNormals, sampler_SurfaceNormals, q, 0).xy * 2 - 1;
                    float2 b = SAMPLE_TEXTURE2D_LOD(_SurfaceNormals, sampler_SurfaceNormals, q * 2.47 + .27, 0).zw * 2 - 1;
                    float2 turbulence = (a + b * .43) * .7;
                    p = JetPosition(origin, axis, speed, age, relative, strong);
                    float growth = smoothstep(.005, .2, age);
                    float amplitude = lerp(.013, .13, strong) * growth * sqrt(max(.001, age));
                    p += (across * turbulence.x + sideways * turbulence.y) * amplitude;
                    p += axis * turbulence.y * amplitude * .3;
                    age /= lerp(.85, .68, strong);
                    submerged = step(p.y, WaterHeight(p)) * _SurfacePlane.w;
                }
                else
                {
                    float3 random = RandomDirection(phase);
                    float gravity = max(.1, -_LocalGravity.y);
                    float vy = axis.y * speed;
                    float target = lerp(_HoldBounds.x + .02, waterHeight, _SurfacePlane.w);
                    float fallTime = (vy + sqrt(vy * vy + 2 * gravity * max(0, origin.y - target))) / gravity;
                    float3 impact = origin + axis * speed * fallTime + _LocalGravity.xyz * (.5 * fallTime * fallTime);
                    impact.y = lerp(_HoldBounds.x + .02, WaterHeight(impact) + .008, _SurfacePlane.w);
                    float3 velocity = axis * speed;
                    float lifetime = lerp(.25, .6, phase);
                    if (mode < 1.5)
                    {
                        float birthAge = lerp(.08, .32, phase);
                        float3 birth = JetPosition(origin, axis, speed, birthAge, relative, strong);
                        age = frac(_BilgeTime / lifetime + phase) * lifetime;
                        velocity += _LocalGravity.xyz * birthAge + (across * random.x + sideways * random.y) * lerp(.35, 1.7, strong);
                        p = birth + velocity * age + _LocalGravity.xyz * (.5 * age * age);
                        submerged = step(p.y, WaterHeight(p)) * _SurfacePlane.w;
                        visibility *= 1 - submerged;
                    }
                    else if (mode < 2.5)
                    {
                        lifetime = lerp(.8, 1.5, phase);
                        age = frac(_BilgeTime / lifetime + phase) * lifetime;
                        float size = i.center.w * (.22 + age * 1.4);
                        p = impact + axis * age * .35 + float3(random.x, 0, random.z) * age * .4;
                        p += float3(i.uv.x * size, 0, i.uv.y * size);
                        p.y = lerp(_HoldBounds.x + .025, WaterHeight(p) + .009, _SurfacePlane.w);
                        visibility *= (1 - submerged) * smoothstep(.015, .08, fallTime);
                    }
                    else if (mode < 3.5)
                    {
                        age = frac(_BilgeTime / lifetime + phase) * lifetime;
                        velocity = axis * lerp(.3, 1.4, phase) + across * random.x * 2.2;
                        velocity.y = lerp(1.2, 3.5, phase) * lerp(.35, 1, strong);
                        p = impact + velocity * age + _LocalGravity.xyz * (.5 * age * age);
                        visibility *= 1 - submerged;
                    }
                    else if (mode < 4.5)
                    {
                        lifetime = lerp(.6, 1.3, phase);
                        age = frac(_BilgeTime / lifetime + phase) * lifetime;
                        float3 birth = submerged > .5 ? origin : impact - float3(0, .045, 0);
                        p = birth + axis * speed * (1 - exp(-4 * age)) * .16;
                        p += (across * random.x + sideways * random.z) * age * .2;
                        p.y += age * (.12 + phase * .18);
                        visibility *= _SurfacePlane.w * step(p.y, WaterHeight(p) - .018);
                        submerged = 1;
                    }
                    else
                    {
                        lifetime = lerp(.4, .75, phase);
                        age = frac(_BilgeTime / lifetime + phase) * lifetime;
                        velocity = across * random.x * .65 + float3(0, .4 + phase * .6, 0);
                        p = impact + velocity * age;
                        visibility *= (1 - submerged) * .13 * strong;
                    }
                    if (mode < 1.5 || mode > 2.5)
                    {
                        float3 right = TransformWorldToObjectDir(UNITY_MATRIX_I_V._m00_m10_m20);
                        float3 up = TransformWorldToObjectDir(UNITY_MATRIX_I_V._m01_m11_m21);
                        float2 direction = normalize(float2(dot(velocity, right), dot(velocity, up)) + float2(.00001, .00001));
                        float3 along = right * direction.x + up * direction.y;
                        float3 crosswise = right * direction.y - up * direction.x;
                        float size = i.center.w * smoothstep(0, .025, age) * (1 - smoothstep(lifetime * .6, lifetime, age));
                        float stretch = mode < 3.5 ? lerp(1.7, 4, saturate(length(velocity) / 8)) : 1;
                        p += crosswise * i.uv.x * size + along * i.uv.y * size * stretch;
                    }
                    age /= lifetime;
                }
                o.world = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.world);
                o.localY = p.y;
                o.uv = i.uv;
                o.color = i.color;
                o.state = float4(age, submerged, visibility, lerp(clamp(spillDepth * .7, .006, .8), clamp(i.center.w * .8, .15, .95), strong));
                return o;
            }
            half4 Frag(LeakVaryings i):SV_Target
            {
                clip(min(i.localY - _HoldBounds.x, _HoldBounds.y - i.localY));
                float intensity = i.state.z;
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float alpha, foam;
                float3 normal;
                if (i.color.b > .5)
                {
                    float radius = length(i.uv);
                    float soft = max(.02, fwidth(radius));
                    alpha = 1 - smoothstep(1 - soft - .12, 1 + soft, radius);
                    float envelope = smoothstep(0, .08, i.state.x) * (1 - smoothstep(.5, 1, i.state.x));
                    normal = normalize(GetCameraPositionWS() - i.world);
                    foam = .06;
                    if (i.color.b > 1.5 && i.color.b < 2.5)
                    {
                        float2 detail = BilgeDetail(i.world.xz * 3, _BilgeTime);
                        float foamTexture = BilgeFoam(i.world.xz * 2.2 + detail * .25, _BilgeTime * 2.2);
                        alpha *= smoothstep(.24, .62, foamTexture) * envelope * .62;
                        normal = TransformObjectToWorldNormal(normalize(float3(-_SurfacePlane.x, 1, -_SurfacePlane.y)));
                        foam = foamTexture * .8;
                    }
                    else if (i.color.b > 3.5 && i.color.b < 4.5)
                    {
                        float rim = exp(-pow((radius - .73) * 11, 2));
                        alpha *= (rim * .7 + .06) * envelope;
                        foam = rim * .32;
                    }
                    else if (i.color.b > 4.5)
                    {
                        alpha = exp(-radius * radius * 4.5) * envelope;
                        foam = .48;
                    }
                    else alpha *= envelope * lerp(.28, .6, i.color.g);
                    float thickness = i.color.b < 1.5 || i.color.b > 2.5 ? .015 : .035;
                    half3 color = BilgeShading(i.world, normal, screenUV, thickness, foam, false, 1, 0);
                    return half4(color, saturate(alpha * intensity));
                }
                float emitted = _BilgeTime - i.uv.y;
                float2 flow = float2(i.uv.x * 1.6, emitted * 8.5 + _Seed);
                float2 detail = BilgeDetail(flow, _BilgeTime) * .25;
                normal = normalize(cross(ddy(i.world), ddx(i.world)));
                float3 tangent = normalize(cross(abs(normal.y) > .9 ? float3(1, 0, 0) : float3(0, 1, 0), normal));
                normal = normalize(normal + (tangent * detail.x + cross(normal, tangent) * detail.y) * 1.7);
                float coarse = BilgeFoam(flow * .47, _BilgeTime);
                float fine = BilgeFoam(flow * 1.9 + detail, _BilgeTime);
                float aeration = smoothstep(.02, .52, i.state.x);
                foam = smoothstep(.32, .75, coarse) * aeration * lerp(.12, .64, i.color.g);
                foam += smoothstep(.43, .79, fine) * aeration * .32 * i.color.g;
                foam += i.state.y * smoothstep(.45, .78, fine) * .26;
                float breakup = smoothstep(.32, .85, i.state.x);
                float ragged = 1 - smoothstep(.4, .83, coarse + detail.x * .6) * breakup;
                float tail = 1 - smoothstep(.57, 1, i.state.x + detail.y * .25);
                alpha = _Opacity * lerp(.9, 1, saturate(foam)) * ragged * tail * intensity;
                alpha *= lerp(1, .65 + foam * .3, i.state.y);
                half3 color = BilgeShading(i.world, normal, screenUV, i.state.w, saturate(foam), false,_OpticalDensity,i.state.w*.5);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
