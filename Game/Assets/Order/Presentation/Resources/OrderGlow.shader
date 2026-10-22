Shader "Order/Glow"
{
    // Unlit effects: particles, selection rings, beacons, beams and the placement footprint.
    Properties
    {
        [HDR] _Color("Color", Color) = (1,1,1,1)
        _Soft("Soft particle disc", Float) = 0
        _Grid("Placement grid", Float) = 0
        _Fog("Fog of war", Float) = 0
        _FadeHeight("Fade out by height", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "OrderCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Soft, _Grid, _Fog, _FadeHeight, _SrcBlend, _DstBlend;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; float heightOS:TEXCOORD2; };
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = input.color; o.uv = input.uv; o.heightOS = input.positionOS.y;
                return o;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half4 c = input.color * _Color;
                if (_Soft > .5) { float2 q = input.uv * 2 - 1; float disc = saturate(1 - dot(q, q)); c.a *= disc * disc; }
                if (_Grid > .5)
                {
                    float2 cell = frac(input.positionWS.xz), edge = min(cell, 1 - cell);
                    c.a *= lerp(.35, 1, 1 - smoothstep(.02, .05, min(edge.x, edge.y)));
                }
                if (_FadeHeight > 0) c.a *= 1 - saturate(input.heightOS / _FadeHeight);
                if (_Fog > .5) c.a *= OrderWorldFade(input.positionWS);
                return c;
            }
            ENDHLSL
        }
    }
}
