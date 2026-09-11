using UnityEngine;
using Broodline.Sim.Combat;

namespace Broodline.View
{
    /// Renders SimState and never decides anything.
    ///
    /// client_architecture section 11: "View renders simulation output and
    /// never derives it. If the view needs a number that the engine does not
    /// expose, the engine gains an accessor." Everything drawn here is read
    /// straight off SimRunner's read-only surface or off a snapshot copied
    /// from it. Nothing is computed about the game.
    public sealed class WaveView : MonoBehaviour
    {
        /// One world unit per lane tile. The lane runs along +X from spawn at
        /// tile 0 to the Ark at tile Tiles; pockets sit one unit off in +Z,
        /// which is the perpendicular offset Lane bakes into its distance table.
        public const float TileSize = 1f;
        public const float PocketOffset = 1f;

        // SyntheticCreature.Build already adds a BoneAnimator and calls
        // Bind(bones) on it. Adding a second one here gave every body an
        // unbound animator whose Update ran each frame and returned at its
        // null check - pure dispatch waste that scaled with body count.
        private static readonly SyntheticCreatureSpec BodySpec =
            new SyntheticCreatureSpec { Triangles = 7000, Bones = 24, Materials = 2 };

        private Transform[] _raiders;
        private Transform[] _creatures;

        public void Build(SimRunner r)
        {
            BuildMarker("Ark", new Color(0.85f, 0.78f, 0.45f),
                               new Vector3(r.LaneTiles * TileSize, 0f, 0f), 1.5f);

            for (int t = 0; t < r.LaneTiles; t++)
                BuildMarker("tile" + t, new Color(0.22f, 0.22f, 0.26f),
                            new Vector3(t * TileSize, -0.5f, 0f), 0.9f);

            _creatures = new Transform[r.CreatureCount];
            for (int c = 0; c < r.CreatureCount; c++)
            {
                var body = SyntheticCreature.Build(BodySpec);
                body.name = "creature" + c + "-" + r.CreatureSpecies[c];
                body.transform.position = new Vector3(
                    r.Lane.PocketTiles[r.CreaturePocket[c]] * TileSize, 0f, PocketOffset);
                _creatures[c] = body.transform;
            }

            _raiders = new Transform[r.RaiderHp.Length];
            for (int i = 0; i < _raiders.Length; i++)
            {
                var body = SyntheticCreature.Build(BodySpec);
                body.name = "raider" + i;
                body.SetActive(false);
                _raiders[i] = body.transform;
            }
        }

        /// Interpolates between the two retained snapshots. alpha is 0 at a
        /// tick boundary and approaches 1 - WaveClock.Alpha.
        public void Render(SimRunner r, WavePair pair, double alpha)
        {
            float a = Mathf.Clamp01((float)alpha);

            for (int i = 0; i < _raiders.Length; i++)
            {
                bool live = i < pair.Current.RaiderCount && pair.Current.RaiderAlive(i);
                if (_raiders[i].gameObject.activeSelf != live)
                    _raiders[i].gameObject.SetActive(live);
                if (!live) continue;

                float tile = Mathf.Lerp(pair.Previous.RaiderTile(i), pair.Current.RaiderTile(i), a);
                _raiders[i].position = new Vector3(tile * TileSize, 0f, 0f);
            }

            for (int c = 0; c < _creatures.Length; c++)
            {
                bool alive = pair.Current.CreatureHp(c) > 0;
                if (_creatures[c].gameObject.activeSelf != alive)
                    _creatures[c].gameObject.SetActive(alive);
                if (!alive) continue;

                // Skittish repositions, so the pocket is read every frame
                // rather than cached at Build.
                _creatures[c].position = new Vector3(
                    r.Lane.PocketTiles[r.CreaturePocket[c]] * TileSize, 0f, PocketOffset);
            }
        }

        private static Transform BuildMarker(string name, Color colour, Vector3 at, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = at;
            go.transform.localScale = Vector3.one * scale * 0.9f;
            go.GetComponent<Renderer>().material.color = colour;
            return go.transform;
        }
    }
}
