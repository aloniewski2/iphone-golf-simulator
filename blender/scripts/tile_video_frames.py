"""Tile video frames of the near player into one sheet for multi-person 3D body fitting:
each frame is cropped square around the player (bounding box from the MediaPipe track,
padded for the racket) and placed in a grid. One upload and one fitting job then covers a
whole clip; the fitted people are matched back to their cells by where they sit.

Run:  python3 tile_video_frames.py FRAMES_DIR POSES.json OUT.jpg FRAME FRAME ... [--cols 4] [--cell 512]
Writes OUT.json with the frame in each cell.
"""
import json, sys
from PIL import Image

args = sys.argv[1:]
cols = int(args[args.index("--cols") + 1]) if "--cols" in args else 4
cell = int(args[args.index("--cell") + 1]) if "--cell" in args else 512
pad = int(args[args.index("--pad") + 1]) if "--pad" in args else 0
args = [a for i, a in enumerate(args) if not a.startswith("--") and (i == 0 or not args[i - 1].startswith("--"))]
frames_dir, poses, out = args[0], json.load(open(args[1])), args[2]
frames = [int(f) for f in args[3:]]
rows = (len(frames) + cols - 1) // cols
sheet = Image.new("RGB", (cols * cell + 2 * pad, rows * cell + 2 * pad), (128, 128, 128))
layout = []
for k, f in enumerate(frames):
    im = Image.open(f"{frames_dir}/f{f:03d}.jpg").convert("RGB"); W, H = im.size
    pts = poses[str(f)]["image"]
    xs = [p[0] * W for p in pts]; ys = [p[1] * H for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    size = max(max(xs) - min(xs), max(ys) - min(ys)) * 1.7
    crop = im.crop((int(cx - size / 2), int(cy - size / 2), int(cx + size / 2), int(cy + size / 2))).resize((cell, cell))
    sheet.paste(crop, (pad + (k % cols) * cell, pad + (k // cols) * cell))
    layout.append({"frame": f, "col": k % cols, "row": k // cols})
sheet.save(out, quality=92)
json.dump({"cols": cols, "rows": rows, "cell": cell, "pad": pad, "cells": layout}, open(out.rsplit(".", 1)[0] + ".json", "w"))
print("TILED", len(frames), "frames", sheet.size)
