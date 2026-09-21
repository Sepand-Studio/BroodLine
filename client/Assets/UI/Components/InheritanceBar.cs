using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// One row of the splice chamber's trait forecast: a species dot, the
    /// trait, its odds, a bar, and a DOM/REC tag.
    ///
    /// THE FORECAST IS THE DESIGN POINT OF THE WHOLE SCREEN, which is why
    /// this is a component and not four elements in a screen's stylesheet.
    /// The handoff's own words: "Show which traits are likely to carry before
    /// the charge is spent, so a pairing is a decision rather than a gamble."
    /// Three of these rows are what turns a splice from a slot machine into a
    /// choice, and every one of them has to read identically or the
    /// comparison between them - which is the entire point - stops working.
    ///
    /// THE PERCENTAGE IS ON `t-num` AND THAT IS NOT DECORATION. Three odds
    /// stacked in a column are read against each other, and Baloo 2's digits
    /// advance at ten different widths (Theme.uss's header has all ten), so
    /// a column of them does not line up and 78 above 54 above 31 reads as
    /// three unrelated numbers. The handoff sets these in Baloo 2; this is
    /// the second place in the phase where bible 10.6 overrules its type
    /// table, and it is the more important of the two.
    ///
    /// IT DOES NOT COMPUTE THE ODDS, THE TAG OR THE COLOUR. All three arrive
    /// decided. `Broodline.UI` references neither `Broodline.Sim` nor the
    /// ruleset, and `SpliceConfirmTests` is built on the rule that this
    /// assembly never re-derives a forecast - a second copy of the dominance
    /// rule living in a UI component is how a screen ends up disagreeing with
    /// the server about what a player is buying.
    ///
    /// `ProgressBar` HERE IS THIS PROJECT'S, NOT UNITY'S, and it resolves
    /// that way without an alias only because this file is INSIDE
    /// `Broodline.UI.Components` - C# searches the enclosing namespace before
    /// any `using`. Every consumer outside it needs
    /// `using ProgressBar = Broodline.UI.Components.ProgressBar;` or the
    /// compiler binds `UnityEngine.UIElements.ProgressBar` and reports
    /// "does not contain a constructor that takes 1 arguments", which reads
    /// like a broken signature rather than a clash. ProgressBar.cs's class
    /// comment is the full account.
    public sealed class InheritanceBar : VisualElement
    {
        public const string UssClassName = "inheritance-bar";
        public const string DotUssClassName = "inheritance-bar__dot";
        public const string TagUssClassName = "inheritance-bar__tag";

        /// `modifier` is the species colour family the trait's owner is in -
        /// `teal`, `coral`, `amber`, `violet`, `green` or `mute` - and it is
        /// the SAME vocabulary `ProgressBar` takes, deliberately: the dot,
        /// the fill and the tag are one colour decision and the caller makes
        /// it once. An unknown modifier is not an error; it renders in the
        /// default violet, on ProgressBar's own rule, because validating
        /// against a hardcoded list here would put a second copy of the
        /// stylesheet's vocabulary in C# to drift from it.
        ///
        /// `odds01` is a probability in 0..1 and is written as a whole
        /// percent. ROUNDED, NOT TRUNCATED: 0.549 is "55%" beside a bar
        /// filled to 54.9%, where truncation would print "54%" for something
        /// nearer a half. Both the number and the bar come from one value, so
        /// they cannot disagree about which trait is likeliest.
        public InheritanceBar(string trait, float odds01, string tag, string modifier)
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("InheritanceBar").CloneTree(this);

            if (!string.IsNullOrEmpty(modifier))
            {
                this.Q<VisualElement>("dot").AddToClassList(DotUssClassName + "--" + modifier);
                this.Q<Label>("tag").AddToClassList(TagUssClassName + "--" + modifier);
            }

            this.Q<Label>("trait").text = trait ?? string.Empty;

            // InvariantCulture, because a locale that writes its digits
            // differently would put a non-tabular glyph in a column whose
            // whole job is to line up. Every other formatted number in this
            // assembly (CreatureLabel.Tier, CreatureLabel.Generation) does
            // the same.
            var percent = Mathf.RoundToInt(Clamp01(odds01) * 100f);
            this.Q<Label>("odds").text = percent.ToString(CultureInfo.InvariantCulture) + "%";

            var tagLabel = this.Q<Label>("tag");
            tagLabel.text = tag ?? string.Empty;

            // REMOVED WHEN THERE IS NOTHING TO SAY, on the rule `SectionCard`
            // and the back chevron already keep: a `display: none` node is
            // still a node a screen reader walks, and an empty chip beside a
            // bar reads as a rendering fault rather than as an absence.
            if (string.IsNullOrEmpty(tag)) tagLabel.RemoveFromHierarchy();

            this.Q<VisualElement>("stack").Add(new ProgressBar(odds01, modifier));
        }

        /// The same NaN-closed clamp `ProgressBar.Fill` uses, and for the
        /// same reason: `Mathf.Clamp01` lets NaN through - both its
        /// comparisons are false for it - and 0/0f is exactly what a forecast
        /// over an empty candidate set yields. Without this the bar would
        /// clamp to empty while the label beside it printed a percentage from
        /// `RoundToInt(NaN)`, which is int.MinValue.
        static float Clamp01(float v) => float.IsNaN(v) ? 0f : Mathf.Clamp01(v);
    }
}
