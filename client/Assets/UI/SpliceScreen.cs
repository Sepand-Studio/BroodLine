using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Broodline.Api;

namespace Broodline.UI
{
    /// One confirmation dialog. Layout is placeholder; every string and every
    /// flag on it is a requirement from broodline_splice_confirm_spec section 4.
    public sealed class SpliceDialog
    {
        public string Title { get; internal set; }
        public string Body { get; internal set; }
        public string ConfirmLabel { get; internal set; }
        public string CancelLabel { get; internal set; }

        /// Whether the CONFIRM button is styled as the primary action.
        /// Section 4: "No destructive default. Cancel is the safe action and
        /// should not be styled as the primary."
        public bool ConfirmIsPrimary { get; internal set; }

        /// Section 4: "Never suppressible. No 'don't show this again' - the
        /// whole point is that it interrupts the familiar rhythm." Nothing in
        /// this assembly ever sets this true; it exists so that a future
        /// "remember my choice" feature has to change a value a test reads.
        public bool Suppressible { get; internal set; }
    }

    /// The Splice Chamber, as the screen renders it.
    ///
    /// The forecast and the coverage warning are the SERVER's. Broodline.UI
    /// does not reference Broodline.Sim and must not: the server's
    /// distribution is the only published source of these odds, and a client
    /// that re-derived them locally would be a second source of a number the
    /// design commits to publishing.
    public sealed class SpliceScreenModel
    {
        public CreatureDto ParentA { get; internal set; }
        public CreatureDto ParentB { get; internal set; }

        /// splice_confirm_spec section 3. Sits directly above the CTA. Not a
        /// tooltip, not a footnote.
        public string DestructionNotice { get; internal set; }

        /// section 3: the cost belongs ON the button.
        public string CtaLabel { get; internal set; }

        /// sample_economy section 7's screen requirement, rendered from the
        /// server's `coverageLost`. Empty when the server named nothing.
        public string CoverageWarning { get; internal set; }

        /// The server's forecast object itself, not a copy.
        public SpliceForecast Forecast { get; internal set; }

        /// section 2: the odds are shown BEFORE the charge is spent. A model
        /// built from a preview has spent nothing - /v1/splice/preview writes
        /// nothing and "neither costs the player anything". Only AfterCommit
        /// flips this.
        public bool ChargeSpent { get; internal set; }

        /// Charges left, known only once a commit has answered. -1 until then:
        /// the cached balance is a label, not a number the client does
        /// arithmetic on (client_architecture section 7).
        public int ChargesRemaining { get; internal set; }

        public SpliceDialog StandardDialog { get; internal set; }

        public bool RequiresFounderConfirm { get { return FounderDialogs.Count > 0; } }

        /// The first Founder dialog, or null when no parent is a Founder.
        public SpliceDialog FounderDialog
        {
            get { return FounderDialogs.Count == 0 ? null : FounderDialogs[0]; }
        }

        /// One per Founder parent, in parent order. Both parents being
        /// Founders is reachable, and collapsing two into one dialog would
        /// drop a name section 4 makes load-bearing.
        public IReadOnlyList<SpliceDialog> FounderDialogs { get; internal set; }

        /// Every dialog, IN THE ORDER THEY ARE SHOWN. Section 4 makes the
        /// Founder dialog the "second dialog, after the standard one", so the
        /// order is part of the requirement rather than an implementation
        /// detail of whoever presents them.
        public IReadOnlyList<SpliceDialog> Dialogs { get; internal set; }
    }

    /// Builds the Splice Chamber's copy, and calls the two splice routes.
    ///
    /// Everything on this screen that is a NUMBER comes from the server.
    /// Everything that is a SENTENCE is built here, from splice_confirm_spec.
    public static class SpliceScreen
    {
        /// section 3. The CTA label the shipped design ended on was "Begin
        /// Splice", and section 1 names that as the gap this spec exists to
        /// close: "a player who taps past everything else still reads the
        /// thing they tap."
        public const string Cta = "Splice — consumes both parents.";

        /// The scaffold's header. The handoff's own name for this screen
        /// (README section 6) and the destination its `Splice` tab points at,
        /// so it is a top-level screen and carries no back chevron.
        public const string Title = "Splice Chamber";

        /// The forecast card's heading. Section 2's own word for what the
        /// table is: the odds shown BEFORE the charge is spent.
        public const string ForecastHeading = "Trait forecast";

        /// What the forecast card says when the server named no outcomes.
        ///
        /// REACHABLE, THOUGH `Build` REQUIRES A PREVIEW. `preview` being
        /// mandatory means the forecast OBJECT is always there; it does not
        /// mean `Combat2` has anything in it. A card that rendered an empty
        /// table would look like a screen that failed to load on the one
        /// screen section 2 makes the odds non-negotiable for, so it says so
        /// instead - and says that the charge is still unspent, because that
        /// is the fact that decides whether a player should tap anyway.
        public const string ForecastEmptyMessage =
            "The server named no trait outcomes for this pairing. Nothing has been spent.";

