using System;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    public sealed class LabView : VisualElement
    {
        public const string UssClassName = "lab";
        public const string RowUssClassName = "lab__facility";

        readonly VisualElement _rows;
        readonly ScreenScaffold _scaffold;

        public LabView()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("LabView").CloneTree(this);
            _rows = this.Q<VisualElement>("rows");
            _rows.RemoveFromHierarchy();
            _scaffold = new ScreenScaffold(LabScreen.Title, eyebrow: LabScreen.Eyebrow);
            var card = new SectionCard();
            card.Body.Add(_rows);
            _scaffold.Content.Add(card);
            _scaffold.FooterNote = LabScreen.Preview;
            Add(_scaffold);
        }

        public void Bind(LabScreenModel m, Action<string> onUpgrade,
            Func<DateTime> now = null, Action onTimerComplete = null)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            now = now ?? (() => DateTime.UtcNow);
            _rows.Clear();
            foreach (var f in m.Rows)
            {
                var id = f.Id;
                var row = new OptionRow(f.Name + " · " + LabScreen.TierLabel(f.Tier), f.Detail, null) { name = "facility-" + f.Id };
                row.AddToClassList(RowUssClassName);
                var glyph = new VisualElement { name = "glyph" };
                glyph.AddToClassList("icon"); glyph.AddToClassList("icon--" + f.Icon); glyph.AddToClassList("lab__glyph");
                glyph.pickingMode = PickingMode.Ignore;
                row.Add(glyph);
                if (f.Upgrading != null)
                {
                    var timer = new TimerChip { name = "timer" };
                    timer.Bind(f.Upgrading.Value);
                    timer.AddToClassList("lab__timer");
                    row.Add(timer);
                    var endsAt = now() + f.Upgrading.Value;
                    var clock = now;
                    bool completed = false;
                    timer.schedule.Execute(() =>
                    {
                        var remaining = endsAt - clock();
                        timer.Bind(remaining);
                        if (remaining > TimeSpan.Zero || completed) return;
                        completed = true;
                        onTimerComplete?.Invoke();
                    }).Every(1000);
                }
                else if (f.CanUpgrade)
                {
                    var up = new Button(() => onUpgrade?.Invoke(id)) { name = "upgrade", text = LabScreen.Upgrade };
                    up.AddToClassList("btn-secondary"); up.AddToClassList("lab__upgrade");
                    row.Add(up);
                }
                _rows.Add(row);
            }
        }
    }
}
