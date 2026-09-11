---
status: current
folder: 01-companions
note: >
  The audit, four gaps, the settings list, the palette finding.
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
| Pale | `#a9b0c4` |

**Under deuteranopia and protanopia, two pairs collapse:** Ember and Loam converge, and Vetch and Hollow converge. **Under tritanopia**, Skitter and Loam move closer.

**This is survivable and it should still be improved.** The design's protection is real — bible §1.2 makes every silhouette distinct at 40px, so a player who cannot separate Ember from Loam by colour separates them by shape immediately. **Colour is a shortcut, and the shortcut simply does not work for some players.**

> **Widen the lightness separation between the two collapsing pairs.** Ember and Loam should differ in value as well as hue, and so should Vetch and Hollow. That preserves the palette's character and restores the shortcut.

**It is a small change and it must happen before the Character Bible is finalised**, because species colour is on the body, the card, the map marker and the store icon.

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

1. **The palette fix at §4 needs an illustrator's judgement, not a formula.** Widening lightness separation while keeping six colours that read as one family is a design problem, and it should be solved before the Character Bible is signed off rather than after.
2. **Nothing here has been tested with anyone.** Every item is derived from guidance and from reading the design, which is the cheap eighty percent. The remaining twenty needs players, and it should happen at soft launch rather than at submission.
3. **Screen-reader support is not addressed at all.** A real-time combat game is a hard case and full VoiceOver support may not be achievable. **The menu layer is achievable and it is most of the app** — roster, Codex, Gene Lab, store and settings are all static lists. Deciding to do the menus properly and to be honest about combat is better than attempting everything and shipping it half-working.
4. **The 44pt minimum interacts with four-pocket Delta terrain on a small phone.** Four pockets across three lanes in portrait is twelve targets plus the lanes themselves, and the maths may not close on the smallest supported device. It should be checked against a real screen before Delta terrain is built.

---

*Owns: the accessibility audit, the four gaps, the settings list and the palette finding. Does not own: the art direction (bible §10), audio (`broodline_audio.md`), or layout (the screen inventory).*
