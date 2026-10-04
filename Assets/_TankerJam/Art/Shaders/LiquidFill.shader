// Oil inside a truck tank. The mesh is a closed cylinder along object +Z, centered on the tank.
// Pixels above a sloshing plane are clipped:
//   plane(z) = _FillHeight + z * _Tilt + sin(z * 9 + t * 18) * _Wobble   (object space)
// Rendered two-sided; back faces seen through the cut are shaded as a lighter flat surface so the
// liquid reads as having a top. Per-truck values come from a MaterialPropertyBlock (TruckView).
Shader "TankerJam/LiquidFill"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.31, 0.66, 1)
        _SurfaceLighten ("Surface Lighten", Range(0, 1)) = 0.3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.8
        _FillHeight ("Fill Height (object Y)", Float) = 0
        _Tilt ("Tilt (slope along Z)", Float) = 0
        _Wobble ("Wobble", Float) = 0
    }

    HLSLINCLUDE
    #include "TankerJamCommon.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half _SurfaceLighten;
        half _Smoothness;
        float _FillHeight;
        float _Tilt;
        float _Wobble;
    CBUFFER_END

    float LiquidPlane(float3 positionOS)
    {
        return _FillHeight + positionOS.z * _Tilt + sin(positionOS.z * 9.0 + _Time.y * 18.0) * _Wobble;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.positionOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                clip(LiquidPlane(i.positionOS) - i.positionOS.y);
                bool front = IS_FRONT_VFACE(face, true, false);
                half3 albedo = _BaseColor.rgb;
                half3 n = normalize(i.normalWS);
                if (!front)
                {
                    // Seen from inside: draw it as the flat liquid surface.
                    albedo = lerp(albedo, half3(1, 1, 1), _SurfaceLighten);
                    n = TransformObjectToWorldNormal(float3(0, 1, 0));
                }
                return half4(TJ_Shade(albedo, i.positionWS, n, _Smoothness, 0.35h), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define TJ_SHADOW_CLIP(p) clip(LiquidPlane(p) - (p).y)
            #include "TankerJamShadow.hlsl"
            ENDHLSL
        }
    }
}
