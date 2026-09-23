using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Creatures
{
    public sealed class GeneratedMesh
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public int[] Triangles;
        public BoneWeight[] Weights;
    }

    /// Naive surface nets over a regular grid: one vertex per sign-changing
    /// cell at the mean of its edge crossings, one quad per sign-changing
    /// grid edge joining the four cells around it. No lookup tables, no
    /// cracks, and a soft, even topology that suits the design's "chunky and
    /// smooth" read. Normals come from the field gradient, not the faces.
    ///
    /// AT MOST TWO INFLUENCES PER VERTEX - QualitySettings' Mobile tier caps
    /// skinning at two, and a vertex authored with four would look different
    /// on the phone than in the Editor.
    ///
    /// WINDING, MEASURED NOT ASSUMED. Each quad's four cells are listed in a
    /// cycle whose Cross(b-a, c-a) - Unity's own face-normal convention -
    /// points along the POSITIVE axis of the grid edge it surrounds. That is
    /// the outward direction exactly when the edge's low corner is inside,
    /// so `flip` is `!inside`. Verified on all four shipped recipes: every
    /// face agrees with the gradient normal, FixWinding never fires, and the
    /// signed volume is positive.
    ///
    /// KNOWN LIMIT. One vertex per cell means a feature THINNER THAN A CELL
    /// can put two surface sheets through one cell and pinch the topology
    /// (the same directed edge used by two quads). It is not visible as a
    /// hole, but it is not a manifold either. Every recipe shipped here was
    /// checked to be free of it; a new recipe with a thin plate should be
    /// checked too, not assumed.
    public static class SurfaceNets
    {
        public static GeneratedMesh Build(Primitive[] prims, float blend, Bounds bounds, int grid, string[] boneNames)
        {
            int n = grid + 1;
            var size = bounds.size; var origin = bounds.min;
            var step = new Vector3(size.x / grid, size.y / grid, size.z / grid);

            // 1. Sample the field at every corner.
            var field = new float[n * n * n];
            int Idx(int x, int y, int z) => (x * n + y) * n + z;
            Vector3 At(int x, int y, int z) => origin + new Vector3(x * step.x, y * step.y, z * step.z);
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) for (int z = 0; z < n; z++)
                field[Idx(x, y, z)] = Sdf.Field(prims, blend, At(x, y, z));

            // 2. One vertex per cell that the surface crosses.
            var cellVertex = new int[grid * grid * grid];
            for (int i = 0; i < cellVertex.Length; i++) cellVertex[i] = -1;
            int Cell(int x, int y, int z) => (x * grid + y) * grid + z;
            var verts = new List<Vector3>();
            int[,] edges =
            {
                {0,0,0, 1,0,0}, {0,1,0, 1,1,0}, {0,0,1, 1,0,1}, {0,1,1, 1,1,1},
                {0,0,0, 0,1,0}, {1,0,0, 1,1,0}, {0,0,1, 0,1,1}, {1,0,1, 1,1,1},
                {0,0,0, 0,0,1}, {1,0,0, 1,0,1}, {0,1,0, 0,1,1}, {1,1,0, 1,1,1},
            };
            for (int x = 0; x < grid; x++) for (int y = 0; y < grid; y++) for (int z = 0; z < grid; z++)
            {
                var sum = Vector3.zero; int count = 0;
                for (int e = 0; e < 12; e++)
                {
                    int ax = x + edges[e, 0], ay = y + edges[e, 1], az = z + edges[e, 2];
                    int bx = x + edges[e, 3], by = y + edges[e, 4], bz = z + edges[e, 5];
                    float fa = field[Idx(ax, ay, az)], fb = field[Idx(bx, by, bz)];
                    if ((fa < 0f) == (fb < 0f)) continue;
                    float t = fa / (fa - fb);
                    sum += Vector3.Lerp(At(ax, ay, az), At(bx, by, bz), t); count++;
                }
                if (count == 0) continue;
                cellVertex[Cell(x, y, z)] = verts.Count;
                verts.Add(sum / count);
            }

            // 3. One quad per sign-changing edge, wound so the face points outward.
            var tris = new List<int>();
            void Quad(int a, int b, int c, int d, bool flip)
            {
                if (a < 0 || b < 0 || c < 0 || d < 0) return;
                if (flip) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
                else      { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
            }
            for (int x = 0; x < grid; x++) for (int y = 0; y < grid; y++) for (int z = 0; z < grid; z++)
            {
                float f0 = field[Idx(x, y, z)];
                bool inside = f0 < 0f;
                // edge along +X from corner (x,y,z): cells (x, y-1..y, z-1..z)
                if (x < grid && y > 0 && z > 0 && (field[Idx(x + 1, y, z)] < 0f) != inside)
                    Quad(cellVertex[Cell(x, y - 1, z - 1)], cellVertex[Cell(x, y, z - 1)],
                         cellVertex[Cell(x, y, z)], cellVertex[Cell(x, y - 1, z)], !inside);
                // edge along +Y: cells (x-1..x, y, z-1..z)
                if (y < grid && x > 0 && z > 0 && (field[Idx(x, y + 1, z)] < 0f) != inside)
                    Quad(cellVertex[Cell(x - 1, y, z - 1)], cellVertex[Cell(x - 1, y, z)],
                         cellVertex[Cell(x, y, z)], cellVertex[Cell(x, y, z - 1)], !inside);
                // edge along +Z: cells (x-1..x, y-1..y, z)
                if (z < grid && x > 0 && y > 0 && (field[Idx(x, y, z + 1)] < 0f) != inside)
                    Quad(cellVertex[Cell(x - 1, y - 1, z)], cellVertex[Cell(x, y - 1, z)],
                         cellVertex[Cell(x, y, z)], cellVertex[Cell(x - 1, y, z)], !inside);
            }

            // 4. Normals from the gradient; weights from the two nearest primitives' bones.
            var normals = new Vector3[verts.Count];
            var weights = new BoneWeight[verts.Count];
            float eps = Mathf.Min(step.x, Mathf.Min(step.y, step.z)) * 0.5f;
            for (int i = 0; i < verts.Count; i++)
            {
                var p = verts[i];
                var g = new Vector3(
                    Sdf.Field(prims, blend, p + Vector3.right * eps) - Sdf.Field(prims, blend, p - Vector3.right * eps),
                    Sdf.Field(prims, blend, p + Vector3.up * eps) - Sdf.Field(prims, blend, p - Vector3.up * eps),
                    Sdf.Field(prims, blend, p + Vector3.forward * eps) - Sdf.Field(prims, blend, p - Vector3.forward * eps));
                normals[i] = g.sqrMagnitude > 1e-12f ? g.normalized : Vector3.up;
                weights[i] = WeightsAt(prims, p, boneNames);
            }

            // Winding check: if the mesh faces inward, the gradient says so.
            FixWinding(verts, normals, tris);

            return new GeneratedMesh { Vertices = verts.ToArray(), Normals = normals, Triangles = tris.ToArray(), Weights = weights };
        }

        static BoneWeight WeightsAt(Primitive[] prims, Vector3 p, string[] boneNames)
        {
            int best = -1, second = -1; float bd = float.MaxValue, sd = float.MaxValue;
            for (int i = 0; i < prims.Length; i++)
            {
                float d = Mathf.Max(0f, Sdf.Eval(prims[i], p)) + 1e-3f;
                int bone = System.Array.IndexOf(boneNames, prims[i].Bone);
                if (bone < 0) bone = 0;
                if (d < bd) { if (bone != best) { second = best; sd = bd; } best = bone; bd = d; }
                else if (d < sd && bone != best) { second = bone; sd = d; }
            }
            if (second < 0) return new BoneWeight { boneIndex0 = best, weight0 = 1f };
            float w0 = 1f / bd, w1 = 1f / sd; float sum = w0 + w1;
            return new BoneWeight { boneIndex0 = best, weight0 = w0 / sum, boneIndex1 = second, weight1 = w1 / sum };
        }

        static void FixWinding(List<Vector3> v, Vector3[] n, List<int> t)
        {
            int agree = 0, disagree = 0;
            for (int i = 0; i < t.Count; i += 3)
            {
                var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) >= 0f) agree++; else disagree++;
            }
            if (disagree <= agree) return;
            for (int i = 0; i < t.Count; i += 3) { var tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
        }
    }
}
