---
status: decision-record
folder: 02-decisions
note: >
  The working list. Every open question in the set classified by who can
  close it. Counts are behind the current file set and need a refresh.
---

# Broodline — What's Left

*The state of the project, and the only document that says what is not done*

> **CURRENT.** Replaces `broodline_design_audit.md` as the working list. That
> file tracked gaps in the design and closed all of them; this tracks what
> remains across the whole set, which is a different kind of list.

---

## 1. Where the project stands

**Seventy files, forty-four of them current.** The design is finished. The content is finished — sixty waves, thirty regions, the graph. The technical foundations are specified — engine, data model, telemetry, server topology, store. The production plan exists and names its gates.

**One hundred and thirty-seven unresolved open questions remain across thirty-four current documents.** The claim that none can be answered by writing was very nearly right and not quite: a reconciliation pass closed six by cross-referencing documents that already contained the answers — tombstone name fields, `committed_to`, node state on an empty server, Pack Sense, globally-meaningful IDs, and replay retention — and found one live contradiction between two current documents that no amount of playtest would have settled. The rest still need something other than a document.

**Treat "closable by reading" as a fifth kind.** It is small, but it is not zero, and it is the only kind that costs nothing to clear. What remains sorts into four kinds, and every one needs something other than a document.

| Kind | Count | Closes when |
|---|---|---|
| **Decisions** | 1 | §2 closed all twenty-two, then the 2026-09-14 device-floor change opened one: **hard-gate A13 devices, or let them install and run below target?** It needs an install-base number nobody has yet. `client_architecture` §12 |
| **Procurement and commissioning** | 8 | Something external is bought or briefed |
| **Bench tests** | 12 | A build exists, no players needed |
| **Playtest** | 64 | Players play |
| **Soft launch** | 35 | Real population, real spend |

**The writing is done and so is the deciding.** What remains is buying, building and watching.

---

## 2. Decisions — all taken

**Twenty-three, all taken, all applied in their home documents.** Veto any and it reverses.

Seven changed how something behaves rather than settling a number, and those are worth knowing:

| | |
|---|---|
| **Offers are shortfall-only** | No unprompted offer exists. Progression, temporal and lifecycle offers were cut — an engine that fires only when a player runs short is one the player asked for |
| **English at launch**, then one language at a time | Japanese first, German fourth, Arabic thirteenth. Not waves |
| **Every raid is verified**, not sampled | Determinism makes it one re-run; sampling lets cheating through during the window where PvP reputation is set |
| **The game is silent 22:00–08:00**, alerts included | An unanswered raid alert auto-resolves identically, so waking a player buys nothing |
| **Skittish repositions deterministically** | Nearest free pocket away from the threat. Reduces the engine's whole RNG surface to two tie-breaks |
| **Screen readers: menus properly, combat honestly** | Full support on every static list; combat placement does not claim it |
| **The device floor is A14 / 4 GB, not A13 / 3 GB** | Taken 2026-09-14. No A13 device was available to measure on, and a budget measured on the floor beats one extrapolated to it. **It drops the iPhone 11, the SE (2020) and the iPad 9th gen from the performance target — a revenue decision, taken knowingly**, and `client_architecture` §3 keeps the objection rather than retiring it. It raised the per-body triangle budget from 7000 to the measured 10000 by withdrawing a margin that existed only to cross the A14→A13 gap. It is a **budget floor, not an enforced compatibility gate**: nothing in the build refuses an A13 device, and whether to add that refusal is newly open in `client_architecture` §12 |

The other sixteen, in one line each: telemetry raw events expire at 90 days · the rig proof runs three weeks · a server merge clears all territory and re-opens claims at the next tick · region names are transliterated · coverage values freeze before a Codex is translated · no cross-promotion of other titles including your own · Convoy Rigs are visible as Rigs with contributor count · under-13 accounts have no chat at all · moderation is automated-first with human escalation · chat logs retain 30 days · superseded replays show their recorded outcome and never re-simulate · no retirement cooldown until playtest shows the exploit is real · the Transit Board bands cargo value rather than stating it · the Pale grant fires on defeat · the Inner Reach is never claimable in any season.

