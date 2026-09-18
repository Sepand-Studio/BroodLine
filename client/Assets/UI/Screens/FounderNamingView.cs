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
    [UxmlElement]
    public partial class FounderNamingView : VisualElement
    {
        public const string UssClassName = "founder-naming-view";

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
            var scaffold = new ScreenScaffold(FounderNamingScreen.Title);

            // The UXML declares the screen's own furniture as children of
            // this element; the scaffold's slots take them over here. A
            // re-parent rather than a second tree, so there is exactly one
            // place each element is authored.
            var card = new SectionCard();
            card.Body.Add(_founder);
            card.Body.Add(_name);

            scaffold.Content.Add(_prompt);
            scaffold.Content.Add(card);
            scaffold.Content.Add(_blocker);
            scaffold.CtaRow.Add(_confirm);
            scaffold.CtaRow.Add(_skip);
            scaffold.FooterNote = FounderNamingScreen.FooterNote;
            Add(scaffold);

            // Registered once, in the constructor, against fields the next
            // Bind overwrites - `DeployView` established this, and the
            // hazard it avoids is a re-bound screen stacking a second
            // handler behind the first.
            _confirm.clicked += Confirm;
            _skip.clicked += Skip;
        }

        public void Bind(CreatureDto founder, string defaultName, Action<string> onName, Action onSkip)
        {
            if (founder == null) throw new ArgumentNullException(nameof(founder));

            _prompt.text = FounderNamingScreen.Prompt(founder);

            _founder.Clear();
            var card = new CreatureCard { name = founder.CreatureId.ToString() };
            // No `counters` map reaches this Bind - same reason as
            // `DeployView`'s: nothing in this assembly can derive one and the
            // caller's signature carries none.
            card.Bind(founder, null);
            _founder.Add(card);

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
