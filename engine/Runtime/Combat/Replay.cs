using System;
using System.Text;

namespace Broodline.Sim.Combat
{
    public class ReplayFormatException : Exception
    {
        public ReplayFormatException(string message) : base(message) { }
    }

    /// combat_engine section 3: "A replay is NOT a recording of state. It is
    /// the inputs." Six fields, a few hundred bytes, and the engine version,
    /// which is load-bearing - section 9.4 of solo_execution renders a
    /// superseded replay's stored outcome with a notice rather than
    /// re-simulating it.
    ///
    /// Binary and fixed-layout rather than JSON, because "a few hundred bytes"
    /// is a constraint: bible section 4.9 gives every raid a replay and there
    /// are two raids per player per day.
    ///
    /// No System.IO. Serialize returns bytes and Deserialize takes them; the
    /// host writes files. The engine's no-dependencies rule is not traded for
    /// a convenience.
    public sealed class Replay
    {
        private const uint Magic = 0x50524C42;   // "BLRP" little-endian
        private const ushort FormatVersion = 1;

        public int WaveId;
        public ulong Seed;

        public Terrain Terrain;
        public int LaneCount;
        public int PocketCount;
        public int LaneTiles;

        public CreatureSpec[] Deployment;
        /// Per section 3's "five creature snapshots: ... HP". The engine
        /// derives HP from Stats, so this is redundant TODAY and checked
        /// against that derivation at Validate. It is stored anyway because
        /// section 3 says so and because carry-over damage would need it.
        public int[] DeploymentHp;

        public int RallyTick = -1;       // -1 == absent
        public int RallyCreature = -1;

        public string EngineVersion = SimVersion.Value;

        /// Rebuilds the lane the run used. The stored geometry is not trusted -
        /// it is checked against what the family produces, at Validate.
        public Lane BuildLane()
        {
            switch (Terrain)
            {
                case Terrain.Defile: return Lane.Defile();
                default: throw new ReplayFormatException("unknown terrain family " + (int)Terrain);
            }
        }

