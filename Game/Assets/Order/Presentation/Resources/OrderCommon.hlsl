#ifndef ORDER_COMMON_INCLUDED
#define ORDER_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Shared by the match shaders: procedural noise, fog of war, out-of-bounds mist and the lighting model.
// Globals are set by TempleScenery; colours arrive already converted to linear space.
TEXTURE2D(_OrderFogTex); SAMPLER(sampler_OrderFogTex);
float4 _OrderMap;      // x: map size in cells, y: 1 / size, z: fog of war enabled
half4 _OrderSky, _OrderGround, _OrderMist, _OrderTeamColor;

float OrderHash(float2 p)
{
    float3 p3 = frac(p.xyx * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}
float2 OrderHash2(float2 p)
{
    float3 p3 = frac(p.xyx * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}
float OrderNoise(float2 p)
{
    float2 i = floor(p), f = frac(p), u = f * f * (3 - 2 * f);
    return lerp(lerp(OrderHash(i), OrderHash(i + float2(1, 0)), u.x), lerp(OrderHash(i + float2(0, 1)), OrderHash(i + 1), u.x), u.y);
}
float OrderFbm(float2 p)
{
    float sum = 0, amplitude = .5;
    for (int i = 0; i < 4; i++) { sum += amplitude * OrderNoise(p); p = p * 2.03 + 17.1; amplitude *= .5; }
    return sum / .9375;
}
// x: distance to the nearest feature point, y: distance to the cell border, z: cell id in [0, 1).
float3 OrderVoronoi(float2 p)
{
    float2 n = floor(p), f = frac(p), nearest = 0, cell = 0;
    float best = 8;
    for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++)
    {
        float2 g = float2(i, j), r = g + OrderHash2(n + g) - f;
        float d = dot(r, r);
        if (d < best) { best = d; nearest = r; cell = g; }
    }
    float border = 8;
    for (int j2 = -1; j2 <= 1; j2++) for (int i2 = -1; i2 <= 1; i2++)
    {
        float2 g = cell + float2(i2, j2), r = g + OrderHash2(n + g) - f;
        float2 delta = r - nearest;
        if (dot(delta, delta) > 1e-5) border = min(border, dot(.5 * (nearest + r), normalize(delta)));
    }
    return float3(sqrt(best), border, OrderHash(n + cell + 3.7));
}

// Bump mapping from a procedural height (world units) using screen-space derivatives.
float3 OrderBump(float3 positionWS, float3 normalWS, float height)
{
    float3 dpdx = ddx(positionWS), dpdy = ddy(positionWS);
    float3 r1 = cross(dpdy, normalWS), r2 = cross(normalWS, dpdx);
    float det = dot(dpdx, r1);
    float3 gradient = sign(det) * (ddx(height) * r1 + ddy(height) * r2);
    return normalize(abs(det) * normalWS - gradient);
}

float OrderOutside(float2 xz)
{
    float2 o = max(max(-xz, xz - _OrderMap.x), 0);
    return length(o);
}
// x: explored, y: currently visible. Out of bounds counts as seen and visible; it is only darkened.
float2 OrderVision(float3 positionWS)
{
    if (_OrderMap.z < .5) return 1;
    half2 fog = SAMPLE_TEXTURE2D_LOD(_OrderFogTex, sampler_OrderFogTex, positionWS.xz * _OrderMap.y, 0).rg;
    // The fog of the nearest edge fades out over a few cells beyond the map.
    float border = saturate((OrderOutside(positionWS.xz) - .5) / 6);
    return smoothstep(.04, .96, lerp(float2(fog.g, fog.r), 1, border));
}
half3 OrderApplyWorld(half3 color, float3 positionWS)
{
    float2 vision = OrderVision(positionWS);
    half luma = dot(color, half3(.3, .59, .11));
    half3 memory = lerp(luma.xxx, color, .45) * .4;
    half3 unknown = lerp(luma.xxx, color, .25) * .07 + _OrderMist.rgb * .3;
    color = lerp(unknown, lerp(memory, color, vision.y), vision.x);
    float d = OrderOutside(positionWS.xz);
    color *= lerp(1, .55, saturate(d * .45));
    return lerp(color, _OrderMist.rgb, smoothstep(1.5, 19, d));
}
// Additive and translucent effects fade instead of tinting toward the mist.
half OrderWorldFade(float3 positionWS)
{
    float2 vision = OrderVision(positionWS);
    return vision.x * lerp(.25, 1, vision.y) * (1 - smoothstep(1.5, 19, OrderOutside(positionWS.xz)));
}

// Hemisphere ambient with baked and screen-space occlusion, soft-shadowed sun and a little specular.
half3 OrderLighting(half3 albedo, float3 positionWS, float3 normalWS, half ao, float4 positionCS, half gloss)
{
    Light sun = GetMainLight(TransformWorldToShadowCoord(positionWS));
    AmbientOcclusionFactor ssao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(positionCS));
    half shadow = sun.shadowAttenuation;
    half3 ambient = lerp(_OrderGround.rgb, _OrderSky.rgb, saturate(normalWS.y * .5 + .5)) * ao * ssao.indirectAmbientOcclusion;
    // Baked occlusion also dims direct light a little, so the foot of every wall reads from above.
    half3 direct = sun.color * (saturate(dot(normalWS, sun.direction)) * shadow * ssao.directAmbientOcclusion * lerp(1, ao, .6));
    half3 halfway = normalize(sun.direction + GetWorldSpaceNormalizeViewDir(positionWS));
    half specular = pow(saturate(dot(normalWS, halfway)), 40) * gloss * shadow * .3;
    return albedo * (ambient + direct) + sun.color * specular;
}

float4 OrderShadowPosition(float3 positionWS, float3 normalWS, float3 lightDirection)
{
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    return positionCS;
}

#endif
