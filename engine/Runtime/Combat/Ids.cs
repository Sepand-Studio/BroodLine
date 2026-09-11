namespace Broodline.Sim.Combat
{
    /// The slice covers one raider and one counter. The enums are declared at
    /// full width anyway so adding the other seven is additive rather than a
    /// renumbering - values are persisted in replays and wave data.
    public enum Species { Vetch = 0, Ember = 1, Skitter = 2, Hollow = 3, Loam = 4, Pale = 5 }

    public enum RaiderType { Courser = 0 }

    /// The number of RaiderType values. MUST be updated whenever a raider
    /// type is added - WaveDef's composition invariants bound their scans on
    /// this count, and a stale value would silently stop enforcing them
    /// instead of throwing.
    public static class RaiderTypeCounts
    {
        public const int RaiderTypeCount = 1;
    }

    public enum Trait { None = 0, Chill = 1 }

    public enum Instinct
    {
        Bloodscent = 0, Vanguard = 1, Overwatch = 2,
        LastStand = 3, Skittish = 4, PackSense = 5
    }

    /// The eight tick phases, in the normative order of combat_engine section 4.
    /// Referenced by no engine code and no test - it documents that order for
    /// readers. The actual enforcement lives in CombatEnforcementTests, which
    /// scans SimRunner.cs for those calls in order - and separately asserts no
    /// other engine file makes them at all - rather than trusting this enum or
    /// a comment.
    public enum Phase
    {
        Spawn = 1, State = 2, Movement = 3, Targeting = 4,
        Attack = 5, Death = 6, Breach = 7, Resolve = 8
    }

    /// The terrain family a lane is built from. combat_engine section 3 stores
    /// this in every replay, so the values are persisted and must never be
    /// renumbered - adding a family appends.
    ///
    /// One member for now. Lane geometry is otherwise reachable only through a
    /// static factory, which a replay cannot name.
    public enum Terrain { Defile = 0 }
}
