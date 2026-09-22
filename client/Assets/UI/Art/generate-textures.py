#!/usr/bin/env python3
"""Regenerates the eight UI textures beside this file. Run from the REPO ROOT:

    python3 client/Assets/UI/Art/generate-textures.py

None of them is art; all eight are arithmetic, which is why the script is
committed with them rather than the PNGs arriving from nowhere. Needs Pillow.

The nine-slice border on shadow-card.png is NOT recorded here - it is
re-asserted on every import by client/Assets/Editor/ArtImportSettings.cs,
because a .meta can be regenerated and a silently-zeroed border turns every
card shadow into a stretched blur.

SEVEN OF THE EIGHT EXIST BECAUSE USS CANNOT DRAW THEM. Theme.uss's header
lists the four primitives the handoff asks for that USS has no property for;
these are the answers to three of them:
  - a gradient          -> a ramp texture, stretched (cta, amber, hybrid,
                           band, band-coral)
  - a box-shadow        -> a nine-sliced sprite (shadow-card)
  - a dashed border     -> a ring texture, stretched (hero-ring, band-ring)
USS has `border-width` and `border-color` and no `border-style` at all, so a
dashed or dotted ring is not a border that has been styled - it is a picture.
"""

import math

from PIL import Image, ImageDraw

# ---------------------------------------------------------------- shadow-card
#
# A 64x64 nine-slice drop shadow, 20px slice borders, for the .elev-1/.elev-2
# WRAPPER classes in Theme.uss.
#
# THE FIRST VERSION OF THIS TEXTURE WAS A BLURRED ROUNDED RECT AND IT WAS
# WRONG. Its nine-slice centre region measured alpha 255, and a nine-slice
# stretches its centre across the whole element interior - so instead of a
# shadow it painted near-opaque ink across every card it touched. Measured on
# the Primitives fixture: card centre 248 with .elev-1 and 245 with .elev-2
# against 255 unelevated, densest in the middle, fading to white at the edges.
# A drop shadow inverted. Nothing was drawn outside the card at all, because
# UI Toolkit clips background-image to the element's own box.
#
# So the ink lives ONLY in the outer CARD-px frame, and the centre region is
# transparent by construction. The card's edge is at texture CARD, which is
# strictly inside the 20px slice border: that headroom is what lets the blur
# finish inside the border band instead of being cut off by the stretched
# centre, which is what produces a seam where a corner tile meets an edge band.
#
# THE GEOMETRY IS WHY -unity-slice-scale DIFFERS PER CLASS. Nine-slice maps
# texture offset p to p*scale screen px from the element edge, and the card
# edge sits at exactly `padding` screen px - so scale must be padding/CARD.
# With CARD=10 that is 0.6 for --elev-1-spread (6px) and 1.2 for
# --elev-2-spread (12px). A single scale cannot serve both: at 0.5 the card
# edge would have to be at texture 12 AND texture 24, and 24 is past the
# border into the stretched centre, which is geometrically impossible.
#
# The two levels then fall out of the one texture exactly, because the
# handoff's elevations are themselves a 2:1 pair - 0 2px 8px and 0 4px 16px.
# DY and SIGMA below are in texture px; multiplied by the two scales they give
# offsets of 2px and 4px, matching the handoff's y-offsets exactly, and blur
# sigmas of 3px and 6px.
CARD = 10           # texture px from each edge to the card's own edge
SIGMA = 5.0         # blur, texture px
DY = 10.0 / 3.0     # downward offset, texture px -> 2px at 0.6, 4px at 1.2
SIZE = 64
BORDER = 20         # the slice border ArtImportSettings re-asserts
INK = (63, 58, 82)  # the handoff's shadow ink; USS tints it to .06 / .09


def phi(z):
    """Standard normal CDF - a blurred half-plane's alpha profile."""
    return 0.5 * (1.0 + math.erf(z / math.sqrt(2.0)))


def profile(v, lo, hi):
    """Alpha of a blurred slab spanning [lo, hi) sampled at v."""
    return min(phi((v - lo) / SIGMA), phi((hi - v) / SIGMA))


shadow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
px = shadow.load()
far = SIZE - CARD
for y in range(SIZE):
    cy = y + 0.5
    for x in range(SIZE):
        cx = x + 0.5
        # Inside the card the shadow is hidden by the card's own opaque
        # --surface fill, so it is cut out rather than drawn and covered.
        # This is also what guarantees the centre region is transparent:
        # [BORDER, SIZE-BORDER) is strictly inside [CARD, SIZE-CARD).
        if CARD <= cx < far and CARD <= cy < far:
            continue
        # Separable, because a 2D Gaussian is: the product of the two
        # half-plane profiles IS the blurred rectangle corner, exactly.
        a = profile(cx, CARD, far) * profile(cy, CARD + DY, far + DY)
        px[x, y] = (INK[0], INK[1], INK[2], int(round(255 * a)))

