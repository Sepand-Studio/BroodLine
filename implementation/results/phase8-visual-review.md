# Phase 8 — the eyes-on pass

**STATUS: DONE, 2026-09-18. A human ran `Boot.unity` against the deployed
stack and walked the first hour until two defects stopped the walk. The
verdict is theirs and is quoted, not paraphrased.**

Task 16 says it in its own words: *"A human does this. There is no automated
substitute and no step here that an agent completes."* What an agent could do
was narrow the job, so this file arrived with the measurements already taken
and the known-open questions already stated. The verdicts below were added
after the human looked. Where they did not reach a screen, the row says so
rather than borrowing a verdict from the captures.

---

## The verdict, verbatim

> "the screens are too simple. also the creatures I want it too look more
> fancy, 3D like a cartoon characters. I like the shapes etc... but the design
> is very poor"

and, on whether to ship it:

> "It won't make any sense to testflight right now with something ugly and
> completely broken"

**What that decides.** Phase 8 closes on this file. The TestFlight gate (DoD
item 10) moves to Phase 9, which is re-scoped from "production creature art
against `rig_proof.md`" to *the look* — screens brought up to the handoff,
creatures in 3D, and a build that is worth handing to a stranger. The
reasoning, and what it changes in the record, is in
`implementation/2026-09-17-phase8-followups.md` §1e.

**What "too simple" turned out to mean, measured after the fact.** Put
`SpliceChamberView.png` next to the handoff's `specs/Designs/shots/splice-chamber.png`:
the handoff has an eyebrow label over the title, a back pill and a resource pill
in the header, species-tinted generation and trait chips, dashed-ring hero
slots, a gradient "predicted hybrid" panel with stat cells and trend arrows,
inheritance bars, an amber-gradient mutation banner, a lineage strip and the
bottom nav. Ours has a plain title, two flat cards with grey chips, two flat
stat boxes and two flat banners. `DeployView` is further off: three text rows
reading "Vetch (G1) / Pocket 1" where the handoff draws a lane with placement
slots. **Phase 8 built the handoff's skeleton to token and reproduced its
components; it did not reach the handoff's fidelity on any screen with real
content.** That is an implementation gap against a spec this repo already
owns, and it is Phase 9's first item.

**"I like the shapes" is a pass on bible §10.2 rule 1.** The six proxies were
drawn from §1.2's silhouette column verbatim so that a collision between them
would be a collision in the design. A human looked and kept the shapes. What
they rejected is a flat tinted fill on a near-white card and a coloured cube on
the lane — which is what bible §10.4's new subsection predicted a flat fill
would do, and what `WaveView.BuildMarker` has always drawn.

---

## Two defects that stopped the walk

Both are recorded in full in `phase8-followups.md`; one line each here so this
sheet is complete on its own.

1. **An abandoned wave bricks the account** (§1c). `wave/start` commits the
   deployed creatures; quitting before `wave/submit` leaves them committed for
   ever, and the FTUE's fight step skips committed creatures, so the first
   screen stalls on "Name it" with nothing to name. **Booked to Phase 9** — a
   server-side reaper or a client-side resume, and a decision about which.
2. **The shell renders over the battlefield** (§1d). `Wave.unity` loads
   additively; its `UIDocument` is a sibling root in the shared panel and
   nothing hides `#shell-root`. `.shell-root { background-color: var(--paper) }`
   resolved to nothing for seven phases while `Tokens.uss` was dead, so the
   shell painted transparently over the lane and nobody saw it. Fixing the
   tokens made it opaque. An attempted fix was reverted unverified. **Booked to
   Phase 9**, with the EditMode suite behind it this time.

---

## The one thing that is objectively out of spec

`OptionRow`'s fill is `--surface-sunk` `#f8f6fc` on `--paper` `#f7f4fb`:
**1.0151 : 1**. The entire row boundary is a 1px `--hairline` at **1.1131 : 1**.
Both fail WCAG 1.4.11's 3:1 for non-text UI boundaries, and bible §10.4
("colour never carries information alone") is arguably breached, since a row's
extent is carried by a faint border and nothing else. Inside a `SectionCard`
on white the fill measures 1.0726 : 1 — better, still nothing.

Three options, cheapest first:

1. Strengthen `--hairline` for this one use.
2. Drop the fill, which currently does nothing, and let the row read as an
   outline deliberately.
3. Compose `OptionRow`s inside a `SectionCard` so the fill sits on white, which
   may be the handoff's actual intent for "inset rows, unselected options".

