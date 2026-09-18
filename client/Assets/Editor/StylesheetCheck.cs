using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// No namespace: Assembly-CSharp-Editor, as with ScreenshotCapture.

/// Asserts that every `.uss` under `Assets/` compiles to at least one rule.
///
///   Unity -batchmode -quit -projectPath client \
///         -executeMethod StylesheetCheck.Run -logFile &lt;path&gt;
///
/// Driven by `implementation/scripts/check-stylesheets.sh`.
///
/// WHY THIS EXISTS, AND WHY IT IS A GATE RATHER THAN A CONVENIENCE. A USS
/// file with a parse error still imports. `AssetDatabase.LoadAssetAtPath`
/// still hands back a non-null `StyleSheet`; `styleSheets.Add` still
/// accepts it; the panel still renders. It renders WITHOUT THE RULES, and
/// every `var(--token)` that depended on it resolves to nothing - not to a
/// fallback, not to magenta, not to an exception. Colour, padding, radius
/// and font-size all go at once, and the result looks like a screen nobody
/// has styled yet rather than like a broken file.
///
/// That is not hypothetical. `Tokens.uss` shipped in exactly that state:
/// its header comment cited a path containing a glob that closed the block
/// comment eighteen lines early, the tail of the comment was parsed as
/// source, and the file compiled to ZERO rules. Broodline's entire design
/// token layer was inert - in the Editor, in the harness, and in the built
/// player - and nothing caught it. Not the compiler, not 267 passing tests,
/// not a build. Unity logged four `LineBreakUnexpected` errors at import
/// and no one had reason to read an import log.
///
/// The check is one line of intent - a stylesheet with no rules is not a
/// stylesheet - and it is the cheapest possible guard against the most
/// expensive class of silent failure this UI has.
public static class StylesheetCheck
{
    public static void Run()
    {
        var failures = new List<string>();
        var checkedCount = 0;

        var rules = typeof(StyleSheet).GetProperty("rules",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (rules == null)
        {
            Debug.LogError("[StylesheetCheck] UnityEngine.UIElements.StyleSheet has no 'rules' property - "
                + "internals moved and this check needs a new hook. Failing rather than passing vacuously.");
            EditorApplication.Exit(3);
            return;
        }

        foreach (var path in AssetDatabase.FindAssets("t:StyleSheet")
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                                 && p.EndsWith(".uss", StringComparison.Ordinal))
                     .Distinct()
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            // Force the import so a parse error is re-logged on THIS run.
            // A cached-clean import is silent, and a silent import is how
            // four parse errors sat unread long enough to ship.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            checkedCount++;

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet == null)
            {
                failures.Add(path + " -> loaded as null");
                continue;
            }

            var count = rules.GetValue(sheet) is Array a ? a.Length : -1;

            // A file that is genuinely all comment is not a failure. Anything
            // with real content in it and no rules out is.
            var hasContent = File.ReadAllLines(Path.Combine(Application.dataPath, "..", path))
                .Select(l => l.Trim())
                .Any(l => l.Length > 0 && !l.StartsWith("*", StringComparison.Ordinal)
                                       && !l.StartsWith("/*", StringComparison.Ordinal));

            if (count <= 0 && hasContent)
                failures.Add($"{path} -> {count} rules, but the file has content. Parse error? "
                    + "Search this log for 'USS parsing error' at that path.");
            else
                Debug.Log($"[StylesheetCheck] ok  {count,4} rules  {path}");
        }

        if (failures.Count > 0)
        {
            Debug.LogError($"[StylesheetCheck] {failures.Count} of {checkedCount} stylesheets compiled to nothing:\n  "
                + string.Join("\n  ", failures));
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[StylesheetCheck] OK: {checkedCount} stylesheets, all compile to at least one rule.");
    }
}
