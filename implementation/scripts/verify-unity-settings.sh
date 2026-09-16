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

grep -q 'enableFrameTimingStats: 1' "$P" \
  && ok "Frame timing stats enabled" \
  || bad "enableFrameTimingStats is off" \
         "run Broodline > Apply Phase 0 Setup  (FrameTimingManager returns NO data without it, so the sweep's cpu_ms and gpu_ms columns come back empty)"

grep -q 'com.unity.render-pipelines.universal' "$M" \
  && ok "URP package present" || bad "URP missing from manifest.json" "install com.unity.render-pipelines.universal"

# The two platforms must carry the SAME scripting defines, and nothing noticed
# when they stopped.
#
# Unity rewrites the define list onto whichever platform is not the active build
# target, so switching between the benchmark's iOS build and the determinism
# player's Standalone build leaves the project dirty in one direction or the
# other. b0f6385 removed SENTIS_ANALYTICS_ENABLED from both for exactly that
# reason; a package write put it back on Standalone only, and a `git add -A`
# swept it into a commit about re-capturing a device artifact.
#
# The cost is not cosmetic: cross-runtime-diff.sh builds a StandaloneOSX IL2CPP
# player and compares it against the iPhone player, so a define present on one
# and absent from the other means the gate is comparing two different
# compilations. determinism.yml watches this file with the note "a change here
# can silently gut the gate", and this is the check that makes that watch mean
# something.
#
# Read the platform lines from INSIDE the scriptingDefineSymbols block. A bare
# grep for "Standalone:" finds applicationIdentifier's first and reports the
# bundle id as a define list - which this check did, and which is its own small
# lesson about pinning a YAML value by grep.
defines_for () {
  awk -v want="$1:" '
    /^  scriptingDefineSymbols:/ { inblock = 1; next }
    inblock && /^  [^ ]/         { exit }
    inblock && $1 == want        { sub(/^[[:space:]]*[A-Za-z]+:[[:space:]]*/, ""); print; exit }
  ' "$P" | tr -d ' \r'
}
# FAIL CLOSED. awk prints nothing and exits 0 when the block is absent, so a
# renamed key - Unity has done this before - made both platforms read as empty,
# compare equal, and report ok while the real values differed. A guard added to
# catch a silent pass must not have one of its own.
grep -q '^  scriptingDefineSymbols:' "$P" \
  && ok "scriptingDefineSymbols block found" \
  || bad "no scriptingDefineSymbols block in $P" \
         "Unity may have renamed the key. Until this check can read the block it cannot compare the platforms, and it must not report ok."

DEFINES_STANDALONE=$(defines_for Standalone)
DEFINES_IPHONE=$(defines_for iPhone)
[ "$DEFINES_STANDALONE" = "$DEFINES_IPHONE" ] \
  && ok "Scripting defines match across Standalone and iPhone ($DEFINES_STANDALONE)" \
  || bad "Scripting defines differ: Standalone='$DEFINES_STANDALONE' iPhone='$DEFINES_IPHONE'" \
         "Player > Other Settings > Scripting Define Symbols - make both platforms match, then commit ProjectSettings.asset. cross-runtime-diff.sh compares a Standalone IL2CPP player against the iPhone one; a define on only one side means the two are not the same program."

# Unity stores Always Included Shaders by GUID, never by name, so assert the
# GUID of URP's Lit.shader rather than the string a human would recognise.
URP_LIT_GUID=933532a4fcc9baf4fa0491de14d08ed7
grep -q "$URP_LIT_GUID" "$G" \
  && ok "URP/Lit in Always Included Shaders" \
  || bad "URP/Lit not in Always Included Shaders" \
         "Project Settings > Graphics > Always Included Shaders > + > Universal Render Pipeline/Lit  (without it Shader.Find returns null in a build and every device run in Task 5 renders magenta)"

# --- The client floor coupling. config/bundles/<highest>/manifest.json's
# minimumClientVersion is what routes/sync.ts refuses clients below with 426;
# ProjectSettings.asset's bundleVersion is what the client sends. Phase 6
# shipped with the floor at 0.1.0 and the client at 0.2.0 - inert - and then
# found parseStart had made the OLD client unusable while the floor still
# told it it was current. The two halves moved in a8e5785; this is what
# keeps them moving together.
#
# FAIL CLOSED: if the bundle listing is empty, or either version string can't
# be read, floor and/or client come back empty below. Both branches of the
# guard's condition already route an empty value to `bad`, never a skip -
# a guard that can't compare the two halves must not report ok.
highest_bundle=$(ls -d config/bundles/*/ 2>/dev/null | sed 's#config/bundles/##; s#/##' | sort -V | tail -1)
floor=$(sed -n 's/.*"minimumClientVersion": *"\([0-9.]*\)".*/\1/p' "config/bundles/$highest_bundle/manifest.json" 2>/dev/null)
client=$(sed -n 's/^  bundleVersion: *\([0-9.]*\).*/\1/p' "$P")
lowest=$(printf '%s\n%s\n' "$floor" "$client" | sort -V | head -1)
if [ -n "$floor" ] && [ -n "$client" ] && [ "$lowest" = "$floor" ]; then
  ok "client $client is not below bundle $highest_bundle's minimumClientVersion $floor"
else
  bad "client bundleVersion '$client' is below bundle $highest_bundle's minimumClientVersion '$floor' (or one is unreadable)" \
      "move ProjectSettings.asset bundleVersion and the manifest's floor TOGETHER - a player told they are current while every wave they start is refused is the failure this check exists for"
fi

echo
[ $fail -eq 0 ] && echo "Task 2 settings verified." || echo "Fix what is marked FAIL in the Unity editor, then re-run."
exit $fail
