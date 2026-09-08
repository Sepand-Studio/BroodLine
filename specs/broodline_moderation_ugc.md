---
status: current
folder: 01-companions
note: >
  UGC surfaces, filtering, age gating, the 12+ decision.
---

# Broodline — Moderation & UGC Policy

*Design spec, the submission requirement*

---

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Last brought into line with the bible: 4 Sep 2026.


## 1. Why This Document Exists

The design describes a game with user-generated content across five surfaces. Until this document, none of them mentioned moderation.

This is not a design preference. Apple's App Review Guidelines require any app carrying user-generated content to provide **content filtering, in-context reporting, user blocking, and published developer contact information.** An app that ships without all four is rejected, and the rejection arrives at the end of the process rather than the start.

There is also a rating problem in §3 that the art direction has not accounted for.

---

## 2. The UGC Surfaces

Five, spread across four specs and never counted together:

| Surface | Source | Volume | Persistence | Visibility |
|---|---|---|---|---|
| **Alliance chat** | Bible §6.2 | High | Ephemeral | Up to 40 members |
| **Founder naming** | Bible §3.3, §9.2 beat 4 | 5 per player | **Permanent** | Public via Lineage View |
| **Recipe Share** | Bible §3.7, §8.4 | Medium | Persistent | **Server-wide** |
| **Alliance name & description** | Bible §6.2 | Low | Persistent | Public on the map |
| **Player display name** | Screen inventory | One | Persistent | Public |
| **Apex Cup leaderboard** | Bible §8.4 | One | Seasonal | **Server-wide** |

**Founder naming is the one nobody would flag.** It's a few words entered in the first five minutes, and bible §9.2 calls it the emotional anchor of session one. It's also permanent, propagates into every descendant's lineage tree, and appears inside system dialogs — `broodline_splice_confirm_spec.md` specifies the consumption confirmation reads "Consume Ash" using the player's chosen name. A slur entered at minute four appears in a modal dialog for two years.

**A sixth surface has since been added: the Apex Cup leaderboard.** It is server-wide and displays player names, which are already filtered at creation. It needs a report control and nothing more.

---

## 3. The Rating — Resolved at 12+

An early art brief committed to a **9+ rating** and justified the violence restraint by it.

Open text chat is difficult to reconcile with 9+. Comparable titles in this category generally carry 12+, and unrestricted player-to-player messaging is a standard reason for it. Two commitments pointed in opposite directions and one had to move.

| Option | Cost |
|---|---|
| **12+ with open chat** ← chosen | Loses the youngest segment of the broad audience |
| 9+ with canned-phrase chat | Cripples the alliance coordination layer |
| 9+ with chat tiered by account age | Complexity, but preserves both commitments |

**Settled at 12+ with open chat**, recorded in bible §10.7. The alliance layer is load-bearing across three systems — rally broadcasts, garrison negotiation, territory coordination — and canned phrases cannot carry it. The restraint on violence stands regardless; it was the right creative call independent of the number that justified it.

Consequence for UA: the marketing audience narrows slightly and targeting should be set against 12+ from the start rather than adjusted after submission.

---

## 4. Recipe Share Should Be Structured-Only

The single cheapest risk reduction available in this document.

A shared recipe, per bible §3.7, is the two parents' trait sets, the species body chosen, the resulting child, and the ancestry chain. **All of that is structured data.** None of it requires free text.

**Recommendation: no title field, no description, no comments on Recipe Share.** The recipe is the data plus the sharer's display name. Rating is a numeric score with no written review.

This removes the entire moderation surface from the most public, most persistent, server-wide UGC feature in the game. The only text that appears is creature names in the ancestry chain, which are already filtered at Founder naming. A feature that would otherwise need a review queue needs nothing beyond what already exists.

The cost is real but small: players cannot explain their strategy in words. The recipe itself is the explanation, and the ancestry chain shows the reasoning better than a paragraph would.

---

## 5. Age Gating

An age gate at account creation is required regardless of rating, because purchases and social features both carry obligations for minors — COPPA in the US, and equivalent provisions under GDPR for users under 13 to 16 depending on member state.

**Under-13 accounts:**
- Chat disabled, or restricted to a canned phrase set
- Recipe Share: can browse and use, cannot publish
- Display name selected from a generated list rather than free entry
- No behavioural advertising in rewarded video
- Founder naming from a curated name pool, with free entry disabled

Founder naming from a pool is the compromise that hurts most, since bible §9.2 leans on it as the emotional anchor. A large, varied pool of evocative names keeps the beat intact — the player still chooses, they just choose from a list.

---

## 6. The Four Required Mechanisms

**Filtering.** Applied at input, before content is stored. Requires normalisation for leetspeak, homoglyphs, and inserted spacing — a plain blocklist is defeated in an afternoon. Use a commercial service rather than building in-house; this is a solved problem and a bad in-house filter is worse than none because it creates false confidence.

