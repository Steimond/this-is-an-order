using System;
using System.Collections.Generic;
using Order.Content;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Order.Presentation
{
    // Generated look of Temple Basin: temple terraces and stairs over the TempleMap cells, the jungle and ruins beyond
    // the playable area, light, post-processing and the smoothed fog-of-war texture. Purely visual: inside the map the
    // terrain top is exactly TempleMap.Height, and only that part has a collider.
    public sealed class TempleScenery : IDisposable
    {
        public const int Margin = 24, FogTexels = 2;
        const int Size = TempleMap.Size, DataTexels = 4, Chunk = 16;
        const float Batter = .22f;
        public static readonly Color Mist = new Color(.03f, .055f, .065f);
        static readonly Color StoneColor = new Color(.62f, .6f, .54f), MossColor = new Color(.26f, .38f, .24f), RockColor = new Color(.36f, .34f, .31f);
        static readonly Color[] Leaves = { new Color(.17f, .36f, .29f), new Color(.23f, .41f, .25f), new Color(.13f, .31f, .33f), new Color(.28f, .4f, .2f),
            new Color(.2f, .38f, .27f), new Color(.31f, .27f, .38f) };
        static readonly Vector4[] Roads = { new Vector4(26, 11, 38, 11), new Vector4(26, 53, 38, 53), new Vector4(32, 11, 32, 53), new Vector4(5, 32, 59, 32) };
        readonly TempleMap map;
        readonly Transform root;
        readonly List<Object> owned = new List<Object>();
        readonly List<Wall> walls = new List<Wall>();
        readonly List<Rect> ruins = new List<Rect>();
        readonly System.Random random = new System.Random(1931);
        readonly Texture2D fog;
        readonly Color32[] fogPixels;
        readonly float[] shownVisible, shownExplored, targetVisible, targetExplored, blurScratch;
        readonly bool[] lastVisible = new bool[Size * Size], lastExplored = new bool[Size * Size];
        bool fogReady, settled;
        Material foliage, solid, steam;
        public Light Sun { get; private set; }

        struct Wall { public Vector3 Top0, Top1, Low0, Low1, Outward; }
        sealed class Chunks
        {
            public readonly Dictionary<int, MeshBuilder> Builders = new Dictionary<int, MeshBuilder>();
            public MeshBuilder At(Vector3 p)
            {
                int key = Mathf.FloorToInt((p.x + Margin) / Chunk) * 100 + Mathf.FloorToInt((p.z + Margin) / Chunk);
                if (!Builders.TryGetValue(key, out var builder)) Builders[key] = builder = new MeshBuilder();
                return builder;
            }
        }

        public TempleScenery(TempleMap map, Transform root, Camera camera)
        {
            this.map = map; this.root = root;
            int n = Size * FogTexels;
            fog = Own(new Texture2D(n, n, TextureFormat.RGBA32, false, true) { name = "Fog of war", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear });
            fogPixels = new Color32[n * n];
            shownVisible = new float[n * n]; shownExplored = new float[n * n]; targetVisible = new float[n * n]; targetExplored = new float[n * n]; blurScratch = new float[n * n];
            Shader.SetGlobalTexture("_OrderFogTex", fog);
            Shader.SetGlobalVector("_OrderMap", new Vector4(Size, 1f / Size, 1, 0));
            Shader.SetGlobalColor("_OrderTeamColor", Color.white);
            BuildAtmosphere(camera);
            solid = Prop("Scenery");
            foliage = Prop("Foliage", m => { m.SetFloat("_Cull", 0); m.SetFloat("_Sway", 1); m.SetFloat("_Gloss", .05f); });
            PlaceRuins();
            BuildTerrain();
            BuildTrim();
            BuildWilds();
            BuildDecor();
        }

        public T Own<T>(T item) where T : Object { owned.Add(item); return item; }
        public Material Prop(string name, Action<Material> setup = null)
        {
            var material = Own(new Material(Shader.Find("Order/Prop")) { name = name });
            setup?.Invoke(material);
            return material;
        }
        // Additive by default; translucent effects blend normally. Colours above 1 drive the bloom.
        public Material Glow(string name, Color color, bool additive = true, Action<Material> setup = null)
        {
            var material = Own(new Material(Shader.Find("Order/Glow")) { name = name });
            material.SetColor("_Color", color);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            setup?.Invoke(material);
            return material;
        }
        public GameObject Model(string name, Mesh mesh, Material material, Transform parent, bool shadows = true)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        // ---------- Heights ----------
        static float Smooth(float a, float b, float x) { var t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        static float Fbm(float x, float z)
        {
            float sum = 0, amplitude = .5f;
            for (int i = 0; i < 4; i++) { sum += amplitude * Mathf.PerlinNoise(x + 100, z + 100); x = x * 2.03f + 17.1f; z = z * 2.03f + 5.3f; amplitude *= .5f; }
            return sum / .9375f;
        }
        public static float Outside(float x, float z)
        {
            float ox = Mathf.Max(Mathf.Max(-x, x - Size), 0), oz = Mathf.Max(Mathf.Max(-z, z - Size), 0);
            return Mathf.Sqrt(ox * ox + oz * oz);
        }
        // 1 where the corner plateaus would continue beyond the map edge.
        static float Terrace(float x, float z)
        {
            float u = Mathf.Min(x, Size - x), v = Mathf.Min(z, Size - z);
            return 1 - Smooth(-2, 2, Mathf.Max(u - 20, v - 20));
        }
        // Surface of a cell at a point on it: map cells are the gameplay surface, outer cells share the continuous rim.
        float Surface(int cx, int cz, float x, float z) => map.Inside(cx, cz) ? map.Height(cx, cz, Mathf.Clamp01(x - cx)) : Rim(x, z);
        // Beyond the edge the terraces and the basin floor rise into jungle hills.
        float Rim(float x, float z)
        {
            float cx = Mathf.Clamp(x, 0, Size - .001f), cz = Mathf.Clamp(z, 0, Size - .001f), d = Outside(x, z);
            float edge = map.Height((int)cx, (int)cz, cx - (int)cx);
            float hills = Smooth(1, 11, d) * (1.2f + 5 * Fbm(x * .055f, z * .055f)) + Smooth(.5f, 3, d) * (Fbm(x * .23f + 9, z * .23f) - .5f) * .9f;
            return Mathf.Lerp(edge, 3 * Terrace(x, z), Smooth(0, 4, d)) + hills;
        }
        Vector3 RimNormal(Vector3 p)
        {
            const float e = .35f;
            return new Vector3(Rim(p.x - e, p.z) - Rim(p.x + e, p.z), 2 * e, Rim(p.x, p.z - e) - Rim(p.x, p.z + e)).normalized;
        }
        float Ground(float x, float z) => Surface(Mathf.FloorToInt(x), Mathf.FloorToInt(z), x, z);
        static float Path(float x, float z)
        {
            // Worn trails wander around the straight lines between ramps and the plaza.
            float best = 99;
            var p = new Vector2(x + (Mathf.PerlinNoise(x * .07f + 3, z * .07f) - .5f) * 4, z + (Mathf.PerlinNoise(x * .07f, z * .07f + 7) - .5f) * 4);
            foreach (var road in Roads)
            {
                Vector2 a = new Vector2(road.x, road.y), ab = new Vector2(road.z, road.w) - a;
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude)));
            }
            return 1 - Smooth(.45f, 1.25f, best + (Mathf.PerlinNoise(x * .3f + 40, z * .3f + 40) - .5f) * 1.4f);
        }
        float Paving(float x, float z)
        {
            int cx = Mathf.FloorToInt(x), cz = Mathf.FloorToInt(z);
            if (!map.Inside(cx, cz)) return Terrace(x, z) * .75f * (1 - Smooth(1.5f, 6, Outside(x, z)));
            if (map.Ramp(cx, cz)) return 1;
            if (map.Height(cx, cz) > 2.5f) return .85f;
            float r = Vector2.Distance(new Vector2(x, z), new Vector2(Size * .5f, Size * .5f)), ruin = 0;
            foreach (var f in ruins) ruin = Mathf.Max(ruin, .62f * Smooth(0, .4f, Mathf.Min(Mathf.Min(x - f.xMin, f.xMax - x), Mathf.Min(z - f.yMin, f.yMax - z))));
            return Mathf.Max(ruin, 1 - Smooth(7.2f, 7.7f, r + (Fbm(x * .35f, z * .35f) - .5f) * .5f));
        }
        // Overgrown floors of old buildings in the basin: paint only, nothing blocks movement.
        void PlaceRuins()
        {
            var centre = new Vector2(Size * .5f, Size * .5f);
            for (int attempt = 0; attempt < 400 && ruins.Count < 12; attempt++)
            {
                var rect = new Rect(R(2, Size - 6), R(2, Size - 6), Mathf.Round(R(2, 4.5f)), Mathf.Round(R(2, 5)));
                bool clear = Vector2.Distance(rect.center, centre) > 10 && !ruins.Exists(o => o.Overlaps(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6)));
                for (int z = (int)rect.yMin - 1; clear && z <= (int)rect.yMax + 1; z++)
                    for (int x = (int)rect.xMin - 1; clear && x <= (int)rect.xMax + 1; x++)
                        clear = map.Inside(x, z) && map.Height(x, z) < .5f && !map.Ramp(x, z);
                bool Near(ResourceDeposit d)
                {
                    var c = new Vector2(d.X + .5f, d.Z + .5f);
                    return Vector2.Distance(c, new Vector2(Mathf.Clamp(c.x, rect.xMin, rect.xMax), Mathf.Clamp(c.y, rect.yMin, rect.yMax))) < 2.5f;
                }
                if (clear && !map.Resources.Exists(Near)) ruins.Add(rect);
            }
        }

        // ---------- Terrain ----------
        void BuildTerrain()
        {
            var surface = new TerrainBuilder(); var collision = new TerrainBuilder();
            for (int cz = -Margin; cz < Size + Margin; cz++) for (int cx = -Margin; cx < Size + Margin; cx++)
            {
                Vector3 a = Corner(cx, cz, cx, cz), b = Corner(cx, cz, cx, cz + 1), c = Corner(cx, cz, cx + 1, cz + 1), d = Corner(cx, cz, cx + 1, cz);
                if (map.Inside(cx, cz))
                {
                    var up = Vector3.Cross(b - a, d - a).normalized;
                    surface.Quad(a, b, c, d, up, up, up, up); collision.Quad(a, b, c, d, up, up, up, up);
                }
                else surface.Quad(a, b, c, d, RimNormal(a), RimNormal(b), RimNormal(c), RimNormal(d));
                if (cx + 1 < Size + Margin) Edge(cx, cz, cx + 1, cz, surface, collision);
                if (cz + 1 < Size + Margin) Edge(cx, cz, cx, cz + 1, surface, collision);
            }
            var terrain = Own(new Material(Shader.Find("Order/Terrain")) { name = "Temple terrain" });
            BuildData(terrain);
            var view = Model("Terrain", Own(surface.ToMesh("Temple terrain")), terrain, root);
            view.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            var collider = new GameObject("Terrain collider", typeof(MeshCollider));
            collider.transform.SetParent(root, false);
            collider.GetComponent<MeshCollider>().sharedMesh = Own(collision.ToMesh("Temple collision"));
        }
        Vector3 Corner(int cx, int cz, float x, float z) => new Vector3(x, Surface(cx, cz, x, z), z);
        // Vertical wall wherever two neighbouring cells disagree on the height of their shared edge.
        void Edge(int ax, int az, int bx, int bz, TerrainBuilder surface, TerrainBuilder collision)
        {
            bool insideA = map.Inside(ax, az), insideB = map.Inside(bx, bz);
            if (!insideA && !insideB) return;
            bool east = bx != ax;
            Vector2 e0 = east ? new Vector2(bx, az) : new Vector2(ax, bz), e1 = east ? new Vector2(bx, az + 1) : new Vector2(ax + 1, bz);
            float a0 = Surface(ax, az, e0.x, e0.y), a1 = Surface(ax, az, e1.x, e1.y), b0 = Surface(bx, bz, e0.x, e0.y), b1 = Surface(bx, bz, e1.x, e1.y);
            if (Mathf.Abs(a0 - b0) < .01f && Mathf.Abs(a1 - b1) < .01f) return;
            var outward = (a0 + a1 > b0 + b1 ? 1 : -1) * (east ? Vector3.right : Vector3.forward);
            var wall = new Wall
            {
                Top0 = new Vector3(e0.x, Mathf.Max(a0, b0), e0.y), Top1 = new Vector3(e1.x, Mathf.Max(a1, b1), e1.y),
                Low0 = new Vector3(e0.x, Mathf.Min(a0, b0), e0.y), Low1 = new Vector3(e1.x, Mathf.Min(a1, b1), e1.y), Outward = outward
            };
            // The visible wall leans back (its foot stands out by up to Batter), so walls seen edge-on from the
            // camera still read as a strip; the collider and gameplay keep the vertical cell edge.
            var foot0 = wall.Low0 + outward * Batter * Mathf.Clamp01((wall.Top0.y - wall.Low0.y) / 3);
            var foot1 = wall.Low1 + outward * Batter * Mathf.Clamp01((wall.Top1.y - wall.Low1.y) / 3);
            var lean = (outward * 3 + Vector3.up * Batter).normalized;
            surface.Wall(foot0, wall.Top0, wall.Top1, foot1, lean);
            if (insideA && insideB) { collision.Wall(wall.Low0, wall.Top0, wall.Top1, wall.Low1, outward); walls.Add(wall); }
        }
        // AO, paving, stairs and paths baked at DataTexels per cell for the terrain shader.
        void BuildData(Material terrain)
        {
            int cells = Size + 2 * Margin, n = cells * DataTexels;
            var heights = new float[n * n];
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
                heights[j * n + i] = Ground(-Margin + (i + .5f) / DataTexels, -Margin + (j + .5f) / DataTexels);
            var directions = new Vector2[8];
            for (int k = 0; k < 8; k++) directions[k] = new Vector2(Mathf.Cos(k * Mathf.PI / 4), Mathf.Sin(k * Mathf.PI / 4));
            int[] radii = { 1, 3, 6, 10 };
            var pixels = new Color32[n * n];
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
            {
                float h = heights[j * n + i], occlusion = 0;
                foreach (var direction in directions)
                {
                    float steepest = 0;
                    foreach (var r in radii)
                    {
                        int si = Mathf.Clamp(i + Mathf.RoundToInt(direction.x * r), 0, n - 1), sj = Mathf.Clamp(j + Mathf.RoundToInt(direction.y * r), 0, n - 1);
                        steepest = Mathf.Max(steepest, (heights[sj * n + si] - h) * DataTexels / r);
                    }
                    occlusion += steepest / Mathf.Sqrt(1 + steepest * steepest);
                }
                float x = -Margin + (i + .5f) / DataTexels, z = -Margin + (j + .5f) / DataTexels;
                int cx = Mathf.FloorToInt(x), cz = Mathf.FloorToInt(z);
                pixels[j * n + i] = new Color(Mathf.Clamp(1 - occlusion / 8 * 1.5f, .2f, 1), Paving(x, z), map.Inside(cx, cz) && map.Ramp(cx, cz) ? 1 : 0, Path(x, z));
            }
            var data = Own(new Texture2D(n, n, TextureFormat.RGBA32, false, true) { name = "Temple terrain data", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear });
            data.SetPixels32(pixels); data.Apply(false, true);
            terrain.SetTexture("_DataTex", data);
            terrain.SetVector("_DataRect", new Vector4(-Margin, -Margin, 1f / cells, 1f / cells));
        }

        // ---------- Wall trim ----------
        // Coping stones along every wall top (flush with the walkable floor), a plinth at the foot, rubble and glow plants.
        void BuildTrim()
        {
            var stone = new Chunks(); var plants = new Chunks();
            foreach (var wall in walls)
            {
                var along = wall.Top1 - wall.Top0; along.y = 0;
                float length = along.magnitude, layer = Mathf.Abs(wall.Outward.x) > .5f ? .004f : .008f, t = 0;
                var builder = stone.At(wall.Top0);
                builder.Moss(MossColor, .35f); builder.Transform = Matrix4x4.identity; builder.Team = 0; builder.Glow = 0;
                while (t < length - .01f)
                {
                    float segment = Mathf.Min(length - t, R(.42f, .78f));
                    if (length - t - segment < .2f) segment = length - t;
                    float s0 = (t + .012f) / length, s1 = (t + segment - .012f) / length;
                    Vector3 top0 = Vector3.Lerp(wall.Top0, wall.Top1, s0) + Vector3.up * layer, top1 = Vector3.Lerp(wall.Top0, wall.Top1, s1) + Vector3.up * layer;
                    builder.Paint = StoneColor * R(1.1f, 1.25f);
                    builder.Slab(top0 + wall.Outward * .12f, top1 + wall.Outward * .12f, -wall.Outward * .54f, .22f);
                    float height = Mathf.Min(top0.y, top1.y) - Mathf.Max(wall.Low0.y, wall.Low1.y);
                    if (height > .45f)
                    {
                        float out0 = Batter * Mathf.Clamp01((wall.Top0.y - wall.Low0.y) / 3) + .07f, out1 = Batter * Mathf.Clamp01((wall.Top1.y - wall.Low1.y) / 3) + .07f;
                        Vector3 foot0 = Vector3.Lerp(wall.Low0, wall.Low1, s0), foot1 = Vector3.Lerp(wall.Low0, wall.Low1, s1), rise = Vector3.up * R(.2f, .26f);
                        builder.Paint = StoneColor * R(.55f, .65f);
                        builder.Slab(foot0 + wall.Outward * Mathf.Lerp(out0, out1, s0) + rise, foot1 + wall.Outward * Mathf.Lerp(out0, out1, s1) + rise, -wall.Outward * .3f, .32f);
                    }
                    t += segment;
                }
                builder.Moss(MossColor, 0);
                if (R(0, 1) < .35f)
                    for (int k = random.Next(1, 4); k > 0; k--)
                    {
                        var at = Vector3.Lerp(wall.Low0, wall.Low1, R(.1f, .9f)) + wall.Outward * R(.1f, .3f);
                        Rock(builder, at, R(.06f, .13f));
                    }
                if (R(0, 1) < .16f && wall.Top0.y - wall.Low0.y > 1)
                {
                    var at = Vector3.Lerp(wall.Low0, wall.Low1, R(.2f, .8f)) + wall.Outward * R(.25f, .6f);
                    GlowPlant(plants.At(at), at, R(.8f, 1.2f));
                }
            }
            Emit(stone, "Wall trim", solid, true);
            Emit(plants, "Wall plants", foliage, false);
        }

        // ---------- Beyond the playable area ----------
        void BuildWilds()
        {
            var stone = new Chunks(); var trees = new Chunks(); var undergrowth = new Chunks();
            var flames = new List<Vector3>();
            for (int c = 0; c < 4; c++)
            {
                bool east = (c & 1) != 0, north = (c & 2) != 0;
                float X(float x) => east ? Size - x : x;
                float Z(float z) => north ? Size - z : z;
                // A colonnade along both outer sides of each plateau; the south side keeps clear of the camera.
                float sideOffset = 1.6f, frontOffset = north ? 1.6f : 2.6f, frontHeight = north ? 2.2f : 1.3f;
                for (int k = 0; k < 6; k++)
                {
                    Column(stone, new Vector3(X(-sideOffset), 0, Z(2.5f + k * 3)), 2.2f);
                    Column(stone, new Vector3(X(2.5f + k * 3), 0, Z(-frontOffset)), frontHeight);
                }
                Obelisk(stone, new Vector3(X(-3.2f), 0, Z(-3.2f)));
                flames.Add(Brazier(stone, new Vector3(X(-1.6f), 0, Z(19))));
                flames.Add(Brazier(stone, new Vector3(X(19), 0, Z(-frontOffset))));
                for (int k = 0; k < 9; k++)
                {
                    var at = new Vector3(X(-4.2f), 0, Z(1 + k * 2.1f));
                    if (R(0, 1) < .3f) continue;
                    at.y = Ground(at.x, at.z);
                    var builder = stone.At(at);
                    builder.Moss(MossColor, .6f); builder.Paint = StoneColor * R(.75f, .95f);
                    builder.Place(at, R(-4, 4)); builder.Box(new Vector3(0, .3f, 0), new Vector3(.5f, R(.5f, 1.2f), 1.9f));
                    builder.Moss(MossColor, 0);
                }
            }
            for (float gz = -Margin; gz < Size + Margin; gz += 2) for (float gx = -Margin; gx < Size + Margin; gx += 2)
            {
                float x = gx + R(0, 1.8f), z = gz + R(0, 1.8f), d = Outside(x, z);
                if (d < (z < 0 ? 3.4f : 1.7f) || d > 19 || (Terrace(x, z) > .4f && d < 5.5f)) continue;
                var at = new Vector3(x, Rim(x, z), z);
                float density = Smooth(1.2f, 5, d) * (.5f + .5f * Fbm(x * .1f, z * .1f)), roll = R(0, 1);
                float scale = Mathf.Lerp(.75f, 1.5f, Smooth(2, 12, d)) * R(.8f, 1.2f);
                // South of the map the camera looks over the jungle: only low growth near the edge.
                bool low = z < 0 && -z < 15;
                if (low) scale *= Mathf.Lerp(.55f, 1, Smooth(9, 15, -z));
                if (roll < density * .7f && !(low && -z < 9)) Tree(trees.At(at), at, scale);
                else if (roll < density) Fern(trees.At(at), at, scale);
                else if (roll < density + .08f) Rock(stone.At(at), at, R(.3f, .9f));
            }
            for (float gz = -8; gz < Size + 8; gz += .85f) for (float gx = -8; gx < Size + 8; gx += .85f)
            {
                float x = gx + R(0, .8f), z = gz + R(0, .8f), d = Outside(x, z);
                if (d < .25f || d > 7 || R(0, 1) > .55f) continue;
                var at = new Vector3(x, Rim(x, z), z);
                if (R(0, 1) < .12f && d > 1) GlowPlant(undergrowth.At(at), at, R(.9f, 1.4f));
                else if (R(0, 1) < .3f && (z > 0 || d > 2)) Fern(undergrowth.At(at), at, R(.4f, .7f));
                else Tuft(undergrowth.At(at), at, R(1, 1.6f), MossColor);
            }
            Emit(stone, "Ruins and rocks", solid, true);
            Emit(trees, "Jungle", foliage, true);
            Emit(undergrowth, "Undergrowth", foliage, false);
            var fire = Glow("Brazier fire", new Color(1, .5f, .18f) * 2.2f, true, m => m.SetFloat("_Soft", 1));
            foreach (var at in flames) Flame(fire, at);
        }
        void Column(Chunks chunks, Vector3 at, float height)
        {
            at.y = Ground(at.x, at.z);
            var b = chunks.At(at);
            float roll = R(0, 1);
            b.Moss(MossColor, .55f); b.Paint = StoneColor * R(.82f, 1);
            if (roll < .12f)
            {
                // Fallen: drums lying beside the base.
                b.Place(at, R(0, 360)); b.Box(new Vector3(0, .12f, 0), new Vector3(.62f, .24f, .62f));
                b.Place(at + new Vector3(R(-.3f, .3f), .2f, R(-.3f, .3f)), Quaternion.Euler(0, R(0, 360), 90), Vector3.one);
                b.Prism(8, .21f, .21f, 0, height * R(.5f, .8f), 0, 0, true);
            }
            else
            {
                bool broken = roll < .4f;
                float shaft = broken ? height * R(.3f, .7f) : height;
                b.Place(at, R(-5, 5));
                b.Box(new Vector3(0, .12f, 0), new Vector3(.62f, .24f, .62f));
                b.Prism(8, .22f, .19f, .24f, .24f + shaft, 0, .5f);
                if (!broken) b.Box(new Vector3(0, .33f + shaft, 0), new Vector3(.58f, .18f, .58f));
                else { b.Place(at + new Vector3(R(-.6f, .6f), .08f, R(-.6f, .6f)), Quaternion.Euler(R(0, 25), R(0, 360), R(0, 25)), Vector3.one); b.Box(Vector3.zero, new Vector3(.35f, .2f, .3f)); }
            }
            b.Moss(MossColor, 0);
        }
        void Obelisk(Chunks chunks, Vector3 at)
        {
            at.y = Ground(at.x, at.z);
            var b = chunks.At(at);
            b.Paint = StoneColor * .7f; b.Place(at, 45); b.Box(new Vector3(0, .2f, 0), new Vector3(1.1f, .4f, 1.1f));
            b.Paint = StoneColor * .85f; b.Prism(4, .42f, .26f, .4f, 3.1f, .45f, .5f);
            b.Paint = new Color(.4f, 1, .9f); b.Glow = 1; b.Prism(4, .385f, .37f, 2.1f, 2.22f, 0, .5f); b.Glow = 0;
        }
        Vector3 Brazier(Chunks chunks, Vector3 at)
        {
            at.y = Ground(at.x, at.z);
            var b = chunks.At(at);
            b.Paint = StoneColor * .75f; b.Place(at); b.Box(new Vector3(0, .35f, 0), new Vector3(.32f, .7f, .32f));
            b.Paint = new Color(.3f, .25f, .2f); b.Prism(8, .16f, .3f, .7f, .92f, 0, 0, true);
            b.Paint = new Color(1, .45f, .15f); b.Glow = 1; b.Disc(.26f, 8, .93f); b.Glow = 0;
            return at + Vector3.up * .95f;
        }

        // ---------- Inside the map: non-blocking ground cover only ----------
        void BuildDecor()
        {
            var plants = new Chunks(); var pebbles = new Chunks();
            var centre = new Vector2(Size * .5f, Size * .5f);
            for (float gz = .3f; gz < Size; gz += 1.15f) for (float gx = .3f; gx < Size; gx += 1.15f)
            {
                float x = gx + R(0, .9f), z = gz + R(0, .9f);
                int cx = (int)x, cz = (int)z;
                if (!map.Inside(cx, cz) || map.Ramp(cx, cz) || NearWall(x, z, .4f) || NearDeposit(x, z, 1.2f) || Path(x, z) > .3f) continue;
                float plaza = Vector2.Distance(new Vector2(x, z), centre);
                bool high = map.Height(cx, cz) > 2.5f;
                var at = new Vector3(x, map.Height(cx, cz), z);
                if (plaza > 7.4f && plaza < 9 && R(0, 1) < .25f) { GlowPlant(plants.At(at), at, R(.8f, 1.1f)); continue; }
                if (plaza < 8.2f) continue;
                float density = high ? .08f : Smooth(.35f, .62f, Fbm(x * .12f, z * .12f)) * .9f;
                if (R(0, 1) < density) Tuft(plants.At(at), at, R(.8f, 1.3f), high ? MossColor : new Color(.36f, .44f, .23f));
                else if (!high && R(0, 1) < .12f) Rock(pebbles.At(at), at, R(.04f, .09f));
            }
            Emit(plants, "Ground cover", foliage, false);
            Emit(pebbles, "Pebbles", solid, false);
        }
        bool NearWall(float x, float z, float distance)
        {
            int cx = (int)x, cz = (int)z;
            float h = map.Height(cx, cz, x - cx);
            bool Drop(int nx, int nz) => map.Inside(nx, nz) && Mathf.Abs(map.Height(nx, nz, Mathf.Clamp01(x - nx)) - h) > .5f;
            return (x - cx < distance && Drop(cx - 1, cz)) || (cx + 1 - x < distance && Drop(cx + 1, cz))
                || (z - cz < distance && Drop(cx, cz - 1)) || (cz + 1 - z < distance && Drop(cx, cz + 1));
        }
        bool NearDeposit(float x, float z, float distance) => map.Resources.Exists(r => (r.X + .5f - x) * (r.X + .5f - x) + (r.Z + .5f - z) * (r.Z + .5f - z) < distance * distance);

        // ---------- Prop shapes ----------
        float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
        void Tree(MeshBuilder b, Vector3 at, float scale)
        {
            float height = R(1.5f, 2.4f) * scale;
            b.Team = 0; b.Glow = 0; b.Sway(at.y, height * 1.4f, 1);
            b.Paint = Color.Lerp(new Color(.24f, .2f, .17f), new Color(.34f, .29f, .24f), R(0, 1));
            b.Place(at, R(0, 360)); b.Prism(5, .15f * scale, .08f * scale, -.1f, height);
            var leaf = Leaves[random.Next(Leaves.Length)];
            int blobs = 2 + random.Next(3);
            for (int i = 0; i < blobs; i++)
            {
                float radius = R(.55f, .95f) * scale;
                var center = at + new Vector3(R(-.45f, .45f) * scale, height - .1f + i * .25f * scale + R(0, .2f) * scale, R(-.45f, .45f) * scale);
                b.Paint = leaf * R(.85f, 1.12f);
                b.Place(center, Quaternion.Euler(R(0, 30), R(0, 360), R(0, 30)), new Vector3(radius, radius * R(.6f, .8f), radius));
                b.Blob(random.Next(1000), .22f, 0);
            }
            if (R(0, 1) < .3f)
            {
                b.Paint = R(0, 1) < .5f ? new Color(.4f, 1, .85f) : new Color(.8f, .45f, 1); b.Glow = 1;
                for (int i = 0; i < 3; i++)
                {
                    b.Place(at + new Vector3(R(-.6f, .6f) * scale, height - .45f * scale, R(-.6f, .6f) * scale), Quaternion.identity, Vector3.one * R(.06f, .1f) * scale);
                    b.Blob(i, .1f, 0);
                }
                b.Glow = 0;
            }
        }
        void Fern(MeshBuilder b, Vector3 at, float scale)
        {
            b.Team = 0; b.Glow = 0; b.Sway(at.y, scale, 1.3f); b.Transform = Matrix4x4.identity;
            var color = Color.Lerp(new Color(.2f, .38f, .24f), new Color(.3f, .44f, .22f), R(0, 1));
            int fronds = 6 + random.Next(3);
            for (int i = 0; i < fronds; i++)
            {
                float a = i * Mathf.PI * 2 / fronds + R(-.3f, .3f), length = R(.6f, 1) * scale, lift = R(.35f, .6f);
                Vector3 direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), side = new Vector3(-direction.z, 0, direction.x) * .1f * scale;
                Vector3 start = at + Vector3.up * .04f, middle = at + direction * length * .5f + Vector3.up * length * lift, tip = at + direction * length + Vector3.up * length * lift * .35f;
                b.Paint = color * R(.88f, 1.1f);
                b.Quad(start - side * .3f, middle - side, middle + side, start + side * .3f);
                b.Triangle(middle - side, tip, middle + side);
            }
        }
        void Tuft(MeshBuilder b, Vector3 at, float scale, Color color)
        {
            b.Team = 0; b.Glow = 0; b.Sway(at.y, .35f * scale, 1.2f); b.Transform = Matrix4x4.identity;
            for (int i = 4 + random.Next(3); i > 0; i--)
            {
                float a = R(0, Mathf.PI * 2), height = R(.14f, .3f) * scale;
                Vector3 direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), foot = at + direction * R(0, .07f) * scale;
                Vector3 side = new Vector3(-direction.z, 0, direction.x) * .028f * scale, tip = foot + direction * height * R(.2f, .5f) + Vector3.up * height;
                b.Paint = Color.Lerp(color, new Color(.55f, .58f, .3f), R(0, .45f));
                b.Triangle(foot - side, tip, foot + side);
            }
        }
        void GlowPlant(MeshBuilder b, Vector3 at, float scale)
        {
            b.Team = 0; b.Sway(at.y, .5f * scale, 1);
            var light = R(0, 1) < .55f ? new Color(.35f, 1, .85f) : new Color(.75f, .45f, 1);
            for (int i = 1 + random.Next(3); i > 0; i--)
            {
                float height = R(.18f, .42f) * scale;
                var foot = at + new Vector3(R(-.08f, .08f), 0, R(-.08f, .08f));
                b.Glow = 0; b.Paint = new Color(.2f, .3f, .25f); b.Place(foot, R(0, 360)); b.Prism(4, .018f * scale, .01f * scale, 0, height);
                b.Glow = 1; b.Paint = light; b.Place(foot + Vector3.up * height, Quaternion.identity, Vector3.one * R(.04f, .07f) * scale); b.Blob(i, .1f, 0);
            }
            b.Glow = 0;
        }
        void Rock(MeshBuilder b, Vector3 at, float size)
        {
            b.Team = 0; b.Glow = 0; b.Sway(0, 1, 0); b.Moss(MossColor, R(.3f, .8f));
            b.Paint = Color.Lerp(RockColor * .9f, RockColor * 1.25f, R(0, 1));
            b.Place(at + Vector3.down * size * .25f, Quaternion.Euler(R(0, 20), R(0, 360), R(0, 20)), new Vector3(size * R(.9f, 1.3f), size * R(.55f, .85f), size * R(.9f, 1.2f)));
            b.Blob(random.Next(1000), .28f, size > .25f ? 1 : 0);
            b.Moss(MossColor, 0);
        }
        void Emit(Chunks chunks, string name, Material material, bool shadows)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            foreach (var builder in chunks.Builders.Values) if (!builder.Empty) Model(name, Own(builder.ToMesh(name)), material, group, shadows);
        }

        // ---------- Effects ----------
        ParticleSystem Particles(string name, Transform parent, Vector3 position, Material material, int max)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var system = go.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World; main.loop = true; main.prewarm = true;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return system;
        }
        void Flame(Material material, Vector3 at)
        {
            var system = Particles("Brazier flame", root, at, material, 40);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .8f); main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, .9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.22f, .42f); main.startColor = new Color(1, .7f, .35f, .9f);
            var emission = system.emission; emission.rateOverTime = 18;
            var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8; shape.radius = .12f; shape.rotation = new Vector3(-90, 0, 0);
            var size = system.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .2f));
            var color = system.colorOverLifetime; color.enabled = true; color.color = Fade(new Color(1, .75f, .4f), new Color(1, .3f, .1f));
            system.Play();
        }
        public void Steam(Transform parent, Vector3 position)
        {
            if (steam == null) steam = Glow("Steam", new Color(.75f, 1, .8f, .3f), false, m => { m.SetFloat("_Soft", 1); m.SetFloat("_Fog", 1); });
            var system = Particles("Gas steam", parent, position, steam, 40);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.6f); main.startSpeed = new ParticleSystem.MinMaxCurve(.3f, .55f);
            main.startSize = new ParticleSystem.MinMaxCurve(.45f, .8f); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var emission = system.emission; emission.rateOverTime = 5;
            var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 10; shape.radius = .1f; shape.rotation = new Vector3(-90, 0, 0);
            var size = system.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .8f, 1, 2.4f));
            var color = system.colorOverLifetime; color.enabled = true; color.color = Fade(Color.white, new Color(.8f, 1, .85f));
            var noise = system.noise; noise.enabled = true; noise.strength = .15f; noise.frequency = .6f;
            system.Play();
        }
        static ParticleSystem.MinMaxGradient Fade(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(start, 0), new GradientColorKey(end, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(.6f, .6f), new GradientAlphaKey(0, 1) });
            return gradient;
        }
        // Drifting spores of the living ecosystem over the basin; hidden by the fog of war like the terrain.
        void Spores()
        {
            var material = Glow("Spores", new Color(.45f, 1, .8f) * 1.6f, true, m => { m.SetFloat("_Soft", 1); m.SetFloat("_Fog", 1); });
            var system = Particles("Spores", root, new Vector3(Size * .5f, 1.2f, Size * .5f), material, 260);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7, 11); main.startSpeed = new ParticleSystem.MinMaxCurve(.02f, .12f);
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .11f);
            var emission = system.emission; emission.rateOverTime = 26;
            var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(Size + 6, 1.6f, Size + 6);
            var color = system.colorOverLifetime; color.enabled = true; color.color = Fade(new Color(.5f, 1, .8f), new Color(.8f, 1, .5f));
            var noise = system.noise; noise.enabled = true; noise.strength = .3f; noise.frequency = .25f; noise.scrollSpeed = .1f;
            system.Play();
        }

        // ---------- Light and post-processing ----------
        void BuildAtmosphere(Camera camera)
        {
            var sun = new GameObject("Sun", typeof(Light));
            sun.transform.SetParent(root, false); sun.transform.rotation = Quaternion.Euler(46, -26, 0);
            Sun = sun.GetComponent<Light>();
            Sun.type = LightType.Directional; Sun.color = new Color(1, .9f, .76f); Sun.intensity = 2.1f;
            Sun.shadows = LightShadows.Soft; Sun.shadowStrength = .88f;
            Shader.SetGlobalColor("_OrderSky", new Color(.42f, .55f, .66f) * .62f);
            Shader.SetGlobalColor("_OrderGround", new Color(.3f, .26f, .2f) * .55f);
            Shader.SetGlobalColor("_OrderMist", Mist);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Mist;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderShadows = true; cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; cameraData.antialiasingQuality = AntialiasingQuality.High;
            var profile = Own(ScriptableObject.CreateInstance<VolumeProfile>());
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(); bloom.threshold.Override(1); bloom.intensity.Override(.6f); bloom.scatter.Override(.68f);
            var grading = profile.Add<ColorAdjustments>(); grading.postExposure.Override(.35f); grading.contrast.Override(8); grading.saturation.Override(-4);
            var tones = profile.Add<ShadowsMidtonesHighlights>(); tones.shadows.Override(new Vector4(.94f, 1, 1.08f, 0)); tones.highlights.Override(new Vector4(1.05f, 1, .94f, 0));
            var vignette = profile.Add<Vignette>(); vignette.intensity.Override(.26f); vignette.smoothness.Override(.5f);
            var volume = new GameObject("Post-processing", typeof(Volume)).GetComponent<Volume>();
            volume.transform.SetParent(root, false);
            volume.isGlobal = true; volume.priority = 50; volume.sharedProfile = profile;
            Spores();
        }

        // ---------- Fog of war ----------
        // Cell visibility upsampled and blurred; Tick eases the shown texture toward it.
        public void SetVision(bool[,] visible, bool[,] explored)
        {
            bool changed = !fogReady;
            for (int z = 0; z < Size; z++) for (int x = 0; x < Size; x++)
            {
                int cell = z * Size + x;
                if (lastVisible[cell] != visible[x, z] || lastExplored[cell] != explored[x, z]) { changed = true; lastVisible[cell] = visible[x, z]; lastExplored[cell] = explored[x, z]; }
            }
            if (!changed) return;
            int n = Size * FogTexels;
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
            {
                int cell = j / FogTexels * Size + i / FogTexels;
                targetVisible[j * n + i] = lastVisible[cell] ? 1 : 0;
                targetExplored[j * n + i] = lastExplored[cell] ? 1 : 0;
            }
            Blur(targetVisible, n); Blur(targetExplored, n);
            settled = false;
            if (fogReady) return;
            fogReady = true;
            Array.Copy(targetVisible, shownVisible, n * n); Array.Copy(targetExplored, shownExplored, n * n);
            Tick(0);
        }
        // Separable [1 4 6 4 1] / 16 kernel, clamped at the edges.
        void Blur(float[] values, int n)
        {
            for (int j = 0; j < n; j++)
            {
                int row = j * n;
                for (int i = 0; i < n; i++)
                    blurScratch[row + i] = (values[row + Math.Max(i - 2, 0)] + 4 * values[row + Math.Max(i - 1, 0)] + 6 * values[row + i]
                        + 4 * values[row + Math.Min(i + 1, n - 1)] + values[row + Math.Min(i + 2, n - 1)]) * (1f / 16);
            }
            for (int j = 0; j < n; j++)
            {
                int a = Math.Max(j - 2, 0) * n, b = Math.Max(j - 1, 0) * n, row = j * n, c = Math.Min(j + 1, n - 1) * n, d = Math.Min(j + 2, n - 1) * n;
                for (int i = 0; i < n; i++)
                    values[row + i] = (blurScratch[a + i] + 4 * blurScratch[b + i] + 6 * blurScratch[row + i] + 4 * blurScratch[c + i] + blurScratch[d + i]) * (1f / 16);
            }
        }
        public void Tick(float dt)
        {
            if (!fogReady || settled) return;
            float ease = dt <= 0 ? 1 : 1 - Mathf.Exp(-dt * 7), largest = 0;
            for (int i = 0; i < fogPixels.Length; i++)
            {
                float v = targetVisible[i] - shownVisible[i], e = targetExplored[i] - shownExplored[i];
                shownVisible[i] += v * ease; shownExplored[i] += e * ease;
                largest = Mathf.Max(largest, Mathf.Max(Mathf.Abs(v), Mathf.Abs(e)));
                fogPixels[i] = new Color32((byte)(shownVisible[i] * 255), (byte)(shownExplored[i] * 255), 0, 255);
            }
            // Stop uploading once the shown fog has caught up with the vision.
            settled = largest < .002f;
            fog.SetPixels32(fogPixels); fog.Apply(false);
        }

        // ---------- Minimap ----------
        // Terrain colours at FogTexels texels per cell, before fog; cliff edges are drawn darker.
        public Color[] MinimapBase()
        {
            int n = Size * FogTexels;
            var result = new Color[n * n];
            var centre = new Vector2(Size * .5f, Size * .5f);
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
            {
                float x = (i + .5f) / FogTexels, z = (j + .5f) / FogTexels;
                int cx = (int)x, cz = (int)z;
                Color color;
                if (map.Ramp(cx, cz)) color = new Color(.68f, .6f, .46f);
                else if (map.Height(cx, cz) > 2.5f) color = Color.Lerp(new Color(.5f, .46f, .38f), new Color(.6f, .55f, .44f), Mathf.PerlinNoise(x * .3f, z * .3f));
                else
                {
                    color = Color.Lerp(new Color(.2f, .3f, .22f), new Color(.31f, .39f, .24f), Mathf.PerlinNoise(x * .12f + 20, z * .12f + 20));
                    color = Color.Lerp(color, new Color(.44f, .37f, .27f), Path(x, z) * .8f);
                    if (Vector2.Distance(new Vector2(x, z), centre) < 7) color = new Color(.5f, .47f, .4f);
                }
                int sx = i % FogTexels == 0 ? cx - 1 : cx + 1, sz = j % FogTexels == 0 ? cz - 1 : cz + 1;
                bool Cliff(int nx, int nz) => map.Inside(nx, nz) && !map.Ramp(nx, nz) && !map.Ramp(cx, cz) && Mathf.Abs(map.Height(nx, nz) - map.Height(cx, cz)) > .5f;
                if (Cliff(sx, cz) || Cliff(cx, sz)) color *= .5f;
                color.a = 1;
                result[j * n + i] = color;
            }
            return result;
        }

        public void Dispose()
        {
            foreach (var item in owned) if (item != null) Object.Destroy(item);
            owned.Clear();
        }

        // Smooth-shaded terrain geometry with explicit normals.
        sealed class TerrainBuilder
        {
            readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            readonly List<int> triangles = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                normals.Add(na); normals.Add(nb); normals.Add(nc); normals.Add(nd);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
            }
            public void Wall(Vector3 low0, Vector3 top0, Vector3 top1, Vector3 low1, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(top0 - low0, top1 - low0), outward) < 0) (low0, top0, top1, low1) = (low1, top1, top0, low0);
                Quad(low0, top0, top1, low1, outward, outward, outward, outward);
            }
            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
