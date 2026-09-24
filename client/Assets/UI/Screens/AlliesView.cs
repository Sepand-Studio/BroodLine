using System;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    public sealed class AlliesView : VisualElement
    {
        public const string UssClassName = "allies";

        readonly VisualElement _body;
        readonly ScreenScaffold _scaffold;

        public AlliesView()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("AlliesView").CloneTree(this);
            _body = this.Q<VisualElement>("body");
            _body.RemoveFromHierarchy();
            _scaffold = new ScreenScaffold(AlliesScreen.Title, eyebrow: AlliesScreen.Eyebrow);
            _scaffold.Content.Add(_body);
            _scaffold.FooterNote = AlliesScreen.Preview;
            Add(_scaffold);
        }

        public void Bind(string allianceName, Action onCreate)
        {
            _body.Clear();
            var band = new HeroBand(ring: false);
            var pennant = new VisualElement { name = "alliance-pennant" };
            pennant.AddToClassList("allies__pennant");
            band.Subject.Add(pennant);
            var kicker = new Label(AlliesScreen.BannerKicker) { name = "banner-kicker" };
            kicker.AddToClassList("allies__banner-kicker");
            band.Subject.Add(kicker);
            var motto = new Label(string.IsNullOrEmpty(allianceName) ? "A banner worth raising" : allianceName)
                { name = "banner-title" };
            motto.AddToClassList("allies__banner-title");
            band.Subject.Add(motto);
            band.AddToClassList("hero-band--deep");
            band.Fix(240f);
            _body.Add(band);

            var card = new SectionCard();
            if (string.IsNullOrEmpty(allianceName))
            {
                card.Body.Add(new EmptyState(AlliesScreen.NoAlliance));
                var create = new Button(() => onCreate?.Invoke()) { name = "create", text = AlliesScreen.Create };
                create.AddToClassList("btn-primary");
                card.Body.Add(create);
            }
            else
            {
                var member = new Label(AlliesScreen.Member(allianceName)) { name = "member" };
                member.style.whiteSpace = WhiteSpace.Normal;
                card.Body.Add(member);
            }
            _body.Add(card);

            var planned = new SectionCard(AlliesScreen.PlannedHeading);
            foreach (var (icon, title, detail) in AlliesScreen.PlannedTools)
            {
                var row = new VisualElement(); row.AddToClassList("allies__benefit");
                var glyph = new VisualElement();
                glyph.AddToClassList("icon"); glyph.AddToClassList("icon--" + icon); glyph.AddToClassList("allies__benefit-icon");
                var words = new VisualElement();
                var heading = new Label(title); heading.AddToClassList("allies__benefit-title");
                var explanation = new Label(detail); explanation.AddToClassList("allies__benefit-detail");
                words.Add(heading); words.Add(explanation);
                row.Add(glyph); row.Add(words);
                planned.Body.Add(row);
            }
            _body.Add(planned);
        }
    }
}
