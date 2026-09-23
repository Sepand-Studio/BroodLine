using UnityEngine;

namespace Broodline.Frontier
{
    /// The founder's authored silhouette and palette. Shared by base Vetch and Cinderplate.
    /// +X is forward; eye and limb positions also define the skin's bind pose.
    public static class FrontierVetch
    {
        public static readonly Vector3[] BindPositions = {
            Vector3.zero, new Vector3(.43f,.49f,0),
            new Vector3(.35f,.23f,.45f), new Vector3(.35f,.23f,-.45f),
            new Vector3(-.57f,.22f,.43f), new Vector3(-.57f,.22f,-.43f)
        };
        public static readonly Vector3 EyeCenter = new Vector3(.80f,.625f,0);
        public const float EyeSpacing = .275f;
        public static readonly Vector3 DorsalPosition = new Vector3(-.18f,1.0f,0);
        public static readonly Vector3 FlankPosition = new Vector3(-.17f,.51f,-.635f);
        static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var c); return c; }
        static readonly Color Skin = Hex("#79a6ac"), Belly = Hex("#edd9b3"), Ink = Hex("#243841");
        static readonly Color Shell = Hex("#497d8a"), Edge = Hex("#30515c"), Scale = Hex("#527c87");

        public static void Build(FrontierMesh b)
        {
            b.Bone = 0; b.Polish = .12f;
            b.Sphere(new Vector3(-.12f,.39f,0), new Vector3(.71f,.31f,.54f), Skin, 18, 10);
            b.Sphere(new Vector3(.02f,.28f,0), new Vector3(.65f,.18f,.49f), Belly, 18, 10);
            // Continuous dark under-shell makes the gaps between plates read as carved seams.
            var center = new Vector3(-.18f,.43f,0);
            var radius = new Vector3(.79f,.54f,.65f);
            b.Polish = .32f;
            b.Sphere(center, new Vector3(.787f,.537f,.647f), Edge, 20, 10, upperOnly: true);
            var hex = new[] { new Vector2(.56f,0), new Vector2(.27f,-.44f), new Vector2(-.43f,-.44f),
                new Vector2(-.72f,0), new Vector2(-.43f,.44f), new Vector2(.27f,.44f) };
            var middle = new Vector2[6];
            for (int i = 0; i < 6; i++) middle[i] = hex[i] * .955f;
            b.ShellPlate(center, radius, middle, Hex("#598994"), 3);
            for (int i = 0; i < 6; i++)
            {
                var a = hex[i]; var d = hex[(i + 1) % 6];
                var outerA = a.normalized * .985f; var outerD = d.normalized * .985f;
                var mid = (outerA + outerD).normalized * .994f;
                var polygon = new[] { a, outerA, mid, outerD, d };
                var average = Vector2.zero;
                foreach (var p in polygon) average += p;
                average /= polygon.Length;
                for (int p = 0; p < polygon.Length; p++) polygon[p] = average + (polygon[p] - average) * .963f;
                b.ShellPlate(center, radius, polygon, Color.Lerp(Shell, Skin, i % 3 * .09f), 3);
            }

            // A large inset face and wide cream chin replace the small projecting head.
            b.Bone = 1; b.Polish = .10f;
            b.Sphere(new Vector3(.48f,.51f,0), new Vector3(.44f,.32f,.44f), Skin, 22, 12);
            b.Sphere(new Vector3(.69f,.375f,0), new Vector3(.30f,.155f,.36f), Belly, 20, 10);
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = EyeCenter + new Vector3(0,0,side * EyeSpacing);
                var orientation = Quaternion.Euler(0, -side * 28, 0);
                var direction = orientation * Vector3.right;
                // Skin sockets and brows stay with the head; the inset eyes blink inside them.
                b.Sphere(eye - direction * .035f, new Vector3(.080f,.145f,.117f), Belly, 14, 8, orientation);
                b.Sphere(eye + new Vector3(-.06f,.105f,0), new Vector3(.115f,.065f,.137f), Skin, 14, 8, orientation);
                b.Bone = side == -1 ? 6 : 7;
                b.Polish = .72f;
                b.Sphere(eye, new Vector3(.065f,.123f,.094f), Hex("#fff0d2"), 14, 8, orientation);
                b.Polish = .92f;
                b.Sphere(eye + direction*.053f, new Vector3(.025f,.105f,.078f), Hex("#704218"), 14, 8, orientation);
                b.Sphere(eye + direction*.069f, new Vector3(.017f,.088f,.065f), Hex("#c58730"), 12, 8, orientation);
                b.Sphere(eye + direction*.082f + Vector3.up*.009f, new Vector3(.014f,.075f,.047f), Hex("#111f2c"), 12, 8, orientation);
                b.Sphere(eye + direction*.094f + new Vector3(0,.05f,-.024f), new Vector3(.009f,.021f,.016f), Color.white, 8, 6, orientation);
                b.Bone = 1; b.Polish = .10f;
                // Cheek scales are broad color groupings rather than tiny freckles.
                for (int spot = 0; spot < 3; spot++)
                {
                    float z = side * (.34f + spot*.018f), y = .50f - spot*.065f;
                    var p = Face(new Vector3(.48f,.51f,0), new Vector3(.44f,.32f,.44f), y, z);
                    b.Sphere(p, new Vector3(.022f,.029f,.025f), Scale, 8, 5, orientation);
                }
                b.Sphere(new Vector3(.949f,.435f,side*.091f), new Vector3(.016f,.012f,.020f), Ink, 8, 5);
            }
            // Smile follows the front surface of the broad muzzle, including the lifted corners.
            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 6f - 1;
                var p = Face(new Vector3(.69f,.375f,0), new Vector3(.30f,.155f,.36f), .35f+t*t*.047f, t*.285f);
                if (i > 0) b.Cone(previous, p, .008f, .008f, Hex("#856c50"), 6);
                previous = p;
            }
            for (int i = 2; i < 6; i++)
            {
                b.Bone = i; var p = BindPositions[i];
                b.Sphere(p, new Vector3(.245f,.235f,.22f), Skin, 12, 8);
                b.Sphere(p + new Vector3(.055f,-.14f,0), new Vector3(.27f,.09f,.235f), Scale, 12, 6);
                for (int toe = -1; toe <= 1; toe++)
                    b.Sphere(p + new Vector3(.246f,-.158f,toe*.116f), new Vector3(.084f,.055f,.065f), Edge, 10, 6);
            }
            b.Bone = 0; b.Polish = .18f;
        }

        static Vector3 Face(Vector3 center, Vector3 radius, float y, float z)
        {
            float dy = (y-center.y)/radius.y, dz = (z-center.z)/radius.z;
            return new Vector3(center.x + radius.x*Mathf.Sqrt(Mathf.Max(0,1-dy*dy-dz*dz)) + .003f, y, z);
        }
    }
}
