#!/usr/bin/env python3
"""Generate six white, tintable pack marks from one 24-unit geometry set."""

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
OUT = ROOT / "client/Assets/UI/Art/icons/store"
SVG_OUT = ROOT / "implementation/art-source/store"
REVIEW = ROOT / "implementation/reviews/phase10-store-pack-icons.png"


def marks():
    shard = [(12,3),(16,9),(13,18),(11,18),(8,9)]
    return {
        "pack-1": (Glyph().fill(shard).fill([(4,11),(7,13),(6,19),(3,17)])
                   .fill([(19,10),(22,13),(20,18),(17,16)])),
        "pack-5": (Glyph().stroke([(5,8),(19,8),(20,20),(4,20)],2.2,True)
                   .stroke([(8,8),(10,4),(14,4),(16,8)],2)
                   .fill([(12,10),(15,14),(12,18),(9,14)])),
        "pack-10": (Glyph().stroke([(3,8),(21,8),(21,20),(3,20)],2.1,True)
                    .stroke([(8,8),(8,5),(16,5),(16,8)],2)
                    .stroke([(3,14),(21,14)],1.8)
                    .fill([(12,10),(14,13),(12,16),(10,13)])),
        "pack-20": (Glyph().stroke([(3,7),(12,3),(21,7),(21,19),(12,22),(3,19)],2.1,True)
                    .stroke([(3,7),(12,11),(21,7)],1.8)
                    .stroke([(12,11),(12,22)],1.8)
                    .stroke([(8,9),(8,20)],1.6).stroke([(16,9),(16,20)],1.6)),
        "pack-50": (Glyph().stroke([(3,4),(21,4),(21,21),(3,21)],2.2,True)
                    .stroke([(6,7),(18,7),(18,18),(6,18)],1.8,True)
                    .oval((8,9,16,17),1.8).stroke([(12,9),(12,17)],1.6)
                    .stroke([(8,13),(16,13)],1.6)),
        "pack-100": (Glyph().stroke([(4,7),(12,2),(20,7),(20,19),(12,22),(4,19)],2.2,True)
                     .stroke([(4,7),(20,7)],1.8)
                     .stroke([(8,10),(16,10),(16,17),(8,17)],1.7,True)
                     .fill([(12,11),(14,14),(12,17),(10,14)])),
    }


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    SVG_OUT.mkdir(parents=True, exist_ok=True)
    icons = marks()
    sheet = Image.new("RGB", (1060, 230), trait_art.token("surface-paper"))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    for i, (name, glyph) in enumerate(icons.items()):
        (SVG_OUT / (name + ".svg")).write_text(glyph.source())
        master = glyph.render()
        master.save(OUT / (name + ".png"))
        x = 16 + i * 174
        draw.rounded_rectangle((x, 18, x + 158, 210), radius=14,
                               fill=trait_art.token("surface"), outline=trait_art.token("brass"), width=2)
        colored = Image.new("RGBA", master.size, trait_art.token("violet-text") + (255,))
        colored.putalpha(master.getchannel("A"))
        sheet.paste(colored, (x + 43, 32), colored)
        draw.text((x + 14, 145), name.upper(), fill=trait_art.token("ink-primary"), font=font)
        small = master.resize((28, 28), Image.Resampling.LANCZOS)
        sample = Image.new("RGBA", small.size, trait_art.token("brass") + (255,))
        sample.putalpha(small.getchannel("A"))
        sheet.paste(sample, (x + 108, 155), sample)
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(REVIEW)
    print("Wrote", len(icons), "store icons and", REVIEW)


if __name__ == "__main__":
    main()
