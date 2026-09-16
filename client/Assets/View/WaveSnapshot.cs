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
    /// It is also the ONE source every renderer reads. That is not a
    /// convenience: the HUD used to take existence and HP from the live runner
    /// while taking position from here, so between tick boundaries a bar
    /// trailed the body it labelled, and on the terminating tick the two
    /// sources disagreed about whether a raider existed at all.
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
        private readonly bool[] _raiderBreaching;
        private readonly int[] _creatureHp;
        private readonly int[] _creatureTile;

        public int RaiderCount { get; private set; }
        public int CreatureCount => _creatureHp.Length;
        public int Integrity { get; private set; }
        public int Tick { get; private set; }

        public WaveSnapshot(int raiderCapacity, int creatureCount)
        {
            _raiderTile = new float[raiderCapacity];
            _raiderHp = new int[raiderCapacity];
            _raiderAlive = new bool[raiderCapacity];
            _raiderChilled = new bool[raiderCapacity];
            _raiderBreaching = new bool[raiderCapacity];
            _creatureHp = new int[creatureCount];
            _creatureTile = new int[creatureCount];
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
                _raiderBreaching[i] = r.RaiderBreachedThisTick(i);
            }
            for (int c = 0; c < r.CreatureCount; c++)
            {
                _creatureHp[c] = r.CreatureHp[c];
                // Skittish repositions, so the pocket is read every tick rather
                // than once at Build - and it is read HERE rather than in each
                // renderer, which is what keeps "one source per entity" true
                // for position as well as for existence.
                _creatureTile[c] = r.Lane.PocketTiles[r.CreaturePocket[c]];
            }
        }

        public float RaiderTile(int i) => _raiderTile[i];
        public int RaiderHp(int i) => _raiderHp[i];
        public bool RaiderAlive(int i) => _raiderAlive[i];
        public bool RaiderChilled(int i) => _raiderChilled[i];
        public int CreatureHp(int c) => _creatureHp[c];
        public int CreatureTile(int c) => _creatureTile[c];

        /// Whether this raider reached the Ark on THIS tick.
        ///
        /// Phases.Breach clears RaiderAlive in the same breath it deducts
        /// integrity, so a breaching raider and a dead one look identical to
        /// anything reading the live state - and both renderers gate on
        /// aliveness. The breach frame is the one frame wave 6 exists to show,
        /// and it was never drawn: the Courser simply vanished a tile short of
        /// the Ark while the HUD printed Loss. Capturing the terminating tick
        /// was necessary for that and not sufficient.
        public bool RaiderBreaching(int i) => _raiderBreaching[i];

        /// Whether anything should be drawn for this raider: on the board, or
        /// breaching on this very tick.
        public bool RaiderVisible(int i) => i < RaiderCount && (_raiderAlive[i] || _raiderBreaching[i]);

        /// How many raiders are DRAWN. Not how many are alive.
        ///
        /// The HUD's entity count used SimRunner.AliveRaiderCount while both
        /// renderers drew RaiderVisible, so on the breach frame - the one frame
        /// wave 6 exists to show - the readout said one fewer than was on
        /// screen. Two accessors added in the same change, disagreeing about
        /// the same tick. Counting from the snapshot the renderers read means
        /// the number cannot drift from the picture.
        public int VisibleRaiderCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < RaiderCount; i++) if (RaiderVisible(i)) n++;
                return n;
            }
        }

        public int LiveCreatureCount
        {
            get
            {
                int n = 0;
                for (int c = 0; c < _creatureHp.Length; c++) if (_creatureHp[c] > 0) n++;
                return n;
            }
        }

        /// Interpolated lane position. THE expression - `WaveView` and the
        /// HUD's per-frame snapshot (`WaveRunner.Snapshot`, which feeds
        /// `WaveHudView`) both call this rather than each writing their own
        /// lerp, because when they did, a bar and the body it labelled
        /// answered two different questions about where a raider was.
        ///
        /// The second caller used to be `WaveHud`, the IMGUI debug surface
        /// Task 16 replaced; the rule survived the rewrite because it was the
        /// fix, not the file.
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
