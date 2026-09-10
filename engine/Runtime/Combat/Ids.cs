namespace Broodline.Sim.Combat
{
    /// The slice covers one raider and one counter. The enums are declared at
    /// full width anyway so adding the other seven is additive rather than a
    /// renumbering - values are persisted in replays and wave data.
    public enum Species { Vetch = 0, Ember = 1, Skitter = 2, Hollow = 3, Loam = 4, Pale = 5 }

    public enum RaiderType { Courser = 0 }

    public enum Trait { None = 0, Chill = 1 }

    public enum Instinct
    {
        Bloodscent = 0, Vanguard = 1, Overwatch = 2,
        LastStand = 3, Skittish = 4, PackSense = 5
    }

    /// The eight tick phases, in the normative order of combat_engine section 4.
    /// Declared so tests can assert the order rather than trusting a comment.
    public enum Phase
    {
        Spawn = 1, State = 2, Movement = 3, Targeting = 4,
        Attack = 5, Death = 6, Breach = 7, Resolve = 8
    }
}
