"""Turn a SAM 3D Body result for a tile sheet (tile_video_frames.py) into per-frame joints in
the MediaPipe layout retarget_video_pose.py reads ("world" entries, 33 joints).

SAM fits every person in the sheet; each is matched to its cell by where its pelvis projects
in the image, and anyone too small (the far player in the background of a crop) is dropped.
Keypoints are the MHR70 set: 0 nose, 3/4 ears, 5/6 shoulders, 7/8 elbows, 9/10 hips,
11/12 knees, 13/14 ankles, 15/18 big toes, 17/20 heels, 41/62 right/left wrist, and per
hand four joints per finger (the fourth of each is the knuckle: right index 28, right pinky
40, left index 49, left pinky 61).

Run (Blender, to read the GLB):  Blender --background --python sam_tiles_to_poses.py -- RESULT.glb TILES.json OUT.json
"""
import json
import sys

import bpy
from mathutils import Vector

MHR_TO_MP = {0: 0, 7: 3, 8: 4, 11: 5, 12: 6, 13: 7, 14: 8, 15: 62, 16: 41, 17: 61, 18: 40, 19: 49, 20: 28,
             23: 9, 24: 10, 25: 11, 26: 12, 27: 13, 28: 14, 29: 17, 30: 20, 31: 15, 32: 18}


def main():
    glb, tiles_path, out = sys.argv[sys.argv.index("--") + 1:][:3]
    tiles = json.load(open(tiles_path))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    people = {}
    for o in bpy.data.objects:
        if not o.name.startswith("keypoint_p"): continue
        pp, kk = o.name.split("_")[1:3]
        vs = [o.matrix_world @ v.co for v in o.data.vertices]
        people.setdefault(int(pp[1:]), {})[int(kk[1:])] = sum(vs, Vector()) / len(vs)
    # Imported camera frame: x right, y away from the camera, z up.
    placed = {}
    for pid, k in people.items():
        pelvis = (k[9] + k[10]) / 2
        height = (k[0] - (k[13] + k[14]) / 2).length
        u, v = pelvis.x / pelvis.y, -pelvis.z / pelvis.y      # image-plane position
        placed[pid] = (u, v, height)
    if not placed: raise SystemExit("no people")
    tallest = max(h for _, _, h in placed.values())
    big = {pid: uv for pid, uv in placed.items() if uv[2] > .5 * tallest}
    cols, rows, size = tiles["cols"], tiles["rows"], tiles["cell"]
    pad = tiles.get("pad", 0)
    width, height = cols * size + 2 * pad, rows * size + 2 * pad
    # Cells sit on a fixed grid about the sheet's centre, so one cell width in image-plane
    # units (the pitch) places everyone -- even when a whole row goes undetected, which
    # stretching the detected spread over the grid would misassign.
    ox, oy = (width / 2 - pad) / size - .5, (height / 2 - pad) / size - .5

    def misfit(pitch):
        err, seen = 0.0, set()
        for u, v, _ in big.values():
            fc, fr = u / pitch + ox, v / pitch + oy
            c, r = round(fc), round(fr)
            err += (fc - c) ** 2 + (fr - r) ** 2
            if not (0 <= c < cols and 0 <= r < rows) or (c, r) in seen: err += 1
            seen.add((c, r))
        return err
    pitch = min((.05 + i * .001 for i in range(600)), key=misfit)
    result = {}
    for pid, (u, v, h) in big.items():
        col, row = round(u / pitch + ox), round(v / pitch + oy)
        cell = next((c for c in tiles["cells"] if c["col"] == col and c["row"] == row), None)
        if not cell: continue
        k = people[pid]
        world = [[0, 0, 0, 0]] * 33
        for mp, mhr in MHR_TO_MP.items():
            p = k[mhr]
            world[mp] = [p.x, -p.z, p.y, 1.0]           # to MediaPipe's axes: x right, y down, z away
        prev = result.get(str(cell["frame"]))
        if prev is None or h > prev[1]: result[str(cell["frame"])] = ({"world": world}, h)
    json.dump({f: v[0] for f, v in result.items()}, open(out, "w"))
    print("SAMPOSES", sorted(int(f) for f in result), "from", len(people), "people")


main()
