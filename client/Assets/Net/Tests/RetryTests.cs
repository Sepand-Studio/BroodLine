using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Broodline.Api;
using Broodline.Net;
using NUnit.Framework;

namespace Broodline.Net.Tests
{
    /// The retry policy, which is one classification and one loop.
    ///
    /// THE CLASSIFICATION IS THE PART WORTH TESTING. Getting the loop wrong
    /// costs a wasted attempt; getting `IsTransient` wrong in the generous
    /// direction turns a 409 into a stall that ends in the same 409, and in
    /// the strict direction leaves the defect this task exists to fix exactly
    /// where it was. Both directions are asserted below rather than only the
    /// one that was broken.
    ///
    /// EVERY TEST INJECTS `wait`. The default is a real `Task.Delay`, and its
    /// continuation returns to the caller's context - which in an EditMode
    /// test is a main thread the test is itself blocking. A recorded no-op
    /// keeps these deterministic and instant, and records the DURATIONS so
    /// the backoff schedule is asserted rather than assumed.
    public class RetryTests
    {
        // -----------------------------------------------------------------
        // IsTransient - did the server answer, and was the answer a verdict
        // -----------------------------------------------------------------

        [Test]
        public void IsTransient_IsTrueForTheThreeFailuresThatWereActuallySeen()
        {
            // Sighting 2, the deploy screen: HttpClient's transport failure,
            // no HTTP status at all.
            Assert.IsTrue(Retry.IsTransient(
                new HttpRequestException("An error occurred while sending the request.")));

            // Sighting 3, a packaged player's first launch: HttpClient's own
            // timeout, which surfaces as a cancellation because nothing on
            // these paths passes a CancellationToken.
            Assert.IsTrue(Retry.IsTransient(new TaskCanceledException()));

            // Sighting 3 again, one layer down: Mono's HttpWebRequest path
            // (RunWithTimeoutWorker / ServicePointScheduler.WaitAsync).
            Assert.IsTrue(Retry.IsTransient(new WebException("timed out")));

            // Sighting 1: sim recreated minutes earlier, 503 sim_unavailable,
            // and routes/wave.ts writes "Retry." into the message itself.
            Assert.IsTrue(Retry.IsTransient(Api(503, "sim_unavailable")));
        }

        [Test]
        public void IsTransient_IsTrueForTheOtherTransportShapes()
        {
            Assert.IsTrue(Retry.IsTransient(new SocketException(10061)));
            Assert.IsTrue(Retry.IsTransient(new IOException("connection reset")));
            Assert.IsTrue(Retry.IsTransient(Api(500, "internal")));
            Assert.IsTrue(Retry.IsTransient(Api(502, "bad gateway")));

            // Status 0 is what `ServerError.From` already uses to mean "a
            // transport failure, not a refusal".
            Assert.IsTrue(Retry.IsTransient(Api(0, string.Empty)));
        }

        [Test]
        public void IsTransient_FindsATransportFailureWrappedInsideAnother()
        {
            // The real shape on Mono: HttpRequestException wrapping a
            // WebException wrapping a SocketException. The outer one already
            // answers, so this pins the chain walk with an outer type that
            // does NOT - which is the only way the loop can be shown to run.
            var wrapped = new InvalidOperationException("outer", new WebException("inner"));
            Assert.IsTrue(Retry.IsTransient(wrapped));

            // And the same outer type with nothing transport-shaped inside
            // is still false, so the assertion above is about the INNER
            // exception and not about InvalidOperationException.
            Assert.IsFalse(Retry.IsTransient(new InvalidOperationException("outer")));
        }

        [Test]
        public void IsTransient_IsFalseForEveryVerdictTheServerActuallyReturns()
        {
            // THE 409 THIS TASK TURNS ON. `creature_committed` is a verdict:
            // asking again produces it again, and a retry would spend the
            // whole backoff arriving back at the same sentence.
            Assert.IsFalse(Retry.IsTransient(Api(409, "creature_committed")));

            Assert.IsFalse(Retry.IsTransient(Api(400, "invalid_request")));
            Assert.IsFalse(Retry.IsTransient(Api(401, "unauthorized")));
            Assert.IsFalse(Retry.IsTransient(Api(404, "not_found")));
            Assert.IsFalse(Retry.IsTransient(Api(409, "wave_locked")));
            Assert.IsFalse(Retry.IsTransient(Api(426, "client_too_old")));
        }

