namespace Broodline.Sim
{
    /// The engine's behavioural identity.
    ///
    /// WHAT FORCES A BUMP: any change that alters engine output for any input.
    /// That set is not a judgment call - it is exactly the set of changes that
    /// fail to reproduce tests/engine/corpus-baseline.txt, so the baseline IS
    /// the detector and emit-corpus-baseline.sh refuses to re-baseline without
    /// a bump.
    ///
    /// WHAT READS IT: Replay.EngineVersion, and nothing else. A replay whose
    /// stored version differs from this renders its recorded outcome and is
    /// never re-simulated - solo_execution section 9.4. That rule was inert
    /// until this constant started moving.
    ///
    /// NOT SEMANTIC. The only comparison anyone performs is equality, so the
    /// string's shape is a readability choice and nothing parses its parts.
    ///
    /// A bump WITHOUT a behaviour change is legal - a deliberate marker for a
    /// release. Run the emitter afterwards; it will report the baseline as
    /// unchanged, which is the proof the release was non-behavioural.
    public static class SimVersion
    {
        public const string Value = "0.4.0";
    }
}
