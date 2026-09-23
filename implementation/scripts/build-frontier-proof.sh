#!/usr/bin/env bash
# Rebuild the standalone visual proof. Never deletes earlier results.
set -euo pipefail
cd "$(dirname "$0")/../.."
editor="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$editor" ]]; then
  echo "Unity 6000.6.0f1 is required. Set UNITY_EDITOR to its executable or use the editor's Broodline > Frontier Proof > Open menu."
  exit 127
fi
if [[ ! -f client/ProjectSettings/ProjectVersion.txt ]]; then
  echo "Missing client/ Unity project."
  exit 1
fi
output="$(pwd)/implementation/results/frontier/build-$(date +%Y%m%d-%H%M%S)-$$.log"
mkdir -p "$(dirname "$output")"
"$editor" -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod FrontierProofBuilder.Build -logFile "$output"
echo "Built the proof scene. Import/compiler log: $output"
