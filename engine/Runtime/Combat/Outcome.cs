namespace Broodline.Sim.Combat
{
    public enum Result { Running = 0, Win = 1, Loss = 2, Stalled = 3 }

    /// One raider reaching the Ark, with its diagnosis attached at Task 9.
    /// combat_engine section 7: the diagnosis is recorded AT the breach,
    /// because by the time the wave ends the information about why a specific
    /// raider was unanswerable is gone.
    public struct Breach
    {
        public int Tick;
        public int Raider;
        public RaiderType Type;
        public int Lane;

        public bool Access;
        public bool Coverage;
        public bool Placement;
    }

    /// What a simulation returns. Inputs in, result out.
    public struct Outcome
    {
        public Result Result;
        public int Ticks;
        public int IntegrityRemaining;
        public Breach[] Breaches;
        public int BreachCount;
        public ulong Hash;
    }
}
