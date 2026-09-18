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
            Content = this.Q<ScrollView>("content");
            CtaRow = this.Q<VisualElement>("cta-row");
            HeaderSlot = this.Q<VisualElement>("header-slot");
            _footer = this.Q<Label>("footer-note");
            FooterNote = null;

            var back = this.Q<Button>("back");
            if (!pushed)
            {
                // Removed, not hidden: ATopLevelScreenHasNoBackChevron asserts
                // absence, and a hidden button is still focusable by a screen
                // reader. accessibility section 3 wants menus done properly.
                back.RemoveFromHierarchy();
            }
            else
            {
                var glyph = new VisualElement();
                glyph.AddToClassList("icon");
                glyph.AddToClassList("icon--back");
                glyph.pickingMode = PickingMode.Ignore;
                back.Add(glyph);
                if (onBack != null) back.clicked += onBack;
            }
        }
    }
}
