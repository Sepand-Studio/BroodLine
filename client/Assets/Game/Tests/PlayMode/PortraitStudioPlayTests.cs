using System.Collections;
using Broodline.Game.Shell;
using Broodline.Frontier;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Broodline.Game.PlayTests
{
    /// Task 11. `EditModeRunner` does not scan `Broodline.Game.PlayTests`
    /// (its own class comment: "PLAYMODE IS NOT COVERED") so this does not
    /// run headlessly and does not move the EditMode gate's count - it is
    /// written for the Editor Test Runner's PlayMode pass, which task-11's
    /// instructions defer to the next scheduled eyes-on checkpoint.
    public class PortraitStudioPlayTests
    {
        [UnityTest]
        public IEnumerator Show_UsesFrontierArtForEveryCompanion()
        {
            var host = new GameObject("studio-host");
            try
            {
                var studio = PortraitStudio.Create(host.transform);
                foreach (var species in FrontierRigDefinition.Companions)
                {
                    studio.Show(species, "cinder", "carapace", 1f);
                    yield return null;
                    var creature = studio.GetComponentInChildren<FrontierCreature>();
                    Assert.IsNotNull(creature, species + " did not use the new companion builder");
                    Assert.AreEqual(species, creature.SpeciesId);
                    Assert.IsNotNull(creature.Dorsal.Find("cinder"));
                    Assert.IsNotNull(creature.Flank.Find("carapace"));
                    var camera = studio.GetComponentInChildren<Camera>();
                    Assert.Greater(camera.orthographicSize, .5f);
                }
            }
            finally { Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator Show_ProducesANonBlankTexture_WithinAFewFrames()
        {
            var host = new GameObject("studio-host");
            var studio = PortraitStudio.Create(host.transform);
            var texture = (RenderTexture)studio.Show("vetch", "carapace", "taunt", 0f);
            for (int i = 0; i < 5; i++) yield return null;

            int opaque = OpaquePixelCount(texture);
            Assert.Greater(opaque, texture.width * texture.height / 50, "the studio rendered nothing");
            Assert.Less(opaque, texture.width * texture.height / 2, "the background must stay transparent");

            Object.Destroy(host);
        }

        /// The fix for the bug the Task 11 review caught: disabling the
        /// camera does not reset the texture it was painting, so a `Clear`
        /// that only stopped rendering would leave the last creature's
        /// frame sitting in GPU memory - and `Show` hands that exact
        /// `Texture` reference to whatever `CreatureStage` is displaying it,
        /// so a cleared studio would keep showing a stale creature behind a
        /// panel meant to be empty.
        [UnityTest]
        public IEnumerator Clear_MakesTheTextureTransparentAgain()
        {
            var host = new GameObject("studio-host");
            var studio = PortraitStudio.Create(host.transform);
            var texture = (RenderTexture)studio.Show("vetch", "carapace", "taunt", 0f);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.Greater(OpaquePixelCount(texture), 0, "precondition: the studio must have rendered something to clear");

            studio.Clear();
            yield return null;

            Assert.AreEqual(0, OpaquePixelCount(texture),
                "Clear must blank the render texture, not just stop drawing to it");

            Object.Destroy(host);
        }

        static int OpaquePixelCount(RenderTexture texture)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = texture;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            read.Apply();
            RenderTexture.active = prev;

            int opaque = 0;
            foreach (var p in read.GetPixels()) if (p.a > 0.5f) opaque++;
            Object.Destroy(read);
            return opaque;
        }
    }
}
