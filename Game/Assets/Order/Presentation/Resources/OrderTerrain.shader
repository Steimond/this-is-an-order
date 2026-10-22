Shader "Order/Terrain"
{
    // Procedural surface of the test map: moss and dirt in the basin, temple flagstones on the plateaus and the centre
    // plaza, stairs on ramps, masonry on walls. _DataTex (from TempleScenery) holds baked AO, paving, stairs and paths.
    Properties
    {
        _DataTex("Data (AO, paving, stairs, path)", 2D) = "black" {}
        _DataRect("Data rect (x0, z0, 1/width, 1/depth)", Vector) = (0,0,1,1)
        _Grass("Grass", Color) = (0.33,0.36,0.21,1)
        _Moss("Moss", Color) = (0.19,0.26,0.19,1)
        _Dry("Dry grass", Color) = (0.42,0.39,0.27,1)
        _Dirt("Dirt", Color) = (0.37,0.31,0.25,1)
        _Jungle("Jungle floor", Color) = (0.15,0.24,0.19,1)
        _Stone("Stone", Color) = (0.63,0.6,0.53,1)
        _StoneDark("Stone dark", Color) = (0.47,0.45,0.41,1)
        _Mortar("Mortar", Color) = (0.24,0.23,0.21,1)
        _Wall("Wall", Color) = (0.56,0.52,0.45,1)
        _Rock("Rock", Color) = (0.36,0.35,0.33,1)
        [HDR] _Vein("Living glow", Color) = (0.2,1.1,0.95,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "OrderCommon.hlsl"
        TEXTURE2D(_DataTex); SAMPLER(sampler_DataTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _DataTex_ST, _DataRect;
            half4 _Grass, _Moss, _Dry, _Dirt, _Jungle, _Stone, _StoneDark, _Mortar, _Wall, _Rock, _Vein;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
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
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS, n = normalize(input.normalWS);
                half4 data = SAMPLE_TEXTURE2D(_DataTex, sampler_DataTex, (p.xz - _DataRect.xy) * _DataRect.zw);
                float outside = OrderOutside(p.xz), wall = 1 - smoothstep(.4, .7, n.y), time = _Time.y;

                // Basin floor: moss, grass and dry patches, worn dirt along the paths, jungle floor out of bounds.
                float broad = OrderFbm(p.xz * .07), patches = OrderFbm(p.xz * .33 + 7.3), grain = OrderNoise(p.xz * 3.7);
                float dry = OrderFbm(p.xz * .15 + 21.4);
                half3 ground = lerp(_Moss.rgb, _Grass.rgb, smoothstep(.3, .75, broad));
                ground = lerp(ground, _Dry.rgb, smoothstep(.55, .78, dry) * .7);
                float dirt = saturate(smoothstep(.64, .86, patches) * .45 + smoothstep(.45, .9, data.a + (patches - .5) * .8) * .75);
                ground = lerp(ground, _Dirt.rgb, dirt);
                ground = lerp(ground, _Jungle.rgb, smoothstep(.5, 3.5, outside));
                ground *= (.8 + .3 * OrderNoise(p.xz * .9 + 3.3)) * (.9 + .2 * grain);
                float groundBump = patches * .05 + grain * .015;
                // Rare thin veins of the living ecosystem glow through the moss.
                float vein = smoothstep(.012, 0, abs(OrderNoise(p.xz * .5 + 31.7) * .75 + OrderNoise(p.xz * 1.4) * .25 - .5));
                vein *= smoothstep(.68, .8, OrderFbm(p.xz * .045 + 3.1)) * (1 - dirt) * (1 - smoothstep(1, 4, outside));
                half3 glow = _Vein.rgb * vein * (.2 + .15 * sin(time * 1.2 + p.x * .21 + p.z * .17));

                // Rectangular slabs in courses of varying length; the centre plaza is laid in rings around a seal.
                float2 centre = p.xz - _OrderMap.x * .5;
                float radius = length(centre), turn = atan2(centre.y, centre.x) / 6.28318 + .5;
                float row = floor(p.z / 1.1), slabLength = 1.2 + 1.3 * OrderHash(float2(row, 1.7)), shiftedX = p.x + OrderHash(float2(row, 5.3)) * 4;
                float slab = floor(shiftedX / slabLength);
                // Some slabs are split across the course, so joints do not line up into a grid.
                float split = OrderHash(float2(slab, row + 9.1)) < .35 ? .55 : 2;
                float2 local = float2(frac(shiftedX / slabLength) * slabLength, frac(p.z / 1.1) * 1.1);
                float slabBorder = min(min(local.x, slabLength - local.x), min(local.y, 1.1 - local.y));
                slabBorder = min(slabBorder, abs(local.y - split));
                slab += local.y > split ? .5 : 0;
                float ring = floor(radius / .95), segments = max(1, floor(ring * 6.2)), segment = (turn + OrderHash(float2(ring, 2.3))) * segments;
                float ringBorder = min(min(frac(radius / .95), 1 - frac(radius / .95)) * .95,
                    min(frac(segment), 1 - frac(segment)) * max(radius, .5) * 6.28318 / segments);
                bool plaza = radius < 9;
                float border = plaza ? ringBorder : slabBorder, stoneId = plaza ? OrderHash(float2(ring, floor(segment))) : OrderHash(float2(slab, row));
                float grout = 1 - smoothstep(.01, .035, border);
                half3 stone = lerp(_StoneDark.rgb, _Stone.rgb, .45 + .55 * stoneId) * (.93 + .14 * OrderNoise(p.xz * 2.6 + stoneId * 40));
                stone *= plaza && ring >= 7 ? .72 : 1;
                stone *= .86 + .14 * OrderFbm(p.xz * .2 + 9.2);
                stone = lerp(stone, _Moss.rgb, smoothstep(.62, .88, OrderFbm(p.xz * .27 + 5.1)) * .45);
                float crack = smoothstep(.012, 0, abs(OrderNoise(p.xz * 1.9 + stoneId * 13) - .5)) * step(.82, stoneId);
                stone *= 1 - crack * .25;
                stone = lerp(stone, lerp(lerp(_Mortar.rgb, stone * .55, .4), _Moss.rgb * .7, smoothstep(.5, .8, patches)), grout);
                float stoneBump = smoothstep(0, .05, border) * .02 - crack * .008;
                float seal = smoothstep(.045, 0, abs(radius - 4.75)) + smoothstep(.035, 0, abs(radius - 4.25)) + smoothstep(.04, 0, abs(radius - 1.15));
                seal += step(4.25, radius) * step(radius, 4.75) * smoothstep(.06, 0, abs(frac(turn * 16) - .5) * radius * .39);
                glow += _Vein.rgb * saturate(seal) * step(radius, 5) * (.7 + .3 * sin(time * .8)) * 1.4;
                // A few slabs are missing and the moss shows through; edges of paved areas break up.
                float missing = !plaza && stoneId < .03 ? .5 : 0;
                float cover = data.g - (OrderFbm(p.xz * .42 + 11.3) - .5) * 1.1 - missing;
                float paved = smoothstep(.42, .56, cover);
                // Ruined floors (partial paving) are older: darker, mossier and more broken.
                float ruined = 1 - smoothstep(.66, .8, data.g);
                stone = lerp(stone, lerp(stone * .7, _Moss.rgb, .3), ruined);
                paved *= 1 - ruined * smoothstep(.45, .7, OrderNoise(p.xz * 1.3 + 4.4));

                // Stairs: every 0.25 of height is a tread; the bump flattens the treads of the smooth ramp.
                float stepPhase = frac(p.y * 4), stairs = smoothstep(.4, .6, data.b);
                half3 stair = lerp(_StoneDark.rgb, _Stone.rgb, .35 + .4 * OrderHash(floor(p.y * 4).xx)) * (.9 + .18 * OrderNoise(p.xz * 3));
                stair *= (1 - smoothstep(.72, 1, stepPhase) * .42 + (1 - smoothstep(0, .1, stepPhase)) * .08) * (.88 + .12 * OrderFbm(p.xz * .6));
                float stairBump = -stepPhase * .2;

                // Masonry courses with offset joints, damp mossy foot and vines; natural rock out of bounds.
                float along = abs(n.x) > abs(n.z) ? p.z : p.x;
                float course = floor(p.y / .45), shifted = along + OrderHash(float2(course, 7.1)) * 3;
                float block = floor(shifted / .95);
                float2 inside = float2(frac(shifted / .95) * .95, frac(p.y / .45) * .45);
                float joint = 1 - smoothstep(.015, .05, min(min(inside.x, .95 - inside.x), min(inside.y, .45 - inside.y)));
                // Fade the joints out where courses get denser than a few pixels (walls seen edge-on).
                joint *= saturate(1.6 - max(fwidth(shifted / .95), fwidth(p.y / .45)) * 6);
                half3 masonry = _Wall.rgb * (.78 + .34 * OrderHash(float2(block, course))) * (.9 + .2 * OrderNoise(float2(along, p.y) * 3.1));
                float damp = 1 - smoothstep(0, 1.3, p.y);
                float vines = smoothstep(.6, .76, OrderFbm(float2(along * .9, p.y * .4)) + (p.y - 2) * .1);
                masonry *= 1 - damp * .3 - smoothstep(.55, .9, OrderNoise(float2(along * 2.3, p.y * .35 + 4))) * .18;
                masonry = lerp(masonry, _Moss.rgb * .75, saturate(damp * .45 + vines * .85));
                masonry = lerp(masonry, _Mortar.rgb, joint);
                half3 rock = _Rock.rgb * (.7 + .55 * OrderFbm(float2(along, p.y) * 1.2 + p.xz * .1));
                float natural = smoothstep(.3, 1.5, outside);
                half3 wallColor = lerp(masonry, rock, natural);
                float wallBump = lerp((1 - joint) * .035, OrderFbm(float2(along, p.y) * 2.1) * .12, natural);
                ground = lerp(ground, rock, (1 - smoothstep(.72, .9, n.y)) * natural);

                half3 floorColor = lerp(lerp(ground, stone, paved), stair, stairs);
                half3 albedo = lerp(floorColor, wallColor, wall);
                float bump = lerp(lerp(lerp(groundBump, stoneBump, paved), stairBump, stairs), wallBump, wall);
                float3 normal = OrderBump(p, n, bump);
                half ao = lerp(data.r, 1 - damp * .45, wall);
                half3 color = OrderLighting(albedo, p, normal, ao, input.positionCS, lerp(.04, .3, paved) * (1 - wall));
                color += glow * (1 - wall) * lerp(1 - paved, 1, plaza) * (1 - stairs) * OrderVision(p).x;
                return half4(OrderApplyWorld(color, p), 1);
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
            float3 _LightDirection;
            float4 shadowVert(Attributes input) : SV_POSITION
            {
                return OrderShadowPosition(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS), _LightDirection);
            }
            half4 shadowFrag() : SV_Target { return 0; }
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
            float4 depthVert(Attributes input) : SV_POSITION { return TransformObjectToHClip(input.positionOS.xyz); }
            half depthFrag() : SV_Target { return 0; }
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
            struct NormalsVaryings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            NormalsVaryings normalsVert(Attributes input)
            {
                NormalsVaryings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 normalsFrag(NormalsVaryings input) : SV_Target
            {
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
