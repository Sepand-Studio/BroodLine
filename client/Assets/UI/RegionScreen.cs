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

        /// The header's title, and the handoff's own name for this screen.
        ///
        /// FROM THE HANDOFF'S SECTION HEADING, which is the source
        /// `RosterScreen.Title` USED to take "Creature Roster" from - Phase 9
        /// Task 16 moved that one to the page title the handoff actually
        /// draws ("Your hybrids", `Creature Roster.dc.html:30`) and left the
        /// section heading as the file's name. This screen's heading and its
        /// drawn title are the same word, so nothing moved here.
        /// specs/Designs/design_handoff_broodline/README.md section 1 is
        /// "World Map - `Splice World Map v2.dc.html`", and its navigation
        /// model's tab table reads "Map -> World Map", which makes this the
        /// Map tab's destination and top-level.
        ///
        /// PHASE 8 TASK 12's PLAN ASKED FOR "Region", AND THAT WORD IS A
        /// HEADING IN NO DESIGN DOCUMENT - not the handoff README, not the
        /// .dc.html, not the bible. It describes what the screen currently
        /// SHOWS, which is one region's harvest nodes, rather than what the
        /// screen IS; and the two differ for one phase only, because the
        /// server ships one region (`rotation.ts`: "with one region and no
        /// relocation"). `RegionView.uss`'s header says in full why the map
        /// is not drawn yet and who owns drawing it.
        ///
        /// THE HANDOFF DRAWS NO HEADER TITLE ON THIS SCREEN AT ALL - its
        /// layout runs "status bar -> three currency chips in a row -> map
        /// viewport" - so this is the screen's NAME rather than a string
        /// quoted off a rendered header. Recorded because the difference
        /// matters to anyone later comparing the two side by side.
        public const string Title = "World Map";

        /// What the node list says when it holds nothing.
        ///
        /// IT STATES THE LIST'S STATE AND GIVES NO INSTRUCTION, on
        /// `DeployScreen.EmptyMessage`'s rule but for the opposite reason:
        /// there the instruction is already on screen in the blocker, and
        /// here there is no instruction to give, because a player cannot
        /// make a node appear. `nodesFor` (services/api/src/map/rotation.ts)
        /// returns slot 0 Common Vein and slot 1 Rich Deposit for every
        /// region, epoch and seed, so an empty list means a response that
        /// carried no nodes rather than a region that has none.
        public const string EmptyMessage = "No harvest nodes in this region.";

        /// The two facts a node's stat row always states - and the two it
        /// cannot.
        ///
        /// RATE AND TOTAL YIELD ARE NOT ON THE WIRE. `nodes.json` authors
        /// `ratePerHour` and `totalYield`, and `rotation.ts` reads both, but
        /// `RegionStateResponse`'s node object is slot, type, accrued,
        /// remaining and grants - it carries neither. So Task 12's "StatCells
        /// for rate and yield" is not something this screen can do, and a
        /// cell stating a rate would be a number the client invented.
        /// Richness is further out still: it is a REGION property in the
        /// handoff and no region property reaches the client at all.
        public const string AccruedStatLabel = "Accrued";
        public const string RemainingStatLabel = "Harvests left";

        /// Stated only when it is non-zero - `RegionView.CardFor` has why.
        public const string GrantsStatLabel = "Creatures";

        /// A plain count, as a stat cell prints it. InvariantCulture for the
        /// reason every other number in this assembly takes it - see
        /// `DeployScreen.WaveStatValue`.
        public static string StatValue(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// The roster headline, "5/20", or empty when the cap is unset.
        ///
        /// AUTHORED HERE RATHER THAN IN THE VIEW, the convention
        /// `UnlimitedLabel` and `ClaimCta` already follow: every
        /// player-facing string is authored outside the view.
        /// `HasRosterCounts` gates it for the reason that property's own doc
        /// gives - an unset cap of 0 must read as "unknown", never as "0 of
        /// 0" - and an empty return is what `RegionView` collapses the header
        /// line on.
        ///
        /// NO SPACES AROUND THE SLASH, WHERE `DeployScreen.DeployedStatValue`
        /// WRITES " / ". Two screens print a count against a cap two ways.
        /// This one is pinned to "5/20" by
        /// `RegionView_ShowsRosterCountAndCapWhenKnown`, a Phase 6
        /// assertion, and unifying the two is a copy decision rather than a
        /// layout one - so it is recorded here and not taken in Task 12.
        public static string RosterHeadline(RegionScreenModel model)
        {
            if (model == null) throw new ArgumentNullException("model");

            return model.HasRosterCounts
                ? model.RosterCount.ToString(CultureInfo.InvariantCulture)
                    + "/" + model.RosterCap.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

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
