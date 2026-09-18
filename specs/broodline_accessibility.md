---
status: current
folder: 01-companions
note: >
  The audit, four gaps, the settings list, the palette finding — §4 CORRECTED
  2026-09-17 against measurement; it had named the wrong pairs and prescribed
  the wrong remedy. See §4.1.
---

# Broodline — Accessibility

*Design spec, who can play this*

> **CURRENT — companion to the design bible.** Flagged as unchecked in
> `broodline_audio.md` §13.4. This is the check.

**The design does unusually well by accident**, because three unrelated decisions — silhouette-first art, muted-play assumption, and colour never carrying information alone — happen to be accessibility decisions. §2 names what is already true so it does not get traded away later. §3 is the four things that are actually wrong.

---

## 1. Why this is written now rather than at submission

Accessibility retrofitted is expensive and usually shallow. **Every item in §3 is cheap right now and structural later**, because none of the screens exist yet — fifty-nine designed, eight built.

It is also a store consideration. Apple surfaces accessibility metadata on product pages, and the four gaps below are the ones a reviewer or a player would find first.

---

## 2. What is already true

**Stated so it does not get lost.** Each of these came from a different design decision for a different reason.

| | Because |
|---|---|
| **Fully playable deaf** | `broodline_audio.md` §2 assumes most sessions are muted, so every audio cue has a visual equivalent and nothing exists only in sound |
| **Colour is never load-bearing** | Bible §10.4 — pip count works in greyscale, species colour is reinforced by silhouette |
| **Roles readable at 40px** | Bible §10.2 rule 1 requires all six species distinguishable as flat black shapes. That is a low-vision win produced by a legibility requirement |
| **Numbers do not jitter** | Bible §10.6's tabular-figure rule, written for trust rather than for readability, is both |
| **Missing a timed prompt costs nothing** | The ninety-second raid alert auto-resolves identically. `broodline_notifications.md` §3 — live defence is a bonus, not the mode |
| **Failure is free** | No attempt limits, no energy on failure, no paywall. A player who needs twenty attempts pays the same as one who needs two |
| **Layouts already flex** | `broodline_localization.md` §8 requires designing to +40% text expansion for German. That is most of the work for dynamic type |

**The last one is worth noticing.** Localization and accessibility want the same thing from layout, and doing either properly delivers most of the other.

---

## 3. The four gaps

### 3.1 The Aberrant marker is motion

Bible §10.4 gives Aberrants "an iridescent white-hot treatment with subtle motion — the only animated treatment in the system."

**Motion is the distinguishing channel**, and a player with reduced-motion enabled loses the only marker that separates the game's rarest object from an ordinary trait.

> **The Aberrant marker needs a static form that is distinguishable without animation.** A distinct outline or glyph, not a brighter version of the same pip.

Bible §10.4 already says an Aberrant is "a distinct marker rather than a fourth pip" — the static form has to honour that, and the animation becomes an enhancement rather than the identity.

### 3.2 Colour-only information on the map

Bible §10.4's rule is that colour never carries information alone, and creature cards honour it. **The map does not.**

| Surface | Currently | Needs |
|---|---|---|
| Node richness heat-map | Colour intensity | A pattern or a numeric tier |
| Territory banners | Alliance colour | A banner shape or an initial |
| Ark markers — own, ally, hostile | Colour | Distinct marker shapes |

**The map is the hardest layout problem in the app already** — `broodline_screen_inventory_v2.md` §12 says so — and it is the screen where colour is doing the most work. Adding a second channel to three overlays is cheap now and a redesign later.

### 3.3 Placement is drag-and-drop on a small target

Combat placement puts five creatures into four to six pockets, in portrait, one-handed. **Drag-and-drop with small targets is the least accessible common mobile interaction** and it is the game's core input.

> **Tap-to-select then tap-to-place must work everywhere drag works.** Same outcome, no drag required.

That is also better for everyone in a moving vehicle, which is a meaningful share of ninety-second sessions.

**Pocket targets get a minimum 44pt hit area** regardless of their visual size, per platform guidance.

### 3.4 Two screens are text-heavy under time pressure

