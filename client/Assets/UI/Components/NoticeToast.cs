using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The notice surface. Phase 9 design §2.1: `BootController.OnNotice`
    /// used to log and stop, and `OutboxPump.Notices` filled a list nothing
    /// rendered. A sentence shown here for `HoldMs` is what turns a tester's
    /// "it went blank" into "it said X".
    ///
    /// STACKS, NEVER REPLACES. Two notices in one frame are two facts.
    ///
    /// HIDDEN WHEN EMPTY. The element sits in an absolutely positioned layer
    /// over everything; an empty toast that still occupied the layer would
    /// swallow taps on the row beneath it.
    ///
    /// The hold timer uses the element's scheduler, which only ticks with a
    /// live panel. Without one (every EditMode test) rows simply stay, which
    /// is what the structure tests assert against.
    public sealed class NoticeToast : VisualElement
    {
        public const string UssClassName = "notice-toast";
        public const string RowUssClassName = "notice-toast__row";
        public const int HoldMs = 4000;

        readonly VisualElement _rows;

        public NoticeToast()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("NoticeToast").CloneTree(this);
            _rows = this.Q<VisualElement>("rows");
            pickingMode = PickingMode.Ignore;
            _rows.pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;
        }

        public int Pending => _rows.childCount;

        public void Show(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var row = new Label(text.Trim());
            row.AddToClassList(RowUssClassName);
            row.pickingMode = PickingMode.Ignore;
            _rows.Add(row);
            style.display = DisplayStyle.Flex;

            row.schedule.Execute(() =>
            {
                row.RemoveFromHierarchy();
                if (_rows.childCount == 0) style.display = DisplayStyle.None;
            }).StartingIn(HoldMs);
        }
    }
}
