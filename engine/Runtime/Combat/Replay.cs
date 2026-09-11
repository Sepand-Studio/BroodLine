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
            if (Deployment == null || DeploymentHp == null ||
                Deployment.Length != DeploymentHp.Length)
                throw new ReplayFormatException("deployment and HP arrays disagree");

            if (Deployment.Length > Stats.DeploymentCap)
                throw new ReplayFormatException(
                    "deployment of " + Deployment.Length + " exceeds the cap of " + Stats.DeploymentCap);

            var lane = BuildLane();
            if (LaneTiles != lane.Tiles)
                throw new ReplayFormatException(
                    "lane length " + LaneTiles + " disagrees with " + Terrain + "'s " + lane.Tiles);
            if (PocketCount != lane.PocketCount)
                throw new ReplayFormatException(
                    "pocket count " + PocketCount + " disagrees with " + Terrain + "'s " + lane.PocketCount);
            if (LaneCount != WaveDef.ForId(WaveId).LaneCount)
                throw new ReplayFormatException("lane count disagrees with the authored wave");

            for (int c = 0; c < Deployment.Length; c++)
            {
                int expected = Stats.CreatureHp(Deployment[c].Species);
                if (DeploymentHp[c] != expected)
                    throw new ReplayFormatException(
                        "creature " + c + " stores HP " + DeploymentHp[c] +
                        " but " + Deployment[c].Species + " starts at " + expected);
            }

            if (RallyTick < -1) throw new ReplayFormatException("negative rally tick");
            if ((RallyTick < 0) != (RallyCreature < 0))
                throw new ReplayFormatException("rally tick and creature disagree about being absent");
            if (RallyCreature >= Deployment.Length)
                throw new ReplayFormatException("rally names creature " + RallyCreature + ", out of range");
        }

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
