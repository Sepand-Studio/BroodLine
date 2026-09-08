---
status: superseded
folder: 99-archive
superseded-by: broodline_whats_left.md
note: >
  Audit of the 27-file era against the bible, with its fix pass applied.
  Superseded as the working list.
---

# Broodline — Gap & Inconsistency Register

*Audit of all 27 project files against `broodline_bible.md`.*
*v1 4 Sep 2026 — audit. **v2 4 Sep 2026 — first fix pass applied.***

---

## What changed in v2

**Final pass, same day.** A scan of the eleven current documents after the fix pass found and closed: 31 stale cross-references still pointing at superseded specs by name; the map's three bands, gates and inverse richness rule being used in bible §5.2 without being defined anywhere current (now bible §5.7); the charge cap rising "per three tiers" from tier 2 (now even tiers); the drip schedule still ending immunity flat on day 14; "Vault Calibration" surviving as a screen name; old chassis names surviving as filter examples in the moderation document; and the bible and README both saying "six companions" above a table of seven.


A fix pass has landed. **Nine documents were edited, three rewritten, and sixteen given status headers.** Of twenty contradictions, eighteen are closed. Of twenty gaps, five are closed and two are partly closed.

**What is left is almost entirely the same shape of problem: five documents describing the pre-reconciliation game, and no numbers anywhere in combat.** Those two are related — four of the five rewrites are blocked on the combat numbers spec, because a campaign wave table or a raider roster cannot be written against species that have no stats.

| | v1 | v2 |
|---|---|---|
| Contradictions open | 20 | 2 |
| Gaps open | 20 | **13** (2 partly closed) |
| Documents with no status header | 27 | **0** |
| Documents indexed in the README | 8 | **27** |

---

## Closed — v13

**The last gap is closed.** Live-ops tuning landed: the Gene Lab event's scaling formula, the Apex Cup gauntlet format, and the Mutation Surge cadence at six a year rather than four.

**Gene Lab: Target = 18 × A × D**, where A is accounts that spliced in the trailing seven days, floored at 200 and capped at 5,000. Eighteen is about three days of a core player's splicing across a five-day event. The floor stops a sixty-player server clearing 100% on day one; the cap means a mature server wins comfortably, which is deliberate — a server-wide goal that punishes success would be a strange thing to build. A is trailing rather than live, because a target that moves underneath players mid-event feels rigged.

**Apex Cup: twenty waves, one terrain family, three attempts, integrity that does not reset.** Integrity carrying across waves is what makes it a gauntlet rather than twenty campaign waves — a leak on wave 3 still costs on wave 17. Losses persist too, so roster *depth* stops mattering and roster *quality* starts to, which is the point of ranking on genetics. One family per month rotating across the eight gives eight distinct metagames a year out of content that already exists.

---

## This register is now historical

Twelve passes, twenty contradictions and twenty gaps, all closed. Every mechanic in the game is specified and every constant has a soft-launch starting value.

**It is no longer the document that says what to do next.** What remains is production — fifty-nine screens of which nine exist, sixty campaign waves of which none are authored, thirty-six character assets of which none are modelled, and two procurement items nobody has started. That is `broodline_build_order.md`.

Keep this one for the reasoning. Several decisions in the set only make sense alongside the thing they replaced.

| | v1 | … | v11 | v12 | v13 |
|---|---|---|---|---|---|
| Contradictions open | 20 | | 1 | 1 | **0** |
| Gaps open | 20 | | 2 | 1 | **0** |

*The one remaining contradiction — C18, vocabulary in the queued rewrites — closed when the last of those rewrites landed.*

---

## What changed in v12

**`broodline_localization.md` written.** Launch language set, naming policy, translation scope and cost, per-storefront pricing rules, filter procurement, font coverage and jurisdictional age thresholds.

**Three things surfaced that were invisible from inside English.**

**The pack ladder's monotonicity was only ever checked in dollars.** Apple's price tiers do not convert linearly, so a ladder that rises correctly in USD can invert in yen or lira — which would put the exact screenshot the fix at C1 existed to prevent in front of Japanese and Turkish players. The rule: verify per storefront, and move the shard counts rather than the prices, since prices are constrained to the tier grid.

