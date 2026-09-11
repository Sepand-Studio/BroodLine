#!/usr/bin/env bash
# Regenerates tests/engine/corpus-baseline.txt from the CURRENT engine.
#
# This is a DELIBERATE re-baseline, not a routine step. Running it makes
# CorpusBaselineTests pass by definition, so it must only be run when the
# behaviour change is intended and understood - a balance change, or a state
# vector that genuinely grew. If a test went red and you are here looking for
# the button that makes it green, you are in the wrong place.
set -euo pipefail
cd "$(dirname "$0")/../.."
dotnet test Broodline.sln --nologo \
  --filter "FullyQualifiedName~CorpusBaselineTests.Emit" \
  -e BROODLINE_EMIT_BASELINE=1
echo "--- regenerated: tests/engine/corpus-baseline.txt ---"
git --no-pager diff --stat -- tests/engine/corpus-baseline.txt
