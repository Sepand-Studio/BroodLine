# Phase 10 — Map, Store, Lab and Allies design pass

The four hub destinations now have a clearer visual hierarchy in source. The World Map places the thirty interactive region markers over a painted terrain atlas, lets a tap select a region, and shows its name, lane/travel detail and the current preview action below the map. Its full labeled region list remains beneath the atlas. The raster at `client/Assets/UI/Resources/Art/map/frontier-atlas.png` was generated for this project without text or markers, so all navigation and status remain live UI elements.

Store leads with the daily gift on a dark brass-edged card. Each pack shows its own mark, contents, price and an explicit `Buy (preview)` action. The Custom Chest shows icons for each choice, a Small/Standard/Large tier control and a live summary of the selected three rewards. Lab leads with an Ark Core tier card and puts blockers in separate chips beside the facility actions. Allies uses a dark banner and a short list of planned cooperative tools below its current local preview action.

Campaign Select now opens with a painted Hollow Reach chapter header, keeping the wave list and its availability rules unchanged. The generated illustration is in `client/Assets/UI/Resources/Art/campaign/hollow-reach.png`; its chapter labels remain live UI text.

Review layout and interaction direction in `implementation/reviews/phase10-ui-review.html`. It is a browser composition preview, not a Unity capture. At the user's request, Unity validation and visual acceptance are deferred until the end of the UI/design work. The remaining visual work is the icon language and final polish after the Unity screen review.

## Region detail follow-up

Map selections now lead to a region detail screen. The Ark's current region has a painted territory header above the existing server-backed harvest cards and a back path to the map. Remote regions show their reach, lanes, neighbours, shortest route and travel time from the server-owned Ark region. The relocation action starts a persisted local countdown in `StubLedger`, with every write logged as `[stub]`; it never changes the server region or exposes remote claim buttons. A second journey cannot start while a preview is active. The full route is visible both before and during the countdown, and completion explicitly leaves the Ark where the server says it is.

The three design states are in `implementation/reviews/phase10-region-detail.html`. That page is a composition preview, not a Unity capture. Per the user's direction, Unity/device validation is still deferred until the end of the UI/design phase.

## Trait icon language

The twelve authored traits now each have a distinct 24-unit silhouette. `client/Assets/UI/Art/generate-trait-icons.py` draws white, tintable 3× PNGs from one geometry source and saves editable SVGs under `implementation/art-source/traits/`. The shared `TraitChip` and `TraitPip` bind those marks by trait name, so roster, splice, Codex and any later use of those components inherit the same icon. Species colour continues to tint the chip, while the icon shape and text carry identity without colour. Unknown traits keep their label and receive no invented icon.

Review the contact sheet at `implementation/reviews/phase10-trait-icons.png`; it shows the large mark and a small chip-size sample for every trait. This is a source/design review. Unity rendering remains in the final validation batch.

## Primary navigation icon language

Map, Ark, Splice, Lab and Allies now have five authored marks in the same 24-unit, white-and-tint system as traits and facilities. The old generic tab PNGs were replaced in place, so tab bindings and state tinting stay unchanged. The generator and editable masters are `client/Assets/UI/Art/generate-nav-icons.py` and `implementation/art-source/nav/`; the large and actual 19px comparisons are in `implementation/reviews/phase10-nav-icons.png`. The old standalone Splice SVG was removed because it no longer matched the PNG. Unity tab rendering remains in the final validation pass.

## Portrait and atlas interaction follow-up

The roster card and sprite-form hero slot now request a Frontier portrait through a UI-facing source. One 256px offscreen camera paints at most one queued portrait each frame; duplicate requests share the job. The cache keeps 64 textures (about 16 MiB of RGBA pixel data), keyed by species, ordered traits, growth stage and art revision. The old baked layers remain visible while a request is queued or if a portrait is evicted; cards clear stale images on rebind, detachment and eviction. The Boot composition root owns the cache. Unity still needs to confirm appearance, capture memory and run the new eviction/rebind tests before the old baked art can be retired.

The atlas now has a clipped pan/zoom surface and 44px zoom controls. Rings, markers and the painted terrain move as one. Zoom stays between 1× and 2.25×, and panning clamps to the enlarged image. The labelled region list remains the larger-target alternate path. Unity touch input and narrow-screen composition remain part of final validation.

Selecting a remote region now reveals the catalog's shortest route on the atlas before opening Region Detail. Its stops are numbered from the Ark, non-route markers recede, and the selection card spells out the hop count, travel time and ordered region names. The Ark keeps its `A` marker. This uses `RegionCatalog.TravelMinutes`, the same source as Region Detail, so the two screens do not invent separate journeys. The new map test checks a distant selection and preserves the selected state on an invalid id; Unity visual and touch checks are still deferred.

## Ark facility identity follow-up

The five outer plots now have different procedural structures: joined incubation columns for Splicing, a low egg nest for the Hatchery, a sealed archive monolith for the Vault, a raised extraction head for Harvest, and a radial propulsion drum for the Drive. They replace the identical temporary signposts and remain inside the existing plot pads. The Lab uses six matching, individually authored facility glyphs, including the Core. Editable SVGs and the generator are under `implementation/art-source/facilities/` and `client/Assets/UI/Art/`; the contact sheet is `implementation/reviews/phase10-facility-icons.png`. Source style checks pass, while Unity framing, lighting and triangle-budget checks remain pending until final validation.

## Store and Allies follow-up

The Store now presents five canonical mixed-content bundles from $0.99 through $19.99 and keeps the $49.99/$99.99 shard purchases in a separate section. Seven distinct marks identify those products. Purchase rows separate product identity from price and the preview button, giving the latter a 44px minimum height. The Custom Chest has explicit Small ($1.99), Standard ($4.99) and Large ($9.99) controls; switching tiers updates all six reward amounts, the live chosen-content summary, price and preview purchase id without discarding picks. Standard remains the default. The UI deliberately makes no savings claim because the economy has no separate-item reference prices. The catalog says **sample pulls**, matching bible §8.3's rule against selling trait access, and the Season Pass still displays that section's $9.99 price and four-week term. The icons have editable sources at `implementation/art-source/store/` and a large/28px seven-mark sheet at `implementation/reviews/phase10-store-pack-icons.png`.

The catalog and chest-selection behavior were exercised through their red/green tests, including preserving the configured chest when claiming the daily gift refreshes the Store. The current Model, UI, UI test and Game assemblies also compile with Unity's bundled Roslyn when the September 23 Bee response files are supplemented with the sources added on September 24. Token, silent-drop and offline frontier-combat gates pass. This is source evidence only: Unity import, EditMode execution, Store interaction and narrow-screen captures remain in the final validation batch because the local headless Editor is blocked in its licensing channel.

Allies now carries a framed, page-scale seal and its own banner line above the existing honest preview state. These source/design changes passed the token and silent-drop checks; Unity layout, narrow-phone text expansion and interaction checks remain deferred to final validation.
