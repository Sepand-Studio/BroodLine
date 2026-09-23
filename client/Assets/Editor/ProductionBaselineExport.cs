using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Broodline.Creatures;
using UnityEditor;
using UnityEngine;

// No namespace: Assembly-CSharp-Editor.

/// EXPORTS THE PHASE 9 PRODUCTION BODIES AS "BEFORE" BASELINES - Phase 10
/// Batch 2. The browser comparison page shows a frozen source baseline for
/// Vetch, Ember and Pale (they existed in the proof) but Skitter, Hollow and
/// Loam only ever had the SDF-meshed production prefabs under
/// `Creatures/Resources`. This writes each prefab's geometry, flattened to
/// one mesh in the prefab's space, in the page's JSON shape
/// (positions, normals, colors, polish, indices) so their "before" is the
/// real thing rather than an invented one.
///
///   Unity -batchmode -quit -projectPath client -executeMethod ProductionBaselineExport.Export
public static class ProductionBaselineExport
{
    [MenuItem("Broodline/Export Production Creature Baselines")]
    public static void Export()
    {
        var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../implementation/results/frontier/production-baselines"));
        Directory.CreateDirectory(outDir);
        foreach (var species in new[] { "vetch", "ember", "pale", "skitter", "hollow", "loam" })
        {
            var outputPath = Path.Combine(outDir, species + "-baseline.json");
            if (File.Exists(outputPath)) { Debug.Log("[baseline] preserving frozen " + species); continue; }
            var prefab = CreatureLibrary.BodyPrefab(species);
            if (prefab == null) { Debug.LogWarning("[baseline] no production body for " + species); continue; }
            var instance = Object.Instantiate(prefab);
            try
            {
                var json = Flatten(instance);
                File.WriteAllText(outputPath, json);
                Debug.Log("[baseline] wrote " + species);
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }

    static string Flatten(GameObject root)
    {
        var positions = new List<float>(); var normals = new List<float>(); var colors = new List<float>();
        var polish = new List<float>(); var indices = new List<int>();
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            Mesh mesh = null;
            if (r is SkinnedMeshRenderer skinned) { mesh = new Mesh(); skinned.BakeMesh(mesh); }
            else if (r is MeshRenderer) { var f = r.GetComponent<MeshFilter>(); mesh = f == null ? null : f.sharedMesh; }
            if (mesh == null) continue;
            var tint = Color.gray;
            var material = r.sharedMaterial;
            if (material != null)
            {
                if (material.HasProperty("_BaseColor")) tint = material.GetColor("_BaseColor");
                else if (material.HasProperty("_Color")) tint = material.color;
            }
            var toRoot = root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
            var normalMatrix = toRoot.inverse.transpose;
            int offset = positions.Count / 3;
            var v = mesh.vertices; var n = mesh.normals; var c = mesh.colors;
            for (int i = 0; i < v.Length; i++)
            {
                var p = toRoot.MultiplyPoint3x4(v[i]); positions.Add(p.x); positions.Add(p.y); positions.Add(p.z);
                var nn = (n.Length == v.Length ? normalMatrix.MultiplyVector(n[i]) : Vector3.up).normalized;
                normals.Add(nn.x); normals.Add(nn.y); normals.Add(nn.z);
                var col = c.Length == v.Length ? c[i] * tint : tint;
                colors.Add(col.r); colors.Add(col.g); colors.Add(col.b);
                polish.Add(.2f);
            }
            var t = mesh.triangles;
            // Keep winding outward regardless of any mirrored transform.
            bool mirrored = toRoot.determinant < 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                indices.Add(t[i] + offset);
                indices.Add((mirrored ? t[i + 2] : t[i + 1]) + offset);
                indices.Add((mirrored ? t[i + 1] : t[i + 2]) + offset);
            }
            if (r is SkinnedMeshRenderer) Object.DestroyImmediate(mesh);
        }
        var sb = new StringBuilder();
        sb.Append("{\"positions\":"); Numbers(sb, positions);
        sb.Append(",\"normals\":"); Numbers(sb, normals);
        sb.Append(",\"colors\":"); Numbers(sb, colors);
        sb.Append(",\"polish\":"); Numbers(sb, polish);
        sb.Append(",\"indices\":["); for (int i = 0; i < indices.Count; i++) { if (i > 0) sb.Append(','); sb.Append(indices[i]); }
        sb.Append("]}");
        return sb.ToString();
    }

    static void Numbers(StringBuilder sb, List<float> values)
    {
        sb.Append('[');
        for (int i = 0; i < values.Count; i++) { if (i > 0) sb.Append(','); sb.Append(values[i].ToString("0.####", CultureInfo.InvariantCulture)); }
        sb.Append(']');
    }
}
