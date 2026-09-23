using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The Splice Chamber bound to `SpliceScreenModel`
    /// (`client/Assets/UI/SpliceScreen.cs`). Every sentence on this view -
    /// the destruction notice, the CTA, the coverage warning, the mutation
    /// line - is the model's own; this view rewrites none of it.
    ///
    /// `onSplice` fires once, when the CTA is tapped. Showing
    /// `m.Dialogs` (the standard confirmation and any Founder interrupts,
    /// in order) before actually committing is the caller's job, not this
    /// view's - `Bind`'s own signature carries a single callback and no
    /// per-dialog confirm/cancel handles, so this view cannot orchestrate a
    /// sequence it has no hooks into. `ConfirmDialog` (Phase 8 Task 14)
    /// already renders that sequence element-by-element; wiring it in front
    /// of this callback belongs to whatever screen composes the two.
    ///
    /// ===================================================================
    /// PHASE 9 TASK 16 BROUGHT IT TO `Splice Chamber.dc.html`, AND THE
    /// PARENTS STOPPED BEING `CreatureCard`s IN THE PROCESS.
    ///
    /// Phase 8's class comment defended the cards on three grounds and the
    /// first two still hold - there are no pickers on this screen, and the
    /// trait marks say what is about to be destroyed. What changed is the
    /// SHAPE the handoff draws them in. `Splice Chamber.dc.html:50-70` is
    /// not a roster tile: it is a white card at radius 18 opening with a
    /// `PARENT A` eyebrow and a generation badge on one line, a 74px ringed
    /// creature centred under it, then the name at 17px, a muted role line,
    /// and the traits as chips. A `CreatureCard` is a fixed 160px tile whose
    /// picture is a 74px plate and whose header is a name - a different
    /// object. The two tiles are `SectionCard`s composed from Task 13's
    /// vocabulary now, and the third of Phase 8's reasons (three tests read
    /// them as `CreatureCard`s) was the weakest: those tests assert that a
    /// tile is NAMED after its creature and carries `locked-out`, both of
    /// which are this view's doing and neither of which was ever
    /// `CreatureCard`'s.
    ///
    /// THE FORECAST TABLE IS `InheritanceBar`s. Phase 8 put each outcome in
    /// a `StatCell` to get `t-num` onto the percentage; the handoff draws a
    /// species dot, the trait, the odds, a filled bar and a DOM/REC tag
    /// (`:139-173`), which is the component Task 13 built for this exact
    /// card. The percentage keeps its numeral marker - `InheritanceBar`'s
    /// own UXML carries it - and gains the bar, which is what makes three
    /// odds comparable at a glance rather than three numbers in three boxes.
    ///
    /// THE CTA LABEL IS STILL THE MODEL'S AND STILL NOT "Begin Splice".
    /// This task's brief specified `SpliceScreen.BeginLabel = "Begin
    /// Splice"` for the button, which is the handoff's own label at
    /// `Splice Chamber.dc.html:225` - and `broodline_splice_confirm_spec
    /// .md:83` exists to replace exactly that string: "CTA label changes
    /// from 'Begin Splice' to 'Splice - consumes both parents.' The cost
    /// belongs on the button. A player who taps past everything else still
    /// reads the thing they tap." Section 1 of the same spec names the
    /// handoff's row as the gap it was written to close, so the handoff is
    /// not being overruled here - it is being superseded by the companion
    /// spec bible 2.7 points at, and `SpliceConfirmTests
    /// .Cta_IsNotTheLabelTheSpecExistsToReplace` is the guard that would
    /// have caught the revert one layer down. The COST ROW is still adopted:
    /// `CostCtaRow` puts the charge price beside the button, which is the
    /// handoff's contribution and says a different thing from the sentence
    /// on it. No `BeginLabel` constant was added, because a retired string
    /// in the source is a string a later edit can reach for.
    /// ===================================================================
    ///
    /// THE DESTRUCTION NOTICE IS THE LAST THING IN THE CONTENT REGION, and
    /// that is the spec rather than a layout preference. splice_confirm_spec
    /// section 3: it "sits directly above the CTA. Not a tooltip, not a
    /// footnote."
    [UxmlElement]
    public partial class SpliceChamberView : VisualElement
    {
        public const string UssClassName = "splice-chamber-view";

        /// On each `InheritanceBar` in the forecast card. KEPT FROM PHASE 8
        /// although the row is no longer a `StatCell`: it is a public const
        /// on this view, `ScreenBindingTests` counts outcomes by it, and the
        /// count - one row per published outcome - is the assertion that
        /// matters either way. A view rendering a single static row would
        /// pass a "not empty" check and fail that one.
        public const string ForecastRowUssClassName = "forecast-row";

        public const string LockedOutUssClassName = "locked-out";

        /// The parent tiles, the joiner between them, and the predicted band,
        /// as classes rather than as literals in this file's stylesheet - so
        /// the coupling between the C# that builds them and the USS that
        /// sizes them is visible from both ends. `SectionCard
        /// .ElevationUssClassName`'s convention.
        public const string ParentUssClassName = "splice-chamber-view__parent";
        public const string JoinUssClassName = "splice-chamber-view__join";
        public const string PredictedUssClassName = "splice-chamber-view__predicted";

        readonly ScreenScaffold _scaffold;
        readonly VisualElement _parents;
        readonly VisualElement _forecast;
        readonly Label _destructionNotice;
        readonly Label _coverageWarning;

        readonly HeroBand _predicted;
        readonly VisualElement _predictedGen;
        readonly VisualElement _predictedSlot;
        readonly Label _predictedName;
        readonly Label _predictedLineage;

        readonly MutationBanner _mutation;
        readonly LineageStrip _lineage;
        readonly Label _lineageNote;

        /// The cost row, rebuilt on every `Bind` - `CostCtaRow` takes its
        /// label and its handler in the constructor and exposes only
        /// `Enabled`, so there is nothing on it to re-point. Held so the old
        /// one can be removed by reference rather than by clearing the
        /// container it shares with the two required sentences.
        CostCtaRow _cta;

        public SpliceChamberView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("SpliceChamberView");
            tree.CloneTree(this);

            _parents = this.Q<VisualElement>("parents");
            _forecast = this.Q<VisualElement>("forecast");
            _destructionNotice = this.Q<Label>("destruction-notice");
            _coverageWarning = this.Q<Label>("coverage-warning");

            // THE FRAME, COMPOSED AND NOT INHERITED - ScreenScaffold's class
            // comment has the reason, and ScaffoldTests' sweep is what
            // depends on it.
            //
            // pushed: false. The handoff's navigation model points the
            // `Splice` tab straight at this screen, so it is a hub with a
            // bottom nav and no back chevron - and it is the screen its push
            // table reaches Creature Roster and Splice Reveal FROM. The
            // handoff DOES draw a back tile here (`:34`), which is the tab
            // bar's own inconsistency rather than ours; nothing in this
            // client can pop to a screen it never pushed from.
            _scaffold = new ScreenScaffold(SpliceScreen.Title, eyebrow: SpliceScreen.Eyebrow);

            // THE PREDICTED HYBRID IS A `HeroBand`, NOT A `SectionCard` WITH
            // A RAMP ON IT, and the measurement is in the task report in
            // full. The short version: `Splice Chamber.dc.html:101` is one of
            // the six instances `HeroBand` was counted from and matches it on
            // every property the component already owns - the same
            // `linear-gradient(170deg, #ffffff, ...)`, the same `0 4px 16px
            // rgba(63,58,82,.08)` drop that is `.elev-2` exactly, and a
            // gradient end (#f4f0fb) 8 in summed channel distance from
            // band-ramp.png's own --violet-tint. The alternative this task's
            // brief named - `SectionCard` plus Theme's `.panel-hybrid` - is
            // `.elev-1` against the handoff's `.elev-2` and ramps to
            // --hybrid-tint-deep, 25 away. Two properties closer on one and
            // exact on the other.
            //
            // `ring: false`, WHICH IS `HeroBand`'s OWN READING OF THIS
            // SCREEN. Its class comment: "Splice Chamber's band is a padded
            // column whose rings are 74px and 112px ornaments on the
            // creatures inside it, not a ring on the band." The 112px ring at
            // `:109` is `HeroSlot`'s, and it is drawn below.
            _predicted = new HeroBand(ring: false);
            _predicted.AddToClassList(PredictedUssClassName);

            _predictedGen = new VisualElement { name = "predicted-gen" };
            _predictedGen.AddToClassList("splice-chamber-view__gen");
            _predicted.Subject.Add(CardHeader(
                "predicted-heading", SpliceScreen.PredictedHeading, _predictedGen));

            _predictedSlot = new VisualElement { name = "predicted-slot" };
            _predictedSlot.AddToClassList("splice-chamber-view__predicted-slot");

            _predictedName = new Label { name = "predicted-name" };
            _predictedName.AddToClassList("splice-chamber-view__predicted-name");
            // `.t-section` (19px Baloo) RATHER THAN `.t-hero` (28), AND THE
            // CAPTURE DECIDED IT. `Splice Chamber.dc.html:117` sets this name
            // at 22px; at 28 "Unnamed Hybrid" wrapped to two lines inside a
            // 185px column beside the 112px creature, which is the one thing
            // a hero name must not do. 19 is the nearest step on the scale
            // below the handoff's 22 and fits on one line with room; 26
            // (--text-card-hero) is nearer the number and still wraps.
            _predictedName.AddToClassList("t-section");

            _predictedLineage = new Label { name = "predicted-lineage" };
            _predictedLineage.AddToClassList("splice-chamber-view__predicted-lineage");
            _predictedLineage.AddToClassList("t-secondary");

            var predictedText = new VisualElement { name = "predicted-text" };
            predictedText.AddToClassList("splice-chamber-view__predicted-text");
            predictedText.Add(_predictedName);
            predictedText.Add(_predictedLineage);

            var predictedBody = new VisualElement { name = "predicted-body" };
            predictedBody.AddToClassList("splice-chamber-view__predicted-body");
            predictedBody.Add(_predictedSlot);
            predictedBody.Add(predictedText);
            _predicted.Subject.Add(predictedBody);

            // THE CARD HEADINGS ARE EYEBROWS, NOT `SectionCard` HEADINGS, AND
            // `FounderNamingView` SET THIS PRECEDENT. `SectionCard(heading)`
            // draws one 19px Baloo line on `.t-section`; the handoff draws a
            // TWO-SIDED ROW - a 9px uppercase `.lbl` on the left and a muted
            // 10px note on the right ("Trait inheritance / odds" at `:135`,
            // "Lineage / 7 generations" at `:185`). One Label cannot be two
            // things, and a 19px display line over a 12px trait row is the
            // type collision Task 14b spent a round undoing on the founder
            // screen. So the cards are built with no heading and carry their
            // own header row, exactly as `FounderNamingView` carries its own
            // kicker and `card-title`.
            var inheritance = new SectionCard();
            inheritance.Body.Add(CardHeader(
                "inheritance-heading", SpliceScreen.InheritanceHeading,
                Note("inheritance-note", SpliceScreen.OddsNote)));
            inheritance.Body.Add(_forecast);

            _mutation = new MutationBanner { name = "mutation" };
            inheritance.Body.Add(_mutation);

            _lineageNote = Note("lineage-note", null);
            var lineage = new SectionCard();
            lineage.Body.Add(CardHeader(
                "lineage-heading", SpliceScreen.LineageHeading, _lineageNote));
            _lineage = new LineageStrip { name = "lineage" };
            lineage.Body.Add(_lineage);

            // THE HANDOFF'S OWN ORDER, top to bottom: the pair, the
            // prediction, the odds, the pedigree. The two sentences after
            // them are this project's, from splice_confirm_spec.
            _scaffold.Content.Add(_parents);
            _scaffold.Content.Add(_predicted);
            _scaffold.Content.Add(inheritance);
            _scaffold.Content.Add(lineage);

            // THE TWO REQUIRED SENTENCES ARE IN THE CTA ROW, NOT AT THE END
            // OF THE CONTENT REGION, AND THAT IS splice_confirm_spec 3 BEING
            // TAKEN LITERALLY RATHER THAN APPROXIMATELY.
            //
            // The spec says the destruction notice "sits directly above the
            // CTA. Not a tooltip, not a footnote." Through Phase 8 that was
            // read as "last in the scrolling column", which is directly above
            // the CTA only while the column is shorter than the viewport.
            // MEASURED ON THIS TASK'S FIRST CAPTURE: with the handoff's four
            // surfaces in it the column runs ~990px against a ~770px content
            // region, so the destruction notice was 120px BELOW THE FOLD on
            // the one screen in the app that destroys two creatures. A player
            // who does not scroll taps a CTA whose cost they were never
            // shown, which is the exact gap section 1 says this spec exists
            // to close.
            //
            // `ScreenScaffold.CtaRow` is outside the ScrollView and is a
            // column, so a child added before the cost row is pinned
            // immediately above the button at every scroll position. The
            // coverage warning goes with it for the same reason -
            // sample_economy 7 makes it a screen requirement, and it is
            // usually empty and collapsed, so it costs nothing when there is
            // nothing to say.
            _scaffold.CtaRow.Add(_coverageWarning);
            _scaffold.CtaRow.Add(_destructionNotice);
            Add(_scaffold);
        }

        /// `lockedOut` names parents this screen must refuse to submit again
        /// - splice_confirm_spec 6's "the named Founder is visibly locked
        /// out". `Broodline.UI` does not decide who belongs in that set; it
        /// only renders the one it is handed.
        ///
        /// `predicted` is the live turntable from `Broodline.Game`'s portrait
        /// studio, and null is a real answer rather than a missing argument -
        /// `FounderNamingView.Bind`'s own note has the full account of why
        /// the parameter is optional and trailing, and it applies here
        /// verbatim: `FtueDirector` may hold no studio, the EditMode suite
        /// has no camera, and `ScreenFixtures` captures this screen in batch
        /// mode. All three want the same screen with a baked creature in it.
        public void Bind(SpliceScreenModel m, ISet<Guid> lockedOut, Action onSplice,
            Texture predicted = null)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            lockedOut = lockedOut ?? new HashSet<Guid>();

            // THE CHARGE PILL, OR NO PILL AT ALL. `ChargesLabel` returns null
            // for the -1 that means "the server has not said", which is the
            // state every preview-built model is in; `SetResourcePill`
            // removes the pill outright for a null value. See that method for
            // why "-1/5" beside a charge glyph is the thing to avoid.
            _scaffold.SetResourcePill(
                "icon--charge", SpliceScreen.ChargesLabel(m.ChargesRemaining),
                SpliceScreen.ChargeCap);

            _parents.Clear();
            _parents.Add(ParentTile(m.ParentA, SpliceScreen.ParentAEyebrow, lockedOut));
            _parents.Add(Join());
            _parents.Add(ParentTile(m.ParentB, SpliceScreen.ParentBEyebrow, lockedOut));

            BindPredicted(m, predicted);

            _destructionNotice.text = m.DestructionNotice ?? string.Empty;

            // SpliceScreen.MutationLine is the model's own sentence for the
            // mutation/Aberrant pair (section 2: "stated as two numbers").
            //
            // AND IT COLLAPSES, for the same reason the coverage warning
            // below does: `MutationLine` returns empty for a null forecast -
            // a state `Bind` itself treats as reachable two blocks down,
            // where it renders `ForecastEmptyMessage` - and the banner is an
            // amber gradient strip with an inset ring and a sparkle in it, so
            // an empty one is decoration under a card saying the server named
            // no outcomes. The collapse is `MutationBanner.Text`'s own
            // contract rather than something this screen arranges, which is
            // the point of the component: `Text = null` IS how "the server
            // named none" is drawn.
            _mutation.Text = SpliceScreen.MutationLine(m.Forecast);

            // THE COVERAGE WARNING COLLAPSES WHEN THE SERVER NAMED NOTHING.
            // `CoverageWarningFor` returns empty for an empty `coverageLost`,
            // which is the ordinary case - and the amber panel it sits in has
            // a fill and 12px of padding, so an empty one drew a blank amber
            // strip under the forecast on a screen where nothing is being
            // lost. It is in every capture of this screen before Phase 8
            // Task 10. Same rule as `DeployView.Blocker`,
            // `FounderNamingView.Blocker` and `ScreenScaffold.FooterNote`;
            // the TEXT still round-trips to string.Empty, which is what
            // `ScreenBindingTests` reads.
            _coverageWarning.text = m.CoverageWarning ?? string.Empty;
            _coverageWarning.style.display = string.IsNullOrEmpty(m.CoverageWarning)
                ? DisplayStyle.None : DisplayStyle.Flex;

            BindForecast(m);

            // THE LINEAGE STRIP DRAWS WHAT THIS SCREEN KNOWS, WHICH IS FOUR
            // STOPS AND NOT THE HANDOFF'S FIVE. `SpliceScreen.LineageChain`
            // has the full account: the chamber holds two parents and a
            // preview, never a tree, so the intermediate generation the
            // handoff draws at `:196` has no source. The NOTE the handoff
            // pairs with the strip - "Unbroken Vetch line since G1 - pedigree
            // bonus +12% trait fidelity" - has no source either and is left
            // null, which collapses its row; the strip's right-hand note is
            // the generation count, which is real.
            var chain = SpliceScreen.LineageChain(m.ParentA, m.ParentB, m.ParentA);
            _lineage.Bind(chain, null);
            var depth = SpliceScreen.PredictedGeneration(m.ParentA, m.ParentB);
            _lineageNote.text = SpliceScreen.LineageNote(depth);

            // THE COST ROW IS REBUILT RATHER THAN RE-BOUND, and that is
            // `CostCtaRow`'s shape rather than a preference: it takes its
            // label and its handler in the constructor and exposes only
            // `Enabled`, so there is nothing on it to re-point. Removing the
            // previous one first is what keeps a re-bound screen from
            // stacking a second button behind the first - the same guarantee
            // the single-subscription pattern gives elsewhere on this branch,
            // reached the other way.
            //
            // BY REFERENCE AND NOT BY `CtaRow.Clear()`, because the two
            // required sentences live in that container now and clearing it
            // would take them with it on the second bind.
            if (_cta != null) _cta.RemoveFromHierarchy();
            _scaffold.CtaRow.Add(_cta = new CostCtaRow(
                "icon--charge", SpliceScreen.ChargeCost, m.CtaLabel ?? string.Empty,
                () => { if (onSplice != null) onSplice(); }));
        }

        void BindPredicted(SpliceScreenModel m, Texture predicted)
        {
            _predictedGen.Clear();
            _predictedGen.Add(new GenChip(SpliceScreen.PredictedGeneration(m.ParentA, m.ParentB)));

            // THE BODY PARENT IS PARENT A, BECAUSE THAT IS WHAT THE DIRECTOR
            // COMMITS. `SpliceScreen.CommitAsync` refuses a missing
            // `bodyFrom` outright - "the body choice is the player's only
            // controlled lever and has no default" - and `FtueDirector`
            // sends `parentA.Species` because no body picker exists yet. The
            // prediction shows the creature the commit would actually make;
            // when the picker lands, both read the same field.
            var body = m.ParentA;

            // THE TWO HeroSlot FORMS, AND `Q("body")` IS WHAT TELLS THEM
            // APART - `FounderNamingView.Bind` has the full note. A live slot
            // removes its three sprite layers rather than leaving them empty
            // behind the stage, and `Bind` runs on both because the studio
            // draws the animal and not the frame around it.
            _predictedSlot.Clear();
            HeroSlot slot;
            if (predicted == null)
            {
                slot = new HeroSlot();
            }
            else
            {
                var stage = new CreatureStage();
                stage.SetTexture(predicted);
                slot = new HeroSlot(stage);
            }
            slot.name = "predicted-creature";
            slot.Bind(body);
            _predictedSlot.Add(slot);

            _predictedName.text = SpliceScreen.PredictedName;
            _predictedLineage.text = SpliceScreen.LineageLine(m.ParentA, m.ParentB);
        }

        void BindForecast(SpliceScreenModel m)
        {
            _forecast.Clear();
            var outcomes = m.Forecast == null ? null : m.Forecast.Combat2;
            if (outcomes == null || outcomes.Count == 0)
            {
                // A forecast card with an empty table is section 2's one
                // non-negotiable rendering as a screen that failed to load.
                _forecast.Add(new EmptyState(SpliceScreen.ForecastEmptyMessage, "warning"));
                return;
            }

            foreach (var outcome in outcomes)
            {
                // EVERY ONE OF THESE FOUR ARRIVES DECIDED, which is
                // `InheritanceBar`'s stated precondition and
                // `SpliceConfirmTests`' standing rule. The odds are the
                // server's `p`; the tag is a READING of that number at the
                // handoff's own 50% line; the owner is a name match against
                // the two creatures in the model, because `combatPool` IS
                // their four combat slots; and the colour is that owner's
                // family. Nothing here computes a distribution.
                var owner = SpliceScreen.TraitOwner(outcome.Trait, m.ParentA, m.ParentB);
                var bar = new InheritanceBar(
                    CreatureLabel.TraitWithTier(outcome.Trait, outcome.Tier),
                    (float)outcome.P,
                    SpliceScreen.InheritanceTag(outcome.P),
                    SpliceScreen.ColourFamily(owner));
                bar.AddToClassList(ForecastRowUssClassName);
                _forecast.Add(bar);
            }
        }

        /// One parent tile - `Splice Chamber.dc.html:50-70`.
        ///
        /// NAMED AFTER THE CREATURE ID AND CARRYING `locked-out`, both of
        /// which are this view's doing rather than the component's, exactly
        /// as they were when the tile was a `CreatureCard`. The name is the
        /// structural precondition a headless test can prove; the class is
        /// splice_confirm_spec 6's "visibly locked out", asserted from both
        /// directions in `ScreenBindingTests`.
        VisualElement ParentTile(CreatureDto creature, string eyebrow, ISet<Guid> lockedOut)
        {
            var card = new SectionCard { name = creature.CreatureId.ToString() };
            card.AddToClassList(ParentUssClassName);

            var gen = new VisualElement();
            gen.AddToClassList("splice-chamber-view__gen");
            gen.Add(new GenChip(creature.Generation));
            card.Body.Add(CardHeader(null, eyebrow, gen));

            var frame = new VisualElement();
            frame.AddToClassList("splice-chamber-view__parent-slot");
            var slot = new HeroSlot();
            slot.Bind(creature);
            frame.Add(slot);
            card.Body.Add(frame);

            var title = new Label(CreatureLabel.DisplayName(creature)) { name = "parent-name" };
            title.AddToClassList("splice-chamber-view__parent-name");
            // `.t-section`, NOT `.t-card-title`, AND THE HANDOFF OUTRANKS THE
            // BRIEF HERE. This task's brief says `.t-card-title`;
            // `Splice Chamber.dc.html:64` sets the parent's name in
            // `'Baloo 2'` at 17px/700, and `.t-card-title` is Nunito at 13 -
            // wrong on both the face and the size. `.t-section` is Baloo at
            // 19 (Theme.uss binds the display face to exactly four
            // selectors, and that is one of them), so it is 2 over the
            // handoff's number in the handoff's own face rather than 4 under
            // it in the wrong one.
            title.AddToClassList("t-section");
            card.Body.Add(title);

            // The role line - bible 1.2's role word beside the creature's
            // Instinct, which is what `CreatureLabel.RoleLine` builds and
            // what the handoff draws at `:65` ("Armored · Slow"). It
            // collapses when neither half is known rather than leaving a
            // muted gap under the name.
            var role = new Label(CreatureLabel.RoleLine(creature)) { name = "parent-role" };
            role.AddToClassList("splice-chamber-view__parent-role");
            role.AddToClassList("t-secondary");
            role.style.display = string.IsNullOrEmpty(role.text)
                ? DisplayStyle.None : DisplayStyle.Flex;
            card.Body.Add(role);

            var traits = new VisualElement();
            traits.AddToClassList("splice-chamber-view__parent-traits");
            traits.Add(new TraitChip(creature.Trait1, creature.Tier1, creature.Species));
            traits.Add(new TraitChip(creature.Trait2, creature.Tier2, creature.Species));
            card.Body.Add(traits);

            var locked = lockedOut.Contains(creature.CreatureId);
            card.SetEnabled(!locked);
            card.EnableInClassList(LockedOutUssClassName, locked);
            return card;
        }

        /// The mark between the two parents - `Splice Chamber.dc.html:72-76`:
        /// a white disc with a violet splice glyph in it.
        ///
        /// 40px, NOT THE 44 THIS TASK'S BRIEF NAMED. The handoff's own
        /// `width: 40px; height: 40px` is at `:73`, and 44 would be the iOS
        /// touch minimum - which this is not, because nothing here is
        /// tappable: the handoff gives the disc no handler and this screen
        /// has no parent picker to open. A 44px non-target beside two 40px
        /// ones elsewhere in the app would be the odd one out for a reason
        /// that does not apply.
        ///
        /// THE MARK IS A CHARACTER RATHER THAN AN `icon--splice` - see
        /// `SpliceScreen.JoinMark`, which has the capture that decided it.
        /// The size and the colour are this screen's, in its own sheet.
        static VisualElement Join()
        {
            var join = new VisualElement { name = "join" };
            join.AddToClassList(JoinUssClassName);
            join.pickingMode = PickingMode.Ignore;

            var glyph = new Label(SpliceScreen.JoinMark) { name = "join-mark" };
            glyph.AddToClassList("splice-chamber-view__join-glyph");
            glyph.pickingMode = PickingMode.Ignore;
            join.Add(glyph);
            return join;
        }

        /// The handoff's card header: an uppercase eyebrow on the left and
        /// something on the right, on one baseline.
        ///
        /// `align-items: center` RATHER THAN THE HANDOFF'S `baseline`, which
        /// UI Toolkit does not have at all - it is one of the four
        /// declarations this project records as silently dropped, beside
        /// `text-transform`, `calc()` and `box-shadow`. Centred is the
        /// nearest thing that exists, and at 9px against 10px the difference
        /// is under a pixel.
        static VisualElement CardHeader(string name, string eyebrow, VisualElement trailing)
        {
            var row = new VisualElement();
            row.AddToClassList("splice-chamber-view__card-header");

            var label = new Label(eyebrow);
            if (!string.IsNullOrEmpty(name)) label.name = name;
            label.AddToClassList("t-micro");
            row.Add(label);

            if (trailing != null) row.Add(trailing);
            return row;
        }

        /// The muted note at a card header's right edge - the handoff's
        /// `odds` and `7 generations`, both 10px in --mute and neither a
        /// `.lbl`, so neither is uppercased.
        static Label Note(string name, string text)
        {
            var note = new Label(text ?? string.Empty) { name = name };
            note.AddToClassList("splice-chamber-view__note");
            note.AddToClassList("t-secondary");
            return note;
        }
    }
}
