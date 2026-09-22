using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Broodline.View.Tests
{
    /// THE GATE FOR A DEFECT CLASS NO OTHER TEST IN THIS PROJECT CAN SEE, and
    /// the reader `RuntimeShaders` was written for - Phase 9 Task 21c.
    ///
    /// WHAT IT IS ANSWERING. `Shader.Find` searches the whole project in the
    /// Editor and only the BUILD's contents in a player, so a shader that no
    /// asset references and that Graphics Settings does not always-include is
    /// found in the Editor and null in the shipped app. That is what took the
    /// packaged app down at launch: `LaneDressing` asked for URP/Unlit, got
    /// null, and `new Material(null)` threw out of Unity's binding layer inside
    /// `BootController.Start`.
    ///
    /// WHY IT CHECKS THE LIST AND NOT THE LOOKUP, which is the whole trick. A
    /// test that called `Shader.Find` and asserted non-null would have passed
    /// on the broken project, every time, because this test runs in the Editor
    /// and in the Editor that call SUCCEEDS. No Editor-hosted test - EditMode
    /// or PlayMode - can reproduce the player's answer. So this asserts the
    /// PRECONDITION the player's answer depends on instead: that each name is
    /// present in the committed `ProjectSettings/GraphicsSettings.asset`. That
    /// is checkable from the Editor and it is exactly what was false.
    ///
    /// THE HONEST LIMIT. It gates names registered in `RuntimeShaders.All`. A
    /// raw `Shader.Find("...")` written somewhere else is outside it, which is
    /// why both runtime call sites were routed through `RuntimeShaders.Require`
    /// in the same change - there are two, and there were only ever two.
    public sealed class RuntimeShaderInclusionTests
    {
        const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";

        [Test]
        public void EveryShaderTheRuntimeFindsByName_IsAlwaysIncludedInTheBuild()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsPath);
            Assert.That(settings, Is.Not.Null.And.Not.Empty,
                "could not load " + GraphicsSettingsPath + " - this test cannot answer without it, " +
                "and a silent pass here would be worse than a failure.");

            var list = new SerializedObject(settings[0]).FindProperty("m_AlwaysIncludedShaders");
            Assert.That(list, Is.Not.Null,
                GraphicsSettingsPath + " has no m_AlwaysIncludedShaders property - the serialized " +
                "name changed and this gate is no longer reading anything.");

            var included = new HashSet<Object>();
            for (var i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (entry != null) included.Add(entry);
            }

            var unprotected = new List<string>();
            foreach (var name in RuntimeShaders.All)
            {
                var shader = Shader.Find(name);
                if (shader == null)
                {
                    unprotected.Add(name + "  <- not in this project AT ALL (the Editor cannot find it either)");
                    continue;
                }

                if (!included.Contains(shader))
                    unprotected.Add(name + "  <- in the project, NOT on the always-included list: " +
                                    "the Editor finds it and a player will not");
            }

            Assert.That(unprotected, Is.Empty,
                "These shaders are looked up by name at runtime but are not guaranteed to be in a " +
                "player build, so Shader.Find returns null there and the caller gets a null Shader:\n  " +
                string.Join("\n  ", unprotected) +
                "\n\nFix: `Unity -batchmode -quit -projectPath client -executeMethod Phase0Setup.Apply`, " +
                "then commit ProjectSettings/GraphicsSettings.asset.");
        }
    }
}
