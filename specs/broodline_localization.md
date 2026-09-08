---
status: current
folder: 01-companions
note: >
  Launch languages, naming policy, pricing per storefront, fonts,
  jurisdiction.
---

# Broodline — Localization

*Design spec, shipping in more than one language*

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.

---

## 1. Why this document exists

Two things in the set are blocked on it, and both have external lead times.

**Regional pricing.** `broodline_economy_model.md` §6 established that the shard ladder must be monotonic — value per dollar rising with price — because players run the arithmetic and post it. That was checked in USD. Apple's price tiers do not convert linearly, so **a ladder that is monotonic in dollars can invert in yen or lira**, and store listings cannot be built until every storefront is verified.

**Moderation filter coverage.** `broodline_moderation_ugc.md` requires filtering on five UGC surfaces, and App Review rejects an app carrying user content without it. A filter covers the languages it was bought for. That is a procurement task, not a writing task, and it is the longest lead time left in the project.

The rest — what gets translated, what does not, and what breaks when it does — follows from those two.

---

## 2. Launch set

**English only at launch. Every other language is added incrementally, one at a time, each gated on its own filter and font.**

| Order | Language | Why this position |
|---|---|---|
| **Launch** | **English** | The whole game, every market that reads it |
| 1 | Japanese | The genre's second-largest market, and the first CJK font decision |
| 2 | Korean | |
| 3 | Simplified Chinese | Taiwan, Hong Kong, Singapore, Malaysia via the global store — not the mainland, §2.1 |
| 4 | German | The worst-case text expansion, and the test of every layout |
| 5 | French | |
| 6 | Spanish | |
| 7 | Traditional Chinese | |
| 8 | Portuguese (Brazil) | |
| 9 | Russian | |
| 10 | Turkish | |
| 11 | Italian | |
| 12 | Indonesian | |
| 13 | Arabic | The RTL build cost, taken once the layout has stopped changing |
| 14–15 | Thai · Vietnamese | |

**One language at a time, not waves.** An earlier draft launched with five and added eight together. Adding a language is a translation pass, a filter procurement, a font decision and a QA pass across fifty-nine screens — and doing several at once means several sets of layout bugs landing in the same build. One at a time means each language is complete before the next starts, and **no language ships partially**, per the guardrail at §10.

**The order is by expected revenue, with two exceptions.** Japanese first because it forces the CJK font decision early, on the market most worth getting it right for. German fourth rather than later because it is the widest text expansion and every layout that survives German survives everything.

**Regions expand the same way.** English-only launch does not mean English-market-only — an English build is playable anywhere the store sells it, and market entry is a separate, incremental decision from language.

**Nothing below this line applies at launch.** It applies to each language as it is added, and it should be read that way. Comparable titles — bible's reference points, *Whiteout Survival* and *Kingshot* — draw heavily from the US, Japan, Korea and Taiwan, with the European four carrying most of the rest.

**Arabic is late for an engineering reason, not a market one.** Right-to-left requires mirroring the layout across fifty-nine screens, which is a build cost rather than a translation cost. It is placed after the layout has stopped changing — adding RTL to a moving layout is the one case where waiting is cheaper than starting.

**Soft launch and launch are both English only.** Soft launch is Canada, Australia, New Zealand and the Philippines — low acquisition cost, behaviour close enough to the US to read the retention curve. Launch widens the markets, not the languages.

### 2.1 Mainland China is a separate decision

Simplified Chinese on the global App Store serves Taiwan, Hong Kong, Singapore, Malaysia and overseas Chinese players. **Mainland China is not a localization task.** It requires a publishing licence, a local publisher, real-name registration and minor playtime limits, and those change the product rather than the strings. Treat it as a business decision taken separately, after launch, or not at all.

---

## 3. Naming

The design's names are doing work, and the rule for each depends on what work.

> **Traits, raiders and Aberrants are translated for meaning.**
> **Species and regions are transliterated and kept.**

**Traits and raiders are functional labels.** A player must map *Chill* to *the thing that stops the Courser*. A transliterated *Chiru* teaches nothing and makes the threat board — the Codex's most-used view — useless. Twelve traits and eight raiders translate for meaning, and the translator's brief is that the name must make the mechanic guessable in the target language, not that it must be faithful to the English.

