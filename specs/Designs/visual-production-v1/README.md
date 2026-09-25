# Broodline visual production board v1

Status: implementation board, 2026-09-24. The [three approved screen concepts](../visual-upgrade-mockups-v1/README.md) and the user's [Cinderplate character target](cinderplate-character-crop.png) set the quality target. This board covers all 48 named entries in the [screen inventory](../../broodline_screen_inventory_v2.md). “Unity” means a current surface can receive production art now; “design” means the composition and asset brief is ready for its later feature implementation. These directions preserve the game model and navigation. The delivery column describes where a screen exists, not whether its visual work has passed review.

## Shared screen grammar

- **Character stage:** a creature fills 55–70% of the available hero width, with full feet/wingtips, a contact shadow, restrained background depth, and a clear 40px silhouette. The same assembled 3D body and two trait attachments appear in hero, card, deploy, and combat views.
- **Scene stage:** the Ark, defile, and atlas are the primary visual information. Use a compact ivory HUD over deep slate scrims; reserve brass for structure and violet for the primary action or energy. Keep the straight battle lane and its pockets unobscured.
- **Paper layer:** warm ivory `#FFF8EB`, parchment `#F5EBD9`, ink `#302D40`, slate `#263345`, violet `#6B4EC2`, and brass `#E9B44C`. Use broad soft bevels, shallow shadows, generous corners, and original symbols. Text remains live UI Toolkit text.
- **Mobile hierarchy:** one scene or hero, one decision area, one primary action. At 360×640, secondary content scrolls rather than shrinking portraits, text, or 44pt-equivalent controls. Long names wrap; 40% text expansion does not clip. Ordinary text targets 4.5:1 contrast, large text and essential boundaries 3:1.
- **State examples:** every family needs populated, empty, loading, blocked/error, and successful variants where the underlying flow supports them. Store keeps its existing catalog, currency, and offer rules.

## All 48 inventory entries

