using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Broodline.UI.Shell
{
    /// The bottom tab bar. `Render` is the whole contract: throw away
    /// whatever was drawn before and redraw from the tabs it is given.
    ///
    /// `Broodline.UI` never references `Broodline.Sim` and does not reference
    /// `Broodline.Game` either, so this class has no idea what unlocks a tab -
    /// it only draws what `Progression.TabsFor` (`Broodline.Model`, called
    /// from `Broodline.Game`) already decided.
    public sealed class TabBar : VisualElement
    {
        public const string UssClassName = "tab-bar";
        public const string TabUssClassName = "tab-bar__tab";
        public const string ActiveTabUssClassName = "tab-bar__tab--active";
        public const string IconUssClassName = "icon";

        public TabBar()
        {
            AddToClassList(UssClassName);
        }

        public void Render(IReadOnlyList<string> tabs, string active, Action<string> onSelect)
        {
            Clear();
            if (tabs == null) return;

            foreach (var tab in tabs)
            {
                var button = new Button(() => onSelect?.Invoke(tab)) { text = tab, name = "tab-" + tab };
                button.AddToClassList(TabUssClassName);
                if (tab == active) button.AddToClassList(ActiveTabUssClassName);

                // The glyph, before the label. `icons.uss` maps
                // `.icon--<lowercased tab>` to a raster and tints it from the
                // token layer, so a tab whose name has no glyph draws nothing
                // rather than drawing the wrong thing.
                var icon = new VisualElement();
                icon.AddToClassList(IconUssClassName);
                icon.AddToClassList(IconUssClassName + "--" + tab.ToLowerInvariant());
                icon.pickingMode = PickingMode.Ignore;   // the button takes the click, not the glyph
                button.Insert(0, icon);

                Add(button);
            }
        }
    }
}