**The display face does not cover CJK.** Chinese, Japanese and Korean are among the largest markets for this genre, and without a deliberate substitute they fall back to a system font — the brand's display voice absent in the places it matters most. Weight-matched Noto Sans substitutes, with the tabular-figure check re-run in each.

**Rename Stillborn.** The Aberrant Instinct meaning *never moves* is a poor word in English in a game about breeding animals, at a 12+ rating, and considerably worse translated literally into a language where the clinical register does not cushion it. **Renamed to Rooted** across the set.

**Three earlier decisions paid for themselves here.** Behaviour previews carry no text, so eleven animations localize for free where eleven paragraphs of behaviour prose would have been the hardest text in the game to translate. The Codex's "effects are numbers, never adjectives" rule turns out to be a localization rule too — *"hits 2 targets within 1 tile"* survives translation where *"improves area damage"* becomes six different claims. And the raider roster's "the mechanic is the silhouette" means a player who cannot read the Codex in their language can still see that Bulwark's shield is the problem.

**Mainland China is not a localization task.** Simplified Chinese on the global store serves Taiwan, Hong Kong, Singapore and Malaysia; the mainland requires a publishing licence, a local publisher, real-name registration and playtime limits, which change the product rather than the strings. Separate business decision.

| | v1 | … | v10 | v11 | v12 |
|---|---|---|---|---|---|
| Contradictions open | 20 | | 1 | 1 | 1 |
| Gaps open | 20 | | 3 | 2 | 1 |

**The design is complete.** One gap remains and it is a tuning pass, not a design question.

---

## What changed in v11

**Alliance convoy staging designed, and `broodline_alliance_territory.md` brought current.** The last superseded document with structurally sound content moves into the companion set — a vocabulary pass, the Convoy branch picking up Collector-class unlocks now that Drive cannot, convoy staging at §7.1, and the weekly tick decision.

**Staging turned out to buy something other than what it looks like.** Four members running solo convoys face four separate 40% loss risks; one Convoy Rig faces a single 40% risk on four times the cargo. The expected loss is identical. **What staging actually buys is escort concentration** — four members' escorts on one convoy against an attacker who can still bring only three creatures.

**Which forced a mechanic that did not exist: joint raids.** An eight-escort convoy against a three-creature party is untakeable, and an untakeable convoy is a PvP economy that has stopped. So a Convoy Rig may be raided by up to three attackers from one alliance, each bringing a full party. Convoy staging is the one place in the game where alliance-versus-alliance is literally true.

**And a guardrail that protects the thing it could have broken.** Joint raids fire **only** against Convoy Rigs. A solo player's convoy is never hit by a three-person party, under any circumstances. Bible §6.10's promise that solo is slower rather than blocked would not have survived the alternative.

**The weekly tick is per-server, from three fixed slots, never changed.** A single global hour hands two thirds of the world a tick that fires while they sleep — and the tick is when territory changes hands, deposits rotate and the weekly event resolves. Three slots approximating APAC, EMEA and Americas prime evening, players matched by region at signup, always displayed in local time. **Never changed** is the load-bearing half: an alliance that organised its week around Sunday evening should not have that moved by an ops decision.

| | v1 | … | v9 | v10 | v11 |
|---|---|---|---|---|---|
| Contradictions open | 20 | | 1 | 1 | 1 |
| Gaps open | 20 | | 4 | 3 | 2 |

**Every mechanic in the game is now specified.** Two gaps remain: a localization plan and a live-ops tuning pass. Everything else is content authoring.

---

## What changed in v10

**Facility cost curves written.** All six facilities priced, `broodline_economy_model.md` §5 rewritten.

**The flat 35% rate was wrong twice over.** It was applied to five facilities when there are six, and a flat rate assumed a capacity valve and the spine of the whole progression should cost the same. They are now priced by job: Splicing Chamber at 60% of Core because it gates the coverage ceiling that gates everything, Harvest Array at 50% because it is self-funding and a shallow curve would let it run away, Gene Vault at 20% because it is a pressure valve rather than power and charging power prices for a valve turns an inconvenience into a paywall.

