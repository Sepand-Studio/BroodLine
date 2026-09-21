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

        // Ember, Skitter, Hollow, Loam, Pale: Task 15.
        public static IReadOnlyList<BodyRecipe> All { get; } = new List<BodyRecipe> { Vetch };

        public static BodyRecipe For(string species)
        {
            if (string.IsNullOrWhiteSpace(species)) return null;
            foreach (var r in All)
                if (string.Equals(r.Id, species.Trim(), StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }
    }
}
