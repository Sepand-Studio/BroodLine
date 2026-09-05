# Broodline — Notifications

*Design spec, what the game says when it is closed*

> **CURRENT — companion to the design bible.** Bible §8.4 warns that FOMO
> burnout kills lifetime value faster than a missed sale, and then the design
> builds nine systems that all want to interrupt. This is the budget.

**One finding reframes a core mechanic.** The ninety-second raid alert cannot be answered by most players most of the time, and the design is already fine with that — but only if live defence is understood as a bonus rather than as the mode — §3.

---

## 1. Five categories

They are not equal and they must not share a budget.

| Category | Example | Time-critical | Cap |
|---|---|---|---|
| **Alert** | Incoming raid | **Yes, 90 seconds** | Uncapped |
| **State** | Facility complete, node depleted, Apex spawned | No | 3/day |
| **Social** | Alliance rally, application, garrison request | No | 3/day |
| **Live-ops** | Event opening or closing, the weekly tick | No | 1/day |
| **Lapsed** | Re-engagement after absence | No | See §6 |

> **Total budget: five notifications a day, plus alerts.**

Alerts are outside the cap because they are the only ones with a deadline and because they are self-limiting — a player can be raided twice a day and defends only when they happen to be present.

**The cap is a hard ceiling, not a target.** A quiet day should be quiet. This genre's default is to fill the budget every day and the result is a player who turns notifications off entirely, at which point the alert becomes undeliverable too.

---

## 2. Priority when the budget is full

State beats social beats live-ops. Within a category, the most time-sensitive wins.

**A notification that loses its slot is dropped, not deferred.** A facility-complete notice delivered four hours late is worse than none — the player already opened the game and saw it, and the notification only tells them the game is out of date.

---

## 3. The raid alert, and what it actually is

Bible §4.9 gives the defender **ninety seconds** to open the game and place escorts live. Otherwise the raid auto-resolves.

**Most players will not answer it.** Push notification open rates within ninety seconds are low under any circumstances — the phone is in a pocket, in another room, face down, on silent, or the player is driving. **Ten percent would be a good outcome.**

**That is fine, and the design already made it fine.** `broodline_combat_engine.md` and bible §4.10 make auto-resolve the identical simulation with no player input, producing a replay. A defender who misses the window loses nothing they would have had except agency in that specific wave.

> **Live defence is a bonus, not the mode. The ninety seconds exist so that a player who happens to be present gets agency, not so that a player who is absent is punished.**

**Three consequences worth stating plainly:**

**The alert must never imply failure to respond is a loss.** *"Your convoy is under attack"* is right. *"Defend now or lose your cargo"* is a lie — the cargo is defended either way, by the same engine.

**Escort quality matters more than presence.** If ninety percent of defences auto-resolve, what a player brought is nearly the whole game, and the Collector Dispatch screen carries far more weight than the Raid Defence Alert does.

**The alert should be honest about the odds.** A defender opening it with fifteen seconds left cannot meaningfully re-place five creatures. Showing the countdown and the current auto-resolve forecast is better than showing a placement UI they cannot use.

---

## 4. What each notification says

**Every notification names the thing and the consequence, in that order, in one line.** No teasers, no *"something happened in your Ark."*

| Trigger | Copy shape |
|---|---|
| Raid incoming | *"A raider is intercepting your convoy near Fenwatch. 90 seconds."* |
| Facility complete | *"Your Splicing Chamber reached tier 4."* |
| Node depleted | *"The Rich Deposit at Sablewick has run dry."* |
| Apex spawned | *"An Apex Vein has opened in Deepscree."* — only if the player has a Scout Report, per bible §5.9 |
| Alliance rally | *"[Alliance] is assaulting a stake in Kettlemoor."* |
| Weekly tick | *"The map reshuffles in one hour."* |
| Event opening | *"Mutation Surge begins today."* |

**Named regions and named facilities, never generic nouns.** The player's map knowledge is the thing the game is trying to build, and a notification is a free opportunity to use a place name.

**Never a countdown in the copy for anything that is not a countdown.** *"Only 2 hours left!"* on a permanent offer is the pattern bible §8.4 rules out.

---

## 5. Quiet hours

**No notification between 22:00 and 08:00 local.** None.

