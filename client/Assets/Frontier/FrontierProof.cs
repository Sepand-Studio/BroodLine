using System;
using Broodline.Game.Shell;
using Broodline.Sim.Combat;
using Broodline.View;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.Frontier
{
    /// Standalone, offline presentation proof. Reads the existing simulation;
    /// never writes inventory, saves, progression, or production scene assets.
    [RequireComponent(typeof(UIDocument))]
    public sealed class FrontierProof : MonoBehaviour
    {
        [SerializeField] Shader surfaceShader;
        [SerializeField] StyleSheet styleSheet;
        [SerializeField] Camera stageCamera;

        enum Page { Founder, Deploy, Battle, Reveal }
        Page _page;
        VisualElement _root, _screen, _stage, _overlays, _card, _roster;
        Image _image;
        Label _status, _detail, _notice, _arkLabel;
        Button _primary, _pause, _rally;
        readonly FrontierHealthBadge[] _health = new FrontierHealthBadge[3];
        FrontierHealthBadge[] _raiderHealth;
        Button[] _pocketButtons;
        FrontierFloatingCues _cues;
        FrontierBattleFeedback _feedback;
        readonly int[] _maxHp = new int[3];
        SafeAreaBinder _safeArea;
        RenderTexture _texture;
        FrontierArt _art;
        Transform _world;
        FrontierCreature _hero;
        FrontierCreature[] _defenders, _raiders;
        SimRunner _runner;
        WaveClock _clock;
        WavePair _pair;
        readonly int[] _pockets = { 0, 2, 4 };
        int _wave = 7, _selected;
        bool _paused, _reducedMotion, _resultShown;
        string _founder = "Pebble";
        string _heroName;
        static readonly string[] Bodies = { "vetch", "ember", "pale" };
        static readonly string[] First = { "taunt", "splash", "chill" };
        static readonly string[] Second = { "carapace", "carapace", "taunt" };

        public void Configure(Shader shader, StyleSheet sheet, Camera camera)
        { surfaceShader = shader; styleSheet = sheet; stageCamera = camera; }

        void Start()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            if (surfaceShader == null || styleSheet == null || stageCamera == null)
            {
                Debug.LogError("Frontier proof is missing its scene dependencies. Rebuild with Broodline > Frontier Proof > Open.");
                _root.Add(new Label("Rebuild the scene with Broodline > Frontier Proof > Open."));
                enabled = false; return;
            }
            _root.styleSheets.Add(styleSheet);
            _root.AddToClassList("frontier-proof");
            _safeArea = SafeAreaBinder.ForRuntimePanel(_root);
            Show(Page.Founder);
        }

        void ClearWorld()
        {
            if (_world != null) { _world.gameObject.SetActive(false); Destroy(_world.gameObject); }
            _art?.Dispose(); _art = null;
            _runner = null; _hero = null; _defenders = null; _raiders = null;
            _feedback = null; _cues = null; _pocketButtons = null; _raiderHealth = null; _arkLabel = null;
        }

        void Show(Page page)
        {
            ClearWorld();
            _page = page; _paused = false; _resultShown = false;
            _root.Clear(); _status = null; _pause = null; _rally = null; _primary = null;
            _art = new FrontierArt(surfaceShader);
            _world = new GameObject("Frontier stage").transform;
            _world.SetParent(transform, false);
            _screen = Element(_root, "proof-screen");
            var header = Element(_screen, "header");
            var wordmark = Element(header, "wordmark");
            wordmark.Add(new FrontierIcon(FrontierIcon.Symbol.Leaf));
            Text(wordmark, "BROODLINE", "brand");
            Text(header, "THE LIVING FRONTIER", "eyebrow");
            Text(_screen, page == Page.Founder ? "A small beginning." : page == Page.Reveal ? "Something wonderful." : "Hold the frontier.", "page-title");

            _stage = Element(_screen, "stage");
            _image = new Image { scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            _image.AddToClassList("stage-image"); _stage.Add(_image);
            _overlays = Element(_stage, "stage-overlays"); _overlays.pickingMode = PickingMode.Ignore;
            _status = Text(_stage, page == Page.Founder ? "FOUNDING GENERATION" : page == Page.Reveal ? "GENERATION 02  /  ART PREVIEW" : "VERDANT DEFILE", "stage-badge");
            _notice = Text(_stage, "", "stage-notice"); _notice.pickingMode = PickingMode.Ignore;
            _notice.style.display = DisplayStyle.None;
            _stage.RegisterCallback<GeometryChangedEvent>(_ => ResizeStage());
            if (page == Page.Founder || page == Page.Reveal)
            {
                _stage.focusable = true;
                _stage.tooltip = "Tap or press Enter to say hello";
                _stage.RegisterCallback<ClickEvent>(_ => GreetHero());
                _stage.RegisterCallback<NavigationSubmitEvent>(e => { GreetHero(); e.StopPropagation(); });
                _stage.RegisterCallback<PointerMoveEvent>(e =>
                {
                    if (_hero == null || _stage.contentRect.width <= 0) return;
                    _hero.LookYaw = Mathf.Clamp((e.localPosition.x / _stage.contentRect.width - .5f) * -28, -14, 14);
                });
                _stage.RegisterCallback<PointerLeaveEvent>(_ => { if (_hero != null) _hero.LookYaw = 0; });
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("card-scroll"); _screen.Add(scroll);
            _card = Element(scroll.contentContainer, "paper-card");
            if (page == Page.Founder) Founder();
            else if (page == Page.Reveal) Reveal();
            else Battlefield(page == Page.Battle);

            var footer = Element(_screen, "footer");
            var motion = new Toggle("Reduced motion") { value = _reducedMotion };
            motion.RegisterValueChangedCallback(e => { _reducedMotion = e.newValue; ApplyMotion(); });
            footer.Add(motion);
            Text(footer, "OFFLINE ART PROOF", "proof-note");
            SetLayer(_world, 6);
            ApplyMotion(); ResizeStage();
        }

        void Founder()
        {
            _art.Environment(_world, 0, Array.Empty<int>(), true);
            _hero = _art.Creature(_world, "vetch");
            _heroName = _founder;
            Notice("Tap your companion to say hello");
            Text(_card, "Meet your first founder", "eyebrow");
            Text(_card, "Every lineage starts with a friend.", "card-title");
            Text(_card, "A curious Vetch. Steady feet, a brave heart, and a whole world ahead.", "body-copy");
            var name = new TextField("Give them a name") { value = _founder, maxLength = 20 };
            name.AddToClassList("name-field"); _card.Add(name);
            var turn = new Slider("Look around", -80, 80);
            turn.RegisterValueChangedCallback(e => _hero.transform.localRotation = Quaternion.Euler(0, e.newValue, 0)); _card.Add(turn);
            _primary = Action(_card, "Begin our journey", () => { _founder = name.value.Trim(); Show(Page.Deploy); }, true);
            name.RegisterValueChangedCallback(e =>
            {
                _primary.SetEnabled(!string.IsNullOrWhiteSpace(e.newValue));
                _heroName = string.IsNullOrWhiteSpace(e.newValue) ? "Your Vetch" : e.newValue.Trim();
            });
            _primary.SetEnabled(!string.IsNullOrWhiteSpace(name.value));
        }

        void Battlefield(bool playing)
        {
            var wave = WaveDef.ForId(_wave);
            var tiles = new int[wave.Lane.PocketCount];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = wave.Lane.PocketTiles[i];
            _art.Environment(_world, wave.Lane.Tiles, tiles, false);
            _defenders = new FrontierCreature[3];
            for (int i = 0; i < 3; i++)
            {
                _defenders[i] = _art.Creature(_world, Bodies[i], First[i], Second[i], i * .7f);
                _defenders[i].transform.localPosition = new Vector3(tiles[_pockets[i]], .05f, 1.5f);
                _defenders[i].transform.localRotation = Quaternion.Euler(0, 90, 0);
                _health[i] = new FrontierHealthBadge(); _overlays.Add(_health[i]);
                _maxHp[i] = Stats.CreatureHp(FrontierFormation.Create(_pockets)[i].Species);
                _health[i].SetHealth(_maxHp[i], _maxHp[i]);
            }
            _arkLabel = Text(_overlays, "ARK", "ark-label"); _arkLabel.pickingMode = PickingMode.Ignore;
            if (!playing)
            {
                _pocketButtons = new Button[tiles.Length];
                for (int p = 0; p < tiles.Length; p++)
                {
                    int pocket = p;
                    _pocketButtons[p] = Action(_overlays, (p + 1).ToString(), () => PlaceCompanion(pocket));
                    _pocketButtons[p].AddToClassList("pocket-button");
                    _pocketButtons[p].tooltip = "Place the selected companion here; occupied stones swap companions";
                }
            }
            Text(_card, playing ? "DEFEND THE ARK" : "CHOOSE YOUR FORMATION", "eyebrow");
            _detail = Text(_card, playing ? "Your creatures attack automatically. Rally one when it counts." : "Three companions. One home to protect.", "body-copy");
            var roster = Element(_card, "roster");
            _roster = roster;
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var button = Action(roster, (i == 0 ? _founder : i == 1 ? "Ember" : "Pale") + "\n" + First[i] + " / " + Second[i], () =>
                {
                    _selected = index;
                    UpdateSelection(roster);
                });
                button.AddToClassList("roster-card");
                button.Insert(0, new FrontierIcon(i == 0 ? FrontierIcon.Symbol.Shield : i == 1 ? FrontierIcon.Symbol.Flame : FrontierIcon.Symbol.Wing));
                button.tooltip = playing ? "Select this companion for Rally" : "Select this companion, then choose a numbered stone";
            }
            UpdateSelection(roster);
            if (!playing)
            {
                Text(_card, "Select a companion, then tap a numbered stone. Occupied stones swap companions.", "caption");
                var row = Element(_card, "button-row");
                Action(row, _wave == 7 ? "Mixed raiders • selected" : "Mixed raiders", () => { _wave = 7; Show(Page.Deploy); });
                Action(row, _wave == 6 ? "Courser • selected" : "Courser", () => { _wave = 6; Show(Page.Deploy); });
                _primary = Action(_card, "Protect the Ark", () => Show(Page.Battle), true);
                Action(_card, "Preview Cinderplate", () => Show(Page.Reveal)).AddToClassList("text-button");
                return;
            }
            _runner = new SimRunner(wave, wave.Lane, FrontierFormation.Create(_pockets), 6);
            _clock = new WaveClock(); _pair = new WavePair(_runner);
            _feedback = new FrontierBattleFeedback(_runner);
            for (int i = 0; i < _maxHp.Length; i++) _maxHp[i] = _runner.CreatureHp[i];
            _raiders = new FrontierCreature[wave.Spawns.Length];
            _raiderHealth = new FrontierHealthBadge[_raiders.Length];
            for (int i = 0; i < _raiders.Length; i++)
            {
                string id = wave.Spawns[i].Type.ToString().ToLowerInvariant();
                _raiders[i] = _art.Creature(_world, id, phase: i * .37f);
                _raiders[i].transform.localScale = Vector3.one * (id == "skirmisher" ? .55f : .8f);
                _raiders[i].Moving = true; _raiders[i].gameObject.SetActive(false);
                _raiderHealth[i] = new FrontierHealthBadge(); _raiderHealth[i].AddToClassList("enemy-health");
                _raiderHealth[i].style.display = DisplayStyle.None; _overlays.Add(_raiderHealth[i]);
            }
            _cues = new FrontierFloatingCues(_overlays);
            var controls = Element(_card, "button-row");
            _pause = Action(controls, "Pause", () => { _paused = !_paused; _pause.text = _paused ? "Resume" : "Pause"; ApplyMotion(); });
            _rally = Action(controls, "Rally " + CompanionName(_selected), () => _clock.RequestRally(_selected), true);
            _primary = Action(_card, "Meet Cinderplate", () => Show(Page.Reveal), true);
            _primary.style.display = DisplayStyle.None;
            Action(_card, "Back to formation", () => Show(Page.Deploy)).AddToClassList("text-button");
        }

        string CompanionName(int index) => index == 0 ? _founder : index == 1 ? "Ember" : "Pale";

        void UpdateSelection(VisualElement roster)
        {
            for (int i = 0; i < roster.childCount; i++) roster[i].EnableInClassList("selected", i == _selected);
            for (int i = 0; i < _health.Length; i++) _health[i].EnableInClassList("selected-companion", i == _selected);
            if (_pocketButtons != null)
                for (int i = 0; i < _pocketButtons.Length; i++)
                {
                    _pocketButtons[i].EnableInClassList("selected", i == _pockets[_selected]);
                    _pocketButtons[i].EnableInClassList("occupied", Array.IndexOf(_pockets, i) >= 0);
                }
            if (_page == Page.Deploy) Notice(CompanionName(_selected) + " selected · choose a stone");
            if (_rally != null && _runner != null && !_runner.RallyUsed) _rally.text = "Rally " + CompanionName(_selected);
        }

        void PlaceCompanion(int pocket)
        {
            if (_page != Page.Deploy || !FrontierFormation.Place(_pockets, _selected, pocket)) return;
            var lane = WaveDef.ForId(_wave).Lane;
            for (int i = 0; i < _defenders.Length; i++)
            {
                _defenders[i].transform.localPosition = new Vector3(lane.PocketTiles[_pockets[i]], .05f, 1.5f);
            }
            UpdateSelection(_roster);
            Notice(CompanionName(_selected) + " moved to stone " + (pocket + 1));
        }

        void Reveal()
        {
            _art.Environment(_world, 0, Array.Empty<int>(), true);
            _hero = _art.Creature(_world, "vetch", "cinder", "carapace");
            _heroName = "Cinderplate";
            _hero.Greet();
            Notice("A familiar face, a new look · tap to say hello");
            Text(_card, "A NEW BRANCH OF THE FAMILY", "eyebrow");
            Text(_card, "Cinderplate", "card-title");
            Text(_card, "Vetch body  ·  Cinder dorsal  ·  Carapace flank", "body-copy");
            var traits = Element(_card, "button-row");
            var fire = Element(traits, "trait-chip"); fire.Add(new FrontierIcon(FrontierIcon.Symbol.Flame)); Text(fire, "Cinder", "caption");
            var shield = Element(traits, "trait-chip"); shield.Add(new FrontierIcon(FrontierIcon.Symbol.Shield)); Text(shield, "Carapace", "caption");
            var growth = new Slider("Growth preview", 0, 1);
            growth.RegisterValueChangedCallback(e => _hero.Growth = e.newValue); _card.Add(growth);
            var turn = new Slider("Turn your creature", -100, 100);
            turn.RegisterValueChangedCallback(e => _hero.transform.localRotation = Quaternion.Euler(0, e.newValue, 0)); _card.Add(turn);
            var inspect = new Foldout { text = "Compare appearances", value = false }; _card.Add(inspect);
            var comparison = Text(inspect, "Viewing Cinderplate · combined appearance", "caption");
            var choices = Element(inspect, "button-row");
            foreach (string appearance in new[] { "Cinderplate", "Vetch", "Ember", "Pale" })
            {
                string selected = appearance;
                Action(choices, appearance, () =>
                {
                    _hero.gameObject.SetActive(false); Destroy(_hero.gameObject);
                    bool hybrid = selected == "Cinderplate";
                    _hero = _art.Creature(_world, hybrid ? "vetch" : selected.ToLowerInvariant(), hybrid ? "cinder" : null, hybrid ? "carapace" : null);
                    _heroName = selected; _hero.Greet();
                    _hero.Growth = growth.value; _hero.transform.localRotation = Quaternion.Euler(0, turn.value, 0);
                    SetLayer(_hero.transform, 6); ApplyMotion();
                    comparison.text = "Viewing " + selected + (hybrid ? " · combined appearance" : " · base species reference");
                    _status.text = selected.ToUpperInvariant() + "  /  ART PREVIEW";
                    Notice("Tap " + selected + " to say hello");
                });
            }
            Text(_card, "An appearance preview. Cinder combat and breeding rewards are not part of this proof.", "caption");
            _primary = Action(_card, "Back to the frontier", () => Show(Page.Deploy), true);
            Action(_card, "Meet your founder again", () => Show(Page.Founder)).AddToClassList("text-button");
        }

        void OnTick()
        {
            _pair.Advance(_runner);
            _feedback.Observe(_runner, ReceiveCue);
        }

        void ReceiveCue(FrontierCue cue)
        {
            Vector3 local = cue.Defender ? _defenders[cue.Entity].transform.localPosition : new Vector3((float)(cue.ProgressRaw / 4294967296.0), 0, 0);
            Vector3 position = _world.TransformPoint(local + Vector3.up);
            switch (cue.Kind)
            {
                case FrontierCueKind.Attack: _defenders[cue.Entity].Attack(); break;
                case FrontierCueKind.Damage:
                    (cue.Defender ? _defenders[cue.Entity] : _raiders[cue.Entity]).Hit();
                    _cues.Show("−" + cue.Amount, position, cue.Defender); break;
                case FrontierCueKind.Chilled: _cues.Show("Chilled", position, false); break;
                case FrontierCueKind.Defeated:
                    _cues.Show(cue.Defender ? "Resting" : "Defeated", position, cue.Defender);
                    if (cue.Defender) Notice(CompanionName(cue.Entity) + " is resting");
                    break;
                case FrontierCueKind.Breach: Notice("A raider reached the Ark!"); _cues.Show("Breach", position, true); break;
                case FrontierCueKind.Rally: Notice(CompanionName(cue.Entity) + " rallied · faster attacks for 4s"); _cues.Show("Rally!", position, false); break;
            }
        }

        void Update()
        {
            _safeArea?.ApplyIfChanged();
            if (_runner == null) return;
            if (!_paused) _clock.Advance(_runner, Time.deltaTime, OnTick);
            var snapshot = _pair.Current;
            for (int i = 0; i < _defenders.Length; i++)
            {
                var defender = _defenders[i];
                int hp = snapshot.CreatureHp(i);
                defender.Hurt = 1 - hp / (float)_maxHp[i];
                defender.transform.localPosition = new Vector3(snapshot.CreatureTile(i), .05f, 1.5f);
                _health[i].SetHealth(Mathf.Max(0, hp), _maxHp[i]);
                _health[i].EnableInClassList("rallied", _runner.CreatureRallyRemaining(i) > 0);
                int target = _runner.CreatureTarget[i];
                if (target >= 0 && target < snapshot.RaiderCount)
                {
                    var localTarget = defender.transform.InverseTransformPoint(_world.TransformPoint(new Vector3(snapshot.RaiderTile(target), .5f, 0)));
                    defender.LookYaw = -Mathf.Atan2(localTarget.z, localTarget.x) * Mathf.Rad2Deg;
                }
                else defender.LookYaw = 0;
            }
            for (int i = 0; i < _raiders.Length; i++)
            {
                bool visible = snapshot.RaiderVisible(i);
                _raiders[i].gameObject.SetActive(visible);
                _raiderHealth[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;
                _raiders[i].transform.localPosition = new Vector3(WaveSnapshot.LerpTile(_pair.Previous, snapshot, i, (float)_clock.Alpha), 0, 0);
                _raiders[i].Chilled = snapshot.RaiderChilled(i);
                _raiders[i].Moving = !_paused && !_runner.Done;
                _raiderHealth[i].SetHealth(snapshot.RaiderHp(i), Stats.RaiderHp(_runner.RaiderType[i]), snapshot.RaiderChilled(i));
            }
            _status.text = "ARK  " + snapshot.Integrity + " / " + WaveDef.ForId(_wave).Integrity + "    •    " + snapshot.Tick / Stats.TicksPerSecond + "s";
            _rally.SetEnabled(!_paused && !_runner.Done && !_runner.RallyUsed && snapshot.CreatureHp(_selected) > 0);
            if (_runner.RallyUsed) _rally.text = "Rally used";
            if (_runner.Done && !_resultShown)
            {
                _resultShown = true;
                _detail.text = _runner.Result == Result.Win ? "The Ark is safe. Your companions held the line." : "The frontier pushed back. Try a different formation.";
                _primary.style.display = DisplayStyle.Flex;
                _pause.SetEnabled(false);
                Notice(_runner.Result == Result.Win ? "THE FRONTIER HOLDS" : "A CHANCE TO REGROUP");
                _status.EnableInClassList("victory", _runner.Result == Result.Win);
                ApplyMotion();
                if (_runner.Result == Result.Win)
                    for (int i = 0; i < _defenders.Length; i++)
                        if (snapshot.CreatureHp(i) > 0) _defenders[i].Celebrate();
                Debug.Log("[Frontier proof] wave=" + _wave + " result=" + _runner.Result + " ticks=" + _runner.Tick + " hash=" + _runner.Outcome.Hash);
            }
        }

        void LateUpdate()
        {
            if (_defenders == null || _stage == null || stageCamera == null) return;
            var size = _stage.contentRect.size;
            for (int i = 0; i < _defenders.Length; i++)
            {
                Anchor(_health[i], _defenders[i].transform.position + Vector3.up * 1.35f, size, 28, 14);
            }
            if (_pocketButtons != null)
            {
                var lane = WaveDef.ForId(_wave).Lane;
                for (int i = 0; i < _pocketButtons.Length; i++) Anchor(_pocketButtons[i], _world.TransformPoint(new Vector3(lane.PocketTiles[i], .08f, 1.5f)), size, 22, 0);
            }
            if (_raiders != null)
                for (int i = 0; i < _raiders.Length; i++)
                    if (_raiders[i].gameObject.activeSelf) Anchor(_raiderHealth[i], _raiders[i].transform.position + Vector3.up * .8f, size, 28, 14);
            if (_arkLabel != null) Anchor(_arkLabel, _world.TransformPoint(new Vector3(25, 2, 0)), size, 22, 10);
            _cues?.Advance(stageCamera, size, _paused ? 0 : Time.deltaTime, _reducedMotion);
        }

        void Anchor(VisualElement element, Vector3 position, Vector2 size, float offsetX, float offsetY)
        {
            var p = stageCamera.WorldToViewportPoint(position);
            element.style.left = p.x * size.x - offsetX;
            element.style.top = (1 - p.y) * size.y - offsetY;
        }

        void Notice(string message)
        {
            _notice.text = message;
            _notice.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void ApplyMotion()
        {
            if (_world == null) return;
            foreach (var actor in _world.GetComponentsInChildren<FrontierCreature>(true))
            {
                actor.ReducedMotion = _reducedMotion;
                actor.Paused = _paused;
            }
        }

        void GreetHero()
        {
            if (_hero == null) return;
            _hero.Greet();
            Notice(_heroName + (_hero.SpeciesId == "ember" ? " gives you a little wave." : _hero.SpeciesId == "pale" ? " dips a wing to greet you." : " tilts their head with curiosity."));
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused || _runner == null || _runner.Done) return;
            _paused = true; if (_pause != null) _pause.text = "Resume"; ApplyMotion();
        }

        void ResizeStage()
        {
            if (_stage == null || stageCamera == null) return;
            float width = _stage.contentRect.width, height = _stage.contentRect.height;
            if (width < 1 || height < 1 || float.IsNaN(width) || float.IsNaN(height)) return;
            float scale = Mathf.Min(2, 1600 / Mathf.Max(width, height));
            int w = Mathf.Max(1, Mathf.RoundToInt(width * scale)), h = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                ReleaseTexture();
                _texture = new RenderTexture(w, h, 24) { name = "Frontier stage", antiAliasing = 1 };
                _texture.Create(); stageCamera.targetTexture = _texture; _image.image = _texture;
            }
            else _image.image = _texture;
            stageCamera.aspect = width / height;
            bool battle = _page == Page.Battle || _page == Page.Deploy;
            Vector3 center = battle ? new Vector3(12, .6f, .55f) : new Vector3(0, .62f, 0);
            // A nearly lengthwise camera keeps the 24-tile lane legible in portrait.
            Vector3 offset = battle ? new Vector3(24, 27, -5) : new Vector3(3.2f, 1.65f, -3.8f);
            stageCamera.transform.SetPositionAndRotation(center + offset, Quaternion.LookRotation(-offset));
            Vector3 extent = battle ? new Vector3(15, 1.7f, 2.2f) : new Vector3(1.18f, 1.04f, 1.18f);
            Quaternion inverse = Quaternion.Inverse(stageCamera.transform.rotation);
            float size = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 p = inverse * Vector3.Scale(extent, new Vector3(x, y, z));
                        size = Mathf.Max(size, Mathf.Abs(p.y), Mathf.Abs(p.x) / stageCamera.aspect);
                    }
            stageCamera.orthographicSize = size * 1.06f;
        }

        void ReleaseTexture()
        {
            if (stageCamera != null) stageCamera.targetTexture = null;
            if (_image != null) _image.image = null;
            if (_texture == null) return;
            _texture.Release(); FrontierArt.Release(_texture); _texture = null;
        }

        void OnDestroy() { ClearWorld(); ReleaseTexture(); }
        static void SetLayer(Transform root, int layer)
        { root.gameObject.layer = layer; foreach (Transform child in root) SetLayer(child, layer); }
        static VisualElement Element(VisualElement parent, string style)
        { var v = new VisualElement(); v.AddToClassList(style); parent.Add(v); return v; }
        static Label Text(VisualElement parent, string value, string style)
        { var label = new Label(value) { enableRichText = false }; label.AddToClassList(style); parent.Add(label); return label; }
        static Button Action(VisualElement parent, string value, System.Action clicked, bool primary = false)
        { var button = new Button(clicked) { text = value, enableRichText = false }; button.AddToClassList(primary ? "primary-button" : "secondary-button"); parent.Add(button); return button; }
    }
}
