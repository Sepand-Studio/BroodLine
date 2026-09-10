namespace Broodline.Sim
{
    public static class Corpus
    {
        /// How many scenarios the cross-runtime gate compares. Single source of
        /// truth for both emitters — tests/engine/CorpusTests.cs (CoreCLR) and
        /// client/Assets/Determinism/CorpusPlayerHarness.cs (the IL2CPP player) —
        /// which previously each carried their own literal 500 held together only
        /// by a "must match" comment. It is also asserted by
        /// EnforcementTests.Corpus_StillSweepsTheFullScenarioCount, because the
        /// Definition of Done names 500 and the diff script now asserts the same
        /// number rather than merely printing whatever it counted.
        public const int ScenarioCount = 500;

        /// Raw Q32.32 values the per-scenario arithmetic sweep runs over.
        ///
        /// <para>
        /// This is Fix64Tests.SampleRaws — the spread already trusted to probe
        /// Fix64's edges — plus the boundary values the pinned contract tests call
        /// out by name: <c>0xFFFFFFFF</c> (the divisor in the documented
        /// wrap-instead-of-saturate case), and <c>(1L&lt;&lt;31)-1</c> / <c>1L&lt;&lt;31</c>
        /// straddling the sub-unit/whole-unit split of the Q32.32 word.
        /// <c>long.MinValue</c> and <c>long.MaxValue</c> are in the SampleRaws
        /// spread already.
        /// </para>
        ///
        /// <para>
        /// <c>long.MinValue</c> earns its place twice over: as a divisor it is the
        /// only value whose magnitude sets the top bit, so <c>DivPrecise</c>'s
        /// <c>s = Nlz(v)</c> comes out 0 and the routine evaluates
        /// <c>u0 &gt;&gt; (64 - s)</c> — a shift of exactly 64, which ECMA-335 leaves
        /// unspecified and C++ makes undefined. That expression is masked to zero
        /// afterwards, but the shift still executes, and IL2CPP compiles it as C++.
        /// If CoreCLR and IL2CPP are ever going to disagree about Fix64, this is
        /// the likeliest single line — so the gate must actually run it.
        /// </para>
        static readonly long[] SweepRaws =
        {
            long.MinValue,
            long.MinValue + 1,
            -(1L << 40),
            -(1L << 32),
            -1L,
            0L,
            1L,
            (1L << 31) - 1,
            1L << 31,
            0xFFFFFFFFL,
            1L << 32,
            1L << 40,
            long.MaxValue - 1,
            long.MaxValue,
        };

        /// Generates scenario N deterministically and returns its whole-run hash.
        /// Both runtimes must produce identical output for every index.
        ///
        /// <para>
        /// The hash folds two things: the toy simulation's own run hash, and a
        /// fixed arithmetic sweep. The sweep exists because the toy simulation
        /// reaches only <c>FromInt</c>, <c>+</c>, <c>-</c> and <c>Fix64.Zero</c>,
        /// and the vendored <c>Add</c>/<c>Sub</c> are literally <c>a + b</c> and
        /// <c>a - b</c> — so without it the gate proved that 64-bit integer
        /// addition agrees across runtimes, which could not plausibly have gone
        /// the other way, while <c>Mul</c>, <c>/</c> and <c>Sqrt</c> — variable
        /// shifts, an unsigned shift loop, and unchecked wrapping products, the
        /// paths that carry real cross-runtime risk — never executed on IL2CPP at
        /// all. The sweep is the same for every scenario by design: folding it
        /// after the simulation hash means a single disagreement anywhere in it
        /// shows up on all <see cref="ScenarioCount"/> lines of the diff rather
        /// than on one.
        /// </para>
        ///
        /// <para>
        /// Every operation below is total at these inputs — division by zero
        /// saturates, <c>Sqrt</c> of a negative returns zero, <c>Mul</c> wraps
        /// silently — so the sweep never throws. That is the documented Fix64
        /// contract (see the header comment on Fix64), and hashing those results
        /// is precisely the point: the gate's job is to prove the two runtimes
        /// agree on the edge behaviour, not that the edge behaviour is pleasant.
        /// </para>
        public static ulong RunScenario(int index)
        {
            var gen = new Rng((ulong)(index + 1));
            int entities = 1 + gen.NextInt(8);
            int ticks = 30 + gen.NextInt(120);
            int inputCount = gen.NextInt(6);
            var inputs = new ToyInput[inputCount];
            for (int i = 0; i < inputCount; i++)
                inputs[i] = new ToyInput
                {
                    Tick = gen.NextInt(ticks),
                    EntityId = gen.NextInt(entities)
                };

            var hash = Hash.Create();
            hash.Add(unchecked((long)ToySim.Run((ulong)(index + 1), entities, inputs, ticks)));
            FoldArithmeticSweep(ref hash);
            return hash.Value;
        }

        /// Folds Mul, /, Sqrt and ToIntFloor over every value (and every ordered
        /// pair of values) in <see cref="SweepRaws"/> into <paramref name="hash"/>,
        /// in a fixed index order. Taken by <c>ref</c> because <see cref="Hash"/>
        /// is a mutable struct: by value it would fold into a copy and discard it.
        static void FoldArithmeticSweep(ref Hash hash)
        {
            for (int i = 0; i < SweepRaws.Length; i++)
            {
                var a = Fix64.FromRaw(SweepRaws[i]);

                hash.Add(Fix64.Sqrt(a).Raw);
                hash.Add(a.ToIntFloor());

                for (int j = 0; j < SweepRaws.Length; j++)
                {
                    var b = Fix64.FromRaw(SweepRaws[j]);
                    hash.Add((a * b).Raw);
                    hash.Add((a / b).Raw);
                }
            }
        }
    }
}
