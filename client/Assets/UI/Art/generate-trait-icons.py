#!/usr/bin/env python3
"""Draw Broodline's twelve trait marks as white, tintable 3x PNGs.

Run from the repository root with a Python that has Pillow:
    python3 client/Assets/UI/Art/generate-trait-icons.py

The matching SVGs preserve the editable geometry. Both formats are drawn
from the same 24-unit commands; UI Toolkit uses the PNGs at 14-19 screen px.
The contact sheet is a review artifact, not a runtime texture.
"""

import math
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / "client/Assets/UI/Art/icons/traits"
SVG_OUT = ROOT / "implementation/art-source/traits"
REVIEW = ROOT / "implementation/reviews/phase10-trait-icons.png"
TOKENS = ROOT / "client/Assets/UI/Shell/Tokens.uss"
SCALE = 4
WHITE = (255, 255, 255, 255)


class Glyph:
    def __init__(self):
        self.commands = []
        self.svg = []

    def stroke(self, points, width=2.2, closed=False):
        self.commands.append(("stroke", points, width, closed))
        tag = "polygon" if closed else "polyline"
        coords = " ".join(f"{x:g},{y:g}" for x, y in points)
        self.svg.append(f'<{tag} points="{coords}" fill="none" stroke="white" stroke-width="{width:g}" stroke-linecap="round" stroke-linejoin="round"/>')
        return self

    def fill(self, points):
        self.commands.append(("fill", points))
        coords = " ".join(f"{x:g},{y:g}" for x, y in points)
        self.svg.append(f'<polygon points="{coords}" fill="white"/>')
        return self

    def oval(self, box, width=2.2, filled=False):
        self.commands.append(("oval", box, width, filled))
        x0, y0, x1, y1 = box
        self.svg.append(f'<ellipse cx="{(x0+x1)/2:g}" cy="{(y0+y1)/2:g}" rx="{(x1-x0)/2:g}" ry="{(y1-y0)/2:g}" fill="{"white" if filled else "none"}" stroke="{"none" if filled else "white"}" stroke-width="{width:g}"/>')
        return self

    def arc(self, cx, cy, rx, ry, start, end, width=2.2):
        points = []
        for i in range(17):
            angle = math.radians(start + (end-start)*i/16)
            points.append((cx + rx*math.cos(angle), cy + ry*math.sin(angle)))
        return self.stroke(points, width)

    def render(self):
        im = Image.new("RGBA", (24*SCALE, 24*SCALE), (0, 0, 0, 0))
        draw = ImageDraw.Draw(im)
        for command in self.commands:
            kind = command[0]
            if kind == "stroke":
                _, points, width, closed = command
                pts = [(round(x*SCALE), round(y*SCALE)) for x, y in points]
                w = round(width*SCALE)
                draw.line(pts + ([pts[0]] if closed else []), fill=WHITE, width=w, joint="curve")
                for x, y in pts if not closed else []:
                    r = w/2
                    draw.ellipse((x-r, y-r, x+r, y+r), fill=WHITE)
            elif kind == "fill":
                draw.polygon([(round(x*SCALE), round(y*SCALE)) for x, y in command[1]], fill=WHITE)
            else:
                _, box, width, filled = command
                b = tuple(round(v*SCALE) for v in box)
                draw.ellipse(b, fill=WHITE if filled else None, outline=None if filled else WHITE,
                             width=round(width*SCALE))
        return im.resize((72, 72), Image.Resampling.LANCZOS)

    def source(self):
        return '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">\n  ' + '\n  '.join(self.svg) + '\n</svg>\n'


