using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Broodline.Api;
using Newtonsoft.Json;
using UnityEngine;

namespace Broodline.Net
{
    /// What happened to one outbox entry, either from an immediate attempt
    /// (a caller's own `SubmitWaveAsync` etc.) or from a background
    /// `FlushAsync` pass driven by `OutboxPump`.
    public enum OutboxOutcome
    {
        /// The server acknowledged it (2xx). The entry is gone from the
        /// queue; the typed response is on the result.
        Sent,

        /// The server refused it definitively (4xx, not a timeout). Per
        /// `client_architecture` section 8, "the server's response is
        /// truth" - the entry is gone from the queue (it will not be
        /// retried) and the error is on the result for the caller to show.
        Rejected,

        /// Not yet resolved - offline, a 5xx, a transport failure, or
        /// simply not reached yet because an earlier entry is still backing
        /// off. The entry remains in the queue and `OutboxPump` will retry
        /// it later.
        Queued,

        /// Refused before anything was queued: this op is one of the ones
        /// that `client_architecture` section 8 marks "not available
        /// offline" (splice / harvest claim / naming), so nothing was
        /// persisted and nothing will be retried. The caller should show
        /// this action as unavailable, the same way it would if the button
        /// had been disabled to begin with.
        Unavailable,
    }

    /// The outcome of one `OutboxClient` call, typed to the response the op
    /// returns when it actually reaches the server.
    public sealed class OutboxResult<T>
    {
        public OutboxOutcome Outcome { get; }

        /// The idempotency key this action was (or would have been) queued
        /// under. Null only for `Unavailable`, which never queues.
        public string Key { get; }

        /// Set when `Outcome == Sent`.
        public T Response { get; }

        /// Set when `Outcome == Rejected` - the server's own exception,
        /// carrying its status code and parsed `ErrorResponse`.
        public BroodlineApiException Error { get; }

        private OutboxResult(OutboxOutcome outcome, string key, T response, BroodlineApiException error)
        {
            Outcome = outcome;
            Key = key;
            Response = response;
            Error = error;
        }

        internal static OutboxResult<T> Sent(string key, T response) => new OutboxResult<T>(OutboxOutcome.Sent, key, response, null);
        internal static OutboxResult<T> Rejected(string key, BroodlineApiException error) => new OutboxResult<T>(OutboxOutcome.Rejected, key, default, error);
        internal static OutboxResult<T> Queued(string key) => new OutboxResult<T>(OutboxOutcome.Queued, key, default, null);
        internal static OutboxResult<T> Unavailable() => new OutboxResult<T>(OutboxOutcome.Unavailable, null, default, null);
    }

    /// One attempt made during a `FlushAsync` pass, untyped - `FlushAsync`
    /// drains entries of mixed `Op`s in one pass, so their responses only
    /// share `object` until the per-op caller (`SubmitWaveAsync` etc.) casts
    /// its own entry's record back to its real response type.
    public sealed class OutboxAttemptRecord
    {
        public string Key { get; }
        public OutboxOutcome Outcome { get; }
        public object Response { get; }
        public BroodlineApiException Error { get; }

        private OutboxAttemptRecord(string key, OutboxOutcome outcome, object response, BroodlineApiException error)
        {
            Key = key;
            Outcome = outcome;
            Response = response;
            Error = error;
        }

        internal static OutboxAttemptRecord Sent(string key, object response) => new OutboxAttemptRecord(key, OutboxOutcome.Sent, response, null);
        internal static OutboxAttemptRecord Rejected(string key, BroodlineApiException error) => new OutboxAttemptRecord(key, OutboxOutcome.Rejected, null, error);
        internal static OutboxAttemptRecord Queued(string key) => new OutboxAttemptRecord(key, OutboxOutcome.Queued, null, null);
    }

    /// One `FlushAsync` pass: what was attempted, and what expired.
    public sealed class OutboxFlushResult
    {
        /// One record per entry actually attempted this pass, oldest first.
        /// Stops at the first `Queued` outcome - the head-of-line block
        /// means nothing behind it was attempted either.
        public IReadOnlyList<OutboxAttemptRecord> Attempts { get; }

        /// Player-facing notices for entries dropped by the 24-hour
        /// idempotency-window expiry this pass. `OutboxPump` is where these
        /// go to the shell's notice list.
        public IReadOnlyList<string> Notices { get; }

        internal OutboxFlushResult(IReadOnlyList<OutboxAttemptRecord> attempts, IReadOnlyList<string> notices)
        {
            Attempts = attempts;
            Notices = notices;
        }
    }

