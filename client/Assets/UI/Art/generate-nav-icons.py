#!/usr/bin/env python3
"""Generate Broodline's five tintable 24-unit tab marks and review sheet."""

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
OUT = ROOT / "client/Assets/UI/Art/icons"
SVG_OUT = ROOT / "implementation/art-source/nav"
REVIEW = ROOT / "implementation/reviews/phase10-nav-icons.png"


def marks():
    # Each mark uses the same round, 2.2-unit stroke as the trait/facility set.
    return {
        "map": (Glyph().stroke([(3,8),(7,4),(14,4),(20,9),(20,17),(16,21),(8,21),(3,16)],2.1,True)
                .stroke([(4,15),(9,13),(12,16),(17,14)],2)
                .oval((6.5,6,10.5,10),filled=True).oval((15,10,19,14),filled=True)),
        "ark": (Glyph().stroke([(4,18),(4,8),(12,3),(20,8),(20,18)],2.4)
                .stroke([(2,20),(22,20)],2.4)
                .stroke([(8,17),(8,10),(12,8),(16,10),(16,17)],2)
                .oval((10,11,14,15),filled=True)),
        "splice": (Glyph().stroke([(5,3),(19,21)],2.2).stroke([(19,3),(5,21)],2.2)
                   .stroke([(7,6),(17,6)],1.8).stroke([(7,18),(17,18)],1.8)
                   .oval((3.5,1.5,7.5,5.5),filled=True).oval((16.5,1.5,20.5,5.5),filled=True)
                   .oval((3.5,18.5,7.5,22.5),filled=True).oval((16.5,18.5,20.5,22.5),filled=True)),
        "lab": (Glyph().stroke([(4,4),(20,4),(20,19),(16,22),(8,22),(4,19)],2.2,True)
                .stroke([(7,10),(17,10)],1.9).stroke([(12,4),(12,9)],1.9)
                .oval((6,13,10,17),filled=True).oval((14,13,18,17),filled=True)
                .oval((10,17,14,21),filled=True)),
        "allies": (Glyph().stroke([(12,2),(12,10)],2.2)
                   .fill([(12,3),(21,4),(18,9),(12,9)])
                   .arc(12,21,9,4,180,360,2)
                   .oval((3,13,8,18),filled=True).oval((9.5,11,14.5,16),filled=True)
                   .oval((16,13,21,18),filled=True)),
    }


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    SVG_OUT.mkdir(parents=True, exist_ok=True)
    icons = marks()
    sheet = Image.new("RGB", (925, 232), trait_art.token("surface-paper"))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    for i, (name, glyph) in enumerate(icons.items()):
        (SVG_OUT / (name + ".svg")).write_text(glyph.source())
        master = glyph.render()
        master.save(OUT / (name + ".png"))
        x = 18 + i * 182
        draw.rounded_rectangle((x, 18, x + 166, 214), radius=14,
                               fill=trait_art.token("surface"), outline=trait_art.token("brass"), width=2)
        tint = Image.new("RGBA", master.size, trait_art.token("violet-text") + (255,))
        tint.putalpha(master.getchannel("A"))
        sheet.paste(tint, (x + 47, 32), tint)
        draw.text((x + 14, 148), name.upper(), fill=trait_art.token("ink-primary"), font=font)
        # The sample is the real tab's 19px draw size against its dark frame.
        draw.rounded_rectangle((x + 105, 158, x + 151, 204), radius=8,
                               fill=trait_art.token("surface-deep"))
        small = master.resize((19, 19), Image.Resampling.LANCZOS)
        sample = Image.new("RGBA", small.size, trait_art.token("brass-light") + (255,))
        sample.putalpha(small.getchannel("A"))
        sheet.paste(sample, (x + 119, 171), sample)
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(REVIEW)
    print("Wrote", len(icons), "nav icons and", REVIEW)


if __name__ == "__main__":
    main()
