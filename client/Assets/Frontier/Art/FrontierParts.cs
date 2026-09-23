using System;
using UnityEngine;

namespace Broodline.Frontier
{
    /// Shared trait geometry, independent of creature construction and mesh caching.
    public static class FrontierParts
    {
        public static bool Has(string trait) => trait == "cinder" || trait == "carapace" ||
            trait == "chill" || trait == "taunt" || trait == "splash";

        static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var c); return c; }
        static readonly Color Cream = Hex("#f4dfb9"), Frost = Hex("#c6cede"), Gold = Hex("#c99a49"), Coral = Hex("#e5867a");
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
                    for (int i = -1; i <= 1; i += 2) b.Sphere(new Vector3(i*.14f,.07f,0),new Vector3(.12f,.12f,.13f),Coral,10,6);
                    break;
                default: throw new ArgumentException("No proof part for " + trait);
            }
        }
    }
}
