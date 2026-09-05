# Broodline — Cosmetic Economy

*Sizes cosmetics as a share of income and the season catalogue against it. Closes the third open item in `broodline_terminal_sink.md`.*

---

## 1. Why This Document Exists

Cosmetics are named as a reward or a sink in eight places across the set — pack contents, Season Pass tracks, Apex Cup prizes, Recipe Share frames, Collector skins, Geneticist Tier perks, chapter completion, alliance flair. The economy model gives them one line: 1,500–6,000 shards.

Nothing says how many exist, how often they ship, or what share of income they are meant to absorb. That makes them the last unmodelled thing in the economy, and it matters more now than it did before: Calibration absorbs a maxed player's income but it is a grey number on a settings screen, and a player with 3,570 shards a day and nothing they *want* is a churn risk regardless of whether the shards have somewhere to go.

---

## 2. The Post-Max Budget

Pre-max, the economy model splits a Core player's income 60% Vault, 25% recurring sinks, 15% discretionary. Post-max it should keep the same shape:

| Share | Pre-max | Post-max |
|---|---|---|
| 60% | Vault modules | **Calibration** |
| 25% | Regen skips, treasury, Roulette, Scout Reports | Unchanged |
| 15% | Discretionary | **Cosmetics, named** |

Calibration inherits the Vault's slot exactly. Cosmetics get the discretionary 15% as a target rather than a residual — which is the change this document makes.

**Amends `broodline_terminal_sink.md` §4**, which computed Calibration against a 70% share. At 60%:

| | 1 year | 3 years |
|---|---|---|
| Core | +6.5% | +14.5% |
| Optimiser | +10.0% | +21.0% |

The conclusions there are unaffected; the figures move slightly.

---

## 3. The Ladder

Four price points, inside the economy model's existing 1,500–6,000 band.

| Tier | Price | What it is |
|---|---|---|
| **Flair** | 1,500 | Lineage frames, recipe card frames, leaderboard badges, alliance banners |
| **Collector skin** | 3,000 | Convoy livery. Visible to other players on the map, which is why the collectors spec already names it as safe |
| **Creature skin** | 4,500 | A species body reskin. Applies to every creature of that species the player owns |
| **Ark piece** | 6,000 | The base itself. The most visible object a player owns and the most expensive |

**Everything is shard-priced. Nothing is cash-only.** Packs may bundle a skin as an extra, but every cosmetic in the game has a shard price and therefore a free path, per the economy model's guardrail.

**Creature skins apply per species, not per creature.** A creature is consumed by splicing; a skin attached to an individual would be destroyed with it, which turns a purchase into a punishment for playing the loop. Buying the Vetch skin means every Vetch the player ever holds wears it.

---

## 4. Season Catalogue

**Five items per season, 16,500 shards total.**

| Item | Price |
|---|---|
| Creature skin (rotating species) | 4,500 |
| Collector skin | 3,000 |
| Two flair items | 3,000 |
| Ark piece | 6,000 |

At thirteen four-week seasons a year that is 65 items and 214,500 shards of catalogue.

### Why that number

| | Annual income | 15% share | Per season | Catalogue is 16,500 |
|---|---|---|---|---|
| Casual | 277,400 | 41,610 | 3,201 | Buys one item, sometimes two |
| Core | 1,303,050 | 195,458 | 15,035 | **Buys four of five** |
| Optimiser | 2,306,800 | 346,020 | 26,617 | Buys everything, with room |

The Core figure is the target and it lands where it should: **a Core player clears about 91% of each season's catalogue**, which means one deliberate skip per season. Not a grind, not a giveaway — one choice.

A Casual player buys the thing they actually want, once a month, which is the right relationship for someone playing an hour a day. An Optimiser clears the catalogue and puts the surplus into Calibration, which is what the 60% slot is for.

---

## 5. Production

Sixty-five items a year sounds like a lot until it is broken down against the modular rig.

| Class | Per year | Asset cost |
|---|---|---|
| Creature skins | 13 | Palette and texture pass over one of six existing bodies. The rig already exists; §10.3 of the bible caps species bodies at six and this reuses them |
| Collector skins | 13 | One vehicle mesh, reskinned |
| Flair | 26 | 2D. Frames, badges, banners |
| Ark pieces | 13 | The single genuinely new 3D asset each month |

The only real production line is the Ark piece. Everything else is a recolour, a texture, or 2D — which is exactly why the art direction's insistence on a modular rig with standardised attachment points pays off here rather than only in creature variety.

**Trait parts are never cosmetic targets.** Bible §10.4 makes the two combat traits visible on the body and calls that the most important functional requirement in the art direction. A skin that alters a trait part breaks roster scanning. Skins change colour, pattern and surface; never silhouette, never the trait parts, never the Instinct badge.

---

## 6. A Contradiction to Fix

The monetization spec says both of these:

- Season Pass paid track carries "exclusive skins"
- "Season Pass paid track is quantity, never exclusivity"

And the economy model's guardrail says every sink has a free path with money buying the same thing sooner.

**Resolution: the guardrail is about power and progression, and cosmetics should be carved out of it explicitly.** A paid-track-exclusive skin costs a lapsed player nothing they need, and cosmetic exclusivity is the normal and harmless case. But the ledger line has to say so, or the next person reading it removes the skins.

Amend the guardrail to: *every sink that affects power, progression or convenience has a free path; cosmetic exclusivity is permitted.* Amend the monetization line to: *the paid track is quantity for everything except cosmetics.*

---

## 7. Retired Seasons

A season's catalogue rotating out permanently means a player returning in month 30 finds a thin store and a Calibration screen.

**Retired cosmetics return to a rotating archive at 1.5× original price**, six items at a time, refreshed monthly. Season Pass exclusives never enter it — that is what makes them exclusive.

This costs nothing to build, absorbs income from returning and lapsed players at exactly the moment they are deciding whether to stay, and gives the 15% share somewhere to go in a month where nothing in the new catalogue appeals.

---

## 8. Remaining Open

**1. Whether thirteen Ark pieces a year is affordable.** It is the only new 3D asset in the schedule and the whole cadence rests on it. If it is not, drop to one Ark piece a quarter and add a second creature skin in the other months — the shard total per season stays within 1,500 of 16,500 either way.

**2. Alliance-owned cosmetics.** Territory markers and alliance banners are the obvious social sink and the alliance treasury is capped at 600/day per player specifically to stop one payer funding everything. A cosmetic-only treasury channel would sidestep that cleanly, but it is the same open item flagged in the terminal sink document and should be decided once, for both.

**3. Cosmetics are not modelled for pre-max players at all.** This document sizes the post-max 15%. A player eighteen months from maxing has the same 15% discretionary share on paper and far more competing demands. Worth checking that the season catalogue does not read as unaffordable during the stretch where most players actually live.

---

*Downstream: economy model §4 gains a cosmetic line and §10 gains the carve-out, monetization spec's exclusivity line amended, live-ops gains the season catalogue and the archive.*
