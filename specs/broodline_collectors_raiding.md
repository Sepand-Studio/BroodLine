---
status: current
folder: 01-companions
supersedes: 99-archive/broodline_collectors_raiding_era2.md
verified-against: broodline_bible.md (2026-09-05)
note: >
  The asynchronous PvP layer, reconciled to bible §4.9–4.11, §5.5–5.6, §6.6–6.7
  and §7.7, and to the region roster's gates and hop times. Exposure is now
  per-gate and fixed, which resolves the §7.2/§7.7 Drive contradiction. Raises
  four bible amendments (§14). Cargo and Marks values are starting points
  pending the economy model.
---

# Broodline — Collectors & Raiding

*Design spec, the asynchronous PvP layer. Companion to bible §4.9 and §5.6.*

---

## 1. Founding principle

Raiding is the only system in Broodline where one player's action costs another player something. The design is shaped almost entirely around containing that.

**Raids take cargo in transit and nothing else.** Never the Ark, never the roster, never stored resources, never Hold (bible §5.6, §6.8). A player who loses every raid for a month has a slower economy and an intact game.

---

## 2. What Collectors are for

The Ark harvests its own region passively, and adjacent regions at a reduced rate (§5.5). **Collectors are how a player harvests anything further away.** Apex Veins additionally require a Collector physically present for the extraction (§5.5); everything else is optional reach.

| Option | Cost | Risk |
|---|---|---|
| **Relocate the Ark** | Slow, commits you, changes your lane count | None — see §3 |
| **Dispatch a Collector** | Keeps you where you are, ties up escort creatures | Interception at every gate on the route |

A player who has found a lane count they can hold stays put and sends Collectors; a player chasing yield moves. The map's "there's a better node over there" has two answers, and neither is strictly better.

---

## 3. The Ark is never a raid target

Bible §5.2 lists an intercept-risk column on Ark relocation routes. Bible §5.6 and §6.8 say raids never touch the Ark. These cannot both stand, and the guardrail wins: **nothing aboard a relocating Ark is raidable.** Stored resources are not raidable, harvest in progress is forfeited on packing (§5.1), and the roster is never at stake.

The three route options in §5.2 — direct, alliance corridor, night move — are therefore **Collector** routes. Ark relocation takes the shortest path and its only variable is time, scaled by Drive. This is a bible amendment; see §14.

---

## 4. Collector classes

| Class | Cargo | Escort slots | Unlock |
|---|---|---|---|
| **Scout** | 500 | 1 | Default |
| **Hauler** | 2,000 | 2 | Drive tier 3 |
| **Deep Hauler** | 5,000 | 3 | Drive tier 7 |
| **Convoy Rig** | 12,000 | 5 | Alliance tech, Convoy branch (§6.7) |

Cargo figures are starting values against the economy model. A Deep Hauler at 5,000 is roughly one Apex extraction, so the top solo class matches the top solo node. The Convoy Rig exceeds any one player's realistic haul and only pays off when allies ride escort (§5.6).

**Class does not change transit speed.** All Collectors move at the hop times in the region roster (35 min per ring hop, 1h10 per gate at base). What a bigger class buys is more cargo per run and more escort slots; what it costs is more cargo at risk per interception and more creatures tied up. That is the decision worth having. The older speed/capacity inversion is gone because speed no longer affects exposure (§6).

Drive tiers cannot exceed Core (§7.3), so Hauler lands around wave 10 and Deep Hauler around wave 30 of the campaign.

---

## 5. Escorts

Creatures assigned as escorts are **unavailable for anything else while in transit** — no defence, no campaign, no garrison (§5.6). Same cost structure as garrisoning, for the same reason.

- Escorts that fall in a raid enter regeneration, 20–60 minutes (§4.11). Never lost.
- Escorts cannot be recalled mid-route.
- Allies may fill a Collector's empty escort slots with their own creatures. Their creatures are tied up the same way.
- Escort composition is the defender's entire input into a raid. Choose them as a tower-defence loadout, because that is what they are, and choose against the counter table — an escort line carrying no Pierce loses to a raid party with a Breaker-role creature regardless of power.

---

## 6. Exposure

**A convoy is raidable only while inside a gate region that is neither its origin nor its destination, for a fixed 20-minute window per gate.**

Everything else on the route is immune. This replaces the older "middle 60% of the route" rule, and it does four jobs at once:

