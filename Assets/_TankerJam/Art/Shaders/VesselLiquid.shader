// All oil layers of one vessel in a single draw, made to feel like calm liquid behind glass.
// The mesh is a unit cylinder (radius 1, height 1, centered) scaled to the column. VesselView feeds a
// MaterialPropertyBlock per vessel: layer tops in world units above the column base (bottom first) and
// linear colors, the column's center/base/radius/height, a surface tilt and a bubble amount.
//
// Look: layers are picked by WORLD height so their boundaries stay level while the surface tilts; each
// boundary is a soft blend with a thin bright meniscus; light passes "through" the liquid (darker, richer
// edges, lighter core) with a soft vertical highlight streak; a very slow low-contrast swirl keeps it
// alive; bubbles rise while the vessel drains. ALU only, no textures, one draw per vessel.
Shader "TankerJam/VesselLiquid"
{
    Properties
    {
        _Smoothness ("Smoothness", Range(0, 1)) = 0.78
        _SpecStrength ("Specular", Range(0, 1)) = 0.3
        _Emission ("Emission", Range(0, 1)) = 0.08
        _Blend ("Layer Blend (world units)", Range(0.001, 0.2)) = 0.045
        _Meniscus ("Meniscus Brightness", Range(0, 1)) = 0.22
        _Depth ("Edge Darkening", Range(0, 1)) = 0.22
        _Glow ("Core Glow", Range(0, 1)) = 0.14
        _Streak ("Highlight Streak", Range(0, 1)) = 0.2
        _Swirl ("Swirl", Range(0, 0.2)) = 0.045
        _BubbleSize ("Bubble Size", Range(0.01, 0.1)) = 0.06
    }

    HLSLINCLUDE
    #include "TankerJamCommon.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half _Smoothness;
        half _SpecStrength;
        half _Emission;
        half _Blend;
        half _Meniscus;
        half _Depth;
        half _Glow;
        half _Streak;
        half _Swirl;
        half _BubbleSize;
    CBUFFER_END

    #define TJ_MAX_LAYERS 16
    // Per vessel (MaterialPropertyBlock).
    float _LayerCount;
    float _LayerTop[TJ_MAX_LAYERS];     // world units above the column base
    float4 _LayerColor[TJ_MAX_LAYERS];
    float4 _ColumnCenter;               // x, base y, z, radius
    float _ColumnHeight;                // world height of the column (surface center)
    float4 _Tilt;                       // surface slope along world x and z
    float _Bubbles;                     // 0..1
    float _Phase;                       // per-vessel offset so vessels don't move in sync
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                half3 n = TransformObjectToWorldNormal(v.normalOS);
                // Tilt the surface: the top ring and cap move with the slope, pivoting on the center.
                if (v.positionOS.y > 0.49)
                {
                    ws.y += (ws.x - _ColumnCenter.x) * _Tilt.x + (ws.z - _ColumnCenter.z) * _Tilt.y;
                    if (v.normalOS.y > 0.9) n = normalize(half3(-_Tilt.x, 1, -_Tilt.y));
                }
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = n;
                return o;
            }

            float Hash(float x) { return frac(sin(x * 127.1) * 43758.5453); }

            // Bubbles on the column wall: a few columns around the vessel, one bubble per cell, rising.
            half BubbleMask(float angle, float h, float radius)
            {
                const float columns = 14.0;
                const float cell = 0.8;
                float u = angle / (2.0 * PI) * columns;
                float c = floor(u);
                float r1 = Hash(c + 1.7), r2 = Hash(c + 9.3), r3 = Hash(c + 4.1);
                if (r1 < 0.35) return 0;                                     // sparse: not every column
                float speed = 0.22 + 0.25 * r2;
                float y = h - _Time.y * speed + r3 * cell;
                float row = floor(y / cell);
                float wobble = sin(_Time.y * 2.3 + r3 * 6.28 + row) * 0.18;  // a little side-to-side drift
                float dx = (frac(u) - 0.5 + wobble * 0.25) * (2.0 * PI * radius / columns);
                float dy = (frac(y / cell) - 0.5) * cell;
                float size = _BubbleSize * (0.6 + 0.8 * Hash(c * 3.1 + row));
                float d = length(float2(dx, dy)) / size;
                // Ring with a faint fill: reads as a bubble, not a dot.
                return (smoothstep(1.0, 0.8, d) * (0.35 + 0.65 * smoothstep(0.45, 0.85, d)));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                int count = (int)_LayerCount;
                float h = i.positionWS.y - _ColumnCenter.y;

                // Layer at this height, and the next one up for the soft blend.
                int k = 0;
                [loop] for (int j = 0; j < TJ_MAX_LAYERS; j++)
                {
                    k = j;
                    if (j >= count - 1 || h <= _LayerTop[j]) break;
                }
                half3 albedo = _LayerColor[k].rgb;
                half meniscus = 0;
                if (k < count - 1)
                {
                    float d = h - _LayerTop[k];                       // < 0 just below the boundary
                    half t = smoothstep(-_Blend, _Blend, d);
                    albedo = lerp(albedo, _LayerColor[k + 1].rgb, t);
                    // Only where two different oils meet: same-colored units are one continuous body of liquid.
                    half differs = step(0.0004, dot(_LayerColor[k + 1].rgb - _LayerColor[k].rgb, _LayerColor[k + 1].rgb - _LayerColor[k].rgb));
                    meniscus = differs * (1.0h - smoothstep(0.0, _Blend * 0.6, abs(d + _Blend * 0.35)));
                }
                if (k > 0)
                {
                    float d = h - _LayerTop[k - 1];                   // just above the boundary below
                    half t = smoothstep(-_Blend, _Blend, d);
                    albedo = lerp(_LayerColor[k - 1].rgb, albedo, t);
                }

                half3 n = normalize(i.normalWS);
                half3 viewDir = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                half ndv = saturate(dot(n, viewDir));
                bool side = abs(n.y) < 0.6;

                // Slow swirl: low-contrast brightness drift around and up the column.
                float angle = atan2(i.positionWS.z - _ColumnCenter.z, i.positionWS.x - _ColumnCenter.x) + PI;
                float t = _Time.y * 0.35 + _Phase;
                half swirl = sin(angle * 3.0 + h * 2.2 + t) * sin(angle * 5.0 - h * 1.4 - t * 0.7);
                albedo *= 1.0h + _Swirl * swirl;

                // Light through the liquid: richer, darker edges and a lighter core.
                albedo *= lerp(1.0h - _Depth, 1.0h, ndv);
                half3 c = TJ_Shade(albedo, i.positionWS, n, _Smoothness, _SpecStrength);
                c += albedo * (_Emission + _Glow * ndv * ndv);
                c += meniscus * _Meniscus * (albedo * 0.5h + 0.5h);

                if (side)
                {
                    // Soft vertical highlight streak (a window reflection, fixed relative to the camera).
                    half3 nv = mul((float3x3)UNITY_MATRIX_V, (float3)n);
                    half streak = smoothstep(0.38, 0.5, nv.x) * (1.0h - smoothstep(0.58, 0.72, nv.x));
                    c += streak * _Streak;

                    // Bubbles only in the liquid (below the surface), only while draining.
                    if (_Bubbles > 0.001 && h < _ColumnHeight - 0.08)
                    {
                        half b = BubbleMask(angle, h, _ColumnCenter.w) * _Bubbles * ndv;
                        c = lerp(c, albedo * 0.35h + 0.65h, b * 0.85h);
                    }
                }
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
