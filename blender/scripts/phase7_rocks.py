import bpy, bmesh, math, random, sys, importlib
import os, bpy; sys.path.insert(0, os.path.join(bpy.path.abspath("//"), "scripts"))
import hole07_lib as H; importlib.reload(H)
import hole07_design as D; importlib.reload(D)
from hole07_design import *
from mathutils import Vector

MAT_ROCK = H.get_material("MAT_ROCK", H.rgb(132, 134, 140))
MAT_ROCK_DARK = H.get_material("MAT_ROCK_DARK", H.rgb(98, 102, 110))

env = H.get_collection("ENVIRONMENT")
lib = bpy.data.collections.get("ASSET_LIBRARY")
def sub_collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        env.children.link(col)
    return col

def make_rock_asset(name, rx, ry, rz, seed, subdiv=1, jitter=0.22, cut_bottom=True, mat=MAT_ROCK):
    H.remove_object(name)
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    lib.objects.link(ob)
    rnd = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    for v in bm.verts:
        j = 1.0 + rnd.uniform(-jitter, jitter)
        v.co = Vector((v.co.x * rx * j, v.co.y * ry * j, max(v.co.z * rz * j, -rz * 0.25 if cut_bottom else -rz)))
    # blocky: snap a few verts outward to make flat facets
    for v in bm.verts:
        if rnd.random() < 0.3:
            v.co *= 1.12
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:
        f.smooth = False
    me.materials.append(mat)
    bm.to_mesh(me); bm.free()
    ob.hide_render = True
    ob.location = (40, -420, 0)
    return ob

rocks = {
    "ROCK_SMALL":  make_rock_asset("ROCK_SMALL",  1.4, 1.1, 0.9, 31),
    "ROCK_MEDIUM": make_rock_asset("ROCK_MEDIUM", 2.6, 2.1, 1.7, 32),
    "ROCK_LARGE":  make_rock_asset("ROCK_LARGE",  4.4, 3.6, 3.0, 33, jitter=0.18),
    "CLIFF_ROCK":  make_rock_asset("CLIFF_ROCK",  9.0, 7.0, 8.5, 34, jitter=0.16, cut_bottom=False),
}

col = sub_collection("ROCKS")
for o in list(col.objects):
    bpy.data.objects.remove(o, do_unlink=True)
root = H.get_root()
rnd = random.Random(77)
island, _ = island_outline()
n = len(island)
c = sum(island, Vector((0, 0))) / n

def outward(i):
    p0 = island[i - 1]; p1 = island[(i + 1) % n]
    t = (p1 - p0).normalized()
    nrm = Vector((t.y, -t.x))
    if nrm.dot(island[i] - c) < 0:
        nrm = -nrm
    return nrm

counter = [0]
def place(kind, loc, scale, rot=None):
    src = rocks[kind]
    ob = bpy.data.objects.new(f"{kind}.{counter[0]:03d}", src.data)
    counter[0] += 1
    col.objects.link(ob)
    ob.parent = root
    ob.location = loc
    ob.scale = scale
    ob.rotation_euler = rot if rot else (rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15), rnd.uniform(0, math.tau))
    return ob

# --- sea stacks / boulders at the cliff foot, biased to the exposed east & south faces
foot_slots = []
for i in range(0, n, 3):
    p = island[i]
    east_or_south = p.x > 20 or p.y < -180
    prob = 0.55 if east_or_south else 0.2
    if rnd.random() < prob:
        foot_slots.append(i)
for i in foot_slots:
    p = island[i]; nrm = outward(i)
    d = rnd.uniform(8.0, 15.0)
    q = p + nrm * d
    s = rnd.uniform(0.7, 1.35)
    place("CLIFF_ROCK", (q.x, q.y, rnd.uniform(-4.0, -1.0)), (s, s * rnd.uniform(0.8, 1.1), s * rnd.uniform(0.7, 1.1)))
    if rnd.random() < 0.5:   # companion boulder
        q2 = q + nrm * rnd.uniform(5, 9) + Vector((rnd.uniform(-4, 4), rnd.uniform(-4, 4)))
        s2 = rnd.uniform(0.6, 1.0)
        place("ROCK_LARGE", (q2.x, q2.y, rnd.uniform(-1.5, 0.3)), (s2, s2, s2))

# --- boulders wedged into the cliff face (mid height) to break the horizontal banding
for i in range(0, n, 5):
    if rnd.random() < 0.35:
        p = island[i]; nrm = outward(i)
        top = height(p.x, p.y)
        q = p + nrm * rnd.uniform(3.5, 6.0)
        s = rnd.uniform(0.5, 0.9)
        place("CLIFF_ROCK", (q.x, q.y, top * rnd.uniform(0.35, 0.7)), (s, s, s * 0.8))

# --- rocks on the island top: cliff-edge outcrops, a few by bunkers, boundary clutter
fair = fairway_outline(4.0)
green_keep = H.ellipse(GREEN["cx"], GREEN["cy"], GREEN["rx"] + 8, GREEN["ry"] + 8, GREEN["rot"], 24)
tee_keep = H.ellipse(TEE["cx"], TEE["cy"], TEE["rx"] + 6, TEE["ry"] + 6, TEE["rot"], 24)
path = path_points(12)
bunks = bunker_outlines()
def ok(p):
    if not H.point_in_poly(p, island) or H.dist_to_poly(p, island) < 3.0:
        return False
    if H.point_in_poly(p, fair) or H.point_in_poly(p, green_keep) or H.point_in_poly(p, tee_keep):
        return False
    if any((q - p).length < 6 for q in path):
        return False
    if any(H.point_in_poly(p, b) for b in bunks):
        return False
    if abs(p.x - CLUBHOUSE["cx"]) < 30 and abs(p.y - CLUBHOUSE["cy"]) < 24:
        return False
    return True

top_placed = 0
for _ in range(600):
    if top_placed >= 26:
        break
    i = rnd.randrange(n)
    p = island[i] - outward(i) * rnd.uniform(4.0, 12.0)
    if not ok(p):
        continue
    kind = rnd.choice(["ROCK_SMALL", "ROCK_MEDIUM", "ROCK_MEDIUM", "ROCK_LARGE"])
    s = rnd.uniform(0.8, 1.3)
    place(kind, (p.x, p.y, height(p.x, p.y) - 0.4 * s), (s, s, s))
    top_placed += 1
# rocks beside bunkers (mockup has small rock clusters near sand)
for b in bunks[:4]:
    bc = sum(b, Vector((0, 0))) / len(b)
    for _ in range(2):
        ang = rnd.uniform(0, math.tau)
        p = bc + Vector((math.cos(ang), math.sin(ang))) * rnd.uniform(16, 22)
        if ok(p):
            s = rnd.uniform(0.6, 1.0)
            place("ROCK_SMALL" if rnd.random() < 0.5 else "ROCK_MEDIUM", (p.x, p.y, height(p.x, p.y) - 0.3), (s, s, s))

H.save()
result = {"rocks_placed": counter[0], "asset_tris": {k: len(v.data.polygons) for k, v in rocks.items()}}
