# Phase 9 — the exit gate

Task 21. The checklist, then the walk. **A human did the walk**; the agent
recorded it and did not perform it.

---

## Step 1 — the checklist

| # | Item | State | Evidence |
|---|---|---|---|
| 1 | Both blockers fixed, gated, seen in a running wave | **partial** | Forfeit: smoke loop step 12 on the deployed stack. Shell/battlefield: walked, see below |
| 2 | Nine screens at handoff fidelity; Codex and Lineage consistent | **yes** | 20 captures at 430×932, `implementation/results/screens/` |
| 3 | Six species and three raiders; layered sprites; Cinderplate; 48-tile sheet | **partial** | Sheet and Cinderplate rendered. Lane: see the open gap |
| 4 | Smoke loop green against the deployed stack, abandon step included | **yes** | PASS, steps 1–14. Ledger assertion deliberately not run |
| 5 | EditMode baseline held | **yes** | `phase9-test-baseline.txt` — 466/465/1/0, sole failure the deliberate one |

Additional, beyond the plan's list:

- **PlayMode: 7 of 7 green**, run by the developer in the Editor Test Runner.
  One test reddened first and was a stale assertion, not a regression —
  recorded in `phase9-blockers.txt` §A.
- **Seven companion gates green**, including two silent-drop scans built this
  phase and no drift from either regeneration.

## Step 2 — the walk

`Boot.unity` against the deployed stack
(`https://broodline-api-cru36b5eaq-uc.a.run.app`), fresh account, first hour.
Reached: cold open, founder naming, second wave, guided splice, lineage,
campaign select. Snapshot at the end: **wave 7 cleared, Founder named, 1
splice, 835 shards**.

### The developer's verdict, quoted

> "the creatures look good but the lane is empty"

### Rows

| Screen | Reached | Verdict |
|---|---|---|
| Founder naming | yes | — |
| Campaign select | yes | — |
| Deploy | yes | "the lane is empty" — **unclassified, see the open gap** |
| The wave itself | yes | "the creatures look good" |
| Splice chamber / reveal | yes | — |
| Lineage | yes | — |
| Roster, Codex | not separately reported | — |

---

## THE OPEN GAP — "the lane is empty", and it is two different defects

**Not classified, deliberately.** The same six words describe two unrelated
failures in unrelated code, and the walk did not record which screen they
were said about. Both were investigated; neither was ruled out.

**If it was the DEPLOY SCREEN's lane card:** this is the gap Task 17 named
and could not close. Its own report states the join is "proven in the
Editor's batchmode render path only", that the `LaneStage`→`LanePreviewCard`
join is "still only read", and that "nobody has yet looked at the lane
picture". The card falls back to a flat `--green-tint` fill when it receives
no texture, which looks exactly like "empty".
Against that reading: the wiring is present — `BootController.cs:129` creates
the stage and passes it, `FtueDirector.cs:353` hands `ShowLane(...)` to the
view as `lane:` — and `LaneStagePlayTests` passed on the developer's own run,
so the stage does render on a running frame.

**If it was DURING THE WAVE:** a different bug entirely.
`WaveView.Build` calls `LaneDressing.Build` as its unconditional first line
(`WaveView.cs:64`), on the default layer.
Against that reading: the dressing is not subtle. Measured ΔE76 against the
field `#e8f5ec` — trees `#cfe6d2` **9.29**, path `#e5d9c7` **13.14**, dashes
**8.02**, and the Ark cylinder `#7a6ac0` **72.39**. A violet cylinder cannot
hide on a pale green field, so "invisible dressing" is ruled out; it would
have to be not built, or not seen by the camera.

**Two controller hypotheses were tested and discarded on the way here**, and
are recorded so nobody re-runs them: that the wave scene contains no dressing
(it is built at runtime, so the committed scene holding five objects is
correct), and that the dressing colours blend into the field (they do not —
the numbers above).

**What settles it:** one look, and the question is which screen. If the deploy
card is empty while the wave's lane is dressed, it is Task 17's join. If the
wave's lane is bare, it is the dressing.

---

## Verdict

**NO: the lane reads as empty, and which of two unrelated defects that is has
not been established.**

Per this task's own rule, the gap becomes a fix-up task and the walk repeats.
The fix-up cannot be written until the screen is named, because the two
readings live in different files.

Everything else in the checklist stands. This is one named gap against a
phase whose other four checklist items are green, not a broad failure.
