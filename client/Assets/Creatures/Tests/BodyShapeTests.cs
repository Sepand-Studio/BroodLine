using System.Collections.Generic;
using System.Linq;
using Broodline.Creatures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Broodline.Creatures.Tests
{
    /// bible 10.2 rule 1 - "Silhouette carries the role... a player should name
    /// the role before reading the card" - turned into geometry for ONE body.
    ///
    /// `Broodline.UI.Tests.SilhouetteTests` asserts the OTHER half of that rule:
    /// that no two baked species collide at 40px. It cannot say whether any one
    /// of them is the animal bible 1.2 names, because a single body has nothing
    /// to be confused with. This file says it, for Vetch: bible 1.2 asks for a
    /// "low dome, four stubby legs, no neck", and the first pass shipped a
    /// smooth pebble instead - the dome sphere sat on the ground, so there was
    /// no band anywhere between the floor and the body in which only legs
    /// existed, and the 0.22 blend (twice a leg's own radius) filleted whatever
    /// the dome left. Both are now measured rather than described.
    ///
    /// WHY THESE RUN ON THE COMMITTED MESH. The recipe is what an author edits,
    /// but the committed asset is what the card and the lane draw. `DriftTests`
    /// ties the two together, so asserting here against the asset means a shape
    /// claim can never pass on a recipe nobody regenerated.
    public class BodyShapeTests
    {
        const string ResourceRoot = "Assets/Creatures/Resources/";

        /// THE SLICE, AND WHY IT IS HERE.
        ///
        /// Vetch's legs meet the ground at y = 0 and its shell's underside sits
        /// at y = 0.26, so the clearance band is [0, 0.26] and 0.12 is close to
        /// its middle - the point least sensitive to a future author nudging
        /// either end. `TheSliceSitsInVetchsGroundClearance` re-derives both
        /// ends from the recipe and fails with that arithmetic if it stops
        /// being true, so this constant can never quietly start slicing through
        /// the belly (which would read 1 for any animal) or through the clipped
        /// leg tips below the mesh's own floor (which would read 0).
        ///
        /// It is not a knife-edge: the old blob reads ONE component at every
        /// height from 0.06 to 0.20, and the shape this file was written for
        /// reads FOUR at every one of them. The number below is a choice inside
        /// a wide band, not a tuned constant.
        const float SliceHeight = 0.12f;

        /// 0.01 is about an eighth of a leg's diameter and about a sixth of the
        /// mesher's own cell, so the raster is finer than the geometry it
        /// measures. The component count is identical at 0.005 and at 0.02.
        const float Cell = 0.01f;

        /// Four cells - a 0.02 x 0.02 speck. Below this a "component" is a
        /// rasterisation artefact at a grazing triangle, not a limb.
        const int MinComponentCells = 4;

        static Mesh Committed(string id)
        {
            var path = ResourceRoot + CreaturePaths.MeshDir + "/" + id + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Assert.IsNotNull(mesh, "no committed mesh at " + path + " - run generate-creatures.sh");
            return mesh;
        }

        // --------------------------------------------------------------- legs

        /// FOUR LEGS MEANS FOUR LEGS. Slice the solid near the ground and count
        /// what is there: four stubby legs leave four separate islands, a
        /// pebble leaves one. This is the checkable form of bible 1.2's
        /// silhouette column for this body, and it is what stops the next
        /// author - Task 15 writes five more species from these conventions -
        /// reintroducing a blend or a dome that erases the legs again.
        [Test]
        public void Vetch_StandsOnFourLegs_CountedInASliceNearTheGround()
        {
            var parts = Section(Committed("vetch"), SliceHeight);

            Assert.AreEqual(4, parts.Count,
                "bible 1.2 asks Vetch for four stubby legs. A horizontal slice at y = " + SliceHeight +
                " found " + parts.Count + " connected piece(s) of solid, not 4" +
                (parts.Count == 1
                    ? " - one piece means the body's own mass reaches the ground between the legs, or the " +
                      "blend has melted them into it. Raise the body's underside clear of the slice and keep " +
                      "BodyRecipe.Blend at or below a leg's radius."
                    : "") + ". Areas: " + string.Join(", ", parts.Select(p => p.Area.ToString("F4"))) + ".");

            // NOT SPINDLY. bible 1.2 says stubby and bible 10.2 rule 1 gives
            // "spindly" to Skitter, so four legs thin enough to read as spindly
            // would be as wrong as none at all - and would sail through the
            // count above. A leg drawn at 40px needs real width: 0.15 is about
            // an eighth of Vetch's own length. Measured here: 0.216 - 0.221.
            foreach (var p in parts)
                Assert.GreaterOrEqual(p.Diameter, 0.15f,
                    "a leg's cross-section is " + p.Diameter.ToString("F3") + " across at y = " + SliceHeight +
                    " - that reads as spindly, which bible 10.2 rule 1 gives to Skitter, not to a wall");

            // All four are the same leg. A recipe that melted one of them into
            // the dome while leaving three would still count four if a stray
            // island appeared elsewhere; this says the four are a matched set.
            float biggest = parts.Max(p => p.Area), smallest = parts.Min(p => p.Area);
            Assert.LessOrEqual(biggest - smallest, biggest * 0.25f,
                "the four legs are not the same size at y = " + SliceHeight + " (" +
                string.Join(", ", parts.Select(p => p.Area.ToString("F4"))) +
                ") - one of them is half-absorbed into the body");
        }

        /// The slice height above is only meaningful while it lies inside
        /// Vetch's ground clearance, and that is a property of the RECIPE, not
        /// of this file. Derive both ends of the band from the recipe and say
        /// so, so a future author who lowers the shell or shortens the legs
        /// gets this arithmetic rather than a bare "expected 4, was 1".
        [Test]
        public void TheSliceSitsInVetchsGroundClearance()
        {
            var r = SpeciesRecipes.For("vetch");
            const float margin = 0.05f;

            float floor = Committed("vetch").bounds.min.y;
            Assert.LessOrEqual(floor + margin, SliceHeight,
                "the slice at y = " + SliceHeight + " is within " + margin + " of the mesh's own floor (" +
                floor.ToString("F4") + "), where the mesher's cell averaging has already clipped the leg tips");

            // SmoothMin(a, b, k) is never below min(a, b) - k/4, so the smooth
            // union can push a primitive's surface out by at most a quarter of
            // the blend. That makes this a true lower bound on the body's
            // underside without sampling the field.
            float body = r.Primitives
                .Where(p => !p.Bone.StartsWith("leg"))
                .Min(Underside) - r.Blend * 0.25f;
            Assert.GreaterOrEqual(body - margin, SliceHeight,
                "the slice at y = " + SliceHeight + " is within " + margin + " of Vetch's underside (" +
                body.ToString("F4") + ") - it would cut the shell or the head, and then the count " +
                "measures the body's footprint rather than its legs. Either lift the body or lower the slice.");
        }

        static float Underside(Primitive p)
        {
            switch (p.Kind)
            {
                case PrimitiveKind.Sphere: return p.A.y - p.Radius;
                case PrimitiveKind.Capsule: return Mathf.Min(p.A.y, p.B.y) - p.Radius;
                default: return p.A.y - p.Half.y;
            }
        }

        // --------------------------------------------------------------- head

        /// "NO NECK" IS NOT "NO HEAD". bible 1.2 asks for a head that is a lump
        /// on the front, and the first pass buried it: the head sphere sat
        /// inside a body that was just as wide at the front as in the middle,
        /// so nothing in the silhouette said where the animal was looking.
        ///
        /// Two numbers, because either alone is passable by a blob. How far
        /// forward the head carries the surface says there IS a lump; how
        /// narrow the front is says the lump is a HEAD and not simply a longer
        /// body. Measured on the shape this file was written for: 0.293 and
        /// 37%. Measured on the pebble it replaced: 0.186 and 73%.
        [Test]
        public void Vetch_CarriesAHeadLumpOnTheFront_NarrowerThanItsBody()
        {
            var r = SpeciesRecipes.For("vetch");
            var head = r.Primitives.Where(p => p.Bone == "head").ToArray();
            Assert.IsNotEmpty(head, "vetch declares no primitive on the head bone");

            var body = r.Primitives.Where(p => p.Bone != "head").ToArray();
            float headY = head[0].A.y;
            float bodyReach = ForwardReach(body, r.Blend, headY);
            var mesh = Committed("vetch");
            float reach = mesh.bounds.max.x - bodyReach;
            Assert.GreaterOrEqual(reach, 0.20f,
                "with its head primitive removed Vetch's surface reaches x = " + bodyReach.ToString("F3") +
                " at the head's own height, and the committed mesh reaches " + mesh.bounds.max.x.ToString("F3") +
                " - the head only carries the silhouette " + reach.ToString("F3") + " further forward, " +
                "which at a body " + mesh.bounds.size.x.ToString("F2") + " long is not a lump anyone can see");

            // The frontmost sixth of the body, in plan. A head is narrower than
            // the shoulders it sits in front of; a blunt prow is not.
            var v = mesh.vertices;
            float front = mesh.bounds.max.x - 0.15f * mesh.bounds.size.x;
            float snout = Span(v.Where(p => p.x >= front).Select(p => p.z));
            float widest = Span(v.Select(p => p.z));
            Assert.LessOrEqual(snout, widest * 0.55f,
                "Vetch's frontmost 15% is " + snout.ToString("F3") + " wide against a widest point of " +
                widest.ToString("F3") + " (" + (snout / widest).ToString("P0") + ") - the front of the body " +
                "is as broad as its middle, so the head reads as a prow rather than a head");
        }

        static float Span(IEnumerable<float> xs)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var x in xs) { lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
            return hi - lo;
        }

        /// The frontmost x at which the field is inside, down the midline at
        /// height y. Every shape here is convex along that ray, so the last
        /// negative sample is the surface.
        static float ForwardReach(Primitive[] prims, float blend, float y)
        {
            var bounds = Sdf.BoundsOf(prims, 0.1f);
            for (float x = bounds.max.x; x > bounds.min.x; x -= 0.002f)
                if (Sdf.Field(prims, blend, new Vector3(x, y, 0f)) < 0f) return x;
            return bounds.min.x;
        }

        // ----------------------------------------------------- the cross-cut

        struct Island { public float Area; public float Diameter; }

        /// The solid cross-section of a CLOSED mesh at height y, rasterised on
        /// an XZ grid and split into 4-connected islands.
        ///
        /// A grid point is inside the solid when the ray straight up from it
        /// crosses an odd number of triangles - exact for a watertight surface,
        /// which `MesherTests.EveryMesh_IsAClosedManifold` guarantees these are,
        /// and independent of the mesher's own grid. Sampling on a lattice
        /// offset by two irrational fractions of a cell keeps a sample off the
        /// shared edges where it would otherwise be counted twice or not at all.
        static List<Island> Section(Mesh mesh, float y)
        {
            var v = mesh.vertices;
            var t = mesh.triangles;
            var b = mesh.bounds;
            int nx = Mathf.CeilToInt(b.size.x / Cell) + 2;
            int nz = Mathf.CeilToInt(b.size.z / Cell) + 2;
            float ox = b.min.x - Cell + Cell * 0.31830989f;
            float oz = b.min.z - Cell + Cell * 0.27182818f;

            var parity = new int[nx, nz];
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], bb = v[t[i + 1]], c = v[t[i + 2]];
                if (a.y <= y && bb.y <= y && c.y <= y) continue;        // wholly below the cut
                float det = (bb.x - a.x) * (c.z - a.z) - (bb.z - a.z) * (c.x - a.x);
                if (Mathf.Abs(det) < 1e-12f) continue;                  // a vertical wall the ray grazes

                int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(bb.x, c.x)) - ox) / Cell));
                int i1 = Mathf.Min(nx - 1, Mathf.CeilToInt((Mathf.Max(a.x, Mathf.Max(bb.x, c.x)) - ox) / Cell));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(bb.z, c.z)) - oz) / Cell));
                int j1 = Mathf.Min(nz - 1, Mathf.CeilToInt((Mathf.Max(a.z, Mathf.Max(bb.z, c.z)) - oz) / Cell));

                for (int ix = i0; ix <= i1; ix++)
                {
                    float px = ox + ix * Cell - a.x;
                    for (int jz = j0; jz <= j1; jz++)
                    {
                        float pz = oz + jz * Cell - a.z;
                        float wb = (px * (c.z - a.z) - pz * (c.x - a.x)) / det;
                        if (wb < 0f) continue;
                        float wc = (pz * (bb.x - a.x) - px * (bb.z - a.z)) / det;
                        if (wc < 0f) continue;
                        float wa = 1f - wb - wc;
                        if (wa < 0f) continue;
                        if (wa * a.y + wb * bb.y + wc * c.y > y) parity[ix, jz]++;
                    }
                }
            }

            var seen = new bool[nx, nz];
            var islands = new List<Island>();
            var queue = new Queue<int>();
            for (int ix = 0; ix < nx; ix++)
                for (int jz = 0; jz < nz; jz++)
                {
                    if (seen[ix, jz] || (parity[ix, jz] & 1) == 0) continue;
                    seen[ix, jz] = true;
                    queue.Clear();
                    queue.Enqueue(ix * nz + jz);
                    int cells = 0;
                    while (queue.Count > 0)
                    {
                        int k = queue.Dequeue();
                        int cx = k / nz, cz = k % nz;
                        cells++;
                        Push(cx + 1, cz); Push(cx - 1, cz); Push(cx, cz + 1); Push(cx, cz - 1);
                        void Push(int ax, int az)
                        {
                            if (ax < 0 || ax >= nx || az < 0 || az >= nz) return;
                            if (seen[ax, az] || (parity[ax, az] & 1) == 0) return;
                            seen[ax, az] = true;
                            queue.Enqueue(ax * nz + az);
                        }
                    }
                    if (cells < MinComponentCells) continue;
                    float area = cells * Cell * Cell;
                    islands.Add(new Island { Area = area, Diameter = 2f * Mathf.Sqrt(area / Mathf.PI) });
                }
            return islands;
        }
    }
}
