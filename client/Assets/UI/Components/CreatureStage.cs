using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// A texture, framed. The portrait studio in Broodline.Game owns the
    /// camera and the creature; this element knows nothing about either -
    /// which is what keeps Broodline.UI free of Broodline.Creatures.
    public sealed class CreatureStage : VisualElement
    {
        public const string UssClassName = "creature-stage";
        readonly VisualElement _frame;

        public CreatureStage()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("CreatureStage").CloneTree(this);
            _frame = this.Q<VisualElement>("frame");
        }

        public void SetTexture(Texture texture)
        {
            if (texture == null) _frame.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            else if (texture is RenderTexture rt) _frame.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else _frame.style.backgroundImage = new StyleBackground((Texture2D)texture);
        }
    }
}