| Screen | Delivery | Composition and focal art | Visual state to review |
|---|---|---|---|
| Founder Naming | Unity | Vetch on a mossy habitat against the Frontier; face and planted feet dominate above a calm naming card. | Default and long name, no portrait fallback, narrow phone. |
| Splice Chamber | Unity | Two large parent portraits flank a restrained violet chamber aperture; trait sockets and odds remain legible below. | Empty slots, both selected, locked slot, insufficient charge. |
| Splice Confirm | Design | Two parent busts converge above the consumption sentence; named founder gets a distinct coral caution panel. | Standard and named founder confirmation. |
| Splice Reveal | Unity | Cinderplate fills the workshop hero stage; three coral dorsal spikes and flank carapace read separately. | Inherited result, mutation, long child name. |
| Creature Roster | Unity | Spacious two-column cards with large silhouettes; assignment and two trait symbols sit below each face. | Six species, empty roster, filter result, long name. |
| Creature Detail | Design | Full-height rotating 3D creature over a habitat plinth, with a compact two-slot trait rail and assignment sheet. | Both slots, one slot, Aberrant, assigned creature. |
| Lineage View | Unity | Child hero at top, five-generation ancestry descending as connected portrait medallions and a species strip. | Founder root, mixed species, deep ancestry, missing ancestor. |
| Trait Codex | Design | Illustrated trait specimens on parchment; symbols, category, counter, and tier read in one glance. | Locked, owned, tier comparison, unknown counter. |
| Growth | Design | Same body at two ages side by side on matched plinths; simple age timeline, no feeding cue. | Young, mature, maximum cosmetic growth. |
| Gene Ark | Unity | 3D Ark and six distinct facilities occupy the top scene; resource and action card is a compact lower shelf. | Home, facility focus, travel/pack visual phases. |
| Campaign Select | Unity | Region panorama and straight-lane preview above a compact wave list; raider silhouettes precede Start. | New wave, replayed wave, locked wave. |
| Wave Defense | Unity | Field occupies nearly the entire phone; allied bodies and three raider types stay separable; HUD is slim. | Deploy, active wave, Rally, low integrity, reduced motion. |
| Post-Wave | Unity | Ark safe at dawn behind a large outcome emblem; rewards and affected creatures sit in a short lower ledger. | Win, no creature grant, non-win. |
| Wave Defeat | Unity | Breaching raider silhouette and broken Ark light over coral dusk; counter trait and free retry are prominent. | Named counter, long raider name, repeated loss. |
| Replay Viewer | Design | Full battlefield with scrub rail and event markers below; camera and units retain live-battle scale. | Pause, seek, decisive counter, replay unavailable. |
| Regeneration Tracker | Design | Quiet hatchery stage with resting creature portraits and timer chips, no injury gore. | Several recovering, one ready, empty. |
| World Map | Unity | Illustrated atlas with region landmarks and deliberate depth; heat tint is subordinate to route and Ark markers. | Nearby region, route, selected region, alert, zoomed phone. |
| Region Detail | Unity | Landmark panorama and region crest over territory and lane facts; route/gate data in a readable lower sheet. | Home, remote, contested, unreachable. |
| Relocate Ark | Design | Destination panorama and Ark travel vignette; time, lane count, and costs form one decision card. | Available, packed, blocked move. |
| Apex Alert | Design | Apex silhouette behind a time pulse; catalyst yield and scout detail are the two visual priorities. | Unscouted, scouted, nearly expired. |
| Collector Dispatch | Design | Collector and escort creature busts beside cargo; route choices sit on an illustrated atlas strip. | No escort, escorted, insufficient capacity. |
| Route Plotter | Design | Atlas route is the hero; each gate is a labeled pennant with controller and exposure minutes. | Direct, allied gate, night move, blocked route. |
| Convoy Status | Design | Moving Collector marker and next gate dominate; window countdown only appears while exposed. | Safe travel, approaching gate, inside window, arrived. |
| Collector Intercept | Design | Opposing convoy on a narrow road, with a distinct short decision clock and ally escort presence. | Available raid, ally escort, expired window. |
| Transit Board | Design | Dense but legible atlas list with gate silhouettes; cargo band and escort count are visually separate. | Available, daily cap, target cooldown, empty. |
| Gene Lab | Unity | Same Ark 3D stage with six unmistakable facility silhouettes, each highlighted by its own icon and light. | Facility sheet, upgrade available, core cap, timer. |
| Splice Roulette | Design | Rotating six-trait display around a central sample vessel; probability table and pity counter stay visible. | Before first spin, spin result, pity threshold, no samples. |
| Sample Store | Design | Ceramic shelves of twelve species samples; inventory capacity is an instrument, not a sales meter. | Samples, Fuse, Archive, 88% capacity. |
| Alliance Hub | Unity | Alliance pennant over an illustrated territory table; roster and rally cards keep creature contributions visible. | No alliance, member, Leader, active rally. |
| Alliance Rally | Design | Three large committed creature silhouettes against a stake panorama, with counter traits clearly attached. | Forming, ready, failed counter, success. |
| Stake Management | Design | Five pennant slots on an atlas inset; hold accrual and weekly tick form a clear timeline. | Empty, owned, contested, decay warning. |
| Garrison Assignment | Design | Stake wall and committed creature portraits; contributor names sit beside, not on, artwork. | Full, empty, last available counter warning. |
| Stake Assault | Design | Target stake panorama above one clean deployment row; rate limit reads as a status, not an offer. | Open, cooldown, capacity blocked. |
| Alliance Tech | Design | Three logistics branches as engraved routes from a central pennant; treasury progress is compact. | Locked, fundable, completed. |
| Join / Apply | Design | Alliance banner and territory thumbnail lead each listing; application state has a clear seal. | Open join, approval required, pending, no result. |
| World Chat | Design | Calm parchment conversation surface with small attached creature and recipe art, readable sender hierarchy. | Long message, attachment, report sheet, blocked sender. |
| Store | Unity | Distinct original art for each product family; free gift leads, direct shards remain plain rows. | Tabs, small/standard/large chest, pass, gift claimed. |
| Geneticist Profile | Design | Geneticist emblem and laboratory vista, with tier progress as a restrained brass instrument. | Current tier, next tier, max tier. |
| Splice Census | Design | Community progress as a living genetic tree; 40/70/100% milestones have distinct blooms. | Live, milestone claimed, dark period. |
| Event Hub | Design | One seasonal key-art panel, then a clean calendar and active-event cards. | Active, soon, completed, empty. |
| Apex Cup | Design | Gauntlet arena and large creature silhouettes above a compact ranked ledger. | Bracket, anonymous row, report action, final result. |
| Recipe Share | Design | Ancestry chain is the image; structured trait symbols and rating sit beneath, without free-text fields. | New recipe, rated, reported, empty. |
| Marks Shop | Design | Route and defense artifacts displayed on a slate workbench; one Marks total and straightforward costs. | Affordable, insufficient Marks, owned. |
| Onboarding | Unity | Each beat has one striking creature or field moment and only the control needed for that beat. | Intro, first counter, first loss, lineage ending. |
| Mail / Notifications | Design | Stamped parchment envelopes grouped by urgency, with small source emblems. | Unread, reward, empty, delivery error. |
| Settings / Account / Support | Design | Quiet ivory utility sheet with ample spacing and original account crest. | Support contact, block list, age-gate result. |
| Age Gate | Design | Friendly original creature vignette above a plain age decision, with no dark pattern. | Unanswered, restricted, standard. |
| Report / Block | Design | Focused sheet retaining message or recipe context; two actions are visually distinct. | Report, block, confirmation, unavailable context. |

## Current production status

The 32 entries marked **Design** above have composition and state briefs. The [offline future-screen gallery](future-screen-studies.html) presents a 390×844 layout study for each entry, with selectable state notes and an asset specification. Its Cinderplate, Vetch, and Ember examples now use crops from the approved character image; other character art remains provisional. These crops demonstrate the intended finish in static studies and are not assembled runtime assets. Their Unity implementation belongs to later feature work. For the 16 existing Unity entries, the current pass is:

