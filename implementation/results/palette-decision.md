# The species palette — what was measured, and four costed options

**Nothing in this document has been applied.** Phase 8 Task 6 changed no species
colour. It built the measurement, pinned it as
`implementation/results/palette-cvd-baseline.txt`, and put the decision here.

---

## How to read the numbers

Every pair of species colours was rendered as a colour-blind player would see
it, then compared. The comparison is **ΔE** — one number for *how different two
colours look*:

| ΔE | means |
|---|---|
| ~2.3 | the smallest difference anyone can see at all |
| under 10 | **similar at a glance** — the pair to worry about |
| 10–25 | clearly different, but related |
| over 25 | obviously different colours |

The baseline also records **ΔL\***, which is *brightness difference only*. It is
kept because it is what survives if colour is removed entirely, but **ΔL\* must
not be read as "how confusable"** — see "The metric trap" below, which is the
single most important finding here.

Three kinds of colour blindness were simulated at full severity: **protan**
(red-blind, ~1% of men), **deutan** (green-blind, ~6% of men), **tritan**
(blue-blind, very rare — and the least trustworthy of the three, see Caveats).

---

## The measurements

All 45 comparisons are in `palette-cvd-baseline.txt`. These are the ones that
matter — every pair scoring under ΔE 30, worst first:

| Pair | Vision | ΔE | ΔL\* | Reading |
|---|---|---|---|---|
| **Vetch / Pale** | protan | **7.2** | 4.8 | **The worst pair in the palette.** Named by nobody until now |
| Vetch / Loam | tritan | 10.5 | 7.5 | Similar at a glance |
| **Ember / Loam** | deutan | **11.0** | 3.7 | The one pair the design correctly identified |
| Vetch / Pale | deutan | 12.4 | 7.6 | The worst pair again, under a commoner deficiency |
| Ember / Loam | protan | 15.6 | 13.8 | Related but distinguishable |
| Ember / Skitter | tritan | 20.1 | 9.8 | Fine |
| Loam / Pale | tritan | 23.0 | 1.2 | Fine |
| Hollow / Pale | tritan | 23.1 | 21.4 | Fine |
| Vetch / Pale | tritan | 25.8 | 6.4 | Fine |
| Vetch / Hollow | deutan | 26.9 | 14.6 | Fine — **the design says this one collapses. It does not** |
| Loam / Pale | deutan | 27.0 | 0.7 | Fine |
| Ember / Pale | protan | 28.7 | 11.1 | Fine |

Everything else scores above ΔE 30. The best-separated pair is Skitter/Hollow at
ΔE 107.

**Vetch and Pale are also the closest pair at normal vision** — ΔE 17.7, where
every other pair in the palette is above 43. So this is not really a colour-blindness
problem. It is a pair of colours that are too close for *everyone*, and red-blindness
sharpens an existing weakness rather than creating a new one.

---

## The metric trap — why the design's and the plan's conclusions were both wrong

Both `broodline_accessibility.md` §4 and the Phase 8 plan ranked the palette by
**brightness difference (ΔL\*)**. Brightness difference does not rank
confusability — here it *inverts* it:

| Pair | ΔL\* | ΔE | What it actually is |
|---|---|---|---|
| Vetch / Ember, tritan | **0.1** | **83.2** | Identically bright, and *completely* different colours. Not confusable at all |
| Skitter / Pale, protan | **0.6** | **71.4** | Same. The plan nominated this as "the worst pair in the palette" |
| Vetch / Pale, protan | 4.8 | **7.2** | Unremarkable brightness gap. **Genuinely the hardest pair to tell apart** |

Ranking by brightness put two of the palette's *best*-separated pairs at the top
of the worry list, and left the actual worst pair unmentioned by both documents.

### Scorecard for `broodline_accessibility.md` §4

| §4's claim | Verdict |
|---|---|
| "Ember and Loam converge" | **Correct.** ΔE 11.0 under deutan — the second-worst pair |
| "Vetch and Hollow converge" | **Wrong.** ΔE 26.9 / 32.9 — comfortably separated |
| "Under tritanopia, Skitter and Loam move closer" | **Wrong.** ΔE 63.0 — among the *best*-separated pairs there is |
| "Widen the lightness separation between the two collapsing pairs" | **Wrong remedy.** Lightness is the wrong lever; and applying it (option B) leaves the real problem untouched |
| "It must happen before the Character Bible is finalised" | **Still true, and the window is still open** — see Cost, below |

§4 was one-for-three on the pairs, and its prescription does not address the pair
it should have found. But it was *right that there is a problem* and right about
the deadline.

---

## The two collisions no simulation can see

Four species share their exact colour with a design token that names them, which
is intended. **Two share their exact colour with a token that names a UI role**,
which is not:

| Species | Token | The token's job | Where it renders today |
|---|---|---|---|
| **Hollow** | `--violet` | primary brand, every CTA | `Theme.uss` Button, `TraitPip.uss`, `LineageView` "mutated" node |
| **Skitter** | `--amber` | warnings, Apex/gold | `CreatureCard` "Founder" border, `LineageView` "founder" node |

A Hollow creature is the same violet as every call-to-action button in the game.
A Skitter is the same amber that marks a Founder. These are **ΔE 0.0 — identical,
for every player, colour-blind or not** — and no colour-blindness simulation will
ever flag them, because they are not confusions between two species. They are one
species being indistinguishable from interface chrome.

`LineageView` is where this bites hardest: on that screen a *founder* node is
amber-bordered and a *mutated* node is violet-bordered, which are exactly
Skitter's and Hollow's colours. `CreatureCard.uss` even carries a comment
reasoning about avoiding a collision with "the map's gold Apex Vein pulse" —
while using, for the Founder marker, the colour that *is* Skitter.

