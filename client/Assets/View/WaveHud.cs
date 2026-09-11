using UnityEngine;
using Broodline.Sim.Combat;

namespace Broodline.View
{
    /// Legible, not finished. IMGUI on purpose - this is a debug surface that
    /// the real Wave Defense screen replaces wholesale, and building it in UI
    /// Toolkit would invite it to be kept.
    ///
    /// The end panel shows the three-boolean breach diagnosis because
    /// client_architecture section 9.1 makes the diagnosis the actionable
    /// content: it names whether the trait was absent, present at insufficient
    /// coverage, or misplaced, which is what makes a loss legible rather than
    /// arbitrary. It is read off the Outcome, never recomputed.
    public sealed class WaveHud : MonoBehaviour
    {
        public SimRunner Runner;
        public WaveClock Clock;
        public WavePair Pair;
        public Camera View;

        /// How far above a body its bar sits, in WORLD units along whichever
        /// axis the camera maps to screen-up.
        ///
        /// The body is a cube at scale 0.81, so this clears it with room for
        /// the tag. The number is in world units and the CONVERSION to pixels
        /// comes from the camera - which is the whole point, and the correction
        /// of two previous attempts.
        ///
        /// As a world-space +Y offset it was a no-op: WaveSceneBuilder frames
        /// this scene top-down, so +Y runs down the view axis and an
        /// orthographic projection discards it. Re-derived as +X it ran along
        /// the LANE, pushing a raider's bar past the top of the frame near the
        /// Ark - vanishing at exactly the moment wave 6 exists to show. Then as
        /// a fraction of the safe area it worked, but only because 2.2% happens
        /// to be under the 3.7% of margin WaveSceneBuilder leaves above the
        /// Ark: an unasserted numeric relationship between two constants in two
        /// assemblies, neither of which names the other.
        ///
        /// camera.transform.up IS "which world axis maps to screen-up", by
        /// definition. Taking it from there rather than re-deriving it is the
        /// only version of this that cannot be wrong the next time the camera
        /// moves.
        private const float BarLiftWorld = 0.75f;

        /// Reserved for the integrity readout, in pixels. A bar clamped to the
        /// top of the safe area would otherwise land on it.
        private const float HeaderHeight = 34f;

        private GUIStyle _label;

        /// The safe area in GUI space, computed ONCE per OnGUI.
        ///
        /// It was a property, and a property that reads Screen.safeArea AND
        /// Screen.height on every access - evaluated twice in DrawIntegrity,
        /// twice in DrawDebug, four times in DrawOutcome and twice per drawn
        /// bar, which is 16 to 20 evaluations and 32 to 40 native calls per
        /// OnGUI. OnGUI fires at least twice per frame. Nothing in it can
        /// change mid-frame.
        private Rect _safe;

        /// Pixels per BarLiftWorld along screen-up, for the current camera.
        /// Constant across the frame for an orthographic camera.
        private float _liftPixels;

        private void OnGUI()
        {
            if (Runner == null || Pair == null) return;
            if (_label == null)
                _label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            // client_architecture section 11: "Layout is safe-area driven with
            // no fixed pixel positions." Every Rect below is offset from this.
            //
            // FLIPPED into GUI space. Screen.safeArea is pixels with the origin
            // at the BOTTOM-left; IMGUI puts it at the top-left. Used raw,
            // safeArea.yMin is the bottom inset read as a distance from the
            // top: on an iPhone 15 Pro that put the integrity readout at y=110
            // with the Dynamic Island occupying 0..177, still entirely
            // underneath it. Invisible in the Editor, where a notchless display
            // has yMin == 0 and the bug is an identity.
            var s = Screen.safeArea;
            _safe = new Rect(s.xMin, Screen.height - s.yMax, s.width, s.height);
            _liftPixels = MeasureLift();

            DrawIntegrity();
            DrawBars();
            DrawDebug();
            if (Runner.Done) DrawOutcome();
        }

