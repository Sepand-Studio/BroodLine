# Phase 10 — Batch 1, the whole-app skin pass

Running record for [the Phase 10 plan](../specs/plans/broodline_phase10_living_frontier.md).
Batch 1 ends with the user's eyes-on review (Task 1.8); nothing below is
accepted because a test passed.

## Task 1.0 — records, assembly split, hygiene (2026-09-23)

**Done in source:**

- `client/Assets/Frontier/Art/` now holds the art core — `FrontierArt`,
  `FrontierMesh`, `FrontierSurface.shader`, `FrontierRigDefinition`,
  `FrontierPose`, `FrontierCreature`, `FrontierFace`, `FrontierParts` and the
  six species builders — under `Broodline.Frontier.Art.asmdef`, an assembly
  with **no references**. `Broodline.View`, `Broodline.Game` and the proof's
  `Broodline.Frontier` reference it. Dependency graph:
  `Frontier.Art ← View ← Game ← Frontier (proof)`. `Broodline.UI` does not
  reference it and must not.
- The proof's tests split: `Art/Tests/FrontierArtTests.cs` (7 tests:
  initialization, mesh primitives, budget/bindings, sockets, blink, pause,
  reduced motion) and `Tests/FrontierProofTests.cs` (4: outcome hash,
  formation ×2, breach feedback). Test bodies are unchanged; the known
  weaknesses (budget loop hard-codes six ids; `weight0 == 1` will reject
  Pale's blended membrane) are Batch 2's to fix, deliberately not folded in
  here so this task stays a pure move.
- `View/RuntimeShaders.All` gains `"Broodline/FrontierSurface"`.
  `RuntimeShaderInclusionTests` will be RED until `Phase0Setup.Apply` runs in
  the Editor and `ProjectSettings/GraphicsSettings.asset` is committed with it.
- `.gitignore` excludes `client/Assets/Frontier/Generated/` (the proof scene
  `FrontierProofBuilder.Build` regenerates). The eight `Frontier/*.cs.meta`
  files the previous commits left untracked are now tracked under `Art/`.
- Bible §10.1 and §10.6 amended: faces are allowed (the Phase 9 "not in a
  face" ruling is superseded by the approved Living Frontier direction), and
  `client/Assets/UI/Shell/Tokens.uss` is the token authority, seeded from the
  visual upgrade plan's palette rather than the 2026-09 handoff README.

**Checks run here:** Roslyn syntax parse of the two test files and
`RuntimeShaders.cs` (no syntax errors; not a Unity compile). `git status`
shows renames, not delete+add, for every tracked file.

**Owed on the Unity machine** (the Editor was open with the project during
this task, so batch mode could not run):

1. Let the Editor recompile; the Console must show no errors from the split.
2. `Broodline → Apply Phase 0 Setup`, then commit `GraphicsSettings.asset`.
3. `Window → General → Test Runner → EditMode`: `Broodline.Frontier.Art.Tests`
   (7) and `Broodline.Frontier.Tests` (4), then the full suite against
   `implementation/results/phase9-test-baseline.txt` (MainThreadAffinity is
   the one known red).
4. `Broodline → Frontier Proof → Open` still builds and plays.
