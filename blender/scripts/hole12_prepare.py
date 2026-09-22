"""Turn the Codex-built "Hole 12 Island Carry" scene into the game's second course model.

    Blender -b <Hole_12_Animated.blend | Hole_12_Island_Carry.blend> --python blender/scripts/hole12_prepare.py -- [--render]

Unlike Hole 7 the scene is not generated here; it was modelled in Blender and handed over as a
.blend (Documents/Codex/2026-09-20/i-j/outputs). This script is the repeatable bridge from that
file to what HoleView expects, and it leaves the source untouched. It takes either hand-off: the
original Island Carry file (open cliff shells with a turf fan on each island) or the later
Animated one (each island a closed solid with the turf as its cap, real bunker bowls cut into the
green island, a wave grid with four morph targets — see hole12_animate_finish.py), which is what
the game uses now. From the animated file it also carries the water over: WATER_WAVES keeps its
shape keys (Unity blendshapes HoleView cross-fades) and WATER_GLINTS is the card sheet it slides.

  * drops the golfer rig, preview props, reference image, collision copies, presentation ball
    and cameras that came along;
  * renames materials to the MAT_* palette HoleView recolours by, and the ground meshes to the
    TERRAIN/FAIRWAY/GREEN/TEE_BOX/BUNKER prefixes it gives MeshColliders;
  * batches the ~900 scenery objects (ripples, flowers, planks, trees...) into a handful of
    meshes so the phone draws the hole in a few dozen calls;
  * adds MARKER_TEE / MARKER_PIN / MARKER_UP under HOLE_12_ROOT, like Hole 7;
  * saves blender/hole_12.blend, exports Unity/Assets/Resources/Course/hole_12.fbx, and prints
    the flight-model numbers (yards from the tee, shore polygons) that Course.Cliffside carries.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
BLEND = os.path.join(REPO, "blender", "hole_12.blend")
FBX = os.path.join(REPO, "Unity", "Assets", "Resources", "Course", "hole_12.fbx")
PREVIEW = os.path.join(REPO, "blender", "previews", "hole_12_overview.png")
YARDS = 1.0936
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

sc = bpy.context.scene
O = bpy.data.objects


def obj(name):
    o = O.get(name)
    if o is None: raise KeyError(f"scene has no object {name!r}")
    return o


def remove(objects):
    for o in list(objects):
        if o.name in O: O.remove(o, do_unlink=True)


# ------------------------------------------------------------------ 1. leftovers from the golfer file
remove(o for o in O if o.name in ("GOLFER_RIG", "GOLFER_BODY", "PREVIEW_BALL", "PREVIEW_FLOOR", "SUN",
                                   "CAM_CLOSE", "CAM_DOWN_LINE", "CAM_FACE_ON", "CAM_Ball_Action",
                                   "Golf ball • animated", "Ball • alignment stripe"))
# The animated hand-off has each island as one closed solid; the original has open cliff shells
# under a flat turf fan. Both go through here.
SOLID = "Terrain • continuous tee and approach" in O
# On the solids the turf cap's edge loop runs into the bunker bowl that breaks through the east
# cliff, so the shore polygons come from the flat collision fans instead — the original turf
# outlines, kept until the numbers are read off them.
FOOTPRINTS = {"COL_Tee island • turf": "FOOTPRINT_TEE", "COL_Bridge approach • turf": "FOOTPRINT_APPROACH",
              "COL_Eastern promontory • turf": "FOOTPRINT_PROMONTORY", "COL_Green island • turf": "FOOTPRINT_GREEN"}
if SOLID:
    for old, new in FOOTPRINTS.items():
        o = obj(old); o.name = new
        sc.collection.objects.link(o)   # outlives its collection, until step 6 is done with it
for col_name in ("REF", "Collisions", "Ball_Animation"):
    col = bpy.data.collections.get(col_name)
    if col:
        remove(o for o in col.objects if o.name not in FOOTPRINTS.values())
        bpy.data.collections.remove(col)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
for other in [x for x in bpy.data.scenes if x is not sc]: bpy.data.scenes.remove(other)

# ------------------------------------------------------------------ 2. materials → the game's palette
# Palette names HoleView already knows, plus the ones added for this hole (see HoleView.Palette).
RENAME = {
    "Grass • emerald rough": "MAT_ROUGH", "Grass • sunlit edge": "MAT_BUNKER_LIP", "Green • fringe": "MAT_FIRSTCUT",
    "Green • bentgrass": "MAT_GREEN", "Sand • warm ivory": "MAT_SAND",
    "Basalt • facet 00": "MAT_BASALT_0", "Basalt • facet 01": "MAT_BASALT_1", "Basalt • facet 02": "MAT_BASALT_2",
    "Basalt • facet 03": "MAT_BASALT_3", "Basalt • facet 04": "MAT_BASALT_4",
    "Bridge • honey cedar": "MAT_WOOD", "Bridge • sunlit planks": "MAT_WOOD_LIGHT", "Pine • bark": "MAT_BARK",
    "Pine • foliage 0": "MAT_TREE_DARK", "Pine • foliage 1": "MAT_TREE_MID", "Pine • foliage 2": "MAT_TREE_LIGHT",
    "Flowers • coral": "MAT_FLOWER_CORAL", "Flowers • golden": "MAT_FLOWER_GOLD", "Flowers • lavender": "MAT_FLOWER_LAVENDER",
    "Flag and lighthouse • vermilion": "MAT_FLAG", "Lighthouse • chalk": "MAT_CHALK", "Lighthouse • slate": "MAT_SLATE",
    "Lantern • blue glass": "MAT_GLASS", "Water • turquoise": "MAT_WATER", "Water • foam": "MAT_FOAM",
    "Water • ripple glints": "MAT_FOAM",
}
import re
for m in list(bpy.data.materials):
    base = re.sub(r"\.\d{3}$", "", m.name)   # the solids carry per-island copies: "Basalt • facet 00.002"
    if base not in RENAME: continue
    new = RENAME[base]
    existing = bpy.data.materials.get(new)
    if existing is not None and existing is not m:
        m.user_remap(existing)  # two source materials share one game colour
        bpy.data.materials.remove(m)
    else:
        m.name = new

# The green and the approach fairway share one bentgrass shader in the source; the game shades
# them apart, so the fairway and tee pads get their own copy with the fairway's name.
fairway = bpy.data.materials["MAT_GREEN"].copy(); fairway.name = "MAT_FAIRWAY"
for name in ("Approach fairway", "Tee deck", "Red tee pad", "Yellow tee pad"):
    obj(name).data.materials[0] = fairway
pole = bpy.data.materials.get("MAT_POLE") or bpy.data.materials["MAT_CHALK"].copy()
pole.name = "MAT_POLE"
for o in O:
    if o.name.startswith("White tee marker") or o.name == "Flagstick": o.data.materials[0] = pole

# ------------------------------------------------------------------ 3. ground meshes the ball rests on
GROUND = {
    # the original hand-off: a turf fan per island                the animated one: closed solids
    "Tee island • turf": "TERRAIN_TEE_ISLAND", "Bridge approach • turf": "TERRAIN_BRIDGE_APPROACH",
    "Approach turf joint": "TERRAIN_JOINT", "Eastern promontory • turf": "TERRAIN_PROMONTORY",
    "Green island • turf": "TERRAIN_GREEN_ISLAND",
    "Terrain • continuous tee and approach": "TERRAIN_TEE_APPROACH",
    "Putting green": "GREEN", "Putting fringe": "GREEN_FRINGE",
    "Approach fairway": "FAIRWAY", "Approach fringe": "FAIRWAY_FRINGE", "Tee deck": "TEE_BOX", "Tee fringe": "TEE_BOX_FRINGE",
    "Red tee pad": "TEE_BOX_RED", "Yellow tee pad": "TEE_BOX_YELLOW",
    "Bunker_00": "BUNKER_00", "Bunker_01": "BUNKER_01", "Bunker_02": "BUNKER_02",
}
SCENERY = {
    "Tee island • cliff": "CLIFF_TEE_ISLAND", "Bridge approach • cliff": "CLIFF_BRIDGE_APPROACH",
    "Eastern promontory • cliff": "CLIFF_PROMONTORY", "Green island • cliff": "CLIFF_GREEN_ISLAND", "Ocean": "WATER_OCEAN",
    "Water • looping waves": "WATER_WAVES", "Water • moving glints": "WATER_GLINTS",
    # stand-ins the game replaces with its own flag and cup at the exact pin
    "Red pin flag": "FLAG", "Flagstick": "FLAG_POLE", "Cup": "HOLE_CUP",
}
if SOLID:
    # the cliffs ARE the ground now (turf cap on top), so they take the ground names
    for old, new in (("Green island • cliff", "TERRAIN_GREEN_ISLAND"), ("Eastern promontory • cliff", "TERRAIN_PROMONTORY")):
        GROUND[old] = new; SCENERY.pop(old)
for old, new in {**GROUND, **SCENERY}.items():
    if old in O: O[old].name = new
for name in ("TERRAIN_GREEN_ISLAND", "GREEN", "GREEN_FRINGE", "FAIRWAY", "TEE_BOX", "WATER_OCEAN", "CUP", "TEE_WHITE"): obj(name)


# ------------------------------------------------------------------ 3b. the putting surface
# The green is sculpted here and the game reads its slope off this very mesh (HoleView samples
# it into the hole's HeightGrid), so the render, the read and the roll agree. Metres from the
# scene origin; each feature is a (1 - u²)² bump, flat on top and at its rim so nothing kinks.
# A few tenths of a metre over 10–15 m gives the 1–3 % slopes a putt visibly breaks on.
CONTOURS = [
    dict(x=-7, y=190, r=15, h=0.26),                    # a shelf rising to the back left, under the lighthouse
    dict(x=7, y=171, r=11, h=-0.14),                    # a bowl at the front right, where the safe shot lands
    dict(x=-13, y=173, x2=-2, y2=166, r=7, h=0.10),     # a roll across the front left
]


def contour_height(x, y):
    z = 0.0
    for f in CONTOURS:
        if "x2" in f:
            dx, dy = f["x2"] - f["x"], f["y2"] - f["y"]
            t = max(0.0, min(1.0, ((x - f["x"]) * dx + (y - f["y"]) * dy) / max(dx * dx + dy * dy, 1e-4)))
            nx, ny = f["x"] + dx * t, f["y"] + dy * t
        else:
            nx, ny = f["x"], f["y"]
        u = math.hypot(x - nx, y - ny) / f["r"]
        if u < 1: z += f["h"] * (1 - u * u) ** 2
    return z


# Every ground mesh on the green island follows the contours; the big flat fans are cut fine
# enough first for the curves to show (and for the game's 0.5 yd height samples to be honest).
SUBDIVIDE = {"GREEN": 7, "GREEN_FRINGE": 7, "FAIRWAY": 5, "FAIRWAY_FRINGE": 5, "TERRAIN_GREEN_ISLAND": 12 if not SOLID else 4}
CAP_Z = 20.5   # on the solid green island everything above this is the turf cap (its cliffs stop at 20.3)
for name in list(SUBDIVIDE) + ["BUNKER_00", "BUNKER_01", "BUNKER_02"]:
    o = obj(name)
    bm = bmesh.new(); bm.from_mesh(o.data)
    if name in SUBDIVIDE:
        if SOLID and name != "TERRAIN_GREEN_ISLAND":
            # the animated file solidified the patches 12 cm so the bunker bowls could be cut
            # through them; only their top faces are ever seen or landed on
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z < 0.5], context='FACES')
        faces = [f for f in bm.faces if not SOLID or name != "TERRAIN_GREEN_ISLAND"
                 or (f.normal.z > 0.9 and f.calc_center_median().z > CAP_Z)]
        bmesh.ops.triangulate(bm, faces=faces)
        faces = [f for f in bm.faces if not SOLID or name != "TERRAIN_GREEN_ISLAND"
                 or (f.normal.z > 0.9 and f.calc_center_median().z > CAP_Z)]
        edges = list({e for f in faces for e in f.edges})
        bmesh.ops.subdivide_edges(bm, edges=edges, cuts=SUBDIVIDE[name], use_grid_fill=True)
    for v in bm.verts:
        if SOLID and name == "TERRAIN_GREEN_ISLAND" and v.co.z < CAP_Z: continue   # cliffs and bunker bowls stay
        w = o.matrix_world @ v.co
        v.co.z += contour_height(w.x, w.y)
    bm.to_mesh(o.data); bm.free()
    o.data.update()
cup_lift = contour_height(obj("CUP").location.x, obj("CUP").location.y)
for name in ("HOLE_CUP", "FLAG_POLE", "FLAG", "CUP"): obj(name).location.z += cup_lift  # the stand-ins ride the surface

# ------------------------------------------------------------------ 4. batch the scenery
def merge(name, objects, collection):
    """One mesh in world space out of `objects` (evaluated, so curve bevels and modifiers are
    baked), material slots unified by name; the sources are removed."""
    objects = [o for o in objects if o.type in ("MESH", "CURVE")]
    if not objects: return None
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new(name)
    slots = []
    bm = bmesh.new()
    for o in objects:
        eo = o.evaluated_get(dg)
        me = eo.to_mesh()
        if me is None or len(me.polygons) == 0: eo.to_mesh_clear(); continue
        mats = [s.material for s in o.material_slots] or [None]  # object-linked slots count too
        remap = []
        for m in mats:
            if m not in slots: slots.append(m)
            remap.append(slots.index(m))
        tmp = bpy.data.meshes.new("_tmp")
        tb = bmesh.new(); tb.from_mesh(me); tb.transform(o.matrix_world)
        for f in tb.faces: f.material_index = remap[min(f.material_index, len(remap) - 1)]
        tb.to_mesh(tmp); tb.free(); eo.to_mesh_clear()
        bm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
    bm.to_mesh(out); bm.free()
    for m in slots: out.materials.append(m)
    ob = O.new(name, out)
    col = bpy.data.collections.get(collection) or bpy.data.collections.new(collection)
    if col.name not in sc.collection.children: sc.collection.children.link(col)
    col.objects.link(ob)
    remove(objects)
    return ob


def named(*prefixes): return [o for o in O if o.name.startswith(prefixes)]

merge("WATER_RIPPLES", named("Ocean ripple"), "Water_Ocean")
merge("WATER_FOAM", [o for o in O if "foam" in o.name.lower()], "Water_Ocean")
merge("ROCKS", named("Shore basalt"), "Terrain_Main")
merge("BRIDGE_PLANKS", named("Bridge plank"), "Path_Bridge")
merge("BRIDGE_RAILS", named("Bridge pier", "Rail post", "Cedar railing"), "Path_Bridge")
merge("TEE_FENCE", named("Tee fence"), "Tee_Island")
merge("TEE_MARKERS", named("White tee marker"), "Tee_Island")
merge("TREES", named("Evergreen"), "Vegetation")
merge("SHRUBS", named("Low shrub"), "Vegetation")
merge("FLOWERS", named("Flower cluster"), "Vegetation")
merge("WATERFALL", named("Waterfall"), "Landmarks")
merge("LIGHTHOUSE", named("Lighthouse", "Lantern", "Finial"), "Landmarks")

# ------------------------------------------------------------------ 5. markers and root, as Hole 7 has them
tee = Vector(obj("TEE_WHITE").location)
cup = Vector(obj("CUP").location)
root = O.get("HOLE_12_ROOT")
if root is None:
    root = O.new("HOLE_12_ROOT", None)
    root.empty_display_type = 'PLAIN_AXES'; root.empty_display_size = 20
    sc.collection.objects.link(root)
gameplay = bpy.data.collections.get("Gameplay_Refs")
# MARKER_UP sits 50 m straight above the tee marker so an importer can recover the up axis
for name, loc in (("MARKER_TEE", tee), ("MARKER_PIN", cup), ("MARKER_UP", tee + Vector((0, 0, 50)))):
    remove([O[name]] if name in O else [])
    e = O.new(name, None)
    e.empty_display_type = 'SPHERE'; e.empty_display_size = 2
    gameplay.objects.link(e)
    e.location = loc
EXPORT_TYPES = ("MESH", "EMPTY")
for o in O:
    if o is root or o.type not in EXPORT_TYPES or o.parent is not None: continue
    o.parent = root  # root is at the origin, so world transforms are unchanged

# anything left with a decorative name: make it a plain identifier for the FBX
for o in O:
    if o.type in EXPORT_TYPES:
        o.name = o.name.replace(" • ", "_").replace(" ", "_").replace(".", "_")

# Slots no face uses (the animated file's booleans left empty ones) would import as Unity's
# default material; drop them and renumber.
for o in O:
    if o.type != 'MESH' or not o.data.materials: continue
    used = {p.material_index for p in o.data.polygons}
    keep = [i for i in range(len(o.data.materials)) if i in used]
    if len(keep) == len(o.data.materials): continue
    renum = {old: new for new, old in enumerate(keep)}
    for p in o.data.polygons: p.material_index = renum[p.material_index]
    for i in reversed(range(len(o.data.materials))):
        if i not in used: o.data.materials.pop(index=i)

bpy.data.orphans_purge(do_recursive=True)
for img in list(bpy.data.images):
    if img.users == 0: bpy.data.images.remove(img)


# ------------------------------------------------------------------ 6. flight-model numbers
def outline(o):
    """The boundary loop of a flat fan mesh, in world XY (the longest, should there be more)."""
    bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
    adj = {}
    for e in bm.edges:
        if not e.is_boundary: continue
        a, b = e.verts[0].index, e.verts[1].index
        adj.setdefault(a, []).append(b); adj.setdefault(b, []).append(a)
    loops, seen = [], set()
    for start in adj:
        if start in seen: continue
        loop = [start]; prev, cur = None, start; seen.add(start)
        while True:
            nxt = [n for n in adj[cur] if n != prev and n not in seen]
            if not nxt: break
            loop.append(nxt[0]); seen.add(nxt[0]); prev, cur = cur, nxt[0]
        loops.append(loop)
    loop = max(loops, key=len)
    pts = [(o.matrix_world @ bm.verts[i].co) for i in loop]
    bm.free()
    return [(p.x, p.y) for p in pts]


def inside(poly, p):
    x, y = p; ok = False
    for i in range(len(poly)):
        ax, ay = poly[i]; bx, by = poly[i - 1]
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax: ok = not ok
    return ok


def cross_t(a, b, c, d):
    """t along ab where segment ab crosses segment cd, else None."""
    r = (b[0] - a[0], b[1] - a[1]); s = (d[0] - c[0], d[1] - c[1])
    den = r[0] * s[1] - r[1] * s[0]
    if abs(den) < 1e-12: return None
    qp = (c[0] - a[0], c[1] - a[1])
    t = (qp[0] * s[1] - qp[1] * s[0]) / den
    u = (qp[0] * r[1] - qp[1] * r[0]) / den
    return t if 0 <= t <= 1 and 0 <= u <= 1 else None


def union(polys):
    """Outline of the union of overlapping simple polygons: every edge piece that lies outside
    all the other polygons, chained end to end."""
    segs = []
    for i, P in enumerate(polys):
        for k in range(len(P)):
            a, b = P[k], P[(k + 1) % len(P)]
            ts = [0.0, 1.0]
            for j, Q in enumerate(polys):
                if j == i: continue
                for l in range(len(Q)):
                    t = cross_t(a, b, Q[l], Q[(l + 1) % len(Q)])
                    if t is not None: ts.append(t)
            ts.sort()
            for t0, t1 in zip(ts, ts[1:]):
                if t1 - t0 < 1e-9: continue
                mid = (a[0] + (b[0] - a[0]) * (t0 + t1) / 2, a[1] + (b[1] - a[1]) * (t0 + t1) / 2)
                if not any(inside(Q, mid) for j, Q in enumerate(polys) if j != i):
                    segs.append(((a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0),
                                 (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1)))
    key = lambda p: (round(p[0], 4), round(p[1], 4))
    adj = {}
    for s, e in segs:
        adj.setdefault(key(s), []).append(e); adj.setdefault(key(e), []).append(s)
    loops, seen = [], set()
    for start in adj:
        if start in seen: continue
        loop, cur, prev = [], start, None
        while cur not in seen:
            seen.add(cur); loop.append(cur)
            nxt = [key(n) for n in adj[cur] if key(n) != prev and key(n) != cur]
            if not nxt: break
            prev, cur = cur, nxt[0]
        loops.append(loop)
    return max(loops, key=len)


def simplify(poly, tol):
    """Douglas–Peucker on a closed loop."""
    def dp(pts):
        if len(pts) < 3: return pts
        a, b = pts[0], pts[-1]
        dx, dy = b[0] - a[0], b[1] - a[1]; L = math.hypot(dx, dy) or 1
        best, idx = 0, 0
        for i in range(1, len(pts) - 1):
            d = abs((pts[i][0] - a[0]) * dy - (pts[i][1] - a[1]) * dx) / L
            if d > best: best, idx = d, i
        if best <= tol: return [a, b]
        return dp(pts[:idx + 1])[:-1] + dp(pts[idx:])
    far = max(range(len(poly)), key=lambda i: math.hypot(poly[i][0] - poly[0][0], poly[i][1] - poly[0][1]))
    first = dp(poly[:far + 1]); second = dp(poly[far:] + [poly[0]])
    return first[:-1] + second[:-1]


def yd(p): return (round((p[0] - tee.x) * YARDS, 1), round((p[1] - tee.y) * YARDS, 1))

if SOLID:
    tee_land = union([outline(obj(n)) for n in ("FOOTPRINT_TEE", "FOOTPRINT_APPROACH", "FOOTPRINT_PROMONTORY")])
    green_land = outline(obj("FOOTPRINT_GREEN"))
    remove(obj(n) for n in FOOTPRINTS.values())
else:
    tee_land = union([outline(obj(n)) for n in ("TERRAIN_TEE_ISLAND", "TERRAIN_BRIDGE_APPROACH", "TERRAIN_JOINT", "TERRAIN_PROMONTORY")])
    green_land = outline(obj("TERRAIN_GREEN_ISLAND"))
shores = {"tee": [yd(p) for p in simplify(tee_land, 1.2)], "green": [yd(p) for p in simplify(green_land, 1.2)]}

bunkers = []
for o in named("BUNKER_"):
    sand = next(i for i, m in enumerate(o.data.materials) if m.name == "MAT_SAND")
    vs = {v for p in o.data.polygons if p.material_index == sand for v in p.vertices}
    pts = [o.matrix_world @ o.data.vertices[i].co for i in vs]
    xs = [p.x for p in pts]; ys = [p.y for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    bunkers.append((*yd((cx, cy)), round((max(xs) - min(xs)) * YARDS, 1), round((max(ys) - min(ys)) * YARDS, 1)))
green = obj("GREEN")
gpts = [green.matrix_world @ v.co for v in green.data.vertices]
green_r = min((max(p.x for p in gpts) - min(p.x for p in gpts)), (max(p.y for p in gpts) - min(p.y for p in gpts))) / 2

print("=== Course.Cliffside hole 12 (yards from the tee marker; paste into Hole.cs)")
print(f"tee {yd((tee.x, tee.y))}  pin {yd((cup.x, cup.y))}  green radius ≈ {green_r * YARDS:.1f} yd (a {green_r * 2:.0f} m green)")
print("Hazards: " + ", ".join(f"Bunker({x}, {d}, {w}, {l})" for x, d, w, l in bunkers))
for name, poly in shores.items():
    print(f"{name} ({len(poly)} points): " + ", ".join(f"P({x}, {d})" for x, d in poly))

# ------------------------------------------------------------------ 7. save, export, preview
os.makedirs(os.path.dirname(BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND, compress=True)
exported = [o for o in O if o.type in EXPORT_TYPES]
for o in O: o.select_set(o in exported)
bpy.context.view_layer.objects.active = root
with bpy.context.temp_override(selected_objects=exported, active_object=root, object=root):
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={'EMPTY', 'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP', embed_textures=False)
print(f"saved {BLEND}\nexported {FBX} ({os.path.getsize(FBX) // 1024} KB, {len(exported)} objects)")

if "--render" in ARGS:
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    sc.render.resolution_percentage = 60
    sc.cycles.samples = 16
    for cam, out in (("CAM_Presentation", PREVIEW), ("CAM_Green", PREVIEW.replace("overview", "green"))):
        sc.camera = obj(cam)
        sc.render.filepath = out
        bpy.ops.render.render(write_still=True)
        print(f"rendered {out}")