- **Gates are the chokepoints.** The region roster built them for exactly this. Raiders watch eight gates, not thirty regions, and a convoy hauling Apex cargo home from the Outer Reach crosses two.
- **Departure and approach are immune** by construction. No sniping a known Ark's dispatches; no losing a convoy on the doorstep.
- **Drive tier cannot shorten exposure.** Bible §7.7 forbids facility tier influencing interception, while §7.2 has Drive scaling the exposure window. A fixed window per gate satisfies §7.7: Drive makes the run faster, and the raidable minutes stay the same. Bible amendment, §14.
- **Within-band runs are safe** unless they pass through a third gate region. A new player working the Inner Reach from Holdfast is never exposed, which is why day-14 immunity can be shorter than the older design assumed (§11).

**Route options** (bible §5.2), applied to Collectors:

| Route | Effect |
|---|---|
| **Direct** | Shortest path, full 20-minute window at each gate |
| **Alliance corridor** | Path through allied-controlled regions where possible; **an allied-controlled gate halves the window to 10 minutes.** This is the §6.6 interception advantage, made concrete. ×1.45 time. |
| **Night move** | 120 Gene Shards; no exposure at all. ×1.7 time. |

The Route Plotter shows each gate on the route, its controller and its window, so a player who loses a convoy in a hostile gate made a choice rather than suffered an accident.

---

## 7. Finding a target

Raiders browse a **Transit Board** listing convoys currently inside a gate's exposure window, within three regions of the raider's Ark.

Each entry shows the **cargo band** — Light, Heavy or Apex — the escort count and the minutes remaining. It shows no cargo number and nothing about the escorts' traits, species or generation. The attacker commits on incomplete information, which is what stops raiding collapsing into arbitrage against the weakest visible target. A paid **route obfuscation** hides the cargo band for one run (§12).

**Matchmaking band:** only convoys from players within **±2 Core tiers** are listed. Enforced server-side, not as a filter.

**Cooldowns:**

- **2 raids per player per day.** Raiding is a supplementary pressure system. If it competed with splicing for session time it would pull players away from what the game is about.
- **90 minutes** between raids.
- **One raid per target per 24 hours.** Non-negotiable. Without it one motivated player makes another player's week miserable, and that player leaves.

---

## 8. The engagement

Bible §4.9: the engine runs with roles inverted.

- The **raid board** is one short lane with defender pockets beside it, one pocket per escort slot of the convoy's class. No terrain modifiers; the gate's look is cosmetic (§4.2).
- The **defender's escorts** are the towers. The **attacker's three-creature party** is the wave.
- Both sides use full loadouts including Instinct. A Skittish attacker really will retreat from its own raid.
- **Target win rate: neither side above 55%** at equal investment. If either drifts past it in soft launch, escort slots per class are the cleanest lever.

---

## 9. Loss caps

The numbers that decide whether day 14 is a churn cliff.

- A successful raid takes **40% of cargo, never more.** The defender keeps 60%.
- The attacker receives **50% of the take.** The rest is destroyed.
- A failed raid takes nothing; the attacker's party enters regeneration.

The destruction split makes raiding a net shard sink rather than a transfer, so the economy does not inflate with raid volume, and it makes the attacker's gain visibly smaller than the defender's loss — nobody can farm another player efficiently.

**Two unrelated 40% constants.** The non-ally harvest penalty (§6.8) is also 40%. They are tuned independently and must carry distinct names in implementation: `raid_cargo_loss_cap` and `nonally_harvest_penalty`.

---

## 10. Being raided

**The 90-second alert.** A push notification opens a countdown. A defender who opens the app in time places escorts and plays the defence live; otherwise it auto-resolves.

**Auto-resolve is the identical engine** (§4.10) and always produces a replay. "You lost 40% of a convoy" is an insult. "Your Overwatch escort sat at the near pocket where its range did nothing — watch" is a lesson. The replay viewer ships at launch.

**Losing still pays.** A defender who loses receives Marks at 30% of a successful defence. Being raided is never a pure subtraction.

**Alliance rally.** The alert posts to alliance chat. Allies cannot intervene mechanically — that would make raids unwinnable against organised alliances — but the social moment is worth having, and an ally already riding escort is in the fight.

---

## 11. Protection

- **New-player immunity: 14 days, then a one-week step-down.** Days 1–14: not listed on any Transit Board. Days 15–21: listed, but the loss cap ramps 10% → 20% → 30% across the week and the loss shield triggers on a single loss. Day 22: full exposure. Bible §5.6 and §9.6 end immunity abruptly at day 14; the step-down is a bible amendment (§14). Expiry of full immunity is still announced as an event, paired with the escort tutorial, never a silent flag flip.
- **Loss shield:** two losses within 24 hours trigger **8 hours** of immunity. A bad night cannot become a bad week.
- **Revenge token:** a loss grants a 24-hour token permitting one raid on that attacker, ignoring the per-target cooldown. Converts a loss into agency and makes raiding a large convoy a decision with consequences.
- **Apex extraction is not a raid target.** A Collector parked at an Apex Vein for 48–72 hours is stationary, not in transit; §1 applies. Contest over the Vein itself happens through presence split (§5.5) and Stake Assault (§6.4). The return leg is raidable at its gates like any other run.
- **Offline players are raidable.** This is an async game. Every mechanic above exists to keep offline loss bounded and legible.

