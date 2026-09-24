using System;
using UnityEngine;

namespace Broodline.Frontier
{
    /// The three raiders actually authored by the current simulation. They
    /// share a dark hide, but their outlines carry three different threats:
    /// numerous low runners, a long charging courser, and a broad Lash with
    /// a single forward-reaching weapon. None borrows a companion face.
    public static class FrontierRaiders
    {
        static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }
        static readonly Color Hide = Hex("#414556");
        static readonly Color Shadow = Hex("#292f40");
        static readonly Color Ridge = Hex("#778495");
        static readonly Color Heat = Hex("#e57b57");

        public static float Scale(string id) => id == "skirmisher" ? .68f : id == "courser" ? 1.22f : 1f;

        public static void Build(FrontierMesh mesh, string id)
        {
            switch (id)
            {
                case "skirmisher": Runner(mesh, .68f, false); break;
                case "courser": Runner(mesh, 1.22f, true); break;
                case "lash": Lash(mesh); break;
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

        static void Lash(FrontierMesh b)
        {
            b.Bone = 0; b.Polish = .18f;
            b.Sphere(new Vector3(-.06f, .50f, 0), new Vector3(.50f, .30f, .31f), Hide, 14, 9);
            b.Sphere(new Vector3(-.34f, .54f, 0), new Vector3(.27f, .22f, .38f), Shadow, 12, 7);
            b.Polish = .35f;
            for (int side = -1; side <= 1; side += 2)
            {
                // A pair of broad, folded vanes makes the Lifter silhouette.
                b.Box(new Vector3(-.19f, .70f, side * .36f), new Vector3(.65f, .065f, .40f), Ridge, side * 24f);
                b.Cone(new Vector3(-.41f, .69f, side * .39f), new Vector3(-.63f, 1.03f, side * .78f), .10f, .01f, Shadow, 7);
            }
            b.Bone = 1; b.Polish = .16f;
            b.Sphere(new Vector3(.39f, .50f, 0), new Vector3(.24f, .18f, .23f), Shadow, 12, 7);
            b.Cone(new Vector3(.53f, .47f, 0), new Vector3(.78f, .37f, 0), .13f, .012f, Ridge, 7);
            b.Polish = .76f;
            for (int side = -1; side <= 1; side += 2)
                b.Sphere(new Vector3(.48f, .56f, side * .18f), new Vector3(.044f, .036f, .025f), Heat, 8, 5);
            // The one conspicuous reach weapon is the Taunt lesson.
            b.Bone = 0; b.Polish = .30f;
            b.Sweep(new[] { new Vector3(.08f, .53f, -.25f), new Vector3(.37f, .39f, -.49f),
                new Vector3(.80f, .43f, -.62f), new Vector3(1.11f, .59f, -.74f) },
                new[] { new Vector2(.10f,.10f), new Vector2(.08f,.08f), new Vector2(.045f,.045f),
                    new Vector2(.005f,.005f) }, Heat, 8);
            for (int i = 2; i < 6; i++)
            {
                b.Bone = i; b.Polish = .13f;
                int side = i % 2 == 0 ? 1 : -1;
                float x = i >= 4 ? -.35f : .25f;
                b.Cone(new Vector3(x, .36f, side * .23f),
                    new Vector3(x + .06f, .04f, side * .38f), .085f, .025f, Shadow, 7);
            }
        }
    }
}
