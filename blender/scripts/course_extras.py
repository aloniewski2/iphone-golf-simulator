"""The parts holes 16-20 add to course_builder.py: themes for the ground and cliffs (volcano,
snow, desert, jungle), pools and rivers of water, lava or ice, falls down the cliffs, and the new
holes' landmarks and plants — palms, cacti, snowy pines, a windmill, a temple, a log cabin, a
rope bridge, tulip fields… Every attribute is optional: a design module without them builds as
it always did.

course_builder calls:  materials(M) once, island_theme() for the island's surfaces, then after
the turf build(B) with its own globals (helpers and all), and hazards() for the numbers.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

# the island's surfaces per theme: the ground off the fairway, the cliffs, the bunkers' sand
THEMES = {
    None:      dict(rough="MAT_ROUGH", cliff="MAT_CLIFF", cliff_dark="MAT_CLIFF_DARK", sand="MAT_SAND"),
    "volcano": dict(rough="MAT_ROUGH_ASH", cliff="MAT_BASALT", cliff_dark="MAT_BASALT_DARK", sand="MAT_SAND_BLACK", rock="MAT_BASALT"),
    "snow":    dict(rough="MAT_SNOW", cliff="MAT_CLIFF", cliff_dark="MAT_CLIFF_DARK", sand="MAT_SAND"),
    "desert":  dict(rough="MAT_DESERT", cliff="MAT_REDROCK", cliff_dark="MAT_REDROCK_DARK", sand="MAT_SAND", rock="MAT_REDROCK",
                    bands=["MAT_REDROCK", "MAT_REDROCK_ORANGE", "MAT_REDROCK_CREAM", "MAT_REDROCK_ORANGE", "MAT_REDROCK_DARK"]),
    "jungle":  dict(rough="MAT_ROUGH_JUNGLE", cliff="MAT_CLIFF", cliff_dark="MAT_CLIFF_DARK", sand="MAT_SAND", lip="MAT_MOSS"),
}

# sRGB; Unity's HoleView.Palette carries the same numbers
COLORS = {
    "MAT_ROUGH_ASH": (56, 60, 54), "MAT_BASALT": (50, 50, 56), "MAT_BASALT_DARK": (32, 32, 38), "MAT_SAND_BLACK": (88, 86, 90),
    "MAT_LAVA": (255, 116, 24), "MAT_LAVA_CRUST": (150, 46, 22), "MAT_SMOKE": (222, 222, 228), "MAT_CANVAS": (240, 230, 206),
    "MAT_SNOW": (238, 244, 250), "MAT_ICE": (170, 218, 242), "MAT_ICE_DEEP": (122, 186, 224),
    "MAT_DESERT": (228, 152, 82), "MAT_REDROCK": (198, 90, 60), "MAT_REDROCK_DARK": (160, 70, 48),
    "MAT_REDROCK_ORANGE": (228, 130, 72), "MAT_REDROCK_CREAM": (240, 208, 162),
    "MAT_ROUGH_JUNGLE": (44, 138, 42), "MAT_MOSS": (74, 152, 52),
    "MAT_PALM_FROND": (74, 170, 60), "MAT_PALM_TRUNK": (150, 112, 72), "MAT_JUNGLE_LEAF": (40, 142, 62), "MAT_COCONUT": (110, 72, 40),
    "MAT_CACTUS": (66, 152, 74), "MAT_CACTUS_DARK": (44, 118, 56), "MAT_BLOOM": (246, 110, 150), "MAT_TUFT": (178, 172, 82),
    "MAT_DEAD_WOOD": (54, 48, 46),
    "MAT_LOG": (146, 92, 52), "MAT_LOG_DARK": (106, 66, 40), "MAT_WINDOW": (255, 214, 120), "MAT_CARROT": (240, 128, 40), "MAT_COAL": (32, 32, 36),
    "MAT_RED_PAINT": (208, 54, 46), "MAT_WHITE_PAINT": (246, 246, 242), "MAT_ROOF_RED": (202, 66, 50), "MAT_HEDGE": (42, 112, 50),
    "MAT_TULIP_RED": (234, 52, 60), "MAT_TULIP_YELLOW": (250, 212, 52), "MAT_TULIP_PINK": (246, 132, 182), "MAT_TULIP_PURPLE": (152, 92, 204),
    "MAT_TULIP_LEAF": (72, 150, 60),
    "MAT_TEMPLE": (172, 166, 148), "MAT_TEMPLE_DARK": (128, 124, 110), "MAT_VINE": (62, 142, 58),
}


def materials(H, M):
    for name, c in COLORS.items():
        M[name] = H.get_material(name, H.rgb(*c))
    # the lava glows in the card's render too
    for name in ("MAT_LAVA", "MAT_WINDOW"):
        bsdf = next(n for n in M[name].node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = H.rgb(*COLORS[name])
            bsdf.inputs["Emission Strength"].default_value = 2.5 if name == "MAT_LAVA" else 1.0


def theme(D):
    return THEMES[getattr(D, "THEME", None)]


def island_surfaces(D, me, M, faces_by_kind):
    """The island's materials by theme. faces_by_kind: {'top': [...], 'lip': [...], 'cliff': [...]}
    with material_index already 0 rough / 1 cliff / 2 dark cliff; desert cliffs are banded by
    height, a jungle's lip goes mossy."""
    th = theme(D)
    me.materials.clear()
    for m in (th["rough"], th["cliff"], th["cliff_dark"]):
        me.materials.append(M[m])
    # above ROCK_ABOVE metres the ground is bare rock (a volcano's cone, a peak)
    above = getattr(D, "ROCK_ABOVE", None)
    if above is not None:
        for f in faces_by_kind["top"]:
            if f.material_index == 0 and f.calc_center_median().z > above: f.material_index = 1; f.smooth = False
    if "lip" in th:
        me.materials.append(M[th["lip"]])
        for f in faces_by_kind["lip"]:
            if f.material_index == 0: f.material_index = 3
    if "bands" in th:
        base = len(me.materials)
        for m in th["bands"]:
            me.materials.append(M[m])
        n = len(th["bands"])
        for f in faces_by_kind["cliff"] + faces_by_kind["steep"]:
            if f.material_index in (1, 2):
                z = f.calc_center_median().z
                f.material_index = base + int((z + 60.0) / 4.5) % n