        /// Fetch the forecast. POST /v1/splice/preview writes nothing and
        /// spends nothing, which is what makes it safe to call as the player
        /// changes the lock.
        public static async Task<SplicePreviewResponse> PreviewAsync(
            BroodlineApiClient api, Guid parentA, Guid parentB, SpliceLock locked)
        {
            if (api == null) throw new ArgumentNullException("api");
            if (locked == null) throw new ArgumentNullException("locked");
            RequireTwoDistinctParents(parentA, parentB);

            return await api.SplicePreviewAsync(new SplicePreviewRequest
            {
                ParentA = parentA,
                ParentB = parentB,
                Locked = locked,
            });
        }

        /// Build the screen from the two parents and the server's preview.
        ///
        /// `preview` is REQUIRED. There is no overload that builds this screen
        /// without one, because section 2 makes showing the odds before the
        /// charge non-negotiable and an optional forecast is a forecast that
        /// ships missing.
        public static SpliceScreenModel Build(
            CreatureDto parentA, CreatureDto parentB, SplicePreviewResponse preview)
        {
            if (parentA == null) throw new ArgumentNullException("parentA");
            if (parentB == null) throw new ArgumentNullException("parentB");
            if (preview == null) throw new ArgumentNullException("preview");
            RequireTwoDistinctParents(parentA.CreatureId, parentB.CreatureId);

            var founders = new List<SpliceDialog>();
            if (parentA.IsFounder) founders.Add(FounderDialogFor(parentA));
            if (parentB.IsFounder) founders.Add(FounderDialogFor(parentB));

            var standard = StandardDialogFor(parentA, parentB);

            // Standard first, Founder dialogs after - section 4's ordering.
            var dialogs = new List<SpliceDialog> { standard };
            dialogs.AddRange(founders);

            return new SpliceScreenModel
            {
                ParentA = parentA,
                ParentB = parentB,
                DestructionNotice = DestructionNoticeFor(parentA, parentB),
                CtaLabel = Cta,
                CoverageWarning = CoverageWarningFor(preview.CoverageLost),
                Forecast = preview.Forecast,
                ChargeSpent = false,
                ChargesRemaining = -1,
                StandardDialog = standard,
                FounderDialogs = founders,
                Dialogs = dialogs,
            };
        }

        /// The same screen, after the charge has actually been spent. Separate
        /// from Build so that `ChargeSpent` is a fact about what happened
        /// rather than a constant, and so the reveal screen is handed the
        /// balance the SERVER reported rather than one the client decremented.
        public static SpliceScreenModel AfterCommit(
            SpliceScreenModel model, SpliceCommitResponse committed)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (committed == null) throw new ArgumentNullException("committed");

            return new SpliceScreenModel
            {
                ParentA = model.ParentA,
                ParentB = model.ParentB,
                // Section 5: the reveal keeps the same line. Same words, so
                // the fact reads as confirmed rather than newly disclosed.
                DestructionNotice = model.DestructionNotice,
                CtaLabel = model.CtaLabel,
                CoverageWarning = model.CoverageWarning,
                Forecast = model.Forecast,
                ChargeSpent = true,
                ChargesRemaining = committed.Balance,
                StandardDialog = model.StandardDialog,
                FounderDialogs = model.FounderDialogs,
                Dialogs = model.Dialogs,
            };
        }

        /// splice_confirm_spec section 3, verbatim in shape:
        ///
        ///   **Both parents are consumed.**
        ///   Vetch Crawler (G4) and Ember Skitter (G6) will be permanently
        ///   removed from your roster. Their record stays in the lineage.
        ///
        /// The second line is not decoration. It "is the reassurance that
        /// makes the loss survivable, and it is true" - the consumed parents
        /// stay in the lineage, which is what the tree shows immediately after.
        public static string DestructionNoticeFor(CreatureDto parentA, CreatureDto parentB)
        {
            return "Both parents are consumed.\n"
                + CreatureLabel.WithGeneration(parentA) + " and "
                + CreatureLabel.WithGeneration(parentB)
                + " will be permanently removed from your roster."
                + " Their record stays in the lineage.";
        }

        /// section 4, Standard. Dismissible - Cancel is a real answer - but
        /// NOT suppressible: nothing in the spec permits a "don't show this
        /// again" on either dialog, and section 7 rejects dialogs that train
        /// dismissal.
        private static SpliceDialog StandardDialogFor(CreatureDto parentA, CreatureDto parentB)
        {
            return new SpliceDialog
            {
                Title = "Splice these two?",
                Body = CreatureLabel.DisplayName(parentA) + " and "
                    + CreatureLabel.DisplayName(parentB)
                    + " will be consumed. This cannot be undone.",
                ConfirmLabel = "Splice",
                CancelLabel = "Cancel",
                ConfirmIsPrimary = true,
                Suppressible = false,
            };
        }

