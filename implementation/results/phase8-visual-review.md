# Phase 8 — the eyes-on pass

**STATUS: PREPARED, NOT DONE. Every verdict below is blank and must be written
by a human who looked.**

Task 16 says it in its own words: *"A human does this. There is no automated
substitute and no step here that an agent completes."* What an agent could do
was narrow the job, so this file arrives with the measurements already taken
and the known-open questions already stated. It does not arrive with answers.

Filling a verdict in without looking would be worse than leaving it empty,
because the next reader cannot tell the two apart. `nothing wrong with this
screen` is a legitimate finding and is written as one — but only after looking.

---

## How to run it

```bash
./implementation/scripts/capture-screens.sh
open implementation/results/screens
```

**Step 2 needs the app actually running, not the corpus.** `Motion.uss` cannot
be checked in a still frame — a capture is one frame of a 220ms transition
either way. Start the local stack, press Play in `Boot.unity`, and walk beats
1–8. This is Phase 7 Task 17 Step 6, which was not run; it is run here.

There is no `docker-compose.yml` in this repo. `.local-stack/` holds `bundles`
and `replays` only, so how the local stack starts is itself something to
establish before Step 2 — note it here when you find out.

---

## The one thing that is objectively out of spec

Look at this first. It is measured, not suspected.

`OptionRow`'s fill is `--surface-sunk` `#f8f6fc` on `--paper` `#f7f4fb`:
**1.0151 : 1**. The entire row boundary is a 1px `--hairline` at **1.1131 : 1**.
Both fail WCAG 1.4.11's 3:1 for non-text UI boundaries, and bible §10.4
("colour never carries information alone") is arguably breached, since a row's
extent is carried by a faint border and nothing else. Inside a `SectionCard`
on white the fill measures 1.0726 : 1 — better, still nothing.

It renders *legibly* in `DeployView.png` and `CampaignSelectView.png`; the
rounded 1px outline does the work. **So the question is whether a boundary that
survives a screenshot survives a phone in daylight** — which is what this task
is for and what no measurement here can settle.

Three options, cheapest first:

1. Strengthen `--hairline` for this one use.
2. Drop the fill, which currently does nothing, and let the row read as an
   outline deliberately.
3. Compose `OptionRow`s inside a `SectionCard` so the fill sits on white, which
   may be the handoff's actual intent for "inset rows, unselected options".

**Verdict:** _(awaiting a device)_

---

## Per screen

One line per defect: what is wrong, and whether it is fixed in this phase or
booked. Pre-measured facts are given so you are not re-deriving them at a
screen; they are not findings.

### First hour

| Screen | Known from measurement | Verdict |
|---|---|---|
| `FounderNamingView` | Card and CTA both on the 12px gutter. Blank coral banner removed. | _(awaiting)_ |
| `CampaignSelectView` | `OptionRow` contrast above applies here. Locked row carries `icon--lock`. | _(awaiting)_ |
| `CodexSheet` | No scaffold by design — it is a sheet, not a screen. Cards separate by `elev-1` on white. | _(awaiting)_ |
| `LineageView` | Founder/Mutated/Consumed now stated in words; greyscale test passes. | _(awaiting)_ |

### The loop

| Screen | Known from measurement | Verdict |
|---|---|---|
| `RosterView` | All six proxies distinct at 40px. Pale measures 1.47:1 on the card slot — faintest by a wide margin. | _(awaiting)_ |
| `SpliceChamberView` | Mutation panel now collapses when the server names no forecast. | _(awaiting)_ |
| `SpliceRevealView` | Hero card `elev-2` + `reveal-flare`; the flare now has a toggle and actually animates. **Watch this one in motion.** | _(awaiting)_ |
| `DeployView` | `OptionRow` contrast above applies here, densest stack of them. | _(awaiting)_ |

### Wave

| Screen | Known from measurement | Verdict |
|---|---|---|
| `WaveHudView` | **The corpus cannot judge this screen.** See below. | _(awaiting — on device)_ |
| `PostWaveView` | Headline colour now toggles by verdict; was hardcoded green. Loss case is **not** in any fixture. | _(awaiting)_ |
| `WaveDefeatView` | Diagnosis card collapses when empty. Headline centred as text. | _(awaiting)_ |

### Map

| Screen | Known from measurement | Verdict |
|---|---|---|
| `RegionView` | Interim by design — a node list, not the map. Deferred to Phase 10. | _(awaiting)_ |

### Harness fixtures (not screens)

| Fixture | Purpose | Verdict |
|---|---|---|
| `Primitives` | elevation + CTA ramp | _(awaiting)_ |
| `Icons` | thirteen glyphs + live nav bar | _(awaiting)_ |
| `Scaffold` | the frame, pushed and top-level | _(awaiting)_ |
| `Components` | the five shared components in their states | _(awaiting)_ |

---

## Two things the corpus structurally cannot answer

**1. `WaveHudView` is styled differently in a capture than in the game.** The
wave scene's `UIDocument` has no `visualTreeAsset`, so `Shell.uxml` never loads
there and the HUD hangs off a sibling root. `ScreenshotCapture` attaches the
shell stylesheets to every fixture, so the picture has always been styled
whether the runtime was or not. The review fixed the runtime side by attaching
Tokens and Theme in `WaveHudView.uxml` — **but that fix is verified by
construction and by the stylesheet gate, not by a running wave.** Play a wave
and check the bar colours, the scrim behind the readout, and whether each bar
sits level with the creature it labels.

**2. Motion has no still-frame evidence at all.** `.option-row` transitions on
select, `.t-num` on colour, `.reveal-flare` on the splice payoff. Screen pushes
and sheet slides were removed from `Motion.uss` in the review because nothing
ever toggled them — so if a push or a sheet *should* animate, that is a finding
for this pass and it is new work, not a regression.

---

## What this pass is allowed to change

Step 4: fix what this phase fixes, re-capture, re-read, iterate until the list
is empty or every remaining item is explicitly booked to a later phase **with a
reason**. A booked item without a reason is an item nobody will pick up.
