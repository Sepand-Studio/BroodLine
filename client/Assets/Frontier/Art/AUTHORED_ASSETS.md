# Authored Frontier creature handoff

The runtime looks for a GameObject asset at `Assets/Frontier/Resources/Frontier/Creatures/<id>` before building the procedural body. The six founder bodies are installed as imported `.frontiermesh` assets; a future `.prefab` may replace one at the same resource path. IDs are lower-case species or raider IDs from `FrontierVisuals`. Missing assets keep the procedural appearance. An invalid rig logs a warning and also falls back. The caller remains `FrontierArt.Creature`, so the same appearance reaches the portrait studio, card portrait cache, Ark and lane stages, and live wave view.

## Authored GameObject contract

- The asset root is at local origin with unit scale. It has exactly one primary `SkinnedMeshRenderer` with a mesh, material, and bind poses for every bone in `FrontierRigDefinition.For(id)`.
- Do not add `FrontierCreature` or socket nodes to the prefab. Runtime creates the controller and dorsal/flank sockets after validating the authored rig.
- `renderer.bones` follows the definition's bone order. Names and direct parent relationships match the definition. Do not rename bones to suit DCC export defaults; retarget the export.
- Put the body, eyes, shell, and material regions in that skinned mesh. The current `FrontierCreature` controller animates these bones and applies the existing chill, hurt, hit, movement, greeting, and celebration presentations. The prefab can contain passive decoration, but it must not replace the controller or write simulation state.
- Keep the dorsal and flank sockets clear. Runtime trait attachments are placed on those sockets from the creature's two combat trait IDs. Check every authored body with both attachment slots occupied. An imported body must not contain permanent trait geometry that changes the meaning of an untraited founder.
- Materials may be authored. The controller writes `_Tint` on the primary renderer; the shader must support that property. Check reduced motion and pause, because the controller still owns both.
- When art or portrait framing changes, increment that species' `ArtRevision` in `FrontierVisualDefinition.cs` so the portrait cache refreshes.

## First production batch

### Installed mesh batch

`Assets/Frontier/ProductionCandidates/` contains editable offline-authored
body and trait meshes. `tools/character-production/install_runtime.py` copies
the six bodies into `Resources/Frontier/Creatures/` and the twelve independent
parts into `Resources/Frontier/Parts/` with distinct Unity GUIDs. The
`FrontierMeshImporter` makes each `.frontiermesh` a GameObject asset with an
embedded mesh and material. These are now the live founder visuals, with
procedural fallback if an imported asset is missing or invalid. They remain
blockouts and have **not passed final visual or device acceptance**.
Source references, portable rigged GLBs and the outstanding visual issues are in
[the production work log](../../../../specs/Designs/visual-production-v1/source-v1/README.md).

Runtime trait replacement now loads `Frontier/Parts/<trait>` independently of
the body. An authored part has one MeshFilter and MeshRenderer on its root,
a mesh and material, origin position, identity rotation and unit scale, no
children and no MonoBehaviours. Its local +Y projects outward from either socket;
the existing flank rotation is applied by the socket. Invalid or missing parts
fall back to the procedural part. Imported meshes and materials remain owned by
the asset, and both parts contribute to portrait bounds. The six founder
`ArtRevision` values were incremented for this runtime replacement.

The Vetch candidate uses the existing eight-bone animation contract. GLB clips
are DCC review aids; do not attach a competing Animator to the runtime prefab.

| Asset | Required visual read at 40px | Acting and material reference |
|---|---|---|
| Vetch | Wide, low dome; four sturdy feet; face inset; no neck | Heavy planted idle, shoulder-led walk, guard and taunt; pebbled teal hide, smooth shell, cream underside. |
| Ember | Tall narrow biped with swept flame crest | Springy idle, two-legged run, clear forward attack; coral keratin against cream chest. |
| Pale | Broad manta wing arc and small hanging body | Slow wing breathing, gliding movement, restrained chill ribbon; cool blue membrane. |
| Skitter | Low radial insectlike runner | Rapid alternating feet and darting strike; amber segmented shell. |
| Hollow | Lean long-legged profile | Alert head motion and deliberate ranged attack; violet hide with pale accents. |
| Loam | Low heavy plantlike support shape | Weighted sway and growth response; soft green body with distinct dorsal silhouette. |
| Courser | Fast dark quadruped | Long stride and readable hit recoil; lean angular outline. |
| Skirmisher | Smaller darting raider | Quick lateral cadence; distinct from Courser at one-third phone width. |
| Lash | Tall lifting raider | Suspended body and sweeping attack; distinct from the two low runner silhouettes. |

Each companion needs idle, movement, attack, hit, and reveal acting. Reduced motion keeps pose changes and decisive effects but removes continuous camera, ring, and particle motion. Build one finished companion plus its two attached traits through Founder Naming, a card, deploy preview, and live battle before exporting the other five. The twelve catalog attachments are Carapace, Taunt, Cinder, Splash, Sprint, Litter, Reach, Pierce, Regrow, Burrow, Screen, and Chill. The runtime now has procedural versions of all twelve; final authored parts must preserve a distinct read in either socket at 40px. The current paired-trait review sheet shows that several remain too subtle at native size.

## Environment and UI handoff

Art production supplies Vetch's front/side/three-quarter turnarounds, eye and mouth expressions, hide/shell material callouts, an Ark paintover, the straight-lane defile paintover, six facility silhouettes, trait icon sheets, and the Cinderplate reveal key pose. These references may be generated or drawn, then reviewed against the [Cinderplate visual target](../../../../specs/Designs/visual-production-v1/cinderplate-visual-target.png) and [approved concepts](../../../../specs/Designs/visual-upgrade-mockups-v1/README.md). The Unity developer produces the models, rigs, materials, effects, lighting, camera framing, LODs, and animations while preserving the original creature identities and Living Frontier palette.

The six facility structures are Core, Splicing Chamber, Hatchery, Gene Vault, Harvest Array, and Drive. The battlefield is a straight sandy lane with vegetation and cliffs at the sides, visible deployment pockets, and the Ark at the near end. The stage camera must leave every combatant readable behind the compact HUD.

## Import review

Capture each authored asset at 430×932, 402×874, 390×844, and 360×640, plus six 40px companion silhouettes. Inspect the two trait slots, alpha edges, animation in motion, safe areas, 40% expanded text, 44pt-equivalent targets, and win/loss states. Recheck 60 fps, 600 MB peak, and a 30 fps degradation floor on an A14 / 4 GB device before marking a batch accepted. The source and fixture checks alone do not replace phone-size review.
