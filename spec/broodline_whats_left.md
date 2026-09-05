# Broodline — What's Left

*The state of the project, and the only document that says what is not done*

> **CURRENT.** Replaces `broodline_gap_register.md` as the working list. That
> file tracked gaps in the design and closed all of them; this tracks what
> remains across the whole set, which is a different kind of list.

---

## 1. Where the project stands

**Fifty-two files.** The design is finished. The content is finished — sixty waves, thirty regions, the graph. The technical foundations are specified — engine, data model, telemetry, server topology, store. The production plan exists and names its gates.

**One hundred and thirty-nine open questions remain across thirty-four current documents**, and the important thing about them is that **almost none can be answered by writing.** They sort into five kinds, and four of the five need something other than a document.

| Kind | Count | Closes when |
|---|---|---|
| **Decisions** | 22 | Someone decides |
| **Procurement and commissioning** | 8 | Something external is bought or briefed |
| **Bench tests** | 11 | A build exists, no players needed |
| **Playtest** | 63 | Players play |
| **Soft launch** | 35 | Real population, real spend |

**The writing is done.** What remains is deciding, buying, building and watching.

---

## 2. Decisions — 22

Nobody is blocked by these, but every one gets more expensive the later it is taken. **The first six are the ones I would take this week.**

| # | Decision | Where | My pick |
|---|---|---|---|
| 1 | **Does the offer engine's unprompted slot exist at all?** | `offers` §9.3 | **Cut it.** Shortfall-only is simpler, entirely useful, and impossible to call pushy |
| 2 | **Seven launch languages, or five?** | `localization` §11.1 | **Five.** Drop French and Spanish to wave two; it shortens the critical path materially |
| 3 | **Telemetry analysis window** | `telemetry` §12.1 | **90 days.** It is the working assumption and it drives storage and obligations |
| 4 | **Rig proof duration** | `rig_proof` §9.4 | **Three weeks.** A gate with no deadline is not a gate |
| 5 | **Raid verification: every raid, or sampled?** | `combat_engine` §11.2 | **Every raid.** Determinism makes it cheap enough, and sampling lets cheating through during the window where reputation is set |
| 6 | **Arabic's deferral date** | `localization` §11.5 | **Wave three, six months post-launch.** Deferred without a date becomes never |
| 7 | Server merge rule | `server_topology` §9.3 | Clear territory, re-open at next tick. Cruel and legible |
| 8 | Suppress the raid alert overnight? | `notifications` §10.1 | Yes. It costs nothing and delivering it costs goodwill |
| 9 | Region names — transliterate or native equivalents? | `localization` §11.2 | Transliterate, same rule as species |
| 10 | Cross-promote future titles inside Broodline? | `offers` §9.4 | No. Same answer as third-party ads |
| 11 | Freeze coverage values before translation? | `localization` §11.3 | Yes, and accept it constrains tuning |
| 12 | Convoy Rig visible on the Transit Board as a Rig? | `alliance` §12.7 | Visible, with contributor count |
| 13 | Screen-reader scope — menus only, or attempt combat? | `accessibility` §8.3 | Menus, properly. Be honest about combat |
| 14 | Under-13 chat: canned phrases, or none? | `moderation` §12.2 | None |
| 15 | Moderation staffing model | `moderation` §12.3 | Automated-first with human escalation |
| 16 | Chat log retention | `moderation` §12.4 | 30 days |
| 17 | Skittish reposition — random pocket, or nearest safe? | `combat_engine` §11.4 | Nearest safe. Reduces RNG to tie-breaks only |
| 18 | Replay on a superseded engine — old rules, or recorded outcome? | `combat_engine` §11.3 | Recorded outcome with a notice. Retaining old engines is a maintenance burden |
| 19 | Retirement cooldown? | `sample_economy` §13.5 | None until playtest shows the locker exploit is real |
| 20 | Transit Board shows cargo value? | `raiding` §15.1 | Yes, banded rather than exact |
| 21 | Pale grant on defeat, or on retry? | `campaign` §12.4 | Defeat. Immediate, and bible §9.3 asks for immediate |
| 22 | Inner Reach ever claimable in a late season? | `region_roster` §11.3 | No, firmly |

---

## 3. Procurement and commissioning — 8

External lead times. **The first two should already be in motion.**

| # | Item | Where |
|---|---|---|
| 1 | **Moderation filter, seven languages** | `localization` §7, `build_order` §2 |
| 2 | **CJK font substitutes**, weight-matched, tabular figures verified | `localization` §8 |
| 3 | The rig proof commission — two bodies, twelve socket-agnostic parts, forty-eight renders | `rig_proof` §8 |
| 4 | Species palette fix — widen lightness separation on two collapsing pairs | `accessibility` §4 |
| 5 | Eleven behaviour previews, costed | `trait_codex` §10.1 |
| 6 | Twenty raider animation clips, quoted rather than estimated | `raider_roster` §10.4 |
| 7 | Eighteen species vocalisations — six animals, three states, no monster register | `audio` §13.2 |
| 8 | The blocked-hit visual tell — art arising from an audio requirement, unowned | `audio` §13.1 |

---

## 4. Bench tests — 11

Answerable with a build and no players. **All of these belong in Phase 1.**

| # | Test | Where | Invalidates if wrong |
|---|---|---|---|
| 1 | **Wave 44 at ~100 entities** on bottom-quartile devices | `telemetry` §8, four wave docs | Five authored waves cannot ship |
| 2 | **All eight locks pass the rule test** — breach rate near 100% without the answer | `telemetry` §4 | A lock has a hole. Free — the engine computes it |
| 3 | Socket-agnostic parts read in both positions across two bodies | `rig_proof` §4 | The 24-asset budget |
| 4 | Growth as a proportion curve, not separate assets | `rig_proof` §6 | The 3D decision |
| 5 | Silhouettes distinct at 40px with parts attached | `rig_proof` §4 | The art direction |
| 6 | Hauler carries plate and shield simultaneously | `rig_proof` §3.2 | The Sunder becomes one combined kit |
| 7 | Four-pocket Delta terrain against 44pt targets on the smallest device | `accessibility` §8.4 | Delta terrain, or the target size |
| 8 | Pack ladder monotonic in every storefront | `store_iap` §8 | Build fails, correctly |
| 9 | Skirmisher and Courser distinguishable at a glance | `raider_roster` §10.2 | Scale is the lever |
| 10 | The Slow and Fast wave shapes | `seasonal` §10.4 | Two of the most distinctive seasonal shapes |
| 11 | Wave 53 reads as harder than wave 51 | `waves_53_60` §9.1 | Chapter 8's entire method |

---

## 5. Playtest — 63

The largest group, and the one the whole project has been building toward. **Seven of these are the structural questions at `broodline_telemetry.md`; the other fifty-six are tuning.** The seven are what soft launch must not arrive without answers to.

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
| **This week** | Take decisions 1–6 at §2. Start procurement 1–2 at §3 |
| **Phase 1** | The rig proof and the engine. Every bench test at §4 runs here |
| **Phase 2–3** | The first hour and the loop. The seven structural playtest questions get instrumented from the first build that can run wave 6 |
| **Soft launch** | English only, four markets. §6 starts being answerable |

**Nothing in this list is a document.** The set is complete enough that the next document worth writing is the one that records what the first playtest found.

---

*Owns: the classification of every open question across the set, and the ordering. Does not own: any of the questions themselves — each lives in its document, and each is struck there when it closes.*
