# Phase 10 — Lab stage and facility detail

The Gene Lab now shares the Ark's live `HomeStage` render and six plot positions with the home screen. Tapping a plot or its list row opens a bottom sheet for that facility, showing its icon, role, current tier, next shard cost and build time. The sheet shows the existing Core-cap blocker or the active countdown instead of an upgrade action when either applies. Preview upgrades still write only to `StubLedger`; the sheet refreshes around the persisted end time.

`ArkStageProjection` maps normalized plot points into a centred scale-and-crop frame. Both the Ark home screen and Lab use it, so markers remain over their facilities when a portrait frame crops the 720×1280 render. The Lab fixture now includes all six plot markers, and `LabFacilitySheet` is a separate overlay capture fixture for the facility detail state.

Source checks passed for USS tokens, UXML/USS parsing and unique Unity asset GUIDs. The EditMode tests added for crop projection, cost/Core-cap binding and Lab plot presence will run in the deferred Unity pass, together with 430×932, 390×844 and 360×640 captures and live tap checks.

The Store's Custom Chest savings comparison remains an economy-content decision: the specs give three prices and six reward types but no per-tier quantities or separate-item reference prices. A percentage saving should be added only after those values are authored.
