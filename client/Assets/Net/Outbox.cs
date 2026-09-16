using System;
using System.Collections.Generic;

namespace Broodline.Net
{
    /// One durable, idempotent mutation waiting to be sent (or resent).
    ///
    /// `client_architecture` section 8's global constraint: the idempotency
    /// key is generated **when the action is taken, not when it is sent** -
    /// `OutboxEntry.For` is the only place a key is minted, and nothing after
    /// that point ever changes it. A retry after a kill, a crash, or three
    /// days offline sends this identical key, so the server's dedupe
    /// (`services/api` `adversarial.test.ts` - see `OutboxClient.cs`) treats
    /// it as the same action rather than a new one.
    public sealed class OutboxEntry
    {
        /// The Idempotency-Key sent with every attempt of this entry.
        public string Key { get; }

        /// The route this entry targets, e.g. "wave/submit" - see
        /// `OutboxClient.cs` for the full set and how each maps to a
        /// generated API call.
        public string Op { get; }

        /// The serialized request body. Opaque to `Outbox` - only
        /// `OutboxClient` knows how to turn this back into a typed request
        /// per `Op`.
        public byte[] Body { get; }

        /// When the action was taken (and the key minted). Entries are
        /// ordered by this, oldest first, and it is also the anchor for the
        /// 24-hour idempotency-window expiry.
        public DateTime CreatedAt { get; }

        /// How many times a send of this entry has failed.
        public int Attempts { get; internal set; }

        /// The entry is not attempted again before this instant. Set to
        /// `CreatedAt` on creation (immediately due) and pushed forward by
        /// `Outbox.Fail`.
        public DateTime NotBefore { get; internal set; }

        /// Set by `Outbox.Expire` when this entry is dropped for being
        /// older than the server's idempotency window. Explains, for the
        /// player, what did not happen - never replayed after this.
        public string Notice { get; internal set; }

        private OutboxEntry(string key, string op, byte[] body, DateTime createdAt, int attempts, DateTime notBefore)
        {
            Key = key;
            Op = op;
            Body = body ?? Array.Empty<byte>();
            CreatedAt = createdAt;
            Attempts = attempts;
            NotBefore = notBefore;
        }

        /// Mints a fresh entry with a new key, right now, at the moment the
        /// action is taken. `now` is also its `CreatedAt` and its initial
        /// `NotBefore` (immediately due).
        public static OutboxEntry For(string op, byte[] body, DateTime now)
        {
            if (string.IsNullOrEmpty(op)) throw new ArgumentException("An outbox entry needs an op.", nameof(op));
            return new OutboxEntry(Guid.NewGuid().ToString("N"), op, body, now, attempts: 0, notBefore: now);
        }

        /// Reconstructs a previously-minted entry from `OutboxStore` - the
        /// key, attempts and backoff already happened; this does not mint
        /// anything new. Internal: only `OutboxStore` (same assembly) calls
        /// this; nothing outside `Broodline.Net` may fabricate an entry with
        /// an existing key.
        internal static OutboxEntry Restore(string key, string op, byte[] body, DateTime createdAt, int attempts, DateTime notBefore)
        {
            return new OutboxEntry(key, op, body, createdAt, attempts, notBefore);
        }
    }

    /// The player's queue of pending mutations - `client_architecture`
    /// section 8. Pure: no I/O, no network, no Unity types. Ordering and
    /// backoff only; `OutboxStore` persists it and `OutboxClient` sends it.
    ///
    /// Ordered per player, drained oldest-first, and a failed head blocks
    /// everything behind it - "a splice that depends on a wave reward must
    /// not overtake it." That head-of-line blocking is deliberate, not a
    /// bug: `Next` only ever looks at index 0.
    public sealed class Outbox
    {
        private readonly List<OutboxEntry> _entries = new List<OutboxEntry>();

        /// All entries, oldest first. Internal - only `OutboxStore` (same
        /// assembly) walks the whole queue; everyone else uses `Peek` /
        /// `Next` / `Ack` / `Fail` / `Expire`.
        internal IReadOnlyList<OutboxEntry> AllEntries => _entries;

        /// Adds an entry, keeping the queue ordered oldest-first by
        /// `CreatedAt`. In the normal case (an entry minted and enqueued in
        /// the same call, per `OutboxClient`) this is always an append; the
        /// insertion-sort only matters when `OutboxStore.Load` replays
        /// entries that were not already in order on disk.
        public void Enqueue(OutboxEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            var index = _entries.Count;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].CreatedAt > entry.CreatedAt)
                {
                    index = i;
                    break;
                }
            }
            _entries.Insert(index, entry);
        }

        /// The head of the queue, regardless of whether it is due. Null if
        /// the queue is empty. Used by persistence and by tests that need
        /// to inspect backoff state without waiting for it to elapse.
        public OutboxEntry Peek() => _entries.Count > 0 ? _entries[0] : null;

        /// The head of the queue if - and only if - it is due
        /// (`NotBefore <= now`); otherwise null, even if entries exist
        /// behind it. That is the head-of-line block: a later entry never
        /// overtakes an earlier one that is backing off.
        public OutboxEntry Next(DateTime now)
        {
            if (_entries.Count == 0) return null;
            var head = _entries[0];
            return head.NotBefore <= now ? head : null;
        }

        /// The entry sent successfully (or rejected by the server with a
        /// definitive 4xx - see `OutboxClient`). Either way it is done and
        /// leaves the queue for good.
        public void Ack(string key)
        {
            var index = _entries.FindIndex(e => e.Key == key);
            if (index >= 0) _entries.RemoveAt(index);
        }

        /// A send attempt failed (5xx or transport failure). Backs the entry
        /// off exponentially - 2, 4, 8, 16, 32 seconds - capped at 60s, and
        /// leaves it at its current position: `CreatedAt` never changes, so
        /// ordering is untouched by failures.
        public void Fail(string key, DateTime now)
        {
            var entry = _entries.Find(e => e.Key == key);
            if (entry == null) return;

            entry.Attempts++;
            var delaySeconds = Math.Min(60d, Math.Pow(2d, entry.Attempts));
            entry.NotBefore = now.AddSeconds(delaySeconds);
        }

        /// Drops every entry older than the server's 24-hour idempotency
        /// window, stamping each with a notice explaining what did not
        /// happen - it is not safe to replay a mutation the server may have
        /// long since forgotten. Returns the dropped entries so the caller
        /// (`OutboxClient` / `OutboxPump`) can surface their notices.
        public IReadOnlyList<OutboxEntry> Expire(DateTime now)
        {
            var expired = new List<OutboxEntry>();
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.CreatedAt.AddHours(24) < now)
                {
                    entry.Notice = $"{entry.Op} from {entry.CreatedAt:t} could not be sent within 24 hours and did not happen.";
                    expired.Add(entry);
                    _entries.RemoveAt(i);
                }
            }
            expired.Reverse(); // restore oldest-first order (we walked backward to remove safely)
            return expired;
        }
    }
}
