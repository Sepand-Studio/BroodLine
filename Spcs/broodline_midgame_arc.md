# Broodline — The Mid-Game Arc

*Design spec, day 14 to day 90*

---

## 1. Why This Document Exists

The FTUE is detailed through day 14 and then stops. Week 3 says only "Apex Veins, Stake mechanics, garrison — requires an alliance and a mature roster," and nothing since has said what a mature roster is or how a player gets one.

This is the most dangerous kind of gap, because the genre's own literature — cited in the monetization spec's evidence base — puts the worst churn window at weeks three through eight, well past the window any onboarding document covers. A player who survives the FTUE is not safe. They're standing at the edge of the part of the game nobody designed.

Three systems already point at this arc without owning it: the Vault gives a shard-spending goal, the campaign's milestone gating creates a waiting period between Core tiers, and the alliance layer gives a social one. This document is what connects them into a sequence instead of three unrelated systems a player discovers by accident.

---

## 2. The Day 14 Cliff

The FTUE names this as the metric to watch and doesn't resolve it. Raid immunity ends, exactly when a player has had two weeks to accumulate cargo worth taking and has not yet built a defensive escort roster or joined an alliance that would rally around them.

**This document's answer: immunity doesn't end on a single day. It steps down.**

| Days 1–14 | Full immunity |
|---|---|
| Days 15–21 | Raidable, but **loss shield triggers after one loss instead of two** |
| Day 22+ | Standard rules — loss shield after two losses in 24 hours |

A one-week soft landing turns a cliff into a slope. The FTUE's own instruction — announce immunity's expiry as an event with a warning, paired with an escort tutorial — now has somewhere to point: the week 3 escort tutorial (§4) exists specifically to be ready before day 15.

---

## 3. The Shape of the Arc

Four phases, each with one job.

| Phase | Days | Job |
|---|---|---|
| **Landing** | 14–21 | Convert FTUE momentum into a standing habit |
| **Depth** | 22–35 | Introduce the systems that make sessions vary |
| **Commitment** | 36–60 | First alliance investment, first real Vault goal |
| **Arrival** | 61–90 | Player has an identity — raider, builder, or optimiser |

The FTUE's rule was one new system per session. This arc's rule is different because the pace has to change: **one new system per week**, with everything else in service of depth on systems already introduced. A day-40 player doesn't need new mechanics; they need reasons to keep using the ones they have.

---

## 4. Landing — Days 14 to 21

**Goal: the player has a reason to open the app that isn't "the tutorial told me to."**

| Day | Beat |
|---|---|
| 15 | Escort tutorial. Guided: assign two creatures as escorts, dispatch a Collector, watch the exposure window. |
| 16–18 | First real relocation decision — not the FTUE's scripted move, but the player's own read of the map against §4 of the region roster's richness/defensibility trade |
| 19 | First Gene Lab event a player experiences at real stakes, having built enough to contribute meaningfully |
| 21 | Soft immunity ends |

**The relocation decision is the phase's real content.** Everything before this point has been guided. This is the first moment the game asks the player to weigh a trade-off it has taught but not resolved for them, and it's deliberately placed right before raiding opens — a player who has just chosen defensible-but-poor or rich-but-exposed terrain is about to find out whether they chose well.

---

## 5. Depth — Days 22 to 35

**Goal: sessions stop looking identical to each other.**

This is where the campaign's chapter structure does real work. Chapters 3 through 5 (waves 15–40) land in this window for an average-pace player, introducing Mantle, Cairn, Flit, and Wither — which is to say, the mid-game campaign is what supplies the variety this phase needs, and it was already built for a different reason.

| System | Introduced | Why here |
|---|---|---|
| **Splice Roulette** | Day ~24 | Player has enough shard income to spend meaningfully; introducing it during the FTUE would have violated "nothing about probability tables in session one" |
| **Trait Codex, full index** | Day ~26 | Enough traits discovered that browsing is worth doing |
| **Region-weighted chassis** | Day ~28 | Player has relocated once (§4) and can read "this region has what I need" |
| **First Stake Assault, as spectator** | Day ~30 | Alliance notification of a contest the player isn't part of yet — social proof before participation |

**The Codex's progressive surfacing (per its own §9) has its second unlock here** — the filtered view that started in session two expands as discovery grows, and this is roughly when a player has enough entries for the full index to be worth the toggle rather than overwhelming.

---

## 6. Commitment — Days 36 to 60

**Goal: the player makes their first investment that outlasts a single session.**

This is the highest-risk phase for churn, because it's where the game asks for something back for the first time — garrison creatures that can't be used elsewhere, treasury contributions, a Vault Core tier that takes real hours.

