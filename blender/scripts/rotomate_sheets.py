"""Rotomation, step 2: body-fit sheets. Each frame is cropped square around the player's
silhouette (padded for the racket and jump) into cells of a 3x2 sheet, with a margin, for
SAM 3D Body: bigger crops than the old 12-up tiles, so every frame gets a confident fit.
Writes sheet-XX.jpg + sheet-XX.json (cells: frame, col, row, crop box in video pixels).

Run:  python rotomate_sheets.py ROTO_DIR [--cols 3 --rows 2 --cell 700 --pad 150] [--joints-box]
"""
import json, sys
from pathlib import Path
import cv2, numpy as np

d = Path(sys.argv[1]); args = sys.argv[2:]
opt = lambda k, v: int(args[args.index(k) + 1]) if k in args else v
cols, rows, cell, pad = opt("--cols", 3), opt("--rows", 2), opt("--cell", 700), opt("--pad", 150)
T = json.loads((d / "targets.json").read_text())
frames = sorted(int(k) for k in T)
per = cols * rows
out = d / "sheets"; out.mkdir(exist_ok=True)
for s in range(0, len(frames), per):
    sheet = np.full((rows * cell + 2 * pad, cols * cell + 2 * pad, 3), 128, np.uint8)
    cells = []
    for k, f in enumerate(frames[s:s + per]):
        im = cv2.imread(str(d / f"frames/f{f:04d}.png")); H, W = im.shape[:2]
        x, y, w, h = T[str(f)]["bbox"]
        joints = T[str(f)].get("joints")
        if joints and "--joints-box" in args:
            # Real footage: the silhouette can miss limbs that match the background, and a
            # partial joint fit can miss the legs; the box holds both, padded for the head top
            # and the feet.
            pts = np.array([j[:2] for j in joints])
            jx0, jy0 = pts.min(0); jx1, jy1 = pts.max(0); jh = jy1 - jy0
            jx0, jx1, jy0, jy1 = jx0 - .12 * jh, jx1 + .12 * jh, jy0 - .18 * jh, jy1 + .06 * jh
            x0b, y0b, x1b, y1b = min(x, jx0), min(y, jy0), max(x + w, jx1), max(y + h, jy1)
            x, y, w, h = x0b, y0b, x1b - x0b, y1b - y0b
        size = max(w, h) * 1.35; cx, cy = x + w / 2, y + h / 2
        x0, y0 = int(round(cx - size / 2)), int(round(cy - size / 2)); x1, y1 = x0 + int(size), y0 + int(size)
        padded = cv2.copyMakeBorder(im, 400, 400, 400, 400, cv2.BORDER_REPLICATE)
        crop = cv2.resize(padded[y0 + 400:y1 + 400, x0 + 400:x1 + 400], (cell, cell), interpolation=cv2.INTER_CUBIC)
        c, r = k % cols, k // cols
        sheet[pad + r * cell:pad + (r + 1) * cell, pad + c * cell:pad + (c + 1) * cell] = crop
        cells.append(dict(frame=f, col=c, row=r, box=[x0, y0, x1, y1]))
    n = s // per
    cv2.imwrite(str(out / f"sheet-{n:02d}.jpg"), sheet, [cv2.IMWRITE_JPEG_QUALITY, 92])
    (out / f"sheet-{n:02d}.json").write_text(json.dumps(dict(cols=cols, rows=rows, cell=cell, pad=pad, cells=cells)))
print("SHEETS", len(range(0, len(frames), per)))
