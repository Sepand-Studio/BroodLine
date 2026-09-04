# Broodline — Trait Codex

*Design spec, the reference layer*

---

## 1. Why This Document Exists

The screen inventory flags the Codex as undefined and gives a one-line reason: players must learn 12–16 Instinct behaviours plus three other categories across four tiers, and there is nowhere to read what any of them do.

The sharper reason is regulatory. The genetics spec makes odds disclosure non-negotiable — the splice screen shows a probability table before a charge is spent. But **a probability table is not a disclosure if the player can't look up what the outcomes are.** "18% Corrosive Spines" is compliant and useless if Corrosive Spines is a name with no definition attached. The Codex is what turns the odds display from a legal artifact into actual information.

Second reason: the splice screen is the highest-frequency screen in the game and it presents eight traits at once. Without a reference, every splice is a decision made on vibes.

---

## 2. The Disclosure Decision

The central question is whether undiscovered traits are hidden.

**Recommendation: full disclosure of effects, fog on ownership.**

Every trait's name, category, tier, and complete numerical effect is visible from session one, whether or not the player has ever held it. What the Codex tracks is a separate **discovered** state — have *you* ever owned a creature carrying this.

This splits the two things a codex is usually asked to do. The collection satisfaction comes from the checkmark and the completion count, not from hiding the rules. And hiding the rules is specifically the thing to avoid: concealed effect data on a paid randomised action is the mechanic regulators are currently most interested in, and the genetics spec already commits to the opposite position on odds. Being transparent about probability while opaque about outcomes would be an odd place to land.

There is a real cost. A fully-disclosed Codex on day one is 56 entries a new player can drown in, which §9 addresses through progressive surfacing rather than through hiding data.

---

## 3. Pool Shape

| Category | Common | Refined | Rare | Apex | Total |
|---|---|---|---|---|---|
| **Frame** | 5 | 5 | 3 | 2 | 15 |
| **Armament** | 5 | 5 | 3 | 2 | 15 |
| **Field** | 4 | 4 | 3 | 1 | 12 |
| **Instinct** | 4 | 5 | 3 | 2 | 14 |
| **Total** | 18 | 19 | 12 | 7 | **56** |

Fifty-six is chosen against two existing constraints, not picked freely. The combat spec targets 12–16 Instincts. The art direction budgets roughly 15 Frame and 15 Armament variants with unique geometry. This pool fits inside both.

The pyramid is shallow on purpose. Only seven Apex traits exist, and they are mutation-only, so the entire top of the collection is unpurchasable at any price — which is the guardrail the genetics spec builds the monetization argument on. Seven is few enough that each one is a known object players discuss, rather than a long tail nobody learns.

---

## 4. Fixed Tiers — A Resolution

The genetics spec is ambiguous here and it needs settling before the probability table can be built.

Section 3 lists "splicing up" as a source of Refined traits, and open question 2 asks whether Recessive results should exist — a trait carrying forward at a lower tier than the parent's. Both imply a given trait can occupy multiple tiers.

**Recommendation: each trait has exactly one fixed tier.** Plated Hide is Common, always. There is no Rare Plated Hide.

Three reasons, in order of weight. Variable tiers multiply the Codex from 56 entries to 224, and the probability table on the splice screen becomes unreadable on a phone. The art pipeline would need visual differentiation between tiers of the same trait, which the modular budget in the art direction does not have room for. And "splicing up" reads perfectly well as *acquiring higher-tier traits through splicing* rather than upgrading a specific one.

This also settles Recessive results in the negative — they can't exist without variable tiers. Which is probably the right outcome anyway; the genetics spec's own note calls them a frustration risk.

---

## 5. Entry Schema

Every Codex entry carries the same fields, so the component is built once:

- **Name, category icon, tier pip cluster** — per the art direction's four-tier pip system, colour plus count, never colour alone
- **Effect, in concrete numbers.** The Gene Vault spec set this precedent: "+8% yield," never "improves harvesting." Same rule applies here without exception.
- **Visual reference** — the creature-body expression of this trait, since traits are visible on the body
- **Sources** — which of the genetics spec's acquisition paths yield it
- **Fragment progress** — X/3 toward a Splice Roulette assembly, where applicable
- **Owned count** — how many creatures in the current roster carry it
- **Times held** — lifetime, feeding the collection layer
- **Discovered date**

---

## 6. Category Exemplars

Representative entries showing the schema. Full authoring of all 56 is a content task, not a design one.

**Frame** — HP, armor type, footprint

| Trait | Tier | Effect |
|---|---|---|
| Plated Hide | Common | +25% HP, armor type **Plated** |
| Fused Hide | Common | +15% HP, armor type **Sealed** |
| Regenerative | Refined | Recovers 2% max HP/sec after 3s without damage |
| Heavy Frame | Rare | +80% HP, occupies **two adjacent tiles** |

**Armament** — damage, type, range, interval

| Trait | Tier | Effect |
|---|---|---|
| Corrosive Spines | Common | 40 dmg, **Corrosive**, range 3, 1.2s |
| Longshot Glands | Refined | 55 dmg, **Kinetic**, range 6, 2.0s |
| Neural Lash | Rare | 30 dmg, **Neural**, range 4, 0.6s |

**Field** — aura, radius in tiles

| Trait | Tier | Effect |
|---|---|---|
| Chill Aura | Common | Enemies within 2 tiles move 20% slower |
| Panic Pheromone | Refined | Enemies within 3 tiles take +15% damage |
| Mending Field | Rare | Allies within 2 tiles regenerate 1.5% max HP/sec |

