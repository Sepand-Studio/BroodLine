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
    /// Runner's PlayMode pass, beside `PortraitStudioPlayTests` and
    /// `WaveCapturePlayTests`.
    ///
    /// IT IS NO LONGER THE ONLY EVIDENCE THAT THIS RIG DRAWS, WHICH IS THE
    /// POINT OF FIX ROUND 1. `LaneStageTests` (EditMode, `Broodline.Game
    /// .Tests`) now renders the stage and reads the pixels back on every
    /// gate. That became possible when `Show` stopped enabling the camera and
    /// started painting one frame on demand through
    /// `Camera.SubmitRenderRequest`, which is synchronous and needs no player
    /// loop. Everything about "does the lane draw, are the creatures on it,
    /// does Clear blank it" is answered there, headlessly, every run.
    ///
    /// SO WHAT IS LEFT HERE IS THE TWO THINGS EDITMODE CANNOT REACH:
    ///
    ///   - **A real player loop.** `LaneStageTests` proves the picture exists
    ///     after a synchronous request in the Editor's own render path. This
    ///     proves it on a running frame, which is where a player sees it.
    ///     A failure here with EditMode green means the render request works
    ///     in the Editor and not in Play mode - a pipeline-asset or
    ///     render-path difference, and the app would show the deploy screen's
    ///     flat green fallback with nothing to say so.
    ///   - **`Clear`'s DEFERRED destroy.** In Play mode `Object.Destroy` runs
    ///     at the end of the frame, not immediately; outside it `Clear` takes
    ///     the `DestroyImmediate` branch. A failure here means a second
    ///     `Show` can build creatures on top of a set that has not gone yet,
    ///     which would put two waves in one picture. No EditMode test can see
    ///     that, because EditMode never reaches an end of frame.
    ///
    /// AND WHAT NEITHER SUITE CHECKS: whether the framing LOOKS right. They
    /// count pixels. `LaneStageTests.EveryPocketOfEveryAuthoredWaveStands
    /// InsideTheFrame` does the arithmetic - every pocket of every authored
    /// lane, with a unit of margin - but only eyes can say the pockets read
    /// as a row. Step 5's `Boot.unity` run is where that is confirmed.
    public class LaneStagePlayTests
    {
        /// "vetch" is the species `PortraitStudioPlayTests` uses and one of
        /// the six `SpeciesRecipes` carries. A species with no recipe answers
        /// with a magenta "missing" sphere AND a `Debug.LogError`, which
        /// Unity's Test Framework fails a PlayMode test on - so an unauthored
        /// body would redden this for a reason that has nothing to do with
        /// the stage.
        const string Species = "vetch";

        [UnityTest]
        public IEnumerator Show_DrawsTheLaneAndTheCreatures_OnARunningFrame()
        {
            var host = new GameObject("lane-host");
            var stage = LaneStage.Create(host.transform);

            // The dressing alone, with nobody standing in it.
            var texture = (RenderTexture)stage.Show(1, new List<CreatureLook>(), new List<int>());
            Assert.IsNotNull(texture, "wave 1 is authored; Show returned no texture for it");

            // ONE FRAME, THOUGH `Show` NO LONGER NEEDS IT. The request is
            // synchronous, so the picture is already there - this yields so
            // the assertions below are read on a frame the player loop has
            // actually run, which is the only thing this file adds over
            // `LaneStageTests`.
            yield return null;

            var empty = ReadPixels(texture);
            Assert.Greater(PixelsUnlikeTheField(empty), texture.width * texture.height / 50,
                "the stage rendered nothing but its clear colour on a running frame");

            var withCreature = (RenderTexture)stage.Show(
                1,
                new List<CreatureLook> { new CreatureLook { Species = Species } },
                new List<int> { 0 });
            yield return null;

            Assert.AreSame(texture, withCreature, "Show handed out a second texture; the card holds the first");
            Assert.Greater(DifferingPixels(empty, ReadPixels(withCreature)), 200,
                "putting a creature in pocket 0 did not change the picture");

            // AND THE CAMERA IS NOT LEFT RUNNING. `FtueDirector`'s `finally`
            // spans the hosted wave, so a camera enabled here would paint a
            // 720x480 target every frame of the fight. Checked in Play mode
            // as well as in EditMode because this is the mode where a live
            // camera would actually cost something.
            var camera = host.GetComponentInChildren<Camera>(includeInactive: true);
            Assert.IsFalse(camera.enabled, "the stage left its camera running");

            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator Clear_MakesTheTextureTransparentAgain_AndItsDestroyIsDeferredHere()
        {
            var host = new GameObject("lane-host");
            var stage = LaneStage.Create(host.transform);
            var texture = (RenderTexture)stage.Show(
                1,
                new List<CreatureLook> { new CreatureLook { Species = Species } },
                new List<int> { 0 });
            yield return null;
            Assert.Greater(OpaquePixelCount(ReadPixels(texture)), 0,
                "precondition: the stage must have rendered something to clear");

            stage.Clear();
            yield return null;

            Assert.AreEqual(0, OpaquePixelCount(ReadPixels(texture)),
                "Clear must blank the render texture, not just stop drawing to it");

            // THE DEFERRED DESTROY, WHICH IS WHY THIS CASE IS IN PLAY MODE.
            // `Clear` takes the `Object.Destroy` branch here and the
            // `DestroyImmediate` branch everywhere else. After the frame
            // above has ended, the creatures must actually be gone - a second
            // `Show` building on top of a set that is still standing would
            // put two waves in one picture, and the pixel assertion above
            // would not notice.
            Assert.IsNull(host.transform.Find("lane-stage/creatures"),
                "the creatures survived the end of the frame Clear was called on");

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
