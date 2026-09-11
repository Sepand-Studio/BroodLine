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

        /// How far to lift a bar off the body it labels, in world units along
        /// the axis that maps to SCREEN-UP.
        ///
        /// That axis is +X, not +Y. WaveSceneBuilder frames this scene top-down
        /// with Quaternion.LookRotation(Vector3.down, Vector3.right), so world
        /// +Y runs straight down the view axis and an orthographic projection
        /// discards it entirely - a +Y offset moved every bar by exactly zero
        /// pixels and drew it dead centre on its own cube. The offset was
        /// correct for a side-on camera and was not re-derived when the camera
        /// turned portrait.
        private const float BarLift = 1.4f;

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
        /// than from the screen edge. It is not a nicety on the device this
        /// phase actually ran on: y=8 put the integrity readout under the
        /// iPhone 15 Pro's Dynamic Island.
        private Rect Safe => Screen.safeArea;

        private void DrawIntegrity()
        {
            // Integrity is the loss condition. combat_engine section 8 makes it
            // a pool, not a life count.
            GUI.Label(new Rect(Safe.xMin + 12, Safe.yMin + 8, 420, 24),
                "<b>Integrity " + Runner.Integrity + "</b>   tick " + Runner.Tick, _label);
        }

        /// ONE source per entity, and it is the snapshot pair - the same thing
        /// WaveView draws from, interpolated by the same expression.
        ///
        /// This used to mix them: existence and HP came from the live runner
        /// while position came from Pair.Current un-interpolated. Between tick
        /// boundaries the bar trailed the body it labelled, and on the
        /// terminating tick the two sources disagreed about whether the raider
        /// existed at all - the live span said dead so the bar vanished, the
        /// snapshot said alive so the body stayed. A body with no bar.
        private void DrawBars()
        {
            float a = Mathf.Clamp01((float)Clock.Alpha);
            var current = Pair.Current;
            var previous = Pair.Previous;

            for (int i = 0; i < current.RaiderCount; i++)
            {
                if (!current.RaiderAlive(i)) continue;
                int max = Stats.RaiderHp(Runner.RaiderType[i]);   // static for the wave
                float tile = WaveSnapshot.LerpTile(previous, current, i, a);
                var at = WorldToScreen(new Vector3(tile * WaveView.TileSize + BarLift, 0f, 0f));

                // Chill is the thing wave 6 exists to teach. If it is not
                // visible the slice cannot be judged by eye.
                bool chilled = current.RaiderChilled(i);
                Bar(at, current.RaiderHp(i), max,
                    chilled ? new Color(0.45f, 0.75f, 1f) : new Color(0.85f, 0.35f, 0.3f),
                    chilled ? "CHILLED" : null);
            }

            for (int c = 0; c < Runner.CreatureCount; c++)
            {
                if (current.CreatureHp(c) <= 0) continue;
                int max = Stats.CreatureHp(Runner.CreatureSpecies[c]);   // static for the wave
                var at = WorldToScreen(new Vector3(
                    Runner.Lane.PocketTiles[Runner.CreaturePocket[c]] * WaveView.TileSize + BarLift,
                    0f, WaveView.PocketOffset));

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
            int entities = Runner.RaiderCount + Runner.CreatureCount;
            GUI.Label(new Rect(Safe.xMin + 12, Safe.yMax - 76, 460, 72),
                "entities " + entities +
                "\nalpha " + Clock.Alpha.ToString("F2") +
                "   catch-up " + Clock.StepsLastFrame +
                "\nframe " + (Time.deltaTime * 1000f).ToString("F1") + " ms", _label);
        }

        private void DrawOutcome()
        {
            var o = Runner.Outcome;
            string text = "<b>" + o.Result + "</b>\nticks " + o.Ticks +
                          "\nintegrity " + o.IntegrityRemaining;

            for (int b = 0; b < o.BreachCount; b++)
            {
                // Iterate to BreachCount, never Breaches.Length - the buffer is
                // the wave's spawn capacity and a zeroed trailing entry reads
                // as "the trait was absent".
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
            float frac = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;

            var back = new Rect(at.x - w / 2f, at.y, w, h);
            GUI.DrawTexture(back, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                            new Color(0f, 0f, 0f, 0.55f), 0f, 0f);
            GUI.DrawTexture(new Rect(back.x, back.y, w * frac, h), Texture2D.whiteTexture,
                            ScaleMode.StretchToFill, false, 0f, fill, 0f, 0f);

            if (tag != null)
                GUI.Label(new Rect(at.x - w / 2f, at.y + h, 120f, 18f), tag, _label);
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
