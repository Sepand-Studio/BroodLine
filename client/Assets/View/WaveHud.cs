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

        private void DrawIntegrity()
        {
            // Integrity is the loss condition. combat_engine section 8 makes it
            // a pool, not a life count.
            GUI.Label(new Rect(12, 8, 420, 24),
                "<b>Integrity " + Runner.Integrity + "</b>   tick " + Runner.Tick, _label);
        }

        private void DrawBars()
        {
            for (int i = 0; i < Runner.RaiderCount; i++)
            {
                if (!Runner.RaiderAlive[i]) continue;
                int max = Stats.RaiderHp(Runner.RaiderType[i]);
                var at = WorldToScreen(new Vector3(Pair.Current.RaiderTile(i) * WaveView.TileSize, 1.4f, 0f));

                // Chill is the thing wave 6 exists to teach. If it is not
                // visible the slice cannot be judged by eye.
                bool chilled = Runner.RaiderChilled[i];
                Bar(at, Runner.RaiderHp[i], max,
                    chilled ? new Color(0.45f, 0.75f, 1f) : new Color(0.85f, 0.35f, 0.3f),
                    chilled ? "CHILLED" : null);
            }

            for (int c = 0; c < Runner.CreatureCount; c++)
            {
                if (Runner.CreatureHp[c] <= 0) continue;
                int max = Stats.CreatureHp(Runner.CreatureSpecies[c]);
                var at = WorldToScreen(new Vector3(
                    Runner.Lane.PocketTiles[Runner.CreaturePocket[c]] * WaveView.TileSize,
                    1.4f, WaveView.PocketOffset));

                // Rally is the player's ONLY input. A tap with no visible
                // consequence is indistinguishable from a tap that was dropped,
                // which is exactly the failure WaveClock exists to prevent.
                bool rallied = Runner.Tick < Runner.CreatureRallyUntil[c];
                Bar(at, Runner.CreatureHp[c], max,
                    rallied ? new Color(1f, 0.85f, 0.3f) : new Color(0.4f, 0.8f, 0.45f),
                    rallied ? "RALLY " + (Runner.CreatureRallyUntil[c] - Runner.Tick) : null);
            }
        }

        private void DrawDebug()
        {
            // The entity count is shown although nothing degrades yet.
            // client_architecture section 4 keys every rung of the degradation
            // ladder off it, so surfacing it now means the ladder arrives with
            // a number already proven to be there and already deterministic.
            int entities = Runner.RaiderCount + Runner.CreatureCount;
            GUI.Label(new Rect(12, Screen.height - 76, 460, 72),
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

            GUI.Box(new Rect(Screen.width / 2f - 160, 60, 320, 220), "");
            GUI.Label(new Rect(Screen.width / 2f - 144, 72, 300, 200), text, _label);
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
