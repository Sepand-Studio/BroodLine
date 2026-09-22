using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// "A wave was left unfinished." Phase 9 design §2.1: the only way out of
    /// a roster committed to a wave nobody will submit. One button, because
    /// resume is Phase 10's on playtest evidence, and a sheet that offers a
    /// choice it cannot honour is worse than one that offers none.
    ///
    /// A SHEET, LIKE `CodexSheet`: no scaffold, destined for `#sheet-layer`
    /// through `ScreenFlow.ShowSheetAsync`, which hides it on resume.
    ///
    /// `#card` IS THE ELEVATION WRAPPER, NOT THE SURFACE - the same split
    /// `SectionCard` uses, and for the same reason (its own class comment
    /// records this exact bug shipping once already this phase): UI Toolkit
    /// clips a background-image to the element's own box, so `elev-2`'s
    /// nine-sliced shadow has to be drawn by something larger than the
    /// `--surface` fill it sits behind. `#surface`, one level in, carries
    /// the fill, the radius and the padding; `#card` carries only `elev-2`.
    public sealed class AbandonedWaveSheet : VisualElement
    {
        public const string UssClassName = "abandoned-wave-sheet";
        public const string Eyebrow = "Unfinished wave";
        public const string Headline = "A wave was left unfinished.";
        public const string Body =
            "Your creatures are still out there. Forfeit the wave to bring them home - nothing is lost but the fight.";
        public const string ForfeitLabel = "Forfeit";

        /// What the control says while the forfeit is in flight - Phase 9 Task
        /// 21h, and the same defect as `InterruptedView.RetryingLabel`, one
        /// step later in the same walk.
        ///
        /// MEASURED ON THE DEVICE: "Forfeit" was tapped at 15:27:42 and landed
        /// roughly two minutes later, with the sheet unchanged throughout.
        /// `ForfeitIfLockedAsync` makes TWO calls behind this one tap -
        /// `POST /v1/wave/abandon` and then a roster reload - and neither is
        /// wrapped in `Retry`, so the wait is however long the transport takes
        /// and the sheet said nothing about any of it.
        public const string ForfeitingLabel = "Forfeiting...";

        public AbandonedWaveSheet(Action onForfeit)
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("AbandonedWaveSheet").CloneTree(this);

            this.Q<Label>("eyebrow").text = Eyebrow;
            this.Q<Label>("headline").text = Headline;
            this.Q<Label>("body").text = Body;

            var forfeit = this.Q<Button>("forfeit");
            forfeit.text = ForfeitLabel;

            // GUARDED AS BEFORE: with no callback this button does nothing, and
            // a button that went busy for nothing would be a worse lie than the
            // one it already is. Busy BEFORE the invoke for the reason
            // `InterruptedView`'s own subscription gives - the callback is
            // `ScreenFlow`'s resume and the walk runs on from inside it.
            if (onForfeit != null)
            {
                forfeit.clicked += () =>
                {
                    forfeit.SetEnabled(false);
                    forfeit.text = ForfeitingLabel;
                    onForfeit();
                };
            }
        }
    }
}
