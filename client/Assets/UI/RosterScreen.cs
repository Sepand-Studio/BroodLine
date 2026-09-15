using System;
using System.Collections.Generic;
using Broodline.Api;

namespace Broodline.UI
{
    /// The roster, as the client is able to know it.
    ///
    /// **THERE IS NO ENDPOINT THAT LISTS OWNED CREATURES.** GET /v1/region
    /// /state returns `roster: { count, cap }` and nothing else; the only
    /// routes that hand back a `CreatureDto` are POST /v1/node/claim (what it
    /// just granted) and POST /v1/splice/commit (the child). So a cold start
    /// knows the SIZE of the roster and none of its members, and this type is
    /// a cache over what the session has been told rather than a view of a
    /// list it fetched.
    ///
    /// That gap is surfaced, not papered over: `IsComplete` is false whenever
    /// the cache holds fewer creatures than the server says the player has,
    /// and the screens are expected to say so rather than present a partial
    /// roster as the whole one. The alternative - showing what happens to be
    /// cached as if it were everything - is how a player concludes a creature
    /// was lost.
    ///
    /// LIVENESS IS NEVER DERIVED HERE. A creature is live only when it is not
    /// pruned and `consumed_at` is null, and that predicate belongs to the
    /// server (roster/creatures.ts). This cache only ever APPLIES an outcome
    /// the server reported - the two parents a commit says it consumed - which
    /// is a different thing from deciding liveness locally.
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

        /// The headline from GET /v1/region/state. Replaces the counts
        /// wholesale - client_architecture section 7: the next sync replaces,
        /// with no merge.
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
