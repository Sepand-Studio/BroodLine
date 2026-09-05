# Broodline — The Trait Codex

*Design spec, the reference layer*

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Rewritten against twelve traits, three coverage tiers and six Instincts.
> Replaces the version built on fifty-six traits in four categories.

---

## 1. Why this document exists

Bible §4.7 calls the Codex required rather than optional and gives it one paragraph. Two arguments sit behind that.

**The regulatory one.** Bible §2.6 makes odds disclosure non-negotiable — the splice screen shows a probability table before a charge is spent. But **a probability table is not a disclosure if the player cannot look up what the outcomes are.** A forecast row reading *"Chill II — 34%"* is compliant and useless if neither *Chill* nor *II* has a definition attached anywhere. Under coverage tiers the problem is worse than it was under fixed tiers, because the row names two things and the player needs both.

**The design one.** The counter model asks a player to learn eight raider-to-trait associations, six Instinct behaviours, and what four traits that counter nothing are actually for. Bible §9.3 teaches the first association by designed failure at wave 6. The other seven have to be learnable somewhere that is not losing.

---

## 2. The pool

**Thirty-four entries.** Small enough that a player can plausibly know all of them, which was never true of the fifty-six-entry version.

| Kind | Count | Tiers | Source |
|---|---|---|---|
| **Species traits** | 12 | Three coverage tiers each | Bred; coverage fused |
| **Instincts** | 6 | Untiered | Bred |
| **Aberrants** | 8 | Untiered, off the ladder | Mutation only |
| **Raiders** | 8 | — | Encountered |

Raider entries are new. The previous version covered traits only and left a player with no way to look up *what answers a Courser* — which is the question the Codex is most often opened to answer, and the one bible §4.11's Wave Defeat screen raises at the worst possible moment.

---

## 3. Disclosure

> **Full disclosure of effects. Fog on ownership.**

Every entry's name, kind, and complete numerical effect at every tier is visible from session two, whether or not the player has ever held it. What the Codex tracks separately is a **discovered** state: have *you* ever owned this, or faced it.

This splits the two jobs a codex is usually asked to do. Collection satisfaction comes from the checkmark and the completion count, not from hiding rules. And hiding rules is specifically the thing to avoid — concealed effect data attached to a paid randomised action is the mechanic regulators are currently most interested in, and bible §2.6 already commits to the opposite position on probability. Being transparent about odds while opaque about outcomes would be an odd place to land.

**Aberrants are fully disclosed from the start**, including their effects. Eight things a player can see, cannot buy at any price, and may never roll is aspirational rather than frustrating, and bible §9.2's scripted tutorial mutation already establishes that mutation is the route.

**Discovery registers on acquisition, not on retention.** A player who briefly owns a creature carrying Screen has discovered Screen permanently. The alternative creates hoarding pressure — a player refusing to splice a creature because it carries their only checkmark — which cuts directly against the consumption economy every other system rests on.

---

## 4. The threat board

The Codex's most-used view and the cheapest thing in this document to build.

**Eight raiders and their answers, side by side, on one screen.**

| Raider | Threat | Answered by | Species |
|---|---|---|---|
| **Skirmisher** | Arrives in eights | **Splash** | Ember |
| **Lash** | Reaches past the front line | **Taunt** | Vetch |
| **Courser** | Too fast to kill in time | **Chill** | Pale |
| **Brood** | Splits, then splits again | **Cinder** | Ember |
| **Drift** | Flies; untargetable without the answer | **Reach** | Hollow |
| **Bulwark** | Shield breaks on hits, not damage | **Sprint** | Skitter |
| **Delver** | Surfaces behind the front line | **Burrow** | Loam |
| **Breaker** | Armour caps every hit at 5 | **Pierce** | Hollow |

**Undiscovered raiders show as a marker, not a blank.** A player should know something they have not met exists, without knowing what it is — surprise, not ambush. First encounters are the campaign's teaching moments and they need to land as events.

**The species column is doing real work.** Bible §4.4's guarantee is that acquiring all six species yields all eight counters, and this table is where a player reads that guarantee rather than being told it. *"I need a Pale"* is a sentence produced by this screen.

---

## 5. Entry schema

Four entry types, four layouts, one component each.

### 5.1 Species trait

