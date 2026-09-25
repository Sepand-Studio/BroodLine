# Living Frontier production art request

Status: ready for character production; no turnarounds or final source art are available yet. The user's [Cinderplate visual target](cinderplate-visual-target.png), its [character crop](cinderplate-character-crop.png), the [approved Founder, Reveal, and Battle concepts](../visual-upgrade-mockups-v1/README.md), [character bible](../Character%20Bible.dc.html), and [creature art specification](../Creature%20Art%20Spec.pdf) are the current references. Keep Broodline's species, raiders, Ark, trait meanings, and ivory/slate/violet/brass palette original.

An external drawing artist is optional. The reference images can guide newly generated or hand-painted turnaround sheets and material studies. They cannot themselves serve as the animated game body: each is one flattened view with fixed lighting, scene, and trait combination. The Unity deliverable is a separate rigged base creature with modular trait geometry, so the same identity reads in Founder, cards, reveal, and combat. A clean transparent 2D render can be used for a fixed menu or promotional composition while the matching 3D asset is built; it must not replace a live assembled creature with a different trait combination.

## First review package: four showcase moments

| Moment | Character and scene art source | Unity delivery | Phone-size decision |
|---|---|---|---|
| Founder Naming | Vetch front, left side, back, and three-quarter turnarounds; neutral, curious, blink, and pleased faces; shell/skin/eye material swatches; habitat paintover | One rigged Vetch, habitat stage, idle and greeting, portrait lighting | Vetch fills the hero without losing feet or shell; expression and name card read at 360×640. |
| Ark home | Ark orthographic and three-quarter paintover, six facility silhouettes, terrain and pennant callouts | Ark and facility models, grounded stage, local lights and camera | Ark is the focus; every facility remains a distinct selectable shape at phone size. |
| Deploy and live battle | Straight-lane environment paintover, pocket ground marks, three raider turnarounds and attack/hit poses | One finished lane, three first-wave raiders, allied and raider acting, restrained hit effects | Allies, raiders, pockets, lane and Ark remain separable while moving beneath the HUD. |
| Cinderplate reveal | Vetch × Ember child front/side/three-quarter, dorsal Cinder and flank Carapace callouts, reveal key pose | Shared assembled body, two attachments, reveal pose and light | Cinder and Carapace read as separate inherited traits; name and lineage remain legible. |

Send layered source files and transparent exports with clean edges. Supply line art/paintover, color swatches, and a short material note for each asset. Keep readable labels and all numbers out of painted backgrounds: Unity UI Toolkit owns text, prices, timers, traits, and actions.

## Character and attachment sheets

For **Vetch, Ember, Pale, Skitter, Hollow, and Loam**, provide one consistent-scale turnaround sheet per species (front, side, rear, three-quarter), four facial/acting keys (rest, notice, attack, hit), a five-pose silhouette strip (idle, move, attack, hit, reveal), and closeups for eyes, skin, shell/fur/membrane, and feet. Draw each as an untraited founder first. The 3D developer uses the existing `FrontierRigDefinition` bone names and tests growth at both endpoints. One primary body mesh plus the two runtime trait sockets must stay within the existing 10,000-triangle combined check.

| Trait | Shape cue that should survive at 40px | Material/color cue |
|---|---|---|
| Carapace | Three broad overlapping plates | Pale mineral with dark seams |
| Taunt | Paired projecting horns | Warm brass and worn keratin |
| Cinder | Three staggered flame spines | Orange core, dark root |
| Splash | Three round outward lobes | Deep coral against Ember |
| Sprint | Two swept-back fins | Amber with a dark leading edge |
| Litter | Five discrete eggs, not one clump | Cream shells and earthy cradle |
| Reach | One long diagonal lance | Violet base, brass tip |
| Pierce | Three narrow upward lances | Frost blue and steel shadow |
| Regrow | Stem with two opposing leaves | Two greens with a light edge |
| Burrow | Stepped digging claw | Earth stone and brass wear |
| Screen | Wide four-spoke fan | Blue membrane and pale ribs |
| Chill | Three uneven crystals | Frost with dark roots |

Each trait needs a dorsal and flank placement paintover on **all six** species. The same part must communicate its trait in both slots. The [native-size review](frontier-40px-review.png) shows the current procedural assembly with one pair per species: several parts disappear at native 40px, so larger and more distinct negative space is a required revision. Avoid a permanent trait baked into an untraited founder.

The first live raiders are **Skirmisher, Courser, and Lash**. Supply matching-scale front/side/three-quarter sheets, moving and hit silhouettes, and a distinct telegraph pose for each. Skirmisher is the small low dart, Courser the larger long runner, and Lash the lifted sweeping threat. Later campaign raider variants remain separate future assets.

## Ark, field, and screen-family art

- **Ark and six facilities:** Core, Splicing Chamber, Hatchery, Gene Vault, Harvest Array, and Travel Drive each need a building paintover, top/side silhouettes, material palette, icon, and active/inactive light state. The Ark remains one coherent structure in home, lab, deploy, and battle.
- **Battlefield:** one straight sandy lane with clear pocket positions, cliffs and vegetation outside the play path, far-side raider arrival, and near-side Ark. Deliver foreground, midground, and distant backdrop layers plus a lighting reference for day, victory dawn, and breach dusk. Do not paint fixed health bars or waves into it.
- **Atlas and travel:** region landmark vignettes, gate markers, route pennants, Ark/Collector markers, hazard tint, and a compact alert crest. Geographic decoration must not imply a route not present in the region catalog.
- **Alliance:** pennant, held/contested stake scenes, garrison plinth, rally assembly stage, and three logistics-branch ornaments. Creature commitments use the same assembled portraits as roster and battle.
- **Lab and samples:** a six-facility icon sheet, twelve species/trait specimen presentations, sample vessel, probability instrument, and timer/locked states. Odds and timers stay live text.
- **Store, events, and utility:** original reward illustrations for the current catalog, community genetic-tree key art, event panel, Marks workbench artifacts, envelope seals, and account/support crest. Avoid new rarity frames, currencies, or offer art not backed by the existing game.

## Export and acceptance

Use transparent exports for characters, attachments, UI ornaments, and icons; layered paintovers for environments and screen key art. Preserve source files so the 3D developer can match proportions and materials. Name each export with the screen family and asset ID; include a normal-light and low-light sample. The Unity prefab contract and paths are in [AUTHORED_ASSETS.md](../../../client/Assets/Frontier/Art/AUTHORED_ASSETS.md).

Review in this order: Vetch and Cinderplate; the first three raiders; Ark/facilities/field; remaining founders and attachments; map/alliance/lab/store/event families. Each batch gets before/current Unity captures at 430×932, 402×874, 390×844, and 360×640, a short running clip, reduced-motion review, and physical A14 / 4 GB profiling. The user approves the phone-size result before a batch is called finished.
