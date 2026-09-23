using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Broodline.Api;

namespace Broodline.UI
{
    /// Whether the member list has ever been fetched.
    ///
    /// `NeverLoaded` is the zero value deliberately: a `RosterScreen` nobody
    /// has written to must start out admitting it knows nothing, rather than
    /// defaulting to a state that lets it claim otherwise.
    public enum RosterLoadState
    {
        NeverLoaded = 0,
        Failed,
        Loaded,
    }

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
        /// The page title, and it is NOT the screen's name.
        ///
        /// "YOUR HYBRIDS" IS WHAT THE HANDOFF DRAWS; "Creature Roster" IS
        /// WHAT IT FILES THE SCREEN UNDER, and Phase 9 Task 16 found the two
        /// had been conflated. README section 9's index entry is the file
        /// name - `Creature Roster.dc.html` - while line 30 of that file puts
        /// `Your hybrids` in the 21px Baloo title slot with
        /// `Hatchery · 14 of 20 slots` as its eyebrow above it. A player
        /// never reads an index; they read the header.
        ///
        /// The tab argument the old comment made is untouched and still
        /// right: the tab vocabulary is Map/Ark/Splice/Lab/Allies and none of
        /// them is this screen, which the handoff's push table reaches from
        /// the Splice Chamber - so `RosterView` is still `pushed: true`.
        ///
        /// HERE RATHER THAN IN `RosterView` for the reason `WaveScreens.cs`
        /// states and the Task 15 review paid for: "a test that asserts a
        /// view renders 'Start' cannot tell a view reading its model from a
        /// view holding a literal." This class already authors
        /// `IncompleteNotice`, so it is where the screen's words live.
        public const string Title = "Your hybrids";

        /// The eyebrow over the title - `Creature Roster.dc.html:29`'s
        /// `Hatchery · 14 of 20 slots`, verbatim in shape.
        ///
        /// UPPERCASE IN THE STRING, WHICH IS NOT A STYLE CHOICE MADE HERE.
        /// The handoff renders every eyebrow through `.lbl { text-transform:
        /// uppercase }` and UI Toolkit has no `text-transform` at all, so the
        /// casing is baked into what this returns - the user's ruling at the
        /// Task 13/14 boundary, and the same one `FounderNamingScreen
        /// .Eyebrow` and `CostCtaRow.CostEyebrow` already carry. Anything
        /// asserting this reads the method, never a repeated literal.
        ///
        /// A METHOD RATHER THAN A CONSTANT BECAUSE IT CARRIES TWO NUMBERS,
        /// and `RosterView` marks the whole line `t-num` for them: how full
        /// the Hatchery is decides whether a claim can be taken at all
        /// (`IsFull`), which is bible 10.6's test for a figure that needs the
        /// tabular face. The floor that goes with the face costs this line
        /// 10px -> 11px against the handoff's 9, recorded in
        /// `RosterView.uss` beside the rule that does it.
        ///
        /// BOTH NUMBERS ARE THE SERVER'S. `ServerCount` is authoritative and
        /// `ServerCap` comes from `rosterCap()`; neither is `Known.Count`,
        /// which is only what this session happens to hold. A roster that has
        /// not loaded says so in `IncompleteNotice` - this line is not where
        /// that is hidden.
        public static string Eyebrow(int count, int cap)
        {
            return "HATCHERY · " + count.ToString(CultureInfo.InvariantCulture)
                + " OF " + cap.ToString(CultureInfo.InvariantCulture) + " SLOTS";
        }

        /// What the grid says when it holds nothing AND the cache knows that
        /// is the whole truth.
        ///
        /// SHOWN ONLY WHEN `IsComplete`, AND THAT CONDITION IS THE WHOLE
        /// POINT OF THE SENTENCE. `IncompleteNotice`'s own comment is that
        /// "you own nothing" and "we could not find out what you own" must
        /// never read the same; this is the first half, so a view that
        /// rendered it over a never-loaded cache would be telling the exact
        /// lie `RosterLoadState` exists to prevent. The second half is
        /// `IncompleteNotice`, and `RosterView` puts it in the scaffold's
        /// footer where it is on screen at the same time.
        public const string EmptyMessage =
            "No creatures yet. Claim a resource node and your first arrivals land here.";

        private readonly Dictionary<Guid, CreatureDto> _known = new Dictionary<Guid, CreatureDto>();
        private readonly List<CreatureDto> _order = new List<CreatureDto>();

        /// The server's count. Authoritative, and the thing `IsComplete`
        /// measures the cache against. MEANINGLESS UNTIL `HasCounts` - see it.
        public int ServerCount { get; private set; }

        public int ServerCap { get; private set; }

        /// The creatures this session has actually been handed.
        public IReadOnlyList<CreatureDto> Known { get { return _order; } }

        /// Whether the member list has ever been loaded, and how that went.
        ///
        /// **THIS EXISTS BECAUSE "EMPTY" AND "UNKNOWN" ARE NOT THE SAME STATE,
        /// AND COUNTING CANNOT TELL THEM APART.** `_order.Count >= ServerCount`
        /// is `0 >= 0` on a cache nothing has ever written to, so without this
        /// flag a failed first load reads as "roster complete, 0 creatures" -
        /// identical to a genuine new player. A player with twelve creatures
        /// would be shown an empty roster and told that was correct, which is
        /// strictly worse than the gap `IncompleteNotice` was written for: the
        /// client would have stopped knowing it was missing data.
        public RosterLoadState LoadState { get; private set; }

