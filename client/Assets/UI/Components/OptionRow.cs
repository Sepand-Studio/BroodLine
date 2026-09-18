using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// A pickable option in a list - the route picker, the splice parent
    /// picker, the chapter list. screen_inventory_v2 11 counts it on most of
    /// the screens Phases 10-13 add, which is why it is built once here.
    ///
    /// SELECTION IS A CLASS AND NOTHING ELSE. `Selected` reads and writes the
    /// class list directly rather than shadowing it in a bool field, so there
    /// is no second copy of the state to drift from what is rendered. The
    /// whole of the handoff's selected treatment - a 2px ring in `--ring`
    /// over a `--surface` fill, against a 1px `--hairline` over
    /// `--surface-sunk` when unselected - hangs off that one class, and so
    /// does `Motion.uss`'s transition.
    ///
    /// IT DOES NOT SELECT ITSELF. Which row in a list is selected is a
    /// question about the list, so the row reports the tap through
    /// `onSelect` and the screen decides. A row that set its own class on
    /// click would give a list two selected rows the first time a screen
    /// forgot to clear the previous one.
    public sealed class OptionRow : VisualElement
    {
        public const string UssClassName = "option-row";
        public const string SelectedUssClassName = "option-row--selected";

        public OptionRow(string title, string detail, Action onSelect)
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("OptionRow").CloneTree(this);

            this.Q<Label>("title").text = title ?? string.Empty;
            this.Q<Label>("detail").text = detail ?? string.Empty;

            // Registered, not provable. ComponentTests' class comment, point
            // 3: confirming a ClickEvent callback FIRES needs a dispatched
            // event, which needs an attached Panel, which that assembly does
            // not have. Same trade ConfirmDialog.Standard already makes. The
            // event bubbles up from the two labels, so no picking mode needs
            // changing for a tap anywhere on the row to arrive here.
            if (onSelect != null) RegisterCallback<ClickEvent>(_ => onSelect());
        }

        public bool Selected
        {
            get => ClassListContains(SelectedUssClassName);
            set => EnableInClassList(SelectedUssClassName, value);
        }
    }
}
