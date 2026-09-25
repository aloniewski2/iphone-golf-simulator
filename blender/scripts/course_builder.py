"""Build a Cliffside hole from a design module, with the same parts Hole 7 is made of.

    Blender -b --factory-startup --python blender/scripts/course_builder.py -- hole13_spiral_design [--no-render]

The design module (hole13_spiral_design.py and friends) holds the numbers — islands, height
field, fairway centreline, greens, bunkers, trees, bridges, stairs, landmarks — and this script
turns them into the course: the same terrain-and-cliff construction as Hole 7's phase2_5 (steep
inner slopes shade as rock, so terraces and crater walls read as cliffs), its turf surfaces and
bunker lips, sea, shallows and foam round every island, its trees and rocks (built by phase6 and
phase7's own asset code, then merged into one mesh each so the phone draws them in a few calls),
boardwalks, stairs, a lighthouse and a ruined tower. It saves blender/hole_NN.blend, exports
Unity/Assets/Resources/Course/hole_NN.fbx with MARKER_TEE/PIN/UP for HoleView, renders the hole's
card (hole_NN_card.jpg) and writes the flight-model numbers for Course.Cliffside to
blender/holes_mock/hole_NN_numbers.txt.
"""
import bpy, bmesh, math, os, random, sys, importlib, subprocess
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
YARDS = 1.0936

bpy.ops.wm.read_factory_settings(use_empty=True)
import hole07_lib as H
import hole07_design as H7          # offset_outline, band_object (pure helpers)
D = importlib.import_module(ARGS[0])
import course_extras as X                 # themes, pools, rivers, falls, the new landmarks and plants
N = D.NUMBER
H.BLEND_PATH = os.path.join(REPO, "blender", f"hole_{N:02d}.blend")
FBX = os.path.join(REPO, "Unity", "Assets", "Resources", "Course", f"hole_{N:02d}.fbx")
CARD = os.path.join(REPO, "Unity", "Assets", "Resources", "Course", f"hole_{N:02d}_card.jpg")
NUMBERS = os.path.join(REPO, "blender", "holes_mock", f"hole_{N:02d}_numbers.txt")
height = D.height
rnd = random.Random(D.SEED)

sc = bpy.context.scene
sc.name = f"HOLE_{N:02d}"
for c in H.COLLECTIONS:
    H.get_collection(c)
root = H.get_root()

# ---------------------------------------------------------------- materials (Hole 7's palette)
M = {name: H.get_material(name, H.rgb(*c), **kw) for name, c, kw in [
    ("MAT_ROUGH", (62, 150, 40), {}), ("MAT_FAIRWAY", (118, 205, 58), {}), ("MAT_GREEN", (150, 224, 70), {}),
    ("MAT_FAIRWAY_STRIPE", (100, 190, 50), {}), ("MAT_FIRSTCUT", (90, 180, 48), {}), ("MAT_BUNKER_LIP", (160, 226, 84), {}),
    ("MAT_SAND", (236, 214, 160), {}), ("MAT_WATER", (18, 78, 178), {"roughness": 0.15}),
    ("MAT_WATER_SHALLOW", (46, 150, 222), {"roughness": 0.15}), ("MAT_FOAM", (222, 242, 250), {"roughness": 0.6}),
    ("MAT_CLIFF", (112, 116, 124), {}), ("MAT_CLIFF_DARK", (86, 90, 98), {}), ("MAT_WOOD", (112, 74, 46), {}),
    ("MAT_WOOD_LIGHT", (199, 151, 94), {}), ("MAT_ROCK", (132, 134, 140), {}), ("MAT_SLATE", (66, 97, 112), {}),
    ("MAT_CHALK", (240, 239, 220), {}), ("MAT_FLAG", (230, 40, 40), {}), ("MAT_GLASS", (150, 205, 235), {"roughness": 0.2}),
    ("MAT_STONE", (150, 146, 140), {}), ("MAT_ROOF", (64, 58, 60), {}),
]}
X.materials(H, M)
TH = X.theme(D)


# ---------------------------------------------------------------- islands: terrain and cliffs
def island_outline(ctrl, seed):
    r = random.Random(seed)
    out = H.resample(H.chaikin([Vector(p) for p in ctrl], 2), 5.0)
    c = sum(out, Vector((0, 0))) / len(out)
    out = [p + (p - c).normalized() * r.uniform(-1.5, 1.5) for p in out]
    if H.signed_area(out) < 0:
        out.reverse()
    return out


