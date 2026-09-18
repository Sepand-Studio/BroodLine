# Two species wear two interface colours — rendered, measured, and settled

> ## The decision
>
> **The two species colours are fine; `LineageView` is what needed a second
> channel.**
>
> **Status: DECIDED AND APPLIED** (Phase 8 Task 13). **No colour moved** — not a
> species colour, not a token, not a UI role. `LineageView`'s nodes now state
> **Founder**, **Mutated** and **Consumed** in words beside the coloured rail, so
> none of the three rides on colour alone. Options C and D — repainting the UI
> roles, or repainting the two species — were costed and **rejected**; see "The
> options, as costed" below for why.
>
> **Task 19 owes the spec edits.** `bible §1.2`, `broodline_accessibility.md §4`
> and the design handoff README are stale on Pale's row (`#a9b0c4` → `#c6cede`,
> Task 6) and say nothing about this. Both records belong in the same sweep.

Phase 8 Task 6 deferred this to Task 13 so it could be decided against a picture
instead of a hex table. Task 13 drew the six proxies, tinted them, and captured the
screens the argument is about. **The picture changed the answer**: read off the hex
table this looks like a palette problem, and it is not — it is one screen carrying
two facts on colour and nothing else, which is a bible §10.4 violation that existed
before any species had a colour. `implementation/results/palette-decision.md` has
the other half of the story — the one colour that did move, and why.

Regenerate the pictures with `bash implementation/scripts/capture-screens.sh` and
look at `implementation/results/screens/RosterView.png` and `LineageView.png`. They
are not committed, by `results/`'s own rule: one command rebuilds them.

---

## The collision, restated

Two of the six species share their **exact hex** with a token that names a
job in the interface. Not similar — identical, ΔE 0.0, for every player,
colour-blind or not. No colour-blindness simulation will ever flag them, because
they are not two species being confused with each other.

| Species | Token | The token's other job |
|---|---|---|
| **Hollow** | `--violet` `#7a6ac0` | primary brand, every CTA, the *mutated* rail, the *highlight* fill |
| **Skitter** | `--amber` `#e8b34a` | warnings, Apex gold, the *Founder* card border, the *founder* rail |

**Already settled, do not reopen:** the founder rail and the mutated rail are not
confusable *with each other*. Measured off the pixel again this task —
`rgb(232,179,74)` against `rgb(123,106,192)`, warm gold against cool purple, both
plainly legible at 3px. That was never the problem.

---

## What the render actually shows

The important result is that **the severity depends entirely on whether the screen
draws a creature**, and the two screens split cleanly.

### On a creature card, it is mild — milder than the hex table implies

`RosterView` now carries one of each of the six. The Founder card (Ash, a Vetch)
wears a 2px amber border. The card immediately beside it holds a Skitter whose
body is amber.

**They do not read as the same signal.** One is a hairline frame around a whole
card; the other is a drawn animal inside a slot. Different shape, different place,
different weight. The eye reads "that card is picked out" and "that creature is
gold" and never has to choose between them. This is bible §10.4 working exactly as
written — *"Colour never carries information alone… species colour is reinforced by
silhouette"* — because the Founder marker is a border shape and the species is a
body shape.

The same is true wherever a `CreatureCard` appears: the roster, the splice chamber,
the reveal, the defeat screen, post-wave, founder naming.

### On the lineage tree, it is real — and worse than the hex table implies

`LineageView` draws **no creature at all.** A lineage node is a label, a state
word, and two trait pips. There is no silhouette slot on it. So on that screen
colour is not *reinforced* by shape — it is the **only** channel, and two separate
facts ride on it:

- **Founder** is carried by the amber rail and by nothing else. The node's label
  reads "Ash (G1)"; nothing says "Founder".
- **Mutated** is carried by the violet rail and by nothing else. The node's label
  reads "Hollow (G2)"; nothing says "Mutated".

Two consequences, both visible in one captured frame:

1. **"Skitter (G1)" sits 60px to the right of an amber rail that means Founder** —
   and the tile wearing that rail is a **Vetch**. A player who learned "gold spider
   = Skitter" from the roster has nothing on this screen to tell them the gold bar
   is not the same fact.

2. **The one node in the tree that is a Hollow is the one wearing violet** — because
   it is the mutated one. That coincidence will be read as a rule by anyone who
   sees it once.

