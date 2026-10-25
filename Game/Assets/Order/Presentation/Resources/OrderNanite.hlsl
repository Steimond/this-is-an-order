#ifndef ORDER_NANITE_INCLUDED
#define ORDER_NANITE_INCLUDED

#include "OrderCommon.hlsl"

// The machines' nanite liquid (SETTING.md, «Наниты — суть расы»): graphite magnetic liquid that fills a building's form
// and drains off it. Shared by the liquid itself and by the building it reveals, so the two meet on one edge.

// Height of the edge between the liquid (below) and what it reveals (above) at this point: the base height broken into
// tongues, as liquid runs down a wall in streaks, slowly wandering so the edge looks alive.
float NaniteFront(float3 p, float baseHeight, float roughness)
{
    float2 q = float2(p.x * 2.1 + p.z * 1.6, p.z * 1.9 - p.x * 1.2);
    float streak = OrderNoise(q * 1.8 + _Time.y * .13) * .65 + OrderNoise(q * 4.6 - _Time.y * .27) * .35;
    return baseHeight - (streak - .35) * roughness;
}

// Surface coordinates on the face's dominant plane, the vertical one running along the wall, so patterns slide down
// walls and lie flat on roofs.
float2 NanitePlane(float3 p, float3 normalWS)
{
    float3 w = abs(normalWS);
    return w.y > w.x && w.y > w.z ? p.xz : w.x > w.z ? p.zy : p.xy;
}

float NaniteHash3(float3 p)
{
    p = frac(p * .1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}
float NaniteNoise3(float3 p)
{
    float3 i = floor(p), f = frac(p), u = f * f * (3 - 2 * f);
    float a = lerp(NaniteHash3(i), NaniteHash3(i + float3(1, 0, 0)), u.x), b = lerp(NaniteHash3(i + float3(0, 1, 0)), NaniteHash3(i + float3(1, 1, 0)), u.x);
    float c = lerp(NaniteHash3(i + float3(0, 0, 1)), NaniteHash3(i + float3(1, 0, 1)), u.x), d = lerp(NaniteHash3(i + float3(0, 1, 1)), NaniteHash3(i + 1), u.x);
    return lerp(lerp(a, b, u.y), lerp(c, d, u.y), u.z);
}
// How the liquid's own shape moves: slow swells drifting with the flow and a quicker shiver, as an offset per unit of
// amplitude. It is taken from the world position, so the split vertices of neighbouring faces move together and the
// surface never cracks.
float3 NaniteWobble(float3 p, float flow, float t)
{
    float3 q = p * 1.45 + float3(0, -flow * t * .45, 0);
    float3 slow = float3(NaniteNoise3(q + float3(11.3, t * .31, 0)), NaniteNoise3(q + float3(27.1, 0, t * .27)), NaniteNoise3(q + float3(3.7, -t * .33, 5.1))) - .5;
    float3 r = p * 4.1 + t * float3(.6, -.9 * flow, .5);
    float3 quick = float3(NaniteNoise3(r), NaniteNoise3(r + 17.7), NaniteNoise3(r + 41.3)) - .5;
    return slow * 2 + quick * .5;
}

// A polished black surface mirrors the sky above, the ground below and a bright band at the horizon.
half3 NaniteSky(float3 r)
{
    half3 sky = lerp(_OrderGround.rgb * .3, _OrderSky.rgb * .9, saturate(r.y * .6 + .5));
    return sky + exp(-abs(r.y - .08) * 9) * half3(.5, .56, .64);
}

#endif
