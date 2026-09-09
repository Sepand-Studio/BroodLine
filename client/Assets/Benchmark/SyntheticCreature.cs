using UnityEngine;

namespace Broodline.Benchmark
{
    public struct SyntheticCreatureSpec
    {
        public int Triangles;
        public int Bones;
        public int Materials;
    }

    /// Builds a skinned mesh with an exact triangle, bone and material cost.
    /// Geometry is deliberately meaningless — only its cost matters.
    public static class SyntheticCreature
    {
        public static GameObject Build(SyntheticCreatureSpec spec)
        {
            var root = new GameObject("SyntheticCreature");
            var smr = root.AddComponent<SkinnedMeshRenderer>();

            var bones = new Transform[spec.Bones];
            var bindPoses = new Matrix4x4[spec.Bones];
            for (int i = 0; i < spec.Bones; i++)
            {
                var b = new GameObject("b" + i).transform;
                b.SetParent(i == 0 ? root.transform : bones[i - 1], false);
                b.localPosition = new Vector3(0f, i == 0 ? 0f : 0.1f, 0f);
                bones[i] = b;
                bindPoses[i] = b.worldToLocalMatrix * root.transform.localToWorldMatrix;
            }

            // Three vertices per triangle: no sharing, so the count is exact.
            int vertCount = spec.Triangles * 3;
            var verts = new Vector3[vertCount];
            var weights = new BoneWeight[vertCount];
            for (int v = 0; v < vertCount; v++)
            {
                int tri = v / 3;
                float t = tri / (float)Mathf.Max(1, spec.Triangles);
                verts[v] = new Vector3(
                    Mathf.Cos(t * Mathf.PI * 8f) * 0.3f + (v % 3) * 0.01f,
                    t * (0.1f * spec.Bones),
                    Mathf.Sin(t * Mathf.PI * 8f) * 0.3f);
                weights[v] = new BoneWeight
                {
                    boneIndex0 = Mathf.Min(spec.Bones - 1, tri * spec.Bones / Mathf.Max(1, spec.Triangles)),
                    weight0 = 1f
                };
            }

            var mesh = new Mesh { name = "SyntheticCreatureMesh" };
            if (vertCount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.boneWeights = weights;
            mesh.bindposes = bindPoses;
            mesh.subMeshCount = spec.Materials;

            // Distribute triangles across submeshes; the remainder goes to the last.
            int perSub = spec.Triangles / spec.Materials;
            int cursor = 0;
            for (int s = 0; s < spec.Materials; s++)
            {
                int count = (s == spec.Materials - 1) ? spec.Triangles - cursor : perSub;
                var idx = new int[count * 3];
                for (int i = 0; i < count * 3; i++) idx[i] = (cursor * 3) + i;
                mesh.SetTriangles(idx, s, false);
                cursor += count;
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mats = new Material[spec.Materials];
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            for (int m = 0; m < spec.Materials; m++) mats[m] = new Material(shader);

            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.sharedMaterials = mats;
            smr.localBounds = mesh.bounds;
            return root;
        }
    }
}
