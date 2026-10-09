"""Measure a Higgsfield/Tripo A-pose character for fit_opponents.py.

Slices the mesh horizontally (like check_character_anatomy.py) and reports, as fractions of
the mesh height, the features the rig fitting depends on:

  armpit   highest slice where each arm is a separate island from the torso
  hand     lowest point of each arm, and its centroid's lateral offset
  wrist    arm centre a little above the hand
  crotch   highest slice where the legs are still two islands (below any skirt: the
           thigh gap under it is used instead)
  neck     narrowest slice between the shoulders and the head

Run:  Blender --background --python measure_landmarks.py -- model.glb [...]
Prints one JSON line per model: MEASURE {...}
"""
import json
import sys

import bpy


def islands(points, cell):
    cells = {}
    for p in points:
        cells.setdefault((round(p[0] / cell), round(p[1] / cell)), []).append(p)
    seen = set(); out = []
    for c in cells:
        if c in seen: continue
        stack = [c]; seen.add(c); members = []
        while stack:
            x, y = stack.pop(); members += cells[(x, y)]
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    q = (x + dx, y + dy)
                    if q in cells and q not in seen: seen.add(q); stack.append(q)
        out.append(members)
    return out


def measure(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    verts = []
    for o in bpy.context.scene.objects:
        if o.type == "MESH": verts += [o.matrix_world @ v.co for v in o.data.vertices]
    lo = min(v.z for v in verts); hi = max(v.z for v in verts); H = hi - lo
    # Tripo: facing +X, the character's left on +Y. Lateral = y, forward = x.
    pts = [((v.y) / H, (v.x) / H, (v.z - lo) / H) for v in verts]
    cx = sum(p[0] for p in pts) / len(pts)
    pts = [(p[0] - cx, p[1], p[2]) for p in pts]

    def slice_at(z, band=.006):
        return [(p[0], p[1]) for p in pts if abs(p[2] - z) < band]

    result = {}
    for side, sign in (("L", 1), ("R", -1)):
        # Arm islands: separate from the torso, on this side.
        armpit = None; lowest = None
        for i in range(140, 50, -1):
            z = i / 200
            blobs = islands(slice_at(z), .012)
            # Arms hang out past the legs and head: an island centred beyond 0.16 of the
            # height to this side.
            arm = [b for b in blobs if sign * (sum(p[0] for p in b) / len(b)) > .16 and len(b) > 3]
            if arm:
                outer = max(arm, key=lambda b: sign * sum(p[0] for p in b) / len(b))
                if armpit is None: armpit = z
                lowest = (z, sign * sum(p[0] for p in outer) / len(outer))
            elif armpit is not None and lowest and z < lowest[0] - .02:
                break
        result[f"armpit_{side}"] = armpit
        result[f"hand_{side}"] = lowest[0] if lowest else None
        result[f"hand_y_{side}"] = lowest[1] if lowest else None
        # Wrist: the arm's centre 7% of the height above its lowest point.
        if lowest:
            z = lowest[0] + .07
            blobs = islands(slice_at(z), .012)
            arm = [b for b in blobs if sign * (sum(p[0] for p in b) / len(b)) > .16]
            if arm:
                outer = max(arm, key=lambda b: sign * sum(p[0] for p in b) / len(b))
                result[f"wrist_y_{side}"] = sign * sum(p[0] for p in outer) / len(outer)
    # From the waist down: the first slice where the legs part.
    crotch = None
    for i in range(110, 30, -1):
        z = i / 200
        legs = [b for b in islands([p for p in slice_at(z) if abs(p[0]) < .16], .01) if len(b) > 6]
        if len(legs) >= 2: crotch = z; break
    result["crotch"] = crotch
    widths = []
    for i in range(110, 170):
        z = i / 200
        s = [p for p in slice_at(z) if abs(p[0]) < .12]
        if s: widths.append((max(p[0] for p in s) - min(p[0] for p in s), z))
    result["neck"] = min(widths)[1] if widths else None
    return result


args = sys.argv[sys.argv.index("--") + 1:]
for path in args:
    print("MEASURE", json.dumps({"model": path.split("/")[-1], **{k: (round(v, 3) if isinstance(v, float) else v) for k, v in measure(path).items()}}), flush=True)
