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
    /// what does Clear do to the picture" is answered there, headlessly,
    /// every run.
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
    ///     THAT SECOND BULLET DESCRIBED A DEFECT THIS FILE DID NOT ACTUALLY
    ///     CATCH, AND A DEVICE FOUND IT - Phase 9 Task 21f. The case below it
    ///     watched the destroy LAND at the end of the frame, which it always
    ///     did; what nothing watched was the frame `Paint` takes BEFORE it,
    ///     which is the only frame the card ever keeps.
    ///     `ASecondShowInTheSameFrame_DoesNotPaintThePreviousDeployments
    ///     Bodies` is the arm for it.
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
            // 640x480 target every frame of the fight. Checked in Play mode
            // as well as in EditMode because this is the mode where a live
            // camera would actually cost something.
            var camera = host.GetComponentInChildren<Camera>(includeInactive: true);
            Assert.IsFalse(camera.enabled, "the stage left its camera running");

            Object.Destroy(host);
        }

        /// THIS CASE ASSERTED THE OPPOSITE UNTIL PHASE 9 TASK 21F, under the
        /// name `Clear_MakesTheTextureTransparentAgain_AndItsDestroyIsDeferred
        /// Here`. `LaneStage.Clear` no longer wipes the render texture, and
        /// its own note carries why: the card holds that texture as a LIVE
        /// background and `ScreenFlow` presents with `after: null`, so the
        /// wipe landed on a deploy screen the player was still looking at.
        /// Reproduced on an iPhone 17 by backgrounding the app mid-wave past
        /// `WaveHost.CompletionTimeoutSeconds`.
        [UnityTest]
        public IEnumerator Clear_LeavesThePicture_AndItsDestroyIsStillDeferredHere()
        {
            var host = new GameObject("lane-host");
            var stage = LaneStage.Create(host.transform);
            var texture = (RenderTexture)stage.Show(
                1,
                new List<CreatureLook> { new CreatureLook { Species = Species } },
                new List<int> { 0 });
            yield return null;
            var painted = ReadPixels(texture);
            Assert.Greater(OpaquePixelCount(painted), 0,
                "precondition: the stage must have rendered something for Clear to leave alone");

            stage.Clear();
            yield return null;

            Assert.AreEqual(0, DifferingPixels(painted, ReadPixels(texture)),
                "Clear changed the picture a LanePreviewCard may still be showing - re-adding its "
                + "GL.Clear is the Task 21f defect, and the card falls back to its flat fill");

            // THE DEFERRED DESTROY, WHICH IS WHY THIS CASE IS IN PLAY MODE.
            // `Clear` takes the `Object.Destroy` branch here and the
            // `DestroyImmediate` branch everywhere else. After the frame
            // above has ended, the creatures must actually be gone. Task 21f
            // added a `SetActive(false)` before that destroy; this assertion
            // is what stops the deactivation being mistaken for a release.
            Assert.IsNull(host.transform.Find("lane-stage/creatures"),
                "the creatures survived the end of the frame Clear was called on");

            Object.Destroy(host);
        }

        /// THE FRAME THE CARD KEEPS, AND THE ONE DEFECT NO EDITMODE TEST CAN
        /// SEE - Phase 9 Task 21F, found on a device before it was written
        /// down here.
        ///
        /// `Show` calls `Clear`, builds the new bodies and `Paint`s ONE frame,
        /// all synchronously. In play mode `Clear`'s `Object.Destroy` does not
        /// run until the END of that frame, so the single painted frame caught
        /// the PREVIOUS deployment's bodies still alive and still rendering -
        /// and because `Paint` is one-shot, nothing ever repaired it. On the
        /// deploy screen a roster toggled down to `DEPLOYED 1/5` drew TWO
        /// creatures, with the removed one still standing in pocket B and the
        /// slot strip beside it correctly showing one.
        ///
        /// EDITMODE CANNOT REACH THIS. There `Clear` takes the
        /// `DestroyImmediate` branch, so the bodies are gone before `Paint`
        /// runs and the case passes on the broken code - the could-not-fail
        /// shape this phase has found repeatedly. It is written here, where
        /// the deferral is real, and it HAS NOT BEEN RUN: see this file's
        /// class comment.
        ///
        /// THE REFERENCE IS A SECOND STAGE THAT ONLY EVER DREW THE SECOND
        /// DEPLOYMENT, rather than a pixel threshold to be generous with. Two
        /// stages given the same wave and the same bodies paint the same
        /// frame, so any difference is the stale body. Drop the
        /// `SetActive(false)` from `LaneStage.Clear` and pocket 2's creature
        /// survives into the comparison, which is thousands of pixels.
        [UnityTest]
        public IEnumerator ASecondShowInTheSameFrame_DoesNotPaintThePreviousDeploymentsBodies()
        {
            var host = new GameObject("lane-host");
            var stage = LaneStage.Create(host.transform);

            // Two bodies, then - WITHOUT yielding, which is the whole case -
            // one. This is what a tap on a field row does: `FtueDirector`'s
            // `redraw` closure re-runs `ShowLane` inside the click handler.
            stage.Show(
                1,
                new List<CreatureLook> { new CreatureLook { Species = Species }, new CreatureLook { Species = Species } },
                new List<int> { 0, 2 });
            var reduced = (RenderTexture)stage.Show(
                1,
                new List<CreatureLook> { new CreatureLook { Species = Species } },
                new List<int> { 0 });
            var afterToggle = ReadPixels(reduced);

            var cleanHost = new GameObject("clean-lane-host");
            var clean = LaneStage.Create(cleanHost.transform);
            var reference = (RenderTexture)clean.Show(
                1,
                new List<CreatureLook> { new CreatureLook { Species = Species } },
                new List<int> { 0 });
            var never = ReadPixels(reference);

            Assert.Greater(OpaquePixelCount(never), 0,
                "precondition: the reference stage painted nothing, so it proves nothing");
            Assert.AreEqual(0, DifferingPixels(never, afterToggle),
                "the frame the card keeps still has the previous deployment's bodies in it - "
                + "Clear's Destroy is deferred to the end of the frame and Paint runs before it");

            Object.Destroy(host);
            Object.Destroy(cleanHost);
            yield return null;
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
