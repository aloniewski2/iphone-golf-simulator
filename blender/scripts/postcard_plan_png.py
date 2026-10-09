#!/usr/bin/env python3
"""Plan-view PNG of a postcard design, coloured by the scoring lie (needs Pillow; system python3 has it).

    python3 blender/scripts/postcard_plan_png.py hole08_design [out.png] [--scale 3]

Default output: blender/previews/hole_NN_plan.png. Tee at the top, play runs DOWN the image (course +D),
X to the right, so it reads like the ASCII map. Not a flyover, not a render: a diagnostic of what the code scores.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import postcard_check as pc  # noqa: E402

COL = {pc.WATER: (24, 70, 160), pc.OOB: (200, 40, 40), pc.ROUGH: (58, 148, 38), pc.FAIRWAY: (118, 208, 56),
       pc.GREEN: (156, 228, 72), pc.BUNKER: (240, 218, 160), pc.TEE: (230, 230, 230)}


def main(argv):
    from PIL import Image, ImageDraw
    scale = 3
    if "--scale" in argv:
        scale = int(argv[argv.index("--scale") + 1])
        del argv[argv.index("--scale"):argv.index("--scale") + 2]
    args = [a for a in argv if not a.startswith("--")]
    if not args:
        print(__doc__)
        return 2
    modname = args[0]
    here = os.path.dirname(os.path.abspath(__file__))
    h, _, g = pc.run(modname, fast=True)
    out = args[1] if len(args) > 1 else os.path.join(here, "..", "previews", f"hole_{h.number:02d}_plan.png")
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    cell = 1.0
    xs = [p[0] for p in h.shore]
    ds = [p[1] for p in h.shore]
    x0, d0 = min(xs) - 25, min(ds) - 25
    W, H = int((max(xs) + 25 - x0) * scale), int((max(ds) + 25 - d0) * scale)
    img = Image.new("RGB", (W, H), COL[pc.WATER])
    px = img.load()
    for j in range(H):
        for i in range(W):
            p = (x0 + (i + .5) / scale, d0 + (j + .5) / scale)
            px[i, j] = COL[h.lie_at(p)]
    dr = ImageDraw.Draw(img)
    to = lambda p: ((p[0] - x0) * scale, (p[1] - d0) * scale)
    dr.line([to(p) for p in h.shore] + [to(h.shore[0])], fill=(255, 255, 255), width=1)
    for k in h.hazards:
        pts = [to(p) for p in pc.ellipse_pts(k, 72)]
        dr.line(pts + [pts[0]], fill=(255, 140, 0) if k[0] == "water" else (120, 80, 0), width=1)
    dr.line([to(p) for p in h.center], fill=(255, 255, 0), width=1)
    for p in h.center:
        q = to(p)
        dr.ellipse([q[0] - 3, q[1] - 3, q[0] + 3, q[1] + 3], outline=(255, 255, 0))
    dr.ellipse([to(h.pin)[0] - 2, to(h.pin)[1] - 2, to(h.pin)[0] + 2, to(h.pin)[1] + 2], fill=(255, 0, 0))
    for yd in range(0, int(max(ds) + 25), 50):
        y = to((0, yd))[1]
        dr.line([(0, y), (12, y)], fill=(255, 255, 255))
        dr.text((14, y - 5), f"{yd}", fill=(255, 255, 255))
    img.save(out)
    print("wrote", os.path.abspath(out), img.size)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
