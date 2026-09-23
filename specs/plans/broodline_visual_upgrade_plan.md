---
status: proposed
date: 2026-09-22
visual-direction: approved-2026-09-23
note: >
  Visual upgrade proposal following Phase 9 and the user's Kingshot reference.
  Describes future work; does not certify Phase 9, replace the current bible,
  authorize asset purchases, or claim that the proposed art has been produced.
---

# Broodline — Visual Upgrade Plan

**Visual direction approved, 2026-09-23.** After reviewing the founder naming, live battle, and corrected splice reveal mockups, the user said, “I like it.” The [v1 mockups](../Designs/visual-upgrade-mockups-v1/README.md) are now the visual reference: expressive creatures, rich natural environments, a crafted Ark, warm ivory panels, and violet actions. Preserve this direction when translating it into Unity; simplify surface detail where phone readability and performance require it. This approval establishes the visual target; production estimates and asset procurement remain planning assumptions. The mockups are concept art, not verified runtime output.

## 1. Recommendation and evidence

**Implementation update, 2026-09-23:** The user approved proceeding. A standalone founder → formation/battle → Cinderplate art proof is now implemented in source under `client/Assets/Frontier/`. See the [setup and validation record](../../implementation/2026-09-23-frontier-proof.md). The user will run it on their existing Unity machine. Local simulation/source checks passed; Unity rendering, visual acceptance, device profiling, and production-scene integration remain pending. This does not mark the full first-hour slice or the plan below complete.

**Build a warm, character-led creature adventure with the depth and visual confidence of a polished mobile strategy game.** Keep Broodline's species, splicing, lineage, and mobile Ark. Give those ideas expressive characters, recognizable materials, a believable environment, and a stronger interface hierarchy.

Changing colors and adding gradients will help, but it will not close the main gap. The largest gains will come from character design and animation, battlefield composition, and presenting creatures as the stars of the interface.

### What the Kingshot reference teaches

