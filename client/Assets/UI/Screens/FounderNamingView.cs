using System;
using Broodline.Api;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// Beat 4 - bible 3.3's Founder naming, "one creature only ... the
    /// emotional anchor, placed before any complexity."
    ///
    /// TWO ANSWERS, AND BOTH ARE REAL. The skip path is not a dismissal: it
    /// leaves the creature unnamed server-side, and the Roster carries the
    /// rename ("all five are renameable at any time"). What this screen must
    /// not do is submit a blank name - `CreatureNameRequest.name` is
    /// required, so an empty confirm is a round trip that can only be
    /// refused. `Confirm` refuses it here instead and says which button the
    /// player wanted.
    ///
    /// Every sentence is `FounderNamingScreen`'s, including the default -
    /// see that class's `DefaultFor` for why the default lives on the model
    /// rather than here.
    ///
    /// PHASE 9 TASK 14 BROUGHT IT TO `Onboarding.dc.html` STEP 1 AND TASK 14b
    /// CORRECTED THE COMPOSITION. The handoff's step screen is four bands and
    /// no page header: a full-width progress row, a hero that takes the rest
    /// of the column, one white card carrying kicker / title / body / note,
    /// and the CTAs. Task 14 put the kicker and title in the scaffold's
    /// HEADER and the body in the card, which left a 21px page title and a
    /// 19px card line competing across two containers; they are one type
    /// ladder in one surface again.
    ///
    /// THIS IS THE ONLY SCREEN IN THE PROJECT WITH NO PAGE HEADER, and the
    /// handoff is the whole reason - every other screen in the bundle has one
    /// and titles it at 20-21px, which is what `--text-screen-title` already
    /// is. The 26px here is a CARD heading (`--text-card-hero`), not a bigger
    /// page title.
    ///
    /// This is the first screen in the project to compose Task 13's
    /// vocabulary, so the arrangement here is the one the four screens after
    /// it follow.
    [UxmlElement]
    public partial class FounderNamingView : VisualElement
    {
        public const string UssClassName = "founder-naming-view";

        /// NO FLOOR, AND THE HANDOFF'S 300 IS WHY THERE ISN'T ONE - PHASE 9
        /// TASK 21e. `HeroBand.Fill`'s own note already sanctions this shape:
        /// "a band that wants to grow with no floor passes 0 and says so."
        ///
        /// THIS WAS `Onboarding.dc.html:43`'s `min-height: 300px` AND IT
        /// CLIPPED THE CARD ON A PHONE. The exit gate's walk of the packaged
        /// app, on an iPhone 17 at 402x874, found this screen's white card cut
        /// off flat under the name field with no corners and no tip note. The
        /// numbers, measured with `probe-frame.sh` rather than reasoned about:
        /// the shell gives a screen 402x704 at that device (874 less a 62pt
        /// top inset, a 34pt bottom inset, the 16pt top bar and the 58pt tab
        /// bar), the scaffold's scroll viewport is 582 of that, and this
        /// column wanted 656 - a progress row of 37, a band pinned at 300, a
        /// card of 275 and 44 of elevation wrappers.
        ///
        /// A FLOOR ON THE COLUMN'S ONLY GROWING CHILD IS EITHER INERT OR
        /// HARMFUL, AND IT CANNOT BE ANYTHING ELSE. The band is the one
        /// element in this column carrying `flex-grow`
        /// (`ScreenScaffold.uss`'s `min-height: 100%` note says so of the
        /// whole project), so its height is `max(floor, natural + all the
        /// slack)`. Where there IS slack the band is already past 300 and the
        /// floor changes nothing - measured at 398 on a 402x874 frame and 456
        /// at the corpus's 430x932. Where there is NOT, the floor is the only
        /// reason the column overflows. There is no third case. Removing it
        /// moves no pixel of the committed corpus and gives the card its
        /// bottom back at 402.
        ///
        /// THE BAND STILL DOES NOT COLLAPSE, WHICH IS WHAT THE FLOOR WAS FOR.
        /// It grows into whatever is left; at 402 that lands it near 226. Its
        /// ring is a fixed 264 square and would be sawn off by
        /// `.hero-band__surface`'s `overflow: hidden` at that height, so
        /// `HeroBand.OrnamentScale` fits the whole ornament to the band -
        /// added in this task, and its note has the measurement.
        ///
        /// THE BRIEF'S HYPOTHESIS WAS THAT THE CARD WAS BEING SQUEEZED. It is
        /// not, and the distinction is worth the sentence because it is what
        /// says this fix is the right one: a ScrollView's content container is
        /// sized BY its content, so nothing in this column ever shrinks - the
        /// card measured 275 at every frame from 874 down to 740. What happens
        /// is that the container grows past the viewport and the tail goes
        /// below the fold. It scrolls, and a swipe on the device brings the
        /// note into view. What made it read as broken is that the cut lands
        /// mid-card, under a pinned CTA that says "this is the end".
        const float BandFloor = 0f;

        readonly Label _prompt;
        readonly VisualElement _founder;
        readonly TextField _name;
        readonly Label _blocker;
        readonly Button _confirm;
        readonly Button _skip;

        Action<string> _onName;
        Action _onSkip;

        public FounderNamingView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("FounderNamingView");
            tree.CloneTree(this);

            _prompt = this.Q<Label>("prompt");
            _founder = this.Q<VisualElement>("founder");
            _name = this.Q<TextField>("name");
            _blocker = this.Q<Label>("blocker");
            _confirm = this.Q<Button>("confirm");
            _skip = this.Q<Button>("skip");

            var progress = this.Q<VisualElement>("progress");
            this.Q<Label>("step").text = FounderNamingScreen.Step;
            this.Q<Label>("note-text").text = FounderNamingScreen.Note;

            // THE EYEBROW AND THE TITLE ARE THE CARD'S, NOT THE HEADER'S,
            // AND THAT IS THIS SCREEN'S WHOLE COMPOSITION.
            //
            // UPPERCASE ALREADY, AND NOTHING HERE MAKES IT SO. UI Toolkit has
            // no `text-transform`, so the handoff's
            // `.lbl { text-transform: uppercase }` has no property to land in
            // and the casing is baked into the constant. Read, never
            // repeated - `FounderNamingScreen.Eyebrow` has the full note.
            var kicker = this.Q<Label>("kicker");
            kicker.text = FounderNamingScreen.Eyebrow;
            // `card-title`, NOT `heading`: `SectionCard` already owns an element
            // named `heading` (it removes it when the card is built
            // without one, which is how both cards here are built), and a
            // second element answering that name inside the same card is a
            // `Q<Label>("heading")` that means whichever one a future edit
            // happens to create first.
            var title = this.Q<Label>("card-title");
            title.text = FounderNamingScreen.Title;

            // THE FRAME, COMPOSED AND NOT INHERITED. ScreenScaffold's class
            // comment has the reason in full: `ScaffoldTests`' sweep asks
            // each screen for a DESCENDANT carrying `screen-scaffold`, and
            // UQuery never matches the element it is called on - so a screen
            // that derived from the scaffold would read as bare. Composing
            // also leaves this type's own `[UxmlElement] partial class` and
            // `UssClassName` where every existing test reads them.
            //
            // pushed: false - beat 4 is a top-level destination, so no back
            // chevron. ScreenHost.Show, never Push.
            //
            // `title: null` IS THE HANDOFF, NOT AN OMISSION, and it is the
            // one screen in the project that reads that way. `Onboarding
            // .dc.html` has NO page header: line 33 starts the column with a
            // full-width row of five `flex: 1` progress bars and the step
            // label, with nothing above it but the status bar, and its 26px
            // Baloo title (line 161) is a CARD heading inside the white
            // surface on line 158. `ScreenScaffold`'s own note has what an
            // empty title costs the other nine screens - nothing, because all
            // nine pass a literal - and why it hides the row rather than
            // removing it.
            var scaffold = new ScreenScaffold(title: null);

            // The UXML declares the screen's own furniture as children of
            // this element; the scaffold's slots take them over here. A
            // re-parent rather than a second tree, so there is exactly one
            // place each element is authored.
            //
            // THE PIPS ARE THE FIRST THING IN THE CONTENT REGION, WHICH IS
            // WHERE THE HANDOFF DRAWS THEM - a full-width row above the hero,
            // not a badge beside a title. They were in the scaffold's header
            // slot until this task, squeezed to five fixed 8px marks because
            // a title column was growing beside them; with no header there is
            // no column to compete with and `.progress-pip` takes the
            // handoff's own `flex: 1`. The content container's --gutter is
            // 12px against the handoff's 16px side padding, on the scale.
            scaffold.Content.Add(progress);

            // TWO SURFACES, NOT ONE, AND THE SPLIT IS THE HANDOFF'S. Step 1
            // draws the creature in a tall panel of its own and the words in
            // a card beneath it.
            //
            // THE HERO IS A `HeroBand` AND NOT A `SectionCard`, WHICH IS
            // PHASE 9 TASK 14c. Through Task 14b it was a flat white card at
            // --radius-card, which is ~448px of near-white around a 96px
            // creature - correct in composition and wrong in fidelity. The
            // handoff's step hero is a gradient band at radius 26 with a
            // dashed ring and a violet pool behind the subject
            // (Onboarding.dc.html:43-47), and five other screens in the
            // bundle draw the same surface, which is why it is a component
            // rather than a rule in this screen's sheet.
            //
            // `Fill` RATHER THAN A `flex-grow` HERE. The handoff's band is
            // `flex: 1; min-height: 300px` and BOTH halves have to land on
            // the band's surface rather than on its elevation wrapper -
            // getting that wrong is what cost Task 14b a capture, and
            // `HeroBand.Fill` is where that knowledge now lives so that no
            // screen has to carry it again.
            var hero = new HeroBand();
            hero.Fill(BandFloor);
            hero.Subject.Add(_founder);

            // ONE WHITE CARD CARRIES EVERYTHING ELSE, IN THE HANDOFF'S OWN
            // ORDER: kicker, title, body, field, note. `Onboarding.dc.html`
            // lines 158-169 are exactly that stack in one surface, and Task
            // 14 split it across two containers - the kicker and title in the
            // scaffold's header, the body in a card - which is what made a
            // 21px page title and a 19px card line read as two competing
            // display lines. They are one ladder again: 10px kicker, 26px
            // title, 13px body.
            var card = new SectionCard();
            card.Body.Add(kicker);
            card.Body.Add(title);
            card.Body.Add(_prompt);
            card.Body.Add(_name);
            card.Body.Add(_blocker);
            card.Body.Add(this.Q<VisualElement>("note"));

            scaffold.Content.Add(hero);
            scaffold.Content.Add(card);
            scaffold.CtaRow.Add(_confirm);
            scaffold.CtaRow.Add(_skip);

            // NO FOOTER NOTE, AND THAT IS THE ONE THING THIS SCREEN GAVE UP.
            // "Founders keep their names for life." is still on the screen -
            // it is the tip note inside the card now, which is where
            // `Onboarding.dc.html` puts its own. Left unset rather than set
            // to null for effect: the scaffold hides the row by default.
            Add(scaffold);

            // Registered once, in the constructor, against fields the next
            // Bind overwrites - `DeployView` established this, and the
            // hazard it avoids is a re-bound screen stacking a second
            // handler behind the first.
            _confirm.clicked += Confirm;
            _skip.clicked += Skip;
        }

        /// `portrait` is the live turntable from `Broodline.Game`'s portrait
        /// studio, and null is a real answer rather than a missing argument.
        ///
        /// NULL FALLS BACK TO THE SPRITE STACK, WHICH IS WHY THE PARAMETER IS
        /// OPTIONAL AND TRAILING. `FtueDirector` is constructed with a studio
        /// that may be absent (its own field is `PortraitStudio studio =
        /// null`), the EditMode suite has no camera to render one, and
        /// `ScreenFixtures` captures this screen in batch mode. All three
        /// want the same screen with a baked creature in it, and none of them
        /// should have to say so.
        ///
        /// `Texture` RATHER THAN `RenderTexture`, matching
        /// `PortraitStudio.Show`'s own return type and `CreatureStage`'s
        /// parameter - the studio hands out a `RenderTexture` today and the
        /// stage already branches on that, so narrowing it here would put a
        /// cast in the one place that has no reason to know.
        public void Bind(CreatureDto founder, string defaultName, Action<string> onName,
            Action onSkip, Texture portrait = null)
        {
            if (founder == null) throw new ArgumentNullException(nameof(founder));

            _prompt.text = FounderNamingScreen.Prompt(founder);

            // THE TWO HeroSlot FORMS, AND `Q("body")` IS WHAT TELLS THEM
            // APART. A live slot removes its three sprite layers rather than
            // leaving them empty behind the stage, which is that component's
            // stated contract and the discriminator `FirstHourScreensTests`
            // reads. `Bind` runs on both: a live slot still takes the species
            // tint for its ring, because the studio draws the animal and not
            // the frame around it.
            _founder.Clear();
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
            slot.name = founder.CreatureId.ToString();
            slot.Bind(founder);
            _founder.Add(slot);

            // An empty or absent `defaultName` falls back to the model's own
            // default rather than to a blank field. bible 3.3 asks for "a
            // sensible default"; a caller that forgets to compute one must
            // not be able to ship an empty prompt.
            var seed = FounderNamingScreen.Sanitize(defaultName);
            _name.value = seed.Length > 0 ? seed : FounderNamingScreen.DefaultFor(founder);

            Blocker(null);
            _confirm.text = FounderNamingScreen.ConfirmLabel;
            _skip.text = FounderNamingScreen.SkipLabel;

            _onName = onName;
            _onSkip = onSkip;
        }

        /// What the confirm button does, as a method a test can call.
        ///
        /// PUBLIC FOR THAT REASON, and stated rather than hidden:
        /// `Button.clicked` is raised by a `Clickable` manipulator handling a
        /// dispatched `ClickEvent`, which needs an attached `Panel` - and a
        /// bare `new FounderNamingView()` has none (the constraint
        /// `ComponentTests` and `WaveScreensTests` both document). With the
        /// trim-and-refuse rule inside an anonymous click closure it would be
        /// unreachable from any EditMode test; here the rule is tested
        /// directly and only the click DISPATCH is trusted by inspection.
        /// `WaveHudView.Refresh` is public for the mirror-image reason.
        public void Confirm()
        {
            var name = FounderNamingScreen.Sanitize(_name.value);
            if (name.Length == 0)
            {
                Blocker(FounderNamingScreen.EmptyNameBlocker);
                return;
            }
            Blocker(null);
            _onName?.Invoke(name);
        }

        /// What the skip button does. Public for the same reason `Confirm`
        /// is, and named as the answer it is rather than as a dismissal:
        /// bible 3.3 makes the naming prompt optional and the Roster rename
        /// the fallback, so skipping is a choice the caller must hear about.
        public void Skip()
        {
            Blocker(null);
            _onSkip?.Invoke();
        }

        /// The refusal banner, shown only when it says something.
        ///
        /// THE DISPLAY TOGGLE IS NOT TIDYING. `.founder-naming-view__blocker`
        /// carries a --coral-tint fill and --space-2 of padding, so an empty
        /// blocker is a blank coral strip under the name field - visible in
        /// every capture of this screen before Phase 8 Task 9, on a screen
        /// where nothing is wrong. `ScreenScaffold.FooterNote` collapses its
        /// own row for the same reason and `AnEmptyFooterNoteHidesItsRow`
        /// pins it there; this is that rule applied to the one other
        /// conditional row on the screen. The TEXT still round-trips to
        /// string.Empty, which is what FirstHourScreensTests reads.
        void Blocker(string message)
        {
            _blocker.text = message ?? string.Empty;
            _blocker.style.display = string.IsNullOrEmpty(message)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
