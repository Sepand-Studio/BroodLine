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

from PIL import Image, ImageFilter, ImageDraw

# shadow-card.png - a 64x64 nine-slice. 20px slice borders, 24px rounded box,
# blurred 8px, in the handoff's rgba(63,58,82,.06)->(.09) ink at full alpha so
# the USS tint controls the strength.
img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
d.rounded_rectangle([14, 12, 50, 50], radius=10, fill=(63, 58, 82, 255))
img = img.filter(ImageFilter.GaussianBlur(6))
img.save("client/Assets/UI/Art/shadow-card.png")

# cta-ramp.png - 2x64, #8878cf -> #6f5fbb top to bottom. Two px wide because a
# 1px texture invites the importer to treat it as degenerate.
a, b = (0x88, 0x78, 0xcf), (0x6f, 0x5f, 0xbb)
ramp = Image.new("RGB", (2, 64))
for y in range(64):
    t = y / 63
    c = tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))
    for x in range(2):
        ramp.putpixel((x, y), c)
ramp.save("client/Assets/UI/Art/cta-ramp.png")
print("wrote both")