    /// The client's queueable mutations - `client_architecture` section 8.
    /// `BroodlineClient.cs`'s cold-start call is the client's one call with
    /// no outbox involved; every mutation that can be taken offline or must
    /// never double-fire goes through here instead.
    ///
    /// Each method **generates the key first, persists the entry, then
    /// attempts** - in that order, so a kill between "persisted" and
    /// "attempted" loses nothing: `OutboxPump` retries the persisted entry
    /// with its already-minted key on the next foreground or reachability
    /// change.
    ///
    /// The offline affordance is asymmetric, by design (`client_architecture`
    /// section 8's "not available offline" list): `SubmitWaveAsync` and
    /// `FtueSpliceStockAsync` queue while offline, because campaign waves and
    /// the tutorial stock grant are client-playable and idempotent-safe to
    /// delay. `SpliceCommitAsync`, `ClaimNodeAsync` and `NameCreatureAsync` do
    /// not queue - they are server-authoritative (the server rolls the
    /// splice, the server owns the harvest and the name), so calling them
    /// while `Application.internetReachability` is `NotReachable` returns
    /// `OutboxOutcome.Unavailable` immediately without persisting anything.
    /// That is deliberate: don't smooth it into "queue everything."
    public sealed class OutboxClient
    {
        private readonly BroodlineApiClient _api;
        private readonly Outbox _box;
        private readonly OutboxStore _store;
        private readonly Func<bool> _isOffline;

        /// SERIALISES EVERY PATH THAT TOUCHES `_box` OR `_store`.
        ///
        /// There are two of them and they overlap in ordinary use: a player
        /// action (`SubmitWaveAsync` and friends, awaited by `FtueDirector`)
        /// and a background drain (`OutboxPump` on foreground or on
        /// reachability returning). Nothing in Unity serialises them for us,
        /// because every await on this path uses `ConfigureAwait(false)` and
        /// so resumes on a threadpool thread rather than the main one.
        ///
        /// Unsynchronised, two drains can take the same head from
        /// `_box.Next`, and `List<OutboxEntry>`'s `Insert`/`RemoveAt`/
        /// `FindIndex` run concurrently on the same instance. Worse,
        /// `OutboxStore.Save` opens `_path + ".tmp"` with `FileMode.Create`
        /// from both threads and interleaves binary writes into one file
        /// before `File.Replace` - which `Load`'s `BinaryReader` then fails
        /// on at next launch, discarding every pending mutation the player
        /// had queued.
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        /// Production constructor - wires the real connectivity signal
        /// (`Application.internetReachability`). Delegates to the overload
        /// below rather than duplicating the offline logic.
        public OutboxClient(BroodlineApiClient api, Outbox box, OutboxStore store)
            : this(api, box, store, DefaultIsOffline)
        {
        }

        /// Same as above, with the connectivity check substituted - the seam
        /// `OutboxClientTests.cs` uses to drive the offline-affordance
        /// asymmetry deterministically instead of depending on
        /// `Application.internetReachability`, which is not controllable
        /// from an EditMode test. Fix round 1: this constructor is real,
        /// callable production API (any caller may supply its own
        /// connectivity predicate), not a setter that exists only for tests
        /// to reach into.
        public OutboxClient(BroodlineApiClient api, Outbox box, OutboxStore store, Func<bool> isOffline)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _box = box ?? throw new ArgumentNullException(nameof(box));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _isOffline = isOffline ?? throw new ArgumentNullException(nameof(isOffline));
        }

        private static bool DefaultIsOffline() => Application.internetReachability == NetworkReachability.NotReachable;

        /// Submits a completed wave's replay for the issuance it belongs to.
        /// Queues while offline - the wave already simulated locally.
        public Task<OutboxResult<WaveSubmitResponse>> SubmitWaveAsync(Guid issuanceId, byte[] replayBytes)
        {
            var body = Serialize(new WaveSubmitRequest
            {
                IssuanceId = issuanceId,
                Replay = Convert.ToBase64String(replayBytes ?? Array.Empty<byte>()),
            });
            return EnqueueAndAttemptAsync<WaveSubmitResponse>("wave/submit", body, queueWhileOffline: true);
        }

        /// Commits a splice between two parents. NOT queued while offline -
        /// the server rolls the outcome.
        public Task<OutboxResult<SpliceCommitResponse>> SpliceCommitAsync(Guid parentA, Guid parentB, SpliceLock locked, string bodyFrom)
        {
            var body = Serialize(new SpliceCommitRequest
            {
                ParentA = parentA,
                ParentB = parentB,
                Locked = locked ?? new SpliceLock(),
                BodyFrom = bodyFrom,
            });
            return EnqueueAndAttemptAsync<SpliceCommitResponse>("splice/commit", body, queueWhileOffline: false);
        }