**Species and regions are proper nouns.** Vetch, Ember, Skitter, Hollow, Loam and Pale are animals; Threnody and Kettlemoor are places. They appear in the Character Bible, in marketing, in the store icon, and in the name a player gives a Founder in the first five minutes. Translating *Vetch* into a local legume breaks the shared vocabulary the community forms around and gives the brand six different faces.

**That Vetch and Loam are obscure words even in English is the point.** They read as species names rather than as descriptions, which is exactly what transliteration preserves.

**Aberrants translate for meaning**, since they are effects — with one exception that should not survive contact with a translator.

### 3.1 Rename Stillborn

`broodline_combat_numbers.md` §4.4 names an Aberrant Instinct **Stillborn**, meaning a creature that never moves.

In a game about breeding animals, at a 12+ rating, that is a poor word in English and considerably worse once translated literally into a language where the clinical register does not cushion it. Localization is where this becomes unavoidable, because a translator will ask.

**Recommend renaming it to Rooted.** Same meaning, no collision with Burrow or Regrow, and nothing to explain.

---

## 4. What does not need translating

Worth naming, because three earlier decisions turn out to have paid for themselves here.

**Behaviour previews carry no text.** `broodline_trait_codex.md` §5.2 chose animated previews over written descriptions to teach eleven behaviours. Eleven animations localize for free, where eleven paragraphs of behaviour description would have been the hardest text in the game to translate — behaviour prose drifts, and a drifted description of Overwatch is a player who mis-builds a roster.

**"Effects are numbers, never adjectives" is also a localization rule.** *"Hits 2 targets within 1 tile"* survives translation intact. *"Improves area damage"* becomes six subtly different claims about what Splash does.

**Creature and raider art carries the mechanic.** `broodline_raider_roster.md` §4's rule that the answering trait must be readable off the silhouette means a player who cannot read the Codex in their language can still see that Bulwark's shield is the problem.

---

## 5. Word count and cost

Rough, and enough to budget against.

| Surface | Words |
|---|---|
| UI strings across 59 screens | ~4,000 |
| Codex — 34 entries plus the threat board | ~6,000 |
| Campaign, onboarding and event copy | ~3,000 |
| Store listing, ASO metadata, screenshots | ~1,500 |
| Legal, support, settings | ~2,000 |
| **Per language** | **~16,500** |

**Nothing at launch.** Each language added is roughly 16,500 words at $0.12–0.20 per word with a review pass — **about $2,000 to $3,300 per language**, plus its filter, its font and its QA pass. Fifteen languages over time is in the region of $30,000 to $50,000 in translation alone, spread across however long the rollout takes.

**The Codex is the largest and the most sensitive block.** It is a disclosure mechanism attached to a paid randomised action, and a mistranslated coverage tier is a compliance problem rather than a polish problem. It should go to a reviewer who has played the game, not to a general pool.

---

## 6. Pricing

**Verify monotonicity per storefront, and adjust shard counts rather than prices.**

Prices are constrained to Apple's tier grid, so they cannot be tuned freely. Shard counts can. In any storefront where the tier grid would produce a ladder that inverts — the $9.99 equivalent delivering better value per unit than the $19.99 equivalent — **the shard counts move, not the prices.**

The ladder to preserve, from `broodline_economy_model.md` §6, is value per unit currency rising with every step. Six tiers, monotonic, in every storefront.

**Use Apple's regional pricing recommendations rather than straight conversion.** Turkey, Brazil, Indonesia and Russia carry substantially lower spend per player, and converting US prices at the exchange rate produces a store nobody buys from.

**Consistency is within a storefront, never across them.** A Japanese player and a US player may receive different shard counts for equivalent tiers. That is normal and defensible. What is not defensible is a player seeing an inverted ladder in their own currency — that is the thing they screenshot.

---

## 7. Moderation filter coverage

The external dependency, and the one to start first.

`broodline_moderation_ugc.md` requires filtering on five UGC surfaces, and coverage is per-language. **At launch that is English.** Procure the English filter before soft launch ends, and **choose a vendor that covers the full §2 list**, because the vendor relationship is the long pole and switching vendors mid-rollout means re-tuning every language already shipped.

