#!/usr/bin/env python3
"""Regenerates the two UI textures beside this file. Run from the REPO ROOT:

    python3 client/Assets/UI/Art/generate-textures.py

Neither texture is art; both are arithmetic, which is why the script is
committed with them rather than the PNGs arriving from nowhere. Needs Pillow.

The nine-slice border on shadow-card.png is NOT recorded here - it is
re-asserted on every import by client/Assets/Editor/ArtImportSettings.cs,
because a .meta can be regenerated and a silently-zeroed border turns every
card shadow into a stretched blur.
"""

import math

from PIL import Image

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

# ------------------------------------------------------------------- cta-ramp
# 2x64, #8878cf -> #6f5fbb top to bottom. Two px wide because a 1px texture
# invites the importer to treat it as degenerate.
a, b = (0x88, 0x78, 0xcf), (0x6f, 0x5f, 0xbb)
ramp = Image.new("RGB", (2, 64))
for y in range(64):
    t = y / 63
    c = tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))
    for x in range(2):
        ramp.putpixel((x, y), c)
ramp.save("client/Assets/UI/Art/cta-ramp.png")
print("wrote both")