def build_island(name, outline, seed):
    r = random.Random(seed)
    verts, faces = H.fill_polygon(outline, D.GRID, height, 0.0, margin=2.0)
    ob = H.new_mesh_object(name, "COURSE")
    me = ob.data
    me.from_pydata(verts, [], faces); me.validate(); me.update()
    bm = bmesh.new(); bm.from_mesh(me)
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    bm.normal_update()
    top_faces = list(bm.faces)
    boundary = [e for e in bm.edges if e.is_boundary]
    nxt = {}
    for e in boundary:
        f = e.link_faces[0]
        loop = [l for l in f.loops if l.edge == e][0]
        nxt[loop.vert] = loop.link_loop_next.vert
    start = boundary[0].verts[0]
    ring = [start]; v = nxt[start]
    while v != start and len(ring) < len(boundary) + 2:
        ring.append(v); v = nxt[v]
    center = sum((v.co.xy for v in ring), Vector((0, 0))) / len(ring)
    n = len(ring)
    normals = []
    for i, tv in enumerate(ring):
        t = (ring[(i + 1) % n].co.xy - ring[i - 1].co.xy).normalized()
        nrm = Vector((t.y, -t.x))
        if nrm.dot(tv.co.xy - center) < 0: nrm = -nrm
        normals.append(nrm)
    bulge = lambda i, k: 0.55 * math.sin(i * 0.61 + k * 1.7) + 0.45 * math.sin(i * 0.17 + k * 0.9) + 0.3 * math.sin(i * 1.9 + k)
    rings = [ring]
    for k, (frac, base, amp, zj) in enumerate([(0.965, 0.9, 0.3, 0.0), (0.84, 3.2, 1.8, 1.0), (0.64, 4.0, 2.6, 1.2),
                                               (0.44, 3.6, 2.8, 1.2), (0.24, 4.8, 2.4, 1.0), (None, 6.5, 2.0, 0.0)], 1):
        new = []
        for i, tv in enumerate(ring):
            push = max(0.3, base + amp * bulge(i, k) + r.uniform(-0.5, 0.5))
            z = tv.co.z * frac + r.uniform(-zj, zj) if frac is not None else -6.0
            new.append(bm.verts.new((tv.co.x + normals[i].x * push, tv.co.y + normals[i].y * push, z)))
        rings.append(new)
    cliff = []
    for r0, r1 in zip(rings[:-1], rings[1:]):
        for i in range(n):
            try: cliff.append(bm.faces.new((r0[i], r0[(i + 1) % n], r1[(i + 1) % n], r1[i])))
            except ValueError: pass
    try: cliff.append(bm.faces.new(list(reversed(rings[-1]))))
    except ValueError: pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    lip = set(rings[0]) | set(rings[1]); lower = set(v for rr in rings[4:] for v in rr)
    steep_faces, lip_faces = [], []
    for f in top_faces:
        steep = f.normal.z < 0.62            # a terrace wall or a crater side: rock, not grass
        f.material_index = 1 if steep else 0
        f.smooth = not steep
        if steep: steep_faces.append(f)
    for f in cliff:
        f.smooth = False
        f.material_index = 0 if all(v in lip for v in f.verts) else 2 if all(v in lower for v in f.verts) else 1
        if f.material_index == 0: f.smooth = True; lip_faces.append(f)
    # the theme's ground and cliffs (Hole 7's grass and grey rock without one)
    X.island_surfaces(D, me, M, {"lip": lip_faces, "cliff": cliff, "steep": steep_faces, "top": top_faces})
    bm.to_mesh(me); bm.free(); me.update()
    return ob


outlines = [island_outline(ctrl, D.SEED * 10 + i) for i, ctrl in enumerate(D.ISLANDS)]
for i, o in enumerate(outlines):
    build_island(f"TERRAIN_ISLAND_{i + 1}", o, D.SEED * 20 + i)