The pre-wave composition panel and the Wave Defeat diagnosis both carry a lot of meaning in a short window.

**Neither is genuinely timed** — the pre-wave panel waits for the player and Wave Defeat has no countdown. **So the fix is to make sure they stay that way**, and to resist any future decision to auto-advance either.

**Wave Defeat's three-state diagnosis at bible §4.11 must not be the only place a player can learn why they lost.** It should be re-readable from the replay, at the player's own pace, for as long as the replay is retained.

---

## 4. The species palette

Six colours, and they carry no information alone — but they are still meant to help.

| Species | Hex |
|---|---|
| Vetch | `#6ba7c0` |
| Ember | `#e5867a` |
| Skitter | `#e8b34a` |
| Hollow | `#7a6ac0` |
| Loam | `#7cc492` |
| Pale | `#c6cede` |

> **Pale moved, 2026-09-17.** `#a9b0c4` → `#c6cede`, in Phase 8 Task 6. It is the
> only species colour this project has changed. `client/Assets/UI/Shell/Tokens.uss`
> (`--slate`) and `PaletteContrast.Species` are the live values, and
> `PaletteContrastTests` asserts the two cannot drift apart. The reasoning is in
> `implementation/results/palette-decision.md`.

**This is survivable, it has been improved once, and it is now close to its
floor.** The design's protection is real — bible §1.2 makes every silhouette
distinct at 40px, so a player who cannot separate Ember from Loam by colour
separates them by shape immediately. **Colour is a shortcut, and the shortcut
simply does not work for some players.** What changed is that the shortcut is now
measured rather than assumed, and the measurement says something different from
what this section used to.

### 4.1 What this section used to say, and what measurement found

**This section named the wrong pairs and prescribed the wrong remedy.** It is
corrected here rather than quietly rewritten, because the paragraph it replaces
was acted on as if it were a finding for two phases.

The paragraph read: *"Under deuteranopia and protanopia, two pairs collapse:
Ember and Loam converge, and Vetch and Hollow converge. Under tritanopia,
Skitter and Loam move closer."* It then prescribed widening the lightness
separation of those pairs, and closed with *"It is a small change and it must
happen before the Character Bible is finalised."* **All four sentences are
superseded, and the last is deleted from the body outright.**

**It is deleted for the first half of it, not the second.** *"It is a small
change"* is false as written: the change it describes — widening the lightness
separation of Ember/Loam and Vetch/Hollow — was costed, measured, and found to
spend two colours while leaving the worst pair exactly where it was. That change
does not exist. *"It must happen before the Character Bible is finalised"* was
**correct**, and it is why the work was scheduled into Phase 8 at all rather than
Phase 9, which generates bodies. It happened inside that window. A half-true
sentence is deleted rather than trimmed because the true half is now carried by
§4.3, where it belongs — as a constraint on the art brief rather than as a
deadline that has already passed.

Every number below is measured, at severity 1.0, in **linear** RGB, and lives in
`implementation/results/palette-cvd-baseline.txt` — all 45 pair/deficiency
measurements, regenerated by `PaletteBaselineWriter.Write`.

| This section's claim | Measured | Verdict |
|---|---|---|
| "Ember and Loam converge" | ΔE 11.0 deutan | **Correct.** The palette's second-tightest pair, and untouched |
| "Vetch and Hollow converge" | ΔE 26.9 deutan, 32.9 protan | **Wrong.** Comfortably separated |
| "Under tritanopia, Skitter and Loam move closer" | ΔE 63.0 tritan | **Wrong.** Among the *best*-separated pairs there is |
| "Widen the lightness separation between the two collapsing pairs" | costed as option B | **Wrong lever, wrong pairs.** Spends two colours, leaves the worst pair exactly where it was, and regresses nineteen measurements. **Retired — do not revisit** |

**Why it went wrong is worth more than the score.** This section ranked the
palette by **lightness difference (ΔL\*)**. ΔL\* does not rank confusability;
here it *inverts* it. Vetch/Ember under tritanopia is ΔL\* 0.1 and ΔE 83.2 —
identically bright, completely different colours, not confusable at all.
Ranking by brightness put two of the palette's best-separated pairs at the top
of the worry list and left the real one unmentioned.

