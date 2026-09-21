using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Creatures
{
    /// GRIDS ARE MEASURED AGAINST THE 400-TRIANGLE BUDGET, not copied from the
    /// brief's uniform 16. A part's bounding box is far from cubic, and the
    /// mesher spends the same cell COUNT on every axis, so the short axis is
    /// oversampled and the triangle count runs high: at 16 the carapace meshes
    /// to 972 and the cinder crest to 884, both over budget. The taunt, whose
    /// box is nearest to square, fits at 16.
    public static class PartRecipes
    {
        static Color C(string hex) => SpeciesColours.Parse(hex);

        /// Grid 10: 380 triangles. 11 gives 496.
        public static readonly PartRecipe Carapace = new PartRecipe
        {
            Id = "carapace", Base = C("#5d93ab"), Under = C("#355d70"), Blend = 0.08f, Grid = 10,
            Primitives = new[]
            {
                Primitive.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.05f, 0.3f), "root"),
                Primitive.Box(new Vector3(0f, 0.15f, 0f), new Vector3(0.22f, 0.05f, 0.2f), "root"),
                Primitive.Sphere(new Vector3(0f, 0.2f, 0f), 0.12f, "root"),
            },
        };

        /// Grid 16: 344 triangles. Left at the brief's value - the banner is
        /// only 0.04 thick and a coarser grid pinches it (15 and 12 both put
        /// two sheets through one cell); 16 resolves it cleanly.
        public static readonly PartRecipe Taunt = new PartRecipe
        {
            Id = "taunt", Base = C("#e5867a"), Under = C("#a8574d"), Blend = 0.05f, Grid = 16,
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(0f, 0f, 0f), new Vector3(-0.1f, 0.5f, 0f), 0.04f, "root"),   // the pole
                Primitive.Box(new Vector3(-0.22f, 0.42f, 0f), new Vector3(0.14f, 0.1f, 0.02f), "root"),   // the banner
            },
        };

        /// Grid 10: 348 triangles, and the three spikes stay three separate
        /// closed shells. 12 is both over budget (472) and wrong - it merges
        /// two of the spikes into one blob.
        public static readonly PartRecipe Cinder = new PartRecipe
        {
            Id = "cinder", Base = C("#e5867a"), Under = C("#a8574d"), Blend = 0.05f, Grid = 10,
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(-0.22f, 0f, 0f), new Vector3(-0.26f, 0.32f, 0f), 0.07f, "root"),
                Primitive.Capsule(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.44f, 0f), 0.08f, "root"),
                Primitive.Capsule(new Vector3(0.22f, 0f, 0f), new Vector3(0.26f, 0.32f, 0f), 0.07f, "root"),
            },
        };

        // The other nine: Task 15.
        public static IReadOnlyList<PartRecipe> All { get; } = new List<PartRecipe> { Carapace, Taunt, Cinder };

        public static PartRecipe For(string trait)
        {
            if (string.IsNullOrWhiteSpace(trait)) return null;
            foreach (var p in All)
                if (string.Equals(p.Id, trait.Trim(), StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }
    }
}
