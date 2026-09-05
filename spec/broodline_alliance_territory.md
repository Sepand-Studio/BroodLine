# Broodline — Alliance & Territory Control
*Design spec, the coordination layer*

---

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Brought current from the original spec. The meta layer was never in conflict
> with the reconciliation — the changes are a vocabulary pass, the removal of
> Alliance Hall as a personal facility, convoy staging at §7.1, and the weekly
> tick decision at §12.


## 1. Core Concept

Three specs already assume regions can be controlled. None define how. This one closes that.

Territory control is the game's **social gravity**. It's the reason to be in an alliance rather than adjacent to one, and it's the only system that makes another player's login schedule matter to you. But it carries the highest toxicity risk in the design, so the guardrails in §8 are not optional trimmings — they're the difference between a healthy server and one where the top alliance owns everything and nobody else can get a foothold.

**Founding principle:** control confers *advantage*, never *exclusion*. A player locked out of the map churns. A player operating at a disadvantage joins an alliance.

---

## 2. Alliance Structure

- **40 members maximum.** Enough for round-the-clock timezone coverage on raid alerts, small enough that an individual member is visible. Larger caps turn alliances into anonymous mailing lists.
- **Roles:** Founder, up to five Officers, Members.
- **Officer permissions:** place and withdraw Claim Stakes, assign garrison creatures, initiate rallies, spend alliance treasury.
- **Joining:** open, application-gated, or invite-only, set by the Founder.

Officers matter more here than in most games because garrison assignment costs individual members real resources. Handing that power to five people is a trust decision, and the UI should make who assigned what fully visible to prevent quiet abuse.

---

## 3. Claiming: Stakes and Hold

An alliance claims a region by placing a **Claim Stake**. The Stake accumulates **Hold**, a score that determines control.

Hold accrues from:
- **Member presence** — Arks positioned in the region, weighted by time present
- **Harvest volume** — cargo successfully delivered from that region's nodes
- **Garrison strength** — creatures assigned to defend the Stake

Control resolves on the **weekly tick**, deliberately synced with the Rich Deposit rotation and the Gene Lab event. One day a week, the map reshuffles and territory changes hands at the same moment. That's a single reason to log in rather than three scattered ones, and it makes that day the week's event.

If two alliances hold Stakes in the same region, it's **Contested** — Hold accrues to both, control goes to the higher total at the tick, and the losing alliance's Stake is refunded rather than destroyed. Losing a contest should cost you a week, not your investment.

---

## 4. Contesting: Stake Assault

Pure accumulation would make territory a spreadsheet. **Stake Assault** is the fight.

Any alliance member may assault a rival Stake in a region where their own alliance holds a Stake. This resolves as a tower-defense engagement using the combat engine: the defending alliance's **garrison** creatures are the towers, the assaulting player's deployment is the incoming wave.

- A successful assault removes a fixed chunk of the defender's accumulated Hold
- Assaults are rate-limited per player per day, so Hold can't be zeroed by one motivated person with no sleep schedule
- Defending garrison creatures that fall enter regeneration normally — never lost
- The defending alliance receives a notification and can reinforce the garrison

This is the intended peak-coordination moment: a contested Apex Vein region, two alliances trading assaults across a week, garrisons being rebuilt between them.

---

## 5. Garrisons

Garrison creatures are contributed by individual members and are **unavailable for anything else while garrisoned** — no raids, no escorts, no campaign.

That cost is the point. It's what makes territory a genuine alliance investment rather than a free passive bonus, and it creates the internal conversation that alliances exist to have: who contributes what, and is this region worth it.

- Garrison capacity scales with Stake maturity — a fresh Stake defends thinly
- Contributions are publicly visible within the alliance
- Members can withdraw a garrisoned creature at any time, on a short cooldown

Visibility is doing quiet work here. Alliances self-police contribution far better than any system you could build, provided they can see who's carrying weight.

---

## 6. What Control Confers

| Benefit | Effect |
|---|---|
| **Cooperative harvest** | Allied members harvest the region's nodes at full rate simultaneously, no yield splitting |
| **Non-ally penalty** | Hostile Arks harvest Rich Deposits and Apex Veins at **−40%** — a penalty, never a lockout. **Never applies to Common Veins**, in any region, under any controller |
| **Interception penalty** | Hostile raiders suffer reduced interception effectiveness on routes through the region |
| **Alliance convoy staging** | Multi-member Convoy Rigs may depart from controlled regions only — see §7.1 |
| **Regional banner** | Cosmetic display on the map heat-map overlay |