        /// Whether `ServerCount` and `ServerCap` mean anything yet. Set by the
        /// map poll or by a roster load, whichever arrives first; false on a
        /// cache nothing has written to, where both fields are 0 because they
        /// are unset rather than because the player owns nothing.
        ///
        /// MEASURED BY `ServerCap > 0`, NOT BY "something was applied". The
        /// generated `RegionStateResponse` default-initialises `Roster` to
        /// `new Roster()`, so applying a constructed-not-deserialised response
        /// writes Count 0 and Cap 0 and a "did we apply one" flag would call
        /// that known. `rosterCap()` returns 20 for the only authored tier and
        /// THROWS for any other, so zero is a cap the server cannot send.
        public bool HasCounts { get { return ServerCap > 0; } }

        /// Whether `Known` is the whole live roster.
        ///
        /// FALSE UNTIL A LOAD HAS ACTUALLY SUCCEEDED. Observing creatures from
        /// a claim's grant does not make it true either: a grant is not a
        /// listing, and the whole point of this property is that only the
        /// server saying "here is all of it" counts as that.
        public bool IsComplete
        {
            get { return LoadState == RosterLoadState.Loaded && _order.Count >= ServerCount; }
        }

        /// Whether the Hatchery is at capacity.
        ///
        /// FALSE WHEN UNKNOWN, and that is the safe direction rather than the
        /// convenient one. This prediction is advisory - `roster_full` is a
        /// refusal the server makes inside its own transaction, and design 4.3
        /// refuses the whole claim there. So an unknown cap that read as FULL
        /// would grey out the claim button for a player who has room and give
        /// them nothing to tap; an unknown cap that reads as NOT full lets them
        /// tap and lets the server answer, which it was always going to do.
        public bool IsFull { get { return HasCounts && ServerCount >= ServerCap; } }

        /// What the screen must say when it does not have the whole roster.
        /// Empty ONLY when the list is known to be complete.
        ///
        /// Three different sentences for three different situations, because
        /// collapsing them is the defect this property exists to prevent. "You
        /// own nothing" and "we could not find out what you own" must never
        /// read the same.
        public string IncompleteNotice
        {
            get
            {
                if (IsComplete) return string.Empty;

                if (LoadState == RosterLoadState.Failed)
                {
                    return "Your roster could not be loaded, so this is not all of it. "
                        + "Try again.";
                }
                if (LoadState == RosterLoadState.NeverLoaded)
                {
                    if (!HasCounts) return "Your roster has not been loaded yet.";
                    return "Showing " + _order.Count + " of " + ServerCount
                        + " creatures. Your roster has not been loaded yet.";
                }

                // Loaded, but the map has since reported more than the list
                // holds - the player gained a creature elsewhere.
                return "Showing " + _order.Count + " of " + ServerCount
                    + " creatures. The rest have not been loaded this session.";
            }
        }

        /// Load the whole live roster, and RECORD WHETHER THAT WORKED. One
        /// call at cold start, and again after any refusal whose remedy is
        /// `RefreshRoster`.
        ///
        /// Returns null on success and the refusal otherwise. It does not
        /// throw for a server error, because a caller that forgets a try/catch
        /// around a roster load is exactly how the cache ends up silently
        /// claiming to be complete - the failure is recorded here, on the
        /// object that would otherwise make the false claim, rather than left
        /// to a caller to remember.
        ///
        /// An INSTANCE method where RegionScreen's is static, and the
        /// asymmetry is the point: this call has state to update and a lie to
        /// avoid telling, and RegionScreen has neither.
        public async Task<ServerError> LoadAsync(BroodlineApiClient api)
        {
            if (api == null) throw new ArgumentNullException("api");

            RosterResponse response;
            try
            {
                response = await api.RosterAsync();
            }
            catch (Exception error)
            {
                MarkLoadFailed();
                return ServerError.From(error);
            }

            ApplyRoster(response);
            return null;
        }

        /// The roster could not be loaded. Whatever the cache holds is not the
        /// whole of it, and it must stop saying otherwise.
        ///
        /// The cached creatures are KEPT rather than cleared: they were real
        /// when the server handed them over, and throwing them away would turn
        /// a failed refresh into an empty screen. What changes is the CLAIM.
        public void MarkLoadFailed()
        {
            LoadState = RosterLoadState.Failed;
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
            // The one thing that may set this. A grant, a splice or a map poll
            // must not: none of them is the server stating the complete set.
            LoadState = RosterLoadState.Loaded;
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

            // SERVERCOUNT IS DELIBERATELY NOT TOUCHED, and the arithmetic that
            // used to be here - `Math.Max(_order.Count, ServerCount - 1)` -
            // is the thing being removed rather than corrected.
            //
            // That was a client-side guess at a server-authoritative number,
            // and it is only right while nothing else has touched the roster
            // since the last sync - the one assumption the other three writers
            // never make. A creature gained through a channel this cache never
            // heard about (a claim before the next map poll) makes the guess
            // land LOW, and a low ServerCount makes IsComplete report a
            // COMPLETE roster that is missing creatures: the same false
            // completeness RosterLoadState was added to prevent, reached by
            // mutation instead of by a failed first load.
            //
            // And the cache cannot tell that case apart from a healthy one:
            // in both, it holds every creature it has ever been told about.
            // "Was it complete before?" is answered from the same stale
            // picture that is wrong, so conditioning on it reproduces the bug.
            // There is no client-side number here that is safe to invent.
            //
            // Leaving it alone makes _order.Count fall BELOW ServerCount, so
            // IsComplete goes false and the screen asks for a refresh. After a
            // splice against a genuinely complete cache that is one redundant
            // refresh; after one against a stale cache it is the truth. Over-
            // reporting incompleteness is the safe direction to be wrong in.
            //
            // OWED: SpliceCommitResponse carries no `roster` headline though
            // RegionStateResponse does, and claimNode already computes count
            // and cap inside its own transaction. With one on both mutating
            // responses, this would ASSIGN a server-stated number like every
            // other writer and the refresh would not be needed.
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
