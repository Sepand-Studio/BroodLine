using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// One cell of the handoff's three-cell stat row - "Control / Travel /
    /// Arks" under the region detail card, and the same shape wherever a
    /// screen states three facts about one thing.
    ///
    /// THE VALUE LABEL CARRIES `t-num`, WHICH IS THE POINT OF THE COMPONENT.
    /// bible 10.6 keeps exactly two requirements from the abandoned Era-2
    /// brief - tabular figures, and an 11px floor on any number a decision
    /// depends on - and `t-num` is the marker both are enforced through
    /// (Theme.uss binds Nunito and --text-secondary to it; TypographyTests
    /// pins the tabular face; verify-uss-tokens.sh check 2 pins the floor).
    /// It is the one half of 10.6 no stylesheet can satisfy on its own,
    /// because nothing makes a screen remember to put the class on.
    /// Building the cell once is what makes it impossible to forget.
    ///
    /// THE VALUE IS RE-BINDABLE. The handoff's region card swaps all three
    /// cells whenever a region is picked; `Value` is that write.
    ///
    /// THE ROW AROUND IT IS THE CALLER'S. A cell carries half the handoff's
    /// 8px gap on each of its own sides; the container it sits in needs
    /// `flex-direction: row`, which this component cannot supply - a
    /// component's stylesheet reaches the component and its descendants and
    /// never its parent. StatCell.uss's closing note records how that was
    /// found out.
    public sealed class StatCell : VisualElement
    {
        public const string UssClassName = "stat-cell";

        readonly Label _value;

        public StatCell(string label, string value)
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("StatCell").CloneTree(this);

            this.Q<Label>("label").text = label ?? string.Empty;
            _value = this.Q<Label>("value");
            Value = value;
        }

        public string Value
        {
            set => _value.text = value ?? string.Empty;
        }
    }
}
