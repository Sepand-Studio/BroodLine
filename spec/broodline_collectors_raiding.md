# Broodline — Collectors & Raiding

*Design spec, the asynchronous PvP layer*

---

## 1. Core Concept

Four documents cite this system by name. The node spec requires a Collector present at Apex Veins. The combat spec inverts its engine for raids. The alliance spec draws a hard line between raiding and territory. The FTUE schedules raid exposure at day 14. None of them define what a Collector is or how a raid resolves.

Raiding is the **only system in Broodline where one player's action costs another player something.** That makes it the highest-risk system in the set, and the design is shaped almost entirely around containing that risk.

**Founding principle:** raids take **cargo in transit** and nothing else. Never the Ark, never the roster, never stored resources, never Hold. A player who loses every raid for a month has a slower economy and an intact game.

---

## 2. What Collectors Are For

Your Ark harvests its **home region** passively. Collectors extend that reach.

This is the load-bearing decision in the document, and it's a change in emphasis from the node spec, which treats Collectors as an Apex-only requirement. Apex Veins are rare; if they were the only source of convoys, raiding would have almost no targets and the whole Marks economy would starve.

Instead: **a Collector is how you harvest anything outside your current region.** That gives the map a second answer to "there's a better node over there."

| Option | Cost | Risk |
|---|---|---|
| **Relocate the Ark** | Slow, commits you, changes your combat terrain | Transit exposure once |
| **Dispatch a Collector** | Fast, keeps you where you are, ties up escorts | Interception on every run |

Both are legitimate. A player who has found good defensive terrain stays put and sends Collectors; a player chasing yield moves. The tension the node spec is built around now has two dials instead of one.

Apex Veins retain their special rule: a Collector must be **physically present for the duration**, not just running a delivery. That's what makes Apex windows the game's peak-contest moment.

---

## 3. Collector Classes

| Class | Cargo | Escorts | Transit speed | Unlock |
|---|---|---|---|---|
| **Scout Collector** | 500 | 1 | 1.5× | Default |
| **Hauler** | 2,000 | 2 | 1.0× | Vault Drive T3 |
| **Deep Hauler** | 5,000 | 3 | 0.7× | Vault Drive T7 |
| **Convoy Rig** | 12,000 | 5 | 0.6× | Alliance tech, Convoy branch |

Cargo figures are set against the economy model: a Deep Hauler at 5,000 is roughly one full Apex Vein capture, so the top solo class matches the top solo node. The Convoy Rig exceeds any one player's realistic haul, which is the point — it only pays off when an alliance stages a multi-member run from controlled territory.

The speed/capacity inversion matters. Big cargo means long exposure, so the largest hauls are the most raidable. A player choosing Deep Hauler over two Scout runs is accepting risk for efficiency, which is exactly the decision worth having.

---

## 4. Escorts

Creatures assigned as escorts are **unavailable for anything else while in transit** — no defense, no campaign, no garrison. Same cost structure as garrisoning, for the same reason: an investment that costs nothing isn't a decision.

- Escorts that fall in a raid enter **regeneration** normally, 20–60 minutes. Never lost.
- Escorts can't be recalled mid-route.
- Escort composition is the defender's entire input into a raid. Choose them like a tower-defense loadout, because that is what they are.

---

## 5. Routes & Exposure

A route is drawn across region segments. Transit time scales with distance and collector speed.

**The exposure window is the middle 60% of the route.** The first 20% and last 20% are immune.

Departure immunity stops a raider camping a known Ark and sniping every dispatch on launch. Approach immunity means a convoy that has survived most of the way home isn't lost on the doorstep, which is the single most enraging possible outcome. A 40-minute route is exposed for 24 minutes.

**Route options:**
- **Direct** — shortest, fully exposed through neutral regions
- **Allied** — longer, but interception effectiveness drops 50% in alliance-controlled regions, per the alliance spec's stated benefit
- **Staged** — Convoy Rig only, departs from controlled territory

