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
        /// Vetch's legs meet the ground at y = 0, its shell's underside sits at
        /// y = 0.26 and its head's at 0.24, which the blend can push down to
        /// 0.2175 - so the clearance band is [0, 0.2175] and 0.12 is close to
        /// its middle, the point least sensitive to a future author nudging
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

        // -------------------------------------------- the other five species

        /// ONE CLAIM PER SPECIES, AND NOT THE SAME CLAIM SIX TIMES.
        ///
        /// `Vetch_StandsOnFourLegs...` above counts connected pieces in a slice
        /// near the ground, which is exactly right for a body whose silhouette
        /// column says "four stubby legs" - and it is the WRONG instrument for
        /// three of the five species Task 15 adds:
        ///
        ///   Loam has no legs, so the count is 1 for a grub and 1 for a smooth
        ///   sausage; it distinguishes nothing.
        ///   Pale has no limbs either - same problem.
        ///   Hollow has two stilts, and Ember has two legs, so the count cannot
        ///   tell the two upright bodies apart - which is the one pair the 40px
        ///   detector says is closest (9.6% against an 8% floor).
        ///
        /// So each species is pinned by what bible 1.2's silhouette column
        /// actually names for it, and the measurement is chosen to match:
        ///
        ///   vetch   four stubby legs      -> 4 islands, each >= 0.15 across
        ///   ember   tall, two legs        -> 2 islands + height >= 1.8x width
        ///   skitter six long thin legs    -> 6 islands, each <= 0.16 across
        ///   hollow  stilts, forward neck  -> body rides >= 40% of the height,
        ///                                    head reaches >= 0.45 past the body
        ///   loam    ground-hugger, no legs-> 1 island spanning >= 90% of the
        ///                                    length, height <= 0.35x length,
        ///                                    and four waists in the profile
        ///   pale    broad arc, small body -> span >= 3x the length, footprint
        ///                                    <= 25% of the span
        ///
        /// Every threshold sits between a half and two thirds of what the
        /// shipped recipe measures, so these detect a REGRESSION rather than
        /// pinning the current numbers in place.

        /// The slice height for each body that has a ground-clearance story at
        /// all. `EverySliceHeight_SitsInItsBodysGroundClearance` re-derives the
        /// band from the recipe, exactly as Vetch's own check above does.
        static readonly Dictionary<string, float> SliceHeights = new Dictionary<string, float>
        {
            { "vetch", SliceHeight }, { "ember", 0.18f }, { "skitter", 0.12f },
            { "hollow", 0.25f }, { "skirmisher", 0.12f },
        };

        static BodyRecipe Recipe(string id) => SpeciesRecipes.For(id) ?? RaiderRecipes.For(id);

        /// The generalised form of `TheSliceSitsInVetchsGroundClearance`: every
        /// slice this file cuts has to lie between the mesh's own floor and the
        /// body's underside, or the count below it measures a footprint rather
        /// than a set of limbs. Task 12b found that defect by hand on Vetch;
        /// this is the version that finds it on the eight bodies that came
        /// after, including the raiders.
        [Test]
        public void EverySliceHeight_SitsInItsBodysGroundClearance()
        {
            const float margin = 0.04f;
            foreach (var kv in SliceHeights)
            {
                var r = Recipe(kv.Key);
                Assert.IsNotNull(r, "no recipe for " + kv.Key);
                float floor = Committed(kv.Key).bounds.min.y;
                Assert.LessOrEqual(floor + margin, kv.Value,
                    kv.Key + ": the slice at y = " + kv.Value + " is within " + margin + " of the mesh's own " +
                    "floor (" + floor.ToString("F4") + "), where the mesher has already clipped the limb tips");

                float body = r.Primitives.Where(p => !p.Bone.StartsWith("leg")).Min(Underside) - r.Blend * 0.25f;
                Assert.GreaterOrEqual(body - margin, kv.Value,
                    kv.Key + ": the slice at y = " + kv.Value + " is within " + margin + " of its underside (" +
                    body.ToString("F4") + ") - the body reaches the ground between its own limbs, which is the " +
                    "defect Task 12b measured on Vetch. Lift the body or lower the slice.");
            }
        }

        // --------------------------------------------------------------- ember

        /// bible 1.2: "tall narrow torso, head crest, two legs". Two legs is the
        /// cheap half; the ratio is the half that separates Ember from Hollow,
        /// the only other upright body and the closest pair at 40px.
        [Test]
        public void Ember_StandsOnTwoLegs_AndIsTallerThanItIsWide()
        {
            var mesh = Committed("ember");
            var legs = Section(mesh, SliceHeights["ember"]);
            Assert.AreEqual(2, legs.Count,
                "bible 1.2 asks Ember for two legs. A slice at y = " + SliceHeights["ember"] + " found " +
                legs.Count + " piece(s) of solid. One means the torso reaches the ground between them - the " +
                "brief's own torso did, bottoming at y = -0.03. Areas: " +
                string.Join(", ", legs.Select(p => p.Area.ToString("F4"))) + ".");

            // Measured on the shipped recipe: 1.263 / 0.613 = 2.06.
            float widest = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
            Assert.GreaterOrEqual(mesh.bounds.size.y / widest, 1.8f,
                "Ember is " + mesh.bounds.size.y.ToString("F3") + " tall against a widest horizontal span of " +
                widest.ToString("F3") + " (" + (mesh.bounds.size.y / widest).ToString("F2") + "x) - bible 1.2 " +
                "says TALL NARROW torso, and bible 10.2 rule 1 gives 'tall and narrow is ranged'");
        }

        /// "head crest" is in bible 1.2's silhouette column, so it has to be in
        /// the SILHOUETTE - and `SilhouetteTests` bakes bodies with nothing
        /// mounted, so a crest that lived only at `sk_crown` would never appear
        /// in the read it exists for. Measured here the way Vetch's head lump
        /// is: take the crest primitive out of the field, and ask how much
        /// higher the committed mesh goes than what is left.
        [Test]
        public void Ember_CarriesACrestAboveItsBareHead()
        {
            var r = SpeciesRecipes.For("ember");
            var bare = r.Primitives
                .Where(p => !(p.Bone == "head" && p.Kind == PrimitiveKind.Capsule)).ToArray();
            Assert.AreNotEqual(r.Primitives.Length, bare.Length, "ember declares no crest primitive on its head bone");

            float bareTop = TopOf(bare, r.Blend);
            float top = Committed("ember").bounds.max.y;
            Assert.GreaterOrEqual(top - bareTop, 0.08f,
                "with its crest removed Ember tops out at " + bareTop.ToString("F3") + " and the committed mesh " +
                "reaches " + top.ToString("F3") + " - the crest only carries the outline " + (top - bareTop).ToString("F3") +
                " higher, which on a body " + Committed("ember").bounds.size.y.ToString("F2") + " tall is not a crest " +
                "anyone can see at 40px. Measured on the shipped recipe: 0.152.");
        }

        // ------------------------------------------------------------- skitter

        /// bible 1.2: "six long thin legs" - and bible 10.2 rule 1 hands
        /// "spindly is fast" to this species specifically. So this is Vetch's
        /// leg test with both numbers inverted: six rather than four, and a
        /// CEILING on how wide a leg may be rather than a floor.
        [Test]
        public void Skitter_StandsOnSixLegs_EachThinnerThanVetchsAre()
        {
            var legs = Section(Committed("skitter"), SliceHeights["skitter"]);
            Assert.AreEqual(6, legs.Count,
                "bible 1.2 asks Skitter for six long thin legs. A slice at y = " + SliceHeights["skitter"] +
                " found " + legs.Count + ". Areas: " + string.Join(", ", legs.Select(p => p.Area.ToString("F4"))) +
                ". Keep BodyRecipe.Blend at or below a leg's radius - at the brief's 0.08, more than twice it, " +
                "the smooth union fillets them into the body.");

            // Vetch's own test asserts >= 0.15 and calls anything under that
            // spindly. This asserts the other side of the same line. Measured:
            // 0.130 - 0.134, against Vetch's 0.216 - 0.221.
            foreach (var p in legs)
                Assert.LessOrEqual(p.Diameter, 0.16f,
                    "a Skitter leg is " + p.Diameter.ToString("F3") + " across at y = " + SliceHeights["skitter"] +
                    " - Vetch's legs are 0.216 and its own test calls anything under 0.15 spindly, which is the " +
                    "read bible 10.2 rule 1 gives to Skitter. These are meant to be the thin ones.");

            float biggest = legs.Max(p => p.Area), smallest = legs.Min(p => p.Area);
            Assert.LessOrEqual(biggest - smallest, biggest * 0.25f,
                "the six legs are not a matched set (" + string.Join(", ", legs.Select(p => p.Area.ToString("F4"))) + ")");
        }

        // -------------------------------------------------------------- hollow

        /// bible 1.2: "tiny body, stilt legs, long forward neck". Counting
        /// components would read 2 - and so does Ember, so the count settles
        /// nothing. What makes them STILTS is how high they hold the body, and
        /// what makes the neck LONG is how far past the body it puts the head.
        /// Both are measured; neither can be true of Ember.
        [Test]
        public void Hollow_RidesOnStilts_AndCarriesItsHeadOnALongForwardNeck()
        {
            var r = SpeciesRecipes.For("hollow");
            var mesh = Committed("hollow");

            Assert.AreEqual(2, Section(mesh, SliceHeights["hollow"]).Count,
                "bible 1.2 asks Hollow for two stilts");

            // Measured: the body's underside is 0.603 on a body 1.160 tall, 52%.
            // Ember's is 0.288 of 1.263, 23% - so this number, unlike the count,
            // tells the two upright bodies apart.
            float under = r.Primitives.Where(p => !p.Bone.StartsWith("leg")).Min(Underside) - r.Blend * 0.25f;
            float h = mesh.bounds.size.y;
            Assert.GreaterOrEqual(under / h, 0.40f,
                "Hollow's body bottoms at " + under.ToString("F3") + " on a body " + h.ToString("F3") +
                " tall (" + (under / h).ToString("P0") + ") - bible 1.2 says STILT legs, and a body that rides " +
                "less than 40% of its own height up is standing on legs like Ember's");

            // The neck, measured the way Vetch's head lump is: drop the neck and
            // head from the field and ask how much further forward the committed
            // mesh reaches. Measured: the body alone ends at 0.074, the mesh at
            // 0.805 - the neck carries 0.731, about 63% of the body's length.
            var bodyOnly = r.Primitives.Where(p => p.Bone != "neck" && p.Bone != "head").ToArray();
            float reach = float.NegativeInfinity;
            for (float y = 0.5f; y < h; y += 0.02f)
                reach = Mathf.Max(reach, ForwardReach(bodyOnly, r.Blend, y));
            Assert.GreaterOrEqual(mesh.bounds.max.x - reach, 0.45f,
                "with its neck and head removed Hollow reaches x = " + reach.ToString("F3") + " and the committed " +
                "mesh reaches " + mesh.bounds.max.x.ToString("F3") + " - the neck only carries the silhouette " +
                (mesh.bounds.max.x - reach).ToString("F3") + " forward, which bible 1.2 would not call LONG");
        }

        // ---------------------------------------------------------------- loam

        /// bible 1.2: "segmented ground-hugger, blunt snout, no legs". A slice
        /// reads ONE island for a grub and one for a pebble and one for a
        /// sausage, so the count is useless here - what it can say is that the
        /// one island runs the WHOLE length, which is what "no legs" means
        /// geometrically: the belly, not feet, is what touches down.
        [Test]
        public void Loam_HasNoLegs_AndHugsTheGroundAlongItsWholeLength()
        {
            var mesh = Committed("loam");
            var slice = Section(mesh, 0.10f);
            Assert.AreEqual(1, slice.Count,
                "bible 1.2 gives Loam no legs, so a slice at y = 0.10 must be one piece of solid, not " +
                slice.Count);

            // Measured: 1.52 of a 1.540 body, 99%. Vetch's four legs at the same
            // height span 0.22 each of a 1.18 body - about 19%.
            Assert.GreaterOrEqual(slice[0].XExtent, mesh.bounds.size.x * 0.90f,
                "Loam's ground contact runs " + slice[0].XExtent.ToString("F3") + " of a body " +
                mesh.bounds.size.x.ToString("F3") + " long (" + (slice[0].XExtent / mesh.bounds.size.x).ToString("P0") +
                ") - a ground-hugger's belly touches down along its whole length; anything less is standing on something");

            // Measured: 0.428 tall on 1.540 long = 0.278. Vetch, the other wide
            // low body, is 0.65.
            Assert.LessOrEqual(mesh.bounds.size.y, mesh.bounds.size.x * 0.35f,
                "Loam is " + mesh.bounds.size.y.ToString("F3") + " tall against " + mesh.bounds.size.x.ToString("F3") +
                " long (" + (mesh.bounds.size.y / mesh.bounds.size.x).ToString("F2") + ") - bible 1.2 says GROUND-HUGGER");
        }

        /// SEGMENTATION IS MEASURED ON THE FIELD, NOT ON THE MESH, and that is
        /// a deliberate exception to this file's rule.
        ///
        /// Loam is 1.54 long, so at grid 20 the mesher's x cell is 0.091 -
        /// twice the depth of the 0.045 waists between its segments. The
        /// committed mesh keeps all four but smears them from 15-21% deep to
        /// 3-9%, and a threshold low enough to pass that would also pass a
        /// sausage. Resolving them in the mesh needs grid 22, which meshes to
        /// 3296 triangles against `MesherTests`' 2500 budget.
        ///
        /// So this asserts the RECIPE's own surface, which is what an author
        /// edits, and `DriftTests` is what ties the committed asset to it.
        /// `Vetch_CarriesAHeadLumpOnTheFront` already mixes the two the same
        /// way. What this cannot claim is that the segmentation reads at 40px -
        /// it does not, and it is not meant to; it reads on the card render.
        [Test]
        public void Loam_IsSegmented_MeasuredOnTheRecipesOwnSurface()
        {
            var r = SpeciesRecipes.For("loam");
            var w = HalfWidthProfile(r);
            float peak = w.Max();
            var depths = new List<float>();
            for (int i = 1; i < w.Length - 1; i++)
            {
                if (!(w[i] <= w[i - 1] && w[i] < w[i + 1])) continue;
                int k = i - 1; while (k - 1 >= 0 && w[k - 1] > w[k]) k--;
                int j = i + 1; while (j + 1 < w.Length && w[j + 1] > w[j]) j++;
                depths.Add((Mathf.Min(w[k], w[j]) - w[i]) / peak);
            }

            // Five segments leave four waists. Measured on the shipped recipe:
            // 15.1%, 15.1%, 17.0%, 20.8% of the widest half-width.
            Assert.GreaterOrEqual(depths.Count, 4,
                "Loam's half-width profile has " + depths.Count + " waist(s), not the four that five segments " +
                "leave - the union has welded them into one smooth body, which is what the brief's blend of 0.12 " +
                "did. Profile: " + string.Join(", ", w.Select(v => v.ToString("F3"))));
            foreach (var d in depths)
                Assert.GreaterOrEqual(d, 0.10f,
                    "a waist is only " + d.ToString("P1") + " of the widest half-width below its own shoulders - " +
                    "too shallow to read as a segment. Spread the spheres further apart or lower BodyRecipe.Blend.");
        }

        // ---------------------------------------------------------------- pale

        /// bible 1.2: "broad wing arc, small hanging body". No limbs, so no
        /// component count says anything; the two nouns in that column are both
        /// spans, and spans are what this measures.
        [Test]
        public void Pale_IsMostlyWingSpan_WithASmallBodyHangingUnderIt()
        {
            var mesh = Committed("pale");

            // Measured: 1.720 across against 0.471 long = 3.65. No other body is
            // above 1.5, and Vetch - the other wide one - is 0.79.
            Assert.GreaterOrEqual(mesh.bounds.size.z, mesh.bounds.size.x * 3f,
                "Pale spans " + mesh.bounds.size.z.ToString("F3") + " across against " +
                mesh.bounds.size.x.ToString("F3") + " long (" +
                (mesh.bounds.size.z / mesh.bounds.size.x).ToString("F2") + "x) - bible 1.2 says BROAD wing arc");

            // The hanging body, low down where the wings are not. Measured: a
            // 0.25 footprint under a 1.720 span, 14%.
            var foot = Section(mesh, mesh.bounds.min.y + 0.10f * mesh.bounds.size.y);
            Assert.AreEqual(1, foot.Count, "Pale has no limbs, so its footprint is one piece");
            Assert.LessOrEqual(foot[0].ZExtent, mesh.bounds.size.z * 0.25f,
                "Pale's footprint is " + foot[0].ZExtent.ToString("F3") + " across under a span of " +
                mesh.bounds.size.z.ToString("F3") + " (" + (foot[0].ZExtent / mesh.bounds.size.z).ToString("P0") +
                ") - bible 1.2 says a SMALL hanging body under the arc, not a body as broad as its wings");
        }

        // ------------------------------------------------------------- raiders

        /// TASK 12b'S DEFECT, APPLIED WHERE NOBODY HAD LOOKED. Raiders go
        /// through the same mesher under the same conventions, and they are
        /// covered by neither `SilhouetteTests` (species only) nor the claims
        /// above. A raider whose hull reaches the ground between its own legs
        /// reads as a blob exactly as Vetch did.
        ///
        /// The expected counts come from `Character Bible.dc.html`'s shape
        /// column, so a body that grows or loses a limb fails here rather than
        /// silently shipping: Skirmisher is "angular wedge, two blade legs";
        /// Courser is "forward-raked diamond, speed lines" and Lash is "wedge
        /// body, long trailing whip" - neither names a limb, so their hulls
        /// reach the ground on purpose and one island is the right answer.
        [Test]
        public void EveryRaider_ShowsTheLimbsItsShapeColumnNames()
        {
            var expected = new Dictionary<string, int>
            {
                { "courser", 1 }, { "lash", 1 }, { "skirmisher", 2 },
            };
            Assert.AreEqual(expected.Count, RaiderRecipes.All.Count,
                "RaiderRecipes carries " + RaiderRecipes.All.Count + " bodies and this test names " +
                expected.Count + " - a raider added without a shape claim is a raider nobody checked");

            foreach (var r in RaiderRecipes.All)
            {
                Assert.IsTrue(expected.ContainsKey(r.Id), "no shape claim for raider " + r.Id);
                var islands = Section(Committed(r.Id), 0.12f);
                Assert.AreEqual(expected[r.Id], islands.Count,
                    r.Id + ": a slice at y = 0.12 found " + islands.Count + " piece(s), expected " +
                    expected[r.Id] + ". Areas: " + string.Join(", ", islands.Select(p => p.Area.ToString("F4"))) +
                    (expected[r.Id] > 1 && islands.Count == 1
                        ? " - one piece where the character bible names legs is the Task 12b defect: the hull is " +
                          "resting on the ground between them."
                        : ""));
            }
        }

        /// COURSER AND LASH NEEDED A CLAIM THAT CAN FAIL. The island count above
        /// expects 1 for both, which is right and is also satisfied by a
        /// sphere, a box, or anything else with no limbs - by the standard this
        /// file sets one screen up for Loam and Pale, that is not a claim. The
        /// character bible's shape column names something measurable for each,
        /// so here it is measured.
        ///
        /// "Forward-raked diamond": a diamond tapers at BOTH ends, so its
        /// height profile peaks in the middle rather than at an end, and the
        /// rake is which end is sharper. Measured on the shipped recipe: the
        /// peak is interior, the tail is 45% of it and the nose 25%, so the
        /// nose is the sharp end at 56% of the tail. A box reads 100% and 100%.
        [Test]
        public void Courser_IsADiamondRakedForward_NotABoxOnWheels()
        {
            var h = Profile(Committed("courser"), out _);
            int peak = 0;
            for (int i = 0; i < h.Length; i++) if (h[i] > h[peak]) peak = i;
            Assert.IsTrue(peak > 0 && peak < h.Length - 1,
                "Courser's tallest section is at one end (bin " + peak + " of " + h.Length +
                ") - that is a wedge, and the character bible says diamond");

            float nose = h[h.Length - 1], tail = h[0];
            Assert.LessOrEqual(nose, h[peak] * 0.55f,
                "Courser's nose is " + nose.ToString("F3") + " tall against a peak of " +
                h[peak].ToString("F3") + " (" + (nose / h[peak]).ToString("P0") + ") - a diamond comes to a point");
            Assert.LessOrEqual(tail, h[peak] * 0.55f,
                "Courser's tail is " + tail.ToString("F3") + " tall against a peak of " +
                h[peak].ToString("F3") + " (" + (tail / h[peak]).ToString("P0") + ") - it tapers at BOTH ends or it is not a diamond");
            Assert.LessOrEqual(nose, tail * 0.70f,
                "Courser's nose (" + nose.ToString("F3") + ") is not meaningfully sharper than its tail (" +
                tail.ToString("F3") + ") - the character bible says FORWARD-raked, so the point goes at the front");
        }

        /// "Wedge body, long trailing whip." The whip is the half of that the
        /// island count cannot see: a long thin thing behind a wide thing.
        /// Measured: the rearmost 40% of the length is 38% as wide as the
        /// widest point, and 44% of the body's length is whip.
        [Test]
        public void Lash_TrailsALongThinWhipBehindItsWedge()
        {
            Profile(Committed("lash"), out var w);
            float widest = w.Max();
            int rear = Mathf.RoundToInt(w.Length * 0.4f);
            float rearWidest = 0f;
            for (int i = 0; i < rear; i++) rearWidest = Mathf.Max(rearWidest, w[i]);
            Assert.LessOrEqual(rearWidest, widest * 0.55f,
                "Lash's rearmost 40% is " + rearWidest.ToString("F3") + " wide against a widest point of " +
                widest.ToString("F3") + " (" + (rearWidest / widest).ToString("P0") + ") - that is a tail on a " +
                "body, not the whip the character bible names");

            int whip = w.Count(v => v > 0f && v <= widest * 0.5f);
            Assert.GreaterOrEqual(whip / (float)w.Length, 0.30f,
                "only " + (whip / (float)w.Length).ToString("P0") + " of Lash's length is thin enough to read " +
                "as whip rather than body - the character bible says LONG");
        }

        /// The committed mesh's height and plan half-width at each of 16
        /// stations along x. Sixteen because the raiders mesh at grid 18-20, so
        /// finer bins would be reading the mesher's own cell rather than the
        /// shape; the slab overlaps by 0.02 so no bin comes back empty.
        static float[] Profile(Mesh mesh, out float[] halfWidth)
        {
            const int bins = 16;
            var v = mesh.vertices;
            var b = mesh.bounds;
            var height = new float[bins];
            halfWidth = new float[bins];
            for (int i = 0; i < bins; i++)
            {
                float lo = b.min.x + b.size.x * i / bins - 0.02f;
                float hi = b.min.x + b.size.x * (i + 1) / bins + 0.02f;
                var slab = v.Where(p => p.x >= lo && p.x < hi).ToArray();
                if (slab.Length == 0) continue;
                height[i] = slab.Max(p => p.y) - slab.Min(p => p.y);
                halfWidth[i] = slab.Max(p => Mathf.Abs(p.z));
            }
            return height;
        }

        // -------------------------------------------------------- field probes

        /// The topmost point of a field, down the midline. Used to ask what a
        /// body would top out at with one primitive removed.
        static float TopOf(Primitive[] prims, float blend)
        {
            var bounds = Sdf.BoundsOf(prims, 0.1f);
            float top = bounds.min.y;
            for (float x = bounds.min.x; x <= bounds.max.x; x += 0.01f)
                for (float y = bounds.max.y; y > bounds.min.y; y -= 0.002f)
                    if (Sdf.Field(prims, blend, new Vector3(x, y, 0f)) < 0f) { top = Mathf.Max(top, y); break; }
            return top;
        }

        /// The body's half-width in plan at each of `samples` stations along x:
        /// the furthest -z at which the field is still inside, over every height.
        /// Sampled off the field rather than the mesh because the mesher's cell
        /// is coarser than the feature being measured - see the test above.
        static float[] HalfWidthProfile(BodyRecipe r, int samples = 62)
        {
            var b = Sdf.BoundsOf(r.Primitives, 0f);
            var w = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float x = b.min.x + b.size.x * ((i + 0.5f) / samples);
                float widest = 0f;
                for (float y = b.min.y + 0.02f; y <= b.max.y; y += 0.02f)
                    for (float z = 0f; z > b.min.z; z -= 0.004f)
                        if (Sdf.Field(r.Primitives, r.Blend, new Vector3(x, y, z)) < 0f)
                            widest = Mathf.Max(widest, -z);
                w[i] = widest;
            }
            return w;
        }

        // ----------------------------------------------------- the cross-cut

        struct Island { public float Area; public float Diameter; public float XExtent; public float ZExtent; }

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
                    int lox = ix, hix = ix, loz = jz, hiz = jz;
                    while (queue.Count > 0)
                    {
                        int k = queue.Dequeue();
                        int cx = k / nz, cz = k % nz;
                        cells++;
                        lox = Mathf.Min(lox, cx); hix = Mathf.Max(hix, cx);
                        loz = Mathf.Min(loz, cz); hiz = Mathf.Max(hiz, cz);
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
                    islands.Add(new Island
                    {
                        Area = area,
                        Diameter = 2f * Mathf.Sqrt(area / Mathf.PI),
                        XExtent = (hix - lox + 1) * Cell,
                        ZExtent = (hiz - loz + 1) * Cell,
                    });
                }
            return islands;
        }
    }
}
