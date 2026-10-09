"""Rotomation, step 3a: dump SAM 3D Body results (sheets and single frames) for fitting.

Reads every SAM GLB of the rotomation -- ROTO/sam/sheet-XX.glb (3x2 sheets from
rotomate_sheets.py) and ROTO/sam1/fXXXX.glb (single-frame crops, used where a sheet failed) --
and writes ROTO/sam-dump.json: per image, its layout (the sheet/single JSON) and every person
SAM found, with the 70 MHR keypoints in SAM's camera frame (x right, y away, z up) and a
subsample of the body mesh's vertices, for the silhouette fit in rotomate_fuse.py.

Run:  Blender --background --python rotomate_sam.py -- ROTO_DIR
"""
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def people_of(glb):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(glb))
    people = {}
    for o in bpy.data.objects:
        if o.name.startswith("keypoint_p"):
            pp, kk = o.name.split("_")[1:3]
            vs = [o.matrix_world @ v.co for v in o.data.vertices]
            c = sum(vs, Vector()) / len(vs)
            people.setdefault(int(pp[1:]), {"k": {}})["k"][int(kk[1:])] = [c.x, c.y, c.z]
    for o in bpy.data.objects:
        if o.name.startswith("person_") and o.type == "MESH":
            pid = int(o.name.split("_")[1])
            mw = o.matrix_world
            people.setdefault(pid, {"k": {}})["mesh"] = [list(mw @ v.co) for v in list(o.data.vertices)[::6]]
    return people


def main():
    d = Path(sys.argv[sys.argv.index("--") + 1])
    images = []
    for layout in sorted((d / "sheets").glob("sheet-*.json")):
        glb = d / "sam" / (layout.stem + ".glb")
        if glb.exists(): images.append((layout, glb))
    for layout in sorted((d / "singles").glob("single-*.json")):
        glb = d / "sam1" / f"f{layout.stem.split('-')[1]}.glb"
        if glb.exists(): images.append((layout, glb))
    out = []
    for layout, glb in images:
        out.append({"name": layout.stem, "layout": json.loads(layout.read_text()), "people": list(people_of(glb).values())})
        print("ROTO", layout.stem, len(out[-1]["people"]), flush=True)
    (d / "sam-dump.json").write_text(json.dumps(out))


main()
