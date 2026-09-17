using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Broodline.Api;

namespace Broodline.UI
{
    /// One harvest node, as the map draws it. Placeholder fidelity: a row of
    /// text where the region map will be.
    public sealed class NodeRow
    {
        public int Slot { get; internal set; }
        public string Type { get; internal set; }

        /// Shard units waiting to be claimed. The SERVER's accrual - design
        /// 4.1 and 4.2 make `accrue` pure and hand it one clock read per
        /// request, so a client that ticked this up locally between polls
        /// would be showing a number the claim will disagree with.
        public int Accrued { get; internal set; }

        /// Harvests left before the node depletes, or null for a node that
        /// does not deplete.
        public int? Remaining { get; internal set; }

        /// Creatures this claim would grant. Zero for a pure shard node.
        public int Grants { get; internal set; }

        /// False when the claim would be refused on state the screen can
        /// already see. The reason is in `Blocker`.
        public bool CanClaim { get; internal set; }

        public string Blocker { get; internal set; }

        /// `Remaining` as text - the harvest count when it depletes, or
        /// `RegionScreen.UnlimitedLabel` when it does not. `Remaining`
        /// doubles as a number and a fact (this class's own doc on it), so
        /// the view must not be the one choosing between the two.
        public string RemainingLabel { get; internal set; }

        /// The claim button's label. Static today, but authored here rather
        /// than in `RegionView`, per the same convention as `SpliceScreen.
        /// Cta` and `DeployScreen.Cta` - every player-facing string is
        /// authored outside the view.
        public string ClaimLabel { get; internal set; }
    }

    /// The region, its nodes, and what each has accrued. Built from
    /// GET /v1/region/state, which is DERIVED and writes nothing.
    public sealed class RegionScreenModel
    {
        public string RegionId { get; internal set; }
        public int Epoch { get; internal set; }
        public IReadOnlyList<NodeRow> Nodes { get; internal set; }

        /// The server's roster headline. Count and cap only - see
        /// RosterScreen for why that is all there is.
        public int RosterCount { get; internal set; }
        public int RosterCap { get; internal set; }

        /// Whether the two numbers above mean anything.
        ///
        /// THE LOAD-STATE AMBIGUITY RosterScreen HAS DOES NOT APPLY HERE, and
        /// the difference is structural rather than lucky: this model only
        /// ever exists as the product of `Build`, which refuses a null
        /// response, so a failed load produces NO model at all rather than one
        /// that claims something false. RosterScreen is a mutable cache that
        /// OUTLIVES a failed call; this is an immutable projection of one
        /// response that cannot exist without it.
        ///
        /// What does carry over is the `0 >= 0` shape, and it is reachable:
        /// the generated `RegionStateResponse` DEFAULT-INITIALISES `Roster` to
        /// `new Roster()`, so a response object that was constructed rather
        /// than deserialised carries Count 0 and Cap 0 - and a null check
        /// would not catch it. Both numbers are 0 there because they are
        /// UNSET, not because the Hatchery is a zero-capacity one.
        ///
        /// SO THE TEST IS `Cap > 0`, not a null check. `rosterCap()` returns
        /// 20 for the only authored tier and THROWS for any other, so zero is
        /// a value the server cannot send and is unambiguously "unset".
        /// `RosterIsFull` is gated on it for the same reason
        /// RosterScreen.IsFull is: an unknown cap reading as FULL would grey
        /// out the claim button for a player who has room.
        public bool HasRosterCounts { get; internal set; }

        public bool RosterIsFull { get { return HasRosterCounts && RosterCount >= RosterCap; } }
    }

    /// The map screen. Harvest a node; that is the loop's first beat.
    public static class RegionScreen
    {
        /// `NodeRow.RemainingLabel`'s word for a node that never depletes -
        /// deliberately not a number, so a node with no `Remaining` count
        /// never prints something a player could mistake for a countdown.
        public const string UnlimitedLabel = "unlimited";

        /// `NodeRow.ClaimLabel`, in one place - see `DeployScreen.Cta` for
        /// the same convention.
        public const string ClaimCta = "Claim";

        public static async Task<RegionStateResponse> LoadAsync(BroodlineApiClient api)
        {
            if (api == null) throw new ArgumentNullException("api");
            return await api.RegionStateAsync();
        }