The −40% figure is the single most important number in this document. It has to be painful enough that alliances want control and negotiate over it, and mild enough that an unaffiliated player can still work a contested region and make progress. If it ever becomes an effective lockout, the map closes and new players leave.

---

## 7. Alliance Tech

Members contribute Gene Shards to an alliance treasury, unlocking account-wide perks for all members.

Branches, all **logistics and convenience, never combat power**:
- **Convoy** — unlocks the Convoy Rig collector class and **all four Collector classes**, additional escort slots, faster convoy transit
- **Extraction** — faster Hold accrual, increased garrison capacity
- **Lab** — reduced regeneration timers, faster Splice Charge regen

Keeping combat power out of alliance tech is what stops the top alliance from compounding into unbeatability. A member of a brand-new alliance fields exactly as strong a creature as a veteran; what they lack is throughput and logistics, which effort closes.

**The Convoy branch picked up a job it did not originally have.** Collector classes used to unlock from the Drive facility, and bible §7.2 now restricts Drive to the Ark alone, because faster Collector transit is a shorter exposure window and therefore a defensive PvP advantage bought with shards. Campaign milestones and this branch carry the unlocks instead. A solo player reaches every class through milestones; the branch only makes it sooner.

---

## 7.1 Convoy staging

The one alliance mechanic that was named in three documents and designed in none.

**A Convoy Rig carries two to five members' cargo as a single unit.** Members commit Collectors during a **thirty-minute assembly window** in a region the alliance controls, and the convoy departs as one.

| | Solo convoy | Convoy Rig |
|---|---|---|
| Cargo | One player's | Sum of 2–5 contributors' |
| Escort slots | 2–4 | **Sum of contributors', capped at 8** |
| Interception points | One per convoy | One, total |
| Loss cap | 40% of that cargo | 40% of total, **split by contribution share** |
| Raid party against it | 3 creatures, one attacker | **Up to 3 attackers, 3 creatures each** |

**Staging trades exposure count for exposure size.** Four members running solo convoys face four separate 40% risks; one Rig faces a single 40% risk on four times the cargo. The expected loss is identical. **What staging actually buys is escort concentration** — four members' escorts on one convoy against an attacker who can still only bring three creatures.

**That is why joint raids exist.** An eight-escort convoy against a three-creature party would be untakeable, and an untakeable convoy is a PvP economy that stops. So a Convoy Rig may be raided by **up to three attackers from one alliance**, each bringing a full party. Convoy staging is alliance-versus-alliance, and it is the only place in the game where that is literally true.

**Joint raids fire only against Convoy Rigs.** A solo player's convoy is never hit by a three-person raid, under any circumstances. Bible §6.10's promise that solo is slower rather than blocked would not survive the alternative, and this is the cleanest way to keep it.

**Contribution and payout:**

- **Cargo** returns to each contributor in proportion to what they committed
- **Losses** are split the same way, so a bad interception costs everyone proportionally rather than costing the largest contributor everything
- **Defense Marks** go to every contributor who committed an escort, whether or not their own cargo survived
- **Raid Marks** split among joint attackers by each party's share of the escorts defeated
- **The assembly window is not raidable.** A convoy that has not departed is not in transit

**The real cost of staging is coordination, and it should stay that way.** Five people in a thirty-minute window, from a region the alliance holds, is a genuine ask. It should never become the default way to move cargo — it should be what an alliance does when the cargo is worth it.

---

## 8. Anti-Monopoly Guardrails

The load-bearing section.

- **Common Veins can never be claimed.** They stay neutral and fully harvestable by anyone, forever. This is the livable baseline the node spec promises, and it means no player can ever be starved off the map.
- **Five concurrent Stakes per alliance, maximum.** A dominant alliance must choose which regions matter. Even at peak strength they cannot own the map.
- **The non-ally penalty is capped at −40%** and can never be increased by tech, purchase, or event.
- **No base attacks.** Nothing in this system lets one player damage another's Ark, roster, or stored resources. Raiding takes cargo in transit; assaults take Hold. Player reviews of comparable titles consistently name base-burning as the churn trigger, and the design simply doesn't contain the mechanic.
- **New alliance grace:** alliances under 14 days old cannot have their Stakes assaulted, giving a group time to establish before contesting.

