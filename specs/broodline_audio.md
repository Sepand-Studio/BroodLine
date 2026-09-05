# Broodline — Audio Direction

*Design spec, what the game sounds like*

> **CURRENT — companion to the design bible.** Bible §10 is entirely visual. The
> design's central claim is that combat is legible, and legibility has an audio
> half that nothing has taken a position on.

**One constraint governs everything below and it is uncomfortable.** Most sessions will be played muted — §2. Audio is therefore additive and never load-bearing, and the brief has to be written as though nobody hears it, while still being worth making for the players who do.

---

## 1. The division of labour

Bible §10.4 already splits visual channels cleanly: **the body says which, the card says how much.** Audio takes the third question.

> **The body says which. The card says how much. The sound says whether it is working.**

That is the gap. A player looking at a Hollow shooting a Breaker sees a Hollow shooting a Breaker. Whether the shot is landing for 55 or capping at 5 is invisible, and it is the single most important thing happening on the board.

---

## 2. Most sessions are muted

Ninety-second sessions, one-handed, in portrait, on a phone, frequently in public. **The realistic muted rate for this genre is high — well over half.**

> **Every audio cue has a visual equivalent. No information exists only in sound.**

That is not a limitation to work around, it is the brief. Audio's job is to make the game feel better for the players who hear it and to reinforce what is already visible — never to carry anything a muted player would miss.

**Two consequences.**

**The blocked-hit sound at §3 must be paired with a visual tell**, because it is the most informative moment in combat. A muted player needs to see that damage is capping.

**Haptics are the third channel and they survive muting.** A phone on silent still buzzes. The three moments worth a haptic are a mutation firing, an Aberrant appearing, and a breach — and there should be no others, because haptics stop meaning anything at the fourth.

---

## 3. The most important sound in the game

**Damage that is not working.**

A creature hitting a Breaker without Pierce, or a Bulwark without Sprint, connects and accomplishes almost nothing. The engine caps it or blocks it entirely. **That moment is where a player learns the counter system without being told**, and it currently has no representation at all.

| | |
|---|---|
| **Blocked** | Dull, short, wrong. A hit that stops. The absence of follow-through is the whole point |
| **Penetrated** | The same impact with the follow-through restored. Pierce or Sprint present |

**The pair must be recognisable as a pair.** A player should hear the difference before they can explain it, and then hear the difference disappear when they bring the right creature. That is the counter system taught by ear.

**It is also the sound most likely to become annoying**, since a Breaker takes sixty-eight seconds to cross and will absorb dozens of blocked hits. It needs to be quiet, short, and to duck under repetition rather than stacking.

---

## 4. Sounds by effect class, not by trait

Twelve traits do not need twelve sounds. **Eight effect classes cover all of them**, which keeps the library small and makes the classes learnable.

| Class | Traits | Character |
|---|---|---|
| **Blocked / penetrated** | Pierce, Sprint | §3 |
| **Splash** | Splash | One impact spreading, not several impacts |
| **Burn** | Cinder | A suppression, not an explosion — it stops a birth |
| **Slow** | Chill | A drag on an existing movement sound, not a new sound |
| **Pull** | Taunt | The Lash's attention changing target. Heard on the raider, not the creature |
| **Surface** | Burrow | Earth breaking. The loudest single effect in the set |
| **Reach** | Reach | An acquisition, not an impact — the sound of something becoming targetable |
| **Shield strip** | Sprint on Bulwark | Progressive, four steps, resolving into the shield falling |

**Chill and Taunt are modifiers on a raider's existing sound**, not new sounds layered on top. That is the difference between a soundscape and a pile.

---

## 5. Untargetable is silence

Drift overhead and Delver underground are both untargetable without their answer, and the engine has creatures simply not firing at them.

**That silence is the tell and it should not be filled.** A player whose creatures go quiet while something crosses the lane is being told something true.

**Delver is the exception and needs one sound.** `broodline_waves_37_44.md` §5 gives it a visible disturbance tracking up the lane, and a low sustained rumble underneath it is what makes the anticipation work. It is the only sound in the game with no impact attached — it is a threat approaching, and then it either surfaces or it reaches the Ark.

---

## 6. Instinct triggers

Bible §10.4 requires a visible trigger state when Last Stand fires or Skittish repositions. **Audio is the third channel and it is cheap here** — six short cues, one per Instinct, played on trigger rather than continuously.

| Instinct | Trigger |
|---|---|
| Last Stand | Below 25% HP — the attack speed change is audible on its own; the cue marks the moment |
| Skittish | Below 40% — a retreat, and the two seconds of not acting is a silence |
| Pack Sense | Adjacency formed — a pairing, heard once |
| Overwatch, Bloodscent, Vanguard | Passive. **No cue.** Nothing triggers |

**Three cues, not six.** Half the Instincts have no trigger and inventing one for them would make the set less legible rather than more.

---

## 7. Breach is a cost, not a failure

`broodline_combat_numbers.md` §2 makes integrity a pool. A raider reaching the Ark deducts from it; the wave is lost when the pool empties.