# ---------------------------------------------------------------- small mesh pieces
def cone(bm, x, y, z0, h, r0, r1, segs=8, rz=0.0, tilt=None):
    """A cone or frustum up from (x, y, z0); `tilt` (axis, angle) leans it about its foot."""
    verts = []
    ring0 = [bm.verts.new((math.cos(a) * r0, math.sin(a) * r0, 0)) for a in [math.tau * i / segs + rz for i in range(segs)]]
    verts += ring0
    if r1 <= 0:
        top = bm.verts.new((0, 0, h)); verts.append(top)
        for i in range(segs): bm.faces.new((ring0[i], ring0[(i + 1) % segs], top))
    else:
        ring1 = [bm.verts.new((math.cos(a) * r1, math.sin(a) * r1, h)) for a in [math.tau * i / segs + rz for i in range(segs)]]
        verts += ring1
        for i in range(segs): bm.faces.new((ring0[i], ring0[(i + 1) % segs], ring1[(i + 1) % segs], ring1[i]))
        bm.faces.new(ring1)
    if r0 > 0: bm.faces.new(list(reversed(ring0)))
    if tilt:
        bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0), matrix=Matrix.Rotation(tilt[1], 3, tilt[0]))
    bmesh.ops.translate(bm, vec=(x, y, z0), verts=verts)
    return verts


def blob(bm, c, rx, ry, rz, seed=0, subdiv=1, rot=0.0):
    r = random.Random(seed)
    res = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    for v in res["verts"]:
        j = 1.0 + r.uniform(-0.12, 0.12)
        p = Vector((v.co.x * rx * j, v.co.y * ry * j, v.co.z * rz * j))
        p.rotate(Matrix.Rotation(rot, 3, 'Z'))
        v.co = Vector(c) + p
    return res["verts"]


def box(bm, c, size, rz=0.0, rx=0.0):
    r = bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=size, verts=r["verts"])
    if rx: bmesh.ops.rotate(bm, verts=r["verts"], cent=(0, 0, 0), matrix=Matrix.Rotation(rx, 3, 'X'))
    bmesh.ops.rotate(bm, verts=r["verts"], cent=(0, 0, 0), matrix=Matrix.Rotation(rz, 3, 'Z'))
    bmesh.ops.translate(bm, vec=c, verts=r["verts"])
    return r["verts"]


def object_from(H, name, col, M, parts, location=None):
    """parts: [(material name, builder(bm))] into one object, a material slot each. With
    `location`, the mesh is built round the origin and the object stands there (for pivots)."""
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
    if location is not None: ob.location = location
    return ob


def plume(bm, x, y, z, n, r, seed, drift=(0.5, 0.15)):
    """A column of smoke: clumps of puffs that swell and lean downwind as they rise."""
    rnd = random.Random(seed)
    zz = z
    for k in range(n):
        s = r * (1.0 + 0.22 * k)
        cx, cy = x + drift[0] * r * k * (1 + 0.15 * k), y + drift[1] * r * k
        for j in range(3 if k else 2):
            a = rnd.uniform(0, math.tau); off = s * rnd.uniform(0.25, 0.6); ss = s * rnd.uniform(0.62, 0.85)
            blob(bm, (cx + math.cos(a) * off, cy + math.sin(a) * off, zz + rnd.uniform(-0.2, 0.3) * s), ss, ss, ss * 0.85,
                 seed=seed * 31 + k * 7 + j, subdiv=2)
        zz += s * 0.95


def puffs(bm, x, y, z, n, r, seed, rise=1.0):
    rnd = random.Random(seed)
    for k in range(n):
        s = r * (1.0 - 0.1 * k) * rnd.uniform(0.85, 1.15)
        blob(bm, (x + rnd.uniform(-r, r) * 0.5 + k * 0.3, y + rnd.uniform(-r, r) * 0.5, z + k * r * 1.1 * rise), s, s, s * 0.9, seed=seed + k)