**The real worst pair was Vetch/Pale, at ΔE 7.2 under protanopia** — and the
closest pair at normal vision too (17.7, where nothing else was under 43). They
were too close for *everyone*; red-blindness only sharpened a weakness that was
already there. Moving Pale took that pair to **17.3**, and the palette's binding
constraint is now **Vetch/Loam at ΔE 10.5 under tritanopia**, which Pale cannot
affect. Roughly ΔE 10–12 is the floor for six saturated hues in this family
however they are arranged, so 10.5 is a ceiling rather than a compromise.

**Read the tritan rows with more doubt than the other two.** Machado's protan
and deutan matrices at severity 1.0 are true rank-2 projections (determinant
0.0000, the signature of a dichromat). The tritan matrix is not — determinant
**0.236** — it is an extrapolation. The palette's current binding pair rests on
the least trustworthy of the three simulations.

### 4.2 Two species wear two interface colours, and no colour moved

**Settled in Phase 8 Task 13. The two species colours are fine; `LineageView` is
what needed a second channel.** Recorded here because the decision is invisible
in the palette table and would otherwise read as an oversight.

Hollow is **exactly** `--violet` `#7a6ac0` (the primary brand, every CTA) and
Skitter is **exactly** `--amber` `#e8b34a` (warnings, Apex gold, the Founder
marker). ΔE 0.0, for every viewer. No colour-blindness simulation will ever flag
this, because it is not a confusion between two species — it is one species being
indistinguishable from interface chrome.

**Both are intended, by decision rather than by omission.**
`PaletteContrast.TokenIdentities` records both identities so neither can be
silently erased and no third can appear. The defect the render actually exposed
was narrower and worse: `LineageView` draws no creature at all, so on that one
screen colour was the *only* channel and two facts rode on it. It now states
**Founder**, **Mutated** and **Consumed** in words beside the coloured rail, so
none of the three rides on colour alone — bible §10.4, satisfied by adding a
channel rather than by moving a hue.
`FirstHourScreensTests.Lineage_StatesEveryFactInWordsAndNotOnlyInColour` asserts
it; if that test is ever deleted to make a redesign pass, this decision reopens.

The two alternatives were costed and **rejected**. Repainting the UI roles needs a
seventh hue that is not in the design handoff and would have to survive the same
measurement the six just went through. Repainting the two species spends a third
of the palette, re-opens the floor Task 6 spent a colour reaching, and does it
immediately before Phase 9's art. Full reasoning:
`implementation/results/species-collision.md`.

### 4.3 A constraint on the art brief: Pale cannot read as one flat colour

**This is owed to bible §10.4 and to whoever draws the six species in Phase 9.**

Every species tint was measured as a flat fill on the creature card's
`--surface-sunk` `#f8f6fc` silhouette slot:

| Species | Tint | Contrast vs the slot |
|---|---|---|
| Hollow | `#7a6ac0` | **4.21 : 1** — reads well |
| Vetch | `#6ba7c0` | 2.47 : 1 |
| Ember | `#e5867a` | 2.44 : 1 |
| Loam | `#7cc492` | 1.93 : 1 |
| Skitter | `#e8b34a` | 1.78 : 1 |
| **Pale** | `#c6cede` | **1.47 : 1 — effectively invisible** |

Read off the capture, the entire Pale creature moves no channel by more than
**50 of 255**. It is a ghost on the card.

**This is not a defect in the interim proxies; it is a prediction about the real
art.** A flat one-colour silhouette has no internal value structure. Whatever
Phase 9 draws, **a Pale creature at `#c6cede` on a near-white card cannot read on
hue alone** — it will need its own value structure, an outline, or a darker slot
behind it. That is cheap to plan for now and expensive after six species are
modelled.

**And it is the direct cost of the improvement above.** Task 6's lightening of
Pale took the palette's worst colour-blind pair from ΔE 7.2 to 17.3, and took
Pale's contrast against the card from about 1.9:1 to 1.47:1 — because lightening
a colour on a near-white surface is the same operation as hiding it. **Both
decisions were correct against the measurements available at the time.**
Contrast-against-a-card was not measurable in Task 6, because no species colour
had landed on a surface yet; it only became measurable once the proxies were
drawn and tinted, which is precisely why Task 6 deferred to a render. Two good
measurements can pull in opposite directions, and this pair does.

