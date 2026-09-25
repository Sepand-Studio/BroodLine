#!/usr/bin/env bash
# Capture the six live Frontier founder bodies at their 40px review size.
set -uo pipefail
cd "$(dirname "$0")/../.."

UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
LOG="$(pwd)/implementation/results/frontier-silhouettes.log"
OUT="$(pwd)/implementation/results/silhouettes"
mkdir -p implementation/results
rm -f "$LOG"
rm -rf "$OUT"

"$UNITY" -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod FrontierSilhouetteCapture.Run -logFile "$LOG"
code=$?

grep '\[silhouette\] captured 6 Frontier bodies and 6 paired-trait assemblies' "$LOG" || {
  tail -30 "$LOG"
  echo "FAIL: the capture did not report all six bodies."
  exit 1
}
[ "$code" -eq 0 ] || { echo "FAIL: Unity exited $code"; exit "$code"; }
python3 - "$OUT" <<'PY'
import sys
from pathlib import Path
from PIL import Image
folder = Path(sys.argv[1])
species = {'vetch', 'ember', 'pale', 'skitter', 'hollow', 'loam'}
expected = species | {name + '-traits' for name in species}
found = {p.stem for p in folder.glob('*.png')}
assert found == expected, f'expected {expected}, found {found}'
for path in folder.glob('*.png'):
    with Image.open(path) as image:
        assert image.size == (40, 40), f'{path.name}: {image.size}'
print('OK: six bare and six paired-trait Frontier silhouettes captured at 40×40.')
PY
