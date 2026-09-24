#!/usr/bin/env python3
"""Generate white, tintable facility marks and an editable SVG master set."""

import importlib.util
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[4]
TRAITS = Path(__file__).with_name("generate-trait-icons.py")
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("trait_art", TRAITS)
trait_art = importlib.util.module_from_spec(spec)
spec.loader.exec_module(trait_art)
Glyph = trait_art.Glyph
OUT = ROOT / "client/Assets/UI/Art/icons/facilities"
SVG_OUT = ROOT / "implementation/art-source/facilities"
REVIEW = ROOT / "implementation/reviews/phase10-facility-icons.png"


def marks():
    return {
        "core": (Glyph().stroke([(12,2),(20,6),(20,17),(12,22),(4,17),(4,6)], 2, True)
                 .oval((8,8,16,16), 2).stroke([(12,2),(12,7)], 2)
                 .stroke([(4,12),(8,12)], 2).stroke([(16,12),(20,12)], 2)),
        "splicing": (Glyph().stroke([(3,18),(21,18)], 2.4)
                     .stroke([(5,17),(5,6),(9,6),(9,17)], 2, True)
                     .stroke([(15,17),(15,6),(19,6),(19,17)], 2, True)
                     .stroke([(9,4),(15,4)], 2).oval((10,8,14,12), filled=True)),
        "hatchery": (Glyph().arc(12,19,10,5,0,180,2.2)
                     .oval((3.5,8,9,17), 1.8).oval((9,5,15,17), 1.8)
                     .oval((15,8,20.5,17), 1.8).stroke([(2,19),(22,19)], 2.4)),
        "vault": (Glyph().stroke([(4,3),(20,3),(20,21),(4,21)], 2.2, True)
                  .stroke([(7,6),(17,6),(17,18),(7,18)], 1.7, True)
                  .stroke([(12,6),(12,18)], 1.7).oval((10,10,14,14), filled=True)),
        "harvest": (Glyph().stroke([(12,2),(12,19)], 2.4)
                    .stroke([(4,8),(20,8)], 2.4)
                    .stroke([(5,8),(5,17),(9,20)], 2.1)
                    .stroke([(19,8),(19,17),(15,20)], 2.1)
                    .stroke([(4,21),(20,21)], 2.4).oval((10,3,14,7), filled=True)),
        "drive": (Glyph().oval((7,7,17,17), 2.2)
                  .fill([(10,2),(14,2),(15,6),(9,6)])
                  .fill([(10,22),(14,22),(15,18),(9,18)])
                  .fill([(2,10),(6,9),(6,15),(2,14)])
                  .fill([(22,10),(18,9),(18,15),(22,14)])
                  .oval((10,10,14,14), filled=True)),
    }


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    SVG_OUT.mkdir(parents=True, exist_ok=True)
    icons = marks()
    sheet = Image.new("RGB", (1000, 230), trait_art.token("surface-paper"))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    for i, (name, glyph) in enumerate(icons.items()):
        (SVG_OUT / (name + ".svg")).write_text(glyph.source())
        master = glyph.render()
        master.save(OUT / (name + ".png"))
        x = 20 + i * 163
        draw.rounded_rectangle((x, 18, x + 148, 211), radius=14,
                               fill=trait_art.token("surface"), outline=trait_art.token("brass"), width=2)
        colored = Image.new("RGBA", master.size, trait_art.token("violet-text") + (255,))
        colored.putalpha(master.getchannel("A"))
        sheet.paste(colored, (x + 38, 36), colored)
        draw.text((x + 16, 143), name.title(), fill=trait_art.token("ink-primary"), font=font)
        small = master.resize((24, 24), Image.Resampling.LANCZOS)
        sample = Image.new("RGBA", small.size, trait_art.token("violet-text") + (255,))
        sample.putalpha(small.getchannel("A"))
        sheet.paste(sample, (x + 104, 154), sample)
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(REVIEW)
    print("Wrote", len(icons), "facility icons and", REVIEW)


if __name__ == "__main__":
    main()
