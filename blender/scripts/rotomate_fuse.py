"""Rotomation, step 3b: place every SAM person on its video frame and write per-frame poses.

Input: ROTO/sam-dump.json (rotomate_sam.py), ROTO/masks (rotomate_track.py).

  * Which frame: a single-frame crop holds one frame. On a 3x2 sheet each person is matched to
    its cell by where its pelvis projects -- the cells sit on a regular grid in SAM's image
    plane, but SAM picks its own focal length and principal point per image, so the grid's
    pitch AND offset are fitted (a fixed centred offset mismatched whole rows).
  * Where on screen: the person's body mesh, projected, is fitted to the frame's silhouette
    (scale + offset, maximising overlap). That gives every joint in video pixels -- used to
    check the fit against the video and to measure the root.
  * Root: the camera is static, so the silhouette measures the body directly: the lowest
    silhouette row against the court line gives the jump height, the pelvis pixel the travel.

Output: ROTO/poses.json -- {frame: {"world": 33 x [x, y, z, 1] (MediaPipe axes, SAM metric
3D), "px": {mhr: [x, y]}, "iou", "src", "foot_y", "pelvis_px"}}, read by
retarget_video_pose.py (--source world) and rotomate_overlay.py.

Run:  mpenv/bin/python rotomate_fuse.py ROTO_DIR
"""
import json
import math
import sys
from pathlib import Path

import cv2
import numpy as np
from scipy.optimize import minimize

MHR_TO_MP = {0: 0, 7: 3, 8: 4, 11: 5, 12: 6, 13: 7, 14: 8, 15: 62, 16: 41, 17: 61, 18: 40, 19: 49, 20: 28,
             23: 9, 24: 10, 25: 11, 26: 12, 27: 13, 28: 14, 29: 17, 30: 20, 31: 15, 32: 18}
DOWN = 4                                           # silhouette fit at quarter resolution

d = Path(sys.argv[1])
dump = json.loads((d / "sam-dump.json").read_text())


def uv(p):
    p = np.asarray(p, float)
    return np.stack([p[..., 0] / p[..., 1], -p[..., 2] / p[..., 1]], -1)


def assign_cells(people, layout):
    """[(person, cell)] for a sheet: fit the grid pitch and offset to the pelvis positions."""
    cols, rows = layout["cols"], layout["rows"]
    cells = {(c["col"], c["row"]): c for c in layout["cells"]}
    if cols * rows == 1:
        return [(max(people, key=lambda p: p["height"]), layout["cells"][0])]
    pts = np.array([p["pel_uv"] for p in people])
    best = None
    for pitch in np.arange(.10, .40, .0005):
        g = pts / pitch
        # Offset per axis: circular mean of the fractional parts (the grid phase).
        off = [-math.atan2(np.sin(2 * np.pi * g[:, a]).mean(), np.cos(2 * np.pi * g[:, a]).mean()) / (2 * np.pi) for a in (0, 1)]
        idx = np.round(g + off).astype(int)
        resid = float(((g + off - idx) ** 2).sum())
        span = idx.max(0) - idx.min(0)
        if span[0] > cols - 1 or span[1] > rows - 1: continue
        if len({tuple(i) for i in idx}) < len(idx): continue
        # Where the occupied block sits in the grid: prefer the principal point nearest centre.
        for sx in range(0, cols - span[0]):
            for sy in range(0, rows - span[1]):
                cr = idx - idx.min(0) + [sx, sy]
                # The grid coordinate of the image centre (u = v = 0) under this placement.
                centre = np.array(off) - idx.min(0) + [sx, sy] - (np.array([cols, rows]) - 1) / 2
                score = resid + .05 * float(np.abs(centre).sum())
                if best is None or score < best[0]: best = (score, pitch, cr)
    if best is None: return []
    return [(p, cells.get(tuple(c))) for p, c in zip(people, best[2]) if tuple(c) in cells]


