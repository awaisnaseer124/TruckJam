// Opaque toy material. Albedo = lerp(vertex rgb, _BaseColor, vertex alpha):
//  - meshes without vertex colors (white, a=1) take _BaseColor,
//  - baked parts with a=0 keep their own color (tires, windows, chassis),
//  - a=0.65 on white gives the tint lightened by 35% (roof light), as in the prototype.
// This lets a whole truck body be one mesh, one material, one draw call per color.
Shader "TankerJam/ToyLit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.45
        _SpecStrength ("Specular", Range(0, 1)) = 0.2
        _Emission ("Emission", Range(0, 1)) = 0
    }

    HLSLINCLUDE
    #include "TankerJamCommon.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half _Smoothness;
        half _SpecStrength;
        half _Emission;
    CBUFFER_END
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
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
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
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 albedo = lerp(i.color.rgb, _BaseColor.rgb, i.color.a);
                half3 n = normalize(i.normalWS);
                half3 c = TJ_Shade(albedo, i.positionWS, n, _Smoothness, _SpecStrength);
                return half4(c + albedo * _Emission, 1);
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

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "TankerJamShadow.hlsl"
            ENDHLSL
        }
    }
}