**A full Gene Lab is ~1,727,000 shards, not ~1.6M.** Core alone is ~585,000. Completion times move to roughly 27 months for a core player and 15 for an optimiser, up from 24 and 14.

**Timers and shards bind at different points, and the crossover is Core tier 7.** Through tier 6 a player has the shards before they have the hours; by tier 10 a 48-hour timer sits against a cost worth forty-four days of budget and the timer is noise. That is also where skip pricing stops mattering — a full 72-hour skip is 1,800 shards against a 200,000-shard tier.

**The most useful finding is about the payer ceiling.** §5.3 puts an optimiser at Core tier 8 in about six weeks *if shards were the only constraint*. Core tier 8 requires campaign milestone 8, which the campaign places at wave 41 — five months at the fastest plausible pace. **From roughly Core tier 5 onward a heavy spender is milestone-bound rather than shard-bound**, and the gap widens with every tier. The shard curve is a floor on time, not a prediction of it, and the anti-whale structure is stronger than the economy model's own numbers suggested.

| | v1 | v2 | v3 | v4 | v5 | v6 | v7 | v8 | v9 | v10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Contradictions open | 20 | 2 | 2 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |
| Gaps open | 20 | 13 | 9 | 8 | 7 | 6 | 5 | 4 | 4 | 3 |

**Nothing structural or numerical is left.** Three gaps remain: two policy decisions and a live-ops tuning pass. Everything else is content authoring.

---

## What changed in v9

**`broodline_enemy_archetypes.md` rewritten as `broodline_raider_roster.md`.** Eight raiders on four shared bodies, with the art and animation budget, recognition rules, telegraphing and spawn patterns. The unbound-splice fiction, the variants-per-body budget logic, the telegraphing requirement and the raids exclusion were all rescued; the twelve-archetype roster and the armour mapping were not.

**The character art budget now has a number: thirty-six assets.** Twenty-four creature, twelve raider — four raider bodies and eight variant kits. It was the largest uncosted production line item in the project. Two of the four pairings are doing unusual work: **Lifter** produces both Lash and Drift from one mesh by furling or deploying a dorsal membrane, which is two of the most mechanically distinct raiders in the game off one body; and **Brood** splits into thirteen bodies that are all the same Segment mesh at two smaller scales, making the most visually dramatic raider also the cheapest.

**"The mechanic is the silhouette."** Each raider's answering trait has to be readable off its body at thumbnail size — Bulwark's shield is held out in front, separate from the body, so a player who has never opened the Codex understands the shield is the problem. That rule is what makes the counter system teachable by playing rather than only by losing, and it is the strongest argument for spending the art budget where it is spent.

**Telegraphing got sharper than the bible required.** Bible §4.12 asks for composition visible before commitment. The pre-wave screen now also names the answering trait for each raider and **marks the ones the current deployment cannot handle.** Showing a Courser without showing that the player holds no Chill is technically compliant and practically useless.

**Chapter 8's open question is resolved: the Sunder.** A Hauler at 1.5× scale carrying both Plate and Shield, answered by Pierce and Sprint together — the only raider in the game requiring two counters at once. Core tier 12 sits behind wave 60, and a capstone beatable by stacking one species would make the game's longest progression track meaningless. It is a variant rather than a body, it appears in exactly one wave, and the guardrails keeping it singular are explicit.

**The single highest-leverage untested number is Skirmisher's 1.5-second spawn interval.** It determines whether Splash is a counter or a convenience, and it should be tested before any raider stat is touched.

| | v1 | v2 | v3 | v4 | v5 | v6 | v7 | v8 | v9 |
|---|---|---|---|---|---|---|---|---|---|
| Contradictions open | 20 | 2 | 2 | 1 | 1 | 1 | 1 | 1 | 1 |
| Gaps open | 20 | 13 | 9 | 8 | 7 | 6 | 5 | 4 | 4 |

