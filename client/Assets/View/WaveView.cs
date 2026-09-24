using UnityEngine;
using Broodline.Sim.Combat;
using Broodline.Frontier;

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

        private Transform[] _raiders;
        private Transform[] _creatures;
        private FrontierCreature[] _raiderMotion;
        private FrontierCreature[] _creatureMotion;
        private int[] _raiderMaxHp;
        private int[] _creatureMaxHp;
        private float[] _raiderLastTile;
        private int[] _creatureLastTile;
        private FrontierArt _art;

        public void Build(SimRunner r) => Build(r, null, null);

        /// Draws the sim's roster through the shared Frontier art core. Unknown
        /// future ids keep their slot but do not interrupt the wave.
        public void Build(SimRunner r, CreatureSpec[] deployment, WaveDef wave)
        {
            LaneDressing.Build(transform, r.LaneTiles, TileSize, PocketOffset);
            _art = new FrontierArt(RuntimeShaders.Require(RuntimeShaders.Frontier));

            var creaturesRoot = new GameObject("creatures").transform;
            creaturesRoot.SetParent(transform, false);
            _creatures = new Transform[r.CreatureCount];
            _creatureMotion = new FrontierCreature[r.CreatureCount];
            _creatureMaxHp = new int[r.CreatureCount];
            _creatureLastTile = new int[r.CreatureCount];
            bool warnedMissingSpecies = false;
            for (int c = 0; c < r.CreatureCount; c++)
            {
                var species = r.CreatureSpecies[c].ToString();
                FrontierCreature creature = null;
                GameObject body;
                if (!FrontierVisuals.Has(species))
                {
                    if (!warnedMissingSpecies)
                    {
                        Debug.LogWarning("[view] no Frontier body for species '" + species + "'; unknown bodies are hidden.");
                        warnedMissingSpecies = true;
                    }
                    body = new GameObject("creature" + c + "-" + species + "-unavailable");
                    body.transform.SetParent(creaturesRoot, false);
                }
                else
                {
                    string first = null, second = null;
                    if (deployment != null && c < deployment.Length)
                    {
                        first = deployment[c].Trait1 == Trait.None ? null : deployment[c].Trait1.ToString();
                        second = deployment[c].Trait2 == Trait.None ? null : deployment[c].Trait2.ToString();
                    }
                    creature = _art.Creature(creaturesRoot, species, first, second, c * .37f);
                    body = creature.gameObject;
                    body.name = "creature" + c + "-" + species;
                }
                var tile = r.Lane.PocketTiles[r.CreaturePocket[c]];
                body.transform.position = new Vector3(tile * TileSize, 0f, PocketOffset);
                body.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
                _creatures[c] = body.transform;
                _creatureMotion[c] = creature;
                _creatureLastTile[c] = tile;
            }

            var raidersRoot = new GameObject("raiders").transform;
            raidersRoot.SetParent(transform, false);
            _raiders = new Transform[r.RaiderHp.Length];
            _raiderMotion = new FrontierCreature[_raiders.Length];
            _raiderMaxHp = new int[_raiders.Length];
            _raiderLastTile = new float[_raiders.Length];
            bool warnedMissingRaider = false;
            for (int i = 0; i < _raiders.Length; i++)
            {
                var type = wave != null && i < wave.Spawns.Length ? wave.Spawns[i].Type.ToString() : "courser";
                FrontierCreature raider = null;
                GameObject body;
                if (!FrontierVisuals.Has(type))
                {
                    if (!warnedMissingRaider)
                    {
                        Debug.LogWarning("[view] no Frontier body for raider '" + type + "'; unknown bodies are hidden.");
                        warnedMissingRaider = true;
                    }
                    body = new GameObject("raider" + i + "-" + type + "-unavailable");
                    body.transform.SetParent(raidersRoot, false);
                }
                else
                {
                    raider = _art.Creature(raidersRoot, type, phase: i * .23f);
                    body = raider.gameObject;
                    body.name = "raider" + i + "-" + type;
                }
                body.transform.rotation = Quaternion.identity;
                body.SetActive(false);
                _raiders[i] = body.transform;
                _raiderMotion[i] = raider;
            }
        }

        /// Interpolates between the two retained snapshots. alpha is 0 at a
        /// tick boundary and approaches 1 - WaveClock.Alpha, which is clamped
        /// where it is produced, so it is not re-clamped here. Three copies of
        /// that clamp existed while the class comment on Alpha claimed it had
        /// moved to one.
        ///
        /// No SimRunner parameter. Everything drawn comes from the pair, which
        /// is what "one source per entity" has to mean to be worth asserting:
        /// creature position used to be read live from the runner while
        /// everything else came from the snapshot.
        public void Render(WavePair pair, double alpha)
        {
            float a = (float)alpha;
            var current = pair.Current;

            for (int i = 0; i < _raiders.Length; i++)
            {
                // Visible, not alive. A raider that reached the Ark is cleared
                // by Phases.Breach on the same tick it takes the integrity, so
                // gating on aliveness drew the breach as a disappearance.
                bool live = current.RaiderVisible(i);
                if (_raiders[i].gameObject.activeSelf != live)
                    _raiders[i].gameObject.SetActive(live);
                if (!live) continue;

                float tile = WaveSnapshot.LerpTile(pair.Previous, current, i, a);
                _raiders[i].position = new Vector3(tile * TileSize, 0f, 0f);

                if (_raiderMotion[i] != null)
                {
                    int hp = current.RaiderHp(i);
                    if (_raiderMaxHp[i] == 0) _raiderMaxHp[i] = hp;
                    _raiderMotion[i].Moving = !Mathf.Approximately(tile, _raiderLastTile[i]);
                    _raiderMotion[i].Hurt = _raiderMaxHp[i] > 0 ? 1f - (float)hp / _raiderMaxHp[i] : 0f;
                    _raiderMotion[i].Chilled = current.RaiderChilled(i);
                    if (hp < pair.Previous.RaiderHp(i)) _raiderMotion[i].Hit();
                    _raiderLastTile[i] = tile;
                }
            }

            for (int c = 0; c < _creatures.Length; c++)
            {
                bool alive = current.CreatureHp(c) > 0;
                if (_creatures[c].gameObject.activeSelf != alive)
                    _creatures[c].gameObject.SetActive(alive);
                if (!alive) continue;

                _creatures[c].position = new Vector3(
                    current.CreatureTile(c) * TileSize, 0f, PocketOffset);

                if (_creatureMotion[c] != null)
                {
                    int hp = current.CreatureHp(c);
                    if (_creatureMaxHp[c] == 0) _creatureMaxHp[c] = hp;
                    _creatureMotion[c].Hurt = _creatureMaxHp[c] > 0 ? 1f - (float)hp / _creatureMaxHp[c] : 0f;
                    var tile = current.CreatureTile(c);
                    _creatureMotion[c].Moving = tile != _creatureLastTile[c];
                    if (hp < pair.Previous.CreatureHp(c)) _creatureMotion[c].Hit();
                    _creatureLastTile[c] = tile;
                }
            }
        }

        public void SetPaused(bool paused)
        {
            if (_creatureMotion != null)
                foreach (var creature in _creatureMotion) if (creature != null) creature.Paused = paused;
            if (_raiderMotion != null)
                foreach (var raider in _raiderMotion) if (raider != null) raider.Paused = paused;
        }

        public void SetSpeed(float scale)
        {
            if (_creatureMotion != null)
                foreach (var creature in _creatureMotion) if (creature != null) creature.TimeScale = scale;
            if (_raiderMotion != null)
                foreach (var raider in _raiderMotion) if (raider != null) raider.TimeScale = scale;
        }

        void OnDestroy() => _art?.Dispose();
    }
}