- **Name, three-pip cluster, and the species that carries it**
- **What it counters** — the raider, named, with a link to its entry. Or, for the four that counter nothing, the line **"Counters nothing. By design."** stated plainly rather than left as an absence
- **Effect at each of the three tiers, in concrete numbers.** All three shown at once, whatever the player currently holds. The ladder is only legible if the rungs are visible together
- **Visual reference** — the body part this trait puts on a creature, per bible §10.4
- **Sample progress** — X/3 toward the next tier, from current inventory
- **Owned count** — how many creatures in the roster carry it, and at what tiers
- **Discovered date** and **times held**, lifetime

**Effects are numbers, never adjectives.** "Hits 2 targets within 1 tile," never "improves area damage." This rule has no exceptions and it is inherited from the Gene Vault spec, which was right about it.

**"Counters nothing. By design." is the most important line on any of these cards.** Four of twelve traits — Carapace, Litter, Regrow, Screen — answer no raider, and a player who thinks they are missing something will conclude the Codex is incomplete or that they have been unlucky. Stating it flatly, alongside a concrete effect, is what turns four apparent gaps into four real choices.

### 5.2 Instinct

The hard case. The other entries are numbers; an Instinct is a behaviour, and behaviours do not describe well in a tooltip.

Three labelled fields rather than prose, matching how bible §1.4 defines them:

| Field | Bloodscent | Last Stand | Overwatch |
|---|---|---|---|
| **Targets** | Lowest current HP in range | Nearest | Furthest in range |
| **Trigger** | — | Self below 25% HP | — |
| **Response** | — | +50% attack speed | +25% range, −20% attack speed |

Plus two things no other entry has:

**A behaviour preview.** A short looping animation on a simplified three-tile lane, showing the creature acting on its rule. This is the most expensive item in the document and it is the only thing that will actually teach six behaviour trees. Bible §4.6's standard is that an Instinct must be readable from watching one wave; the preview applies the same standard on demand, without requiring the player to own the trait first.

**Six previews, not fourteen.** The previous version budgeted for a pool that no longer exists, and the reduction is what makes this affordable. Eleven in total once Aberrant Instincts are counted — see §5.3.

**A counterplay line.** Every Instinct entry states where it fails. *Bloodscent finishes wounded swarms and wastes swings on a single armoured leader. Overwatch is superb down a long lane and wasted at a chokepoint.* Bible §4.6 requires that no Instinct is strictly best; stating the weakness in the Codex is what stops a metagame consensus forming around two of them in week one.

### 5.3 Aberrant

- **Name, kind** — combat or Instinct — and the **iridescent white-hot marker** with subtle motion, per bible §10.4. The only animated treatment in the system, and a distinct marker rather than a fourth pip, because an Aberrant is not further along the same ladder
- **Effect, in numbers**
- **"Counters nothing. Never will."** Per bible §1.6 no Aberrant may ever answer a raider — they are unbuyable and unplannable, which makes them pure upside and would make them a hard wall if any were required
- **"Cannot be purchased at any price"**, stated on the card. It is the game's strongest single claim and this is the only place it is visible to a player
- **Source** — mutation only, with the current Aberrant sub-roll shown

**Five of the eight are Instincts** and need behaviour previews too. **Their previews are locked until discovered.** The effect text is disclosed from the start; the animation is the reward. That costs nothing extra to build — the preview exists either way — and it gives a discovery a moment that a line of text does not.

### 5.4 Raider

- **Name, silhouette, threat in one line**
- **The answering trait**, named and linked, with the species that carries it
- **HP, speed, integrity cost**
- **Mechanic**, stated concretely — *"Every incoming hit is capped at 5 damage. Regenerates 15 HP per second."*
- **What each coverage tier of the answer does against it.** Chill I slows one Courser; Chill III slows four. This is the single most useful cross-reference in the app
- **First encountered**, and the campaign wave it is introduced at

---

## 6. Where it lives

**Both places, because it is not a screen — it is a component with a browsable index.**

**Every trait pip in the app is tappable.** On the splice forecast, the roster, creature detail, the Lineage View, the Roulette, the probability table, the Wave Defeat screen. Tapping opens the entry as a bottom sheet over whatever the player was doing, with no navigation loss.

**The browsable index lives in the Lab tab**, filterable by kind, tier, discovered state and owned state, with the threat board as its default view.

