#!/usr/bin/env python3
"""Audit real Frontier raider builders and render a cast contact sheet."""

import json
import math
from pathlib import Path
import subprocess
import tempfile

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "client/Assets/Frontier/Art"
TOOLS = ROOT / "implementation/tools"
REVIEW = ROOT / "implementation/reviews/phase10-raider-cast.png"
JSON = ROOT / "implementation/results/frontier/raiders.json"
MONO = Path("/Library/Frameworks/Mono.framework/Versions/Current")
SOURCES = ["FrontierMesh", "FrontierRigDefinition", "FrontierPose", "FrontierFace", "FrontierParts",
           "FrontierRaiders", "FrontierVetch", "FrontierEmber", "FrontierPale", "FrontierSkitter",
           "FrontierHollow", "FrontierLoam"]


def export():
    with tempfile.TemporaryDirectory(prefix="frontier-raiders-") as temp:
        exe = Path(temp) / "Export.exe"
        subprocess.run([str(MONO / "Commands/mono"), str(MONO / "lib/mono/4.5/csc.exe"),
                        "-nologo", "-langversion:preview", "-r:System.Numerics.dll",
                        "-r:System.Web.Extensions.dll", "-out:" + str(exe),
                        str(TOOLS / "FrontierUnityMath.cs"), str(TOOLS / "FrontierRaiderExport.cs"),
                        *[str(ART / (name + ".cs")) for name in SOURCES]], check=True)
        JSON.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run([str(MONO / "Commands/mono"), str(exe), str(JSON)], check=True)
    return json.loads(JSON.read_text())["raiders"]


def project(point, yaw):
    x, y, z = point
    turn = math.radians(yaw)
    flat_x = x * math.cos(turn) + z * math.sin(turn)
    depth = -x * math.sin(turn) + z * math.cos(turn)
    elevation = math.radians(18)
    return flat_x, y * math.cos(elevation) - depth * math.sin(elevation), y * math.sin(elevation) + depth * math.cos(elevation)


def render(mesh, size, yaw=-48, silhouette=False, shared_span=None, background=None):
    scale = 2
    w, h = size[0] * scale, size[1] * scale
    image = background.convert("RGB").resize((w, h), Image.Resampling.LANCZOS) if background is not None else Image.new("RGB", (w, h), (255, 248, 235))
    draw = ImageDraw.Draw(image)
    values = mesh["positions"]
    points = [project(values[i:i+3], yaw) for i in range(0, len(values), 3)]
    lo_x, hi_x = min(p[0] for p in points), max(p[0] for p in points)
    lo_y, hi_y = min(p[1] for p in points), max(p[1] for p in points)
    fit = min(w * .78 / max(.1, shared_span or hi_x - lo_x),
              h * .78 / max(.1, shared_span or hi_y - lo_y))
    cx, cy = (lo_x + hi_x) / 2, (lo_y + hi_y) / 2
    screen = [((p[0] - cx) * fit + w / 2, h / 2 - (p[1] - cy) * fit, p[2]) for p in points]
    colors = mesh["colors"]
    normals = mesh["normals"]
    faces = []
    idx = mesh["indices"]
    for k in range(0, len(idx), 3):
        a, b, c = idx[k:k+3]
        depth = (screen[a][2] + screen[b][2] + screen[c][2]) / 3
        faces.append((depth, a, b, c))
    faces.sort()
    light = (.45, .78, -.4)
    for _, a, b, c in faces:
        n = [(normals[3*a+i] + normals[3*b+i] + normals[3*c+i])/3 for i in range(3)]
        shade = .53 + .42 * max(0, sum(n[i] * light[i] for i in range(3)))
        color = (35, 39, 54) if silhouette else tuple(round(max(0, min(255,
            ((colors[3*a+i] + colors[3*b+i] + colors[3*c+i])/3) ** (1/2.2) * 255 * shade))) for i in range(3))
        draw.polygon([screen[a][:2], screen[b][:2], screen[c][:2]], fill=color)
    return image.resize(size, Image.Resampling.LANCZOS)


def main():
    rows = export()
    paper = (245, 235, 217)
    sheet = Image.new("RGB", (1620, 1320), paper)
    draw = ImageDraw.Draw(sheet)
    try:
        title = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 28)
        body = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 17)
    except OSError:
        title = ImageFont.load_default()
        body = title
    draw.text((28, 16), "THE FRONTIER RAIDERS", fill=(48, 45, 64), font=title)
    draw.text((28, 54), "Four body families · eight threats · one combined capstone", fill=(73, 52, 141), font=body)
    for i, row in enumerate(rows):
        col, r = i % 3, i // 3
        x, y = 26 + col * 535, 92 + r * 353
        draw.rounded_rectangle((x, y, x + 510, y + 332), radius=20, fill=(255, 248, 235), outline=(201, 154, 73), width=2)
        draw.text((x + 18, y + 12), row["id"].title(), fill=(48, 45, 64), font=title)
        draw.text((x + 18, y + 51), f'{row["triangles"]} triangles · {row["bones"]} bones', fill=(104, 97, 119), font=body)
        three = render(row["geometry"], (285, 225))
        side = render(row["geometry"], (185, 225), yaw=-90)
        sheet.paste(three, (x + 14, y + 87))
        sheet.paste(side, (x + 310, y + 87))
        draw.text((x + 22, y + 308), "THREE-QUARTER", fill=(104, 97, 119), font=body)
        draw.text((x + 355, y + 308), "SIDE", fill=(104, 97, 119), font=body)
    draw.text((28, 1170), "40px silhouettes", fill=(48, 45, 64), font=body)
    for i, row in enumerate(rows):
        x = 200 + i * 152
        mini = render(row["geometry"], (40, 40), silhouette=True)
        sheet.paste(mini, (x, 1163))
        draw.text((x - 9, 1205), row["id"].title(), fill=(48, 45, 64), font=body)
    draw.text((28, 1261), "Relative size", fill=(48, 45, 64), font=body)
    for i, row in enumerate(rows):
        x = 200 + i * 152
        mini = render(row["geometry"], (40, 40), silhouette=True, shared_span=3.6)
        sheet.paste(mini, (x, 1246))
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(REVIEW)
    print(REVIEW)


if __name__ == "__main__":
    main()