        /// How many pixels BarLiftWorld is, asked of the camera.
        private float MeasureLift()
        {
            var camera = View != null ? View : Camera.main;
            if (camera == null) return 0f;

            var origin = camera.WorldToScreenPoint(Vector3.zero);
            var lifted = camera.WorldToScreenPoint(camera.transform.up * BarLiftWorld);
            return Mathf.Abs(lifted.y - origin.y);
        }

        private void DrawIntegrity()
        {
            // Integrity is the loss condition. combat_engine section 8 makes it
            // a pool, not a life count.
            GUI.Label(new Rect(_safe.xMin + 12, _safe.yMin + 8, 420, 24),
                "<b>Integrity " + Runner.Integrity + "</b>   tick " + Runner.Tick, _label);
        }

        /// ONE source per entity, and it is the snapshot pair - the same thing
        /// WaveView draws from, interpolated by the same expression, which is
        /// now literally the same method rather than a second lerp that happens
        /// to agree.
        ///
        /// This used to mix them: existence and HP came from the live runner
        /// while position came from Pair.Current un-interpolated. Between tick
        /// boundaries the bar trailed the body it labelled, and on the
        /// terminating tick the two sources disagreed about whether the raider
        /// existed at all - the live span said dead so the bar vanished, the
        /// snapshot said alive so the body stayed. A body with no bar.
        private void DrawBars()
        {
            float a = (float)Clock.Alpha;             // clamped at the clock
            var current = Pair.Current;
            var previous = Pair.Previous;

            for (int i = 0; i < current.RaiderCount; i++)
            {
                if (!current.RaiderVisible(i)) continue;
                int max = Stats.RaiderHp(Runner.RaiderType[i]);   // static for the wave
                float tile = WaveSnapshot.LerpTile(previous, current, i, a);
                var at = WorldToScreen(new Vector3(tile * WaveView.TileSize, 0f, 0f));

                // Chill is the thing wave 6 exists to teach. If it is not
                // visible the slice cannot be judged by eye. A breach outranks
                // it: that frame is the verdict.
                bool breaching = current.RaiderBreaching(i);
                bool chilled = current.RaiderChilled(i);
                Bar(at, current.RaiderHp(i), max,
                    breaching ? new Color(1f, 0.4f, 0.4f)
                    : chilled ? new Color(0.45f, 0.75f, 1f)
                              : new Color(0.85f, 0.35f, 0.3f),
                    breaching ? "BREACH" : chilled ? "CHILLED" : null);
            }

            for (int c = 0; c < current.CreatureCount; c++)
            {
                if (current.CreatureHp(c) <= 0) continue;
                int max = Stats.CreatureHp(Runner.CreatureSpecies[c]);   // static for the wave
                var at = WorldToScreen(new Vector3(
                    current.CreatureTile(c) * WaveView.TileSize, 0f, WaveView.PocketOffset));

                // Rally is the player's ONLY input. A tap with no visible
                // consequence is indistinguishable from a tap that was dropped,
                // which is exactly the failure WaveClock exists to prevent.
                // The countdown is read from the engine rather than subtracted
                // here - the view renders and never derives.
                int rally = Runner.CreatureRallyRemaining(c);
                Bar(at, current.CreatureHp(c), max,
                    rally > 0 ? new Color(1f, 0.85f, 0.3f) : new Color(0.4f, 0.8f, 0.45f),
                    rally > 0 ? "RALLY " + rally : null);
            }
        }

