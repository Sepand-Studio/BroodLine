---
status: superseded
folder: 99-archive
superseded-by: broodline_sample_economy.md
note: >
  Era-2.
---

# Broodline — Gene Vault Progression

*Design spec, the Ark's infrastructure layer*

---

## 1. Core Concept

Two specs already lean on this system. The node spec scales harvest yield and the contested-node presence stat by "Lab tier." The genetics spec scales roster capacity by "Gene Vault tier." Neither says what the Vault is.

The Gene Vault is the **Ark's physical infrastructure** — the thing you build up over months, and the primary long-term Gene Shard sink in the game.

**The split with Geneticist Tier matters and should never blur:**

| | Geneticist Tier | Gene Vault |
|---|---|---|
| Earned by | XP — logins, events, purchases | Gene Shards + time |
| Represents | Who you are | What you've built |
| Grants | Convenience, cosmetics, small accelerations | Capacity and throughput |
| Shape | One linear 12-rung ladder | Five parallel modules |

If these two systems start granting overlapping perks, players stop being able to tell what they're progressing toward. Tier is a status ladder. The Vault is a base.

---

## 2. The Five Modules

The Vault is not one number going up. It's five modules, each independently upgradeable, so a player is always choosing what to invest in rather than following a single forced path.

| Module | Governs | Feeds |
|---|---|---|
| **Harvest Array** | Yield rate multiplier, presence weight in contested regions | Node spec §4, §5 |
| **Habitat** | Roster capacity | Genetics spec §7 |
| **Splice Chamber** | Maximum creature generation | Genetics spec §2 |
| **Drive** | Relocation speed, transit exposure window | Node spec §6 |
| **Core** | Ark integrity in region defense; caps all other modules | Combat spec §7 |

Different players will prioritise differently, and that's the point. A raider invests in Drive and Harvest Array. A collector-optimiser pushes Habitat and Splice Chamber. Neither is wrong, and their Vault screens should look visibly different from each other by month three.

---

## 3. Core as the Spine

**No module may exceed the Core's tier.** Core is the expensive, slow, deliberate upgrade — the town-hall pattern, and it works because it forces periodic hard commitment rather than continuous incremental drip.

Core has **12 tiers**. Each requires:
- Gene Shards, on a steepening curve
- A build timer
- **A campaign milestone**

That last requirement is the most important guardrail in this document. Core progression cannot be bought past — it requires actually playing the game. A player who spends heavily on day one accelerates through the shard and timer costs and then stops, blocked at the same campaign wall as everyone else.

This is what keeps money buying *speed* rather than *skipping*, which is the line the monetization spec draws and the one most easily crossed by a progression system.

---

## 4. The Splice Chamber & the Generation Ceiling

The most interesting module, and the one that gives the game's name mechanical teeth.

**Splice Chamber tier sets the maximum generation a creature can reach.** Attempting a splice whose child would exceed the ceiling is blocked, with the Chamber upgrade surfaced directly in the message.

This does three useful things at once:

**It gates the power curve cleanly.** The genetics spec ties trait quality ceilings to generation, so without a gate a determined day-three player could splice their way to a deep lineage far ahead of the intended curve. The Chamber controls that without any artificial-feeling restriction.

**It makes lineage an achievement.** A Gen 14 creature isn't just the product of many splices — it's proof of sustained infrastructure investment. That's what makes the Lineage View worth screenshotting.

**It gives the mid-game a clear goal.** "I need Chamber tier 6 to push this broodline further" is a legible, motivating, weeks-long objective, and it points directly back into harvesting and the map economy.

---

## 5. Cost Structure & Timer Philosophy

Upgrades cost Gene Shards and real time. Timers are the sink; that's the genre and there's no need to pretend otherwise. But timers are also the single most-cited complaint in reviews of comparable titles, so the position needs to be deliberate.

**Rules:**

- **One Vault upgrade in progress at a time.** A second concurrent slot unlocks at Core tier 8 — a meaningful, earned milestone rather than a purchase.
- **Maximum timer of 48 hours**, at the very top of the Core track. The genre norm of multi-day waits is not worth the retention cost.
- **Early tiers are near-instant** — the first six or so Core tiers should complete in minutes to hours, so a new player feels the system moving.
- **Speed-ups are always purchasable and always earnable.** Gene Shards skip timers; so do event rewards and campaign milestones.
- **Never block the whole game on a timer.** A player with an upgrade running can still harvest, splice, run campaign waves, raid, and defend. The Vault is one track among several, never a gate on the session.

The failure mode to design against: a player opens the app, sees a 3-day timer and nothing else to do, and closes it. Every timer in this system should have at least three other things competing for the player's next 60 seconds.

---

## 6. What the Vault Must Never Do

- **No combat power.** No module increases creature damage, health, or the five-creature deployment cap. Core's Ark integrity is the sole exception and it applies only to PvE region defense — raids never target the Ark.
- **No PvP advantage.** Vault tier must not influence raid outcomes, interception, or Stake Assault. A high-Vault player has more throughput, not stronger creatures.
- **No trait access.** Every trait tier stays reachable at any Vault level. The Chamber gates generation, never rarity.
- **No hard lockouts.** A low-Vault player harvests more slowly and holds fewer creatures. They are never excluded from content.

The consistent principle across all eight specs: money and time buy *rate*, never *reach*.

---

## 7. Screen Requirements

The Gene Vault screen was flagged as undefined in the screen inventory. It needs:

- Five module cards, each showing current tier, next-tier effect, and cost
- Core tier prominently displayed, with the cap it imposes on other modules made obvious
- Active upgrade timer with skip cost
- **Next-tier effect stated in concrete numbers**, not "improves harvesting" — players should see "+8% yield" before committing shards
- Campaign milestone requirement surfaced clearly when Core is blocked, with a direct route to the relevant wave
- Splice Chamber card showing the current generation ceiling and the roster's highest-generation creature against it

That last item is a small thing that does real work — it turns an abstract number into "you are two generations from your ceiling."

---

## 8. Monetization

**Safe:**
- Gene Shard purchase, which flows into Vault upgrades as the primary long-term sink
- Timer speed-ups
- Vault cosmetic skins — the Ark is visible on the map to other players, so exterior customisation has genuine display value

**Do not ship:**
- Bypassing the campaign milestone requirement on Core tiers. This is the anti-whale guardrail; selling around it defeats the entire structure.
- Additional concurrent upgrade slots for money. Core tier 8 is the intended gate.
- Module tiers sold as bundles that skip the Core cap.

---

## 9. Open Questions

1. **Is 12 Core tiers the right depth?** It needs to sustain roughly two years of progression without the last three tiers becoming unreachable for anyone but the top 1%.
2. **Should the Drive module reduce raid exposure?** Faster transit means a smaller interception window, which edges into PvP advantage. May need to affect relocation only, not collector routes.
3. **What happens to the Vault when the Ark relocates?** Nothing, presumably — but if relocation had a small infrastructure cost, it would add weight to the move decision the node spec is built around.
4. **How does Habitat interact with the splice economy?** A larger roster cap means more hoarding, which slightly weakens the consumption loop. Worth watching in playtest.
5. **Do modules ever downgrade?** Almost certainly not, but region loss or alliance collapse might argue for temporary debuffs rather than permanent loss.

---

*Remaining undocumented: the FTUE — the first session, where the Ark, the map, the first splice, and Founder naming are introduced.*
