using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Creatures
{
    public static class SpeciesRecipes
    {
        /// GRID = 20, NOT THE BRIEF'S 24. Measured, not guessed: naive surface
        /// nets emit two triangles per surface cell, so the count scales with
        /// grid squared. This recipe meshes to 3336 triangles at 24 and 2748
        /// at 22 - both over the 2500 budget the erratum fixes. 20 lands at
        /// 2352 with 6% of headroom, and the cell is still 0.072-0.089 across,
        /// about a third of a leg's diameter, so the silhouette still reads
        /// smooth. If a later recipe needs more detail, buy it with a smaller
        /// padding, not a bigger grid.
        ///
        /// SEATED ON y = 0, LITERALLY. Every y here is the brief's value plus
        /// 0.10, which is what it took to lift the meshed body out of the
        /// ground: the dome bottomed at -0.10 and the legs ended at y = 0 with
        /// a 0.11 radius, so the brief's numbers sank the creature by 7% of
        /// its own length below the plane the lane stands it on. Shifting the
        /// whole recipe - primitives, bones AND sockets - translates the mesh
        /// exactly and costs no triangles (still 2352); it now meshes to
        /// y = +0.00015. `DriftTests.EveryBody_RestsOnTheGroundPlane` holds
        /// every future body to it. The convention in Recipe.cs stays true of
        /// what is written here, which is the point: nothing downstream has to
        /// know about a hidden offset.
        public static readonly BodyRecipe Vetch = new BodyRecipe
        {
            Id = "vetch",
            Blend = 0.22f, Grid = 20, Padding = 0.2f,
            Bones = new[]
            {
                new BoneDef { Name = "root", Position = new Vector3(0f, 0.42f, 0f) },
                new BoneDef { Name = "head", Parent = "root", Position = new Vector3(0.55f, 0.44f, 0f) },
                new BoneDef { Name = "leg_fl", Parent = "root", Position = new Vector3(0.32f, 0.32f, 0.3f) },
                new BoneDef { Name = "leg_fr", Parent = "root", Position = new Vector3(0.32f, 0.32f, -0.3f) },
                new BoneDef { Name = "leg_bl", Parent = "root", Position = new Vector3(-0.32f, 0.32f, 0.3f) },
                new BoneDef { Name = "leg_br", Parent = "root", Position = new Vector3(-0.32f, 0.32f, -0.3f) },
            },
            Primitives = new[]
            {
                Primitive.Sphere(new Vector3(0f, 0.52f, 0f), 0.52f, "root", 0.15f),                          // the dome
                Primitive.Box(new Vector3(0f, 0.38f, 0f), new Vector3(0.58f, 0.16f, 0.46f), "root", 0.15f),  // the low belly that flattens it
                Primitive.Sphere(new Vector3(0.58f, 0.42f, 0f), 0.22f, "head", 0.35f),                        // no neck: the head is a lump on the front
                Primitive.Capsule(new Vector3(0.32f, 0.36f, 0.3f), new Vector3(0.36f, 0.1f, 0.34f), 0.11f, "leg_fl", 0.25f),
                Primitive.Capsule(new Vector3(0.32f, 0.36f, -0.3f), new Vector3(0.36f, 0.1f, -0.34f), 0.11f, "leg_fr", 0.25f),
                Primitive.Capsule(new Vector3(-0.32f, 0.36f, 0.3f), new Vector3(-0.36f, 0.1f, 0.34f), 0.11f, "leg_bl", 0.25f),
                Primitive.Capsule(new Vector3(-0.32f, 0.36f, -0.3f), new Vector3(-0.36f, 0.1f, -0.34f), 0.11f, "leg_br", 0.25f),
            },
            // THE FLANK IS THE RIGHT FLANK, SO IT FACES -Z. Recipe.cs makes +Z
            // the creature's left and a part's +Y the direction out of the
            // body, and Quaternion.Euler(90,0,0) sends +Y to +Z - which at
            // z = -0.5 points INTO the body. The brief's position and its
            // rotation disagreed; the position is the deliberate authoring
            // choice (it decides where the part sits in the silhouette) and
            // the rotation is the mechanical one, so the rotation gave way:
            // Euler(-90,0,0) sends +Y to -Z, outward, and leaves the part's
            // +X still forward. Mirroring to z = +0.5 would have worked too,
            // but it moves the part to the other side of the creature, which
            // is a visible change nobody asked for.
            // `RecipeTests.EverySocket_FacesOutOfTheBody` pins it.
            Sockets = new[]
            {
                new SocketDef { Name = Sockets.Dorsal, Position = new Vector3(-0.05f, 1.02f, 0f), Euler = Vector3.zero, Scale = 1f },
                new SocketDef { Name = Sockets.Flank, Position = new Vector3(0f, 0.55f, -0.5f), Euler = new Vector3(-90f, 0f, 0f), Scale = 0.8f },
                new SocketDef { Name = Sockets.Crown, Position = new Vector3(0.66f, 0.65f, 0f), Euler = new Vector3(0f, 0f, -35f), Scale = 0.55f },
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
