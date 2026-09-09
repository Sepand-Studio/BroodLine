#!/usr/bin/env bash
# Verifies the toolchain Phase 0 needs. Run from anywhere in the repo.
set -uo pipefail
fail=0
ok ()  { printf '  ok    %s\n' "$1"; }
bad () { printf '  FAIL  %s\n' "$1"; fail=1; }

echo "Phase 0 prerequisites"

if git lfs version >/dev/null 2>&1; then
  ok "git-lfs $(git lfs version | awk '{print $1}')"
else
  bad "git-lfs missing — brew install git-lfs && git lfs install"
fi

if xcodebuild -version >/dev/null 2>&1; then
  ok "Xcode $(xcodebuild -version | head -1 | awk '{print $2}')"
else
  bad "Xcode missing, or command line tools not selected"
fi

EDITORS="/Applications/Unity/Hub/Editor"
if [ -d "$EDITORS" ] && [ -n "$(ls -A "$EDITORS" 2>/dev/null)" ]; then
  for v in "$EDITORS"/*; do
    ver=$(basename "$v")
    if [ -d "$v/PlaybackEngines/iOSSupport" ]; then
      ok "Unity $ver with iOS Build Support"
    else
      bad "Unity $ver present, but the iOS Build Support module is MISSING"
    fi
  done
else
  bad "No Unity editor — Unity Hub > Installs > Install Editor > Unity 6 LTS, tick iOS Build Support"
fi

echo
if [ $fail -eq 0 ]; then echo "All prerequisites present."; else echo "Install what is marked FAIL, then re-run."; fi
exit $fail