def silhouette_fit(person, frame):
    """Similarity map (scale, tx, ty) from SAM image-plane coords to video pixels that lays the
    projected mesh over the frame's silhouette; returns (map, IoU)."""
    mask = cv2.imread(str(d / f"masks/m{frame:04d}.png"), 0)
    small = cv2.resize(mask, (mask.shape[1] // DOWN, mask.shape[0] // DOWN), interpolation=cv2.INTER_AREA) > 96
    mesh = uv(person["mesh"])
    ys, xs = np.nonzero(small)
    if len(xs) == 0: return None, 0.0
    # Start: match the mesh's height and centre to the silhouette's (without the racket head
    # the silhouette is only a little taller than the body).
    mh = np.ptp(mesh[:, 1]); s0 = (ys.max() - ys.min()) / mh * .92
    tx0 = xs.mean() - s0 * mesh[:, 0].mean(); ty0 = ys.max() - s0 * mesh[:, 1].max()
    H, W = small.shape

    def iou(x):
        s, tx, ty = math.exp(x[0]) * s0, x[1], x[2]
        px = np.round(mesh * s + [tx, ty]).astype(int)
        ok = (px[:, 0] >= 0) & (px[:, 0] < W) & (px[:, 1] >= 0) & (px[:, 1] < H)
        body = np.zeros_like(small, np.uint8); body[px[ok, 1], px[ok, 0]] = 1
        body = cv2.dilate(body, np.ones((3, 3), np.uint8)) > 0
        inter = (body & small).sum(); union = (body | small).sum()
        return -inter / max(1, union)
    res = minimize(iou, [0, tx0, ty0], method="Nelder-Mead", options=dict(xatol=.2, fatol=1e-4, maxiter=400,
                                                                             initial_simplex=[[0, tx0, ty0], [.15, tx0, ty0], [0, tx0 + 8, ty0], [0, tx0, ty0 + 8]]))
    s, tx, ty = math.exp(res.x[0]) * s0 * DOWN, res.x[1] * DOWN, res.x[2] * DOWN
    return (s, tx, ty), -res.fun


out = {}
for image in dump:
    people = []
    for p in image["people"]:
        k = {int(i): v for i, v in p["k"].items()}
        if not all(i in k for i in (0, 9, 10, 13, 14)) or "mesh" not in p: continue
        pel = (np.array(k[9]) + np.array(k[10])) / 2
        people.append({"k": k, "mesh": p["mesh"], "pel_uv": uv(pel),
                       "height": float(np.linalg.norm(np.array(k[0]) - (np.array(k[13]) + np.array(k[14])) / 2))})
    if not people: continue
    tallest = max(p["height"] for p in people)
    people = [p for p in people if p["height"] > .5 * tallest]
    single = image["layout"]["cols"] * image["layout"]["rows"] == 1
    for person, cell in assign_cells(people, image["layout"]):
        if cell is None: continue
        f = cell["frame"]
        fit, score = silhouette_fit(person, f)
        if fit is None: continue
        prev = out.get(str(f))
        # A single-frame crop beats a sheet cell; otherwise the better silhouette overlap wins.
        rank = (single, score)
        if prev is not None and (prev["_rank"][0], prev["_rank"][1]) >= rank: continue
        s, tx, ty = fit
        k = person["k"]
        px = {str(i): [float(a) for a in uv(k[i]) * s + [tx, ty]] for i in MHR_TO_MP.values() if i in k}
        world = [[0, 0, 0, 0]] * 33
        for mp, mhr in MHR_TO_MP.items():
            if mhr in k: x, y, z = k[mhr]; world[mp] = [x, -z, y, 1.0]
        out[str(f)] = {"world": world, "px": px, "iou": round(score, 3), "src": image["name"], "_rank": rank}

# Root measurements from the silhouettes (static camera).
for f, e in out.items():
    mask = cv2.imread(str(d / f"masks/m{int(f):04d}.png"), 0) > 96
    ys, xs = np.nonzero(mask)
    e["foot_y"] = float(np.percentile(ys, 99.7))
    pel = (np.array(e["px"]["9"]) + np.array(e["px"]["10"])) / 2
    e["pelvis_px"] = [float(pel[0]), float(pel[1])]
    e.pop("_rank")
(d / "poses.json").write_text(json.dumps(out))
frames = sorted(int(f) for f in out)
missing = [f for f in range(frames[0], frames[-1] + 1) if str(f) not in out]
low = sorted((out[str(f)]["iou"], f) for f in frames)[:8]
print(f"FUSE {len(out)} frames {frames[0]}-{frames[-1]}; missing {missing}")
print("FUSE lowest IoU", low)
