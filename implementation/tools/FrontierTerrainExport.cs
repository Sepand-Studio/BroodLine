using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Broodline.Frontier;
using UnityEngine;

// This stub exists only for compiling the real FrontierMesh authoring code
// against the offline Unity math adapter; terrain has no rig or bones.
namespace Broodline.Frontier
{
    public sealed class FrontierRigDefinition
    {
        public FrontierBone[] Bones = new FrontierBone[0];
        public Matrix4x4[] BindPoses() => new Matrix4x4[0];
    }
    public sealed class FrontierBone { public Vector3 Position; }
}

class FrontierTerrainExport
{
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    static void Check(Mesh mesh)
    {
        if (mesh.vertices.Length != mesh.normals.Length || mesh.vertices.Length != mesh.colors.Length ||
            mesh.vertices.Length != mesh.uv.Length || mesh.triangles.Length / 3 > 30000)
            throw new Exception("Terrain channel or 30k detail budget failure");
        for (int i = 0; i < mesh.vertices.Length; i++)
        {
            var p = mesh.vertices[i]; var n = mesh.normals[i];
            if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(n.x) || !Finite(n.y) || !Finite(n.z) ||
                Math.Abs(n.magnitude - 1f) > .002f)
                throw new Exception("Invalid terrain vertex " + i);
        }
        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            int a = mesh.triangles[i], b = mesh.triangles[i + 1], c = mesh.triangles[i + 2];
            var face = Vector3.Cross(mesh.vertices[b] - mesh.vertices[a], mesh.vertices[c] - mesh.vertices[a]);
            if (face.sqrMagnitude < 1e-18f || Vector3.Dot(face, mesh.normals[a] + mesh.normals[b] + mesh.normals[c]) <= 0)
                throw new Exception("Inverted terrain triangle " + i / 3);
        }
    }

    static object Data(Mesh mesh) => new
    {
        positions = mesh.vertices.SelectMany(v => new[] { v.x, v.y, v.z }).ToArray(),
        normals = mesh.normals.SelectMany(v => new[] { v.x, v.y, v.z }).ToArray(),
        colors = mesh.colors.SelectMany(c => new[] { c.r, c.g, c.b }).ToArray(),
        indices = mesh.triangles,
    };

    static void Main(string[] args)
    {
        var b = new FrontierMesh();
        bool painted = args.Length > 1 && args[1] == "painted";
        var grass = FrontierTerrainColor("#6b9361");
        var sand = FrontierTerrainColor("#d2bd94");
        if (!painted) b.Box(new Vector3(12, -.25f, 0), new Vector3(32, .45f, 12), grass);
        b.Box(new Vector3(12, -.018f, 0), new Vector3(28, .06f, 1.8f), sand);
        FrontierTerrain.AddDetail(b, 24, new[] { 2, 6, 10, 14, 18 }, false);
        var mesh = b.Finish("frontier-terrain-detail");
        Check(mesh);
        File.WriteAllText(args[0], new JavaScriptSerializer { MaxJsonLength = 50000000 }.Serialize(
            new { triangles = mesh.triangles.Length / 3, vertices = mesh.vertices.Length, geometry = Data(mesh) }));
        Console.WriteLine("PASS: terrain detail, " + mesh.triangles.Length / 3 + " triangles; finite channels and outward winding");
    }

    static Color FrontierTerrainColor(string hex) { ColorUtility.TryParseHtmlString(hex, out var value); return value; }
}