# ---------------------------------------------------------------- turf
def fairway_outline(extra=0.0):
    cl_pts = [(x, y) for x, y, _w in D.FAIRWAY_CL]
    cl = H.catmull_rom(cl_pts, 10)
    nseg = len(D.FAIRWAY_CL) - 1
    left, right = [], []
    for i, p in enumerate(cl):
        t = i / (len(cl) - 1) * nseg; k = min(int(t), nseg - 1); f = t - k
        w0 = D.FAIRWAY_CL[k][2] * (1 - f) + D.FAIRWAY_CL[k + 1][2] * f
        tan = (cl[min(len(cl) - 1, i + 1)] - cl[max(0, i - 1)]).normalized()
        nrm = Vector((-tan.y, tan.x))
        end_t = min(i, len(cl) - 1 - i) / 6.0
        w = (w0 + extra) * (math.sin(min(1.0, end_t) * math.pi / 2) * 0.9 + 0.1)
        left.append(p + nrm * w); right.append(p - nrm * w)
    return H.chaikin(left + right[::-1], 1), cl


fair_keep = None
if D.FAIRWAY_CL:
    fo, cl = fairway_outline()
    fair = H.surface_object("FAIRWAY", "COURSE", fo, 4.0, height, 0.14, M["MAT_FAIRWAY"])
    fair.data.materials.append(M["MAT_FAIRWAY_STRIPE"])
    for poly in fair.data.polygons:
        c = poly.center
        bi = min(range(len(cl)), key=lambda j: (cl[j].x - c.x) ** 2 + (cl[j].y - c.y) ** 2)
        poly.material_index = int(bi / len(cl) * 9) % 2
    H.surface_object("FAIRWAY_FIRSTCUT", "COURSE", fairway_outline(5.0)[0], 5.0, height, 0.08, M["MAT_FIRSTCUT"])
    fair_keep = fairway_outline(5.0)[0]

green_outs = []
for i, g in enumerate(D.GREENS):
    if "outline" in g:
        out = H.chaikin([Vector(p) for p in g["outline"]], 1)
        apron = H7.offset_outline(out, 4.0)
    else:
        out = H.chaikin(H.ellipse(g["cx"], g["cy"], g["rx"], g["ry"], g["rot"], 40, wobble=0.04, seed=3), 1)
        apron = H.chaikin(H.ellipse(g["cx"], g["cy"], g["rx"] + 5, g["ry"] + 5, g["rot"], 40, wobble=0.04, seed=3), 1)
    H.surface_object(f"GREEN_APRON_{i + 1}", "COURSE", apron, 3.0, height, 0.18, M["MAT_FAIRWAY"])
    H.surface_object(f"GREEN_{i + 1}", "COURSE", out, 1.5, height, 0.26, M["MAT_GREEN"])
    green_outs.append(out)

# the terraces' risers, a darker cut so the steps read from the tee
for i, (ry, _rise) in enumerate(getattr(D, "TIERS", [])):
    xs = [p.x for p in green_outs[0]]
    x0, x1 = min(xs) + 2, max(xs) - 2
    rr = getattr(D, "RISER", 3.0) / 2
    strip = [Vector((x0, ry - rr)), Vector((x1, ry - rr)), Vector((x1, ry + rr)), Vector((x0, ry + rr))]
    H.surface_object(f"GREEN_RISER_{i + 1}", "COURSE", strip, 1.0, height, 0.29, M["MAT_FIRSTCUT"])

T = D.TEE
tee_out = H.chaikin(H.ellipse(T["cx"], T["cy"], T["rx"], T["ry"], T["rot"], 24), 1)
H.surface_object("TEE_BOX", "COURSE", tee_out, 2.5, height, 0.24, M["MAT_GREEN"])


def bunker_lip(name, outline, sand_z, lip_z):
    inner = [Vector((p.x, p.y)) for p in outline]
    c = sum(inner, Vector((0, 0))) / len(inner)
    outer = [c + (p - c) * 1.22 for p in inner]; mid = [c + (p - c) * 1.10 for p in inner]
    ob = H.new_mesh_object(name, "COURSE"); n = len(inner)
    verts = [(p.x, p.y, height(p.x, p.y) + sand_z + 0.02) for p in inner]
    verts += [(p.x, p.y, height(p.x, p.y) + lip_z) for p in outer]
    verts += [(p.x, p.y, height(p.x, p.y) + lip_z + 0.35) for p in mid]
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, 2 * n + j, 2 * n + i)); faces.append((2 * n + i, 2 * n + j, n + j, n + i))
    H.build_mesh(ob, verts, faces, smooth=True); H.assign(ob, M["MAT_BUNKER_LIP"])