**Every rewrite is done.** Fourteen current companions and no gap that blocks another. The four remaining gaps are a pricing pass, two policy decisions and a tuning pass — none of them structural.

---

## What changed in v8

**`broodline_trait_codex.md` rewritten.** Thirty-four entries — twelve species traits, six Instincts, eight Aberrants and, new, the eight raiders themselves. The disclosure policy, entry schema, behaviour-preview requirement, bottom-sheet placement, progressive surfacing and collection layer were all rescued intact; the fifty-six-trait pool and the fixed-tier ruling were not.

**Three things the rewrite added.**

**The threat board.** Eight raiders and their answering traits side by side on one screen, with the species that carries each. The old version covered traits only, which left a player no way to look up *what answers a Courser* — the question the Codex is most often opened to answer, and the one Wave Defeat raises at the worst possible moment. It is also the cheapest thing in the document and it is where a player reads bible §4.4's species guarantee rather than being told it.

**Raider entries, with per-tier cross-reference.** Each raider card states what each coverage tier of its answer does against it — Chill I slows one Courser, Chill III slows four. That is the single most useful lookup in the app and it existed nowhere.

**"Counters nothing. By design."** Four of twelve traits and all eight Aberrants answer no raider. A player who reads that as an absence concludes the Codex is incomplete or that they have been unlucky. Stating it flatly on the card, next to a concrete effect, turns twelve apparent gaps into twelve real choices. This is the smallest change in the document and probably the most load-bearing for the trait-utility risk.

**The pool got learnable.** Fifty-six entries across four categories and four tiers was a wall that §9 of the old document tried to manage with progressive surfacing. Thirty-four is small enough that a player can plausibly know all of them, and the twelve species traits complete in the first week — which is intentional, because early completion of the tractable part is what teaches a player that the Aberrants are worth chasing.

**Eleven behaviour previews remains the only uncosted line.** Six species Instincts plus five Aberrant Instincts. Down from fourteen, which is what makes it plausible.

| | v1 | v2 | v3 | v4 | v5 | v6 | v7 | v8 |
|---|---|---|---|---|---|---|---|---|
| Contradictions open | 20 | 2 | 2 | 1 | 1 | 1 | 1 | 1 |
| Gaps open | 20 | 13 | 9 | 8 | 7 | 6 | 5 | **4** |

---

## What changed in v7

**`broodline_region_roster.md` rewritten, and all thirty regions authored.** The previous version supplied nine and left twenty-one open, which was the largest content gap in the set. It is closed.

**Eight terrain families, redefined by lane arrangement alone.** Bible §4.2 cut elevation, water and emplacement variety, which left the old families describing modifiers that no longer exist. A family is now four numbers: lane count, arrangement, pocket count, lane length. **Convergence turns out to be the real dial** — three lanes that merge for the last six tiles let one Hollow at range 7 contribute to all three; three parallel lanes do not. That single distinction separates Scree from Delta and is worth more than any modifier the old version proposed.

**Pocket count runs against lane count on purpose.** Delta gives three lanes and four pockets; Defile gives one lane and six. A player on Delta cannot cover everything and has to decide what to give up.

**Region defence got rules for the first time.** Bible §4.8 listed it as a wave type and specified nothing. It now has a cadence that scales with richness — every 5 to 12 hours — a raider pool that widens as richness rises, and a stated loss cost of regeneration timers plus a two-hour harvest interruption and nothing else. That closes most of G14.

**Species distribution checks out.** Sixty weighting slots across six species, range nine to eleven, every species present in every band. Hollow and Pale are the two thinnest in the Inner Reach, which is the right pair — Hollow is Founder 1 and Pale is guaranteed at wave 6, so neither can be missed, and scarcity in the safe band creates a reason to move rather than a risk of lockout.

**One family is flagged as possibly wrong.** Weir — three short lanes with generous pockets — is the only family whose difficulty is about damage rate rather than counter breadth. That makes it interesting and also makes it the one that could undercut the design's central claim that composition beats numbers.

