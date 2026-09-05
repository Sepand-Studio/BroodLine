# Broodline — The Design Audit

*Historical record. Closed.*

> **This is not a working document.** It records what an audit of the design set
> found, and how it was closed. What remains open is in
> `broodline_whats_left.md`.
>
> Kept because several current decisions only make sense beside the thing they
> replaced, and because the patterns at §4 are worth not relearning.

---

## 1. What it was

The design had been written in two waves. Nine specs came first, against an unsettled game; nineteen reconciliation decisions then moved large parts of it and a design bible absorbed the result.

**A second wave of twelve documents was written between those two events and never mapped.** Half still described ten chassis, a damage triangle, four trait tiers and five Vault modules. None was indexed in the README or the supersession map, and between them they held the only versions of the shard income rates, the raid ruleset, the campaign milestone map, the region topology and the App Store moderation requirements.

The audit opened with **twenty contradictions and twenty gaps.** Twelve passes closed all of them.

---

## 2. What the audit found first

The five worth remembering, because each was invisible until someone read two documents side by side.

**The bible regressed on the pack ladder.** It carried a non-monotonic version — $14.99 buying more shards than $19.99 — that two later documents had already corrected. That is the exact arithmetic players run and post.

**`broodline_spec_reconciliation.md` was actively dangerous.** It declared itself authoritative — *"where it conflicts with an original spec, this is correct"* — while its constants ledger was wrong on eight rows. Anyone reasoning from it would have built the wrong game confidently.

**The screen inventory undercounted by a third.** Its header said 32 screens against tables holding 45; the true figure is 59, of which eight exist. The design workstream looked half done and was a seventh done.

**Collector scope had never been signed off.** One document made them Apex-only, another general-purpose, and the bible's Hold accrual assumed convoys were routine. Three systems rested on an open question.

**Nothing carried a status header.** Twenty-seven files, no way to tell current from superseded without reading.

---

## 3. What closed it

Twelve passes, and the shape of the work changed as it went.

| Passes | |
|---|---|
| **1–2** | The index layer. Status headers on every file, the supersession map rewritten, the README rebuilt, six second-wave documents brought current |
| **3–5** | The missing numbers. Combat values, the sample economy, base-stock supply — three documents that did not exist and that nine gaps were waiting on |
| **6–9** | The rewrites. Campaign, region roster, trait codex, raider roster, all against six species and eight raiders |
| **10–13** | Facility pricing, convoy staging, localization, live-ops tuning |

**The critical path was one document.** Until the combat numbers existed — species stats, trait coverage values, raider profiles — four rewrites and nine gaps could not move. Everything else ran in parallel behind it.

---

## 4. The patterns worth keeping

Six things the audit learned that are cheaper to remember than to rediscover.

**A lock is only a lock if it is written as a rule about damage or targeting.** Three of eight raider counters — Breaker, Bulwark, Delver — were specified as *conditions*: armoured, shielded, submerged. Each had a plausible number attached and in all three the arithmetic let ordinary damage through the side. The five that were sound were written as rules.

**Authoring finds what reading cannot.** Sixty campaign waves found ten errors in the specs they were authored against, including a budget formula that could not fund its own second wave and a loss screen that collapsed three different failures into one message. None would have surfaced from another read.

**A number that was right for one reason survives when the reason dies.** Rich Deposit depletion was set at six days *to sync with the weekly rotation*. That reasoning was destroyed by making depletion track extraction, and the number stayed. It needed the bounds put back deliberately.

**Reconciliation reassigns roles silently.** Generation lost its mechanical job under the tier-semantics decision and had to be explicitly restored as the coverage ceiling. Nothing flagged it; the system simply had a component doing nothing.

**Two documents sharing a name share a fate.** Most of the drift traced to Gene Vault meaning both a facility and a screen, and to *fragments* and *samples* being the same object under two words. The sample economy was split into its own file for exactly this reason.

**Check the second-order guarantee, not the first.** The four-type wave cap was audited and held; the *four-species* case — four counters scattered across four species, filling four of five deployment slots — was the one that actually reached the deployment limit, and nothing flagged it until a wave was authored.

---

## 5. Where things ended

| | At the audit | At close |
|---|---|---|
| Contradictions open | 20 | 0 |
| Gaps open | 20 | 0 |
| Files with no status header | 27 | 0 |
| Files indexed | 8 | all |
| Campaign waves authored | 0 | 60 |
| Regions authored | 9 | 30 |
| Decisions outstanding | — | 0 |

**The set grew from 27 files to 53** across the audit and the production work that followed it.

---

*Owns: the record of what the audit found and how it closed. Does not own: anything current. `broodline_whats_left.md` is the working list; `broodline_supersession_map.md` is the index.*
