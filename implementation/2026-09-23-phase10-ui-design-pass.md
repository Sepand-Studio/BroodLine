# Phase 10 — Map, Store, Lab and Allies design pass

The four hub destinations now have a clearer visual hierarchy in source. The World Map places the thirty interactive region markers over a painted terrain atlas, lets a tap select a region, and shows its name, lane/travel detail and the current preview action below the map. Its full labeled region list remains beneath the atlas. The raster at `client/Assets/UI/Resources/Art/map/frontier-atlas.png` was generated for this project without text or markers, so all navigation and status remain live UI elements.

Store leads with the daily gift on a dark brass-edged card. Each pack shows a shard emblem, amount, price and an explicit `Buy (preview)` action. The Custom Chest shows icons for each choice and a live summary of the selected three rewards. Lab leads with an Ark Core tier card and puts blockers in separate chips beside the facility actions. Allies uses a dark banner and a short list of planned cooperative tools below its current local preview action.

Campaign Select now opens with a painted Hollow Reach chapter header, keeping the wave list and its availability rules unchanged. The generated illustration is in `client/Assets/UI/Resources/Art/campaign/hollow-reach.png`; its chapter labels remain live UI text.

Review layout and interaction direction in `implementation/reviews/phase10-ui-review.html`. It is a browser composition preview, not a Unity capture. At the user's request, Unity validation and visual acceptance are deferred until the end of the UI/design work. The remaining visual work is the icon language and final polish after the Unity screen review.