bunker_outs = []
for i, (cx, cy, rx, ry, rot, seed) in enumerate(D.BUNKERS, 1):
    out = H.blob(cx, cy, rx, ry, math.radians(rot), seed=seed, n=9, wobble=0.22, smooth=2)
    H.surface_object(f"BUNKER_{i:02d}", "COURSE", out, 1.5, height, 0.30, M[TH["sand"]])
    bunker_lip(f"BUNKER_{i:02d}_LIP", out, 0.30, 0.22)
    bunker_outs.append(out)

# ---------------------------------------------------------------- the sea
bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0))
water = bpy.context.active_object
water.name = "WATER_OCEAN"; water.scale = (4000, 4000, 1)
H.link_to(water, "ENVIRONMENT"); water.parent = root; H.assign(water, M["MAT_WATER"])
for i, o in enumerate(outlines):
    H7.band_object(f"WATER_SHALLOW_{i + 1}", H7.offset_outline(o, 2.0), H7.offset_outline(o, 20.0, wobble=6.0, freq=0.35, seed=2 + i), 0.04, M["MAT_WATER_SHALLOW"])
    H7.band_object(f"WATER_FOAM_{i + 1}", H7.offset_outline(o, 2.0), H7.offset_outline(o, 9.5, wobble=2.2, freq=0.9, seed=5 + i), 0.08, M["MAT_FOAM"])
    crest = H7.band_object(f"WATER_FOAM_CREST_{i + 1}", H7.offset_outline(o, 14.0, wobble=3.0, freq=0.6, seed=9 + i),
                           H7.offset_outline(o, 15.6, wobble=3.0, freq=0.6, seed=9 + i), 0.09, M["MAT_FOAM"])
    bm = bmesh.new(); bm.from_mesh(crest.data); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if (f.index // 9) % 3 == 1], context='FACES')
    bm.to_mesh(crest.data); bm.free()


# ---------------------------------------------------------------- water, lava and ice inland; falls
X.pools_and_rivers(globals())

# ---------------------------------------------------------------- Hole 7's tree and rock models
def run_part(script, stop):
    src = open(os.path.join(HERE, script)).read()
    src = src[:src.index(stop)]
    ns = {"__file__": os.path.join(HERE, script), "__name__": "part"}
    exec(compile(src, script, "exec"), ns)
    return ns


tree_part = run_part("phase6_trees.py", "# ------------------------------------------------------------------ placement")
trees = tree_part["assets"]
trees.update(X.plant_assets(H, M, tree_part["make_asset"]))   # palms, cacti, snowy pines…
rocks = run_part("phase7_rocks.py", 'col = sub_collection("ROCKS")')["rocks"]

# A course's own look: the design's PALETTE recolours any material by name (turf, cliffs,
# trees, rocks, water) for the card render. The game colours by the same names from the
# hole's Theme (HoleView.Themes), so keep the two in step.
for _name, _rgb in getattr(D, "PALETTE", {}).items():
    _m = bpy.data.materials.get(_name)
    if _m is None: continue
    _c = H.rgb(*_rgb)
    _m.diffuse_color = _c
    if _m.use_nodes:
        for _n in _m.node_tree.nodes:
            if _n.type == 'BSDF_PRINCIPLED': _n.inputs["Base Color"].default_value = _c
root = H.get_root()
H.BLEND_PATH = os.path.join(REPO, "blender", f"hole_{N:02d}.blend")   # (their reload of the library reset it to Hole 7's)


def merged(name, col, items):
    """One mesh from many placed copies of the asset meshes: (asset object, location, rotation z, scale)."""
    bm = bmesh.new()
    mats, index = [], {}
    for src, loc, rz, s in items:
        part = bmesh.new(); part.from_mesh(src.data)
        remap = []
        for m in src.data.materials:
            if m.name not in index: index[m.name] = len(mats); mats.append(m)
            remap.append(index[m.name])
        for f in part.faces: f.material_index = remap[f.material_index] if remap else 0
        bmesh.ops.scale(part, vec=s, verts=part.verts)
        bmesh.ops.rotate(part, verts=part.verts, cent=(0, 0, 0), matrix=__import__("mathutils").Matrix.Rotation(rz, 3, 'Z'))
        bmesh.ops.translate(part, vec=loc, verts=part.verts)
        me = bpy.data.meshes.new("tmp"); part.to_mesh(me); part.free()
        bm.from_mesh(me); bpy.data.meshes.remove(me)
    ob = H.new_mesh_object(name, col)
    for m in mats: ob.data.materials.append(m)
    bm.to_mesh(ob.data); bm.free()
    for p in ob.data.polygons: p.use_smooth = False
    return ob


# ---------------------------------------------------------------- structures
def box(bm, c, size, rz=0.0):
    r = bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=size, verts=r["verts"])
    bmesh.ops.rotate(bm, verts=r["verts"], cent=(0, 0, 0), matrix=__import__("mathutils").Matrix.Rotation(rz, 3, 'Z'))
    bmesh.ops.translate(bm, vec=c, verts=r["verts"])
    return r["verts"]


