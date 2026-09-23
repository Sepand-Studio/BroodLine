using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Broodline.Api;

namespace Broodline.Net
{
    /// Trying again, for the calls the outbox does not carry.
    ///
    /// THE OUTBOX IS THE MODEL AND THIS IS THE OTHER HALF. `OutboxClient`
    /// already retries the five keyed mutations: it mints an Idempotency-Key
    /// when the action is taken, persists the entry, and `OutboxPump` drains
    /// it with the SAME key however many attempts that takes. Nothing carries
    /// the calls that are not queueable - `GET /v1/sync`, `POST /v1/wave/start`
    /// - and the server's own contract assumes a client that tries again:
    /// `services/api/src/routes/wave.ts:572` answers "Verification is
    /// temporarily unavailable. Retry." and the comment above it says why
    /// that is honest ("A retryable 503 is honest only because nothing was
    /// consumed"). Before this file, nothing acted on it.
    ///
    /// WHAT MAY BE PASSED TO `TransientAsync`, AND IT IS NOT "ANY CALL".
    /// A transport failure returns no response, so the caller cannot know
    /// whether the request arrived. Replaying a mutation blind can
    /// double-commit. Only these may be retried here:
    ///
    ///   - `GET /v1/sync`, `GET /v1/roster`, `GET /v1/lineage` and the other
    ///     reads: naturally idempotent, nothing to double-commit.
    ///   - `POST /v1/splice/preview`: `SpliceScreen.PreviewAsync`'s own
    ///     comment - "writes nothing and spends nothing".
    ///   - `POST /v1/wave/start`: SAFE FOR A REASON THAT IS NOT OBVIOUS AND
    ///     IS NOT AN IDEMPOTENCY-KEY. See `DeployScreen.StartAsync`, which
    ///     carries the proof and the measurement that established it.
    ///
    /// The five KEYED mutations (`/v1/account`, `/v1/wave/submit`,
    /// `/v1/node/claim`, `/v1/splice/commit`, `/v1/creature/name`) do NOT
    /// belong here even though they are replay-safe: they are replay-safe
    /// only under their own Idempotency-Key, and `Outbox.cs` is what
    /// guarantees the same key is reused across attempts. Retrying one here
    /// would re-mint the key at the call site and defeat the dedupe.
    ///
    /// **NO `ConfigureAwait(false)` IN THIS FILE, AND THAT IS DELIBERATE -
    /// IT IS THE OPPOSITE OF THE RULE FOR THE REST OF `Net/`.**
    /// `FtueDirector`'s class comment states the rule and its exception:
    /// `Net/` may use the flag because it touches no `VisualElement`. This
    /// file breaks that exception on purpose, because `onRetry` EXISTS to
    /// put a sentence in front of the player - `FtueDirector` hands it
    /// `_notice`, which reaches `NoticeToast.Show` and therefore a
    /// `VisualElement`. With `ConfigureAwait(false)` the wait would resume on
    /// a threadpool thread and the callback would throw
    /// "VisualElementCreation can only be called from the main thread" -
    /// turning the notice that fixes the silence into the crash that causes
    /// it. Every await below therefore captures the caller's context, which
    /// on every production path here is Unity's main thread.
    public static class Retry
    {
        /// How a wait is taken. Substituted by tests, which must not spend
        /// real seconds - and must not post a continuation back to a main
        /// thread that an EditMode test is itself blocking.
        public delegate Task Wait(TimeSpan duration);

        /// The waits BETWEEN attempts, so this is three waits and FOUR
        /// attempts.
        ///
        /// SIZED FOR A CLOUD RUN COLD START, which is what this exists for:
        /// `min_instance_count = 0` on both `api` and `sim`, so the first
        /// call after an idle period waits on a container coming up. Eleven
        /// seconds of total patience is a long time to hold a player, which
        /// is exactly why `onRetry` is not optional in practice: the player
        /// is told on the first failure, not after the last.
        public static readonly IReadOnlyList<TimeSpan> ColdBackoff = new[]
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(7),
        };

