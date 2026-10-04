// Shared lighting for Tanker Jam's toy look: one main light with shadows, a hemisphere ambient
// (matches the prototype's three.js HemisphereLight) and a soft Blinn-Phong highlight.
#ifndef TANKERJAM_COMMON_INCLUDED
#define TANKERJAM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Set globally by LightingRig.
half4 _TJ_SkyColor;
half4 _TJ_GroundColor;
half _TJ_HemiIntensity;

half3 TJ_Hemisphere(half3 normalWS)
{
    half k = normalWS.y * 0.5h + 0.5h;
    return lerp(_TJ_GroundColor.rgb, _TJ_SkyColor.rgb, k) * _TJ_HemiIntensity;
}

half3 TJ_Shade(half3 albedo, float3 positionWS, half3 normalWS, half smoothness, half specStrength)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light light = GetMainLight(shadowCoord);
    half ndl = saturate(dot(normalWS, light.direction));
    half atten = light.shadowAttenuation * light.distanceAttenuation;
    half3 viewDir = SafeNormalize(GetWorldSpaceViewDir(positionWS));
    half3 h = SafeNormalize(light.direction + viewDir);
    half spec = pow(saturate(dot(normalWS, h)), exp2(10.0h * smoothness + 1.0h)) * specStrength;
    half3 diffuse = albedo * (light.color * ndl * atten + TJ_Hemisphere(normalWS));
    return diffuse + light.color * spec * atten;
}

#endif
