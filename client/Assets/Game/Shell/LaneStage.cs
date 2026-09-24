using System;
using System.Collections.Generic;
using Broodline.Creatures;
using Broodline.Frontier;
using Broodline.Sim.Combat;
using Broodline.View;
using UnityEngine;

namespace Broodline.Game.Shell
{
    /// One camera, one render texture, one lane, with the chosen creatures
    /// standing in their pockets. Phase 9 design §3.8, the second of the two
    /// off-screen rigs - `PortraitStudio` is the first and this file follows
    /// its shape line for line.
    ///
    /// IT IS A PICTURE AND NOT A SIMULATION, WHICH IS THE ONE THING TO KNOW
    /// BEFORE EDITING IT. Nothing here ticks, nothing here decides, and
    /// nothing here may change what the engine does. It READS three shared
    /// constants - `WaveRunner.LaneTiles`, `WaveView.TileSize` and
    /// `WaveView.PocketOffset` - and one shared lookup, `WaveDef.ForId(id)
    /// .Lane.PocketTiles`. All four are read-only from here. The thing that
    /// would catch a violation, `WaveCapturePlayTests`, is a PlayMode test
    /// that cannot be run headlessly on this Editor
    /// (`run-unity-tests.sh:28-30`), so the guard is the narrowness of this
    /// file rather than a gate: it draws, and it has no other verb.
    ///
    /// THE POSITIONS ARE `WaveView.Build`'s, RE-READ RATHER THAN RE-DERIVED.
    /// A creature stands at `Lane.PocketTiles[pocket] * TileSize` on x, at
    /// `PocketOffset` on z, turned so its snout faces the lane - the same
    /// three lines the live wave uses, so the deploy screen's picture and the
    /// wave the player then fights cannot disagree about where anything is.
    ///
    /// FAR BELOW THE ORIGIN ON THE STUDIO LAYER. Same arrangement as
    /// `PortraitStudio`: no scene camera sees this rig and this rig sees no
    /// scene. Further down than the studio (-800 against -400) so the two
    /// cannot see each other either - the studio's camera is orthographic
    /// with a 0.9 half-extent and this one's is 6.0, and both cull to layer
    /// `CreatureAssembler.StudioLayer`, so overlapping them would put a lane
    /// in the founder's portrait.
    ///
    /// THE CAMERA IS NEVER ENABLED AT ALL. `PortraitStudio` leaves its own
    /// running because it has an `Update()` that turns the creature; this
    /// class has none, so `Show` paints exactly one frame through
    /// `Camera.SubmitRenderRequest` and the camera stays off. `Paint`'s own
    /// note has the measurement that changed this and the two things it buys
    /// - no render target painted through the fight, and a rig an EditMode
    /// test can drive without a player loop.
    public sealed class LaneStage : MonoBehaviour
    {
        /// 4:3, AND IT WAS 3:2 UNTIL PHASE 9 TASK 21B MEASURED WHAT THAT COST.
        ///
        /// The card this fills, `LanePreviewCard`, is a fixed
        /// `--lane-card-height` (274px) inside the content column, so its
        /// ASPECT follows the frame: 366/274 = 1.3358 at the 390 design frame
        /// and 406/274 = 1.4818 at the 430 capture frame.
        /// `-unity-background-scale-mode: scale-and-crop` scales this texture
        /// to COVER that box and cuts the overflow off both ends of whichever
        /// axis is long. Cropping rather than letterboxing is
        /// `LanePreviewCard.uss`'s own decision, stated there: "a LANE that
        /// does not fit its card should fill it and lose an edge."
        ///
        /// AT 720x480 THE LONG AXIS WAS THE HORIZONTAL ONE, AND THE EDGE IT
        /// LOST HAD A CREATURE STANDING ON IT. 1.5000 against the design
        /// frame's 1.3358 put 39.4 texture pixels - 0.903 world units - under
        /// the crop on each side, so the pockets at tiles 6 and 20 kept 0.347
        /// units of margin where `LaneStageTests` guarantees 1.0, and both end
        /// creatures were cut in half in the shipped card. Nothing in the
        /// project could see it: this rig's own suites read the render
        /// texture, whose frame was correct, and the capture corpus passes no
        /// texture at all.
        ///
        /// 4:3 IS WHAT THE OTHER SIDE ALREADY THOUGHT THIS WAS.
        /// `LanePreviewCard.uss` says "the stage renders 4:3 into a 4:3 card,
        /// so nothing is cropped in practice" and `LanePreviewCard`'s class
        /// comment says `--lane-card-height` "is a token rather than a literal
        /// because the stage's render texture is sized to match it". Both
        /// described a contract this constant had drifted off; 640x480 is that
        /// contract, and it is 11% fewer pixels than 720x480 besides.
        ///
        /// SO THE CROP IS NOW VERTICAL, WHICH IS THE HARMLESS DIRECTION. At
        /// 1.3333 this is a hair narrower in aspect than the 390 frame's card,
        /// so the cover fits the width and spills 0.5px of height; at the 430
        /// frame it spills 15px of height off each of top and bottom, which is
        /// empty ground above the trees and empty ground below the path. No
        /// frame at or above the design width takes anything off the ends of
        /// the lane. `LaneStageTests.EveryPocketStandsInsideTheRECTANGLETHE
        /// CARDSHOWS_NotOnlyInsideTheTexture` is the reader that holds this.
        public const int Width = 640, Height = 480;

