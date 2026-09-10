# FixPointCS — vendored source provenance

The files in this directory are **third-party code, vendored unmodified**. They
carry the licence (`LICENSE.txt`, MIT) and per-file copyright headers, but until
this file existed they recorded nothing about *where they came from* — which
left no way to answer "is this current?", "has anyone edited it?", or "what
would upgrading involve?" without guessing.

## Upstream

| | |
|---|---|
| Project | FixPointCS |
| Authors | Jere Sanisalo, Petri Kero |
| Repository | <https://github.com/XMunkki/FixPointCS> |
| Licence | MIT (`LICENSE.txt`, reproduced verbatim here and in every file header) |
| **Pinned revision** | `a852f05b428a942f8dc274ee516a893ae224e0d4` |

## Files taken

Only the two fixed-point sources the engine actually compiles, plus the licence.
Nothing else from the upstream repository is vendored — not the test suite, not
the C++/Java generators, not `Fixed32.cs`, not `F64.cs`/`F32.cs`.

| File | Upstream path at the pinned revision | SHA-256 |
|---|---|---|
| `Fixed64.cs` | `FixPointCS/Fixed64.cs` | `2614e091964a97a8bb36ed12ee63140cddd05242b633695a764e87f40afb0d63` |
| `FixedUtil.cs` | `FixPointCS/FixedUtil.cs` | `8853a1d4c409cf15d938a37e13d60509a99136a4dc4a69ed98268338a37038b1` |
| `LICENSE.txt` | `LICENSE.txt` | `d9e35896ab1848e5c26ce0b7c9f835d6127f153d1967a394d85a0980bd8a1a6b` |

The `.meta` files alongside them are Unity's, not upstream's, and are committed
for the usual reason (a directory's own `.meta` lives one level up).

## These files are byte-identical to upstream

Verified, not assumed: each file was fetched from the pinned revision and
compared with `cmp`. All three match exactly, and the SHA-256 values above are
the checksums of both copies.

```bash
REV=a852f05b428a942f8dc274ee516a893ae224e0d4
BASE=https://raw.githubusercontent.com/XMunkki/FixPointCS/$REV
for f in Fixed64.cs FixedUtil.cs; do
  curl -sS "$BASE/FixPointCS/$f" | cmp - "$f" && echo "identical: $f"
done
curl -sS "$BASE/LICENSE.txt" | cmp - LICENSE.txt && echo "identical: LICENSE.txt"
```

**Byte-identity is a property worth keeping, so do not edit these files.** It is
what makes the pinned revision meaningful: an upgrade becomes a clean
replace-and-re-diff rather than an archaeology exercise in separating upstream
changes from local ones. Anything this project needs to change about the
behaviour belongs in the wrapper, `engine/Runtime/Fix64.cs` — which is exactly
why that wrapper exists, and why its header comment documents the vendored
edge-case contract (silent `Mul` overflow, saturating division-by-zero,
`Sqrt` of a negative returning zero) instead of patching it here.

Two project mechanisms are scoped to this directory and depend on it staying
vendored rather than becoming ours:

- **The IL floating-point scan** (`tests/engine/DeterminismRuleTests.cs`) skips
  types in the `FixPointCS` namespace, because this library ships
  `ToDouble`/`FromDouble`/`ToFloat`/`FromFloat` conversion helpers at its API
  edge. The exclusion is namespace-scoped so it cannot quietly extend to our own
  code — and the scan now flags a *call* from non-vendored code into any of
  those helpers, not merely a float opcode.
- **The Unity-conditional-compilation ban**
  (`tests/engine/EnforcementTests.cs`) excludes `ThirdParty/`, because these
  files carry their own `#if CPP` / `#if JAVA` / `#if NET_4_6` ladders from
  upstream's multi-language generator.

## Upgrading

1. Replace the files from the new upstream revision, unmodified.
2. Update the revision, the file list and the SHA-256 values above.
3. Re-run `dotnet build Broodline.sln` — the banned-API analyzer and
   `TreatWarningsAsErrors` both apply to vendored code, and a new revision is
   the likeliest thing to trip them.
4. Re-run `dotnet test Broodline.sln`, then
   `./implementation/scripts/cross-runtime-diff.sh`. The `Pinned_` tests in
   `tests/engine/Fix64Tests.cs` exist to tell you whether the edge-case contract
   moved underneath the wrapper; the cross-runtime gate tells you whether the
   two runtimes still agree about it.
5. Any change in behaviour is a simulation change: `Broodline.Sim.SimVersion`
   and every stored replay are affected.
