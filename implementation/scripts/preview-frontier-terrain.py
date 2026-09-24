#!/usr/bin/env python3
"""Compile the real shared terrain detail and render an offline geometry preview."""

import json
import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile

from PIL import Image, ImageDraw, ImageFont

RAIDER_PREVIEW = Path(__file__).with_name("preview-frontier-raiders.py")
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("frontier_raider_preview", RAIDER_PREVIEW)
raider_preview = importlib.util.module_from_spec(spec)
spec.loader.exec_module(raider_preview)
render = raider_preview.render

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "client/Assets/Frontier/Art"
TOOLS = ROOT / "implementation/tools"
JSON = ROOT / "implementation/results/frontier/terrain.json"
PAINTED_JSON = ROOT / "implementation/results/frontier/terrain-painted.json"
REVIEW = ROOT / "implementation/reviews/phase10-terrain-detail.png"
BACKDROP_REVIEW = ROOT / "implementation/reviews/phase10-battle-backdrop-concept.png"
BACKDROP = ROOT / "client/Assets/View/Resources/Art/battle-backdrop.png"
MONO = Path("/Library/Frameworks/Mono.framework/Versions/Current")


def main():
    with tempfile.TemporaryDirectory(prefix="frontier-terrain-") as temp:
        exe = Path(temp) / "Export.exe"
        subprocess.run([str(MONO / "Commands/mono"), str(MONO / "lib/mono/4.5/csc.exe"),
                        "-nologo", "-langversion:preview", "-r:System.Numerics.dll",
                        "-r:System.Web.Extensions.dll", "-out:" + str(exe),
                        str(TOOLS / "FrontierUnityMath.cs"), str(TOOLS / "FrontierTerrainExport.cs"),
                        str(ART / "FrontierMesh.cs"), str(ART / "FrontierTerrain.cs")], check=True)
        JSON.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run([str(MONO / "Commands/mono"), str(exe), str(JSON)], check=True)
        subprocess.run([str(MONO / "Commands/mono"), str(exe), str(PAINTED_JSON), "painted"], check=True)
    mesh = json.loads(JSON.read_text())
    sheet = Image.new("RGB", (1600, 720), (245, 235, 217))
    draw = ImageDraw.Draw(sheet)
    try:
        title = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 28)
        body = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 17)
    except OSError:
        title = body = ImageFont.load_default()
    draw.text((32, 20), "FRONTIER TERRAIN DETAIL", fill=(48, 45, 64), font=title)
    draw.text((32, 59), "Cliff shelves · brush · pocket stones · Ark threshold  |  " + str(mesh["triangles"]) + " triangles", fill=(104, 97, 119), font=body)
    sheet.paste(render(mesh["geometry"], (1540, 580), yaw=-62), (30, 106))
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(REVIEW)
    print(REVIEW)
    if BACKDROP.exists():
        painted = json.loads(PAINTED_JSON.read_text())
        concept = Image.new("RGB", (1600, 720), (245, 235, 217))
        caption = ImageDraw.Draw(concept)
        caption.text((32, 20), "BATTLEFIELD BACKDROP", fill=(48, 45, 64), font=title)
        caption.text((32, 59), "Offline composition concept · painted ground beneath procedural lane · Unity lighting and camera still to review", fill=(104, 97, 119), font=body)
        with Image.open(BACKDROP) as ground:
            concept.paste(render(painted["geometry"], (1540, 580), yaw=-62, background=ground), (30, 106))
        concept.save(BACKDROP_REVIEW)
        print(BACKDROP_REVIEW)


if __name__ == "__main__":
    main()
