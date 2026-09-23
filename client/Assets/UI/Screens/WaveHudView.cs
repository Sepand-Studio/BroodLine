using System;
using System.Collections.Generic;
using Broodline.Model;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The live Wave Defense HUD, in UI Toolkit.
    ///
    /// It replaces `View/WaveHud.cs`, which was IMGUI on purpose and said so:
    /// "a debug surface that the real Wave Defense screen replaces wholesale,
    /// and building it in UI Toolkit would invite it to be kept." Design
    /// section 4 makes this the replacement, and drops `DrawDebug` with it -
    /// entity count, alpha, catch-up, backlog and frame time were the five
    /// numbers that made it read as a tool rather than a game.
    ///
    /// THREE THINGS SURVIVE FROM THAT FILE BECAUSE THEY WERE EACH A FIX:
    ///
    /// 1. The bar's lift above a body is a WORLD offset along the camera's
    ///    own up axis, converted by the camera. `camera.transform.up` IS
    ///    "which world axis maps to screen-up", by definition. WaveHud
    ///    records three failed derivations - a world +Y offset that an
    ///    orthographic top-down camera discards, a +X one that ran along the
    ///    lane and pushed a raider's bar off the top of the frame near the
    ///    Ark, and a safe-area fraction that worked only by an unasserted
    ///    numeric coincidence between two constants in two assemblies.
    ///
    /// 2. One source per entity, and it is the snapshot. Mixing a live
    ///    runner's HP with a snapshot's position made the bar trail the body
    ///    it labelled between ticks, and disagree with it about existence on
    ///    the terminating tick. This view is handed a `HudSnapshot` built
    ///    from one source and never reaches past it.
    ///
    /// 3. A breach outranks a chill. That frame is the verdict.
    ///
    /// 4. A bar is clamped into the frame and clear of the integrity readout.
    ///    `WaveHud.ClampIntoSafeArea` reserved a header band for exactly this
    ///    reason: a bar that DOES get clamped must land below the integrity
    ///    line rather than on top of the one element the safe-area work was
    ///    done to make readable. See `ClampIntoFrame`.
    ///
    /// AND ONE THING IS NEW AND LOAD-BEARING: every element here is
    /// `PickingMode.Ignore`. Rally is the player's only input during a wave
    /// (combat_engine section 8) and it is a tap anywhere on the screen. A
    /// full-screen UI Toolkit panel that accepted pointer events would eat
    /// every one of them, which is a HUD that silently disables the game.
    ///
    /// IT TAKES NO `ScreenScaffold`, AND THAT IS NOT AN OVERSIGHT IN PHASE 8.
    /// This type IS in `Broodline.UI.Screens` and IS therefore reached by
    /// `ScaffoldTests.EveryScreenComposesTheScaffold`'s reflection sweep; that
    /// test exempts it BY NAME, because it is an overlay rather than a screen.
    /// `WaveRunner` adds it straight to the wave scene's own
    /// `document.rootVisualElement` - it never reaches `ScreenHost`, never
    /// enters the back stack, and has no header, no CTA row and nothing to
    /// scroll. Giving it a scaffold to satisfy the sweep, or moving the type
    /// out of this namespace to dodge the sweep, would each be working around
    /// that test rather than with it. The exemption list is asserted to be
    /// exactly two names so a third cannot be quietly added.
    ///
    /// WHAT IT DOES ADOPT is the vocabulary: `.elev-1` around each bar's
    /// track in `SectionCard`'s exact wrapper arrangement, `--hud-scrim`
    /// behind the band, the readout and the track, `--surface` as the one ink,
    /// and `t-num` on the numerals. `WaveHudView.uss`'s header has what left
    /// the file and why.
    ///
    /// PHASE 9 TASK 18 GAVE IT THE HANDOFF'S TOP BAND - the kicker and the
    /// wave counter over the scrim, with the integrity readout beneath them in
    /// the handoff's pill anatomy. Two things are deliberately NOT drawn and
    /// each is measured rather than declined: the pause and speed controls
    /// (`WaveHudView.uss`'s header - there is no such icon, every element here
    /// is unpickable, and a tap anywhere Rallies) and the `Gene energy` pill
    /// (`WaveHudScreen.IntegrityLabel` - this game's combat loop has no
    /// energy). `HudSnapshot` gained nothing; `Wave` is a property, on
    /// `Camera`'s own precedent.
    ///
    /// AND IT STOPPED ALLOCATING PER FRAME, which on this view is a
    /// correctness property rather than a polish one. `Refresh` rebuilt the
    /// readout's string every frame and a rallied creature's "RALLY 42" with
    /// it; both are now written only when the numbers behind them move. See
    /// `_drawnIntegrity` and `Bar.TaggedState`.
    [UxmlElement]
    public partial class WaveHudView : VisualElement
    {
        public const string UssClassName = "wave-hud-view";
        public const string BarUssClassName = "wave-hud-view__bar";
        public const string ElevationWrapperUssClassName = "wave-hud-view__elev";
        public const string TrackUssClassName = "wave-hud-view__track";
        public const string FillUssClassName = "wave-hud-view__fill";
        public const string TagUssClassName = "wave-hud-view__tag";
        public const string ChilledUssClassName = "chilled";
        public const string BreachingUssClassName = "breaching";
        public const string RalliedUssClassName = "rallied";
        public const string CreatureUssClassName = "creature";

        /// How far above a body its bar sits, in WORLD units along whichever
        /// axis the camera maps to screen-up. The body is a cube at scale
        /// 0.81, so this clears it with room for the tag.
        public const float BarLiftWorld = 0.75f;

        /// The bar's own width in panel units. Half of it is subtracted to
        /// centre the bar on the body.
        const float BarWidth = 52f;

        readonly Label _integrity;
        readonly Label _tick;
        readonly Label _waveNumber;
        readonly VisualElement _waveLine;
        readonly VisualElement _bars;

        /// The whole top-left block of chrome - the band and the readout - as
        /// ONE direct child of this root. `Place` measures it; see the note
        /// there, and the UXML's own beside `#chrome`.
        readonly VisualElement _chrome;

        /// The last integrity and tick each readout was written for, so an
        /// unchanged frame does not rebuild its string.
        ///
        /// THIS IS A CORRECTNESS FIX AND NOT A TIDY-UP. `Refresh` runs every
        /// frame of the one scene with a defended per-frame budget, and each
        /// of these strings is a `ToString` - managed allocations per frame,
        /// handed to the collector for lines that only change when the
        /// simulation ticks. The simulation ticks at a fixed 30Hz and the
        /// renderer does not, so at 60fps at least half of them were for an
        /// identical string. `int.MinValue` rather than 0 because 0 is a tick
        /// and an integrity a wave really reaches; the sentinel has to be a
        /// value the snapshot cannot carry.
        ///
        /// TWO SENTINELS AND, AS OF PHASE 9 TASK 21e, TWO GUARDS - see
        /// `Refresh`. The tick moved off the integrity Label onto one of its
        /// own, so the two numbers are written independently and at their own
        /// rates.
        int _drawnIntegrity = int.MinValue;
        int _drawnTick = int.MinValue;

        /// Constructed once and re-attached, never re-allocated. This view
        /// redraws every frame of a scene with a defended per-frame budget;
        /// `new VisualElement()` per body per frame is exactly the garbage
        /// `WaveRunner._onTick` was hoisted out of `Update` to avoid.
        readonly List<Bar> _pool = new List<Bar>();

        /// A pooled bar and the two children `Draw` writes every frame.
        ///
        /// CACHED AT CONSTRUCTION, not looked up per draw. `UQuery`'s `Q<T>`
        /// walks the subtree on every call, and `Draw` ran two of them per
        /// body per frame on the one scene `WaveHost` calls "the only one
        /// with a per-frame budget worth defending" - a dozen bodies at
        /// 60fps is well over a thousand subtree walks a second for two
        /// references that never change after `NewBar` builds them.
        sealed class Bar
        {
            public VisualElement Root;
            public VisualElement Fill;
            public Label Tag;

            /// What this bar's tag was last written for. `WaveHudScreen
            /// .BarTag` is a pure function of exactly these two fields, so a
            /// body whose state and countdown are unchanged produces the same
            /// sentence - and "RALLY 42" is a concatenation, allocated before
            /// the Label's setter gets to notice the text is identical.
            ///
            /// SAFE ACROSS REUSE, which is the thing to check: a pooled bar is
            /// index-aligned to `bodies[i]` and the body at index i is a
            /// different raider from one frame to the next. That does not
            /// matter here precisely because the tag depends on nothing else -
            /// two different bodies in the same state with the same remainder
            /// have the same tag.
            ///
            /// -1 IS NOT A `BodyState`. The enum runs 0..3, so the sentinel
            /// cannot collide with a real state the way a default 0 (Normal)
            /// would - a bar built for a Normal body would otherwise skip its
            /// own first write and keep whatever text the UXML left.
            public int TaggedState = -1;
            public int TaggedRally = int.MinValue;
        }

        Func<HudSnapshot> _read;

        /// Held so a second `Bind` REPLACES the per-frame redraw rather than
        /// stacking a second one behind it. Same hazard `DeployView` and
        /// `WaveDefeatView` guard for `clicked`, and the same fix: one
        /// registration, owned by a field the next Bind can overwrite. A
        /// recycled HUD would otherwise redraw twice a frame, then three
        /// times, on the one screen with a per-frame budget worth defending.
        IVisualElementScheduledItem _redraw;

        /// The camera the world positions are projected through. Assigned by
        /// the shell that owns the wave scene; `RuntimePanelUtils` needs it
        /// and this assembly must not go looking for `Camera.main` itself,
        /// because a view that finds its own camera is a view that finds the
        /// wrong one the first time a second camera exists.
        ///
        /// Not a `Bind` parameter: the brief pins `Bind(Func&lt;HudSnapshot&gt;)`
        /// and the camera outlives any one binding.
        public Camera Camera { get; set; }

        /// Which wave is being fought, for the counter in the top band.
        ///
        /// A PROPERTY AND NOT A FIELD ON `HudSnapshot`, WHICH IS THE WHOLE
        /// POINT. The handoff's chrome has wanted this since Phase 8 and
        /// `WaveHudView.uss`'s header recorded it as owed because the obvious
        /// route - a fourth field on the snapshot - reaches Broodline.Model,
        /// `WaveRunner`'s per-frame snapshot builder and
        /// `WaveHudScreen.Tick`'s line, which the device re-capture
        /// procedure reads off the screen to time a tap. None of that is
        /// needed: the wave id is fixed for the whole run, so it is not
        /// per-frame data. `Camera` is the same shape for the same reason and
        /// says so ("not a `Bind` parameter: the brief pins
        /// `Bind(Func<HudSnapshot>)` and the camera outlives any one
        /// binding"), and `HudSnapshot` gains nothing, which is what the
        /// brief asked for.
        ///
        /// WRITTEN ONCE, HERE, AND NEVER IN `Refresh`. Formatting it per frame
        /// would be a string concatenation on the one screen with a per-frame
        /// budget worth defending, for a number that cannot change.
        ///
        /// `int?` BECAUSE A HUD WITH NO WAVE MUST NOT CLAIM WAVE ZERO. The
        /// same rule `DeployScreen.FoesStatValue` states: a caller that does
        /// not know says nothing. The counter's whole row is removed from the
        /// layout when it is null; the eyebrow, which names the place rather
        /// than the wave, stays.
        public int? Wave
        {
            get => _wave;
            set
            {
                _wave = value;
                _waveNumber.text = value == null
                    ? string.Empty : WaveHudScreen.WaveOf(value.Value);
                _waveLine.style.display = value == null
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        int? _wave;

        public WaveHudView()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;

            var tree = Resources.Load<VisualTreeAsset>("WaveHudView");
            tree.CloneTree(this);

            // EVERY ELEMENT, SWEPT, RATHER THAN EACH ONE NAMED. Rally is the
            // player's only input during a wave (combat_engine section 8) and
            // it is a tap anywhere on the screen, so one pickable element in a
            // full-screen overlay is a HUD that silently disables the game.
            // `WaveHud_AcceptsNoPointerEventAnywhere` sweeps the subtree for
            // exactly this, and until Phase 9 Task 18 this constructor set two
            // elements by hand against a test that checked all of them - a
            // gap that only stayed closed because the tree had two elements
            // in it. It has fourteen now. `NewBar` still sets its own,
            // because a bar is built after this runs.
            foreach (var element in this.Query<VisualElement>().ToList())
                element.pickingMode = PickingMode.Ignore;

            _chrome = this.Q<VisualElement>("chrome");
            _integrity = this.Q<Label>("integrity");
            _tick = this.Q<Label>("tick");
            _waveNumber = this.Q<Label>("wave-number");
            _waveLine = this.Q<VisualElement>("wave-line");
            _bars = this.Q<VisualElement>("bars");

            // THE THREE FIXED STRINGS, SET ONCE. Every one of them is
            // `WaveHudScreen`'s rather than a literal here - the convention
            // that file's own header states, and the one the Task 15 review
            // found three screens breaking.
            this.Q<Label>("eyebrow").text = WaveHudScreen.Eyebrow;
            this.Q<Label>("wave-word").text = WaveHudScreen.WaveWord;
            this.Q<Label>("integrity-label").text = WaveHudScreen.IntegrityLabel;

            // Null until a caller says otherwise, which collapses the counter
            // rather than printing "Wave  / 12" with a hole in it.
            Wave = null;
        }

        /// `read` is PULLED, once per frame, rather than pushed on a tick
        /// boundary. The simulation ticks at a fixed 30Hz and the renderer
        /// does not; a HUD updated on tick boundaries would be a second
        /// clock disagreeing with `WaveClock`'s interpolation.
        public void Bind(Func<HudSnapshot> read)
        {
            _read = read ?? throw new ArgumentNullException(nameof(read));

            // THE READOUT'S MEMO IS CLEARED, NOT CARRIED OVER. A second `Bind`
            // replaces the source, and a new source that happens to open on
            // the same integrity and tick as the old one's last frame would
            // otherwise leave the previous binding's text on screen.
            _drawnIntegrity = int.MinValue;
            _drawnTick = int.MinValue;

            // Drawn NOW as well as scheduled, so a bound HUD is already
            // correct before the first frame elapses - and so this is
            // provable without a live `Panel`, which a scheduled item is not.
            Refresh();

            _redraw?.Pause();
            _redraw = schedule.Execute(Refresh).Every(0);
        }

        /// One frame's redraw. Public because the scheduler is the only thing
        /// that would otherwise call it, and a scheduler needs an attached
        /// panel - so a test could never observe a second frame.
        public void Refresh()
        {
            var snapshot = _read?.Invoke();
            if (snapshot == null) return;

            // GUARDED, BECAUSE THIS IS THE ONE SCREEN THAT REPAINTS DURING
            // COMBAT. See `_drawnIntegrity`: the line is a concatenation of
            // two `ToString`s and the frame rate is not the tick rate, so an
            // unguarded write handed the collector three objects a frame for a
            // string that had not changed.
            // TWO GUARDS NOW, NOT ONE, BECAUSE THE TWO NUMBERS MOVE AT VERY
            // DIFFERENT RATES - Phase 9 Task 21e. While the tick was appended
            // to the integrity string, one `||` was right: either number
            // moving rebuilt the one Label, and the tick moves 30 times a
            // second, so the integrity string was rebuilt 30 times a second
            // too. Split, integrity is rewritten the handful of times a wave
            // that the pool actually moves (`HudSnapshot.Integrity`: "a pool,
            // not a life count") and the per-frame cost drops to the tick's
            // own single `ToString` and concat. A shared guard here would
            // have been strictly worse than before, not merely unchanged:
            // it would rebuild BOTH strings whenever EITHER moved.
            if (snapshot.Integrity != _drawnIntegrity)
            {
                _drawnIntegrity = snapshot.Integrity;
                _integrity.text = WaveHudScreen.Integrity(snapshot);
            }

            if (snapshot.Tick != _drawnTick)
            {
                _drawnTick = snapshot.Tick;
                _tick.text = WaveHudScreen.Tick(snapshot);
            }

            var bodies = snapshot.Bodies;
            var count = bodies == null ? 0 : bodies.Count;
            Attach(count);
            // `_pool[i]` IS `_bars`' child i: `Attach` only ever appends
            // `_pool[_bars.childCount]` and only ever removes from the end,
            // so the two stay index-aligned. Drawing from the pool rather
            // than `_bars.ElementAt(i)` is what makes the cached `Fill` and
            // `Tag` reachable without a per-frame query.
            for (var i = 0; i < count; i++) Draw(_pool[i], bodies[i]);
        }

        /// Exactly `count` bars attached, drawn from a pool that only ever
        /// grows. `_bars.childCount` IS the visible bar count - there are no
        /// hidden leftovers, so counting the children counts the bodies.
        void Attach(int count)
        {
            while (_bars.childCount > count) _bars.RemoveAt(_bars.childCount - 1);
            while (_bars.childCount < count)
            {
                if (_bars.childCount >= _pool.Count) _pool.Add(NewBar());
                _bars.Add(_pool[_bars.childCount].Root);
            }
        }

        static Bar NewBar()
        {
            var bar = new VisualElement { name = "bar", pickingMode = PickingMode.Ignore };
            bar.AddToClassList(BarUssClassName);

            // THE ELEVATION WRAPPER, IN SectionCard's EXACT ARRANGEMENT.
            // Theme.uss header note 2: UI Toolkit clips a background image to
            // the element's own box, so a nine-sliced drop shadow has to be
            // drawn by something LARGER than the surface it falls from - the
            // wrapper is that element, and the track is the surface inside
            // it. The horizontal negative margin 615229c added is what keeps
            // the track on the bar's own 52px instead of 6px inside it.
            //
            // EVERY ELEMENT IN THIS TREE IS PickingMode.Ignore, INCLUDING
            // THIS ONE. Rally is the player's only input during a wave and it
            // is a tap anywhere on the screen; one pickable element in a
            // full-screen overlay is a HUD that silently disables the game.
            // `WaveHud_AcceptsNoPointerEventAnywhere` sweeps every descendant
            // rather than a named list, so a new element that forgets this
            // fails rather than shipping.
            var elev = new VisualElement { name = "elev", pickingMode = PickingMode.Ignore };
            elev.AddToClassList(ElevationWrapperUssClassName);
            elev.AddToClassList(SectionCard.ElevationUssClassName);
            bar.Add(elev);

            // The track is the dark backing WaveHud drew behind every bar;
            // the fill is the proportion of it that is still alive. Two
            // elements rather than one because the tag hangs BELOW both, and
            // a background on the bar itself would paint behind the tag too.
            var track = new VisualElement { name = "track", pickingMode = PickingMode.Ignore };
            track.AddToClassList(TrackUssClassName);
            var fill = new VisualElement { name = "fill", pickingMode = PickingMode.Ignore };
            fill.AddToClassList(FillUssClassName);
            track.Add(fill);
            elev.Add(track);

            var tag = new Label { name = "tag", pickingMode = PickingMode.Ignore };
            tag.AddToClassList(TagUssClassName);
            // "RALLY 42" ends in a countdown and combat_engine section 8
            // makes Rally the player's only input during a wave, so that
            // remainder is a number a decision depends on - bible 10.6's own
            // test for this marker. The class carries --text-secondary, the
            // same 11px `.wave-hud-view__tag` used to write out itself.
            tag.AddToClassList("t-num");
            bar.Add(tag);

            // The names stay on the elements: `Q<T>("fill")` is how the tests
            // reach them, and nothing about caching the references here - or
            // about the wrapper now standing between the bar and its track -
            // changes the tree those queries walk.
            return new Bar { Root = bar, Fill = fill, Tag = tag };
        }

        void Draw(Bar pooled, BodyBar body)
        {
            var bar = pooled.Root;
            var fill = pooled.Fill;
            var tag = pooled.Tag;

            var fraction = body.MaxHp > 0 ? Mathf.Clamp01((float)body.Hp / body.MaxHp) : 0f;
            fill.style.width = Length.Percent(fraction * 100f);

            bar.EnableInClassList(CreatureUssClassName, body.Kind == BodyKind.Creature);
            bar.EnableInClassList(ChilledUssClassName, body.State == BodyState.Chilled);
            bar.EnableInClassList(BreachingUssClassName, body.State == BodyState.Breaching);
            bar.EnableInClassList(RalliedUssClassName, body.State == BodyState.Rallied);

            // The model's own sentence - `WaveHudScreen.BarTag` - not a
            // literal this view authors. Empty rather than null so the label
            // clears rather than keeping the previous body's tag when this
            // pooled element is reused.
            //
            // GUARDED ON THE TWO FIELDS THE SENTENCE IS A FUNCTION OF, for
            // `_drawnIntegrity`'s reason one level down: "RALLY 42" is a
            // concatenation, and a rallied creature was producing one per
            // frame for a countdown that moves at the tick rate. See
            // `Bar.TaggedState`.
            var state = (int)body.State;
            if (state != pooled.TaggedState || body.RallyRemaining != pooled.TaggedRally)
            {
                pooled.TaggedState = state;
                pooled.TaggedRally = body.RallyRemaining;
                tag.text = WaveHudScreen.BarTag(body) ?? string.Empty;
            }

            Place(bar, body.World);
        }

        /// World to panel, asked of the camera, and then into the bar
        /// container's own space.
        ///
        /// The lift is applied in WORLD space along `camera.transform.up`
        /// before the projection, which is the only version of this that
        /// cannot be wrong the next time the camera moves - see point 1 of
        /// the class comment.
        ///
        /// `WorldToLocal` IS NOT DECORATION. `CameraTransformWorldToPanel`
        /// answers in PANEL space, while `style.left`/`top` are relative to
        /// the bar's containing block - and the HUD's root is safe-area
        /// padded, so those two origins differ by the notch inset on exactly
        /// the devices the padding exists for. Skipping this conversion works
        /// perfectly in the Editor, where a notchless display makes the inset
        /// zero and the bug an identity, which is the same way the readout
        /// ended up under the Dynamic Island the first time.
        ///
        /// Silently skipped with no panel or no camera, which is the state a
        /// test sees. The bars still exist and still carry their fill and
        /// their tag; only their position is unknowable, and inventing one
        /// would be worse than leaving it.
        void Place(VisualElement bar, Vector3 world)
        {
            var camera = Camera;
            if (panel == null || camera == null) return;

            var lifted = world + camera.transform.up * BarLiftWorld;
            var inPanel = RuntimePanelUtils.CameraTransformWorldToPanel(panel, lifted, camera);
            var at = _bars.WorldToLocal(inPanel);

            // The chrome's own band, MEASURED rather than declared.
            // `WaveHud` reserved a constant 34px for it and its own comment
            // called the resulting agreement between two files "an unasserted
            // numeric relationship between two constants in two assemblies".
            // The element knows how tall it is; ask it.
            //
            // AND IT IS `_chrome` AND NOT `_integrity` AS OF PHASE 9 TASK 18,
            // WHICH IS THE TRAP TASK 11's OWN NOTE HERE NAMED IN ADVANCE:
            // "`layout` is relative to the parent, so the band is only
            // comparable with `_bars`' own space while the thing measured is a
            // direct child of the same root - and a wrapper would have moved
            // the measurement one level down, silently, while still returning
            // a plausible number." The readout is now the value line of a pill
            // inside a band inside `#chrome`, three levels down, so asking it
            // would have returned its offset within the pill - a small,
            // plausible number - and bars would have clamped on top of the
            // wave counter. `#chrome` is the direct child that replaced it,
            // and it is the right band on its own merits too: the thing a
            // clamped bar must not cover is all of the chrome, not the last
            // line of it.
            //
            // AND THE BAND IT RESERVES TRIPLED, WHICH IS A REAL COST AND IS
            // NAMED HERE RATHER THAN LEFT IN A REPORT. `ClampIntoFrame` takes
            // `headerBottom` as a SCALAR and applies it across the full frame
            // width, so the reserved strip went from the readout's ~46px to
            // the chrome's ~148 (8 + 72 + 6 + 62, measured on
            // `WaveHudView.png`) - and it now applies over the right
            // two-thirds of the screen, where there is no chrome at all. A bar
            // that would land in y in [46, 148) is pushed to 148 and detaches
            // from the raider it labels, which is the same class of defect the
            // safe-area inset already causes near the Ark (see
            // `ClampIntoFrame`'s own note) and is now reachable further down
            // the lane.
            //
            // WHAT IT WANTS IS `_chrome`'s WIDTH TOO. The chrome is
            // `align-items: flex-start`, so it is as wide as its widest pill
            // and no wider; a clamp that took a RECT rather than a scalar
            // would reserve only the rectangle the chrome actually occupies
            // and leave the rest of the frame at zero. That is a signature
            // change to a public static method four tests read, and no test in
            // a panel-less suite can see the difference - `Place` returns
            // early with no panel and no camera - so it is recorded for the
            // Boot.unity pass rather than made blind. Until then the
            // reservation is conservative in the direction that keeps the
            // chrome readable, which is the direction the safe-area work chose.
            var header = _chrome.layout;
            var headerBottom = float.IsNaN(header.yMax) ? 0f : header.yMax;

            var placed = ClampIntoFrame(
                new Vector2(at.x - BarWidth * 0.5f, at.y),
                _bars.contentRect,
                headerBottom,
                new Vector2(BarWidth, float.IsNaN(bar.layout.height) ? 0f : bar.layout.height));

            bar.style.left = placed.x;
            bar.style.top = placed.y;
        }

        /// Keeps a bar - and the tag hanging under it - inside the frame and
        /// clear of the integrity readout.
        ///
        /// IT FIRES IN ROUTINE PLAY ON A NOTCHED DEVICE, and the comment that
        /// used to sit here said the opposite - "with the lift taken from the
        /// camera this should never fire, because `WaveSceneBuilder` leaves
        /// 3.7% of the vertical extent above the Ark at every aspect". The
        /// framing headroom is real and it is ~31 of 844 units; `SafeAreaBinder`
        /// insets this root by ~59 at the top and ~34 at the bottom, which is
        /// larger. So bars near the Ark clamp, and a clamped bar detaches from
        /// the body it labels by up to ~55 units. Invisible in the Editor,
        /// which is why it read as unreachable for a phase.
        ///
        /// The clamp still does what it promises; it is not the defect. The
        /// root cause, if it is ever worth fixing, is that the safe-area
        /// padding insets the world-tracking `_bars` layer, which overlays a
        /// full-bleed camera and does not want to be inset - only the chrome
        /// does.
        ///
        /// It reserves the header band so that a bar which does get clamped
        /// lands below the integrity line rather than on top of the one
        /// element the safe area work was done to make readable.
        ///
        /// Static and public because it is the only arithmetic in this file a
        /// test can reach: `Place` returns early without a panel and a camera,
        /// and a bare `new WaveHudView()` has neither. Untested clamping is
        /// how the previous version's three wrong lift derivations survived.
        public static Vector2 ClampIntoFrame(Vector2 desired, Rect frame, float headerBottom, Vector2 barSize)
        {
            // No resolved layout yet. Clamping against a frame of zero or NaN
            // would stack every bar in one corner, which is worse than a bar
            // briefly off-screen on the first frame.
            if (float.IsNaN(frame.width) || float.IsNaN(frame.height) ||
                frame.width <= 0f || frame.height <= 0f) return desired;

            var right = Mathf.Max(0f, frame.width - barSize.x);
            var top = Mathf.Max(0f, headerBottom);
            var bottom = Mathf.Max(top, frame.height - barSize.y);

            return new Vector2(Mathf.Clamp(desired.x, 0f, right), Mathf.Clamp(desired.y, top, bottom));
        }
    }
}