def make_icons():
    g = {}

    # Protective shell: segmented shield rather than a generic UI shield.
    g["carapace"] = (Glyph().stroke([(12,2),(20,6),(19,14),(16,19),(12,22),(8,19),(5,14),(4,6)], closed=True)
                     .stroke([(12,3),(12,20)], 1.8).stroke([(5,9),(12,11),(19,9)], 1.8))
    # Vocal challenge: broad call bubble with a sharp exclamation centre.
    g["taunt"] = (Glyph().stroke([(4,5),(20,5),(20,17),(13,17),(9,21),(9,17),(4,17)], closed=True)
                 .stroke([(12,8),(12,12)], 2.5).oval((10.8,14,13.2,16.4), filled=True))
    # Ember's flame carries a hot inner tongue and a long outer tongue.
    g["cinder"] = (Glyph().fill([(12,2),(15,8),(18,5),(19,12),(17,18),(12,22),(7,20),(4,15),(7,9),(8,13)])
                  .fill([(12,11),(15,16),(12,20),(9,17)]))
    # Three droplets fan outward; one large drop is not enough to read splash.
    g["splash"] = (Glyph().fill([(12,3),(16,11),(15,16),(12,18),(9,16),(8,11)])
                  .fill([(5,11),(8,16),(7,20),(4,20),(2.5,17)])
                  .fill([(19,10),(22,16),(21,20),(18,21),(16,17)]))
    # Forward thrust and trailing speed lines, with a distinct arrow point.
    g["sprint"] = (Glyph().stroke([(3,8),(10,8)], 2).stroke([(2,12),(8,12)], 2)
                  .stroke([(4,16),(11,16)], 2).stroke([(11,5),(20,12),(11,19)], 2.6))
    # A small cluster of eggs under a common nest arc.
    g["litter"] = (Glyph().oval((3,9,9,18), 1.6).oval((9,5,15,17), 1.6).oval((15,9,21,18), 1.6)
                  .arc(12,16,10,5,15,165,2))
    # Forked extension: its open tip reads differently from Pierce's point.
    g["reach"] = (Glyph().stroke([(3,12),(17,12)], 2.6).stroke([(16,5),(21,12),(16,19)], 2.4)
                 .stroke([(7,8),(7,16)], 1.8))
    # Focused long point passing through a target ring.
    g["pierce"] = (Glyph().oval((4,4,20,20), 1.8).stroke([(2,20),(18,4)], 2.4)
                  .fill([(17,3),(22,2),(21,7)]))
    # Two leaves grown from a shared stem.
    g["regrow"] = (Glyph().stroke([(12,22),(12,12)], 2.4)
                  .stroke([(12,15),(7,10),(3,10),(4,14),(9,17),(12,16)], 1.9)
                  .stroke([(12,12),(16,5),(21,3),(20,9),(16,13),(12,13)], 1.9))
    # Tunnel mouth with a downward digging chevron and loose earth.
    g["burrow"] = (Glyph().arc(12,19,9,10,180,360,2.3).stroke([(3,19),(21,19)], 2.3)
                  .stroke([(12,7),(12,15)], 2.2).stroke([(9,12),(12,15),(15,12)], 2)
                  .oval((5,20,7,22), filled=True).oval((17,20,19,22), filled=True))
    # A layered energy barrier, distinct from the plated Carapace shield.
    g["screen"] = (Glyph().stroke([(4,4),(16,4),(20,8),(20,20),(8,20),(4,16)], closed=True)
                  .stroke([(8,4),(8,20)], 1.7).stroke([(4,9),(20,9)], 1.7)
                  .stroke([(4,15),(20,15)], 1.7))
    # Six arms and small split tips survive at chip size.
    chill = Glyph().stroke([(12,2),(12,22)],2).stroke([(3.3,7),(20.7,17)],2).stroke([(3.3,17),(20.7,7)],2)
    for x,y,dx,dy in [(12,4,2,2),(12,4,-2,2),(12,20,2,-2),(12,20,-2,-2),
                       (5,8,3,0),(5,8,0,3),(19,16,-3,0),(19,16,0,-3),
                       (5,16,3,0),(5,16,0,-3),(19,8,-3,0),(19,8,0,3)]:
        chill.stroke([(x,y),(x+dx,y+dy)],1.5)
    g["chill"] = chill
    return g


def token(name):
    match = re.search(r"^\s*--"+name+r":\s*(#[0-9a-fA-F]{6})", TOKENS.read_text(), re.M)
    if match is None:
        raise ValueError("Missing token " + name)
    return tuple(bytes.fromhex(match.group(1)[1:]))


def contact_sheet(icons):
    species = ["vetch","vetch","ember","ember","skitter","skitter",
               "hollow","hollow","loam","loam","pale","pale"]
    names = list(icons)
    paper, surface, ink = token("surface-paper"), token("surface"), token("ink-primary")
    tones = dict(vetch=token("teal-text"), ember=token("coral-text"), skitter=token("amber-text"),
                 hollow=token("violet-text"), loam=token("green-text"), pale=ink)
    sheet = Image.new("RGB", (1100, 470), paper)
    draw = ImageDraw.Draw(sheet)
    font_path = "/System/Library/Fonts/Supplemental/Arial.ttf"
    try:
        title_font = ImageFont.truetype(font_path, 24)
        detail_font = ImageFont.truetype(font_path, 15)
    except OSError:
        title_font = ImageFont.load_default(size=24)
        detail_font = ImageFont.load_default(size=15)
    draw.text((24,15), "THE TWELVE TRAITS", fill=ink, font=title_font)
    draw.text((24,47), "One silhouette per trait · white master glyphs tint to their carrier species", fill=token("ink-secondary"), font=detail_font)
    for i, name in enumerate(names):
        x = 24 + (i%6)*179
        y = 82 + (i//6)*190
        draw.rounded_rectangle((x,y,x+165,y+174), radius=14, fill=surface, outline=token("brass"), width=2)
        tone = tones[species[i]]
        source = icons[name].render()
        alpha = source.getchannel("A")
        colored = Image.new("RGBA", source.size, tone + (255,))
        colored.putalpha(alpha)
        sheet.paste(colored, (x+47,y+12), colored)
        draw.text((x+12,y+91), name.title(), fill=ink, font=title_font)
        draw.text((x+12,y+127), species[i].title()+" · 19px / 14px", fill=token("ink-secondary"), font=detail_font)
        small = source.resize((14,14),Image.Resampling.LANCZOS)
        small_colored = Image.new("RGBA", small.size, tone+(255,)); small_colored.putalpha(small.getchannel("A"))
        sheet.paste(small_colored,(x+140,y+17),small_colored)
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(REVIEW)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    SVG_OUT.mkdir(parents=True, exist_ok=True)
    icons = make_icons()
    for name,glyph in icons.items():
        (SVG_OUT / (name+".svg")).write_text(glyph.source())
        glyph.render().save(OUT / (name+".png"))
    contact_sheet(icons)
    print("Wrote", len(icons), "trait glyphs and", REVIEW)


if __name__ == "__main__":
    main()
