Shader "Order/NaniteLiquid"
{
    // The machines' nanite liquid: near-black graphite magnetic liquid. It is drawn over the shape of a mesh (a rising
    // building, a worker melting into one) and only below its front, a height broken into tongues (NaniteFront):
    // - the surface is a mirror: sharp sun highlights and the sky and horizon reflected, strongest at grazing angles;
    // - magnetic spikes stand out of it in a cellular pattern and slowly sway; they grow tall near the front, where the
    //   liquid is thin and restless;
    // - the whole surface keeps flowing along _Flow (up while it fills, down while it drains), in slow swells and fine
    //   ripples;
    // - nanites spark in it: points that blink on and off and drift with the flow, so it reads as alive, at work;
    // - a cold glowing meniscus runs along the front.
    Properties
    {
        _Front("Liquid below this world height", Float) = 1000
        _FrontNoise("Front roughness", Float) = .25
        _Flow("Flow (+ up, - down)", Float) = 0
        _Color("Graphite", Color) = (.028,.03,.035,1)
        _Sheen("Cold light", Color) = (.62,.86,1,1)
        _Spark("Nanite sparks", Float) = 1
        _Spikes("Magnetic spikes", Float) = 1
        _Fog("Fog of war", Float) = 1
        // The liquid's own shape swells and shivers by up to _Wobble world units; its foot at _Base stays on the ground.
        _Wobble("Shape wobble", Float) = .06
        _Base("Ground under it", Float) = -1000
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "OrderNanite.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color, _Sheen;
            float _Front, _FrontNoise, _Flow, _Spark, _Spikes, _Fog, _Wobble, _Base;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
        float LiquidFront(float3 p) { return NaniteFront(p, _Front, _FrontNoise); }
        float3 LiquidWorld(float4 positionOS)
        {
            float3 p = TransformObjectToWorld(positionOS.xyz);
            return p + NaniteWobble(p, _Flow, _Time.y) * _Wobble * saturate((p.y - _Base) * 2.5);
        }
        void Cut(float3 p) { clip(LiquidFront(p) - p.y); }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = LiquidWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                float front = LiquidFront(p);
                clip(front - p.y);
                float below = front - p.y, t = _Time.y;
                float3 n = normalize(input.normalWS);
                // The light follows the bent shape: the normal of the moved surface itself, turned to the mesh's side.
                float3 facet = normalize(cross(ddy(p), ddx(p)));
                facet *= dot(facet, n) < 0 ? -1 : 1;
                n = normalize(lerp(n, facet, .75));
                // The flow runs along the wall: up the plane's second axis, or across a roof.
                float2 plane = NanitePlane(p, n);
                float2 flow = float2(0, _Flow * t);
                // Slow swells and fine ripples carried by the flow.
                float swell = OrderFbm(plane * 1.3 - flow * .35 + float2(t * .05, 0));
                float ripple = OrderNoise(plane * 9 - flow * 1.6 + float2(0, t * .4));
                // Magnetic spikes: cones on the centres of a cellular pattern that sways; tall near the front.
                float agitation = 1 + 2.2 * exp(-below * 5);
                float2 cellular = plane * 4.2 - flow * .8 + float2(sin(t * .7 + plane.y), cos(t * .6 + plane.x)) * .12;
                float3 cell = OrderVoronoi(cellular);
                float spike = pow(saturate(1 - cell.x * 1.7), 2.6) * (.6 + .4 * sin(t * 1.9 + cell.z * 6.28));
                // Broad swells bend the reflections into long glossy streaks; the spikes break them into points.
                float height = swell * .085 + ripple * .006 + spike * .024 * _Spikes * agitation;
                float3 nb = OrderBump(p, n, height);

                float3 v = GetWorldSpaceNormalizeViewDir(p);
                Light sun = GetMainLight(TransformWorldToShadowCoord(p));
                half shadow = sun.shadowAttenuation;
                float3 h = normalize(sun.direction + v);
                float nh = saturate(dot(nb, h)), nv = saturate(dot(nb, v));
                half fresnel = .04 + .96 * pow(1 - nv, 5);
                half3 ambient = lerp(_OrderGround.rgb, _OrderSky.rgb, saturate(nb.y * .5 + .5));
                half3 color = _Color.rgb * (ambient + sun.color * saturate(dot(nb, sun.direction)) * shadow);
                // A mirror: the reflected sky, and two sun lobes, one pin-sharp and one soft.
                color += NaniteSky(reflect(-v, nb)) * lerp(.06, .8, fresnel);
                color += sun.color * (pow(nh, 150) * 3 + pow(nh, 30) * .16) * shadow;
                // Nanites at work: sparks that blink on and off and drift with the flow.
                float2 sp = plane * 13 - flow * 2.2;
                float2 sc = floor(sp), sf = frac(sp) - .5;
                // Only a few cells carry a nanite, and each lights up briefly now and then.
                float seed = OrderHash(sc), blink = saturate(sin(t * (1.7 + seed * 2.6) + seed * 40) * 3 - 2.4) * step(.88, OrderHash(sc + 3.3));
                float spark = smoothstep(.09, 0, length(sf - (OrderHash2(sc + 7.1) - .5) * .55)) * blink;
                color += _Sheen.rgb * spark * 3 * _Spark;
                // A cold meniscus on the front, and a faint glow in the spikes' tips just below it.
                // On a flat top the front crosses the whole face at once, so the meniscus is faint there.
                color += _Sheen.rgb * (exp(-below * 30) * .9 * (1 - .85 * abs(n.y)) + spike * exp(-below * 4) * .25);
                if (_Fog > .5) color = OrderApplyWorld(color, p);
                return half4(color, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            float3 _LightDirection;
            struct ShadowVaryings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            ShadowVaryings shadowVert(Attributes input)
            {
                ShadowVaryings o; o.positionWS = LiquidWorld(input.positionOS);
                o.positionCS = OrderShadowPosition(o.positionWS, TransformObjectToWorldNormal(input.normalOS), _LightDirection);
                return o;
            }
            half4 shadowFrag(ShadowVaryings input) : SV_Target { Cut(input.positionWS); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            struct DepthVaryings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            DepthVaryings depthVert(Attributes input)
            {
                DepthVaryings o; o.positionWS = LiquidWorld(input.positionOS); o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            half depthFrag(DepthVaryings input) : SV_Target { Cut(input.positionWS); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex normalsVert
            #pragma fragment normalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct NormalsVaryings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            NormalsVaryings normalsVert(Attributes input)
            {
                NormalsVaryings o;
                o.positionWS = LiquidWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 normalsFrag(NormalsVaryings input) : SV_Target
            {
                Cut(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
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