        /// section 4, Founder. The name carries the sentence.
        ///
        /// An unnamed Founder falls back to the species, which gives the WEAK
        /// version of this dialog - section 4 says "Consume Vetch Crawler"
        /// does not stop a player. It is raised anyway: a weak interrupt is a
        /// far smaller failure than no interrupt, and skipping it would make
        /// a null name the way to lose a Founder silently.
        private static SpliceDialog FounderDialogFor(CreatureDto founder)
        {
            var name = CreatureLabel.DisplayName(founder);
            return new SpliceDialog
            {
                Title = "Consume " + name + " permanently?",
                Body = name + " is one of your Founders. This cannot be undone. "
                    + name + " will remain at the root of every lineage descended from them.",
                // Buttons state the OUTCOME, not Yes/No. "A player tapping
                // 'Keep Ash' cannot do it by accident."
                ConfirmLabel = "Consume " + name,
                CancelLabel = "Keep " + name,
                // No destructive default.
                ConfirmIsPrimary = false,
                // Never suppressible.
                Suppressible = false,
            };
        }

        /// sample_economy section 7: "the splice confirmation must state which
        /// coverage will not carry, by name and tier."
        ///
        /// Rendered from the SERVER's `coverageLost`, which names only what
        /// cannot carry under any outcome the forecast published - empty for
        /// four distinct traits, and the un-locked duplicate otherwise. The
        /// route says why that call is not the client's to make: "the client
        /// must not be the thing that works out which coverage is at stake."
        ///
        /// Empty in, empty out. A warning shown when nothing is certainly lost
        /// would be false half the time, and a warning that is false half the
        /// time trains dismissal.
        public static string CoverageWarningFor(ICollection<CoverageLost> coverageLost)
        {
            if (coverageLost == null || coverageLost.Count == 0) return string.Empty;

            var named = new List<string>(coverageLost.Count);
            foreach (var lost in coverageLost)
            {
                if (lost == null) continue;
                named.Add(CreatureLabel.TraitWithTier(lost.Trait, lost.Tier));
            }
            if (named.Count == 0) return string.Empty;

            return JoinWithAnd(named) + " will not carry.";
        }

        /// "A", "A and B", "A, B and C".
        private static string JoinWithAnd(IReadOnlyList<string> items)
        {
            if (items.Count == 1) return items[0];
            if (items.Count == 2) return items[0] + " and " + items[1];

            var head = new List<string>(items.Count - 1);
            for (var i = 0; i < items.Count - 1; i++) head.Add(items[i]);
            return string.Join(", ", head.ToArray()) + " and " + items[items.Count - 1];
        }

        /// The mutation pair, section 2: "stated as two numbers", separately,
        /// "so the player understands these are not the same event". Both come
        /// off the server's forecast; neither is computed here.
        public static string MutationLine(SpliceForecast forecast)
        {
            if (forecast == null) return string.Empty;
            return "Mutation " + Percent(forecast.Mutation)
                + " — of which Aberrant " + Percent(forecast.Aberrant) + ".";
        }

        /// The one place a probability becomes a percent string on this
        /// screen - `MutationLine` uses it, and so does `SpliceChamberView`
        /// for each `Combat2` row, so the same number reads the same way on
        /// both. Public (not private) for exactly that second caller: a view
        /// formatting `outcome.P` itself, even with a standard .NET
        /// specifier like `"P1"`, is a second copy of this rule that can -
        /// and did - drift from it (`"55%"` here vs `"55.0 %"` from `P1`).
        public static string Percent(double p)
        {
            return Math.Round(p * 100.0, 1).ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// Spend the charge. The ONLY place this assembly destroys anything.
        ///
        /// The Idempotency-Key is generated when the player takes the action,
        /// not when the request is sent, so a retry of a splice the server
        /// already performed reads back the stored answer instead of consuming
        /// two more creatures.
        public static async Task<SpliceCommitResponse> CommitAsync(
            BroodlineApiClient api, string idempotencyKey,
            Guid parentA, Guid parentB, SpliceLock locked, string bodyFrom)
        {
            if (api == null) throw new ArgumentNullException("api");
            if (locked == null) throw new ArgumentNullException("locked");
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                throw new ArgumentException(
                    "A splice needs an Idempotency-Key taken when the player confirmed.",
                    "idempotencyKey");
            }
            if (string.IsNullOrEmpty(bodyFrom))
            {
                throw new ArgumentException(
                    "bodyFrom must name one of the two parents' species - the body choice is "
                    + "the player's only controlled lever and has no default.",
                    "bodyFrom");
            }
            RequireTwoDistinctParents(parentA, parentB);

            return await api.SpliceCommitAsync(idempotencyKey, new SpliceCommitRequest
            {
                ParentA = parentA,
                ParentB = parentB,
                Locked = locked,
                BodyFrom = bodyFrom,
            });
        }

        /// /v1/splice/preview and /v1/splice/commit both refuse
        /// `parentA === parentB` as invalid_request. Refused here so the
        /// screen cannot express a request that can only be rejected.
        private static void RequireTwoDistinctParents(Guid parentA, Guid parentB)
        {
            if (parentA == parentB)
            {
                throw new ArgumentException(
                    "A splice needs two different creatures.", "parentB");
            }
        }
    }
}