# The centre region MUST be empty. This is the defect the first version
# shipped, so it is asserted rather than trusted.
for y in range(BORDER, SIZE - BORDER):
    for x in range(BORDER, SIZE - BORDER):
        assert px[x, y][3] == 0, "nine-slice centre is not transparent at %d,%d" % (x, y)

shadow.save("client/Assets/UI/Art/shadow-card.png")

# ---------------------------------------------------------------------- ramps
#
# EACH RAMP RUNS THE WAY ITS GRADIENT DOES, which is why four of the five are
# 2x64 and one is 64x2. A ramp is stretched to fill its element, so the axis
# it varies along IS its direction and there is no other way to say so - USS
# has no gradient and therefore no angle. The handoff's five:
#
#   cta         linear-gradient(180deg, #8878cf, #6f5fbb)   top -> bottom
#   amber       linear-gradient(100deg, #fdf0d0, #f9e0a8)   left -> right
#   hybrid      linear-gradient(170deg, #ffffff, #f4f0fb)   top -> bottom
#   band        linear-gradient(170deg, #ffffff, #efe9fb)   top -> bottom
#   band-coral  linear-gradient(170deg, #fbeee9, #f6e2e4)   top -> bottom
#
# 100deg is 10 degrees off horizontal and 170deg is 10 off vertical; CSS
# measures a gradient line clockwise from up, so both are within 10 degrees
# of an axis and a 2px cross-section loses nothing visible at these sizes.
#
# TWO PX ON THE SHORT AXIS, never one: a 1px texture invites the importer to
# treat it as degenerate.
#
# THE HYBRID RAMP'S DEEP END IS NOT THE HANDOFF'S. Tokens.uss's
# --hybrid-tint-deep has the reasoning - the handoff's #f4f0fb is a sheen on
# the #f7f4fb page the panel sits on there, and is invisible on the white
# SectionCard this project puts the panel on. The other seven endpoints are
# the handoff's own, to the digit.
#
# THE BAND RAMP IS NOT THE HYBRID RAMP, AND THE FIRST THING CHECKED WAS
# WHETHER IT COULD BE. They are the same handoff declaration - 170deg, white
# to one step off --paper - but the hybrid ramp was DEEPENED at both ends for
# a panel drawn on a white SectionCard (#f7f4fb -> #ebe4f7), and the band is
# drawn on --paper itself. Starting a band at --paper would make its top edge
# invisible against the page it sits on, which is the same defect
# --hybrid-tint-deep exists to avoid, mirrored. So the band keeps the
# handoff's own white start.
#
# ITS DEEP END IS --violet-tint (#f1ecfa) AND NOT THE HANDOFF'S #efe9fb,
# which is 6 away in summed channel distance. The six band instances in the
# bundle end at #eeeaf8, #efe9fb, #f0edf9, #f2edfb and #f4f0fb - a 16-wide
# spread that is one colour at this precision - so a seventh near-violet
# invented to match one of them is the drift verify-uss-tokens.sh exists to
# stop. Measured on the surfaces that matter: #f1ecfa is 38 from the white
# the ramp starts at and 15 from the --paper page the band sits on, so the
# band reads as its own surface at both ends.


def ramp(name, start, end, horizontal=False):
    """A two-stop linear ramp, 64 steps along its gradient axis."""
    n = 64
    img = Image.new("RGB", (n, 2) if horizontal else (2, n))
    for i in range(n):
        t = i / (n - 1)
        c = tuple(round(start[k] + (end[k] - start[k]) * t) for k in range(3))
        for j in range(2):
            img.putpixel((i, j) if horizontal else (j, i), c)
    img.save("client/Assets/UI/Art/" + name)


ramp("cta-ramp.png", (0x88, 0x78, 0xcf), (0x6f, 0x5f, 0xbb))
ramp("amber-ramp.png", (0xfd, 0xf0, 0xd0), (0xf9, 0xe0, 0xa8), horizontal=True)
ramp("hybrid-ramp.png", (0xf7, 0xf4, 0xfb), (0xeb, 0xe4, 0xf7))
ramp("band-ramp.png", (0xff, 0xff, 0xff), (0xf1, 0xec, 0xfa))

