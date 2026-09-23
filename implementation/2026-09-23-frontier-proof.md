# Living Frontier — playable art proof

Status: **the user confirmed the proof runs, found the personality-only pass too subtle, and authorized a substantial character redesign**. Vetch and Cinderplate now have rebuilt geometry and a source-derived comparison preview. The redesign still needs Unity rendering, animation and device review. This is an implementation of the [approved visual direction](../specs/plans/broodline_visual_upgrade_plan.md), not completion of the full visual-upgrade plan.

## Vetch / Cinderplate redesign — revision 02

The founder now has a larger inset face, amber eyes with skin-colored orbital ridges, a broad cream muzzle, a wider four-foot stance and dark rounded toes. Seven beveled polygon plates follow a continuous domed shell instead of individual spherical bumps. Skin, shell and eyes carry separate polish values in UV0 while sharing the same material. Cinderplate uses the same redesigned body with larger two-tone dorsal spikes and beveled flank armor; its attachment sockets follow the new shell dimensions.

Founder and Reveal cameras now frame the actual body and attachment bounds, with a fixed allowance for animation. Turning or switching species recalculates the framing. Reveal reserves space for maximum growth up front, so changing the growth slider does not cancel the visible growth by moving the camera. The footer reads **VETCH REDESIGN · 02**, making it easy to identify the new source on the Unity machine.

This pass redesigns **Vetch and its Cinderplate variant**. Ember and Pale keep their earlier bodies. Shared Carapace geometry and surface shading also appear on their existing attachments/materials. This remains procedural stylized art; it does not yet match the sculpted, textured finish of the approved concept images.

To inspect the actual meshes without Unity:

```bash
python3 implementation/scripts/preview-frontier-vetch.py
```

Open the resulting `implementation/results/frontier/vetch-redesign.html` in a browser. Both models use the same scale and camera; drag to turn, select front/side views, or enable **Show Cinderplate**. The left mesh comes from commit `ae35c7c`; the right comes from the current C# builders. Full Git history containing that baseline and Mono/Roslyn are required (override `--mono` / `--csc` when needed). The preview is self-contained once generated, without CDN dependencies or network calls.

The exporter uses a small math/mesh adapter outside Unity's Assets folder. It checks finite vertices, unit normals, face winding, bone indices and triangle counts, but **does not validate Unity APIs, skinning, shader import, lighting or performance**. Browser lighting approximates the Unity material; these images are geometry previews, not Unity screenshots. Preview output remains an ignored, regenerable artifact.

Revision 02 checks performed locally:

- Previous Vetch: **5,448 triangles**; redesigned Vetch: **8,180**; assembled Cinderplate: **8,798**. All passed the offline geometry checks and remain below the existing 10,000-triangle assembled ceiling.
- Inspected front, side and three-quarter browser renders, then corrected the exposed lower shell and detached brow appearance. Inspected Cinderplate's assembly as well.
- The interactive preview loaded without JavaScript errors; view controls, Cinderplate switching and auto-rotation worked. The 390px layout had no horizontal overflow.
- Eight changed/new C# files passed Roslyn syntax parsing. This is not a Unity compile.
- Existing Unity geometry tests now also cover the upper dome, curved plate primitive, polish channel and portrait containment of bodies/attachments. Unity tests remain unexecuted here.

On the Unity machine, stop Play, pull `phase_10`, wait for compilation and reopen **Broodline → Frontier Proof → Open**. Check Founder first, then **Preview Cinderplate** from formation. Confirm the revision 02 footer, clear Console, run the EditMode tests below, and capture both screens. In particular, check eye blinking, head rotation/growth intersections with the shell, spike visibility and framing at all three supported Game-view sizes.

## Open it on your Unity machine

A historical source patch is packaged at `implementation/results/frontier/broodline-frontier-proof-source.zip`. Its application was verified against its recorded base in a temporary directory. It is not a compiled build and predates the startup fix, personality pass and revision 02 redesign. **Pull `phase_10` for the current implementation.**

