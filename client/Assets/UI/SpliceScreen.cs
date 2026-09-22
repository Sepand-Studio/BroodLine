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
        ///
        /// SUPERSEDED ON THE SCREEN BY `InheritanceHeading` IN PHASE 9 TASK
        /// 16 and kept here rather than deleted: it is a `public const` on a
        /// model class, the phrase section 2 actually uses, and the eyebrow
        /// that replaced it is the HANDOFF's word for the same card. If the
        /// two ever have to be reconciled, the losing one should still be
        /// readable beside the winner.
        public const string ForecastHeading = "Trait forecast";

        // ---------------------------------------------------------------
        // Phase 9 Task 16 - `Splice Chamber.dc.html`'s own words.
        //
        // EVERY EYEBROW BELOW SHIPS UPPERCASE AND NOTHING RENDERS IT SO.
        // The handoff puts all of them through `.lbl { text-transform:
        // uppercase }` (`Splice Chamber.dc.html:16`) and UI Toolkit has no
        // `text-transform` at all - Task 13 established that and the user
        // ruled at the Task 13/14 boundary that the casing is baked into the
        // NAMED CONSTANT and never scattered as a literal in markup or in a
        // test. `FounderNamingScreen.Eyebrow` and `CostCtaRow.CostEyebrow`
        // are the same constant for the same reason.
        // ---------------------------------------------------------------

        /// The kicker over the title - `Splice Chamber.dc.html:38`, which
        /// says where in the app the player is standing rather than what the
        /// screen does.
        public const string Eyebrow = "GENE LAB";

        /// The two parent tiles' eyebrows - `Splice Chamber.dc.html:52`
        /// and `:80`.
        public const string ParentAEyebrow = "PARENT A";
        public const string ParentBEyebrow = "PARENT B";

        /// The predicted-hybrid band's eyebrow - `Splice Chamber.dc.html:103`.
        public const string PredictedHeading = "PREDICTED HYBRID";

        /// What the child is called before it exists - `Splice Chamber
        /// .dc.html:117`. NOT a name the server will use: bible 3.3 makes
        /// naming a Founder-only affordance today and `only_founders_named`
        /// refuses a named non-Founder, so this is a placeholder that says it
        /// is one.
        public const string PredictedName = "Unnamed Hybrid";

        /// The inheritance card's eyebrow and its right-hand note -
        /// `Splice Chamber.dc.html:135` and `:136`. The note is lowercase in
        /// the handoff and is NOT a `.lbl`, so it is not uppercased here.
        public const string InheritanceHeading = "TRAIT INHERITANCE";
        public const string OddsNote = "odds";

        /// The lineage card's eyebrow - `Splice Chamber.dc.html:185`.
        public const string LineageHeading = "LINEAGE";

        /// What one splice costs, as the cost row states it.
        ///
        /// "1", NOT THE HANDOFF'S "2". `Splice Chamber.dc.html:222` draws a
        /// 2; `splice_monetization_spec` and `SpliceCommitAsync`'s own
        /// balance arithmetic are one charge per splice, and the balance the
        /// header pill shows is decremented by one. A cost row that said 2
        /// beside a pill that fell by 1 would be the screen disagreeing with
        /// itself about the only number the decision is about.
        ///
        /// A STRING BECAUSE `CostCtaRow` TAKES ONE, and it is the whole
        /// value: no currency symbol, because the glyph beside it is the
        /// currency.
        public const string ChargeCost = "1";

        /// The muted cap on the header's charge pill - `Splice Chamber
        /// .dc.html:45`'s `/5`. The 5 is the Splicing Chamber's charge
        /// ceiling and is the server's; this is only how it is written.
        public const string ChargeCap = "/5";

        /// The mark between the two parent tiles - `Splice Chamber
        /// .dc.html:74`'s `<path d="M6 6 l12 12M18 6 L6 18">`, which is a
        /// multiplication cross.
        ///
        /// A CHARACTER AND NOT AN ICON, WHICH THE CAPTURE DECIDED. The first
        /// pass put `icon--splice` in the disc; that sheet's glyph is the
        /// NAV BAR's flask - "the lab" - and at 18px it read as a small
        /// violet blob rather than as "these two cross". `icons.uss` has no
        /// cross, and adding one means a new PNG through
        /// `generate-textures.py` for a mark the text face already draws.
        /// U+00D7 is the same character `LineageLine` puts between the two
        /// species one panel below, so the two say the same thing the same
        /// way - and it is already proven to render in Nunito-Bold SDF,
        /// because that line does.
        public const string JoinMark = "×";

        /// The tag beside an inheritance bar. splice_confirm_spec 2 shows
        /// the odds before the charge; this is the one-word reading of them.
        ///
        /// THE 50% LINE IS THE HANDOFF'S, READ OFF ITS OWN THREE ROWS:
        /// `Splice Chamber.dc.html` tags 78% and 54% `DOM` and 31% `REC`
        /// (lines 148, 160, 172), so the boundary sits between 54 and 31 and
        /// a half is the only round number in that gap. It is a READING of a
        /// published probability and not a re-derivation of one - nothing
        /// here computes dominance, which is `splice/distribution.ts`'s and
        /// stays there.
        public static string InheritanceTag(double odds01)
        {
            return odds01 > 0.5 ? "DOM" : "REC";
        }

        /// Which parent brings a forecast trait, by NAME MATCH against the
        /// two creatures the screen already holds.
        ///
        /// NOT A RE-DERIVATION, AND THE DISTINCTION IS THE ONE
        /// `SpliceConfirmTests` IS BUILT ON. The server decides the odds; a
        /// trait's OWNER is not a probability at all - `combatPool`
        /// (`services/api/src/splice/distribution.ts`) is literally the two
        /// parents' four combat slots, so every trait in `combat2` came from
        /// one of the two creatures in this model. Asking which is a lookup.
        ///
        /// A IS CHECKED FIRST AND THAT IS ARBITRARY WHERE BOTH CARRY IT.
        /// Both parents holding the same trait is exactly the case
        /// `coverageLost` is about, and either answer names a real owner - so
        /// the tie is broken by order rather than by inventing a rule.
        ///
        /// NULL WHEN NEITHER HAS IT, which a mutation would be. The bar then
        /// takes the neutral modifier, which is what the handoff's own third
        /// row draws (`:164`, `#b3adc2`).
        public static string TraitOwner(string trait, CreatureDto parentA, CreatureDto parentB)
        {
            if (string.IsNullOrEmpty(trait)) return null;
            if (Carries(parentA, trait)) return parentA.Species;
            if (Carries(parentB, trait)) return parentB.Species;
            return null;
        }

        private static bool Carries(CreatureDto creature, string trait)
        {
            if (creature == null) return false;
            return string.Equals(creature.Trait1, trait, StringComparison.OrdinalIgnoreCase)
                || string.Equals(creature.Trait2, trait, StringComparison.OrdinalIgnoreCase);
        }

        /// bible 1.2's six species as the COLOUR-FAMILY names `ProgressBar`
        /// and `InheritanceBar` take - `teal`, `coral`, `amber`, `violet`,
        /// `green`, `mute`.
        ///
        /// HERE RATHER THAN IN A COMPONENT BECAUSE THIS IS THE ONLY SCREEN
        /// THAT NEEDS THE TRANSLATION. `TraitChip`, `HeroSlot` and
        /// `LineageStrip` all take a SPECIES and map it in their own
        /// stylesheets (`.trait-chip--vetch` and so on), which is the right
        /// arrangement and is untouched. `InheritanceBar` is the one
        /// component in the vocabulary that takes a colour family instead,
        /// because its dot, fill and tag are one colour decision the caller
        /// makes once - so somebody has to spell bible 1.2's colour column in
        /// C#, and it is the screen that composes the bar. If a second screen
        /// needs this, it lifts out; one caller is not a shared table yet.
        ///
        /// PALE IS `mute` AND NOT A SEVENTH FAMILY. bible 1.2 gives Pale
        /// `#c6cede` (`--slate`), and `InheritanceBar.uss` has no slate rule
        /// - its neutral is `--mute` over `--hairline`, which is the same
        /// quiet end of the palette `HeroSlot.uss` puts Pale's disc on. A
        /// slate bar is a palette ruling, not a screen-local repaint.
        ///
        /// AN UNKNOWN SPECIES IS `mute`, NOT VIOLET. `InheritanceBar` would
        /// default an unknown modifier to violet, which is Hollow's colour -
        /// so "we do not know whose trait this is" would render as "this is a
        /// Hollow's". Neutral is the honest answer and it is also what the
        /// handoff draws for its own unowned row.
        public static string ColourFamily(string species)
        {
            switch ((species ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "vetch":   return "teal";
                case "ember":   return "coral";
                case "skitter": return "amber";
                case "hollow":  return "violet";
                case "loam":    return "green";
                default:        return "mute";
            }
        }

        /// The generation the child would be.
        ///
        /// THE SERVER'S OWN ARITHMETIC, QUOTED: `splice/commit.ts:292` is
        /// `Math.max(a.generation, b.generation) + 1`. This is the one number
        /// on the predicted panel the client can state without the server
        /// having said it, because the rule is a published constant rather
        /// than a distribution - and the panel that states it is a PREDICTION
        /// by name.
        ///
        /// THE CEILING IS NOT APPLIED HERE. `commit.ts:298` refuses a splice
        /// that would exceed the Splicing Chamber's generation ceiling, and
        /// that refusal is the server's to make with its own tier in hand.
        /// Capping the number locally would show a player a generation the
        /// commit will not produce, which is worse than showing the one the
        /// rule gives.
        public static int PredictedGeneration(CreatureDto parentA, CreatureDto parentB)
        {
            var a = parentA == null ? 0 : parentA.Generation;
            var b = parentB == null ? 0 : parentB.Generation;
            return (a > b ? a : b) + 1;
        }

        /// "Vetch × Skitter lineage" - `Splice Chamber.dc.html:118`.
        ///
        /// THE SPECIES AND NOT THE DISPLAY NAME, which is the opposite call
        /// from `DestructionNoticeFor`. That sentence is about two INDIVIDUAL
        /// creatures being destroyed and the name is what stops a player; a
        /// lineage is about which STOCKS meet, and "Ash × Skitter lineage"
        /// would name one creature and one species in the same breath.
        public static string LineageLine(CreatureDto parentA, CreatureDto parentB)
        {
            if (parentA == null || parentB == null) return string.Empty;
            return parentA.Species + " × " + parentB.Species + " lineage";
        }

        /// The lineage card's right-hand note - `Splice Chamber.dc.html:186`'s
        /// "7 generations", which is the depth the child would reach.
        public static string LineageNote(int generations)
        {
            return generations.ToString(CultureInfo.InvariantCulture)
                + (generations == 1 ? " generation" : " generations");
        }

        /// The chain the lineage strip draws: the founder root, the two
        /// parents, and the child.
        ///
        /// THIS SCREEN DECIDES THE SEQUENCE BECAUSE NOBODY ELSE CAN.
        /// `LineageStrip` states its own contract - "IT TAKES THE CHAIN, IT
        /// DOES NOT WALK ONE ... picking the path through it that a player
        /// thinks of as 'the line' belongs to whoever has the tree" - and the
        /// Splice Chamber has NO TREE. `SpliceScreenModel` holds two parents
        /// and a preview; `GET /v1/lineage` is not called until after the
        /// commit (`FtueDirector.SpliceAsync`), by design, because the flag
        /// the reveal needs comes from the same read.
        ///
        /// SO WHAT IS DRAWN IS WHAT IS KNOWN, AND EVERY STOP IS A FACT:
        ///   G1        every line starts at a Founder - generation 1 is the
        ///             root by definition of the field, not an assumption.
        ///             Dropped when a parent IS a G1, so the rail never draws
        ///             the same generation twice in a row.
        ///   parents   their own generations, oldest first, each tinted by
        ///             its own species.
        ///   child     `PredictedGeneration`, marked `current` because it is
        ///             what the screen is about.
        ///
        /// WHAT IT IS NOT is the handoff's G1-G3-G4-G6-G7, which has an
        /// intermediate generation nobody on this screen has. That gap is a
        /// missing lineage read, not a missing rule, and it is named in the
        /// task report rather than filled with a number.
        ///
        /// NEVER EMPTY while both parents exist, so the strip's own collapse
        /// is reachable only from a caller that has neither.
        public static IReadOnlyList<(int gen, string species, bool current)> LineageChain(
            CreatureDto parentA, CreatureDto parentB, CreatureDto bodyParent)
        {
            var chain = new List<(int gen, string species, bool current)>(4);
            if (parentA == null || parentB == null) return chain;

            var older = parentA.Generation <= parentB.Generation ? parentA : parentB;
            var younger = ReferenceEquals(older, parentA) ? parentB : parentA;

            // The founder root, and only when it is not already one of the
            // two stops below it.
            if (older.Generation > 1) chain.Add((1, null, false));

            chain.Add((older.Generation, older.Species, false));
            if (younger.Generation != older.Generation)
            {
                chain.Add((younger.Generation, younger.Species, false));
            }

            var body = bodyParent ?? parentA;
            chain.Add((PredictedGeneration(parentA, parentB), body.Species, true));
            return chain;
        }

        /// The header pill's charge count, or null when the client does not
        /// know it.
        ///
        /// -1 IS "UNKNOWN" AND MUST NOT RENDER AS A NUMBER.
        /// `SpliceScreenModel.ChargesRemaining` is -1 until a commit has
        /// answered, because "the cached balance is a label, not a number the
        /// client does arithmetic on" (client_architecture 7) - so the
        /// preview-built model this screen is always shown from has no
        /// balance at all. `ScreenScaffold.SetResourcePill` removes the pill
        /// outright for a null or empty value, which is the honest rendering
        /// of "we have not been told"; printing "-1/5" beside a charge glyph
        /// is not.
        public static string ChargesLabel(int chargesRemaining)
        {
            return chargesRemaining < 0
                ? null : chargesRemaining.ToString(CultureInfo.InvariantCulture);
        }

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