# A SECOND BAND RAMP, BECAUSE A TINT IS A RAMP AND NOT A COLOUR. Phase 9
# Task 18. `Wave Defeat.dc.html:31` is the band in coral -
# `linear-gradient(170deg, #fbeee9, #f6e2e4)` at radius 26 with the same
# `0 4px 16px` - and `HeroBand.cs` records four more of the same furniture in
# a blue-grey. None of them is white-to-tint the way the violet six are: they
# run pale-tint to deep-tint, so neither of the two cheap hooks reproduces one.
#
# WHAT WAS MEASURED AND REJECTED, so nobody re-derives it:
#   - `-unity-background-image-tint-color` on this file's own band-ramp.png.
#     The tint MULTIPLIES, so the top lands exactly on the tint passed and the
#     bottom lands on --violet-tint TIMES it. For coral (tint #fbeee9) the
#     bottom comes out #eddce4 against the handoff's #f6e2e4 - 9+6+0 = 15 in
#     summed channel distance, which this project would accept. For the FOUR
#     blue-grey bands (tint #eef4f7) it comes out #e1e2f2 against #e4eef4:
#     3+12+2 = 17 summed, but the green channel carries all of it and it
#     carries it the wrong way. The handoff's deep end is G-R = +10 (a cool
#     blue-green); multiplied through the violet ramp it is G-R = +1 and
#     B-G = +16, which is the violet ramp's own cast showing through. A hook
#     that passed coral and failed the four is a hook designed for one screen.
#   - an ALPHA ramp (white fading to transparent) over `background-color`, so
#     the fill alone would be the hook. It cannot express these: solving
#     white*a + deep*(1-a) = the handoff's top per channel gives a = 0.56,
#     0.41 and 0.19 for coral's three channels. A two-stop ramp between two
#     DIFFERENT hues is not one colour behind one alpha wash.
#
# So each tint is its own two-stop ramp at the handoff's own endpoints, which
# is what every other ramp in this file already is. The fifth and sixth cost
# one line here, one token pair and one rule in HeroBand.uss.
ramp("band-ramp-coral.png", (0xfb, 0xee, 0xe9), (0xf6, 0xe2, 0xe4))

# ----------------------------------------------------------- the two rings
#
# ONE FUNCTION, TWO RINGS, AND TASK 14c IS WHY IT IS A FUNCTION. It was
# hero-ring's inline block until the hero BAND needed a ring of its own and
# the first question asked was whether hero-ring.png could simply be
# stretched to it. It cannot, and the arithmetic is worth keeping because it
# is the general answer for the next ring too: a stretched ring keeps its
# DASH COUNT and scales its STROKE, and both of those are wrong by a lot at
# 2.75x.
#
#   hero-ring.png stretched from 96px to the band's 264px
#     stroke   2px  ->  2 * 264/96 = 5.5px   against the handoff's 1.6
#     dashes   30   ->  30                   against the handoff's ~52
#
# So the dashes come out 1.7x too long, the gaps 1.7x too wide and the ink
# 3.4x too heavy - a different ring, not the same ring bigger. A second
# texture it is, and the shared code below is what stops the two drifting in
# how they are DRAWN while differing in what they draw.
#
# THE BAND RING IS ONE TEXTURE FOR THREE HANDOFF INSTANCES, and that is a
# deliberate rounding rather than an oversight. `Onboarding.dc.html:45` is
# r 132 with `stroke-dasharray: 4 12`, `Splice Reveal.dc.html:45` is r 140
# with the same, and `Hybrid Growth.dc.html:50` is r 104 with `4 10` - which
# is 51.8, 55.0 and 46.7 dash periods respectively. 52 sits inside 11% of all
# three, and because the texture is stretched a screen that wants a different
# diameter gets the same 52 dashes at a different scale. Three PNGs for a
# spread that narrow is three files to keep in step for no visible gain.
#
# 52, NOT 51.84, FOR THE REASON THE 30 BELOW IS AN INTEGER: a dash pattern
# laid along a circle has to CLOSE, and an arc length that does not divide
# the circumference leaves one short dash where the pattern meets its own
# start. At the drawn size that reads as a nick rather than as a pattern.
#
# THE RING DOES NOT ROTATE, AND THE HANDOFF'S DOES. Every instance of it
# carries `class="spin"`, which is `animation: orbit 20s linear infinite`.
# USS on 6000.6.0f1 has `transition-*` and no `@keyframes` and no
# `animation`, so there is no property this can land in - it is the same
# class of absence as `align-items: baseline`, `text-transform` and `calc()`,
# and it is recorded here rather than dropped silently. The only mechanism
# that WOULD produce it is a C# scheduler writing `style.rotate` every frame,
# which was considered and declined: it would spend a repaint per frame on
# decoration, and it would make the capture corpus - this project's only
# evidence that anything renders at all - land on a different frame every
# run. HeroSlot made the same call in Task 13 ("the rotation is not
# reproduced here and is not missed on a still surface") and the Vocabulary
# capture shows a ring that reads correctly without it.