def solid(name, col, parts):
    """parts: [(material name, builder(bm))] into one object with a slot per material."""
    ob = H.new_mesh_object(name, col)
    bm = bmesh.new()
    for slot, (mat, build) in enumerate(parts):
        before = set(bm.faces)
        build(bm)
        for f in bm.faces:
            if f not in before: f.material_index = slot; f.smooth = False
        ob.data.materials.append(M[mat])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(ob.data); bm.free()
    return ob


def cyl(bm, x, y, z0, h, r0, r1, segs=10):
    ring0 = [bm.verts.new((x + math.cos(a) * r0, y + math.sin(a) * r0, z0)) for a in [math.tau * i / segs for i in range(segs)]]
    if r1 <= 0:
        top = bm.verts.new((x, y, z0 + h))
        for i in range(segs): bm.faces.new((ring0[i], ring0[(i + 1) % segs], top))
    else:
        ring1 = [bm.verts.new((x + math.cos(a) * r1, y + math.sin(a) * r1, z0 + h)) for a in [math.tau * i / segs for i in range(segs)]]
        for i in range(segs): bm.faces.new((ring0[i], ring0[(i + 1) % segs], ring1[(i + 1) % segs], ring1[i]))
        bm.faces.new(ring1)
    bm.faces.new(list(reversed(ring0)))


keep_out = []   # (point, radius) where no tree or rock goes

for i, (a, b, width, *style) in enumerate(D.STAIRS):
    a, b = Vector(a), Vector(b)
    tread_mat, rail_mat = ("MAT_STONE", "MAT_ROCK") if style and style[0] == "stone" else ("MAT_WOOD_LIGHT", "MAT_WOOD")
    za, zb = height(a.x, a.y), height(b.x, b.y)
    run = (b - a); steps = max(4, int(max(run.length, abs(zb - za) * 1.3) / 0.9))
    rz = math.atan2(run.y, run.x) - math.pi / 2
    def treads(bm, a=a, run=run, za=za, zb=zb, steps=steps, rz=rz, width=width):
        for k in range(steps):
            t = (k + 0.5) / steps; p = a + run * t
            z = za + (zb - za) * t
            box(bm, (p.x, p.y, z + 0.1), (width, run.length / steps + 0.1, 0.35), rz)
    def rails(bm, a=a, run=run, za=za, zb=zb, steps=steps, rz=rz, width=width):
        side = Vector((-run.y, run.x)).normalized() * (width / 2)
        for k in range(0, steps + 1, 2):
            t = k / steps; p = a + run * t; z = za + (zb - za) * t
            for sgn in (1, -1): box(bm, (p.x + side.x * sgn, p.y + side.y * sgn, z + 0.6), (0.2, 0.2, 1.2))
    solid(f"STAIRS_{i + 1}", "STRUCTURES", [(tread_mat, treads), (rail_mat, rails)])
    for k in range(6): keep_out.append((a + run * (k / 5), width + 3))

for i, pts in enumerate(D.BRIDGES):
    def deck(bm, pts=pts):
        for (x0, y0, z0), (x1, y1, z1) in zip(pts, pts[1:]):
            L = math.hypot(x1 - x0, y1 - y0); n = max(2, int(L / 0.9)); rz = math.atan2(y1 - y0, x1 - x0) - math.pi / 2
            for k in range(n):
                t = (k + 0.5) / n
                box(bm, (x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, z0 + (z1 - z0) * t), (4.2, L / n - 0.12, 0.3), rz)
    def frame(bm, pts=pts):
        for (x0, y0, z0), (x1, y1, z1) in zip(pts, pts[1:]):
            L = math.hypot(x1 - x0, y1 - y0); n = max(2, int(L / 6)); rz = math.atan2(y1 - y0, x1 - x0)
            nx, ny = -(y1 - y0) / L, (x1 - x0) / L
            for k in range(n + 1):
                t = k / n; x, y, z = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, z0 + (z1 - z0) * t
                for s in (2.2, -2.2):
                    box(bm, (x + nx * s, y + ny * s, (z - 3) / 2 + 0.6), (0.45, 0.45, z + 3 + 1.2))   # post, sea bed to rail
            for s in (2.2, -2.2):
                box(bm, ((x0 + x1) / 2 + nx * s, (y0 + y1) / 2 + ny * s, (z0 + z1) / 2 + 1.1), (L, 0.18, 0.18), rz)
    solid(f"BRIDGE_{i + 1}", "STRUCTURES", [("MAT_WOOD_LIGHT", deck), ("MAT_WOOD", frame)])
    for (x, y, _z) in pts: keep_out.append((Vector((x, y)), 8))

