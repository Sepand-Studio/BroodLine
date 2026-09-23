using System;
using Broodline.Api;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The frame a creature is looked AT in, as opposed to listed in: a
    /// dotted ring around a tinted disc with the animal inside it. The
    /// handoff draws it on both parent cards in `Splice Chamber.dc.html`, on
    /// the predicted hybrid beside them, on the founder in `Onboarding` step
    /// 1, and on the hero card in `Splice Reveal.dc.html`.
    ///
    /// IT TAKES A STACK OF SPRITES OR A LIVE PORTRAIT, AND THAT IS THE WHOLE
    /// REASON IT IS ONE COMPONENT. Those are two different pictures - three
    /// baked `Texture2D`s composited flat, or one `RenderTexture` a camera in
    /// `Broodline.Game` is turning a rigged creature in front of - and every
    /// screen that shows one will, at some point in a session, show the
    /// other: the splice chamber's parents are sprites while its predicted
    /// hybrid turns, and the founder turns while the roster behind it does
    /// not. The ring, the disc, the size and the species tint are identical
    /// in both cases and are what a reader recognises. Building them twice is
    /// how two screens end up with two slightly different rings.
    ///
    /// THE RING IS A TEXTURE, NOT A BORDER, and there was no choice. USS has
    /// `border-width` and `border-color` and no `border-style` at all, so
    /// "dashed" is not a value it can be given. HeroSlot.uss and
    /// generate-textures.py have the arithmetic; the short version is that
    /// `hero-ring.png` is authored white so a single texture can be tinted to
    /// any species, the way icons.uss tints one glyph sheet to two nav
    /// states.
    ///
    /// `Bind(null)` CLEARS RATHER THAN THROWS, unlike `CreatureCard.Bind`.
    /// A card is always about a creature; a slot is frequently empty - the
    /// splice chamber opens with neither parent picked, and the handoff draws
    /// the ring and the empty disc in exactly that state. Clearing is the
    /// picture of "nothing chosen yet", which is a state the screen needs,
    /// and `ArgumentNullException` would not be.
    ///
    /// NOTHING IS GUESSED WHEN ART IS MISSING. `CreatureSprites` returns null
    /// for a species it has no body for and for a trait it has no part for,
    /// and each layer is set from it independently - so an unbaked trait
    /// leaves one layer empty over a correct body rather than replacing the
    /// animal with a stand-in. Five of the six species are unbaked until Task
    /// 15, and that is what those slots will show until then.
    public sealed class HeroSlot : VisualElement
    {
        public const string UssClassName = "hero-slot";

        /// On the slot when a live portrait is inside it rather than a
        /// sprite stack. The stylesheet uses it for the ring's default tint;
        /// a screen can read it to tell the two apart.
        public const string LiveUssClassName = "hero-slot--live";

        public const string RingUssClassName = "hero-slot__ring";
        public const string DiscUssClassName = "hero-slot__disc";
        public const string LayerUssClassName = "hero-slot__layer";

        readonly VisualElement _body;
        readonly VisualElement _dorsal;
        readonly VisualElement _flank;

        string _speciesClass;

        /// The sprite form: three stacked layers inside the ring.
        public HeroSlot()
        {
            Build();
            _body = this.Q<VisualElement>("body");
            _dorsal = this.Q<VisualElement>("dorsal");
            _flank = this.Q<VisualElement>("flank");
        }

        /// The live form: the same ring and disc around a turning portrait.
        ///
        /// THE THREE LAYERS ARE REMOVED, NOT LEFT EMPTY BEHIND THE STAGE.
        /// Three absolute children with no background image cost nothing to
        /// draw, but they are three nodes a screen reader walks and - more to
        /// the point here - `Q&lt;VisualElement&gt;("body")` is how the screen
        /// tests tell a live slot from a sprite one. A slot that answers to
        /// both names is a slot no test can distinguish.
        public HeroSlot(CreatureStage stage)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));

            Build();
            AddToClassList(LiveUssClassName);

            var disc = this.Q<VisualElement>("disc");
            disc.Clear();
            disc.Add(stage);
        }

        void Build()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("HeroSlot").CloneTree(this);
        }

        /// Sets the species tint and, on a sprite slot, the three layers.
        /// Null clears both.
        public void Bind(CreatureDto creature)
        {
            SetSpecies(creature == null ? null : creature.Species);

            // A LIVE SLOT STILL TAKES THE TINT AND NOT THE LAYERS. The
            // portrait studio is already drawing the animal; what it cannot
            // draw is the ring around itself. A screen showing a turning
            // Vetch still wants a teal ring, so `Bind` is useful on both
            // forms and only its second half is conditional.
            if (_body == null) return;

            SetLayer(_body, creature == null ? null : CreatureSprites.Body(creature.Species));
            SetLayer(_dorsal, creature == null
                ? null : CreatureSprites.Part(creature.Species, "sk_dorsal", creature.Trait1));
            SetLayer(_flank, creature == null
                ? null : CreatureSprites.Part(creature.Species, "sk_flank", creature.Trait2));
        }

        /// One `hero-slot--&lt;species&gt;` at a time, and the previous one is
        /// removed by name rather than by clearing the whole class list -
        /// `hero-slot`, `hero-slot--live` and whatever a caller added are all
        /// on this element too.
        ///
        /// An unrecognised species adds nothing and the ring keeps its
        /// default --mute-soft, on `CreatureSprites`' rule: null rather than
        /// a guess. A display name like "Ember Skitter" names two species and
        /// nothing can pick between them.
        void SetSpecies(string species)
        {
            if (_speciesClass != null)
            {
                RemoveFromClassList(_speciesClass);
                _speciesClass = null;
            }

            var key = (species ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length == 0) return;

            _speciesClass = UssClassName + "--" + key;
            AddToClassList(_speciesClass);
        }

        static void SetLayer(VisualElement layer, Texture2D texture)
        {
            layer.style.backgroundImage = texture == null
                ? new StyleBackground(StyleKeyword.None)
                : new StyleBackground(texture);
        }
    }
}