The official [character render](https://www.centurygames.com/wp-content/uploads/2025/04/lordchar.png) uses a strong pose, an expressive face, broad readable shapes, and distinct cloth, metal, and leather surfaces. The [hero presentation](https://www.centurygames.com/wp-content/uploads/2025/04/kingshot-4.png) gives the character most of the upper screen and groups supporting information below it. The [battle image](https://www.centurygames.com/wp-content/uploads/2025/04/kingshot-2.png) frames the action with cliffs, trees, buildings, shadows, and a clear route.

These are visual observations from promotional images linked by the [official Kingshot page](https://www.centurygames.com/games/kingshot/), not an audit of its live client. Adopt those presentation principles through original Broodline art. Medieval heroes, castles, rarity ladders, and city-building mechanics are not part of this proposal.

### What the repository establishes

| Area | Current evidence | Consequence for this plan |
|---|---|---|
| Art direction | Bible §10 and Phase 9 specify rounded pastel UI, six distinct silhouettes, modular traits, and restrained animal personalities | Preserve functional shape rules; explicitly revise the earlier restriction on facial personality |
| Creature construction | C# primitive recipes generate skinned meshes; the shader uses base/underside colors, a lighting ramp, and rim light | Keep the assembly infrastructure; introduce authored meshes, surface detail, and a production material path |
| Animation | `CreatureMotion` applies breathing, bobbing, flinch squash, droop, and desaturation mainly to the root | Add species-specific movement and acting; more polygons alone will not make the creatures feel alive |
| Trait attachment | Generated sockets are parented to the object root; assembly finds direct child sockets | Bone-following attachments must be implemented before articulated animation is trusted |
| Portraits | Cards stack separately baked body/dorsal/flank images; three hero moments use a live studio | Preserve cheap lists, but use complete assembled portraits for correct depth and consistent art |
| Battlefield | `LaneDressing` creates a flat field, straight strip, dashes, sphere trees, and a cylinder Ark | Replace the scene's art and framing, while keeping simulation positions and placement truthful |
| Interface | Shared UI Toolkit components, tokenized colors, Baloo 2/Nunito; body text is currently 12 and navigation 10 logical units | Reuse the component system; improve scale, contrast, hierarchy, and screen composition |
| Product finish | Phase 9 follow-ups record an empty-lane concern, empty tab callback, unreachable Codex, portrait lifetime gap, and missing campaign terminal | Include the presentation-related functional fixes in the playable slice |

**Evidence limits:** the checkout contains Git LFS pointer files instead of the inspected PNG/PDF assets, and the ignored runtime screenshot corpus is absent. This review uses source code, HTML design source, specifications, phase records, and the external reference images. It does not claim a fresh visual inspection of the running game. Phase 9's record distinguishes simulator walks from physical-device validation; its exit gate must not be treated as complete. The root README and spec index also contain stale status/path descriptions, so current code and the later phase follow-ups take precedence for implementation facts.

### Planning defaults

- First delivery: a finished first-hour playable slice, followed by the rest of the game's visual system.
- Audience and platform: existing portrait mobile strategy audience and the bible's approachable creature tone.
- Recommended production: AI-assisted concepts and exploration, with authored 3D models, rigs, animation, and coherent UI assets. No commission or spending is authorized by this document.
- Preserve gameplay rules, server authority, progression, economy, and replay compatibility. Future maps and facilities receive a visual roadmap rather than silently becoming new gameplay scope.

## 2. The proposed visual identity

**Working direction: “The Living Frontier.”** A traveling sanctuary and genetics workshop in a lush, unfamiliar world. Creatures are curious, capable animals that the player wants to name. Technology feels crafted: enamel panels, warm wood, brushed brass, glass chambers, and restrained violet energy.

Use large clean forms, soft directional light, grounded shadows, and selective surface detail. Keep backgrounds quieter than the creatures. Concentrate the brightest highlights on the active decision, a trait activating, or a new hybrid being revealed.

### Characters

Retain the six role silhouettes. Add readable eyes, gaze, eyelids, small facial movements, and species-specific posture where anatomy supports them. Faces add attachment; silhouette and the two trait parts still communicate combat identity. This is a proposed change to Phase 9's “personality ... not in a face” ruling, to be recorded in the bible when the direction is accepted.

| Species | Character brief | Material and motion brief |
|---|---|---|
| Vetch | Sturdy, dependable low dome; embedded face with a confident gaze; four planted feet and no added neck | Pebbled teal hide, broad smooth keratin plates, cream underside; deliberate steps, weight shift, bracing response |
| Ember | Proud, energetic upright creature; narrow torso and a strong crest remain its signature | Coral hide and warm crest highlights; alert head turns, springy movement, forceful attack recoil |
| Skitter | Inquisitive, restless little scout; tiny body and six readable legs | Amber body, darker feet, restrained smooth highlights; alternating leg rhythm and quick attentive pauses |
| Hollow | Watchful, elegant specialist; long forward neck and stilt legs remain unmistakable | Violet hide, dark extremities, pale accents; precise aiming posture and measured steps |
| Loam | Calm, companionable segmented ground-hugger; blunt snout, no added legs | Moss-green hide, broad folds, warm underside; coordinated segment undulation with parts riding a defined segment |
| Pale | Graceful, observant glider; broad wing arc and small suspended body | Pale blue membrane, darker supporting structure and core; deliberate wing motion and clear ground shadow |

**Cinderplate is the hero asset.** Derive it through the same modular pipeline as every other hybrid. Use it for the reveal proof, key art, and eventual app icon. Do not invent a bespoke mascot model whose in-game equivalent cannot match it. The code currently implements Chill, Taunt, Splash, and Carapace; a Cinder-equipped mascot render does not imply Cinder gameplay is implemented.

Each species needs a turnaround, material sheet, neutral and expressive poses, and its silhouette with both trait slots populated. Production motion covers idle, locomotion, attack, hit response, exhausted/downed pose, and reveal/celebration. Use shared clip structure with species-specific performance. Growth changes proportions on the same rig; it must not alter combat reach, collisions, or stats.

Keep all twelve trait parts, with slot 1 on `sk_dorsal` and slot 2 on `sk_flank`. Each part must work in either position. Produce the existing 48-placement proof on Vetch and Pale, simultaneous pair tests, and a deforming Loam segment test. If one shape cannot work at both sockets, use the rig brief's explicit fallback of two socket variants per trait; never restrict legal breeding combinations to fix art.

Raiders keep darker values, angular shapes, and distinct threat cues in the same rendering style. Polish the three implemented types first: Courser, Lash, and Skirmisher. Later production follows the current raider roster's four body families and eight variants, rather than commissioning eight unrelated rigs.

### Color system

Use warm readable panels over deeper environmental and portrait backdrops. Violet remains the brand and primary action color; gold is reserved for rewards and milestones. These are proposed starting tokens, to be validated in the proof screens before full rollout.

| Role | Proposed value | Use |
|---|---|---|
| Deep backdrop | `#263345` | Portrait stages, background framing, HUD panels |
| Warm paper | `#F5EBD9` | Main interface background where scenery is absent |
| Raised surface | `#FFF8EB` | Cards, sheets, forecast information |
| Main ink | `#302D40` | Body copy and numbers on light surfaces |
| Secondary ink | `#686177` | Supporting labels on light surfaces |
| Brand/action | `#6B4EC2` | Primary CTA, selection, genetic energy |
| Action shadow | `#49348D` | Button depth and pressed state |
| Reward gold | `#E9B44C` | Rewards and earned milestones, with dark text |
| Success | `#286846` | Success text and state indicators |
| Danger | `#B53E3E` | Errors and irreversible-action emphasis |

Calculated flat-color contrast for the proposed main, secondary, success, and danger text on the raised surface ranges from 5.34:1 to 12.62:1; white on the primary action is 6.01:1. These calculations validate the starting swatches, not the final rendered UI, which still needs the checks below.

Retain the six existing species identity swatches initially: Vetch `#6BA7C0`, Ember `#E5867A`, Skitter `#E8B34A`, Hollow `#7A6AC0`, Loam `#7CC492`, Pale `#C6CEDE`. Give models richer shadows, accents, and material variation instead of repeatedly changing these measured identity colors. Separate species tokens from status tokens so Loam green is not automatically “success” and Ember coral is not automatically “danger.”

Pale gets a dark portrait backdrop and darker structural features; a stronger white rim alone is insufficient. Every selection, threat, tier, and resource state needs shape, text, or a glyph in addition to hue.

### Typography

Retain the bundled **Baloo 2 Bold** for short titles and **Nunito Regular/Bold** for readable UI. The present pairing is compatible with the proposed tone; replacing it is lower value than fixing small type and excessive density.

Use a revised logical type scale: hero 30, screen title 24, section 18, card title 16, body 14, supporting label 12, CTA 17, tab label 12. No decision-bearing label or number below 12 in the new design. Decorative eyebrows may use 11. Add the regular-weight font asset where needed, and verify tabular numerals and ambiguous characters in the actual Unity font assets.

Allow scrolling and wrapping instead of shrinking text. Keep the localization spec's +40% expansion test. English remains the launch scope; deliberately chosen CJK font coverage belongs with the later language rollout.

### Icons and interface surfaces

Use two coordinated icon families:

- **Game objects:** original colored, shaded symbols for resources, systems, and rewards. Consistent three-quarter lighting, broad shapes, and a restrained outline. Navigation symbols: a folded terrain map, an Ark cabin, two joining gene forms, a flask, and an alliance pennant.
- **Functional controls:** simple solid or strong-stroke back, close, lock, information, check, warning, and timer glyphs. Preserve familiar meanings and visible labels.

Give the twelve traits their own symbol vocabulary: plate for Carapace, outward call for Taunt, ember crest for Cinder, expanding droplets for Splash, speed strokes for Sprint, clustered young for Litter, long upward lance for Reach, piercing tip for Pierce, sprout for Regrow, ground tunnel for Burrow, protective veil for Screen, and frost crystal for Chill. Symbols indicate traits; tier remains separate pips. Do not add rarity frames that imply a new progression system.

Author editable masters and export production sprites at appropriate densities. Test control symbols at 20–24 logical units, trait symbols at 24–32, and resource art at 32–48. Use nine-sliced panel/button art for shallow bevels, inset borders, and warm shadows. Keep text live. A panel should frame content without looking like another object competing with the creature.

## 3. Screen and world redesign

### First three proof screens

1. **Founder naming:** a large posed Vetch in a small habitat, a clear name field, one short line of personality, and an obvious primary action. The creature occupies roughly the upper half of the usable screen. Show responsive gaze/idle acting rather than constant showroom rotation.
2. **Deploy and live battle:** one finished terrain kit with the same art in the preview and battlefield. Visible pocket markers, readable deployed animals, clear incoming threats, and an Ark that looks like something worth protecting.
3. **Splice reveal:** show the actual assembled hybrid, then its inherited traits and lineage. Use a short anticipation/assembly/reveal sequence of about two seconds, with skip and reduced-motion paths. The result already committed by the server determines the reveal.

### Rest of the first-hour slice

| Surface | Intended change |
|---|---|
| Roster | Larger complete portraits, clear name/role, two legible trait badges, tier pips, strong selection state; use a two-column card grid with a single-column large-text layout |
| Splice chamber | Two recognizable parents feeding a prominent forecast; explicit body and trait-lock choices; odds and consumption information remain visible; advanced detail expands below |
| Campaign | Illustrated chapter header, clear current objective, compact completed/locked wave rows, and a deliberate end-of-available-content state |
| Battle HUD | Compact high-contrast information at safe edges, unobstructed playfield, clear Rally readiness and response; hide hub navigation during combat |
| Victory/defeat | Strong outcome headline, creature acting, grouped rewards or concrete counter diagnosis, and one clear next step |
| Lineage | Portrait nodes with unmistakable inheritance connections; consumed ancestors remain identifiable and accessible |
| Codex and recovery sheets | Match the new UI, remain reachable, preserve clear error/retry/forfeit language, and verify real hit targets rather than screenshot appearance alone |

The new visual reference supersedes old HTML handoff styling only after the proof is accepted. It does not supersede splice confirmation requirements or FTUE information gating. Preserve the tutorial's staged disclosure of systems.

### Battlefield art

Build one lush frontier defile kit first: terrain surface, stone edge/cliff modules, trees, shrubs, grass clusters, path edging, distant backdrop, placement platforms, and the Ark. Use low-frequency terrain detail, clustered props near the edges, and a clear middle play area. Add contact shadows, gentle foliage motion, occasional ambient particles, and restrained genetic glow.

Compose the lane diagonally through the portrait frame to improve use of screen height. Fit the camera to actual lane/pocket bounds and HUD-safe space. Keep simulation geometry straight for this slice; do not paint a winding road beneath units that move in a straight line. Reuse the same world positions for deployment, preview, and combat, with camera framing appropriate to each.

The Ark becomes an original mobile genetics sanctuary: sturdy platform, folded transport supports, compact timber/ceramic laboratory, brass fittings, and a violet core. Its readable silhouette is more important than tiny machinery. Model it once for battlefield use and later base presentation.

Later expansion adds environment kits for the existing region/chapter identities, plus authored landmarks and map symbols. Reuse modules across regions; thirty regions should not require thirty unrelated scenes. The full world map, Ark management, Lab, alliance, store, and event screens get the new visual language when their underlying features are implemented. Do not expose decorative navigation to nonexistent destinations.

## 4. Engineering and asset delivery

Keep Unity 6, URP, UI Toolkit, and the deterministic simulation. The essential changes are presentation infrastructure, not an engine replacement.

### Asset contracts

- Introduce a presentation-only `CreatureVisualDefinition` asset keyed by the existing species/raider identifiers. It holds the prefab, explicit socket references, animation controller, growth configuration, material setup, and portrait framing. `CreatureLook` remains the assembly input; no server DTO or saved creature format changes are required.
- Have `CreatureLibrary` resolve the visual definition. Keep generated recipes as a named prototype fallback. Separate generated outputs from authored art so regeneration cannot overwrite production models. Remove the requirement that an authored prefab must also have a procedural body recipe.
- Put `sk_dorsal`, `sk_flank`, and `sk_crown` on appropriate rig bones. Resolve them through an explicit binding component rather than direct-child lookup. Preserve slot assignment, coordinate conventions, and growth semantics. Validate missing sockets and unsupported assets during import/build.
- Keep `Broodline.UI` independent of `Broodline.Creatures` and `Broodline.View`. Game-side presenters supply images and presentation data to UI components.

### Rendering and portraits

- Extend the creature material path with UV/base-map support, broad material masks, baked surface shading, and controlled highlights. Validate shadows in both portrait and combat cameras. Use projected contact shadows for moving crowds and restrained real shadows for the environment; avoid a separate realtime light per creature.
- Make a complete assembled portrait the new source for cards. Use one offscreen renderer and a bounded cache keyed by species, ordered traits, visual growth stage, and art revision. Render only missing/changed visible portraits; keep a static fallback while queued. Start with 256px portraits and a 64-entry RGBA cache cap, approximately 16 MiB of color pixels before rendering overhead. Measure the real memory cost.
- Keep hero portraits live only while visible. Fix screen/studio lifetime together so a departing screen cannot briefly show a cleared creature. Use a deliberate three-quarter pose and optional user rotation rather than mandatory continuous rotation.
- Provide one deterministic capture pose and fixed lighting for portrait/contact-sheet regeneration. Runtime idle variation must not contaminate bake outputs.

### Combat motion and effects

- Add presentation events at the points where the simulation resolves attacks and effects. Expose tick, sequence, source, target, and effect kind through a read-only per-tick buffer; read it after each simulation step, including catch-up steps. Events do not enter authoritative state, RNG, replay serialization, or outcome hashing.
- Animate only events that actually occurred. Do not infer the attacker from a target's HP delta; simultaneous attacks and Splash make that ambiguous. Keep damage timing authoritative and use animation as a response, never as the trigger that applies damage.
- Add a presentation driver for locomotion, attack recoil, hit response, downed posture, Chill, Taunt, Splash, Carapace, breach, and Rally. Pool VFX, deduplicate by tick/sequence, and reset on replay/restart. Add cues for later traits when their simulation behavior exists.
- Reduced motion removes camera shake, repeated flashes, continuous rotation, and large reveal movement while retaining static trait/status information. Sound may reinforce these cues in a later audio pass; visual acceptance works muted.

### UI and visual dependencies

- Introduce semantic theme tokens for text, surface, action, selection, species, success, danger, and rewards. Migrate shared components before individual screens; remove obsolete tokens only after all references move. Update generated UI textures and palette tests with the new theme.
- Finish navigation for the currently implemented destinations and make the Codex accessible from trait help. Preserve unlock rules and draft screen state when navigating. Hide unavailable destinations until they have functionality.
- Before the visual walk, reproduce the Phase 9 lane concern separately in the deploy preview and live wave on the same build. Record the result even if it no longer reproduces. Fix campaign exhaustion and the portrait-clear transition as part of presentation finish. Background/foreground wave recovery must remain usable.
- Update the bible's art section and handoff guidance when the proof direction is accepted. Keep historical phase reports intact. Add a short current-state entry point that accurately names the implemented slice and the source of visual truth.

## 5. Milestones and acceptance

Each milestone has an observable output. Art approval means looking at the result at phone size and in motion; a passing structural test does not establish visual quality.

| Milestone | Deliverables | Exit condition |
|---|---|---|
| A — Recover a trustworthy baseline | Hydrated LFS art, current build, screenshot/video set, list of actual reachable screens, separate deploy/live-lane diagnosis | Evidence identifies the build, viewport, and execution environment; no claim of device testing based on simulator captures |
| B — Establish the visual target | Vetch/Pale/Cinderplate concept sheets; palette, typography and icon sheet; founder, deploy/battle, and reveal mockups | User accepts the direction at phone size before broad asset production; one consistent style across all proof surfaces |
| C — Prove production art in Unity | Vetch and Pale rigs; twelve modular parts; Cinderplate; bone/socket proof; first terrain/Ark kit; new material and portrait path | Modular proof passes, both traits read, renders match the approved target, and real-asset benchmark passes before the other four bodies are finalized |
| D — Finish the playable slice | Remaining four species, three implemented raiders, shared UI kit, redesigned first-hour screens, combat feedback, navigation/presentation fixes | Fresh-account loop through naming, battle, splice, reveal, and lineage works with consistent production art and no visible placeholders |
| E — Validate and extend | Device performance report, visual review, corrected regressions; then map/base/Lab/alliance/store visual rollout tied to feature delivery | Accepted playable slice and physical-device evidence precede broad expansion or TestFlight claims |

### Required checks

- **Character identity:** six species recognizable as 40px silhouettes, with and without attachments; Vetch and Pale distinguishable against their intended backgrounds; both trait parts legible in cards and combat. Inspect all 48 proof placements and stress pairs during motion and growth.
- **Portrait truth:** card and live model represent the same body, ordered traits, and growth stage. Verify depth/occlusion, cache invalidation, fallback, and scrolling through a large roster without retaining unbounded textures.
- **Interface:** captures at 402×874 and 430×932 logical sizes plus a smaller supported portrait viewport; safe areas, keyboard, sheets, long names, +40% text expansion, large text, disabled/loading/error states. Minimum 44pt-equivalent hit targets after scaling. Test actual taps near CTA and sheet edges.
- **Contrast:** adopt 4.5:1 for ordinary text and 3:1 for large text and essential control boundaries as project acceptance targets. Verify against the rendered background, including translucent HUD panels. Check grayscale and the existing color-vision simulations. These are design targets, not a claim of formal certification.
- **Motion and combat:** correct source/target feedback, Splash with multiple targets, effects across multiple ticks per frame, restart/replay reset, reduced motion, and muted play. The presentation-event path must leave existing outcome hashes and replay results unchanged with presentation enabled or disabled.
- **Regression:** run relevant creature/UI EditMode and PlayMode suites, deterministic corpus checks where the event boundary changes, stylesheet/token/silent-drop checks, and the shell probe. Run PlayMode through the supported route; the recorded batchmode deadlock is not a passing test. Record the inherited baseline failure by identity rather than hiding it or treating every red as equivalent.
- **Performance:** use the existing 104-entity harness and A14/4 GB floor with real rigs, materials, effects, HUD, and environment. Target 60 fps, retain the existing 30 fps degradation option and 600 MB peak-memory ceiling. The documented 10,000 triangles per assembled creature, including parts, is an upper bound from synthetic testing, not a production allowance to fill. Preserve benchmark thresholds and repeat the sustained device run with final assets.
- **Visual acceptance:** compare the same screen, pose, camera, and state before/after. Watch a real first-hour session. Obtain feedback from at least five first-time viewers on species distinction, trait identification, next action, and desire to keep the creature. Treat that as formative feedback, not statistically reliable retention evidence.

### Production scope and effort

For the first-hour slice, budget six player bodies, twelve trait parts, three implemented raider presentations (using shared rigs where practical), one Ark, one terrain kit, character animation, twelve trait symbols, five navigation symbols, the resource/control icon set, and one reusable UI kit. Instinct geometry remains contingent; produce the rig proof's one cue without commissioning six speculative attachments. Later roster production follows the four-body/eight-variant raider design.

A planning allowance for one Unity developer working with part-time art support is **8–12 calendar weeks for a strong first-hour slice after asset access is restored**, including iteration. This is an estimate, not a bid or a deadline; the Vetch/Pale proof is where to replace it with measured throughput. Full-game screen and environment expansion is additional work.

If paid art is unavailable, keep the same milestones but use authored procedural improvements and generated concept/painted assets within the available tools. Prioritize the Ark/environment, UI, lighting, and two showcase creatures. Do not assume generated pictures become rigged, interchangeable 3D characters automatically or promise the same animation quality on the same schedule.

**First implementation package:** recover the visual baseline, resolve the lane ambiguity, and prepare the Vetch/Pale/Cinderplate plus three-screen proof. That package makes the proposed quality jump concrete before rebuilding every screen or commissioning the full cast.

## Source map

The review surveyed the spec/design/phase inventory and focused its detailed reading on the authoritative creature/art model, accessibility/localization, rig proof, UI and client architecture, Phases 8–9 and their follow-ups, and the current rendering/UI implementation. It was not a line-by-line audit of every backend or archived specification.

- `specs/broodline_bible.md` — species, breeding semantics, art rules, mascot, FTUE constraints.
- `specs/broodline_rig_proof.md` — sockets, 48-placement proof, fallback, real-asset performance gate.
- `specs/broodline_raider_roster.md`, `broodline_screen_inventory_v2.md`, `broodline_accessibility.md`, `broodline_localization.md` — expansion scope and usability constraints.
- `specs/Designs/` and `design_handoff_broodline/` — existing HTML designs and original token vocabulary; raster/PDF references unavailable in this checkout.
- `specs/plans/broodline_phase9_the_look.md` and `broodline_client_architecture.md` — current presentation boundaries and budgets.
- `implementation/2026-09-18-phase9-followups.md`, `implementation/results/phase9-visual-review.md`, and `phase9-test-baseline.txt` — inherited gaps and limits of existing evidence.
- `client/Assets/Creatures/` — recipe, assembler, generator, shader, motion, and portrait-bake implementation.
- `client/Assets/View/` and `client/Assets/Game/Shell/` — battlefield, snapshot boundary, portrait/preview rendering, and navigation.
- `client/Assets/UI/` — components, fonts, icons, tokens, and screen structures.
