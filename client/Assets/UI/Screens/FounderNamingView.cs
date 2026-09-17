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

            _blocker.text = string.Empty;
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
                _blocker.text = FounderNamingScreen.EmptyNameBlocker;
                return;
            }
            _blocker.text = string.Empty;
            _onName?.Invoke(name);
        }

        /// What the skip button does. Public for the same reason `Confirm`
        /// is, and named as the answer it is rather than as a dismissal:
        /// bible 3.3 makes the naming prompt optional and the Roster rename
        /// the fallback, so skipping is a choice the caller must hear about.
        public void Skip()
        {
            _blocker.text = string.Empty;
            _onSkip?.Invoke();
        }
    }
}
