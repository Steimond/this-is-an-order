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
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        _ZWrite("Write depth", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "OrderCommon.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Emission, _Gloss, _Rim, _Panels, _Sway, _Fog, _Opacity, _Cull, _ZWrite;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; float4 data:TEXCOORD0; };
        float3 PropWorld(Attributes input)
        {
            float3 p = TransformObjectToWorld(input.positionOS.xyz);
            float phase = _Time.y * 1.7 + p.x * .45 + p.z * .37;
            p.xz += float2(sin(phase), cos(phase * .8)) * .05 * input.data.x * _Sway;
            return p;
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
                float3 n = normalize(input.normalWS) * (front ? 1 : -1);
                half3 albedo = input.color.rgb * lerp(1, _OrderTeamColor.rgb, input.data.y) * _BaseColor.rgb;
                if (_Panels > .5)
                {
                    // Plating seams in object space, so they stay put while a building rises.
                    float3 q = input.positionOS * 2.4, w = abs(input.normalWS);
                    float2 plate = w.y > w.x && w.y > w.z ? q.xz : w.x > w.z ? q.zy : q.xy;
                    float2 seam = abs(frac(plate) - .5);
                    albedo *= 1 - smoothstep(.46, .5, max(seam.x, seam.y)) * .3;
                }
                half3 color = OrderLighting(albedo, input.positionWS, n, 1, input.positionCS, _Gloss);
                half rim = pow(1 - saturate(dot(n, GetWorldSpaceNormalizeViewDir(input.positionWS))), 3) * _Rim;
                color += albedo * (input.data.z * _Emission + rim * 1.5);
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
            float4 shadowVert(Attributes input) : SV_POSITION
            {
                return OrderShadowPosition(PropWorld(input), TransformObjectToWorldNormal(input.normalOS), _LightDirection);
            }
            half4 shadowFrag() : SV_Target { return 0; }
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
            float4 depthVert(Attributes input) : SV_POSITION { return TransformWorldToHClip(PropWorld(input)); }
            half depthFrag() : SV_Target { return 0; }
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
            struct NormalsVaryings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            NormalsVaryings normalsVert(Attributes input)
            {
                NormalsVaryings o;
                o.positionCS = TransformWorldToHClip(PropWorld(input));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 normalsFrag(NormalsVaryings input, bool front : SV_IsFrontFace) : SV_Target
            {
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