---

## 12. Marks

One currency, **Marks**, earned on both sides of a raid and **never purchasable at any price.**

| Outcome | Marks |
|---|---|
| Successful raid | Full |
| Failed raid | 20% |
| Successful defence | Full |
| Failed defence | 30% |

The older design split this into Raid Marks and Defence Marks. One currency is enough: the shop is the same, and a player who mostly defends should be able to buy the same convoy skin as a player who mostly raids.

**Marks Shop** — convenience and cosmetics only, per §8.6:

- One-run escort slot expansion (+1, within class maximum +1)
- Route obfuscation, one run
- Regeneration skips
- Convoy skins, raid banners, defence record flair

Making Marks unpurchasable is the structural guarantee that raiding stays an effort contest. Marks are a fourth currency and need adding to bible §8.2 (§14).

---

## 13. Monetization

**Safe** (bible §5.9, §8.6): Collector skins — convoys are visible on the map; Scout Reports; regeneration skips for Gene Shards; the night-move route fee; defence record flair.

**Never ship:**

- Purchasable Marks or any Marks-equivalent bundle
- Raid attempts beyond the daily cap
- **Cargo insurance** or any paid reduction of the 40% cap. The most tempting offer in the game and pure pay-to-not-lose: it converts every raid a non-payer suffers into an advertisement for a product they declined.
- Escort slots beyond class capacity for money
- Any Drive or Convoy-tech effect on the exposure window

---

## 14. Bible amendments required

Four rulings in this document change the bible. Each needs a line in the reconciliation register.

| § | Current bible text | Ruling here | Why |
|---|---|---|---|
| **5.2** | Ark relocation routes carry intercept risk (62% / 14% / 0%) | Routes and risk are Collector properties; the Ark is never a target | §5.6 and §6.8 already forbid it |
| **7.2** | Drive governs "transit exposure window" | Drive governs transit *time* only; exposure is fixed per gate | §7.7 forbids facility tier influencing interception |
| **5.6, 9.6** | Immunity ends at day 14 | Full immunity to day 14, step-down to day 22 | Day 14 is the churn spike §9.8 flags; an abrupt flip is the worst shape for it |
| **8.2** | Three currencies | Add Marks, non-purchasable | Raid rewards need a currency money cannot reach |

---

## 15. Economy check

The era-2 economy model budgets raid income at 200/day for a core player and 400/day for an optimiser. A Hauler at 2,000, 40% taken, 50% to the attacker, yields 400 per success; at two raids a day and a 50% win rate that is ~400/day. The 6× spread ceiling holds only because the cap is 2 — a cap of 3 breaks it. If raiding must feel more frequent, lower the attacker share, never raise the cap.

These figures are inherited from an unreconciled document and must be re-checked when the economy model is promoted.

---

## 16. Guardrails

- Raids take cargo in transit only — never Ark, roster, stored resources or Hold
- The Ark is never a raid target in any state
- Exposure is 20 minutes per non-terminal gate, fixed; no facility, class or purchase shortens it
- Hard cap of 40% cargo loss; 50% of the take destroyed
- One raid per target per 24 hours; 2 raids per player per day
- ±2 Core tier matchmaking, server-side
- Loss shield after two defeats in 24 hours; one defeat during the step-down week
- No creature is ever lost; escorts and parties regenerate
- Marks are never purchasable
- Auto-resolve is the live engine and always produces a replay

---

## 17. Open questions

1. **Is 2 raids a day enough raider identity?** Correct for the economy, possibly wrong for the fantasy. If playtest says so, the answer is more Marks Shop identity, not a higher cap.
2. **Are eight Mid gates too porous** for raiding to feel focused? The region roster names which two to close if so.
3. **Convoy Rig throughput.** 12,000 cargo with allies riding escort may make the Outer Reach an alliance-only economy. Watch the solo Deep Hauler's share of Outer cargo in soft launch; below a third, cut the Rig to 8,000.
4. **Should the step-down also raise Marks on a lost defence** for days 15–21? Leaning yes — 50% instead of 30% — so the first real losses feel like tuition, not tax.
