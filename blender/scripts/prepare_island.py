"""The island around the resort: a Higgsfield (Tripo H3.1) island model fitted to the arena.

From the air the resort was a rectangular limestone plinth floating in open sea. The island
model has a flat clearing in its middle; this fits that clearing to the plinth:

  1. heightmap the model (rays from above) and find the clearing: the flattest, most common
     height in the middle, grown outward from the centre
  2. scale it so the clearing just contains the plinth (83 x 68 m); scale heights separately
     so the reef sits at sea level (-4.3) and the clearing just under the court (-0.6), which
     while palms and cliffs above the clearing keep near-natural proportions (up to ~16 m)
  3. centre the clearing on the plinth, delete terrain under the plinth, and open the sea side
     behind the far baseline so the view past the court stays ocean
  4. export in the same frame and axes as the arena FBX (TropicalTennisResort.fbx)

Coordinates here are the arena's Blender frame (Unity: x, z_blender, y_blender).

Run:  Blender --background --python blender/scripts/prepare_island.py -- island.glb
"""
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Unity/Assets/Resources/Tennis/Island"
PLINTH = (-37.5, 45.5, -33.0, 35.0)       # x min/max, y min/max (limestone plinth)
SEA_SIDE_Y = 30.0                          # beyond this (far baseline side) stays open sea
REEF_Z, CLEARING_Z = -4.3, -0.6
GRID = 240


def main():
    src = sys.argv[sys.argv.index("--") + 1]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    obj = next(o for o in bpy.context.scene.objects if o.type == "MESH")
    for o in list(bpy.context.scene.objects):
        if o is not obj: bpy.data.objects.remove(o, do_unlink=True)
    obj.parent = None
    bpy.context.view_layer.objects.active = obj; obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    me = obj.data
    xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
    x0, x1, y0, y1, z0, z1 = min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)

    # 1. Heightmap and clearing.
    bm = bmesh.new(); bm.from_mesh(me); tree = BVHTree.FromBMesh(bm); bm.free()
    h = {}
    for i in range(GRID):
        for j in range(GRID):
            x = x0 + (x1 - x0) * (i + .5) / GRID; y = y0 + (y1 - y0) * (j + .5) / GRID
            hit = tree.ray_cast(Vector((x, y, z1 + 1)), Vector((0, 0, -1)), 10)
            if hit[0] is not None: h[(i, j)] = hit[0].z
    centre = [(i, j) for (i, j) in h if abs(i - GRID / 2) < GRID * .2 and abs(j - GRID / 2) < GRID * .2]
    buckets = {}
    for c in centre: buckets.setdefault(round(h[c] / ((z1 - z0) / 200)), []).append(c)
    level = sorted(buckets.items(), key=lambda kv: -len(kv[1]))[0][0] * ((z1 - z0) / 200)
    tol = (z1 - z0) * .02
    seed = min((c for c in centre if abs(h[c] - level) < tol), key=lambda c: (c[0] - GRID / 2) ** 2 + (c[1] - GRID / 2) ** 2)
    region, stack = {seed}, [seed]
    while stack:
        i, j = stack.pop()
        for q in ((i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)):
            if q in h and q not in region and abs(h[q] - level) < tol:
                region.add(q); stack.append(q)
    ci = [i for i, _ in region]; cj = [j for _, j in region]
    # Robust extents: ignore thin tendrils.
    ci.sort(); cj.sort()
    lo_i, hi_i = ci[int(len(ci) * .05)], ci[int(len(ci) * .95)]
    lo_j, hi_j = cj[int(len(cj) * .05)], cj[int(len(cj) * .95)]
    cw = (hi_i - lo_i + 1) / GRID * (x1 - x0); cl = (hi_j - lo_j + 1) / GRID * (y1 - y0)
    cx = x0 + (x1 - x0) * ((lo_i + hi_i) / 2 + .5) / GRID; cy = y0 + (y1 - y0) * ((lo_j + hi_j) / 2 + .5) / GRID

    # 2. Scale.
    pw, pl = PLINTH[1] - PLINTH[0], PLINTH[3] - PLINTH[2]
    s = max(pw / cw, pl / cl) * 1.08
    # Heights: reef-to-clearing is squeezed so the beaches meet the sea and the clearing sits
    # just under the court; everything above the clearing (palms, cliffs) keeps close to its
    # real proportion, or the trees came out five times wider than tall.
    sz = (CLEARING_Z - REEF_Z) / max(1e-4, level - z0)
    sz_up = s * .45
    px, py = (PLINTH[0] + PLINTH[1]) / 2, (PLINTH[2] + PLINTH[3]) / 2
    for v in me.vertices:
        x, y, z = v.co
        height = REEF_Z + (min(z, level) - z0) * sz + max(0, z - level) * sz_up
        v.co = Vector((px + (x - cx) * s, py + (y - cy) * s, height))
    me.update()

    # 3. Clear the plinth footprint and the sea side.
    bm = bmesh.new(); bm.from_mesh(me)
    m = 1.0
    # Any face touching the footprint goes (faces only partly inside left a sawtooth fringe
    # along the terrace), and the sea side is carved as a curved bay rather than a straight cut.
    def under_plinth(v): return PLINTH[0] - m < v.co.x < PLINTH[1] + m and PLINTH[2] - m < v.co.y < PLINTH[3] + m
    def in_bay(c): return c.y > SEA_SIDE_Y + .006 * (c.x - px) ** 2
    doomed = [f for f in bm.faces if any(under_plinth(v) for v in f.verts) or in_bay(f.calc_center_median())]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(me); bm.free()
    obj.name = "Tropical island"; me.name = obj.name
    mat = me.materials[0]; mat.name = "V4 Higgs Island"

    # Textures for Unity (loaded by name at runtime).
    OUT.mkdir(parents=True, exist_ok=True)
    for node in mat.node_tree.nodes:
        if node.type != "TEX_IMAGE" or not node.image: continue
        kind = "Color" if node.image.name.startswith("Color") else "Normal" if node.image.name.startswith("Normal") else None
        if not kind: continue
        img = node.image.copy(); img.scale(*((2048, 2048) if kind == "Color" else (1024, 1024)))
        img.filepath_raw = str(OUT / f"Island_{kind}.png"); img.file_format = "PNG"; img.save()

    # 4. Export like the arena.
    bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT / "TennisIsland.fbx"), use_selection=True, object_types={"MESH"},
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_anim=False,
                             use_mesh_modifiers=True, path_mode="AUTO")
    xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
    print(f"ISLAND clearing {cw:.3f}x{cl:.3f} (norm) level {level:.4f}; scale {s:.1f} x/y, {sz:.1f}/{sz_up:.1f} z; "
          f"extent x {min(xs):.0f}..{max(xs):.0f} y {min(ys):.0f}..{max(ys):.0f} z {min(zs):.1f}..{max(zs):.1f}; "
          f"{len(me.polygons)} faces", flush=True)


main()
