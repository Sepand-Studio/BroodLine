using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Broodline.Api;

namespace Broodline.UI
{
    /// The roster, as the client is able to know it.
    ///
    /// **`GET /v1/roster` IS WHAT MAKES A COLD LAUNCH COMPLETE.** Load it once
    /// at start and this cache holds the player's whole live roster. Before
    /// that route existed the only creature lists a client could hold were the
    /// incidental ones - what POST /v1/node/claim had just granted and the
    /// child POST /v1/splice/commit had just made - so a relaunch lost every
    /// id and the loop closed only inside one session.
    ///
    /// `IsComplete` IS KEPT ANYWAY, and it is not vestigial: the roster call
    /// can fail, and a session that only ever saw a claim's grant still holds
    /// a PARTIAL roster. Showing what happens to be cached as if it were
    /// everything is how a player concludes a creature was lost, so the cache
    /// says when it is partial and the screens are expected to say so too.
    ///
    /// LIVENESS IS NEVER DERIVED HERE. A creature is live only when it is not
    /// pruned and `consumed_at` is null, and that predicate belongs to the
    /// server - `liveCreature()` in roster/creatures.ts, which is what
    /// /v1/roster filters on. This cache only ever APPLIES an outcome the
    /// server reported - the two parents a commit says it consumed - which is
    /// a different thing from deciding liveness locally.
    public sealed class RosterScreen
    {
        private readonly Dictionary<Guid, CreatureDto> _known = new Dictionary<Guid, CreatureDto>();
        private readonly List<CreatureDto> _order = new List<CreatureDto>();

        /// The server's count. Authoritative, and the thing `IsComplete`
        /// measures the cache against.
        public int ServerCount { get; private set; }

        public int ServerCap { get; private set; }

        /// The creatures this session has actually been handed.
        public IReadOnlyList<CreatureDto> Known { get { return _order; } }

        /// Whether `Known` is the whole roster. False until the session has
        /// seen as many creatures as the server counts.
        public bool IsComplete { get { return _order.Count >= ServerCount; } }

        public bool IsFull { get { return ServerCount >= ServerCap; } }

        /// What the screen should say when the cache is partial. Empty when it
        /// is not.
        public string IncompleteNotice
        {
            get
            {
                if (IsComplete) return string.Empty;
                return "Showing " + _order.Count + " of " + ServerCount
                    + " creatures. The rest have not been loaded this session.";
            }
        }

        /// Load the whole live roster. One call, at cold start, and after any
        /// refusal whose remedy is `RefreshRoster`.
        public static async Task<RosterResponse> LoadAsync(BroodlineApiClient api)
        {
            if (api == null) throw new ArgumentNullException("api");
            return await api.RosterAsync().ConfigureAwait(false);
        }

        /// The whole live roster, REPLACING what the cache held.
        ///
        /// Replaced rather than merged, for the reason client_architecture
        /// section 7 gives about the sync snapshot: the server has just stated
        /// the complete set, so a creature the cache holds and this response
        /// does not is a creature that is gone. Merging would keep a splice's
        /// consumed parent on screen forever, which is the exact failure
        /// splice_confirm_spec section 5 closes from the other side.
        ///
        /// `ServerCount` becomes the list's own length - the server sends no
        /// separate count, because it would be a second copy of a number
        /// derived from the same `liveCreature()` read. So after a successful
        /// load `IsComplete` is true by construction, which is the point.
        public void ApplyRoster(RosterResponse roster)
        {
            if (roster == null) throw new ArgumentNullException("roster");

            _known.Clear();
            _order.Clear();
            if (roster.Creatures != null)
            {
                foreach (var creature in roster.Creatures) Observe(creature);
            }

            ServerCount = _order.Count;
            ServerCap = roster.Cap;
        }

