# BroodLine founder character production, 2026-09-24

**Status: source-art studies, six rigged body candidates and twelve independent trait mesh candidates. Not a production-ready character batch.**

Open [review.html](review.html) to compare the source art with actual mesh renders at phone sizes. No generated image is installed as a moving creature or used as a replacement for arbitrary trait combinations.

## Files

- `vetch-base.png`, `vetch-front.png`, `vetch-side.png`, `vetch-rear.png`: individual generated modeling references, without UI or text. Built-in image generation; no billed API fallback.
- `vetch-expressions.png` / `vetch-materials.png`: five-acting-key source strip and five surface references without painted labels or UI.
- `cinderplate.png`: before/after source-art study; three Cinder spines and three pale flank Carapace plates. The base identity is retained, but the framing changes slightly.
- `ember-base.png` / `ember-after.png`: distinct two-legged Ember before and after Cinder and Splash.
- `hollow-base.png` / `hollow-after.png`: Hollow source studies with four legs; the game's Hollow rig has **two** stilt legs. The model candidate follows the live rig, and these images need anatomy correction before final sculpting.
- `loam-base.png` / `loam-after.png`: legless Loam before and after Regrow and Burrow.
- `pale-base.png` / `pale-after-study.png`: broad-wing Pale before and after Screen and Chill. The after view still needs a clearly visible third Chill crystal.
- `skitter-base-study-v2.png` / `skitter-after-study.png`: amber six-foot studies of Sprint and Litter. The near/far side grouping is wrong for three anatomical leg pairs, so these are not accepted modeling turnarounds. `skitter-base-study.png` is an earlier rejected extra-leg attempt.
- `vetch-study.png`: initial study with too much permanent shell armor; not the untraited body specification.
- `vetch-front-rejected-six-feet.png`: rejected generation; extra legs. Use the corrected `vetch-front.png` instead.
- `models/<species>.glb` for all six species: skinned body candidates with their existing bone identity and idle, walk, attack, hit and reveal review clips.
- `models/cinderplate.glb`: same body and rig with independently mounted Cinder and Carapace meshes.
- `models/cinder.glb`, `models/carapace.glb`, and ten `models/parts/*.glb`: twelve independent trait candidates. Cinder has exactly three spines; Carapace has three beveled plates.
- `review/*`: software renders at four phone sizes and native 40px, **not Unity captures**. These masks do not establish gameplay acceptance.

## Unity implementation

Editable mesh sources are under `client/Assets/Frontier/ProductionCandidates/{Vetch,Founders,Traits}/`. `FrontierMeshImporter` imports each `.frontiermesh` into a GameObject asset containing a mesh and material. Each founder body has one primary SkinnedMeshRenderer, the existing species bone names and bind poses, and no baked trait geometry. Vetch blends its continuous dome across root and head bones. The material uses the existing FrontierSurface shader and `_Tint` support. No controller or socket nodes are serialized into the imported body.

`FrontierArt` now supports independent authored trait prefabs at `Frontier/Parts/<trait>`, with validation and procedural fallback. A regression test covers both socket orders, portrait bounds, asset ownership, and invalid-root fallback. Existing body loading and caller APIs remain compatible.

**The mesh batch is now installed as live game art at the user's request.** `tools/character-production/install_runtime.py` copies all six body and twelve trait sources into `Assets/Frontier/Resources/Frontier/{Creatures,Parts}/` as `.frontiermesh` GameObject assets. The original candidates remain editable, the runtime retains procedural fallback for missing or invalid assets, and each founder's ArtRevision was incremented. Unity import and live visual inspection have not run on this machine, so this integration does not imply final art acceptance.

Unity continues to use `FrontierPose` for runtime motion, reactions, pause and reduced motion. GLB clips are portable DCC review aids; they are not installed as a second animation controller. Geometry is in Unity model space (+X forward, Y up); GLB export reflects Z and reverses triangle winding for glTF.

## Measured results and unresolved work

- Vetch: 8,260 triangles. Cinder: 864. Carapace: 108. Cinderplate: 9,232 total. Two Cinder attachments: 9,988 total.
- Source mesh and GLB structural validation passes for all six rigs, twelve part identities and all 864 ordered body/two-trait pairings under 10,000 triangles.
- The models are visibly below the target: facial integration is weak, skin relief is sparse, feet and wings need sculptural refinement, plates need curvature, and material finish lacks the reference's depth. The 40px two-trait visual acceptance remains open.
- Generated RGBA references contain a soft halo and are **not clean runtime cutouts**. Front/side/rear views also need cross-view proportion reconciliation before final sculpting. Expression and material closeup sheets remain outstanding.
- EditMode and stylesheet runners were attempted and both stopped with exit 127: Unity 6000.6.0f1 is absent at the configured path. The previous 601 tests / 53 stylesheets are historical results, not results of this work.
- The 37 fixtures at each of four device sizes, live battle footage, reduced-motion playback, Unity import verification and physical A14 profiling remain unrun. No fixture capture was launched against a missing editor.
- All six founders now have before/after source studies and skinned model candidates; all twelve traits have separate mesh candidates. The source studies for Hollow, Skitter and Pale have the anatomy/detail issues noted above. Expression and material sheets, DCC polish, Unity import confirmation and all visual acceptance gates remain open.

## Reproduce

From the repository root:

```sh
python3 tools/character-production/build_vetch.py
python3 tools/character-production/build_founders.py
python3 tools/character-production/build_traits.py
python3 tools/character-production/export_glb.py
python3 tools/character-production/validate_assets.py
python3 tools/character-production/install_runtime.py
# Requires numpy and Pillow:
python3 tools/character-production/render_review.py
```

The three `build_*.py` scripts are editable offline mesh-authoring sources, not image-to-3D reconstruction. They do not bake the existing procedural game models. `rigs.py` mirrors the Unity rig data for portable GLB review; the Unity definitions are authoritative. `render_review.py` uses a static software rasterizer; it measures neither Unity rendering nor GPU performance.

The checkout was clean after the user pulled commit `dccad54`; the mentioned uncommitted StoreView edit was not present. StoreView and gameplay/save/server/economy/navigation files were not edited.
