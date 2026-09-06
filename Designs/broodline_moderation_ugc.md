---
status: current
folder: 01-companions
supersedes: 99-archive/broodline_moderation_ugc_era2.md
verified-against: broodline_bible.md (2026-09-05)
note: >
  Moderation and UGC policy, reconciled to bible §2.7 (Founder naming), §6.1
  (alliance chat and roles), §8.4 (Recipe Share, Apex Cup) and §10.5. The bible
  has no moderation section; this is the whole policy. Settles the rating at
  12+ (register 6.11) and renames the alliance Founder role to Leader (6.12).
  Adds the Recipe Share rating-integrity rule the live-ops companion deferred.
---

# Broodline — Moderation & UGC Policy

*Design spec, the store-submission requirement. Companion to bible §6, §8.4, §9.4.*

---

## 1. Why this document exists

The bible describes a game with six user-generated-content surfaces and no moderation. That is a submission blocker, not a design gap: Apple's App Review Guidelines require any app carrying UGC to provide **content filtering, in-context reporting, user blocking and published developer contact**, and Google's policies require the equivalent. An app without all four is rejected at the end of the review process rather than the start.

---

## 2. The rating

**Broodline ships at 12+ with open alliance chat.** Register 6.11.

The older art direction assumed 9+. Open player-to-player messaging is a standard reason comparable titles carry 12+, and the alliance layer — rally broadcasts, garrison negotiation, Stake coordination, allies riding escort — is load-bearing across §4.9, §5.6 and §6. Canned phrases cannot carry it.

Bible §10.5's restraint on violence (damage is posture, never injury) stays exactly as written. It is the right creative choice independent of the number that first justified it, and the 12+ target is also the audience the monetization model is built for.

**Vocabulary has no bearing on the rating.** "Splice", "consume" and "Founder" do not move Apple's or Google's questionnaires; open chat and randomised paid mechanics do. Both are already in the design and both are what the rating reflects.

---

## 3. The UGC surfaces

| Surface | Bible | Volume | Persistence | Visibility |
|---|---|---|---|---|
| **Alliance chat** | §6.1 | High | Ephemeral | Up to 40 members |
| **Founder naming** | §2.7, §9.4 | 5 per player | **Permanent** | Public via Lineage View, ancestry chains, recipe cards |
| **Recipe Share** | §8.4 | Medium | Persistent | **Server-wide** |
| **Alliance name and description** | §6.1 | Low | Persistent | Public on the map |
| **Player display name** | Screen inventory | One | Persistent | Public |
| **Apex Cup leaderboard** | §8.4, live-ops §5 | — | Monthly | **Server-wide, highest-visibility** |

The leaderboard is a surface because it displays names. It carries no free text of its own and inherits the display-name filter; it is listed so that nobody assumes it is exempt from reporting.

**Founder naming is the one nobody flags.** Five words entered in the first minutes, permanent, propagated into every descendant's lineage and every shared recipe, and shown inside system dialogs — the consumption confirmation reads *"Consume Ash permanently?"* using the player's chosen name (§2.7). A slur entered at minute four appears in a modal for two years.

---

## 4. Recipe Share is structured-only

The cheapest risk reduction in this document.

A shared recipe (§8.4, live-ops §7) is both parents' species and trait sets, the locked slot, the child, and the ancestry chain. **All of it is structured data.** None of it needs free text.

**No title, no description, no comments.** The recipe is the data plus the sharer's display name. Rating is a numeric score with no written review. The only text on a recipe card is creature names in the ancestry chain, already filtered at naming. The most public, most persistent UGC surface in the game therefore needs no review queue beyond what naming already requires.

The cost is that players cannot explain a strategy in words. The ancestry chain is the explanation.

### Rating integrity

Deferred here by the live-ops companion. Three rules:

- **One rating per account per recipe**, changeable, never stacking.
- **Ratings count only from accounts at least seven days old with at least ten completed splices.** New and idle accounts can rate; their ratings do not weigh.
- **Twenty ratings per account per day**, and browse ranking uses a Bayesian average against the server mean, so a recipe with three five-star ratings does not outrank one with three hundred at four.

Rating manipulation is not a moderation offence; it is made unprofitable rather than punished.

---

## 5. Age gating

An age gate at account creation is required regardless of rating: purchases and social features carry obligations for minors under COPPA in the US and under GDPR's under-13-to-16 provisions by member state.

**Under-13 accounts:**

- Chat disabled. A canned-phrase set is not built at launch; see open question 2.
- Recipe Share: browse and use, cannot publish or rate.
- Display name chosen from a generated list, not free entry.
- **Founder naming from a curated pool**, free entry disabled. This is the compromise that hurts most, because §9.4 makes naming the emotional anchor of session one. A large, evocative pool keeps the beat: the player still chooses, from a list.
- No behavioural advertising in rewarded video.
- Apex Cup leaderboard shows the generated name.

---

