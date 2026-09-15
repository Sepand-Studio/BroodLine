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

            return new SimulateResponse(
                "verified", null, SimVersion.Value,
                new SimulateEcho(record.WaveId, record.Seed.ToString(),
                                 record.Terrain.ToString(), record.LaneCount,
                                 record.RallyTick, record.RallyCreature),
                new SimulateOutcome(outcome.Result.ToString(), outcome.Ticks,
                                    outcome.IntegrityRemaining,
                                    outcome.Hash.ToString(), breaches));
        }
    }
}
