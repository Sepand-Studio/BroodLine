using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The deployment screen bound to `DeployScreenModel`
    /// (`client/Assets/UI/DeployScreen.cs`).
    ///
    /// `DeployScreen.Floor` and `.Cap` (mirrored from
    /// services/api/src/wave/issuance.ts) are the model's rule, not this
    /// view's. This view reads `CanDeploy`/`Blocker` only - it never counts
    /// `m.Slots` to decide whether Start should be enabled, because that
    /// would be a second copy of a rule the model already owns and could
    /// disagree with it (a duplicate id or a committed creature makes
    /// `CanDeploy` false at a count the floor/cap alone would allow).
    ///
    /// ===================================================================
    /// PHASE 9 TASK 17 BROUGHT IT TO `Wave Defense.dc.html` IN
    /// `phase: 'placing'`, AND THE SLOT LIST BECAME A ROSTER LIST.
    ///
    /// Phase 8 Task 10's note defended a row over a card because the old
    /// grid "withheld the one fact the screen is about" - the pocket, which
    /// is protocol-load-bearing ("Index == deployment order", and
    /// `deploymentMatches` compares the replay's deployment against the
    /// stored one IN ORDER). That still holds and this layout states the
    /// pocket twice: on the lane picture, where the creature is standing in
    /// it, and on its field row's badge.
    ///
    /// WHAT CHANGED IS WHAT THE LIST HOLDS. An `OptionRow` per DEPLOYED
    /// creature is a list you can only read; the handoff's "On the field"
    /// card is a list you act on. So the rows are now one `FieldSlotRow` per
    /// ROSTER creature, deployed ones first, and a tap toggles. The trait
    /// pips Task 10 gave up are still given up, for the reason it gave: the
    /// splice chamber is where a player reads traits and this screen is
    /// where they read ORDER.
    ///
    /// THE BADGE IS THE POCKET, AND THE LIST IS ORDERED SO THAT IT CAN BE.
    /// `DeployScreen.PocketTag(i)` letters the deployed rows A, B, C... in
    /// pocket order, which is why they are listed first: row i IS pocket i
    /// for every deployed creature, so the same letter means the same pocket
    /// on the row and on the lane card 200px above it. The alternative the
    /// task text asked for - letters by roster order - puts an "A" on the
    /// first row of the roster and a different "A" on the first pocket of
    /// the lane, which is one glyph meaning two things on one screen.
    /// An undeployed creature has no pocket, so its badge carries
    /// `DeployScreen.EmptyPocketTag` ("+"), which is the handoff's own mark
    /// for an unoccupied slot (`Wave Defense.dc.html:98-113` draws a "+" on
    /// every empty tile).
    ///
    /// `FieldSlotRow.Selected` IS NOT USED AND THAT IS DELIBERATE. Its class
    /// comment draws the distinction: "Filled is whether a creature stands in
    /// the pocket; selected is whether the player is currently pointing at
    /// it." This list has no picking mode - a tap commits immediately and the
    /// caller re-binds - so there is never a row the player is pointing at
    /// and has not yet acted on. Setting it would give the screen two
    /// treatments for one fact.
    ///
    /// A ROW AT THE CAP IS DIMMED AND INERT, on `SpliceChamberView`'s
    /// `locked-out` precedent ("visibly out of reach, not hidden", bible
    /// 3.3's rule read from the other side). `DeployScreen.Cap` is the
    /// server's, the model refuses a sixth, and a "+" that silently did
    /// nothing would read as a broken control rather than as a full lane.
    ///
    /// WHAT THIS SCREEN DOES NOT TAKE FROM THE HANDOFF, because both belong
    /// to the FIGHT rather than to placement: the speed pill in the header
    /// (`:41`) and the wave-progress bar above the CTA (`:263`). Broodline
    /// splits the handoff's one screen into two - `DeployView` places,
    /// `WaveHudView` fights - and a 1x/2x control over a lane that is not
    /// running, or a progress bar at 0%, would be chrome that cannot mean
    /// anything yet.
    [UxmlElement]
    public partial class DeployView : VisualElement
    {
        public const string UssClassName = "deploy-view";

        /// The field list's rows, by the handle a caller reaches one with -
        /// each row is also named with its creature's id, the same way
        /// `RosterView` and `SpliceChamberView` name a creature's element.
        public const string RowUssClassName = "deploy-view__row";

        /// A row for a creature that cannot be added because the deployment
        /// is already at `DeployScreen.Cap`.
        public const string RowFullUssClassName = "deploy-view__row--full";

        readonly ScreenScaffold _scaffold;
        readonly Label _waveNumber;
        readonly LanePreviewCard _lane;
        readonly Label _energyValue;
        readonly Label _integrityValue;
        readonly Label _incomingHeading;
        readonly Label _brief;
        readonly VisualElement _stats;
        readonly VisualElement _fieldRows;
        readonly Label _blocker;
        readonly Button _start;

        Action _onStart;

        public DeployView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("DeployView");
            tree.CloneTree(this);

            _stats = this.Q<VisualElement>("stats");
            _fieldRows = this.Q<VisualElement>("field-rows");
            _blocker = this.Q<Label>("blocker");
            _start = this.Q<Button>("start");

            // THE FRAME, COMPOSED AND NOT INHERITED - ScreenScaffold's class
            // comment has the reason, and ScaffoldTests' sweep is what
            // depends on it.
            //
            // pushed: true. The handoff has no deploy screen of its own - it
            // folds placement into "Wave Defense" as `phase: 'placing'` - and
            // its push table reaches Wave Defense from the Gene Ark, so the
            // screen Broodline split out of it inherits that. The CHEVRON
            // follows `Bind`'s `onBack`, not this flag; see below.
            //
            // THE EYEBROW IS THE HANDOFF'S KICKER and is passed at
            // construction because it never changes - `Wave Defense.dc.html
            // :39` names the region and what is being done in it, not this
            // wave. The NUMBER beside the title is the part that changes, and
            // it arrives at `Bind`.
            _scaffold = new ScreenScaffold(
                DeployScreen.Title, pushed: true, eyebrow: DeployScreen.Eyebrow);

            // THE TITLE IS TWO LABELS ON ONE LINE, AND THE SECOND ONE IS WHY.
            // `Wave Defense.dc.html:40` draws "Wave {{ wave }}" as one 20px
            // Baloo line. This project's standing rule is that a numeral
            // never renders in the display face and that every
            // decision-bearing number carries `t-num` (bible 10.6), so the
            // word and the number cannot be one Label: "Wave" is Baloo
            // through `.t-screen-title` and the wave id is Nunito's tabular
            // face through `.t-num`.
            //
            // THE SCAFFOLD'S OWN LABEL IS RE-PARENTED RATHER THAN REPLACED.
            // `.screen-scaffold__titles` is a COLUMN (the eyebrow sits above
            // the title), so a `.t-num` sibling added beside the title would
            // stack under it. Moving the title into a row this screen owns is
            // the only arrangement that keeps BOTH the scaffold's label - and
            // therefore `Q<Label>("title")`, which four test files read - and
            // the handoff's one line. `ScreenScaffold`'s own invariant
            // (`ScaffoldTests.TheTitleLivesInsideTheTitlesColumn...`) is
            // asserted on a bare scaffold and is untouched; this is a screen
            // arranging what it composed, which is the direction a screen is
            // allowed to reach.
            //
            // AND THE TITLE MUST NOT BE EMPTY. `ScreenScaffold` hides `#header`
            // entirely on a null or empty title, taking the back control, the
            // eyebrow and the resource pill with it - so `DeployScreen.Title`
            // being one word rather than none is load-bearing.
            _waveNumber = new Label { name = "wave-number" };
            _waveNumber.AddToClassList("deploy-view__wave-number");
            _waveNumber.AddToClassList("t-num");

            var titles = _scaffold.Q<VisualElement>("titles");
            var title = _scaffold.Q<Label>("title");
            var titleLine = new VisualElement { name = "title-line" };
            titleLine.AddToClassList("deploy-view__title-line");
            titles.Add(titleLine);
            title.RemoveFromHierarchy();
            titleLine.Add(title);
            titleLine.Add(_waveNumber);

            // --------------------------------------------------- the pills

            _energyValue = PillValue();
            _integrityValue = PillValue();

            var pills = this.Q<VisualElement>("pills");
            pills.Add(Pill("energy", "icon--charge", "pill-energy",
                DeployScreen.EnergyLabel, _energyValue));

            // A SPACER, BECAUSE NEITHER MARGIN NOR PADDING IS AVAILABLE HERE.
            // The handoff's `gap: 7px` (`:51`) has to land on something, USS
            // on 6000.6.0f1 has no `gap`, and a `SectionCard` IS an `.elev-1`
            // wrapper - `margin-left`/`margin-right` are already -6px there
            // (the negative margin verify-uss-tokens.sh check 4 pins against
            // `--elev-1-spread`) and `padding` is exactly where the
            // nine-sliced shadow is drawn (`SectionCard.uss`: "A second
            // padding declaration on the same element ... would either
            // swallow the shadow or silently move the card edge off the slice
            // scale the texture was cut for"). Writing either one on a pill
            // breaks something measured. An element between them costs one
            // node and breaks nothing, which is the trade
            // `SpliceChamberView`'s 40px joiner already makes for the same
            // 8px gap on the same kind of row.
            var gap = new VisualElement { name = "pill-gap" };
            gap.AddToClassList("deploy-view__pill-gap");
            gap.pickingMode = PickingMode.Ignore;
            pills.Add(gap);

            pills.Add(Pill("integrity", "icon--ark", "pill-integrity",
                DeployScreen.IntegrityLabel, _integrityValue));

            // ---------------------------------------------------- the lane

            // THE PICTURE IS A RENDER TEXTURE AND THIS SCREEN KNOWS NOTHING
            // ABOUT WHAT IS IN IT - `LanePreviewCard`'s class comment has the
            // split in full. `LaneStage` (Broodline.Game) owns the camera,
            // the dressing and the assembled creatures; `Bind` is handed the
            // finished `Texture`, and a null one is the ordinary state in
            // every test and every capture, where no stage runs at all.
            //
            // NAMED "lane-card" AND NOT "lane", WHICH IS A COLLISION THIS
            // SCREEN HAD FOR ONE TEST RUN. `LanePreviewCard`'s own UXML
            // already names its picture element "lane", so a card called
            // "lane" puts two elements under that name in one screen and
            // `Q("lane")` means whichever the traversal reaches first.
            // `HeroBand.uss` records the same rule about `body`, which
            // `SectionCard` and `HeroSlot` both own: a name that answers
            // twice is a name no test can rely on.
            _lane = new LanePreviewCard { name = "lane-card" };
            _lane.AddToClassList("deploy-view__lane");

            // ------------------------------------------- the incoming wave

            _incomingHeading = new Label { name = "incoming-heading" };
            _incomingHeading.AddToClassList("t-micro");

            var tag = new Label(DeployScreen.StandardTag) { name = "wave-tag" };
            tag.AddToClassList("deploy-view__wave-tag");
            tag.AddToClassList("t-micro");

            // NO TYPE CLASS ON THE SENTENCE, AND THAT IS NOT AN OMISSION.
            // `Wave Defense.dc.html:219` sets it 12px in #5d5670, and
            // Theme.uss's bare `Label` rule is --text-body (12px) in --ink -
            // the size to the pixel. --ink (#3f3a52) is 88 in summed channel
            // distance from the handoff's ink and --mute (#a29bb5) is 207, so
            // --ink is the near one and adding a class would only restate
            // what the element already has.
            _brief = new Label { name = "brief" };
            _brief.AddToClassList("deploy-view__brief");

            var incoming = new SectionCard { name = "incoming" };
            incoming.AddToClassList("deploy-view__incoming");
            incoming.Body.Add(CardHeader("incoming-header", _incomingHeading, tag));
            incoming.Body.Add(_brief);
            incoming.Body.Add(_stats);

            // --------------------------------------------- on the field

            var hint = new Label(DeployScreen.FieldHint) { name = "field-hint" };
            hint.AddToClassList("deploy-view__field-hint");
            hint.AddToClassList("t-secondary");

            var fieldHeading = new Label(DeployScreen.FieldHeading) { name = "field-heading" };
            fieldHeading.AddToClassList("t-micro");

            // BUILT WITH NO `SectionCard` HEADING, for the reason
            // `SpliceChamberView.CardHeader` records: the component's heading
            // is one 19px Baloo line and the handoff draws a TWO-SIDED ROW -
            // a 9px uppercase eyebrow on the left and a muted note on the
            // right. One Label cannot be two things.
            var field = new SectionCard { name = "field" };
            field.AddToClassList("deploy-view__field");
            field.Body.Add(CardHeader("field-header", fieldHeading, hint));
            field.Body.Add(_fieldRows);

            // THE HANDOFF'S OWN ORDER, top to bottom: the two readouts, the
            // lane, the incoming wave, the field. The blocker and the CTA are
            // this project's and keep Phase 8 Task 10's placement.
            //
            // THE BLOCKER IS THE LAST THING IN CONTENT, directly above the
            // CTA row, because it is the sentence explaining why the button
            // under it is off.
            _scaffold.Content.Add(pills);
            _scaffold.Content.Add(_lane);
            _scaffold.Content.Add(incoming);
            _scaffold.Content.Add(field);
            _scaffold.Content.Add(_blocker);
            _scaffold.CtaRow.Add(_start);
            Add(_scaffold);

            // Registered once, in the constructor, against a field the next
            // Bind can overwrite - so re-binding this same instance (a
            // recycled screen, and this screen is re-bound on every toggle)
            // cannot stack a second `onStart` behind the first the way
            // `clicked += onStart` inside Bind would.
            _start.clicked += () => _onStart?.Invoke();
        }

        /// `onBack` is what the chevron does, and null means there is no
        /// chevron - `RosterView.Bind` has the full reasoning, which is the
        /// same here: `Broodline.UI` cannot name `ScreenHost.Pop`, and today
        /// `FtueDirector` reaches this screen through `ScreenFlow.ShowAsync`
        /// (`ScreenHost.Show`, back stack cleared) rather than a push, so
        /// there is nowhere for a chevron to go back to.
        ///
        /// `lane` IS THE STAGE'S TEXTURE and null clears the card to its own
        /// fill. `roster` is every creature the player owns, which is what
        /// the field list is drawn from; `onToggle` is a tap on one of those
        /// rows, reported by creature id. `facts` is what this screen states
        /// about the WAVE - see `DeployWaveFacts`, and note that a null one
        /// leaves the four readouts blank rather than zero.
        ///
        /// RE-BINDING IS THE NORMAL CASE HERE, NOT THE EXCEPTION. The
        /// director re-binds this same instance on every toggle without
        /// ending the turn, so everything below is a full rewrite of the
        /// screen's state and nothing accumulates: the rows are rebuilt, the
        /// stat cells are rebuilt, and the two handlers land on fields rather
        /// than on a second subscription.
        public void Bind(
            DeployScreenModel m,
            Action onStart,
            Action onBack = null,
            Texture lane = null,
            IReadOnlyList<CreatureDto> roster = null,
            Action<Guid> onToggle = null,
            DeployWaveFacts facts = null)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (facts == null) facts = new DeployWaveFacts();

            _scaffold.OnBack = onBack;

            _waveNumber.text = DeployScreen.HeaderWave(m.WaveId);
            _energyValue.text = DeployScreen.EnergyValue(facts.Energy);
            _integrityValue.text = DeployScreen.IntegrityFull;

            _lane.SetTexture(lane);

            // ONE TAG PER DEPLOYABLE POCKET, WHICH IS `DeployScreen.Cap` AND
            // NOT THE LANE'S POCKET COUNT. `Broodline.UI` has no engine
            // reference and cannot ask a `Lane` how many pockets it has; what
            // it does know is that `DeployScreen.Build` assigns `Pocket = i`
            // over a selection capped at 5, so pockets 0..Cap-1 are exactly
            // the ones this screen can fill. Both authored lanes carry at
            // least that many (`Lane.Defile()` five, `Lane.DefileSix()` six),
            // so a tag here always names a pocket that exists.
            var tags = new List<(string label, bool filled)>(DeployScreen.Cap);
            for (var pocket = 0; pocket < DeployScreen.Cap; pocket++)
            {
                tags.Add((DeployScreen.PocketTag(pocket), pocket < m.Slots.Count));
            }
            _lane.SetSlots(tags);

            _incomingHeading.text = DeployScreen.IncomingHeading(m.WaveId);
            _brief.text = string.IsNullOrEmpty(facts.Brief) ? DeployScreen.DefaultBrief : facts.Brief;

            // Both numbers in the middle cell are the model's. The count is
            // `Slots.Count` read back, not a tally this view keeps; the cap is
            // `DeployScreen.Cap` written by `DeployedStatValue`, which is the
            // only place the two are put in one string.
            _stats.Clear();
            _stats.Add(new StatCell(
                DeployScreen.FoesStatLabel, DeployScreen.FoesStatValue(facts.Foes)));
            _stats.Add(new StatCell(
                DeployScreen.DeployedStatLabel, DeployScreen.DeployedStatValue(m.Slots.Count)));
            _stats.Add(new StatCell(
                DeployScreen.RewardStatLabel,
                DeployScreen.RewardValue(facts.RewardCurrency, facts.RewardAmount)));

            BindFieldRows(m, roster, onToggle);

            // The model's own sentence, verbatim - empty when `CanDeploy`.
            Blocker(m.Blocker);

            // The model's own label too - DeployScreen.Cta, not a literal
            // this view authors itself.
            _start.text = m.CtaLabel ?? string.Empty;
            _start.SetEnabled(m.CanDeploy);
            _onStart = onStart;
        }

        /// The "On the field" list: the deployed creatures first, in pocket
        /// order, then everything else the player owns.
        ///
        /// `roster` NULL FALLS BACK TO THE DEPLOYMENT ITSELF, which is what
        /// every call written before this task passes and what
        /// `ScreenBindingTests` still binds with. A list of the deployed
        /// creatures is the honest reading of "the field" for a caller that
        /// has not said what else exists - and it keeps those rows lettered
        /// by their pockets, which is the fact the list is for. The empty
        /// state then means what it says: there is nothing here to deploy.
        void BindFieldRows(DeployScreenModel m, IReadOnlyList<CreatureDto> roster, Action<Guid> onToggle)
        {
            _fieldRows.Clear();

            var deployed = new Dictionary<Guid, int>();
            foreach (var slot in m.Slots) deployed[slot.Creature.CreatureId] = slot.Pocket;

            var ordered = new List<CreatureDto>();
            foreach (var slot in m.Slots) ordered.Add(slot.Creature);
            if (roster != null)
            {
                foreach (var creature in roster)
                {
                    if (creature == null || deployed.ContainsKey(creature.CreatureId)) continue;
                    ordered.Add(creature);
                }
            }

            if (ordered.Count == 0)
            {
                // Says the list is empty; the blocker below says what to do
                // about it. DeployScreen.EmptyMessage's comment has why those
                // are two sentences and not one.
                _fieldRows.Add(new EmptyState(DeployScreen.EmptyMessage, "ark"));
                return;
            }

            var full = m.Slots.Count >= DeployScreen.Cap;
            VisualElement pair = null;
            for (var i = 0; i < ordered.Count; i++)
            {
                var creature = ordered[i];
                var isDeployed = deployed.TryGetValue(creature.CreatureId, out var pocket);

                // CAPTURED INTO A LOCAL, not read off `creature` inside the
                // lambda: the row outlives this iteration and the id is the
                // whole of what the callback reports.
                var id = creature.CreatureId;
                Action onTap = onToggle == null ? null : () => onToggle(id);

                var row = new FieldSlotRow(
                    isDeployed ? DeployScreen.PocketTag(pocket) : DeployScreen.EmptyPocketTag,
                    CreatureLabel.WithGeneration(creature),
                    filled: isDeployed,
                    onTap: onTap)
                {
                    // The same handle `RosterView` and `SpliceChamberView`
                    // give a creature's element, so a caller reaching one row
                    // does not have to count children.
                    name = creature.CreatureId.ToString(),
                };
                row.AddToClassList(RowUssClassName);

                // VISIBLY OUT OF REACH RATHER THAN SILENTLY INERT. At the cap
                // the model refuses a sixth creature, so a tap on an
                // undeployed row cannot do anything - and a control that
                // answers a tap with nothing reads as broken. Dimmed AND
                // disabled: the class says so and `SetEnabled` stops the
                // event, because either one alone is a state the other half
                // can drift out of.
                if (full && !isDeployed)
                {
                    row.AddToClassList(RowFullUssClassName);
                    row.SetEnabled(false);
                }

                // TWO TO A ROW, which is the handoff's own `grid-template-
                // columns: 1fr 1fr` for this list (`:246`). USS here has no
                // grid, so a pair is a row of two and an odd last row leaves
                // its right half empty rather than stretching the survivor
                // across the card.
                if (i % 2 == 0)
                {
                    pair = new VisualElement();
                    pair.AddToClassList("deploy-view__field-pair");
                    _fieldRows.Add(pair);
                    row.AddToClassList("deploy-view__field-cell--left");
                }
                pair.Add(row);
            }
        }

        /// One of the two readouts across the top - a tinted disc holding a
        /// glyph, then the label over the value.
        ///
        /// A `SectionCard` RATHER THAN A BARE ELEMENT, because the handoff
        /// draws it as a white card with a drop shadow (`:52` - `background:
        /// #ffffff; border-radius: 14px; box-shadow: 0 1px 3px`) and
        /// `SectionCard` is this project's white-card-with-a-shadow. It is
        /// built with NO heading: the label inside is a 9px `.lbl`, not the
        /// component's 19px Baloo line.
        static SectionCard Pill(string name, string icon, string discName, string label, Label value)
        {
            var card = new SectionCard { name = name };
            card.AddToClassList("deploy-view__pill");

            var glyph = new VisualElement { name = discName + "-glyph" };
            glyph.AddToClassList("icon");
            glyph.AddToClassList("deploy-view__pill-glyph");
            glyph.AddToClassList(icon);
            glyph.pickingMode = PickingMode.Ignore;

            // THE DISC'S TINT IS THE MODIFIER'S, NOT THIS METHOD'S. The
            // handoff gives the two readouts different tints (`#e2f4e8`
            // behind the bolt, `#fbeae7` behind the hexagon) and a colour
            // passed as an argument here would be a colour written in C#,
            // which is what the token layer exists to stop. The element
            // carries `deploy-view__pill-disc` plus its own name-derived
            // modifier and `DeployView.uss` decides both.
            var disc = new VisualElement { name = discName };
            disc.AddToClassList("deploy-view__pill-disc");
            disc.AddToClassList("deploy-view__pill-disc--" + name);
            disc.Add(glyph);

            var caption = new Label(label) { name = name + "-label" };
            caption.AddToClassList("deploy-view__pill-label");
            caption.AddToClassList("t-micro");

            var column = new VisualElement();
            column.AddToClassList("deploy-view__pill-text");
            column.Add(caption);
            column.Add(value);

            card.Body.Add(disc);
            card.Body.Add(column);
            return card;
        }

        static Label PillValue()
        {
            var value = new Label();
            value.AddToClassList("deploy-view__pill-value");
            value.AddToClassList("t-num");
            return value;
        }

        /// The handoff's two-sided card header - an uppercase eyebrow on the
        /// left and a muted note on the right.
        ///
        /// `align-items: center` RATHER THAN THE HANDOFF'S `baseline`, which
        /// UI Toolkit does not have at all - it is one of the four
        /// declarations this project records as silently dropped, beside
        /// `text-transform`, `calc()` and `box-shadow`. At 10px against 11
        /// the difference is under a pixel. `SpliceChamberView.CardHeader` is
        /// the same row on the same reasoning; this is the third screen in
        /// the phase to want it, and the fourth should make it a component.
        static VisualElement CardHeader(string name, VisualElement heading, VisualElement trailing)
        {
            var row = new VisualElement { name = name };
            row.AddToClassList("deploy-view__card-header");
            row.Add(heading);
            if (trailing != null) row.Add(trailing);
            return row;
        }

        /// The refusal banner, shown only when it says something.
        ///
        /// THE DISPLAY TOGGLE IS NOT TIDYING, and this screen is where it was
        /// most visible: `.deploy-view__blocker` carries a --coral-tint fill
        /// and --space-3 of padding, so a LEGAL deployment - the ordinary
        /// case, the one a player sees every wave - drew a blank coral strip
        /// between the roster grid and the Start button. It is in every
        /// capture of this screen before Phase 8 Task 10.
        /// `FounderNamingView.Blocker` is the same rule on the same kind of
        /// row, and `ScreenScaffold.FooterNote` is where it started. The TEXT
        /// still round-trips to string.Empty, which is what
        /// `ScreenBindingTests` reads.
        void Blocker(string message)
        {
            _blocker.text = message ?? string.Empty;
            _blocker.style.display = string.IsNullOrEmpty(message)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
