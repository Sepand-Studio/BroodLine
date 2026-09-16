using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// Campaign Select - design section 5.2's second deliberately-added
    /// screen: "how a player reaches wave 6 after wave 2. Without it the
    /// first hour ends at beat 8 with no route to the designed loss."
    ///
    /// Every enabled/disabled decision is `CampaignSelectScreen`'s, which
    /// mirrors `issueWave`'s branch rather than reinventing it - the rule is
    /// "the smallest AUTHORED id past what has been cleared", not
    /// `cleared + 1`, because this bundle authors 1, 2, 6 and 7 and the
    /// arithmetic version locks the only wave a player who cleared 2 can
    /// actually play. This view renders the verdict; it never counts ids
    /// itself.
    [UxmlElement]
    public partial class CampaignSelectView : VisualElement
    {
        public const string UssClassName = "campaign-select-view";
        public const string RowUssClassName = "campaign-row";

        readonly Label _title;
        readonly VisualElement _waves;

        public CampaignSelectView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("CampaignSelectView");
            tree.CloneTree(this);

            _title = this.Q<Label>("title");
            _waves = this.Q<VisualElement>("waves");
        }

        /// `waves` is the bundle's authored ids, as `/v1/sync`'s
        /// `config.waves` sent them. IN THE ORDER GIVEN - the config is the
        /// campaign's order and re-sorting it here would be this view
        /// deciding something the bundle already decided.
        public void Bind(IReadOnlyList<int> waves, int highestWaveCleared, Action<int> onPick)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));

            _title.text = CampaignSelectScreen.Title;

            _waves.Clear();
            foreach (var id in waves)
            {
                _waves.Add(RowFor(id, waves, highestWaveCleared, onPick));
            }
        }

        static VisualElement RowFor(
            int id, IReadOnlyList<int> waves, int highestWaveCleared, Action<int> onPick)
        {
            var row = new VisualElement { name = CampaignSelectScreen.RowName(id) };
            row.AddToClassList(RowUssClassName);

            row.Add(new Label { name = "label", text = CampaignSelectScreen.RowLabel(id) });
            row.Add(new Label
            {
                name = "state",
                text = CampaignSelectScreen.StateLabel(id, waves, highestWaveCleared),
            });

            // SetEnabled ON THE ROW, not on a button inside it: a locked wave
            // must not be reachable by tapping its label either, and
            // `enabledSelf` on the row is what a caller - and a test - reads
            // back to know whether this wave can be started.
            var playable = CampaignSelectScreen.IsPlayable(id, waves, highestWaveCleared);
            row.SetEnabled(playable);

            // NO HANDLER AT ALL on a locked row, rather than a handler behind
            // a disabled element. `SetEnabled(false)` already stops UI Toolkit
            // routing pointer events there, so this is belt and braces - but
            // the belt is what a reader can check, and `onPick` reaching the
            // director for a wave `wave/start` can only refuse is the kind of
            // thing that presents as an unexplained `wave_locked`.
            if (!playable) return row;

            var picked = id;
            row.RegisterCallback<ClickEvent>(_ => onPick?.Invoke(picked));

            return row;
        }
    }
}
