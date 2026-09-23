#!/usr/bin/env bash
# Bake the six Frontier companions into the existing card resource paths.
# Close the Unity Editor before running this script.
set -euo pipefail
cd "$(dirname "$0")/../.."
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity}"
LOG="$(pwd)/implementation/results/bake-frontier.log"
[ -x "$UNITY_EDITOR" ] || { echo "Unity 6000.6.0f1 is required; set UNITY_EDITOR to its executable."; exit 127; }
"$UNITY_EDITOR" -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod FrontierBaker.Bake -logFile "$LOG"
grep -F '[frontier-bake]' "$LOG" || { echo "Bake did not report success; inspect $LOG"; exit 1; }