        /// The camera's vertical half-extent in world units. At Width:Height
        /// it covers 2 * 6.0 * 1.3333 = 16.0 units across, which is what
        /// decides the framing arithmetic beside `Aim`.
        ///
        /// 6.0 AND NOT 5.5, AND THE ASPECT CHANGE IS WHY - Phase 9 Task 21b.
        /// The half-extent is `OrthographicSize * Width / Height`, so moving
        /// the texture from 3:2 to 4:3 to stop the card cropping the ends of
        /// the lane would have taken it from 8.25 to 7.333 and pulled the
        /// frame IN past the outer pockets - tiles 6 and 20 need 7 units each
        /// side of the span's centre, plus the unit of margin a body needs.
        /// 6.0 puts it back at exactly 8.0, which is that 7 + 1 with nothing
        /// spare, and the card now shows all of it rather than 89% of it. The
        /// visible span is therefore WIDER than it was before this change
        /// (8.00 against 7.35), so a creature is about 8% smaller on the card
        /// and nothing is cut off it. That is the trade, stated.
        ///
        /// PUBLIC BECAUSE THE FRAMING IS TESTED AND NOT ONLY DOCUMENTED.
        /// `LaneStageTests.EveryPocketOfEveryAuthoredWaveStandsInsideTheFrame`
        /// recomputes that half-extent from this and `Width`/`Height` and
        /// checks every pocket of every authored lane against it - which is
        /// the one thing about this rig no capture can show and no pixel
        /// count can answer. Without that reader this would be a public
        /// constant the brief did not ask for and nothing outside this file
        /// read, which is drift.
        public const float OrthographicSize = 6.0f;

        /// Below `PortraitStudio.Far` (-400) by more than either camera can
        /// see - see the class comment.
        static readonly Vector3 Far = new Vector3(0f, -800f, 0f);

        Camera _camera;
        RenderTexture _texture;
        GameObject _creatures;
        FrontierArt _art;
        FrontierArt _terrainArt;
        GameObject _dressing;
        int _terrainWaveId = -1;

