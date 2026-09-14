using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Service.Tests
{
    public class SimulateTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _http;
        public SimulateTests(WebApplicationFactory<Program> f) => _http = f.CreateClient();

        /// Wave 6, one Courser, the authored wave the bundle carries. Built
        /// from the engine rather than from a fixture file, so a rules change
        /// breaks this loudly instead of drifting.
        private static Replay ValidRecord()
        {
            var record = new Replay
            {
                WaveId = 6,
                Seed = 0x5EEDu,
                Terrain = Terrain.Defile,
                LaneCount = 1,
                Deployment = new[]
                {
                    new CreatureSpec { Species = Species.Vetch, Trait1 = Trait.Chill, Tier1 = 1,
                                       Trait2 = Trait.None, Tier2 = 0,
                                       Instinct = Instinct.Vanguard, Pocket = 0 },
                },
            };
            var lane = record.BuildLane();
            record.PocketCount = lane.PocketCount;
            record.LaneTiles = lane.Tiles;
            record.DeploymentHp = new[] { Stats.CreatureHp(Species.Vetch) };
            return record;
        }

        private Task<HttpResponseMessage> Post(byte[] bytes) =>
            _http.PostAsJsonAsync("/internal/simulate",
                new { replay = Convert.ToBase64String(bytes) });

        [Fact]
        public async Task AnHonestRecordVerifiesToTheSameHashAsAnInProcessRun()
        {
            var record = ValidRecord();
            var expected = Broodline.Sim.Combat.Sim.Replay(record.Copy());

            var res = await Post(record.Serialize());
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<SimulateResponse>();

            Assert.Equal("verified", body!.Verdict);
            // The hash crosses the wire as a DECIMAL STRING. Outcome.Hash is a
            // ulong and JSON numbers are IEEE 754 doubles - values above 2^53
            // arrive silently wrong, and a hash is uniformly distributed over
            // the full 64 bits, so "silently wrong" is the common case rather
            // than the edge one.
            Assert.Equal(expected.Hash.ToString(), body.Outcome!.Hash);
            Assert.Equal(expected.Result.ToString(), body.Outcome.Result);
            Assert.Equal(expected.IntegrityRemaining, body.Outcome.IntegrityRemaining);
            Assert.Equal(6, body.Echo!.WaveId);
        }

        [Fact]
        public async Task ForgedBytesAreRejectedWithTwoHundred()
        {
            // A rejection is a FACT ABOUT THE SUBMISSION, not a fault in the
            // service. api decides whether the issuance survives by reading
            // the status code, so these must not share one - design 3.2.
            var res = await Post(new byte[] { 1, 2, 3, 4, 5 });
            Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);

            var body = await res.Content.ReadFromJsonAsync<SimulateResponse>();
            Assert.Equal("rejected", body!.Verdict);
            Assert.Equal("replay_malformed", body.Reason);
            Assert.Null(body.Outcome);
        }

        [Fact]
        public async Task ARecordFromAnotherEngineIsRejectedRatherThanReSimulated()
        {
            // solo_execution 9.4's show-the-stored-outcome rule is the
            // VIEWER's. On the payout path an unverifiable submission is a
            // rejection, or the server pays out a number it never checked -
            // design 2.3.
            var record = ValidRecord();
            record.EngineVersion = "0.1.0";

            var body = await (await Post(record.Serialize()))
                .Content.ReadFromJsonAsync<SimulateResponse>();

            Assert.Equal("rejected", body!.Verdict);
            Assert.Equal("engine_too_old", body.Reason);
            Assert.Equal(SimVersion.Value, body.EngineVersion);
        }

        [Fact]
        public async Task ARecordViolatingTheRulesIsRejectedAsRulesViolated()
        {
            // Distinct from malformed: these bytes decode perfectly. The
            // record simply describes a run this engine's rules forbid.
            var record = ValidRecord();
            record.DeploymentHp = new[] { 9999 };

            var body = await (await Post(record.Serialize()))
                .Content.ReadFromJsonAsync<SimulateResponse>();

            Assert.Equal("rejected", body!.Verdict);
            Assert.Equal("rules_violated", body.Reason);
        }

        [Fact]
        public async Task TheServiceHoldsNoDatabaseHandle()
        {
            // design 3.1, asserted rather than promised. sim is a pure
            // function behind HTTP; the moment it grows a connection string it
            // grows a pool against the Cloud SQL cap that solo_execution 5.7
            // names as the thing that runs out before CPU does.
            var assembly = typeof(Program).Assembly;
            foreach (var referenced in assembly.GetReferencedAssemblies())
            {
                Assert.DoesNotContain("Npgsql", referenced.Name ?? "");
                Assert.DoesNotContain("Google.Cloud.Storage", referenced.Name ?? "");
            }
        }
    }
}
