---
status: current
folder: 03-technical
note: >
  The seven playtest questions as events, metrics and thresholds. Design
  telemetry, not operational monitoring.
---

# Broodline — Telemetry

*Technical spec, what to measure and what it would mean*

> **CURRENT — technical spec.** `broodline_build_order.md` §5 lists what playtest
> has to answer. This document turns each into an event, a metric and a
> threshold, so the answer is a number rather than an impression.

**Instrument before the first playable build.** Every question here is answerable from data the game already has to compute — but only if it is logged at the moment it exists. Three of the seven cannot be reconstructed after the fact.

---

## 1. The principle

> **A telemetry question is worth building only if a specific number would change a specific decision.**

Every metric below names the decision it changes. Anything that would be interesting but would not change anything is not here, and the temptation to add it should be resisted — a dashboard nobody acts on is a dashboard nobody reads.

**Seven questions, and they are not equal.** Two of them can invalidate a system; five would move a number.

---

## 2. Q1 — Litter, in both directions

**The design's largest open risk**, per bible §1.2: if the four utility traits are weak, four species become delivery vehicles for a single counter and the roster collapses. `broodline_base_stock.md` §4.3 then found Litter might be too *strong* instead.

| | |
|---|---|
| **Events** | `creature_deployed` with full loadout, per wave. `base_stock_granted` with source |
| **Metric A** | Share of waves where at least one deployed creature carries Litter |
| **Metric B** | Share of a player's daily base stock originating from `source: litter` |
| **Fails low if** | Metric A under **10%** — Litter carriers are never deployed, so a combat slot is being spent on a non-combat effect |
| **Fails high if** | Metric B over **40%** — one trait supplies most of a player's creature supply |
| **Decision** | Low: move Litter off the combat slot or replace its effect. High: tiers move from 8/6/4 hours to 12/9/6 |

**Both directions must be measured**, because the two failures have opposite fixes and one metric cannot distinguish them.

---

## 3. Q2 — Skirmisher's spawn interval

**The single highest-leverage untested number in the combat design**, per `broodline_raider_roster.md` §10. It decides whether Splash is a counter or a convenience.

| | |
|---|---|
| **Events** | `wave_result` with wave id, outcome, integrity remaining. `breach` with raider type |
| **Metric** | Skirmisher breach rate per wave, split by the deploying roster's Splash coverage tier — none, I, II, III |
| **Working** | Breach rate falls materially with each tier. Roughly: **no Splash > 50%, Splash I ~25%, II ~10%, III near zero** |
| **Fails if** | The curve is flat — rosters without Splash breach at similar rates to Splash II rosters |
| **Decision** | Flat curve: test 1.0s and 2.0s intervals before touching any raider stat |

**Wave 10 is the natural test case.** `broodline_waves_01_12.md` authors it at a 1.2s interval specifically to make volume bite, which makes it a partial live test of the number.

---

## 4. Q3 — Are the locks locks?

Three of eight raider locks turned out to be soft on inspection, per `broodline_waves_37_44.md` §5.1, and all three were corrected. **The corrections are untested.**

| | |
|---|---|
| **Events** | `breach` with raider type and the three diagnosis booleans from `broodline_combat_engine.md` §7 |
| **Metric** | For each of the eight raiders: **breach rate among rosters with `access: false`** |
| **Working** | Near **100%** for the six hard locks. A raider that gets through less than 80% of the time without its answer is not a lock |
| **Soft by design** | Skirmisher and Lash. Their rates should be materially lower and that is correct |
| **Decision** | Any hard-lock raider under 80%: the rule has a hole, and the fix is structural rather than a stat change |

**This is free.** The engine computes the diagnosis at breach time for the Wave Defeat screen; logging it costs one field.

---

## 5. Q4 — Is Screen ever chosen?

**The only wave in sixty that a utility trait genuinely answers**, per `broodline_waves_45_52.md` §5. If Screen is not picked at wave 50 it will not be picked anywhere.

| | |
|---|---|
| **Events** | `creature_deployed` with full loadout, on wave 50 specifically, per attempt |
| **Metric** | Share of wave-50 attempts deploying a Screen carrier, **and the change between first attempt and successful attempt** |
| **Working** | Screen presence rises across attempts — players who fail without it and succeed with it have learned something |
| **Fails if** | Under **15%** on successful attempts, or no change across attempts |
| **Decision** | Screen's effect is too weak, or the wave does not communicate what it is asking. Both are fixable; neither is fixable after launch without a balance patch nobody will read |

**The first-to-successful delta is the real metric**, not the raw rate. A player who brings Screen by habit tells you nothing; a player who adds it after losing tells you the wave worked.

---

## 6. Q5 — Is wave 60 forced or frustrating?

`broodline_waves_53_60.md` §6 commits all five deployment slots. **Every other wave in the campaign leaves at least one free**, and the difference between "tight" and "forced" is the difference between a capstone and a wall.

| | |
|---|---|
| **Events** | `wave_attempt` with attempt index. `wave_abandoned`. `session_end` with last wave attempted |
| **Metric** | Median attempts to clear wave 60, and the **abandonment rate** — players who attempt it and do not return within seven days |
| **Working** | Median **3–8 attempts**, abandonment under **10%** |
| **Fails if** | Abandonment over 20%, or median over 15 attempts |
| **Decision** | Relax the composition — drop the Coursers or the Delvers to two, which frees a slot without changing the Sunder |