        public static LaneStage Create(Transform host)
        {
            var go = new GameObject("lane-stage");
            go.transform.SetParent(host, false);
            go.transform.position = Far;
            go.layer = CreatureAssembler.StudioLayer;
            var stage = go.AddComponent<LaneStage>();

            var cam = new GameObject("camera").AddComponent<Camera>();
            cam.transform.SetParent(go.transform, false);
            cam.orthographic = true;
            cam.orthographicSize = OrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;

            // THE FIELD COLOUR, NOT BLACK AND NOT TRANSPARENT, AND
            // `WaveSceneBuilder` ALREADY MADE THIS CALL FOR THE SAME REASON:
            // "so the lane's edges do not show black past the dressing's
            // finite quads". The dressing's field quad is 8 units deep in z
            // and this camera's ground coverage is ~14, so there IS ground
            // past it in every frame. `LaneDressing.Field` is #e8f5ec, which
            // is --green-tint, which is `.lane-preview-card`'s own fill - so
            // the band past the dressing is the same colour as the card
            // behind the texture and the seam is invisible either way.
            cam.backgroundColor = LaneDressing.Field;

            cam.cullingMask = 1 << CreatureAssembler.StudioLayer;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            cam.gameObject.layer = CreatureAssembler.StudioLayer;

            var light = new GameObject("light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Directional;
            light.cullingMask = 1 << CreatureAssembler.StudioLayer;
            // `WaveSceneBuilder`'s own directional light, to the degree, so
            // a creature is lit the same way in the picture and in the wave.
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 1.1f;

            stage._art = new FrontierArt(RuntimeShaders.Require(RuntimeShaders.Frontier));

            stage._camera = cam;
            stage._texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = stage._texture;

            // A RESTING AIM SO THE RIG IS NEVER IN AN UNDEFINED POSE. `Show`
            // re-aims at the wave's own pocket span; this is the lane's
            // midpoint, which is `WaveSceneBuilder.laneMidX` exactly.
            stage.Aim(WaveRunner.LaneTiles * WaveView.TileSize * 0.5f);

            // AND IT STAYS DISABLED. `Paint` renders one frame on demand
            // rather than letting the player loop drive this camera - see
            // that method for why, and for what the first round of this task
            // cost by copying `PortraitStudio`'s enabled camera across
            // without its `Update()`.
            cam.enabled = false;
            return stage;
        }

        /// Rebuild the picture: `placed[i]` stands at pocket `pockets[i]`.
        ///
        /// NULL FOR A WAVE THIS BUILD DOES NOT AUTHOR, RATHER THAN A THROW,
        /// and that is the one deliberate difference from `WaveDef.ForId`'s
        /// own contract. `ForId` throws `WaveCompositionException` on an
        /// unknown id ON PURPOSE - "a replay naming a wave this engine does
        /// not have must fail loudly at load rather than re-simulate
        /// something else" - and Campaign Select can reach one, because it
        /// renders whatever `config.waves` sends. That throw still happens,
        /// where it matters, at `FtueDirector`'s guarded `_play` call. What
        /// must NOT happen is a decoration stranding the turn before it: the
        /// deploy screen's Start button is the only thing that resumes the
        /// walk, and an exception out of a picture would leave the player on
        /// a screen with no live control. So this returns null, the card
        /// falls back to its own fill (`LanePreviewCard.SetTexture`'s
        /// documented null case), and the loud failure keeps its own site.
        public Texture Show(int waveId, IReadOnlyList<FrontierLook> placed, IReadOnlyList<int> pockets)
        {
            Clear();

            Lane lane;
            try
            {
                lane = WaveDef.ForId(waveId).Lane;
            }
            catch (WaveCompositionException error)
            {
                Debug.LogWarning("[lane-stage] no authored wave " + waveId +
                    " to draw - the lane card falls back to its own fill: " + error.Message);
                return null;
            }

            if (_terrainWaveId != waveId)
            {
                if (_dressing != null)
                {
                    _dressing.SetActive(false);
                    if (Application.isPlaying) Destroy(_dressing);
                    else DestroyImmediate(_dressing);
                    _terrainArt?.Dispose();
                }
                var pocketTiles = new int[lane.PocketCount];
                for (int p = 0; p < pocketTiles.Length; p++) pocketTiles[p] = lane.PocketTiles[p];
                _terrainArt = new FrontierArt(RuntimeShaders.Require(RuntimeShaders.Frontier));
                _dressing = _terrainArt.Environment(transform, WaveRunner.LaneTiles, pocketTiles, false);
                _dressing.name = "dressing";
                CreatureAssembler.SetLayerRecursively(_dressing, CreatureAssembler.StudioLayer);
                _terrainWaveId = waveId;
            }

            Aim(PocketSpanCentre(lane));

            _creatures = new GameObject("creatures");
            _creatures.transform.SetParent(transform, false);
            _creatures.transform.localPosition = Vector3.zero;

            var count = placed == null ? 0 : placed.Count;
            for (var i = 0; i < count; i++)
            {
                var look = placed[i];
                if (look == null) continue;

                // A POCKET THIS LANE DOES NOT HAVE IS SKIPPED, NOT CLAMPED.
                // `DeployScreen.Build` assigns `Pocket = i` off the selection
                // index and caps at `DeployScreen.Cap` (5); `Lane.Defile()`
                // has five pockets and `Lane.DefileSix()` six, so today the
                // two cannot disagree. Clamping a sixth creature onto the
                // fifth pocket would draw two creatures in one place and say
                // nothing; skipping draws one fewer than the player chose,
                // which the field list beside the card still names in words.
                var pocket = pockets != null && i < pockets.Count ? pockets[i] : i;
                if (pocket < 0 || pocket >= lane.PocketCount)
                {
                    Debug.LogWarning("[lane-stage] wave " + waveId + " has " + lane.PocketCount +
                        " pockets and a creature was placed at pocket " + pocket + " - not drawn.");
                    continue;
                }

                if (_art == null) _art = new FrontierArt(RuntimeShaders.Require(RuntimeShaders.Frontier));
                FrontierCreature creature;
                try { creature = _art.Creature(_creatures.transform, look.Species, look.Trait1, look.Trait2); }
                catch (ArgumentException error)
                {
                    Debug.LogWarning("[lane-stage] unsupported companion appearance: " + error.Message);
                    continue;
                }
                creature.Growth = Mathf.Clamp01(look.Growth01);
                creature.Pose(0f, true);
                creature.Paused = true; // this stage is a still picture, even while a wave is running
                var body = creature.gameObject;

                // `WaveView.Build`'s three lines, re-read. The rotation
                // expression is copied verbatim, comment included, because
                // two spellings of "face the lane" that drifted apart would
                // put the picture and the wave at right angles to each other
                // with nothing to say so.
                body.transform.localPosition = new Vector3(
                    lane.PocketTiles[pocket] * WaveView.TileSize, 0f, WaveView.PocketOffset);
                body.transform.localRotation =
                    Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);   // snout (+X) toward the lane (-Z)

                CreatureAssembler.SetLayerRecursively(body, CreatureAssembler.StudioLayer);
            }

            Paint();
            return _texture;
        }

