using UnityEngine;
using Broodline.Sim.Combat;
using Broodline.Creatures;

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
        private CreatureMotion[] _raiderMotion;
        private CreatureMotion[] _creatureMotion;
        private int[] _raiderMaxHp;
        private int[] _creatureMaxHp;
        private float[] _raiderLastTile;

        public void Build(SimRunner r) => Build(r, null, null);

        /// Bodies through the assembler - the recipe/prefab pipeline Tasks
        /// 7-9 built - instead of the synthetic-mesh placeholder this method
        /// used to call directly. `deployment` may be null: the one-argument
        /// `Build` above still works, drawing every creature from
        /// `r.CreatureSpecies` alone with no traits mounted. Raider bodies
        /// come from `wave.Spawns[i].Type`.
        ///
        /// NOT EVERY SPECIES OR RAIDER TYPE HAS A RECIPE YET - Task 15
        /// finishes the roster; today only Vetch and no raider at all do.
        /// `CreatureAssembler.Build` answers an unrecognised one with a
        /// magenta "missing" sphere and a Debug.LogError, which is the right
        /// answer for a body that SHOULD exist and does not - but Unity's
        /// Test Framework fails a PlayMode test on any unhandled LogError,
        /// and the standard Wave 6 deployment already carries a Loam
        /// alongside a Courser raider, neither of which has a recipe. Built
        /// straight through the assembler, every PlayMode run of this wave -
        /// including the capture test the next checkpoint runs - would log
        /// two errors and fail on them alone, every time, until Task 15.
        ///
        /// So this method checks the recipe ITSELF before handing a look to
        /// the assembler: a body with no recipe yet becomes an empty, inert
        /// placeholder - still occupies its slot, still counted by the tests
        /// that check childCount, draws nothing - and the gap is reported
        /// ONCE per Build call, as a warning rather than an error, which is
        /// loud enough to see in the log without failing a test or repeating
        /// once per body. A recipe that DOES exist but whose prefab fails to
        /// load still reaches CreatureAssembler.Build unguarded, so that
        /// failure mode - content that should be there and is not building -
        /// keeps its loud, per-body Debug.LogError. This only softens "not
        /// authored yet", never "authored but broken".
        public void Build(SimRunner r, CreatureSpec[] deployment, WaveDef wave)
        {
            LaneDressing.Build(transform, r.LaneTiles, TileSize, PocketOffset);

            var creaturesRoot = new GameObject("creatures").transform;
            creaturesRoot.SetParent(transform, false);
            _creatures = new Transform[r.CreatureCount];
            _creatureMotion = new CreatureMotion[r.CreatureCount];
            _creatureMaxHp = new int[r.CreatureCount];
            bool warnedMissingSpecies = false;
            for (int c = 0; c < r.CreatureCount; c++)
            {
                var species = r.CreatureSpecies[c].ToString();
                GameObject body;
                if (SpeciesRecipes.For(species) == null)
                {
                    if (!warnedMissingSpecies)
                    {
                        Debug.LogWarning("[view] no body recipe yet for creature species '" + species +
                            "' (there may be others on this lane) - drawing nothing for them until " +
                            "SpeciesRecipes carries it (Task 15).");
                        warnedMissingSpecies = true;
                    }
                    body = new GameObject("creature" + c + "-" + species + "-norecipe");
                }
                else
                {
                    var look = new CreatureLook { Species = species };
                    if (deployment != null && c < deployment.Length)
                    {
                        look.Trait1 = deployment[c].Trait1 == Trait.None ? null : deployment[c].Trait1.ToString();
                        look.Trait2 = deployment[c].Trait2 == Trait.None ? null : deployment[c].Trait2.ToString();
                    }
                    body = CreatureAssembler.Build(look);
                    body.name = "creature" + c + "-" + species;
                }
                body.transform.SetParent(creaturesRoot, false);
                body.transform.position = new Vector3(r.Lane.PocketTiles[r.CreaturePocket[c]] * TileSize, 0f, PocketOffset);
                body.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Euler(0f, 90f, 0f); // snout (+X) toward the lane (-Z)
                _creatures[c] = body.transform;
                _creatureMotion[c] = body.GetComponent<CreatureMotion>();
                _creatureMaxHp[c] = 0;
            }

            var raidersRoot = new GameObject("raiders").transform;
            raidersRoot.SetParent(transform, false);
            _raiders = new Transform[r.RaiderHp.Length];
            _raiderMotion = new CreatureMotion[_raiders.Length];
            _raiderMaxHp = new int[_raiders.Length];
            _raiderLastTile = new float[_raiders.Length];
            bool warnedMissingRaider = false;
            for (int i = 0; i < _raiders.Length; i++)
            {
                var type = wave != null && i < wave.Spawns.Length ? wave.Spawns[i].Type.ToString() : "courser";
                GameObject body;
                if (RaiderRecipes.For(type) == null)
                {
                    if (!warnedMissingRaider)
                    {
                        Debug.LogWarning("[view] no body recipe yet for raider type '" + type +
                            "' (there may be others on this wave) - drawing nothing for them until " +
                            "RaiderRecipes carries it (Task 15).");
                        warnedMissingRaider = true;
                    }
                    body = new GameObject("raider" + i + "-" + type + "-norecipe");
                }
                else
                {
                    body = CreatureAssembler.Build(new CreatureLook { RaiderType = type });
                    body.name = "raider" + i + "-" + type;
                }
                body.transform.SetParent(raidersRoot, false);
                body.transform.rotation = Quaternion.identity;   // raiders walk +X, snout forward
                body.SetActive(false);
                _raiders[i] = body.transform;
                _raiderMotion[i] = body.GetComponent<CreatureMotion>();
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
                    _raiderMotion[i].Hurt01 = _raiderMaxHp[i] > 0 ? 1f - (float)hp / _raiderMaxHp[i] : 0f;
                    if (hp < pair.Previous.RaiderHp(i)) _raiderMotion[i].Flinch();
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
                    _creatureMotion[c].Hurt01 = _creatureMaxHp[c] > 0 ? 1f - (float)hp / _creatureMaxHp[c] : 0f;
                    if (hp < pair.Previous.CreatureHp(c)) _creatureMotion[c].Flinch();
                }
            }
        }
    }
}
