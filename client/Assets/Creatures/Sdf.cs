using UnityEngine;

namespace Broodline.Creatures
{
    public static class Sdf
    {
        public static float Sphere(Vector3 p, Vector3 c, float r) => (p - c).magnitude - r;

        public static float Capsule(Vector3 p, Vector3 a, Vector3 b, float r)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Mathf.Max(1e-6f, Vector3.Dot(ba, ba)));
            return (pa - ba * h).magnitude - r;
        }

        public static float Box(Vector3 p, Vector3 c, Vector3 half)
        {
            var q = new Vector3(Mathf.Abs(p.x - c.x) - half.x, Mathf.Abs(p.y - c.y) - half.y, Mathf.Abs(p.z - c.z) - half.z);
            var outside = Vector3.Max(q, Vector3.zero).magnitude;
            var inside = Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
            return outside + inside;
        }

        /// Polynomial smooth minimum. k = 0 is a hard union.
        public static float SmoothMin(float a, float b, float k)
        {
            if (k <= 0f) return Mathf.Min(a, b);
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        public static float Eval(in Primitive p, Vector3 x)
        {
            switch (p.Kind)
            {
                case PrimitiveKind.Sphere: return Sphere(x, p.A, p.Radius);
                case PrimitiveKind.Capsule: return Capsule(x, p.A, p.B, p.Radius);
                default: return Box(x, p.A, p.Half);
            }
        }

        public static float Field(Primitive[] prims, float blend, Vector3 x)
        {
            float d = float.MaxValue;
            for (int i = 0; i < prims.Length; i++) d = SmoothMin(d, Eval(prims[i], x), blend);
            return d;
        }

        public static Bounds BoundsOf(Primitive[] prims, float padding)
        {
            var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
            foreach (var p in prims)
            {
                Vector3 lo, hi;
                switch (p.Kind)
                {
                    case PrimitiveKind.Sphere: lo = p.A - Vector3.one * p.Radius; hi = p.A + Vector3.one * p.Radius; break;
                    case PrimitiveKind.Capsule: lo = Vector3.Min(p.A, p.B) - Vector3.one * p.Radius; hi = Vector3.Max(p.A, p.B) + Vector3.one * p.Radius; break;
                    default: lo = p.A - p.Half; hi = p.A + p.Half; break;
                }
                min = Vector3.Min(min, lo); max = Vector3.Max(max, hi);
            }
            min -= Vector3.one * padding; max += Vector3.one * padding;
            var b = new Bounds(); b.SetMinMax(min, max); return b;
        }
    }
}
