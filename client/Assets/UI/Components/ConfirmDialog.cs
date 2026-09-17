using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The two-level confirmation dialog - screen_inventory_v2 11. Renders a
    /// `SpliceDialog` (Phase 6's model, `client/Assets/UI/SpliceScreen.cs`);
    /// it does not decide any copy or any flag itself.
    ///
    /// `Standard` is dismissible: Cancel is a real answer and so is tapping
    /// outside it. `Named` is bible 3.3's whole argument made structural - a
    /// player's Founder is only ever lost to a tap that reads "Consume Ash",
    /// so `Named` can be neither trained through (no primary/default button)
    /// nor swiped past (no scrim dismiss) - and it enforces both itself,
    /// rather than trusting `SpliceDialog.ConfirmIsPrimary`, so a future
    /// caller cannot re-enable either one by passing the wrong flag.
    [UxmlElement]
    public partial class ConfirmDialog : VisualElement
    {
        public const string UssClassName = "confirm-dialog";
        public const string PrimaryUssClassName = "primary";

        readonly VisualElement _scrim;
        readonly Label _title;
        readonly Label _body;
        readonly Button _confirm;
        readonly Button _cancel;

        public ConfirmDialog()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("ConfirmDialog");
            tree.CloneTree(this);

            _scrim = this.Q<VisualElement>("scrim");
            _title = this.Q<Label>("title");
            _body = this.Q<Label>("body");
            _confirm = this.Q<Button>("confirm");
            _cancel = this.Q<Button>("cancel");
        }

        /// Cancel is the safe action (splice_confirm_spec section 4); tapping
        /// the scrim is wired to the same `cancel` action, and the confirm
        /// button is styled primary exactly when the model says so.
        public static ConfirmDialog Standard(SpliceDialog d, Action confirm, Action cancel)
        {
            var dialog = new ConfirmDialog();
            dialog.SetCopy(d);

            dialog._confirm.EnableInClassList(PrimaryUssClassName, d.ConfirmIsPrimary);

            dialog._scrim.pickingMode = PickingMode.Position;
            dialog._scrim.RegisterCallback<ClickEvent>(_ => cancel?.Invoke());

            dialog._confirm.clicked += () => confirm?.Invoke();
            dialog._cancel.clicked += () => cancel?.Invoke();
            return dialog;
        }

        /// No destructive default and no dismiss-by-tap-outside, regardless
        /// of what `d` says. The scrim is set `PickingMode.Ignore` rather
        /// than simply left unwired: a picking mode of Ignore means a live
        /// Panel never routes a pointer event to it at all, so there is no
        /// path - today's or a future edit's - by which a tap on it could
        /// read as a dismissal.
        public static ConfirmDialog Named(SpliceDialog d, Action confirm, Action cancel)
        {
            var dialog = new ConfirmDialog();
            dialog.SetCopy(d);

            dialog._scrim.pickingMode = PickingMode.Ignore;

            dialog._confirm.clicked += () => confirm?.Invoke();
            dialog._cancel.clicked += () => cancel?.Invoke();
            return dialog;
        }

        void SetCopy(SpliceDialog d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            _title.text = d.Title;
            _body.text = d.Body;
            _confirm.text = d.ConfirmLabel;
            _cancel.text = d.CancelLabel;
        }
    }
}