# ---------------------------------------------------------------- plants (tree-library assets)
def plant_assets(H, M, make):
    """New kinds for the TREES list, built like phase6's: make(name, builder, [materials])."""
    def palm(h, lean, fronds, seed):
        def build(bm):
            r = random.Random(seed)
            segs = 7; x = y = 0.0; z = 0.0
            for k in range(segs):
                t0, t1 = k / segs, (k + 1) / segs
                dx0, dx1 = lean * t0 * t0, lean * t1 * t1
                v = cone(bm, dx0, 0, h * t0, h / segs + 0.05, 0.62 - 0.2 * t0, 0.58 - 0.2 * t1, 7)
                for f in {f for vv in v for f in vv.link_faces}: f.material_index = 0
            top = Vector((lean, 0, h))
            for k in range(fronds):
                a = math.tau * k / fronds + r.uniform(-0.2, 0.2)
                d = Vector((math.cos(a), math.sin(a), 0))
                L = h * r.uniform(0.42, 0.52)
                for s in range(3):
                    t = (s + 0.6) / 3.2
                    p = top + d * (L * t) + Vector((0, 0, 0.8 * L * t - 1.3 * L * t * t))
                    size = L / 3.2 * (1.1 - 0.25 * s)
                    v = blob(bm, p, size * 0.75, 0.9 - 0.2 * s, 0.28, seed=seed + 7 * k + s, rot=a)
                    for f in {f for vv in v for f in vv.link_faces}: f.material_index = 1
            for k in range(3):
                a = math.tau * k / 3
                v = blob(bm, top + Vector((math.cos(a) * 0.6, math.sin(a) * 0.6, -0.5)), 0.45, 0.45, 0.45, seed=seed + k, subdiv=1)
                for f in {f for vv in v for f in vv.link_faces}: f.material_index = 2
        return build

    def jungle_bush(r, seed):
        def build(bm):
            rr = random.Random(seed)
            v = blob(bm, (0, 0, r * 0.4), r * 0.55, r * 0.55, r * 0.45, seed=seed)
            for f in {f for vv in v for f in vv.link_faces}: f.material_index = 0
            for k in range(6):
                a = math.tau * k / 6 + rr.uniform(-0.3, 0.3)
                c = (math.cos(a) * r * 0.75, math.sin(a) * r * 0.75, r * 0.45)
                v = blob(bm, c, r * 0.75, r * 0.3, r * 0.1, seed=seed + k, rot=a)
                for f in {f for vv in v for f in vv.link_faces}: f.material_index = 1 if k % 2 else 0
        return build

    def saguaro(h, seed):
        def build(bm):
            rr = random.Random(seed)
            def column(x, y, z0, hh, rad):
                v = cone(bm, x, y, z0, hh, rad, rad * 0.95, 8)
                v += cone(bm, x, y, z0 + hh, rad * 0.7, rad * 0.95, 0, 8)
                return v
            column(0, 0, 0, h, 0.9)
            for k, (side, z) in enumerate(((1, h * 0.42), (-1, h * 0.58))):
                if rr.random() < 0.15: continue
                reach = rr.uniform(1.4, 2.0)
                v = cone(bm, 0, 0, 0, reach, 0.55, 0.55, 8, tilt=('Y', side * math.pi / 2))
                bmesh.ops.translate(bm, vec=(0, 0, z), verts=v)
                column(side * reach, 0, z - 0.2, h * rr.uniform(0.28, 0.38), 0.55)
        return build

    def barrel(r, seed):
        def build(bm):
            v = blob(bm, (0, 0, r * 0.8), r, r, r * 0.95, seed=seed, subdiv=2)
            for f in {f for vv in v for f in vv.link_faces}: f.material_index = 0
            v = blob(bm, (0, 0, r * 1.72), r * 0.3, r * 0.3, r * 0.18, seed=seed + 1)
            for f in {f for vv in v for f in vv.link_faces}: f.material_index = 1
        return build

    def tuft(r, seed):
        def build(bm):
            rr = random.Random(seed)
            for k in range(7):
                a = math.tau * k / 7
                cone(bm, 0, 0, 0, r * rr.uniform(1.2, 1.8), 0.18, 0, 4, tilt=(Vector((-math.sin(a), math.cos(a), 0)), rr.uniform(0.25, 0.6)))
        return build

    def dead_tree(h, seed):
        def build(bm):
            rr = random.Random(seed)
            cone(bm, 0, 0, 0, h, 0.7, 0.18, 6)
            for k in range(4):
                a = rr.uniform(0, math.tau); z = h * rr.uniform(0.45, 0.8)
                v = cone(bm, 0, 0, 0, h * 0.4, 0.28, 0.05, 5, tilt=(Vector((-math.sin(a), math.cos(a), 0)), rr.uniform(0.6, 1.0)))
                bmesh.ops.translate(bm, vec=(0, 0, z), verts=v)
        return build

    def snow_pine(h, r, tiers):
        def build(bm):
            v = cone(bm, 0, 0, 0, h * 0.28, r * 0.13, r * 0.1, 6)
            for f in {f for vv in v for f in vv.link_faces}: f.material_index = 0
            tier_h = h * 0.78 / tiers
            for i in range(tiers):
                z0 = h * 0.22 + i * tier_h * 0.78
                rr = r * (1.0 - i / (tiers + 0.6))
                v = cone(bm, 0, 0, z0, tier_h * 1.25, rr, 0.0, 7)
                for f in {f for vv in v for f in vv.link_faces}: f.material_index = 1 + i % 2
                v = cone(bm, 0, 0, z0 + tier_h * 0.55, tier_h * 0.72, rr * 0.58, 0.0, 7)
                for f in {f for vv in v for f in vv.link_faces}: f.material_index = 3
        return build

    wood = M.get("MAT_WOOD")
    dark = H.get_material("MAT_TREE_DARK", H.rgb(34, 104, 52))
    mid = H.get_material("MAT_TREE_MID", H.rgb(52, 140, 62))
    return {
        "PALM": make("PALM", palm(15, 2.6, 8, 1), [M["MAT_PALM_TRUNK"], M["MAT_PALM_FROND"], M["MAT_COCONUT"]]),
        "PALM_TALL": make("PALM_TALL", palm(21, 4.0, 9, 2), [M["MAT_PALM_TRUNK"], M["MAT_PALM_FROND"], M["MAT_COCONUT"]]),
        "JUNGLE_BUSH": make("JUNGLE_BUSH", jungle_bush(3.4, 5), [M["MAT_JUNGLE_LEAF"], M["MAT_PALM_FROND"]]),
        "SAGUARO": make("SAGUARO", saguaro(9.5, 3), [M["MAT_CACTUS"]]),
        "SAGUARO_SMALL": make("SAGUARO_SMALL", saguaro(6.5, 7), [M["MAT_CACTUS"]]),
        "BARREL_CACTUS": make("BARREL_CACTUS", barrel(1.2, 4), [M["MAT_CACTUS_DARK"], M["MAT_BLOOM"]]),
        "DRY_TUFT": make("DRY_TUFT", tuft(0.9, 6), [M["MAT_TUFT"]]),
        "DEAD_TREE": make("DEAD_TREE", dead_tree(11, 9), [M["MAT_DEAD_WOOD"]]),
        "SNOW_PINE_LARGE": make("SNOW_PINE_LARGE", snow_pine(26, 7.8, 4), [wood, dark, mid, M["MAT_SNOW"]]),
        "SNOW_PINE_MEDIUM": make("SNOW_PINE_MEDIUM", snow_pine(18, 6.0, 3), [wood, dark, mid, M["MAT_SNOW"]]),
        "SNOW_PINE_SMALL": make("SNOW_PINE_SMALL", snow_pine(11, 4.2, 3), [wood, dark, mid, M["MAT_SNOW"]]),
    }


# ---------------------------------------------------------------- water, lava and ice
# every pool's and river's outline, so no tree or rock is planted in them
WET = []

POOL_NAMES = {"water": ("WATER_POOL", "MAT_WATER_SHALLOW"), "lava": ("LAVA_POOL", "MAT_LAVA"), "ice": ("ICE_POOL", "MAT_ICE")}


