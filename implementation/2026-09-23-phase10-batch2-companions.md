# Phase 10 — Batch 2, the companions

Running record for Batch 2 of [the Phase 10 plan](../specs/plans/broodline_phase10_living_frontier.md):
the five remaining companions brought to the Vetch standard, in the review
order the character brief set (Ember/Pale → Skitter/Hollow → Loam), inside the
`Broodline.Frontier.Art` assembly so the result feeds production.

## Task 2.1 — the visual definition (2026-09-23)

`Frontier/Art/FrontierVisualDefinition.cs`: one entry per body (six
companions, three raiders) naming its rig, its mesh builder, a portrait pose,
the growth range and an art revision. `FrontierArt.Creature` resolves the
builder through `FrontierVisuals.For(id)` instead of a species branch, and
`PortraitKey` gives the later portrait cache a key that changes when the art
does. Vetch is revision 2 (the accepted reference); the others start at 1 and
step with each accepted redesign.

## Task 2.2 — Ember and Pale (2026-09-23)

**The review tools first.** The browser comparison page
(`implementation/scripts/preview-frontier-companions.py`) still compiled the
pre-split paths and its shader named a uniform `flat`, a GLSL reserved word
the app's browser rejects; both fixed. The page now serves from
`implementation/results/frontier/companions.html` (any static server) and
shows the frozen `cef49fa` baseline beside the current builder for every
species, with three-quarter/face/side views, parts, growth and silhouettes.

**Ember, revision 02.** A real `Neck` bone under a sleeker head, so the head
turns from the shoulders; one swept flame mane from the brow over the crown
and down the nape, five overlapping blades in gold → orange → ember, leaning
back rather than up; a cream chest and throat; a long tapered tail with three
flame tufts; arms ending in cream hands with claws, ready to wave; planted
feet with a heel, three cream toes and dark claws. Eyes at .10 with brows and
a cheek flush. Coral `#e5867a` kept; shade `#b8605f`. 7,992 body triangles,
8,976 with Cinder and Carapace.

**Pale, revision 02.** The membrane gained a `rise` parameter
(`FrontierMesh.Membrane`, `MembraneY`) and Pale uses a flatter arc (.13
against the proof's .24 dome, which read as a beetle shell from the side); a
thick dark leading spar blended shoulder → tip, a wrist knuckle at the
articulation, three finger spars fanning to the trailing edge; a pale head
with a dark eye mask set behind the eye plane so the eyes stand proud of it,
ear tufts for a head silhouette, a cream chin; a slate body with a cream
belly, tucked feet and two streamers. 6,020 body triangles, 7,004 assembled.

**Tests.** `FrontierArtTests` now iterates every definition in
`FrontierVisuals.All` (the budget and binding check had hard-coded six ids)
and accepts blended bone weights summing to one, with the bind-pose check
skinning by both weights; the reduced-motion test covers all six companions.

**Unity evidence.** A `Companions` fixture (`Editor/CompanionSheet.cs`) renders
the six bodies at rest and walking with Cinder + Carapace through the real
shader into the capture corpus, so the review has Unity renders as well as
the browser page.

**Not done here.** Species-specific acting beyond what `FrontierPose` already
does (Ember's bounce and arm wave, Pale's hover and wing-dip greeting exist);
the Companion Studio in the proof is unchanged and still the place to watch
motion; the user's review of this pair is the gate before Skitter/Hollow.

## Task 2.3 — Skitter and Hollow (2026-09-23)

**Skitter, revision 02.** A compact amber egg with a darker saddle and a
light chest ruff; a bigger, rounder head with a light muzzle, a dark nose,
eyes at .078 with brows, cheek marks and two ear tufts; a short tail tuft;
six legs that are limbs - a rounded shoulder, a thigh, a dark rounded knee,
a shin and a dark paw with three toes - clearly separated at every joint.
Warm-blooded rather than chitinous (bible 10.7): no shell, no mandibles.
7,806 body triangles, 8,790 assembled.

**Hollow, revision 02.** The tiny torso, stilt legs and long forward neck
kept; a tapered head with a brow ridge over focused eyes, two plumes on the
crown, a dark nose; a pale throat stripe the length of the neck's underside;
a pale chest; a short three-strand plume tail; thigh, rounded knee, and a
dark sock down to three long toes and a dew claw. Violet `#7a6ac0` kept;
deep `#5b4c9c`, plume `#a99bd9`. 5,232 body triangles, 6,216 assembled.
Nothing shared with Ember: no mane, no bulk, a line aimed forward.

**Production baselines.** `Editor/ProductionBaselineExport.cs` flattens each
Phase 9 production body prefab (the SDF-meshed `Creatures/Resources` set)
into the comparison page's JSON at
`implementation/results/frontier/production-baselines/`, so Skitter, Hollow
and Loam have a real "before" beside the redesign. Run it in batch mode
(`-executeMethod ProductionBaselineExport.Export`) and regenerate the page.
