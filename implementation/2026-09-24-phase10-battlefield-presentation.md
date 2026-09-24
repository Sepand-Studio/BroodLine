# Phase 10 — Battlefield presentation source pass

Deploy preview and live waves now place the same 2:1 painted ground plane below the Frontier path, rocks, brush and actors. `BattleBackdrop` loads `Assets/View/Resources/Art/battle-backdrop.png`, uses the already-included URP Unlit shader, and owns its mesh and material until its GameObject is destroyed. When the texture is missing, `FrontierArt.Environment` keeps the solid ground slab and both cameras clear to `BattleBackdrop.FallbackField`; the playable lane and simulation positions are unchanged. The unused Phase 9 `LaneDressing` implementation was removed.

The bitmap was generated with the built-in imagegen tool for this project. The source image is 1774×887. `implementation/reviews/phase10-battle-backdrop-concept.png` combines the actual procedural terrain-detail mesh with the painting as an **offline composition concept**; it is not a Unity camera or lighting capture. The generation prompt was:

```text
Use case: stylized-concept
Asset type: Broodline mobile game's battlefield ground backdrop texture, displayed underneath a playable 3D lane and beyond its rocky edges.
Primary request: a wide overhead painted frontier landscape ground surface. Muted moss and sage grassland, weathered cream earth, faint stone strata, sparse tiny leaf clusters and scattered grit. The middle two thirds should stay calm and low contrast so the game's separate 3D path, creatures, HUD and Ark remain unmistakable; add slightly richer brush and rock texture toward the far outer edges. Make the entire image read as ground viewed straight down, with no horizon or perspective, and avoid any strong central feature.
Style/medium: polished hand-painted fantasy game environment texture, painterly but restrained, complementary to cream, brass, deep blue and violet UI.
Composition/framing: wide landscape rectangle; evenly lit, top-down orthographic, continuous terrain that can sit behind a long straight battle lane.
Lighting/mood: soft warm daylight, welcoming frontier with subtle mystery.
Constraints: no characters, creatures, buildings, paths, symbols, interface, labels, text, watermark, hard border or vignette. Do not include a road because the game's procedural path is drawn separately.
```

The wave HUD now says whether the one-use Rally is ready, spent or disabled. Its pause, speed and motion controls have 44px minimum touch height. “Motion: Low” is saved in `PlayerPrefs` and applied to companion and raider poses, pooled ground pulses and floating cues; it changes presentation only. The Rally status reads the simulation's `RallyUsed` flag after completed ticks, so it does not infer gameplay state in the view.

Source checks: terrain exporter passed finite channels, normals and winding for both solid and painted fixtures (6,972 and 6,960 triangles); UI token and silent-drop checks passed; the 120-wave combat and 900-placement offline checks retained their prior hashes and cue totals. Unity imports, screen composition at 430×932 / 390×844 / 360×640, control taps, asset cleanup and device performance remain for the deferred final Unity pass.
