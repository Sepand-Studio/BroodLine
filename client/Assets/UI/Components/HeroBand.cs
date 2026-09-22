using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The gradient panel a subject is looked at INSIDE - the handoff's most
    /// repeated large surface after the card, and the one piece of furniture
    /// Task 13's vocabulary did not have.
    ///
    /// SIX SCREENS DRAW IT, WHICH IS THE ONLY REASON IT IS A COMPONENT.
    /// Counted across `specs/Designs/design_handoff_broodline/screens/`, the
    /// `linear-gradient(170deg, #ffffff, ...)` band at radius 22-26 with
    /// `overflow: hidden` appears on `Onboarding.dc.html:43`,
    /// `Gene Ark:47`, `Gene Lab:51`, `Splice Chamber:101`,
    /// `Splice Reveal:43` and `Hybrid Growth:48`. Two of those are Task 16's
    /// screens and one is Task 19's, so building it per-screen means three
    /// tasks inventing the same band three times - which is the trade Task
    /// 13's brief was written to stop.
    ///
    /// FIVE MORE OF THE SAME FURNITURE EXIST IN OTHER TINTS, NOT COUNTED
    /// ABOVE BECAUSE THIS PHASE HAS NO SCREEN THAT DRAWS THEM. The same
    /// `linear-gradient(170deg, ...)` band at the same 22-26 radius also
    /// appears on `Collector Intercept:46` (`#eef4f7 -> #e2edf3`),
    /// `Alliance Hub:50`, `Alliance Rally:45` and `Relocate Ark:45`
    /// (`#eef4f7 -> #e4eef4`), and `Wave Defeat:31`
    /// (`#fbeee9 -> #f6e2e4`, radius 26, `box-shadow: 0 4px 16px` - the
    /// `.elev-2` pair, so it is this band in coral and nothing else).
    /// THE TINT HOOK EXISTS AS OF PHASE 9 TASK 18 AND `Wave Defeat` IS ITS
    /// FIRST READER. `.hero-band__surface` hard-coded `band-ramp.png` and
    /// `--violet-tint` with nowhere to say "the same band in another tint";
    /// `Tint` is that, and its own comment has the two cheaper hooks that were
    /// measured and rejected. `Coral` is built; the four blue-grey ones are
    /// one tint between them and are three lines away, written down under
    /// `Tint` rather than pre-built, because no screen in this phase draws one.
    ///
    /// SIX UNTIL PHASE 9 TASK 17 MEASURED THE LIST, AND THE ONE THAT LEFT IT
    /// WAS `Wave Defense:69`. Two things were wrong about it and both were
    /// load-bearing for the task that reached for it:
    ///   - `:69` is not a band at all. It is the LANE VIEWPORT - the very
    ///     next line is `<svg viewBox="0 0 406 300">` carrying the lane's
    ///     path, its tree circles and its Ark - and `LanePreviewCard` is
    ///     already that element, quoting `:69`'s own declaration verbatim in
    ///     its stylesheet header.
    ///   - The colour attributed to it here was another screen's.
    ///     `#eef4f7 -> #e2edf3` is `Collector Intercept:46` and appears
    ///     nowhere else in the bundle; `Wave Defense:69` is a third ramp,
    ///     `#eef4f0 -> #e4eff5`, green-to-blue rather than blue-grey.
    /// Corrected rather than left, because the next task to want the tint
    /// hook is Task 18 and `Wave Defeat:31` is the band it actually needs.
    /// No shape changed here - this is a comment.
    ///
    /// THE RING IS NOT THE BAND, AND THE HANDOFF IS CLEARER ABOUT THAT THAN
    /// A COUNT OF `class="spin"` IS. Three of the six band screens draw the
    /// dashed ring inside it (Onboarding, Splice Reveal, Hybrid Growth) and
    /// three do not: `Gene Ark` and `Gene Lab` fill the band with an
    /// isometric scene and `Splice Chamber`'s band is a padded column whose
    /// rings are 74px and 112px ornaments on the creatures inside it, not a
    /// ring on the band. The other two `spin` screens - `Breeder Profile:32`
    /// and `Gene Lab Event:33` - are not this band at all: they are
    /// `linear-gradient(160deg, ...)` header cards with the ring bled off
    /// the top-right corner. So the ring is an OPTIONAL LAYER at the band's
    /// own scale, and `new HeroBand(ring: false)` is a first-class answer
    /// rather than a degenerate one.
    ///
    /// REMOVED, NOT HIDDEN, when there is no ring. `HeroSlot` and
    /// `SectionCard` both make this call and for the same reason: a
    /// `display: none` element still occupies a slot in the accessibility
    /// tree and still answers `Q`, so a band that answers to "ring" whether
    /// or not it has one is a band no test can tell apart.
    ///
    /// HOW IT GROWS IS THIS COMPONENT'S JOB AND NOT THE SCREEN'S, which is
    /// the whole of what Task 14b cost. `.hero-band` IS the `.elev-2`
    /// wrapper; `flex-grow` on it stretches the wrapper while the surface
    /// keeps its own height, and the nine-sliced shadow then draws its drop
    /// around bare paper - 160px of it, on this project's founder screen, in
    /// a capture. `Fill` and `Fix` write to the right element from one place
    /// so a caller cannot get one half without the other, and the two of them
    /// are mutually exclusive by construction rather than by convention.
    /// The three shapes the handoff asks for:
    ///
    ///     content-sized   `new HeroBand()`      Gene Ark, Gene Lab,
    ///                                           Splice Chamber, Hybrid Growth
    ///     grows, floored  `band.Fill(300)`      Onboarding
    ///     fixed           `band.Fix(372)`       Splice Reveal
    ///
    /// `--radius-band` (26) IS THE MINORITY READING - it is Onboarding's and
    /// Splice Reveal's own radius, but the other four want less: `Gene Ark`,
    /// `Gene Lab` and `Hybrid Growth` are 24, `Splice Chamber` is 22. Those
    /// four reach into `.hero-band__surface` from their own screen sheet to
    /// override it; `--radius-band`'s note in `Tokens.uss` records all six.
    public sealed class HeroBand : VisualElement
    {
        public const string UssClassName = "hero-band";

        /// Task 3's class, from Theme.uss. Named here rather than typed as a
        /// literal at the call site so the coupling is visible from both
        /// ends, on `SectionCard.ElevationUssClassName`'s convention.
        public const string ElevationUssClassName = "elev-2";

        public const string SurfaceUssClassName = "hero-band__surface";
        public const string HaloUssClassName = "hero-band__halo";
        public const string RingUssClassName = "hero-band__ring";
        public const string GlowUssClassName = "hero-band__glow";
        public const string SubjectUssClassName = "hero-band__subject";

        /// PUBLIC-SHAPE ADDITION, FLAGGED - Phase 9 Task 14c fix round 2,
        /// new finding 2 ("the subject is 36% of the ring where the
        /// handoff's is 91%"). On `Subject` only when `ring` is true, so
        /// `HeroBand.uss` can size a `HeroSlot` placed inside it
        /// without touching `HeroSlot`'s own class (a Task 13 component;
        /// its public shape, including `--hero-slot`, does not move) and
        /// without a selector keyed off a sibling, which USS on 6000.6.0f1
        /// cannot write - there is no `~` or `+` combinator, only descendant
        /// and child. See `HeroBand.uss`'s note beside
        /// `.hero-band__subject--haloed` for the measurement: the founder's
        /// creature was 36% of the ring's diameter where the handoff reads
        /// 91%, and this is the class that lets the fix live in ONE place
        /// for every ring-bearing band screen (this one and Task 16's
        /// `Splice Reveal`) rather than in each screen's own sheet.
        public const string SubjectHaloedUssClassName = "hero-band__subject--haloed";

        /// PUBLIC-SHAPE ADDITION, DECLARED RATHER THAN SLIPPED IN - Phase 9
        /// Task 18, and it is the hook the class comment above says a later
        /// task would need. Task 19 reaches this component too.
        ///
        /// A TINT IS A RAMP AND NOT A COLOUR, WHICH IS THE WHOLE SHAPE OF THIS
        /// TYPE. The violet six run white to `--violet-tint`; the five in
        /// other tints run PALE-TINT to DEEP-TINT - `Wave Defeat:31` is
        /// `#fbeee9 -> #f6e2e4` and the four blue-grey ones are
        /// `#eef4f7 -> #e2edf3`/`#e4eef4`. Two hooks that WOULD have been
        /// cheaper were measured and rejected in `generate-textures.py`'s ramp
        /// block: tinting `band-ramp.png` through
        /// `-unity-background-image-tint-color` lands coral's deep end 15 away
        /// in summed channel distance (acceptable) and the blue-grey's 17 away
        /// with the sign of `G - R` reversed (not - it is the violet ramp's own
        /// cast showing through), and an alpha ramp over one `background-color`
        /// cannot express a two-stop gradient between two different hues at
        /// all. So a tint NAMES A RAMP, and each one costs a `ramp()` line, a
        /// token pair and one rule in `HeroBand.uss`.
        ///
        /// THE FIVE ARE TWO TINTS, NOT FIVE, at this precision: `#e2edf3` and
        /// `#e4eef4` are 4 apart in summed channel distance, so `Collector
        /// Intercept:46`, `Alliance Hub:50`, `Alliance Rally:45` and `Relocate
        /// Ark:45` are one blue-grey between them. Only `Coral` is built here,
        /// because only `Wave Defeat` is drawn in this phase and an enum member
        /// with no USS rule behind it renders as violet in silence - the same
        /// reason this component shipped with the violet six alone. The
        /// recipe for the other is written down rather than pre-built:
        ///   1. `ramp("band-ramp-mist.png", (0xee,0xf4,0xf7), (0xe3,0xed,0xf3))`
        ///   2. `--mist-tint-deep: #e3edf3` in `Tokens.uss`, with its handoff
        ///      line - it is the midpoint of the two the four screens draw.
        ///   3. `.hero-band--mist .hero-band__surface` beside the coral rule.
        ///   4. `Mist` here, and a frame in the `Band` fixture.
        public enum Tint
        {
            /// The six violet bands - `band-ramp.png`, and the state every
            /// band built before this task is in. No modifier class at all, so
            /// `.hero-band__surface`'s own declarations are what render.
            Violet,

            /// `Wave Defeat.dc.html:31`.
            Coral,
        }

        /// The modifier a tint puts on the ROOT, so `HeroBand.uss` can reach
        /// the surface inside it. On the root and not on the surface because
        /// USS on 6000.6.0f1 has no way to write "the surface of a coral band"
        /// from a class on the surface without inventing a second class for
        /// every tint, and because `Tint` is a property of the BAND.
        ///
        /// Returns null for `Violet`, which is not a special case being
        /// smuggled in: the violet ramp is `.hero-band__surface`'s own
        /// declaration, so the default tint is the absence of a modifier and
        /// a `.hero-band--violet` rule would restate the base rule verbatim.
        /// A THROW ON AN UNHANDLED MEMBER, NOT A FALL-THROUGH TO NULL - fix
        /// round 1. `Tint`'s comment explains why `Mist` is not a member yet,
        /// and a comment is not a mechanism: a third member added without its
        /// USS rule would have returned null here, worn no modifier, drawn the
        /// VIOLET ramp and passed every test in the project. The recipe under
        /// `Tint` is four steps and this is the one that fails loudly when
        /// step 4 is done without steps 1 to 3.
        public static string TintUssClassName(Tint tint)
        {
            switch (tint)
            {
                case Tint.Violet: return null;
                case Tint.Coral: return UssClassName + "--coral";
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(tint), tint,
                        "HeroBand.Tint has a member with no class and therefore no ramp - see "
                        + "the four-step recipe on HeroBand.Tint before adding one.");
            }
        }

        /// Which ramp this instance draws. Read-only after construction: the
        /// tint is a fact about which handoff screen the band belongs to, and
        /// no screen in the bundle changes it at runtime.
        public Tint Tinted { get; }

        readonly VisualElement _surface;

        /// Where the screen's own content goes, deliberately not `this` and
        /// deliberately not the surface. A caller adding to the band directly
        /// would land in the shadow's padding ring, outside the fill; adding
        /// to the surface would land BEHIND the ring instead of in front of
        /// it, because the ring layer is the surface's first child.
        public VisualElement Subject { get; }

        /// `ring: true` is the default because the band's canonical instance
        /// has one - `Onboarding.dc.html:43` is the fullest of the six and is
        /// what every measurement in `HeroBand.uss` is taken from.
        ///
        /// `tint` DEFAULTS TO VIOLET, so every call site written before Phase 9
        /// Task 18 keeps its exact behaviour and its exact class list - the
        /// default adds no modifier at all. See `Tint`.
        public HeroBand(bool ring = true, Tint tint = Tint.Violet)
        {
            AddToClassList(UssClassName);
            AddToClassList(ElevationUssClassName);

            Tinted = tint;
            var modifier = TintUssClassName(tint);
            if (modifier != null) AddToClassList(modifier);

            Resources.Load<VisualTreeAsset>("HeroBand").CloneTree(this);

            _surface = this.Q<VisualElement>("surface");
            Subject = this.Q<VisualElement>("subject");

            // See `SubjectHaloedUssClassName`'s own comment. `ring` is
            // already the signal for "does this instance have the halo the
            // handoff frames a subject in", so the same flag decides both.
            if (ring) Subject.AddToClassList(SubjectHaloedUssClassName);
            else this.Q<VisualElement>("halo").RemoveFromHierarchy();
        }

        /// The handoff's `flex: 1; min-height: <n>px` - the band takes
        /// whatever the column has left and never goes under `minHeight`.
        ///
        /// BOTH ELEMENTS, FROM ONE CALL, AND THAT IS THE POINT OF THE METHOD.
        /// See this type's class comment and `SectionCard.uss`'s note beside
        /// `.section-card__surface`: growing only the wrapper is what put a
        /// nine-sliced shadow around 160px of bare paper on the founder
        /// screen, and no test saw it.
        ///
        /// INLINE RATHER THAN A CLASS AND A USS RULE, so the coupling is
        /// something a test with no live panel can actually read. A pair of
        /// `.hero-band--fill` rules would be equally correct and equally
        /// silent when one of them was deleted, which is the failure mode
        /// this component exists to close.
        ///
        /// `minHeight` IS NOT OPTIONAL AND HAS NO DEFAULT. The handoff's band
        /// floors are 300 (Onboarding) and 372 (Splice Reveal) - they are
        /// each a screen's own number and there is no sensible fallback. A
        /// band that wants to grow with no floor passes 0 and says so.
        public void Fill(float minHeight)
        {
            style.flexGrow = 1;
            _surface.style.flexGrow = 1;
            _surface.style.minHeight = minHeight;
            _surface.style.height = StyleKeyword.Null;
        }

        /// The handoff's `height: <n>px` - `Splice Reveal.dc.html:43` is the
        /// one instance that pins its band rather than growing it.
        ///
        /// ON THE SURFACE, NEVER ON THE ROOT, and this method exists so that
        /// a caller does not have to know why. The root is the `.elev-2`
        /// wrapper: a height set there sizes the shadow and leaves the fill
        /// at whatever its content came to, which is the same defect `Fill`
        /// guards from the other direction. Sized from the surface, the
        /// wrapper takes that height plus its own --elev-2-spread on each
        /// side, which is exactly where the drop is drawn.
        public void Fix(float height)
        {
            style.flexGrow = StyleKeyword.Null;
            _surface.style.flexGrow = StyleKeyword.Null;
            _surface.style.minHeight = StyleKeyword.Null;
            _surface.style.height = height;
        }
    }
}
