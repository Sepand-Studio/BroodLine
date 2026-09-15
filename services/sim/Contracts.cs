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
    ///
    /// Deployment joins for design 6.2, and it does not weaken that rule. api
    /// stored the specs it resolved from the player's OWN rows at issuance
    /// (Task 8); this is the deployment the submitted replay actually claimed,
    /// read by the one parser that exists. api compares the two and still has
    /// no opinion about what Chill DOES - only about whether seven fields
    /// match seven it already holds. The moment it needs a rule to make that
    /// comparison, the boundary has moved and this is the wrong shape.
    public sealed record SimulateEcho(
        int WaveId,
        string Seed,          // ulong as decimal string - see Hash
        string Terrain,
        int LaneCount,
        int RallyTick,
        int RallyCreature,
        CreatureSpecDto[] Deployment);

    /// The engine's CreatureSpec in the shape api already stores it -
    /// services/api/src/db/schema.ts's CreatureSpec, field for field. Seven
    /// fields and no creature id: design 2.1 keeps identity a storage concern
    /// and the engine has never held one.
    ///
    /// THE TRAIT AND SPECIES TRANSLATION LIVES HERE, in this direction only,
    /// and that is the whole reason this DTO exists rather than the echo
    /// carrying CreatureSpec itself.
    ///
    /// api holds NAMES - 'Chill', 'Taunt' - because they come off a creature
    /// row's trait_N text columns, which come off config/bundles' traits.json.
    /// The engine holds a Trait enum, whose NUMBERS are file format
    /// (Ids.cs). Something has to translate, and ORDINAL -> NAME here is the
    /// only arrangement under which api compares string to string and never
    /// learns that Chill is 1.
    ///
    /// The other direction was available and is worse. A NAME -> ORDINAL table
    /// inside api is a second copy of engine semantics in the service that
    /// exists not to have one, and it has a case this direction does not: an
    /// unknown name. Answering that with None - the obvious default, and the
    /// zero value - would make a forged 'Bogus' compare equal to a simulated
    /// Trait.None slot, so a deployment nobody owns would read as legitimate.
    /// ToString() has no such case: Deployments.Problem refuses every ordinal
    /// outside 0..TraitCount-1 before this record is ever built, so the name
    /// is always a declared member and never a printed number.
    ///
    /// TIERS ARE NULLABLE, and null is not decoration. data_model 2: an
    /// Aberrant has no coverage tier, so it carries null rather than 0 -
    /// "null and zero must not be conflated; zero would sort and display as
    /// less than tier I" - and drizzle/0005_loop.sql's coverage_tier_N_not_zero
    /// enforces it, so a stored tier is null or 1..3 and never 0. The engine
    /// spells that same state 0, because CreatureSpec.Tier1 is a plain int and
    /// Stats.SplashTargets reads 0 as "not really carried". This is the ONE
    /// place the two spellings meet. A non-nullable int here would answer 0
    /// where api holds null, and 6.2's comparison would then reject an honest
    /// Aberrant on every submission it ever saw.
    public sealed record CreatureSpecDto(
        string Species,
        string Trait1,
        int? Tier1,
        string Trait2,
        int? Tier2,
        string Instinct,
        /// The one field of a spec the REQUEST supplies - design 6.1. Which
        /// pockets a lane has is content and sim is the only authority on it;
        /// api bounds this at int32 and no further. Two creatures MAY share a
        /// pocket - Deployments.Problem bounds the value and says nothing about
        /// sharing - and this echo is the first thing that makes that
        /// observable across the boundary.
        int Pocket);

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
