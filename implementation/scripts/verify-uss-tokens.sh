#!/usr/bin/env bash
# Asserts the token layer is actually the token layer.
#
# Tokens.uss is faithful to the handoff and was landed in 6b76a0f, the last
# commit of Phase 7 - AFTER most screens were written. WaveHudView.uss
# therefore carried five raw hex values, four of them verbatim copies of token
# values and one (#ff6b5c) a sixth colour that is in no design document at all.
# That is what this gate exists to stop recurring.
#
# FAIL CLOSED: if the stylesheet list comes back empty, that is a FAIL and not
# a pass - a lint that finds nothing to lint has not verified anything.
set -uo pipefail
cd "$(dirname "$0")/../.."
fail=0
ok ()  { printf '  ok    %s\n' "$1"; }
bad () { printf '  FAIL  %s\n  ->  %s\n' "$1" "$2"; fail=1; }

TOKENS=client/Assets/UI/Shell/Tokens.uss
[ -f "$TOKENS" ] || { echo "FAIL: no $TOKENS"; exit 1; }

sheets=$(find client/Assets/UI -name '*.uss' ! -path "*/Shell/Tokens.uss" | sort)
if [ -z "$sheets" ]; then
  bad "no .uss files found under client/Assets/UI" "the lint found nothing to lint, which is not a pass"
  echo; echo "Fix what is marked FAIL, then re-run."; exit 1
fi
ok "$(printf '%s\n' "$sheets" | wc -l | tr -d ' ') stylesheets to check"

# Strips /* ... */, including multi-line, and prints "<line number>:<code>".
#
# BOTH CHECK 1 AND CHECK 3 READ IT, and check 3 only learned to after it
# reddened on a stylesheet DOCUMENTING a token that had just been retired -
# "Was var(--radius-pill), which drew each 83x39 tab as an ellipse". That is
# the file being honest about why a value is now a literal, and a lint that
# fails on it makes the honesty the defect. Check 1 already had this exact
# awk inline for the same reason (Theme.uss quotes the handoff's `#5b4d9e` in
# its header); this is that pass, extracted, so the two cannot drift apart
# and a check 4 has one to reach for.
#
# IT DOES NOT WEAKEN CHECK 3: a var() inside a comment is not a reference,
# because USS never resolves it. Verified by adding var(--not-a-token) to a
# real rule after this change and confirming the check still reddens.
strip_comments () {
  awk '
    { line = $0
      while (1) {
        if (inc) { i = index(line, "*/"); if (!i) { line = ""; break }
                   inc = 0; line = substr(line, i + 2); continue }
        i = index(line, "/*"); if (!i) break
        rest = substr(line, i + 2); j = index(rest, "*/")
        if (j) { line = substr(line, 1, i - 1) substr(rest, j + 2); continue }
        line = substr(line, 1, i - 1); inc = 1; break
      }
      printf "%d:%s\n", NR, line }
  ' "$1"
}

# --- 1. No raw hex outside Tokens.uss. -------------------------------------
# Two exclusions, both load-bearing:
#   url(...)  - asset references, no colour.
#   comments  - Theme.uss DOCUMENTS the handoff's `linear-gradient(180deg,
#               #8878cf, #6f5fbb)` and `box-shadow: 0 3px 0 #5b4d9e` in its
#               header, explaining which primitives USS cannot express. A lint
#               that reddens on that turns the file's honesty into a failure,
#               which is the opposite of the point.
# strip_comments removes /* ... */ (including multi-line) before the grep.
offenders=$(printf '%s\n' "$sheets" | while read -r f; do
  strip_comments "$f" | grep -E ':.*#[0-9a-fA-F]{3,8}\b' | grep -v 'url(' | sed "s#^#$f:#"
done)
if [ -z "$offenders" ]; then
  ok "no raw hex outside Tokens.uss"
else
  bad "raw hex outside Tokens.uss:"$'\n'"$offenders" \
      "add a token to Tokens.uss and reference it with var(); a colour that is not in the token layer is not in the design system"
fi

# --- 2. The 11px numeral floor. bible 10.6. --------------------------------
# --text-micro is 10px. Any rule that sets .t-num must not use it.
micro=$(printf '%s\n' "$sheets" | while read -r f; do
  awk -v F="$f" '
    /\.t-num/            { inrule=1 }
    inrule && /--text-micro/ { printf "%s:%d: %s\n", F, NR, $0 }
    /}/                  { inrule=0 }
  ' "$f"
done)
if [ -z "$micro" ]; then
  ok "no .t-num rule uses --text-micro (the 10px token)"
