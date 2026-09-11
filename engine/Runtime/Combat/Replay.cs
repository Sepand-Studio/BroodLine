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

        /// The most creatures this ENCODING could describe. Deliberately not
        /// Stats.DeploymentCap - see ValidateFormat.
        private const int FormatDeploymentCeiling = 255;

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
            var lane = Lane.ForFamily(Terrain);
            if (lane == null) throw new ReplayFormatException("unknown terrain family " + (int)Terrain);
            return lane;
        }

        /// Whether this engine can re-simulate the record at all.
        ///
        /// solo_execution 9.4 wants a superseded replay to render its stored
        /// outcome WITH A NOTICE rather than be re-run. Validate throws on a
        /// mismatch, which is right for the verification path but makes a
        /// replay viewer catch an exception to decide what to draw. This is the
        /// same question asked without control flow: branch on it, show the
        /// notice, and never call Sim.Replay.
        public bool IsFromThisEngine => EngineVersion == SimVersion.Value;

        /// Self-consistency, in terms no engine version can disagree about.
        ///
        /// This half runs inside Deserialize, so there is no such thing as an
        /// unvalidated Replay object for a caller to forget about. The split
        /// exists because the OTHER half - enum widths, the authored wave, the
        /// lane's geometry, the HP a species starts at - is all THIS engine's,
        /// and a record from a later one legitimately disagrees with every bit
        /// of it. Running those against a future record reports "creature 0
        /// carries an unknown trait", which sends the reader after a forgery
        /// that is not there. That diagnosis belongs to Validate, behind the
        /// version check.
        ///
        /// What is here is what protects the READER and nothing else: array
        /// agreement, and rally indices, which callers feed straight to
        /// TryRally.
        ///
        /// Stats.DeploymentCap is NOT here, and that is the point of the line.
        /// It is a balance constant - Stats.cs says so - so a later engine that
        /// raises it to 6 writes a perfectly good six-creature record, and this
        /// half rejected it from inside Deserialize with "deployment count 6 out
        /// of range". No Replay object was produced at all, so IsFromThisEngine
        /// could not even be consulted and solo_execution 9.4's
        /// render-the-stored-outcome path was unreachable. That is the same
        /// misdiagnosis the version-first ordering below was written to
        /// eliminate, one layer down. The cap now lives once, behind the
        /// version check, in Deployments.Problem.
        public void ValidateFormat()
        {
            if (Deployment == null || DeploymentHp == null ||
                Deployment.Length != DeploymentHp.Length)
                throw new ReplayFormatException("deployment and HP arrays disagree");

            if (RallyTick < -1) throw new ReplayFormatException("negative rally tick");
            if ((RallyTick < 0) != (RallyCreature < 0))
                throw new ReplayFormatException("rally tick and creature disagree about being absent");

            // BOTH bounds. Only the upper one was checked, so -7 and -1 and
            // -2000000000 all read as "absent" - six distinct byte encodings
            // validating to one outcome, in a format whose whole job is to be
            // the canonical description of a run.
            if (RallyCreature < -1 || RallyCreature >= Deployment.Length)
                throw new ReplayFormatException("rally names creature " + RallyCreature + ", out of range");
        }

        /// Everything the record asserts about itself, checked against what
        /// THIS engine would build. A replay that disagrees is rejected rather
        /// than re-simulated into something else - the same treatment
        /// WaveDef.Validate gives a composition violation, and for the same
        /// reason.
        public void Validate() => Validate(out _, out _);

        /// Validate, handing back the wave and lane it had to build anyway.
        ///
        /// Sim.Replay needs both immediately afterwards and used to construct a
        /// second copy of each, which is a WaveDef, a Lane and its 120-entry
        /// distance table rebuilt per verification for nothing.
        internal void Validate(out WaveDef wave, out Lane lane)
        {
            // FIRST, before any bound below. Every one of them is this engine's
            // width, and a legitimate record from a LATER engine - one carrying
            // the seven Traits combat_engine names - trips them. Checked last,
            // as it was, an out-of-range Species or Trait or wave id on a
            // future record was reported as corruption; the version is the
            // honest diagnosis and it is available before anything is parsed
            // against local content.
            //
            // solo_execution 9.4: "a replay recorded under an earlier engine
            // version renders its stored outcome WITH A NOTICE and is not
            // re-simulated." The notice is the operative half: a superseded
            // replay must visibly say it was not re-run, not silently show a
            // number.
            // Callers wanting that behaviour branch on IsFromThisEngine rather
            // than catching this.
            if (!IsFromThisEngine)
                throw new ReplayFormatException(
                    "replay was recorded by engine " + EngineVersion + ", this engine is " +
                    SimVersion.Value + " - show its stored outcome rather than re-simulating it");

            ValidateFormat();

            // ForId throws WaveCompositionException, which is a SIBLING of this
            // class's exception rather than a subtype. Every other rejection
            // here is a ReplayFormatException and callers catch that, so an
            // unknown wave id has to be translated at this boundary or it
            // escapes a correctly-written handler.
            try { wave = WaveDef.ForId(WaveId); }
            catch (WaveCompositionException e)
            { throw new ReplayFormatException("wave id " + WaveId + " is not authored: " + e.Message); }

            lane = BuildLane();
            if (LaneTiles != lane.Tiles)
                throw new ReplayFormatException(
                    "lane length " + LaneTiles + " disagrees with " + Terrain + "'s " + lane.Tiles);
            if (PocketCount != lane.PocketCount)
                throw new ReplayFormatException(
                    "pocket count " + PocketCount + " disagrees with " + Terrain + "'s " + lane.PocketCount);
            if (LaneCount != wave.LaneCount)
                throw new ReplayFormatException("lane count disagrees with the authored wave");

            // ONE definition of what a simulatable deployment is, shared with
            // the SimRunner constructor. Enum casts in Deserialize are
            // unchecked by design - C# permits any int - so range is
            // established here, and the play path establishes it from the same
            // source rather than from a second, shorter list.
            var problem = Deployments.Problem(Deployment, lane);
            if (problem != null) throw new ReplayFormatException(problem);

            for (int c = 0; c < Deployment.Length; c++)
            {
                int expected = Stats.CreatureHp(Deployment[c].Species);
                if (DeploymentHp[c] != expected)
                    throw new ReplayFormatException(
                        "creature " + c + " stores HP " + DeploymentHp[c] +
                        " but " + Deployment[c].Species + " starts at " + expected);
            }

            if (RallyTick >= Stats.HardTickCap)
                throw new ReplayFormatException(
                    "rally at tick " + RallyTick + ", at or past the hard cap of " + Stats.HardTickCap +
                    " - no run reaches it, so the input could never have been consumed");
        }

        /// A deep copy. Deployment and DeploymentHp are arrays, so a shallow
        /// field copy would leave two Replays sharing them - which is exactly
        /// the aliasing SimRunner.ReadRecord exists to prevent.
        ///
        /// Null-tolerant on both arrays. Deserialize can no longer produce a
        /// Replay with either one null, but the fields are public and settable
        /// and this is the only method that dereferences them without a check -
        /// a hand-built Replay copied for a test raised a bare
        /// NullReferenceException from inside a method named Copy.
        public Replay Copy()
        {
            return new Replay
            {
                WaveId = WaveId,
                Seed = Seed,
                Terrain = Terrain,
                LaneCount = LaneCount,
                PocketCount = PocketCount,
                LaneTiles = LaneTiles,
                Deployment = (CreatureSpec[])Deployment?.Clone(),
                DeploymentHp = (int[])DeploymentHp?.Clone(),
                RallyTick = RallyTick,
                RallyCreature = RallyCreature,
                EngineVersion = EngineVersion
            };
        }

        public byte[] Serialize()
        {
            // Copy() tolerates a null Deployment, and the fields are public and
            // settable, so a hand-built record reaches here - and this was the
            // one public entry point that answered with a bare
            // NullReferenceException instead of a ReplayFormatException. The
            // test that asserts Copy() tolerates such a record was, in effect,
            // blessing the object that crashed here.
            if (Deployment == null || DeploymentHp == null ||
                Deployment.Length != DeploymentHp.Length)
                throw new ReplayFormatException("deployment and HP arrays disagree");

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

            // A FORMAT ceiling, not this engine's roster cap. This bound exists
            // to stop a forged length allocating wildly before the buffer check
            // below can run; what a roster may legally contain is
            // Stats.DeploymentCap, a balance constant, and enforcing it here
            // made a later engine's legitimate record undecodable rather than
            // superseded. 255 is what one byte of roster could ever mean.
            if (count < 0 || count > FormatDeploymentCeiling)
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

            // An unvalidated Replay is not a thing this returns. Handing one
            // back and trusting every caller to remember a second call is a
            // rule, and the batch that HARDENED Validate wrote two new callers
            // that skipped it - one of which fed the unbounded RallyCreature
            // straight into TryRally. The engine-specific half stays in
            // Validate, where a superseded record is diagnosed as superseded
            // rather than as corrupt.
            r.ValidateFormat();
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