**Reporting.** In-context on every surface: long-press a chat message, a report control on a recipe card, a report option on a profile and an alliance. Reports capture surrounding context automatically — the message log, the recipe ID, the creature record — because a report with no context cannot be actioned.

**Blocking.** Player-level. A blocked player's messages are hidden, their recipes are hidden from browse, and they cannot apply to your alliance. Blocking is unilateral and requires no justification.

**Contact.** Published support address, reachable from Settings and from the App Store listing.

---

## 7. Per-Surface Policy

| Surface | Filter | Report | Additional |
|---|---|---|---|
| Alliance chat | Real-time, pre-post | Yes | Officer mute; player block |
| Founder naming | At entry | Yes | Force-rename on violation |
| Recipe Share | N/A — structured only | Yes | Author block hides all their recipes |
| Alliance name & description | At creation and edit | Yes | Force-rename; alliance not dissolved |
| Display name | At creation and change | Yes | Force-rename to a neutral default |

**Name filters should be tuned loose rather than tight.** Creature names are short, creative, and often archaic — Ash, Cur, Tor, Pike are all legitimate and all resemble things a strict filter rejects. Blocking a player's chosen name during the FTUE's emotional beat is a measurable retention cost, and names are low-volume and reportable. Accept some false negatives here; the reporting path catches what the filter misses.

---

## 8. Enforcement Ladder

1. **Filtered** — content never posts, player sees a neutral rejection
2. **Warning** — in-app notice naming the surface and the rule
3. **Feature restriction** — chat mute 24h, then 7d; or Recipe Share publishing suspended; or naming disabled
4. **Account suspension** — 7 or 30 days
5. **Permanent ban**

Escalation is per-surface. A player who abuses chat loses chat, not splicing.

---

## 9. What Enforcement Never Does

- **Never removes creatures, roster, or lineage.** The entire design promises no creature is ever lost involuntarily, and that promise cannot have an exception for moderation. A ban removes access; it never destroys a broodline.
- **Never confiscates purchased currency or items.** Beyond the design principle, punitive removal of paid goods generates refund disputes and chargebacks.
- **Never applies alliance-wide punishment for an individual's conduct.** Collective punishment turns moderation into a griefing tool — report the officer, sink the alliance.
- **Never publicly names a sanctioned player.** Enforcement is between the operator and the player.

---

## 10. Response Times

| Category | Target |
|---|---|
| Standard reports | 24 hours |
| Threats, self-harm content, targeted harassment | 4 hours |
| Child safety material | **Immediate** |

Child safety reports leave the normal queue entirely. Any such content is preserved, reported to NCMEC and the relevant authorities, and the account suspended pending review — this is a legal obligation, not a policy choice, and it needs an escalation path staffed before launch rather than after the first incident.

**Moderation staffing scales with concurrent players and must be budgeted as an operating cost.** A 24-hour SLA that nobody is resourced to meet is worse than a published 72-hour one.

---

## 11. Localization Dependency

Filters must cover **every language the game ships in**, and filter quality varies enormously by language. This is now a hard dependency on the localization plan, which does not exist in any document.

A game shipping in eight languages with an English-only filter has, in practice, no filter in seven markets.

---

## 12. Guardrails

- No UGC surface ships without filter, report, and block
- Recipe Share carries no free text
- Enforcement never touches creatures, lineage, or purchases
- Age gate at account creation, with a restricted under-13 mode
- Name filters tuned loose; reporting catches the remainder
- Child safety escalation staffed before launch
- Filter coverage in every shipped language

---

## 13. Open Questions

1. ~~**9+ or 12+?**~~ **Resolved at 12+** — see §3 and bible §10.7.
2. **Is a canned-phrase set worth building for under-13 accounts,** or is disabled chat acceptable? Disabled chat effectively excludes minors from the alliance layer, which is a third of the game.
3. **Moderation staffing model** — in-house, outsourced, or automated-first with human escalation. Cost scales with success, which makes it easy to under-budget.
4. **Chat log retention period.** Long enough for review, short enough for GDPR deletion obligations. These pull against each other and need a legal answer, not a design one.
5. ~~**Does the Apex Cup leaderboard display names publicly?**~~ **Yes — counted as the sixth surface in §2.** It needs a report control; display names are already filtered at creation, so nothing further is required.
6. **Where does the age gate sit relative to the cold open?** Bible §9.2 puts play in the first twenty seconds and forbids anything before it. An age gate is a form. Recommend the gate on first launch before the cold open, kept to a single date field — it is the only pre-play screen the design should ever allow, and deferring it means retrofitting under-13 restrictions onto an account that has already named a Founder.

---

*Screens this document requires that the inventory now carries: Account Creation / Age Gate, Report, Block List, Settings / Support.*
