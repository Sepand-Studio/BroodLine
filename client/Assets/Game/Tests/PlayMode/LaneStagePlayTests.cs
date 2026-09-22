using System.Collections;
using System.Collections.Generic;
using Broodline.Creatures;
using Broodline.Game.Shell;
using Broodline.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Broodline.Game.PlayTests
{
    /// Phase 9 Task 17. `EditModeRunner` does not scan
    /// `Broodline.Game.PlayTests` (its own class comment: "PLAYMODE IS NOT
    /// COVERED"), and `implementation/scripts/run-unity-tests.sh:28-30` says
    /// the PlayMode path "is EXPECTED TO DEADLOCK on this Editor" - so this
    /// file does not run headlessly, does not move the EditMode gate's count,
    /// and HAS NOT BEEN RUN. It is written for a human in the Editor Test
    /// Runner's PlayMode pass, and it is the third such debt in this
    /// assembly beside `PortraitStudioPlayTests` and `WaveCapturePlayTests`.
    ///
    /// WHAT A HUMAN SHOULD SEE, so that a test nobody here can run is still
    /// worth having written:
    ///
    ///   - `Show_DrawsTheLaneAndThenTheCreaturesOnTopOfIt` passes when the
    ///     stage renders a lane at all AND when adding a creature changes the
    ///     picture. A failure on the first assertion means the rig is dark -
    ///     a camera on the wrong layer, a missing URP Unlit shader, a render
    ///     texture that never got a frame - and the deploy screen would show
    ///     a flat green card in the app with nothing to say so, which is
    ///     indistinguishable from the no-stage fallback. A failure on the
    ///     SECOND means the dressing draws and the creatures do not: a
    ///     species with no recipe, a pocket index the lane does not have, or
    ///     the creatures parented outside the camera's frustum - which is
    ///     the whole point of this screen, and the one thing the capture
    ///     corpus can never see.
    ///   - `Clear_MakesTheTextureTransparentAgain` passes when a cleared
    ///     stage stops showing the last wave. A failure means
    ///     `FtueDirector`'s `finally` leaves a stale lane in GPU memory that
    ///     the NEXT deploy screen would show for its first frames - the
    ///     defect `PortraitStudio.Clear`'s own note records from the
    ///     portrait side.
    ///
    /// AND WHAT NEITHER OF THEM CHECKS: whether the framing is right. These
    /// count pixels; only eyes can say the pockets read as a row and that no
    /// creature is half off the edge. `LaneStage.Aim` carries the arithmetic
    /// that was done instead, and Step 5's `Boot.unity` run is where it is
    /// confirmed.
    public class LaneStagePlayTests
    {
        /// "vetch" is the species `PortraitStudioPlayTests` uses and the one
        /// `WaveView`'s own comment names as having a recipe. A species
        /// without one answers with a magenta "missing" sphere AND a
        /// `Debug.LogError`, which Unity's Test Framework fails a PlayMode
        /// test on - so a body that is not authored yet would fail this test
        /// for a reason that has nothing to do with the stage.
        const string Species = "vetch";

        [UnityTest]
        public IEnumerator Show_DrawsTheLaneAndThenTheCreaturesOnTopOfIt()
        {
            var host = new GameObject("lane-host");
            var stage = LaneStage.Create(host.transform);

            // The dressing alone, with nobody standing in it.
            var texture = (RenderTexture)stage.Show(1, new List<CreatureLook>(), new List<int>());
            for (var i = 0; i < 5; i++) yield return null;

            Assert.IsNotNull(texture, "wave 1 is authored; Show returned no texture for it");
            var empty = ReadPixels(texture);
            Assert.Greater(PixelsUnlikeTheField(empty), texture.width * texture.height / 50,
                "the stage rendered nothing but its clear colour - the lane itself is not drawing");

            // And now with one creature in pocket 0. THE DIFFERENCE IS THE
            // ASSERTION: a rig that drew the dressing and silently dropped
            // the creatures would pass everything above.
            var withCreature = (RenderTexture)stage.Show(
                1,
                new List<CreatureLook> { new CreatureLook { Species = Species } },
                new List<int> { 0 });
            for (var i = 0; i < 5; i++) yield return null;

            Assert.AreSame(texture, withCreature, "Show handed out a second texture; the card holds the first");
            Assert.Greater(DifferingPixels(empty, ReadPixels(withCreature)), 200,
                "adding a creature to pocket 0 did not change the picture");

            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator Clear_MakesTheTextureTransparentAgain()
        {
            var host = new GameObject("lane-host");
            var stage = LaneStage.Create(host.transform);
            var texture = (RenderTexture)stage.Show(
                1,
                new List<CreatureLook> { new CreatureLook { Species = Species } },
                new List<int> { 0 });
            for (var i = 0; i < 5; i++) yield return null;
            Assert.Greater(OpaquePixelCount(ReadPixels(texture)), 0,
                "precondition: the stage must have rendered something to clear");

            stage.Clear();
            yield return null;

            Assert.AreEqual(0, OpaquePixelCount(ReadPixels(texture)),
                "Clear must blank the render texture, not just stop drawing to it");

            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator Show_ForAWaveThisBuildDoesNotAuthor_DrawsNothingRatherThanThrowing()
        {
            // `WaveDef.ForId` throws `WaveCompositionException` for an
            // unknown id BY DESIGN, and Campaign Select can reach one because
            // it renders whatever `config.waves` sends. A picture must not be
            // the thing that strands a turn - `LaneStage.Show`'s own comment
            // has the full argument - so the contract is a null and a
            // warning, and the loud failure keeps its own site at
            // `FtueDirector`'s guarded `_play` call.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[lane-stage\]"));

            var host = new GameObject("lane-host");
            var stage = LaneStage.Create(host.transform);

            Assert.IsNull(stage.Show(999, new List<CreatureLook>(), new List<int>()),
                "an unauthored wave produced a picture");
            yield return null;

            Object.Destroy(host);
        }

        static Color[] ReadPixels(RenderTexture texture)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = texture;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            read.Apply();
            RenderTexture.active = prev;

            var pixels = read.GetPixels();
            Object.Destroy(read);
            return pixels;
        }

        /// The camera clears to `LaneDressing.Field`, so "the stage drew
        /// something" means "pixels that are not that colour" rather than
        /// `PortraitStudio`'s "pixels that are not transparent". The
        /// tolerance is one channel level either way, which is the same
        /// threshold `capture-screens.sh` uses to tell antialiasing from a
        /// real change.
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
