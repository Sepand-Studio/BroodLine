using System;
using UnityEngine;

namespace Broodline.Frontier
{
    /// Four shared body families with variant silhouettes. Only the first
    /// three ids are spawned by the current simulation; the others are art
    /// definitions for the authored campaign roster, not new combat rules.
    public static class FrontierRaiders
    {
        static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }
        static readonly Color Hide = Hex("#414556");
        static readonly Color Shadow = Hex("#292f40");
        static readonly Color Ridge = Hex("#778495");
        static readonly Color Heat = Hex("#e57b57");
        static readonly Color Bone = Hex("#b3b7aa");

        public static readonly string[] Ids = { "skirmisher", "courser", "lash", "drift",
            "breaker", "bulwark", "delver", "brood", "sunder" };

        public static float Scale(string id) => id == "skirmisher" ? .62f : id == "courser" ? 1.2f
            : id == "sunder" ? 1.5f : 1f;

        public static void Build(FrontierMesh mesh, string id)
        {
            switch (id)
            {
                case "skirmisher": Runner(mesh, Scale(id), false); break;
                case "courser": Runner(mesh, Scale(id), true); break;
                case "lash": Lifter(mesh, false); break;
                case "drift": Lifter(mesh, true); break;
                case "breaker": Hauler(mesh, false, true, false); break;
                case "bulwark": Hauler(mesh, false, false, true); break;
                case "sunder": Hauler(mesh, true, true, true); break;
                case "delver": Segment(mesh, false); break;
                case "brood": Segment(mesh, true); break;
                default: throw new ArgumentException("No Frontier raider for " + id);
            }
        }

        static void Runner(FrontierMesh b, float s, bool courser)
        {
            Vector3 P(float x, float y, float z) => new Vector3(x * s, y * s, z * s);
            b.Polish = .16f;
            b.Bone = 0;
            b.Sphere(P(-.10f, .48f, 0), P(courser ? .61f : .47f, .25f, .28f), Hide, 14, 8);
            b.Sphere(P(-.29f, .55f, 0), P(.33f, .17f, .24f), Shadow, 12, 7);
            // Split slate ridges read as unfinished stone rather than armor.
            b.Polish = .38f;
            for (int i = 0; i < (courser ? 4 : 2); i++)
            {
                float x = -.38f + i * .24f;
                b.Cone(P(x, .67f, 0), P(x + .12f, .82f + i * .035f, 0), .11f * s, .012f * s, Ridge, 6);
            }
            b.Bone = 1;
            b.Polish = .16f;
            b.Sphere(P(.50f, .49f, 0), P(courser ? .28f : .23f, .19f, .22f), Shadow, 12, 7);
            b.Cone(P(.62f, .45f, 0), P(courser ? 1.08f : .90f, .42f, 0), .13f * s, .012f * s, Ridge, 7);
            b.Polish = .75f;
            for (int side = -1; side <= 1; side += 2)
            {
                b.Sphere(P(.64f, .54f, side * .18f), P(.055f, .038f, .025f), Heat, 8, 5);
                b.Cone(P(.40f, .59f, side * .20f), P(.18f, courser ? .89f : .76f, side * .22f), .07f * s, .009f * s, Ridge, 6);
            }
            for (int i = 2; i < 6; i++)
            {
                b.Bone = i; b.Polish = .12f;
                int side = i % 2 == 0 ? 1 : -1;
                float rear = i >= 4 ? -.38f : .30f;
                var hip = P(rear, .39f, side * .20f);
                var knee = P(rear + (courser ? .16f : -.06f), .20f, side * .31f);
                var foot = P(rear + (courser ? .35f : .13f), .035f, side * .35f);
                b.Cone(hip, knee, .105f * s, .064f * s, Hide, 7);
                b.Sphere(knee, P(.075f, .07f, .075f), Ridge, 8, 5);
                b.Cone(knee, foot, .067f * s, .037f * s, Shadow, 7);
                b.Box(foot + P(.08f, 0, 0), P(.20f, .055f, .12f), Shadow);
            }
        }

        static void Hauler(FrontierMesh b, bool sunder, bool plate, bool shield)
        {
            float s = sunder ? 1.5f : 1f;
            Vector3 P(float x, float y, float z) => new Vector3(x * s, y * s, z * s);
            b.Bone = 0; b.Polish = .12f;
            b.Sphere(P(-.10f, .66f, 0), P(.73f, .40f, .46f), Hide, 16, 10);
            b.Sphere(P(-.53f, .67f, 0), P(.32f, .30f, .41f), Shadow, 12, 7);
            b.Box(P(-.09f, .35f, 0), P(.95f, .21f, .68f), Shadow);
            b.Bone = 1;
            b.Sphere(P(.58f, .68f, 0), P(.31f, .25f, .34f), Shadow, 12, 7);
            b.Cone(P(.70f, .60f, 0), P(.98f, .52f, 0), .18f * s, .04f * s, Ridge, 8);
            b.Polish = .72f;
            for (int side = -1; side <= 1; side += 2)
                b.Sphere(P(.75f, .73f, side * .24f), P(.052f, .04f, .025f), Heat, 8, 5);
            for (int i = 2; i < 6; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                float x = i >= 4 ? -.48f : .38f;
                b.Bone = i; b.Polish = .10f;
                b.Cone(P(x, .42f, side * .30f), P(x + .08f, .055f, side * .43f), .16f * s, .09f * s, Hide, 8);
                b.Box(P(x + .14f, .07f, side * .43f), P(.31f, .12f, .20f), Shadow);
            }
            if (plate)
            {
                b.Bone = 0; b.Polish = .48f;
                b.Box(P(-.13f, 1.03f, -.05f), P(1.12f, .18f, .72f), Ridge, -8f);
                b.Box(P(-.36f, .88f, -.39f), P(.74f, .34f, .13f), Bone, -9f);
                for (int i = 0; i < 3; i++)
                    b.Cone(P(-.58f + i * .34f, 1.09f, .08f), P(-.55f + i * .34f, 1.27f, .10f), .12f * s, .02f * s, Shadow, 6);
            }
            if (shield)
            {
                b.Bone = 6; b.Polish = .46f;
                b.Box(P(1.14f, .76f, 0), P(.17f, .97f, 1.03f), Ridge);
                b.Box(P(1.26f, .76f, 0), P(.045f, .68f, .68f), Shadow);
                b.Box(P(1.29f, .76f, 0), P(.06f, .24f, .25f), Heat);
                for (int side = -1; side <= 1; side += 2)
                    b.Cone(P(.70f, .53f, side * .28f), P(1.08f, .68f, side * .39f), .09f * s, .06f * s, Bone, 7);
            }
        }