| | v1 | v2 | v3 | v4 | v5 | v6 | v7 |
|---|---|---|---|---|---|---|---|
| Contradictions open | 20 | 2 | 2 | 1 | 1 | 1 | 1 |
| Gaps open | 20 | 13 | 9 | 8 | 7 | 6 | **5** |

---

## What changed in v6

**`broodline_campaign_structure.md` rewritten.** Sixty waves in eight chapters against six species and eight raiders, replacing the sixty-five-wave version written for ten chassis and twelve archetypes. It supplies the three schedules other documents were resolving against, and rescues the mutual staircase, the replay cap and the seasonal rule verbatim.

**The last structural gap is closed.** Everything remaining is content authoring, tuning, or a policy decision.

**Four things it settled that were open.**

**Ark integrity becomes a difficulty dial.** Campaign waves author their own pool, rising from 2 in chapter 1 to 10 in chapter 8. Early waves are crisp puzzles where a single leak matters; late waves are attrition. It costs nothing to build and it is what makes chapter 1 tight without being cruel — and it is what makes the designed loss at wave 6 authorable at all, since one Courser through an integrity-2 wave is a guaranteed loss.

**The wave budget formula did not fund its own first wave.** `broodline_combat_numbers.md` gave Budget(w) = 40 × 1.045^w × L, and a single Skirmisher unit costs 60. Now anchored at **60 × 1.04^(w−1) × L**, so wave 1 at one lane is exactly one Skirmisher unit.

**Founder 1 is a Hollow, and the tutorial parents are a Vetch and an Ember.** Bible §9.5 requires the splice parents to be non-Founder sample stock with the named creature locked out, and bible §10.9 fixes the tutorial splice's output as Cinderplate, a Vetch-bodied Vetch × Ember hybrid. The base-stock document had Founder 1 as a Vetch consumed at beat 6, which violated both. Corrected: the two tutorial parents are unnamed, and the creature the player names is a Hollow — the most distinctive silhouette and two of the eight counters, so it stays useful in month three.

**Breaker is taught last and Skirmisher first, for the same reason.** The combat spec named Skirmisher and Lash the two softest locks — answerable by enough raw output. That is what a player needs in the first hour. Breaker is the hardest lock and arrives at wave 46, by which point Hollow has carried Pierce for six weeks.

| | v1 | v2 | v3 | v4 | v5 | v6 |
|---|---|---|---|---|---|---|
| Contradictions open | 20 | 2 | 2 | 1 | 1 | 1 |
| Gaps open | 20 | 13 | 9 | 8 | 7 | 6 |

---

## What changed in v5

**`broodline_base_stock.md` written.** What a Gen-1 creature is, supply rates and sources by archetype, species distribution, the Founder and campaign-milestone guarantees, a scarcity ratchet that runs for the life of the account, and the Instinct roll weights.

**Two guarantees stopped being intentions and became mechanisms.**

**Constraint 2 is now audited and clean.** Bible §4.4 has always promised that acquiring all six species yields all eight counters and that milestones prevent lockout, but nothing specified the milestones. The mechanism: a Gen-1 creature always carries its species' trait pair — never rolled — so acquiring the species *is* acquiring its counters. Five Founders across days 0–3 deliver seven of eight counters; the sixth species arrives at a campaign milestone.

**The designed first loss now has a raider.** Bible §9.3 requires a player to lose a wave for lack of a trait, early and safely, with the answer available within minutes, and never said which wave. **Pale is deliberately the sixth species and Courser is deliberately the loss** — it ignores Taunt so the Vetch wall does not save them, crosses a lane in fifteen seconds so raw damage does not either, and its answer sits on the one species they do not have. The lesson lands as *"I need a Pale"* rather than *"I got unlucky,"* and the next milestone grants one. That chain ties bible §9.3, §9.4 and §4.4 together and closes the last thing missing from onboarding.

**Litter is confirmed too strong, not too weak.** At six creatures a day against a core player's eleven-and-a-half, one trait slot supplies over half a player's base stock. Recommendation is 12 / 9 / 6 hours rather than 8 / 6 / 4. The trait was originally flagged as possibly worthless; it is the opposite.