def pools_and_rivers(B):
    H, H7, D, M, height = B["H"], B["H7"], B["D"], B["M"], B["height"]
    for i, p in enumerate(getattr(D, "POOLS", [])):
        kind, z = p["kind"], p["z"]
        if "outline" in p:
            out = H.chaikin([Vector(q) for q in p["outline"]], 2)
        else:
            out = H.chaikin(H.ellipse(p["cx"], p["cy"], p["rx"], p["ry"], math.radians(p.get("rot", 0)), 32, wobble=p.get("wobble", 0.05), seed=i + 3), 1)
        prefix, mat = POOL_NAMES[kind]
        WET.append(H7.offset_outline(out, 3.0))
        H.surface_object(f"{prefix}_{i + 1}", "COURSE", out, 3.0, lambda x, y, z=z: z, 0.0, M[mat])
        # its edge: a crust round lava, pale ice at a frozen shore, a pale shallow round water
        edge = {"lava": "MAT_LAVA_CRUST", "ice": "MAT_ICE_DEEP", "water": "MAT_FOAM"}[kind]
        H7.band_object(f"{prefix}_{i + 1}_EDGE", H7.offset_outline(out, -1.2), H7.offset_outline(out, 0.6), z + 0.05, M[edge], col="COURSE")
    for i, r in enumerate(getattr(D, "RIVERS", [])):
        kind = r["kind"]
        cl = H.catmull_rom([Vector(q) for q in r["points"]], 8)
        left, right = [], []
        for k, q in enumerate(cl):
            tan = (cl[min(len(cl) - 1, k + 1)] - cl[max(0, k - 1)]).normalized()
            nrm = Vector((-tan.y, tan.x))
            w = r["width"] / 2 * (1 + 0.18 * math.sin(k * 0.7 + i))
            left.append(q + nrm * w); right.append(q - nrm * w)
        out = H.chaikin(left + right[::-1], 1)
        WET.append(out)
        prefix, mat = POOL_NAMES[kind]
        lift = r.get("lift", 0.2)
        zf = (lambda x, y, z=r["z"]: z) if "z" in r else (lambda x, y, lift=lift: height(x, y) + lift)
        H.surface_object(f"{prefix.split('_')[0]}_RIVER_{i + 1}", "COURSE", out, 1.6, zf, 0.0, M[mat])
        cut_turf(H, out)
    for i, f in enumerate(getattr(D, "FALLS", [])):
        fall(B, i, f)


def cut_turf(H, out):
    """Where a river crosses the fairway (or an apron, or the tee) the turf stops at its banks:
    the turf's coarse grid would otherwise bridge the river's channel and hide it. Each face
    by the river is split along its banks and what lies inside is taken away."""
    xs = [p[0] for p in out]; ys = [p[1] for p in out]
    for ob in [o for o in bpy.data.objects if o.type == 'MESH' and (o.name.startswith(("FAIRWAY", "GREEN_APRON")) or o.name == "TEE_BOX")]:
        if ob.matrix_world != Matrix.Identity(4):
            print(f"cut_turf: {ob.name} is not at the origin; left whole"); continue
        vs = [v.co for v in ob.data.vertices]
        if not vs or max(v.x for v in vs) < min(xs) or min(v.x for v in vs) > max(xs) or max(v.y for v in vs) < min(ys) or min(v.y for v in vs) > max(ys):
            continue
        bm = bmesh.new(); bm.from_mesh(ob.data)
        for k in range(len(out)):
            a, b = Vector(out[k][:2]), Vector(out[(k + 1) % len(out)][:2])
            lo, hi = Vector((min(a.x, b.x) - 0.2, min(a.y, b.y) - 0.2)), Vector((max(a.x, b.x) + 0.2, max(a.y, b.y) + 0.2))
            near = []
            for f in bm.faces:
                fx = [v.co.x for v in f.verts]; fy = [v.co.y for v in f.verts]
                if max(fx) >= lo.x and min(fx) <= hi.x and max(fy) >= lo.y and min(fy) <= hi.y: near.append(f)
            if not near or (b - a).length < 1e-4: continue
            edges = list({e for f in near for e in f.edges}); verts = list({v for f in near for v in f.verts})
            nrm = Vector((-(b - a).y, (b - a).x, 0)).normalized()
            bmesh.ops.bisect_plane(bm, geom=near + edges + verts, dist=1e-4, plane_co=(a.x, a.y, 0), plane_no=nrm)
        gone = [f for f in bm.faces if H.point_in_poly(f.calc_center_median().xy, out)]
        if gone:
            bmesh.ops.delete(bm, geom=gone, context='FACES')
            print(f"cut_turf: {ob.name} loses {len(gone)} faces to the river")
        bm.to_mesh(ob.data); bm.free()


def fall(B, i, f):
    """A fall down a cliff: a ribbon from its lip to the water below, leaning out as it drops,
    with spray (or, for lava, steam) where it lands."""
    H, M = B["H"], B["M"]
    kind = f["kind"]
    d = Vector(f["dir"]).normalized(); side = Vector((-d.y, d.x))
    top, bottom, w = f["top"], f["bottom"], f["width"] / 2
    steps = 10
    verts, faces = [], []
    for back in (0.0, 0.35):
        base = len(verts)
        for k in range(steps + 1):
            t = k / steps
            c = Vector((f["x"], f["y"])) + d * (f.get("lean", 3.0) * t * t + back)
            z = top + (bottom - top) * t
            ww = w * (1 + 0.35 * t)
            verts += [(c.x + side.x * ww, c.y + side.y * ww, z), (c.x - side.x * ww, c.y - side.y * ww, z)]
            if k:
                q = (base + 2 * k - 2, base + 2 * k - 1, base + 2 * k + 1, base + 2 * k)
                faces.append(q if back else tuple(reversed(q)))
    prefix, mat = {"water": ("WATER_FALL", "MAT_FOAM"), "lava": ("LAVA_FALL", "MAT_LAVA"), "ice": ("ICE_FALL", "MAT_ICE")}[kind]
    ob = H.new_mesh_object(f"{prefix}_{i + 1}", "COURSE")
    H.build_mesh(ob, verts, faces, smooth=True)
    H.assign(ob, M[mat])
    end = Vector((f["x"], f["y"])) + d * f.get("lean", 3.0)
    if kind != "ice":
        object_from(H, f"SPRAY_{i + 1}", "ENVIRONMENT", M, [
            ("MAT_FOAM" if kind == "water" else "MAT_SMOKE", lambda bm: puffs(bm, end.x, end.y, bottom + 0.8, 4, w * 1.1, 70 + i, rise=0.6))])


