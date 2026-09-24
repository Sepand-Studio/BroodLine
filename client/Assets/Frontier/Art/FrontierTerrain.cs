using System;
using UnityEngine;

namespace Broodline.Frontier
{
    /// Extra lane dressing shared by deploy preview and the live wave.
    /// All geometry stays outside the movement path and the pocket centres.
    public static class FrontierTerrain
    {
        static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }

        public static void AddDetail(FrontierMesh b, int length, int[] pockets, bool habitat)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (habitat) return;
            var stone = Hex("#737e78");
            var stoneLight = Hex("#9aa69a");
            var moss = Hex("#638460");
            var leaf = Hex("#4c7d65");
            var stem = Hex("#806247");
            var sand = Hex("#d2bd94");
            var rng = new System.Random(20260924);

            // Broken rock shelves close the far edge of the stage. The tops
            // vary in height and sit beyond the existing tree belt at |z|≈4.
            int shelves = (int)Math.Ceiling((length + 4f) / 2.6f);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < shelves; i++)
                {
                    float x = -2f + i * 2.6f;
                    float rise = .38f + (i % 4) * .10f + (float)rng.NextDouble() * .10f;
                    float z = side * (5.42f + (i % 3) * .12f);
                    b.Sphere(new Vector3(x, rise * .55f - .02f, z),
                        new Vector3(1.35f, rise * .65f, .62f),
                        i % 3 == 0 ? stoneLight : stone, 12, 6);
                    b.Sphere(new Vector3(x - .18f, rise + .07f, z - side * .09f),
                        new Vector3(.91f, .06f, .40f), moss, 10, 4);
                    if (i % 3 == 1)
                        b.Box(new Vector3(x + .45f, rise + .13f, z + side * .13f),
                            new Vector3(.51f, .22f, .46f), stoneLight, i * 17f);
                }

            // Small brush at the path's outer shoulder adds a middle scale
            // between grass blades and trees without hiding a defender.
            for (int i = 0; i < length / 2; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                var p = new Vector3(.65f + i * 2.0f, .02f, side * (2.45f + (i % 3) * .18f));
                b.Cone(p, p + Vector3.up * .34f, .045f, .025f, stem, 6);
                b.Sphere(p + new Vector3(0, .30f, 0), new Vector3(.30f, .21f, .25f), leaf, 9, 6);
                b.Sphere(p + new Vector3(.20f, .22f, -.12f), new Vector3(.18f, .17f, .18f), moss, 8, 5);
            }

            // Loose stones belong to each authored pocket, and are offset
            // beyond its pad rim; they never change its position or collision.
            if (pockets != null)
                foreach (int pocket in pockets)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        float x = pocket + (j - 1) * .7f;
                        float z = 2.27f + (j % 2) * .14f;
                        b.Sphere(new Vector3(x, .035f, z), new Vector3(.16f, .07f, .11f),
                            j == 1 ? sand : stoneLight, 8, 4);
                    }
                }

            // Flat threshold slabs lead into the Ark without drawing a
            // second path through the combat field.
            for (int i = 0; i < 3; i++)
                b.Box(new Vector3(length - .15f + i * .47f, .018f, 0),
                    new Vector3(.39f, .05f, 1.24f), i == 1 ? stoneLight : sand);
        }
    }
}
