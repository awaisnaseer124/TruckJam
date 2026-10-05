// Tinted see-through material for tank shells and vessel glass: lit, alpha blended, fresnel rim so
// thin glass still reads at grazing angles. Optional vertical highlight streaks (vessel glass) read as a
// window reflected in curved glass. No depth write; keep these meshes few and simple (overdraw).
Shader "TankerJam/ToyTransparent"
{
    Properties
    {
        _BaseColor ("Color (alpha = opacity)", Color) = (1, 1, 1, 0.26)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _SpecStrength ("Specular", Range(0, 1)) = 0.5
        _Rim ("Rim Opacity", Range(0, 1)) = 0.25
        _Streak ("Highlight Streaks", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    HLSLINCLUDE
    #include "TankerJamCommon.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half _Smoothness;
        half _SpecStrength;
        half _Rim;
        half _Streak;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
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
                half3 normalWS : TEXCOORD1;
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
                return o;
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n = normalize(i.normalWS);
                n = IS_FRONT_VFACE(face, n, -n);
                half3 viewDir = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                half fresnel = pow(1.0h - saturate(dot(n, viewDir)), 3.0h);
                Light light = GetMainLight();
                half ndl = saturate(dot(n, light.direction));
                half3 h = SafeNormalize(light.direction + viewDir);
                half spec = pow(saturate(dot(n, h)), exp2(10.0h * _Smoothness + 1.0h)) * _SpecStrength;
                half3 c = _BaseColor.rgb * (light.color * ndl + TJ_Hemisphere(n)) + light.color * spec;
                half a = saturate(_BaseColor.a + fresnel * _Rim + spec);
                // Two soft vertical streaks fixed relative to the camera (a broad one and a thin one).
                half3 nv = mul((float3x3)UNITY_MATRIX_V, (float3)n);
                half streak = smoothstep(0.42, 0.52, nv.x) * (1.0h - smoothstep(0.6, 0.74, nv.x))
                            + 0.6h * smoothstep(-0.5, -0.44, nv.x) * (1.0h - smoothstep(-0.4, -0.34, nv.x));
                streak *= _Streak * (1.0h - saturate(abs(n.y) * 2.0h));
                c += streak;
                a = saturate(a + streak * 0.8h);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
