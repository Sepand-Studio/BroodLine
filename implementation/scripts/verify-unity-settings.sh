#!/usr/bin/env bash
# Asserts the Unity settings that are expensive to discover late.
# Only checks values that live in COMMITTED files. The active build target is
# deliberately not checked: it lives in client/Library/, which is gitignored.
set -uo pipefail
cd "$(dirname "$0")/../.."
fail=0
ok ()  { printf '  ok    %s\n' "$1"; }
bad () { printf '  FAIL  %s\n  ->  %s\n' "$1" "$2"; fail=1; }

E=client/ProjectSettings/EditorSettings.asset
P=client/ProjectSettings/ProjectSettings.asset
G=client/ProjectSettings/GraphicsSettings.asset
M=client/Packages/manifest.json

[ -d client ] || { echo "FAIL: no client/ project — Task 2 has not run"; exit 1; }

grep -q 'm_SerializationMode: 2' "$E" \
  && ok "Asset Serialization: Force Text" \
  || bad "Asset Serialization is not Force Text" "Project Settings > Editor > Asset Serialization > Force Text"

# Unity 6 omits m_ExternalVersionControlSupport when it is the default (Visible
# Meta Files), so assert the observable consequence instead of the key.
[ -n "$(find client/Assets -name '*.meta' -print -quit 2>/dev/null)" ] \
  && ok "Visible Meta Files (.meta files present)" \
  || bad "No .meta files under client/Assets" "Project Settings > Editor > Version Control > Visible Meta Files"

grep -q 'defaultScreenOrientation: 0' "$P" \
  && ok "Default orientation: Portrait" \
  || bad "Default orientation is not Portrait ($(grep -m1 defaultScreenOrientation "$P" | tr -d ' '))" \
         "Player > Resolution and Presentation > Default Orientation > Portrait"

for k in allowedAutorotateToPortraitUpsideDown allowedAutorotateToLandscapeLeft allowedAutorotateToLandscapeRight; do
  grep -q "$k: 0" "$P" && ok "$k disabled" \
    || bad "$k is enabled" "Player > Resolution and Presentation > untick it"
done

grep -q 'com.unity.render-pipelines.universal' "$M" \
  && ok "URP package present" || bad "URP missing from manifest.json" "install com.unity.render-pipelines.universal"

# Unity stores Always Included Shaders by GUID, never by name, so assert the
# GUID of URP's Lit.shader rather than the string a human would recognise.
URP_LIT_GUID=933532a4fcc9baf4fa0491de14d08ed7
grep -q "$URP_LIT_GUID" "$G" \
  && ok "URP/Lit in Always Included Shaders" \
  || bad "URP/Lit not in Always Included Shaders" \
         "Project Settings > Graphics > Always Included Shaders > + > Universal Render Pipeline/Lit  (without it Shader.Find returns null in a build and every device run in Task 5 renders magenta)"

echo
[ $fail -eq 0 ] && echo "Task 2 settings verified." || echo "Fix what is marked FAIL in the Unity editor, then re-run."
exit $fail
