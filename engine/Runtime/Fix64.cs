using System;

namespace Broodline.Sim
{
    /// Deterministic Q32.32 fixed-point number for the simulation core.
    ///
    /// Wraps a single raw <see langword="long"/> and delegates every arithmetic
    /// operation to the vendored FixPointCS.Fixed64 library (see
    /// Runtime/ThirdParty/FixPointCS). FixPointCS.Fixed64 exposes float/double
    /// conversion helpers (FromDouble, FromFloat, ToDouble, ToFloat) at its API
    /// edge; this wrapper never calls them. That is deliberate: the vendored
    /// FixPointCS namespace is exempt from the assembly's no-floating-point scan
    /// (DeterminismRuleTests.SimulationCore_ContainsNoFloatingPoint), but
    /// Broodline.Sim.Fix64 is not, so no float or double may appear anywhere in
    /// this file — not in a signature, not in a body, not in a cast.
    public readonly struct Fix64 : IEquatable<Fix64>, IComparable<Fix64>
    {
        public static readonly Fix64 Zero = new Fix64(FixPointCS.Fixed64.Zero);
        public static readonly Fix64 One = new Fix64(FixPointCS.Fixed64.One);

        public long Raw { get; }

        private Fix64(long raw)
        {
            Raw = raw;
        }

        public static Fix64 FromRaw(long raw)
        {
            return new Fix64(raw);
        }

        public static Fix64 FromInt(int v)
        {
            return new Fix64(FixPointCS.Fixed64.FromInt(v));
        }

        public int ToIntFloor()
        {
            return FixPointCS.Fixed64.FloorToInt(Raw);
        }

        // SqrtPrecise (not the polynomial-approximation Sqrt) is the exact
        // integer square root algorithm: it is bit-exact at perfect squares,
        // which the wrapper's test suite asserts on directly.
        public static Fix64 Sqrt(Fix64 v)
        {
            return new Fix64(FixPointCS.Fixed64.SqrtPrecise(v.Raw));
        }

        public static Fix64 operator +(Fix64 a, Fix64 b)
        {
            return new Fix64(FixPointCS.Fixed64.Add(a.Raw, b.Raw));
        }

        public static Fix64 operator -(Fix64 a, Fix64 b)
        {
            return new Fix64(FixPointCS.Fixed64.Sub(a.Raw, b.Raw));
        }

        public static Fix64 operator *(Fix64 a, Fix64 b)
        {
            return new Fix64(FixPointCS.Fixed64.Mul(a.Raw, b.Raw));
        }

        // DivPrecise (not the polynomial-approximation Div) is the exact
        // integer division algorithm, for the same bit-exactness reason as
        // Sqrt/SqrtPrecise above.
        public static Fix64 operator /(Fix64 a, Fix64 b)
        {
            return new Fix64(FixPointCS.Fixed64.DivPrecise(a.Raw, b.Raw));
        }

        public static bool operator ==(Fix64 a, Fix64 b)
        {
            return a.Raw == b.Raw;
        }

        public static bool operator !=(Fix64 a, Fix64 b)
        {
            return a.Raw != b.Raw;
        }

        public static bool operator <(Fix64 a, Fix64 b)
        {
            return a.Raw < b.Raw;
        }

        public static bool operator <=(Fix64 a, Fix64 b)
        {
            return a.Raw <= b.Raw;
        }

        public static bool operator >(Fix64 a, Fix64 b)
        {
            return a.Raw > b.Raw;
        }

        public static bool operator >=(Fix64 a, Fix64 b)
        {
            return a.Raw >= b.Raw;
        }

        public bool Equals(Fix64 other)
        {
            return Raw == other.Raw;
        }

        public override bool Equals(object obj)
        {
            return obj is Fix64 other && Equals(other);
        }

        public override int GetHashCode()
        {
            // long.GetHashCode(), not string-based: M:System.String.GetHashCode
            // is banned in this project (BannedSymbols.txt) because it is
            // randomized per-process on CoreCLR but not on Unity's Mono.
            return Raw.GetHashCode();
        }

        public int CompareTo(Fix64 other)
        {
            return Raw.CompareTo(other.Raw);
        }
    }
}
