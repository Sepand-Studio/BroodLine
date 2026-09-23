# The species palette — the decision, and the one still open

**Status: decided and applied.** Phase 8 Task 6 measured the palette, found that
both `broodline_accessibility.md` §4 and the Phase 8 plan had ranked it by the
wrong quantity, and moved exactly one colour:

> **Pale: `#a9b0c4` → `#c6cede`** — in `Tokens.uss` (`--slate`) and in
> `PaletteContrast.Species`, which `PaletteContrastTests` asserts cannot drift
> apart.

Nothing else moved. **One decision is still open and is deferred to Task 13** —
see "The collision no simulation can see", below.

`bible §1.2`, `accessibility.md §4` and the design handoff README all still carry
`#a9b0c4` and are stale on that one row until **Task 19** records this. The token
layer is the live value.

---

## How to read the numbers

Every pair of species colours was rendered as a colour-blind player would see it,
then compared. The comparison is **ΔE** — one number for *how different two
colours look*:

| ΔE | means |
|---|---|
| ~2.3 | the smallest difference anyone can see at all |
| under 10 | **similar at a glance** — the pair to worry about |
| 10–25 | clearly different, but related |
| over 25 | obviously different colours |

The baseline also records **ΔL\***, *brightness difference only*. It is kept
because it is what survives if colour is removed entirely, but **ΔL\* must not be
read as "how confusable"** — see "The metric trap".

Three deficiencies at full severity: **protan** (red-blind, ~1% of men),
**deutan** (green-blind, ~6% of men), **tritan** (blue-blind, rare — and the
least trustworthy, see Caveats).

---

## Where the palette stands now

All 45 comparisons are in `palette-cvd-baseline.txt`. The tightest pairs, worst
first:

| Pair | Vision | ΔE | ΔL\* | |
|---|---|---|---|---|
| **Vetch / Loam** | tritan | **10.5** | 7.5 | the new worst pair — and a tritan number, so weak evidence |
| **Ember / Loam** | deutan | **11.0** | 3.7 | the one pair §4 correctly identified |
| Ember / Loam | protan | 15.6 | 13.8 | |
| Vetch / Pale | protan | 17.3 | 15.5 | **was 7.2 — this is what the change bought** |
| Ember / Skitter | tritan | 20.1 | 9.8 | |
| Vetch / Pale | deutan | 21.9 | 18.3 | was 12.4 |
| Loam / Pale | tritan | 24.9 | 9.5 | |
| Loam / Pale | deutan | 26.8 | 10.1 | |

Everything else is above ΔE 28.

### Before, and why it moved

Vetch and Pale were the palette's worst pair at **ΔE 7.2** under protanopia — and
also its closest pair at **normal vision (17.7, where no other pair was under
43)**. They were too close for *everyone*; red-blindness only sharpened an
existing weakness. After the move they are **17.3** and **23.9** respectively, and
Pale has left the worst-pair position entirely.

### What it cost

The gate named the whole price before the baseline was re-pinned. Five
measurements got worse:

| Pair | Vision | | |
|---|---|---|---|
| Loam / Pale | deutan | ΔE 27.0 → 26.8 | |
| Loam / Pale | protan | ΔE 33.9 → 32.6 | |
| Skitter / Pale | deutan | ΔE 70.6 → 68.3 | and ΔL\* 6.2 → 4.5 |
| Skitter / Pale | protan | ΔE 71.4 → 69.9 | |

Every one of these was already far above the confusable threshold and still is —
the lowest is 26.8, against a "worry" line of about 10. **The trade is 0.2–2.3 ΔE
on four comfortable pairs, for 10.1 ΔE on the worst pair in the palette.**
Brightness separation also improved sharply across the Pale pairs, several from
near-zero: `Loam/Pale deutan` ΔL\* 0.7 → 10.1, `Skitter/Pale protan` 0.6 → 10.1.

### Why it stops here

**ΔE 10.5 is a ceiling, not a compromise.** Deleting Pale from the palette
outright would *also* leave a worst pair of 10.5, because the binding constraint
is now Vetch/Loam, which Pale cannot affect. All three candidates considered
(`#bfc8dc`, `#c6cede`, `#d8dce8`) landed on exactly 10.5, which is the cleanest
evidence there is that the remaining constraint is structural and the choice
between them was an art call. Roughly ΔE 10–12 is the floor for six saturated
hues in this family however they are moved.

`#c6cede` was chosen as the middle candidate: the same periwinkle-grey hue family
as before, about one step lighter (L\* 72 → 83), still reading as a pale cool
neutral rather than as white.

---

## The metric trap — why both documents went after the wrong pairs

§4 and the Phase 8 plan both ranked the palette by **brightness difference
(ΔL\*)**. Brightness does not rank confusability; here it *inverts* it:

| Pair | ΔL\* | ΔE | What it actually is |
|---|---|---|---|
| Vetch / Ember, tritan | **0.1** | **83.2** | Identically bright, completely different. Not confusable at all |
| Skitter / Pale, protan | **0.6** | **71.4** | Same. The plan nominated this "the worst pair in the palette" |
| Vetch / Pale, protan | 4.8 | **7.2** | Unremarkable brightness gap. **Was genuinely the hardest pair** |

Ranking by brightness put two of the palette's *best*-separated pairs at the top
of the worry list and left the real one unmentioned by both documents. Finding it
is what moved Pale.

### Scorecard for `broodline_accessibility.md` §4

