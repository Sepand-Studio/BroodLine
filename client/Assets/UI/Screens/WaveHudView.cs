using System;
using System.Collections.Generic;
using Broodline.Model;
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
    [UxmlElement]
    public partial class WaveHudView : VisualElement
    {
        public const string UssClassName = "wave-hud-view";
        public const string BarUssClassName = "wave-hud-view__bar";
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
        readonly VisualElement _bars;

        /// Constructed once and re-attached, never re-allocated. This view
        /// redraws every frame of a scene with a defended per-frame budget;
        /// `new VisualElement()` per body per frame is exactly the garbage
        /// `WaveRunner._onTick` was hoisted out of `Update` to avoid.
        readonly List<VisualElement> _pool = new List<VisualElement>();

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

        public WaveHudView()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;

            var tree = Resources.Load<VisualTreeAsset>("WaveHudView");
            tree.CloneTree(this);

            _integrity = this.Q<Label>("integrity");
            _bars = this.Q<VisualElement>("bars");
            _integrity.pickingMode = PickingMode.Ignore;
            _bars.pickingMode = PickingMode.Ignore;
        }

        /// `read` is PULLED, once per frame, rather than pushed on a tick
        /// boundary. The simulation ticks at a fixed 30Hz and the renderer
        /// does not; a HUD updated on tick boundaries would be a second
        /// clock disagreeing with `WaveClock`'s interpolation.
        public void Bind(Func<HudSnapshot> read)
        {
            _read = read ?? throw new ArgumentNullException(nameof(read));

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

            _integrity.text = WaveHudScreen.Integrity(snapshot);

            var bodies = snapshot.Bodies;
            var count = bodies == null ? 0 : bodies.Count;
            Attach(count);
            for (var i = 0; i < count; i++) Draw(_bars.ElementAt(i), bodies[i]);
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
                _bars.Add(_pool[_bars.childCount]);
            }
        }

        static VisualElement NewBar()
        {
            var bar = new VisualElement { name = "bar", pickingMode = PickingMode.Ignore };
            bar.AddToClassList(BarUssClassName);

            // The track is the dark backing WaveHud drew behind every bar;
            // the fill is the proportion of it that is still alive. Two
            // elements rather than one because the tag hangs BELOW both, and
            // a background on the bar itself would paint behind the tag too.
            var track = new VisualElement { name = "track", pickingMode = PickingMode.Ignore };
            track.AddToClassList(TrackUssClassName);
            var fill = new VisualElement { name = "fill", pickingMode = PickingMode.Ignore };
            fill.AddToClassList(FillUssClassName);
            track.Add(fill);
            bar.Add(track);

            var tag = new Label { name = "tag", pickingMode = PickingMode.Ignore };
            tag.AddToClassList(TagUssClassName);
            bar.Add(tag);

            return bar;
        }

        void Draw(VisualElement bar, BodyBar body)
        {
            var fill = bar.Q<VisualElement>("fill");
            var tag = bar.Q<Label>("tag");

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
            tag.text = WaveHudScreen.BarTag(body) ?? string.Empty;

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

            // The integrity readout's own band, MEASURED rather than declared.
            // `WaveHud` reserved a constant 34px for it and its own comment
            // called the resulting agreement between two files "an unasserted
            // numeric relationship between two constants in two assemblies".
            // The label knows how tall it is; ask it.
            var header = _integrity.layout;
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
        /// A LAST RESORT, NOT A LAYOUT STRATEGY, which is what `WaveHud` said
        /// about its own version: with the lift taken from the camera this
        /// should never fire, because `WaveSceneBuilder` leaves 3.7% of the
        /// vertical extent above the Ark at every aspect. It exists for the
        /// cases the framing cannot promise, and it reserves the header band
        /// so that a bar which DOES get clamped lands below the integrity
        /// line rather than on top of the one element the safe area work was
        /// done to make readable.
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
