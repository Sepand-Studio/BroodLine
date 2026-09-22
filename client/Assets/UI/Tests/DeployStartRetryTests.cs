using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Broodline.Api;
using Broodline.Net;
using Broodline.UI;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Broodline.UI.Tests
{
    /// `DeployScreen.StartAsync` against a backend that is not up yet.
    ///
    /// THE DEFECT THESE PIN. A developer pressed Start on the deploy screen,
    /// `HttpClient` threw "An error occurred while sending the request" - a
    /// transport failure with no HTTP status - and the tap was dropped. A
    /// second press worked, because by then Cloud Run had a container. Both
    /// `api` and `sim` run `min_instance_count = 0`, so every session that
    /// begins after an idle period pays this.
    ///
    /// WHY RETRYING THIS PARTICULAR CALL IS SAFE, restated here because a
    /// test that only shows a retry happening does not show it is allowed:
    /// `POST /v1/wave/start` carries no Idempotency-Key, but
    /// `services/api/src/wave/issuance.ts:244` returns a live issuance rather
    /// than minting a second one, and does so BEFORE the roster checks that
    /// would otherwise refuse `creature_committed`. Its own comment says the
    /// alternative "makes the second call of every honest double-tap a 409".
    /// `services/api/test/wave-start.test.ts:828` is the server-side test
    /// that pins it. What is asserted HERE is the client's half: the SAME
    /// body is replayed, byte for byte, so the server sees the honest
    /// double-tap it is built to answer and not a different deployment.
    public class DeployStartRetryTests
    {
        [Test]
        public void StartAsync_RetriesATransportFailure_AndReturnsTheIssuance()
        {
            var handler = new StubHandler(failures: 2, issuanceId: Fixed);
            var waits = new List<TimeSpan>();
            var notices = new List<int>();

            var response = DeployScreen.StartAsync(
                Api(handler), Deployment(),
                onRetry: (attempt, _) => notices.Add(attempt),
                wait: d => { waits.Add(d); return Task.CompletedTask; })
                .GetAwaiter().GetResult();

            Assert.AreEqual(Fixed, response.IssuanceId, "the third attempt's issuance reached the caller");
            Assert.AreEqual(3, handler.Bodies.Count, "two failures and the answer");
            Assert.AreEqual(2, waits.Count);

            // The player was told, once, at the first failure.
            CollectionAssert.AreEqual(new[] { 1, 2 }, notices);
        }

        [Test]
        public void TheRETRIEDAttempt_CarriesTheSameCreaturesInTheSamePockets()
        {
            // THE PROPERTY THE SERVER'S SAFETY DEPENDS ON. `issueWave`'s
            // check 4 hands back the first issuance for a repeat, and
            // `wave-start.test.ts:828`'s honest-double-tap half is the SAME
            // creatures. A retry that reached the wire with a different
            // deployment - re-ordered, truncated, empty - would be a
            // different request, and the safety argument written on
            // `DeployScreen.StartAsync` would not cover it.
            //
            // ASSERTED ON CONTENT, NOT ONLY ON EQUALITY. An earlier draft of
            // this test compared the four recorded bodies to each other and
            // nothing else, which is a tautology: `StartAsync` holds one
            // request object and hands the same reference to every attempt,
            // so no edit to this file could have made it fail. Reading the
            // ids and pockets off the LAST attempt is a claim that a body
            // rebuilt, re-ordered or dropped on retry would break.
            var a = Creature();
            var b = Creature();
            var deployment = DeployScreen.Build(1, RosterOf(a, b),
                new List<Guid> { b.CreatureId, a.CreatureId });   // b first: order is part of the request
            var handler = new StubHandler(failures: 3, issuanceId: Fixed);

            DeployScreen.StartAsync(Api(handler), deployment,
                wait: _ => Task.CompletedTask).GetAwaiter().GetResult();

            Assert.AreEqual(4, handler.Bodies.Count, "three failures and a fourth attempt");

            var last = JsonConvert.DeserializeObject<WaveStartRequest>(
                handler.Bodies[handler.Bodies.Count - 1]);
            Assert.AreEqual(1, last.WaveId);

            // The engine indexes its parallel arrays by deployment order, and
            // `deploymentMatches` compares the replay against the stored
            // deployment IN ORDER - so b-then-a and a-then-b are different
            // deployments, and a sort anywhere on this path is a defect.
            var sent = new List<Guid>();
            var pockets = new List<int>();
            foreach (var d in last.Deployment) { sent.Add(d.CreatureId); pockets.Add(d.Pocket); }
            CollectionAssert.AreEqual(new[] { b.CreatureId, a.CreatureId }, sent);
            CollectionAssert.AreEqual(new[] { 0, 1 }, pockets);

            // And it is what the FIRST attempt sent, so the server sees the
            // honest double-tap it is built to answer.
            Assert.AreEqual(handler.Bodies[0], handler.Bodies[handler.Bodies.Count - 1]);
        }

        [Test]
        public void StartAsync_DoesNotRetryA409_AndTheRefusalReachesTheScreen()
        {
            // `creature_committed` is a verdict. Retrying it spends the whole
            // backoff to produce the same sentence, and the screen needs the
            // server's own exception to classify it.
            var handler = new RefusingHandler(HttpStatusCode.Conflict, "creature_committed",
                "One of those creatures is already out fighting. Finish or abandon that wave first.");
            var waits = 0;

            // `Catch`, not `Throws`: NSwag throws the GENERIC
            // BroodlineApiException<ErrorResponse> for a status the contract
            // declares, and `Assert.Throws<T>` matches the exact type only.
            var thrown = Assert.Catch<BroodlineApiException>(() =>
                DeployScreen.StartAsync(Api(handler), Deployment(),
                    wait: _ => { waits++; return Task.CompletedTask; })
                    .GetAwaiter().GetResult());

            Assert.AreEqual(409, thrown.StatusCode);
            Assert.AreEqual(1, handler.Calls, "a verdict is not asked twice");
            Assert.AreEqual(0, waits);

            // And it still classifies, which is the reason it had to arrive
            // unwrapped.
            var classified = ServerError.From(thrown);
            Assert.AreEqual(ServerErrorKind.CreatureCommitted, classified.Kind);
            StringAssert.Contains("already out fighting", classified.PlayerMessage);
        }

        [Test]
        public void StartAsync_GivesUpAfterTheBackoff_AndTheLastFailureIsTransient()
        {
            // What `FtueDirector.StartFailureNotice` then reads to choose
            // between "tap Start again" and the server's own sentence.
            var handler = new StubHandler(failures: 99, issuanceId: Fixed);

            // Asserted through `IsTransient` rather than by exception type:
            // some runtimes re-wrap a handler's own HttpRequestException on
            // the way out of HttpClient, and the classification is what the
            // director actually reads.
            var thrown = Assert.Catch<Exception>(() =>
                DeployScreen.StartAsync(Api(handler), Deployment(),
                    wait: _ => Task.CompletedTask).GetAwaiter().GetResult());

            Assert.AreEqual(Retry.ColdBackoff.Count + 1, handler.Bodies.Count);
            Assert.IsTrue(Retry.IsTransient(thrown));
        }

        [Test]
        public void StartAsync_RefusesAnIllegalDeploymentWithoutSendingAnything()
        {
            // The guard is OUTSIDE the retry: an empty deployment is this
            // build's own defect and must not be asked four times.
            var handler = new StubHandler(failures: 0, issuanceId: Fixed);
            var roster = RosterOf(Creature());
            var illegal = DeployScreen.Build(1, roster, new List<Guid>());

            Assert.Throws<InvalidOperationException>(() =>
                DeployScreen.StartAsync(Api(handler), illegal,
                    wait: _ => Task.CompletedTask).GetAwaiter().GetResult());

            Assert.AreEqual(0, handler.Bodies.Count, "nothing reached the wire");
        }

        // -----------------------------------------------------------------
        // Fixtures
        // -----------------------------------------------------------------

        static readonly Guid Fixed = new Guid("11111111-2222-3333-4444-555555555555");

        static BroodlineApiClient Api(HttpMessageHandler handler)
        {
            return new BroodlineApiClient(new HttpClient(handler)) { BaseUrl = "http://stub.invalid/" };
        }

        static CreatureDto Creature()
        {
            return new CreatureDto
            {
                CreatureId = Guid.NewGuid(),
                Species = "Vetch Crawler",
                Generation = 1,
                Trait1 = "Chill", Tier1 = 1,
                Trait2 = "Guard", Tier2 = 1,
                Instinct = "Forage",
                Name = null, IsFounder = false, CommittedTo = null,
            };
        }

        static RosterScreen RosterOf(params CreatureDto[] creatures)
        {
            var roster = new RosterScreen();
            foreach (var creature in creatures) roster.Observe(creature);
            return roster;
        }

        static DeployScreenModel Deployment()
        {
            var a = Creature();
            var b = Creature();
            return DeployScreen.Build(1, RosterOf(a, b),
                new List<Guid> { a.CreatureId, b.CreatureId });
        }

        /// Throws a transport failure `failures` times, then answers.
        sealed class StubHandler : HttpMessageHandler
        {
            readonly int _failures;
            readonly Guid _issuanceId;

            public List<string> Bodies { get; } = new List<string>();

            public StubHandler(int failures, Guid issuanceId)
            {
                _failures = failures;
                _issuanceId = issuanceId;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // NO `ConfigureAwait(false)`, THOUGH `SessionTests`' own
                // handler has it. That file's three are the deliberate,
                // recorded red of `MainThreadAffinityTests`
                // (`phase8-test-baseline.txt`: "3 real sites in
                // SessionTests.cs"); a fourth here would silently widen a
                // failure the whole project reads as a fixed quantity. It
                // buys nothing either - this handler runs in EditMode and
                // awaits a completed task.
                Bodies.Add(request.Content == null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync());

                if (Bodies.Count <= _failures)
                {
                    // The real shape: no status, no body, nothing to classify.
                    throw new HttpRequestException("An error occurred while sending the request.");
                }

                var json = JsonConvert.SerializeObject(new WaveStartResponse
                {
                    IssuanceId = _issuanceId,
                    Seed = "3320262469013792924",
                    WaveId = 1,
                    ExpiresAt = "2026-09-22T00:00:00.000Z",
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }
        }

        /// Always refuses, with a real error body.
        sealed class RefusingHandler : HttpMessageHandler
        {
            readonly HttpStatusCode _status;
            readonly string _json;

            public int Calls { get; private set; }

            public RefusingHandler(HttpStatusCode status, string code, string message)
            {
                _status = status;
                _json = JsonConvert.SerializeObject(new ErrorResponse { Code = code, Message = message });
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(new HttpResponseMessage(_status)
                {
                    Content = new StringContent(_json, Encoding.UTF8, "application/json"),
                });
            }
        }
    }
}
