using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// A percentage bar - richness, growth, integrity, fragment counts on an
    /// n/15 scale. Counted: the handoff draws this same shape 29 times,
    /// across 10 of its 23 screens.
    ///
    /// ===================================================================
    /// NAME COLLISION, AND EVERY CONSUMER WILL MEET IT.
    /// `UnityEngine.UIElements` HAS ITS OWN `ProgressBar` (base
    /// `AbstractProgressBar`), so any file that imports both that namespace
    /// and `Broodline.UI.Components` cannot write `ProgressBar` unqualified.
    /// It does not read as a namespace clash when it happens: the compiler
    /// binds Unity's type and reports
    ///
    ///     error CS1729: 'ProgressBar' does not contain a constructor that
    ///     takes 1 arguments
    ///
    /// which looks like a broken signature on this class. Measured, not
    /// guessed - that is the exact error this task's first red run produced.
    /// The fix at each call site is one line:
    ///
    ///     using ProgressBar = Broodline.UI.Components.ProgressBar;
    ///
    /// ComponentTests.cs and ScreenFixtures.cs both carry it. The type name
    /// is kept because Tasks 9-12 consume this interface verbatim.
    /// ===================================================================
    ///
    /// THE FILL IS A PERCENTAGE WIDTH, NOT A PIXEL ONE, so a bar re-flows
    /// with its container and needs no layout pass to be read back - which
    /// is what lets AProgressBarClampsRatherThanOverflowing assert the clamp
    /// with no panel attached.
    ///
    /// IT CLAMPS RATHER THAN OVERFLOWING. A fill above 1 would otherwise
    /// paint past the track's rounded end; a negative one would collapse to
    /// a sliver that still reads as "some". Both are the shapes a division
    /// by a stale denominator makes, and both would look like data rather
    /// than like a bug.
    public sealed class ProgressBar : VisualElement
    {
        public const string UssClassName = "progress-bar";

        readonly VisualElement _fill;

        /// `modifier` picks the fill colour, as `progress-bar--&lt;modifier&gt;`:
        /// teal, green, coral, amber or mute - the five families the handoff
        /// fills these bars with besides violet. Null means the default
        /// violet and no extra class at all, so a bar that was never given
        /// one cannot be mistaken for one that was.
        ///
        /// AN UNKNOWN MODIFIER IS NOT AN ERROR HERE, and it renders as the
        /// default violet rather than as nothing - USS simply matches no
        /// rule. Validating against a hardcoded list of five would put a
        /// second copy of ProgressBar.uss's vocabulary in this file to drift
        /// from it, so the check stays where the vocabulary is.
        public ProgressBar(float fill01, string modifier = null)
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("ProgressBar").CloneTree(this);

            if (!string.IsNullOrEmpty(modifier)) AddToClassList(UssClassName + "--" + modifier);

            _fill = this.Q<VisualElement>("fill");
            Fill = fill01;
        }

        public float Fill
        {
            set => _fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
        }
    }
}
