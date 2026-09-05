# Broodline — Monetization

*Design spec, offer structure. Supersedes `splice_monetization_spec`.*

---

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Last brought into line with the bible: 4 Sep 2026.


## 1. What This Document Is Now

The original monetization spec was written first and has been overtaken. The economy model owns rates and shard income. The live-ops spec owns the five events. Bible §7 owns the primary sink. The raiding spec owns Marks.

**This document owns offers**: what is sold, at what price, in what shape, and — most importantly — what is never sold at all.

The strategic position is unchanged and still correct: **dual-path currencies, a status ladder, rotating event offers, and a trial-before-paywall hook.** Money buys rate, never reach.

---

## 2. Currencies

| Currency | Free path | Paid path | Spent on |
|---|---|---|---|
| **Splice Charges** | Regen, login, rewarded ads, Gene Lab | Packs, Season Pass | Splicing |
| **Gene Shards** | Harvest, waves, events, raids | Direct purchase | Gene Lab, timers, Roulette, cosmetics |
| **Geneticist XP** | Login streak, events, achievements | Included in every pack | Geneticist Tier |
| **Raid Marks** | Successful attacks | **Never** | Marks Shop |
| **Defense Marks** | Successful defenses | **Never** | Marks Shop |

Two Marks currencies exist and neither is purchasable at any price. That is a structural guarantee, not a launch decision — see §8.

**Vocabulary note:** the retired Breeder Tier, Breeder XP, Breeder Pass, and Breeder's Cup are now Geneticist Tier, Geneticist XP, Season Pass, and Apex Cup throughout.

---

## 3. Splice Charges

- Cap **5**, regen **1 per 25 minutes**
- Cap rises with Geneticist Tier: +1 per three tiers, maximum 10
- Rewarded video: +1 charge, three per day
- Daily login streak: escalating grants at days 3, 7, 14, 30

**Correcting the original spec's own arithmetic.** It reasoned from a player earning roughly five charges a day. At a 25-minute regen and a cap of 5, a refill from empty takes 2h05m, so a three-session player captures **15 charges daily**, plus three from ads. The real figure is 15–18.

This matters beyond bookkeeping: an early spec worried a 3% mutation rate was too rare to sustain the loop's excitement, and that worry was calculated from the wrong charge number. Bible §2.3 now sets ~9% with an Aberrant sub-roll inside it.

**Charges never block play.** At zero charges a player can replay campaign waves, harvest, defend regions, dispatch collectors, raid, and claim events. This is the oldest guardrail in the model and it holds.

---

## 4. Geneticist Tier

Twelve tiers on cumulative Geneticist XP. Permanent, account-wide, and deliberately **convenience rather than power**.

| Tier | Perk |
|---|---|
| 2 | +1 charge cap |
| 4 | 10% faster charge regen |
| 6 | One free sample pull daily |
| 8 | **Second simultaneous splice queue** |
| 10 | Cosmetic hybrid aura |
| 12 | Unique title, +15% shard bonus on purchases |

Tier 8 is the strongest perk in the game and it is still only throughput. Once splicing is destructive, the real constraint is how many splices you can run, not how strong any one creature is — so a second queue is enormously valuable without touching combat power at all. That is the shape every perk on this ladder should have.

---

## 5. Store

### Fixed packs

| Pack | Price | Shards | Charges | XP | Extra |
|---|---|---|---|---|---|
| Starter Splice | $0.99 | 100 | 5 | 500 | — |
| Lab Bundle | $4.99 | 600 | 15 | 1,500 | 1 sample pull |
| Lab Expansion | $9.99 | 1,400 | 40 | 5,000 | 3 sample pulls |
| Geneticist's Vault | $19.99 | 3,200 | 100 | 15,000 | 8 sample pulls, skin |
| Warden's Cache | $49.99 | 9,000 | 250 | 40,000 | 20 sample pulls, skin |
| Ark Reserve | $99.99 | 20,000 | 500 | 90,000 | 50 sample pulls, exclusive skin |

**Shard value per dollar rises monotonically: 101, 120, 140, 160, 180, 200.** The original ladder did not — $4.99 delivered worse value than $0.99, and a $14.99 pack contained more shards than the $19.99 tier. Players run this arithmetic and post it, and a store that punishes buying the middle tier reads as manipulative rather than merely mispriced.

**First purchase of any pack: 2× contents, one time.** Retained from the original spec; it remains the best-evidenced conversion mechanic available.

### Custom Chest

Pick three of six reward slots — charges, shards, XP, sample pulls, cosmetic, speed-up — at $1.99, $4.99, or $9.99. Whiteout's highest-converting mechanic, and the reason is straightforward: players stop paying for contents they don't want.

### Double Regen

**$9.99, permanent.** Regen improves from 25 to 12.5 minutes. This is the conversion target of the trial in §6 and the closest thing to an anchor purchase in the game.

The original spec's "Mythic Lab Access — unlimited charges for 48 hours" is dropped. Unlimited is self-limiting in practice, since splicing consumes parents and a player runs out of creatures before charges, but selling an unlimited anything invites the reading that throughput is purchasable without bound.

### Season Pass

Four-week cycles. Free track carries modest charges, shards, and cosmetic unlocks. Paid track at $9.99 carries two to three times the same currencies, plus exclusive skins and XP boosts.

