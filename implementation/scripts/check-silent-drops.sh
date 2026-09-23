#!/usr/bin/env bash
# The two TEXT-LEVEL silent-drop scans, for .uxml and .uss.
#
# WHY THIS EXISTS AS A GATE AND NOT AS PROSE IN A BRIEF. This project's
# recurring failure is the SILENT DROP: markup that still imports, still
# loads non-null, still renders - just without the part that got eaten. Both
# halves of it were found by hand during Phase 9, one round apart:
#
#   - Task 18 found a `--` inside a .uxml comment, written while naming a
#     custom property the way every .uss comment does. `--` is illegal inside
#     an XML comment, and UNITY'S IMPORTER RECOVERS PARTIALLY rather than
#     failing: no build error, no warning, no red gate. Two elements were
#     simply ABSENT FROM THE TREE, surfacing only as two NREs from a `Q<>`
#     that silently returned null.
#   - Task 19's OWN FIX ROUND then left a stray `*/` in CodexSheet.uss - the
#     .uss half of the same family, which drops every rule below it.
#
# Every task since has re-run these two scans BY HAND from prose in a brief,
# and one of those hand-runs caught a real instance. That is the whole
# argument for making them a gate. Deferred to Task 20 across four tasks.
#
# WHAT THIS DOES NOT DO, stated because a comment claiming a gate that does
# not exist is worse than no comment (that shipped twice in Task 18):
#   - It does NOT compile USS. `check-stylesheets.sh` does that, and only a
#     full Editor run can. This is the two-second text scan that finds the
#     delimiter defect without paying for an Editor lock.
#   - It does NOT verify that Unity resolves every element a .uxml names, nor
#     that a `Q<>` finds it. It verifies the DOCUMENT, not the binding.
#
# FAIL CLOSED: an empty file list is a FAIL, not a pass - a lint that finds
# nothing to lint has verified nothing. Same rule as verify-uss-tokens.sh.
set -uo pipefail
cd "$(dirname "$0")/../.."

[ -d client/Assets ] || { echo "FAIL: no client/Assets"; exit 1; }

python3 - <<'PY'
import glob, sys, xml.etree.ElementTree as ET

fail = 0

# ---------------------------------------------------------------------------
# SCAN 1 - .uxml: PARSES AS XML *AND* YIELDS AT LEAST ONE ELEMENT.
#
# BOTH ARMS ARE LOAD-BEARING AND THEY CATCH DIFFERENT THINGS. Verified, not
# assumed: `--` inside a comment, a comment ending in `-`, and an unclosed
# comment are each REJECTED by the parser outright, so arm 1 catches the
# Task 18 defect. But a well-formed document whose root has NO CHILDREN
# PARSES CLEANLY - arm 1 sees nothing wrong with it. Since partial recovery
# is the failure mode here, "it parsed" is not the question; "did anything
# survive" is. Arm 2 is the general case the Task 18 reviewer asked for.
# ---------------------------------------------------------------------------
uxml = sorted(glob.glob('client/Assets/**/*.uxml', recursive=True))
if not uxml:
    print("FAIL: no .uxml files found - the scan verified nothing.")
    sys.exit(1)

bad_parse, empty = [], []
for f in uxml:
    try:
        root = ET.parse(f).getroot()
    except ET.ParseError as e:
        bad_parse.append((f, str(e)))
        continue
    if sum(1 for _ in root.iter()) - 1 == 0:
        empty.append(f)

for f, e in bad_parse:
    print(f"FAIL: {f}: not well-formed XML: {e}")
for f in empty:
    print(f"FAIL: {f}: parses, but the root has no child elements - nothing would be built.")
if bad_parse or empty:
    fail = 1
print(f"[uxml] {len(uxml)} files, {len(bad_parse)} parse failures, {len(empty)} empty trees.")

# ---------------------------------------------------------------------------
# SCAN 2 - .uss: COMMENT DELIMITERS BALANCE.
#
# Two signatures, both silent:
#   - a `/*` that is never closed swallows every rule below it;
#   - a `*/` with no comment open is the stray-close Task 19 wrote.
# Quoted strings are tracked so a `/*` or `*/` inside url("...") or a content
# string is not mistaken for a delimiter. CSS comments do not nest.
# ---------------------------------------------------------------------------
uss = sorted(glob.glob('client/Assets/**/*.uss', recursive=True))
if not uss:
    print("FAIL: no .uss files found - the scan verified nothing.")
    sys.exit(1)

def scan(src):
    """Returns a list of (line, message). Empty means balanced."""
    problems = []
    i, n, line = 0, len(src), 1
    state, quote, open_line = 'normal', '', 0
    while i < n:
        c = src[i]
        if c == '\n':
            line += 1
            i += 1
            continue
        if state == 'normal':
            if c in '"\'':
                state, quote = 'string', c
            elif src.startswith('/*', i):
                state, open_line = 'comment', line
                i += 2
                continue
            elif src.startswith('*/', i):
                problems.append((line, "stray `*/` with no comment open - "
                                       "every rule below it is at risk"))
                i += 2
                continue
        elif state == 'string':
            if c == '\\':
                i += 2
                continue
            if c == quote:
                state = 'normal'
        elif state == 'comment':
            if src.startswith('*/', i):
                state = 'normal'
                i += 2
                continue
        i += 1
    if state == 'comment':
        problems.append((open_line, "`/*` opened here is never closed - "
                                    "it swallows the rest of the file"))
    elif state == 'string':
        problems.append((line, "unterminated string literal"))
    return problems

bad_uss = 0
for f in uss:
    with open(f, encoding='utf-8') as fh:
        for ln, msg in scan(fh.read()):
            print(f"FAIL: {f}:{ln}: {msg}")
            bad_uss += 1
if bad_uss:
    fail = 1
print(f"[uss]  {len(uss)} files, {bad_uss} delimiter defects.")

if fail:
    sys.exit(1)
print("OK: both silent-drop scans clean.")
PY
