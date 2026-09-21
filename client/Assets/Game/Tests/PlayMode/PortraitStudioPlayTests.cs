using System.Collections;
using Broodline.Game.Shell;
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
        public IEnumerator Show_ProducesANonBlankTexture_WithinAFewFrames()
        {
            var host = new GameObject("studio-host");
            var studio = PortraitStudio.Create(host.transform);
            var texture = (RenderTexture)studio.Show("vetch", "carapace", "taunt", 0f);
            for (int i = 0; i < 5; i++) yield return null;

            var prev = RenderTexture.active;
            RenderTexture.active = texture;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            read.Apply();
            RenderTexture.active = prev;

            int opaque = 0;
            foreach (var p in read.GetPixels()) if (p.a > 0.5f) opaque++;
            Assert.Greater(opaque, texture.width * texture.height / 50, "the studio rendered nothing");
            Assert.Less(opaque, texture.width * texture.height / 2, "the background must stay transparent");

            Object.Destroy(read);
            Object.Destroy(host);
        }
    }
}