**There is no exception, including the raid alert.** A raid at 03:00 that goes unanswered auto-resolves identically to one the player declines to open — so suppressing it costs nothing and delivering it costs goodwill.

That leaves the game silent overnight, which in this genre is unusual and is the right call. The systems that could justify a 03:00 notification — timers, raids, node depletion — all resolve without the player, by design.

**Quiet hours are local to the device**, not to the server. The weekly tick is per-server local evening per `broodline_server_topology.md` §5, so the tick notification lands inside waking hours for most of a server by construction.

---

## 6. Lapsed re-engagement

The category most likely to be abused and the one with the clearest evidence about what fails.

| Absence | |
|---|---|
| **1 day** | Nothing |
| **2 days** | One notification, naming a specific state change since they left |
| **4 days** | One, naming a different thing |
| **7 days** | One, and it is the last automatic one |
| **14+ days** | Nothing further except live-ops openings, at most one a fortnight |

**Four notifications total across the first week of absence, then near-silence.**

**Each names something real that happened.** *"Your Rich Deposit at Thornwyke ran dry."* *"Your alliance took Kettlemoor."* Not *"we miss you"* and not a gift. A returning player who comes back for a bribe leaves again when it is spent; one who comes back because their deposit ran dry has a reason to open the map.

**Nothing about loss.** *"You were raided four times while away"* is true and it is the worst possible message — a player deciding whether to return should not be told the state they abandoned got worse. `broodline_collectors_raiding.md` caps losses at 40% of cargo precisely so that returning is never a cleanup job, and the notification layer should not undo that.

---

## 7. Under-13 accounts

`broodline_moderation_ugc.md` §5 puts restricted accounts in a mode with no free-text naming, no chat and browse-only Recipe Share.

> **Restricted accounts receive alerts and state notifications only.** No social, no live-ops, no lapsed re-engagement.

Re-engagement messaging to a minor is the category with the least defensible upside, and cutting it entirely is simpler than defending a threshold.

---

## 8. Permissions

**The prompt comes after the first Founder is named**, not on first launch.

Bible §9.2 puts play in the first twenty seconds and forbids anything before it. `broodline_moderation_ugc.md` §9 already reserves the single pre-play screen for the age gate. Asking for notification permission at launch spends the player's first decision on a request they have no reason to grant.

**By beat 4 they have named a creature.** Asking then, with a specific reason — *"we will tell you when your Founder is ready to splice again"* — converts far better than a cold prompt and is honest about what it is for.

**A player who declines is never asked again by the game.** iOS allows one prompt; a second requires sending them to Settings, and a game that does that has misjudged its relationship.

---

## 9. Guardrails

- Five notifications a day maximum, plus alerts
- No notification between 22:00 and 08:00 local, including alerts
- Every notification names the thing and the consequence in one line
- Never imply that failing to respond to a raid alert causes a loss
- Never notify about accumulated losses during absence
- Lapsed re-engagement stops after four notifications
- Restricted accounts get alerts and state only
- Permission requested after the first Founder is named, once, never again
- A notification that misses its slot is dropped, never deferred

---

## 10. Open questions

1. ~~**Suppressing the raid alert overnight is the call I would make and it is contestable.**~~ **Resolved — suppressed. The game is silent from 22:00 to 08:00 local, alerts included.**
2. **Ten percent live-defence rate is an estimate, not a measurement.** If it turns out to be two percent, the Raid Defence Alert screen is close to dead content and the effort should move to Collector Dispatch. `broodline_telemetry.md` does not currently measure it and probably should.
3. **The five-a-day cap is not derived from anything.** It is a judgement about restraint. Three would be more restrained and might cost real re-engagement; seven is where this genre usually sits and is where players turn notifications off.
4. **Nothing here covers in-app notification, only push.** The mail centre, badge counts and the alert bar are a separate surface with a separate budget, and the interaction between them is unaddressed — a player who has already seen something in-app should probably not be pushed it.

---

*Owns: notification categories, the daily budget, copy shape, quiet hours, lapsed re-engagement and the permission prompt. Does not own: the raid alert window itself (bible §4.9), auto-resolve (`broodline_combat_engine.md`), or restricted-mode rules (`broodline_moderation_ugc.md`).*
