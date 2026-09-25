# Broodline character set: before and after splice

The [Cinderplate character crop](cinderplate-character-crop.png) is the finish target: appealing face, broad readable anatomy, layered ceramic and keratin materials, restrained glow, warm key light, cool fill, and a grounded contact shadow. The full [approved image](cinderplate-visual-target.png) supplies the workshop context. These are reference images, not transparent game sprites or 3D geometry.

## Production rule

Make **six base species** and **twelve modular trait pieces**. A splice keeps one recognizable species body and mounts the inherited traits in its dorsal and flank sockets. This supports the game's many trait combinations without drawing a separate full character for every result. Use the same assembled identity for portraits, Founder/reveal moments, roster, Ark, deploy, and battle. A fixed 2D hero render may supplement a scene, but it must match the live assembled creature and leave names, tiers, and actions to UI Toolkit.

| Base body, before splice | Required silhouette and character read | First after-splice proof |
|---|---|---|
| Vetch | Low wide teal dome, four sturdy legs, inset face, no neck; patient, confident expression. Broad shell material over pebbled hide and cream underside. | Carapace plates plus Taunt horns. **Cinderplate** is the separate showcase: Vetch body, broad flank Carapace, and exactly three coral Cinder spikes. |
| Ember | Tall narrow coral biped, small forelimbs, cream chest, swept flame-shaped head crest; bright alert expression. | Three Cinder spines and distinct deep-coral Splash lobes. Keep the two-legged silhouette. |
| Skitter | Small amber body, six long thin legs, tiny head; a fast radial silhouette. | Two swept Sprint fins and a discrete Litter clutch. Keep every leg legible around the new parts. |
| Hollow | Tiny violet body, long forward neck and stilt legs; watchful, precise expression. | Diagonal Reach lance and three narrow Pierce lances. Separate the two lance families in angle and value. |
| Loam | Green segmented ground-hugger, blunt snout, no legs; gentle, heavy expression. | Two-leaf Regrow stem and stepped Burrow claw. The claw must not suggest feet on the base body. |
| Pale | Ice-blue broad wing arc with a small hanging body; calm, airy expression. | Four-spoke Screen fan and uneven Chill crystals. Keep the original wing arc clear behind both parts. |

## Source images to create for each species

1. A clean full-body three-quarter **base** render on a transparent background, with the same light direction, camera elevation, and scale across all six species. No trait geometry baked into the base.
2. A front, side, rear, and three-quarter turnaround with neutral lighting; an expression strip for rest, notice, attack, hit, and reveal; and closeups for eyes and key materials.
3. An **after-splice** proof with both listed traits attached, using the same camera and body proportions as the base. Show the dorsal and flank attachment separately in closeups.
4. A 40px silhouette and a 390×844 hero composition. At 40px, a player must distinguish the species and identify that both trait slots are occupied.

The character art source can be generated or drawn; it does not depend on an external artist. Art review checks anatomy against the [character bible](../Character%20Bible.dc.html), color and material against the approved Cinderplate, and the two trait shapes against the [production art request](ARTIST_BRIEF.md). Source art then guides the 3D models, rigs, materials, and animation. The existing [authored prefab contract](../../../client/Assets/Frontier/Art/AUTHORED_ASSETS.md) makes those models replace the procedural bodies across the game without changing simulation or server behavior.

## Image-generation prompt contract

Use the approved Cinderplate image as a **style and material reference**, not as an edit target. Generate each species as its own asset. Keep the whole body, all feet or wing tips, and room around attachment sockets. Request transparent backgrounds for individual renders and plain neutral backgrounds for turnarounds. Require original Broodline creature anatomy, stylized premium real-time game art, visible sculpted volume, soft directional lighting, and no text, UI, platform, humans, borrowed franchise creatures, rarity effects, or extra traits. For an after-splice image, reference the accepted base render and change only the named trait pieces; preserve species face, body proportions, limb count, and base palette.

Do not import an image into a game screen solely because it looks polished. Verify its anatomy, trait meaning, alpha edges, consistency with the model, and legibility at the game's actual sizes before promotion from reference to runtime art.