The Route Plotter shows interception risk per segment, so a player who loses a convoy on the direct route through three contested regions made a choice rather than suffered an accident.

---

## 6. Finding a Target

Raiders browse a **Transit Board** listing in-transit convoys within three regions.

Each entry shows estimated cargo value, escort count, and time remaining in the exposure window. It does **not** show escort creature stats, traits, or generation. The attacker commits on incomplete information, which is what stops raiding from collapsing into risk-free arbitrage against the weakest visible target.

**Matchmaking band:** only convoys from players within **±2 Vault Core tiers** appear. A Core T10 player never sees a Core T3 player's convoy. This is the primary anti-predation mechanism and it should be enforced server-side, not as a UI filter.

**Cooldowns:**
- **2 raids per player per day.** Deliberately low. Raiding is a supplementary pressure system, not a primary loop — if it competed with splicing for session time, it would be pulling players away from the thing the game is actually about.
- **90 minutes** between raids
- **One raid per target per 24 hours.** Non-negotiable. Without it, one motivated player can make another player's week miserable, and that player leaves.

---

## 7. The Engagement

Per the combat spec, the engine runs with roles inverted.

- The **defender's escorts** are placed as towers along a short lane derived from the ambush terrain
- The **attacker's three-creature raid party** is the incoming wave
- Attacker creatures use their full loadout **including Instinct** — a Skittish attacker really will retreat, and that is a genuine drawback the attacker must plan around
- Terrain is generated from the segment where interception occurred, so raiding through mountains differs from raiding across open ground

**Target win rate: neither side above 55% at equal investment.** Defenders hold terrain and emplacements; attackers choose the moment and the target. If either side drifts past 55% in soft launch, escort slot counts are the cleanest lever.

---

## 8. Loss Caps

The numbers that decide whether day 14 is a churn cliff.

- A successful raid takes **40% of cargo. Never more.** The defender always keeps 60%.
- The attacker receives **50% of what was taken.** The remaining 50% is destroyed.
- A failed raid takes **nothing**, and the attacker's party enters regeneration.

The destruction split is deliberate and does two jobs. It makes raiding a **net shard sink** rather than a pure transfer, which keeps the economy from inflating as raid volume grows. And it means the attacker's gain is visibly smaller than the defender's loss, which is the honest shape of the transaction — nobody should be able to farm another player efficiently.

**Note on the −40% collision.** The alliance spec's non-ally harvest penalty is also −40%. These are unrelated constants that happen to share a value, and they will be tuned independently. They should be given distinct names in implementation before someone changes one and moves both.

---

## 9. Being Raided

The defender experience is where this system lives or dies.

**The 90-second alert.** A push notification opens a countdown. If the defender opens the app in time, they take manual control of escort placement and play the defense live. Otherwise it auto-resolves.

**Auto-resolve is the identical engine**, per the combat spec — a full simulation, never a stat roll. It produces a **replay**, and the replay is what makes losing survivable. "You lost 40% of a convoy" is an insult. "Your Overwatch escort was placed at a chokepoint where its range bonus did nothing, watch it happen" is a lesson.

**Losing still pays.** A defender who loses a raid receives **Defense Marks** anyway, at roughly 30% of a successful defense. This is a small mechanic doing enormous retention work: it means being raided is never a pure subtraction, and it directly targets the day-14 churn spike the FTUE spec flags as a metric to watch.

**Alliance rally.** The alert broadcasts to alliance chat. Alliance members can't intervene mechanically — that would make raids unwinnable against organised alliances — but the social moment is worth having.

---

## 10. Protection Systems

