"""Find the racket in each video frame by colour and measure where it points.

The game's rackets have a red-orange frame with a yellow rim: distinctive against the blue
court, sand and palms. Around the near player (bounding box from the MediaPipe pose track)
those pixels are collected, the ones connected to the racket hand kept, and the racket head
is their centroid. The hand-to-head vector is the racket's direction on screen; its length
against the longest seen (the racket side-on to the camera) gives how far it points toward
or away from the camera.

Run (the MediaPipe venv):  python track_video_racket.py FRAMES_DIR POSES.json FIRST LAST OUT [--hand R|L]
Writes OUT.json {frame: {"head": [x, y], "hand": [x, y], "dir2d": [dx, dy], "len": px}} and an overlay OUT.jpg.
"""
import json, sys
import cv2, numpy as np

frames_dir, poses_path, first, last, out = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), sys.argv[5]
hand_side = sys.argv[sys.argv.index("--hand") + 1] if "--hand" in sys.argv else None
poses = json.load(open(poses_path))
result, thumbs = {}, []
for i in range(first, last + 1):
    img = cv2.imread(f"{frames_dir}/f{i:03d}.jpg"); H, W = img.shape[:2]
    p = poses.get(str(i))
    if p is None: continue
    pts = np.array([[q[0] * W, q[1] * H] for q in p["image"]])
    x0, y0 = pts.min(0); x1, y1 = pts.max(0)
    size = max(x1 - x0, y1 - y0) * 1.1
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    bx0, by0 = int(max(0, cx - size)), int(max(0, cy - size)); bx1, by1 = int(min(W, cx + size)), int(min(H, cy + size))
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    # Red-orange frame and yellow rim: hue 0-28 or 170-180, saturated, bright.
    m1 = cv2.inRange(hsv, (0, 165, 110), (9, 255, 255)) | cv2.inRange(hsv, (19, 150, 150), (32, 255, 255)); m2 = cv2.inRange(hsv, (168, 150, 100), (180, 255, 255))
    mask = np.zeros_like(m1); mask[by0:by1, bx0:bx1] = (m1 | m2)[by0:by1, bx0:bx1]
    # Skin is orange too: remove the character's own skin by excluding pixels near the head
    # and the limbs (the pose), keeping thin bright frame strokes.
    body = np.zeros_like(mask)
    for a, b in ((0, 11), (0, 12), (11, 13), (13, 15), (12, 14), (14, 16), (11, 23), (12, 24), (23, 25), (25, 27), (24, 26), (26, 28)):
        cv2.line(body, tuple(pts[a].astype(int)), tuple(pts[b].astype(int)), 255, int(size * .09))
    head_r = int(np.linalg.norm(pts[7] - pts[8]) * .75 + size * .05)
    cv2.circle(body, tuple(((pts[7] + pts[8]) / 2).astype(int)), head_r, 255, -1)
    mask[body > 0] = 0
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    # Which hand holds it: the one nearest the largest red blob.
    n, lab, stats, cents = cv2.connectedComponentsWithStats(mask)
    if n <= 1: continue
    wr = {"L": pts[15], "R": pts[16]}
    blobs = sorted(range(1, n), key=lambda k: -stats[k, cv2.CC_STAT_AREA])[:4]
    best = None
    for k in blobs:
        c = cents[k]
        for side, hp in wr.items():
            if hand_side and side != hand_side: continue
            d = np.linalg.norm(c - hp)
            score = stats[k, cv2.CC_STAT_AREA] / (1 + d / size * 4)
            if best is None or score > best[0]: best = (score, k, side)
    _, k, side = best
    ys, xs = np.nonzero(lab == k)
    hand = wr[side]
    # Racket head centre: the centroid of the frame pixels furthest from the hand.
    d = np.hypot(xs - hand[0], ys - hand[1])
    far = d > np.percentile(d, 40)
    head = np.array([xs[far].mean(), ys[far].mean()])
    v = head - hand
    result[i] = {"head": head.tolist(), "hand": hand.tolist(), "dir2d": (v / (np.linalg.norm(v) + 1e-6)).tolist(), "len": float(np.linalg.norm(v)), "side": side, "size": float(size)}
    vis = img.copy(); vis[lab == k] = (255, 0, 255)
    cv2.arrowedLine(vis, tuple(hand.astype(int)), tuple(head.astype(int)), (0, 255, 0), 5)
    cv2.putText(vis, f"{i}{side}", (20, 60), cv2.FONT_HERSHEY_SIMPLEX, 2, (0, 255, 255), 4)
    thumbs.append(cv2.resize(vis[by0:by1, bx0:bx1], (300, 300)))
json.dump(result, open(out + ".json", "w"))
rows = [np.hstack(thumbs[j:j + 6] + [np.zeros_like(thumbs[0])] * (6 - len(thumbs[j:j + 6]))) for j in range(0, len(thumbs), 6)]
cv2.imwrite(out + ".jpg", np.vstack(rows))
print("RACKET", len(result), "frames; hands", {s: sum(1 for r in result.values() if r["side"] == s) for s in "LR"})