else
  bad "a numeral class is set below the 11px floor:"$'\n'"$micro" \
      "bible 10.6: minimum 11pt for any number a decision depends on"
fi

# --- 3. Every var() resolves to a token that exists. -----------------------
# RUNTIME-INJECTED properties are legitimate and are not in Tokens.uss:
# SafeAreaBinder sets --safe-top/--safe-bottom on the panel root at runtime
# from Screen.safeArea. They cannot be static values and Shell.uss is right to
# use them. Anything else added here needs a comment saying who sets it.
RUNTIME_SET="--safe-top --safe-bottom"
missing=$(printf '%s\n' "$sheets" | while read -r f; do
  strip_comments "$f" | grep -oE 'var\(--[a-z0-9-]+' | sed 's/var(//' | sort -u | while read -r t; do
    # The leading ( on the pattern is not decoration. bash 3.2 - which is what
    # /bin/bash still is on macOS - mis-parses the unbalanced ) of a case
    # pattern inside $( ), and the whole script dies with "syntax error near
    # unexpected token `;;'" before check 3 runs at all. The optional leading (
    # is POSIX, balances the parens for that parser, and changes nothing else.
    # CI runs bash 5, where the original parses fine - so this would have been
    # a Mac-only failure that the hosted runner never reproduced.
    case " $RUNTIME_SET " in (*" $t "*) continue ;; esac
    grep -q -- "^\s*$t:" "$TOKENS" || echo "$f: $t"
  done
done)
if [ -z "$missing" ]; then
  ok "every var() resolves to a token defined in Tokens.uss"
else
  bad "var() references a token that does not exist:"$'\n'"$missing" \
      "USS resolves an unknown custom property to nothing and renders the element untinted; it does not warn"
fi

# --- 4. An elevation wrapper's negative margin matches its own spread. ------
# .elev-1 / .elev-2 pad by --elev-N-spread to give the nine-sliced shadow
# somewhere to fall, and cancel that padding with an equal NEGATIVE horizontal
# margin so the surface inside lands on the gutter rather than 6px (or 12px)
# in from it. The two numbers have to agree or an elevated card silently stops
# lining up with the unelevated rows and CTAs beside it - which is exactly the
# state Phase 8 Task 9 measured, at x=18 against x=12 on one screen.
#
# THE MARGIN CANNOT BE WRITTEN AS THE TOKEN. USS on 6000.6.0f1 has no calc():
# `calc(var(--elev-1-spread) * -1)` imports as "Unknown function 'calc'", the
# declaration is dropped, and NOTHING FAILS - the sheet still compiles, the
# tests still pass and the capture comes back byte-identical. Tried, measured,
# reverted to a literal. This check is what stands in for the arithmetic USS
# cannot do.
THEME=client/Assets/UI/Shell/Theme.uss
if [ ! -f "$THEME" ]; then
  bad "no $THEME" "check 4 cannot run"
else
  elev=""
  for n in 1 2; do
    spread=$(grep -E -- "^[[:space:]]*--elev-$n-spread:" "$TOKENS" \
             | sed 's/.*://' | tr -d ' ;' | sed 's/px$//')
    # The margin lines inside the `.elev-<n> {` block, comments stripped.
    margins=$(strip_comments "$THEME" \
              | sed -n "/^[0-9]*:\.elev-$n {/,/^[0-9]*:}/p" \
              | grep -E 'margin-(left|right):' | sed 's/.*://' | tr -d ' ;' | sed 's/px$//')
    [ -n "$spread" ] || { elev="$elev"$'\n'"  --elev-$n-spread is not in $TOKENS"; continue; }
    count=$(printf '%s\n' "$margins" | grep -c . )
    [ "$count" = "2" ] || { elev="$elev"$'\n'"  .elev-$n sets $count horizontal margins, expected 2 (left and right)"; continue; }
    for m in $margins; do
      [ "$m" = "-$spread" ] || elev="$elev"$'\n'"  .elev-$n has a horizontal margin of ${m}px against a spread of ${spread}px; expected -${spread}px"
    done
  done
  if [ -z "$elev" ]; then
    ok "each elevation wrapper cancels its own spread horizontally"
  else
    bad "an elevation wrapper's negative margin does not match its spread:$elev" \
        "the card inside it will sit inset from the gutter, level with nothing; USS has no calc() here, so these two numbers are kept in step by this check"
  fi
fi

echo
[ $fail -eq 0 ] && echo "Token layer verified." || echo "Fix what is marked FAIL, then re-run."
exit $fail