---

## 3. Procurement and commissioning — 8

External lead times. **The first two should already be in motion.**

| # | Item | Where |
|---|---|---|
| 1 | **Moderation filter, English** — from a vendor covering the full language list | `localization` §7, `build_order` §2 |
| 2 | **CJK font substitutes**, weight-matched, tabular figures verified — for Japanese, the first language added, not for launch | `localization` §8 |
| 3 | The rig proof commission — two bodies, twelve socket-agnostic parts, forty-eight renders. **Phase 0's remaining half**: moved to the art track on 2026-09-14 so it stopped gating engineering, and it still gates the twenty-four-asset budget, every production species, and the real-mesh re-run of the entity-count harness | `rig_proof` §8, §8.3; `solo_execution` §8.1–8.2 |
| 4 | Species palette fix — widen lightness separation on two collapsing pairs | `accessibility` §4 |
| 5 | Eleven behaviour previews, costed | `trait_codex` §10.1 |
| 6 | Twenty raider animation clips, quoted rather than estimated | `raider_roster` §10.4 |
| 7 | Eighteen species vocalisations — six animals, three states, no monster register | `audio` §13.2 |
| 8 | The blocked-hit visual tell — art arising from an audio requirement, unowned | `audio` §13.1 |

---

## 4. Bench tests — 12

Answerable with a build and no players. **All of these belong in Phase 1.**

| # | Test | Where | Invalidates if wrong |
|---|---|---|---|
| 1 | **Wave 44 at ~100 entities** on bottom-quartile devices | `telemetry` §8, four wave docs | Five authored waves cannot ship. **Partly answered 2026-09-14** — Phase 0 measured 104 entities at 60 fps on the A14 floor with *synthetic* meshes. What is still owed is the same run on real delivered meshes, `rig_proof` §8.3 |
| 2 | **All eight locks pass the rule test** — breach rate near 100% without the answer | `telemetry` §4 | A lock has a hole. Free — the engine computes it |
| 3 | Socket-agnostic parts read in both positions across two bodies | `rig_proof` §4 | The 24-asset budget |
| 3a | **A socket on a deforming parent** — what the transform does when the surface under it articulates. A dummy, not a third body | `rig_proof` §4 item 10 | The socket standard on Loam, Skitter and Hollow |
| 4 | Growth as a proportion curve, not separate assets | `rig_proof` §6 | The 3D decision |
| 5 | Silhouettes distinct at 40px with parts attached | `rig_proof` §4 | The art direction |
| 6 | Hauler carries plate and shield simultaneously | `rig_proof` §3.2 | The Sunder becomes one combined kit |
| 7 | Four-pocket Delta terrain against 44pt targets on the smallest device | `accessibility` §8.4 | Delta terrain, or the target size. **Unaffected by the 2026-09-14 floor change in the dimension that binds**: the smallest device becomes the iPhone 12 mini rather than the SE (2020), and both are **375 pt wide**. Height grows 667 → 812 pt, which only adds room |
| 8 | Pack ladder monotonic in every storefront | `store_iap` §8 | Build fails, correctly |
| 9 | Skirmisher and Courser distinguishable at a glance | `raider_roster` §10.2 | Scale is the lever |
| 10 | The Slow and Fast wave shapes | `seasonal` §10.4 | Two of the most distinctive seasonal shapes |
| 11 | Wave 53 reads as harder than wave 51 | `waves_53_60` §9.1 | Chapter 8's entire method |

---

## 5. Playtest — 64

The largest group, and the one the whole project has been building toward. **Seven of these are the structural questions at `broodline_telemetry.md`, one more is structural and arrived later, and the other fifty-six are tuning.** The structural ones are what soft launch must not arrive without answers to.

