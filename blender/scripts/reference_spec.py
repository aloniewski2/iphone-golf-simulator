"""Lock the concept video's character as a measurable spec (plan phase A).

Silhouettes: GrabCut, seeded from the MediaPipe pose box of the near player, on the key
frames (front f226, back f87, side f1); saved as PNG masks cropped to the body. Ratios are
measured off those masks (height, head, shoulders, legs, shoes) and written with the flat
colours (median sRGB inside each region) and the motion timing table to character-spec.json.
Every later comparison (Blender silhouettes, Unity captures) is scored against these files.

Run (any Python with OpenCV):  python reference_spec.py
"""
import json
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
VIDEO = ROOT / "SportsLibrary/ArtDirection/Tennis/Generated/video"
OUT = ROOT / "SportsLibrary/ArtDirection/Tennis/Reference"
TRACKS = {}
for name in ("mp-all.json", "mp-serve.json"):
    for k, v in json.loads((VIDEO / "tracks" / name).read_text()).items(): TRACKS.setdefault(int(k), v)

# The video has no clean standing side view, so the spec holds front and back.
VIEWS = {"front": 226, "back": 87}


TOP_PAD = {226: .12, 87: .55}      # the front frame has the WINNER banner just above the head


def mask_for(frame):
    im = cv2.imread(str(VIDEO / f"frames24/f{frame:03d}.jpg"))
    h, w = im.shape[:2]
    pts = np.array([[p[0] * w, p[1] * h] for p in TRACKS[frame]["image"]])
    lo, hi = pts.min(0), pts.max(0)
    size = hi - lo
    # The pose box misses the top of the big head and the shoes: pad generously.
    x0, y0 = int(max(0, lo[0] - size[0] * .45)), int(max(0, lo[1] - size[1] * TOP_PAD.get(frame, .55)))
    x1, y1 = int(min(w, hi[0] + size[0] * .45)), int(min(h, hi[1] + size[1] * .12))
    # Seed: outside the box is background; inside, the court's and sky's blues are
    # background too (the character has no blue), the rest is probably the player.
    hsv = cv2.cvtColor(im, cv2.COLOR_BGR2HSV)
    blue = (hsv[..., 0] > 88) & (hsv[..., 0] < 128) & (hsv[..., 1] > 45)
    m = np.full((h, w), cv2.GC_BGD, np.uint8)
    m[y0:y1, x0:x1] = cv2.GC_PR_FGD
    m[blue] = cv2.GC_BGD
    bg, fg = np.zeros((1, 65), np.float64), np.zeros((1, 65), np.float64)
    cv2.grabCut(im, m, None, bg, fg, 6, cv2.GC_INIT_WITH_MASK)
    sil = np.where((m == cv2.GC_FGD) | (m == cv2.GC_PR_FGD), 255, 0).astype(np.uint8)
    # Cast shadows (dark, low-saturation) and thin court lines are not the player.
    shadow = (hsv[..., 2] < 95) & (hsv[..., 1] < 90)
    sil[shadow] = 0
    sil = cv2.morphologyEx(sil, cv2.MORPH_OPEN, np.ones((9, 9), np.uint8))
    # ...but the dark grey shorts read as shadow too: close the holes that leaves.
    sil = cv2.morphologyEx(sil, cv2.MORPH_CLOSE, np.ones((25, 25), np.uint8))
    # Keep the biggest blob (the player), drop the racket's thin strings where possible.
    n, lab, stats, _ = cv2.connectedComponentsWithStats(sil)
    if n > 1: sil = np.where(lab == 1 + np.argmax(stats[1:, cv2.CC_STAT_AREA]), 255, 0).astype(np.uint8)
    return im, sil


def ratios(sil):
    ys, xs = np.nonzero(sil)
    top, bot = ys.min(), ys.max()
    height = bot - top
    rows = [np.nonzero(sil[y])[0] for y in range(top, bot + 1)]
    widths = np.array([r.max() - r.min() if len(r) else 0 for r in rows])
    return dict(height_px=int(height), widest_head=float(widths[:int(height * .3)].max() / height),
                width_profile=[round(float(v) / height, 3) for v in widths[::max(1, height // 40)]])


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    spec = json.loads((OUT / "character-spec.json").read_text()) if (OUT / "character-spec.json").exists() else {}
    spec.setdefault("silhouettes", {})
    for view, frame in VIEWS.items():
        im, sil = mask_for(frame)
        ys, xs = np.nonzero(sil)
        crop = sil[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        cv2.imwrite(str(OUT / f"silhouette-{view}.png"), crop)
        over = im.copy(); over[sil > 0] = (over[sil > 0] * .5 + np.array([255, 0, 255]) * .5).astype(np.uint8)
        cv2.imwrite(str(OUT / f"silhouette-{view}-check.jpg"), over[max(0, ys.min() - 20):ys.max() + 20, max(0, xs.min() - 20):xs.max() + 20])
        spec["silhouettes"][view] = dict(frame=frame, **ratios(sil))
    spec["silhouettes"]["_note"] = ("GrabCut masks, approximate (about +-3% of height): the back view fills the gap "
                                    "between the legs where the tan decking matches skin")
    (OUT / "character-spec.json").write_text(json.dumps(spec, indent=1))
    print("SPEC", {v: spec["silhouettes"][v]["height_px"] for v in VIEWS})


if __name__ == "__main__":
    main()
