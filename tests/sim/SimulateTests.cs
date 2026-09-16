using System;
using System.Linq;
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

        /// Five creatures, every field distinct from its neighbours', across
        /// Defile's five pockets - the shape design 6.2's comparison actually
        /// runs against, rather than ValidRecord's single Vetch.
        ///
        /// Creature 4 carries TIER 0 on both slots deliberately. Zero is the
        /// engine's "the trait is not really carried" (Stats.SplashTargets and
        /// Attacks.DamageTaken both name it), and it is the one tier api can
        /// never store: drizzle/0005_loop.sql's coverage_tier_N_not_zero
        /// refuses it so that null - an Aberrant, which has no coverage - can
        /// never be conflated with it. It is therefore the value that has to
        /// cross this boundary as null.
        private static Replay FiveCreatureRecord()
        {
            var record = new Replay
            {
                WaveId = 6,
                Seed = 0x5EEDu,
                Terrain = Terrain.Defile,
                LaneCount = 1,
                Deployment = new[]
                {
                    new CreatureSpec { Species = Species.Pale, Trait1 = Trait.Chill, Tier1 = 1,
                                       Trait2 = Trait.Carapace, Tier2 = 2,
                                       Instinct = Instinct.Vanguard, Pocket = 0 },
                    new CreatureSpec { Species = Species.Vetch, Trait1 = Trait.Taunt, Tier1 = 3,
                                       Trait2 = Trait.Carapace, Tier2 = 1,
                                       Instinct = Instinct.Bloodscent, Pocket = 1 },
                    new CreatureSpec { Species = Species.Ember, Trait1 = Trait.Splash, Tier1 = 2,
                                       Trait2 = Trait.Carapace, Tier2 = 3,
                                       Instinct = Instinct.Overwatch, Pocket = 2 },
                    new CreatureSpec { Species = Species.Hollow, Trait1 = Trait.Carapace, Tier1 = 1,
                                       Trait2 = Trait.None, Tier2 = 0,
                                       Instinct = Instinct.LastStand, Pocket = 3 },
                    new CreatureSpec { Species = Species.Loam, Trait1 = Trait.Chill, Tier1 = 0,
                                       Trait2 = Trait.Taunt, Tier2 = 0,
                                       Instinct = Instinct.Skittish, Pocket = 4 },
                },
            };
            var lane = record.BuildLane();
            record.PocketCount = lane.PocketCount;
            record.LaneTiles = lane.Tiles;
            record.DeploymentHp = new int[record.Deployment.Length];
            for (int c = 0; c < record.Deployment.Length; c++)
                record.DeploymentHp[c] = Stats.CreatureHp(record.Deployment[c].Species);
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
        public async Task TheEchoCarriesTheDeploymentItSimulated()
        {
            // design 6.2. api stored the deployment it RESOLVED FROM OWNED ROWS
            // at issuance (Task 8); this is the deployment the submitted replay
            // actually claimed, re-read by the one parser that exists. Task 10
            // compares them, and a modified client that deployed a creature its
            // player does not own is the thing that comparison catches - the
            // hole Phase 5 shipped knowingly.
            //
            // ORDER IS PART OF THE ANSWER. SimState indexes its parallel arrays
            // by deployment order and resolveDeployment iterates in REQUEST
            // order for that reason, so an echo that agreed as a SET and
            // disagreed as a SEQUENCE would describe a different wave. Asserted
            // positionally, never by searching.
            var record = FiveCreatureRecord();

            var res = await Post(record.Serialize());
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<SimulateResponse>();

            Assert.Equal("verified", body!.Verdict);
            var echo = body.Echo!.Deployment;
            Assert.Equal(5, echo.Length);

            Assert.Equal("Pale", echo[0].Species);
            Assert.Equal("Chill", echo[0].Trait1);
            Assert.Equal(1, echo[0].Tier1);
            Assert.Equal("Carapace", echo[0].Trait2);
            Assert.Equal(2, echo[0].Tier2);
            Assert.Equal("Vanguard", echo[0].Instinct);
            Assert.Equal(0, echo[0].Pocket);

            Assert.Equal("Vetch", echo[1].Species);
            Assert.Equal("Taunt", echo[1].Trait1);
            Assert.Equal(3, echo[1].Tier1);
            Assert.Equal("Bloodscent", echo[1].Instinct);
            Assert.Equal(1, echo[1].Pocket);

            Assert.Equal("Ember", echo[2].Species);
            Assert.Equal("Splash", echo[2].Trait1);
            Assert.Equal(2, echo[2].Tier1);
            Assert.Equal("Overwatch", echo[2].Instinct);
            Assert.Equal(2, echo[2].Pocket);

            Assert.Equal("Hollow", echo[3].Species);
            Assert.Equal("Carapace", echo[3].Trait1);
            Assert.Equal("LastStand", echo[3].Instinct);
            Assert.Equal(3, echo[3].Pocket);

            Assert.Equal("Loam", echo[4].Species);
            Assert.Equal("Skittish", echo[4].Instinct);
            Assert.Equal(4, echo[4].Pocket);
        }

        [Fact]
        public async Task TraitsAndSpeciesCrossAsNamesRatherThanAsOrdinals()
        {
            // THE DIRECTION OF THE TRANSLATION IS THE DESIGN, and it only runs
            // here. api holds trait NAMES, off the creature row's trait_N text
            // columns; the engine holds a Trait enum. Somebody has to translate,
            // and sim translating ORDINAL -> NAME is the only arrangement in
            // which api still never learns what a trait is.
            //
            // The other direction would put a NAME -> ORDINAL table inside api,
            // and that table has an unknown-name case. Answering it with None -
            // the obvious default, and 0 - would make a forged "Bogus" compare
            // equal to a simulated Trait.None slot, so the forgery would read as
            // legitimate. This direction has no such case: Deployments.Problem
            // has already refused every ordinal outside 0..TraitCount-1 before
            // this echo is built, so ToString() always names a declared member
            // and can never fall back to printing a number.
            //
            // Pinned as LITERAL STRINGS, not as Trait.Chill.ToString(), because
            // the latter agrees with itself no matter what the enum is renamed
            // to - and these names are the api-side row values 'Chill',
            // 'Taunt', 'Splash', 'Carapace' that config/bundles' traits.json
            // authors. A rename on either side has to break something.
            var record = FiveCreatureRecord();

            var body = await (await Post(record.Serialize()))
                .Content.ReadFromJsonAsync<SimulateResponse>();

            var echo = body!.Echo!.Deployment;
            Assert.Equal(new[] { "Chill", "Taunt", "Splash", "Carapace", "Chill" },
                         echo.Select(d => d.Trait1).ToArray());
            Assert.Equal(new[] { "Pale", "Vetch", "Ember", "Hollow", "Loam" },
                         echo.Select(d => d.Species).ToArray());
            Assert.Equal("None", echo[3].Trait2);

            // And the raw bytes, because ReadFromJsonAsync would happily bind a
            // JSON NUMBER into a string-typed member's place if the serializer
            // ever started emitting the enum as its ordinal.
            var text = await (await Post(record.Serialize())).Content.ReadAsStringAsync();
            Assert.Contains("\"trait1\":\"Chill\"", text);
            Assert.Contains("\"species\":\"Pale\"", text);
        }

        [Fact]
        public async Task AbsentCoverageEchoesAsNullRatherThanZero()
        {
            // data_model 2: an Aberrant has no coverage tier, so a TraitInstance
            // holding one carries null rather than 0 - "null and zero must not
            // be conflated; zero would sort and display as less than tier I".
            // drizzle/0005_loop.sql enforces exactly that with
            // coverage_tier_N_not_zero, so a stored spec's tier is null or 1..3
            // and NEVER 0.
            //
            // The engine spells the same state 0, because CreatureSpec.Tier1 is
            // a non-nullable int and Stats.SplashTargets reads 0 as "not really
            // carried". THIS is the one place the two spellings meet, and a
            // non-nullable int here would answer 0 where api holds null - so
            // design 6.2's comparison would reject an HONEST Aberrant on every
            // submission.
            var record = FiveCreatureRecord();

            var body = await (await Post(record.Serialize()))
                .Content.ReadFromJsonAsync<SimulateResponse>();

            var echo = body!.Echo!.Deployment;
            Assert.Null(echo[3].Tier2);   // Trait.None, tier 0
            Assert.Null(echo[4].Tier1);   // Chill at tier 0 - carried in name only
            Assert.Null(echo[4].Tier2);

            // The tiers that ARE coverage still cross as themselves. Without
            // this the assertions above pass for a mapping that nulls
            // everything.
            Assert.Equal(1, echo[0].Tier1);
            Assert.Equal(2, echo[0].Tier2);
            Assert.Equal(3, echo[1].Tier1);

            // JSON null, not an omitted member. api reads this through
            // generated/sim.ts, where `number | null` and `number | undefined`
            // are different types and only one of them compares equal to the
            // null it stored.
            var text = await (await Post(record.Serialize())).Content.ReadAsStringAsync();
            Assert.Contains("\"tier2\":null", text);
        }

        [Fact]
        public async Task TwoCreaturesMayShareAPocketAndTheEchoSaysSoForBoth()
        {
            // POCKET SEMANTICS BELONG TO sim, and this echo is the first thing
            // that makes them observable across the boundary. api bounds a
            // pocket at the two content-independent ends only (routes/wave.ts's
            // MAX_POCKET) and refuses duplicate creature IDS while allowing
            // duplicate POCKETS - which is correct exactly as long as the engine
            // agrees, and until now nothing said it did.
            //
            // Deployments.Problem bounds a pocket against lane.PocketCount and
            // says nothing about sharing, so two creatures in one pocket is a
            // legal wave. Pinned here so that a later engine that starts
            // refusing it cannot do so silently: api would keep issuing those
            // deployments and every submission carrying one would be rejected
            // after the fact, having already committed the creatures.
            var record = FiveCreatureRecord();
            record.Deployment[1].Pocket = 0;
            record.Deployment[2].Pocket = 0;

            var body = await (await Post(record.Serialize()))
                .Content.ReadFromJsonAsync<SimulateResponse>();

            Assert.Equal("verified", body!.Verdict);
            Assert.Equal(new[] { 0, 0, 0, 3, 4 },
                         body.Echo!.Deployment.Select(d => d.Pocket).ToArray());
        }

        [Fact]
        public async Task AnEmptyDeploymentEchoesAnEmptyArrayRatherThanNull()
        {
            // schemas.ts: zero creatures is a deployment the engine simulates
            // (and loses), and an issuance may legitimately hold one. design
            // 6.2's comparison has to be able to tell that apart from "sim did
            // not report a deployment at all" - if both arrive as null, an
            // echo that silently lost its deployment would compare equal to an
            // honest empty one and the check would pass vacuously.
            var record = ValidRecord();
            record.Deployment = new CreatureSpec[0];
            record.DeploymentHp = new int[0];
            record.RallyCreature = -1;

            var res = await Post(record.Serialize());
            var body = await res.Content.ReadFromJsonAsync<SimulateResponse>();

            Assert.Equal("verified", body!.Verdict);
            Assert.NotNull(body.Echo!.Deployment);
            Assert.Empty(body.Echo.Deployment);
            Assert.Contains("\"deployment\":[]",
                            await (await Post(record.Serialize())).Content.ReadAsStringAsync());
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
