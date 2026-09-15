using System;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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

        /// Raw text, not PostAsJsonAsync: these cases are about bodies that
        /// a serializer would never produce.
        private Task<HttpResponseMessage> PostRaw(string path, string json) =>
            _http.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));

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
        public async Task ABodyThatIsNotJsonAtAllIsRejectedWithTwoHundred()
        {
            // Model binding fails BEFORE the handler or any endpoint filter
            // runs, so without the middleware in Program.cs this is ASP.NET's
            // own 400 rather than the service's verdict. api's SimVerdict is a
            // three-way union - verified / rejected / unavailable - and it
            // maps every non-2xx to `unavailable`, which leaves the issuance
            // live for a retry that will fail identically forever. A body this
            // service cannot parse is a FACT ABOUT THE SUBMISSION, exactly
            // like the forged bytes above, and must arrive as one.
            var res = await PostRaw("/internal/simulate", "{ not json at all");
            Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);

            var body = await res.Content.ReadFromJsonAsync<SimulateResponse>();
            Assert.Equal("rejected", body!.Verdict);
            Assert.Equal("replay_malformed", body.Reason);
            Assert.Null(body.Outcome);
            Assert.Null(body.Echo);
            Assert.Equal(SimVersion.Value, body.EngineVersion);

            // THE SHAPES MUST NOT DRIFT. The middleware builds its verdict
            // with SimulateResponse.Rejected, the same factory the handler
            // uses, so the bytes on the wire must be identical to a rejection
            // that came out of the handler - same member order, same casing,
            // same engineVersion. Hand-written JSON in the middleware, or
            // different serializer options on WriteAsJsonAsync, would pass
            // every assertion above (ReadFromJsonAsync is case-insensitive)
            // and still hand api a differently-shaped body. Compared as raw
            // text for exactly that reason.
            var fromHandler = await PostRaw("/internal/simulate", "{\"replay\":\"!!!!\"}");
            Assert.Equal(System.Net.HttpStatusCode.OK, fromHandler.StatusCode);
            Assert.Equal(await fromHandler.Content.ReadAsStringAsync(),
                         await res.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task AWellFormedBodyWhoseReplayIsNotAStringIsRejectedWithTwoHundred()
        {
            // The brief for this task expected `{}` - a well-formed body with
            // no `replay` - to be the well-formed binding failure. It is not:
            // System.Text.Json fills the record's sole constructor parameter
            // with null when the member is absent, so `{}` reaches the handler
            // and is answered there (see the test below). The well-formed body
            // that genuinely cannot bind is one whose `replay` is present but
            // of the wrong JSON type - a number, object or array where the
            // record declares a string. That throws BadHttpRequestException
            // out of binding exactly as unparseable bytes do, and it is this
            // case, not `{}`, that the middleware is what rescues.
            foreach (var wrongType in new[] { "123", "{\"a\":1}", "[1,2]" })
            {
                var res = await PostRaw("/internal/simulate",
                                        "{\"replay\":" + wrongType + "}");
                Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);

                var body = await res.Content.ReadFromJsonAsync<SimulateResponse>();
                Assert.Equal("rejected", body!.Verdict);
                Assert.Equal("replay_malformed", body.Reason);
                Assert.Null(body.Outcome);
            }
        }

        [Fact]
        public async Task AWellFormedBodyMissingReplayIsRejectedWithTwoHundred()
        {
            // NOT a middleware test, and deliberately kept as a separate one
            // so that is not mistaken. `{}` BINDS - to SimulateRequest(null) -
            // and is answered by SimulateEndpoint.Handle, whose
            // Convert.FromBase64String(null ?? "") yields zero bytes and whose
            // Replay.Deserialize then throws ReplayFormatException. This
            // asserts that path stays 200 rather than that the middleware
            // works; it was already green before the middleware existed and is
            // still green with the middleware deleted. Its value is as a
            // regression guard on Handle's `request.Replay ?? ""`, which is
            // the single character standing between an absent member and a
            // NullReferenceException 500.
            var res = await _http.PostAsJsonAsync("/internal/simulate", new { });
            Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);

            var body = await res.Content.ReadFromJsonAsync<SimulateResponse>();
            Assert.Equal("rejected", body!.Verdict);
            Assert.Equal("replay_malformed", body.Reason);
            Assert.Null(body.Outcome);
            Assert.Null(body.Echo);
            Assert.Equal(SimVersion.Value, body.EngineVersion);
        }

        [Fact]
        public async Task TheTrailingSlashSpellingOfTheRouteGetsTheSameVerdict()
        {
            // Routing matches "/internal/simulate/" to the SAME endpoint, so
            // it must get the same answer. This is the case that rules out
            // scoping the middleware on ctx.Request.Path: PathString equality
            // is OrdinalIgnoreCase, so it forgives "/Internal/Simulate", but
            // it does not forgive the trailing slash - a path-compared `when`
            // clause answers this request with a bare 400 while the identical
            // body on the identical endpoint gets a verdict.
            foreach (var spelling in new[] { "/internal/simulate/", "/Internal/Simulate" })
            {
                var res = await PostRaw(spelling, "{ not json at all");
                Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);

                var body = await res.Content.ReadFromJsonAsync<SimulateResponse>();
                Assert.Equal("rejected", body!.Verdict);
                Assert.Equal("replay_malformed", body.Reason);
            }
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
    /// The `when` clause on Program.cs's always-200 middleware, tested on its
    /// own.
    ///
    /// It cannot be exercised through the service's real routes: the only
    /// other one is GET /healthz, which takes no body and so can never fail
    /// model binding, and an unmatched path never binds either. With today's
    /// two routes, deleting the `when` clause changes NO observable behaviour
    /// - which is precisely why it needs a test that constructs the situation
    /// the clause exists for, rather than a test that would pass with the
    /// clause gone.
    ///
    /// The situation is "a BadHttpRequestException reaches that middleware
    /// from something that is not the Simulate endpoint" - the second
    /// body-taking endpoint someone adds later. An IStartupFilter appends
    /// middleware BELOW the real pipeline (after next(app), so after
    /// UseEndpoints, which falls through for an unmatched path) and throws
    /// from there: the exception then unwinds through the always-200
    /// middleware exactly as a real binding failure would, with a real
    /// non-Simulate endpoint - or none - on the context.
    public class AlwaysTwoHundredScopingTests
    {
        private sealed class ThrowBelowThePipeline : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                app =>
                {
                    next(app);
                    app.Use(async (ctx, chain) =>
                    {
                        if (ctx.Request.Path == "/probe/no-endpoint")
                            throw new BadHttpRequestException("probe", 400);

                        if (ctx.Request.Path == "/probe/other-endpoint")
                        {
                            // A DIFFERENT endpoint, named - the shape a second
                            // real route would have. Exercises the name
                            // comparison itself, not just the null check.
                            ctx.SetEndpoint(new Endpoint(
                                _ => Task.CompletedTask,
                                new EndpointMetadataCollection(
                                    new EndpointNameMetadata("SomethingElse")),
                                "probe-other-endpoint"));
                            throw new BadHttpRequestException("probe", 400);
                        }

                        await chain();
                    });
                };
        }

        private sealed class Factory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder) =>
                builder.ConfigureServices(
                    s => s.AddSingleton<IStartupFilter, ThrowBelowThePipeline>());
        }

        [Theory]
        [InlineData("/probe/no-endpoint")]
        [InlineData("/probe/other-endpoint")]
        public async Task ABindingFailureSomewhereELSEKeepsItsFourHundred(string path)
        {
            // Without the `when` clause this middleware would answer EVERY
            // endpoint's binding failure with sim's own verdict - handing api
            // a confident "this replay is malformed" for a request that was
            // never a replay submission at all, and hiding the real 400 from
            // whoever added that endpoint.
            using var factory = new Factory();
            var res = await factory.CreateClient().GetAsync(path);

            Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
            var text = await res.Content.ReadAsStringAsync();
            Assert.DoesNotContain("replay_malformed", text);
            Assert.DoesNotContain("rejected", text);
        }

        [Fact]
        public async Task TheSimulateEndpointStillGetsItsVerdictUnderTheSameHost()
        {
            // Guards the test above from passing for the wrong reason. If the
            // IStartupFilter displaced the always-200 middleware rather than
            // sitting below it, every assertion above would still hold while
            // proving nothing about scoping.
            using var factory = new Factory();
            var res = await factory.CreateClient().PostAsync("/internal/simulate",
                new StringContent("{ not json at all", Encoding.UTF8, "application/json"));

            Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadFromJsonAsync<SimulateResponse>();
            Assert.Equal("rejected", body!.Verdict);
            Assert.Equal("replay_malformed", body.Reason);
        }
    }
}
