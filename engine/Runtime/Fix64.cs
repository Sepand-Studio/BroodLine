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
    /// <b>There is no unary minus.</b> Negation has to be spelled out —
    /// <c>Fix64.Zero - a</c>, or <c>Fix64.FromInt(-1) * a</c>, or
    /// <c>Fix64.FromRaw(-a.Raw)</c> — and each of those reduces to unchecked
    /// <see langword="long"/> negation: <c>Add</c> and <c>Sub</c> are plain
    /// <c>a + b</c> and <c>a - b</c> with no overflow guard, so they wrap.
    /// Therefore <b>negation is the identity at</b>
    /// <c>a.Raw == long.MinValue</c> — <c>Fix64.Zero - a</c> hands back the
    /// very same value. <see cref="Zero"/> and <c>FromRaw(long.MinValue)</c>
    /// are the only two values that are their own negation, since
    /// <c>x == -x</c> modulo 2⁶⁴ holds exactly when <c>2x</c> is a multiple
    /// of 2⁶⁴. Every identity below of the form "negating an operand negates
    /// the result" is stated for <c>a.Raw != long.MinValue</c> for this
    /// reason alone: at that one value negating the operand changes nothing,
    /// so the identity survives only in the degenerate case where the result
    /// is itself one of those two self-negating values. This failure has
    /// nothing to do with saturation and nothing to do with rounding.
    /// </description></item>
    /// <item><description>
    /// Division by zero saturates to <see cref="long.MaxValue"/> — always a
    /// large <i>positive</i> value, regardless of the operands' signs. A
    /// negative dividend divided by zero does not produce a large negative
    /// result; it produces the same saturated positive value a positive
    /// dividend would, because <c>DivPrecise</c>'s guard returns before the
    /// result's sign is reapplied. That guard is
    /// <c>(|a.Raw| >> 32) >= |b.Raw|</c>, which is <i>not</i> the same test
    /// as "the quotient fits": it fires only once the exact quotient's raw
    /// magnitude reaches 2⁶⁴, twice what a <see langword="long"/> holds. A
    /// quotient whose exact raw magnitude lands in the band above
    /// <see cref="long.MaxValue"/> but below 2⁶⁴ therefore slips past the
    /// guard and is wrapped into a <see langword="long"/> rather than
    /// saturated, usually with the sign flipped:
    /// <c>FromRaw(long.MaxValue) / FromRaw(0xFFFFFFFF)</c> divides a positive
    /// by a positive and returns raw <c>-9223372034707292161</c>, where the
    /// exact quotient is raw <c>+9223372039002259455</c>. No exception is
    /// thrown on any of these paths.
    /// </description></item>
    /// <item><description>
    /// <see cref="Sqrt"/> of a negative value returns <see cref="Zero"/>
    /// rather than throwing. The vendored <c>SqrtPrecise</c> routes negative
    /// input through <c>FixedUtil.InvalidArgument</c>, whose default handler
    /// is a no-op, so the invalid call is silently absorbed and falls through
    /// to the same early-return as <c>Sqrt(Zero)</c>. This one holds for
    /// every negative value, <c>FromRaw(long.MinValue)</c> included: the
    /// vendored routine's first test is <c>a &lt;= 0</c>.
    /// </description></item>
    /// <item><description>
    /// <b><c>Mul</c> has no overflow guard at all</b> — do not read the
    /// division bullet above as evidence of symmetry here. It is
    /// <c>LogicalShiftRight(af * bf, 32) + ai * b + af * bi</c> in plain
    /// unchecked <see langword="long"/> arithmetic, so a product whose exact
    /// raw value does not fit wraps silently to an unrelated value: nothing
    /// clamps it to either extreme, and nothing throws. A wrapped result can
    /// still land on an extremum by coincidence — that is the wrap, not
    /// saturation. Nothing in the result signals that it is wrong, and the
    /// value is generally nowhere near the true magnitude —
    /// <c>FromInt(int.MaxValue) * FromInt(int.MaxValue)</c>
    /// returns raw <c>4294967296</c>, which is the real value <c>1</c>, for a
    /// true product of about 4.6·10¹⁸.
    /// </description></item>
    /// <item><description>
    /// <c>*</c> and <c>/</c> round in different directions at the bit they
    /// discard. Multiplication (<c>Mul</c>) truncates toward negative
    /// infinity — it floors — for both signs alike. Division
    /// (<c>DivPrecise</c>) truncates toward zero: it divides the operands'
    /// magnitudes and reapplies the sign afterward. Taking the first bullet's
    /// <c>a.Raw != long.MinValue</c> as given throughout, the two sign
    /// identities then hold under these exact conditions:
    /// <c>(Zero - a) / b == Zero - (a / b)</c> holds precisely when the
    /// division does not saturate, because negating the dividend only flips
    /// the sign reapplied at the end and leaves the magnitude division
    /// untouched; when it does saturate, <c>a / b</c> and <c>(Zero - a) / b</c>
    /// collapse to the same positive <see cref="long.MaxValue"/> instead.
    /// <c>(Zero - a) * b == Zero - (a * b)</c> holds precisely when the exact
    /// product <c>a.Raw * b.Raw</c> is a whole multiple of 2³²; a remainder
    /// below the format's 2⁻³² resolution makes the two sides floor to values
    /// one raw unit apart. Multiplication overflow does <i>not</i> on its own
    /// break this second identity, because the wrap applies symmetrically to
    /// both sides — so the two sides agreeing is evidence of sign symmetry
    /// only, never evidence that the product is correct.
    /// </description></item>
    /// </list>
    /// <para>
    /// None of this is a defect in this wrapper — it is exactly what the
    /// vendored implementation does, kept deliberately rather than patched
    /// with a unary minus, a sign guard, an overflow check, a thrown
    /// exception, or a rounding-mode branch added to arithmetic that runs
    /// per-entity, per-tick. Changing any of these behaviours is a deliberate
    /// decision to revisit, not a bug fix — and it must update this comment
    /// and the "Pinned_" tests together.
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
