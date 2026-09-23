using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Frontier
{
    /// THE ARK'S HOME BASE - Phase 10 Task 1.4. A broad grassy platform in
    /// three-quarter view with the Ark at its centre and six facility plots
    /// around it (bible §7.2), built from the same primitives and the same
    /// shared surface as the battlefield so the base and the lane are one
    /// world. `Plots` are the world points the Game layer projects into the
    /// home screen's markers; keep them in this file next to the geometry
    /// that draws the pads, so a pad and its marker cannot drift apart.
    public sealed partial class FrontierArt
    {
        public static readonly IReadOnlyList<(string Id, Vector3 At)> Plots = new[]
        {
            ("core",     new Vector3(0f, 0f, 0f)),
            ("splicing", new Vector3(-2.5f, 0f, 1.5f)),
            ("hatchery", new Vector3(2.5f, 0f, 1.5f)),
            ("vault",    new Vector3(-2.7f, 0f, -1.4f)),
            ("harvest",  new Vector3(2.7f, 0f, -1.4f)),
            ("drive",    new Vector3(0f, 0f, -2.9f)),
        };

        /// Budget: the whole base, well under 30k triangles (measured by
        /// HomeStageTests), because it is repainted only on demand.
        public GameObject Base(Transform parent)
        {
            var b = new FrontierMesh();
            var grass = Hex("#6b9361"); var sand = Hex("#d2bd94"); var rock = Hex("#8a9485");
            var pad = Hex("#c7c4aa"); var moss = Hex("#587f52");

            // The platform: a rock plinth under a grass top, wider than the
            // plots by a margin so the edge trees have ground to stand on.
            b.Cone(new Vector3(0, -.75f, 0), new Vector3(0, -.03f, 0), 4.9f, 4.55f, rock, 26);
            b.Cone(new Vector3(0, -.035f, 0), Vector3.zero, 4.5f, 4.45f, grass, 30);

            // A sand ring path linking the plots, drawn as a thin ring of
            // short boxes so it reads as trodden ground rather than a disc.
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2 / 28f;
                var p = new Vector3(Mathf.Cos(a) * 2.6f, .012f, Mathf.Sin(a) * 1.9f);
                b.Box(p, new Vector3(.62f, .02f, .34f), sand, a * Mathf.Rad2Deg);
            }

            foreach (var (id, at) in Plots)
            {
                if (id == "core") continue;
                b.Cone(at + new Vector3(0, -.04f, 0), at + new Vector3(0, .05f, 0), .95f, .86f, pad, 14);
                b.Cone(at + new Vector3(0, .05f, 0), at + new Vector3(0, .07f, 0), .62f, .6f, sand, 14);
                // A marker post with a small enamel panel, the plot's own
                // "sign" until each facility gets its building in Batch 6.
                b.Cone(at + new Vector3(.55f, 0, .45f), at + new Vector3(.55f, .72f, .45f), .05f, .04f, Hex("#806247"), 6);
                b.Box(at + new Vector3(.55f, .62f, .45f), new Vector3(.34f, .22f, .04f), Cream);
            }

            Ark(b, Plots[0].At);

            // Trees and rocks around the rim, deterministic so the picture is
            // the same every launch and every capture.
            var rng = new System.Random(20260924);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2 / 16f + (float)rng.NextDouble() * .25f;
                float r = 3.75f + (float)rng.NextDouble() * .55f;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                float size = .55f + (float)rng.NextDouble() * .5f;
                if (i % 4 == 0)
                {
                    b.Box(p + Vector3.up * size * .3f, new Vector3(size * 1.3f, size * .7f, size * .9f), rock, i * 41f);
                    b.Sphere(p + new Vector3(0, size * .66f, 0), new Vector3(size * .6f, .09f, size * .45f), moss, 10, 5);
                }
                else
                {
                    b.Cone(p, p + Vector3.up * size * 1.5f, .1f, .045f, Hex("#806247"), 7);
                    var leaf = i % 2 == 0 ? Hex("#3e735b") : Hex("#87a65c");
                    b.Sphere(p + Vector3.up * size * 1.65f, new Vector3(size * .66f, size * .7f, size * .7f), leaf, 10, 7);
                    b.Sphere(p + new Vector3(-size * .35f, size * 1.35f, size * .1f), new Vector3(size * .5f, size * .45f, size * .5f), Color.Lerp(leaf, grass, .35f), 10, 6);
                    b.Sphere(p + new Vector3(size * .3f, size * 1.85f, -size * .17f), new Vector3(size * .42f, size * .5f, size * .46f), Color.Lerp(leaf, Cream, .12f), 10, 6);
                }
                Foliage(b, p + new Vector3(-size * .7f, 0, size * .4f), i);
            }
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2 / 10f + .3f;
                Foliage(b, new Vector3(Mathf.Cos(a) * 1.55f, 0, Mathf.Sin(a) * 1.1f), i);
            }

            var mesh = b.Finish("frontier-base"); _environmentMeshes.Add(mesh);
            return Draw(parent, mesh.name, mesh);
        }
    }
}