**Still unchecked by anyone:** every number in §4 comes from a model of an
average dichromat. No colour-blind person has looked at this palette.

---

## 5. Settings

A short list. Every one of these is cheap because the underlying constraint already exists.

| Setting | Default | Notes |
|---|---|---|
| **Reduced motion** | System | Honours §3.1's static Aberrant marker; disables ambient map motion |
| **Larger text** | System | Layouts already flex to +40% for German |
| **Tap-to-place** | On | §3.3. Not a setting to find, a mode that always works |
| **Colour-blind assist** | Off | Applies §3.2's second channels to the map by default |
| **Reduce transparency** | System | The map's overlays are the surface that needs it |
| **Haptics** | On | `broodline_audio.md` §2 fires on exactly three events |

**No separate "accessibility mode."** A mode a player has to find and enable is a mode most of the players who need it never see. These are settings, and three of the six read from the system rather than asking.

---

## 6. What is deliberately not offered

**No difficulty setting.** Bible §4.4's counter model means a wave is answered by composition rather than by reflex, and failure costs only regeneration timers. A player who cannot beat a wave brings different creatures — that is the game, and an easy mode would replace it with a slider.

**No auto-play for campaign waves.** Auto-resolve exists for raid defence because the defender may be absent. Offering it for campaign would remove the only input the game asks for.

**No colour customisation.** Species colour is on the body and in the Character Bible; letting a player recolour it breaks the shared vocabulary the Codex and community depend on. §4's palette fix is the right answer instead.

---

## 7. Guardrails

- Every audio cue has a visual equivalent, and every colour cue has a second channel
- The Aberrant marker is distinguishable without motion
- Tap-to-place works everywhere drag works
- Pocket hit targets are at least 44pt
- No screen carrying a diagnosis auto-advances
- Reduced motion, larger text and reduced transparency read from the system by default
- No difficulty setting, no campaign auto-play, no colour customisation

---

## 8. Open questions

1. **CLOSED, 2026-09-17, and not the way this asked.** This read: *"The palette
   fix at §4 needs an illustrator's judgement, not a formula. Widening lightness
   separation while keeping six colours that read as one family is a design
   problem."* **Lightness was the wrong lever** — §4.1 — and widening it was
   costed and retired. A formula is what found the real worst pair, and one
   colour moved. **What remains genuinely open is narrower:** the palette is at
   roughly its floor (ΔE 10–12 for six saturated hues in this family), the
   binding pair rests on the weakest of the three simulations (§4.1), and
   **Pale now needs an illustrator's judgement for a different reason** — it
   cannot read as one flat colour on a near-white card (§4.3). That is the art
   question that is actually still owed.
2. **Nothing here has been tested with anyone**, and that is still true after §4
   was measured. Every item is derived from guidance and from reading the design,
   which is the cheap eighty percent. The remaining twenty needs players, and it
   should happen at soft launch rather than at submission. **§4's numbers do not
   change this**: they come from a model of an average dichromat, and no
   colour-blind person has looked at this palette. The first TestFlight build in
   a tester's hands is the first real check.
3. **Screen-reader support is not addressed at all.** A real-time combat game is a hard case and full VoiceOver support may not be achievable. **The menu layer is achievable and it is most of the app** — roster, Codex, Gene Lab, store and settings are all static lists. Deciding to do the menus properly and to be honest about combat is better than attempting everything and shipping it half-working.
4. **The 44pt minimum interacts with four-pocket Delta terrain on a small phone.** Four pockets across three lanes in portrait is twelve targets plus the lanes themselves, and the maths may not close on the smallest supported device. It should be checked against a real screen before Delta terrain is built.

---

*Owns: the accessibility audit, the four gaps, the settings list and the palette finding. Does not own: the art direction (bible §10), audio (`broodline_audio.md`), or layout (the screen inventory).*
