using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Creatures
{
    /// GRIDS ARE MEASURED AGAINST THE 400-TRIANGLE BUDGET, not copied from the
    /// brief's uniform 16. A part's bounding box is far from cubic, and the
    /// mesher spends the same cell COUNT on every axis, so the short axis is
    /// oversampled and the triangle count runs high: at 16 the carapace meshes
    /// to 972 and the cinder crest to 884, both far over budget, and the taunt
    /// to 620 now that its banner has real thickness. Every grid here is the
    /// largest that fits in 400, measured on the recipe beside it.
    public static class PartRecipes
    {
        static Color C(string hex) => SpeciesColours.Parse(hex);

        /// SEATED, NOT HOVERING. The first pass authored this as flat boxes
        /// standing on y = 0.01 - a plane - and mounted it on a dome. A plane
        /// touches a dome at one point, so the plate met the shell at its
        /// centre and its corners hung 0.17 above the surface: the contact
        /// sheet showed a grey slab floating over Vetch's back. There is no
        /// subtraction in `Sdf`, so the underside cannot be hollowed to match
        /// the curve. The fix is the other way round: the plates run DEEP
        /// (0.32 tall, most of it below the mount point and inside the body)
        /// so that wherever the shell's surface falls away, the plate is
        /// already below it. Only the top 0.06-0.16 is ever visible, which is
        /// the stepped armour the silhouette wants.
        ///
        /// Negative y is deliberate and is not a violation of Recipe.cs's "+Y
        /// points out of the body surface" - +Y still does. A part is simply
        /// allowed to extend back INTO the body, and a part that seats on a
        /// curved surface has to.
        ///
        /// Grid 9: 384 triangles. 10 gives 444, over the 400 budget.
        public static readonly PartRecipe Carapace = new PartRecipe
        {
            Id = "carapace", Base = C("#5d93ab"), Under = C("#355d70"), Blend = 0.04f, Grid = 9,
            Primitives = new[]
            {
                Primitive.Box(new Vector3(0f, -0.1f, 0f), new Vector3(0.26f, 0.16f, 0.23f), "root"),   // the rim plate, top at +0.06
                Primitive.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.17f, 0.16f, 0.15f), "root"),  // the second step, top at +0.14
                Primitive.Sphere(new Vector3(0f, 0.06f, 0f), 0.1f, "root"),                            // the boss
            },
        };

        /// A BANNER THAT READS AT BOTH SOCKETS. The first pass authored the
        /// flag as a 0.28 x 0.20 plate 0.04 thick lying in the part's XY plane.
        /// At the dorsal socket that is a flag; at the FLANK socket, where
        /// Euler(-90,0,0) sends the part's +Z to world up, the same plate lies
        /// flat and shows the camera its 0.04 edge - which is what "too thin to
        /// read as a banner" was seeing. A plate thin along the part's +X
        /// instead is vertical in BOTH mounts, because the part's up is +Y at
        /// the dorsal socket and +Z at the flank and this plate spans both.
        /// It is also half again as large in each visible direction and twice
        /// as thick, so it survives the card's 40px.
        ///
        /// THE MAST IS SHORTER ON PURPOSE (0.46, was 0.50, and straight rather
        /// than raked). At the dorsal socket on the old taller body the pole
        /// ran off the top of its own 192px bake; from the new apex a 0.46 mast
        /// clears the frame by 9px.
        ///
        /// Grid 13: 384 triangles. The thicker banner also retires the pinch
        /// that made the old one need 16: 12 through 16 are all clean now.
        public static readonly PartRecipe Taunt = new PartRecipe
        {
            Id = "taunt", Base = C("#e5867a"), Under = C("#a8574d"), Blend = 0.05f, Grid = 13,
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.46f, 0f), 0.055f, "root"),  // the mast
                Primitive.Box(new Vector3(0f, 0.28f, -0.21f), new Vector3(0.04f, 0.155f, 0.19f), "root"), // the banner, thin along +X
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
