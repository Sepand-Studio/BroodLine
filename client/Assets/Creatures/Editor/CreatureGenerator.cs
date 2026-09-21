using System.IO;
using System.Linq;
using Broodline.Creatures;
using UnityEditor;
using UnityEngine;

namespace Broodline.Creatures.Editor
{
    /// Recipes to assets. Phase 9 design §3.4. Menu for a person, `Generate`
    /// for batchmode (generate-creatures.sh). Idempotent: existing assets are
    /// overwritten in place so their GUIDs, and every reference to them, hold.
    public static class CreatureGenerator
    {
        const string Root = "Assets/Creatures/Resources/";
        const string ShaderName = "Broodline/Creature";

        [MenuItem("Broodline/Generate Creatures")]
        public static void Generate()
        {
            foreach (var dir in new[] { CreaturePaths.MeshDir, CreaturePaths.MaterialDir, CreaturePaths.BodyDir, CreaturePaths.PartDir, CreaturePaths.RaiderDir })
                Directory.CreateDirectory(Root + dir);
            // Directory.CreateDirectory makes the folder on disk; AssetDatabase
            // does not know about it until it is imported, and CreateAsset into
            // an unimported folder fails. Only matters on the first run, which
            // is exactly the run nobody is watching.
            AssetDatabase.Refresh();

            int n = 0;
            foreach (var r in SpeciesRecipes.All) { Body(r, CreaturePaths.BodyDir, SpeciesColours.For(r.Id)); n++; }
            foreach (var r in RaiderRecipes.All) { Body(r, CreaturePaths.RaiderDir, (SpeciesColours.RaiderBase, SpeciesColours.RaiderUnder)); n++; }
            foreach (var p in PartRecipes.All) { Part(p); n++; }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReportShader();
            Debug.Log("[creatures] generated " + n + " assets under " + Root);
        }

        /// A shader that fails to compile does NOT fail the build - Unity logs
        /// it and renders magenta, and a generator that exited zero says
        /// nothing about that. So ask, and say so either way, every run.
        static void ReportShader()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) { Debug.LogError("[creatures] shader " + ShaderName + " NOT FOUND"); return; }
            var messages = ShaderUtil.GetShaderMessages(shader);
            if (!ShaderUtil.ShaderHasError(shader) && (messages == null || messages.Length == 0))
            {
                Debug.Log("[creatures] shader " + ShaderName + " compiled clean (0 messages)");
                return;
            }
            foreach (var m in messages)
                Debug.LogError("[creatures] shader " + m.severity + ": " + m.message + " " + m.messageDetails + " (line " + m.line + ")");
            if (ShaderUtil.ShaderHasError(shader))
                Debug.LogError("[creatures] shader " + ShaderName + " HAS ERRORS - the creatures will render magenta");
        }

        public static GeneratedMesh Regenerate(BodyRecipe r) =>
            SurfaceNets.Build(r.Primitives, r.Blend, Sdf.BoundsOf(r.Primitives, r.Padding), r.Grid, r.Bones.Select(b => b.Name).ToArray());

        public static GeneratedMesh Regenerate(PartRecipe p) =>
            SurfaceNets.Build(p.Primitives, p.Blend, Sdf.BoundsOf(p.Primitives, p.Padding), p.Grid, new[] { "root" });

        static void Body(BodyRecipe r, string dir, (Color Base, Color Under) colours)
        {
            var g = Regenerate(r);
            var boneNames = r.Bones.Select(b => b.Name).ToArray();

            Debug.Log("[creatures] " + r.Id + ": " + g.Vertices.Length + " verts, " + (g.Triangles.Length / 3) + " tris, grid " + r.Grid);
            var mesh = WriteMesh(Root + CreaturePaths.MeshDir + "/" + r.Id + ".asset", g);
            var material = WriteMaterial(Root + CreaturePaths.MaterialDir + "/" + r.Id + ".mat", colours.Base, colours.Under);

            var go = new GameObject(r.Id);
            try
            {
                // Bones, parented per the recipe, at their rest positions.
                var bones = new Transform[r.Bones.Length];
                for (int i = 0; i < r.Bones.Length; i++)
                {
                    var t = new GameObject(r.Bones[i].Name).transform;
                    t.SetParent(go.transform, false);
                    t.position = r.Bones[i].Position;
                    bones[i] = t;
                }
                for (int i = 0; i < r.Bones.Length; i++)
                    if (r.Bones[i].Parent != null)
                        bones[i].SetParent(bones[System.Array.IndexOf(boneNames, r.Bones[i].Parent)], true);

                var bind = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * go.transform.localToWorldMatrix;
                mesh.boneWeights = g.Weights;
                mesh.bindposes = bind;

                var body = new GameObject("body");
                body.transform.SetParent(go.transform, false);
                var smr = body.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = bones;
                smr.rootBone = bones[0];
                smr.sharedMaterial = material;
                smr.updateWhenOffscreen = false;
                smr.localBounds = mesh.bounds;

                foreach (var s in r.Sockets)
                {
                    var t = new GameObject(s.Name).transform;
                    t.SetParent(go.transform, false);
                    t.localPosition = s.Position;
                    t.localRotation = Quaternion.Euler(s.Euler);
                    t.localScale = Vector3.one * s.Scale;
                }

                // Task 8 adds CreatureMotion here; the component does not exist yet.
                PrefabUtility.SaveAsPrefabAsset(go, Root + dir + "/" + r.Id + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
            EditorUtility.SetDirty(mesh);
        }

        static void Part(PartRecipe p)
        {
            var g = Regenerate(p);
            Debug.Log("[creatures] part-" + p.Id + ": " + g.Vertices.Length + " verts, " + (g.Triangles.Length / 3) + " tris, grid " + p.Grid);
            var mesh = WriteMesh(Root + CreaturePaths.MeshDir + "/part-" + p.Id + ".asset", g);
            var material = WriteMaterial(Root + CreaturePaths.MaterialDir + "/part-" + p.Id + ".mat", p.Base, p.Under);
            var go = new GameObject(p.Id);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(go, Root + CreaturePaths.PartDir + "/" + p.Id + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static Mesh WriteMesh(string path, GeneratedMesh g)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.name = Path.GetFileNameWithoutExtension(path);
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = g.Vertices;
            mesh.normals = g.Normals;
            mesh.triangles = g.Triangles;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Material WriteMaterial(string path, Color baseColour, Color under)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new System.InvalidOperationException("no shader " + ShaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetColor("_BaseColor", baseColour);
            mat.SetColor("_UnderColor", under);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