### The seven that can invalidate a system

| # | Question | Where |
|---|---|---|
| 1 | **Litter** — deployed at all, and too strong at tier III? | `base_stock` §9.1, `combat_numbers` §11, `sample_economy` §13.3 |
| 2 | **Skirmisher's 1.5s spawn interval** — is Splash a counter or a convenience? | `raider_roster` §10.1, `combat_numbers` §11.3 |
| 3 | **Is Screen chosen at wave 50?** The only wave a utility trait answers | `waves_45_52` §7.1 |
| 4 | **Wave 60 — forced or frustrating?** Five slots committed, none free | `waves_53_60` §9.4 |
| 5 | **Can Reach II and Burrow II be fused by wave 42?** The sample-economy test | `waves_37_44` §7.2 |
| 6 | **Regrow's regeneration cut** — too strong, and does wave 54 test it? | `combat_numbers` §11.1, `waves_53_60` §9.2 |
| 7 | **Wave 27's four-species requirement** — challenge or wall? | `waves_21_28` §7.2 |

### One more that is structural, and not a telemetry question

| # | Question | Where |
|---|---|---|
| 8 | **Are Bloodscent, Vanguard and Overwatch identifiable in a busy wave?** Bible §1.4 gives those three no trigger, so targeting behaviour is their entire in-combat signal. Decides whether production needs six Instinct cues, one shared cue, or none — and bible §10.3's asset count is marked contingent on it | `rig_proof` §9.3, bible §10.3 |

### The tuning questions, by area

Fifty-six, and I will not list them individually — every one is "is this number right" and every one is answered by the same playtest. By area:

| Area | Count | The shape of the question |
|---|---|---|
| Authored waves | 19 | Introduction discounts, integrity pools, spawn intervals, whether a lesson lands |
| Economy | 11 | Base rates, weightings, caps, the facility cost shares |
| Species and raiders | 8 | Signature weights, disturbance tells, flight altitudes, the Sunder's six integrity |
| Codex and UI | 7 | Threat-board contents, the missing-counters view, pre-wave check as a toggle |
| Alliances and raids | 6 | Convoy Rig size, two raids a day, the soft-immunity week |
| Notifications and offers | 5 | The daily cap, the +17% ceiling, the live-defence rate |

---

## 6. Soft launch — 35

Need real population and real spend. **These cannot be answered any earlier and should not be worried about until then.**

| Area | Count | Examples |
|---|---|---|
| **Server population** | 6 | The 1.5–3 harvesters-per-deposit figure everything derives from; what counts as a harvester; four tick slots or three |
| **Economy at scale** | 9 | The 6× spread; Calibration pricing by year three; Apex yield caps; the payer ceiling without ads |
| **Retention arc** | 7 | The soft-immunity landing; D14 churn; archetype-based UI as a product decision |
| **Live-ops** | 5 | Gene Lab alliance layer; authored versus generated Apex Cup waves; Recipe Share anti-spam |
| **Monetization** | 4 | ARPDAU against the comparables; the $9.99 and $99.99 price points; retroactive pass grants |
| **Seasons** | 4 | Fixed difficulty across two years; eight families before a repeat; reward amounts |

---

## 7. What this means for the next three months

Read against `broodline_build_order.md`:

| | |
|---|---|
| **This week** | Every decision is taken. Start procurement 1 at §3 |
| **Phase 1** | The rig proof and the engine. Every bench test at §4 runs here |
| **Phase 2–3** | The first hour and the loop. The seven structural playtest questions get instrumented from the first build that can run wave 6 |
| **Soft launch** | English only, four markets. §6 starts being answerable |

**Nothing in this list is a document.** The set is complete enough that the next document worth writing is the one that records what the first playtest found.

---

*Owns: the classification of every open question across the set, and the ordering. Does not own: any of the questions themselves — each lives in its document, and each is struck there when it closes.*