        private void DrawDebug()
        {
            // The entity count is shown although nothing degrades yet.
            // client_architecture section 4 keys every rung of the degradation
            // ladder off it, so surfacing it now means the ladder arrives with
            // a number already proven to be there and already deterministic.
            //
            // Counted from the SNAPSHOT, which is what the renderers draw from.
            // Summing RaiderCount + CreatureCount counted corpses; reading
            // SimRunner.AliveRaiderCount fixed that and introduced a subtler
            // one, because a breaching raider is drawn and is not alive - so on
            // the breach frame the readout said one fewer than was on screen.
            //
            // Backlog, not StepsLastFrame. Steps saturates at MaxCatchUpSteps
            // by construction, so it can show 8 and never the 22 or 352 ticks
            // of real debt that sustained overload produces.
            var current = Pair.Current;
            int entities = current.VisibleRaiderCount + current.LiveCreatureCount;

            GUI.Label(new Rect(_safe.xMin + 12, _safe.yMax - 76, 460, 72),
                "entities " + entities +
                "\nalpha " + Clock.Alpha.ToString("F2") +
                "   catch-up " + Clock.StepsLastFrame +
                "   backlog " + Clock.BacklogTicks.ToString("F1") +
                "\nframe " + (Time.deltaTime * 1000f).ToString("F1") + " ms", _label);
        }

        private void DrawOutcome()
        {
            var o = Runner.Outcome;
            string text = "<b>" + o.Result + "</b>\nticks " + o.Ticks +
                          "\nintegrity " + o.IntegrityRemaining;

            // Breaches is a ReadOnlySpan bounded at the breaches recorded, so
            // its Length IS the count. The old warning - iterate to
            // BreachCount, never Length, or a zeroed trailing entry reads as
            // "the trait was absent" - describes a buffer this no longer
            // receives.
            for (int b = 0; b < o.Breaches.Length; b++)
            {
                var br = o.Breaches[b];
                text += "\n\n<b>breach</b> " + br.Type + " at tick " + br.Tick +
                        "\n  access    " + br.Access +
                        "\n  coverage  " + br.Coverage +
                        "\n  placement " + br.Placement;
            }

            GUI.Box(new Rect(_safe.center.x - 160, _safe.yMin + 60, 320, 220), "");
            GUI.Label(new Rect(_safe.center.x - 144, _safe.yMin + 72, 300, 200), text, _label);
        }

        private void Bar(Vector2 at, int hp, int max, Color fill, string tag)
        {
            const float w = 52f, h = 6f;
            const float tagHeight = 18f;
            float frac = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;

            var back = ClampIntoSafeArea(
                new Rect(at.x - w / 2f, at.y - _liftPixels, w, h),
                tag != null ? tagHeight : 0f);

            GUI.DrawTexture(back, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                            new Color(0f, 0f, 0f, 0.55f), 0f, 0f);
            GUI.DrawTexture(new Rect(back.x, back.y, w * frac, h), Texture2D.whiteTexture,
                            ScaleMode.StretchToFill, false, 0f, fill, 0f, 0f);

            if (tag != null)
                GUI.Label(new Rect(back.x, back.y + h, 120f, tagHeight), tag, _label);
        }

        /// Keeps a bar - and the tag hanging under it - inside the safe area
        /// and clear of the integrity readout.
        ///
        /// A last resort, not a layout strategy. With the lift taken from the
        /// camera this should never fire: WaveSceneBuilder leaves 3.7% of the
        /// vertical extent above the Ark at every aspect, which is more than
        /// the lift. It exists for the cases the framing cannot promise - no
        /// camera at all, where WorldToScreen answers zero - and it reserves
        /// HeaderHeight so that a bar which DOES get clamped lands below the
        /// integrity line rather than on top of the one element the safe-area
        /// work was done to make readable.
        private Rect ClampIntoSafeArea(Rect r, float extraBelow)
        {
            float top = _safe.yMin + HeaderHeight;
            float bottom = _safe.yMax - r.height - extraBelow;

            r.x = Mathf.Clamp(r.x, _safe.xMin, Mathf.Max(_safe.xMin, _safe.xMax - r.width));
            r.y = Mathf.Clamp(r.y, top, Mathf.Max(top, bottom));
            return r;
        }

        private Vector2 WorldToScreen(Vector3 world)
        {
            var camera = View != null ? View : Camera.main;
            if (camera == null) return Vector2.zero;
            var p = camera.WorldToScreenPoint(world);
            return new Vector2(p.x, Screen.height - p.y);
        }
    }
}
