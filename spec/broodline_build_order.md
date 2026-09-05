# Broodline — Build Order

*Production plan, what to make and in what order*

> **CURRENT — the plan.** `broodline_gap_register.md` tracked what was missing
> from the design. Nothing structural is missing now. This document tracks what
> is missing from the *game*, which is a different list with different owners.

---

## 1. Where the project actually stands

**The design is done.** Seventeen current documents, one bible, every mechanic specified, every constant set to a soft-launch starting value. The remaining open questions in those documents are tuning questions, and tuning questions are answered by playtest rather than by writing.

**Almost none of the game is made.** Fifty-nine screens, of which nine exist and are current. Sixty campaign waves, none authored. Thirty-six character assets, none modelled. Twenty animation clips and eleven behaviour previews, none produced. Two procurement items with external lead times that nobody has started.

That asymmetry is the whole reason for this document. The register's habit — find the missing spec, write it — has no more moves. The next move is different in kind.

---

## 2. Start the procurement now

**Both of these have lead times measured in weeks and neither depends on anything.** They should be in motion before the first screen is built, because the failure mode is discovering in week twenty that a launch language has no filter.

| Item | Why now | Blocks |
|---|---|---|
| **Moderation filter, 7 languages** | Vendor selection, contracting and per-language tuning are external. App Review rejects an app carrying UGC without filtering | Any server opening in that language |
| **CJK font substitutes** | Weight-matching Noto Sans to the display face is a design task, but licensing and the tabular-figure verification are not | Chinese, Japanese and Korean builds |

Neither is glamorous and both are the kind of thing that quietly becomes the critical path.

---

## 3. The critical path

Five phases. The ordering is by dependency, not by importance.

### Phase 1 — Prove the pipeline

**Nothing else is safe to start until these three are done.**

| Task | Why first |
|---|---|
| **Rig and animate two species** | Bible §10.3 requires attachment-point standardisation before the first creature is modelled. **Vetch and Pale** — one species cannot test whether the same part mounts on dissimilar bodies, which is the actual risk. Brief and acceptance criteria: `broodline_rig_proof.md` |
| **Build the combat engine against one lane** | Every wave, every raid, every region defence and the entire Apex Cup run on it. One lane, one raider, five pockets, the counter check. Spec: `broodline_combat_engine.md` |
| **Build the Codex bottom sheet** | The screen inventory already flags it build-first. Every trait pip in the app opens it, so it is a dependency of almost every other screen rather than a screen of its own |

**The pipeline proof is a gate, not a milestone.** If two dissimilar bodies cannot carry the same twelve trait parts in either socket at acceptable quality, the twenty-four-asset budget is wrong and the art plan changes before money is spent on it. `broodline_rig_proof.md` states what passing looks like and what each failure costs.

### Phase 2 — The first hour

Everything a player touches in session one, plus the two beats that follow.

- Splice screen and confirmation flow, complete, including the coverage-loss notice
- Wave 1 and 2, authored, and the beat structure around them
- Founder naming, with the skip path
- Lineage View, two generations
- Roster
- Wave 6 and the Wave Defeat screen — the designed Courser loss
- Account creation and the age gate
- Trait Codex index and the threat board

**Wave 6 belongs in this phase and not a later one.** It is the beat that teaches the counter system, and it is the first place the design can be wrong in a way that playtest will show. A first hour that ends before the designed loss tests the tutorial but not the game.

### Phase 3 — The loop

The systems a player meets in week one, in the drip order at bible §9.7.

- Gene Lab, all six facilities, and the build/timer/skip flow
- World map, region detail, relocation
- Harvesting and the node lifecycle
- Sample Store and fusing
- Campaign chapters 1 to 3, authored — waves 1 to 20
- Region defence

**This is the phase where the economy becomes testable.** Everything before it is content; this is the first point at which shard income, sample income, base stock supply and splice cadence can be observed against each other rather than reasoned about.

### Phase 4 — The meta

Day 14 onward. The layer that turns a game into a live one.

- Collectors, routes, the Transit Board and raiding, with all four protection systems
- Alliances, Stakes, Hold, garrisons and Stake Assault
- Convoy staging and joint raids
- Campaign chapters 4 to 8 — waves 21 to 60
- The Sunder
- Mail, profile, settings, report and block

**Report and block ship with the first UGC surface, whichever phase that lands in.** They are listed here for completeness, not for scheduling. App Review's rejection arrives at the end of the process rather than the start.

### Phase 5 — Live-ops

Everything that needs a running server to mean anything.

- Event hub and all five events
- Splice Roulette with the published odds and pity counter
- Apex Cup gauntlet
- Season Pass
- Store, all six pack tiers, per-storefront monotonicity verified
- Recipe Share

---

## 4. Content authoring, in parallel

Three tracks that do not block the critical path and are large enough to start early.

| Track | Volume | Depends on |
|---|---|---|
| **Campaign waves** | **All 60 authored** across seven wave documents | Done. The engine still has to be built against them |
| **Region adjacency graph** | 30 regions, 2–4 borders each, 8 gate pairs fixed | Nothing. It is level design against an authored roster |
| **Character art** | 36 assets — 6 species, 12 socket-agnostic trait parts, 6 Instinct cues, 4 raider bodies, 8 variant kits | The Phase 1 rig proof, and nothing after it |

