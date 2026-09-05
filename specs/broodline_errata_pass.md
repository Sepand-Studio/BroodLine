# Broodline — Errata, This Pass

*Consistency pass over the eight documents produced in this session. Fourteen findings: eleven fixed in place, three need a decision.*

---

## Fixed

### Numbers that disagreed across documents

**1. Wave 40 lane count.** The audit's §2 and §3 tables put wave 40 at three lanes. The addenda's chapter-aligned schedule makes it the *last two-lane wave*, with three lanes starting at 41. Every downstream figure used the corrected schedule, but the audit's tables were never re-run. Added a supersession banner rather than restating them — the tables are the record of how the failure was found and the corrected figures live in the addenda §6.

**2. Wave-1 anchor duration.** Species stats §7 derived `raider HP = 50 × budget cost` from a 20-second lane cleared in 14, calling that 30% headroom. The addenda then set one-lane waves at 25 seconds, which makes the same clear 40% headroom and matches the 1.75 ratio the addenda actually reports. The 50× constant is unchanged; the derivation text now says 25 seconds and 40%. The kill criterion in the tuning sheet moved with it.

**3. Calibration percentages.** The terminal sink computed against a 70% income share; the cosmetic economy then set the post-max split at 60% Calibration, 25% recurring, 15% cosmetics. The terminal sink's table still showed the 70% figures. Now 60% throughout: Core +6.5% at one year and +14.5% at three, Optimiser +10.0% and +21.0%.

**4. Vault module count.** Both the audit and the addenda called the Bastion "a seventh module" and then listed five existing ones. The list was wrong, not the count — the bible specifies six modules, so seventh was right. Corrected the list. Chasing this down found that the economy model prices only five, which is `broodline_four_decisions.md` §2 and the largest error in the set.

### Internal contradictions

**5. The terminal sink contradicted itself on the Splicing Chamber.** §3.3 ruled it "forbidden outright" because its generation cap is reach; §4 then made its charge regeneration one of the two Calibration targets. Both are right — the module has two effects and only one is legal — but the table said otherwise. Rewritten, along with "four of five modules fail," which is now three outright and a fourth in half of what it does.

**6. Hatchery Calibration did nothing.** +0.5% of a roster cap topping out near 60 rounds to zero. A player would have bought four Calibrations at 50,000 and up before seeing a single slot appear. Changed to **+1 roster slot per 4 Calibrations** — the same rate, expressed in a unit that visibly moves.

**7. Regrow's tier scaling was lopsided.** Tier I covered self, tier II the entire deployment, tier III the deployment plus retroactive. The I→II step was enormous and II→III trivial. Tier II is now self and adjacent tiles, matching Plating's grammar, evaluated from end-of-wave positions.

**8. "Those eight behaviours"** followed a list of five. Rewritten.

### Stale cross-references

**9. Species stats open item 1** asked whether coverage alone carries 65 waves. It does not, and three documents since have answered it. Marked answered with the resolution recorded — the instinct in the original note was right, and it is worth keeping visible that Ark-side scaling was the correct branch.

**10. The utility trait spec's closing line** said the species stat lines still needed a pass. They were written the same session.

**11. Bastion open item on screens** in the addenda said the Gene Lab needs a seventh tile. With the Bastion free and tied to Core tier, the layout is unchanged.

---

## Needs a Decision

### 12. Brood splits break the identity the whole audit rests on

Species stats §7 established that a wave's total enemy hit points equal `50 × budget`, which is what makes the ratio tables possible. It then said Brood's cost "covers the parent only; its splits are free."

Those cannot both be true. A Brood-heavy wave would carry more hit points than its budget priced, and every ratio in the audit and the addenda would understate the requirement.

**Fixed provisionally** by ruling that cost prices *total effective hit points* — a Brood's 1,000 is a 400 HP parent and three 200 HP splits, and a Bulwark's 1,750 includes its shield. This preserves the identity and is the cleaner rule.

**But it changes what Brood is.** Under the old reading Brood was cheap and disproportionately dangerous; under the new one it is priced honestly and becomes an ordinary raider that happens to arrive in pieces. If Brood is supposed to punish a missing Cinder harder than its cost suggests, the identity has to go and the ratio tables need re-running with a per-raider HP multiplier instead. Worth an explicit call.

### 13. Wave 1 is ten Skirmishers

That falls out of the budget formula — 100 points at 10 each — and it is a lot of bodies for the first wave a player ever sees, on a single lane, in a tutorial. Either wave 1 sits outside the formula as authored FTUE content, or the Skirmisher's cost is wrong, or the formula's base of 100 is. The FTUE spec should probably own wave 1 outright.

### 14. Module naming is inconsistent in the source set

The screen inventory names the Gene Lab's facilities Splicing Chamber, **Hatchery**, **Gene Vault**, Harvest Array, Drive and Core. The economy model and everything written this session use **Hatchery** and **Splicing Chamber**. These are the same things under two vocabularies, and the terminal sink now makes real decisions about which modules can be Calibrated — so the names have to agree before that document is actionable.

This predates this session and is not fixed here. It is the same class of problem as the Breeder/Geneticist drift the economy model §9 already caught.

---

## Also Noted, Not Changed

**Skittish repositions mid-wave**, which means the end-of-wave adjacency that Plating II and Regrow II depend on is not strictly fixed. The effect is small — one creature moving one tile — but if adjacency-based traits multiply, it becomes a rules question worth answering once.

**The utility trait spec and the addenda give different reasons for measuring at wave 40** — the four-type cap in one, the last two-lane wave in the other. Both are true and both point at the same wave, so no change; noted so nobody reconciles them into a single wrong reason later.
