using System;
using System.Collections.Generic;
using System.Reflection;
using Broodline.Frontier;
using Broodline.Game.Shell;
using Broodline.Sim.Combat;
using Broodline.UI.Components;
using Broodline.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Broodline.Game.Tests
{
    /// THE JOIN, WHICH NOTHING IN THIS PROJECT USED TO LOOK AT - Phase 9
    /// Task 21b.
    ///
    /// `LaneStage` had two suites and `LanePreviewCard` had the capture
    /// corpus, and the two halves were never put together anywhere:
    ///
    ///   - `LaneStageTests` and `LaneStagePlayTests` render the stage and
    ///     read the RENDER TEXTURE back. They prove a picture exists. They
    ///     never build a card, never attach a panel, and cannot tell whether
    ///     a single pixel of that picture reaches a screen.
    ///   - `ScreenFixtures.Deploy()` builds the card and passes NO `lane:` at
    ///     all - its own comment calls that "the designed state here rather
    ///     than a gap" - so all twenty captures show the fallback fill, which
    ///     is exactly what a broken join looks like.
    ///
    /// So "the deploy screen shows the real lane" was an inference over two
    /// separately-green suites, and the walk that produced Task 21b came back
    /// "the lane is empty" against both of them being green. This file is the
    /// missing middle: a real `LaneStage` texture, in a real
    /// `LanePreviewCard`, drawn through a real runtime UI Toolkit panel, read
    /// back as pixels.
    ///
    /// IT PROVES ITS OWN SENSITIVITY RATHER THAN ASSERTING IT, which is this
    /// project's standing requirement after two tests were found here that
    /// could not fail. Every case below renders the card TWICE - once with
    /// the stage's texture and once with `SetTexture(null)`, the documented
    /// fallback - and asserts against the DIFFERENCE. A join that silently
    /// dropped the texture would make the two renders identical and take the
    /// test red with it; there is no threshold to be generous with.
    ///
    /// WHAT IT DOES NOT COVER, SAID PLAINLY:
    ///
    ///   - **Play mode, and a device.** This runs in the Editor's EditMode
    ///     render path, the same one `LaneStageTests` uses. It proves the
    ///     texture reaches a UI Toolkit panel's pixels; it does not prove it
    ///     on a running player loop or on a phone. Nothing headless in this
    ///     project can - `run-unity-tests.sh:28-30`.
    ///   - **Whether the picture is any good.** It counts pixels that differ
    ///     from the fallback. A lane framed so badly that the creatures are
    ///     specks in a corner passes this easily; that is what
    ///     `EveryPocketStandsInsideTheRECTANGLETHECARDSHOWS_NotOnlyInside
    ///     TheTexture` does the arithmetic for, and what only eyes settle.
    ///   - **The shell around the card.** It hosts the card alone in a
    ///     366px column, not a whole `DeployView` in a scaffold. The corpus
    ///     owns that geometry.
    public class LaneCardJoinTests
    {
        /// The 390x844 `PanelSettings` reference resolution `BootSceneBuilder`
        /// writes, less `--gutter` twice. `LaneStageTests.TokenPixels` reads
        /// the token; this is the frame it is read against.
        const int ContentWidth = 366;
        const int PanelWidth = 390;
        const int PanelHeight = 932;
        const string Species = "vetch";
        const string PanelSettingsPath = "Assets/UI/Shell/PanelSettings.asset";

        static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("no graphics device - run without -nographics, as capture-screens.sh does");
            }
        }

        /// THE ONE ASSERTION THE WALK WOULD HAVE HAD TO AGREE WITH: the lane
        /// the stage painted is on the card's pixels, and it is not the flat
        /// fill.
        [Test]
        public void TheStagesPicture_ReachesTheCardsPixels_AndIsNotTheFallbackFill()
        {
            RequireGraphics();

            var host = new GameObject("lane-host");
            try
            {
                var stage = LaneStage.Create(host.transform);
                var texture = stage.Show(
                    1,
                    new List<FrontierLook>
                    {
                        new FrontierLook { Species = Species },
                        new FrontierLook { Species = Species },
                    },
                    new List<int> { 0, 1 });
                Assert.IsNotNull(texture, "wave 1 is authored; Show returned no texture for it");

                var withLane = RenderCard(texture, out var cardArea);
                var withoutLane = RenderCard(null, out _);

                Assert.Greater(cardArea, 20000,
                    "the card did not lay out - there is nothing to have measured");

                // THE FALLBACK IS THE CONTROL AND IT MUST BE FLAT. If this
                // ever stops being near-uniform the difference count below
                // stops meaning "the lane arrived" and starts meaning
                // "something moved", so it is checked rather than assumed.
                Assert.Less(PixelsUnlikeTheFill(withoutLane), cardArea / 20,
                    "the card's no-texture state is not the flat --green-tint fill this test compares against");

                // AND THE DIFFERENCE IS THE WHOLE TEST. Same card, same
                // panel, same layout; the only variable is whether
                // `SetTexture` was handed the stage's render texture. A join
                // that dropped it makes these two renders equal.
                var moved = DifferingPixels(withLane, withoutLane);
                Assert.Greater(moved, cardArea / 20,
                    "binding the stage's texture changed " + moved + " of the card's " + cardArea
                    + " pixels - the picture is not reaching the card, which is the deploy screen's "
                    + "flat --green-tint fallback and looks exactly like an empty lane");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// AND THE CREATURES SPECIFICALLY, not merely "something changed".
        /// The dressing alone would satisfy the case above - a rig that drew
        /// the field and the path and silently dropped every body passes it -
        /// and the deploy screen is ABOUT the creatures standing in their
        /// pockets. `LaneStageTests` makes this distinction in the render
        /// texture; this makes it on the card, where the crop can eat a
        /// creature the texture is perfectly happy with.
        [Test]
        public void TheCreaturesSpecifically_SurviveTheCardsCropAndReachItsPixels()
        {
            RequireGraphics();

            var host = new GameObject("lane-host");
            try
            {
                var stage = LaneStage.Create(host.transform);

                var dressingOnly = RenderCard(
                    stage.Show(1, new List<FrontierLook>(), new List<int>()), out var cardArea);

                var lane = WaveDef.ForId(1).Lane;
                var looks = new List<FrontierLook>();
                var pockets = new List<int>();
                for (var p = 0; p < lane.PocketCount; p++)
                {
                    looks.Add(new FrontierLook { Species = Species });
                    pockets.Add(p);
                }
                var everyPocket = RenderCard(stage.Show(1, looks, pockets), out _);

                // ONE BODY IS ABOUT A UNIT ACROSS AND THE CARD SHOWS 16 UNITS
                // OF LANE ACROSS 366px, so a creature is roughly 23px wide and
                // covers a few hundred pixels. Six of them is thousands. The
                // floor is deliberately far below that and still far above
                // antialiasing: what it has to separate is "the bodies are on
                // the card" from "the bodies were cropped off it", and a
                // cropped end pocket costs hundreds of pixels at a time.
                var moved = DifferingPixels(dressingOnly, everyPocket);
                Assert.Greater(moved, 1500,
                    "filling every pocket of wave 1 changed only " + moved + " of the card's " + cardArea
                    + " pixels - the creatures are not on the card the player sees");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// THE DEPLOY SCREEN OUTLIVES THE BEAT, AND A `Clear` UNDER IT USED TO
        /// EMPTY THE LANE - Phase 9 Task 21F, found on the exit gate's second
        /// device walk and reproduced before it was written down.
        ///
        /// `ScreenFlow.ShowAsync` presents with `after: null`, so the deploy
        /// screen STAYS the presented shell screen for the whole beat and
        /// after it. `LanePreviewCard` holds the stage's `RenderTexture` as a
        /// LIVE background, so it shows whatever is in that buffer whenever it
        /// draws. `FtueDirector.FightAsync` then clears the stage in a
        /// `finally` that spans the beat - and it has FOUR exits that show no
        /// other screen first (`CanDeploy` false, `StartAsync` throwing,
        /// `_play` throwing, an outbox submit that is not `Sent`). On every one
        /// of them the wipe landed on the card the player was looking at.
        ///
        /// MEASURED ON AN iPHONE 17 AT 402x874: background the packaged app
        /// mid-wave past `WaveHost.CompletionTimeoutSeconds`, and the deploy
        /// screen comes back with a flat `--green-tint` card - no path, no
        /// dashes, no trees, no creatures - while its slot strip still reads
        /// `A B C` filled and the fact cells still read `DEPLOYED 3/5`.
        /// Nothing logged: `Show` was never re-entered, so the card was never
        /// handed null; its buffer was emptied underneath it. That is why this
        /// case lives HERE rather than in `LaneStageTests` - the stage suites
        /// read the render texture, and a wiped texture is exactly what they
        /// used to demand.
        ///
        /// THE FALLBACK IS THE CONTROL, as everywhere else in this file, so
        /// the assertion is against a DIFFERENCE and not a threshold. Re-add
        /// the `GL.Clear` to `LaneStage.Clear` and the post-clear render
        /// becomes the fallback fill, which reddens both halves.
        [Test]
        public void AStageClearedUnderAPresentedCard_LeavesTheLaneDrawn_NotTheFallbackFill()
        {
            RequireGraphics();

            var host = new GameObject("lane-host");
            try
            {
                var stage = LaneStage.Create(host.transform);
                var texture = stage.Show(
                    1,
                    new List<FrontierLook>
                    {
                        new FrontierLook { Species = Species },
                        new FrontierLook { Species = Species },
                    },
                    new List<int> { 0, 1 });
                Assert.IsNotNull(texture, "wave 1 is authored; Show returned no texture for it");

                var beforeClear = RenderCard(texture, out var cardArea);
                var fallback = RenderCard(null, out _);
                Assert.Greater(cardArea, 20000,
                    "the card did not lay out - there is nothing to have measured");
                Assert.Greater(DifferingPixels(beforeClear, fallback), cardArea / 20,
                    "precondition: the lane is not reaching the card even before the Clear");

                // THE BEAT ENDS THE WAY A TIMED-OUT WAVE ENDS IT: the director's
                // `finally` clears the stage, and NOTHING replaces the screen.
                stage.Clear();

                var afterClear = RenderCard(texture, out _);

                Assert.Greater(DifferingPixels(afterClear, fallback), cardArea / 20,
                    "after the stage was cleared the card is drawing its flat --green-tint fallback - "
                    + "this is the deploy screen the walk reported as an empty lane");
                Assert.AreEqual(0, DifferingPixels(beforeClear, afterClear),
                    "the Clear changed what the still-presented card is showing");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------

        /// The card, in a real runtime panel, at the design frame's content
        /// width - and the pixels inside it, in the panel's own render target.
        ///
        /// `ScreenshotCapture.ForceRender`'s SHAPE, AND ITS REASONS ARE THAT
        /// FILE'S. There is no public API to make a runtime UI Toolkit panel
        /// draw one frame from inside a synchronous Editor call; `Repaint()`
        /// marks dirty and `Render()` is what issues geometry, both public on
        /// an internal type, both reached by reflection, both required. That
        /// file found this out over three blank runs and wrote it down; this
        /// does not re-derive it.
        ///
        /// THE PANEL SETTINGS ARE THE SHIPPED ASSET, NOT A FRESH INSTANCE,
        /// for that file's reason too: a `CreateInstance<PanelSettings>()`
        /// carries no default/SDF/atlas shaders and would render nothing,
        /// which is indistinguishable from the defect under test.
        static Color[] RenderCard(Texture lane, out int cardArea)
        {
            var target = new RenderTexture(
                PanelWidth, PanelHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();

            var shipped = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            Assert.IsNotNull(shipped, PanelSettingsPath + " is missing; this test cannot render the shell's own panel");
            var settings = UnityEngine.Object.Instantiate(shipped);
            settings.targetTexture = target;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.clearColor = true;
            settings.colorClearValue = Color.white;

            var go = new GameObject("LaneCardJoinTests") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var document = go.AddComponent<UIDocument>();
                document.panelSettings = settings;

                // The tokens only. `--green-tint` and `--lane-card-height` are
                // what the card's own sheet reads, and a card laid out without
                // them is a card with no height to measure.
                AddSheet(document.rootVisualElement, "Assets/UI/Shell/Tokens.uss");
                AddSheet(document.rootVisualElement, "Assets/UI/Shell/Theme.uss");
                document.rootVisualElement.AddToClassList("shell-root");

                var card = new LanePreviewCard();
                card.SetTexture(lane);
                card.SetSlots(new List<(string, bool)> { ("A", true), ("B", true) });

                var column = new VisualElement { style = { width = ContentWidth } };
                column.Add(card);
                var frame = new VisualElement { style = { width = PanelWidth, height = PanelHeight } };
                frame.Add(column);
                document.rootVisualElement.Add(frame);
                document.rootVisualElement.MarkDirtyRepaint();
                ForceRender(frame);

                var bounds = card.worldBound;
                var pixels = ReadBack(target);
                return Crop(pixels, bounds, out cardArea);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(settings);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        static void AddSheet(VisualElement root, string path)
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            Assert.IsNotNull(sheet, path + " is missing, so the card would lay out untokenized");
            root.styleSheets.Add(sheet);
        }

        static Color[] ReadBack(RenderTexture target)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = target;
            var read = new Texture2D(PanelWidth, PanelHeight, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, PanelWidth, PanelHeight), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            var pixels = read.GetPixels();
            UnityEngine.Object.DestroyImmediate(read);
            return pixels;
        }

        /// The card's own rectangle, inset four pixels so the rounded corners
        /// and the one-pixel edges of `--radius-card` are not what this is
        /// counting. `worldBound` is top-left origin and `ReadPixels` is
        /// bottom-left, so the rows are flipped on the way in.
        static Color[] Crop(Color[] panel, Rect bounds, out int area)
        {
            var x0 = Mathf.Clamp(Mathf.RoundToInt(bounds.xMin) + 4, 0, PanelWidth - 1);
            var x1 = Mathf.Clamp(Mathf.RoundToInt(bounds.xMax) - 4, 0, PanelWidth - 1);
            var top = Mathf.Clamp(Mathf.RoundToInt(bounds.yMin) + 4, 0, PanelHeight - 1);
            var bottom = Mathf.Clamp(Mathf.RoundToInt(bounds.yMax) - 4, 0, PanelHeight - 1);

            var cropped = new List<Color>();
            for (var y = PanelHeight - 1 - bottom; y <= PanelHeight - 1 - top; y++)
                for (var x = x0; x <= x1; x++)
                    cropped.Add(panel[y * PanelWidth + x]);

            area = cropped.Count;
            return cropped.ToArray();
        }

        /// `--green-tint`, which is `.lane-preview-card`'s fill AND
        /// `LaneStage`'s clear colour AND `LaneDressing.Field` - the three
        /// were made one colour on purpose so the seam between the texture
        /// and the card behind it is invisible. The cost of that decision is
        /// this test: a correct render and a missing one are the same colour
        /// over most of the card, so nothing short of a pixel difference can
        /// tell them apart.
        static int PixelsUnlikeTheFill(Color[] pixels)
        {
            var fill = LaneDressing.Field;
            var unlike = 0;
            foreach (var p in pixels)
            {
                if (Mathf.Abs(p.r - fill.r) > 2f / 255f
                    || Mathf.Abs(p.g - fill.g) > 2f / 255f
                    || Mathf.Abs(p.b - fill.b) > 2f / 255f) unlike++;
            }
            return unlike;
        }

        static int DifferingPixels(Color[] a, Color[] b)
        {
            var differing = 0;
            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                if (Mathf.Abs(a[i].r - b[i].r) > 2f / 255f
                    || Mathf.Abs(a[i].g - b[i].g) > 2f / 255f
                    || Mathf.Abs(a[i].b - b[i].b) > 2f / 255f) differing++;
            }
            return differing;
        }

        static void ForceRender(VisualElement root)
        {
            var panel = root.panel;
            Assert.IsNotNull(panel, "no panel attached after assigning UIDocument.panelSettings");

            var type = panel.GetType();
            MethodInfo Required(string name)
            {
                var m = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                Assert.IsNotNull(m, "UnityEngine.UIElements internals moved: " + type.FullName
                    + " has no public parameterless " + name + "(). This test needs a new hook, "
                    + "the same one ScreenshotCapture.ForceRender needs.");
                return m;
            }

            var repaint = Required("Repaint");
            var render = Required("Render");
            var update = type.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            string[] phases =
            {
                "UpdateAnimations", "UpdateBindings", "UpdateDataBinding",
                "TickSchedulingUpdaters", "UpdateAssetTrackers",
            };

            // Twice, for ScreenshotCapture's reason: the first pass resolves
            // layout for a tree that only just got added, the second draws
            // the resolved layout rather than the first frame's guesses.
            for (var pass = 0; pass < 2; pass++)
            {
                update?.Invoke(panel, null);
                foreach (var phase in phases)
                {
                    type.GetMethod(phase, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)
                        ?.Invoke(panel, null);
                }
                repaint.Invoke(panel, null);
                render.Invoke(panel, null);
            }
        }
    }
}