`PaletteContrastTests` now pins all six identities against `Tokens.uss`, so a
future edit cannot silently erase one or create a third.

---

## Cost: this is cheaper than anyone has assumed

**No species colour is applied anywhere in the client today.** There is no
species → colour mapping in C#, no species class in any stylesheet, and
`--slate` — Pale's colour — is referenced by zero `var()` calls. The four
species-named tokens that *are* used (`--teal`, `--coral`, `--green`, `--amber`)
are all being used for their **UI role**, not for a species.

So the code cost of changing any species colour right now is **zero**. The cost
is entirely downstream — Character Bible, creature art, map markers, store icons
— which is precisely the window §4 said to act inside, and it has not closed.

---

## The options

### A. Leave it. Rely on silhouette.
- **Changes:** nothing.
- **Costs:** nothing.
- **Risk:** the worst pair stays at ΔE 7.2 — Vetch and Pale look similar at a
  glance to a red-blind player, and fairly similar to everyone else.
- **What protects it:** bible §10.4 already says *"Colour never carries
  information alone"*, and §10.2 rule 1 makes all six distinguishable as flat
  black shapes at 40px, which Task 13 turns into an assertion. Colour is a
  shortcut; the shortcut is weak for one pair.

### B. The design's own prescription — move Skitter and Loam. **Do not do this.**
- **Changes:** Skitter `#e8b34a` → `#cc901a`, Loam `#7cc492` → `#9cd2ac`.
- **Costs:** two species colours.
- **Result:** the worst pair stays at **ΔE 7.2**, because this touches neither
  Vetch nor Pale. It improves Vetch/Loam (10.5 → 17.8) and Ember/Loam
  (11.0 → 17.8) but regresses Ember/Skitter (20.1 → 13.4).
- **Verdict:** two colours spent, the actual problem untouched. Recorded so that
  nobody re-derives it. This is what §4 asked for.

### C. Global repaint — re-solve five of the six.
- **Changes:** Vetch → `#a7cad9`, Skitter → `#b27e17`, Hollow → `#443681`,
  Loam → `#337045`, Pale → `#e1e3ea`.
- **Costs:** five species colours, and the palette's character. §4's promise that
  a fix "preserves the palette's character" is the thing that fails here.
- **Result:** worst pair **ΔE 11.5**.
- **Verdict:** the most improvement available, at much the highest price — and
  note it leaves Vetch/Pale as the binding pair anyway, because it was designed
  against the wrong ranking.

### D. Move Pale only. **Recommended if anything moves at all.**
- **Changes:** one colour — Pale, whose token `--slate` is referenced by nothing.
- **Costs:** the cheapest change available in the palette. No stylesheet, no code.
- **Result:** worst pair **ΔE 10.5** — 91% of option C's improvement for one
  colour instead of five.
- **Why it stops at 10.5:** deleting Pale from the palette *entirely* still
  leaves a worst pair of 10.5 (Vetch/Loam under tritan). Around ΔE 10–12 is
  simply the ceiling for six saturated hues in this family, however they move.
- **Candidates** — all of these reach the 10.5 ceiling, so the choice is an art
  call, not a maths one. Pale is currently `#a9b0c4` (L\* 72, a soft periwinkle
  grey); each of these is the same hue family, one to three steps lighter:

  | Candidate | L\* | Vetch/Pale protan | at normal vision |
  |---|---|---|---|
  | `#bfc8dc` | 80 | 7.2 → **14.6** | 17.7 → 21.8 |
  | `#c6cede` | 83 | 7.2 → **17.3** | 17.7 → 23.9 |
  | `#d8dce8` | 88 | 7.2 → **22.8** | 17.7 → 29.1 |

  `#bfc8dc` is the smallest move that reaches the ceiling; `#d8dce8` buys the
  most daylight against Vetch at the cost of reading closer to white.

---

## Recommendation

**Option D, with `#c6cede`** — or **A** if the art direction would rather not
move Pale at all.

The reasoning is cost, not urgency. The palette is not broken: even the worst
pair at ΔE 7.2 is visibly different side by side, and bible §10.4 already refuses
to let colour carry information alone. But moving Pale is the cheapest change
available anywhere in this palette — one token that nothing references, in a
codebase that applies no species colour yet — and it captures nearly all of the
improvement that a five-colour repaint would. If it is ever going to be done, it
is cheaper today than it will ever be again.

**Option B should be explicitly retired**, and `broodline_accessibility.md` §4's
final paragraph replaced with whatever is decided here. Task 19 owns that edit.

**The two role collisions (Hollow/`--violet`, Skitter/`--amber`) are a separate
decision** and a more concrete one: they are exact, they affect every player, and
`LineageView` renders both against species content. They are not fixed by any of
A–D and want their own call — either the species colours move, or the UI roles do.

**This decision gates Phase 9**, because species colour goes on the body, the
card, the map marker and the store icon. It does not gate the rest of Phase 8.

---

## Caveats

- **Tritan rows are weaker evidence.** Machado's protan and deutan matrices at
  severity 1.0 are true rank-2 projections (determinant 0 to six decimals — the
  mathematical signature of a dichromat). The tritan matrix is not
  (determinant 0.236); it is an extrapolation. Treat tritan-only findings — which
  includes Vetch/Loam at 10.5, the pair that sets option D's ceiling — as
  indicative rather than settled.
- **ΔE76, not ΔE2000.** ΔE76 is plain distance in CIE L\*a\*b\*. It is used to
  *rank* pairs, not to certify a threshold; ΔE2000 would shift individual numbers
  slightly and is far more error-prone to implement, which is a poor trade for a
  ranking.
- **Simulation is not a player.** These matrices model average dichromats. Nobody
  colour-blind has looked at this palette. Task 18's playtest is the first chance.