**Authoring the first twelve found four errors in the specs they were authored against** — the budget formula's base, Skirmisher pricing, Lash's introduction wave, and where session one ends. The next eight settled a fifth: the growth rate is right, and the first two chapters only looked anomalous because the budget was being read as a target rather than a ceiling. Chapter 4 found a sixth, and it is the largest — **the Wave Defeat screen collapses three different failures into one message**, and for every raider after Courser that makes the game look like it is lying to the player. Chapter 5 found a seventh: **Bulwark's shield was not a lock**, because Sprint's mechanic is Skitter's base attack rate, so a Skitter answered it without the trait. Chapter 6 found the eighth and generalised it — **three of the eight locks were written as conditions rather than as rules about damage**, and all three let ordinary damage through the side. That is the argument for authoring early rather than late, and for authoring chapter 4 before building anything that depends on the numbers.

**All three tracks are complete.** What remains under "content" is the seasonal chapters, which have no schedule yet and should be authored before they get one.

---

## 5. What playtest has to answer

Every number in the set is a starting value. These are the ones where being wrong changes a system rather than a figure, in the order they should be checked.

| # | Question | If it is wrong |
|---|---|---|
| 1 | **Litter, in both directions.** Does anyone deploy a Litter carrier? Does Litter III supply half a player's base stock? | The trait moves to 12/9/6 hours, or off the combat slot entirely |
| 1b | **Wave 50 — is Screen chosen?** The only wave in the campaign a utility trait genuinely answers | If Screen is not picked there it will not be picked anywhere |
| 2 | **Skirmisher's 1.5-second spawn interval** | Splash stops being a counter and becomes a convenience. The single highest-leverage untested number |
| 3 | **Breaker's Plate as a real lock** | Pierce becomes optional and the hardest counter in the game is decorative |
| 3b | ~~Delver's submersion~~ **— checked and corrected.** All eight locks now pass the rule test | — |
| 4 | **Splash III's radius** | Ember runs away with the metagame |
| 5 | **The four-species worst case** at combat numbers §8.1 | Waves that force all six species land as a wall rather than a challenge |
| 6 | **Wave 44 at a hundred entities** — a render-budget question, not a simulation one | The largest authored waves cannot ship as authored |
| 6 | **The 75/25 harvest weighting** | Either most sample inventory is dead, or a player can never build toward a new species |
| 7 | **Weir** | The one terrain family whose difficulty is about damage rate rather than counter breadth may undercut the design's central claim |

**One and two are the two to instrument from the first playable build.** Both are cheap to measure and both change a system rather than a value.

---

## 6. Decisions still owed

Not gaps — nobody is blocked. But each gets more expensive the later it is taken.

| Decision | Cost of deferring |
|---|---|
| **Seven launch languages, or five?** | Each language is translation, filter procurement, a font decision and a QA pass across 59 screens. Dropping French and Spanish to wave two shortens the critical path materially |
| **Arabic's deferral needs a date** | RTL gets more expensive with every screen added. Deferred without a date becomes never |
| **Region names: transliterate, or native equivalents?** | Thirty place names. Cheap now, a retranslation later |
| **Freeze coverage values before translation** | A tuning pass at soft launch could invalidate six languages of Codex copy at once |
| **Is the Convoy Rig visible on the Transit Board as a Rig?** | Small, but it changes what the raid UI has to show |

---

## 7. What must not slip

The five structural guarantees, restated because build order is where they get traded away.

1. **No purchase grants trait access.** It has already leaked in four separate systems during design; it will be proposed again during build, as a small exception in a store pack
2. **Campaign milestones gate Core tiers.** The anti-whale structure is one line of logic and its removal would not look like a design change
3. **Waves escalate by count, lane and integrity — never by raider stats**
4. **No creature is ever lost involuntarily**
5. **Report, block, filtering and a published contact address ship with the first UGC surface**

**The first is the one to watch.** The other four are visible if broken. A pack that quietly grants a creature of a named species looks like a generous bundle.

---

## 8. The shape of the schedule

Rough, and offered as a sanity check rather than a plan.

| Phase | Character |
|---|---|
| Procurement | Runs alongside everything. Start now |
| 1 — Pipeline | Short, and a gate. Nothing large should start before it clears |
| 2 — First hour | The first thing worth showing anyone |
| 3 — Loop | The first thing worth playtesting. Economy becomes observable here |
| 4 — Meta | The longest phase, and the one where the campaign authoring has to have kept pace |
| 5 — Live-ops | Cannot be meaningfully tested before soft launch |
| Soft launch | English only, in Canada, Australia, New Zealand and the Philippines |

**Soft launch is where every number in seventeen documents gets checked at once**, which is why the playtest list at §5 should be instrumented long before it — arriving at soft launch with seven open structural questions wastes the market.

---

*Owns: build order, phase contents, the procurement start, the playtest priority list, and the decisions still owed. Does not own: any design decision — every one of those lives in the bible or a companion.*
