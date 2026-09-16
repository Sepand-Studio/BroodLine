using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Broodline.Api;
using Broodline.Game.Shell;
using Broodline.Model;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Broodline.Game.Tests
{
    /// The cold-start sequence, against a stub HttpMessageHandler (the
    /// LoopGuardTests pattern of exercising the generated client through a
    /// real HttpClient rather than mocking Broodline.Api's own types).
    /// task-13-brief.md Step 1, verbatim test bodies; the handler and JSON
    /// helper classes below are this file's own, since the brief names them
    /// (DelayedSyncHandler, RecordingHandler, ApiOver, SnapshotWith,
    /// TokensJson, SnapshotJson) without spelling out their shape.
    public class SessionTests
    {
        [Test]
        public async Task ColdStart_RendersTheCachedSnapshotBeforeSyncReturns()
        {
            var store = new InMemorySnapshotStore(cached: SnapshotWith(highestWaveCleared: 2));
            var handler = new DelayedSyncHandler(SnapshotWith(highestWaveCleared: 6));
            var rendered = new List<int>();
            var s = new Session(ApiOver(handler), store, new InMemoryAuthStore(token: "t"), onSnapshot: sn => rendered.Add(sn.HighestWaveCleared));
            await s.ColdStartAsync();
            CollectionAssert.AreEqual(new[] { 2, 6 }, rendered);   // cached first, then the server's, no spinner
        }

        [Test]
        public async Task ColdStart_WithNoAccount_CreatesAGuestAndPersistsTheTokens()
        {
            // birthdateBand "adult", storefrontRegion "us-central1": the Age Gate is
            // deferred (design 12) and internal testers are adults. Says so in code.
            var handler = new RecordingHandler(createAccount: TokensJson("a1", "r1"), sync: SnapshotJson(highestWaveCleared: 0));
            var auth = new InMemoryAuthStore(token: null);
            var s = new Session(ApiOver(handler), new InMemorySnapshotStore(cached: null), auth, onSnapshot: _ => { });
            await s.ColdStartAsync();
            Assert.AreEqual("/v1/account", handler.Requests[0].Path);
            StringAssert.Contains("\"birthdateBand\":\"adult\"", handler.Requests[0].Body);
            Assert.IsNotNull(handler.Requests[0].Headers["idempotency-key"]);
            Assert.AreEqual("r1", auth.Saved.RefreshToken);
            Assert.AreEqual("Bearer a1", handler.Requests[1].Headers["Authorization"]);   // the sync that followed
        }

        [Test]
        public async Task ColdStart_RetriesOnceAfterA401_WithTheRefreshedToken()
        {
            // Fix round 1 finding: Session.RefreshAsync (the 401 retry) had
            // zero coverage - a returning player whose access token expired
            // between launches goes through exactly this path, and a bug in
            // it (wrong header order, the wrong tokens object saved) fails
            // silently and surfaces only as "I got logged out."
            var handler = new RefreshFlowHandler(
                unauthorized: ErrorJson("unauthorized", "token expired"),
                refresh: RefreshJson("a2", "r2"),
                sync: SnapshotJson(highestWaveCleared: 3));
            var auth = new InMemoryAuthStore(token: "expired");
            var s = new Session(ApiOver(handler), new InMemorySnapshotStore(cached: null), auth, onSnapshot: _ => { });

            var snapshot = await s.ColdStartAsync();

            // The retried sync returned the real snapshot, not the 401.
            Assert.AreEqual(3, snapshot.HighestWaveCleared);

            // Three requests, in order: the sync that was refused, the
            // refresh it provoked, and the sync that was retried with the
            // refreshed token.
            Assert.AreEqual(3, handler.Requests.Count);
            Assert.AreEqual("/v1/sync", handler.Requests[0].Path);
            Assert.AreEqual("/v1/session/refresh", handler.Requests[1].Path);
            Assert.AreEqual("/v1/sync", handler.Requests[2].Path);

            // The retry carries the REFRESHED bearer token, not the expired
            // one the first attempt used.
            Assert.AreEqual("Bearer expired", handler.Requests[0].Headers["Authorization"]);
            Assert.AreEqual("Bearer a2", handler.Requests[2].Headers["Authorization"]);

            // The new tokens - not the old ones, not half of each - were
            // handed to the auth store.
            Assert.AreEqual("a2", auth.Saved.AccessToken);
            Assert.AreEqual("r2", auth.Saved.RefreshToken);
        }

        // -----------------------------------------------------------------
        // Test doubles for ISnapshotStore / IAuthStore
        // -----------------------------------------------------------------

        sealed class InMemorySnapshotStore : ISnapshotStore
        {
            readonly PlayerSnapshot _cached;
            public PlayerSnapshot Saved { get; private set; }

            public InMemorySnapshotStore(PlayerSnapshot cached) { _cached = cached; }

            public PlayerSnapshot Load() => _cached;
            public void Save(PlayerSnapshot snapshot) => Saved = snapshot;
        }

        sealed class InMemoryAuthStore : IAuthStore
        {
            readonly Tokens _existing;
            public Tokens Saved { get; private set; }

            public InMemoryAuthStore(string token)
            {
                _existing = token == null ? null : new Tokens { AccessToken = token, RefreshToken = "existing-refresh" };
            }

            public Tokens Load() => _existing;
            public void Save(Tokens tokens) => Saved = tokens;
        }

        // -----------------------------------------------------------------
        // A recorded request, and the stub HttpMessageHandler base both
        // fakes share. Nothing here ever touches the network: SendAsync is
        // overridden and never calls base.SendAsync.
        // -----------------------------------------------------------------

        sealed class RequestRecord
        {
            public string Method;
            public string Path;
            public string Body;
            public IReadOnlyDictionary<string, string> Headers;
        }

        abstract class StubHandler : HttpMessageHandler
        {
            public List<RequestRecord> Requests { get; } = new List<RequestRecord>();

            protected sealed override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content != null ? await request.Content.ReadAsStringAsync().ConfigureAwait(false) : string.Empty;

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in request.Headers) headers[h.Key] = string.Join(",", h.Value);
                if (request.Content != null)
                    foreach (var h in request.Content.Headers) headers[h.Key] = string.Join(",", h.Value);

                Requests.Add(new RequestRecord
                {
                    Method = request.Method.Method,
                    Path = request.RequestUri.AbsolutePath,
                    Body = body,
                    Headers = headers,
                });

                return await Respond(request, body).ConfigureAwait(false);
            }

            protected abstract Task<HttpResponseMessage> Respond(HttpRequestMessage request, string body);

            protected static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
                new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        /// Always answers /v1/sync, after a short real delay - proving the
        /// cached render in ColdStart_RendersTheCachedSnapshotBeforeSyncReturns
        /// happened before the network call resolved, not merely before it
        /// was issued.
        sealed class DelayedSyncHandler : StubHandler
        {
            readonly string _syncJson;

            public DelayedSyncHandler(PlayerSnapshot snapshot)
            {
                _syncJson = SyncJsonFor(snapshot);
            }

            protected override async Task<HttpResponseMessage> Respond(HttpRequestMessage request, string body)
            {
                await Task.Delay(30).ConfigureAwait(false);
                return JsonResponse(HttpStatusCode.OK, _syncJson);
            }
        }

        /// Routes by path: /v1/account gets the account-creation fixture,
        /// anything else (only /v1/sync, in these tests) gets the sync one.
        sealed class RecordingHandler : StubHandler
        {
            readonly string _createAccountJson;
            readonly string _syncJson;

            public RecordingHandler(string createAccount, string sync)
            {
                _createAccountJson = createAccount;
                _syncJson = sync;
            }

            protected override Task<HttpResponseMessage> Respond(HttpRequestMessage request, string body)
            {
                var response = request.RequestUri.AbsolutePath.EndsWith("/account")
                    ? JsonResponse(HttpStatusCode.OK, _createAccountJson)
                    : JsonResponse(HttpStatusCode.OK, _syncJson);
                return Task.FromResult(response);
            }
        }

        /// 401 on the first call to /v1/sync, 200 on every call after;
        /// /v1/session/refresh always answers 200. Exactly the shape
        /// Session.ColdStartAsync's catch-and-retry needs to prove itself
        /// against: a real BroodlineApiException with StatusCode 401 out of
        /// the first sync, then success once the token has been refreshed.
        sealed class RefreshFlowHandler : StubHandler
        {
            readonly string _unauthorizedJson;
            readonly string _refreshJson;
            readonly string _syncJson;
            int _syncCalls;

            public RefreshFlowHandler(string unauthorized, string refresh, string sync)
            {
                _unauthorizedJson = unauthorized;
                _refreshJson = refresh;
                _syncJson = sync;
            }

            protected override Task<HttpResponseMessage> Respond(HttpRequestMessage request, string body)
            {
                if (request.RequestUri.AbsolutePath.EndsWith("/session/refresh"))
                    return Task.FromResult(JsonResponse(HttpStatusCode.OK, _refreshJson));

                _syncCalls++;
                var response = _syncCalls == 1
                    ? JsonResponse(HttpStatusCode.Unauthorized, _unauthorizedJson)
                    : JsonResponse(HttpStatusCode.OK, _syncJson);
                return Task.FromResult(response);
            }
        }

        // -----------------------------------------------------------------
        // Fixture helpers
        // -----------------------------------------------------------------

        static HttpClient ApiOver(HttpMessageHandler handler) => new HttpClient(handler);

        static PlayerSnapshot SnapshotWith(int highestWaveCleared) => new PlayerSnapshot
        {
            PlayerId = "p1",
            ServerId = 1,
            Balances = new Dictionary<string, int>(),
            HighestWaveCleared = highestWaveCleared,
            BundleVersion = "0.1.0",
            MinimumClientVersion = "0.1.0",
            Tabs = new Dictionary<string, int>(),
            Waves = new List<WaveSummary>(),
            Traits = new List<TraitSummary>(),
            Ftue = new FtueFacts(),
        };

        /// The wire shape of a `/v1/sync` response for the given snapshot -
        /// built from the real generated DTOs (not hand-assembled JSON
        /// strings) so this fails to compile, rather than fails at runtime,
        /// if the contract ever drops one of these Required.Always fields.
        static string SyncJsonFor(PlayerSnapshot s) => JsonConvert.SerializeObject(new SyncResponse
        {
            Player = new Player { PlayerId = Guid.NewGuid(), ServerId = s.ServerId },
            Balances = new Dictionary<string, int>(s.Balances ?? new Dictionary<string, int>()),
            Campaign = new Campaign { HighestWaveCleared = s.HighestWaveCleared, MilestonesClaimed = 0 },
            Timers = new List<Timers>(),
            Config = new Config
            {
                BundleVersion = s.BundleVersion ?? "0.1.0",
                MinimumClientVersion = s.MinimumClientVersion ?? "0.1.0",
                Tabs = new Dictionary<string, int>(s.Tabs ?? new Dictionary<string, int>()),
                Waves = new List<Waves>(),
                Traits = new List<Traits>(),
            },
            Ftue = new Ftue
            {
                FounderNamed = s.Ftue?.FounderNamed ?? false,
                TutorialStockGranted = s.Ftue?.TutorialStockGranted ?? false,
                Splices = s.Ftue?.Splices ?? 0,
            },
        });

        static string SnapshotJson(int highestWaveCleared) => SyncJsonFor(SnapshotWith(highestWaveCleared));

        static string TokensJson(string accessToken, string refreshToken) => JsonConvert.SerializeObject(new CreateAccountResponse
        {
            AccountId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            ServerId = 1,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Balances = new Dictionary<string, int>(),
        });

        static string ErrorJson(string code, string message) =>
            JsonConvert.SerializeObject(new ErrorResponse { Code = code, Message = message });

        static string RefreshJson(string accessToken, string refreshToken) =>
            JsonConvert.SerializeObject(new RefreshResponse { AccessToken = accessToken, RefreshToken = refreshToken });
    }
}
