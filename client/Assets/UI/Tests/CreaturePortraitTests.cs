using System;
using Broodline.Api;
using Broodline.UI.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Tests
{
    public sealed class CreaturePortraitTests
    {
        sealed class Source : ICreaturePortraitSource
        {
            public event Action<Texture2D> Evicted;
            public Action<Texture2D> Ready;
            public Texture2D Request(string species, string first, string second, float growth01,
                Action<Texture2D> ready)
            {
                Ready = ready;
                return null;
            }
            public void Evict(Texture2D texture) => Evicted?.Invoke(texture);
        }

        static CreatureDto Creature(string species) => new CreatureDto
        {
            CreatureId = Guid.NewGuid(), Species = species, Generation = 1,
            Trait1 = "None", Trait2 = "None", Instinct = "Forage"
        };

        [TearDown]
        public void ClearSource() => CreatureCard.PortraitSource = null;

        [Test]
        public void ReboundCardIgnoresOldPortraitAndEvictionRestoresFallback()
        {
            var source = new Source();
            CreatureCard.PortraitSource = source;
            var card = new CreatureCard();
            card.Bind(Creature("Vetch"), null);
            var stale = source.Ready;
            card.Bind(Creature("Ember"), null);
            var current = source.Ready;
            var portrait = card.Q<VisualElement>("portrait");
            var oldImage = new Texture2D(1, 1);
            var image = new Texture2D(1, 1);
            try
            {
                stale(oldImage);
                Assert.AreEqual(DisplayStyle.None, portrait.style.display.value);
                current(image);
                Assert.AreSame(image, portrait.style.backgroundImage.value.texture);
                source.Evict(image);
                Assert.AreEqual(DisplayStyle.None, portrait.style.display.value);
                Assert.IsNotNull(card.Q<VisualElement>("silhouette").style.backgroundImage.value.texture);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(oldImage);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        [Test]
        public void HeroSlotFallsBackWhenItsPortraitIsEvicted()
        {
            var source = new Source();
            CreatureCard.PortraitSource = source;
            var slot = new HeroSlot();
            slot.Bind(Creature("Vetch"));
            var image = new Texture2D(1, 1);
            try
            {
                source.Ready(image);
                var portrait = slot.Q<VisualElement>("portrait");
                Assert.AreSame(image, portrait.style.backgroundImage.value.texture);
                source.Evict(image);
                Assert.AreEqual(DisplayStyle.None, portrait.style.display.value);
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
