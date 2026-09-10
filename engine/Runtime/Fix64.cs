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
    ///
    /// <para>
    /// Edge-case contract, inherited unchanged from the vendored implementation
    /// and pinned by the "Pinned_" tests in Fix64Tests.cs:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// Division by zero, and any quotient whose magnitude overflows a
    /// <see langword="long"/>, saturates to <see cref="long.MaxValue"/> —
    /// always a large <i>positive</i> value, regardless of the operands'
    /// signs. A negative dividend divided by zero does not produce a large
    /// negative result; it produces the same saturated positive value a
    /// positive dividend would. This falls out of <c>DivPrecise</c>'s overflow
    /// guard, which returns before the result's sign is reapplied. No
    /// exception is thrown either way.
    /// </description></item>
    /// <item><description>
    /// <see cref="Sqrt"/> of a negative value returns <see cref="Zero"/>
    /// rather than throwing. The vendored <c>SqrtPrecise</c> routes negative
    /// input through <c>FixedUtil.InvalidArgument</c>, whose default handler
    /// is a no-op, so the invalid call is silently absorbed and falls through
    /// to the same early-return as <c>Sqrt(Zero)</c>.
    /// </description></item>
    /// <item><description>
    /// <c>*</c> and <c>/</c> round in different directions at the bit they
    /// discard. Multiplication (<c>Mul</c>) truncates toward negative
    /// infinity — it floors — for both signs alike. Division
    /// (<c>DivPrecise</c>) truncates toward zero: it divides the operands'
    /// magnitudes and reapplies the sign afterward. So, outside the
    /// saturating case described in the first bullet above,
    /// <c>(-a) / b == -(a / b)</c> holds — negating the dividend only
    /// flips the sign reapplied at the end, not the magnitude division.
    /// When the division saturates, <c>a / b</c> and <c>(-a) / b</c> both
    /// collapse to the same positive <see cref="long.MaxValue"/>, so the
    /// identity fails there instead. Separately,
    /// <c>(-a) * b == -(a * b)</c> does not hold whenever the exact product
    /// has a fractional remainder below the format's 2⁻³² resolution.
    /// </description></item>
    /// </list>
    /// <para>
    /// None of this is a defect in this wrapper — it is exactly what the
    /// vendored implementation does, kept deliberately rather than patched
    /// with a sign guard, a thrown exception, or a rounding-mode branch added
    /// to arithmetic that runs per-entity, per-tick. Changing any of these
    /// three behaviours is a deliberate decision to revisit, not a bug fix —
    /// and it must update this comment and the "Pinned_" tests together.
    /// </para>
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
