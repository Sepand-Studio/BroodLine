# Broodline — Splice Chamber & Confirmation Flow

*Closes reconciliation item 0.1. Screen updated for the Part 1 decisions.*

**Current companion to the design bible**, referenced by bible §2.7 for full copy and interaction states. Numbered references like 1.2 or "constraint 9" point at `broodline_reconciliation.md`, the decision register.

---

## 1. Why this exists

The designed Splice Chamber ends on a Cost row and a **Begin Splice** CTA. Parent consumption is never mentioned. The player learns both parents are gone on the next screen, at reveal.

The genetics spec calls the pre-splice warning non-negotiable and names accidental consumption as the most likely source of refund requests and one-star reviews. This is the single highest-risk gap in the shipped design and it is cheap to close.

Two principles:

- **Nothing irreversible happens without the player reading what it costs.** Not a fine-print line — a dedicated element and a confirm step.
- **The confirm is not a speed bump.** Wording it as a scary-but-skippable interstitial trains players to tap through it. It states a fact and moves on.

---

## 2. What the screen must display

Updated for 1.2 (three slots), 1.4 (coverage tiers), 1.5 (samples vs traits) and 1.6 (Aberrant class).

**Per parent slot:**
- Species and body silhouette
- Generation
- Two combat traits, each with coverage tier (I–III)
- One Instinct, untiered per 1.7
- Aberrant marker if carried
- Lock state if the creature is garrisoned, escorting or deployed

**Body choice.** Which parent's body the child inherits. Free choice, no randomness — the player's only controlled lever against the roll.

**How the three slots fill**, per 1.2. The pools are separate — combat slots fill only from the parents' four combat traits, the Instinct slot only from their two Instincts.

| Slot | Fills from | Resolution |
|---|---|---|
| Combat 1 | Any of the parents' four combat traits | **Player locks it.** Guaranteed to carry. |
| Combat 2 | The remaining three | Rolls, with displayed odds |
| Instinct | The parents' two Instincts | Rolls, modified by affinity and DOM/REC |

The lock must be visually unmistakable — the design's selected-option treatment (white fill, 2px ring) already carries this.

**Forecast.** Per-trait odds for the two rolling slots. DOM/REC indicators per 3.5. Odds shown *before* the charge is spent — non-negotiable per the genetics spec, and the thing regulators are currently most interested in.

**Mutation, stated as two numbers** per 3.1:
- Base mutation chance (~9%) — a trait neither parent carried
- Aberrant chance — the sub-roll inside it, shown separately so the player understands these are not the same event

**Generation.** The child's generation, and the current ceiling from the Splicing Chamber. When a splice would exceed the ceiling, block it and surface the Chamber upgrade directly in the message rather than showing a bare error.

**Coverage ceiling.** Per constraint 9, generation gates how high coverage can go. Worth showing the child's ceiling alongside its generation — it turns an abstract number into a reason to push a line deeper.

**Charge cost and current count.**

**The destruction notice.** See below.

---

## 3. The destruction notice

Sits directly above the CTA. Not a tooltip, not a footnote.

> **Both parents are consumed.**
> Vetch Crawler (G4) and Ember Skitter (G6) will be permanently removed from your roster. Their record stays in the lineage.

Rules:

- Names the actual creatures, not "the selected parents"
- Uses the player's chosen name when one exists
- The second line matters as much as the first — it is the reassurance that makes the loss survivable, and it is true
- Same language as the confirm dialog and the reveal screen. Three different phrasings of the same fact reads as evasion.

**CTA label changes from "Begin Splice" to "Splice — consumes both parents."** The cost belongs on the button. A player who taps past everything else still reads the thing they tap.

---

## 4. Confirmation levels

### Standard

Single dialog. Dismissible.

> **Splice these two?**
> Vetch Crawler and Ember Skitter will be consumed. This cannot be undone.
> **[ Splice ]** **[ Cancel ]**

### Founder

Second dialog, after the standard one. Per the genetics spec, the Founder's chosen name is what makes a player stop and read — so the name carries the sentence.

> **Consume Ash permanently?**
> Ash is one of your Founders. This cannot be undone. Ash will remain at the root of every lineage descended from them.
> **[ Consume Ash ]** **[ Keep Ash ]**

Rules:

- **The name, not the species.** "Consume Ash" stops a player; "Consume Vetch Crawler" does not.
- **Buttons state the outcome, not Yes/No.** A player tapping "Keep Ash" cannot do it by accident.
- **No destructive default.** Cancel is the safe action and should not be styled as the primary.
- **Never suppressible.** No "don't show this again" — the whole point is that it interrupts the familiar rhythm.

Blocking Founder consumption outright was considered and rejected in the genetics spec: it leaves five dead roster slots by month six. One dialog is the cheaper fix.

---

## 5. After the splice

Splice Reveal keeps its existing line — that parents are consumed and their traits live on in the pedigree — but it is now a confirmation of something already understood rather than a disclosure.

**Then show the lineage.** Per 2.1 and the FTUE's beat 8, the consumed parents appearing in the tree immediately after is the lesson: individuals are consumed, the record survives. Teaching it here, in the tutorial, with nothing at stake, means the player already understands the trade the first time it costs them something real.

---

## 6. FTUE handling

Per the FTUE spec's §3, unchanged and reinforced:

- Tutorial parents are **provided specifically for the tutorial** and framed as sample stock
- **The named Founder is visibly locked out** of both parent slots during the tutorial
- Destruction is stated in the same language the live game uses — no softened tutorial variant
- The lineage view immediately after shows both consumed parents present

Do not soften this. A tutorial that hides consumption produces a player who discovers it in week two by destroying something they cared about.

---

## 7. What not to do

- **No "are you sure?" without content.** A dialog that does not name what is lost trains dismissal.
- **No countdown or hold-to-confirm.** Friction theatre. It slows engaged players and does not stop distracted ones.
- **No purchase offer in the confirm path.** Selling a way to avoid consumption at the moment of loss is the worst available moment to monetize and directly contradicts the monetization spec's guardrails.
- **No "SPLICE FAILED" state.** Per the genetics spec there is no failure outcome — the worst result is rolling traits the player already had. Disappointment does not need a failure banner on top.

---

## 8. Implementation note

`Splice Chamber.dc.html` is a static file with no logic block, despite the handoff README stating every screen is interactive and directing implementers to read its `renderVals()`. There is nothing to port. This screen is built from the spec above, not recreated from the prototype — the prototype supplies layout and visual language only.
