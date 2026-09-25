# Generation prompts

Built-in image_gen was used with the approved Cinderplate image as a style reference, then with `vetch-base.png` as the view/identity reference. No API key or billed API fallback was used.

## vetch_prompt

```text
Use case: stylized-concept. BroodLine Vetch production source, one isolated individual full-body three-quarter view. Reference image is style and anatomy reference. Match its squat low wide teal body, four sturdy legs, no neck, inset expressive amber eyes, small confident smile, cream belly, dark teal toes. Important base anatomy: untraited founder, pebbled hide with only small flush skin scales; NO large overlapping shell armor plates and NO horns/spines/spikes/orange markings. Keep broad low dome shape of reference despite removing added armor. Polished premium stylized 3D material volume, subtle skin roughness, satin small scales, glossy eyes, warm key and cool fill. Entire body visible with generous margin. Transparent background with genuine alpha, no ground plane or contact shadow outside character, no text, no UI, no platform, no labels. Face toward right at same camera elevation as reference.
```

## cinderplate_prompt

```text
Use case: precise-object-edit. Turn this isolated base Vetch into Cinderplate by adding ONLY two modular trait attachments. Keep exactly the same body, pose, face, eyes, skin, four legs, proportions, camera, lighting and image placement. Add CINDER: exactly THREE large staggered coral-orange keratin flame spines with dark roots in a single row along the dorsal midline, not four and not additional little spikes. Add CARAPACE: exactly THREE broad overlapping pale-mineral shield plates with deep teal beveled rims and dark seams on the visible flank, mounted as one separately readable attachment. Preserve recognizable low wide teal dome body, no neck. No other traits, no permanent full shell, no horns. Entire body and spike tips within frame. Genuine transparent background without studio backdrop, no text, no UI, no platform, no ground plane.
```

## vetch_front_prompt

```text
Use case: stylized-concept. Production modeling reference for BroodLine Vetch. From this base character generate one clean full-body ORTHOGRAPHIC FRONT view, centered symmetric neutral stance, all four feet positioned visibly, no perspective. Preserve EXACT low wide dome proportions, four sturdy legs, no neck, inset amber eyes, gentle smile, teal pebbled hide, cream underside, dark toes. No added armor plates, no horns or spines. Soft neutral even studio light reveals form without dramatic highlights. Plain flat light gray background, no glow, no atmospheric haze, no vignette. Entire creature within frame with 12 percent margin. No text, no UI, no platform, no extra views or characters.
```

The first front-view prompt above produced the rejected six-foot view. The corrected front, side and rear prompts are recorded below.

## Corrected front

Generate one clean orthographic FRONT VIEW modeling reference of this exact Vetch. Same low wide teal dome, cream chin and belly, inset amber eyes, pebbled hide. EXACTLY FOUR legs total: two front legs at front left and front right, and ONLY two rear legs, partially visible immediately behind them. No other feet or extra limbs beneath belly or outside the front legs. In this front view rear legs are largely occluded. No neck, no horns, no armor attachment or spines, no tail. Warm patient subtle smile. Neutral even soft light and flat light gray background. Full character with wide margin. No text, labels, UI, platform, extra views or other creatures.

## Side

Generate one clean orthographic SIDE VIEW of this exact Vetch creature, facing right. Production modeling reference. Exactly FOUR legs in total: only TWO near-side legs fully visible in side view, far-side two legs occluded behind the near-side pair. NO extra feet. Low broad dome, pebbled teal hide, cream underside, inset amber eye, no neck, no tail, no large armor plates, no traits, no spikes. Retain same face and body proportions. Neutral soft even studio light, flat light gray background, full character inside frame with margin. No text, labels, UI, scenery, platform, other views or other creatures.

## Rear

Use case: stylized-concept. One clean orthographic REAR VIEW modeling reference of this exact Vetch. Camera directly behind, no face visible. Same low wide teal dome, dense small pebbled hide, cream underside, four short sturdy legs, dark toes. Exactly FOUR legs total: two rear legs nearest camera, two front legs mostly occluded behind them, no extra feet. Rounded rear with no tail. No large armor plates, no traits, spikes or horns. Keep body proportions identical to reference. Neutral even soft light, flat light gray background. Full body within frame with generous margin. No text, labels, UI, platform, extra views or extra creatures.

## Remaining founder and trait prompt set

Every base request used the approved Cinderplate crop **only as a style/material reference**. Each after request used its generated base as an identity reference and asked to change only its two named detachable traits. All requested one original BroodLine creature, full body with visible limbs or wing tips, studio light, plain neutral background, no UI, text, labels, platform, borrowed character or extra trait. These are the defining subject and after-splice clauses from the generation calls:

| Founder base clause | After-splice clause |
|---|---|
| **Ember:** tall narrow coral biped with exactly two sturdy feet, two small lifted forelimbs, cream chest, long upright neck, one swept flame-shaped head crest, bright alert eyes. No dome, Cinder row or Splash lobes. | Preserve the same upright biped. Add exactly three separate tall orange Cinder spines with dark roots on upper back; add three rounded deep-coral Splash lobes on visible flank. Keep them distinct from the existing head crest. |
| **Skitter:** small low amber body with tiny alert head and exactly six long jointed legs in three pairs, no Sprint fins or Litter eggs. | Keep existing legs unchanged. Add exactly two separated swept-back amber Sprint fins high on the back and a distinct flank cradle with exactly five visible cream Litter eggs. Several generations failed leg-pair fidelity; all are studies only. |
| **Hollow:** tiny slender violet body, long forward neck, watchful head, cream throat and long stilt legs, no inherited lances. | Preserve body and legs. Add one long diagonal violet Reach lance with brass tip from the back, and exactly three separated narrow upward frost-blue Pierce lances on the flank. The generated source has four legs; Unity's Hollow rig has two, so this image is not an anatomy master. |
| **Loam:** gentle green segmented heavy ground-hugging grub with blunt snout, cream underside, no legs, feet or claws, no inherited stem or digging tool. | Preserve legless body. Add a single Regrow stem with exactly two opposing green leaves on a dorsal segment, and one stepped stone/brass Burrow claw from the visible flank; it must not read as a foot. |
| **Pale:** ice-blue manta-like broad wing arc with small hanging central body, calm face, translucent membrane, no legs, feathers, Screen or Chill. | Preserve the wing arc. Add one distinct four-spoke Screen fan behind the central body and a separate cluster of exactly three uneven Chill crystals below a wing root. The generated result shows only two clearly visible crystals; the targeted correction was rejected by the generator and remains outstanding. |

Vetch acting request: five equally sized views of the same base Vetch, left-to-right rest, notice, attack/guard, hit/blink, reveal/celebration, with consistent eyes, proportions, teal pebbled hide and cream chin, no traits, no text or UI.

Vetch materials request: five separate closeup specimens, left-to-right pebbled teal hide, fine-scale cream belly, dark keratin toes, pale mineral Carapace with teal bevels, and coral-orange Cinder keratin with dark roots and amber tips, under identical studio light, no labels or UI.
