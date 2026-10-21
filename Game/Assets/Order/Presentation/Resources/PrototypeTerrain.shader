Shader "Order/PrototypeTerrain"
{
    Properties
    {
        _BaseColor("Color", Color) = (1,1,1,1)
        _VertexColors("Terrain vertex colors", Float) = 1
        _Grid("Placement grid", Float) = 0
        _Opacity("Opacity", Range(0,1)) = 1
        _ZWrite("Write depth", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _VertexColors;
                float _Grid, _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.color=input.color;o.world=TransformObjectToWorld(input.positionOS.xyz);o.normal=TransformObjectToWorldNormal(input.normalOS);return o;
            }
            half4 frag(Varyings input):SV_Target
            {
                float2 edge=min(frac(input.world.xz),1-frac(input.world.xz));
                float gridLine=1-smoothstep(.015,.035,min(edge.x,edge.y));
                half3 color=lerp(_BaseColor.rgb,input.color.rgb,_VertexColors);
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.world));
                float diffuse=saturate(dot(normalize(input.normal),sun.direction));
                float detail=1+sin(input.world.x*2.7+sin(input.world.z*1.9))*sin(input.world.z*3.3)*.025*_VertexColors;
                half3 lit=color*(half3(.22,.28,.34)+sun.color*(.28+.72*diffuse)*sun.shadowAttenuation)*detail;
                lit=lerp(lit,lerp(color*.4,half3(.8,1,.9),gridLine),_Grid);
                return half4(lit,_Opacity);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 shadowVert(float4 positionOS:POSITION):SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 shadowFrag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}
