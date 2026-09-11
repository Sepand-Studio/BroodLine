namespace Broodline.Sim
{
    /// FNV-1a (Fowler/Noll/Vo, 64-bit variant) state hash for the simulation
    /// core: the mechanism by which two runs of the same simulation — one on
    /// CoreCLR, one on Unity/IL2CPP — are proven to have produced the same
    /// result. Feed it every value that should affect the comparison, in a
    /// fixed order, and compare <see cref="Value"/> across runs.
    ///
    /// <para>
    /// <b>Deliberately order-sensitive</b> (<c>HashTests.OrderMatters</c> pins
    /// this): a reordered tick is a different simulation, and a hash that could
    /// not tell the two apart would be useless for exactly the case it exists
    /// to catch.
    /// </para>
    ///
    /// <para>
    /// <b><see cref="Create"/>, not <see langword="new Hash()"/>:</b> FNV's
    /// offset basis (<c>14695981039346656037</c>, asserted verbatim by
    /// <c>HashTests.EmptyHash_IsTheFnvOffsetBasis</c>) is not zero, but a
    /// default-initialised struct's fields always are. A parameterless
    /// constructor can't be written to override that, so construction goes
    /// through this factory instead. Keeping the empty state at the real
    /// offset basis rather than sneaking a zero->basis special case into
    /// <see cref="Value"/> matters because a real hash can legitimately compute
    /// to zero — a getter that treated zero as "uninitialised" would silently
    /// misreport a genuine result.
    /// </para>
    ///
    /// <para>
    /// Deliberately a <b>mutable</b> struct: <see cref="Add"/> folds a value into
    /// the running hash in place. As with <see cref="Rng"/>, copying a
    /// <see cref="Hash"/> value forks an independent copy of the accumulated
    /// state (normal value-type semantics) — hold it by reference (e.g. as a
    /// field) if a single accumulation needs to be shared across calls.
    /// </para>
    public struct Hash
    {
        // FNV-1a 64-bit constants, from the published spec — not tunable.
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong _value;

        /// The running hash of every value folded in via <see cref="Add"/> so far.
        public ulong Value => _value;

        /// Starts a new hash at FNV's offset basis. See the type-level note on
        /// why this factory exists instead of a parameterless constructor.
        public static Hash Create()
        {
            Hash h;
            h._value = OffsetBasis;
            return h;
        }

        /// Folds a 64-bit value into the hash, byte by byte, most-significant
        /// byte first.
        ///
        /// <para>
        /// <b>Byte order is explicit and is part of this type's pinned
        /// contract</b> — changing it changes every hash this type has ever
        /// produced, exactly like changing <see cref="Rng"/>'s algorithm. It is
        /// fixed by the loop bounds below, never by inspecting the host
        /// platform.
        /// </para>
        ///
        /// <para>
        /// <b>Deliberately not <c>System.BitConverter.GetBytes</c></b>, for two
        /// independent reasons, neither of which the project's existing
        /// enforcement would catch: <c>BitConverter.GetBytes</c> is not on
        /// <c>engine/BannedSymbols.txt</c>, and the no-floating-point IL scan
        /// (<c>DeterminismRuleTests</c>) only looks for <c>float</c>/<c>double</c>.
        /// </para>
        /// <list type="bullet">
        /// <item><description>
        /// It allocates a <c>byte[4]</c>/<c>byte[8]</c> per call. <see cref="Add"/>
        /// runs once per tracked value per tick in Tasks 6 and 7's tick loop, and
        /// the Global Constraint "No allocation in the tick loop" applies to this
        /// type more than any other — it is the one that runs there most often.
        /// </description></item>
        /// <item><description>
        /// Its byte order follows <c>BitConverter.IsLittleEndian</c>, i.e.
        /// whichever endianness the <i>host CPU</i> happens to be — not a
        /// property of this type or of the value being hashed. Every runtime
        /// this project currently targets (CoreCLR, Unity Mono, IL2CPP on
        /// desktop/mobile) happens to be little-endian, so this would not fail
        /// today; it is a latent hazard specifically because it would keep
        /// passing right up until someone targets a big-endian runtime, in the
        /// one type whose entire job is proving two runtimes agree. Folding with
        /// explicit shifts makes the byte order a property of this code, not of
        /// whatever machine happens to run it.
        /// </description></item>
        /// </list>
        public void Add(long value)
        {
            ulong bits = unchecked((ulong)value);

            unchecked
            {
                for (int shift = 56; shift >= 0; shift -= 8)
                {
                    byte b = (byte)(bits >> shift);
                    _value = (_value ^ b) * Prime;
                }
            }
        }
    }
}
