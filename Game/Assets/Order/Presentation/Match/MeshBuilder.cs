using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Order.Presentation
{
    // Flat-shaded low-poly geometry for generated scenery and models (shader Order/Prop).
    // Paint is given in sRGB and stored linear; uv0 = (wind sway weight, team colour mask, emission, 0).
    public sealed class MeshBuilder
    {
        readonly List<Vector3> positions = new List<Vector3>(), normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Vector4> data = new List<Vector4>();
        readonly List<int> triangles = new List<int>();
        Color paint = Color.white, moss;
        float mossAmount, swayBase, swayHeight = 1, sway;
        public Matrix4x4 Transform = Matrix4x4.identity;
        public float Team, Glow;
        public bool Empty => positions.Count == 0;
        public Color Paint { set => paint = Linear(value); }
        public static Color Linear(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
        // Faces looking up blend toward the moss colour.
        public void Moss(Color color, float amount) { moss = Linear(color); mossAmount = amount; }
        // Wind weight grows with world height above the base, so stems stay rooted.
        public void Sway(float baseY, float height, float amount) { swayBase = baseY; swayHeight = Mathf.Max(.01f, height); sway = amount; }
        public void Place(Vector3 position, Quaternion rotation, Vector3 scale) => Transform = Matrix4x4.TRS(position, rotation, scale);
        public void Place(Vector3 position, float yaw = 0, float scale = 1) => Transform = Matrix4x4.TRS(position, Quaternion.Euler(0, yaw, 0), Vector3.one * scale);

        public void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            a = Transform.MultiplyPoint3x4(a); b = Transform.MultiplyPoint3x4(b); c = Transform.MultiplyPoint3x4(c);
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f) return;
            normal.Normalize();
            var color = mossAmount > 0 ? Color.Lerp(paint, moss, mossAmount * Mathf.SmoothStep(0, 1, (normal.y - .45f) / .45f)) : paint;
            Vertex(a, normal, color); Vertex(b, normal, color); Vertex(c, normal, color);
        }
        void Vertex(Vector3 p, Vector3 normal, Color color)
        {
            triangles.Add(positions.Count);
            positions.Add(p); normals.Add(normal); colors.Add(color);
            data.Add(new Vector4(sway * Mathf.Clamp01((p.y - swayBase) / swayHeight), Team, Glow, 0));
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Triangle(a, b, c); Triangle(a, c, d); }

        public void Box(Vector3 center, Vector3 size)
        {
            var e = size * .5f;
            Vector3 P(float x, float y, float z) => center + new Vector3(x * e.x, y * e.y, z * e.z);
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1));
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1));
            Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1));
            Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1));
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));
        }
        // Box with independent corner heights along one axis: a block lying on a slope (wall coping on ramps).
        public void Slab(Vector3 a0, Vector3 a1, Vector3 across, float thickness)
        {
            if (Vector3.Cross(across, a1 - a0).y < 0) (a0, a1) = (a1, a0);
            Vector3 b0 = a0 + across, b1 = a1 + across, down = Vector3.down * thickness;
            Quad(a0, b0, b1, a1);
            Quad(a0 + down, a1 + down, b1 + down, b0 + down);
            Quad(a0 + down, a0, a1, a1 + down);
            Quad(b1 + down, b1, b0, b0 + down);
            Quad(b0 + down, b0, a0, a0 + down);
            Quad(a1 + down, a1, b1, b1 + down);
        }
        // Vertical prism (or frustum) around the local Y axis; tip > 0 ends in a point instead of a flat cap.
        public void Prism(int sides, float bottomRadius, float topRadius, float bottom, float top, float tip = 0, float turn = 0, bool closeBottom = false)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i + turn) / sides * Mathf.PI * 2, a1 = (i + 1 + turn) / sides * Mathf.PI * 2;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = d0 * bottomRadius + Vector3.up * bottom, b1 = d1 * bottomRadius + Vector3.up * bottom;
                Vector3 t0 = d0 * topRadius + Vector3.up * top, t1 = d1 * topRadius + Vector3.up * top;
                Quad(b0, t0, t1, b1);
                if (tip > 0) Triangle(t0, Vector3.up * (top + tip), t1);
                else if (topRadius > 0) Triangle(t0, Vector3.up * top, t1);
                if (closeBottom) Triangle(b1, Vector3.up * bottom, b0);
            }
        }
        public void Disc(float radius, int sides, float y, bool down = false)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = (float)i / sides * Mathf.PI * 2, a1 = (float)(i + 1) / sides * Mathf.PI * 2;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, y, Mathf.Sin(a0) * radius), p1 = new Vector3(Mathf.Cos(a1) * radius, y, Mathf.Sin(a1) * radius);
                if (down) Triangle(p1, new Vector3(0, y, 0), p0); else Triangle(p0, new Vector3(0, y, 0), p1);
            }
        }
        public void Ring(float inner, float outer, int sides, float y)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = (float)i / sides * Mathf.PI * 2, a1 = (float)(i + 1) / sides * Mathf.PI * 2;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)), up = Vector3.up * y;
                Quad(d0 * inner + up, d1 * inner + up, d1 * outer + up, d0 * outer + up);
            }
        }
        // Irregular blob (rock, canopy): an icosphere with every corner pushed in or out.
        public void Blob(int seed, float roughness, int detail = 1)
        {
            var (corners, faces) = Icosphere.Get(detail);
            var shaped = new Vector3[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                var c = corners[i];
                float n = Mathf.PerlinNoise(c.x * 1.7f + seed * .37f + 50, c.z * 1.7f + c.y * 1.3f + seed * .11f + 50);
                shaped[i] = c * (1 + (n - .5f) * 2 * roughness);
            }
            for (int i = 0; i < faces.Length; i += 3) Triangle(shaped[faces[i]], shaped[faces[i + 1]], shaped[faces[i + 2]]);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0, data);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        static class Icosphere
        {
            static readonly (Vector3[], int[])[] cache = new (Vector3[], int[])[3];
            public static (Vector3[] corners, int[] faces) Get(int detail)
            {
                detail = Mathf.Clamp(detail, 0, 2);
                if (cache[detail].Item1 != null) return cache[detail];
                float t = (1 + Mathf.Sqrt(5)) / 2;
                var corners = new List<Vector3>
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
                };
                for (int i = 0; i < corners.Count; i++) corners[i] = corners[i].normalized;
                var faces = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                    3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
                for (int level = 0; level < detail; level++)
                {
                    var middle = new Dictionary<long, int>();
                    int Mid(int a, int b)
                    {
                        long key = a < b ? (long)a << 32 | (uint)b : (long)b << 32 | (uint)a;
                        if (middle.TryGetValue(key, out var index)) return index;
                        corners.Add(((corners[a] + corners[b]) * .5f).normalized);
                        return middle[key] = corners.Count - 1;
                    }
                    var next = new List<int>();
                    for (int i = 0; i < faces.Count; i += 3)
                    {
                        int a = faces[i], b = faces[i + 1], c = faces[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                        next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                    }
                    faces = next;
                }
                return cache[detail] = (corners.ToArray(), faces.ToArray());
            }
        }
    }
}