**The bodies-only-leave problem is quantified.** Species diversity drains at roughly six body-exits a day as a player optimises. Four uniform wave drops a day — 0.67 of each species — is sufficient to hold all six indefinitely, which is why wave-completion base stock must never scale with any facility, purchase or event.

| | v1 | v2 | v3 | v4 | v5 |
|---|---|---|---|---|---|
| Contradictions open | 20 | 2 | 2 | 1 | 1 |
| Gaps open | 20 | 13 | 9 | 8 | 7 |

---

## What changed in v4

**`broodline_sample_economy.md` written.** Sources and daily rates by archetype, tier mix per source, the 75/25 weighting rule, fusing, the twelve-tier capacity curve, retirement yield, what a splice costs in coverage, the catalyst, the Aberrant sub-roll, and what a sample pull is.

**Three findings that changed other documents.**

**Splices per day was wrong across four files.** Every document reasoned from 15–18 charges a day to 15–18 splices a day. A splice consumes two creatures, so that requires thirty base-stock creatures daily against a roster floor of 20. **Charges are not the constraint on splicing — base stock is**, a core player runs about six splices a day, and mutation cadence moves from every 0.7 days to every 1.9. The economy model and monetization spec are corrected.

**A splice destroys coverage and nothing said so.** Two of the parents' four combat traits do not reach the child, and any coverage fused into them dies with them — a tier-III trait is nine tier-I samples. This is the largest sink in the sample economy, it is why retirement is a real alternative to splicing, and the splice confirmation screen now has to name it before the charge is spent.

**Litter III may be half a player's base stock.** One Gen-1 creature every four hours is six a day against a core player's twelve. The trait was flagged as possibly too weak; from the economy side it looks possibly too strong. Both directions are now on the playtest watchlist.

**Constraint 3 audit corrected.** Holding all eight counters takes six creatures, not four — only Ember and Hollow carry two counters each. The floor of 20 still clears it.

| | v1 | v2 | v3 | v4 |
|---|---|---|---|---|
| Contradictions open | 20 | 2 | 2 | 1 |
| Gaps open | 20 | 13 | 9 | 8 |

---

## What changed in v3

**`broodline_combat_numbers.md` written.** The critical-path document exists. It supplies six species stat profiles under a checkable power budget, all twelve traits with a concrete effect at each of three coverage tiers, six Instincts with numbers, eight raider profiles with the mechanic each counter answers, the generation-to-coverage ceiling, and a wave budget formula with the four-raider-type cap audited against the roster.

**Four gaps close and four rewrites unblock.** G1, G2, G17 and most of G3 are closed. The campaign, region, codex and raider rewrites were all waiting on it.

**Three things it found that the bible did not have.** Ark integrity was a single point — a leaked Skirmisher lost the wave, which made a partial Splash answer worthless; it is now a pool. The sidegrade envelope existed only in the superseded chassis roster and is rescued as a power budget. And Breaker's Plate, as originally conceived, was not a real lock — enough unpierced damage grinds through 600 HP on a 24-tile lane — so Breaker now regenerates. That is flagged as the most fragile number in the design.

| | v1 | v2 | v3 |
|---|---|---|---|
| Contradictions open | 20 | 2 | 2 |
| Gaps open | 20 | 13 | 9 |

---

## 1. Document status after the pass