**Verdict:** superseded. The screens that carry `OptionRow` are being brought
up to the handoff in Phase 9, and the handoff's unselected option is a 1px
`#ece7f6` inset on `#f7f5fb` — i.e. the same numbers. The decision is whether
to follow the handoff or exceed it, and it belongs with the redesign, not
before it. The measurement stands and travels with the row.

---

## Per screen

The human's verdict was given on the running app, screen by screen as reached,
not on the corpus. **Reached** means it was on the phone-sized viewport in
front of them during the walk.

### First hour

| Screen | Known from measurement | Verdict |
|---|---|---|
| `FounderNamingView` | Card and CTA both on the 12px gutter. Blank coral banner removed. | **Reached.** Too simple. Also where defect 1 surfaces: "Name it" and "Not now" both dead when the account's creatures are committed — a data defect, not a screen defect, but the screen gave no hint. |
| `CampaignSelectView` | `OptionRow` contrast above applies here. Locked row carries `icon--lock`. | **Reached.** Too simple. `OptionRow` verdict: superseded, see above. |
| `CodexSheet` | No scaffold by design — it is a sheet, not a screen. Cards separate by `elev-1` on white. | **Not reached** — the walk stopped at defect 2 before any Codex open. Blanket verdict presumed to apply; not confirmed. |
| `LineageView` | Founder/Mutated/Consumed now stated in words; greyscale test passes. | **Not reached.** Same. |

### The loop

| Screen | Known from measurement | Verdict |
|---|---|---|
| `RosterView` | All six proxies distinct at 40px. Pale measures 1.47:1 on the card slot — faintest by a wide margin. | **Reached.** Shapes kept, rendering rejected: "I like the shapes… but the design is very poor". Pale's faintness was visible and is the §10.4 prediction confirmed by eye. |
| `SpliceChamberView` | Mutation panel now collapses when the server names no forecast. | **Not reached in the walk;** measured against the handoff shot above — the widest fidelity gap of the ten. |
| `SpliceRevealView` | Hero card `elev-2` + `reveal-flare`; the flare now has a toggle and actually animates. **Watch this one in motion.** | **Not reached.** Motion unassessed — see below. |
| `DeployView` | `OptionRow` contrast above applies here, densest stack of them. | **Reached.** Too simple; the handoff's lane-with-slots is absent entirely. |

### Wave

| Screen | Known from measurement | Verdict |
|---|---|---|
| `WaveHudView` | **The corpus cannot judge this screen.** See below. | **Reached, and it is defect 2.** "the game screen was collapsing on top of the menu screen." Creatures are coloured cubes; verdict on those: "3D like a cartoon characters", i.e. not this. Bar colours, scrim and bar alignment could not be judged through the shell. |
| `PostWaveView` | Headline colour now toggles by verdict; was hardcoded green. Loss case is **not** in any fixture. | **Not reached.** |
| `WaveDefeatView` | Diagnosis card collapses when empty. Headline centred as text. | **Not reached.** |

### Map

| Screen | Known from measurement | Verdict |
|---|---|---|
| `RegionView` | Interim by design — a node list, not the map. Deferred to Phase 10. | **Not reached.** Deferral stands. |

### Harness fixtures (not screens)

| Fixture | Purpose | Verdict |
|---|---|---|
| `Primitives` | elevation + CTA ramp | Not shown to the human; they are instruments. No verdict is owed. |
| `Icons` | thirteen glyphs + live nav bar | Same. |
| `Scaffold` | the frame, pushed and top-level | Same. |
| `Components` | the five shared components in their states | Same. |

---

## Two things the corpus structurally could not answer — and still cannot

**1. `WaveHudView` in the running game.** The runtime-side stylesheet fix was
verified by construction and by the stylesheet gate. The walk reached the wave
and could not judge the HUD because the shell was drawn over it. **Still open;
Phase 9, after defect 2.**

**2. Motion.** `.option-row` on select, `.t-num` on colour change,
`.reveal-flare` on the splice payoff. The walk stopped before any of the three
fired in front of the human. **Not assessed. Still open; Phase 9.** Nothing
here should be read as "the motion is fine".

---

## What this pass changed

Step 4 says fix what this phase fixes, re-capture, iterate. **Nothing was fixed
in this phase on the strength of this pass.** One fix was attempted for defect
2 and reverted the same hour because the EditMode suite could not run with the
Editor open and the symptom got worse. Every item above is booked to Phase 9
with its reason, and the phase closes on the decision quoted at the top rather
than on an empty list.
