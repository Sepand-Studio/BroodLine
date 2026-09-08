---
status: current
folder: 03-technical
note: >
  Entitlement, validation, the Season Pass model, refunds, offer
  placement, pricing plumbing.
---

# Broodline — Store & IAP

*Technical spec, taking money without breaking the model*

> **CURRENT — technical spec.** `broodline_monetization.md` owns what is sold and
> what is never sold. This owns entitlement, validation, refunds, where offers
> may appear, and the one advertising surface the design has.

**Two decisions the design implies and has never stated.** The Season Pass should not be an auto-renewing subscription — §3. And refunds have to allow a negative balance, because the alternative is punishing a player for using a store feature Apple grants unilaterally — §5.

---

## 1. Entitlement categories

Three, and they behave differently enough that conflating them causes bugs that only appear after a reinstall.

| Category | Items | Restorable |
|---|---|---|
| **Consumable** | Gene Shards, Splice Charges, Geneticist XP, sample pulls | No. Granted once, spent |
| **Non-consumable** | Double Regen, skins, recipe card frames, cosmetic auras | **Yes, always** |
| **Season Pass** | The paid track for one season | Within its season |

**Double Regen is the one that matters most.** `broodline_monetization.md` makes it the anchor purchase at $9.99, permanent, halving charge regeneration. **It must survive reinstall, device change and account restore**, and it is the item a player will notice missing.

**Everything is tied to the account, never to the device.** A player who reinstalls, changes phone or restores from backup keeps everything. `broodline_server_topology.md` §5 forbids server transfers, so entitlement is per-account-per-server and the account is the anchor.

---

## 2. Validation

**Server-side receipt validation, always. No exceptions.**

Client-side validation is trivially bypassed and the exposure is the entire currency economy. `broodline_data_model.md` §8 already makes every currency grant server-authoritative; a purchase is a currency grant with a receipt attached.

**The flow:** the client completes the platform transaction, submits the receipt, and **the server validates, grants and acknowledges.** The client shows the grant only after acknowledgement. A client that grants optimistically and reconciles later will occasionally show a player shards they do not have.

**Idempotency by transaction id.** A retried submission grants once. Network failure during purchase is the most common real-world case and it must not double-grant or drop.

**Unacknowledged transactions are re-submitted on next launch.** A player who buys and loses connection before the grant lands gets it when they come back, without asking.

---

## 3. The Season Pass is not a subscription

`broodline_monetization.md` describes four-week seasons with a free and a paid track. **Nothing says whether the paid track auto-renews, and it should not.**

**Three reasons, and the third is the design's own.**

**Auto-renewal creates cancellation and refund load** disproportionate to a $9.99 item, and it is the mechanic that generates the most support contact in this genre.

**A four-week season and a monthly billing cycle do not align.** Thirteen seasons a year against twelve billing months produces a drift that eventually bills a player twice inside one season.

**And the design already argues against it.** `broodline_monetization.md`: *"a lapsed season costs a player nothing they cannot earn later."* A subscription exists to make lapsing costly. Selling the pass per season, as a one-off, is the version consistent with what the store page says.

> **The Season Pass is a non-renewing purchase, per season, available until the season ends.**

**Late purchase grants retroactively.** A player who buys in week three receives every paid-track reward they have already earned on the free track. Otherwise the pass has a hidden expiry curve and buying late is a bad deal the store does not disclose.

---

## 4. First purchase doubles

`broodline_monetization.md` grants **2× contents on the first purchase of any pack, one time only.**

**Server-tracked, per account, permanent.** It must survive reinstall — a flag stored on the device would let a player farm it, and a flag that resets on reinstall would let them do it accidentally.

**It applies to the first purchase, not to the first of each pack.** The wording matters and it should be unambiguous on the store screen, because a player who expects it twice and does not get it will report it as a bug.

---

## 5. Refunds

Apple grants refunds unilaterally and does not ask. **The design has to have a position on a player who refunds shards they have already spent.**

> **Reverse the grant. Allow the balance to go negative. Take no punitive action.**

A player who buys 3,200 shards, spends 3,000 on a facility, and refunds ends at −3,200. They keep the facility. They earn out of the negative balance at their normal rate, and nothing else about their account changes.

**Why not claw back the facility.** Undoing a tier upgrade means undoing a build timer, possibly a campaign milestone gate, possibly downstream splices that used the resulting generation ceiling. `broodline_data_model.md` §3 has no per-creature stat overrides precisely so that state stays derivable — and unwinding it would be a migration, per player, per refund.

**Why not ban or flag.** Refunding is a store feature. A player using it once is not abusing anything, and treating them as though they were is how a support ticket becomes a review.

