using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The amber strip under the trait forecast: a sparkle in a white disc
    /// and one sentence - "Mutation window open - 1 in 9 chance of an
    /// unlisted trait".
    ///
    /// IT IS THE ONE AMBER SURFACE IN THE APP THAT IS GOOD NEWS. Amber is
    /// the warning family everywhere else (`icon--warning`, the vault's
    /// capacity ceiling, an Apex burn-out), and the handoff still puts this
    /// on amber - because a mutation is the rare thing, and the reveal's own
    /// note says it "should feel meaningfully rarer than the base reveal - a
    /// distinct amber treatment". So the fill is a gradient rather than the
    /// flat --amber-tint every warning panel uses, and the mark is a sparkle
    /// rather than a triangle. Those two differences are the whole job.
    ///
    /// EMPTY COLLAPSES IT, and that is the interface rather than a
    /// convenience. The server decides whether there is a mutation window;
    /// this assembly does not, and a screen should not have to branch on it.
    /// `Text = null` is how "the server named none" is drawn, and it is the
    /// same contract `ScreenScaffold.FooterNote` and `NoticeToast` keep.
    ///
    /// HIDDEN RATHER THAN REMOVED, unlike `SectionCard`'s heading. A banner
    /// is re-settable - the chamber re-binds it every time the parents
    /// change - so it has to survive being empty, and a removed element has
    /// no home to come back to inside a card whose other children moved up.
    public sealed class MutationBanner : VisualElement
    {
        public const string UssClassName = "mutation-banner";

        /// Theme.uss's amber panel: the fill, the radius and the padding.
        /// The gradient and the border go over the top of it.
        public const string PanelUssClassName = "panel-amber";

        readonly Label _text;

        public MutationBanner()
        {
            AddToClassList(PanelUssClassName);
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("MutationBanner").CloneTree(this);

            _text = this.Q<Label>("text");
            Text = null;
        }

        public string Text
        {
            set
            {
                _text.text = value ?? string.Empty;
                style.display = string.IsNullOrEmpty(value)
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }
    }
}
