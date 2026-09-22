using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Creatures
{
    /// Three of the eight, from `Character Bible.dc.html`'s shape column, in
    /// the register bible 10.5 asks for: angular, dark, never cute. Boxes only
    /// and blend 0.02, so every edge stays an edge.
    ///
    /// `Sdf.Box` IS AXIS-ALIGNED - there is no rotation anywhere in the field -
    /// so Courser's "forward-raked diamond" and Skirmisher's "angular wedge"
    /// are stacks of axis-aligned boxes whose facets step. Stepped facets read
    /// angular, which is the whole of what the register asks for.
    ///
    /// CHECKED FOR TASK 12b'S DEFECT, BECAUSE NOTHING ELSE DOES. Raiders are
    /// outside `SilhouetteTests` and outside `BodyShapeTests`' species claims,
    /// and a body that reaches the ground between its own limbs reads as a
    /// blob here exactly as it did on Vetch. Only Skirmisher has limbs in the
    /// character bible's shape column: its wedge bottoms at 0.265 and the two
    /// blade legs stand in that band, so a slice at 0.12 finds two of them.
    /// Courser and Lash have no limbs, so their hulls reach the ground on
    /// purpose and read one - which is correct for them and would not be for
    /// Skirmisher. `BodyShapeTests.EveryRaider_ShowsTheLimbsItsShapeColumnNames`
    /// is the standing form of that check.
    ///
    /// LASH'S WHIP IS TWO OVERLAPPING BARS, NOT A TAPERING CHAIN. Three boxes
    /// stepping up and back left 0.005 gaps and 0.02 overlaps between them,
    /// and a gap thinner than a cell folds the surface the same way a solid
    /// thinner than a cell does - measured, 12 doubled edges. One long bar
    /// under one thicker root has no junctions to be thin at.
    public static class RaiderRecipes
    {
        public static readonly BodyRecipe Courser = new BodyRecipe
        {
            Id = "courser", Raider = true,
            Blend = 0.02f, Grid = 18, Padding = 0.12f,
            Bones = new[]
            {
                new BoneDef { Name = "root", Position = new Vector3(0.04f, 0.22f, 0f) },
                new BoneDef { Name = "prow", Parent = "root", Position = new Vector3(0.42f, 0.25f, 0f) },
            },
            Primitives = new[]
            {
                Primitive.Box(new Vector3(0.08f, 0.20f, 0f), new Vector3(0.30f, 0.10f, 0.14f), "root"),
                Primitive.Box(new Vector3(0.04f, 0.33f, 0f), new Vector3(0.20f, 0.07f, 0.10f), "root"),
                Primitive.Box(new Vector3(0.14f, 0.09f, 0f), new Vector3(0.20f, 0.09f, 0.10f), "root"),
                Primitive.Box(new Vector3(0.42f, 0.25f, 0f), new Vector3(0.12f, 0.05f, 0.05f), "prow"),
                Primitive.Box(new Vector3(-0.36f, 0.28f, 0.12f), new Vector3(0.16f, 0.09f, 0.035f), "root"),
                Primitive.Box(new Vector3(-0.40f, 0.30f, -0.12f), new Vector3(0.18f, 0.08f, 0.035f), "root"),
            },
            Sockets = new[]
            {
                new SocketDef { Name = Sockets.Kit, Position = new Vector3(0.04f, 0.41f, 0f), Euler = Vector3.zero, Scale = 0.6f },
            },
        };

        public static readonly BodyRecipe Lash = new BodyRecipe
        {
            Id = "lash", Raider = true,
            Blend = 0.02f, Grid = 20, Padding = 0.12f,
            Bones = new[]
            {
                new BoneDef { Name = "root", Position = new Vector3(0.08f, 0.20f, 0f) },
                new BoneDef { Name = "head", Parent = "root", Position = new Vector3(0.36f, 0.26f, 0f) },
                new BoneDef { Name = "whip", Parent = "root", Position = new Vector3(-0.40f, 0.27f, 0f) },
            },
            Primitives = new[]
            {
                Primitive.Box(new Vector3(0.08f, 0.18f, 0f), new Vector3(0.26f, 0.18f, 0.15f), "root"),
                Primitive.Box(new Vector3(0.34f, 0.25f, 0f), new Vector3(0.13f, 0.09f, 0.09f), "head"),
                Primitive.Box(new Vector3(0.50f, 0.27f, 0f), new Vector3(0.06f, 0.05f, 0.05f), "head"),
                Primitive.Box(new Vector3(-0.16f, 0.25f, 0f), new Vector3(0.16f, 0.075f, 0.06f), "whip"),
                Primitive.Box(new Vector3(-0.44f, 0.27f, 0f), new Vector3(0.40f, 0.05f, 0.042f), "whip"),
            },
            Sockets = new[]
            {
                new SocketDef { Name = Sockets.Kit, Position = new Vector3(0.08f, 0.37f, 0f), Euler = Vector3.zero, Scale = 0.6f },
            },
        };

        public static readonly BodyRecipe Skirmisher = new BodyRecipe
        {
            Id = "skirmisher", Raider = true,
            Blend = 0.02f, Grid = 18, Padding = 0.12f,
            Bones = new[]
            {
                new BoneDef { Name = "root", Position = new Vector3(0f, 0.40f, 0f) },
                new BoneDef { Name = "leg_l", Parent = "root", Position = new Vector3(0.04f, 0.20f, 0.13f) },
                new BoneDef { Name = "leg_r", Parent = "root", Position = new Vector3(0.04f, 0.20f, -0.13f) },
            },
            Primitives = new[]
            {
                Primitive.Box(new Vector3(0f, 0.40f, 0f), new Vector3(0.24f, 0.13f, 0.14f), "root"),
                Primitive.Box(new Vector3(0.30f, 0.42f, 0f), new Vector3(0.12f, 0.07f, 0.075f), "root"),
                Primitive.Box(new Vector3(0.04f, 0.14f, 0.13f), new Vector3(0.055f, 0.14f, 0.045f), "leg_l"),
                Primitive.Box(new Vector3(0.04f, 0.14f, -0.13f), new Vector3(0.055f, 0.14f, 0.045f), "leg_r"),
            },
            Sockets = new[]
            {
                new SocketDef { Name = Sockets.Kit, Position = new Vector3(0f, 0.55f, 0f), Euler = Vector3.zero, Scale = 0.6f },
            },
        };

        public static IReadOnlyList<BodyRecipe> All { get; } = new List<BodyRecipe>
        { Courser, Lash, Skirmisher };

        public static BodyRecipe For(string raiderType)
        {
            if (string.IsNullOrWhiteSpace(raiderType)) return null;
            foreach (var r in All)
                if (string.Equals(r.Id, raiderType.Trim(), StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }
    }
}