**Abandonment matters more than attempts.** A player retrying fifteen times is engaged; a player who stops is the failure, and the two look identical in an attempt count.

---

## 7. Q6 — The sample economy at wave 42

`broodline_waves_37_44.md` §4: wave 42 is the first wave where fusing is not optional, and it arrives around week eight. If coverage cannot be fused by then, sample drop rates are too low.

| | |
|---|---|
| **Events** | `wave_attempt` with the deploying roster's coverage tiers. `sample_granted`, `sample_fused` |
| **Metric** | Share of players reaching wave 42 who hold **Reach II and Burrow II simultaneously** |
| **Working** | Over **60%** |
| **Fails if** | Under 30% — the wave demands coverage the economy has not supplied |
| **Decision** | Raise sample drop rates at `broodline_sample_economy.md` §3, or move wave 42 later. Raising drops is preferable; the wave is well placed |

---

## 8. Q7 — Entity count

Flagged in three wave documents. **Wave 44 is the worst case in the campaign and it exists**, which makes this the easiest question to answer and the one most likely to be discovered late.

| | |
|---|---|
| **Events** | `frame_stats` sampled at peak entity count, with device class |
| **Metric** | Frame time at peak on the **bottom quartile of supported devices** |
| **Working** | Under 33ms sustained — the simulation runs at 30Hz and the render must not be the constraint |
| **Fails if** | Frame time over 50ms, or thermal throttling within a wave |
| **Decision** | Instance Skirmishers and Mites for render, or cut the largest counts. **Cutting counts is a content change across five authored waves** and should be the second option |

**This is a proof, not a metric.** It can be answered on a bench before any player sees it, and `broodline_combat_engine.md` §9 says it should run in parallel with the rig proof.

---

## 9. The event schema

Nine events carry all seven questions.

| Event | Fields |
|---|---|
| `wave_attempt` | wave_id, attempt_index, terrain, roster snapshot |
| `wave_result` | wave_id, outcome, integrity_remaining, duration_ticks |
| `breach` | wave_id, raider_type, lane, tick, **access / coverage / placement** |
| `creature_deployed` | wave_id, species, both traits with tiers, instinct, pocket, lane |
| `sample_granted` | trait, tier, source |
| `sample_fused` | trait, from_tier, to_tier |
| `base_stock_granted` | species, **source** |
| `wave_abandoned` | wave_id, attempt_index |
| `frame_stats` | peak_entities, frame_time_ms, device_class |

**`creature_deployed` is the expensive one** — five rows per wave attempt, each with a full loadout. It is also the only way to answer Q1 and Q4, both of which are about what a player *chose to bring*, which cannot be reconstructed from an outcome.

**Sample it if volume is a problem, but sample by player rather than by wave.** A 10% player sample answers every question here; a 10% wave sample destroys the first-to-successful delta that Q4 depends on.

---

## 10. What is deliberately not measured

**Nothing about a named individual player's roster is retained beyond the analysis window.** These events are for balance, and a per-player creature history is a different thing with different obligations.

**No engagement metrics here.** Session length, D1/D7 retention and ARPDAU are real and they are measured elsewhere. Mixing them into a balance dashboard produces the failure mode where a wave gets easier because retention dipped for an unrelated reason.

**No leaderboards or comparative displays derived from this data.** It is diagnostic, and surfacing it changes behaviour it is trying to observe.

---

## 11. Priority

Not all seven need to ship together.

| | Before the first playable build |
|---|---|
| **Q3** | Free — the engine already computes it |
| **Q7** | A bench test, no player needed |
| **Q1, Q2** | Both need `creature_deployed`, which is the largest event. Build it once for both |

| | Before soft launch |
|---|---|
| **Q4, Q5, Q6** | All need players reaching waves 42, 50 and 60, which is weeks of play |

**Arriving at soft launch with seven open structural questions wastes the market**, per the build order. Four of the seven can be answered before a single external player sees the game.

---

## 12. Open questions

1. **What is the analysis window?** Retention of raw events drives storage and it drives obligations. Ninety days is the working assumption and nobody has set it.
2. **Q4's threshold of 15% is a guess.** So is Q1's 10% and Q6's 60%. They are stated as numbers because a threshold nobody wrote down is a threshold nobody checks, but the first playtest should be treated as calibrating them rather than testing against them.
3. **Nothing measures whether the Codex is opened.** It is required for the counter system to be learnable by anything other than losing, and its usage is unmeasured. A `codex_opened` event with the context it was opened from would be cheap and would answer whether the bottom sheet placement was right.
4. **Q5 measures wave 60 but not wave 27 or 58**, the two other waves that force four species. If wave 60 fails, knowing whether the four-species waves also strained would tell you whether the problem is the fifth slot or the whole approach.

---

*Owns: the seven playtest questions as events, metrics and thresholds, the event schema and the instrumentation priority. Does not own: the design questions themselves (`broodline_build_order.md` §5), engagement metrics, or the simulation (`broodline_combat_engine.md`).*
