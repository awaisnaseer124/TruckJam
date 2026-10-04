// Minimal shadow caster used inside each Tanker Jam shader (keeps the per-shader CBUFFER identical across
// passes, which the SRP Batcher requires). Optional alpha clip via TJ_SHADOW_CLIP(positionOS) hook.
#ifndef TANKERJAM_SHADOW_INCLUDED
#define TANKERJAM_SHADOW_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct ShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionOS : TEXCOORD0;
};

ShadowVaryings ShadowVert(ShadowAttributes v)
{
    ShadowVaryings o;
    UNITY_SETUP_INSTANCE_ID(v);
    float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDir = normalize(_LightPosition - positionWS);
#else
    float3 lightDir = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    o.positionCS = positionCS;
    o.positionOS = v.positionOS.xyz;
    return o;
}

half4 ShadowFrag(ShadowVaryings i) : SV_Target
{
#ifdef TJ_SHADOW_CLIP
    TJ_SHADOW_CLIP(i.positionOS);
#endif
    return 0;
}

#endif
