using System.Collections.Generic;
using Broodline.Creatures;
using Broodline.Game.Shell;
using Broodline.Sim.Combat;
using Broodline.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broodline.Game.Tests
{
    /// The lane stage, headlessly. Phase 9 Task 17 fix round 1.
    ///
    /// THIS FILE EXISTS BECAUSE THE FIRST ROUND HAD NO EVIDENCE PATH AT ALL.
    /// The capture corpus cannot show this rig by construction - no fixture
    /// in `ScreenFixtures` drives a camera - `LaneStagePlayTests` cannot be
    /// run on this Editor (`run-unity-tests.sh:28-30`), and nothing in the
    /// EditMode suite touched `LaneStage`. So "the deploy screen shows the
    /// real lane" rested entirely on a human opening the Editor.
    ///
    /// WHAT MADE IT TESTABLE WAS THE FIX TO A DIFFERENT DEFECT. `Show` used
    /// to enable the camera and let the player loop paint it, which EditMode
    /// does not run; it now paints one frame on demand through
    /// `Camera.SubmitRenderRequest` (`LaneStage.Paint`), which is
    /// synchronous. The two halves of that change are the same line.
    ///
    /// THE PIXEL CASES NEED A GRAPHICS DEVICE AND SAY SO RATHER THAN
    /// FAILING. `run-unity-tests.sh` runs `-batchmode` WITHOUT
    /// `-nographics`, which is the same thing `capture-screens.sh` depends on
    /// and documents ("NEEDS A GRAPHICS DEVICE"). A runner that adds
    /// `-nographics` has no device to render with, and a red test there would
    /// say the stage is broken when what is missing is the GPU. `Ignore` is
    /// the honest answer; the arithmetic case below runs either way.
    public class LaneStageTests
    {
        /// "vetch" is the species `PortraitStudioPlayTests` uses and one of
        /// the six `SpeciesRecipes` carries. A species with no recipe answers
        /// with a magenta sphere AND a `Debug.LogError`.
        ///
        /// THAT ERROR WOULD NOT REDDEN ANYTHING HERE, and the reason matters
        /// enough to state: `EditModeRunner` drives NUnit by plain reflection
        /// and installs no log handler, so this suite has no log scope at all
        /// - `LogAssert` throws "No log scope is available" in it. The claim
        /// that the Test Framework fails a test on `LogError` is true in
        /// PlayMode, where this comment came from, and false here. The species
        /// choice is still right, but it buys a readable picture rather than a
        /// red test.
        const string Species = "vetch";

        static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("no graphics device - run without -nographics, as capture-screens.sh does");
            }
        }

        // ---------------------------------------------------------------
        // The picture
        // ---------------------------------------------------------------

        /// THE ONE THING NOTHING ELSE IN THIS PROJECT DEMONSTRATES: that the
        /// stage draws a lane at all, and that the creatures are ON it.
        ///
        /// TWO ASSERTIONS AND THE SECOND IS THE POINT. A rig that rendered
        /// the dressing and silently dropped every creature - a species with
        /// no recipe, a pocket the lane does not carry, bodies parented
        /// outside the frustum - passes the first and fails the second. The
        /// deploy screen is ABOUT the creatures standing in their pockets.
        [Test]
        public void Show_DrawsTheLane_AndThenTheCreaturesOnTopOfIt()
        {
            RequireGraphics();

            var host = new GameObject("lane-host");
            try
            {
                var stage = LaneStage.Create(host.transform);

                var texture = (RenderTexture)stage.Show(1, new List<CreatureLook>(), new List<int>());
                Assert.IsNotNull(texture, "wave 1 is authored; Show returned no texture for it");
                var empty = ReadPixels(texture);

                // THE OPACITY CHECK COMES FIRST, and it is not decoration: the
                // unlike-the-field count below reads the same on a painted lane and
                // on a texture `Clear()` left transparent, because transparent black
                // is unlike the field too. Without this line the threshold only
                // makes the inversion harder to see.
                Assert.Greater(OpaquePixelCount(empty), 0,
                    "the texture is still the blank Clear() left - no frame was painted at all");
                Assert.Greater(PixelsUnlikeTheField(empty), texture.width * texture.height / 50,
                    "the stage rendered nothing but its clear colour - the lane itself is not drawing");

                var withCreature = (RenderTexture)stage.Show(
                    1,
                    new List<CreatureLook> { new CreatureLook { Species = Species } },
                    new List<int> { 0 });
                Assert.AreSame(texture, withCreature,
                    "Show handed out a second texture; the card is holding the first");
                Assert.Greater(DifferingPixels(empty, ReadPixels(withCreature)), 200,
                    "putting a creature in pocket 0 did not change the picture");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// `Show` PAINTS SYNCHRONOUSLY AND LEAVES THE CAMERA OFF, which is
        /// the whole of fix round 1's Important 1. Asserted from both sides:
        /// the texture has the picture in it with no frame having been
        /// pumped (this is EditMode - there is no player loop to pump), and
        /// the camera is not left running to paint it again.
        [Test]
        public void Show_PaintsOnceOnDemand_AndLeavesNoCameraRunning()
        {
            RequireGraphics();

            var host = new GameObject("lane-host");
            try
            {
                var stage = LaneStage.Create(host.transform);
                var texture = (RenderTexture)stage.Show(
                    1,
                    new List<CreatureLook> { new CreatureLook { Species = Species } },
                    new List<int> { 0 });

                // OPAQUE COUNT, NOT UNLIKE-THE-FIELD. This assertion used to read
                // `PixelsUnlikeTheField(...) > 0` and COULD NOT FAIL: `Show` calls
                // `Clear()` first, which `GL.Clear`s to (0,0,0,0), and transparent
                // black is maximally unlike the field colour - so an unpainted
                // texture scored every one of its 345,600 pixels and passed. The
                // message described exactly the case it could not detect.
                // Only a real render writes alpha 1, because the camera clears to
                // `LaneDressing.Field`, so opacity is what separates painted from
                // blanked.
                Assert.Greater(OpaquePixelCount(ReadPixels(texture)), 0,
                    "nothing was painted, so Show is still relying on a player loop this suite does not run");

                var camera = host.GetComponentInChildren<Camera>(includeInactive: true);
                Assert.IsNotNull(camera, "the stage built no camera");
                Assert.IsFalse(camera.enabled,
                    "the camera is left enabled, so it repaints a 720x480 target every frame of the fight");

                // AND IT IS AIMED WHERE `Aim` SAYS, not merely aimed.
                // `EveryPocketOfEveryAuthoredWaveStandsInsideTheFrame` proves the
                // ARITHMETIC of centring on the pocket span, but it re-derives the
                // centre rather than calling `Aim` - so reverting `Show` to the lane
                // midpoint would leave it green while putting tile 20 a quarter of a
                // unit inside the frame. This reads the camera the stage actually
                // built, which is the only thing that closes that gap.
                var lane = WaveDef.ForId(1).Lane;
                int min = lane.PocketTiles[0], max = min;
                for (var p = 1; p < lane.PocketCount; p++)
                {
                    if (lane.PocketTiles[p] < min) min = lane.PocketTiles[p];
                    if (lane.PocketTiles[p] > max) max = lane.PocketTiles[p];
                }
                var spanCentre = (min + max) * 0.5f * WaveView.TileSize;
                var laneMidpoint = WaveRunner.LaneTiles * 0.5f * WaveView.TileSize;
                Assert.AreNotEqual(spanCentre, laneMidpoint,
                    "wave 1's pocket span is centred on the lane, so this test cannot tell the two apart");
                Assert.AreEqual(spanCentre, camera.transform.position.x, 0.01f,
                    "the camera is at the LANE's midpoint rather than the pocket span's centre");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Clear_BlanksTheTexture_RatherThanOnlyStoppingTheDrawing()
        {
            RequireGraphics();

            var host = new GameObject("lane-host");
            try
            {
                var stage = LaneStage.Create(host.transform);
                var texture = (RenderTexture)stage.Show(
                    1,
                    new List<CreatureLook> { new CreatureLook { Species = Species } },
                    new List<int> { 0 });
                Assert.Greater(OpaquePixelCount(ReadPixels(texture)), 0,
                    "precondition: the stage must have painted something to clear");

                stage.Clear();

                // `Show` hands the SAME texture reference to whatever
                // `LanePreviewCard` is displaying, and `ScreenFlow` passes
                // `after: null` so the deploy screen is still presented when
                // the director's `finally` runs this. A Clear that only
                // stopped drawing would leave the last wave on screen.
                Assert.AreEqual(0, OpaquePixelCount(ReadPixels(texture)),
                    "Clear left the last wave's frame in the texture");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// `WaveDef.ForId` throws `WaveCompositionException` for an unknown id
        /// BY DESIGN, and Campaign Select can reach one because it renders
        /// whatever `config.waves` sends. A picture must not be the thing
        /// that strands a turn - the deploy screen's Start button is the only
        /// control that resumes the walk - so the contract is a null and a
        /// warning, and the loud failure keeps its own site at
        /// `FtueDirector`'s guarded `_play` call.
        ///
        /// NO `LogAssert.Expect` ON THE WARNING, AND NOT BY PREFERENCE.
        /// `Broodline.TestHarness/EditModeRunner` drives NUnit's engine
        /// directly rather than through the Test Framework's own runner, so
        /// there is no log scope for `LogAssert` to attach to and it fails
        /// with "No log scope is available" - measured, on this very test.
        /// Nothing else in the EditMode suites uses it, for the same reason.
        /// The warning is still emitted and is visible in
        /// `implementation/results/unity-EditMode.log`; a Unity test only
        /// FAILS on an unexpected error, so a warning needs no expectation.
        [Test]
        public void Show_ForAWaveThisBuildDoesNotAuthor_DrawsNothingRatherThanThrowing()
        {
            RequireGraphics();

            var host = new GameObject("lane-host");
            try
            {
                var stage = LaneStage.Create(host.transform);
                Assert.IsNull(stage.Show(999, new List<CreatureLook>(), new List<int>()),
                    "an unauthored wave produced a picture");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------
        // The framing
        // ---------------------------------------------------------------

        /// THE ONE THING A PIXEL COUNT CANNOT ANSWER, DONE AS ARITHMETIC.
        /// Every test above would pass with the lane's last pocket half off
        /// the right edge - a creature is still drawn, the picture still
        /// changes. Only eyes or this can say it is IN frame.
        ///
        /// It re-derives the half-extent from `LaneStage`'s own constants
        /// rather than restating 8.25, so a later change to
        /// `OrthographicSize` or to the texture's aspect reddens here instead
        /// of silently cropping a pocket. `Aim` centres on the pocket span,
        /// which is what this checks the consequence of: at the LANE's
        /// midpoint (12) the frame is [3.75, 20.25] and tile 20 sits 0.25
        /// inside it, which is half a creature off the picture.
        ///
        /// NO GRAPHICS NEEDED, so this runs on every runner.
        [Test]
        public void EveryPocketOfEveryAuthoredWaveStandsInsideTheFrame()
        {
            var halfExtent = LaneStage.OrthographicSize * LaneStage.Width / LaneStage.Height;
            Assert.AreEqual(8.25f, halfExtent, 0.01f,
                "the framing arithmetic in LaneStage.Aim's note is written against 8.25");

            foreach (var waveId in new[] { 1, 2, 6, 7 })
            {
                var lane = WaveDef.ForId(waveId).Lane;
                int min = lane.PocketTiles[0], max = min;
                for (var p = 1; p < lane.PocketCount; p++)
                {
                    if (lane.PocketTiles[p] < min) min = lane.PocketTiles[p];
                    if (lane.PocketTiles[p] > max) max = lane.PocketTiles[p];
                }

                var centre = (min + max) * 0.5f * WaveView.TileSize;
                var left = centre - halfExtent;
                var right = centre + halfExtent;

                for (var p = 0; p < lane.PocketCount; p++)
                {
                    var x = lane.PocketTiles[p] * WaveView.TileSize;

                    // A CREATURE IS NOT A POINT. `SpeciesRecipes`' bodies are
                    // about a unit across, so a pocket standing half a unit
                    // from the edge is a creature clipped in half. One unit
                    // of margin is the floor this asserts.
                    Assert.GreaterOrEqual(x - left, 1f,
                        "wave " + waveId + " pocket " + p + " is against the frame's left edge");
                    Assert.GreaterOrEqual(right - x, 1f,
                        "wave " + waveId + " pocket " + p + " is against the frame's right edge");
                }
            }
        }

        // ---------------------------------------------------------------

        static Color[] ReadPixels(RenderTexture texture)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = texture;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            read.Apply();
            RenderTexture.active = prev;

            var pixels = read.GetPixels();
            Object.DestroyImmediate(read);
            return pixels;
        }

        /// The camera clears to `LaneDressing.Field`, so "the stage drew
        /// something" means "pixels that are not that colour" rather than
        /// `PortraitStudio`'s "pixels that are not transparent". One channel
        /// level of tolerance, which is the threshold `capture-screens.sh`
        /// uses to tell antialiasing from a real change.
        static int PixelsUnlikeTheField(Color[] pixels)
        {
            var field = LaneDressing.Field;
            var unlike = 0;
            foreach (var p in pixels)
            {
                if (Mathf.Abs(p.r - field.r) > 1f / 255f
                    || Mathf.Abs(p.g - field.g) > 1f / 255f
                    || Mathf.Abs(p.b - field.b) > 1f / 255f) unlike++;
            }
            return unlike;
        }

        static int DifferingPixels(Color[] a, Color[] b)
        {
            var differing = 0;
            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                if (Mathf.Abs(a[i].r - b[i].r) > 1f / 255f
                    || Mathf.Abs(a[i].g - b[i].g) > 1f / 255f
                    || Mathf.Abs(a[i].b - b[i].b) > 1f / 255f) differing++;
            }
            return differing;
        }

        static int OpaquePixelCount(Color[] pixels)
        {
            var opaque = 0;
            foreach (var p in pixels) if (p.a > 0.5f) opaque++;
            return opaque;
        }
    }
}
