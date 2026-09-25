using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Renders the six assembled Frontier bodies at the 40px review size.
/// This uses the same portrait source as roster cards, not the baked sprites.
public static class FrontierSilhouetteCapture
{
    static readonly string[] Species = { "vetch", "ember", "pale", "skitter", "hollow", "loam" };
    static readonly string[] First = { "carapace", "cinder", "screen", "sprint", "reach", "regrow" };
    static readonly string[] Second = { "taunt", "splash", "chill", "litter", "pierce", "burrow" };

    [MenuItem("Broodline/Capture Frontier Silhouettes")]
    public static void Run()
    {
        var dir = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../implementation/results/silhouettes"));
        Directory.CreateDirectory(dir);
        for (var i = 0; i < Species.Length; i++)
        {
            var id = Species[i];
            // Roster cards request mature growth (1), so this samples the
            // same body scale the player sees in that small slot.
            Write(dir, id, id, null, null);
            Write(dir, id + "-traits", id, First[i], Second[i]);
        }
        Debug.Log("[silhouette] captured " + Species.Length + " Frontier bodies and " +
            Species.Length + " paired-trait assemblies at 40px to " + dir);
    }

    static void Write(string dir, string name, string id, string first, string second)
    {
        var source = ScreenFixtures.CapturePortraits.Request(id, first, second, 1f, null);
        if (source == null) throw new InvalidOperationException("No Frontier portrait for " + name);
        var small = new Texture2D(40, 40, TextureFormat.RGBA32, false);
        try
        {
            for (var y = 0; y < 40; y++)
                for (var x = 0; x < 40; x++)
                    small.SetPixel(x, y, source.GetPixelBilinear((x + .5f) / 40f, (y + .5f) / 40f));
            small.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), small.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(small); }
    }
}