        /// ONE FRAME, NOW, AND THEN NOTHING - which is the whole difference
        /// between this rig and `PortraitStudio`.
        ///
        /// That one leaves `_camera.enabled = true` because it has an
        /// `Update()` that turns its creature every frame
        /// (`PortraitStudio.cs:88-90`), so a live camera is what the feature
        /// IS. This class has no `Update` and its own header says so -
        /// "Nothing here ticks". Phase 9 Task 17's first round copied the
        /// enabled camera across with the pattern and not the reason, and the
        /// cost was an orthographic camera painting a 640x480 target every
        /// frame of the fight, on a phone, at the one moment the frame budget
        /// matters: `FtueDirector`'s `finally` spans `_play(...)`, so it ran
        /// for the whole wave.
        ///
        /// A PICTURE IS A FRAME, NOT A LOOP. The camera stays disabled and
        /// this paints on demand. `Show` is the only caller and calls it
        /// once, after the creatures are in place.
        ///
        /// `SubmitRenderRequest` FIRST, `Camera.Render()` AS THE FALLBACK,
        /// AND THE ORDER IS FORCED. `Camera.Render()` is documented as
        /// unsupported under a Scriptable Render Pipeline - this project is
        /// URP - and 2022.2 added `RenderPipeline.StandardRequest` as the
        /// supported way to drive one camera into one target on demand.
        /// `GraphicsSettings.currentRenderPipeline` is the probe: it is the
        /// pipeline ASSET the project is configured with, so it answers
        /// before anything has rendered - where
        /// `RenderPipelineManager.currentPipeline` is created lazily on the
        /// first frame and is null in an EditMode test that has not drawn
        /// yet. A build with no SRP takes `Camera.Render()` and still
        /// renders rather than silently producing nothing. A SILENT NOTHING
        /// IS THE FAILURE MODE THAT MATTERS HERE: the deploy screen's
        /// fallback for a null or blank texture is the card's own flat fill,
        /// which looks exactly like a stage that has not been given one.
        ///
        /// NEITHER `Camera.SupportsRenderRequest` NOR
        /// `Camera.RenderRequestSupported` EXISTS, though both names appear
        /// in `UnityEngine.CoreModule.dll`'s string table - they are bindings
        /// on other types. Both were tried and both failed to compile on
        /// 6000.6.0f1; recorded so the next reader does not spend the same
        /// two runs finding out.
        ///
        /// AND THIS IS WHAT MAKES THE RIG TESTABLE HEADLESSLY. An enabled
        /// camera paints during the player loop, which EditMode does not run
        /// - so the old shape could only ever be proven by a human in Play
        /// mode. An explicit request renders synchronously, so
        /// `LaneStageTests` can call `Show` and read the pixels back in the
        /// EditMode suite that runs on every gate.
        void Paint()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            {
                _camera.SubmitRenderRequest(
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = _texture });
            }
            else
            {
                _camera.Render();
            }
        }

        public void Clear()
        {
            // DEFERRED IN PLAY MODE, IMMEDIATE OUTSIDE IT, which is
            // `LaneDressing.Paint`'s own branch and is here for its reason:
            // `Object.Destroy` runs at the end of the frame, and an EditMode
            // test (or a batch `-executeMethod`) never reaches one - so the
            // creatures would still be standing when the next `Show` built a
            // second set on top of them, and the test that reads the texture
            // back would be reading two waves at once.
            //
            // DEACTIVATED BEFORE EITHER BRANCH, AND THAT LINE IS PHASE 9 TASK
            // 21F - MEASURED ON A DEVICE, NOT REASONED ABOUT. `Show` calls
            // this, builds the new bodies and `Paint`s ONE FRAME, all inside
            // the same frame; the deferred `Destroy` above does not run until
            // the END of that frame. So in play mode the single frame the card
            // keeps was painted with the PREVIOUS deployment's bodies still
            // alive and still rendering, and nothing ever repaints it. On the
            // deploy screen at 402x874 a roster toggled down to `DEPLOYED 1/5`
            // drew TWO creatures - the removed one still standing in pocket B
            // - and the slot strip beside it correctly showed one.
            //
            // `SetActive(false)` RATHER THAN `DestroyImmediate` IN BOTH
            // BRANCHES. An inactive GameObject renders nothing, so this is
            // sufficient for the paint, and it leaves the destruction
            // semantics this file deliberately chose exactly as they were -
            // `LaneStagePlayTests` still watches the deferred destroy actually
            // land at the end of the frame. Making play mode destroy
            // immediately would fix the same frame by changing a rule that is
            // not the one that is wrong.
            if (_creatures != null)
            {
                _creatures.SetActive(false);
                if (Application.isPlaying) Destroy(_creatures);
                else DestroyImmediate(_creatures);
            }
            _creatures = null;

            // Belt and braces: `Paint` leaves this false, so this is the
            // state the camera is already in. Kept so that a future caller
            // that enables it for its own reasons cannot leave it running
            // past a Clear.
            if (_camera != null) _camera.enabled = false;

            // AND THE TEXTURE IS LEFT ALONE. THIS METHOD USED TO `GL.Clear` IT
            // TO TRANSPARENT AND THAT IS THE PHASE 9 TASK 21F DEFECT, CAUGHT
            // ON A DEVICE. Stated as the old comment stated it, because the
            // reasoning was sound and the conclusion was backwards:
            //
            //   "`Show` hands the same `Texture` reference out to whatever
            //   `LanePreviewCard` is displaying [...] `ScreenFlow.ShowAsync`
            //   passes `after: null`, so the deploy screen is still presented
            //   when the director's `finally` runs this."
            //
            // Both halves are true, and TOGETHER THEY ARE THE BUG RATHER THAN
            // THE REASON FOR THE WIPE. The card holds this render texture as a
            // LIVE background, so it shows whatever is in the buffer at the
            // moment it draws - and the deploy screen outliving the beat means
            // the wipe lands on a card the player is still looking at.
            // `FtueDirector.FightAsync` has four exits that show no other
            // screen first (`CanDeploy` false, `StartAsync` throwing, `_play`
            // throwing, and an outbox submit that is not `Sent`), and on every
            // one of them the `finally` wiped the lane out from under the
            // presented deploy screen. Reproduced on an iPhone 17 at 402x874:
            // background the app mid-wave past `WaveHost
            // .CompletionTimeoutSeconds`, and the deploy screen comes back
            // with a flat `--green-tint` card - no path, no dashes, no trees,
            // no creatures - while its slot strip still reads `A B C` filled.
            // NOTHING LOGGED, because `Show` was never re-entered: the card
            // was never handed null, its buffer was emptied underneath it.
            //
            // WHAT THE WIPE WAS PROTECTING AGAINST CANNOT HAPPEN. It guarded
            // "a cleared stage still shows the last wave's lane", but the card
            // only ever RECEIVES a texture from `FtueDirector.ShowLane`, which
            // calls `Show(waveId, ...)` - and `Show` repaints for the wave it
            // is about to display before the card is bound to it. So a lane
            // for the WRONG wave has no path onto a card, with or without this
            // wipe; the only picture this method could ever erase is the
            // correct picture for the screen that is still up. The frame
            // belongs to the last `Show` and is replaced by the next one.
            //
            // A wave this build does not author is unaffected and keeps its
            // own answer: `Show` returns null there, `ShowLane` passes null on,
            // and `LanePreviewCard.SetTexture` falls back to its own fill.
        }

        /// Point the camera at `centreX` from a lower three-quarter angle, so
        /// the pockets read as a row rather than as a line seen end-on.
        ///
        /// THE ARITHMETIC, BECAUSE NO CAPTURE IN THIS PROJECT CAN CHECK IT.
        /// The camera sits 9 up and 7 back and looks at ground z = +0.5, so
        /// its forward is (0, -0.768, 0.640) - 50.2 degrees below horizontal.
        /// `LookAt`'s default world up makes the camera's right world +X, so
        /// the 16.0 units of horizontal coverage lie along the lane and the
        /// vertical half-extent of 6.0 covers 6.0 / 0.768 = 7.8 units of
        /// ground on each side of z = 0.5 - comfortably past both the path
        /// (z = 0) and the pockets (z = +1).
        ///
        /// CENTRED ON THE POCKETS AND NOT ON THE LANE, AND THE 8.0 IS WHY.
        /// Both authored lanes put their pockets between tiles 6 and 20, so
        /// the span's centre is 13 and the lane's midpoint is 12. At the
        /// lane's midpoint the frame runs x in [4, 20] and the last pocket
        /// stands ON the right edge - a creature there is half off the
        /// picture. At the span's centre it runs [5, 21] and every pocket has
        /// exactly 1 unit of margin. The resting aim in `Create` is still the
        /// lane's midpoint, because at that moment no wave has been named.
        ///
        /// THERE IS NO SLACK LEFT IN THAT 1 UNIT, which is new as of Task 21b
        /// and is the cost of the card showing the whole frame rather than
        /// 89% of it. Widening the lane's pocket span, or narrowing
        /// `OrthographicSize`, now reddens
        /// `EveryPocketStandsInsideTheRECTANGLETHECARDSHOWS_NotOnlyInside
        /// TheTexture` immediately rather than quietly clipping a creature.
        void Aim(float centreX)
        {
            _camera.transform.localPosition = new Vector3(centreX, 9f, -7f);
            _camera.transform.LookAt(transform.position + new Vector3(centreX, 0f, 0.5f));
        }

        /// The middle of this lane's pocket span, in world units.
        ///
        /// READ-ONLY OVER `Lane.PocketTiles`, which is a `ReadOnlySpan<int>`
        /// over a clone the lane made at construction precisely so nothing
        /// outside the engine can move it (`Lane`'s own note: "geometry and
        /// range checks silently disagreeing"). This walks it and writes
        /// nothing.
        static float PocketSpanCentre(Lane lane)
        {
            if (lane.PocketCount == 0) return 0f;
            int min = lane.PocketTiles[0], max = min;
            for (var p = 1; p < lane.PocketCount; p++)
            {
                var tile = lane.PocketTiles[p];
                if (tile < min) min = tile;
                if (tile > max) max = tile;
            }
            return (min + max) * 0.5f * WaveView.TileSize;
        }

        void OnDestroy()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_texture != null) _texture.Release();
            _art?.Dispose();
            _terrainArt?.Dispose();
        }
    }
}
