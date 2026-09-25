using System;
using System.IO;
using Broodline.Frontier;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

/// Imports offline-authored mesh data as a GameObject with embedded mesh and
/// material. Copies under Resources supply the live founder and trait visuals.
[ScriptedImporter(1, "frontiermesh")]
public sealed class FrontierMeshImporter : ScriptedImporter
{
    [Serializable]
    public sealed class Source
    {
        public int version;
        public string id, kind;
        public Vector3[] vertices, normals;
        public Color[] colors;
        public float[] polish, blend;
        public int[] bone0, bone1, triangles;
    }

    public override void OnImportAsset(AssetImportContext context)
    {
        var source = JsonUtility.FromJson<Source>(File.ReadAllText(context.assetPath));
        bool body = source != null && source.kind == "body";
        var rig = body ? FrontierRigDefinition.For(source.id) : null;
        Validate(source, rig);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Frontier/Art/FrontierSurface.shader");
        if (shader == null) throw new InvalidDataException("FrontierSurface shader is required.");
        context.DependsOnSourceAsset("Assets/Frontier/Art/FrontierSurface.shader");
        context.DependsOnSourceAsset("Assets/Frontier/Art/FrontierRigDefinition.cs");
        if (body)
            context.DependsOnSourceAsset("Assets/Frontier/Art/Frontier"
                + char.ToUpperInvariant(source.id[0]) + source.id.Substring(1) + ".cs");
        var mesh = new Mesh { name = source.id + " authored mesh" };
        mesh.vertices = source.vertices;
        mesh.normals = source.normals;
        mesh.colors = source.colors;
        var surface = new Vector2[source.vertices.Length];
        for (int i = 0; i < surface.Length; i++) surface[i] = new Vector2(source.polish[i], 0);
        mesh.uv = surface;
        mesh.triangles = source.triangles;
        mesh.RecalculateBounds();
        var material = new Material(shader) { name = source.id + " layered surface" };
        var root = new GameObject(source.id);
        if (body)
        {
            var bones = new Transform[rig.Bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = new GameObject(rig.Bones[i].Name).transform;
                bones[i].SetParent(rig.Bones[i].Parent < 0 ? root.transform : bones[rig.Bones[i].Parent], false);
                bones[i].localPosition = rig.LocalPosition(i);
            }
            var weights = new BoneWeight[source.vertices.Length];
            for (int i = 0; i < weights.Length; i++)
                weights[i] = new BoneWeight { boneIndex0 = source.bone0[i], boneIndex1 = source.bone1[i],
                    weight0 = 1 - source.blend[i], weight1 = source.blend[i] };
            mesh.boneWeights = weights;
            mesh.bindposes = rig.BindPoses();
            var renderer = new GameObject("body").AddComponent<SkinnedMeshRenderer>();
            renderer.transform.SetParent(root.transform, false);
            renderer.sharedMesh = mesh; renderer.sharedMaterial = material;
            renderer.bones = bones; renderer.rootBone = bones[0];
            renderer.localBounds = new Bounds(mesh.bounds.center * 1.2f,
                mesh.bounds.size * 1.8f + rig.MotionAllowance * 2);
        }
        else
        {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        context.AddObjectToAsset("mesh", mesh);
        context.AddObjectToAsset("material", material);
        context.AddObjectToAsset("root", root);
        context.SetMainObject(root);
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

    static void Validate(Source s, FrontierRigDefinition rig)
    {
        if (s == null || s.version != 1 || (s.kind != "body" && s.kind != "part")
            || (s.kind == "part" && !FrontierParts.Has(s.id)))
            throw new InvalidDataException("Unknown Frontier mesh identity or version.");
        int count = s.vertices?.Length ?? 0;
        if (count == 0 || count > 65535 || s.normals?.Length != count || s.colors?.Length != count
            || s.polish?.Length != count || s.blend?.Length != count
            || s.bone0?.Length != count || s.bone1?.Length != count
            || s.triangles == null || s.triangles.Length == 0 || s.triangles.Length % 3 != 0
            || s.triangles.Length > 30000)
            throw new InvalidDataException("Incomplete mesh streams or triangle budget exceeded.");
        for (int i = 0; i < count; i++)
        {
            var c = s.colors[i];
            if (!Finite(s.vertices[i]) || !Finite(s.normals[i]) || s.normals[i].sqrMagnitude < .5f
                || !Finite(c.r) || !Finite(c.g) || !Finite(c.b) || !Finite(c.a)
                || !Finite(s.polish[i]) || s.polish[i] < 0 || s.polish[i] > 1
                || !Finite(s.blend[i]) || s.blend[i] < 0 || s.blend[i] > 1
                || s.bone0[i] < 0 || s.bone1[i] < 0
                || (rig != null && (s.bone0[i] >= rig.Bones.Length || s.bone1[i] >= rig.Bones.Length)))
                throw new InvalidDataException("Invalid vertex or skin weight at " + i);
        }
        foreach (int index in s.triangles)
            if (index < 0 || index >= count) throw new InvalidDataException("Triangle index outside mesh.");
    }
}
