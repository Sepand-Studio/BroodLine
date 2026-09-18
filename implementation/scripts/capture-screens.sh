#!/usr/bin/env bash
# Renders every screen to implementation/results/screens/*.png at 430x932.
#
# NEEDS A GRAPHICS DEVICE: -batchmode WITHOUT -nographics. On a headless box
# this produces blank or black images rather than failing, so the size check
# below is not decoration - it is the only thing between a silent blank and a
# reviewer looking at nothing.
#
# -logFile IS A REAL PATH, NEVER "-". Piping Unity's log through stdout ("-")
# buffers it until the process exits, which makes a slow run and a hung one
# look identical - documented the hard way earlier this phase. Tail the file
# below, or `tail -f` it from another shell while this runs.
#
# ==========================================================================
# BYTE-IDENTICAL IS THE WRONG TEST FOR AN UNCHANGED SCREEN. READ THIS BEFORE
# REPORTING A DRIFT, because it costs a cycle to rediscover.
#
# The right assertion is: NO PIXEL DIFFERS BY MORE THAN ONE CHANNEL LEVEL.
# Anything above that is a real change; 1/255 on a handful of pixels is not.
#
# Measured in Phase 8 Task 9, which rebuilt four screens and found the other
# twelve captures had each moved by 1 to 19 pixels, every one of them a
# partial-coverage pixel on a glyph edge, none by more than one level. Three
# facts, taken together, say what that is:
#   - three consecutive runs of the same source agree byte for byte, so this
#     script is deterministic;
#   - a re-capture with the task's changes stashed reproduced the previous
#     corpus 16 of 16, so the drift is caused by the change;
#   - every drifted pixel is antialiasing on text.
# The mechanism is the shared dynamic SDF font atlas: new or resized strings
# on ANY fixture repack it, and because the fixtures are captured in one
# process in ScreenFixtures.Names order, a screen drawn later samples a
# differently packed atlas. INFERRED, not instrumented - the atlas itself was
# never read - but the three measurements above are what it is acted on.
#
# So a change that touches text will move every capture slightly, and that is
# not a regression. Compare rasters, not bytes. The one-liner:
#
#   python3 - <<'PY'
#   from PIL import Image, ImageChops
#   a=Image.open(NEW).convert('RGB'); b=Image.open(OLD).convert('RGB')
#   d=ImageChops.difference(a,b)
#   print(max(max(p) for p in d.getdata()))   # >1 is a real change
#   PY
#
# NOT `Image.getbbox()` ON AN RGBA IMAGE. Pillow 10 gave getbbox an
# `alpha_only` parameter that defaults to True, so on a difference image -
# whose alpha channel is all zero when both inputs are opaque - it returns
# None and every pair reads as identical. That answer was believed for one
# round in Task 9. Convert to RGB first, as above.
# ==========================================================================
set -uo pipefail
cd "$(dirname "$0")/../.."

UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
LOG="$(pwd)/implementation/results/capture-screens.log"
OUT="$(pwd)/implementation/results/screens"
mkdir -p implementation/results

# Delete any previous results FIRST, the run-unity-tests.sh convention: a run
# that never starts (the editor already holding the project is the common
# case) must not leave a prior successful capture behind for the checks below
# to pass against. A stale OK is worse than a failure.
rm -f "$LOG"
rm -rf "$OUT"

[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }
[ -d client ] || { echo "FAIL: no client/ project"; exit 1; }

"$UNITY" -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod ScreenshotCapture.CaptureAll \
  -logFile "$LOG"
code=$?

echo "--- unity log: $LOG ---"
tail -20 "$LOG"

n=$(ls "$OUT"/*.png 2>/dev/null | wc -l | tr -d ' ')
[ "$n" -ge 12 ] || { echo "FAIL: expected >=12 screens, got $n. See $LOG."; exit 1; }

small=$(find "$OUT" -name '*.png' -size -8k)
[ -z "$small" ] || { echo "FAIL: these captures are suspiciously small (blank?):"; echo "$small"; exit 1; }

[ "$code" -eq 0 ] || { echo "FAIL: Unity exited $code despite $n screens on disk - check the log."; exit "$code"; }

echo "OK: $n screens captured."
