#!/usr/bin/env python3
"""Regenerates the five UI textures beside this file. Run from the REPO ROOT:

    python3 client/Assets/UI/Art/generate-textures.py

None of them is art; all five are arithmetic, which is why the script is
committed with them rather than the PNGs arriving from nowhere. Needs Pillow.

The nine-slice border on shadow-card.png is NOT recorded here - it is
re-asserted on every import by client/Assets/Editor/ArtImportSettings.cs,
because a .meta can be regenerated and a silently-zeroed border turns every
card shadow into a stretched blur.

THREE OF THE FIVE EXIST BECAUSE USS CANNOT DRAW THEM. Theme.uss's header
lists the four primitives the handoff asks for that USS has no property for;
these are the answers to three of them:
  - a gradient          -> a ramp texture, stretched (cta, amber, hybrid)
  - a box-shadow        -> a nine-sliced sprite (shadow-card)
  - a dashed border     -> a ring texture, stretched (hero-ring)
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
# EACH RAMP RUNS THE WAY ITS GRADIENT DOES, which is why two of the three are
# 2x64 and one is 64x2. A ramp is stretched to fill its element, so the axis
# it varies along IS its direction and there is no other way to say so - USS
# has no gradient and therefore no angle. The handoff's three:
#
#   cta      linear-gradient(180deg, #8878cf, #6f5fbb)   top -> bottom
#   amber    linear-gradient(100deg, #fdf0d0, #f9e0a8)   left -> right
#   hybrid   linear-gradient(170deg, #ffffff, #f4f0fb)   top -> bottom
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
# SectionCard this project puts the panel on. The other five endpoints are
# the handoff's own, to the digit.


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
RING = 288                     # 3x of --hero-slot
RING_SS = 4                    # supersample, then downsample for the edges
RING_STROKE = 2 * 3            # --hero-ring at 3x
RING_DASHES = 30
RING_DUTY = 3.0 / 11.0         # the handoff's `stroke-dasharray: 3 8`

big = RING * RING_SS
ring = Image.new("RGBA", (big, big), (255, 255, 255, 0))
pen = ImageDraw.Draw(ring)
stroke = RING_STROKE * RING_SS
inset = stroke / 2.0
box = (inset, inset, big - inset - 1, big - inset - 1)
period = 360.0 / RING_DASHES
for k in range(RING_DASHES):
    start = k * period
    pen.arc(box, start, start + period * RING_DUTY, fill=(255, 255, 255, 255), width=int(stroke))

# RGB IS WHITE EVERYWHERE, INCLUDING WHERE ALPHA IS ZERO, which is what keeps
# the downsample clean: Pillow resamples the channels independently and does
# not premultiply, so a transparent pixel carrying black RGB would bleed grey
# into every antialiased edge. Set transparent to white-with-zero-alpha above
# and the colour channel is constant, so only alpha is actually resampled.
ring.resize((RING, RING), Image.LANCZOS).save("client/Assets/UI/Art/hero-ring.png")

print("wrote shadow-card, cta-ramp, amber-ramp, hybrid-ramp, hero-ring")
