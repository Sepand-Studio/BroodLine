using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Broodline.Api;
using Broodline.Net;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Broodline.Net.Tests
{
    /// `OutboxClient`'s networking half - the dispatch-by-`Op` switch, the
    /// 4xx-acks / 5xx-fails split, and the offline affordance asymmetry -
    /// against a fake `HttpMessageHandler` (`BroodlineApiClient`'s
    /// constructor takes a plain `HttpClient`, so this needs no real server
    /// and no PlayMode). Fix round 1: these three paths previously had only
    /// a successful compile and manual reading behind them.
    ///
    /// Every test constructs its own `Outbox` and inspects it directly after
    /// the call - not just the returned `OutboxResult<T>` - so "acked" and
    /// "still queued" are proven against the actual queue state, the same
    /// standard `OutboxTests.cs`'s persistence test was held to.
    public class OutboxClientTests
    {
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), "outbox-client-tests-" + Guid.NewGuid().ToString("N") + ".bin");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
            var tempTemp = _tempPath + ".tmp";
            if (File.Exists(tempTemp)) File.Delete(tempTemp);
        }

        [Test]
        public async Task ServerAcks2xx_ReturnsSent_WithTheResponseThreadedThrough_AndAcksTheEntry()
        {
            var box = new Outbox();
            var handler = new FixedResponseHandler(HttpStatusCode.OK, WaveSubmitJson("Win", integrityRemaining: 5));
            var client = NewClient(handler, box, isOffline: () => false);

            var result = await client.SubmitWaveAsync(Guid.NewGuid(), new byte[] { 1, 2, 3 });

            Assert.AreEqual(OutboxOutcome.Sent, result.Outcome);
            Assert.IsNotNull(result.Response, "a Sent result with no response proves nothing was actually threaded through");
            Assert.AreEqual("Win", result.Response.Result);
            Assert.AreEqual(5, result.Response.IntegrityRemaining);

            // Acked: gone from the queue, not merely "the call returned".
            Assert.IsNull(box.Peek());
            Assert.AreEqual(1, handler.Requests.Count);
            Assert.AreEqual(result.Key, handler.Requests[0].Headers["Idempotency-Key"]);
        }

        [Test]
        public async Task Server4xx_ReturnsRejected_AcksTheEntry_AndSurfacesTheError()
        {
            var box = new Outbox();
            // 422 is one of SubmitWaveAsync's explicitly-modeled codes, so
            // this also proves the ErrorResponse parses, not only that some
            // exception was thrown.
            var handler = new FixedResponseHandler(HttpStatusCode.UnprocessableEntity, ErrorJson("submission_rejected", "not a legal replay"));
            var client = NewClient(handler, box, isOffline: () => false);

            var result = await client.SubmitWaveAsync(Guid.NewGuid(), new byte[] { 1 });

            Assert.AreEqual(OutboxOutcome.Rejected, result.Outcome);
            Assert.IsNotNull(result.Error, "Rejected with no error gives the caller nothing to show");
            Assert.AreEqual(422, result.Error.StatusCode);
            Assert.AreEqual("submission_rejected", ((BroodlineApiException<ErrorResponse>)result.Error).Result.Code);

            // Acked - a 4xx is definitive, never retried.
            Assert.IsNull(box.Peek());
        }

        [Test]
        public async Task Server5xx_ReturnsQueued_LeavesTheEntryQueued_AndBacksOff()
        {
            var box = new Outbox();
            // 503 is one of SubmitWaveAsync's explicitly-modeled codes.
            var handler = new FixedResponseHandler(HttpStatusCode.ServiceUnavailable, ErrorJson("unavailable", "try later"));
            var client = NewClient(handler, box, isOffline: () => false);

            var result = await client.SubmitWaveAsync(Guid.NewGuid(), new byte[] { 1 });

            Assert.AreEqual(OutboxOutcome.Queued, result.Outcome);
            Assert.IsNull(result.Response);
            Assert.IsNull(result.Error);

            var stillQueued = box.Peek();
            Assert.IsNotNull(stillQueued, "a 5xx must leave the entry in the queue for a later retry, not drop it");
            Assert.AreEqual(result.Key, stillQueued.Key);
            Assert.AreEqual(1, stillQueued.Attempts); // proves Fail actually ran, not just that Ack didn't
            Assert.Greater(stillQueued.NotBefore, stillQueued.CreatedAt); // backed off, not still due-now
        }

        [Test]
        public async Task TransportFailure_BehavesLikeA5xx_QueuedAndBackedOff()
        {
            var box = new Outbox();
            var handler = new ThrowingHandler();
            var client = NewClient(handler, box, isOffline: () => false);

            var result = await client.SubmitWaveAsync(Guid.NewGuid(), new byte[] { 1 });

            Assert.AreEqual(OutboxOutcome.Queued, result.Outcome);
            var stillQueued = box.Peek();
            Assert.IsNotNull(stillQueued, "a transport failure must leave the entry queued, exactly like a 5xx");
            Assert.AreEqual(1, stillQueued.Attempts);
            Assert.Greater(stillQueued.NotBefore, stillQueued.CreatedAt);
            Assert.AreEqual(1, handler.CallCount); // the attempt genuinely happened
        }

        [Test]
        public async Task Offline_NonQueueableOps_ReturnUnavailable_AndPersistNothing_AndNeverTouchTheNetwork()
        {
            var box = new Outbox();
            var handler = new FixedResponseHandler(HttpStatusCode.OK, "{}"); // must never be reached
            var client = NewClient(handler, box, isOffline: () => true);

            var claim = await client.ClaimNodeAsync(3);
            Assert.AreEqual(OutboxOutcome.Unavailable, claim.Outcome);
            Assert.IsNull(claim.Key);

            var splice = await client.SpliceCommitAsync(Guid.NewGuid(), Guid.NewGuid(), new SpliceLock(), "A");
            Assert.AreEqual(OutboxOutcome.Unavailable, splice.Outcome);

            var name = await client.NameCreatureAsync(Guid.NewGuid(), "Rex");
            Assert.AreEqual(OutboxOutcome.Unavailable, name.Outcome);

            // Nothing was ever persisted, and the (fake) network was never touched.
            Assert.IsNull(box.Peek());
            Assert.AreEqual(0, handler.Requests.Count);
        }

        [Test]
        public async Task Offline_QueueableOps_QueueImmediately_WithoutAttemptingTheNetwork()
        {
            var box = new Outbox();
            var handler = new FixedResponseHandler(HttpStatusCode.OK, "{}"); // must never be reached
            var client = NewClient(handler, box, isOffline: () => true);

            var wave = await client.SubmitWaveAsync(Guid.NewGuid(), new byte[] { 1 });
            Assert.AreEqual(OutboxOutcome.Queued, wave.Outcome);

            var queuedWave = box.Peek();
            Assert.IsNotNull(queuedWave, "wave/submit must queue while offline, not disappear or throw");
            Assert.AreEqual(wave.Key, queuedWave.Key);
            // Left due-now, not backed off - no attempt was spent (and failed) on it.
            Assert.AreEqual(0, queuedWave.Attempts);
            Assert.AreEqual(queuedWave.CreatedAt, queuedWave.NotBefore);

            box.Ack(queuedWave.Key);

            var stock = await client.FtueSpliceStockAsync();
            Assert.AreEqual(OutboxOutcome.Queued, stock.Outcome);
            var queuedStock = box.Peek();
            Assert.IsNotNull(queuedStock);
            Assert.AreEqual(0, queuedStock.Attempts);

            // The whole point: offline queueing never spends a doomed attempt
            // on the fake network either.
            Assert.AreEqual(0, handler.Requests.Count);
        }

        // -----------------------------------------------------------------
        // Fixture helpers - mirrors the StubHandler idiom in
        // Broodline.Game.Tests/SessionTests.cs (this assembly cannot
        // reference that one, so it is its own small copy).
        // -----------------------------------------------------------------

        private static OutboxClient NewClient(HttpMessageHandler handler, Outbox box, Func<bool> isOffline)
        {
            var api = new BroodlineApiClient(new HttpClient(handler));
            var store = new OutboxStore(Path.Combine(Path.GetTempPath(), "outbox-client-tests-" + Guid.NewGuid().ToString("N") + ".bin"));
            return new OutboxClient(api, box, store, isOffline);
        }

        private sealed class RequestRecord
        {
            public string Method;
            public string Path;
            public string Body;
            public IReadOnlyDictionary<string, string> Headers;
        }

        /// Always answers with the same status and JSON body, and records
        /// every request it saw.
        private sealed class FixedResponseHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _json;

            public List<RequestRecord> Requests { get; } = new List<RequestRecord>();

            public FixedResponseHandler(HttpStatusCode status, string json)
            {
                _status = status;
                _json = json;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content != null ? await request.Content.ReadAsStringAsync().ConfigureAwait(false) : string.Empty;

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in request.Headers) headers[h.Key] = string.Join(",", h.Value);

                Requests.Add(new RequestRecord
                {
                    Method = request.Method.Method,
                    Path = request.RequestUri.AbsolutePath,
                    Body = body,
                    Headers = headers,
                });

                return new HttpResponseMessage(_status) { Content = new StringContent(_json, Encoding.UTF8, "application/json") };
            }
        }

        /// Simulates a transport-level failure (no HTTP response at all) -
        /// a dropped connection, DNS failure, or timeout.
        private sealed class ThrowingHandler : HttpMessageHandler
        {
            public int CallCount { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                throw new HttpRequestException("simulated transport failure");
            }
        }

        private static string WaveSubmitJson(string result, int integrityRemaining) => JsonConvert.SerializeObject(new WaveSubmitResponse
        {
            Result = result,
            IntegrityRemaining = integrityRemaining,
            Breaches = new List<BreachDto>(),
        });

        private static string ErrorJson(string code, string message) =>
            JsonConvert.SerializeObject(new ErrorResponse { Code = code, Message = message });
    }
}
