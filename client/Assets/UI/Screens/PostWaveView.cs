using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// Post-Wave. Design section 5.1 adds this screen to `build_order`'s
    /// Phase 2 list deliberately - beat 3's creature drop happens here, so
    /// the beat set needs a surface that list does not name.
    ///
    /// It binds the SERVER's `WaveSubmitResponse` rather than the local
    /// `WaveReport`, and that is the whole point of the split:
    /// `client_architecture` section 7 makes the client a cache with an
    /// outbox and never a source of truth, so the reward and the verdict a
    /// player is shown are the ones the server returned from re-simulating
    /// the replay - not the ones the device computed.
    [UxmlElement]
    public partial class PostWaveView : VisualElement
    {
        public const string UssClassName = "post-wave-view";
        public const string GrantUssClassName = "post-wave-view__grant";

        readonly Label _headline;
        readonly Label _reward;
        readonly VisualElement _granted;
        readonly Button _next;

        Action _onNext;

        public PostWaveView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("PostWaveView");
            tree.CloneTree(this);

            _headline = this.Q<Label>("headline");
            _reward = this.Q<Label>("reward");
            _granted = this.Q<VisualElement>("granted");
            _next = this.Q<Button>("next");

            _next.clicked += () => _onNext?.Invoke();
        }

        public void Bind(WaveSubmitResponse response, IReadOnlyList<CreatureDto> granted, Action next)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            _headline.text = PostWaveScreen.Headline(response.Result);
            _reward.text = PostWaveScreen.RewardLine(response.Reward);

            // ONE CARD PER CREATURE, named after its id - the same shape
            // `RosterView` uses, and the reason it is named rather than just
            // counted: a screen that shows the right NUMBER of arrivals and
            // the wrong creatures is beat 3 failing silently.
            _granted.Clear();
            if (granted != null)
            {
                foreach (var creature in granted)
                {
                    var card = new CreatureCard { name = creature.CreatureId.ToString() };
                    card.Bind(creature, null);
                    card.AddToClassList(GrantUssClassName);
                    _granted.Add(card);
                }
            }

            _next.text = PostWaveScreen.NextLabel;
            _onNext = next;
        }
    }
}