        static void Lifter(FrontierMesh b, bool drift)
        {
            b.Bone = 0; b.Polish = .14f;
            b.Sphere(new Vector3(-.09f, .82f, 0), new Vector3(.36f, .49f, .29f), Hide, 14, 9);
            b.Sphere(new Vector3(-.25f, .84f, 0), new Vector3(.21f, .40f, .33f), Shadow, 12, 7);
            b.Bone = 1;
            b.Sphere(new Vector3(.31f, 1.18f, 0), new Vector3(.28f, .20f, .24f), Shadow, 12, 8);
            b.Cone(new Vector3(.45f, 1.13f, 0), new Vector3(.73f, 1.02f, 0), .13f, .02f, Ridge, 8);
            b.Polish = .76f;
            for (int side = -1; side <= 1; side += 2)
                b.Sphere(new Vector3(.48f, 1.22f, side * .16f), new Vector3(.05f, .035f, .025f), Heat, 8, 5);
            for (int i = 2; i <= 3; i++)
            {
                int side = i == 2 ? 1 : -1;
                b.Bone = i; b.Polish = .12f;
                b.Cone(new Vector3(-.13f, .58f, side * .21f), new Vector3(-.04f, .06f, side * .29f), .11f, .035f, Shadow, 8);
                b.Box(new Vector3(.10f, .045f, side * .31f), new Vector3(.30f, .07f, .13f), Ridge);
            }
            for (int i = 4; i <= 5; i++)
            {
                int side = i == 4 ? 1 : -1;
                b.Bone = i; b.Polish = .20f;
                if (drift)
                {
                    b.Membrane(side, i, i, Ridge, .13f, .37f, 1.52f);
                    b.Cone(new Vector3(.33f, 1.05f, side * .15f),
                        new Vector3(-.09f, 1.36f, side * 1.67f), .06f, .008f, Bone, 8);
                }
                else
                {
                    b.Box(new Vector3(-.22f, .96f, side * .37f), new Vector3(.57f, .09f, .40f), Ridge, side * 27f);
                    b.Cone(new Vector3(-.38f, .97f, side * .44f), new Vector3(-.58f, 1.17f, side * .72f), .09f, .01f, Shadow, 7);
                }
            }
            for (int i = 6; i <= 7; i++)
            {
                int side = i == 6 ? 1 : -1;
                b.Bone = i; b.Polish = .16f;
                if (drift)
                    b.Cone(new Vector3(.12f, .76f, side * .24f), new Vector3(.36f, .54f, side * .32f), .085f, .015f, Shadow, 7);
                else
                    b.Sweep(new[] { new Vector3(.10f, .86f, side * .24f), new Vector3(.43f, .67f, side * .40f),
                        new Vector3(.91f, .67f, side * .48f), new Vector3(1.22f, .80f, side * .55f) },
                        new[] { new Vector2(.10f,.10f), new Vector2(.085f,.085f), new Vector2(.045f,.045f),
                            new Vector2(.004f,.004f) }, side > 0 ? Heat : Ridge, 8);
            }
        }

        static void Segment(FrontierMesh b, bool brood)
        {
            b.Polish = .10f;
            for (int i = 0; i < 5; i++)
            {
                b.Bone = i == 4 ? 1 : i == 0 ? 5 : 5 - i;
                float x = -.66f + i * .31f;
                float h = i == 4 ? .35f : .32f;
                b.Sphere(new Vector3(x, h, 0), new Vector3(i == 4 ? .24f : .27f, .23f, .31f),
                    i % 2 == 0 ? Hide : Shadow, 12, 8);
                b.Box(new Vector3(x, .47f, 0), new Vector3(.22f, .07f, .47f), Ridge);
                if (brood)
                {
                    b.Polish = .65f;
                    for (int side = -1; side <= 1; side += 2)
                        b.Sphere(new Vector3(x, .38f, side * .27f), new Vector3(.055f, .055f, .035f), Heat, 7, 5);
                    b.Polish = .10f;
                }
            }
            b.Bone = 1; b.Polish = .18f;
            b.Cone(new Vector3(.72f, .33f, 0), new Vector3(.99f, .28f, 0), .16f, .03f, Bone, 8);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Sphere(new Vector3(.79f, .42f, side * .18f), new Vector3(.045f, .035f, .025f), Heat, 8, 5);
                if (!brood)
                {
                    b.Bone = side > 0 ? 6 : 7;
                    b.Cone(new Vector3(.57f, .29f, side * .24f), new Vector3(.82f, .035f, side * .49f), .10f, .015f, Ridge, 8);
                    b.Box(new Vector3(.88f, .04f, side * .50f), new Vector3(.25f, .055f, .20f), Bone, side * 30f);
                }
            }
        }
    }
}
