using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The lane, as a picture, with its pockets named along the bottom.
    ///
    /// THE PICTURE IS A RENDER TEXTURE AND THIS ELEMENT KNOWS NOTHING ABOUT
    /// WHAT IS IN IT. `LaneStage` in `Broodline.Game` owns the camera, the
    /// dressing and the assembled creatures and hands over a `Texture`; this
    /// frames it. That is the same split `CreatureStage` and the portrait
    /// studio already keep, and it is what lets `Broodline.UI` reference only
    /// `Broodline.Model`, `Broodline.Net` and `Generated.Api` while showing
    /// the output of a creature pipeline it cannot name.
    ///
    /// THE TAGS ARE ALONG THE BOTTOM EDGE AND THE HANDOFF'S ARE NOT. Its
    /// wave screen puts four 54px tiles at authored positions ON the lane -
    /// (122,46), (240,84), (46,176), (262,186) - which are coordinates in a
    /// drawing. This lane is rendered by a camera from wave data, so the
    /// pockets are wherever the wave puts them and their screen positions
    /// are not knowable to a UI element holding the finished texture. A
    /// bottom strip is the honest form of the same information: which
    /// pockets exist, and which are taken. `FieldSlotRow` carries the rest
    /// of it in words, which is where the handoff puts the detail too.
    ///
    /// 4:3, AND THE HEIGHT IS A TOKEN. --lane-card-height is 274px, which is
    /// 4:3 at the 366px content width of a 390 frame - the handoff's own
    /// lane is 406x300, the same ratio at its 430. It is a token rather than
    /// a literal because the stage's render texture is sized to match it and
    /// the two live in different assemblies.
    public sealed class LanePreviewCard : VisualElement
    {
        public const string UssClassName = "lane-preview-card";
        public const string SlotUssClassName = "lane-preview-card__slot";
        public const string SlotFilledUssClassName = "lane-preview-card__slot--filled";

        readonly VisualElement _lane;
        readonly VisualElement _slots;

        public LanePreviewCard()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("LanePreviewCard").CloneTree(this);

            _lane = this.Q<VisualElement>("lane");
            _slots = this.Q<VisualElement>("slots");
        }

        /// A `RenderTexture` and a `Texture2D` need different `Background`
        /// factories and there is no overload that takes the base type -
        /// `CreatureStage.SetTexture` has the same three-way branch, and it
        /// is repeated rather than shared because the alternative is a
        /// static helper in a third file that neither component would find.
        ///
        /// Null clears, which is the state before the stage has rendered a
        /// frame: the card shows its own tinted fill rather than the last
        /// wave's picture.
        public void SetTexture(Texture texture)
        {
            if (texture == null) _lane.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            else if (texture is RenderTexture rt) _lane.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else _lane.style.backgroundImage = new StyleBackground((Texture2D)texture);
        }

        /// Rebuilds the strip. `filled` is whether a creature stands in that
        /// pocket; the label is the pocket's name, "A" through "D".
        ///
        /// REBUILDS RATHER THAN RECYCLES, on `LineageStrip`'s reasoning: the
        /// count is the wave's and changes between waves, so there is no
        /// fixed set of children to re-point, and a leftover tag from a
        /// four-pocket wave standing on a three-pocket one would be a pocket
        /// the player cannot fill.
        public void SetSlots(IReadOnlyList<(string label, bool filled)> slots)
        {
            _slots.Clear();
            if (slots == null) return;

            foreach (var slot in slots)
            {
                var tag = new Label(slot.label ?? string.Empty);
                tag.AddToClassList(SlotUssClassName);
                if (slot.filled) tag.AddToClassList(SlotFilledUssClassName);
                _slots.Add(tag);
            }
        }
    }
}