**The paid track is more of the same, faster — never exclusive power.** It carries no creatures and no traits at all: charges, shards, XP, samples, cosmetics. Every species carries a counter, so granting a named species creature would be granting counter access by another route. A lapsed season costs a player nothing they cannot earn later.

---

## 6. The Trial Hook

Unchanged from the original spec, because it is the single highest-leverage tactic in the model.

1. **First charge wall**, around session 2–3, grants an automatic **24-hour Double Regen trial**. Free, unprompted, nothing asked.
2. **At expiry**, offer to extend — Gene Shards for two more days, or the permanent unlock for $9.99.

Letting a player feel the upgrade before asking for money converts far better than a cold paywall. The FTUE spec protects this by forbidding any purchase prompt in session one; putting a store banner in front of a new player spends the leverage for nothing.

---

## 7. Event Offers

Five recurring events, each shipping a themed offer. Mechanics are specced in the live-ops document; the offers are:

| Event | Offer |
|---|---|
| Gene Lab (weekly) | Charge and shard bundle |
| Splice Roulette (always on) | Discounted spin packs |
| Apex Cup (monthly) | Cosmetic flair and charges |
| Mutation Surge (quarterly) | **Charges only** |
| Recipe Share (always on) | Cosmetic recipe card frames |

Mutation Surge is the one to watch. The original spec described it as a limited trait pack, which would have sold event-exclusive traits — breaking the guarantee that access is never purchasable. The Surge raises the Aberrant sub-roll for everyone and sells attempts at those better odds. Nothing else, and no catalysts.

**Never more than two timed events live at once.** Burnout costs more LTV than a missed sale.

---

## 8. What Is Never Sold

Scattered across eight specs, consolidated here. Each is a structural guarantee, not a launch-window restraint.

| Never sold | Why | Source |
|---|---|---|
| **Traits of any kind** | Access is bred, never bought — the sole thing keeping hard counters from being a paywall | Bible §1.7, §8.6 |
| **Aberrant traits and catalysts** | Mutation-only is what makes "money buys attempts" true | Bible §1.6 |
| **Campaign milestone skips** | The only thing stopping money from skipping progression | Bible §7.3 |
| **Raid or Defense Marks** | Keeps PvP an effort contest | Raiding |
| **Cargo insurance** | Pay-to-not-lose; advertises itself to every non-payer who was just raided | Raiding |
| **Extra raid attempts** | 2/day is an economy constant | Raiding, Economy |
| **Hold, or Hold acceleration** | Converts territory from effort to spending | Alliance |
| **Additional Stake slots** | The cap of five is a competitive-health mechanism | Alliance |
| **Non-ally penalty increases** | −40% is fixed; higher closes the map | Alliance |
| **Additional concurrent build slots** | Core tier 8 is the intended gate | Bible §7.4 |
| **Event-exclusive traits or species** | Everything must remain reachable afterward | Live-ops |
| **Trait Codex access, in any form** | It is a disclosure mechanism | Codex |
| **Creatures of a named species** | Every species carries a counter, so a named body is counter access | Bible §1.2, §8.6 |
| **Regions** | Money buys volume, never a specific outcome | Bible §5 |

**Cargo insurance deserves the most vigilance.** It is the most obviously profitable offer in the entire design and the most destructive. Every raid a non-payer loses becomes an advertisement for a product they declined, which is precisely the resentment mechanic that collapses review scores.

---

## 9. The Three Structural Guarantees

Everything above rests on three load-bearing facts:

1. **Campaign milestones gate Core tiers.** A heavy spender clears shard and timer costs and stops at the same wall as everyone else.
2. **Trait access is bred, never bought.** Money buys coverage, which is rate. Aberrants have no price at all.
3. **Marks are unpurchasable.** PvP standing cannot be bought.

If any one of these is sold around, the model becomes pay-to-win in a single patch and does not recover. They should be treated as immutable rather than as defaults.

---

## 10. Advertising

Rewarded video only. No interstitials, no forced views, no ads in combat.

- +1 charge, three per day
- Shard grants tied to daily objectives
- Introduced in session two, framed as a free lever rather than a paywall alternative

Whiteout runs zero ads. Rewarded-only is a deliberate departure: it gives non-payers a genuine third path alongside time and money, at the cost of some ARPDAU purity.

---

## 11. Guardrails

- Charges never block progress; something is always playable at zero
- Shard value per dollar rises monotonically across the ladder
- Geneticist Tier perks stay convenience and cosmetic at every rung
- Season Pass paid track is quantity, never exclusivity
- No purchase prompt in session one
- Odds displayed before any paid randomised action
- Nothing in §8 is ever sold, at any price, in any bundle, during any event

---

## 12. Open Questions

1. **The ARPDAU benchmarks in the original spec** — Whiteout at $1.21, Kingshot at $1.45 — predate this work and should be re-validated before they anchor any revenue model.
2. **Is $9.99 right for permanent Double Regen?** It's the anchor purchase and it's priced by feel rather than against measured willingness to pay.
3. **Does the $99.99 tier belong at launch?** It sets an expectation about the game's spending ceiling on day one, and the audience it serves may not exist until month six.
4. **Rewarded ad caps at three per day** may be low. Higher caps serve non-payers better and dilute charge pack value.
5. **Regional pricing and market-specific ladders** are unaddressed, and depend on the localization plan that does not yet exist.

---

*Replaces `splice_monetization_spec`. Offer structure only — rates and prices live in `broodline_economy_model.md`, event mechanics in `broodline_live_ops_events.md`.*
