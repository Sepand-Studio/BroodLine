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
                Facility(b, id, at);
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

        /// Five readable facility profiles at the same camera distance. The
        /// structures stay inside their plot pads, leaving their UI hotspots
        /// free to describe tier and action without supplying the silhouette.
        static void Facility(FrontierMesh b, string id, Vector3 p)
        {
            var frame = Hex("#354554");
            var brass = Hex("#c99a49");
            var violet = Hex("#8668cf");
            var coral = Hex("#e5867a");
            var pale = Hex("#f4dfb9");
            var green = Hex("#6b9361");
            switch (id)
            {
                case "splicing":
                    // Paired incubation columns and one visible joining arc.
                    b.Box(p + new Vector3(0, .15f, 0), new Vector3(1.25f, .18f, .65f), frame);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var x = side * .37f;
                        b.Cone(p + new Vector3(x, .23f, 0), p + new Vector3(x, .94f, 0), .20f, .15f, pale, 10);
                        b.Cone(p + new Vector3(x, .33f, 0), p + new Vector3(x, .38f, 0), .205f, .205f, coral, 10);
                        b.Sphere(p + new Vector3(x, .73f, 0), new Vector3(.10f, .14f, .10f), violet, 8, 6);
                    }
                    b.Box(p + new Vector3(0, 1.01f, 0), new Vector3(.89f, .12f, .18f), frame);
                    b.Sphere(p + new Vector3(0, 1.02f, 0), new Vector3(.13f, .16f, .14f), coral, 10, 7);
                    break;
                case "hatchery":
                    // Warm low nest, with three separate eggs rather than a
                    // generic tower that would duplicate the Splice Chamber.
                    b.Sphere(p + new Vector3(0, .26f, 0), new Vector3(.68f, .24f, .53f), green, 14, 8);
                    b.Sphere(p + new Vector3(0, .34f, 0), new Vector3(.57f, .17f, .45f), Hex("#b7a16e"), 14, 7);
                    for (int i = -1; i <= 1; i++)
                    {
                        float z = i == 0 ? -.10f : .16f;
                        b.Sphere(p + new Vector3(i * .29f, .52f, z), new Vector3(.19f, .30f, .20f), pale, 10, 8);
                        b.Sphere(p + new Vector3(i * .29f - .04f, .55f, z - .13f), new Vector3(.05f, .08f, .025f), coral, 7, 5);
                    }
                    break;
                case "vault":
                    // Sealed archive: a monolith, door seam and brass lock.
                    b.Box(p + new Vector3(0, .53f, 0), new Vector3(.91f, .89f, .67f), frame);
                    b.Box(p + new Vector3(0, 1.02f, 0), new Vector3(1.10f, .16f, .82f), pale);
                    b.Box(p + new Vector3(0, .55f, -.35f), new Vector3(.58f, .65f, .04f), Hex("#546274"));
                    b.Box(p + new Vector3(0, .55f, -.39f), new Vector3(.045f, .52f, .035f), brass);
                    b.Sphere(p + new Vector3(0, .55f, -.43f), new Vector3(.10f, .10f, .04f), brass, 10, 6);
                    break;
                case "harvest":
                    // A raised extraction head with two grounded collector
                    // arms, distinct from the base's trees and signposts.
                    b.Box(p + new Vector3(0, .18f, 0), new Vector3(.96f, .22f, .72f), frame);
                    b.Cone(p + new Vector3(0, .25f, 0), p + new Vector3(0, 1.13f, 0), .15f, .07f, brass, 9);
                    b.Box(p + new Vector3(0, .91f, 0), new Vector3(1.18f, .12f, .20f), frame);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        b.Cone(p + new Vector3(side * .49f, .17f, 0), p + new Vector3(side * .49f, .85f, 0), .10f, .065f, green, 8);
                        b.Box(p + new Vector3(side * .49f, .18f, -.22f), new Vector3(.25f, .17f, .30f), pale);
                    }
                    b.Sphere(p + new Vector3(0, 1.16f, 0), new Vector3(.15f, .13f, .15f), coral, 10, 7);
                    break;
                case "drive":
                    // Low propulsion drum and four radial vanes.
                    b.Cone(p + new Vector3(0, .12f, 0), p + new Vector3(0, .62f, 0), .55f, .39f, frame, 16);
                    b.Cone(p + new Vector3(0, .63f, 0), p + new Vector3(0, .70f, 0), .39f, .36f, brass, 16);
                    b.Sphere(p + new Vector3(0, .77f, 0), new Vector3(.28f, .24f, .28f), violet, 12, 8);
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * 90f;
                        b.Box(p + Quaternion.Euler(0, angle, 0) * new Vector3(.55f, .37f, 0),
                            new Vector3(.49f, .28f, .12f), pale, angle);
                    }
                    break;
            }
        }
    }
}
