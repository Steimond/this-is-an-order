Shader "Order/Prop"
{
    // Flat-shaded low-poly props, units and buildings built by MeshBuilder.
    // Vertex colour is the albedo; uv0 = (wind sway weight, team colour mask, emission, unused).
    Properties
    {
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Emission("Emission strength", Float) = 2.5
        _Gloss("Gloss", Range(0,1)) = .15
        _Rim("Rim light", Float) = 0
        _Panels("Panel lines", Float) = 0
        _Sway("Wind sway", Float) = 0
        _Fog("Fog of war", Float) = 1
        _Opacity("Opacity", Range(0,1)) = 1
        _TeamColor("Team colour override", Color) = (1,1,1,1)
        _TeamOverride("Use team colour override", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        _ZWrite("Write depth", Float) = 1
        // Machine buildings appear from the top down as the nanite liquid drains off them: everything below the edge is cut
        // away, the edge is broken into tongues like the liquid's own (NaniteFront, roughness _RevealNoise), a glowing band
        // of nanites marks it, and just above it a wet film dries over _Wet. Far below the ground by default, so nothing
        // is cut.
        _RevealBelow("Hidden below world height", Float) = -1000
        _RevealColor("Reveal edge glow", Color) = (0,0,0,0)
        _RevealNoise("Reveal edge roughness", Float) = 0
        _Wet("Wet film above the edge", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "OrderNanite.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _TeamColor;
            half4 _RevealColor;
            float _Emission, _Gloss, _Rim, _Panels, _Sway, _Fog, _Opacity, _Cull, _ZWrite, _TeamOverride, _RevealBelow, _RevealNoise, _Wet;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; float4 data:TEXCOORD0; };
        float3 PropWorld(Attributes input)
        {
            float3 p = TransformObjectToWorld(input.positionOS.xyz);
            float phase = _Time.y * 1.7 + p.x * .45 + p.z * .37;
            p.xz += float2(sin(phase), cos(phase * .8)) * .05 * input.data.x * _Sway;
            return p;
        }
        float RevealFront(float3 positionWS) { return NaniteFront(positionWS, _RevealBelow, _RevealNoise); }
        void Reveal(float3 positionWS) { clip(positionWS.y - RevealFront(positionWS)); }
        // A thin glowing seam along the edge; on a flat top the edge sweeps across the whole face at once, so it glows
        // there only faintly.
        half3 RevealEdge(float3 positionWS, float3 normalWS)
        {
            float above = positionWS.y - RevealFront(positionWS);
            return _RevealColor.rgb * (1 - saturate(above / .07)) * (1 - .85 * abs(normalWS.y));
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            struct Varyings
            {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1;
                half4 color:COLOR; float4 data:TEXCOORD2; float3 positionOS:TEXCOORD3;
            };
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = PropWorld(input);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color; o.data = input.data; o.positionOS = input.positionOS.xyz;
                return o;
            }
            half4 frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                Reveal(input.positionWS);
                float3 n = normalize(input.normalWS) * (front ? 1 : -1);
                // Team colour is the player's (global) unless the material carries another side's colour.
                half3 team = lerp(_OrderTeamColor.rgb, _TeamColor.rgb, _TeamOverride);
                half3 albedo = input.color.rgb * lerp(1, team, input.data.y) * _BaseColor.rgb;
                if (_Panels > .5)
                {
                    // Plating seams in object space, so they stay put while a building rises.
                    float3 q = input.positionOS * 2.4, w = abs(input.normalWS);
                    float2 plate = w.y > w.x && w.y > w.z ? q.xz : w.x > w.z ? q.zy : q.xy;
                    float2 seam = abs(frac(plate) - .5);
                    albedo *= 1 - smoothstep(.46, .5, max(seam.x, seam.y)) * .3;
                }
                // Just drained: a film of nanites still wets the surface over _Wet above the edge, darker and mirror-glossy,
                // drying off upwards in streaks.
                float3 p = input.positionWS, v = GetWorldSpaceNormalizeViewDir(p);
                half wet = 0;
                if (_Wet > 0)
                {
                    float above = p.y - RevealFront(p);
                    wet = saturate(1 - above / (_Wet * (.6 + .8 * OrderNoise(NanitePlane(p, n) * float2(9, 1.2)))));
                    albedo *= lerp(1, .22, wet);
                }
                half3 color = OrderLighting(albedo, p, n, 1, input.positionCS, lerp(_Gloss, 1, wet));
                half rim = pow(1 - saturate(dot(n, v)), 3) * _Rim;
                color += albedo * (input.data.z * _Emission * (1 - wet) + rim * 1.5);
                if (wet > 0)
                {
                    Light sun = GetMainLight();
                    half nh = saturate(dot(n, normalize(sun.direction + v)));
                    color += (NaniteSky(reflect(-v, n)) * (.025 + .7 * pow(1 - saturate(dot(n, v)), 5)) + sun.color * pow(nh, 200) * 2.5) * wet;
                }
                color += RevealEdge(input.positionWS, n);
                if (_Fog > .5) color = OrderApplyWorld(color, input.positionWS);
                return half4(color, _Opacity);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            float3 _LightDirection;
            struct ShadowVaryings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            ShadowVaryings shadowVert(Attributes input)
            {
                ShadowVaryings o; o.positionWS = PropWorld(input);
                o.positionCS = OrderShadowPosition(o.positionWS, TransformObjectToWorldNormal(input.normalOS), _LightDirection);
                return o;
            }
            half4 shadowFrag(ShadowVaryings input) : SV_Target { Reveal(input.positionWS); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            struct DepthVaryings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            DepthVaryings depthVert(Attributes input)
            {
                DepthVaryings o; o.positionWS = PropWorld(input); o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            half depthFrag(DepthVaryings input) : SV_Target { Reveal(input.positionWS); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex normalsVert
            #pragma fragment normalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct NormalsVaryings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            NormalsVaryings normalsVert(Attributes input)
            {
                NormalsVaryings o;
                o.positionWS = PropWorld(input);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 normalsFrag(NormalsVaryings input, bool front : SV_IsFrontFace) : SV_Target
            {
                Reveal(input.positionWS);
                float3 normalWS = normalize(input.normalWS) * (front ? 1 : -1);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normalWS) * .5 + .5)), 0);
            #else
                return half4(normalWS, 0);
            #endif
            }
            ENDHLSL
        }
    }
}
