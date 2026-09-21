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
    /// PHASE 9 TASK 14 BROUGHT IT TO `Onboarding.dc.html` STEP 1, and the
    /// shape of that is three things: the step frame (five pips and a
    /// counter, in the scaffold's header slot), the founder in a `HeroSlot`
    /// above the field, and the caveat moved off the scaffold's footer onto a
    /// violet tip panel inside the card - which is where the handoff draws
    /// its own note. This is the first screen in the project to compose Task
    /// 13's vocabulary, so the arrangement here is the one the four screens
    /// after it follow.
    [UxmlElement]
    public partial class FounderNamingView : VisualElement
    {
        public const string UssClassName = "founder-naming-view";

        /// The card the founder is looked at in, as opposed to the one the
        /// name is typed into. Named here rather than typed as a literal at
        /// the call site so the coupling to the stylesheet is visible from
        /// both ends, on `SectionCard.ElevationUssClassName`'s convention.
        public const string HeroCardUssClassName = "founder-naming-view__hero";

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
            // THE EYEBROW IS ALREADY UPPERCASE AND NOTHING HERE MAKES IT SO.
            // UI Toolkit has no `text-transform`, so the handoff's
            // `.lbl { text-transform: uppercase }` has no property to land in
            // and the casing is baked into the constant. Read, never
            // repeated - `FounderNamingScreen.Eyebrow` has the full note.
            var scaffold = new ScreenScaffold(
                FounderNamingScreen.Title, eyebrow: FounderNamingScreen.Eyebrow);

            // The UXML declares the screen's own furniture as children of
            // this element; the scaffold's slots take them over here. A
            // re-parent rather than a second tree, so there is exactly one
            // place each element is authored.
            //
            // THE HEADER SLOT IS WHERE THE STEP FRAME GOES, which is the
            // scaffold's own answer to "something else per screen" beside the
            // title. FounderNamingView.uss's `__progress` note has why the
            // handoff's full-width bar row could not be reproduced literally.
            scaffold.HeaderSlot.Add(progress);

            // TWO CARDS, NOT ONE, AND THE SPLIT IS THE HANDOFF'S. Step 1
            // draws the creature in a tall panel of its own and the words in
            // a card beneath it; the only thing this collapses is the height,
            // because a `HeroSlot` is 96px where the handoff's hero region is
            // 300+ and a card padded out to that would be mostly empty paper.
            var hero = new SectionCard();
            hero.AddToClassList(HeroCardUssClassName);
            hero.Body.Add(_founder);

            // THE PROMPT IS INSIDE THE CARD, AND THE BRIEF COULD BE READ
            // EITHER WAY. It says the hero slot goes "above the prompt" and
            // separately that the name field goes in a SectionCard, which
            // leaves open whether the prompt is a bare line between the two
            // cards or the card's first child. The handoff settles it: its
            // card is kicker, title, body, note in one white surface, and the
            // kicker and title have already moved to the scaffold's header -
            // so the body copy and the note belong to the same card, with the
            // field between them. A bare `t-section` line floating between
            // two elevated cards is a STRUCTURAL difference from the handoff,
            // which is the class of gap this task exists to close.
            var card = new SectionCard();
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
