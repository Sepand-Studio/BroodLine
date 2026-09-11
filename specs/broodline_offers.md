---
status: current
folder: 01-companions
note: >
  The shortfall offer - the only promotion in the game, and what it may
  know.
---

# Broodline — The Offer Engine

*Design spec, when the game shows you a pack*

> **CURRENT — companion to the design bible.** `broodline_monetization.md` owns
> what is sold. `broodline_store_iap.md` §6 lists which surfaces may carry an
> offer. This owns what decides to show one, how often, and what the algorithm
> is allowed to know.

**Two lines hold everything else up.** The algorithm chooses **which** offer, never **what it costs** — §4. And an offer improves contents at a fixed price rather than discounting, because a discount inverts the monotonic ladder the store rests on — §3.

**No third-party advertising** — `broodline_monetization.md` §10. This is Broodline promoting Broodline, and it is the only promotional surface in the game.

---

## 1. What an offer is

**A pack from the existing ladder, with added contents, at its normal price, for a limited window.**

| | |
|---|---|
| **Not** a discount | See §3 |
| **Not** a new SKU invented per player | Offers draw from the ladder at `broodline_monetization.md` §5 and the Custom Chest |
| **Not** exclusive contents | Bible §8.6's never-sold list applies without exception. An offer cannot contain a trait, a creature, a catalyst or Marks |
| **Not** a modal a player did not open | `broodline_store_iap.md` §6 forbids interstitials |

**An offer appears as a card**, in a surface the player is already looking at, with the pack it improves, the improvement, and the time remaining. Tapping it opens the normal purchase flow.

---

## 2. What triggers one

**One thing. A shortfall.**

A player attempts something they cannot afford. `broodline_store_iap.md` §6 already permits a one-time insufficient-currency prompt naming the shortfall. The offer engine is that prompt, with a pack attached.

> **The offer is the smallest pack that covers the shortfall**, never the largest that fits the moment.

A player 200 shards short of a Splicing Chamber tier gets the Starter Splice, not the Geneticist's Vault. Anything else is the game reading a moment of frustration as a buying signal, which is the pattern this whole model exists to avoid.

**The free path is shown alongside, always.** How long until they have enough by playing.

### 2.1 What was cut, and why

An earlier draft had three further categories — **progression** offers at thresholds like Core tier 8, **temporal** offers on event openings, and **lifecycle** offers at day 15 and on return from absence. All three are cut.

**They were never going to carry volume.** Progression offers fired once per threshold, ever, and there are perhaps seven thresholds in two years. Temporal offers were one per event. Against a one-a-week cap, the unprompted slot was empty most weeks — and an engine that is mostly idle is an engine that is mostly build cost.

**And the one that remains is the only one a player asked for.** A shortfall offer appears because the player tried to do something. Every other kind appears because the game decided it was a good moment, and "the game decided" is where an offer system stops being a service and starts being a prompt.

**Two things survive outside this engine**, because they were never really offers:

- **Day 15's Double Regen trial conversion** stays where `broodline_monetization.md` put it — it is the end of a trial, shown once, and it is not triggered by this engine
- **The Season Pass entry card** on a season's opening day, per `broodline_store_iap.md` §6 — a store surface, not a promotion

**The engine is now small enough to describe in a sentence:** when a player cannot afford something, show them the smallest pack that would cover it, once, with the free path beside it.

---

## 3. Offers add contents, never cut price

**A discount would break the ladder.** `broodline_localization.md` §6 requires the shard ladder to be monotonic in every storefront — value per unit currency rising with every step. A 40% discount on the Lab Bundle puts it above the Lab Expansion and the Geneticist's Vault at once, and a player who screenshots the store is screenshotting an inverted ladder.

> **An offer improves the contents of a pack at its normal price.**
> *"Lab Bundle, +50% Gene Shards, 48 hours."*

**And it has a ceiling.** An improved pack may rise toward the next tier's value but never past it.

| Pack | Base | Ceiling |
|---|---|---|
| Starter Splice | 101/$ | 120/$ |
| Lab Bundle | 120/$ | 140/$ |
| Lab Expansion | 140/$ | 160/$ |
| Geneticist's Vault | 160/$ | 180/$ |
| Warden's Cache | 180/$ | 200/$ |
| Ark Reserve | 200/$ | **No offer.** Nothing sits above it |

**Checked at build time, per storefront, alongside the base ladder.** `broodline_store_iap.md` §8 already fails the build on an inverted ladder; offers go through the same check.

**The Ark Reserve never carries an offer.** The top of the ladder has nothing above it to bound an improvement, and improving the largest pack is the least defensible offer in the store.

---

## 4. What the algorithm may know

The line that matters most, and it is short.

**Permitted inputs:**

| | |
|---|---|
| The current shortfall | What they just tried to do and by how much |
| Progression state | Core tier, campaign wave, facilities built |
| Time since the last offer | For the cap at §5 |
| Dismissal history **by offer type** | An ignored type stops appearing — §6 |

**Forbidden inputs:**

