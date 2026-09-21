using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// One lane pocket, named and occupied or not: "A  Vetch Wall R2",
    /// "B  Empty".
    ///
    /// IT IS THE TEXT HALF OF THE LANE, AND THE LANE NEEDS ONE. The handoff's
    /// wave screen draws four pockets ON the lane as 54px tiles at authored
    /// positions, and then draws this list under it anyway - because the
    /// tiles are tiny, unlabelled beyond three letters, and sit over a
    /// picture. bible 10.4's "colour never carries information alone" has a
    /// sibling here: position does not either. The row states in words what
    /// the tile states in place.
    ///
    /// `filled` AND `Selected` ARE DIFFERENT QUESTIONS AND BOTH ARE THE
    /// CALLER'S. Filled is whether a creature stands in the pocket; selected
    /// is whether the player is currently pointing at it. A row that set its
    /// own selection on tap would give a list two selected rows the first
    /// time a screen forgot to clear the previous one - `OptionRow`'s class
    /// comment records the same rule for the same reason.
    ///
    /// THE LETTER IS NOT A NUMBER. It is a pocket's name, so it carries no
    /// `t-num`; bible 10.6's floor is about numbers a decision depends on,
    /// and "A" is an identifier. It is the one label in this vocabulary that
    /// deliberately does not get the marker.
    public sealed class FieldSlotRow : VisualElement
    {
        public const string UssClassName = "field-slot-row";
        public const string FilledUssClassName = "field-slot-row--filled";
        public const string SelectedUssClassName = "field-slot-row--selected";

        public FieldSlotRow(string slotLetter, string label, bool filled, Action onTap)
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("FieldSlotRow").CloneTree(this);

            this.Q<Label>("letter").text = slotLetter ?? string.Empty;
            this.Q<Label>("label").text = label ?? string.Empty;

            EnableInClassList(FilledUssClassName, filled);

            // Registered, not provable - see ComponentTests' class comment,
            // point 3, and `OptionRow`. The event bubbles from both labels,
            // so a tap anywhere on the row arrives here without any picking
            // mode being changed.
            if (onTap != null) RegisterCallback<ClickEvent>(_ => onTap());
        }

        /// Reads and writes the class list directly rather than shadowing it
        /// in a field, so there is no second copy of the state to drift from
        /// what is rendered - `OptionRow.Selected`'s arrangement exactly.
        public bool Selected
        {
            get => ClassListContains(SelectedUssClassName);
            set => EnableInClassList(SelectedUssClassName, value);
        }
    }
}
