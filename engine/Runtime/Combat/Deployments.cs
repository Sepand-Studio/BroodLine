namespace Broodline.Sim.Combat
{
    /// What makes a run RECORDABLE, in ONE place.
    ///
    /// A replay is the inputs, and the inputs are a wave id, a terrain family,
    /// a seed and a deployment. The engine WRITES one of these for every run it
    /// performs - so any run it accepts whose record cannot be read back, or
    /// reads back as a DIFFERENT run, is a bug the verification path reports as
    /// an honest player cheating.
    ///
    /// Three axes, and the first attempt at this file closed only one of them:
    ///
    ///   DEPLOYMENT   measured with a single Vetch before the shared list
    ///                existed: Pocket 178956971 SIMULATED to Loss in 540 ticks
    ///                because 178956971 * 24 wraps into Lane's 120-entry
    ///                distance table; Pocket 5 threw IndexOutOfRangeException
    ///                out of the tick loop; Species 6, Trait 2, Tier 7 and
    ///                Instinct 9 each emitted a 212-byte record that Validate
    ///                then rejected.
    ///
    ///   WAVE         a WaveDef claiming id 6 while carrying integrity 8 ran to
    ///                Win in 540 ticks with hash 8532104364784216008. The record
    ///                stores only the ID, so verification rebuilt the AUTHORED
    ///                wave 6 and returned Loss, hash 2495532238167386945 - and
    ///                Validate accepted it, because nothing cross-checks the
    ///                fields the record does not carry. A win read back as a
    ///                loss, silently, which is worse than a rejection.
    ///
    ///   LANE         a Lane built as Terrain.Defile with 30 tiles ran a full
    ///                wave and emitted a record that threw
    ///                "lane length 30 disagrees with Defile's 24" on load.
    ///
    /// The rule for all three is the same: a run may only use content this
    /// engine can rebuild from what the record stores.
    ///
    /// Reports a REASON rather than throwing, because the callers owe different
    /// exception types to their own callers - WaveCompositionException on the
    /// play path, ReplayFormatException at the untrusted boundary - and a shared
    /// throw would force one of them to translate. Returning null on the happy
    /// path also keeps this allocation-free for every valid run.
    internal static class Deployments
    {
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
                if ((int)d.Species < 0 || (int)d.Species >= Ids.SpeciesCount)
                    return "creature " + c + " has species " + (int)d.Species;
                if ((int)d.Instinct < 0 || (int)d.Instinct >= Ids.InstinctCount)
                    return "creature " + c + " has instinct " + (int)d.Instinct;
                if ((int)d.Trait1 < 0 || (int)d.Trait1 >= Ids.TraitCount ||
                    (int)d.Trait2 < 0 || (int)d.Trait2 >= Ids.TraitCount)
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

        /// Why a run on this wave and lane could not be recorded faithfully, or
        /// null.
        ///
        /// The record stores an ID and a family, not the content. So the only
        /// wave and lane a run may use are the ones THIS engine produces for
        /// that ID and that family - anything else is a run whose record
        /// describes a different game, and the engine finds that out at
        /// verification time rather than at play time.
        internal static string Unrecordable(WaveDef wave, Lane lane)
        {
            if (wave == null) return "no wave";
            if (lane == null) return "no lane";

            WaveDef authored;
            try { authored = WaveDef.ForId(wave.Id); }
            catch (WaveCompositionException e)
            { return "wave id " + wave.Id + " is not authored, so no record of it could be read back: " + e.Message; }

            if (wave.Integrity != authored.Integrity)
                return "wave " + wave.Id + " was given integrity " + wave.Integrity +
                       " but the authored wave has " + authored.Integrity +
                       " - a replay stores only the id, so this run would verify as a different wave";
            if (wave.LaneCount != authored.LaneCount)
                return "wave " + wave.Id + " was given lane count " + wave.LaneCount +
                       " but the authored wave has " + authored.LaneCount;
            if (wave.Spawns.Length != authored.Spawns.Length)
                return "wave " + wave.Id + " was given " + wave.Spawns.Length +
                       " spawns but the authored wave has " + authored.Spawns.Length;
            for (int i = 0; i < wave.Spawns.Length; i++)
                if (wave.Spawns[i].Tick != authored.Spawns[i].Tick ||
                    wave.Spawns[i].Type != authored.Spawns[i].Type)
                    return "wave " + wave.Id + "'s spawn " + i +
                           " disagrees with the authored timeline";

            var rebuilt = authored.Lane;
            if (lane.Family != rebuilt.Family)
                return "terrain " + lane.Family + " disagrees with wave " + wave.Id + "'s " + rebuilt.Family;
            if (lane.Tiles != rebuilt.Tiles)
                return "lane length " + lane.Tiles + " disagrees with " +
                       lane.Family + "'s " + rebuilt.Tiles;
            if (lane.PocketCount != rebuilt.PocketCount)
                return "pocket count " + lane.PocketCount + " disagrees with " +
                       lane.Family + "'s " + rebuilt.PocketCount;
            for (int p = 0; p < lane.PocketCount; p++)
                if (lane.PocketTiles[p] != rebuilt.PocketTiles[p])
                    return "pocket " + p + " sits beside tile " + lane.PocketTiles[p] +
                           " but " + lane.Family + " puts it at " + rebuilt.PocketTiles[p];

            return null;
        }
    }
}