The bottom sheet is the important half. A player reading a probability table needs the definition *there*, in the moment of decision, not four taps away in a reference section they will visit twice. It is flagged as build-first in the screen inventory for exactly this reason: it appears over the splice screen, which is the highest-frequency screen in the game.

---

## 7. Progressive surfacing

Full disclosure without pacing is a wall, and bible §9.1's rule is one new system per session.

- **The index defaults to a filtered view** — traits the player has encountered, raiders they have faced. A visible toggle switches to the complete pool. Nothing is hidden; the default is small
- **The Codex is not introduced in session one.** Bible §9.7 keeps coverage tiers and probability tables out of it entirely. The Codex arrives in session two alongside trait basics
- **The threat board unlocks at wave 6**, with the designed Courser loss. That is the moment a player first needs it and the moment the association it teaches becomes real
- **A second unlock around day 26**, per `broodline_midgame_arc.md` §5, when enough entries are discovered that the full index is worth the toggle rather than overwhelming

---

## 8. Collection

The game's completion metagame, built almost entirely on data already tracked.

- **Completion percentage** per kind and overall, across all 34 entries
- **Milestones at 25%, 50%, 75% and 100% discovered**, granting Gene Shards and cosmetics
- **Aberrant discoveries are marked distinctly**, with the iridescent treatment and motion
- **Completion state appears on the Player Profile**, feeding alliance applications and the Apex Cup leaderboard

**Full completion requires discovering all eight Aberrants.** At a 5% sub-roll inside a 9% mutation rate, per `broodline_sample_economy.md` §9, a core player finds one roughly every five weeks — so a full set is a multi-year pursuit that no amount of money shortens. That is exactly the right shape for a completion goal in this game, and it is the only long-term goal in the design that money cannot touch even probabilistically.

**The twelve species traits complete in the first week**, since all six species are guaranteed by wave 6 and each carries two. That is intentional: early completion of the tractable part is what teaches a player that the counter is worth chasing.

---

## 9. Guardrails

- Every entry's full numerical effect is visible before it is ever owned
- Effects stated as numbers, never as adjectives
- Every trait pip in the app opens its entry in place
- Traits that counter nothing say so explicitly, on the card
- Every Aberrant card states that it cannot be purchased at any price
- Instinct entries state their weakness alongside their behaviour
- Discovery registers on acquisition, never on retention
- Undiscovered raiders show as a marker, never a blank
- **No monetization of any kind.** No paid reveals, no completion shortcuts, no premium entries

That last one deserves a line of defence. The Codex is a disclosure mechanism attached to a paid randomised action, and selling access to any part of it — even cosmetically framed — undermines the transparency position the entire splice screen is built on. It stays commercially untouched. Bible §8.6 already lists Codex access among the things never sold.

---

## 10. Open questions

1. **Eleven behaviour previews is the only uncosted line item here.** Six species Instincts plus five Aberrant Instincts. They are the right answer and nobody has priced them. A static three-panel diagram is the fallback and it is meaningfully worse — behaviour is motion, and a diagram of motion is a description of motion.
2. **Should the threat board show raider counts and wave composition?** Useful for planning; it also edges toward playing the game for the player. Leaning no, on the grounds that bible §4.12 already requires composition to be visible on the pre-wave screen, which is the right place for it.
3. **Does the Codex need a "what am I missing" view** — the counters the player's roster does not currently hold? It would be genuinely useful and it duplicates the Lineage View's species composition strip, which bible §3.5 already calls the planning tool. Probably a link rather than a second view.
4. **Sample progress on a trait entry is per-player, not per-creature.** X/3 toward the next tier reads cleanly in inventory terms and may mislead, since coverage applies to one creature rather than to the trait globally. Worth watching in playtest for whether players expect fusing to upgrade everything at once.
5. **Thirty-four entries may be too few by year two.** It fits the art budget and it is learnable, which was the whole problem with fifty-six. Seasonal species are the intended answer, and each adds two traits and one Instinct signature rather than a long tail.

---

*Owns: the Codex pool and its four entry schemas, the threat board, the disclosure and discovery policy, progressive surfacing, and the collection layer. Does not own: trait effects and coverage values (`broodline_combat_numbers.md` §4), raider stats (§6 of the same), the Aberrant sub-roll (`broodline_sample_economy.md` §9), or where the Codex sits in the build order (`broodline_screen_inventory_v2.md`).*
