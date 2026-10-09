"""Rotomation, step 1: 2D targets from a locked-camera reference video (the Higgsfield
side-view serve, SportsLibrary/ArtDirection/Tennis/Generated/video/serve-side/).

  * Background plate: the per-pixel median over the whole clip (the camera never moves and
    the player never stays put), so |frame - plate| gives a clean silhouette every frame.
  * Joints: MediaPipe Pose on a crop that follows the silhouette, upscaled (the player is
    only ~200 px tall), in full-frame pixels with visibility.
  * Racket: the saturated orange-red frame pixels inside the silhouette's neighbourhood that
    are not skin, split from the body by colour; its far end from the racket wrist is the
    racket head.

Writes targets.json {frame: {"joints": [[x, y, vis] * 33], "bbox": [...], "racket_head": [x, y] | null}}
and silhouettes as PNG masks.

Run (OpenCV + MediaPipe env):  python rotomate_track.py VIDEO OUT_DIR
"""
import json, subprocess, sys
from pathlib import Path

import cv2, numpy as np, mediapipe as mp

video, out = Path(sys.argv[1]), Path(sys.argv[2])
frames_dir = out / "frames"; masks_dir = out / "masks"
frames_dir.mkdir(parents=True, exist_ok=True); masks_dir.mkdir(exist_ok=True)
if not any(frames_dir.iterdir()):
    ff = sys.argv[3] if len(sys.argv) > 3 else "ffmpeg"
    subprocess.run([ff, "-v", "error", "-i", str(video), "-vsync", "0", str(frames_dir / "f%04d.png")], check=True)
paths = sorted(frames_dir.glob("f*.png"))
stack = np.stack([cv2.imread(str(p)) for p in paths[::3]])
plate = np.median(stack, axis=0).astype(np.uint8); cv2.imwrite(str(out / "plate.png"), plate)
del stack
pose = mp.solutions.pose.Pose(static_image_mode=True, model_complexity=2, min_detection_confidence=.2)
targets = {}
for i, p in enumerate(paths, 1):
    im = cv2.imread(str(p)); H, W = im.shape[:2]
    diff = cv2.absdiff(im, plate).max(-1)
    mask = (diff > 28).astype(np.uint8) * 255
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, lab, stats, _ = cv2.connectedComponentsWithStats(mask)
    if n < 2: continue
    # The player: the biggest blob (the ball is small, shadows are faint).
    k = 1 + np.argmax(stats[1:, cv2.CC_STAT_AREA])
    sil = np.where(lab == k, 255, 0).astype(np.uint8)
    sil = cv2.morphologyEx(sil, cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8))
    cv2.imwrite(str(masks_dir / f"m{i:04d}.png"), sil)
    x, y, w, h = cv2.boundingRect(sil)
    cx, cy, size = x + w / 2, y + h / 2, max(w, h) * 1.5
    x0, y0 = int(max(0, cx - size / 2)), int(max(0, cy - size / 2)); x1, y1 = int(min(W, cx + size / 2)), int(min(H, cy + size / 2))
    crop = cv2.resize(im[y0:y1, x0:x1], None, fx=900 / (y1 - y0), fy=900 / (y1 - y0), interpolation=cv2.INTER_CUBIC)
    r = pose.process(cv2.cvtColor(crop, cv2.COLOR_BGR2RGB))
    joints = None
    if r.pose_landmarks:
        joints = [[x0 + l.x * (x1 - x0), y0 + l.y * (y1 - y0), l.visibility] for l in r.pose_landmarks.landmark]
    # Racket: saturated orange-red, not skin-toned; near the body.
    hsv = cv2.cvtColor(im, cv2.COLOR_BGR2HSV)
    red = (((hsv[..., 0] < 9) | (hsv[..., 0] > 170)) & (hsv[..., 1] > 150) & (hsv[..., 2] > 90)).astype(np.uint8)
    near = cv2.dilate(sil, np.ones((41, 41), np.uint8)) > 0
    red = (red > 0) & near & (diff > 20)
    head = None
    if joints and red.sum() > 25:
        ys, xs = np.nonzero(red)
        wr = np.array(joints[16][:2])          # MediaPipe right wrist
        d = np.hypot(xs - wr[0], ys - wr[1])
        far = d > np.percentile(d, 70)
        if far.sum() > 5: head = [float(xs[far].mean()), float(ys[far].mean())]
    targets[i] = dict(joints=joints, bbox=[x, y, w, h], racket_head=head)
(out / "targets.json").write_text(json.dumps(targets))
got = sum(1 for t in targets.values() if t["joints"])
print(f"TRACK {len(paths)} frames, joints on {got}, racket on {sum(1 for t in targets.values() if t['racket_head'])}")
