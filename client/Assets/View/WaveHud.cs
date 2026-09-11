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

        /// How far to lift a bar above the body it labels, as a fraction of the
        /// safe area's height.
        ///
        /// SCREEN space, and that is the fix rather than the number. This was a
        /// world-space offset twice, and was wrong both times. On +Y it was a
        /// no-op: WaveSceneBuilder frames the scene top-down with
        /// Quaternion.LookRotation(Vector3.down, Vector3.right), so world +Y
        /// runs down the view axis and an orthographic projection discards it -
        /// every bar drew dead centre on its own cube. Moving it to +X made the
        /// direction right and the result worse: +X runs along the LANE, the
        /// camera covers world-X [-0.96, 24.96], and the Ark is at 24, so a
        /// raider past tile ~23.6 had its bar pushed off the top of the screen -
        /// vanishing for the last several ticks of the Courser's approach, which
        /// is exactly the moment wave 6 exists to show.
        ///
        /// A lift is a screen-space idea. Saying it in world units required
        /// knowing which world axis maps to screen-up, and that knowledge is
        /// what was wrong on both attempts. ClampIntoSafeArea below is the
        /// belt to this braces: no anchor, however derived, leaves the frame.
        private const float BarLiftFraction = 0.022f;

        private GUIStyle _label;

        private void OnGUI()
        {
            if (Runner == null || Pair == null) return;
            if (_label == null)
                _label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            DrawIntegrity();
            DrawBars();
            DrawDebug();
            if (Runner.Done) DrawOutcome();
        }

        /// client_architecture section 11: "Layout is safe-area driven with no
        /// fixed pixel positions." Every Rect below is offset from this rather
        /// than from the screen edge.
        ///
        /// FLIPPED into GUI space, which is the whole point of the property.
        /// Screen.safeArea is pixels with the origin at the BOTTOM-left; IMGUI
        /// puts it at the top-left. Used raw, safeArea.yMin is the bottom inset
        /// being read as a distance from the top: on an iPhone 15 Pro that put
        /// the integrity readout at y=110 with the Dynamic Island occupying
        /// 0..177, still entirely underneath it - the exact symptom the
        /// safe-area work was done to fix. Invisible in the Editor, where a
        /// notchless display has yMin == 0 and the bug is an identity.
        private static Rect Safe
        {
            get
            {
                var s = Screen.safeArea;
                return new Rect(s.xMin, Screen.height - s.yMax, s.width, s.height);
            }
        }

        private void DrawIntegrity()
        {
            // Integrity is the loss condition. combat_engine section 8 makes it
            // a pool, not a life count.
            GUI.Label(new Rect(Safe.xMin + 12, Safe.yMin + 8, 420, 24),
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
            // Read from the engine, not summed here. RaiderCount is the number
            // SPAWNED, so the sum this used to compute counted corpses and
            // never went down - a ladder keyed off it would degrade on an empty
            // board.
            int entities = Runner.AliveRaiderCount + Runner.AliveCreatureCount;

            // Backlog, not StepsLastFrame. Steps saturates at MaxCatchUpSteps
            // by construction, so it can show 8 and never the 22 or 352 ticks
            // of real debt that sustained overload produces - which is the
            // number worth seeing.
            GUI.Label(new Rect(Safe.xMin + 12, Safe.yMax - 76, 460, 72),
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

            // Breaches is a ReadOnlySpan bounded at BreachCount, so its Length
            // IS the count. The old warning - iterate to BreachCount, never
            // Length, or a zeroed trailing entry reads as "the trait was
            // absent" - describes a buffer this no longer receives.
            for (int b = 0; b < o.Breaches.Length; b++)
            {
                var br = o.Breaches[b];
                text += "\n\n<b>breach</b> " + br.Type + " at tick " + br.Tick +
                        "\n  access    " + br.Access +
                        "\n  coverage  " + br.Coverage +
                        "\n  placement " + br.Placement;
            }

            GUI.Box(new Rect(Safe.center.x - 160, Safe.yMin + 60, 320, 220), "");
            GUI.Label(new Rect(Safe.center.x - 144, Safe.yMin + 72, 300, 200), text, _label);
        }

        private void Bar(Vector2 at, int hp, int max, Color fill, string tag)
        {
            const float w = 52f, h = 6f;
            const float tagHeight = 18f;
            float frac = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;

            var back = ClampIntoSafeArea(
                new Rect(at.x - w / 2f, at.y - Safe.height * BarLiftFraction, w, h),
                tag != null ? tagHeight : 0f);

            GUI.DrawTexture(back, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                            new Color(0f, 0f, 0f, 0.55f), 0f, 0f);
            GUI.DrawTexture(new Rect(back.x, back.y, w * frac, h), Texture2D.whiteTexture,
                            ScaleMode.StretchToFill, false, 0f, fill, 0f, 0f);

            if (tag != null)
                GUI.Label(new Rect(back.x, back.y + h, 120f, tagHeight), tag, _label);
        }

        /// Keeps a bar - and the tag hanging under it - inside the safe area.
        ///
        /// The lift is small and the clamp rarely fires, which is the point:
        /// it is not a layout strategy, it is the guarantee that no bar can
        /// leave the frame at the moment it matters most. The Courser's bar
        /// disappearing at the Ark is what this exists to make impossible, and
        /// it is impossible by construction rather than by the offset happening
        /// to be small enough.
        private static Rect ClampIntoSafeArea(Rect r, float extraBelow)
        {
            var safe = Safe;
            r.x = Mathf.Clamp(r.x, safe.xMin, Mathf.Max(safe.xMin, safe.xMax - r.width));
            r.y = Mathf.Clamp(r.y, safe.yMin,
                              Mathf.Max(safe.yMin, safe.yMax - r.height - extraBelow));
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
