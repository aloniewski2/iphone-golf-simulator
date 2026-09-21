import bpy, bmesh, math, random, sys, importlib
import os, bpy; sys.path.insert(0, os.path.join(bpy.path.abspath("//"), "scripts"))
import hole07_lib as H; importlib.reload(H)
import hole07_design as D; importlib.reload(D)
from hole07_design import *
from mathutils import Vector, Matrix

# ------------------------------------------------------------------ materials
MAT_WOOD       = H.get_material("MAT_WOOD",       H.rgb(112, 74, 46))
MAT_TREE_DARK  = H.get_material("MAT_TREE_DARK",  H.rgb(34, 104, 52))
MAT_TREE_MID   = H.get_material("MAT_TREE_MID",   H.rgb(52, 140, 62))
MAT_TREE_LIGHT = H.get_material("MAT_TREE_LIGHT", H.rgb(92, 176, 70))

# ------------------------------------------------------------------ asset library collection
def asset_collection():
    env = H.get_collection("ENVIRONMENT")
    lib = bpy.data.collections.get("ASSET_LIBRARY")
    if lib is None:
        lib = bpy.data.collections.new("ASSET_LIBRARY")
        env.children.link(lib)
    lib.hide_render = True
    lib.hide_viewport = False
    return lib

def instance_collection(name):
    env = H.get_collection("ENVIRONMENT")
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        env.children.link(col)
    return col

def clear_collection_objects(col):
    for o in list(col.objects):
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)

# ------------------------------------------------------------------ mesh builders (bmesh)
def add_cone(bm, z0, z1, r0, r1, segs, mat):
    ring0 = [bm.verts.new((math.cos(a) * r0, math.sin(a) * r0, z0)) for a in [2 * math.pi * i / segs for i in range(segs)]]
    faces = []
    if r1 <= 1e-4:
        top = bm.verts.new((0, 0, z1))
        for i in range(segs):
            faces.append(bm.faces.new((ring0[i], ring0[(i + 1) % segs], top)))
    else:
        ring1 = [bm.verts.new((math.cos(a) * r1, math.sin(a) * r1, z1)) for a in [2 * math.pi * i / segs for i in range(segs)]]
        for i in range(segs):
            faces.append(bm.faces.new((ring0[i], ring0[(i + 1) % segs], ring1[(i + 1) % segs], ring1[i])))
        faces.append(bm.faces.new(list(reversed(ring1))))
    if r0 > 1e-4:
        faces.append(bm.faces.new(ring0))
    for f in faces:
        f.material_index = mat
        f.smooth = False
    return faces

def add_blob(bm, center, rx, ry, rz, mat, subdiv=1, seed=0):
    rnd = random.Random(seed)
    res = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    verts = res["verts"]
    for v in verts:
        j = 1.0 + rnd.uniform(-0.12, 0.12)
        v.co = Vector((center[0] + v.co.x * rx * j, center[1] + v.co.y * ry * j, center[2] + v.co.z * rz * j))
    faces = {f for v in verts for f in v.link_faces}
    for f in faces:
        f.material_index = mat
        f.smooth = False
    return faces