And a third thing the plan predicted and the render confirms: **violet is doing
three jobs in that single frame** — the highlighted founder tile is filled
`--violet-tint`, the mutated rail 300px below is `--violet`, and every trait pip is
a violet-tint chip.

**So the finding is not really "two species collide with two tokens".** It is:
**`LineageView` carries two facts on colour alone, which bible §10.4 forbids
outright, and the two colours it chose happen to belong to two species.** The
collision is a symptom. The §10.4 violation is the defect, and it is a defect
whether or not a species ever gets a colour.

---

## A separate finding the hex table could not predict

Tinting the proxies made a **contrast** problem visible that no ΔE number was
looking for. Every species tint is a flat fill on the card's `--surface-sunk`
(`#f8f6fc`) silhouette slot:

| Species | Tint | Contrast vs the slot |
|---|---|---|
| Hollow | `#7a6ac0` | **4.21 : 1** — reads well |
| Vetch | `#6ba7c0` | 2.47 : 1 |
| Ember | `#e5867a` | 2.44 : 1 |
| Loam | `#7cc492` | 1.93 : 1 |
| Skitter | `#e8b34a` | 1.78 : 1 |
| **Pale** | `#c6cede` | **1.47 : 1 — effectively invisible** |

Measured on the capture: the entire Pale creature moves no channel by more than
**50 of 255**. It is a ghost on the card.

**This is a property of the proxies, not of the palette, and it did not drive the
decision above.** A flat one-colour silhouette has no internal value structure;
Phase 9's real art will have outline, shading and a darker core.

**It is recorded because it is a prediction about that art, and it belongs to
whoever draws it.** Whatever Phase 9 draws, a Pale creature at `#c6cede` on a
near-white card *cannot* read on hue alone — it will need its own value structure,
an outline, or a darker slot behind it. That is a constraint on the brief, not a
note on a proxy, and it is the kind of thing that is cheap now and expensive after
six species are modelled.

**And here is the part that stings.** Task 6 lightened Pale from `#a9b0c4` to
`#c6cede` and that was the right call: it took the palette's worst colour-blind
pair from ΔE 7.2 to 17.3 and moved the worst-pair position off Pale entirely, for
the price of 0.2–2.3 ΔE on four pairs that were already comfortable. It also took
Pale's contrast against the card from about 1.9:1 to **1.47:1** — because lightening
a colour on a near-white surface is the same operation as hiding it. **Both
decisions were correct against their own measurements.** Nobody was measuring
contrast-against-the-card in Task 6, because no species colour had been applied to
a surface yet; that only became measurable here, which is exactly why Task 6
deferred to a render. Two good measurements can still pull in opposite directions,
and this pair does.

---

## The options, as costed

### A. Leave it. Change nothing.

**Cost:** zero.
**Buys:** nothing.
**Risk:** the lineage tree keeps carrying Founder and Mutated on colour alone,
which is a bible §10.4 violation on its own terms, and the two colours it uses will
be the two species a player has learned from every other screen.
**Defensible if:** the lineage tree is judged a low-traffic screen and Phase 9's
real art is expected to make species unmistakable by shape everywhere. It is not
defensible as a *palette* answer, because the palette is not what is wrong.

### B. Give the lineage tree a second channel. ← CHOSEN AND APPLIED

Stop the rail being the only thing that says Founder or Mutated. A word, a glyph,
or a rail weight — the existing node already has a state line that says "Consumed",
and "Founder" and "Mutated" belong in the same place. The rails can then keep their
colours, because colour is no longer carrying the fact alone.

**Cost:** one screen. Two USS rules and roughly six lines in `LineageView.cs`,
plus its existing tests. Under an hour.
**Buys:** it fixes the §10.4 violation, which is the actual defect, and the species
collision stops mattering as a side effect — amber can mean Founder on a screen
where the word "Founder" is also present.
**Risk:** low. `LineageView.uss` is the only consumer of those two rules. Nothing
in the token layer moves, so `PaletteContrastTests` and the CVD baseline are
untouched, and Phase 9's art is unaffected.
**Does not fix:** violet doing three jobs in one frame. That is a separate, milder
tidy-up on the same screen and can ride along.

### C. Move the two UI roles to different colours.

Founder stops being amber; mutated stops being violet.