**Second presentation pass:** The ZIP now includes selectable deployment stones with occupied-position swaps, defender/enemy health bars, pooled damage/status cues, Rally and breach announcements, hit reactions, target-aware head turns, blink bones, species comparison controls, and richer foliage/path edging. This is a complete replacement package from its recorded base commit, not an incremental patch over the first ZIP.

**Creature-personality pass (follow-up to the user's successful playthrough):** The requested priority is creature personality and detail. Vetch gains layered shell insets and a curious greeting; Ember gains articulated forearms and a wave; Pale gains membrane ribs, a hovering idle, and a wing-dip greeting. All three have head-weighted brows/freckles and muzzle-following smile lines, with species-specific idle and blink timing. Tap the creature stage (or focus it and press Enter) on Founder/Reveal to greet; moving the pointer within the stage changes the creature's gaze. Surviving companions briefly celebrate a win. Reduced motion suppresses these gestures, while pause now freezes each creature's presentation clock and resumes without losing an in-progress reaction. Pull `phase_10` for the latest source; the earlier ZIP predates this pass and the initialization fix.

1. Fetch and check out the **`phase_10`** branch on your Unity machine, then pull its latest changes. The branch contains the new `client/Assets/Frontier/` directory, its `.meta` files, `FrontierProofBuilder.cs`, and the updated test harness. Use your existing Git LFS installation to fetch the three approved mockup PNGs and other repository assets. You do not need to apply the source ZIP when pulling this branch.
2. Use **Unity 6000.6.0f1** and open the repository's **`client`** project. Let package import finish. Restore the repository's Git LFS assets using your existing LFS setup. The new proof generates its creature/environment geometry and icons locally; its UI reuses the repository's Baloo 2 and Nunito font assets and shell theme.
3. Select **Broodline → Frontier Proof → Open**. The command offers to save any open modified scene, then builds and opens `Assets/Frontier/Generated/FrontierProof.unity` with its own panel settings. It does not add the scene to release build settings.
4. Set Game view to **430 × 932**, then press **Play**. Also check **390 × 844** and **360 × 640**; the lower information panel scrolls on smaller screens.
5. Name the Vetch founder, turn it with **Look around**, and tap the creature stage to say hello. On formation, select a companion, then tap a numbered stone; tapping an occupied stone swaps the two companions. Select Mixed raiders or Courser, then **Protect the Ark**. During battle, select a companion and use Rally once. Pause/Resume and Reduced motion are available.
6. Choose **Meet Cinderplate** after the battle, or **Preview Cinderplate** from formation. Inspect its dorsal/flank parts with the turn and growth sliders. Expand **Compare appearances** to view the Vetch, Ember, and Pale base species alongside the combined Cinderplate appearance. These are visual references, not a claim that a breeding transaction occurred. This preview does not unlock an inventory item or add Cinder to combat.

For a command-line scene build, close the project in the editor first:

```bash
UNITY_EDITOR="/path/to/Unity" bash implementation/scripts/build-frontier-proof.sh
```

On a standard macOS Unity Hub install the environment override is unnecessary. The script checks for the editor before creating output, preserves earlier logs, and writes a timestamped build log to `implementation/results/frontier/`.

## What is implemented

| Area | First proof implementation |
|---|---|
| Creatures | Original procedural Vetch, Pale and Ember bodies with eyes, face/underside separation, shell plates, crest, and broad articulated wings; three raider presentations |
| Animation | Skinned head/limb movement, breathing, walking, symmetric wing flap, head-following blink bones, target-aware head turns, attack recoil, hit flash, hurt posture/tint, and growth preview |
| Splicing | Dorsal and flank parts follow the root bone; crown socket follows the head. Cinderplate demonstrates Cinder dorsal plus Carapace flank, with five trait appearances available |
| Environment | Sandy Defile route with broken stone edging, deployment pads, layered tree canopies, grass/flowers, rocks, and a wood/ceramic/brass Ark with violet core |
| Materials | Shared vertex-color URP surface with warm lighting, cool fill, shadow reception and subtle highlights |
| Interface | Warm paper/ivory panels, violet actions, larger Baloo 2/Nunito type, four matching vector icons, visible focus states, safe-area support, scrollable information panel, and reduced motion |
| Interaction | Founder naming/rotation, direct stone placement and swaps, two existing waves, selection and Rally, pause/resume, result messaging, and Cinderplate/base-species inspection |
| Battle readability | Defender/enemy health bars, chill status, selected/Rally indicators, hit feedback, damage/defeat labels, and breach/Rally announcements. Eighteen retained floating labels bound cue-pool size; reduced motion disables their travel/fade and hit flashes |

The three approved PNGs remain **concept references**, not runtime screenshots. This implementation uses authored procedural forms to make the direction reviewable without additional texture/model imports. It is not the final sculpted/textured/rigged asset pipeline described in the larger plan. Full-game screen migration, all six finished species, complete icon coverage, portrait baking, audio, production VFX, and the remaining functional follow-ups are still future work.

## Architecture and isolation

- `FrontierProof` owns the UI, camera render texture, page lifecycle, and read-only presentation of simulation snapshots. Its only combat input is the existing tick-boundary Rally API.
- `FrontierFormation` creates valid existing-species/trait deployments. Waves 6 and 7 and their authored geometry are reused without changes to combat rules, ticks, or replay formats.
- `FrontierBattleFeedback` observes completed simulation ticks. It reports damage/status/defeat/breach/Rally changes without changing state. Attack recoil is inferred from cooldown and target changes; damage is observed directly from HP changes, including kills. Repeated observation cannot duplicate cues.
- `FrontierArt` owns/cache-shares character/trait meshes and the shared material per page. Switching pages releases the prior world and its assets. The stage render texture resizes to the viewport with a 1600-pixel maximum side and is released on destruction.
- The scene is isolated from Boot/Wave and the shell navigation. It has no account, network, save, breeding, purchase, reward, or progression integration. Names last for the current Play session only.
- Defender pads are displayed at a 1.5-unit transverse offset for the proof's composition. Simulation range calculations retain their original lane geometry. The view does not claim to be a scale diagram of attack range.
- The new USS has a separate, namespaced palette. Existing production UI tokens and their gate are unchanged. Reconciliation into shared production components follows visual acceptance.
- Shader references are serialized into the generated scene. The editor builder checks shader import errors, nonempty compiled stylesheet rules, and required font assets. Those checks do not replace actually rendering the shader on target hardware.

## Checks performed here

At implementation time this Mac had Mono/Roslyn but lacked the required Unity editor and Git LFS tooling. The user chose to run the Unity proof on their existing Unity machine. For the requested commit/push, the official Git LFS 3.8.0 executable was subsequently downloaded to a temporary folder and checked against its published release digest. Unity validation is still pending.

- Roslyn syntax parse after the second pass: **12 C# files, zero syntax errors** (11 Unity-side files and the standalone combat harness). This is not a Unity API/type-check or shader compile.
- Compiled the actual engine sources, `FrontierFormation`, `FrontierBattleFeedback`, `WaveClock`, and `WaveSnapshot` with Mono's Roslyn compiler: **120 wave/formation combinations passed**. Direct stepping and variable-frame stepping with snapshots/feedback produced identical final hashes, integrity, and ticks. Damage totals matched HP loss; defeats and breaches matched outcomes; repeated observations emitted no duplicate cues.
- **900 placements/swaps passed**, covering every legal three-companion arrangement and every companion/destination choice. Invalid destinations preserved the original arrangement.
- Default formation `[0, 2, 4]`, seed `6`: wave 6 **Win**, tick **555**, integrity **2**, hash **305626706584771394**; wave 7 **Win**, tick **975**, integrity **3**, hash **14844355235551983252**. These are local Mono results, not device certification.
- The same harness verified tick-boundary Rally consumption, rejection of a second Rally, and rejection of overlapping companion pockets.
- Existing silent-drop scan: **37 UXML files and 45 USS files**, zero reported delimiter/parse defects. This text scan does not validate USS property support or layout.
- Existing production token gate and launcher shell syntax passed; patch whitespace check passed.

The combat checks are now reproducible from the repository:

```bash
python3 implementation/scripts/check-frontier-combat.py
```

The script defaults to the Mono installation used on this Mac. Pass `--mono` and `--csc` for another installation. It compiles actual source into a temporary directory and tests it without Unity. To regenerate and verify the complete transfer package after editing, run `python3 implementation/scripts/package-frontier-proof.py` before committing; after committing, normal repository synchronization is preferred.

## Unity acceptance pass still required

1. Check the Console after import and after building/opening the scene. No new compile, shader, missing-font, missing-reference, or USS warnings/errors should appear.
2. In **Window → General → Test Runner → EditMode**, run **Broodline.Frontier.Tests**. Eleven tests cover active/inactive creature initialization and first-pose rendering properties, outward mesh winding/finite geometry, the 10,000-triangle assembled ceiling and skin bindings (including Ember's new arms), trait attachment/rest pose, simulation observation equivalence, formation validation/swaps, terminal breach feedback, blink bones, paused-pose continuity, and reduced-motion suppression of greetings/celebration. These Unity tests are authored but **have not run here**. The existing repository reflection runner also includes the new assembly.
3. Walk all screens and both waves. Check foreground/body separation, face readability, blinking, Pale's symmetric wing motion, trait overlap, Ark visibility, health labels, damage/status cues, Rally feedback, and layout at all three sizes. Test occupied-position swaps, a whitespace-only name, a 20-character name, repeated page/species switching, reduced motion, and pausing/resuming. In particular, inspect health-label crowding and deployment hit areas at the smallest size; source checks cannot establish their visual usability.
4. Select **Broodline → Frontier Proof → Capture Game View** in Play mode on founder, mid-battle, and Cinderplate screens. PNGs are queued to `implementation/results/frontier/`. Those captures are the next visual evidence to compare with the approved mockups.
5. Run the full existing EditMode suite and stylesheet import check. Preserve the repository's documented Phase 9/MainThreadAffinity failure rather than treating it as a new pass or silently rebaselining it.
6. Profile an actual device before integrating into release scenes. No claim is made yet for A14/4 GB, 104 entities, 60 fps, or the 600 MB budget. This proof runs three defenders and the authored small waves; it does not exercise maximum encounter capacity.

The next implementation step after reviewing the real captures is to refine the assets/framing and migrate accepted components into the production founder, deployment/battle, and splice flows.

## First Unity startup report and fix

The user's Unity run reported `MaterialPropertyBlock.CreateImpl` being called from the `FrontierCreature` constructor, followed by null references in `Pose`. The rendering block had been allocated in a MonoBehaviour field initializer. It is now allocated during explicit creature initialization, invoked by the scene's startup code on the main thread. Shader property ID lookup is also deferred; posing an uninitialized component is harmless. A regression test creates both active and inactive creatures and verifies their first pose writes the expected renderer tint without unexpected Unity errors. Source syntax checks passed; the Unity regression test and interactive startup still require rerunning on the Unity machine.

To retest: stop Play, pull the updated `phase_10` branch, let Unity finish compiling, clear the Console, and press Play again. The accompanying `Unity.AI.Toolkit.Accounts` timeout comes from the Unity AI package; it is separate from this offline proof's creature initialization failure.

API references used during implementation: [Unity runtime panel settings](https://docs.unity.com/en-us/engine/6000.0/manual/uitoolkits/uielements/uie-support-for-runtime-ui/uie-render-runtime-ui/uie-runtime-panel-settings), [URP shadow methods](https://docs.unity.cn/6000.0/Documentation/Manual/urp/use-built-in-shader-methods-shadows.html), and [ShaderUtil.ShaderHasError](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/shaderutil/shaderhaserror).