| Existing Unity screen | Current visual production state | Next art or review gate |
|---|---|---|
| Founder Naming | Enlarged live Vetch, habitat plinth, painted Frontier backdrop, compact phone layout | Authored Vetch model, acting, and phone approval |
| Splice Chamber | Compact phones now lead with the predicted portrait; parents remain below | Larger parents, chamber environment, empty/blocked states |
| Splice Reveal | Enlarged live child and workshop plinth | Cinderplate form language, authored child and parent art |
| Creature Roster | Larger assembled portraits in cards | Six final species and 40px cast review |
| Lineage View | Assembled portrait medallions in ancestry tiles | Connected ancestry composition and deep-tree states |
| Gene Ark | Live Ark stage over painted Frontier, compact action shelf | Authored Ark and six facility structures |
| Campaign Select | Larger region panorama and distinct next-wave row | Raider silhouettes and lane preview |
| Wave Defense | Populated deploy stage and compact phone layout | Running-build battle capture, raider distinction, HUD placement |
| Post-Wave | Painted victory scene and outcome seal | Battle-to-outcome clip and all grant states |
| Wave Defeat | Hollow Reach breach scene and emblem | Breaching raider key pose and loss clip |
| World Map | Illustrated atlas; 44px marker hit areas with 32px faces; selected destination appears above the map on narrow phones | Landmark, route, alert, and zoom passes |
| Region Detail | Existing region surface | Landmark and remote/contested states |
| Gene Lab | Ark stage and six 44px facility markers now lead above the Core card on short phones | Distinct facility meshes and timed states |
| Alliance Hub | Pennant against a subdued territory atlas | Roster and rally art |
| Store | Existing product icons and catalog; the user's prior `StoreView.uss` edit is preserved | Original product art and all purchase states |
| Onboarding | Founder and reveal beats receive the showcase art path | Remaining beat-by-beat visual review |

This is an in-progress production pass. No turnarounds or final character source files are available, so the approved concepts and existing character bible are the working references. The [before/after character set](CHARACTER_SET.md) defines six base bodies, the twelve modular trait pieces, and the first after-splice proofs; the [production art brief](ARTIST_BRIEF.md) lists the remaining paintovers, materials, and screen-family art. An external artist is optional. The [authored asset contract](../../../client/Assets/Frontier/Art/AUTHORED_ASSETS.md) allows the 3D cast to replace procedural bodies without changing gameplay or UI dependencies. The shared procedural assembly now mounts all twelve catalog traits in either slot; the first five had live parts before this pass. No authored creature prefabs, running-build clips, or physical A14 measurements have been delivered yet. Fixture screenshots are review evidence, not visual acceptance.

## Review evidence from this pass

- [Showcase before/current sheet](review-showcase.jpg): Founder Naming, Ark home, Splice Reveal, and deploy. The current Founder and Reveal examples use live 3D portrait fixtures; their before examples use the former sprite fixtures.
- [Supporting before/current sheet](review-supporting.jpg): Campaign, Lineage, defeat, and Interrupted. These are Unity fixture captures at 430×932, not a running-build clip.
- [Six founder species and six paired-trait assemblies at a native 40px](frontier-40px-review.png): the actual Frontier portrait path at mature roster growth, plus a 5× inspection view. The species colors and main shapes are distinguishable, but several attachments are too subtle at native size; final authored art must give both slots a stronger silhouette. The pairs cover all twelve catalog traits once each.
- The final frame sweep rendered all 37 fixtures at 430×932, 402×874, 390×844, and 360×640. The narrow reveal uses measured name height to keep a long two-line name inside its hero panel. World Map puts the selected region name in the atlas header when a different marker is selected, so it is visible before scrolling. Its markers use 44px hit areas around 32px visible faces; the route fixture's geometry probe confirms the hit dimensions at 430 and 360.
- The offline gallery's 32 names match all 32 **Design** rows above; its script parses, all referenced files exist, and a local browser rendered the default compositions. Filter and state buttons are intended for review notes, not game navigation.

The Unity gate ran **601/601 EditMode tests**, captured **37/37 fixtures at each of four frame sizes**, and compiled **53/53 stylesheets**. Current token, silent-drop, settings, gallery, and diff checks pass. These results verify source and fixture stability only within their stated scope. Safe-area states, 40% text expansion, live battle footage, physical-device performance, and user phone-size approval remain open gates. Xcode currently lists the available physical iPhone and iPad as offline.

## Existing surfaces outside the 48

Codex and facility sheets use the same ivory sheet header, large specimen/facility art, and brass dividers. Confirm dialogs and toasts inherit the button and status vocabulary; Interrupted uses a quiet slate recovery scene. Store tabs and all chest sizes, remote-region preview, and deploy pocket selection receive separate captures. These are states of existing entries rather than new inventory rows.

## Review sequence

1. Prove Founder Naming, Ark home, deploy/live battle, and Cinderplate reveal at phone size against the approved concepts.
2. Approve six founder bodies, two modular trait sockets, three first-wave raiders, the Ark/facilities, and one battlefield environment.
3. Apply shared cards, HUD, atlas markers, sheets, and outcomes to the Unity entries. Use each row's listed states as the capture checklist.
4. Produce full-size art boards for the design entries as their feature work is scheduled. A row here is an implementation brief, not a claim that later functionality or a painted screen exists.
