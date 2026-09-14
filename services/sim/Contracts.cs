namespace Broodline.Sim.Service
{
    /// The body is the SERIALIZED REPLAY, not its six fields spread out.
    ///
    /// Replay.Serialize is fixed-layout little-endian and Deserialize
    /// validates inside itself, so the format has exactly one parser.
    /// Re-describing the fields in JSON at this boundary builds a second one,
    /// and two parsers for one format can disagree - which is the whole
    /// failure mode the binary format was chosen to avoid.
    public sealed record SimulateRequest(string Replay);

    /// What sim echoes back so api can key a ledger row WITHOUT PARSING
    /// ANYTHING. solo_execution 6.1: api never learns what a trait does.
    public sealed record SimulateEcho(
        int WaveId,
        string Seed,          // ulong as decimal string - see Hash
        string Terrain,
        int LaneCount,
        int RallyTick,
        int RallyCreature);

    public sealed record BreachDto(
        int Tick, int Raider, string Type, int Lane,
        bool Access, bool Coverage, bool Placement);

    public sealed record SimulateOutcome(
        string Result,
        int Ticks,
        int IntegrityRemaining,
        /// DECIMAL STRING, not a number. Outcome.Hash is a ulong; JSON numbers
        /// are doubles, and a uniformly distributed 64-bit value exceeds 2^53
        /// almost always. A number here is silently wrong most of the time.
        string Hash,
        BreachDto[] Breaches);

    public sealed record SimulateResponse(
        string Verdict,             // "verified" | "rejected"
        string? Reason,             // null when verified
        string EngineVersion,
        SimulateEcho? Echo,
        SimulateOutcome? Outcome)
    {
        public static SimulateResponse Rejected(string reason) =>
            new("rejected", reason, SimVersion.Value, null, null);
    }
}
