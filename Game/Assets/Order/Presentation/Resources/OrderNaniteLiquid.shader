Shader "Order/NaniteLiquid"
{
    // The machines' nanite liquid: near-black graphite magnetic liquid. It is drawn over the shape of a mesh (a rising
    // building, a worker melting into one) and only below its front, a height broken into tongues (NaniteFront):
    // - the surface is a mirror: sharp sun highlights and the sky and horizon reflected, strongest at grazing angles;
    // - magnetic spikes stand out of it in a cellular pattern and slowly sway; they grow tall near the front, where the
    //   liquid is thin and restless;
    // - the whole surface keeps flowing (up while it fills, down while it drains), in slow swells and fine ripples. The
    //   view passes how far the flow has gone (_Shift), not its speed, so a flow that turns round slows down and turns
    //   instead of the whole pattern jumping;
    // - a cold glowing meniscus runs along the front.
    // Nothing in it blinks or glints brighter than the camera's glow threshold by much: points that flash on and off
    // (sparks of nanites, tried first) bloomed into flickering blots bigger than the building.
    Properties
    {
        _Front("Liquid below this world height", Float) = 1000
        _FrontNoise("Front roughness", Float) = .25
        _Shift("Flow travelled (+ up, - down)", Float) = 0
        _Color("Graphite", Color) = (.028,.03,.035,1)
        _Sheen("Cold light", Color) = (.62,.86,1,1)
        _Spikes("Magnetic spikes", Float) = 1
        _Fog("Fog of war", Float) = 1
        // The liquid's own shape swells and shivers by up to _Wobble world units; its foot at _Base stays on the ground.
        _Wobble("Shape wobble", Float) = .06
        _Base("Ground under it", Float) = -1000
        _Pool("Pooling on flat tops (middle x, z, radius, depth)", Vector) = (0,0,1,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "OrderNanite.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color, _Sheen;
            float4 _Pool;
            float _Front, _FrontNoise, _Shift, _Spikes, _Fog, _Wobble, _Base;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
        // The wobble moves the shape, not where the liquid ends: that is judged where the point rests (rest), so the liquid
        // and the building it reveals keep parting on one line, without gaps or overlaps on a flat face.
        float3 LiquidOffset(float3 rest) { return NaniteWobble(rest, _Shift, _Time.y) * _Wobble * saturate((rest.y - _Base) * 2.5); }
        float3 LiquidWorld(float3 rest) { return rest + LiquidOffset(rest); }
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
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 restWS:TEXCOORD2; float3 bentWS:TEXCOORD3; };
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.restWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionWS = LiquidWorld(o.restWS);
                o.bentWS = LiquidBent(o.restWS, normalize(o.normalWS), o.positionWS - o.restWS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            // The relief over the face's plane, in world units of height: slow swells, fine ripples carried by the flow,
            // and magnetic spikes, cones on the centres of a cellular pattern that sways. fine fades the ripples (x) and
            // the spikes (y) out where they would be finer than the pixels.
            float Relief(float2 plane, float2 flow, float t, float spikes, float2 fine, out float spike)
            {
                float swell = OrderFbm(plane * 1.3 - flow * .35 + float2(t * .05, 0));
                float ripple = OrderNoise(plane * 9 - flow * 1.6 + float2(0, t * .4));
                float2 cellular = plane * 4.2 - flow * .8 + float2(sin(t * .7 + plane.y), cos(t * .6 + plane.x)) * .12;
                float3 cell = OrderVoronoi(cellular);
                spike = pow(saturate(1 - cell.x * 1.7), 2.6) * (.6 + .4 * sin(t * 1.9 + cell.z * 6.28)) * fine.y;
                return swell * .085 + ripple * .006 * fine.x + spike * .024 * spikes;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                // The mesh's own normal says where the liquid ends and which way it runs; the light takes the bent one.
                float3 n = normalize(input.normalWS);
                float below = Below(input.restWS, n), t = _Time.y;
                clip(below);
                float3 nl = normalize(input.bentWS);
                // The flow runs along the wall: up the plane's second axis, or across a roof.
                float2 plane = NanitePlane(p, n), flow = float2(0, _Shift);
                float3 u, w; NanitePlaneAxes(n, u, w);
                // The relief's slope by finite differences over the surface itself, not by screen derivatives, which
                // are shared by 2×2 pixels and turn a fine moving pattern into flickering bright squares.
                float pixel = max(length(ddx(p)), length(ddy(p)));
                float2 fine = saturate(1.5 - pixel * float2(30, 12));
                // Spikes stand tall near the front, where the liquid is thin and restless.
                float agitation = 1 + 2.2 * exp(-below * 5), spike, unused;
                const float e = .015;
                float h0 = Relief(plane, flow, t, _Spikes * agitation, fine, spike);
                float hu = Relief(plane + float2(e, 0), flow, t, _Spikes * agitation, fine, unused);
                float hv = Relief(plane + float2(0, e), flow, t, _Spikes * agitation, fine, unused);
                float3 slope = (u * (hu - h0) + w * (hv - h0)) / e;
                // Broad swells bend the reflections into long glossy streaks; the spikes break them into points.
                float3 nb = normalize(nl - (slope - nl * dot(nl, slope)));

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
                // Where the normal turns within a pixel the sharp lobe widens and dims (specular anti-aliasing), and a soft
                // ceiling keeps a glint from blooming into a flash bigger than the building.
                float3 dn = fwidth(nb);
                float a2 = 2. / 152 + min(2 * dot(dn, dn), .2), sharp = 2 / a2 - 2;
                half3 glint = sun.color * (pow(nh, sharp) * 3 * (sharp + 2) / 152 + pow(nh, 30) * .16) * shadow;
                color += glint / (1 + glint * .6);
                // A cold meniscus on the front, and a faint glow in the spikes' tips just below it.
                // On a flat top the front sweeps across the face far faster than down a wall, so the meniscus is faint there.
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
            struct ShadowVaryings { float4 positionCS:SV_POSITION; float3 restWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            ShadowVaryings shadowVert(Attributes input)
            {
                ShadowVaryings o; o.restWS = TransformObjectToWorld(input.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = OrderShadowPosition(LiquidWorld(o.restWS), o.normalWS, _LightDirection);
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
            struct DepthVaryings { float4 positionCS:SV_POSITION; float3 restWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            DepthVaryings depthVert(Attributes input)
            {
                DepthVaryings o; o.restWS = TransformObjectToWorld(input.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(LiquidWorld(o.restWS));
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
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct NormalsVaryings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 restWS:TEXCOORD1; };
            NormalsVaryings normalsVert(Attributes input)
            {
                NormalsVaryings o;
                o.restWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(LiquidWorld(o.restWS));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 normalsFrag(NormalsVaryings input) : SV_Target
            {
                Cut(input.restWS, input.normalWS);
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