        [Test]
        public void IsTransient_IsFalseForThisBuildsOwnDefects()
        {
            // These fail identically on every attempt. `OutboxClient.
            // IsPermanent` makes the same ruling about its own two.
            Assert.IsFalse(Retry.IsTransient(new NullReferenceException()));
            Assert.IsFalse(Retry.IsTransient(new ArgumentNullException("body")));
            Assert.IsFalse(Retry.IsTransient(null));
        }

        [Test]
        public void IsTransient_ReadsTheStatusOfAnApiException_NotItsInnerException()
        {
            // NSwag hands the deserialization failure in as the inner
            // exception, and a 409 whose body could not be parsed is still a
            // 409. Walking the chain past the status would make the verdict
            // depend on why the body was unreadable.
            var refusal = new BroodlineApiException(
                "conflict", 409, "{", null, new IOException("truncated"));
            Assert.IsFalse(Retry.IsTransient(refusal),
                "a 4xx is a verdict however the body failed to parse");
        }

        // -----------------------------------------------------------------
        // TransientAsync - the loop
        // -----------------------------------------------------------------

        [Test]
        public async Task TransientAsync_ReturnsTheFirstSuccessAndNeverWaits()
        {
            var clock = new Waits();
            var calls = 0;

            var result = await Retry.TransientAsync<string>(
                () => { calls++; return Task.FromResult("ok"); }, wait: clock.WaitAsync);

            Assert.AreEqual("ok", result);
            Assert.AreEqual(1, calls);
            CollectionAssert.IsEmpty(clock.Taken, "a call that worked must not sleep");
        }

        [Test]
        public async Task TransientAsync_RetriesAColdBackendAndSucceedsOnTheThirdAttempt()
        {
            // The whole defect in one test: two transport failures - a cold
            // Cloud Run container - and then the answer, with no exception
            // reaching the caller.
            var clock = new Waits();
            var calls = 0;
            var notices = new List<int>();

            var result = await Retry.TransientAsync<string>(
                () =>
                {
                    calls++;
                    if (calls < 3) throw new HttpRequestException("An error occurred while sending the request.");
                    return Task.FromResult("issuance");
                },
                onRetry: (attempt, _) => notices.Add(attempt),
                wait: clock.WaitAsync);

            Assert.AreEqual("issuance", result);
            Assert.AreEqual(3, calls);

            // Two failures, two waits, IN THE AUTHORED ORDER - a schedule
            // that backed off in the wrong direction, or reused one delay,
            // passes a bare count.
            CollectionAssert.AreEqual(
                new[] { Retry.ColdBackoff[0], Retry.ColdBackoff[1] }, clock.Taken);

            // And the player heard about it at the FIRST failure, numbered so
            // a caller can say something only once. FightAsync and
            // BootController both key on `attempt == 1`.
            CollectionAssert.AreEqual(new[] { 1, 2 }, notices);
        }

        [Test]
        public void TransientAsync_NoticesBeforeItSleeps_NotAfter()
        {
            // The ordering the notice depends on: a player who taps Start
            // against a cold backend must hear something inside the first
            // failed attempt, not eleven seconds later. Recorded as one
            // interleaved log so a swap is visible rather than inferred.
            var log = new List<string>();
            var calls = 0;

            var run = Retry.TransientAsync<string>(
                () =>
                {
                    calls++;
                    if (calls < 2) throw new HttpRequestException("down");
                    return Task.FromResult("ok");
                },
                onRetry: (_, __) => log.Add("notice"),
                wait: d => { log.Add("wait"); return Task.CompletedTask; });

            run.GetAwaiter().GetResult();
            CollectionAssert.AreEqual(new[] { "notice", "wait" }, log);
        }

