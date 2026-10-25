Shader "Order/NaniteLiquid"
{
    // The machines' nanite liquid: near-black graphite magnetic liquid. It is drawn over the shape of a mesh (a rising
    // building, a worker melting into one) and only below its front, a height broken into tongues (NaniteFront):
    // - the surface is a mirror: sharp sun highlights and the sky and horizon reflected, strongest at grazing angles;
    // - it churns in slow swells and fine ripples of 3D noise whose domain is warped by more noise wandering in time, so
    //   the pattern changes where it is instead of sliding across as a tile; the flow (up while it fills, down while it
    //   drains) carries it a little. The view passes how far the flow has gone (_Shift), not its speed, so a flow that
    //   turns round slows down and turns instead of the whole pattern jumping;
    // - its shape swells and shivers (the vertex wobble);
    // - magnetic spikes stand out of it as real geometry: the same shader with NANITE_SPIKES draws a mesh of cusped
    //   cones on feet spread over the shape (NaniteMeshes.Spikes). A 3D noise wandering in time is the field: patches
    //   of spikes rise and sink together, and they sink into the liquid where its edge comes near;
    // - a cold glowing meniscus runs along the front.
    // Nothing in it blinks or glints brighter than the camera's glow threshold by much: points that flash on and off
    // (sparks of nanites, tried first) bloomed into flickering blots bigger than the building.
    // The look is tuned in the material (Resources/NaniteLiquid, VisualLab.unity to see it all); what the view sets for
    // each object (its edge, flow, pooling, and how much of the material's spikes and wobble it takes) is hidden.
    Properties
    {
        [Header(Look)]
        _Color("Graphite", Color) = (.028,.03,.035,1)
        _Sheen("Cold light", Color) = (.62,.86,1,1)
        _Mirror("Sky reflection", Range(0,2)) = 1
        _Glint("Sun glints", Range(0,2)) = 1
        _Meniscus("Glow along the edge", Range(0,2)) = 1
        [Header(Surface)]
        _Swell("Swells", Range(0,3)) = 1
        _Ripple("Ripples", Range(0,3)) = 1
        _Churn("Swells change, speed", Range(0,4)) = 1
        // The shape swells and shivers by up to _Wobble world units; its foot on the ground stays put.
        _Wobble("Shape wobble, world units", Range(0,.2)) = .06
        _WobbleSpeed("Shape wobble, speed", Range(0,4)) = 1
        [Header(Spikes)]
        _SpikeHeight("Height", Range(0,3)) = 1
        _SpikeWidth("Width", Range(.3,2)) = 1
        _SpikeDensity("Density: share of spikes that can stand", Range(0,1)) = .55
        _SpikeField("Field scale: higher, smaller patches", Range(.2,4)) = 1
        _SpikeThreshold("Field threshold: higher, fewer stand", Range(0,1)) = .22
        _SpikeSpeed("Rise and sink, speed", Range(0,4)) = 1
        _SpikeTips("Glow in the tips", Range(0,1)) = .18
        _Fog("Fog of war", Float) = 1
        [HideInInspector] _Front("Liquid below this world height", Float) = 1000
        [HideInInspector] _FrontNoise("Front roughness", Float) = .25
        [HideInInspector] _Shift("Flow travelled (+ up, - down)", Float) = 0
        [HideInInspector] _Base("Ground under it", Float) = -1000
        [HideInInspector] _Pool("Pooling on flat tops (middle x, z, radius, depth)", Vector) = (0,0,1,0)
        [HideInInspector] _Spikes("This object's share of the spikes", Float) = 1
        [HideInInspector] _WobbleFade("This object's share of the wobble", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "OrderNanite.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color, _Sheen;
            float4 _Pool;
            float _Mirror, _Glint, _Meniscus, _Swell, _Ripple, _Churn, _Wobble, _WobbleSpeed;
            float _SpikeHeight, _SpikeWidth, _SpikeDensity, _SpikeField, _SpikeThreshold, _SpikeSpeed, _SpikeTips, _Fog;
            float _Front, _FrontNoise, _Shift, _Base, _Spikes, _WobbleFade;
        CBUFFER_END
        // A spike's vertex also carries a tangent across its foot, its place on the cone and the spike itself
        // (NaniteMeshes.Spikes); the surface uses position and normal only.
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float4 place:TEXCOORD0; float4 spike:TEXCOORD1; };
        // The wobble moves the shape, not where the liquid ends: that is judged where the point rests (rest), so the liquid
        // and the building it reveals keep parting on one line, without gaps or overlaps on a flat face.
        float3 LiquidOffset(float3 rest) { return NaniteWobble(rest, _Shift, _Time.y * _WobbleSpeed) * _Wobble * _WobbleFade * saturate((rest.y - _Base) * 2.5); }
        // The bent shape's normal, smooth over each face: the mesh normal tilted against the slope of the offset along it
        // (finite differences), so the light follows the swells and no single small facet flashes as it turns.
        float3 LiquidBent(float3 rest, float3 n, float3 offset)
        {
            float3 a = normalize(cross(n, abs(n.y) < .9 ? float3(0, 1, 0) : float3(1, 0, 0))), b = cross(n, a);
            const float e = .04;
            float h = dot(offset, n);
            float2 slope = float2(dot(LiquidOffset(rest + a * e), n) - h, dot(LiquidOffset(rest + b * e), n) - h) / e;
            return normalize(n - a * slope.x - b * slope.y);
        }
        float Below(float3 rest, float3 n) { return NaniteFront(rest, _Front, _FrontNoise) - NaniteLevel(rest, normalize(n), _Pool); }
        void Cut(float3 rest, float3 n) { clip(Below(rest, n)); }
        // How tall a spike stands: the field is a 3D noise wandering in time and drifting with the flow, so patches of
        // spikes rise and sink together and weak ones lie down flat; each spike a little its own; and none where the
        // edge is near, where the liquid is about to leave or has only just come.
        float SpikeHeight(float3 foot, float below, float tallest, float seed)
        {
            float t = _Time.y * _SpikeSpeed;
            float3 q = foot * 1.7 * _SpikeField + float3(t * .11, -_Shift * .3, t * .07);
            float field = NaniteNoise3(q) * .65 + NaniteNoise3(q * 2.3 + float3(5.1, -t * .17, 2.3)) * .35;
            float stand = pow(smoothstep(_SpikeThreshold, _SpikeThreshold + .53, field), 1.2);
            return tallest * _SpikeHeight * _Spikes * stand * (.65 + .35 * frac(seed * 7.31)) * smoothstep(0, .12, below);
        }
        // One point of the liquid's shape: where it is drawn, where it rests for the edge (a spike's foot, for the whole
        // spike), the mesh normal there (the edge and the pooling go by it), the normal the light takes, and how far up a
        // spike it is (0 on the surface, 1 at a tip).
        struct LiquidPoint { float3 positionWS, restWS, normalWS, bentWS; float tip; };
        LiquidPoint LiquidShape(Attributes input)
        {
            LiquidPoint o;
            o.restWS = TransformObjectToWorld(input.positionOS.xyz);
            o.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
            float3 offset = LiquidOffset(o.restWS);
        #if defined(NANITE_SPIKES)
            // A cone on the foot, z = h (1 - x)^1.5 at the share x of its radius: sharp at the tip, its flanks curving in,
            // its broad foot running smoothly into the surface and the neighbours' feet, sunk a little under it so the
            // two never fight for a pixel.
            // The mesh carries spikes as close as they can stand; the density keeps that share of them (by their seed),
            // and the rest shrink to a point under the surface.
            float3 n = normalize(input.normalOS), tangent = normalize(input.tangentOS.xyz), across = cross(n, tangent);
            float3 radial = tangent * input.place.x + across * input.place.y;
            float stands = step(input.spike.z, _SpikeDensity);
            float x = input.place.z, rest = 1 - x, radius = max(input.spike.x * _SpikeWidth * stands, 1e-4);
            float h = SpikeHeight(o.restWS, Below(o.restWS, o.normalWS), input.spike.y, input.spike.z) * stands;
            o.positionWS = TransformObjectToWorld(input.positionOS.xyz + radial * radius * x + n * (h * rest * sqrt(rest) - .008)) + offset;
            o.bentWS = normalize(TransformObjectToWorldNormal(n + radial * 1.5 * h * sqrt(rest) / radius));
            o.tip = rest;
        #else
            o.positionWS = o.restWS + offset;
            o.bentWS = LiquidBent(o.restWS, o.normalWS, offset);
            o.tip = 0;
        #endif
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ NANITE_SPIKES
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 restWS:TEXCOORD2; float3 bentWS:TEXCOORD3; float tip:TEXCOORD4; };
            Varyings vert(Attributes input)
            {
                LiquidPoint shape = LiquidShape(input);
                Varyings o;
                o.positionWS = shape.positionWS; o.restWS = shape.restWS; o.normalWS = shape.normalWS; o.bentWS = shape.bentWS; o.tip = shape.tip;
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            // The surface's relief, in world units of height: slow swells of 3D noise over a domain warped by more noise
            // that wanders in time, carried a little by the flow; and fine ripples, faded out where they would be finer
            // than the pixels.
            float Relief(float3 p, float t, float fine)
            {
                t *= _Churn;
                float3 q = p * 1.1 + float3(0, -_Shift * .3, 0);
                float3 warp = float3(NaniteNoise3(q + float3(0, 0, t * .13)), NaniteNoise3(q + float3(9.2, t * .11, 0)), NaniteNoise3(q + float3(t * .12, 4.7, 0))) - .5;
                float3 r = q + warp * 1.8;
                float swell = NaniteNoise3(r) * .65 + NaniteNoise3(r * 2.2 + float3(0, t * .09, 0)) * .35;
                return swell * .07 * _Swell + NaniteNoise3(p * 7 + warp * 4) * .004 * _Ripple * fine;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                // The mesh's own normal says where the liquid ends; the light takes the bent one.
                float3 n = normalize(input.normalWS);
                float below = Below(input.restWS, n), t = _Time.y;
                clip(below);
                float3 nb = normalize(input.bentWS);
            #if !defined(NANITE_SPIKES)
                // The relief's slope by finite differences over the surface itself, not by screen derivatives, which
                // are shared by 2×2 pixels and turn a fine moving pattern into flickering bright squares.
                float pixel = max(length(ddx(p)), length(ddy(p))), fine = saturate(1.5 - pixel * 30);
                float3 a = normalize(cross(nb, abs(nb.y) < .9 ? float3(0, 1, 0) : float3(1, 0, 0))), b = cross(nb, a);
                const float e = .02;
                float h0 = Relief(p, t, fine), ha = Relief(p + a * e, t, fine), hb = Relief(p + b * e, t, fine);
                // Broad swells bend the reflections into long glossy streaks.
                nb = normalize(nb - (a * (ha - h0) + b * (hb - h0)) / e);
            #endif

                float3 v = GetWorldSpaceNormalizeViewDir(p);
                Light sun = GetMainLight(TransformWorldToShadowCoord(p));
                half shadow = sun.shadowAttenuation;
                float3 h = normalize(sun.direction + v);
                float nh = saturate(dot(nb, h)), nv = saturate(dot(nb, v));
                half fresnel = .04 + .96 * pow(1 - nv, 5);
                half3 ambient = lerp(_OrderGround.rgb, _OrderSky.rgb, saturate(nb.y * .5 + .5));
                half3 color = _Color.rgb * (ambient + sun.color * saturate(dot(nb, sun.direction)) * shadow);
                // A mirror: the reflected sky, and two sun lobes, one pin-sharp and one soft. A spike's steep flanks catch the
                // sky at a grazing angle almost everywhere, so they mirror it less, to stay dark graphite with bright edges.
            #if defined(NANITE_SPIKES)
                color += NaniteSky(reflect(-v, nb)) * lerp(.04, .5, fresnel) * _Mirror;
            #else
                color += NaniteSky(reflect(-v, nb)) * lerp(.06, .8, fresnel) * _Mirror;
            #endif
                // Where the normal turns within a pixel (a spike's tip, a far-off ripple) the sharp lobe widens and dims
                // (specular anti-aliasing), and a soft ceiling keeps a glint from blooming into a flash.
                float3 dn = fwidth(nb);
                float a2 = 2. / 152 + min(2 * dot(dn, dn), .2), sharp = 2 / a2 - 2;
                half3 glint = sun.color * (pow(nh, sharp) * 3 * (sharp + 2) / 152 + pow(nh, 30) * .16) * shadow * _Glint;
                color += glint / (1 + glint * .6);
            #if defined(NANITE_SPIKES)
                // A faint cold light gathers in the tips.
                color += _Sheen.rgb * pow(input.tip, 6) * _SpikeTips;
            #else
                // A cold meniscus on the front; on a flat top the front sweeps across the face far faster than down a
                // wall, so it is faint there.
                color += _Sheen.rgb * exp(-below * 30) * .9 * (1 - .85 * abs(n.y)) * _Meniscus;
            #endif
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
            #pragma multi_compile_local _ NANITE_SPIKES
            float3 _LightDirection;
            struct ShadowVaryings { float4 positionCS:SV_POSITION; float3 restWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            ShadowVaryings shadowVert(Attributes input)
            {
                LiquidPoint shape = LiquidShape(input);
                ShadowVaryings o; o.restWS = shape.restWS; o.normalWS = shape.normalWS;
                o.positionCS = OrderShadowPosition(shape.positionWS, shape.bentWS, _LightDirection);
                return o;
            }
            half4 shadowFrag(ShadowVaryings input) : SV_Target { Cut(input.restWS, input.normalWS); return 0; }
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
            #pragma multi_compile_local _ NANITE_SPIKES
            struct DepthVaryings { float4 positionCS:SV_POSITION; float3 restWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            DepthVaryings depthVert(Attributes input)
            {
                LiquidPoint shape = LiquidShape(input);
                DepthVaryings o; o.restWS = shape.restWS; o.normalWS = shape.normalWS; o.positionCS = TransformWorldToHClip(shape.positionWS);
                return o;
            }
            half depthFrag(DepthVaryings input) : SV_Target { Cut(input.restWS, input.normalWS); return 0; }
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
            #pragma multi_compile_local _ NANITE_SPIKES
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct NormalsVaryings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 restWS:TEXCOORD1; float3 bentWS:TEXCOORD2; };
            NormalsVaryings normalsVert(Attributes input)
            {
                LiquidPoint shape = LiquidShape(input);
                NormalsVaryings o; o.restWS = shape.restWS; o.normalWS = shape.normalWS; o.bentWS = shape.bentWS;
                o.positionCS = TransformWorldToHClip(shape.positionWS);
                return o;
            }
            half4 normalsFrag(NormalsVaryings input) : SV_Target
            {
                Cut(input.restWS, input.normalWS);
                float3 normalWS = normalize(input.bentWS);
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
