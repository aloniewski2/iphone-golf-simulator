"""Assemble render_clip_sheets.py frames into one contact sheet per clip (a row of frames),
and an overview of every clip.

Run:  python3 blender/scripts/make_clip_sheets.py FRAMES_DIR [OUT_DIR]
"""
import sys
from collections import defaultdict
from pathlib import Path

from PIL import Image, ImageDraw

src = Path(sys.argv[1]); out = Path(sys.argv[2]) if len(sys.argv) > 2 else src / "sheets"
out.mkdir(parents=True, exist_ok=True)
rows = defaultdict(list)
for p in sorted(src.glob("*.png")):
    clip, view, _ = p.stem.rsplit("-", 2)
    rows[(clip, view)].append(p)
sheets = []
for (clip, view), frames in sorted(rows.items()):
    ims = [Image.open(p).convert("RGB") for p in frames]
    w, h = ims[0].size
    sheet = Image.new("RGB", (w * len(ims), h + 28), (20, 24, 40))
    d = ImageDraw.Draw(sheet); d.text((8, 6), f"{clip} ({view})", fill=(255, 230, 120))
    for i, im in enumerate(ims): sheet.paste(im, (i * w, 28))
    sheet.save(out / f"{clip}-{view}.jpg", quality=86)
    sheets.append(sheet)
print(f"{len(sheets)} sheets -> {out}")