## 6. The four required mechanisms

**Filtering.** At input, before storage. Requires normalisation for leetspeak, homoglyphs and inserted spacing; a plain blocklist is defeated in an afternoon. Use a commercial service. A bad in-house filter is worse than none because it creates false confidence.

**Reporting.** In context on every surface: long-press a chat message, a report control on a recipe card, a report option on a profile, an alliance and a leaderboard row. Reports capture context automatically — surrounding messages, the recipe ID, the creature record — because a report without context cannot be actioned.

**Blocking.** Player-level, unilateral, no justification. A blocked player's messages are hidden, their recipes hidden from browse, and they cannot apply to your alliance or be matched to your convoys on the Transit Board.

**Contact.** A published support address reachable from Settings and both store listings.

---

## 7. Per-surface policy

| Surface | Filter | Report | Additional |
|---|---|---|---|
| Alliance chat | Real-time, pre-post | Yes | Officer mute; player block |
| Founder naming | At entry | Yes | Force-rename on violation; the tree and the record survive |
| Recipe Share | N/A — structured only | Yes | Author block hides all their recipes |
| Alliance name and description | At creation and edit | Yes | Force-rename; the alliance is never dissolved |
| Display name | At creation and change | Yes | Force-rename to a neutral default |
| Apex Cup leaderboard | Inherits display-name filter | Yes | Row hidden pending review |

**Name filters run loose, not tight.** Creature names are short, creative and often archaic — Ash, Cur, Tor, Pike are all legitimate and all resemble what a strict filter rejects. Blocking a chosen name during the FTUE's emotional beat is a measurable retention cost; names are low-volume and reportable. Accept false negatives here and let reporting catch them.

---

## 8. The alliance role called Founder

Bible §6.1 names the alliance's head role **Founder**. Bible §9.4 names the five starting creatures **Founders**. *"Founder consumed"*, *"Founder left the alliance"* and *"Consume Founder?"* cannot coexist in one game's notifications, support tickets and moderation logs.

**The alliance role is renamed Leader.** Creatures keep Founder, because §9.4 built session one on it. Register 6.12.

---

## 9. Enforcement ladder

1. **Filtered** — never posts; neutral rejection shown
2. **Warning** — in-app, naming the surface and the rule
3. **Feature restriction** — chat mute 24h then 7d; Recipe Share publishing suspended; naming disabled
4. **Account suspension** — 7 or 30 days
5. **Permanent ban**

Escalation is per surface. A player who abuses chat loses chat, not splicing.

---

## 10. What enforcement never does

- **Never removes creatures, roster or lineage.** The design's promise that no creature is ever lost involuntarily (§4.11, README constraint 4) has no moderation exception. A ban removes access; it never destroys a broodline.
- **Never confiscates purchased currency or items.** Beyond principle, punitive removal of paid goods produces refund disputes and chargebacks.
- **Never punishes an alliance for an individual's conduct.** Collective punishment turns reporting into a griefing tool: report the Leader, sink the alliance.
- **Never publicly names a sanctioned player.**

---

## 11. Response times

| Category | Target |
|---|---|
| Standard reports | 24 hours |
| Threats, self-harm content, targeted harassment | 4 hours |
| Child safety material | **Immediate** |

Child safety reports leave the normal queue entirely: content preserved, reported to NCMEC and the relevant authorities, account suspended pending review. This is a legal obligation and needs a staffed escalation path **before launch**, not after the first incident.

**Moderation staffing scales with concurrent players and is an operating cost**, budgeted against the 500–1,500 DAU server target times server count. A 24-hour SLA nobody is resourced to meet is worse than a published 72-hour one.

---

## 12. Localization dependency

Filters must cover **every shipped language**, and filter quality varies enormously by language. A game in eight languages with an English-only filter has no filter in seven markets. This is a hard dependency on the localization plan, which does not yet exist.

---

## 13. Guardrails

- No UGC surface ships without filter, report and block
- Recipe Share carries no free text; ratings are weighted, not policed
- Enforcement never touches creatures, lineage or purchases
- Age gate at account creation with a restricted under-13 mode
- Name filters tuned loose; reporting catches the remainder
- Child safety escalation staffed before launch
- Filter coverage in every shipped language
- No two things in the game share the name Founder

---

## 14. Open questions

1. **Moderation staffing model** — in-house, outsourced, or automated-first with human escalation. Cost scales with success, which makes it easy to under-budget. Lean: outsourced with an in-house escalation lead, from soft launch.
2. **A canned-phrase set for under-13 accounts.** Disabled chat excludes minors from a third of the game. Worth building after launch if under-13 accounts exceed a tenth of the base; not before.
3. **Chat log retention.** Long enough for review, short enough for GDPR deletion. A legal answer, not a design one; 30 days as the placeholder.
4. **Should the Apex Cup leaderboard be opt-out?** A player who does not want their name server-wide should be able to compete anonymously. Lean yes; cheap.
