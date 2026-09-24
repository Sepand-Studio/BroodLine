# Phase 10 — Map, Store, Lab and Allies design pass

The four hub destinations now have a clearer visual hierarchy in source. The World Map places the thirty interactive region markers over a painted terrain atlas, lets a tap select a region, and shows its name, lane/travel detail and the current preview action below the map. Its full labeled region list remains beneath the atlas. The raster at `client/Assets/UI/Resources/Art/map/frontier-atlas.png` was generated for this project without text or markers, so all navigation and status remain live UI elements.

Store leads with the daily gift on a dark brass-edged card. Each pack shows a shard emblem, amount, price and an explicit `Buy (preview)` action. The Custom Chest shows icons for each choice and a live summary of the selected three rewards. Lab leads with an Ark Core tier card and puts blockers in separate chips beside the facility actions. Allies uses a dark banner and a short list of planned cooperative tools below its current local preview action.

Campaign Select now opens with a painted Hollow Reach chapter header, keeping the wave list and its availability rules unchanged. The generated illustration is in `client/Assets/UI/Resources/Art/campaign/hollow-reach.png`; its chapter labels remain live UI text.

Review layout and interaction direction in `implementation/reviews/phase10-ui-review.html`. It is a browser composition preview, not a Unity capture. At the user's request, Unity validation and visual acceptance are deferred until the end of the UI/design work. The remaining visual work is the icon language and final polish after the Unity screen review.

## Region detail follow-up

Map selections now lead to a region detail screen. The Ark's current region has a painted territory header above the existing server-backed harvest cards and a back path to the map. Remote regions show their reach, lanes, neighbours, shortest route and travel time from the server-owned Ark region. The relocation action starts a persisted local countdown in `StubLedger`, with every write logged as `[stub]`; it never changes the server region or exposes remote claim buttons. A second journey cannot start while a preview is active. The full route is visible both before and during the countdown, and completion explicitly leaves the Ark where the server says it is.

The three design states are in `implementation/reviews/phase10-region-detail.html`. That page is a composition preview, not a Unity capture. Per the user's direction, Unity/device validation is still deferred until the end of the UI/design phase.

## Trait icon language

The twelve authored traits now each have a distinct 24-unit silhouette. `client/Assets/UI/Art/generate-trait-icons.py` draws white, tintable 3× PNGs from one geometry source and saves editable SVGs under `implementation/art-source/traits/`. The shared `TraitChip` and `TraitPip` bind those marks by trait name, so roster, splice, Codex and any later use of those components inherit the same icon. Species colour continues to tint the chip, while the icon shape and text carry identity without colour. Unknown traits keep their label and receive no invented icon.

Review the contact sheet at `implementation/reviews/phase10-trait-icons.png`; it shows the large mark and a small chip-size sample for every trait. This is a source/design review. Unity rendering remains in the final validation batch.