**Repeat abuse is a platform problem, not a game one.** A player who refunds repeatedly is visible to Apple, and the game's response is to keep reversing grants. If the balance is deeply negative the player has effectively stopped being a customer, and no further mechanism is needed.

---

## 6. Where offers may appear

Bible §8.5 and `broodline_monetization.md` set the rules; this is where they land in the build.

**Permitted:**

| Surface | |
|---|---|
| Store tab | Always. It is the store |
| Event hub | Offers tied to a running event |
| Insufficient-currency prompt | **Once**, at the point of a blocked action, with the shortfall named |
| Season Pass entry | Once per season, on the season's opening |

**Forbidden:**

- **After a wave defeat.** Bible §9.3 makes the designed loss at wave 6 a teaching moment. An offer attached to it converts the game's best beat into its worst
- **After a raid loss.** `broodline_collectors_raiding.md` caps losses at 40% so returning is never a cleanup job. An offer at that moment undoes it
- **On launch, before play.** Bible §9.2 puts play in the first twenty seconds
- **Interstitially, ever.** No full-screen offer a player did not open
- **During the first fourteen days**, except the Double Regen trial conversion at `broodline_monetization.md`

**The insufficient-currency prompt is the one to be careful with**, because it is legitimate and it is one step from being a paywall. It appears once per blocked action, it names the shortfall, and **it always shows the free path alongside** — how long until the player has enough by playing.

---

## 7. Rewarded advertising

The design has an ad surface and it has never been specified. `broodline_monetization.md`'s charge model counts "three from rewarded ads" in a player's daily throughput.

> **Rewarded video only. Opt-in, three per day, never interstitial.**

| | |
|---|---|
| Reward | One Splice Charge, or a small shard grant |
| Cap | **3 per day**, resetting on the player's local midnight |
| Placement | The charge screen and the Gene Lab timer screen. Nowhere else |
| Trigger | **Player-initiated always.** No prompt, no offer, no "watch to continue" |

**Never gates anything.** Bible §8.2's guarantee that charges never block play holds regardless — an ad is a way to go faster, never a way to proceed.

**No ads for restricted accounts.** `broodline_moderation_ugc.md` §5 puts under-13 accounts in a restricted mode, and advertising to them is the category with the least defensible upside.

**Three a day against a core player's fifteen to eighteen charges is a modest accelerant**, which is the correct weight. If it becomes a meaningful share of throughput the cap is wrong, not the placement.

---

## 8. Pricing plumbing

Per `broodline_localization.md` §6: **the shard ladder must be monotonic in every storefront**, and prices are constrained to the platform's tier grid, so **shard counts move rather than prices.**

**That means pack contents are storefront-dependent and must be server-driven**, not compiled in. A build with hardcoded pack contents cannot correct an inverted ladder in a single currency without shipping an update.

**Always display the platform's localised price string.** Never convert, never approximate, never show a dollar figure to a player buying in yen.

**The monotonicity check is a build-time test**, run against the full storefront matrix. It should fail the build, not produce a warning — an inverted ladder is the thing players screenshot.

---

## 9. Guardrails

- Server-side receipt validation, always. Idempotent by transaction id
- Entitlement is per account, never per device
- Non-consumables always restore. Double Regen especially
- The Season Pass does not auto-renew, and late purchase grants retroactively
- First-purchase 2× is server-tracked, permanent, and applies once per account
- Refunds reverse the grant, permit a negative balance, and trigger nothing else
- No offer after a defeat of any kind, no interstitials, none before day 14
- Rewarded video is opt-in, capped at three daily, and gates nothing
- No advertising to restricted accounts
- Pack contents are server-driven; the monotonicity check fails the build

---

## 10. Open questions

1. **Three rewarded ads a day is a guess.** It is small enough not to distort throughput and large enough to be worth building. Whether it is worth building at all is a separate question — a game with two ad placements and a three-a-day cap earns very little from them, and the honest alternative is no ads.
2. **Retroactive Season Pass grants create a burst.** A player buying in week four receives four weeks of paid-track rewards at once, which is a large grant and a strange moment. It is still better than the alternative, but it may need pacing rather than a single dump.
3. **Nothing here covers promotional or offer codes**, which marketing will want and which need their own entitlement path.
4. **The negative-balance rule at §5 has an edge case nobody has thought through**: a player who refunds while holding a Convoy in transit, mid-Stake-Assault, or with a facility timer running. The balance goes negative and everything continues, which is probably correct and has not been checked.

---

*Owns: entitlement categories, validation, the Season Pass model, refunds, offer placement, rewarded advertising and pricing plumbing. Does not own: what is sold (`broodline_monetization.md`), what is never sold (bible §8.6), or storefront pricing rules (`broodline_localization.md` §6).*