---

## 9. Inactivity & Decay

Dead alliances squatting on rich regions is the slow-motion failure mode that strangles a server in month eight.

- A Stake with no member presence in its region for **5 days** begins losing Hold
- At zero Hold the Stake dissolves and the region returns to neutral
- Alliances below 5 active members in a 14-day window have their Stake cap reduced

Decay should be visible and warned well in advance — an alliance losing territory should see it coming for days, not discover it after the fact.

---

## 10. Solo and Unaffiliated Players

A meaningful share of players will never join an alliance, and they still need to be a viable audience.

- Full access to Common Veins everywhere, permanently
- Rich Deposits and Apex Veins at the −40% penalty in controlled regions, full rate in neutral ones
- Ineligible for cooperative harvest, alliance tech, convoys, and Stake mechanics
- Campaign, splicing, region defense, and the Apex Cup remain fully available

The honest framing: solo play is slower, not blocked. That's the correct trade — alliance membership should be attractive rather than compulsory, and the difference should read as opportunity cost, not punishment.

---

## 11. Monetization

**Safe:**
- Alliance banners, Stake skins, regional display flair — highly visible on the map screen everyone checks daily
- Alliance-wide cosmetic unlocks funded by the treasury, giving contribution a visible payoff
- Apex Cup leaderboard flair, already specced

**Do not ship:**
- Purchasable Hold, or anything that accelerates it with money. This converts territory from an effort contest into a spending contest and is the fastest available route to a pay-to-win reputation.
- Additional Stake slots for money. The cap of five is a competitive-health mechanism, not an inconvenience to be monetized.
- Increases to the non-ally penalty. It is a fixed constant of the design.

Territory is the part of the game where monetizing the wrong lever is least recoverable, because the damage lands on non-payers who then leave.

---

## 12. Open Questions

1. **Is 40 the right alliance cap?** Comparable titles run near 100. Smaller means tighter communities and more individual visibility; it also means fewer members awake for a 90-second raid alert at 4am.
2. **Should Stake Assault require the assaulting alliance to hold a Stake in the region?** As written, yes — it prevents drive-by griefing, but it also means a small alliance can't harass a large one without first committing a Stake.
3. ~~**Weekly tick timing across timezones.**~~ **Resolved: per-server, from three fixed slots, set at server creation and never changed.**

   Whichever single global hour were chosen would advantage one third of the world and hand the other two thirds a tick that fires while they sleep — and the tick is when territory changes hands, Rich Deposits rotate, and the weekly event resolves. Three slots approximating APAC, EMEA and Americas prime evening, with players matched to a server by region at signup, gives almost everyone a tick they can be awake for.

   **Never changed** is the load-bearing half. A moving tick makes territory planning impossible, and an alliance that organised its week around Sunday evening should not have that moved by an ops decision. The hour is fixed at server creation and displayed **in the player's own local time** everywhere it appears — the tick is a fact about their week, not a UTC offset to work out.
4. **Does the −40% penalty apply to Common Veins in a controlled region?** It should not, per §8, but that needs to be unambiguous in implementation.
5. **Alliance mergers and diplomacy.** Formal alliance-of-alliances structures drive coordination but also drive server-wide monopolies. Probably out of scope for launch.
6. **Is five the right maximum for a Convoy Rig?** Larger concentrates more escorts and makes joint raids harder still; smaller makes the assembly window easier to fill. Five against three attackers is the ratio that was reasoned about, and it is untested.
7. **Should a Convoy Rig be visible on the Transit Board as a Rig**, or disguised as an ordinary convoy? Visible is honest and paints a target; disguised is a nasty surprise for an attacker who committed a solo party. Leaning visible, with contributor count shown.

---

*Owns: alliance structure, Stakes and Hold, Stake Assault, garrisons, what control confers, alliance tech, convoy staging, decay, and the weekly tick. Does not own: raid rules (`broodline_collectors_raiding.md`), region topology (`broodline_region_roster.md`), or Collector classes and cargo (`broodline_collectors_raiding.md` §3).*
