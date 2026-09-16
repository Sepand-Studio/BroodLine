using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The map screen bound to `RegionScreenModel`
    /// (`client/Assets/UI/RegionScreen.cs`). Placeholder fidelity, same as
    /// the model's own doc comment: "a row of text where the region map will
    /// be."
    ///
    /// Every `NodeRow.CanClaim`/`.Blocker` is the model's own verdict -
    /// `map/claim.ts`'s roster-room check, mirrored there only so the screen
    /// can say so before the tap. This view renders that verdict; it does
    /// not recompute "does this grant fit the roster" from `RosterCount`/
    /// `RosterCap` itself.
    [UxmlElement]
    public partial class RegionView : VisualElement
    {
        public const string UssClassName = "region-view";
        public const string NodeRowUssClassName = "node-row";

        readonly Label _roster;
        readonly VisualElement _nodes;

        public RegionView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("RegionView");
            tree.CloneTree(this);

            _roster = this.Q<Label>("roster");
            _nodes = this.Q<VisualElement>("nodes");
        }

        public void Bind(RegionScreenModel m, Action<int> onClaim)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));

            // `HasRosterCounts` gates this the same way it gates
            // `RosterIsFull` on the model itself - an unset cap of 0 must
            // read as "unknown", never as "0 of 0".
            _roster.text = m.HasRosterCounts
                ? m.RosterCount.ToString(CultureInfo.InvariantCulture) + "/"
                    + m.RosterCap.ToString(CultureInfo.InvariantCulture)
                : string.Empty;

            _nodes.Clear();
            foreach (var node in m.Nodes)
            {
                _nodes.Add(RowFor(node, onClaim));
            }
        }

        static VisualElement RowFor(NodeRow node, Action<int> onClaim)
        {
            var row = new VisualElement
            {
                name = "node-" + node.Slot.ToString(CultureInfo.InvariantCulture),
            };
            row.AddToClassList(NodeRowUssClassName);

            row.Add(new Label { name = "type", text = node.Type });
            row.Add(new Label
            {
                name = "accrued",
                text = node.Accrued.ToString(CultureInfo.InvariantCulture),
            });
            row.Add(new Label
            {
                name = "remaining",
                // Null means a node that never depletes - RegionScreenModel's
                // own doc calls this out; render it as a fact, not a number.
                text = node.Remaining == null
                    ? "unlimited"
                    : node.Remaining.Value.ToString(CultureInfo.InvariantCulture),
            });
            row.Add(new Label { name = "blocker", text = node.Blocker ?? string.Empty });

            var claim = new Button { name = "claim", text = "Claim" };
            claim.SetEnabled(node.CanClaim);
            var slot = node.Slot;
            claim.clicked += () => onClaim?.Invoke(slot);
            row.Add(claim);

            return row;
        }
    }
}
