namespace Broodline.Sim.Combat
{
    /// The slice covers three raiders and three counters. The enums are
    /// declared at full width anyway so adding the other five is additive
    /// rather than a renumbering - values are persisted in replays and wave
    /// data, so an existing member's NUMBER is part of the file format.
    public enum Species { Vetch = 0, Ember = 1, Skitter = 2, Hollow = 3, Loam = 4, Pale = 5 }

    /// broodline_combat_numbers.md section 6. Courser keeps 0: every replay
    /// recorded under engine 0.2.0 stores that number, and renumbering it
    /// would reinterpret each of them as a different raider.
    public enum RaiderType { Courser = 0, Lash = 1, Skirmisher = 2 }

    /// The number of RaiderType values. MUST be updated whenever a raider
    /// type is added - WaveDef's composition invariants bound their scans on
    /// this count, and a stale value would silently stop enforcing them
    /// instead of throwing. At 1 the two invariants had nothing to compare
    /// and neither could fire; at 3 they are live, and wave 7 is the first
    /// authored content they actually judge.
    public static class RaiderTypeCounts
    {
        public const int RaiderTypeCount = 3;
    }

    /// The widths of the enums above, BESIDE the enums they count.
    ///
    /// System.Enum.IsDefined allocates and reflects and Convert.ToInt32 boxes,
    /// so every bounds check in the engine is a bare int comparison against one
    /// of these - which is only equivalent to IsDefined while the numbers agree
    /// with the declarations. That agreement is asserted by EnumWidthTests.
    ///
    /// They live HERE rather than beside the validator that uses them, because
    /// RaiderTypeCounts already established that convention two declarations
    /// up, and the repo briefly had two: a comment-guarded constant next to its
    /// enum, and a test-guarded one in another file. Someone adding a member
    /// then has two inconsistent examples and follows whichever they see first.
    ///
    /// The cost of drift is a bad diagnosis, not just a missed check. Trait has
    /// five members and combat_numbers names seven more; add Pierce without
    /// moving TraitCount and every replay carrying it is rejected as CORRUPT
    /// rather than as unsupported, which sends the reader after a forgery that
    /// is not there. That is not hypothetical any more - this is the edit that
    /// took TraitCount from 2 to 5.
    public static class Ids
    {
        public const int SpeciesCount = 6;
        public const int InstinctCount = 6;
        public const int TraitCount = 5;      // None, Chill, Taunt, Splash, Carapace
    }

    /// combat_numbers sections 4.2 and 4.3. None and Chill keep 0 and 1 for
    /// the reason RaiderType.Courser keeps 0: a deployment's traits are
    /// persisted in every replay, so these numbers are file format.
    ///
    /// Carapace answers no raider - CounterFor never returns it - which is
    /// what makes it legal in a wave alongside any other trait.
    public enum Trait { None = 0, Chill = 1, Taunt = 2, Splash = 3, Carapace = 4 }

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