**So a breach must not sound like a loss.** It sounds like a cost, and it escalates as the pool drains — the same event, pitched and weighted by what is left.

| Pool remaining | |
|---|---|
| Above half | A dull knock. Registered, absorbed |
| Below half | Heavier, with a tail |
| Last breach before zero | The loss cue begins inside it |

**The Sunder's six-integrity breach at wave 60 is the largest single deduction in the game** and should be the heaviest sound in it short of the loss itself.

---

## 8. Rally

One per wave, four seconds, no cooldown. **The only moment of direct player agency inside a wave.**

It should be the biggest sound the game makes — not loud, but the widest. Everything else ducks under it for its four seconds, and when it ends the board is audibly back to normal. A player who uses Rally should feel the wave briefly become theirs.

---

## 9. Species voice

Six species, and bible §10.7's anti-bio-horror checklist applies to audio as directly as to art.

> **Animals, not monsters.** Natural history, not creature feature.

**No screeching. No chittering swarms. No wet sounds.** A Skitter arriving in numbers should sound like a lot of small animals, not like an infestation. The register bible §10.7 asks for — veterinary plates, greenhouse light, appeal over horror — has an audio equivalent and it is closer to a nature documentary than to a horror film.

**Three states per species: idle, attack, hurt.** Hurt is posture rather than injury, per bible §10.7 — a sound of stress, never of damage.

**Raiders share the visual language of asymmetry and unfinished structure** per `broodline_raider_roster.md` §4. **In audio that reads as the same animals, wrong.** The same vocal family, off — untuned, unregulated, nobody's. That is cheaper than authoring a separate raider palette and it is better fiction.

---

## 10. Outside combat

**The splice screen is the emotional centre of the game and it should be the quietest.** Bible §2.7 makes consumption the thing the screen exists to be honest about. A confirmation that consumes two creatures does not need a fanfare; it needs a moment.

**Mutation is the exception.** Bible §9.2 scripts one in the tutorial and it is the hook. It should be the most distinctive non-combat sound in the game, and **Aberrant results get a further layer** — the audio equivalent of the iridescent white-hot treatment at bible §10.4, and the only place audio and haptics fire together.

**The map is ambient and per band.** Three beds — Inner settled, Mid working, Outer Fracture-adjacent — and the change between them on relocation is one of the few places the game can express distance.

**Music is minimal and mostly absent.** Four cues: a title theme, a splice bed, a wave bed, and the weekly tick. **Nothing loops under general play.** A game played in ninety-second bursts with the sound off does not need a soundtrack; it needs four moments that are worth hearing.

---

## 11. Asset budget

The visual budget is 36 characters. Audio needs its own number for the same reason.

| | Assets |
|---|---|
| Species voice — 6 × idle, attack, hurt | 18 |
| Raider voice — 4 bodies × attack, hurt | 8 |
| Effect classes | 8 |
| Blocked / penetrated pair | 2 |
| Instinct triggers | 3 |
| Delver rumble | 1 |
| Breach — three weights | 3 |
| Rally, win, loss | 3 |
| Splice, mutation, Aberrant, fuse | 4 |
| UI — confirm, deny, navigate, reward | 4 |
| Ambient beds — three bands | 3 |
| Music cues | 4 |
| **Total** | **61** |

**Sixty-one sounds.** Raider voice reuses the four-body structure from `broodline_raider_roster.md` §3, so eight variants share four palettes — the same saving the mesh budget takes.

---

## 12. Guardrails

- Every audio cue has a visual equivalent. Nothing exists only in sound
- Haptics fire on exactly three events: mutation, Aberrant, breach
- Chill and Taunt modify existing raider sounds rather than adding new ones
- Untargetable raiders produce silence from the player's creatures, and it is not filled
- Breach escalates with pool depletion and never sounds like a loss until it is one
- No screeching, chittering or wet sounds. Bible §10.7 applies in full
- Raiders are the same animals, wrong — not a separate palette
- Nothing loops under general play

---

## 13. Open questions

1. **The blocked-hit sound needs its visual pair specified and nobody owns it.** A damage number that reads 5 is the obvious answer and the game shows no damage numbers. It may need a distinct impact flash, which is an art decision arising from an audio requirement.
2. **Sixty-one assets is a budget, not a quote**, and the eighteen species vocalisations are the risk. Six animals × three states, each recognisable, each avoiding the monster register, is a real casting and design problem rather than a library purchase.
3. **Ambient beds may be wasted.** Three per-band beds only pay off for players with sound on who relocate between bands, which is a small share of a small share.
4. **Nothing here covers accessibility.** A hard-of-hearing player and a muted player have the same experience by construction, which is a good accident — but it has not been checked properly and it should be.

---

*Owns: the audio division of labour, effect classes, species and raider voice, the breach and Rally treatments, and the asset budget. Does not own: visual direction (bible §10), the counter mechanics themselves (`broodline_combat_numbers.md`), or engine timing (`broodline_combat_engine.md`).*
