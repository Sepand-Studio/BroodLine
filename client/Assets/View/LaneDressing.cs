using UnityEngine;

namespace Broodline.View
{
    /// The handoff's lane, as dressing: a pastel field, a path strip with
    /// dashes, soft blob trees, an Ark prism. None of it is read by anything;
    /// the lane stays straight because Lane's distance table is straight.
    public static class LaneDressing
    {
        // Mirrors: --paper #f7f4fb, --green-tint #e8f5ec, the handoff's path #e5d9c7, --violet #7a6ac0, --surface #ffffff.
        //
        /// PUBLIC, ALONE AMONG THE FIVE, AND FOR TWO READERS - Phase 9 Task
        /// 17.
        ///
        /// The first is `LaneStage`, which clears its camera to this colour
        /// so the ground past the dressing's finite quads is the same colour
        /// as the ground inside them - the call `WaveSceneBuilder` already
        /// made for its own camera, in a literal `new Color(0.91f, 0.96f,
        /// 0.93f)` that this constant is the source of.
        ///
        /// The second is what actually made the widening necessary rather
        /// than merely tidy: `LaneStageTests` and `LaneStagePlayTests` read
        /// it to THRESHOLD the render. "Did the stage draw a lane?" is
        /// "how many pixels are not the clear colour?", and a test that
        /// answered that against a copy of the hex would pass a stage whose
        /// clear colour had drifted away from the dressing's - which is the
        /// one failure the assertion exists to catch.
        ///
        /// Exposed rather than copied a fourth time. It is also
        /// `--green-tint`, which is `.lane-preview-card`'s own fill, so four
        /// files now depend on the five of them being one colour.
        public static readonly Color Field = Hex("#e8f5ec");
        static readonly Color Path = Hex("#e5d9c7");
        static readonly Color Dash = Hex("#ffffff");
        static readonly Color Tree = Hex("#cfe6d2");
        static readonly Color Ark = Hex("#7a6ac0");

        public static GameObject Build(Transform parent, int laneTiles, float tileSize, float pocketOffset)
        {
            var root = new GameObject("dressing");
            root.transform.SetParent(parent, false);
            float length = laneTiles * tileSize;

            Flat("field", root.transform, new Vector3(length * 0.5f, -0.02f, 0.5f), new Vector3(length + 6f, 0.02f, 8f), Field);
            Flat("path", root.transform, new Vector3(length * 0.5f, -0.01f, 0f), new Vector3(length + 1f, 0.02f, 0.9f), Path);
            for (int t = 0; t < laneTiles; t++)
                Flat("dash" + t, root.transform, new Vector3(t * tileSize + 0.5f, 0.0f, 0f), new Vector3(0.4f, 0.02f, 0.08f), Dash);

            var trees = new[] { new Vector3(3f, 0f, 2.6f), new Vector3(9f, 0f, -1.9f), new Vector3(15.5f, 0f, 2.9f), new Vector3(20f, 0f, -2.2f) };
            for (int i = 0; i < trees.Length; i++)
            {
                var tree = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tree.name = "tree" + i;
                tree.transform.SetParent(root.transform, false);
                tree.transform.localPosition = trees[i];
                tree.transform.localScale = new Vector3(1.6f, 0.7f, 1.6f);
                Paint(tree, Tree);
            }

            var ark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ark.name = "ark";
            ark.transform.SetParent(root.transform, false);
            ark.transform.localPosition = new Vector3(length, 0.5f, 0f);
            ark.transform.localScale = new Vector3(1.4f, 0.5f, 1.4f);
            Paint(ark, Ark);
            return root;
        }

        static void Flat(string name, Transform parent, Vector3 at, Vector3 size, Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            Paint(go, colour);
        }

        static void Paint(GameObject go, Color colour)
        {
            var r = go.GetComponent<Renderer>();
            // `RuntimeShaders.Require` AND NOT `Shader.Find`, since Phase 9 Task
            // 21c. This exact line was the packaged app's launch crash: in a
            // player `Shader.Find` answered null for a shader no asset
            // references and the always-included list did not carry, and
            // `new Material(null)` threw `ArgumentNullException` from inside
            // Unity's bindings - three inlined frames below the `BootController
            // .Start` the stack named. `RuntimeShaders` has the whole account.
            r.material = new Material(RuntimeShaders.Require(RuntimeShaders.Unlit)) { color = colour };

            // CreatePrimitive adds a Collider, and none of this dressing
            // should be pickable or physically solid. Object.Destroy is
            // DEFERRED - it runs at the end of the frame - and in EditMode
            // (this runs from an EditMode test, and from the scene builder's
            // batchmode -executeMethod, neither of which ticks a player loop
            // frame the way Play mode does) that end-of-frame never comes
            // before a caller inspects the tree, so the collider would still
            // be there when WaveViewTests or the scene asset was checked.
            // DestroyImmediate has no such delay.
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }

        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    }
}