        /// Everything the record asserts about itself, checked against what
        /// this engine would build. A replay that disagrees is rejected rather
        /// than re-simulated into something else - the same treatment
        /// WaveDef.Validate gives a composition violation, and for the same
        /// reason.
        public void Validate()
        {
            // EVERY field here arrives from untrusted bytes. Sim.Replay is the
            // server verification path, so a field that is read but not bounded
            // is not a cosmetic gap - it is either a crash in the verifier or,
            // worse, a forged run that validates. Both were demonstrated before
            // this method was widened: Pocket 178956971 passed and SIMULATED,
            // because 178956971 * 24 wraps into Lane's 120-entry distance table.
            if (Deployment == null || DeploymentHp == null ||
                Deployment.Length != DeploymentHp.Length)
                throw new ReplayFormatException("deployment and HP arrays disagree");

            if (Deployment.Length > Stats.DeploymentCap)
                throw new ReplayFormatException(
                    "deployment of " + Deployment.Length + " exceeds the cap of " + Stats.DeploymentCap);

            // ForId throws WaveCompositionException, which is a SIBLING of this
            // class's exception rather than a subtype. Every other rejection
            // here is a ReplayFormatException and callers catch that, so an
            // unknown wave id has to be translated at this boundary or it
            // escapes a correctly-written handler.
            WaveDef wave;
            try { wave = WaveDef.ForId(WaveId); }
            catch (WaveCompositionException e)
            { throw new ReplayFormatException("wave id " + WaveId + " is not authored: " + e.Message); }

            var lane = BuildLane();
            if (LaneTiles != lane.Tiles)
                throw new ReplayFormatException(
                    "lane length " + LaneTiles + " disagrees with " + Terrain + "'s " + lane.Tiles);
            if (PocketCount != lane.PocketCount)
                throw new ReplayFormatException(
                    "pocket count " + PocketCount + " disagrees with " + Terrain + "'s " + lane.PocketCount);
            if (LaneCount != wave.LaneCount)
                throw new ReplayFormatException("lane count disagrees with the authored wave");

            for (int c = 0; c < Deployment.Length; c++)
            {
                var d = Deployment[c];

                // Enum casts in Deserialize are unchecked by design - C# permits
                // any int - so range is established here. Species first, because
                // Stats.CreatureHp returns 0 for an unknown one, which would
                // turn the HP cross-check below into a no-op and let a forged
                // record delete a creature by starting it at 0 HP.
                if ((int)d.Species < 0 || (int)d.Species >= SpeciesCount)
                    throw new ReplayFormatException("creature " + c + " has species " + (int)d.Species);
                if ((int)d.Instinct < 0 || (int)d.Instinct >= InstinctCount)
                    throw new ReplayFormatException("creature " + c + " has instinct " + (int)d.Instinct);
                if ((int)d.Trait1 < 0 || (int)d.Trait1 >= TraitCount ||
                    (int)d.Trait2 < 0 || (int)d.Trait2 >= TraitCount)
                    throw new ReplayFormatException("creature " + c + " carries an unknown trait");

                if (d.Pocket < 0 || d.Pocket >= lane.PocketCount)
                    throw new ReplayFormatException(
                        "creature " + c + " is in pocket " + d.Pocket +
                        ", outside " + Terrain + "'s 0.." + (lane.PocketCount - 1));

                if (d.Tier1 < 0 || d.Tier1 > 3 || d.Tier2 < 0 || d.Tier2 > 3)
                    throw new ReplayFormatException("creature " + c + " has a coverage tier outside 0..3");

                int expected = Stats.CreatureHp(d.Species);
                if (DeploymentHp[c] != expected)
                    throw new ReplayFormatException(
                        "creature " + c + " stores HP " + DeploymentHp[c] +
                        " but " + d.Species + " starts at " + expected);
            }

            if (RallyTick < -1) throw new ReplayFormatException("negative rally tick");
            if ((RallyTick < 0) != (RallyCreature < 0))
                throw new ReplayFormatException("rally tick and creature disagree about being absent");
            if (RallyCreature >= Deployment.Length)
                throw new ReplayFormatException("rally names creature " + RallyCreature + ", out of range");
            if (RallyTick >= Stats.HardTickCap)
                throw new ReplayFormatException(
                    "rally at tick " + RallyTick + ", at or past the hard cap of " + Stats.HardTickCap +
                    " - no run reaches it, so the input could never have been consumed");

            // solo_execution 9.4: "a replay recorded under an earlier engine
            // version renders its stored outcome and is not re-simulated." That
            // rule had no implementation - the field was written and never
            // compared - so a replay from a superseded engine re-simulated
            // silently into a different outcome, which on the server path reads
            // an honest player's raid as a mismatch. This is where it becomes
            // real. Callers wanting the stored-outcome-with-a-notice behaviour
            // catch this and render rather than re-running.
            if (EngineVersion != SimVersion.Value)
                throw new ReplayFormatException(
                    "replay was recorded by engine " + EngineVersion + ", this engine is " +
                    SimVersion.Value + " - show its stored outcome rather than re-simulating it");
        }

        // Enum widths, checked by a test against the enums themselves rather
        // than trusted here. System.Enum.IsDefined allocates and reflects, and
        // Convert.ToInt32 boxes; the enums are contiguous from 0, so a bare int
        // comparison is both cheaper and clearer about what it enforces.
        private const int SpeciesCount = 6;
        private const int InstinctCount = 6;
        private const int TraitCount = 2;     // None, Chill

        public byte[] Serialize()
        {
            var version = Encoding.UTF8.GetBytes(EngineVersion ?? "");
            if (version.Length > 255) throw new ReplayFormatException("engine version string too long");

            int size = 4 + 2 + 1 + version.Length
                     + 4 + 8 + 4 + 4 + 4 + 4
                     + 4 + Deployment.Length * 8 * 4
                     + 4 + 4;

            var b = new byte[size];
            int i = 0;

            PutU32(b, ref i, Magic);
            PutU16(b, ref i, FormatVersion);
            b[i++] = (byte)version.Length;
            Buffer.BlockCopy(version, 0, b, i, version.Length); i += version.Length;

            PutI32(b, ref i, WaveId);
            PutU64(b, ref i, Seed);
            PutI32(b, ref i, (int)Terrain);
            PutI32(b, ref i, LaneCount);
            PutI32(b, ref i, PocketCount);
            PutI32(b, ref i, LaneTiles);

            PutI32(b, ref i, Deployment.Length);
            for (int c = 0; c < Deployment.Length; c++)
            {
                var d = Deployment[c];
                PutI32(b, ref i, (int)d.Species);
                PutI32(b, ref i, (int)d.Trait1);
                PutI32(b, ref i, d.Tier1);
                PutI32(b, ref i, (int)d.Trait2);
                PutI32(b, ref i, d.Tier2);
                PutI32(b, ref i, (int)d.Instinct);
                PutI32(b, ref i, d.Pocket);
                PutI32(b, ref i, DeploymentHp[c]);
            }

            PutI32(b, ref i, RallyTick);
            PutI32(b, ref i, RallyCreature);

            return b;
        }