| §4's claim | Verdict |
|---|---|
| "Ember and Loam converge" | **Correct.** ΔE 11.0 deutan — now the second-worst pair, and untouched |
| "Vetch and Hollow converge" | **Wrong.** ΔE 26.9 / 32.9 — comfortably separated |
| "Under tritanopia, Skitter and Loam move closer" | **Wrong.** ΔE 63.0 — among the *best*-separated pairs there is |
| "Widen the lightness separation between the two collapsing pairs" | **Wrong lever, wrong pairs** — costed as option B below and retired |
| "It must happen before the Character Bible is finalised" | **Correct, and it happened inside that window** |

§4 was one-for-three on the pairs and wrong about the remedy, but right that
there was a problem and right about the deadline.

---

## The options, as costed

Kept as a record so nobody re-derives them.

**A. Leave it.** Zero cost, worst pair stays 7.2. Not chosen, but it was defensible:
bible §10.4 already refuses to let colour carry information alone, and §10.2 rule 1
makes all six distinguishable as flat black shapes at 40px.

**B. What §4 asked for — move Skitter and Loam.** `#cc901a` and `#9cd2ac`. Two
colours spent and **the worst pair stays at 7.2**, because it touches neither
Vetch nor Pale. Regresses nineteen measurements including Ember/Skitter ΔE
20.1 → 13.4. **Retired. Do not revisit.**

**C. Global repaint — five of six.** Worst pair → 11.5, one point better than what
was done, at five colours and the palette's character. It also leaves Vetch/Pale
binding, because it too was designed against the wrong ranking.

**D. Move Pale only. ← CHOSEN.** One colour, worst pair → 10.5, i.e. 91% of option
C's improvement for one colour instead of five. `--slate` was referenced by
nothing and no species colour was applied anywhere in the client, so it cost no
code at all. That is why it was done now rather than deferred: it will never be
cheaper.

---

## The collision no simulation can see — RESOLVED 2026-09-23 (Phase 10 Task 1.1)

**Resolved by moving the two UI roles, not the two species.** The action violet
is now `--action` / `--violet` `#6b4ec2` (Hollow stays `#7a6ac0`) and rewards
have their own gold, `--reward` `#e9b44c` (Skitter stays `#e8b34a`, one step
off, and `--amber` remains Skitter's). The six identities now live on
`--species-*` tokens, which `PaletteContrast.TokenIdentities` pins; the two
former collisions are pinned in the other direction by
`PaletteContrast.ResolvedCollisions`, so the test fails if either role token
ever equals its species again. The section below is kept as written, because
the reasoning is why the roles moved rather than the species.

### As recorded before the resolution

**Not fixed here, and deliberately so.** Two species share their *exact* hex with
a token that names a UI role:

| Species | Token | The token's job | Where it renders today |
|---|---|---|---|
| **Hollow** | `--violet` | primary brand, every CTA | `Theme.uss` Button, `TraitPip.uss`, `LineageView` *mutated* node |
| **Skitter** | `--amber` | warnings, Apex/gold | `CreatureCard` *Founder* border, `LineageView` *founder* node |

These are **ΔE 0.0 — identical, for every player**, and no colour-blindness
simulation will ever flag them, because they are not confusions between two
species. They are one species being indistinguishable from interface chrome.

**The concrete case is `LineageView`.** On that screen a *founder* node is
amber-bordered and a *mutated* node is violet-bordered — which are exactly
Skitter's and Hollow's colours. A Skitter that is not a founder and a founder that
is not a Skitter would carry the same amber on the same element.

`CreatureCard.uss` shows how easily this hides. Its Founder rule reasons carefully
about collisions — it picks amber over the aberrant treatment, noting that bible
§10.4 keeps aberrant white because *"Keeping it white rather than gold avoids
colliding with the map's gold Apex Vein pulse"* — and then uses, for the Founder
marker, the colour that **is** Skitter. The author was actively checking for
clashes with gold and did not see the species sitting in it.

### Why it waits for Task 13

Task 13 is the first point at which a species colour is applied to a real surface:
it draws the six proxies flat black and has `CreatureCard` tint them via
`-unity-background-image-tint-color`. **Deciding this against a rendered creature
card and a rendered `LineageView` node is worth more than deciding it against a
hex table**, and it costs nothing to wait, because nothing references either
colour *as a species* today.

The decision at Task 13 is: either the two species colours move, or the two UI
roles do. `PaletteContrastTests` pins all six species↔token identities against
`Tokens.uss` in the meantime, so neither can be silently erased and no third can
appear.

---

## Caveats

- **Tritan rows are weaker evidence, and this now matters more.** Machado's
  protan and deutan matrices at severity 1.0 are true rank-2 projections
  (determinant 0.0000 — the mathematical signature of a dichromat). The tritan
  matrix is not (determinant 0.2356); it is an extrapolation. **The new worst
  pair, Vetch/Loam at 10.5, is a tritan number**, so the palette's current
  binding constraint rests on the least trustworthy of the three simulations.
- **ΔE76, not ΔE2000.** Plain distance in CIE L\*a\*b\*, used to *rank* pairs
  rather than certify a threshold. ΔE2000 would shift individual numbers slightly
  and is far more error-prone to implement — a poor trade for a ranking.
- **A two-colour option (moving Pale *and* Vetch) was probed and is not recorded
  as an option.** A coarse search suggested ≈11.0, barely above 10.5, but it did
  not run to completion. Indicative only; it was not costed and should not be
  cited as if it were.
- **No colour-blind person has looked at this palette.** Every number here comes
  from a model of an average dichromat. The models disagree with each other and
  with real viewers, and none of them has an opinion about a creature card at
  arm's length on a phone. **Task 18's playtest is the first real check**, and
  until it happens this whole document is a well-measured prediction rather than
  a finding.
