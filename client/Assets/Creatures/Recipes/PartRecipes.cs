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
        /// A PART IS ITS OWNER SPECIES' COLOUR AT 0.65 OF ITS VALUE, DERIVED
        /// HERE RATHER THAN TYPED PER PART - AND THE RULE THAT SHIPPED FIRST
        /// WAS WRONG BY A MEASURABLE 1.00.
        ///
        /// "Colour comes from the species that owns the trait" is true and
        /// insufficient: applied literally against `SpeciesColours`, TEN of the
        /// twelve parts got a base hex BYTE-IDENTICAL to the body they mount on
        /// for base stock. Measured through `Creature.shader` over all 144
        /// body x part x socket combinations, `pale screen@sk_flank` came out
        /// at deltaE 0.93 and `ember splash@sk_flank` at 1.98 - against a
        /// median of 47 for cross-species pairs. bible 1.2 records the
        /// palette's worst colour-blind pair as deltaE 7.2 and calls that bad
        /// enough to change a species colour over. These were worse.
        ///
        /// bible 10.4 predicted it: "Pale specifically will need its own value
        /// structure, an outline, or a darker slot behind it. Hue alone will
        /// not carry it" - and calls trait visibility "the single most
        /// important functional requirement in the art direction". The carapace
        /// was the only part that had a deliberate value step from its owner,
        /// and it was the BEST of the twelve rather than the exception it
        /// looked like.
        ///
        /// BOTH NUMBERS ARE MEASURED, NEITHER IS CHOSEN. Sweeping value against
        /// the part rim over all 144 combinations: at full value the floor is
        /// deltaE 0.95 and no rim setting rescues it, because there is nothing
        /// to amplify when two colours are equal - a rim change ALONE would
        /// have fixed nothing. 0.85 of value reaches 7.7, 0.75 reaches 12.5,
        /// 0.70 reaches 15.6. 0.68 predicts 17.46 and the bake measures 17.18,
        /// which is where bible 1.2's own accepted number sits: the 17.3 the
        /// Vetch/Pale pair was moved TO when it was the palette's worst. This
        /// stops at the first value that clears the bar the project already
        /// set, because darker keeps eating the species hue that carries a
        /// part's provenance.
        ///
        /// AND THE SATURATION GOES UP, BECAUSE THE MASCOT IS A SPEC. Dropping
        /// value alone took Ember's parts to #95574f. The phrase "teal dome
        /// plus three coral spikes" is the design handoff's, at
        /// `specs/Designs/design_handoff_broodline/README.md:506`; the bible
        /// states the same shape WITHOUT the colour words, at
        /// `specs/broodline_bible.md:1114` - "a dome plus three spikes". The
        /// colour constraint is still the bible's, just derived rather than
        /// quoted: it pins the crest to EMBER, and Ember is coral in the
        /// palette. Cited apart because this project grades itself on
        /// citations, and one of these is a quotation while the other is an
        /// inference. Either way the spikes must read coral, and at #95574f
        /// they are brown. Value 0.68 with
        /// saturation x1.40 puts them at #9c4136, still plainly coral, and
        /// measures 17.18 against 16.71 for the duller version. Chroma is free
        /// here: deltaE counts it, and the spec asks for it.
        ///
        /// The residual worst pair is now `vetch chill@sk_flank` - Pale's grey
        /// parts on Vetch's teal, a CROSS-species pairing. Every own-species
        /// case has been lifted above it, which is the shape of a fixed
        /// problem. `PartVisibilityTests` is the standing gate, floor deltaE 8.
        public const float PartValue = 0.68f;
        public const float PartSaturation = 1.40f;

        static Color OwnedBase(string owner) => Scale(SpeciesColours.For(owner).Base);
        static Color OwnedUnder(string owner) => Scale(SpeciesColours.For(owner).Under);

        static Color Scale(Color c)
        {
            Color.RGBToHSV(c, out var h, out var sat, out var val);
            return Color.HSVToRGB(h, Mathf.Clamp01(sat * PartSaturation), val * PartValue);
        }

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
            Id = "carapace", Base = OwnedBase("vetch"), Under = OwnedUnder("vetch"), Blend = 0.04f, Grid = 9,
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
            Id = "taunt", Base = OwnedBase("vetch"), Under = OwnedUnder("vetch"), Blend = 0.05f, Grid = 13,
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
            Id = "cinder", Base = OwnedBase("ember"), Under = OwnedUnder("ember"), Blend = 0.05f, Grid = 10,
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(-0.22f, 0f, 0f), new Vector3(-0.26f, 0.32f, 0f), 0.07f, "root"),
                Primitive.Capsule(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.44f, 0f), 0.08f, "root"),
                Primitive.Capsule(new Vector3(0.22f, 0f, 0f), new Vector3(0.26f, 0.32f, 0f), 0.07f, "root"),
            },
        };

        /// THE NINE THAT TASK 15 ADDED.
        ///
        /// THE UNDERSIDE IS NOT WHERE A PART'S SEPARATION COMES FROM, and round
        /// one spent its reasoning there before measuring. `Creature.shader`
        /// does `lerp(_BaseColor, _UnderColor, saturate(-n.y))` on a WORLD-space
        /// normal, and from the one fixed bake camera only 10.5% (dorsal) and
        /// 14.6% (flank) of a part's camera-facing vertices point downward at
        /// all. Darkening the carapace's `Under` end to end, #355d70 all the
        /// way to #16303f, moved its rendered read from 1.080 to 1.089 at the
        /// flank and not at all at the dorsal. The `Under` is still authored a
        /// step darker - it is what gives a part its own value structure where
        /// it IS visible - but it is not the lever, and the class comment above
        /// is.
        ///
        /// GRIDS ARE MEASURED, NEVER COPIED, for the reason the class comment
        /// above already gives - and the second reason is the manifold. Every
        /// thin feature here was checked against two cells: sprint's fins
        /// pinched at radius 0.065 (12 doubled edges) and close at 0.06, and
        /// pierce's three spines pinched when their bases were 0.10 apart and
        /// close at 0.16, because a GAP thinner than two cells folds the
        /// surface exactly as a solid thinner than two cells does.
        public static readonly PartRecipe Splash = new PartRecipe
        {
            // SEATED DEEP, for the reason `Carapace` above records: a plate
            // that stops at its mount point meets a curved body at one point
            // and hangs off it everywhere else. The plate runs from -0.19 to
            // +0.05 so only its top is ever outside the hide.
            Id = "splash", Base = OwnedBase("ember"), Under = OwnedUnder("ember"), Blend = 0.05f, Grid = 10, Padding = 0.12f,
            Primitives = new[]
            {
                Primitive.Box(new Vector3(0f, -0.07f, 0f), new Vector3(0.18f, 0.12f, 0.18f), "root"),
                Primitive.Sphere(new Vector3(0.185f, 0.03f, 0f), 0.075f, "root"),
                Primitive.Sphere(new Vector3(-0.092f, 0.03f, 0.16f), 0.075f, "root"),
                Primitive.Sphere(new Vector3(-0.092f, 0.03f, -0.16f), 0.075f, "root"),
            },
        };

        public static readonly PartRecipe Sprint = new PartRecipe
        {
            Id = "sprint", Base = OwnedBase("skitter"), Under = OwnedUnder("skitter"), Blend = 0.045f, Grid = 11, Padding = 0.10f,
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(0.05f, -0.04f, 0.09f), new Vector3(-0.19f, 0.27f, 0.115f), 0.06f, "root"),
                Primitive.Capsule(new Vector3(0.05f, -0.04f, -0.09f), new Vector3(-0.19f, 0.27f, -0.115f), 0.06f, "root"),
            },
        };

        /// BLEND 0.05, NOT THE BRIEF'S 0.1. A clutch is five eggs, and 0.1 is
        /// larger than the 0.09 radius they carry - `Sdf.SmoothMin` would have
        /// welded them into one lump, which is the opposite of a clutch.
        public static readonly PartRecipe Litter = new PartRecipe
        {
            Id = "litter", Base = OwnedBase("skitter"), Under = OwnedUnder("skitter"), Blend = 0.05f, Grid = 11, Padding = 0.12f,
            Primitives = new[]
            {
                Primitive.Sphere(new Vector3(0f, 0.13f, 0f), 0.10f, "root"),
                Primitive.Sphere(new Vector3(0.13f, 0.02f, 0.06f), 0.09f, "root"),
                Primitive.Sphere(new Vector3(-0.13f, 0.02f, 0.06f), 0.09f, "root"),
                Primitive.Sphere(new Vector3(0.05f, 0.02f, -0.13f), 0.09f, "root"),
                Primitive.Sphere(new Vector3(-0.10f, 0.04f, -0.09f), 0.085f, "root"),
            },
        };

        /// SHORTER AND THICKER THAN THE BRIEF'S (0.35, 0.55) AT RADIUS 0.045.
        /// A lance that long sits in a bounding box 0.72 tall, and at the grid
        /// that fits 400 triangles the cell is 0.068 - a 0.09-thick lance is
        /// 1.3 cells and the mesher folds it. At (0.24, 0.37) and radius 0.07
        /// it is 2.1 cells and closes, at 388 triangles, and 0.48 tall is
        /// still the longest diagonal reach of the twelve.
        public static readonly PartRecipe Reach = new PartRecipe
        {
            Id = "reach", Base = OwnedBase("hollow"), Under = OwnedUnder("hollow"), Blend = 0.05f, Grid = 11, Padding = 0.08f,
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(0f, 0f, 0f), new Vector3(0.24f, 0.37f, 0f), 0.07f, "root"),
                Primitive.Sphere(new Vector3(0.27f, 0.41f, 0f), 0.078f, "root"),
            },
        };

        public static readonly PartRecipe Pierce = new PartRecipe
        {
            Id = "pierce", Base = OwnedBase("hollow"), Under = OwnedUnder("hollow"), Blend = 0.05f, Grid = 11, Padding = 0.12f,
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(0.15f, 0f, 0f), new Vector3(0f, 0.42f, 0f), 0.05f, "root"),
                Primitive.Capsule(new Vector3(-0.075f, 0f, 0.13f), new Vector3(0f, 0.42f, 0f), 0.05f, "root"),
                Primitive.Capsule(new Vector3(-0.075f, 0f, -0.13f), new Vector3(0f, 0.42f, 0f), 0.05f, "root"),
            },
        };

        public static readonly PartRecipe Regrow = new PartRecipe
        {
            Id = "regrow", Base = OwnedBase("loam"), Under = OwnedUnder("loam"), Blend = 0.045f, Grid = 11, Padding = 0.12f,
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(0f, -0.02f, 0f), new Vector3(0f, 0.20f, 0f), 0.06f, "root"),
                Primitive.Box(new Vector3(0.13f, 0.23f, 0f), new Vector3(0.12f, 0.045f, 0.07f), "root"),
                Primitive.Box(new Vector3(-0.13f, 0.23f, 0f), new Vector3(0.12f, 0.045f, 0.07f), "root"),
            },
        };

        public static readonly PartRecipe Burrow = new PartRecipe
        {
            Id = "burrow", Base = OwnedBase("loam"), Under = OwnedUnder("loam"), Blend = 0.045f, Grid = 10, Padding = 0.12f,
            Primitives = new[]
            {
                Primitive.Box(new Vector3(-0.03f, 0.04f, 0f), new Vector3(0.16f, 0.11f, 0.13f), "root"),
                Primitive.Box(new Vector3(0.10f, 0.15f, 0f), new Vector3(0.11f, 0.07f, 0.095f), "root"),
                Primitive.Box(new Vector3(0.19f, 0.23f, 0f), new Vector3(0.07f, 0.05f, 0.065f), "root"),
                Primitive.Sphere(new Vector3(0.26f, 0.28f, 0f), 0.05f, "root"),
            },
        };

        /// A FAN OF RODS, NOT THE BRIEF'S FOUR BOXES ROTATED 0/25/50/75
        /// DEGREES ABOUT X. `Sdf.Box` is axis-aligned - the field has no
        /// rotation at all - so a rotated box is not something this pipeline
        /// can express. A capsule CAN point anywhere, so the fan is four rods
        /// splayed in the part's YZ plane from one hub, with a web box between
        /// them; that reads as a fan at 40px and is expressible.
        public static readonly PartRecipe Screen = new PartRecipe
        {
            Id = "screen", Base = OwnedBase("pale"), Under = OwnedUnder("pale"), Blend = 0.05f, Grid = 11, Padding = 0.12f,
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0.34f, 0.11f), 0.05f, "root"),
                Primitive.Capsule(new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0.34f, -0.11f), 0.05f, "root"),
                Primitive.Capsule(new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0.22f, 0.27f), 0.05f, "root"),
                Primitive.Capsule(new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0.22f, -0.27f), 0.05f, "root"),
                Primitive.Box(new Vector3(0f, 0.13f, 0f), new Vector3(0.04f, 0.11f, 0.20f), "root"),
            },
        };

        /// THREE SHARDS, NOT THE BRIEF'S TWO BOXES ROTATED 45 DEGREES ABOUT Y,
        /// for the same reason `Screen` above gives. A crystal reads from
        /// having several shards of different heights, which axis-aligned
        /// boxes give; the 45-degree rotation was carrying the "crystal" read
        /// on its own and the field cannot supply it. Blend 0.03, the smallest
        /// here, so the facets stay facets.
        public static readonly PartRecipe Chill = new PartRecipe
        {
            Id = "chill", Base = OwnedBase("pale"), Under = OwnedUnder("pale"), Blend = 0.03f, Grid = 11, Padding = 0.12f,
            Primitives = new[]
            {
                Primitive.Box(new Vector3(0f, 0.17f, 0f), new Vector3(0.075f, 0.20f, 0.075f), "root"),
                Primitive.Box(new Vector3(0.10f, 0.10f, -0.06f), new Vector3(0.055f, 0.13f, 0.055f), "root"),
                Primitive.Box(new Vector3(-0.08f, 0.09f, 0.07f), new Vector3(0.05f, 0.11f, 0.05f), "root"),
            },
        };

        public static IReadOnlyList<PartRecipe> All { get; } = new List<PartRecipe>
        {
            Carapace, Taunt, Cinder, Splash, Sprint, Litter,
            Reach, Pierce, Regrow, Burrow, Screen, Chill,
        };

        public static PartRecipe For(string trait)
        {
            if (string.IsNullOrWhiteSpace(trait)) return null;
            foreach (var p in All)
                if (string.Equals(p.Id, trait.Trim(), StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }
    }
}