| Document | Was | Now |
|---|---|---|
| `broodline_bible.md` | Current, internally inconsistent in six places | **Current.** Twelve edits: pack ladder, Marks, never-sold list, Collector scope, Drive scope, relocation model, Ark interception, step-down immunity, campaign terrain, rally, dominance, rating, 3D, Founder skip, Common Vein penalty, companion index |
| `broodline_screen_inventory_v2.md` | Header said 32 screens, tables held 45 | **Current.** 56 screens. Eleven added; two open questions closed |
| `broodline_supersession_map.md` | Covered 9 of 27 files, three stale rows | **Rewritten.** Covers all 27, rename table completed, every unmapped section addressed |
| `README.md` | Indexed 8 files, status stale | **Rewritten.** Indexes all 27 |
| `broodline_economy_model.md` | Second-wave, old vocabulary | **Current companion.** Six facilities, samples, 9% mutation, Gene Lab, revised totals |
| `broodline_collectors_raiding.md` | Second-wave, old vocabulary | **Current companion.** Drive/PvP closed, Apex raidability closed, step-down immunity, counters in raids |
| `broodline_live_ops_events.md` | Second-wave, fragments, 3% | **Current companion.** Samples, Aberrant sub-roll, structured Recipe Share |
| `broodline_monetization.md` | Second-wave, spins and fragments | **Current companion.** Sample pulls, extended never-sold list |
| `broodline_moderation_ugc.md` | Second-wave, rating unresolved | **Current companion.** 12+ settled, sixth UGC surface added, age-gate placement raised |
| `broodline_midgame_arc.md` | Second-wave, Vault vocabulary | **Current companion** |
| `broodline_splice_confirm_spec.md` | Current, unlabelled | **Current companion**, labelled |
| `broodline_combat_numbers.md` | Did not exist | **Current companion.** Written in v3 |
| `broodline_sample_economy.md` | Did not exist | **Current companion.** Written in v4 |
| `broodline_base_stock.md` | Did not exist | **Current companion.** Written in v5 |
| `broodline_campaign_structure.md` | Superseded, rewrite queued | **Current companion.** Rewritten in v6 |
| `broodline_region_roster.md` | Superseded, rewrite queued | **Current companion.** Rewritten in v7, all thirty regions authored |
| `broodline_trait_codex.md` | Superseded, rewrite queued | **Current companion.** Rewritten in v8 |
| `broodline_raider_roster.md` | Did not exist | **Current companion.** Rewritten in v9 from `broodline_enemy_archetypes.md` |
| `broodline_alliance_territory.md` | Superseded original | **Current companion.** Brought current in v11 |
| `broodline_localization.md` | Did not exist | **Current companion.** Written in v12 |
| `broodline_reconciliation.md` | Decision record | Unchanged. Still the record of *why* |
| `broodline_spec_reconciliation.md` | Claimed authority, mostly wrong | **Superseded outright.** Header added; its constants ledger errors enumerated in the map |
| Chassis roster | Second-wave, unmapped | **Superseded.** The sidegrade envelope and never-sold rule are both rescued; little else remains |
| `broodline_enemy_archetypes.md` | Second-wave, unmapped | **Superseded and replaced** by `broodline_raider_roster.md` |
| The nine original specs | Superseded, no headers | **Headers added** |

---

## 2. Contradictions — closed

| # | Topic | Resolution |
|---|---|---|
| C1 | Store pack ladder | Bible §8.3 now carries the monotonic ladder, 101→200 shards/$, six tiers, Double Regen at $9.99. Mythic unlimited-charge pack dropped |
| C2 | Splice Roulette cadence | Always on, featured pool rotates biweekly |
| C3 | Mutation Surge and catalysts | Surge raises the Aberrant sub-roll globally and grants no catalyst. Catalysts come from Apex Vein extraction only |
| C4 | Ark relocation time | Distance model adopted — 25 / 50 / 95 min, with route multipliers ×1.0 / ×1.4 / ×1.7. The flat 3–5 hour crossings are gone |
| C5 | Lane count and terrain | Bible wins: 1–3 lanes, no modifiers. Region roster flagged for rebuild |
| C6 | Collector scope | **General purpose.** A Collector harvests anything outside the Ark's region; Apex additionally requires presence for the window |
| C7 | Drive vs PvP | **Drive touches the Ark only.** Collector classes unlock through campaign milestones and the alliance Convoy branch |
| C8 | What an intercepted Ark loses | Transit time and forfeited harvest. Nothing else — no cargo, roster, resources or facility state |
| C9 | Season length | Four weeks |
| C10 | Screen count | 56, not 32. Nine current, twelve reworked, thirty-five undesigned |
| C11 | Rally | One per wave, no cooldown |
| C12 | Instinct dominance | Dominance applies to combat traits only. The Instinct roll is modified by affinity alone |
| C13 | Species colour | Bible wins; the earlier "no canonical colour" ruling is recorded as reversed |
| C14 | Raid immunity | Steps down: full to day 14, one-loss shield days 15–21, standard from day 22 |
| C15 | Species creature on the paid track | Neither the Season Pass nor any pack grants a creature. Every species carries a counter, so a named body is counter access |
| C16 | Age rating | **12+ with open chat.** The alliance layer cannot run on canned phrases |
| C17 | Campaign terrain | Campaign waves carry their own authored terrain, fixed per wave. Region defence uses the current region |
| C20 | Stale supersession rows | Fixed in the rewrite |

