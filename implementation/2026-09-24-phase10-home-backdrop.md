# Phase 10 — Ark home backdrop source pass

The Ark home stage now loads `client/Assets/Game/Resources/Art/home-backdrop.png` and draws it on an unlit quad behind the separate procedural base. The quad is parented to the off-screen camera, so the existing six world-space plot anchors and their touch targets keep their positions. The stage falls back to its previous deep-blue clear colour if the painting is missing. It disposes the quad's mesh, material and render texture with the stage.

The painting was generated with the built-in imagegen tool for this project at 941×1672. It is the background artwork, not a Unity capture of the full Ark screen. The generation prompt was:

```text
Use case: stylized-concept
Asset type: portrait background plate for Broodline's Ark home screen, sitting behind a separately rendered isometric 3D base and tappable facility markers.
Primary request: a quiet, beautiful frontier valley seen from an elevated three-quarter angle. Soft moss and sage woodland, weathered cream stone, distant blue-slate cliffs, subtle warm brass sunlight, gentle atmospheric mist. Keep the central 60% calm and low contrast so a separately rendered 3D Ark platform reads clearly; frame the periphery with more detailed trees, rock ledges and foliage. The background should feel like the same inviting but mysterious fantasy world as a painted campaign map.
Composition/framing: tall phone portrait, 9:16. Elevated view, no hard horizon, no strong central object. The scene should fill edge to edge and remain legible when cropped slightly on different phones.
Style/medium: polished painterly fantasy game environment, restrained natural texture, cohesive with cream/brass/deep-blue/violet UI and stylized creature models.
Lighting/mood: soft late-afternoon daylight, luminous but not saturated.
Constraints: no Ark, floating platform, buildings, machinery, creatures, characters, roads, labels, icons, text, watermark, panel edges or UI. This is only the background behind the game's separate 3D stage.
```

`HomeStageTests` now checks resource inclusion, Studio-layer placement, near/far depth, facing/winding and material binding in addition to the existing base budget and plot framing assertions. Unity test execution and portrait captures at 430×932, 390×844 and 360×640 remain in the deferred final validation pass.
