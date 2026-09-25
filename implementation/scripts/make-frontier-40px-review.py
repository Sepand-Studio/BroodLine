#!/usr/bin/env python3
"""Arrange Unity's six native 40px portraits for phone-size art review."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "implementation/results/silhouettes"
TARGET = ROOT / "specs/Designs/visual-production-v1/frontier-40px-review.png"
SPECIES = ("vetch", "ember", "pale", "skitter", "hollow", "loam")
TRAITS = (
    "Carapace + Taunt", "Cinder + Splash", "Screen + Chill",
    "Sprint + Litter", "Reach + Pierce", "Regrow + Burrow",
)
INK = (38, 51, 69)
PAPER = (255, 248, 235)


def font(size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", size)


def main() -> None:
    sheet = Image.new("RGB", (1320, 753), PAPER)
    draw = ImageDraw.Draw(sheet)
    draw.text((20, 12), "FRONTIER CAST · 40PX RUNTIME PORTRAITS", fill=INK, font=font(20))
    draw.text((20, 47), "Native 40px above; 5× nearest-neighbor view below. Mature roster growth.", fill=(91, 82, 105), font=font(18))
    draw.text((20, 402), "ASSEMBLED WITH TWO CATALOG TRAITS", fill=INK, font=font(20))
    for row, suffix in enumerate(("", "-traits")):
        top = 82 + row * 358
        for index, species in enumerate(SPECIES):
            source = SOURCE / f"{species}{suffix}.png"
            with Image.open(source) as opened:
                portrait = opened.convert("RGBA")
            if portrait.size != (40, 40):
                raise ValueError(f"{source} is {portrait.size}, expected 40×40")
            left = 10 + index * 220
            sheet.paste(INK, (left, top, left + 200, top + 75))
            sheet.paste(portrait, (left + 80, top + 17), portrait)
            label = species.title() if row == 0 else TRAITS[index]
            label_font = font(20 if row == 0 else 15)
            bounds = draw.textbbox((0, 0), label, font=label_font)
            draw.text((left + (200 - bounds[2]) / 2, top + 82), label, fill=INK, font=label_font)
            sheet.paste(INK, (left, top + 113, left + 200, top + 313))
            large = portrait.resize((200, 200), Image.Resampling.NEAREST)
            sheet.paste(large, (left, top + 113), large)
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(TARGET)
    print(f"Wrote {TARGET}")


if __name__ == "__main__":
    main()