- **New-player immunity: 14 days** from account creation, per the FTUE drip schedule. Expiry is announced as an event with advance warning, paired with an escort tutorial. Never a silent flag flip.
- **Loss shield:** losing two raids within 24 hours triggers an automatic **8-hour immunity**. The most important line in this document. A bad night can't become a bad week.
- **Revenge token:** losing a raid grants a 24-hour token permitting one raid on that attacker, ignoring the per-target cooldown. Converts a loss into agency, and makes raiding a large convoy a decision with consequences.
- **Offline players are raidable.** This is an async game and pretending otherwise would break it — but every mechanic above exists to make offline loss bounded and legible.

---

## 11. Marks

Two currencies, both **non-purchasable at any price.**

| Currency | Earned by |
|---|---|
| **Raid Marks** | Successful attack; 20% consolation on failure |
| **Defense Marks** | Successful defense; 30% on a loss |

Marks Shop contents — convenience and cosmetics only, per the standing guardrail:

- Temporary escort slot expansion, one run
- Transit speed boosts
- **Route obfuscation** — hides cargo value on the Transit Board for one run
- Regeneration skips
- Convoy skins, raid banners, defense record flair

Making Marks unpurchasable is the structural guarantee that raiding stays an effort contest. The moment PvP currency has a dollar price, every raid becomes a spending comparison and the mode acquires a pay-to-win reputation it cannot shed.

---

## 12. Guardrails

- Raids take cargo only — never Ark, roster, stored resources, or Hold
- No creature is ever lost; escorts and raid parties regenerate
- Hard cap of 40% cargo loss, with 50% of the take destroyed
- One raid per target per 24 hours, 2 raids per player per day
- ±2 Vault Core tier matchmaking band, enforced server-side
- Loss shield after two defeats in 24 hours
- Marks are never purchasable
- Auto-resolve uses the live engine and always produces a replay

---

## 13. Monetization

**Safe:**
- Collector cosmetic skins — convoys are visible on the map to other players
- Scout Reports, already specced in the node spec
- Regeneration skips via Gene Shards
- Defense record flair and raid banners

**Do not ship:**
- Purchasable Marks, or any Marks-equivalent bundle
- Raid attempts beyond the daily cap for money
- Cargo insurance, or any paid reduction of the 40% loss. This is the most tempting offer in the entire game and it is pay-to-not-lose — it converts every raid a non-payer suffers into an advertisement for a product they declined, which is the exact resentment mechanic that sinks review scores.
- Escort slots beyond class capacity for money

---

## 14. Reconciling With the Economy Model

The economy model budgets raid income at **200/day (Core)** and **400/day (Optimiser)**.

Check: a Hauler carrying 2,000, at 40% taken and 50% to the attacker, yields **400 per successful raid**. At 2 raids/day and a 50% win rate, a dedicated raider earns **~400/day**. The Core archetype figure of 200 assumes roughly one raid attempt daily, which matches a player for whom raiding is occasional.

This holds, but only because the daily cap is 2. **Raising the cap to 3 pushes a dedicated raider to ~600/day and breaks the model's 6× spread ceiling.** If raiding needs to feel more frequent in playtest, lower the attacker share rather than raising the cap.

---

## 15. Open Questions

1. **Should the Transit Board show cargo value at all?** Hiding it makes raids a gamble and reduces target-farming, but it also makes the raid decision arbitrary and the mode less strategic. Leaning toward showing it, with paid obfuscation as the counter.
2. **Is 2 raids/day too few to sustain a raider identity?** It's correct for economy balance and possibly wrong for player fantasy. The Marks Shop may need to carry more of that identity.
3. **Does the Drive module's transit speed edge into PvP advantage?** The Vault spec flags this. Faster transit is a smaller exposure window, which is a defensive buff bought with shards. May need Drive to affect relocation only.
4. **Apex Vein Collectors are stationary for 48+ hours.** Are they raidable the whole time, or only on the return leg? Whole-time is dramatic and probably brutal.
5. **Alliance convoy staging mechanics.** Multi-member Convoy Rigs need a contribution and payout structure that hasn't been designed.

---

*Remaining undocumented: the Trait Codex, mail and notification centre, player profile, and settings.*