| Milestone | Typical day | What it asks |
|---|---|---|
| First garrison contribution | ~38 | A creature becomes unavailable for raids or campaign |
| Core tier 6 (Vault) | ~40 | 8,000 shards, 8-hour timer — the first upgrade that can't complete in one sitting |
| First Apex Vein participation | ~45 | Requires alliance coordination and a Collector committed for the window |
| Alliance tech: first branch unlocked | ~50 | Treasury contribution becomes worth making |
| Chapter 6 complete (wave 50) | ~55 | Halfway through the campaign |

**Garrison contribution is the moment to protect most carefully.** The alliance spec is right that visibility is what makes contribution work, but a player's *first* contribution needs a softer landing than the steady-state system — recommend the game surface a **suggested contribution**, one mid-tier creature the player can spare, rather than asking them to choose cold from a roster they don't yet know how to value. Getting this wrong reads as "the game asked me to give something up and I didn't understand what I was giving."

**This phase is also where solo players make their real decision.** A player who hasn't joined an alliance by day 40 is telling the game something. The alliance spec's framing — solo is slower, not blocked — needs to actually hold here: Gene Lab's community layer, the Apex Cup, and full Common Vein access should be visibly sufficient to keep a solo player engaged past this point, not merely theoretically available.

---

## 7. Arrival — Days 61 to 90

**Goal: the player has settled into one of the game's three archetypes and the game is legibly rewarding that choice.**

The economy model already names these archetypes for income purposes. By day 90 they should be visible as play identities, not just spending tiers:

| Archetype | What day 90 looks like |
|---|---|
| **Raider** | Vault Drive and Harvest Array prioritised, active Marks Shop use, a revenge-token habit |
| **Builder** | Vault Habitat and Splice Chamber prioritised, deep roster, high Lineage View engagement |
| **Optimiser** | Alliance officer or active Stake contributor, Apex Vein regular, Apex Cup competitive |

No archetype should be strictly ahead of another by day 90 — the Vault spec's screens are explicitly meant to look different by month three for exactly this reason. What this phase adds is that the game should *notice*: distinct daily-objective sets or a recap screen reflecting a player's actual pattern back to them is worth more here than any new mechanic, because it's the first moment the game can plausibly say "I see what kind of player you are."

By day 90, Core tier 8 (second Vault build slot, ~week 7 for a Core player per the economy model) should be in reach or recently hit — the single clearest evidence, alongside chapter 6–7 campaign progress, that a player's investment compounds rather than plateaus.

---

## 8. Metrics

The FTUE tracks completion and D1/D3/D7. This arc needs its own instrumentation, because the failure mode here is slow rather than sharp — a player doesn't quit mid-session, they just stop coming back as often.

- **D14 → D21 retention**, isolated from D1–D7, to check whether the soft-landing immunity step actually holds the raid-exposure cliff
- **Time to first garrison contribution** and **completion rate of that flow** — the highest-risk single moment in the arc
- **Solo-vs-allied retention curves from day 40 onward**, to verify solo play is genuinely sufficient rather than sufficient in theory
- **Session frequency by week**, not just retention — a player who logs in daily but for thirty seconds is a different problem than one who logged in three times and stopped
- **Archetype legibility**: by day 90, can the game classify a player's pattern with confidence? If most players are illegible, the three archetypes are a modeling convenience, not a real description of how people play

---

## 9. Guardrails

- No new core system introduced after week 5; depth comes from existing systems, not new ones
- Raid immunity steps down over a week, never drops in one day
- First garrison contribution is suggested, not chosen cold
- Solo play remains genuinely competitive through day 90, verified by retention data rather than assumed from the alliance spec's design intent
- No archetype is rewarded ahead of the others in raw progression speed

---

## 10. Open Questions

1. **Is one week the right length for the soft immunity landing?** Longer protects more; it also delays the raiding spec's economy from getting a full cohort of active targets.
2. **Should the suggested garrison contribution be undoable** — a one-time "actually, take this back" — to lower the stakes of the first ask further?
3. **Does the Codex's second progressive-surfacing unlock need its own prompt**, or should it just happen silently when the player next opens it?
4. **Archetype-based UI** (§7) is a real product decision, not just a metrics one. Does the roadmap have room for a personalized recap or objective set, or is this phase's insight advisory only for now?
5. **This document assumes an average-pace player throughout.** A much faster or slower player hits these beats at different days, and the phase boundaries may need to be defined by milestones reached rather than calendar days.

---

*This closes the design gaps identified in the spec audit. Remaining: profile, mail, and settings — conventional screens, buildable without a spec.*
