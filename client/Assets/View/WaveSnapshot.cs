using Broodline.Sim.Combat;

namespace Broodline.View
{
    /// One tick's worth of the world, copied out for rendering.
    ///
    /// client_architecture section 2 requires two of these retained - previous
    /// and current - because interpolation needs both, and says View "copies
    /// the minimum it needs for interpolation rather than deep-copying world
    /// state each tick." This is that minimum: what moves and what is alive.
    ///
    /// Fix64 carries no float conversion because floats are banned in the
    /// engine. The conversion happens HERE and nowhere else: Q32.32 means the
    /// raw long is the value scaled by 2^32.
    public sealed class WaveSnapshot
    {
        private const double Q32 = 4294967296.0;   // 2^32

        private readonly float[] _raiderTile;
        private readonly int[] _raiderHp;
        private readonly bool[] _raiderAlive;
        private readonly bool[] _raiderChilled;
        private readonly int[] _creatureHp;

        public int RaiderCount { get; private set; }
        public int Integrity { get; private set; }
        public int Tick { get; private set; }

        public WaveSnapshot(int raiderCapacity, int creatureCount)
        {
            _raiderTile = new float[raiderCapacity];
            _raiderHp = new int[raiderCapacity];
            _raiderAlive = new bool[raiderCapacity];
            _raiderChilled = new bool[raiderCapacity];
            _creatureHp = new int[creatureCount];
        }

        public void Capture(SimRunner r)
        {
            RaiderCount = r.RaiderCount;
            Integrity = r.Integrity;
            Tick = r.Tick;

            for (int i = 0; i < r.RaiderCount; i++)
            {
                _raiderTile[i] = (float)(r.RaiderProgress[i].Raw / Q32);
                _raiderHp[i] = r.RaiderHp[i];
                _raiderAlive[i] = r.RaiderAlive[i];
                _raiderChilled[i] = r.RaiderChilled[i];
            }
            for (int c = 0; c < r.CreatureCount; c++)
                _creatureHp[c] = r.CreatureHp[c];
        }

        public float RaiderTile(int i) => _raiderTile[i];
        public int RaiderHp(int i) => _raiderHp[i];
        public bool RaiderAlive(int i) => _raiderAlive[i];
        public bool RaiderChilled(int i) => _raiderChilled[i];
        public int CreatureHp(int c) => _creatureHp[c];

        /// Interpolated lane position, using the same expression WaveView uses
        /// to place the body. The HUD used to read Current directly while the
        /// View lerped, so a bar and the thing it labelled answered two
        /// different questions about where a raider was.
        public static float LerpTile(WaveSnapshot previous, WaveSnapshot current, int i, float alpha)
            => previous.RaiderTile(i) + (current.RaiderTile(i) - previous.RaiderTile(i)) * alpha;
    }

    /// The two retained snapshots, swapped rather than reallocated so the
    /// render path allocates nothing per tick.
    public sealed class WavePair
    {
        private WaveSnapshot _previous;
        private WaveSnapshot _current;

        public WavePair(SimRunner r)
        {
            int capacity = r.RaiderHp.Length;
            _previous = new WaveSnapshot(capacity, r.CreatureCount);
            _current = new WaveSnapshot(capacity, r.CreatureCount);
            _previous.Capture(r);
            _current.Capture(r);
        }

        public WaveSnapshot Previous => _previous;
        public WaveSnapshot Current => _current;

        /// Call once per simulation step, after Step returns.
        public void Advance(SimRunner r)
        {
            var swap = _previous;
            _previous = _current;
            _current = swap;
            _current.Capture(r);
        }
    }
}