**The loose-filter rule does not travel.** That document argues creature-name filters should be tuned loose rather than tight, because names are short and archaic and a false rejection lands on the emotional beat of session one. Tuning loose in a language nobody on the team reads is how a slur ships in a modal dialog for two years.

**So: loose in English, native-reviewed per language, with a human review queue rather than an auto-block for borderline names.** A name in review is provisionally accepted and displays normally to the player who chose it. Blocking at the moment of naming is the failure this is avoiding.

**Chat filtering can be tighter than name filtering** in every language. Chat is ephemeral and a filtered message costs a retype; a rejected Founder name costs the beat the whole onboarding is built around.

---

## 8. Typography

**The launch font pairing covers the launch language and nothing after position 3.**

Baloo 2 and Nunito cover Latin, and Nunito extends to Cyrillic and Vietnamese. **Neither covers Chinese, Japanese or Korean.** Japanese, Korean and Chinese are among the largest markets for this genre, and without a decision they will fall back to a system font — which means the display voice the brand is built on is absent in the places it matters most.

**Recommend Noto Sans SC, JP, KR and TC as substitutes**, weight-matched to Baloo 2's display role, chosen deliberately rather than inherited from a fallback chain.

**The tabular-figures requirement must be re-verified in every substitute.** It is carried from the art direction and it is not decoration — misaligned digits in a shard count or a probability table read as a bug.

**Design to +40% text expansion.** German runs about 35% longer than English and Russian about 30%. CJK is shorter but needs more line height. **Test German as the worst case on every screen**, and treat the pre-wave composition panel and the splice forecast as the two most likely to break, since both pack labelled numbers into fixed rows.

---

## 9. Jurisdiction

Age gating varies, and `broodline_moderation_ugc.md` §5 specifies one gate.

| Region | Threshold | Effect |
|---|---|---|
| United States | 13 (COPPA) | Under-13 restricted mode |
| European Union | 13–16 by member state | Restricted mode at the higher local threshold |
| United Kingdom | 13 | Plus the Age Appropriate Design Code |
| Korea | 14 | Plus a parental consent flow |
| Elsewhere | 13 | Default |

**The gate takes a date of birth and resolves the threshold from the storefront**, rather than asking the player to know their local rule. Restricted mode is the same everywhere it applies — no free-text naming, no chat, browse-only Recipe Share — so only the threshold varies, not the product.

---

## 10. Guardrails

- The shard ladder is monotonic in every storefront, verified per storefront
- Species and region names are transliterated and never translated
- Trait and raider names are translated for meaning, and the brief is that the mechanic must stay guessable
- Filter coverage exists for every language a server runs in, before that server opens
- Name filtering is native-reviewed per language, never auto-blocked on a loose English rule
- Tabular figures verified in every font, in every language
- Restricted mode is identical everywhere; only the age threshold varies
- No language ships partially. A language is complete or it is not offered
- One language at a time. Each is complete before the next starts

---

## 11. Open questions

1. ~~**Is seven the right launch set?**~~ **Resolved — English only at launch, then one language at a time in the §2 order.**
2. **Should region names be transliterated or given native-register equivalents?** Thirty place names in a fictional setting is exactly the case where a good localizer adds more value than a rule does. The species rule should hold regardless; regions are arguable.
3. **How is the Codex kept in sync?** It is the largest text block and it changes whenever a trait value is tuned. A tuning pass at soft launch could invalidate six languages of copy at once. Freezing coverage values before translation begins is the obvious answer and it constrains tuning.
4. **Does alliance chat need real-time translation?** Comparable titles offer it and it materially helps cross-region servers. Servers are regional here, so the need is smaller — but it interacts with the filter, since a translated message must be filtered in both languages.
5. ~~**Arabic's deferral should have a date.**~~ **Resolved — position 13, after the layout has stopped changing.**

---

*Owns: the launch language set, naming policy, translation scope and cost, per-storefront pricing rules, filter procurement, font coverage, and jurisdictional age thresholds. Does not own: the shard ladder itself (`broodline_economy_model.md` §6), moderation mechanisms (`broodline_moderation_ugc.md`), or the design tokens and type pairing (bible §10).*
