using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Broodline.Frontier;
using UnityEngine;

/// Offline geometry audit and JSON export. The adapter uses the real C# mesh
/// builders and rig math; Unity lighting and runtime animation remain separate.
class FrontierRaiderExport
{
    static float[] V(Vector3 p) => new[] { p.x, p.y, p.z };
    static bool Finite(Vector3 p) => !float.IsNaN(p.sqrMagnitude) && !float.IsInfinity(p.sqrMagnitude);

    static void Check(Mesh mesh, FrontierRigDefinition rig)
    {
        if (mesh.vertices.Length != mesh.normals.Length || mesh.vertices.Length != mesh.colors.Length ||
            mesh.vertices.Length != mesh.uv.Length || mesh.vertices.Length != mesh.boneWeights.Length)
            throw new Exception("Missing mesh channel: " + mesh.name);
        if (mesh.bindposes.Length != rig.Bones.Length) throw new Exception("Bind pose count: " + mesh.name);
        if (mesh.triangles.Length / 3 > 10000) throw new Exception("Budget: " + mesh.name);
        for (int i = 0; i < mesh.vertices.Length; i++)
        {
            var w = mesh.boneWeights[i];
            if (!Finite(mesh.vertices[i]) || Math.Abs(mesh.normals[i].magnitude - 1f) > .001f ||
                w.boneIndex0 < 0 || w.boneIndex0 >= rig.Bones.Length ||
                w.boneIndex1 < 0 || w.boneIndex1 >= rig.Bones.Length ||
                Math.Abs(w.weight0 + w.weight1 - 1f) > .001f)
                throw new Exception("Invalid vertex or weight: " + mesh.name + " / " + i);
        }
        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            int a = mesh.triangles[i], b = mesh.triangles[i + 1], c = mesh.triangles[i + 2];
            var face = Vector3.Cross(mesh.vertices[b] - mesh.vertices[a], mesh.vertices[c] - mesh.vertices[a]);
            if (face.sqrMagnitude < 1e-18f || Vector3.Dot(face, mesh.normals[a] + mesh.normals[b] + mesh.normals[c]) <= 0)
                throw new Exception("Inverted/degenerate triangle: " + mesh.name + " / " + i / 3);
        }
    }

    static Matrix4x4[] World(FrontierRigDefinition rig, FrontierBonePose[] pose)
    {
        var world = new Matrix4x4[pose.Length];
        for (int i = 0; i < pose.Length; i++)
        {
            var p = pose[i];
            var local = Matrix4x4.TRS(p.Position, p.Rotation, p.Scale);
            world[i] = rig.Bones[i].Parent < 0 ? local : world[rig.Bones[i].Parent] * local;
        }
        return world;
    }

    static void CheckPose(Mesh mesh, FrontierRigDefinition rig, FrontierBonePose[] pose)
    {
        var world = World(rig, pose);
        for (int i = 0; i < mesh.vertices.Length; i++)
        {
            var weight = mesh.boneWeights[i];
            var position = Vector3.zero;
            var first = world[weight.boneIndex0] * mesh.bindposes[weight.boneIndex0];
            position += first.MultiplyPoint3x4(mesh.vertices[i]) * weight.weight0;
            if (weight.weight1 > 0)
            {
                var second = world[weight.boneIndex1] * mesh.bindposes[weight.boneIndex1];
                position += second.MultiplyPoint3x4(mesh.vertices[i]) * weight.weight1;
            }
            if (!Finite(position)) throw new Exception("Invalid pose: " + mesh.name + " / " + i);
        }
    }

    static object Data(Mesh mesh) => new
    {
        positions = mesh.vertices.SelectMany(V).ToArray(),
        normals = mesh.normals.SelectMany(V).ToArray(),
        colors = mesh.colors.SelectMany(c => new[] { c.r, c.g, c.b }).ToArray(),
        indices = mesh.triangles,
    };

    static void Main(string[] args)
    {
        var rows = new List<object>();
        foreach (var id in FrontierRaiders.Ids)
        {
            var rig = FrontierRigDefinition.For(id);
            var builder = new FrontierMesh();
            FrontierRaiders.Build(builder, id);
            var mesh = builder.FinishRig(id, rig);
            Check(mesh, rig);
            var rest = new FrontierBonePose[rig.Bones.Length];
            FrontierPose.Sample(rig, new FrontierMotionState(), 0, 0, rest, true);
            CheckPose(mesh, rig, rest);
            foreach (var moving in new[] { false, true })
                foreach (float time in new[] { 0f, .14f, .4f, 1f })
                {
                    var pose = new FrontierBonePose[rig.Bones.Length];
                    FrontierPose.Sample(rig, new FrontierMotionState { Moving = moving, AttackUntil = .28f,
                        HitUntil = .16f }, time, 0, pose);
                    CheckPose(mesh, rig, pose);
                }
            rows.Add(new { id, triangles = mesh.triangles.Length / 3, bones = rig.Bones.Length,
                kit = rig.Kit.HasValue, geometry = Data(mesh) });
            Console.WriteLine(id + ": " + mesh.triangles.Length / 3 + " triangles, " + rig.Bones.Length + " bones; channels, winding and poses passed");
        }
        File.WriteAllText(args[0], new JavaScriptSerializer { MaxJsonLength = 50000000 }.Serialize(new { raiders = rows }));
    }
}