Every Armament entry must display its damage type prominently, because the Kinetic/Corrosive/Neural triangle is the main reason a player keeps more than five creatures. If damage type is buried in an entry, the triangle stops informing roster decisions and the combat spec's central composition lever goes soft.

**Naming constraint:** the art direction's anti-Tyranid checklist applies to trait names as well as art. Avoid carapace, chitin, hive, and brood-cult vocabulary. Lean toward veterinary and field-biology register — hide, gland, pelt, musculature — which also serves the mammalian-and-reptilian direction the art brief commits to.

---

## 7. Instinct — The Hard Case

Instincts are the reason this document is non-trivial. The other three categories are numbers. Instinct is a behaviour tree, and behaviour trees do not describe well in a tooltip.

Each entry displays the combat spec's three components as labelled fields rather than prose:

| Field | Bloodscent | Last Stand | Overwatch |
|---|---|---|---|
| **Targets** | Lowest current HP in range | Nearest | Furthest in range |
| **Trigger** | — | Self below 25% HP | — |
| **Response** | — | +50% attack speed | +25% range, −20% attack speed |

Seven Instincts are defined in the combat spec. **Seven more need authoring** to reach fourteen, and the combat spec's design rules apply: readable from watching one wave, none strictly best, triggers fire visibly.

**Behaviour preview.** Each Instinct entry carries a short looping animation on a simplified three-tile lane, showing the creature acting on its rule. This is the single most expensive item in the document and it is the only thing that will actually teach fourteen behaviour trees. The combat spec's standard is that an Instinct must be readable from watching one wave; the preview applies the same standard on demand, without requiring the player to have the trait first.

**Counterplay line.** Every Instinct entry states where it fails — Bloodscent is excellent against swarms and poor against a single armoured leader; Overwatch is superb on elevation and wasted at a chokepoint. Stating the weakness in the Codex is what stops a metagame consensus forming around two Instincts in week one.

---

## 8. Where the Codex Lives

The screen inventory asks whether the Codex belongs in the Lab tab or contextually inside Splice. **Both, because it isn't a screen — it's a component with a browsable index.**

- **Every trait pip in the app is tappable.** On the splice screen, the roster, creature detail, lineage view, the Roulette, the probability table. Tapping opens the entry as a bottom sheet over whatever the player was doing, with no navigation loss.
- **The browsable index lives in the Lab tab**, filterable by category, tier, discovered state, and owned state.

The bottom sheet is the important half. A player reading a probability table needs the definition *there*, in the moment of decision, not four taps away in a reference section they will visit twice.

---

## 9. Progressive Surfacing

Full disclosure without pacing is a wall. The FTUE spec's rule is one new system per session, and 56 entries on day one violates the spirit of it.

- The **index defaults to a filtered view** — categories the player has encountered, tiers they've held
- A visible toggle switches to the complete pool. Nothing is hidden, but the default is small.
- The Codex itself is not introduced in session one. Per the FTUE drip, trait categories arrive in session two; the Codex arrives with them.
- **Apex traits are visible from the start.** Seeing seven unobtainable-by-purchase traits early is aspirational, and the FTUE's scripted tutorial mutation already establishes that mutation is the path.

---

## 10. Collection Layer

The Codex is the game's completion metagame, and it costs almost nothing to build on top of data already tracked.

- Per-category and overall completion percentage
- **Completion milestones** grant Gene Shards and cosmetics at 25%, 50%, 75%, and 100% discovered
- Apex discoveries are marked distinctly — iridescent treatment plus motion, per the art direction's tier language
- Completion state is surfaced on the player profile, feeding alliance applications and the Apex Cup leaderboard

Full completion requires discovering all seven Apex traits, which are mutation-only. At a 3% mutation rate that is a multi-year pursuit that no amount of money shortens. That is exactly the right shape for a completion goal in this game.

---

## 11. Guardrails

- Every trait's full numerical effect is visible before it is ever owned
- Effects stated as numbers, never as adjectives
- Every trait pip in the app opens its entry in place
- Damage type displayed prominently on every Armament entry
- Each trait occupies exactly one tier
- Instinct entries state their weakness alongside their behaviour
- **No monetization of any kind.** No paid reveals, no completion shortcuts, no premium entries.

That last one deserves a line of defence. The Codex is a disclosure mechanism attached to a paid randomised action, and selling access to any part of it — even cosmetically framed — undermines the transparency position the genetics spec is built on. It should stay commercially untouched.

---

## 12. Open Questions

1. **Are behaviour previews affordable at fourteen?** They're the right answer and they're an animation line item nobody has costed. A static three-panel diagram is the fallback and it is meaningfully worse.
2. **Should the Codex show which traits appear on which chassis?** Useful for planning, and it implies chassis-trait restrictions that no spec currently establishes.
3. **Is 56 traits enough at launch?** It fits the art budget. Whether it sustains a two-year splice pool without the meta solidifying is a different question, and seasonal additions are the intended answer.
4. **Does "times held" create hoarding pressure?** A player who wants a Codex checkmark may refuse to splice a creature carrying an undiscovered trait — which cuts against the consumption economy. Discovery should probably register on acquisition, not on retention.
5. **Where do chassis get documented?** The Codex covers traits. Ten chassis are referenced across five specs and defined in none.

---

*Remaining undocumented: the chassis roster, mail and notification centre, player profile, and settings.*
