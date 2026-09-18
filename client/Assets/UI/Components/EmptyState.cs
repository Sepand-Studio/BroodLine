using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// What a list says when it holds nothing.
    ///
    /// IT EXISTS BECAUSE NOTHING IN THE GAME CURRENTLY SAYS IT. Every roster,
    /// every forecast, every chapter list and every alliance tab can come
    /// back empty, and today each of them renders as blank paper - which
    /// reads as a screen that failed to load rather than as a state the game
    /// meant.
    ///
    /// THE GLYPH IS icons.uss's VOCABULARY, NOT A SECOND ONE. `icon` carries
    /// the 19px box, the scale mode and the --mute-soft tint; `icon--&lt;name&gt;`
    /// carries the raster. The thirteen names live in icons.uss - map, ark,
    /// splice, lab, allies, back, charge, shard, tier, timer, lock, check,
    /// warning - and a name outside that set renders as an empty 19px box
    /// with no error anywhere, so it is worth checking against that file
    /// rather than guessing. This class does not validate it: a hardcoded
    /// list of thirteen here would be a second copy of icons.uss to drift
    /// from it, and the Icons fixture is where a wrong mark is actually
    /// visible.
    ///
    /// THERE IS NO EMPTY-STATE DESIGN IN THE HANDOFF. Its 23 screens all
    /// render populated. So this is the minimum the bible asks for and not a
    /// reinterpretation of something that exists: a centred mark, a muted
    /// sentence, and the air around them.
    public sealed class EmptyState : VisualElement
    {
        public const string UssClassName = "empty-state";

        public EmptyState(string message, string glyph = null)
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("EmptyState").CloneTree(this);

            var mark = this.Q<VisualElement>("glyph");
            if (string.IsNullOrEmpty(glyph))
            {
                // Removed, not hidden, as with SectionCard's heading and
                // ScreenScaffold's chevron: a hidden element is still a node
                // a screen reader walks, and accessibility section 3 wants
                // this done properly.
                mark.RemoveFromHierarchy();
            }
            else
            {
                mark.AddToClassList("icon--" + glyph);
                mark.pickingMode = PickingMode.Ignore;
            }

            this.Q<Label>("message").text = message ?? string.Empty;
        }
    }
}
