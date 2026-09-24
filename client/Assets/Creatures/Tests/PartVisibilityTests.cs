using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Creatures.Tests
{
    /// Visibility gates for the production card fallback rendered by
    /// FrontierBaker. Phase 9 measured the retired CreatureBaker's twelve
    /// PartRecipes over 144 layers. Frontier owns five presentation traits,
    /// so keeping that old roster mixed 60 current layers with 84 stale ones
    /// and made both its counts and its findings meaningless.
    ///
    /// These tests read the stored PNG bytes directly. They therefore cover
    /// the exact layers CreatureCard shows before its live portrait is ready,
    /// without an importer or colour-space conversion changing the sample.
    public sealed class FrontierCardVisibilityTests
    {
        const double MinDeltaE = 8.0;
        const int MinOverlapPixels = 10;
        const int MinPartPixels = 60;
        const int SafeInsetPixels = 2;

        static readonly string[] Species =
            { "vetch", "ember", "pale", "skitter", "hollow", "loam" };

        static readonly string[] Traits =
            { "cinder", "carapace", "chill", "taunt", "splash" };

        static string ArtRoot => Path.GetFullPath(Path.Combine(
            Application.dataPath, "UI/Resources/Art/creatures"));

        struct Reading
        {
            public double DeltaE;
            public int Overlap;
            public int Pixels;
            public bool TouchesEdge;
        }

        static Color32[] Load(string path, out int width, out int height)
        {
            Assert.IsTrue(File.Exists(path), "no baked sprite at " + path +
                          " - run implementation/scripts/bake-frontier.sh");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)), "could not decode " + path);
                width = texture.width;
                height = texture.height;
                return texture.GetPixels32();
            }
            finally { Object.DestroyImmediate(texture); }
        }

        static Dictionary<string, Reading> ReadAll()
        {
            var readings = new Dictionary<string, Reading>();
            foreach (var species in Species)
            {
                var body = Load(Path.Combine(ArtRoot, "bodies", species + ".png"),
                    out int width, out int height);
                foreach (var socket in new[] { "sk_dorsal", "sk_flank" })
                foreach (var trait in Traits)
                {
                    var part = Load(Path.Combine(ArtRoot, "parts",
                        species + "-" + socket + "-" + trait + ".png"),
                        out int partWidth, out int partHeight);
                    Assert.AreEqual(width, partWidth, "body and part widths must match");
                    Assert.AreEqual(height, partHeight, "body and part heights must match");

                    double pr = 0, pg = 0, pb = 0, br = 0, bg = 0, bb = 0;
                    int overlap = 0, pixels = 0;
                    bool touchesEdge = false;
                    for (int i = 0; i < part.Length; i++)
                    {
                        if (part[i].a <= 127) continue;
                        pixels++;
                        int x = i % width;
                        int y = i / width;
                        if (x < SafeInsetPixels || y < SafeInsetPixels ||
                            x >= width - SafeInsetPixels || y >= height - SafeInsetPixels)
                            touchesEdge = true;
                        if (body[i].a <= 127) continue;
                        overlap++;
                        pr += part[i].r; pg += part[i].g; pb += part[i].b;
                        br += body[i].r; bg += body[i].g; bb += body[i].b;
                    }

                    var reading = new Reading
                        { Overlap = overlap, Pixels = pixels, TouchesEdge = touchesEdge };
                    if (overlap > 0)
                    {
                        var p = new Vector3((float)(pr / overlap), (float)(pg / overlap),
                            (float)(pb / overlap)) / 255f;
                        var b = new Vector3((float)(br / overlap), (float)(bg / overlap),
                            (float)(bb / overlap)) / 255f;
                        reading.DeltaE = DeltaE(p, b);
                    }
                    readings[species + " " + trait + "@" + socket] = reading;
                }
            }
            return readings;
        }

        [Test]
        public void EveryBakedFrontierPartSeparatesFromTheBodyWhereItOverlaps()
        {
            var readings = ReadAll();
            Assert.AreEqual(60, readings.Count,
                "six companions, five traits and two sockets must produce 60 current layers");

            var failures = readings
                .Where(pair => pair.Value.Overlap >= MinOverlapPixels &&
                               pair.Value.DeltaE < MinDeltaE)
                .Select(pair => "  " + pair.Key + ": deltaE " +
                                pair.Value.DeltaE.ToString("F2") + " (" +
                                pair.Value.Overlap + "px overlap)")
                .ToList();

            Assert.IsEmpty(failures,
                "these Frontier parts merge into the body colour where they overlap:\n" +
                string.Join("\n", failures));
        }

        [Test]
        public void EveryBakedFrontierPartHasReadableCoverageInsideTheFrame()
        {
            var readings = ReadAll();
            Assert.AreEqual(60, readings.Count,
                "six companions, five traits and two sockets must produce 60 current layers");

            var failures = readings
                .Where(pair => pair.Value.Pixels < MinPartPixels || pair.Value.TouchesEdge)
                .Select(pair => "  " + pair.Key + ": " + pair.Value.Pixels + "px" +
                                (pair.Value.TouchesEdge ? ", touches frame edge" : string.Empty))
                .ToList();

            Assert.IsEmpty(failures,
                "these Frontier parts are too small to read or are cropped by the card frame:\n" +
                string.Join("\n", failures));
        }

        static double DeltaE(Vector3 a, Vector3 b) => (Lab(a) - Lab(b)).magnitude;

        static Vector3 Lab(Vector3 srgb)
        {
            float lr = ToLinear(srgb.x), lg = ToLinear(srgb.y), lb = ToLinear(srgb.z);
            float x = (0.4124f * lr + 0.3576f * lg + 0.1805f * lb) / 0.95047f;
            float y = 0.2126f * lr + 0.7152f * lg + 0.0722f * lb;
            float z = (0.0193f * lr + 0.1192f * lg + 0.9505f * lb) / 1.08883f;
            float fx = Pivot(x), fy = Pivot(y), fz = Pivot(z);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        static float Pivot(float value) =>
            value > 0.008856f ? Mathf.Pow(value, 1f / 3f) : 7.787f * value + 16f / 116f;

        static float ToLinear(float value) =>
            value <= 0.04045f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
    }
}
