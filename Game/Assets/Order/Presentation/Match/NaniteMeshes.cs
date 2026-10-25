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
        public static Mesh Puddle()
        {
            var b = new MeshBuilder { Paint = Color.white };
            b.Prism(28, 1, .8f, 0, .1f);
            return Subdivided(b.ToMesh("Nanite puddle"), .15f);
        }
    }
}