        [Test]
        public void TransientAsync_DoesNotRetryAVerdict_AndRethrowsItUntouched()
        {
            // The safety half. A 409 must cost exactly one attempt and arrive
            // at the caller as itself, because `ServerError` reads its status
            // and body to choose what the screen says.
            var clock = new Waits();
            var calls = 0;
            var refusal = Api(409, "creature_committed");

            var thrown = Assert.Throws<BroodlineApiException>(() =>
                Retry.TransientAsync<string>(
                    () => { calls++; throw refusal; }, wait: clock.WaitAsync)
                    .GetAwaiter().GetResult());

            Assert.AreSame(refusal, thrown, "the caller must get the server's own exception");
            Assert.AreEqual(1, calls, "a verdict is not asked twice");
            CollectionAssert.IsEmpty(clock.Taken);
        }

        [Test]
        public void TransientAsync_GivesUpAfterTheBackoffIsSpent_AndRethrowsTheLASTFailure()
        {
            // The last rather than the first: it is the one that describes
            // the state the player is actually in, and a screen that showed
            // the first would report a dropped connection for a backend that
            // is now answering 503.
            var clock = new Waits();
            var calls = 0;
            var last = Api(503, "sim_unavailable");

            var thrown = Assert.Throws<BroodlineApiException>(() =>
                Retry.TransientAsync<string>(
                    () =>
                    {
                        calls++;
                        if (calls <= Retry.ColdBackoff.Count) throw new HttpRequestException("first shapes");
                        throw last;
                    },
                    wait: clock.WaitAsync)
                    .GetAwaiter().GetResult());

            Assert.AreSame(last, thrown);

            // Three waits means FOUR attempts, and the fourth does not sleep
            // before giving up.
            Assert.AreEqual(Retry.ColdBackoff.Count + 1, calls);
            Assert.AreEqual(Retry.ColdBackoff.Count, clock.Taken.Count);
        }

        [Test]
        public void TransientAsync_HonoursAShorterBackoff()
        {
            // The schedule is a parameter, not a constant baked into the
            // loop - the one-wait case is what proves the loop counts the
            // list it was given.
            var clock = new Waits();
            var calls = 0;

            Assert.Throws<HttpRequestException>(() =>
                Retry.TransientAsync<string>(
                    () => { calls++; throw new HttpRequestException("down"); },
                    backoff: new[] { TimeSpan.FromMilliseconds(5) },
                    wait: clock.WaitAsync)
                    .GetAwaiter().GetResult());

            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, clock.Taken.Count);
        }

        [Test]
        public void ColdBackoff_RisesAndIsNotUnboundedPatience()
        {
            // A schedule that did not rise would hammer a starting container,
            // and one that never ended would hold the player forever. Both
            // are stated rather than left to the reader of the array.
            Assert.Greater(Retry.ColdBackoff.Count, 0);
            for (var i = 1; i < Retry.ColdBackoff.Count; i++)
            {
                Assert.Greater(Retry.ColdBackoff[i], Retry.ColdBackoff[i - 1],
                    "each wait must be longer than the one before it");
            }

            var total = TimeSpan.Zero;
            foreach (var w in Retry.ColdBackoff) total += w;
            Assert.Less(total, TimeSpan.FromSeconds(20),
                "a player is held for the whole of this; it is not a background job");
        }

        [Test]
        public void TransientAsync_RefusesANullAttempt()
        {
            Assert.Throws<ArgumentNullException>(
                () => Retry.TransientAsync<string>(null).GetAwaiter().GetResult());
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        /// Records what was asked for and sleeps for none of it.
        sealed class Waits
        {
            public List<TimeSpan> Taken { get; } = new List<TimeSpan>();

            public Task WaitAsync(TimeSpan duration)
            {
                Taken.Add(duration);
                return Task.CompletedTask;
            }
        }

        static BroodlineApiException Api(int status, string code)
        {
            return new BroodlineApiException(
                "server", status, "{\"code\":\"" + code + "\",\"message\":\"m\"}", null, null);
        }
    }
}