        /// Claims a node's harvest. NOT queued while offline - harvest claim
        /// is server-authoritative.
        public Task<OutboxResult<NodeClaimResponse>> ClaimNodeAsync(int slot)
        {
            var body = Serialize(new NodeClaimRequest { Slot = slot });
            return EnqueueAndAttemptAsync<NodeClaimResponse>("node/claim", body, queueWhileOffline: false);
        }

        /// Names a creature (the founder-naming FTUE beat, or any later
        /// rename). NOT queued while offline - shown unavailable instead, per
        /// the same asymmetry as splice and claim.
        public Task<OutboxResult<CreatureDto>> NameCreatureAsync(Guid creatureId, string name)
        {
            var body = Serialize(new CreatureNameRequest { CreatureId = creatureId, Name = name });
            return EnqueueAndAttemptAsync<CreatureDto>("creature/name", body, queueWhileOffline: false);
        }

        /// Grants the FTUE's one-time tutorial splice stock. Queues while
        /// offline, like `SubmitWaveAsync` - the other queueable FTUE
        /// mutation named in `client_architecture` section 8.
        public Task<OutboxResult<FtueStockResponse>> FtueSpliceStockAsync()
        {
            return EnqueueAndAttemptAsync<FtueStockResponse>("ftue/splice-stock", Array.Empty<byte>(), queueWhileOffline: true);
        }

        /// Drains everything currently due, oldest first, stopping at the
        /// first entry that is still backing off (head-of-line blocking).
        /// This is what `OutboxPump` calls on foreground and on
        /// connectivity returning; the per-op methods above call it too, so
        /// an action taken while online gets its real result immediately
        /// instead of waiting for the next pump tick.
        public async Task<OutboxFlushResult> FlushAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await FlushHeldAsync().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// The drain itself. The caller must already hold `_gate` - this is
        /// what lets `EnqueueAndAttemptAsync` enqueue and then drain without
        /// releasing in between, and without deadlocking on its own lock.
        private async Task<OutboxFlushResult> FlushHeldAsync()
        {
            var notices = new List<string>();
            var attempts = new List<OutboxAttemptRecord>();

            while (true)
            {
                // ONE CLOCK READ PER ITERATION, AND EXPIRY RE-RUN AGAINST IT.
                // A flush is not instantaneous: each attempt is a network
                // round trip, and a 5xx on an entry ahead adds a backoff. An
                // entry that was inside the 24-hour idempotency window when
                // the flush started can therefore fall outside it before the
                // loop reaches it - and expiring only once, before the loop,
                // would send it anyway. That is the exact replay `Expire`
                // exists to prevent, because the server's dedupe window has
                // closed and it may already have applied and forgotten the
                // mutation.
                var tick = DateTime.UtcNow;
                var expired = _box.Expire(tick);
                if (expired.Count > 0)
                {
                    foreach (var entry in expired) notices.Add(entry.Notice);
                    _store.Save(_box);
                }

                var next = _box.Next(tick);
                if (next == null) break;

                var record = await AttemptOnceAsync(next).ConfigureAwait(false);
                attempts.Add(record);

                if (record.Outcome != OutboxOutcome.Sent && record.Outcome != OutboxOutcome.Rejected)
                {
                    // Still queued (backing off) - nothing behind it may go either.
                    break;
                }
            }

            return new OutboxFlushResult(attempts, notices);
        }

