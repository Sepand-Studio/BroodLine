namespace Broodline.Sim.Combat
{
    public struct Verdict
    {
        public bool Access;
        public bool Coverage;
        public bool Placement;
        public bool Answered => Access && Coverage && Placement;
    }

    /// combat_engine section 7. Three booleans, evaluated in order; the first
    /// false is the diagnosis, and it maps onto the three messages bible 4.11
    /// specifies.
    ///
    /// The same function serves the pre-wave check and the loss screen -
    /// section 7: "One function, two call sites." Building it twice is how the
    /// panel and the loss screen come to disagree.
    public static class Diagnosis
    {
        /// tile == AnyTile evaluates placement against the whole lane, which is
        /// the pre-wave question: could a carrier reach this raider ANYWHERE?
        public const int AnyTile = -1;

        public static Verdict Evaluate(SimState s, RaiderType type, int simultaneous, int tile)
        {
            var v = new Verdict();
            Trait answering = Stats.CounterFor(type);

            v.Access = HasCarrier(s, answering);
            if (!v.Access) return v;          // first false is the diagnosis

            v.Coverage = CapacityFor(s, answering) >= simultaneous;
            if (!v.Coverage) return v;

            v.Placement = CanBeReached(s, answering, tile);
            return v;
        }

        /// The pre-wave call. Runs against the authored wave rather than a
        /// live breach: the worst simultaneous count the spawn table can
        /// produce, anywhere on the lane.
        public static Verdict PreWaveCheck(SimState s, RaiderType type)
        {
            int worst = 0;
            for (int i = 0; i < s.Wave.Spawns.Length; i++)
                if (s.Wave.Spawns[i].Type == type) worst++;

            return Evaluate(s, type, worst, AnyTile);
        }

        /// How many of this raider type are on the board right now. Coverage is
        /// "a property of the deployment against what was on the board at that
        /// moment", so this is evaluated at breach time, never inferred later.
        public static int SimultaneousCount(SimState s, RaiderType type)
        {
            int n = 0;
            for (int r = 0; r < s.RaiderCount; r++)
                if (s.RaiderAlive[r] && s.RaiderType[r] == type) n++;
            return n;
        }

        private static bool HasCarrier(SimState s, Trait trait)
        {
            if (trait == Trait.None) return false;
            for (int c = 0; c < s.CreatureCount; c++)
                if (s.CreatureAlive(c) && s.CreatureCarries(c, trait, out int tier) && tier > 0)
                    return true;
            return false;
        }

        private static int CapacityFor(SimState s, Trait trait) =>
            trait == Trait.Chill ? Capacity.TotalChillCapacity(s) : 0;

        private static bool CanBeReached(SimState s, Trait trait, int tile)
        {
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;
                if (!s.CreatureCarries(c, trait, out int tier) || tier <= 0) continue;

                int range = Targeting.EffectiveRange(s, c);
                int pocket = s.CreaturePocket[c];

                if (tile == AnyTile)
                {
                    for (int t = 0; t < s.Lane.Tiles; t++)
                        if (s.Lane.InRange(pocket, t, range)) return true;
                }
                else if (s.Lane.InRange(pocket, tile, range))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
