using System;
using System.Collections.Generic;
using Broodline.UI.Components;
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
    ///
    /// A WAVE ROW IS AN `OptionRow` AS OF PHASE 8 TASK 9, and the row's two
    /// labels are that component's - `title` and `detail`, not this screen's
    /// old `label` and `state`. The element NAME is still
    /// `CampaignSelectScreen.RowName(id)`, because that is the handle the
    /// director and the tests reach a single wave by and it belongs to this
    /// screen rather than to the component.
    [UxmlElement]
    public partial class CampaignSelectView : VisualElement
    {
        public const string UssClassName = "campaign-select-view";

        /// Kept, and still applied, though `.option-row` now draws the row:
        /// it is what `CampaignSelectView.uss` hangs the LOCKED treatment off,
        /// which is this screen's own rule rather than the component's.
        public const string RowUssClassName = "campaign-row";

        readonly VisualElement _waves;

        public CampaignSelectView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("CampaignSelectView");
            tree.CloneTree(this);

            _waves = this.Q<VisualElement>("waves");

            // Composed, not inherited - ScreenScaffold's class comment has
            // the reason, and ScaffoldTests' sweep is what depends on it.
            // pushed: false - the campaign is a tab destination, reached by
            // ScreenHost.Show, so it carries no back chevron. The screen's
            // own `title` Label is gone: the scaffold's header IS the title,
            // and two elements named "title" in one tree is a Q() that
            // answers whichever it meets first.
            var scaffold = new ScreenScaffold(CampaignSelectScreen.Title);
            scaffold.Content.Add(_waves);
            Add(scaffold);
        }

        /// `waves` is the bundle's authored ids, as `/v1/sync`'s
        /// `config.waves` sent them. IN THE ORDER GIVEN - the config is the
        /// campaign's order and re-sorting it here would be this view
        /// deciding something the bundle already decided.
        public void Bind(IReadOnlyList<int> waves, int highestWaveCleared, Action<int> onPick)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));

            _waves.Clear();

            // An authored-nothing bundle says so rather than rendering as a
            // screen that failed to load. `FtueDirector.CampaignAsync`
            // returns before binding on an empty set, so this is one
            // caller's guard becoming the view's own.
            if (waves.Count == 0)
            {
                _waves.Add(new EmptyState(CampaignSelectScreen.EmptyMessage, "map"));
                return;
            }

            foreach (var id in waves)
            {
                _waves.Add(RowFor(id, waves, highestWaveCleared, onPick));
            }
        }

        static VisualElement RowFor(
            int id, IReadOnlyList<int> waves, int highestWaveCleared, Action<int> onPick)
        {
            var playable = CampaignSelectScreen.IsPlayable(id, waves, highestWaveCleared);
            var picked = id;

            // NO HANDLER AT ALL on a locked row, rather than a handler behind
            // a disabled element. `SetEnabled(false)` already stops UI Toolkit
            // routing pointer events there, so this is belt and braces - but
            // the belt is what a reader can check, and `onPick` reaching the
            // director for a wave `wave/start` can only refuse is the kind of
            // thing that presents as an unexplained `wave_locked`.
            Action onSelect = null;
            if (playable) onSelect = () => onPick?.Invoke(picked);

            var row = new OptionRow(
                CampaignSelectScreen.RowLabel(id),
                CampaignSelectScreen.StateLabel(id, waves, highestWaveCleared),
                onSelect)
            {
                name = CampaignSelectScreen.RowName(id),
            };
            row.AddToClassList(RowUssClassName);

            // SetEnabled ON THE ROW, not on a button inside it: a locked wave
            // must not be reachable by tapping its label either, and
            // `enabledSelf` on the row is what a caller - and a test - reads
            // back to know whether this wave can be started.
            row.SetEnabled(playable);

            if (!playable)
            {
                // The padlock, out of flow at the row's right edge - the
                // handoff's mark for a destination that exists and is not
                // yours yet. The word "Locked" is already in the detail line;
                // the glyph is what makes the row readable at a glance
                // against three others, which is what design 5.2 asks the
                // campaign list to do.
                var lockGlyph = new VisualElement { name = "lock" };
                lockGlyph.AddToClassList("icon");
                lockGlyph.AddToClassList("icon--lock");
                lockGlyph.pickingMode = PickingMode.Ignore;
                row.Add(lockGlyph);
            }

            return row;
        }
    }
}
