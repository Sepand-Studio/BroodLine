# Companion redesign review — revision 03

All five companion builders and the shared rig/pose system are implemented. Visual acceptance remains pending, in batches: Ember/Pale, Skitter/Hollow, then Loam. The accepted Vetch geometry is unchanged. Existing completed repository changes, including the move into `client/Assets/Frontier/Art`, are preserved.

## Review entry points

Run `python3 implementation/scripts/preview-frontier-companions.py` from the repository root. Open the generated `implementation/results/frontier/companions.html`. It includes matching comparison cameras, front/side/three-quarter controls, growth endpoints, two optional traits, a 40px silhouette lineup, enlarged face and feature views, and a 36-combination attachment sheet for each species. Output files are ignored and can be regenerated on either machine; the exporter requires Mono/Roslyn as documented in its script.

The detail pass adds raised noses with visible nostrils to the five redesigned companions. Ember, Pale, Skitter and Hollow have curved nails suited to their feet or hands; Pale has upper-wing veins, Skitter has contrasting ear interiors, and Hollow has a lighter line in its crown plumes. A later face pass changes Ember from a round muzzle to a tapered snout with narrow alert eyes, and Loam from a rounded chin to a low, wide lip with raised eye humps. Vetch's accepted face remains unchanged. The comparison page has a neutral-color, matched-size three-face view at synchronized angles. Portrait cache art revisions are 3 for Ember/Loam and 2 for Pale/Skitter/Hollow.

In Unity, use **Broodline > Frontier Proof > Open**, enter Play mode, and progress to formation. Open **Companion Studio**. Select a companion and both trait slots, then test turn, growth, Idle, Walk, Attack, Hit, Exhausted, Greet and Celebrate. Test pause/resume during a reaction and reduced motion. Studio selection does not change the playable formation.

Frozen Frontier baselines come from commit `cef49fa`. Skitter, Hollow and Loam remain explicitly new-only in the browser until their actual production meshes are exported: run **Broodline > Export Production Creature Baselines** on the Unity machine, then regenerate the browser preview. The exporter preserves existing baseline files. Archive them before deliberately capturing a replacement. No invented before images are used.

## Checks completed locally

| Species | Body triangles | Largest assembly with two traits |
| --- | ---: | ---: |
| Vetch | 8,180 | 9,164 |
| Ember | 7,840 | 8,824 |
| Pale | 6,832 | 7,816 |
| Skitter | 8,814 | 9,798 |
| Hollow | 6,036 | 7,020 |
| Loam | 8,138 | 9,122 |

The actual C# builders compiled against an offline math adapter pass finite geometry, outward winding, normal/material channels, weights, hierarchical bind-pose round trips, 216 assemblies, 504 sampled poses and 18,144 sampled pose/attachment framing combinations. This is not collision detection or a Unity runtime certification. The accepted Vetch mesh matches its frozen baseline. The older Vetch comparison exporter also still passes.

The fifteen art C# files pass syntax parsing. Browser rendering reports no JavaScript errors; attachment sheets were generated for all six species at both growth endpoints. The browser comparison has no horizontal overflow at 430×932, 390×844 and 360×640. These checks do not substitute for the proof's Unity portrait checks.

## Unity acceptance still required

- Run EditMode tests, including the newly registered `Broodline.Frontier.Art.Tests` assembly. Added regressions bake all species/trait pairs at both growth endpoints, check Loam's independent middle-segment sockets and check asset reuse/disposal across repeated species switching.
- Check startup, blinking, all action transitions, pause continuity, reduced motion and repeated Studio navigation in Play mode.
- Inspect both growth endpoints and all ordered trait pairs in motion for floating parts, deep intersections and occlusion. Pale's wings can obscure dorsal details from some viewpoints; inspect wing motion and the side view before accepting its fitting.
- Capture the actual Unity proof at 430×932, 390×844 and 360×640. Confirm faces, silhouettes and attachments remain readable, and growth enlarges the creature without a continuous camera retreat.
- Review each batch's before/after visuals before treating its art direction as accepted. Device performance and main-game migration remain separate acceptance work.

Unity is unavailable on the implementation Mac, so native test execution, production baseline export, animation/lighting captures and the above visual acceptance checks are pending on the Unity machine.