| | |
|---|---|
| **Lifetime spend** | |
| **Predicted willingness to pay** | |
| **Session count, streak length, or any engagement score** | |
| **Time of day, or proximity to payday** | |

> **The algorithm chooses which offer. It never chooses the price, and it never chooses based on what the player has spent.**

**Two players at the same progression point with the same shortfall see the same offer at the same price.** One of them having spent four hundred dollars changes nothing.

**This is a commercial cost and it is the correct one.** Spend-conditioned pricing is the mechanic that produces the story about a game charging its most invested players more, and that story is unrecoverable. The design already turns down cargo insurance and purchasable Marks on the same reasoning — bible §8.6 — and this is the same decision applied to the store rather than to the game.

---

## 5. Frequency

> **At most two shortfall offers a week, seventy-two hours apart. Never unprompted.**

**Every offer is one the player triggered.** There is no unprompted slot — §2.1. A player who never runs short never sees an offer, and a player who runs short constantly sees at most two a week regardless.

**"Every now and then" is a real constraint and it needs a number**, because the failure mode of an offer engine is not a bad offer — it is a good offer arriving too often until the player stops seeing offers at all. Two a week, only when asked, is a number that can be defended out loud — and for most players the real figure will be closer to two a month.

**Suppression, absolute** — a shortfall during any of these produces the plain prompt with no pack attached:

- **After any defeat.** Campaign wave, region defence, raid attack, raid defence, Stake Assault. Bible §9.3 makes the wave-6 loss the game's best teaching moment and an offer attached to it converts it into its worst
- **During the first fourteen days**, except the day-15 Double Regen trial
- **During any onboarding beat**, bible §9.2
- **On the splice confirmation screen.** Bible §2.7 makes it the screen that states costs honestly; it does not also sell
- **In the twenty-four hours after a raid loss.** `broodline_collectors_raiding.md` caps losses at 40% so returning is never a cleanup job

**Joining an alliance carries no offer**, and it is worth saying why it is on the list. A new alliance member is at their most socially engaged and their most suggestible, and an offer at that moment would work. That is the reason not to.

---

## 6. Ignored offers stop appearing

**Dismissal is a signal and it should be honoured**, not treated as an opportunity to try again.

| | |
|---|---|
| Dismissed once | The type is suppressed for two weeks |
| Dismissed twice | Suppressed for eight weeks |
| Dismissed three times | **Retired permanently** for that player |

**Three dismissals and the engine retires for that player.** There is only one type now. They can still open the store, and the insufficient-currency prompt still names the shortfall and the free path — it simply stops attaching a pack.

**Nothing about this is surfaced.** A player who never sees a progression offer again does not need to be told why, and telling them would turn a courtesy into a negotiation.

---

## 7. What a player can turn off

**A single setting: "Show offers."** Off means no offer card ever appears anywhere. The store tab still works.

**It is not buried and it is not accompanied by a warning about missing out.** A player who turns it off has told the game something and the game should believe them.

---

## 8. Guardrails

- Offers add contents at a fixed price. **Never a discount**
- An improved pack never exceeds the next tier's value per dollar, checked at build time per storefront
- The Ark Reserve never carries an offer
- The algorithm never uses lifetime spend, predicted willingness to pay, or any engagement score
- Two players at the same progression point see the same offer at the same price
- A shortfall offer is the smallest pack that covers the shortfall, with the free path shown alongside
- Shortfall offers only. At most two a week, seventy-two hours apart. **No unprompted offer, ever**
- No offer after any defeat, during the first fourteen days, during onboarding, on the splice confirmation, or within a day of a raid loss
- Three dismissals retire an offer type permanently
- "Show offers" turns everything off, with no warning attached
- Bible §8.6's never-sold list applies to offer contents without exception

---

## 9. Open questions

1. ~~**One a week is deliberately low.**~~ **Resolved — shortfall-only, two a week maximum.** The unprompted slot is cut at §2.1. The remaining question is whether an engine this small is worth building as a separate system at all, or whether it is simply the insufficient-currency prompt with one extra field. **Recommend the latter.** `broodline_telemetry.md` measures dismissal rate either way.
2. **The value ceiling at §3 caps improvements at roughly +17%**, which is a modest offer by this genre's standards and may not convert. The alternative is breaking ladder monotonicity, which is not an alternative.
3. ~~**Progression offers fire once per threshold.**~~ **Cut, with temporal and lifecycle offers — §2.1.** The engine is shortfall-only.
4. **Nothing here covers cross-promotion of future titles.** It is not third-party advertising and it needs no SDK, but it is still an interruption in a product whose posture is generous-feeling free play. **Recommend the same answer as third-party ads: no.** A studio's second game is best advertised by the first one being good.

---

*Owns: the shortfall offer — its trigger, frequency, suppression, the algorithm's permitted inputs, the value-ceiling rule and the dismissal ladder. Does not own: what is sold (`broodline_monetization.md`), what is never sold (bible §8.6), surfaces (`broodline_store_iap.md` §6), or storefront pricing (`broodline_localization.md` §6).*