        public static Replay Deserialize(ReadOnlySpan<byte> b)
        {
            int i = 0;
            if (b.Length < 11) throw new ReplayFormatException("too short to be a replay");
            if (GetU32(b, ref i) != Magic) throw new ReplayFormatException("bad magic");

            ushort format = GetU16(b, ref i);
            if (format != FormatVersion)
                throw new ReplayFormatException("format version " + format + ", expected " + FormatVersion);

            int versionLen = b[i++];
            if (i + versionLen > b.Length) throw new ReplayFormatException("truncated engine version");
            var r = new Replay { EngineVersion = Encoding.UTF8.GetString(b.Slice(i, versionLen).ToArray()) };
            i += versionLen;

            if (i + 28 > b.Length) throw new ReplayFormatException("truncated header");
            r.WaveId      = GetI32(b, ref i);
            r.Seed        = GetU64(b, ref i);
            r.Terrain     = (Terrain)GetI32(b, ref i);
            r.LaneCount   = GetI32(b, ref i);
            r.PocketCount = GetI32(b, ref i);
            r.LaneTiles   = GetI32(b, ref i);

            if (i + 4 > b.Length) throw new ReplayFormatException("truncated deployment count");
            int count = GetI32(b, ref i);
            if (count < 0 || count > Stats.DeploymentCap)
                throw new ReplayFormatException("deployment count " + count + " out of range");
            if (i + count * 8 * 4 + 8 > b.Length) throw new ReplayFormatException("truncated deployment");

            r.Deployment = new CreatureSpec[count];
            r.DeploymentHp = new int[count];
            for (int c = 0; c < count; c++)
            {
                r.Deployment[c] = new CreatureSpec
                {
                    Species  = (Species)GetI32(b, ref i),
                    Trait1   = (Trait)GetI32(b, ref i),
                    Tier1    = GetI32(b, ref i),
                    Trait2   = (Trait)GetI32(b, ref i),
                    Tier2    = GetI32(b, ref i),
                    Instinct = (Instinct)GetI32(b, ref i),
                    Pocket   = GetI32(b, ref i)
                };
                r.DeploymentHp[c] = GetI32(b, ref i);
            }

            r.RallyTick     = GetI32(b, ref i);
            r.RallyCreature = GetI32(b, ref i);

            // A record is fixed-layout, so anything after the last field is not
            // part of it. Accepting the remainder silently means a truncated
            // file padded back to length, or two records concatenated, reads as
            // a valid first record with no complaint.
            if (i != b.Length)
                throw new ReplayFormatException(
                    "record is " + i + " bytes but the buffer is " + b.Length + " - trailing data");

            return r;
        }

        // Explicit little-endian shifts rather than BitConverter, which is
        // host-endian. Nothing in the layout may depend on the machine.
        private static void PutU16(byte[] b, ref int i, ushort v)
        { b[i++] = (byte)v; b[i++] = (byte)(v >> 8); }

        private static void PutU32(byte[] b, ref int i, uint v)
        { b[i++] = (byte)v; b[i++] = (byte)(v >> 8); b[i++] = (byte)(v >> 16); b[i++] = (byte)(v >> 24); }

        private static void PutI32(byte[] b, ref int i, int v) => PutU32(b, ref i, unchecked((uint)v));

        private static void PutU64(byte[] b, ref int i, ulong v)
        { PutU32(b, ref i, (uint)v); PutU32(b, ref i, (uint)(v >> 32)); }

        private static ushort GetU16(ReadOnlySpan<byte> b, ref int i)
        { ushort v = (ushort)(b[i] | (b[i + 1] << 8)); i += 2; return v; }

        private static uint GetU32(ReadOnlySpan<byte> b, ref int i)
        {
            uint v = (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));
            i += 4; return v;
        }

        private static int GetI32(ReadOnlySpan<byte> b, ref int i) => unchecked((int)GetU32(b, ref i));

        private static ulong GetU64(ReadOnlySpan<byte> b, ref int i)
        {
            ulong lo = GetU32(b, ref i);
            ulong hi = GetU32(b, ref i);
            return lo | (hi << 32);
        }
    }
}
