using System.Collections.Generic;
using Broodline.Creatures;
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
    /// with a 0.9 half-extent and this one's is 5.5, and both cull to layer
    /// `CreatureAssembler.StudioLayer`, so overlapping them would put a lane
    /// in the founder's portrait.
    ///
    /// THE CAMERA IS ENABLED ONLY WHILE SHOWN, which is `PortraitStudio`'s
    /// rule and costs nothing until the deploy screen asks for a frame.
    public sealed class LaneStage : MonoBehaviour
    {
        /// 3:2. The card this fills, `LanePreviewCard`, is a fixed
        /// `--lane-card-height` (274px) inside the content column, so its
        /// ASPECT follows the frame: 406/274 = 1.48 at the 430 capture frame
        /// and 366/274 = 1.34 at the 390 design frame. 720x480 is 1.50, so
        /// this is within 1.3% at 430 and `-unity-background-scale-mode:
        /// scale-and-crop` trims ~11% of the width at 390. Cropping rather
        /// than letterboxing is `LanePreviewCard.uss`'s own decision, stated
        /// there: "a LANE that does not fit its card should fill it and lose
        /// an edge". The crop is taken off the left and right, which is why
        /// the framing below centres on the POCKETS and leaves margin.
        public const int Width = 720, Height = 480;

        /// The camera's vertical half-extent in world units. At Width:Height
        /// it covers 2 * 5.5 * 1.5 = 16.5 units across, which is what decides
        /// the framing arithmetic beside `Aim`.
        public const float OrthographicSize = 5.5f;

        /// Below `PortraitStudio.Far` (-400) by more than either camera can
        /// see - see the class comment.
        static readonly Vector3 Far = new Vector3(0f, -800f, 0f);

        Camera _camera;
        RenderTexture _texture;
        GameObject _creatures;

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

            // ONCE, IN `Create`, AND NEVER AGAIN. The dressing is the same
            // for every wave this build authors - `LaneDressing.Build` takes
            // the lane's LENGTH and not its pockets - so rebuilding it per
            // `Show` would allocate four trees, an Ark and twenty-four dashes
            // to draw the identical picture. `Show` rebuilds only the
            // creatures.
            var dressing = LaneDressing.Build(
                go.transform, WaveRunner.LaneTiles, WaveView.TileSize, WaveView.PocketOffset);
            CreatureAssembler.SetLayerRecursively(dressing, CreatureAssembler.StudioLayer);

            stage._camera = cam;
            stage._texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = stage._texture;

            // A RESTING AIM SO THE RIG IS NEVER IN AN UNDEFINED POSE. `Show`
            // re-aims at the wave's own pocket span; this is the lane's
            // midpoint, which is `WaveSceneBuilder.laneMidX` exactly.
            stage.Aim(WaveRunner.LaneTiles * WaveView.TileSize * 0.5f);

            cam.enabled = false;   // costs nothing until Show
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
        public Texture Show(int waveId, IReadOnlyList<CreatureLook> placed, IReadOnlyList<int> pockets)
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

                var body = CreatureAssembler.Build(look);
                body.transform.SetParent(_creatures.transform, false);

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

            _camera.enabled = true;
            return _texture;
        }

        public void Clear()
        {
            if (_creatures != null) Destroy(_creatures);
            _creatures = null;
            if (_camera != null) _camera.enabled = false;

            // `PortraitStudio.Clear`'s note, and it applies here for exactly
            // the same reason: disabling the camera does not reset the
            // texture it was painting, and `Show` hands the same `Texture`
            // reference out to whatever `LanePreviewCard` is displaying - so
            // without this a cleared stage still shows the last wave's lane.
            // It matters MORE here than there: `ScreenFlow.ShowAsync` passes
            // `after: null`, so the deploy screen is still presented when the
            // director's `finally` runs this.
            //
            // CLEARED TO TRANSPARENT, NOT TO THE FIELD COLOUR. The card's own
            // `--green-tint` fill is what should show through an empty
            // texture, and it is the same colour as the field anyway
            // (`LaneDressing.Field`), so the two agree on what a blank lane
            // looks like from either side.
            if (_texture != null)
            {
                var prev = RenderTexture.active;
                RenderTexture.active = _texture;
                GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
                RenderTexture.active = prev;
            }
        }

        /// Point the camera at `centreX` from a lower three-quarter angle, so
        /// the pockets read as a row rather than as a line seen end-on.
        ///
        /// THE ARITHMETIC, BECAUSE NO CAPTURE IN THIS PROJECT CAN CHECK IT.
        /// The camera sits 9 up and 7 back and looks at ground z = +0.5, so
        /// its forward is (0, -0.768, 0.640) - 50.2 degrees below horizontal.
        /// `LookAt`'s default world up makes the camera's right world +X, so
        /// the 16.5 units of horizontal coverage lie along the lane and the
        /// vertical half-extent of 5.5 covers 5.5 / 0.768 = 7.2 units of
        /// ground on each side of z = 0.5 - comfortably past both the path
        /// (z = 0) and the pockets (z = +1).
        ///
        /// CENTRED ON THE POCKETS AND NOT ON THE LANE, AND THE 8.25 IS WHY.
        /// Both authored lanes put their pockets between tiles 6 and 20, so
        /// the span's centre is 13 and the lane's midpoint is 12. At the
        /// lane's midpoint the frame runs x in [3.75, 20.25] and the last
        /// pocket stands 0.25 units inside the right edge - a creature there
        /// is half off the picture. At the span's centre it runs
        /// [4.75, 21.25] and every pocket has 1.25 units of margin. The
        /// resting aim in `Create` is still the lane's midpoint, because at
        /// that moment no wave has been named.
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
        }
    }
}
