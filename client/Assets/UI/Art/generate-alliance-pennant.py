#!/usr/bin/env python3
"""Draw the Allies hero pennant from the UI palette; run from repo root."""

import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[4]
TOKENS = (ROOT / "client/Assets/UI/Shell/Tokens.uss").read_text()
OUT = ROOT / "client/Assets/UI/Art/icons/alliance-pennant.png"
S = 4
W, H = 256, 320


def token(name):
    match = re.search(r"^\s*" + re.escape(name) + r":\s*(#[0-9a-fA-F]{6})\s*;", TOKENS, re.M)
    if not match:
        raise KeyError(name)
    colour = match.group(1)[1:]
    return tuple(int(colour[i:i + 2], 16) for i in (0, 2, 4))


def rgba(rgb, alpha=255):
    return (*rgb, alpha)


def pts(points):
    return [(round(x * S), round(y * S)) for x, y in points]


violet = token("--violet-deep")
violet_light = token("--violet-light")
deep = token("--surface-deep")
brass = token("--brass")
brass_light = token("--brass-light")
cream = token("--surface-paper")

im = Image.new("RGBA", (W * S, H * S))
d = ImageDraw.Draw(im)

# A cloth shadow is isolated from the pennant, so the transparent corners
# remain truly transparent when UI Toolkit scales the illustration.
shadow_mask = Image.new("L", im.size)
sd = ImageDraw.Draw(shadow_mask)
silhouette = [(55, 43), (201, 43), (201, 224), (128, 284), (55, 224)]
sd.polygon(pts([(x + 4, y + 7) for x, y in silhouette]), fill=145)
shadow = Image.new("RGBA", im.size, rgba(deep, 0))
shadow.putalpha(shadow_mask.filter(ImageFilter.GaussianBlur(9 * S)))
im.alpha_composite(shadow)
d = ImageDraw.Draw(im)

# Staff, finials and two visible leather hangers.
d.rounded_rectangle((27*S, 29*S, 229*S, 39*S), 4*S, fill=rgba(brass))
d.line(pts([(30, 32), (226, 32)]), fill=rgba(brass_light), width=2*S)
for x in (27, 229):
    d.ellipse((x*S-7*S, 27*S, x*S+7*S, 41*S), fill=rgba(brass))
    d.ellipse((x*S-3*S, 30*S, x*S+2*S, 35*S), fill=rgba(brass_light))
for x in (83, 173):
    d.rounded_rectangle((x*S-4*S, 35*S, x*S+4*S, 59*S), 3*S, fill=rgba(brass_light))
    d.rectangle((x*S-2*S, 39*S, x*S+2*S, 54*S), fill=rgba(deep))

# Broad brass piping frames a violet cloth. A masked gradient gives the
# surface a restrained fold without pretending the pennant is a photo.
d.polygon(pts(silhouette), fill=rgba(brass))
inner = [(60, 48), (196, 48), (196, 221), (128, 277), (60, 221)]
mask = Image.new("L", im.size)
ImageDraw.Draw(mask).polygon(pts(inner), fill=255)
cloth = Image.new("RGBA", im.size)
cp = cloth.load()
for y in range(45*S, 279*S):
    fy = y / S
    for x in range(58*S, 198*S):
        fx = x / S
        fold = .10 * abs(fx - 128) / 70 + .10 * max(0, fy - 165) / 115
        light = max(0, min(1, .32 + .35 * (1 - abs(fx - 105) / 90) - fold))
        cp[x, y] = rgba(tuple(round(violet[i] * (1 - light) + violet_light[i] * light) for i in range(3)))
im.paste(cloth, (0, 0), mask)
d = ImageDraw.Draw(im)

# Two long woven folds and fine seam stitches give the cloth volume at the
# 110px display size without turning into tiny noise.
d.polygon(pts([(67, 55), (83, 55), (96, 236), (78, 229)]), fill=rgba(cream, 18))
d.polygon(pts([(164, 55), (179, 55), (181, 231), (159, 248)]), fill=rgba(deep, 40))
for side in (-1, 1):
    x = 128 + side * 61
    for y in range(64, 221, 16):
        d.line(pts([(x, y), (x, y + 7)]), fill=rgba(brass_light, 190), width=S)
d.line(pts([(62, 54), (194, 54)]), fill=rgba(brass_light), width=3*S)
d.line(pts([(63, 59), (193, 59)]), fill=rgba(brass, 175), width=S)

# Crest: a cream shield with a shared three-leaf stem. The three branches
# read as an alliance emblem at hero size and remain distinct from the
# smaller navigation glyph.
shield = [(128, 88), (170, 105), (166, 169), (128, 194), (90, 169), (86, 105)]
d.polygon(pts(shield), fill=rgba(brass))
d.polygon(pts([(128, 95), (163, 109), (159, 165), (128, 185), (97, 165), (93, 109)]), fill=rgba(cream))
d.polygon(pts([(128, 102), (156, 114), (153, 159), (128, 177), (103, 159), (100, 114)]), fill=rgba(deep))
d.line(pts([(128, 160), (128, 127)]), fill=rgba(brass_light), width=5*S)
d.line(pts([(128, 143), (109, 128)]), fill=rgba(brass_light), width=4*S)
d.line(pts([(128, 143), (147, 128)]), fill=rgba(brass_light), width=4*S)
for cx, cy, angle in ((128, 120, 0), (108, 123, -1), (148, 123, 1)):
    leaf = [(cx, cy-12), (cx+8+angle*2, cy-2), (cx, cy+8), (cx-8+angle*2, cy-2)]
    d.polygon(pts(leaf), fill=rgba(brass_light))
    d.ellipse((cx*S-2*S, cy*S-5*S, cx*S+1*S, cy*S-2*S), fill=rgba(cream))
d.ellipse((121*S, 151*S, 135*S, 165*S), fill=rgba(brass))
d.ellipse((125*S, 155*S, 131*S, 161*S), fill=rgba(cream))

# A lower embroidered line follows the taper, tying the heraldry to the
# cloth rather than letting the crest float on a blank rectangle.
d.line(pts([(78, 209), (128, 252), (178, 209)]), fill=rgba(brass_light, 220), width=2*S)
d.polygon(pts([(128, 254), (134, 261), (128, 269), (122, 261)]), fill=rgba(brass_light))

im.resize((W, H), Image.Resampling.LANCZOS).save(OUT)
print(f"Wrote {OUT.relative_to(ROOT)}")