        /// The headline from GET /v1/region/state. Replaces the counts
        /// wholesale - client_architecture section 7: the next sync replaces,
        /// with no merge.
        ///
        /// COUNTS ONLY. The map's `roster` is a headline - it carries no
        /// members - so this can raise `ServerCount` above what the cache
        /// holds and make `IsComplete` false. That is correct: it means the
        /// player owns creatures this session has not loaded, and the screen
        /// should say so until /v1/roster answers.
        public void ApplyRegionState(RegionStateResponse state)
        {
            if (state == null) throw new ArgumentNullException("state");
            if (state.Roster == null) return;
            ServerCount = state.Roster.Count;
            ServerCap = state.Roster.Cap;
        }

        /// Creatures a claim granted, plus the roster count the claim implies.
        public void ApplyClaim(NodeClaimResponse claim)
        {
            if (claim == null) throw new ArgumentNullException("claim");
            if (claim.Creatures == null) return;
            foreach (var creature in claim.Creatures) Observe(creature);
            // The grant is the server telling us the roster grew.
            if (_order.Count > ServerCount) ServerCount = _order.Count;
        }

        /// A splice, applied: the child arrives and the two parents are gone.
        ///
        /// Dropping the parents here is not a liveness decision. A 200 from
        /// /v1/splice/commit IS the server's statement that those two rows
        /// were consumed in that transaction, and continuing to draw them
        /// would show the player creatures that no longer exist - which is the
        /// disclosure failure splice_confirm_spec section 5 exists to close
        /// from the other side.
        public void ApplySplice(SpliceCommitResponse committed, Guid parentA, Guid parentB)
        {
            if (committed == null) throw new ArgumentNullException("committed");
            Forget(parentA);
            Forget(parentB);
            Observe(committed.Child);
            // Two consumed, one born.
            if (ServerCount > 0) ServerCount = Math.Max(_order.Count, ServerCount - 1);
        }

        /// Record a creature the server handed us.
        public void Observe(CreatureDto creature)
        {
            if (creature == null) return;
            if (_known.ContainsKey(creature.CreatureId))
            {
                var index = _order.FindIndex(c => c.CreatureId == creature.CreatureId);
                if (index >= 0) _order[index] = creature;
                _known[creature.CreatureId] = creature;
                return;
            }
            _known.Add(creature.CreatureId, creature);
            _order.Add(creature);
        }

        public CreatureDto Find(Guid creatureId)
        {
            CreatureDto creature;
            return _known.TryGetValue(creatureId, out creature) ? creature : null;
        }

        /// Everything the cache holds that is not out fighting. `CommittedTo`
        /// is the SERVER's field; reading it is not re-deriving anything.
        public IReadOnlyList<CreatureDto> Available()
        {
            var free = new List<CreatureDto>();
            foreach (var creature in _order)
            {
                if (creature.CommittedTo == null) free.Add(creature);
            }
            return free;
        }

        /// Whether these two can be sent to /v1/splice/preview. Every refusal
        /// here mirrors one the route makes, so the screen does not offer a
        /// pairing that can only come back rejected.
        public bool CanSplice(Guid parentA, Guid parentB, out string blocker)
        {
            if (parentA == parentB)
            {
                blocker = "A splice needs two different creatures.";
                return false;
            }

            var a = Find(parentA);
            var b = Find(parentB);
            if (a == null || b == null)
            {
                // creature_not_owned's remedy, reached before the request.
                blocker = "That creature is not loaded. Refresh your roster.";
                return false;
            }

            var committed = a.CommittedTo != null ? a : (b.CommittedTo != null ? b : null);
            if (committed != null)
            {
                blocker = CreatureLabel.DisplayName(committed)
                    + " is out fighting. Wait for it to come back, then splice.";
                return false;
            }

            blocker = string.Empty;
            return true;
        }

        private void Forget(Guid creatureId)
        {
            if (!_known.Remove(creatureId)) return;
            var index = _order.FindIndex(c => c.CreatureId == creatureId);
            if (index >= 0) _order.RemoveAt(index);
        }
    }
}