## 3. Contradictions — still open

| # | Topic | Why it is still open |
|---|---|---|
| **C18** | **Vocabulary in the five queued documents** | A rename pass is not enough — chassis→species is a model change, not a word change. Closes with the rewrites |
| ~~C19a~~ | ~~Geneticist Tier 6 perk~~ | **Closed in v4.** A sample pull is one Splice Roulette spin, granted rather than purchased — same odds table, same unweighted roll, same shared pity counter |
| ~~C19b~~ | ~~Charge cap tiers~~ | **Closed in the final pass.** +1 at tiers 2, 4, 6, 8 and 10, shown in the bible and monetization tier tables |

---

## 4. Gaps — closed

| # | Gap | Closed by |
|---|---|---|
| **G12** | Moderation and UGC | `broodline_moderation_ugc.md` promoted to current companion. Rating settled, sixth surface counted, four required mechanisms specified, screens added to the inventory |
| **G16** | 2D or 3D | 3D, recorded in bible §10.3 with reasoning that survives the reduction to 24 assets. Rig one species as a pipeline proof |
| **G18** | Screens absent from the inventory | Eleven added: Transit Board, Raid Party Select, Raid Defence Alert, Region Defence, Vault Calibration, Codex Bottom Sheet, Account Creation / Age Gate, Player Profile, Report, Block List, Escort Tutorial |
| **G20** | Deprecation headers | All sixteen superseded documents carry one. All seven companions carry a current header |
| — | Document index | README rewritten. Every file has a stated status and an owner role |

**Partly closed:**

| # | Gap | What landed | What is left |
|---|---|---|---|
| **G8** | Raid ruleset | The raiding document is current and the bible now points at it. Marks are in the currency table; cargo insurance, extra raid attempts and Marks are in the never-sold list | Alliance convoy staging still undesigned |
| **G11** | Mid-game arc | Document is current; the step-down immunity is adopted into the bible | Beat days assume an average-pace player and may need to be milestone-anchored |

---

## 5. Gaps — still open

Ordered by what unblocks the most.

| # | Gap | Blocks | Note |
|---|---|---|---|

---

## 6. Work list

**Done in v13:** W15 live-ops tuning. **All work items are complete.**

**Done in v12:** W16 localization plan.

**Done in v11:** W14 convoy staging and the weekly tick.

**Done in v10:** W8 facility cost curves.

**Done in v9:** W11 raider roster rewrite.

**Done in v8:** W10 Trait Codex rewrite.

**Done in v7:** W9 region roster rewrite.

**Done in v6:** W6 campaign structure rewrite.

**Done in v5:** W12 base stock supply.

**Done in v4:** W7 sample economy.

**Done in v3:** W5 combat numbers spec.

**Done in the v2 pass:** W1 Collector scope · W2 Drive line · W3 rating · W4 bible §8 · W11 moderation · W13 screen inventory · W17 3D · W18 supersession map · W19 README and headers · W20 small bible edits. Plus the six companion rename-and-correct passes, which were not separately listed in v1.

**Remaining**, in dependency order. Kind: **D** decide · **W** write · **E** edit.

| # | Item | Kind | Closes | Depends on |
|---|---|---|---|---|
| **W18** | **Supersession map v3** — all rewrites have landed | E | — | **ready** |
| **W19** | **Content authoring.** Sixty campaign waves against the chapter table, and the region adjacency graph the Route Plotter needs | W | — | — |

**Nothing is left on this list.** The remaining work items are content authoring and procurement, and they live in `broodline_build_order.md`.
