using UnityEngine;

namespace Broodline.View
{
    /// The shaders this project looks up BY NAME at runtime, and the single
    /// place that knows such a name must ALSO be on Graphics Settings'
    /// "Always Included Shaders" list.
    ///
    /// WHY THIS FILE EXISTS - Phase 9 Task 21c. It is the fix for a crash that
    /// only a packaged player could ever show, and the Editor could not.
    ///
    /// `Shader.Find` IN THE EDITOR SEARCHES THE WHOLE PROJECT. In a PLAYER it
    /// can only find what the build actually contains, and a build contains a
    /// shader only if some asset in it references that shader or the shader is
    /// on the always-included list. `LaneDressing.Paint` asked for
    /// "Universal Render Pipeline/Unlit"; NO material asset in this project
    /// uses that shader, and it was not on the list. So in the simulator build
    /// `Shader.Find` returned null, `new Material(null)` threw
    /// `ArgumentNullException` out of Unity's binding layer, and
    /// `BootController.Start` died before the first screen was ever shown.
    ///
    /// WHY NOTHING CAUGHT IT. Every test in this project runs in the Editor,
    /// where that `Find` succeeds - so no EditMode or PlayMode test could
    /// reproduce it no matter how it was written. What `RuntimeShaderInclusion
    /// Tests` does instead is check the LIST rather than the lookup, which is
    /// the one form of this question the Editor can answer honestly.
    ///
    /// NOTE WHAT THIS IS NOT: it is SHADER inclusion, not managed-code
    /// stripping. `stripEngineCode` and the absent `link.xml` were suspects and
    /// were not the cause; a `link.xml` would not have prevented this and does
    /// not prevent it now. Shaders are assets, and assets are kept or dropped
    /// by asset dependency, not by the IL linker.
    ///
    /// THE TWO READERS: `Phase0Setup.Apply` registers every name here on the
    /// always-included list, and `RuntimeShaderInclusionTests` fails if any
    /// name here is missing from the committed `ProjectSettings/Graphics
    /// Settings.asset`. Add a name HERE before using it in a `Shader.Find`,
    /// and the gate covers it from that moment.
    public static class RuntimeShaders
    {
        /// Lit surfaces. `SyntheticCreature.Build` makes one material per
        /// entity from this.
        public const string Lit = "Universal Render Pipeline/Lit";

        /// The painted ground and pooled battle cues use this unlit shader.
        ///
        /// THIS IS THE ONE THAT WAS MISSING, and it was missing because no
        /// material ASSET uses it - these effects build their materials in code.
        /// URP/Lit was on the list from Phase 0 and so never showed the
        /// problem, which is why the failure waited for the first rig that
        /// wanted a second shader.
        public const string Unlit = "Universal Render Pipeline/Unlit";

        /// The Living Frontier creature and scenery surface. `FrontierArt`
        /// builds its one shared material from this in code, so as with Unlit
        /// no material ASSET references it; registered here in Phase 10 Task
        /// 1.0, the moment the art core became a production dependency.
        public const string Frontier = "Broodline/FrontierSurface";

        /// Every name above. Iterated by the registrar and by the gate; a name
        /// that is not in here is a name nothing protects.
        public static readonly string[] All = { Lit, Unlit, Frontier };

        /// `Shader.Find`, except that a null answer is a sentence naming the
        /// shader instead of an `ArgumentNullException` raised two frames later
        /// from inside a binding, with no shader name anywhere in it.
        ///
        /// IT STILL THROWS, AND THAT IS DELIBERATE. A missing shader is a
        /// build-configuration defect, not a runtime condition to degrade
        /// through: Unity's fallback for a null material is magenta, which on
        /// the lane card is a silent visual defect that a capture would have to
        /// be looked at to catch. The gate above means this throw cannot reach
        /// a shipped build; if it ever does fire, the message is the diagnosis.
        public static Shader Require(string name)
        {
            var shader = Shader.Find(name);
            if (shader != null) return shader;

            throw new System.InvalidOperationException(
                "shader \"" + name + "\" is not in this build. Shader.Find succeeds in the " +
                "Editor and returns null in a player unless the shader is referenced by an " +
                "asset or listed in Graphics Settings > Always Included Shaders. Add it to " +
                "Broodline.View.RuntimeShaders.All and run " +
                "`-executeMethod Phase0Setup.Apply` to register it; " +
                "RuntimeShaderInclusionTests is the gate that should have caught this.");
        }
    }
}