def hazards(D, yd, YARDS):
    """Water(x, d, width, length) for Course.Cliffside: the design's water, lava and ice (ellipses
    in metres, (cx, cy, rx, ry))."""
    out = []
    for cx, cy, rx, ry in getattr(D, "WATER_HAZARDS", []):
        x, dd = yd((cx, cy))
        out.append(f"Water({x}, {dd}, {round(2 * rx * YARDS, 1)}, {round(2 * ry * YARDS, 1)})")
    return out


# ---------------------------------------------------------------- landmarks
def landmark(B, kind, x, y, *args):
    """One of the new holes' landmarks at (x, y). Returns a keep-out radius for trees, or 0."""
    H, M, height = B["H"], B["M"], B["height"]
    z = height(x, y)
    O = lambda name, parts, **kw: object_from(H, name, "STRUCTURES", M, parts, **kw)
    n = sum(1 for o in bpy.data.objects if o.name.startswith(kind))
    tag = f"{kind}_{n + 1}"

    if kind == "SMOKE":                       # a column of smoke: (height above ground, size)
        up, size = args
        object_from(H, tag, "ENVIRONMENT", M, [("MAT_SMOKE", lambda bm: plume(bm, x, y, z + up, 5, size, 11 + n))])
        return 0
    if kind == "STEAM_VENT":
        O(tag, [("MAT_BASALT_DARK", lambda bm: cone(bm, x, y, z - 0.3, 2.4, 2.6, 0.9, 7)),
                ("MAT_SMOKE", lambda bm: plume(bm, x, y, z + 2.4, 4, 1.3, 21 + n))])
        return 6
    if kind == "ARCH_BRIDGE":                 # stone arch: (turn in degrees, span, width)
        rot, span, width = args
        a = math.radians(rot)
        d = Vector((math.cos(a), math.sin(a)))
        def deck(bm):
            steps = 12
            for k in range(steps):
                t = (k + 0.5) / steps
                c = Vector((x, y)) + d * (span * (t - 0.5))
                zz = z + 0.6 + 2.2 * math.sin(math.pi * t)
                box(bm, (c.x, c.y, zz), (span / steps + 0.1, width, 0.7), a)
        def walls(bm):
            steps = 12
            for k in range(steps):
                t = (k + 0.5) / steps
                c = Vector((x, y)) + d * (span * (t - 0.5))
                zz = z + 1.3 + 2.2 * math.sin(math.pi * t)
                for s in (1, -1):
                    o = Vector((-d.y, d.x)) * (width / 2) * s
                    box(bm, (c.x + o.x, c.y + o.y, zz), (span / steps + 0.1, 0.5, 0.9), a)
            for s in (-0.5, 0.5):
                c = Vector((x, y)) + d * (span * s)
                box(bm, (c.x, c.y, z), (1.6, width + 0.8, 2.2), a)
        O(tag, [("MAT_STONE", deck), ("MAT_ROCK", walls)])
        return span / 2 + 3
    if kind == "CABIN":                       # (turn in degrees)
        a = math.radians(args[0]); W, L, Hh = 7.0, 9.0, 4.2
        def logs(bm):
            for k in range(7):
                zz = z + 0.35 + k * 0.6
                box(bm, (x, y, zz), (W, L, 0.56), a)
        def snowroof(bm):
            for s in (1, -1):
                off = Vector((math.cos(a), math.sin(a))) * (W / 4 + 0.2) * s
                v = box(bm, (0, 0, 0), (W / 2 + 1.5, L + 1.4, 0.35))
                bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(-0.55 * s, 3, 'Y'))
                bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(a, 3, 'Z'))
                bmesh.ops.translate(bm, vec=(x + off.x, y + off.y, z + Hh + 1.45), verts=v)
        def chimney(bm):
            c = Vector((x, y)) + Vector((-math.sin(a), math.cos(a))) * (L / 3)
            box(bm, (c.x, c.y, z + Hh + 2.2), (1.1, 1.1, 3.4), a)
        def windows(bm):
            for s in (1, -1):
                c = Vector((x, y)) + Vector((math.cos(a), math.sin(a))) * (W / 2 + 0.05) * s
                box(bm, (c.x, c.y, z + 2.0), (0.12, 1.4, 1.1), a)
        O(tag, [("MAT_LOG", logs), ("MAT_SNOW", snowroof), ("MAT_ROCK", chimney), ("MAT_WINDOW", windows)])
        cc = Vector((x, y)) + Vector((-math.sin(a), math.cos(a))) * (L / 3)
        object_from(H, f"SMOKE_{tag}", "ENVIRONMENT", M, [("MAT_SMOKE", lambda bm: plume(bm, cc.x, cc.y, z + Hh + 4.4, 4, 0.8, 31 + n))])
        return 9
    if kind == "SNOWMAN":
        def body(bm):
            blob(bm, (x, y, z + 1.0), 1.25, 1.25, 1.1, seed=1, subdiv=2)
            blob(bm, (x, y, z + 2.6), 0.9, 0.9, 0.85, seed=2, subdiv=2)
            blob(bm, (x, y, z + 3.8), 0.62, 0.62, 0.6, seed=3, subdiv=2)
        O(tag, [("MAT_SNOW", body),
                ("MAT_CARROT", lambda bm: cone(bm, x, y - 0.55, z + 3.8, 0.9, 0.14, 0, 6, tilt=('X', math.pi / 2))),
                ("MAT_COAL", lambda bm: [blob(bm, (x + dx, y - 0.55, z + 4.0), 0.1, 0.1, 0.1) for dx in (-0.22, 0.22)])])
        return 3
    if kind == "WINDMILL":                    # (turn: which way the sails face)
        a = math.radians(args[0])
        face = Vector((math.cos(a), math.sin(a)))
        def base(bm): cone(bm, x, y, z - 0.5, 4.2, 9.0, 8.6, 10)
        def tower(bm): cone(bm, x, y, z + 3.6, 19.5, 7.4, 4.6, 10)
        def cap(bm): cone(bm, x, y, z + 23.0, 6.0, 5.9, 0.7, 10)
        def gallery(bm): cone(bm, x, y, z + 9.0, 0.5, 8.4, 8.2, 12)
        def door(bm): box(bm, (x + face.x * 7.9, y + face.y * 7.9, z + 1.9), (2.6, 0.5, 3.6), a + math.pi / 2)
        def windows(bm):
            for k, (t, hgt) in enumerate(((0.9, 13.0), (-0.9, 13.0), (0.0, 17.5))):
                d = Vector((math.cos(a + t), math.sin(a + t))); rr = 7.4 - (7.4 - 4.6) * (hgt - 3.6) / 19.5 + 0.05
                box(bm, (x + d.x * rr, y + d.y * rr, z + hgt), (1.2, 0.3, 1.6), a + t + math.pi / 2)
        O(tag, [("MAT_STONE", base), ("MAT_RED_PAINT", tower), ("MAT_ROOF", cap), ("MAT_WOOD", gallery), ("MAT_WOOD", door), ("MAT_SLATE", windows)])
        hub = Vector((x, y)) + face * 6.6
        R, R0, W = 16.0, 2.4, 3.4          # the arms' reach, where the sail starts, how wide it is
        def arm_bar(bm, ang, along, across, length, width, thick, y0=0.0):
            u = Vector((math.cos(ang), 0, math.sin(ang))); c = Vector((-math.sin(ang), 0, math.cos(ang)))
            v = box(bm, (0, 0, 0), (length, thick, width))
            bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(-ang, 3, 'Y'))
            p = u * along + c * across; bmesh.ops.translate(bm, vec=(p.x, y0, p.z), verts=v)
        def frame(bm):
            for k in range(4):
                ang = math.tau * k / 4 + 0.3
                arm_bar(bm, ang, R / 2, 0, R, 0.5, 0.5)                              # the stock
                for off in (0.45, W):                                                # two rails
                    arm_bar(bm, ang, (R + R0) / 2, off, R - R0, 0.3, 0.3, -0.1)
                for s in range(9):                                                   # the lattice
                    arm_bar(bm, ang, R0 + (R - R0) * s / 8, (0.45 + W) / 2, 0.28, W - 0.2, 0.26, -0.1)
            blob(bm, (0, -0.3, 0), 1.1, 1.1, 1.1, subdiv=2)
        def canvas(bm):
            for k in range(4):
                ang = math.tau * k / 4 + 0.3
                arm_bar(bm, ang, (R + R0) / 2, (0.45 + W) / 2, R - R0 - 0.4, W - 0.5, 0.08, 0.18)
        sail = object_from(H, "SAILS_SPIN", "STRUCTURES", M, [("MAT_WHITE_PAINT", frame), ("MAT_CANVAS", canvas)])
        # built round its hub facing -Y; turned to face `face` and stood at the hub
        sail.rotation_euler = (0, 0, a + math.pi / 2)
        sail.location = (hub.x, hub.y, z + 21.0)
        axis = bpy.data.objects.new("SAILS_AXIS", None)
        bpy.context.scene.collection.objects.link(axis); H.link_to(axis, "STRUCTURES")
        axis.parent = sail; axis.location = (0, -2.0, 0)
        return 15
    if kind == "FARMHOUSE":
        a = math.radians(args[0]); W, L = 8.0, 12.0
        def walls(bm): box(bm, (x, y, z + 2.4), (W, L, 4.8), a)
        def roof(bm):
            for s in (1, -1):
                off = Vector((math.cos(a), math.sin(a))) * (W / 4) * s
                v = box(bm, (0, 0, 0), (W / 2 + 1.6, L + 1.0, 0.45))
                bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(-0.75 * s, 3, 'Y'))
                bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(a, 3, 'Z'))
                bmesh.ops.translate(bm, vec=(x + off.x, y + off.y, z + 6.4), verts=v)
        def windows(bm):
            for s in (1, -1):
                for k in (-1, 1):
                    c = Vector((x, y)) + Vector((math.cos(a), math.sin(a))) * (W / 2 + 0.05) * s + Vector((-math.sin(a), math.cos(a))) * 3.0 * k
                    box(bm, (c.x, c.y, z + 2.8), (0.12, 1.5, 1.4), a)
        def chimney(bm): box(bm, (x - math.sin(a) * 3, y + math.cos(a) * 3, z + 8.0), (1.0, 1.0, 3.0), a)
        O(tag, [("MAT_WHITE_PAINT", walls), ("MAT_ROOF_RED", roof), ("MAT_SLATE", windows), ("MAT_STONE", chimney)])
        return 12
    if kind == "JETTY":                       # (turn, length)
        a = math.radians(args[0]); L = args[1]
        d = Vector((math.cos(a), math.sin(a)))
        zz = args[2] if len(args) > 2 else z
        def planks(bm):
            for k in range(int(L / 1.0)):
                c = Vector((x, y)) + d * (k + 0.5)
                box(bm, (c.x, c.y, zz + 0.5), (0.9, 3.2, 0.2), a)
        def posts(bm):
            for k in range(0, int(L) + 1, 3):
                for s in (1.4, -1.4):
                    c = Vector((x, y)) + d * k + Vector((-d.y, d.x)) * s
                    box(bm, (c.x, c.y, zz - 0.5), (0.35, 0.35, 2.4))
        O(tag, [("MAT_WOOD_LIGHT", planks), ("MAT_WOOD", posts)])
        return 0
    if kind == "BOAT":                        # (turn, water level)
        a = math.radians(args[0]); zz = args[1]
        def hull(bm):
            v = box(bm, (0, 0, 0), (1.6, 4.2, 0.8))
            for vv in v:
                if abs(vv.co.y) > 1.5: vv.co.x *= 0.35
                if vv.co.z < 0: vv.co.x *= 0.7
            bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(a, 3, 'Z'))
            bmesh.ops.translate(bm, vec=(x, y, zz + 0.3), verts=v)
        def seat(bm): box(bm, (x, y, zz + 0.62), (1.3, 0.4, 0.12), a)
        O(tag, [("MAT_RED_PAINT", hull), ("MAT_WOOD_LIGHT", seat)])
        return 0
    if kind == "WHITE_BRIDGE":                # arched white footbridge: (turn, span)
        a = math.radians(args[0]); span = args[1]
        d = Vector((math.cos(a), math.sin(a))); side = Vector((-d.y, d.x))
        def deck(bm):
            steps = 10
            for k in range(steps):
                t = (k + 0.5) / steps
                c = Vector((x, y)) + d * (span * (t - 0.5))
                box(bm, (c.x, c.y, z + 0.5 + 1.2 * math.sin(math.pi * t)), (span / steps + 0.08, 3.4, 0.3), a)
        def rails(bm):
            steps = 10
            for k in range(steps + 1):
                t = k / steps
                c = Vector((x, y)) + d * (span * (t - 0.5))
                zz = z + 0.5 + 1.2 * math.sin(math.pi * t)
                for s in (1.6, -1.6):
                    box(bm, (c.x + side.x * s, c.y + side.y * s, zz + 0.55), (0.15, 0.15, 1.1))
                if k < steps:
                    t2 = (k + 0.5) / steps; c2 = Vector((x, y)) + d * (span * (t2 - 0.5))
                    for s in (1.6, -1.6):
                        box(bm, (c2.x + side.x * s, c2.y + side.y * s, z + 0.5 + 1.2 * math.sin(math.pi * t2) + 1.1), (span / steps + 0.1, 0.14, 0.14), a)
        wood = len(args) > 2 and args[2] == "wood"
        O(tag, [("MAT_WOOD_LIGHT" if wood else "MAT_WHITE_PAINT", deck), ("MAT_WOOD" if wood else "MAT_WHITE_PAINT", rails)])
        return span / 2 + 2
    if kind == "HEDGE":                       # (turn, length)
        a = math.radians(args[0]); L = args[1]
        O(tag, [("MAT_HEDGE", lambda bm: box(bm, (x, y, z + 0.9), (L, 1.6, 1.8), a))])
        return L / 2 + 1
    if kind == "FENCE":                       # (list of (x, y) along it)
        pts = [Vector(p) for p in args[0]]
        def fence(bm):
            for p, q in zip(pts, pts[1:]):
                L = (q - p).length; n = max(1, int(L / 3.0)); rz = math.atan2(q.y - p.y, q.x - p.x)
                for k in range(n + 1):
                    c = p + (q - p) * (k / n)
                    box(bm, (c.x, c.y, height(c.x, c.y) + 0.6), (0.22, 0.22, 1.2))
                for h in (0.55, 1.05):
                    c = (p + q) / 2
                    box(bm, (c.x, c.y, height(c.x, c.y) + h), (L, 0.12, 0.12), rz)
        O(tag, [("MAT_WOOD", fence)])
        return 0
    if kind == "TULIPS":                      # a field: (width across the rows, length along them, turn)
        W, L, rot = args
        a = math.radians(rot)
        d = Vector((math.cos(a), math.sin(a))); side = Vector((-d.y, d.x))
        colors = ["MAT_TULIP_RED", "MAT_TULIP_YELLOW", "MAT_TULIP_PINK", "MAT_TULIP_PURPLE"]
        rows = int(W / 2.0)
        def row_builder(which):
            def build(bm):
                for r_ in range(rows):
                    if r_ % len(colors) != which: continue
                    c = Vector((x, y)) + side * (-W / 2 + (r_ + 0.5) * W / rows)
                    for k in range(int(L / 4)):
                        cc = c + d * (-L / 2 + (k + 0.5) * 4)
                        box(bm, (cc.x, cc.y, height(cc.x, cc.y) + 0.6), (4.1, 1.1, 0.7), a)
            return build
        def leaves(bm):
            for r_ in range(rows):
                c = Vector((x, y)) + side * (-W / 2 + (r_ + 0.5) * W / rows)
                for k in range(int(L / 4)):
                    cc = c + d * (-L / 2 + (k + 0.5) * 4)
                    box(bm, (cc.x, cc.y, height(cc.x, cc.y) + 0.2), (4.1, 1.5, 0.4), a)
        fields = sum(1 for o in bpy.data.objects if o.name.startswith("FLOWERS_"))
        object_from(H, f"FLOWERS_{fields + 1}", "ENVIRONMENT", M,
                    [(colors[k], row_builder(k)) for k in range(4)] + [("MAT_TULIP_LEAF", leaves)])
        return max(W, L) / 2 + 2
    if kind == "TEMPLE":                      # a stepped pyramid facing (turn)
        a = math.radians(args[0]); size = args[1] if len(args) > 1 else 26.0
        face = Vector((math.cos(a), math.sin(a)))
        tiers = 5
        def steps(bm):
            for k in range(tiers):
                s = size * (1 - k / (tiers + 0.8))
                box(bm, (x, y, z + 1.6 + k * 3.2), (s, s, 3.2), a)
        def stair(bm):
            for k in range(tiers * 3):
                t = k / (tiers * 3)
                reach = size / 2 * (1 - t * (tiers / (tiers + 0.8))) + 0.6
                c = Vector((x, y)) + face * reach
                box(bm, (c.x, c.y, z + 0.5 + k * 1.07), (1.4, size * 0.22, 1.07), a)
        def shrine(bm):
            box(bm, (x, y, z + tiers * 3.2 + 2.2), (size * 0.28, size * 0.28, 4.4), a)
        def door(bm):
            c = Vector((x, y)) + face * (size * 0.14 + 0.05)
            box(bm, (c.x, c.y, z + tiers * 3.2 + 1.6), (0.3, 2.2, 3.0), a)
        def vines(bm):
            rr = random.Random(n + 5)
            for k in range(22):
                t = rr.uniform(0, math.tau); tier = rr.randrange(tiers)
                s = size * (1 - tier / (tiers + 0.8)) / 2
                c = Vector((x, y)) + Vector((math.cos(t), math.sin(t))) * s * 1.02
                box(bm, (c.x, c.y, z + tier * 3.2 + 1.6), (1.2, 1.2, rr.uniform(1.4, 3.0)), t)
        O(tag, [("MAT_TEMPLE", steps), ("MAT_TEMPLE_DARK", stair), ("MAT_TEMPLE", shrine), ("MAT_COAL", door), ("MAT_VINE", vines)])
        return size / 2 + 6
    if kind == "COLUMNS":                     # ruins: (count, spread, turn)
        count, spread, rot = args
        a = math.radians(rot); d = Vector((math.cos(a), math.sin(a)))
        n = len({o.name.rsplit("_", 1)[0] for o in bpy.data.objects if o.name.startswith("RUIN_COLUMN_")})
        rr = random.Random(n + 9)
        for k in range(count):
            c = Vector((x, y)) + d * (spread * (k / max(1, count - 1) - 0.5))
            h = rr.uniform(3.0, 7.0)
            O(f"RUIN_COLUMN_{n + 1}_{k + 1}", [
                ("MAT_TEMPLE", lambda bm, c=c, h=h: cone(bm, c.x, c.y, height(c.x, c.y) - 0.2, h, 0.95, 0.85, 8)),
                ("MAT_TEMPLE_DARK", lambda bm, c=c, h=h: box(bm, (c.x, c.y, height(c.x, c.y) + h), (2.4, 2.4, 0.6))),
                ("MAT_VINE", lambda bm, c=c, h=h: box(bm, (c.x + 0.9, c.y, height(c.x, c.y) + h * 0.5), (0.3, 1.0, h * 0.6)))])
        return spread / 2 + 4
    if kind == "STONE_HEAD":                  # (turn: which way it looks)
        a = math.radians(args[0]); f = Vector((math.cos(a), math.sin(a)))
        def head(bm): box(bm, (x, y, z + 3.4), (5.2, 5.0, 7.0), a)
        def features(bm):
            c = Vector((x, y)) + f * 2.7
            box(bm, (c.x, c.y, z + 5.0), (0.8, 4.2, 0.8), a)                 # brow
            box(bm, (c.x + f.x * 0.3, c.y + f.y * 0.3, z + 3.9), (1.0, 1.1, 1.8), a)   # nose
            box(bm, (c.x, c.y, z + 2.0), (0.6, 2.8, 0.6), a)                 # mouth
        def moss(bm):
            box(bm, (x, y, z + 7.05), (5.4, 5.2, 0.35), a)
        O("RUIN_HEAD", [("MAT_TEMPLE", head), ("MAT_TEMPLE_DARK", features), ("MAT_MOSS", moss)])
        return 7
    if kind == "ROPE_BRIDGE":                 # from (x, y) to (x1, y1), at deck height (z0, z1)
        x1, y1, z0, z1 = args
        p0, p1 = Vector((x, y)), Vector((x1, y1))
        L = (p1 - p0).length; rz = math.atan2(p1.y - p0.y, p1.x - p0.x)
        side = Vector((-(p1 - p0).y, (p1 - p0).x)).normalized()
        steps = int(L / 0.9)
        sag = lambda t: z0 + (z1 - z0) * t - 2.6 * math.sin(math.pi * t)
        def planks(bm):
            for k in range(steps):
                t = (k + 0.5) / steps; c = p0 + (p1 - p0) * t
                box(bm, (c.x, c.y, sag(t)), (0.75, 2.8, 0.18), rz)
        def ropes(bm):
            for k in range(steps):
                t0, t1 = k / steps, (k + 1) / steps
                c = p0 + (p1 - p0) * ((t0 + t1) / 2)
                for s in (1.5, -1.5):
                    zz = (sag(t0) + sag(t1)) / 2 + 1.2
                    box(bm, (c.x + side.x * s, c.y + side.y * s, zz), (L / steps + 0.05, 0.1, 0.1), rz)
            for p, zz in ((p0, z0), (p1, z1)):
                for s in (1.6, -1.6):
                    box(bm, (p.x + side.x * s, p.y + side.y * s, zz + 0.6), (0.4, 0.4, 2.6))
        O("BRIDGE_ROPE", [("MAT_WOOD_LIGHT", planks), ("MAT_WOOD", ropes)])
        return 0
    if kind == "ICE_FLOES":                   # (count, inner radius, outer radius) round (x, y)
        count, r0, r1 = args
        rr = random.Random(n + 41)
        def floes(bm):
            for k in range(count):
                t = rr.uniform(0, math.tau); r = rr.uniform(r0, r1)
                s = rr.uniform(2.0, 6.0)
                cone(bm, x + math.cos(t) * r, y + math.sin(t) * r, -0.4, 1.0, s, s * 0.85, 6, rz=rr.uniform(0, 1))
        object_from(H, "ICE_FLOES", "ENVIRONMENT", M, [("MAT_SNOW", floes)])
        return 0
    raise ValueError(f"unknown landmark {kind}")


def icicles(B, outlines):
    """Icicles hanging off the cliffs' snowy lip all round each island."""
    H, M, height = B["H"], B["M"], B["height"]
    rr = random.Random(7)
    def build(bm):
        for o in outlines:
            n = len(o); c = sum(o, Vector((0, 0))) / n
            for i in range(0, n, 2):
                t = (o[(i + 1) % n] - o[i - 1]).normalized(); nrm = Vector((t.y, -t.x))
                if nrm.dot(o[i] - c) < 0: nrm = -nrm
                p = o[i] + nrm * rr.uniform(1.0, 1.6)
                L = rr.uniform(1.2, 3.2)
                v = cone(bm, p.x, p.y, 0, L, 0.4, 0, 5, tilt=('X', math.pi))
                bmesh.ops.translate(bm, vec=(0, 0, height(o[i].x, o[i].y) * 0.95), verts=v)
    object_from(H, "ICICLES", "ENVIRONMENT", M, [("MAT_ICE", build)])