for i, (kind, x, y, *more) in enumerate(D.LANDMARKS):
    z = height(x, y) - 0.5
    if kind not in ("LIGHTHOUSE", "TOWER"):
        r = X.landmark(globals(), kind, x, y, *more)
        if r: keep_out.append((Vector((x, y)), r))
        continue
    if kind == "LIGHTHOUSE":
        solid("LIGHTHOUSE", "STRUCTURES", [
            ("MAT_CHALK", lambda bm: (cyl(bm, x, y, z, 7, 3.4, 3.0), cyl(bm, x, y, z + 11, 4, 2.7, 2.5))),
            ("MAT_FLAG", lambda bm: (cyl(bm, x, y, z + 7, 4, 3.0, 2.7), cyl(bm, x, y, z + 19.2, 3.2, 2.6, 0))),
            ("MAT_SLATE", lambda bm: cyl(bm, x, y, z + 15, 0.6, 3.3, 3.3, 12)),
            ("MAT_GLASS", lambda bm: cyl(bm, x, y, z + 15.6, 3.6, 2.0, 2.0, 8)),
        ])
    elif kind == "TOWER":
        def crenels(bm):
            for k in range(8):
                a = math.tau * k / 8
                box(bm, (x + math.cos(a) * 3.2, y + math.sin(a) * 3.2, z + 12.6), (1.3, 1.3, 1.4), a)
        solid("TOWER", "STRUCTURES", [
            ("MAT_ROCK", lambda bm: (cyl(bm, x, y, z, 12, 4.2, 3.6, 9), cyl(bm, x + 5, y - 3, z, 5, 2.6, 2.2, 7))),
            ("MAT_STONE", crenels),
            ("MAT_SLATE", lambda bm: cyl(bm, x, y, z + 12, 7.5, 3.9, 0, 9)),
        ])
    keep_out.append((Vector((x, y)), 9))

# ---------------------------------------------------------------- trees and rocks
greens_keep = [H7.offset_outline(g, 7.0) for g in green_outs]
tee_keep = H.chaikin(H.ellipse(T["cx"], T["cy"], T["rx"] + 7, T["ry"] + 7, T["rot"], 24), 1)
bunker_keep = [[c + (p - c) * 1.5 for c in [sum(b, Vector((0, 0))) / len(b)] for p in b] for b in bunker_outs]


def on_land(p, margin):
    return any(H.point_in_poly(p, o) and H.dist_to_poly(p, o) > margin for o in outlines)


def blocked(p, margin=4.0):
    if not on_land(p, margin): return True
    if fair_keep and H.point_in_poly(p, fair_keep): return True
    if any(H.point_in_poly(p, g) for g in greens_keep) or H.point_in_poly(p, tee_keep): return True
    if any(H.point_in_poly(p, b) for b in bunker_keep): return True
    if any(H.point_in_poly(p, w) for w in X.WET): return True
    return any((p - q).length < r for q, r in keep_out)


xs = [p.x for o in outlines for p in o]; ys = [p.y for o in outlines for p in o]
placed = []
for count, kind, min_d, region in D.TREES:
    for _ in range(count):
        for _try in range(300):
            p = Vector((rnd.uniform(min(xs), max(xs)), rnd.uniform(min(ys), max(ys))))
            if region and not region(p.x, p.y): continue
            if blocked(p) or any((q - p).length < min_d for q, _k in placed): continue
            placed.append((p, kind)); break
merged("TREES", "ENVIRONMENT", [(trees[k], (p.x, p.y, height(p.x, p.y) - 0.35), rnd.uniform(0, math.tau), [rnd.uniform(0.85, 1.18)] * 2 + [rnd.uniform(0.9, 1.25)]) for p, k in placed])