def dotted_ring(name, size, stroke, dashes, duty, ss=4):
    """A closed dotted circle, authored white, supersampled then downsampled.

    `size` is the texture's own edge in px, `stroke` its ink width in the SAME
    texture px, `dashes` the integer number of periods around the circle and
    `duty` the inked fraction of one period.
    """
    big = size * ss
    img = Image.new("RGBA", (big, big), (255, 255, 255, 0))
    pen = ImageDraw.Draw(img)
    width = stroke * ss
    inset = width / 2.0
    box = (inset, inset, big - inset - 1, big - inset - 1)
    period = 360.0 / dashes
    for k in range(dashes):
        start = k * period
        pen.arc(box, start, start + period * duty, fill=(255, 255, 255, 255), width=int(width))

    # RGB IS WHITE EVERYWHERE, INCLUDING WHERE ALPHA IS ZERO, which is what
    # keeps the downsample clean: Pillow resamples the channels independently
    # and does not premultiply, so a transparent pixel carrying black RGB
    # would bleed grey into every antialiased edge. The fill above is
    # white-with-zero-alpha for exactly that reason, so the colour channel is
    # constant and only alpha is actually resampled.
    img.resize((size, size), Image.LANCZOS).save("client/Assets/UI/Art/" + name)


# ------------------------------------------------------------------ hero-ring
#
# The dotted ring around a HeroSlot, as a picture, because USS has
# `border-width` and `border-color` and NO `border-style` - there is no dashed
# border to ask for. The handoff draws this ring as an SVG circle with
# `stroke-dasharray: 3 8` (predicted hybrid, r 54) and `4 7` (parent slots,
# r 42), slowly rotating; the rotation is not reproduced here and is not
# missed on a still surface.
#
# AUTHORED WHITE, LIKE THE GLYPHS, AND FOR THE SAME REASON.
# -unity-background-image-tint-color MULTIPLIES, so a mark authored in its
# final colour can only ever be darkened. White times a tint IS the tint, so
# HeroSlot.uss can tint one ring texture to the species of whatever is inside
# it - which is what the handoff does, and what a second PNG per species
# would otherwise have cost. icons.uss's header has the long form.
#
# 3x, LIKE THE GLYPHS: 288px authored, 96px drawn (--hero-slot). Stretched to
# fill, so a slot at any other size still gets a round ring - only the dash
# count would read differently, and nothing in the phase draws one at another
# size.
#
# THIRTY DASHES, NOT A DASH LENGTH. A dash pattern laid along a circle has to
# CLOSE - an arc length that does not divide the circumference leaves one
# short dash where the pattern meets its own start, and on a 96px ring that
# reads as a nick rather than as a pattern. So the count is the integer and
# the length falls out of it: 30 periods of 12 degrees, 27.6% of each period
# inked, which is the handoff's own 3-on-8-off duty cycle at this radius.
dotted_ring("hero-ring.png",
            288,           # 3x of --hero-slot (96px)
            2 * 3,         # --hero-ring (2px) at 3x
            30,
            3.0 / 11.0)    # the handoff's `stroke-dasharray: 3 8`

# ------------------------------------------------------------------ band-ring
#
# The dotted ring behind a HeroBand's subject - the handoff's own
# `<g class="spin" ... stroke-dasharray="4 12"><circle r="132">` on
# `Onboarding.dc.html:45`, drawn in a 406-wide viewBox. This project's content
# column IS 406px at the 430 capture frame, so the 264px diameter is taken at
# face value rather than rescaled; --band-ring in Tokens.uss is that number
# and this line and HeroBand.uss are the two files that have to agree on it.
#
# 3x, LIKE THE GLYPHS AND LIKE hero-ring: 792px authored, 264px drawn. The
# stroke is the SAME six texture px as hero-ring's, which at 3x is the same
# 2px of ink - the handoff draws both rings at 1.6-2px and --hero-ring
# already records that rounding.
#
# THE DASH PITCH COMES OUT AT THE HANDOFF'S, CHECKED RATHER THAN ASSUMED. At
# 264px drawn with a 2px stroke the centreline radius is 131, so the
# circumference is 823.1px and one of 52 periods is 15.83px: 3.96px of ink and
# 11.87px of gap, against the handoff's 4 and 12.
dotted_ring("band-ring.png",
            792,           # 3x of --band-ring (264px)
            2 * 3,         # the same 2px of ink as hero-ring, at 3x
            52,
            4.0 / 16.0)    # the handoff's `stroke-dasharray: 4 12`

print("wrote shadow-card, cta-ramp, amber-ramp, hybrid-ramp, band-ramp, "
      "band-ramp-coral, hero-ring, band-ring")
