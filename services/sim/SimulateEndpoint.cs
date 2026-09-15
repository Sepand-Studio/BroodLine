using Broodline.Sim.Combat;

namespace Broodline.Sim.Service
{
    public static class SimulateEndpoint
    {
        /// Three rejection reasons, and the ORDER they are checked in is the
        /// design.
        ///
        /// Deserialize throws on bytes that are not a record at all.
        /// IsFromThisEngine is checked NEXT and explicitly, before Sim.Replay -
        /// because Validate also throws ReplayFormatException on a version
        /// mismatch, and collapsing the two would report a legitimate record
        /// from a later engine as "malformed", sending the reader after a
        /// forgery that is not there. Replay.cs makes the same argument for
        /// the same reason, one layer down.
        public static SimulateResponse Handle(SimulateRequest request)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(request.Replay ?? ""); }
            catch (FormatException) { return SimulateResponse.Rejected("replay_malformed"); }

            Replay record;
            try { record = Replay.Deserialize(bytes); }
            catch (ReplayFormatException) { return SimulateResponse.Rejected("replay_malformed"); }

            if (!record.IsFromThisEngine)
                return SimulateResponse.Rejected("engine_too_old");

            Outcome outcome;
            try { outcome = Combat.Sim.Replay(record); }
            catch (ReplayFormatException) { return SimulateResponse.Rejected("rules_violated"); }
            catch (WaveCompositionException) { return SimulateResponse.Rejected("rules_violated"); }

            var breaches = new BreachDto[outcome.BreachCount];
            for (int i = 0; i < breaches.Length; i++)
            {
                var b = outcome.Breaches[i];
                breaches[i] = new BreachDto(b.Tick, b.Raider, b.Type.ToString(), b.Lane,
                                            b.Access, b.Coverage, b.Placement);
            }

            // The deployment the SUBMITTED RECORD claimed, read back off the
            // record rather than off SimState - design 6.2 asks what this
            // submission asserted it fought with, and `record` is that
            // assertion after Deployments.Problem has already refused every
            // out-of-range species, trait, tier and pocket in it. Built AFTER
            // Combat.Sim.Replay for that reason: a record whose deployment the
            // rules reject is a rejection, not an echo.
            var deployment = new CreatureSpecDto[record.Deployment.Length];
            for (int c = 0; c < deployment.Length; c++)
            {
                var d = record.Deployment[c];
                deployment[c] = new CreatureSpecDto(
                    d.Species.ToString(),
                    d.Trait1.ToString(), Coverage(d.Tier1),
                    d.Trait2.ToString(), Coverage(d.Tier2),
                    d.Instinct.ToString(),
                    d.Pocket);
            }

            return new SimulateResponse(
                "verified", null, SimVersion.Value,
                new SimulateEcho(record.WaveId, record.Seed.ToString(),
                                 record.Terrain.ToString(), record.LaneCount,
                                 record.RallyTick, record.RallyCreature,
                                 deployment),
                new SimulateOutcome(outcome.Result.ToString(), outcome.Ticks,
                                    outcome.IntegrityRemaining,
                                    outcome.Hash.ToString(), breaches));
        }

        /// The engine's tier in the shape api stores it: 0 is NOT A TIER.
        ///
        /// Stats.SplashTargets and Attacks.DamageTaken both read 0 as "the
        /// trait is not really carried", and data_model 2 spells that same
        /// state null because zero would sort and display as less than tier I.
        /// drizzle/0005_loop.sql's coverage_tier_N_not_zero makes it
        /// unstorable on the api side, so an echoed 0 could never equal
        /// anything api holds - design 6.2's comparison would reject an honest
        /// Aberrant every time.
        ///
        /// A FUNCTION rather than a cast, so the choice is written down once
        /// and the two slots cannot drift apart.
        private static int? Coverage(int tier) => tier == 0 ? null : tier;
    }
}
