namespace Broodline.Sim.Combat
{
    /// What makes a deployment simulatable, in ONE place.
    ///
    /// There were two: Replay.Validate bounded every field of a deserialized
    /// deployment, and the SimRunner constructor bounded exactly one of them -
    /// the array length. That asymmetry is not cosmetic, because the engine
    /// WRITES a record of every run it performs: a deployment the simulation
    /// accepts but the replay codec rejects is a run the engine can play and
    /// cannot read back, and on the verification path that reads as an honest
    /// player's raid being corrupt.
    ///
    /// Measured before this file existed, each with a single Vetch:
    ///   Pocket   = 178956971  SIMULATED to Loss/540 - 178956971 * 24 wraps
    ///                         into Lane's 120-entry distance table
    ///   Pocket   = 5 or -1    IndexOutOfRangeException out of the tick loop,
    ///                         not WaveCompositionException
    ///   Species  = 6, Trait1 = 2, Tier1 = 7, Instinct = 9
    ///                         each ran a full 540-tick wave and emitted a
    ///                         212-byte record that Validate then rejected
    ///
    /// Reports a REASON rather than throwing, because the two callers owe
    /// different exception types to their own callers - WaveCompositionException
    /// on the play path, ReplayFormatException at the untrusted boundary - and
    /// a shared throw would force one of them to translate. Returning null on
    /// the happy path also keeps this allocation-free for every valid run.
    internal static class Deployments
    {
        // Enum widths. System.Enum.IsDefined allocates and reflects, and
        // Convert.ToInt32 boxes; the enums are contiguous from 0, so a bare int
        // comparison is cheaper and clearer about what it enforces.
        //
        // INTERNAL rather than private because EnumWidthTests asserts each
        // against the enum it describes. The cost of drift is a bad diagnosis:
        // add a Trait (seven are coming) without moving TraitCount and every
        // replay carrying it is rejected as CORRUPT rather than as unsupported,
        // which sends the reader after a forgery that is not there.
        internal const int SpeciesCount = 6;
        internal const int InstinctCount = 6;
        internal const int TraitCount = 2;     // None, Chill
        internal const int MaxCoverageTier = 3;

        /// Why this deployment cannot be simulated on this lane, or null.
        internal static string Problem(CreatureSpec[] deployment, Lane lane)
        {
            if (deployment == null) return "no deployment";

            if (deployment.Length > Stats.DeploymentCap)
                return "deployment of " + deployment.Length +
                       " exceeds the cap of " + Stats.DeploymentCap;

            for (int c = 0; c < deployment.Length; c++)
            {
                var d = deployment[c];

                // Species first. Stats.CreatureHp returns 0 for an unknown one,
                // which would turn Replay's HP cross-check into a no-op and let
                // a forged record delete a creature by starting it at 0 HP.
                if ((int)d.Species < 0 || (int)d.Species >= SpeciesCount)
                    return "creature " + c + " has species " + (int)d.Species;
                if ((int)d.Instinct < 0 || (int)d.Instinct >= InstinctCount)
                    return "creature " + c + " has instinct " + (int)d.Instinct;
                if ((int)d.Trait1 < 0 || (int)d.Trait1 >= TraitCount ||
                    (int)d.Trait2 < 0 || (int)d.Trait2 >= TraitCount)
                    return "creature " + c + " carries an unknown trait";
                if (d.Tier1 < 0 || d.Tier1 > MaxCoverageTier ||
                    d.Tier2 < 0 || d.Tier2 > MaxCoverageTier)
                    return "creature " + c + " has a coverage tier outside 0.." + MaxCoverageTier;

                if (d.Pocket < 0 || d.Pocket >= lane.PocketCount)
                    return "creature " + c + " is in pocket " + d.Pocket +
                           ", outside " + lane.Family + "'s 0.." + (lane.PocketCount - 1);
            }

            return null;
        }
    }
}