**Cost:** there is no spare hue. All six of the palette's colours are species and
the seventh is the brand violet, so this needs a **new colour that is not in the
design handoff** — the thing `Tokens.uss` exists to prevent, and which needs an
argument of its own. Roughly four USS rules plus a token, plus that argument.
**Buys:** removes the shared hex outright.
**Risk:** medium. A seventh hue has to survive the same colour-blindness
measurement the six just went through, against six existing colours. And it still
leaves the lineage tree carrying two facts on colour alone — it just changes which
colours.

### D. Move the two species colours.

Hollow off `--violet`, Skitter off `--amber`.

**Cost:** two of the six species colours — a third of the palette, and the two
Task 6 deliberately did not move.
**Buys:** removes the shared hex outright, from the species side.
**Risk:** **high, and it is the option to avoid.** Task 6 measured that roughly
ΔE 10–12 is the floor for six saturated hues in this family however they are
arranged, and spent one colour to reach it. Moving two more re-opens that
measurement, requires re-running and re-pinning the colour-blindness baseline, and
spends a third of the species palette on a problem that only actually bites on one
screen. It also has to happen before Phase 9's art is built, which is the most
expensive possible moment.

---

## What was actually built

**B, as words.** Glyphs were considered and rejected: `icons.uss` has thirteen
names, but `splice` is already this screen's empty-state mark and `tier` is the
codex sheet's, so either would have given one glyph a second meaning — the same
shape of defect being fixed. A word needs no legend, reads at 11px where a 12px
glyph does not, and the screen already said **Consumed** in words. The other two
now follow the pattern `LineageScreen.ConsumedLabel`'s own comment had described
all along: *"the screen says so in words as well as in a class name."*

- `LineageView.MarksFor` builds a `node__marks` row carrying **Founder**,
  **Mutated**, **Consumed** — in that order, because that is how a creature's
  history reads, and a node can carry more than one.
- The row is **built only when it has something in it.** A node with nothing true
  about it gets no element. The `state` Label this replaced was the eighth
  instance of this phase's blank-banner shape — constructed for every node,
  emptied for the living ones. It measured zero height and was not a defect, but
  it was not worth rebuilding next to where it had just been diagnosed.
- All three marks are **one neutral ink**. Colouring "Founder" amber would put the
  fact straight back on the colour it was just taken off.

**Two things were measured rather than assumed**, both because the coordinator
asked for them:

1. **Legibility.** The marks were `--mute` for one capture. That made the one fact
   the screen already stated in words *harder* to read than before — a consumed
   node carries `opacity: 0.55`, which multiplies whatever ink is set, and the
   darkest pixel of "Consumed" went from 181 to 208 of 255. A mark standing in for
   a colour cannot be quieter than the colour was. Changed to `--ink`, which
   restores it to exactly 181.
2. **Vertical rhythm.** Before, a living node and a consumed node differed by one
   line (text bands at y 245/292/326 against 245/276/309/343). After, every tile in
   the row carries four bands at the same rows — 244/279/310/344 — because each one
   now has exactly one mark. The rhythm got *more* uniform, not less.

**The acceptance test, run:** the capture was converted to greyscale, so the amber
and violet rails become indistinguishable grey bars. Every node still reads:
Ash — **Founder**, Skitter — **Consumed**, Pale — **Consumed**, Hollow —
**Mutated**. The rails are now reinforcement rather than the signal.

`FirstHourScreensTests.Lineage_StatesEveryFactInWordsAndNotOnlyInColour` asserts
it, and was seen to fail twice before being trusted: once with the words removed
("Founder is carried by the rail's colour and by nothing else"), and once with only
the empty-row guard removed ("a node with nothing to say built a marks row anyway —
that is the ninth blank banner"). A rule asserted only by a screenshot is not
asserted; if that test is ever deleted to make a redesign pass, this decision comes
back open.

---

## Caveats

- **Read from a static 430×932 capture on a desktop display**, not from a phone at
  arm's length, and not by a colour-blind viewer. Task 18's playtest is the first
  real check of any of this.
- **The judgement that the card case is "mild" is a judgement**, not a
  measurement. What is measured is that the two uses differ in form and position;
  that this is enough is an opinion formed by looking at the frame.
- **The lineage fixture is one tree.** That its single Hollow is also its single
  mutated node is a coincidence of this fixture — but it is the coincidence a real
  tree can produce at any time, which is the point.
- **The contrast table is arithmetic on the tokens**, not a reading off the
  screen — except the Pale figure, which was read off the capture (50 of 255, worst
  channel) and agrees.
