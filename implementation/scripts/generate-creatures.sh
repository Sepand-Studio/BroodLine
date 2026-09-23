#!/usr/bin/env bash
# Regenerates every creature mesh, material and prefab from its recipe, and
# (with BAKE=1) re-bakes the card sprites and the contact sheet. Batchmode;
# the Editor must be closed. Usage: generate-creatures.sh   |   BAKE=1 generate-creatures.sh
set -uo pipefail
cd "$(dirname "$0")/../.."
UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
LOG="$(pwd)/implementation/results/generate-creatures.log"
mkdir -p implementation/results
rm -f "$LOG"
[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }

"$UNITY" -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod Broodline.Creatures.Editor.CreatureGenerator.Generate -logFile "$LOG"
code=$?
grep '\[creatures\]' "$LOG" || { echo "FAIL: no [creatures] line in $LOG - the Editor is probably open"; exit 1; }
[ "$code" -eq 0 ] || { echo "FAIL: generate exited $code"; tail -30 "$LOG"; exit "$code"; }

if [ "${BAKE:-0}" = "1" ]; then
  "$UNITY" -batchmode -quit -projectPath "$(pwd)/client" \
    -executeMethod Broodline.Creatures.Editor.CreatureBaker.Bake -logFile "$LOG.bake"
  code=$?
  grep '\[bake\]' "$LOG.bake" || { echo "FAIL: no [bake] line"; exit 1; }
  [ "$code" -eq 0 ] || { echo "FAIL: bake exited $code"; tail -30 "$LOG.bake"; exit "$code"; }
fi
echo "ok"
