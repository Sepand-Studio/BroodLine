using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// A plot's detail over the Lab, driven by the same FacilityRow as the
    /// list. It only presents the local preview verdict; the Game layer owns
    /// the ledger write and closes or refreshes the sheet after a tap.
    public sealed class FacilitySheet : VisualElement
    {
        readonly VisualElement _scrim, _icon, _timerHost;
        readonly Label _name, _tier, _role, _cost, _blocker, _preview;
        readonly Button _dismiss, _upgrade;
        Action _onDismiss, _onUpgrade;
        string _iconClass;

        public FacilitySheet()
        {
            AddToClassList("facility-sheet");
            Resources.Load<VisualTreeAsset>("FacilitySheet").CloneTree(this);
            _scrim = this.Q<VisualElement>("scrim");
            _icon = this.Q<VisualElement>("icon");
            _timerHost = this.Q<VisualElement>("timer-host");
            _name = this.Q<Label>("name");
            _tier = this.Q<Label>("tier");
            _role = this.Q<Label>("role");
            _cost = this.Q<Label>("cost");
            _blocker = this.Q<Label>("blocker");
            _preview = this.Q<Label>("preview");
            _dismiss = this.Q<Button>("dismiss");
            _upgrade = this.Q<Button>("upgrade");
            _scrim.RegisterCallback<ClickEvent>(_ => _onDismiss?.Invoke());
            _dismiss.clicked += () => _onDismiss?.Invoke();
            _upgrade.clicked += () => _onUpgrade?.Invoke();
        }

        public void Bind(FacilityRow row, Action onUpgrade, Action onDismiss,
            Func<DateTime> now = null, Action onTimerComplete = null)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            _onUpgrade = onUpgrade;
            _onDismiss = onDismiss;
            _name.text = row.Name;
            _tier.text = LabScreen.TierLabel(row.Tier) + " / " + Broodline.Model.Catalogs.FacilityCatalog.MaxTier;
            _role.text = row.Role;
            _preview.text = LabScreen.Preview;
            _dismiss.text = "Close";
            _upgrade.text = LabScreen.Upgrade;
            if (_iconClass != null) _icon.RemoveFromClassList(_iconClass);
            _iconClass = "icon--" + row.Icon;
            _icon.AddToClassList(_iconClass);

            _cost.text = row.NextCostShards > 0
                ? "NEXT TIER  ·  " + LabScreen.Cost(row.NextCostShards, row.NextUpgradeTime)
                : "MAXIMUM TIER";
            _blocker.text = row.Blocker ?? string.Empty;
            _blocker.style.display = string.IsNullOrEmpty(row.Blocker) ? DisplayStyle.None : DisplayStyle.Flex;
            _upgrade.style.display = row.CanUpgrade ? DisplayStyle.Flex : DisplayStyle.None;

            _timerHost.Clear();
            if (row.Upgrading.HasValue && row.UpgradeEndsAt.HasValue)
            {
                var timer = new TimerChip { name = "upgrade-timer" };
                timer.Bind(row.Upgrading.Value);
                _timerHost.Add(timer);
                var clock = now ?? (() => DateTime.UtcNow);
                var endsAt = row.UpgradeEndsAt.Value;
                bool completed = false;
                timer.schedule.Execute(() =>
                {
                    var remaining = endsAt - clock();
                    timer.Bind(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
                    if (remaining > TimeSpan.Zero || completed) return;
                    completed = true;
                    onTimerComplete?.Invoke();
                }).Every(1000);
            }
        }
    }
}