rock_items = []
for o in outlines:
    n = len(o); c = sum(o, Vector((0, 0))) / n
    for i in range(0, n, 3):
        if rnd.random() > 0.45: continue
        t = (o[(i + 1) % n] - o[i - 1]).normalized(); nrm = Vector((t.y, -t.x))
        if nrm.dot(o[i] - c) < 0: nrm = -nrm
        q = o[i] + nrm * rnd.uniform(7, 13); s = rnd.uniform(0.6, 1.2)
        rock_items.append((rocks["CLIFF_ROCK"], (q.x, q.y, rnd.uniform(-4, -1)), rnd.uniform(0, math.tau), (s, s, s)))
        if rnd.random() < 0.5:
            q2 = q + nrm * rnd.uniform(5, 9); s2 = rnd.uniform(0.5, 0.9)
            rock_items.append((rocks["ROCK_LARGE"], (q2.x, q2.y, rnd.uniform(-1.5, 0.3)), rnd.uniform(0, math.tau), (s2, s2, s2)))
for _ in range(40):
    p = Vector((rnd.uniform(min(xs), max(xs)), rnd.uniform(min(ys), max(ys))))
    if blocked(p, 3.0): continue
    kind = rnd.choice(["ROCK_SMALL", "ROCK_MEDIUM", "ROCK_MEDIUM", "ROCK_LARGE"]); s = rnd.uniform(0.7, 1.2)
    rock_items.append((rocks[kind], (p.x, p.y, height(p.x, p.y) - 0.4 * s), rnd.uniform(0, math.tau), (s, s, s)))
rocks_ob = merged("ROCKS", "ENVIRONMENT", rock_items)
if "rock" in TH:   # red rock in the desert, basalt on the volcano
    for k in range(len(rocks_ob.data.materials)): rocks_ob.data.materials[k] = M[TH["rock"]]
if getattr(D, "ICICLES", False):
    X.icicles(globals(), outlines)

# the asset library served its purpose: nothing of it is exported
for o in list(bpy.data.collections["ASSET_LIBRARY"].objects):
    bpy.data.objects.remove(o, do_unlink=True)

# ---------------------------------------------------------------- markers
tx, ty = D.TEE_MARKER
tz = height(tx, ty) + 0.24
pz = height(*D.PIN) + 0.26
for name, loc in (("MARKER_TEE", (tx, ty, tz)), ("MARKER_PIN", (D.PIN[0], D.PIN[1], pz)), ("MARKER_UP", (tx, ty, tz + 50))):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'SPHERE'; e.empty_display_size = 2
    sc.collection.objects.link(e); H.link_to(e, "GAMEPLAY"); e.parent = root; e.location = loc


# ---------------------------------------------------------------- the flight model's numbers
def yd(p): return (round((p[0] - tx) * YARDS, 1), round((p[1] - ty) * YARDS, 1))


def simplify(poly, tol):
    def dp(pts):
        if len(pts) < 3: return pts
        a, b = pts[0], pts[-1]; dx, dy = b[0] - a[0], b[1] - a[1]; L = math.hypot(dx, dy) or 1
        best, idx = 0, 0
        for i in range(1, len(pts) - 1):
            d = abs((pts[i][0] - a[0]) * dy - (pts[i][1] - a[1]) * dx) / L
            if d > best: best, idx = d, i
        return [a, b] if best <= tol else dp(pts[:idx + 1])[:-1] + dp(pts[idx:])
    far = max(range(len(poly)), key=lambda i: math.hypot(poly[i][0] - poly[0][0], poly[i][1] - poly[0][1]))
    return dp(poly[:far + 1])[:-1] + dp(poly[far:] + [poly[0]])[:-1]


