using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The handoff's layout frame, built once.
    ///
    /// Phase 7 shipped twelve screens as bare VisualElements: no header, no
    /// CTA row, no scrolling content region, each stylesheet re-deriving its
    /// own padding. This is that chrome, and ScaffoldTests asserts every
    /// screen uses it.
    ///
    /// It does NOT own navigation. ScreenHost decides depth and hides the tab
    /// bar (client_architecture section 9); this renders a chevron when told
    /// to and calls back.
    ///
    /// COMPOSED, NOT INHERITED, and the sweep in ScaffoldTests is why. UQuery
    /// searches an element's descendants and not the element itself, so
    /// `screen.Q(className: UssClassName)` finds a scaffold a screen ADDED as
    /// a child and does not find one a screen derived from. Tasks 9-12 add
    /// one; that is also the arrangement that lets a screen keep its own
    /// `[UxmlElement] partial class X : VisualElement` declaration and its own
    /// `UssClassName`, which every existing test in Broodline.UI.Tests reads.
    public sealed class ScreenScaffold : VisualElement
    {
        public const string UssClassName = "screen-scaffold";

        readonly Label _footer;
        readonly VisualElement _header;
        readonly Button _back;
        readonly bool _pushed;

        Action _onBack;

        public VisualElement Content { get; }
        public VisualElement CtaRow { get; }
        /// For a currency header or an avatar, per screen.
        public VisualElement HeaderSlot { get; }

        public string FooterNote
        {
            set
            {
                _footer.text = value ?? string.Empty;
                _footer.style.display = string.IsNullOrEmpty(value)
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public ScreenScaffold(string title, bool pushed = false, Action onBack = null)
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("ScreenScaffold").CloneTree(this);

            this.Q<Label>("title").text = title ?? string.Empty;

            // NO SCROLLER CHROME, AND THAT IS A DECISION MADE ONCE HERE FOR
            // ALL TEN SCREENS. Left at its default of Auto, a ScrollView draws
            // Unity's desktop scroller - a 22px track with arrow buttons -
            // whenever content overflows. Measured on the Scaffold fixture's
            // short frame: it eats into the 12px gutter and clips the
            // right-hand card, so this is visible damage rather than an
            // off-theme control. This is a phone game, phones show no
            // persistent scrollbar, and content clipped at the region's edge
            // is itself the affordance that says there is more below. UI
            // Toolkit has no auto-fading overlay scroller to reach for, so
            // Hidden is the honest choice rather than a compromise.
            //
            // IT HIDES THE CHROME, NOT THE SCROLLING. ScrollerVisibility
            // governs whether the scroller element is shown; the ScrollView
            // still scrolls to wheel and to touch drag. Verified on
            // 6000.6.0f1 by the fixture: the short frame still clips its
            // content at the CTA row rather than growing to fit it, which is
            // the scroll region doing its job with its chrome switched off.
            var content = this.Q<ScrollView>("content");
            content.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            Content = content;
            CtaRow = this.Q<VisualElement>("cta-row");
            HeaderSlot = this.Q<VisualElement>("header-slot");
            _footer = this.Q<Label>("footer-note");
            FooterNote = null;

            // THE CHEVRON FOLLOWS THE CALLBACK, NOT ONLY `pushed`, AND THE
            // SECOND HALF OF THAT CONDITION WAS ADDED IN TASK 9. See `OnBack`
            // below for why, and for why it is settable after construction.
            _pushed = pushed;
            _header = this.Q<VisualElement>("header");
            _back = this.Q<Button>("back");

            var glyph = new VisualElement();
            glyph.AddToClassList("icon");
            glyph.AddToClassList("icon--back");
            glyph.pickingMode = PickingMode.Ignore;
            _back.Add(glyph);

            // Subscribed ONCE, here, against a field the property below
            // overwrites - the arrangement every screen on this branch uses
            // for a re-bindable handler, and the hazard it avoids is a second
            // handler stacking behind the first on the next Bind.
            _back.clicked += () => _onBack?.Invoke();

            OnBack = onBack;
        }

        /// The action behind the back chevron, and the thing that decides
        /// whether there IS one.
        ///
        /// SETTABLE AFTER CONSTRUCTION BECAUSE A SCREEN LEARNS IT AT BIND
        /// TIME. `Broodline.UI` does not reference `Broodline.Game`, so no
        /// screen in this assembly can name `ScreenHost.Pop`; the action
        /// always arrives from a caller, and a caller hands it over with the
        /// rest of the screen's data rather than at `new`. The scaffold
        /// itself has to exist at construction - `ScaffoldTests`' sweep
        /// builds every screen with its parameterless constructor and looks
        /// for one - so the two cannot be the same moment.
        ///
        /// NULL REMOVES THE CHEVRON, AND THAT IS THE POINT RATHER THAN A
        /// SIDE EFFECT. `pushed` still governs layout and is still the
        /// handoff's push table; what it must not govern alone is whether an
        /// AFFORDANCE is drawn. `ScreenHost.Pop` is a documented no-op at
        /// depth zero, which makes an unwired chevron harmless FUNCTIONALLY
        /// and is exactly what makes it bad: a player taps it and there is no
        /// error, no transition and no feedback, so the app reads as broken
        /// rather than as busy. `LineageView` draws no chevron today for this
        /// reason and gains one the moment a caller pushes it and hands over
        /// a `Pop`.
        ///
        /// REMOVED, NOT HIDDEN, as with `SectionCard`'s heading and
        /// `EmptyState`'s glyph: a `display: none` Button is still a node a
        /// screen reader walks, and accessibility section 3 wants menus done
        /// properly. `ATopLevelScreenHasNoBackChevron` asserts absence.
        ///
        /// Pinned from three sides: that test,
        /// `APushedScreenHasABackChevronThatCallsBack`, and
        /// `APushedScreenDrawsNoChevronUntilItIsGivenSomewhereToGo`.
        public Action OnBack
        {
            get => _onBack;
            set
            {
                _onBack = value;
                var wanted = _pushed && value != null;
                if (wanted && _back.hierarchy.parent == null) _header.Insert(0, _back);
                else if (!wanted && _back.hierarchy.parent != null) _back.RemoveFromHierarchy();
            }
        }
    }
}