        public static RegionScreenModel Build(RegionStateResponse state)
        {
            if (state == null) throw new ArgumentNullException("state");

            var count = state.Roster == null ? 0 : state.Roster.Count;
            var cap = state.Roster == null ? 0 : state.Roster.Cap;

            var rows = new List<NodeRow>();
            if (state.Nodes != null)
            {
                foreach (var node in state.Nodes)
                {
                    if (node == null) continue;
                    rows.Add(RowFor(node, count, cap, cap > 0));
                }
            }

            return new RegionScreenModel
            {
                RegionId = state.RegionId,
                Epoch = state.Epoch,
                Nodes = rows,
                RosterCount = count,
                RosterCap = cap,
                HasRosterCounts = cap > 0,
            };
        }

        /// The two refusals the screen can see coming, and no others.
        ///
        /// `grants` and `roster` are on this response FOR THIS - map/claim.ts:
        /// "`grants > 0 && roster.count + grants > roster.cap`" is the check
        /// the claim itself makes, published so the screen can say so before
        /// the player taps rather than after. It is an ADVISORY copy of the
        /// server's rule, not a second authority: the claim re-reads the count
        /// inside its own transaction and refuses `roster_full` there, and
        /// that refusal is the one that counts.
        private static NodeRow RowFor(
            Nodes node, int rosterCount, int rosterCap, bool capIsKnown)
        {
            var row = new NodeRow
            {
                Slot = node.Slot,
                Type = node.Type,
                Accrued = node.Accrued,
                Remaining = node.Remaining,
                Grants = node.Grants,
                CanClaim = true,
                Blocker = string.Empty,
                RemainingLabel = node.Remaining == null
                    ? UnlimitedLabel
                    : node.Remaining.Value.ToString(CultureInfo.InvariantCulture),
                ClaimLabel = ClaimCta,
            };

            if (node.Remaining != null && node.Remaining.Value <= 0)
            {
                row.CanClaim = false;
                row.Blocker = "This node is spent.";
                return row;
            }

            // A NODE THAT HAS ACCRUED NOTHING IS STILL CLAIMABLE, and that is
            // not a cosmetic choice.
            //
            // A node's clock is `harvest_positions.last_settled_at`, and
            // `settlePosition` - called only by `claimNode` - is its only
            // writer. An absent row reads as `now` (map/claim.ts's
            // `loadLastSettled`, deliberately, so that a first claim is not
            // backdated to the cap), which accrues zero. So until a player
            // claims once, nothing accrues; and the row is keyed by EPOCH, so
            // the same thing is true again after every weekly rollover.
            //
            // Greying the button out on `accrued <= 0` made that state
            // permanent: the only write that arms the clock was the one this
            // screen refused to send, and harvest - the loop's first beat -
            // could never start. The claim pays zero, writes the clock, and
            // the next visit accrues. Leave it enabled.

            // `capIsKnown` GATES THIS, and without it the check inverts its
            // own purpose: an unset cap of 0 makes `count + grants > 0` true
            // for every granting node, so a response that carried no roster
            // headline would grey out every claim button on the map - telling
            // a player with an empty roster that it has no room.
            if (capIsKnown && node.Grants > 0 && rosterCount + node.Grants > rosterCap)
            {
                row.CanClaim = false;
                // design 4.3 refuses the WHOLE claim rather than truncating
                // the grant, so this is not a partial-harvest warning - the
                // shards do not arrive either.
                row.Blocker = "Your roster has no room for "
                    + node.Grants + " more. Splice or retire first - "
                    + "a full roster refuses the whole claim, shards included.";
                return row;
            }

            return row;
        }

        /// Harvest. The Idempotency-Key is generated when the player taps,
        /// not when the request is sent, so a retry reads back the stored
        /// answer instead of paying twice.
        public static async Task<NodeClaimResponse> ClaimAsync(
            BroodlineApiClient api, string idempotencyKey, int slot)
        {
            if (api == null) throw new ArgumentNullException("api");
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                throw new ArgumentException(
                    "A claim needs an Idempotency-Key taken when the player tapped.",
                    "idempotencyKey");
            }
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException("slot", "A node slot is not negative.");
            }

            return await api.ClaimNodeAsync(idempotencyKey, new NodeClaimRequest { Slot = slot });
        }
    }
}
