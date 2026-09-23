using System;
using Broodline.Api;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// Beat 7 - `broodline_splice_confirm_spec` section 5: the reveal "keeps
    /// its existing line - that parents are consumed and their traits live on
    /// in the pedigree - but it is now a confirmation of something already
    /// understood rather than a disclosure."
    ///
    /// WHY `mutated` IS A PARAMETER AND NOT READ OFF `committed`. The brief
    /// for Phase 8's task binds a commit response carrying a mutation flag.
    /// **`SpliceCommitResponse` does not carry one, deliberately** -
    /// `services/api/src/routes/splice.ts` rolls `mutated`/`aberrant`,
    /// stores them, and withholds them from the response, because design
    /// section 10 defers Aberrant content: "reporting `mutated: true` for an
    /// event with no effect ... would show the player a mutation that did
    /// not happen."
    ///
    /// `GET /v1/lineage` DOES report it (`LineageNode.mutated`), and the
    /// director calls that route anyway for beat 8, so the flag reaching
    /// this screen still arrives through a server response and is never
    /// inferred on the client. Taking it as a parameter is what keeps that
    /// true while the commit route stays silent - and the day the flag lands
    /// on the commit response, only the caller changes.
    ///
    /// ===================================================================
    /// PHASE 9 TASK 16: THE HERO CARD IS A `HeroBand`, AND THIS IS THE
    /// BAND'S CANONICAL FIXED INSTANCE.
    ///
    /// `Splice Reveal.dc.html:43` is not merely one of the six surfaces
    /// `HeroBand` was counted from - it is the one that matches every
    /// default the component ships with and the one its `Fix` method exists
    /// for. Measured property by property: `linear-gradient(170deg,
    /// #ffffff, #efe9fb)` against band-ramp.png's white-to---violet-tint;
    /// `border-radius: 26px` against `--radius-band`, which is 26 exactly;
    /// `box-shadow: 0 4px 16px rgba(63,58,82,.08)` against `.elev-2`, which
    /// Tokens.uss maps to that pair verbatim; `overflow: hidden`, which the
    /// component sets; `height: 372px`, which is `Fix(372)` and is the
    /// literal example in `HeroBand`'s own class comment; a dashed ring at
    /// `:45` and a filled pool at `:46`, which is `ring: true`. Nothing was
    /// adapted to make it fit. The `.elev-2` the Phase 8 wrapper carried is
    /// the band's own now, because `HeroBand` adds it - so the element this
    /// screen's tests read as the hero card has not lost a class, it has
    /// stopped hand-rolling one.
    ///
    /// AND `reveal-flare` STILL RIDES ON IT. Motion.uss's transition was
    /// written in Phase 8 Task 5 against this exact screen; the band is the
    /// element that appears, so the class goes on the band and `Bind` still
    /// toggles `reveal-flare--hidden` to give the transition two computed
    /// styles to run between.
    /// ===================================================================
    ///
    /// THE PARENTS ROW IS OURS AND NOT THE HANDOFF'S, WHICH IS A DELIBERATE
    /// DIVERGENCE RATHER THAN AN OVERSIGHT. `Splice Reveal.dc.html` shows
    /// the child and its traits and never draws the two creatures it cost;
    /// bible 2.1 makes "the parents are still drawn on the screen that says
    /// they are gone" the FTUE's whole teaching moment, and
    /// `FirstHourScreensTests.SpliceReveal_KeepsBothParentsOnTheScreenThatSaysTheyAreGone`
    /// is the guard. The spec wins over the mock here for the same reason it
    /// wins on the chamber's CTA label: the mock is what the specs were
    /// written to correct.
    [UxmlElement]
    public partial class SpliceRevealView : VisualElement
    {
        public const string UssClassName = "splice-reveal-view";
        public const string MutatedUssClassName = "mutated";
        public const string ParentUssClassName = "splice-reveal-view__parent";

        /// The hero band's own class, NAMED HERE AND APPLIED IN THE
        /// CONSTRUCTOR, because the band is built in C# and not declared in
        /// the UXML any more.
        ///
        /// IT WAS MISSING FOR ONE CAPTURE AND NOTHING SAW IT BUT THE PICTURE.
        /// Four rules in this screen's sheet are scoped to it - the 280px
        /// ring, the 212px pool and the two that size the subject to the
        /// handoff's 186 - and with no class on the element every one of them
        /// was dead: the band rendered at `HeroBand`'s own 264/196/160
        /// defaults, which is a correct band of the wrong screen's
        /// proportions. No test could have caught it (a USS rule reaches an
        /// element only through a live panel's cascade) and both stylesheet
        /// gates were green. Named as a const so the coupling between this
        /// file and that sheet is visible from both ends.
        public const string ChildUssClassName = "splice-reveal-view__child";

        /// Motion.uss's end state for `reveal-flare`. Named here because Bind
        /// is what toggles it, and an unreferenced transition class was how
        /// this animation came to exist without ever running.
        public const string FlareHiddenUssClassName = "reveal-flare--hidden";

        readonly ScreenScaffold _scaffold;
        readonly Label _kicker;
        readonly Label _headline;
        readonly HeroBand _child;
        readonly VisualElement _childSlot;
        readonly VisualElement _pill;
        readonly Label _pillText;
        readonly Label _childName;
        readonly Label _childLine;
        readonly VisualElement _traits;
        readonly VisualElement _parents;
        readonly Label _consumption;
        readonly Button _next;

        Action _onNext;

        public SpliceRevealView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("SpliceRevealView");
            tree.CloneTree(this);

            _kicker = this.Q<Label>("kicker");
            _headline = this.Q<Label>("headline");
            _parents = this.Q<VisualElement>("parents");
            _consumption = this.Q<Label>("consumption");
            _next = this.Q<Button>("next");

            // THE FRAME, COMPOSED AND NOT INHERITED - ScreenScaffold's class
            // comment has the reason, and ScaffoldTests' sweep is what
            // depends on it.
            //
            // NO PAGE HEADER, WHICH IS THE HANDOFF AND IS A DELIBERATE
            // DECISION RATHER THAN AN OMISSION - Phase 9 Task 16b.
            // `Splice Reveal.dc.html:38-41` is a centred kicker over a 26px
            // headline with NOTHING above it, exactly `Onboarding.dc.html`'s
            // shape, which is the one other screen in the bundle built that
            // way and is why `ScreenScaffold` has a headerless mode at all.
            // Ours drew a 21px left-aligned "Splice Reveal" over a centred
            // 28px headline: the same two-container type ladder Task 14b
            // spent a round undoing on the founder screen.
            //
            // AND IT IS FOUR CHANGES, NOT THE ONE TASK 16's REPORT PRICED.
            // `#header` also holds the EYEBROW (ScreenScaffold.cs's own note
            // on the headerless branch lists all four things inside that
            // row), so `title: null` on its own would have silently deleted
            // `SPLICE COMPLETE · CHARGE SPENT` - the one thing `:39` DOES
            // draw up there. The kicker is therefore RELOCATED into the
            // content column as a centred label above the headline, which is
            // exactly where the handoff draws it; `SpliceRevealView` joins
            // `ScaffoldTests`' `headerless` list on purpose; and that
            // sweep's count moves 9 -> 8. `SpliceRevealScreen.Title` stays as
            // the screen's NAME in the vocabulary and is now rendered by
            // nothing; its own note there says so, and says not to pass it
            // back into the scaffold.
            //
            // `pushed: true` IS KEPT AND IS NOW INERT, SAID PLAINLY. The
            // chevron lives in the row this hides, so nothing can draw it;
            // the push relationship is still real (the handoff's push table
            // reaches this screen from the chamber) and `Bind` still sets
            // `OnBack`. No caller passes one - `FtueDirector` reaches this
            // screen through `ScreenFlow.ShowAsync` and going back would
            // mean a chamber whose two parents no longer exist - so nothing
            // visible is lost. A back affordance here would have to go in
            // `Content`, the way the kicker just did.
            _scaffold = new ScreenScaffold(title: null, pushed: true);

            // THE KICKER AND THE HEADLINE ARE TWO DIFFERENT SENTENCES, which
            // is why the kicker survives the header rather than being folded
            // into the headline: `:39` states the cost in the past tense and
            // never changes, `:40` says which of two things just happened.
            _kicker.text = SpliceRevealScreen.Eyebrow;

            _child = new HeroBand { name = "child" };
            _child.AddToClassList(ChildUssClassName);
            _child.AddToClassList("reveal-flare");
            _child.Fix(BandHeight);

            _childSlot = new VisualElement { name = "child-slot" };
            _childSlot.AddToClassList("splice-reveal-view__child-slot");

            _pillText = new Label { name = "pill-text" };
            _pillText.AddToClassList("splice-reveal-view__pill-text");
            _pillText.AddToClassList("t-micro");
            _pillText.AddToClassList("t-warn");

            var spark = new VisualElement();
            spark.AddToClassList("icon");
            spark.AddToClassList("icon--sparkle");
            spark.AddToClassList("splice-reveal-view__pill-spark");
            spark.pickingMode = PickingMode.Ignore;

            _pill = new VisualElement { name = "mutation-pill" };
            _pill.AddToClassList("splice-reveal-view__pill");
            _pill.AddToClassList("panel-amber");
            _pill.Add(spark);
            _pill.Add(_pillText);

            _childName = new Label { name = "child-name" };
            _childName.AddToClassList("splice-reveal-view__child-name");
            _childName.AddToClassList("t-hero");

            _childLine = new Label { name = "child-line" };
            _childLine.AddToClassList("splice-reveal-view__child-line");
            _childLine.AddToClassList("t-secondary");

            _child.Subject.Add(_childSlot);
            _child.Subject.Add(_pill);
            _child.Subject.Add(_childName);
            _child.Subject.Add(_childLine);

            // THE TRAITS CARD, WITH THE HANDOFF'S TWO-SIDED HEADER. Same
            // construction and same reason as the chamber's two cards:
            // `SectionCard(heading)` draws one 19px Baloo line and the
            // handoff draws an uppercase eyebrow beside a muted note
            // (`:72-75`). `FounderNamingView` set the precedent of a card
            // carrying its own header inside `Body`.
            var card = new SectionCard();
            card.Body.Add(CardHeader(
                SpliceRevealScreen.TraitsHeading, SpliceRevealScreen.TraitsNote));

            _traits = new VisualElement { name = "traits" };
            _traits.AddToClassList("splice-reveal-view__traits");
            card.Body.Add(_traits);

            // THE CONSUMPTION LINE IS THE CARD'S NOTE ROW NOW - `:95-100`'s
            // tinted strip under the trait rows - rather than a bare Label
            // above the CTA. It is still the LAST sentence before the button,
            // because the card is the last thing in the column, so
            // splice_confirm_spec 3's placement rule survives the move; what
            // it gains is the panel the handoff draws around it and the
            // amber/violet switch that says whether a mutation happened.
            card.Body.Add(_consumption);

            _scaffold.Content.Add(_kicker);
            _scaffold.Content.Add(_headline);
            _scaffold.Content.Add(_child);
            _scaffold.Content.Add(card);
            _scaffold.Content.Add(_parents);
            _scaffold.CtaRow.Add(_next);
            Add(_scaffold);

            _next.clicked += () => { if (_onNext != null) _onNext(); };
        }

        /// `Splice Reveal.dc.html:43`'s own `height: 372px`.
        ///
        /// THIS SCREEN'S NUMBER AND NOT `HeroBand`'s, on the rule
        /// `FounderNamingView.BandFloor` states: the other band in the bundle
        /// that pins a size pins 300, and a component that guessed between
        /// them would be wrong on one screen in two. `Fix` rather than
        /// `Fill`, because this one is a fixed height and not a floor - the
        /// two are mutually exclusive by construction in that component.
        const float BandHeight = 372f;

        /// `onBack` is what the chevron does, and null means there is no
        /// chevron - `RosterView.Bind` has the full reasoning. Here it is
        /// also the weakest case for one: `FtueDirector` reaches this screen
        /// through `ScreenFlow.ShowAsync`, and going BACK from a reveal would
        /// be going back to a chamber whose two parents no longer exist.
        ///
        /// `portrait` is the live turntable from `Broodline.Game`'s portrait
        /// studio, and null is a real answer rather than a missing argument -
        /// `FounderNamingView.Bind` has the full account of why it is
        /// optional and trailing.
        public void Bind(SpliceCommitResponse committed, CreatureDto parentA, CreatureDto parentB,
            bool mutated, Action next, Action onBack = null, Texture portrait = null)
        {
            if (committed == null) throw new ArgumentNullException(nameof(committed));

            _scaffold.OnBack = onBack;

            _headline.text = SpliceRevealScreen.Headline(mutated);
            EnableInClassList(MutatedUssClassName, mutated);

            var child = committed.Child;

            // THE TWO HeroSlot FORMS - `FounderNamingView.Bind` has the note.
            // A live slot removes its three sprite layers rather than leaving
            // them empty behind the stage, and `Bind` runs on both because
            // the studio draws the animal and not the frame around it.
            //
            // A CHILDLESS RESPONSE LEAVES AN EMPTY BAND RATHER THAN A WRONG
            // ONE. `SpliceCommitResponse.Child` is required on the wire, so
            // this is the defensive arm rather than a state the server
            // produces; `ChildLine` already returns empty for it and the name
            // falls back to nothing.
            _childSlot.Clear();
            if (child != null)
            {
                HeroSlot slot;
                if (portrait == null)
                {
                    slot = new HeroSlot();
                }
                else
                {
                    var stage = new CreatureStage();
                    stage.SetTexture(portrait);
                    slot = new HeroSlot(stage);
                }
                slot.name = child.CreatureId.ToString();
                slot.Bind(child);
                _childSlot.Add(slot);
            }

            _childName.text = CreatureLabel.DisplayName(child);
            _childLine.text = SpliceRevealScreen.ChildLine(child);

            // THE PILL STATES THE RARE OUTCOME AND NAMES IT WHERE IT CAN.
            // `MutatedTrait` is a set difference over the child and its two
            // parents - a mutation is defined as a trait in neither, which is
            // what the reveal's own note says - and it returns null for a
            // reachable state, in which case the pill still says the event
            // happened. HIDDEN rather than removed when there was no
            // mutation, because the band re-binds and a removed pill has no
            // index to come back at between the slot and the name.
            var pill = SpliceRevealScreen.MutationPill(
                SpliceRevealScreen.MutatedTrait(child, parentA, parentB));
            _pillText.text = mutated ? pill : string.Empty;
            _pill.style.display = mutated ? DisplayStyle.Flex : DisplayStyle.None;

            BindTraits(child, parentA, parentB);

            // BOTH PARENTS ARE STILL DRAWN, on the screen that says they are
            // gone. splice_confirm_spec section 5 makes the reveal a
            // confirmation of a fact the player already read, and a screen
            // that quietly removed them would be teaching the opposite of
            // what the tree shows one tap later.
            //
            // AND A ROW THAT WAS HANDED NEITHER SAYS SO. `AddParent` skips a
            // null, so two nulls used to render as nothing at all - the same
            // screen, minus the lesson, with no sentence saying whether the
            // parents were consumed or simply never arrived.
            // EITHER PARENT MISSING IS THE SAME CASE AS BOTH, and the test
            // used to be `childCount == 0`, which only caught both. A splice
            // consumes two creatures, so one card on its own does not render
            // half the truth - it renders a different and wrong one, a child
            // with a single parent. Worse, `Consumption` returns empty as
            // soon as EITHER is null, so the one-parent case fell through
            // this guard AND that sentence: a lone card, no explanation,
            // on the screen bible 2.1 makes the first hour's teaching moment.
            _parents.Clear();
            if (parentA == null || parentB == null)
            {
                _parents.Add(new EmptyState(SpliceRevealScreen.NoParentsMessage, "warning"));
            }
            else
            {
                AddParent(parentA);
                AddParent(parentB);
            }

            // AND THE SENTENCE COLLAPSES WHEN THERE IS NONE, rather than
            // leaving a blank panel in the card above the CTA.
            //
            // THE PANEL SWITCHES WITH THE OUTCOME - `:133`'s `noteBg` is
            // `#fdf6e6` for a mutation and `#f1ecfa` otherwise, which are
            // --amber-tint and --violet-tint. The reveal's own note calls the
            // mutation "a distinct amber treatment", and this is the second
            // place in the app that spends it on good news rather than on a
            // warning; `MutationBanner`'s class comment has that argument in
            // full.
            var consumption = SpliceRevealScreen.Consumption(parentA, parentB);
            _consumption.text = consumption;
            _consumption.style.display = string.IsNullOrEmpty(consumption)
                ? DisplayStyle.None : DisplayStyle.Flex;
            _consumption.EnableInClassList("panel-amber", mutated);
            _consumption.EnableInClassList("panel-violet", !mutated);

            _next.text = SpliceRevealScreen.NextLabel;
            _onNext = next;

            // AND THE FLARE ACTUALLY RUNS. `reveal-flare` was applied to the
            // hero card and pinned by a test, but nothing ever added or
            // removed its `--hidden` pair - so there was no state change for
            // the transition to run against and the payoff of the whole first
            // hour snapped in with no motion at all. A USS transition needs
            // two computed styles: set the end state the element starts from,
            // then clear it once layout has resolved, which is the next frame.
            _child.AddToClassList(FlareHiddenUssClassName);
            _child.schedule.Execute(() => _child.RemoveFromClassList(FlareHiddenUssClassName));
        }

        /// The inherited-traits rows - `Splice Reveal.dc.html:77-93`.
        ///
        /// TWO ROWS WHERE THE HANDOFF DRAWS THREE, AND THE DIFFERENCE IS THE
        /// DATA MODEL RATHER THAN THE LAYOUT. data_model 2 gives a creature
        /// exactly two combat slots; the handoff's third row is a third
        /// combat trait ("Ashveil aura") that no `CreatureDto` can hold. The
        /// FACT that row carries - that one of the traits came from neither
        /// parent - is on screen either way, because each row states its own
        /// source and a mutated slot reads `Mutation`.
        void BindTraits(CreatureDto child, CreatureDto parentA, CreatureDto parentB)
        {
            _traits.Clear();
            if (child == null) return;

            _traits.Add(TraitRow(child.Trait1, child.Tier1, parentA, parentB));

            // THE HAIRLINE IS THEME'S `.divider` AND IT IS AN ELEMENT,
            // because USS on 6000.6.0f1 has no structural pseudo-classes -
            // there is no `:first-child` and no `:not()`, so "a top border on
            // every row but the first" cannot be expressed as a selector.
            // `.divider` is `height: 1px; background: var(--divider);
            // margin: 8px 0`, which is `Splice Reveal.dc.html:82` property
            // for property.
            var rule = new VisualElement();
            rule.AddToClassList("divider");
            rule.pickingMode = PickingMode.Ignore;
            _traits.Add(rule);

            _traits.Add(TraitRow(child.Trait2, child.Tier2, parentA, parentB));
        }

        /// One row: the trait as a chip, and which parent brought it.
        ///
        /// THE CHIP IS TINTED BY THE OWNER AND NOT BY THE CHILD'S SPECIES,
        /// which is the whole point of a species-tinted chip on this screen.
        /// `SpliceScreen.TraitOwner` is a name match against the two parents
        /// - the same lookup the chamber's inheritance bars make - so a
        /// Vetch trait on an Ember-bodied child still reads teal, exactly as
        /// the handoff draws it (`:78` dots the Carapace row `#6ba7c0` on a
        /// screen whose child is a Cinderplate).
        static VisualElement TraitRow(string trait, int? tier, CreatureDto parentA, CreatureDto parentB)
        {
            var row = new VisualElement { name = trait };
            row.AddToClassList("splice-reveal-view__trait-row");

            var owner = SpliceRevealScreen.SourceOf(trait, parentA, parentB);
            row.Add(new TraitChip(trait, tier, owner == null ? null : owner.Species));

            var source = new Label(SpliceRevealScreen.SourceLabel(trait, parentA, parentB))
            {
                // NAMED, because the row holds two Labels - this one and the
                // chip's own - and `Q<Label>()` on the row would answer with
                // whichever the tree happens to reach first.
                name = "source",
            };
            source.AddToClassList("splice-reveal-view__trait-source");
            source.AddToClassList("t-micro");
            row.Add(source);
            return row;
        }

        /// The handoff's two-sided card header - see
        /// `SpliceChamberView.CardHeader`, which this mirrors and for the
        /// same reason.
        static VisualElement CardHeader(string eyebrow, string note)
        {
            var row = new VisualElement();
            row.AddToClassList("splice-reveal-view__card-header");

            var label = new Label(eyebrow) { name = "traits-heading" };
            label.AddToClassList("t-micro");
            row.Add(label);

            var trailing = new Label(note) { name = "traits-note" };
            trailing.AddToClassList("splice-reveal-view__note");
            trailing.AddToClassList("t-secondary");
            row.Add(trailing);
            return row;
        }

        void AddParent(CreatureDto parent)
        {
            if (parent == null) return;
            var card = new CreatureCard { name = parent.CreatureId.ToString() };
            card.Bind(parent, null);
            card.AddToClassList(ParentUssClassName);
            _parents.Add(card);
        }
    }
}
