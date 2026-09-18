using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// A titled card surface - the handoff's most repeated container, and the
    /// first thing in the project to consume Task 3's elevation classes.
    ///
    /// THE ROOT IS THE SHADOW WRAPPER, NOT THE CARD. Theme.uss's header
    /// note 2: UI Toolkit clips background-image to the element's own box, so
    /// a nine-sliced drop shadow cannot be drawn by the card it belongs to -
    /// it has to be drawn by something larger. So `.elev-1` goes on this
    /// element, which is padding plus the shadow sprite and nothing else, and
    /// the opaque `--surface` fill lives on the `surface` child inside it.
    /// Putting the elevation on the surface instead paints a grey smudge
    /// across the card's interior; that shipped once this phase and only the
    /// Primitives capture caught it, which is why
    /// ASectionCardWearsItsElevationOnTheWrapperAndNotOnTheSurface now
    /// asserts both halves of the arrangement.
    ///
    /// `Body` IS THE SURFACE'S CONTENT SLOT, deliberately not `this`. A
    /// caller adding to the card directly would land in the shadow's padding
    /// ring, outside the white fill.
    public sealed class SectionCard : VisualElement
    {
        public const string UssClassName = "section-card";

        /// Task 3's class, from Theme.uss. Named here rather than typed as a
        /// literal at the call site so the coupling is visible from both ends.
        public const string ElevationUssClassName = "elev-1";

        public VisualElement Body { get; }

        public SectionCard(string heading = null)
        {
            AddToClassList(UssClassName);
            AddToClassList(ElevationUssClassName);
            Resources.Load<VisualTreeAsset>("SectionCard").CloneTree(this);

            var label = this.Q<Label>("heading");
            if (string.IsNullOrEmpty(heading))
            {
                // Removed, not hidden - same rule ScreenScaffold applies to
                // its back chevron. A display:none Label still occupies a
                // slot in the accessibility tree, and
                // ASectionCardWithNoHeadingDoesNotReserveOne asserts absence.
                label.RemoveFromHierarchy();
            }
            else
            {
                label.text = heading;
            }

            Body = this.Q<VisualElement>("body");
        }
    }
}
