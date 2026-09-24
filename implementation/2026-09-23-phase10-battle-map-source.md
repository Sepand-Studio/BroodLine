# Phase 10 — battle and map source pass

The live `WaveView` now assembles all six companions and the three implemented raiders through the Frontier art assembly. It shares the procedural lane environment with the deploy preview; the preview places pads at the authored pockets for the selected wave. Pause and speed reach the creature presentations. Damage reactions are triggered once per simulation tick rather than once per rendered frame. No engine, replay or outcome logic changed.

`BattleCameraFrame` fits the lane, pocket bodies, Ark and terrain to the narrowest supported portrait aspect in perspective. `WaveRunner` applies it to the Wave scene's own camera at run time, so an older saved scene also receives the composition; `WaveSceneBuilder.Build` writes it into a rebuilt scene. The HUD receives that same scene camera when the wave is loaded additively.

The World Map now has a tappable three-ring atlas of the 30 catalog regions, with the Ark region highlighted. Its existing labeled list stays underneath as the readable and larger-target way to choose a region. The atlas uses the existing region model and navigation callback; relocation and nonlocal region detail remain future work.

The Store daily-gift action now refreshes its presented page in place, leaving a single Store on the navigation stack. Lab upgrade chips count down while the Lab is visible and rebind the screen when the local timer completes. Facility key registration and timer settlement now go through the stub ledger's logged write path, including the timer clear. No real balance is changed.

Local checks: Roslyn syntax parsing of changed C# files, `git diff --check`, token verification and silent-drop scans passed. The companion browser geometry pass covered all six companions, 216 assemblies, 504 poses and 18,144 framing combinations. Three new raider meshes passed finite geometry and channel checks and were below 1,400 triangles each. Unity compilation, EditMode/PlayMode tests, portrait captures, battle animation, camera framing and map touch targets still require the Unity machine. The new `BattleCameraFrameTests` and `WorldMapViewTests` are included for that run.

Next visual review should inspect `Boot.unity` through a deploy and live wave, plus the Map at 430×932, 390×844 and 360×640. The current atlas is a code-native layout pass; illustrated terrain, pan/zoom, remote region detail and the later performance/device gate are not yet complete.