P = lambda pts: ", ".join(f"P({x}, {d})" for x, d in pts)
centre = [yd((tx, ty))] + [yd((x, y)) for x, y, _w in D.FAIRWAY_CL] + [yd(D.PIN)]
widths = sorted(w for _x, _y, w in D.FAIRWAY_CL) or [18]
g0 = D.GREENS[0]
green_r = getattr(D, "GREEN_RADIUS_YD", None) or round(min(g0["rx"], g0["ry"]) * YARDS + 2)
shores = [[yd(p) for p in simplify([(q.x, q.y) for q in o], 1.5)] for o in outlines]
lines = [
    f"                new Hole",
    f"                {{",
    f"                    Number = {N}, Par = {D.PAR}, Name = \"{D.NAME}\",",
    f"                    Blurb = \"{D.BLURB}\",",
    f"                    Centerline = new[] {{ {P(centre)} }},",
    f"                    FairwayWidth = {round(2 * widths[len(widths) // 2] * YARDS)}, GreenRadius = {green_r},",
    f"                    RoughWidth = 100,",
    f"                    Hazards = new[] {{ " + ", ".join([f"Bunker({yd((cx, cy))[0]}, {yd((cx, cy))[1]}, {round(2 * rx * YARDS, 1)}, {round(2 * ry * YARDS, 1)})" for cx, cy, rx, ry, _r, _s in D.BUNKERS] + X.hazards(D, yd, YARDS)) + " },",
    f"                    Shore = new[] {{ {P(shores[0])} }},",
]
if len(shores) > 1:
    lines.append("                    Islets = new[] { " + ", ".join(f"new[] {{ {P(s)} }}" for s in shores[1:]) + " },")
lines.append("                },")
os.makedirs(os.path.dirname(NUMBERS), exist_ok=True)
open(NUMBERS, "w").write("\n".join(lines) + "\n")
print("\n".join(lines))
print(f"tee→pin {math.hypot(D.PIN[0] - tx, D.PIN[1] - ty) * YARDS:.0f} yd")

# ---------------------------------------------------------------- save, export
for o in bpy.data.objects:
    if o is not root and o.parent is None and o.type in ("MESH", "EMPTY"): o.parent = root
H.BLEND_PATH = os.path.join(REPO, "blender", f"hole_{N:02d}.blend")
assert "hole_07" not in H.BLEND_PATH
H.save()
exported = [o for o in bpy.data.objects if o.type in ("MESH", "EMPTY")]
for o in bpy.data.objects: o.select_set(o in exported)
with bpy.context.temp_override(selected_objects=exported, active_object=root, object=root):
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={'EMPTY', 'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP', embed_textures=False)
print(f"exported {FBX} ({os.path.getsize(FBX) // 1024} KB, {len(exported)} objects, {len(placed)} trees)")

# ---------------------------------------------------------------- the card: the hole from the air
if "--no-render" not in ARGS:
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    # ortho_scale spans the width; the island's length is foreshortened by the tilt
    span = max(max(xs) - min(xs), (max(ys) - min(ys)) * math.cos(math.radians(48)) * 1.5)
    cam = bpy.data.objects.new("CAM_CARD", bpy.data.cameras.new("CAM_CARD")); sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = span * 1.12; cam.data.clip_end = 5000
    tilt, turn = math.radians(48), math.radians(14)
    back = Vector((math.sin(turn), -math.cos(turn), 0)) * math.sin(tilt) * 900 + Vector((0, 0, math.cos(tilt) * 900))
    cam.location = Vector((cx, cy, 20)) + back
    cam.rotation_euler = (tilt, 0, turn)
    sun = bpy.data.objects.new("SUN", bpy.data.lights.new("SUN", 'SUN')); sc.collection.objects.link(sun)
    sun.data.energy = 4.0; sun.data.angle = math.radians(4); sun.rotation_euler = (math.radians(48), 0, math.radians(135))
    world = bpy.data.worlds.new("World"); sc.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = H.rgb(150, 205, 245)
    sc.camera = cam
    # EEVEE where there's a GPU (the Mac); Cycles on the CPU for a headless Linux box
    sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items} else 'BLENDER_EEVEE'
    if sys.platform.startswith("linux"):
        sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 24; sc.cycles.use_denoising = False
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = 900, 600, 100
    if sys.platform == "darwin":
        png = CARD.replace(".jpg", ".png")
        sc.render.filepath = png; sc.render.image_settings.file_format = 'PNG'
        bpy.ops.render.render(write_still=True)
        subprocess.run(["sips", "-s", "format", "jpeg", "-s", "formatOptions", "88", png, "--out", CARD], capture_output=True)
        os.remove(png)
    else:   # no sips: Blender writes the JPEG itself
        sc.render.filepath = CARD; sc.render.image_settings.file_format = 'JPEG'; sc.render.image_settings.quality = 88
        bpy.ops.render.render(write_still=True)
    print(f"rendered {CARD}")