def make_asset(name, builder, mats):
    lib = asset_collection()
    H.remove_object(name)
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    lib.objects.link(ob)
    bm = bmesh.new()
    builder(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for m in mats:
        me.materials.append(m)
    bm.to_mesh(me); bm.free()
    ob.hide_render = True
    ob.location = (0, -420, 0)   # parked off-course
    return ob

def pine(h, r, tiers, trunk_r, foliage_mat_idx):
    def build(bm):
        add_cone(bm, 0, h * 0.28, trunk_r, trunk_r * 0.8, 6, 0)
        tier_h = h * 0.78 / tiers
        for i in range(tiers):
            z0 = h * 0.22 + i * tier_h * 0.78
            rr = r * (1.0 - i / (tiers + 0.6))
            add_cone(bm, z0, z0 + tier_h * 1.25, rr, 0.0, 7, foliage_mat_idx)
    return build

def round_tree(h, r, seed):
    def build(bm):
        add_cone(bm, 0, h * 0.42, r * 0.16, r * 0.12, 6, 0)
        add_blob(bm, (0, 0, h * 0.42 + r * 0.8), r, r * 0.95, r * 0.85, 1, subdiv=1, seed=seed)
        add_blob(bm, (r * 0.45, -r * 0.2, h * 0.42 + r * 0.55), r * 0.6, r * 0.6, r * 0.5, 2, subdiv=1, seed=seed + 1)
    return build

def bush(r, seed):
    def build(bm):
        add_blob(bm, (0, 0, r * 0.55), r, r * 0.9, r * 0.65, 1, subdiv=1, seed=seed)
        add_blob(bm, (r * 0.7, r * 0.3, r * 0.4), r * 0.6, r * 0.6, r * 0.45, 2, subdiv=1, seed=seed + 3)
    return build

TREE_MATS = [MAT_WOOD, MAT_TREE_DARK, MAT_TREE_MID, MAT_TREE_LIGHT]
assets = {
    "TREE_PINE_LARGE":  make_asset("TREE_PINE_LARGE",  pine(30, 8.6, 4, 1.1, 1), TREE_MATS),
    "TREE_PINE_MEDIUM": make_asset("TREE_PINE_MEDIUM", pine(22, 6.6, 3, 0.9, 2), TREE_MATS),
    "TREE_PINE_SMALL":  make_asset("TREE_PINE_SMALL",  pine(14, 4.6, 3, 0.6, 1), TREE_MATS),
    "TREE_ROUND":       make_asset("TREE_ROUND",       round_tree(16, 7.4, 4), TREE_MATS),
    "BUSH_SMALL":       make_asset("BUSH_SMALL",       bush(3.6, 8), TREE_MATS),
}

# ------------------------------------------------------------------ placement
island, _ = island_outline()
fair = fairway_outline(6.0)
green_keep = H.ellipse(GREEN["cx"], GREEN["cy"], GREEN["rx"] + 9, GREEN["ry"] + 9, GREEN["rot"], 24)
tee_keep = H.ellipse(TEE["cx"], TEE["cy"], TEE["rx"] + 8, TEE["ry"] + 8, TEE["rot"], 24)
bunkers = [[c + (p - c) * 1.5 for c in [sum(b, Vector((0, 0))) / len(b)] for p in b] for b in bunker_outlines()]
path = path_points(12)
club = CLUBHOUSE

def near_path(p, d=8.0):
    return any((q - p).length < d for q in path)

def in_clubhouse(p, pad=14):
    return abs(p.x - club["cx"]) < club["w"] / 2 + pad and abs(p.y - club["cy"]) < club["d"] / 2 + pad

def blocked(p):
    if not H.point_in_poly(p, island) or H.dist_to_poly(p, island) < 6.0:
        return True
    if H.point_in_poly(p, fair) or H.point_in_poly(p, green_keep) or H.point_in_poly(p, tee_keep):
        return True
    if any(H.point_in_poly(p, b) for b in bunkers):
        return True
    if near_path(p) or in_clubhouse(p):
        return True
    return False

rnd = random.Random(21)
placed = []   # (Vector, kind)
def try_place(kind, min_d, tries=400, region=None):
    for _ in range(tries):
        x = rnd.uniform(-115, 90); y = rnd.uniform(-240, 265)
        p = Vector((x, y))
        if region and not region(p):
            continue
        if blocked(p):
            continue
        if any((q - p).length < min_d for q, _k in placed):
            continue
        # thin out the exposed east cliff mid-hole so the water/cliff edge stays readable
        if x > 30 and -70 < y < 120 and rnd.random() < 0.6:
            continue
        placed.append((p, kind))
        return True
    return False

# feature clusters first: tee lobe, west edge, green sides, north headland
west  = lambda p: p.x < -40
south = lambda p: p.y < -150
north = lambda p: p.y > 150
east_green = lambda p: p.x > 30 and p.y > 120
for _ in range(8):  try_place("TREE_PINE_LARGE", 16, region=west)
for _ in range(5):  try_place("TREE_PINE_LARGE", 16, region=south)
for _ in range(6):  try_place("TREE_PINE_LARGE", 14, region=north)
for _ in range(4):  try_place("TREE_PINE_LARGE", 14, region=east_green)
for _ in range(5):  try_place("TREE_PINE_MEDIUM", 12, region=north)
for _ in range(4):  try_place("TREE_PINE_MEDIUM", 12)
for _ in range(6):  try_place("TREE_PINE_SMALL", 9)
for _ in range(3):  try_place("TREE_ROUND", 12, region=south)
for _ in range(4):  try_place("TREE_ROUND", 12)
for _ in range(8):  try_place("BUSH_SMALL", 7)
west_mid = lambda p: p.x < -45 and -70 < p.y < 120
south_east = lambda p: p.x > 25 and p.y < -110
for _ in range(4):  try_place("TREE_PINE_MEDIUM", 11, region=west_mid)
for _ in range(3):  try_place("TREE_PINE_SMALL", 9, region=west_mid)
for _ in range(4):  try_place("TREE_PINE_LARGE", 14, region=south_east)

col = instance_collection("TREES")
clear_collection_objects(col)
root = H.get_root()
counts = {}
for i, (p, kind) in enumerate(placed):
    src = assets[kind]
    ob = bpy.data.objects.new(f"{kind}.{i:03d}", src.data)
    col.objects.link(ob)
    ob.parent = root
    s = rnd.uniform(0.85, 1.18)
    ob.location = (p.x, p.y, height(p.x, p.y) - 0.35)
    ob.rotation_euler = (0, 0, rnd.uniform(0, math.tau))
    ob.scale = (s, s, s * rnd.uniform(0.95, 1.1))
    counts[kind] = counts.get(kind, 0) + 1

H.save()
result = {"placed": len(placed), "by_kind": counts,
          "asset_tris": {k: len(v.data.polygons) for k, v in assets.items()}}