        private async Task<OutboxResult<T>> EnqueueAndAttemptAsync<T>(string op, byte[] body, bool queueWhileOffline)
        {
            var offline = _isOffline();
            if (offline && !queueWhileOffline)
            {
                return OutboxResult<T>.Unavailable();
            }

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                // The key is generated now, when the action is taken - never
                // regenerated on a later retry.
                var entry = OutboxEntry.For(op, body, DateTime.UtcNow);
                _box.Enqueue(entry);
                _store.Save(_box);

                if (offline)
                {
                    // queueWhileOffline must be true here (the branch above
                    // returns otherwise). No server to reach - leave it due-now
                    // rather than spend a doomed attempt on it.
                    return OutboxResult<T>.Queued(entry.Key);
                }

                var flush = await FlushHeldAsync().ConfigureAwait(false);
                return ResultFor<T>(entry.Key, flush.Attempts);
            }
            finally
            {
                _gate.Release();
            }
        }

        private static OutboxResult<T> ResultFor<T>(string key, IReadOnlyList<OutboxAttemptRecord> attempts)
        {
            foreach (var record in attempts)
            {
                if (record.Key != key) continue;
                if (record.Outcome == OutboxOutcome.Sent) return OutboxResult<T>.Sent(key, (T)record.Response);
                if (record.Outcome == OutboxOutcome.Rejected) return OutboxResult<T>.Rejected(key, record.Error);
                break;
            }

            // Not sent, not rejected: either it backed off, or an earlier
            // entry blocked it from being attempted at all this pass.
            // Either way it is still in the queue for the next flush.
            return OutboxResult<T>.Queued(key);
        }

        private async Task<OutboxAttemptRecord> AttemptOnceAsync(OutboxEntry entry)
        {
            try
            {
                var response = await SendAsync(entry).ConfigureAwait(false);
                _box.Ack(entry.Key);
                _store.Save(_box);
                return OutboxAttemptRecord.Sent(entry.Key, response);
            }
            catch (BroodlineApiException ex) when (ex.StatusCode >= 400 && ex.StatusCode < 500)
            {
                // The server's response is truth: a definitive 4xx acks the
                // entry (it will never be retried) and surfaces the error -
                // the action was rejected, not lost.
                _box.Ack(entry.Key);
                _store.Save(_box);
                return OutboxAttemptRecord.Rejected(entry.Key, ex);
            }
            catch (Exception ex) when (IsPermanent(ex))
            {
                // NOT TRANSIENT, SO NOT RETRIED. An unknown `Op` or a body
                // this build cannot deserialize fails the same way on every
                // attempt, forever. Treated as a 5xx it would back off
                // 2/4/8/16/32/60s for a full 24 hours, block every entry
                // behind it the whole time (head-of-line), and then expire
                // with `Outbox`'s notice telling the player it "could not be
                // sent within 24 hours" - which is false, and hides the real
                // cause. Ack it, surface it as refused, and log what actually
                // happened.
                Debug.LogError(
                    "[OutboxClient] dropping entry '" + entry.Key + "' (" + entry.Op +
                    "): it can never be sent by this build. " + ex);
                _box.Ack(entry.Key);
                _store.Save(_box);
                return OutboxAttemptRecord.Rejected(entry.Key, null);
            }
            catch
            {
                // 5xx (BroodlineApiException with StatusCode >= 500) or a
                // transport failure/timeout (no status code at all): there
                // was no server response to treat as truth. Back off and
                // retry later.
                _box.Fail(entry.Key, DateTime.UtcNow);
                _store.Save(_box);
                return OutboxAttemptRecord.Queued(entry.Key);
            }
        }

        /// A failure that this build will reproduce on every future attempt,
        /// so retrying it is not patience - it is a 24-hour stall ending in a
        /// notice that misdescribes what happened.
        ///
        /// `InvalidOperationException` is `SendAsync`'s `default:` branch: an
        /// `Op` this build has no case for, which a persisted queue written
        /// by a newer client (or a renamed route) can hand back. `JsonException`
        /// is `Deserialize` on a `Body` that no longer parses. Neither is a
        /// server verdict and neither becomes one by waiting.
        private static bool IsPermanent(Exception ex)
            => ex is InvalidOperationException || ex is JsonException;

        private Task<object> SendAsync(OutboxEntry entry)
        {
            switch (entry.Op)
            {
                case "wave/submit":
                    return SendAsync(_api.SubmitWaveAsync(entry.Key, Deserialize<WaveSubmitRequest>(entry.Body)));
                case "splice/commit":
                    return SendAsync(_api.SpliceCommitAsync(entry.Key, Deserialize<SpliceCommitRequest>(entry.Body)));
                case "node/claim":
                    return SendAsync(_api.ClaimNodeAsync(entry.Key, Deserialize<NodeClaimRequest>(entry.Body)));
                case "creature/name":
                    return SendAsync(_api.NameCreatureAsync(entry.Key, Deserialize<CreatureNameRequest>(entry.Body)));
                case "ftue/splice-stock":
                    return SendAsync(_api.FtueSpliceStockAsync(entry.Key));
                default:
                    throw new InvalidOperationException($"Unknown outbox op '{entry.Op}'.");
            }
        }

        // Boxes a strongly-typed generated-client call as Task<object>, so
        // `FlushAsync` can drain a queue of mixed ops through one loop.
        private static async Task<object> SendAsync<T>(Task<T> call) => await call.ConfigureAwait(false);

        private static byte[] Serialize<TReq>(TReq request) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));

        private static TReq Deserialize<TReq>(byte[] body) => JsonConvert.DeserializeObject<TReq>(Encoding.UTF8.GetString(body));
    }
}