        /// Is this failure one that the SAME request might survive?
        ///
        /// The division is "did the server answer", not "is it annoying":
        ///
        ///   - A `BroodlineApiException` means the server answered, and the
        ///     answer is authoritative. 5xx (and the 503 `sim_unavailable`
        ///     that `wave.ts` writes "Retry." into) is the server saying it
        ///     could not do it THIS time. Every 4xx is a verdict: a 409, a
        ///     400, a 404 answer the same way however many times they are
        ///     asked, and retrying one turns a refusal into a stall. So the
        ///     status decides it outright and the inner exception is not
        ///     consulted - `OutboxClient.AttemptOnceAsync` draws the line in
        ///     exactly the same place, for the same reason.
        ///   - Anything else that reached the wire is a TRANSPORT failure
        ///     with no response at all. The three sightings this file was
        ///     written for are all here: `HttpRequestException` ("An error
        ///     occurred while sending the request", from the deploy screen),
        ///     a `TaskCanceledException` from `HttpClient`'s own timeout, and
        ///     Mono's `WebException` out of
        ///     `HttpWebRequest.RunWithTimeoutWorker` in a packaged player.
        ///   - EVERYTHING ELSE IS FALSE, and that is the load-bearing half.
        ///     A `NullReferenceException` or an `ArgumentException` out of
        ///     the request builder is a defect in this build; it fails
        ///     identically on every attempt, and retrying it only delays the
        ///     moment anybody finds out - `OutboxClient.IsPermanent` makes
        ///     the same ruling about its own two.
        ///
        /// `OperationCanceledException` is read as a timeout because nothing
        /// on these paths passes a `CancellationToken`: the generated client
        /// takes one and every call site leaves it defaulted, so the only
        /// thing that can cancel one of these is `HttpClient.Timeout`. If a
        /// caller ever does pass a token, this needs a token check first.
        public static bool IsTransient(Exception error)
        {
            if (error == null) return false;

            var api = error as BroodlineApiException;
            if (api != null) return api.StatusCode >= 500 || api.StatusCode == 0;

            for (var e = error; e != null; e = e.InnerException)
            {
                if (e is HttpRequestException) return true;
                if (e is OperationCanceledException) return true;   // includes TaskCanceledException
                if (e is WebException) return true;
                if (e is SocketException) return true;
                if (e is IOException) return true;
            }

            return false;
        }

        /// Run `attempt` until it succeeds, it fails in a way retrying cannot
        /// fix, or the backoff runs out - then rethrow the LAST failure.
        ///
        /// The last rather than the first, deliberately: it is the one that
        /// describes the state the player is actually in. A first attempt
        /// that failed on a dropped connection and a fourth that failed on a
        /// 503 are different situations, and the screen should show the one
        /// that is still true.
        ///
        /// `onRetry` is called BEFORE the wait, not after it, with the number
        /// of attempts made so far and what the last one threw. That ordering
        /// is the whole point: a player who taps Start against a cold backend
        /// hears something within the first failed attempt rather than eleven
        /// seconds later.
        public static async Task<T> TransientAsync<T>(
            Func<Task<T>> attempt,
            IReadOnlyList<TimeSpan> backoff = null,
            Action<int, Exception> onRetry = null,
            Wait wait = null)
        {
            if (attempt == null) throw new ArgumentNullException("attempt");

            var waits = backoff ?? ColdBackoff;

            // A method group rather than `wait ?? (d => Task.Delay(d))`: an
            // untyped lambda has no type for `??` to coalesce to, which does
            // not compile on this language version.
            var sleep = wait ?? DefaultWait;

            for (var made = 0; ; made++)
            {
                try
                {
                    return await attempt();
                }
                catch (Exception error)
                {
                    // `made` counts attempts COMPLETED including this one
                    // after the increment below is applied by the loop, so
                    // the guard reads "is there a wait left for the attempt
                    // just made". With three waits, attempts 1-3 wait and
                    // attempt 4 rethrows.
                    if (made >= waits.Count || !IsTransient(error)) throw;

                    if (onRetry != null) onRetry(made + 1, error);
                    await sleep(waits[made]);
                }
            }
        }

        /// The real wait. Its continuation returns to the caller's context -
        /// Unity's main thread on every production path here - which is what
        /// keeps `onRetry`'s notice legal. See the class comment.
        static Task DefaultWait(TimeSpan duration)
        {
            return Task.Delay(duration);
        }
    }
}
