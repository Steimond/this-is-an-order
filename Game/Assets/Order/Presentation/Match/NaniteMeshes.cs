using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Order.Presentation
{
    // Meshes for the machines' nanite liquid. The liquid bends its own shape in the vertex shader (Order/NaniteLiquid),
    // so a low-poly model is split into small triangles first; and the puddle a worker melts into or gathers out of.
    public static class NaniteMeshes
    {
        // A copy of the mesh with every triangle split in four until no edge is longer than maxEdge. The liquid needs only
        // positions and normals; the triangles stay separate, as in the flat-shaded original.
        public static Mesh Subdivided(Mesh source, float maxEdge)
        {
            var vertices = source.vertices; var normals = source.normals; var triangles = source.triangles;
            var outVertices = new List<Vector3>(); var outNormals = new List<Vector3>();
            float limit = maxEdge * maxEdge;
            void Split(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, int depth)
            {
                float longest = Mathf.Max((a - b).sqrMagnitude, Mathf.Max((b - c).sqrMagnitude, (c - a).sqrMagnitude));
                if (longest <= limit || depth >= 6)
                {
                    outVertices.Add(a); outVertices.Add(b); outVertices.Add(c);
                    outNormals.Add(na); outNormals.Add(nb); outNormals.Add(nc);
                    return;
                }
                Vector3 ab = (a + b) * .5f, bc = (b + c) * .5f, ca = (c + a) * .5f;
                Vector3 nab = (na + nb).normalized, nbc = (nb + nc).normalized, nca = (nc + na).normalized;
                Split(a, ab, ca, na, nab, nca, depth + 1); Split(ab, b, bc, nab, nb, nbc, depth + 1);
                Split(ca, bc, c, nca, nbc, nc, depth + 1); Split(ab, bc, ca, nab, nbc, nca, depth + 1);
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int x = triangles[i], y = triangles[i + 1], z = triangles[i + 2];
                Split(vertices[x], vertices[y], vertices[z], normals[x], normals[y], normals[z], 0);
            }
            var mesh = new Mesh { name = source.name + " · nanite liquid", indexFormat = outVertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(outVertices); mesh.SetNormals(outNormals);
            var indices = new int[outVertices.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetTriangles(indices, 0);
            // The shape swells a little beyond the mesh it was made from.
            mesh.RecalculateBounds(); var bounds = mesh.bounds; bounds.Expand(.4f); mesh.bounds = bounds;
            return mesh;
        }

        // A puddle of unit radius: a low disc with a sloping rim, split finely so it can ripple.
        public static Mesh Puddle() => Subdivided(PuddleShape(), .15f);
        // The magnetic spikes standing in that puddle.
        public static Mesh PuddleSpikes() => Spikes(PuddleShape(), .065f, .055f, .14f, .5f);
        static Mesh PuddleShape()
        {
            var b = new MeshBuilder { Paint = Color.white };
            b.Prism(28, 1, .8f, 0, .1f);
            return b.ToMesh("Nanite puddle");
        }

        // The magnetic spikes of the liquid over a model's shape (Order/NaniteLiquid with NANITE_SPIKES): feet spread over
        // its faces at least `spacing` apart, each carrying a cusped cone that the shader raises and lowers. Every vertex
        // keeps its spike's foot (position), the face's normal there and a tangent across it, its place on the cone (uv0:
        // the direction round it as cos and sin, and the share of the radius, 1 at the foot and 0 at the tip) and the
        // spike's radius, tallest height and seed (uv1). Faces turned down further than minUp carry none.
        public static Mesh Spikes(Mesh source, float spacing, float radius, float height, float minUp = -.5f)
        {
            var vertices = source.vertices; var normals = source.normals; var triangles = source.triangles;
            var random = new System.Random(triangles.Length * 7919 + source.vertexCount);
            var feet = new List<Vector3>(); var faces = new List<Vector3>();
            var grid = new Dictionary<Vector3Int, List<int>>();
            Vector3Int Cell(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x / spacing), Mathf.FloorToInt(p.y / spacing), Mathf.FloorToInt(p.z / spacing));
            bool Free(Vector3 p)
            {
                var c = Cell(p);
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                    if (grid.TryGetValue(c + new Vector3Int(x, y, z), out var near))
                        foreach (var i in near) if ((feet[i] - p).sqrMagnitude < spacing * spacing) return false;
                return true;
            }
            // Whether another part of the shape stands in a spike's way: a model is built of parts set on one another, so
            // some faces are hidden under or inside the next part, and a spike there would pierce it from within.
            bool Blocked(Vector3 foot, Vector3 n, int self)
            {
                var from = foot + n * .002f; float reach = height + .02f;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    if (t == self) continue;
                    Vector3 a = vertices[triangles[t]], e1 = vertices[triangles[t + 1]] - a, e2 = vertices[triangles[t + 2]] - a;
                    var pv = Vector3.Cross(n, e2); float det = Vector3.Dot(e1, pv);
                    if (Mathf.Abs(det) < 1e-9f) continue;
                    var tv = from - a; float u = Vector3.Dot(tv, pv) / det;
                    if (u < 0 || u > 1) continue;
                    var qv = Vector3.Cross(tv, e1); float v = Vector3.Dot(n, qv) / det;
                    if (v < 0 || u + v > 1) continue;
                    float along = Vector3.Dot(e2, qv) / det;
                    if (along > 0 && along < reach) return true;
                }
                return false;
            }
            // Darts thrown at every face, kept where no foot is near yet and nothing stands in the way: an even,
            // unordered spread over the shape's outside.
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                var n = (normals[triangles[t]] + normals[triangles[t + 1]] + normals[triangles[t + 2]]).normalized;
                if (n.y < minUp) continue;
                int tries = Mathf.CeilToInt(Vector3.Cross(b - a, c - a).magnitude * .5f / (spacing * spacing) * 6);
                for (int k = 0; k < tries; k++)
                {
                    float u = (float)random.NextDouble(), v = (float)random.NextDouble();
                    if (u + v > 1) { u = 1 - u; v = 1 - v; }
                    var p = a + (b - a) * u + (c - a) * v;
                    if (!Free(p) || Blocked(p, n, t)) continue;
                    var cell = Cell(p);
                    if (!grid.TryGetValue(cell, out var list)) grid[cell] = list = new List<int>();
                    list.Add(feet.Count); feet.Add(p); faces.Add(n);
                }
            }
            // Rings from the foot to the tip, spaced so they climb the cone's height (1 - x)^1.5 in even steps.
            float[] shares = { 1, .7f, .45f, .25f, .1f, 0 };
            const int Round = 6;
            var positions = new List<Vector3>(); var outward = new List<Vector3>(); var across = new List<Vector4>();
            var place = new List<Vector4>(); var spike = new List<Vector4>(); var indices = new List<int>();
            for (int s = 0; s < feet.Count; s++)
            {
                var n = faces[s]; var tangent = Vector3.Cross(n, Mathf.Abs(n.y) < .9f ? Vector3.up : Vector3.right).normalized;
                float turn = (float)random.NextDouble() * Mathf.PI * 2, seed = (float)random.NextDouble();
                int first = positions.Count;
                foreach (var share in shares)
                    for (int k = 0; k < Round; k++)
                    {
                        float angle = turn + k * Mathf.PI * 2 / Round;
                        positions.Add(feet[s]); outward.Add(n); across.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1));
                        place.Add(new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), share, 0)); spike.Add(new Vector4(radius, height, seed, 0));
                    }
                for (int r = 0; r < shares.Length - 1; r++)
                    for (int k = 0; k < Round; k++)
                    {
                        int a = first + r * Round + k, b = first + r * Round + (k + 1) % Round, c = a + Round, d = b + Round;
                        indices.Add(a); indices.Add(b); indices.Add(c);
                        indices.Add(b); indices.Add(d); indices.Add(c);
                    }
            }
            var mesh = new Mesh { name = source.name + " · nanite spikes", indexFormat = positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions); mesh.SetNormals(outward); mesh.SetTangents(across);
            mesh.SetUVs(0, place); mesh.SetUVs(1, spike); mesh.SetTriangles(indices, 0);
            // Every vertex sits at its foot; the spikes and the wobble stand out of that.
            mesh.RecalculateBounds(); var bounds = mesh.bounds; bounds.Expand(2 * (height + radius) + .4f); mesh.bounds = bounds;
            return mesh;
        }
    }
}
