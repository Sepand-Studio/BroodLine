namespace Broodline.Sim
{
    /// Deterministic PRNG for the simulation core: xorshift128+ (Vigna &amp;
    /// Blackman, 2014 — shift triple 23/17/26, the parameterisation also used by
    /// V8 and JavaScriptCore's XorShift128+).
    ///
    /// <para>
    /// One stream per simulation, seeded once by the server and threaded through
    /// explicitly (never held ambiently) so a stray call cannot silently consume
    /// a draw and desync two otherwise-identical runs. Per <c>engine/BannedSymbols.txt</c>,
    /// <c>System.Random</c> and <c>UnityEngine.Random</c> are both banned for this
    /// project for the same reason: neither guarantees the same stream across
    /// CoreCLR and Unity/Mono.
    /// </para>
    ///
    /// <para>
    /// <b>Pinned contract.</b> This struct's output is part of the save/replay
    /// format: <c>RngTests.KnownSeed_ProducesKnownFirstDraw</c> asserts the exact
    /// first draw for seed <c>1</c> as a literal constant. Once that test exists,
    /// changing anything below — the shift triple, the operation order, or the
    /// seed-splitting scheme documented on the constructor — is a breaking change:
    /// every stored replay becomes unreproducible and <see cref="SimVersion"/>
    /// must change with it. This is not a hypothetical; it is the entire reason
    /// this type exists rather than <see langword="System.Random"/>.
    /// </para>
    ///
    /// <para>
    /// Deliberately a <b>mutable</b> struct: <see cref="NextULong"/> advances the
    /// stream in place. Copying an <see cref="Rng"/> value forks an independent
    /// stream from that point (normal value-type semantics) — callers that need a
    /// single shared stream must hold it by reference (e.g. as a field) rather
    /// than re-reading a copy.
    /// </para>
    public struct Rng
    {
        private ulong _state0;
        private ulong _state1;

        /// Seeds the 128-bit xorshift state from a single 64-bit seed.
        ///
        /// <para>
        /// <b>Seed-splitting scheme (pinned, see the type-level contract above):</b>
        /// the two state words are produced by two successive applications of
        /// SplitMix64 (Steele, Lea &amp; Flood's mixer, used unmodified — the same
        /// technique Vigna's own reference xorshift128+ implementation recommends
        /// for turning one seed into a full state). A naive split — e.g. the seed's
        /// high/low 32 bits, or <c>(seed, seed ^ constant)</c> — was rejected
        /// because adjacent seeds (1, 2, 3, ...) would then start from
        /// near-identical state words; SplitMix64's avalanche means seed and
        /// seed+1 diverge completely from the very first draw, which is what
        /// <c>RngTests.DifferentSeeds_Diverge</c> checks for.
        /// </para>
        ///
        /// <para>
        /// <b>Zero-state guard:</b> xorshift128+'s degenerate case is the
        /// all-zero 128-bit state, which maps to itself and emits zero forever
        /// (the transformation is linear over GF(2), and zero is its fixed
        /// point). SplitMix64's mixing step is a bijection on 64-bit words, so
        /// each state word independently comes out to exactly zero for exactly
        /// one seed value in 2^64 — astronomically unlikely, but not impossible,
        /// and "silently returns all zeros forever for one specific seed" is
        /// exactly the kind of defect that TDD and code review both miss. Each
        /// word is therefore guarded independently: if either comes out zero, it
        /// is replaced with a fixed nonzero constant. This is a stronger
        /// (simpler to prove correct) guarantee than the minimum needed — the
        /// algorithm only degenerates if BOTH words are zero at once — but
        /// guarding each word individually means the non-degeneracy invariant
        /// (<c>_state0 != 0 &amp;&amp; _state1 != 0</c>) holds unconditionally
        /// rather than only in combination.
        /// </para>
        public Rng(ulong seed)
        {
            ulong z = seed;
            _state0 = SplitMix64(ref z);
            _state1 = SplitMix64(ref z);

            if (_state0 == 0) _state0 = 0x9E3779B97F4A7C15UL; // golden-ratio constant; arbitrary nonzero fallback
            if (_state1 == 0) _state1 = 0xBF58476D1CE4E5B9UL; // SplitMix64's own multiplier; arbitrary nonzero fallback
        }

        // SplitMix64 (Steele, Lea & Flood, "Fast Splittable Pseudorandom Number
        // Generators"). Advances `state` by the golden-ratio increment and
        // returns a bijective mix of the new state. Used only to seed the
        // xorshift state above, never as the simulation's own draw source.
        private static ulong SplitMix64(ref ulong state)
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// Advances the stream and returns the next 64-bit draw. Mutates this
        /// instance in place — see the type-level note on value semantics.
        public ulong NextULong()
        {
            unchecked
            {
                ulong x = _state0;
                ulong y = _state1;
                _state0 = y;
                x ^= x << 23;
                x ^= x >> 17;
                x ^= y ^ (y >> 26);
                _state1 = x;
                return x + y;
            }
        }

        /// Draws a uniformly-distributed <see langword="int"/> in
        /// <c>[0, exclusiveMax)</c>.
        ///
        /// <para>
        /// <b>Boundary contract:</b> for <c>exclusiveMax &lt;= 0</c> the range is
        /// empty, so there is no value to draw; rather than throw (allocating an
        /// exception object, and giving the tick loop a control-flow path this
        /// deterministic core otherwise avoids) this returns <c>0</c> without
        /// consuming a draw from the stream. <c>NextInt(1)</c> is the one-element
        /// case, not a degenerate one: it always returns <c>0</c>, and — unlike
        /// the <c>&lt;= 0</c> case — it DOES consume exactly one draw, so a
        /// caller's stream position stays predictable regardless of the entity
        /// count it passes in. This matters concretely: Task 7's
        /// <c>Corpus.RunScenario</c> calls <c>NextInt(entityCount)</c> with
        /// counts starting at 1.
        /// </para>
        ///
        /// <para>
        /// <b>Avoiding modulo bias:</b> naively returning
        /// <c>NextULong() % exclusiveMax</c> is biased whenever
        /// <c>exclusiveMax</c> does not evenly divide 2^64 — the values in
        /// <c>[0, 2^64 mod exclusiveMax)</c> would then be produced by one extra
        /// residue class of raw draws compared to the rest. Instead this uses
        /// rejection sampling (the same construction as OpenBSD's
        /// <c>arc4random_uniform</c>, extended to 64 bits): compute
        /// <c>threshold = (2^64 - exclusiveMax) mod exclusiveMax</c> — via the
        /// unsigned wraparound of <c>0UL - bound</c>, since 2^64 itself does not
        /// fit in a <see langword="ulong"/> — and redraw whenever a draw lands
        /// below that threshold. Every retained draw is then uniform over
        /// <c>[0, exclusiveMax)</c> exactly, with no bias at any bound. The
        /// rejection probability is at most 0.5 for any bound and is 0 whenever
        /// <c>exclusiveMax</c> is a power of two (including <c>1</c>, where
        /// <c>threshold == 0</c> and the loop always accepts its first draw).
        /// </para>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0) return 0;

            ulong bound = (ulong)exclusiveMax;
            ulong threshold = unchecked(0UL - bound) % bound; // == 2^64 mod bound, without overflow

            ulong r;
            do
            {
                r = NextULong();
            } while (r < threshold);

            return (int)(r % bound);
        }
    }
}
