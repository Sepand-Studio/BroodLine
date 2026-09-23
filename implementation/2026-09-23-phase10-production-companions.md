# Phase 10 — production companion adoption, source handoff

The accepted Frontier companion builders now drive the **playable founder/reveal portrait** and the **deploy lane preview**. Both reuse one `FrontierArt` instance per stage and dispose its meshes and material with the stage. The portrait fits a fixed camera to the rotating body, its attachments and growth allowance; the deploy image still paints one frame and keeps its prior clear/pause contract. `FtueDirector` passes the species and ordered traits through `FrontierLook`; game and server models did not change. Species and trait IDs retain the old renderer's case-insensitive input contract; an unsupported species leaves a portrait fallback or skips a lane body with a warning, and an unsupported trait leaves its socket empty.

`FrontierBaker.Bake` renders all six bodies and both sockets for all five traits (66 card layers) into the existing `UI/Resources/Art/creatures` paths. This preserves the current `CreatureCard` binding. The old card PNGs remain in the repository until the bake runs on the Unity machine; source integration alone does not update those raster assets. The live battle still uses `CreatureAssembler` for companions and raiders; Phase 10 Batch 3 replaces that renderer after raider art is ready.

## Unity machine handoff

1. Pull `phase_10` and let Unity 6000.6.0f1 import the new scripts. Check Console for compile errors.
2. Close the Editor and run `implementation/scripts/bake-frontier.sh` from the repo root. Review the changed body/part PNGs at 40px and card size. Run the EditMode suite, especially `SilhouetteTests`, `LaneStageTests` and `LaneCardJoinTests`. Commit the baked PNG changes only after visual review.
3. Open `Boot.unity` against the normal local stack. Check the founder portrait, splice reveal, and deploy card with Vetch, Ember, Pale, Skitter, Hollow and Loam; toggle traits and deployment within one frame. Confirm the preview keeps the right creature count and never clears while the deploy screen remains visible.
4. Capture those live screens at 430×932, 390×844 and 360×640, plus a roster/card view after the bake. Compare faces, attachments, crop and legibility against `implementation/results/frontier/companions.html`. Check a live wave separately: its old models are expected until Batch 3.

Unity was not installed on the implementation Mac. C# syntax, shell syntax and whitespace checks ran locally; Unity compilation, the bake, EditMode/PlayMode runs, screen captures and visual acceptance remain due on the Unity machine.
