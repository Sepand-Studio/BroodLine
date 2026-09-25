using System;
using UnityEngine;

namespace Broodline.Frontier
{
    /// Shared trait geometry, independent of creature construction and mesh caching.
    public static class FrontierParts
    {
        public static bool Has(string trait) => trait == "cinder" || trait == "carapace" ||
            trait == "chill" || trait == "taunt" || trait == "splash" ||
            trait == "sprint" || trait == "litter" || trait == "reach" ||
            trait == "pierce" || trait == "regrow" || trait == "burrow" || trait == "screen";

        static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var c); return c; }
        static readonly Color Cream = Hex("#f4dfb9"), Frost = Hex("#c6cede"), Gold = Hex("#c99a49"),
            SplashCoral = Hex("#8f4550");
        public static void Build(FrontierMesh b, string trait)
        {
            switch (trait)
            {
                case "cinder":
                    b.Polish = .42f;
                    for (int i = 0; i < 3; i++)
                    {
                        var root = new Vector3(-.31f+i*.30f, -.045f, 0);
                        b.Sphere(root, new Vector3(.16f,.07f,.14f), Hex("#794b3e"), 10, 6);
                        var middle = root + new Vector3(-.055f,.22f+i*.045f,0);
                        b.Cone(root, middle, .14f, .072f, Hex("#cf6c37"), 8);
                        b.Cone(middle, middle + new Vector3(-.085f,.19f+i*.035f,0), .072f, .009f, Hex("#f7b65c"), 8);
                    }
                    break;
                case "carapace":
                    b.Polish = .30f;
                    for (int i = 0; i < 3; i++)
                        b.ShellPlate(new Vector3((i-1)*.26f,0,0), new Vector3(.18f,.085f,.26f),
                            new[] { new Vector2(.92f,0), new Vector2(.44f,-.81f), new Vector2(-.44f,-.81f),
                                new Vector2(-.92f,0), new Vector2(-.44f,.81f), new Vector2(.44f,.81f) },
                            i == 1 ? Cream : Hex("#adc1b4"));
                    break;
                case "chill":
                    for (int i = 0; i < 3; i++) b.Cone(new Vector3((i-1)*.18f,0,0), new Vector3((i-1)*.23f,.25f-Mathf.Abs(i-1)*.08f,0), .09f,.005f,Frost,5);
                    break;
                case "taunt":
                    for (int i = -1; i <= 1; i += 2) b.Sphere(new Vector3(i*.13f,.09f,0),new Vector3(.11f,.15f,.065f),Gold,10,6);
                    break;
                case "splash":
                    // Splash belongs to coral Ember, so the body hue itself
                    // disappears on the creature that most often carries it.
                    // Keep the family colour but use Ember's deep value step.
                    for (int i = -1; i <= 1; i += 2) b.Sphere(new Vector3(i*.14f,.07f,0),new Vector3(.12f,.12f,.13f),SplashCoral,10,6);
                    break;
                case "sprint":
                    // Twin swept fins keep the speed read on both body sizes.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var root = new Vector3(.08f, -.05f, side * .12f);
                        var elbow = new Vector3(-.10f, .19f, side * .16f);
                        b.Cone(root, elbow, .085f, .065f, Hex("#7a5b12"), 9);
                        b.Cone(elbow, new Vector3(-.32f, .31f, side * .19f), .065f, .008f, Gold, 9);
                    }
                    break;
                case "litter":
                    b.Polish = .28f;
                    b.Sphere(new Vector3(0f, -.04f, 0f), new Vector3(.22f, .07f, .20f), Hex("#8b8057"), 12, 6);
                    foreach (var center in new[] {
                        new Vector3(0f,.10f,0f), new Vector3(.13f,.035f,.08f),
                        new Vector3(-.13f,.035f,.08f), new Vector3(.07f,.035f,-.12f),
                        new Vector3(-.09f,.045f,-.11f) })
                        b.Sphere(center, new Vector3(.075f,.105f,.075f), Cream, 10, 7);
                    break;
                case "reach":
                    b.Cone(new Vector3(-.09f,-.05f,0f), new Vector3(.15f,.28f,0f),
                        .115f, .055f, Hex("#5b45ae"), 10);
                    b.Cone(new Vector3(.15f,.28f,0f), new Vector3(.33f,.44f,0f),
                        .055f, .008f, Gold, 9);
                    b.Sphere(new Vector3(.16f,.28f,0f), new Vector3(.08f,.08f,.08f), Hex("#8a6fd6"), 10, 6);
                    break;
                case "pierce":
                    for (int i = -1; i <= 1; i++)
                    {
                        var basePoint = new Vector3(i * .13f, -.045f, i * .025f);
                        var tip = new Vector3(i * .08f, i == 0 ? .43f : .33f, i * .045f);
                        b.Cone(basePoint, tip, .075f, .004f, i == 0 ? Frost : Hex("#8a99ad"), 8);
                    }
                    break;
                case "regrow":
                    b.Cone(new Vector3(0f,-.065f,0f), new Vector3(0f,.22f,0f),
                        .075f, .035f, Hex("#4f8664"), 10);
                    for (int side = -1; side <= 1; side += 2)
                        b.Sphere(new Vector3(side * .13f,.23f,0f), new Vector3(.15f,.045f,.075f),
                            side < 0 ? Hex("#72aa74") : Hex("#a1c584"), 12, 6,
                            Quaternion.Euler(0f, 0f, side * 22f));
                    break;
                case "burrow":
                    b.Polish = .15f;
                    b.Box(new Vector3(-.04f,-.02f,0f), new Vector3(.34f,.13f,.28f), Hex("#716b59"));
                    b.Box(new Vector3(.12f,.10f,0f), new Vector3(.20f,.11f,.21f), Hex("#9a8a69"));
                    b.Cone(new Vector3(.18f,.14f,0f), new Vector3(.35f,.30f,0f),
                        .10f, .012f, Hex("#c3aa79"), 8);
                    break;
                case "screen":
                    b.Polish = .22f;
                    b.Sphere(new Vector3(0f,-.015f,0f), new Vector3(.12f,.08f,.14f), Hex("#6a8392"), 10, 6);
                    foreach (var tip in new[] {
                        new Vector3(0f,.36f,-.25f), new Vector3(0f,.43f,-.09f),
                        new Vector3(0f,.43f,.09f), new Vector3(0f,.36f,.25f) })
                    {
                        b.Cone(new Vector3(0f,0f,0f), tip, .045f, .012f, Frost, 8);
                        b.Sphere(tip, new Vector3(.045f,.045f,.045f), Cream, 8, 5);
                    }
                    break;
                default: throw new ArgumentException("No proof part for " + trait);
            }
        }
    }
}
