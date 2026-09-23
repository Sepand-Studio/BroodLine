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
            var band = new HeroBand(ring: true);
            var pennant = new VisualElement { name = "pennant" };
            pennant.AddToClassList("icon"); pennant.AddToClassList("icon--allies"); pennant.AddToClassList("allies__pennant");
            band.Subject.Add(pennant);
            band.Fix(220f);
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
        }
    }
}
