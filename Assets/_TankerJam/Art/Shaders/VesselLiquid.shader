// All oil layers of one vessel in a single draw. The mesh is a unit cylinder (height 1, centered) scaled to
// the column's total height; each fragment picks its layer by normalized height. Layer tops (0..1, bottom
// first) and linear colors come from a MaterialPropertyBlock set by VesselView.
Shader "TankerJam/VesselLiquid"
{
    Properties
    {
        _Smoothness ("Smoothness", Range(0, 1)) = 0.78
        _SpecStrength ("Specular", Range(0, 1)) = 0.35
        _Emission ("Emission", Range(0, 1)) = 0.08
    }

    HLSLINCLUDE
    #include "TankerJamCommon.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half _Smoothness;
        half _SpecStrength;
        half _Emission;
    CBUFFER_END

    #define TJ_MAX_LAYERS 16
    float _LayerCount;
    float _LayerTop[TJ_MAX_LAYERS];
    float4 _LayerColor[TJ_MAX_LAYERS];
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
                float height01 : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.height01 = v.positionOS.y + 0.5;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 albedo = _LayerColor[0].rgb;
                int count = (int)_LayerCount;
                [loop] for (int k = 0; k < TJ_MAX_LAYERS; k++)
                {
                    if (k >= count) break;
                    albedo = _LayerColor[k].rgb;
                    if (i.height01 <= _LayerTop[k]) break;
                }
                half3 n = normalize(i.normalWS);
                half3 c = TJ_Shade(albedo, i.positionWS, n, _Smoothness, _SpecStrength);
                return half4(c + albedo * _Emission, 1);
            }
            ENDHLSL
        }
    }
}
