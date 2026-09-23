using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Creatures
{
    public static class SpeciesRecipes
    {
        /// GRID = 20, NOT THE BRIEF'S 24. Measured, not guessed: naive surface
        /// nets emit two triangles per surface cell, so the count scales with
        /// grid squared. 20 meshes this body to 2056 triangles with 18% of the
        /// 2500 budget spare, and the cell is still 0.059-0.087 across, about
        /// a quarter of a leg's diameter, so the silhouette reads smooth. If a
        /// later recipe needs more detail, buy it with a smaller padding, not
        /// a bigger grid.
        ///
        /// SEATED ON y = 0, LITERALLY. The legs' lower caps end at y = -0.005,
        /// and the mesher's cell averaging lifts the meshed tip to y = +0.0094.
        /// `DriftTests.EveryBody_RestsOnTheGroundPlane` holds every future body
        /// to that within 0.02. The convention in Recipe.cs stays true of what
        /// is written here, which is the point: nothing downstream has to know
        /// about a hidden offset.
        ///
        /// A DOME ON LEGS, NOT A PEBBLE ON THE GROUND. The first pass authored
        /// the shell as one sphere of radius 0.52 centred at y = 0.52, whose
        /// own underside therefore touched y = 0: the body reached the floor
        /// between its own legs, so no horizontal slice anywhere could separate
        /// them and the render read as a smooth blob. That was the whole of it,
        /// not the blend - a sweep with the blend at zero still found the dome's
        /// ground contact patch as a fifth island. bible 1.2 asks for a "low
        /// dome, four stubby legs, no neck" and 10.2 rule 1 makes the
        /// silhouette load-bearing, so the shell is now three capsules that
        /// bottom out at y = 0.26 and taper toward the flanks, leaving 0.26 of
        /// clearance for legs 0.24 across to stand in.
        /// `BodyShapeTests.Vetch_StandsOnFourLegs_CountedInASliceNearTheGround`
        /// counts them, and reads four rather than one.
        ///
        /// BLEND 0.09, BELOW A LEG'S RADIUS. `Sdf.SmoothMin`'s k is a distance,
        /// and at the old 0.22 it was twice the legs' 0.11 radius - enough to
        /// fillet a leg away over its whole length. At 0.09, under the 0.12
        /// radius the legs now carry, the joins are soft without dissolving.
        /// It is not what made the blob a blob, but it is what kept the legs
        /// from reading even where the dome left room for them.
        public static readonly BodyRecipe Vetch = new BodyRecipe
        {
            Id = "vetch",
            Blend = 0.09f, Grid = 20, Padding = 0.2f,
            Bones = new[]
            {
                new BoneDef { Name = "root", Position = new Vector3(0f, 0.48f, 0f) },
                new BoneDef { Name = "head", Parent = "root", Position = new Vector3(0.52f, 0.42f, 0f) },
                new BoneDef { Name = "leg_fl", Parent = "root", Position = new Vector3(0.32f, 0.25f, 0.3f) },
                new BoneDef { Name = "leg_fr", Parent = "root", Position = new Vector3(0.32f, 0.25f, -0.3f) },
                new BoneDef { Name = "leg_bl", Parent = "root", Position = new Vector3(-0.32f, 0.25f, 0.3f) },
                new BoneDef { Name = "leg_br", Parent = "root", Position = new Vector3(-0.32f, 0.25f, -0.3f) },
            },
            // The shell is three capsules lying fore-and-aft. One primitive
            // cannot be both wide and low here - a sphere's height is its width
            // - so the width comes from spreading them across Z and the low
            // profile from their radius, and the two flankers sit lower and
            // thinner than the spine so the shell falls away at the sides
            // instead of ending in a wall. All three bottom out at y = 0.26.
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(-0.2f, 0.52f, 0f), new Vector3(0.2f, 0.52f, 0f), 0.26f, "root", 0.15f),        // the spine of the dome
                Primitive.Capsule(new Vector3(-0.18f, 0.45f, 0.28f), new Vector3(0.18f, 0.45f, 0.28f), 0.19f, "root", 0.15f), // the left flank, lower and thinner
                Primitive.Capsule(new Vector3(-0.18f, 0.45f, -0.28f), new Vector3(0.18f, 0.45f, -0.28f), 0.19f, "root", 0.15f),
                Primitive.Sphere(new Vector3(0.56f, 0.42f, 0f), 0.18f, "head", 0.35f),                                        // no neck: the head is a lump on the front
                Primitive.Capsule(new Vector3(0.3f, 0.38f, 0.28f), new Vector3(0.34f, 0.115f, 0.32f), 0.12f, "leg_fl", 0.25f),
                Primitive.Capsule(new Vector3(0.3f, 0.38f, -0.28f), new Vector3(0.34f, 0.115f, -0.32f), 0.12f, "leg_fr", 0.25f),
                Primitive.Capsule(new Vector3(-0.3f, 0.38f, 0.28f), new Vector3(-0.34f, 0.115f, 0.32f), 0.12f, "leg_bl", 0.25f),
                Primitive.Capsule(new Vector3(-0.3f, 0.38f, -0.28f), new Vector3(-0.34f, 0.115f, -0.32f), 0.12f, "leg_br", 0.25f),
            },
            // THE FLANK IS THE RIGHT FLANK, SO IT FACES -Z. Recipe.cs makes +Z
            // the creature's left and a part's +Y the direction out of the
            // body, and Quaternion.Euler(90,0,0) sends +Y to +Z - which at a
            // negative z points INTO the body. Euler(-90,0,0) sends +Y to -Z,
            // outward, and leaves the part's +X still forward.
            //
            // ALL THREE MOVED WITH THE BODY. The shell is lower and narrower
            // than the sphere it replaced, so a socket left where it was would
            // have floated: dorsal sits on the new apex (y 0.78, measured off
            // the field, was 1.02), flank on the new flank (z -0.46, was -0.5)
            // and crown on the new head (0.60, 0.56, was 0.66, 0.65).
            // `RecipeTests.EverySocket_FacesOutOfTheBody` re-checks all three.
            Sockets = new[]
            {
                new SocketDef { Name = Sockets.Dorsal, Position = new Vector3(-0.04f, 0.78f, 0f), Euler = Vector3.zero, Scale = 1f },
                new SocketDef { Name = Sockets.Flank, Position = new Vector3(0f, 0.5f, -0.46f), Euler = new Vector3(-90f, 0f, 0f), Scale = 0.8f },
                new SocketDef { Name = Sockets.Crown, Position = new Vector3(0.6f, 0.56f, 0f), Euler = new Vector3(0f, 0f, -35f), Scale = 0.55f },
            },
        };

        /// EMBER - bible 1.2: "tall narrow torso, head crest, two legs".
        ///
        /// THE BRIEF'S TORSO SAT ON THE GROUND - Task 12b's defect again, and
        /// this time it is in the arithmetic rather than hidden in it: a capsule
        /// from y 0.15 to y 1.0 at radius 0.18 has its underside at y = -0.03,
        /// so the body reaches the floor BETWEEN its own two legs and no slice
        /// anywhere can separate them. The torso now bottoms at 0.30 (0.288 once
        /// the blend's quarter-k push is allowed for), which leaves a clearance
        /// band of [0, 0.290] for two legs 0.21 across to stand in.
        /// `BodyShapeTests.Ember_StandsOnTwoLegs_AndIsTallerThanItIsWide` counts
        /// them and reads 2.
        ///
        /// THE HEAD IS NOT BARE. The brief left the crest to `sk_crown`, but
        /// that socket carries the Instinct cue, whose existence bible 10.3
        /// records as contingent, and `SilhouetteTests` compares BODIES with
        /// nothing mounted on them. A crest that lives only in a socket is a
        /// crest that bible 1.2's silhouette column names and the 40px read
        /// never sees. So it is body geometry: one swept capsule on the head
        /// bone, carrying the top of the outline 0.152 above the crestless head
        /// - 12% of Ember's height. `sk_crown` sits forward of it on the snout,
        /// where it still has clear surface.
        ///
        /// THE HEAD SHRANK TO 0.135 TO LET THE CREST READ. At radius 0.155 the
        /// head swallowed it: the eyes-on pass on the first bake showed a nub,
        /// not a crest, because a 0.07 capsule leaving a 0.155 sphere barely
        /// clears it. A smaller head at a lower blend takes the rise from 0.116
        /// to 0.152 at the same overall height, which the fixed bake camera
        /// leaves no room to buy any other way.
        ///
        /// TALLER THAN IT IS WIDE, BY 2.06. Height 1.263 against a widest
        /// horizontal span of 0.613. That ratio is the measurable form of "tall
        /// narrow", and it is what holds Ember apart from Hollow - the only
        /// other upright body, and the closest pair at 40px (9.6%, floor 8%).
        ///
        /// SHORTER THAN THE FIRST PASS, BECAUSE THE BAKE CAMERA CROPS. At 1.40
        /// tall the crest ran off the top of `CreatureBaker`'s 192px frame - row
        /// 0 of 192. At 1.263 it clears by 10px. One fixed camera serves every
        /// body, so height is a budget here exactly as triangles are.
        ///
        /// Grid 23, blend 0.04: 2372 triangles, and the crest's 0.14 thickness
        /// spans 2.1 cells. At grid 20 it spanned 1.4, two surface sheets went
        /// through one cell, and `MesherTests.EveryMesh_IsAClosedManifold`
        /// caught the fold.
        public static readonly BodyRecipe Ember = new BodyRecipe
        {
            Id = "ember",
            Blend = 0.04f, Grid = 23, Padding = 0.12f,
            Bones = new[]
            {
                new BoneDef { Name = "root", Position = new Vector3(0f, 0.54f, 0f) },
                new BoneDef { Name = "head", Parent = "root", Position = new Vector3(0.18f, 0.99f, 0f) },
                new BoneDef { Name = "leg_l", Parent = "root", Position = new Vector3(0f, 0.29f, 0.17f) },
                new BoneDef { Name = "leg_r", Parent = "root", Position = new Vector3(0f, 0.29f, -0.17f) },
            },
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(-0.02f, 0.54f, 0f), new Vector3(0.02f, 0.84f, 0f), 0.24f, "root", 0.15f),
                Primitive.Sphere(new Vector3(0.20f, 0.99f, 0f), 0.135f, "head", 0.35f),
                Primitive.Capsule(new Vector3(0.24f, 1.04f, 0f), new Vector3(0f, 1.21f, 0f), 0.07f, "head", 0.35f),
                Primitive.Capsule(new Vector3(0f, 0.46f, 0.15f), new Vector3(0.02f, 0.105f, 0.21f), 0.105f, "leg_l", 0.25f),
                Primitive.Capsule(new Vector3(0f, 0.46f, -0.15f), new Vector3(0.02f, 0.105f, -0.21f), 0.105f, "leg_r", 0.25f),
            },
            // sk_dorsal IS ON THE BACK OF THE SHOULDER, NOT THE TOP OF IT, AND
            // THE BAKE CAMERA IS WHY. Ember's own crest already sits 10px from
            // the top of `CreatureBaker`'s 192px frame; a taunt or a cinder
            // mounted on the apex at 1.046 ran straight off it (0px margin at
            // every scale down to 0.5). Moved back to x = -0.20 the surface is
            // 0.934, and the tallest part clears by 9px - the same margin
            // Vetch's taunt was tuned to. It also leaves both combat sockets
            // at scale 0.6, which is what rig_proof 3 asks for when it says to
            // present comparable surfaces at the two of them.
            Sockets = new[]
            {
                new SocketDef { Name = Sockets.Dorsal, Position = new Vector3(-0.20f, 0.934f, 0f), Euler = Vector3.zero, Scale = 0.6f },
                new SocketDef { Name = Sockets.Flank, Position = new Vector3(0f, 0.58f, -0.240f), Euler = new Vector3(-90f, 0f, 0f), Scale = 0.6f },
                new SocketDef { Name = Sockets.Crown, Position = new Vector3(0.30f, 0.97f, 0f), Euler = new Vector3(0f, 0f, -40f), Scale = 0.5f },
            },
        };

        /// SKITTER - bible 1.2: "small body, six long thin legs, tiny head".
        ///
        /// BLEND 0.045, NOT THE BRIEF'S 0.08. `Sdf.SmoothMin`'s k is a distance,
        /// and 0.08 is more than twice the legs' radius - the same arithmetic
        /// that filleted Vetch's legs away in Task 12b, on the one body where
        /// the legs ARE the silhouette. At 0.045, below the 0.065 the legs
        /// carry, all six survive.
        ///
        /// LEGS AT RADIUS 0.065, NOT THE BRIEF'S 0.035, AND NOT A TASTE CALL.
        /// The mesher puts one vertex per cell, so a feature thinner than two
        /// cells folds the surface into itself. Skitter's z cell is 0.057, so a
        /// 0.07-thick leg is 1.2 cells: measured, 16 doubled edges. At 0.13
        /// across it is 2.3 cells and the surface closes. They still read thin -
        /// 0.134 in the ground slice against Vetch's 0.221, and
        /// `Skitter_StandsOnSixLegs_EachThinnerThanVetchsAre` holds them under
        /// the 0.15 floor that Vetch's own "stubby" test sits above.
        ///
        /// GRID 20 AND PADDING 0.07, NOT THE BRIEF'S GRID 28. Triangles scale
        /// with the grid squared, and 28 meshes this body to about 4800 against
        /// a 2500 budget. The detail comes back out of the PADDING instead,
        /// which is what `PartRecipes`' own note recommends: 0.07 buys a 0.057
        /// cell at grid 20 where 0.25 would have cost 0.075.
        ///
        /// SIX ISLANDS, NOT FOUR. The body sphere's underside is 0.229 and the
        /// legs reach -0.010, so the clearance band is [0, 0.229] and the slice
        /// at 0.12 finds six separate legs.
        ///
        /// EIGHT BONES, WHICH IS THE CAP. `RecipeTests.EveryBody_NamesOnlyBones
        /// ItDeclares` enforces eight and six legs plus a head plus a root is
        /// exactly that. There is no headroom here: a Skitter that wants a
        /// jaw, a tail or a second head segment has to take a bone off a leg
        /// first, or the cap has to move and the Mobile tier's two-influence
        /// skinning limit has to be re-checked with it.
        ///
        /// ITS FLANK SOCKET SITS ON A LEG'S BULGE, AND THAT IS A TRADE, NOT AN
        /// OVERSIGHT. rig_proof 3 asks the two combat sockets to present
        /// similar local curvature; Skitter's dorsal sits on the 0.200 torso
        /// and its flank on a 0.065 leg, a ratio of 3.08 and the worst of the
        /// six. Moving the flank onto the torso IS possible - (0.00, 0.48,
        /// -0.208) reaches a clearance of +0.028, better than Vetch's own
        /// 0.022 - but only at socket scales 0.50/0.25, which costs 69% of
        /// every flank part's pixels. Section 3 states the collision clause as
        /// a requirement and the curvature clause as advice that "costs
        /// nothing at authoring time"; on a body 0.40 across it costs bible
        /// 10.4's single most important functional requirement, so the parts
        /// stay big. `RecipeTests.EveryCombatSocket_SitsOnTheBodysSurface`
        /// logs the ratio for all six rather than gating on a number nobody
        /// agreed to.
        public static readonly BodyRecipe Skitter = new BodyRecipe
        {
            Id = "skitter",
            Blend = 0.045f, Grid = 20, Padding = 0.07f,
            Bones = new[]
            {
                new BoneDef { Name = "root", Position = new Vector3(0f, 0.44f, 0f) },
                new BoneDef { Name = "head", Parent = "root", Position = new Vector3(0.26f, 0.42f, 0f) },
                new BoneDef { Name = "leg_fl", Parent = "root", Position = new Vector3(0.13f, 0.26f, 0.23f) },
                new BoneDef { Name = "leg_fr", Parent = "root", Position = new Vector3(0.13f, 0.26f, -0.23f) },
                new BoneDef { Name = "leg_ml", Parent = "root", Position = new Vector3(0f, 0.26f, 0.27f) },
                new BoneDef { Name = "leg_mr", Parent = "root", Position = new Vector3(0f, 0.26f, -0.27f) },
                new BoneDef { Name = "leg_bl", Parent = "root", Position = new Vector3(-0.13f, 0.26f, 0.23f) },
                new BoneDef { Name = "leg_br", Parent = "root", Position = new Vector3(-0.13f, 0.26f, -0.23f) },
            },
            Primitives = new[]
            {
                Primitive.Sphere(new Vector3(0f, 0.44f, 0f), 0.20f, "root", 0.15f),
                Primitive.Sphere(new Vector3(0.26f, 0.42f, 0f), 0.085f, "head", 0.35f),
                Primitive.Capsule(new Vector3(0.10f, 0.44f, 0.12f), new Vector3(0.26f, 0.055f, 0.37f), 0.065f, "leg_fl", 0.25f),
                Primitive.Capsule(new Vector3(0.10f, 0.44f, -0.12f), new Vector3(0.26f, 0.055f, -0.37f), 0.065f, "leg_fr", 0.25f),
                Primitive.Capsule(new Vector3(0f, 0.44f, 0.14f), new Vector3(0f, 0.055f, 0.43f), 0.065f, "leg_ml", 0.25f),
                Primitive.Capsule(new Vector3(0f, 0.44f, -0.14f), new Vector3(0f, 0.055f, -0.43f), 0.065f, "leg_mr", 0.25f),
                Primitive.Capsule(new Vector3(-0.10f, 0.44f, 0.12f), new Vector3(-0.26f, 0.055f, 0.37f), 0.065f, "leg_bl", 0.25f),
                Primitive.Capsule(new Vector3(-0.10f, 0.44f, -0.12f), new Vector3(-0.26f, 0.055f, -0.37f), 0.065f, "leg_br", 0.25f),
            },
            Sockets = new[]
            {
                new SocketDef { Name = Sockets.Dorsal, Position = new Vector3(-0.02f, 0.638f, 0f), Euler = Vector3.zero, Scale = 0.5f },
                new SocketDef { Name = Sockets.Flank, Position = new Vector3(0.06f, 0.36f, -0.238f), Euler = new Vector3(-90f, 0f, 0f), Scale = 0.45f },
                new SocketDef { Name = Sockets.Crown, Position = new Vector3(0.30f, 0.46f, 0f), Euler = new Vector3(0f, 0f, -35f), Scale = 0.35f },
            },
        };

        /// HOLLOW - bible 1.2: "tiny body, stilt legs, long forward neck".
        ///
        /// COMPONENT COUNT CANNOT PIN THIS ONE. A slice near the ground finds
        /// two stilts - and Ember's slice finds two legs, so the number that
        /// settles Vetch settles nothing here. Hollow is pinned instead by the
        /// two things bible 1.2 actually names: the body rides at 52% of the
        /// animal's own height, which is what makes them stilts rather than
        /// legs, and the neck carries the head 0.731 forward of where the body
        /// alone ends. Both are in `BodyShapeTests`, both measured.
        ///
        /// sk_dorsal IS ON THE NECK, AND THAT IS A COLLISION FIX. On a body
        /// 0.29 across, a dorsal socket on top and a flank socket on the side
        /// cannot be far enough apart in y for two carapaces to miss: the part
        /// is 0.49 deep in its own y, and half of that already exceeds the
        /// body's radius. Measured on the first placement: -0.103, a real
        /// overlap, on a claim rig_proof 3 calls load-bearing. rig_proof 3 puts
        /// sk_dorsal "along the spine or upper mass", and on a wader the spine
        /// IS the neck; moving it there separates the two parts on x as well as
        /// y, and takes Hollow's worst pair from -0.103 to +0.090.
        ///
        /// Stilts at radius 0.065, blend 0.05: 1688 triangles at grid 22.
        public static readonly BodyRecipe Hollow = new BodyRecipe
        {
            Id = "hollow",
            Blend = 0.05f, Grid = 22, Padding = 0.08f,
            Bones = new[]
            {
                new BoneDef { Name = "root", Position = new Vector3(-0.07f, 0.76f, 0f) },
                new BoneDef { Name = "neck", Parent = "root", Position = new Vector3(0.32f, 0.92f, 0f) },
                new BoneDef { Name = "head", Parent = "neck", Position = new Vector3(0.72f, 1.08f, 0f) },
                new BoneDef { Name = "leg_l", Parent = "root", Position = new Vector3(-0.06f, 0.40f, 0.11f) },
                new BoneDef { Name = "leg_r", Parent = "root", Position = new Vector3(-0.06f, 0.40f, -0.11f) },
            },
            Primitives = new[]
            {
                Primitive.Sphere(new Vector3(-0.07f, 0.76f, 0f), 0.145f, "root", 0.15f),
                Primitive.Capsule(new Vector3(0.02f, 0.80f, 0f), new Vector3(0.66f, 1.06f, 0f), 0.068f, "neck", 0.3f),
                Primitive.Sphere(new Vector3(0.72f, 1.08f, 0f), 0.095f, "head", 0.35f),
                Primitive.Capsule(new Vector3(-0.04f, 0.74f, 0.09f), new Vector3(-0.08f, 0.055f, 0.13f), 0.065f, "leg_l", 0.25f),
                Primitive.Capsule(new Vector3(-0.04f, 0.74f, -0.09f), new Vector3(-0.08f, 0.055f, -0.13f), 0.065f, "leg_r", 0.25f),
            },
            Sockets = new[]
            {
                new SocketDef { Name = Sockets.Dorsal, Position = new Vector3(0.26f, 0.970f, 0f), Euler = Vector3.zero, Scale = 0.45f },
                new SocketDef { Name = Sockets.Flank, Position = new Vector3(-0.07f, 0.72f, -0.160f), Euler = new Vector3(-90f, 0f, 0f), Scale = 0.45f },
                new SocketDef { Name = Sockets.Crown, Position = new Vector3(0.78f, 1.12f, 0f), Euler = new Vector3(0f, 0f, -30f), Scale = 0.35f },
            },
        };

        /// LOAM - bible 1.2: "segmented ground-hugger, blunt snout, no legs".
        ///
        /// NO LEGS MEANS THE GROUND SLICE READS ONE, ALWAYS - so the count that
        /// settles Vetch and Skitter is the wrong instrument here, and asserting
        /// it would be a claim a smooth sausage passes as readily as a grub.
        /// Loam is pinned by three measurements instead: the slice at 0.10 is
        /// ONE island spanning 99% of the body's length (a belly on the ground,
        /// not feet); the body is 0.278 as tall as it is long; and its
        /// half-width profile carries four waists.
        ///
        /// THE BRIEF'S BLEND WOULD HAVE ERASED THE SEGMENTS. Five spheres 0.275
        /// apart at radius 0.24 leave an 18% waist between them, and
        /// `Sdf.SmoothMin` at k = 0.12 fills most of it - what bible 1.2 calls
        /// segmented would have meshed as a smooth sausage. Spread to 0.28 apart
        /// with radii tapering 0.145 - 0.185 - 0.215 - 0.195 - 0.165, at blend
        /// 0.04, the four waists measure 15% to 21% below their own shoulders.
        ///
        /// AND THE MESH SMEARS THEM, WHICH IS WORTH WRITING DOWN RATHER THAN
        /// HIDING. Loam is 1.54 long, so at grid 20 the mesher's x cell is
        /// 0.091 - twice the depth of a 0.045 waist. The committed mesh keeps
        /// all four but at 3% to 9% instead of 15% to 21%. Resolving them costs
        /// grid 22, which meshes to 3296 triangles against a 2500 budget. So
        /// `Loam_IsSegmented_MeasuredOnTheRecipesOwnSurface` measures the FIELD,
        /// as `Vetch_CarriesAHeadLumpOnTheFront` already does for half its
        /// claim, and the segmentation is honestly a read on the card render
        /// rather than in the 40px silhouette.
        ///
        /// EVERY SPHERE'S CENTRE IS AT ITS OWN RADIUS, so each rests exactly on
        /// y = 0 and the mesh bottoms at +0.0000 - the tightest of the nine
        /// bodies against `DriftTests.EveryBody_RestsOnTheGroundPlane`.
        ///
        /// THE BONES CHAIN OUTWARD FROM THE MIDDLE, AND THE MIDDLE ONE IS
        /// `root`, NOT `seg2`. `CreatureMotion.Awake` does
        /// `transform.Find("root")` and falls back to the CREATURE transform
        /// when there is none - and then every frame overwrites the position
        /// `WaveView` just placed the creature at, teleporting it to the origin.
        /// So the chain is seg0 - seg1 - root - seg3 - seg4, rooted at the mass
        /// centre. rig_proof 4 item 10 asks what a socket does when the surface
        /// under it moves; the answer stated here is THE PART RIDES ONE SEGMENT,
        /// and sk_dorsal sits directly over the middle one. It is a rule, not a
        /// rig: `CreatureGenerator` parents every socket to the creature root
        /// rather than to a bone, so nothing in this task makes the socket
        /// literally follow `root`. Making it do so is a generator change, and
        /// this is a content task.
        public static readonly BodyRecipe Loam = new BodyRecipe
        {
            Id = "loam",
            Blend = 0.04f, Grid = 20, Padding = 0.14f,
            Bones = new[]
            {
                new BoneDef { Name = "root", Position = new Vector3(0f, 0.215f, 0f) },
                new BoneDef { Name = "seg1", Parent = "root", Position = new Vector3(-0.28f, 0.185f, 0f) },
                new BoneDef { Name = "seg0", Parent = "seg1", Position = new Vector3(-0.56f, 0.145f, 0f) },
                new BoneDef { Name = "seg3", Parent = "root", Position = new Vector3(0.28f, 0.195f, 0f) },
                new BoneDef { Name = "seg4", Parent = "seg3", Position = new Vector3(0.56f, 0.165f, 0f) },
            },
            Primitives = new[]
            {
                Primitive.Sphere(new Vector3(-0.56f, 0.145f, 0f), 0.145f, "seg0", 0.15f),
                Primitive.Sphere(new Vector3(-0.28f, 0.185f, 0f), 0.185f, "seg1", 0.15f),
                Primitive.Sphere(new Vector3(0f, 0.215f, 0f), 0.215f, "root", 0.15f),
                Primitive.Sphere(new Vector3(0.28f, 0.195f, 0f), 0.195f, "seg3", 0.15f),
                Primitive.Sphere(new Vector3(0.56f, 0.165f, 0f), 0.165f, "seg4", 0.15f),
                Primitive.Box(new Vector3(0.74f, 0.135f, 0f), new Vector3(0.10f, 0.135f, 0.125f), "seg4", 0.3f),
            },
            Sockets = new[]
            {
                new SocketDef { Name = Sockets.Dorsal, Position = new Vector3(0f, 0.430f, 0f), Euler = Vector3.zero, Scale = 0.6f },
                new SocketDef { Name = Sockets.Flank, Position = new Vector3(-0.28f, 0.12f, -0.174f), Euler = new Vector3(-90f, 0f, 0f), Scale = 0.5f },
                new SocketDef { Name = Sockets.Crown, Position = new Vector3(0.80f, 0.28f, 0f), Euler = new Vector3(0f, 0f, -35f), Scale = 0.4f },
            },
        };

        /// PALE - bible 1.2: "broad wing arc, small hanging body".
        ///
        /// A GLIDER THAT HAS TO STAND ON THE FLOOR. The brief hangs the body at
        /// y 0.55 with the wings above it, so nothing in that recipe touches
        /// y = 0 - and `DriftTests.EveryBody_RestsOnTheGroundPlane` holds every
        /// body to within 0.02 of it, because `WaveView` puts every creature on
        /// the lane at y = 0. The body is therefore a capsule that hangs from
        /// the wing roots DOWN to the ground. The arc is still above it and the
        /// read is unchanged; what changes is that Pale now exists on the lane
        /// instead of floating 0.39 above it.
        ///
        /// THE WINGS CANNOT BE FLATTENED CAPSULES. `Sdf` has a smooth union and
        /// nothing else - no intersection, no subtraction - so the brief's "r
        /// 0.09 flattened by a box each" is not expressible: a box added to a
        /// capsule makes it bigger, never thinner. A thin membrane in a
        /// union-only field is a thin BOX.
        ///
        /// FIVE PLATES PER SIDE, NOT THREE, AND THAT IS AN EYES-ON FINDING.
        /// Three plates rising 0.07 apart with 0.10 thickness overlap by only
        /// 0.035 where they meet, and the first bake showed exactly that: a
        /// chain of bricks rather than the ARC bible 1.2 asks for. Five plates
        /// rising 0.025 apart overlap by 0.07 - twice their own step - and the
        /// union closes into one tapering membrane. It costs 52 triangles.
        ///
        /// PINNED BY SPAN, NOT BY COMPONENT COUNT. Pale has no limbs, so a
        /// ground slice reads one island whatever the shape is. What bible 1.2
        /// names is measurable directly and is what `BodyShapeTests` asserts:
        /// the wing span is 3.65x the body's length, and the footprint low down
        /// is 14% of that span - broad arc, small hanging body.
        ///
        /// Grid 22, padding 0.12: 2100 triangles, and the 0.12-thick wing plates
        /// span 2.6 cells.
        ///
        /// ITS FLANK SOCKET STAYS AT y 0.15, AND BOTH REASONS FOR MOVING IT
        /// WERE MEASURED AND FOUND FALSE. It is not occluded: rendered in 3D
        /// with depth, 100% of every part's pixels survive at both of Pale's
        /// sockets, and the CARD cannot occlude at all because `CreatureBaker`
        /// stacks flat sprites part-over-body. Nor is it on the underside
        /// curve: the body capsule runs from y 0.17 to 0.44 at radius 0.17, so
        /// at y 0.15 it is 0.169 wide - 99.3% of its own maximum. Raising it to
        /// y 0.24 changes the part's pixel count by under 1% (610 to 612 on the
        /// carapace) and spends 0.09 of dorsal/flank clearance for it.
        public static readonly BodyRecipe Pale = new BodyRecipe
        {
            Id = "pale",
            Blend = 0.055f, Grid = 22, Padding = 0.12f,
            Bones = new[]
            {
                new BoneDef { Name = "root", Position = new Vector3(0f, 0.32f, 0f) },
                new BoneDef { Name = "wing_l", Parent = "root", Position = new Vector3(0f, 0.53f, 0.33f) },
                new BoneDef { Name = "wing_r", Parent = "root", Position = new Vector3(0f, 0.53f, -0.33f) },
                new BoneDef { Name = "head", Parent = "root", Position = new Vector3(0.18f, 0.44f, 0f) },
            },
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(0f, 0.17f, 0f), new Vector3(0f, 0.44f, 0f), 0.17f, "root", 0.15f),
                Primitive.Sphere(new Vector3(0.18f, 0.42f, 0f), 0.10f, "head", 0.3f),
                Primitive.Box(new Vector3(0.02f, 0.500f, 0.16f), new Vector3(0.21f, 0.060f, 0.17f), "wing_l", 0.25f),
                Primitive.Box(new Vector3(0.01f, 0.525f, 0.33f), new Vector3(0.19f, 0.058f, 0.15f), "wing_l", 0.25f),
                Primitive.Box(new Vector3(0f, 0.550f, 0.50f), new Vector3(0.16f, 0.055f, 0.14f), "wing_l", 0.25f),
                Primitive.Box(new Vector3(-0.01f, 0.575f, 0.66f), new Vector3(0.13f, 0.050f, 0.13f), "wing_l", 0.25f),
                Primitive.Box(new Vector3(-0.02f, 0.600f, 0.79f), new Vector3(0.10f, 0.045f, 0.10f), "wing_l", 0.25f),
                Primitive.Box(new Vector3(0.02f, 0.500f, -0.16f), new Vector3(0.21f, 0.060f, 0.17f), "wing_r", 0.25f),
                Primitive.Box(new Vector3(0.01f, 0.525f, -0.33f), new Vector3(0.19f, 0.058f, 0.15f), "wing_r", 0.25f),
                Primitive.Box(new Vector3(0f, 0.550f, -0.50f), new Vector3(0.16f, 0.055f, 0.14f), "wing_r", 0.25f),
                Primitive.Box(new Vector3(-0.01f, 0.575f, -0.66f), new Vector3(0.13f, 0.050f, 0.13f), "wing_r", 0.25f),
                Primitive.Box(new Vector3(-0.02f, 0.600f, -0.79f), new Vector3(0.10f, 0.045f, 0.10f), "wing_r", 0.25f),
            },
            Sockets = new[]
            {
                new SocketDef { Name = Sockets.Dorsal, Position = new Vector3(0f, 0.610f, 0f), Euler = Vector3.zero, Scale = 0.55f },
                new SocketDef { Name = Sockets.Flank, Position = new Vector3(-0.02f, 0.15f, -0.168f), Euler = new Vector3(-90f, 0f, 0f), Scale = 0.45f },
                new SocketDef { Name = Sockets.Crown, Position = new Vector3(0.26f, 0.46f, 0f), Euler = new Vector3(0f, 0f, -30f), Scale = 0.35f },
            },
        };
        public static IReadOnlyList<BodyRecipe> All { get; } = new List<BodyRecipe>
        { Vetch, Ember, Skitter, Hollow, Loam, Pale };

        public static BodyRecipe For(string species)
        {
            if (string.IsNullOrWhiteSpace(species)) return null;
            foreach (var r in All)
                if (string.Equals(r.Id, species.Trim(), StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }
    }
}
