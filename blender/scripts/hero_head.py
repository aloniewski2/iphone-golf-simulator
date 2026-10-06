"""The Hero's head, made whole (hero_hair.py grows the haircuts on it, hero_hats.py the hats).

Adnan's Hero was sculpted wearing a visor. His body's head is a mask: a thin, lumpy face plate that stops at the
forehead and wraps the cheeks in a ragged edge, with torn shards where the ears should be; the skull exists only
as the bald piece, set ~10 cm back from the face. Everything behind the mask was hair. Patching the mask left a
dented, kidney-shaped head (and blurring the scan into a shell left a bean, the skull sitting far behind the face),
so none of it is used: the head is made here, whole, round and low poly.

  * `build_round_head` makes one closed shell: an egg (`EGG`: a rounded, box-ish ellipsoid with squarish
    cross-sections, full cheeks and a jaw that narrows), the size of his face and skull together, on a UV sphere
    of 40 x 28 (about 1.5k vertices). A nose is raised on it; the mouth (a smile) and the brows are thin dark strips
    lying on it (their own material, `Hero_01_FaceLine`); an ear is grown on each side. It carries the hairline as
    vertex colour R (0 skin, 1 hair) so the game paints it skin on the face, temples, ears and nape and in the
    hair's colour under the hair; bald, it is all skin. His eyeballs stand out of it as they did out of his mask.
  * `fix_neck` mends the body's neck (see below) and takes the mask away: the body's head faces go.

Everything works in directions from one centre, `CENTER`, which sees the whole head (it is star-shaped from it).
Units are metres, Blender axes (the face looks down -Y, Z up).
"""
import bpy, bmesh, math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

CENTER = Vector((0.0, 0.03, 1.42))
HEAD_CUT = 1.255                # the body's faces above this are the mask (and the top of the neck cone): they go
# The hairline: elevation (degrees above CENTER) of the lowest hair at each angle from straight ahead (0 the
# forehead, 90 the ears, 180 the nape). Skin below, hair above; the hair meshes grow from here.
HAIRLINE_PTS = ((0, 33), (30, 30), (55, 22), (78, 8), (100, 14), (125, -4), (155, -22), (180, -28))


def _world_bvh(objs, poly_filter=None):
    verts, polys = [], []
    for o in objs:
        base = len(verts)
        mw = o.matrix_world
        verts += [mw @ v.co for v in o.data.vertices]
        for p in o.data.polygons:
            if poly_filter and not poly_filter(o, p): continue
            polys.append(tuple(base + i for i in p.vertices))
    return BVHTree.FromPolygons(verts, polys) if polys else None


def _ray(tree, d, far=1.0):
    """Distance from CENTER along d to the first surface, or None."""
    if tree is None: return None
    hit = tree.ray_cast(CENTER, d, far)
    return hit[3] if hit[0] is not None else None


def _face_filter(face):
    skin_slots = {i for i, s in enumerate(face.material_slots) if s.material and "WarmSkin" in s.material.name}
    return lambda o, p: o is not face or (p.material_index in skin_slots and (o.matrix_world @ p.center).z > 1.25)


def _azimuth(d):
    """Angle from straight ahead (-Y) round to direction d, 0..pi (the two sides folded together)."""
    return math.atan2(abs(d.x), -d.y)


def _smooth(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def hairline_mask(d, soft=math.radians(7)):
    """1 in hair, 0 in skin, for direction d from CENTER (the hairline is HAIRLINE_PTS)."""
    phi = math.degrees(_azimuth(d)); el = math.degrees(math.asin(max(-1.0, min(1.0, d.z))))
    line = HAIRLINE_PTS[-1][1]
    for (a0, e0), (a1, e1) in zip(HAIRLINE_PTS, HAIRLINE_PTS[1:]):
        if a0 <= phi <= a1:
            line = e0 + (e1 - e0) * (phi - a0) / (a1 - a0); break
    return _smooth((el - line) / math.degrees(soft) * 0.5 + 0.5)


EAR = dict(y=0.066, z=1.398, half_w=0.021, half_h=0.034, rise=0.016, tilt=0.2)


def _ear_height(rho):
    """A raised rim round a shallow dish, sinking into the head at the edge (rho 0 centre .. 1 edge)."""
    fade = 1.0 - _smooth((rho - 0.72) / 0.28)
    return fade * (0.45 + 0.75 * math.exp(-((rho - 0.68) / 0.2) ** 2) - 0.35 * math.exp(-(rho / 0.42) ** 2)) - 0.25 * (1 - fade)


def add_ears(bm, col_layer, rings=7, segs=22):
    """A small ear on each side, grown from the shell (bm's faces must already be in place): a rim ring round a
    dish on a disc that lies on the head's surface and sinks into it at the edge."""
    tree = BVHTree.FromBMesh(bm)
    e = EAR
    for side in (1, -1):
        rows = []
        for k in range(rings + 1):
            rho = k / rings
            ring = []
            for j in range(1 if k == 0 else segs):
                th = 2 * math.pi * j / segs
                uu, vv = math.cos(th) * rho * e["half_w"], math.sin(th) * rho * e["half_h"] * (1.0 if math.sin(th) > 0 else 0.92)
                y = e["y"] + uu * math.cos(e["tilt"]) + vv * math.sin(e["tilt"])
                z = e["z"] - uu * math.sin(e["tilt"]) + vv * math.cos(e["tilt"])
                hit = tree.ray_cast(Vector((side * 0.5, y, z)), Vector((-side, 0, 0)), 0.6)
                x = hit[0].x if hit[0] is not None else side * 0.15
                v = bm.verts.new(Vector((x + side * (e["rise"] * _ear_height(rho)), y, z)))
                v[col_layer] = (0.0, 0.0, 0.0, 1.0)
                ring.append(v)
            rows.append(ring)
        faces = []
        for j in range(segs):
            faces.append(bm.faces.new((rows[0][0], rows[1][j], rows[1][(j + 1) % segs])))
        for k in range(1, rings):
            for j in range(segs):
                faces.append(bm.faces.new((rows[k][j], rows[k + 1][j], rows[k + 1][(j + 1) % segs], rows[k][(j + 1) % segs])))
        for f in faces:
            f.normal_update()
            if f.normal.x * side < 0: f.normal_flip()


def _strip_on(bm, tree, col_layer, pts, width, lift=0.0007, mat=1):
    """A thin dark strip lying on the shell: `pts` are (x, z, half-width scale) along it; each is dropped onto the
    surface from the front (a ray along +Y), so the strip follows the face. Faces out (toward -Y)."""
    rows = []
    for x, z, s in pts:
        hit = tree.ray_cast(Vector((x, -0.6, z)), Vector((0, 1, 0)), 1.0)
        if hit[0] is None: continue
        p, n = hit[0], hit[1]
        up = (Vector((0, 0, 1)) - n * n.z).normalized()
        a = bm.verts.new(p + n * lift + up * (width * s / 2)); b = bm.verts.new(p + n * lift - up * (width * s / 2))
        a[col_layer] = b[col_layer] = (0.0, 0.0, 0.0, 1.0)
        rows.append((a, b))
    for (a0, b0), (a1, b1) in zip(rows, rows[1:]):
        f = bm.faces.new((a0, a1, b1, b0))
        f.material_index = mat; f.normal_update()
        if f.normal.y > 0: f.normal_flip()
        f.smooth = True


def _neck_fits(bm, mw, skin_slots, levels, fit_top):
    """The neck's circle (centre x, centre y, radius) at each height in `levels`, fitted to the front of it (the
    front is whole where the back was torn); None where it can't be told. Returns a list."""
    skin_verts = [mw @ v.co for v in bm.verts if any(f.material_index in skin_slots for f in v.link_faces)]
    fits = []
    for z in levels:
        if z > fit_top: fits.append(None); continue
        pts = [p for p in skin_verts if abs(p.z - z) < 0.012 and abs(p.x) < 0.12 and abs(p.y - CENTER.y) < 0.14]
        if len(pts) < 10: fits.append(None); continue
        ymid = sorted(p.y for p in pts)[len(pts) // 2]
        front = [p for p in pts if p.y <= ymid + 0.005]
        if len(front) < 6: fits.append(None); continue
        A = np.array([[2 * p.x, 2 * p.y, 1.0] for p in front]); b = np.array([p.x ** 2 + p.y ** 2 for p in front])
        (ca, cb, cc), *_ = np.linalg.lstsq(A, b, rcond=None)
        r = math.sqrt(max(1e-8, cc + ca * ca + cb * cb))
        fits.append((float(ca), float(cb), float(r)) if 0.03 < r < 0.09 else None)
    return fits


def _egg_radius(d, iterations=34):
    """How far from CENTER the head's surface is along d: the egg (`EGG`) is a rounded box-ish ellipsoid whose
    cross-sections are squarish (full cheeks, a flat-ish face), narrowing to the jaw."""
    ax, ay, az, p, q, jaw, jaw_top, jaw_span = EGG["a"], EGG["b"], EGG["c"], EGG["p"], EGG["q"], EGG["jaw"], EGG["jaw_top"], EGG["jaw_span"]
    hc = EGG["centre"]

    def inside(t):
        pt = CENTER + d * t
        k = 1.0 - jaw * _smooth((jaw_top - pt.z) / jaw_span)
        f = ((abs(pt.x - hc.x) / (ax * k)) ** p + (abs(pt.y - hc.y) / (ay * k)) ** p) ** (q / p) + (abs(pt.z - hc.z) / az) ** q
        return f < 1.0
    lo, hi = 0.0, 0.45
    for _ in range(iterations):
        mid = (lo + hi) / 2
        if inside(mid): lo = mid
        else: hi = mid
    return lo


EGG = dict(centre=Vector((0.0, 0.03, 1.46)), a=0.140, b=0.150, c=0.170, p=2.6, q=2.4, jaw=0.16, jaw_top=1.40, jaw_span=0.10)
NOSE = dict(tip=Vector((0.001, -0.150, 1.372)), amp=0.024, sigma=6.5)


def build_round_head(arm, name="Head_Scalp", segments=40, rings=28, features=True):
    """One closed, round, low-poly head: see the module's note. The egg (`EGG`) has the size of Adnan's head (his
    face and skull together), a nose is raised on it, the mouth and the brows lie on it as dark strips, and an
    ear is grown on each side. His eyeballs stand out of it. Returns the new object, bound to `arm`'s Head bone."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
    bm.verts.ensure_lookup_table(); bm.verts.index_update()
    dirs = [v.co.normalized() for v in bm.verts]
    d_nose = (NOSE["tip"] - CENTER).normalized()
    for i, v in enumerate(bm.verts):
        d = dirs[i]
        r = _egg_radius(d)
        ang = math.acos(max(-1.0, min(1.0, d.dot(d_nose))))
        r += NOSE["amp"] * math.exp(-(ang / math.radians(NOSE["sigma"])) ** 2)
        v.co = CENTER + d * r
    # hairline: skin on the face, temples, ears and nape, hair colour under the hair
    col = bm.verts.layers.float_color.new("Col")
    for v in bm.verts:
        v[col] = (hairline_mask((v.co - CENTER).normalized()), 0.0, 0.0, 1.0)
    bm.normal_update()
    tree = BVHTree.FromBMesh(bm)
    if features:     # the mouth: a smile, and the brows over the eyes (dark strips on the shell)
        def taper(t): return math.sin(math.pi * t) ** 0.5
        mouth = [(x, 1.336 + 0.008 * (x / 0.030) ** 2, taper((x + 0.030) / 0.060) + 0.05) for x in np.linspace(-0.030, 0.030, 15)]
        _strip_on(bm, tree, col, mouth, 0.0036)
        for side in (1, -1):
            brow = [(side * (0.046 + 0.062 * t), 1.482 + 0.008 * math.sin(math.pi * t) - 0.006 * t, taper(t) + 0.05) for t in np.linspace(0, 1, 13)]
            _strip_on(bm, tree, col, brow, 0.0048)
    add_ears(bm, col)

    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    _bind_to_head(obj, arm)
    obj.data.materials.append(bpy.data.materials.get("Hero_01_Scalp") or bpy.data.materials.new("Hero_01_Scalp"))
    obj.data.materials.append(bpy.data.materials.get("Hero_01_FaceLine") or bpy.data.materials.new("Hero_01_FaceLine"))
    return obj


NECK_BAND = (1.13, 1.32)   # the neck as the body has it: from inside the shirt up to under the jaw (metres)
NECK_FIT_TOP = 1.275       # above this the jaw is in the fit's way: the tube runs straight on up inside the head
PEEL_BAND = (1.20, 1.31)   # where torn shards are peeled: the collar, up to the jaw


def fix_neck(face, segments=28, rings=10, tuck=0.99):
    """Mend the body's neck. Adnan's Hero wore hair over the back of the neck and a collar over the rest, so the
    skin there was left torn: shards standing out at the collar and, behind it, a hole to the background. This
    peels the shards (triangles hanging by an edge or a corner, over and over), takes away the mask (the head is
    `build_round_head`'s) and puts in a closed tube, skinned like the neck beside it, that fills whatever
    is left open: a circle fitted to the front of the neck at each height (the front is whole), `tuck` of its size
    so it lies just under the skin where that is there. The tube runs on up inside the head and down inside the
    shirt. Returns a report."""
    mw = face.matrix_world; inv = mw.inverted()
    # the neck cone is his "MatteCloth" skin (the face plate above the jaw is "WarmSkin"): both are skin here
    skin_slots = [i for i, s in enumerate(face.material_slots) if s.material and ("MatteCloth" in s.material.name or "WarmSkin" in s.material.name)]
    neck_slot = next((i for i, s in enumerate(face.material_slots) if s.material and "MatteCloth" in s.material.name), None)
    if not skin_slots or neck_slot is None: return {"neck": "no skin slot"}
    bm = bmesh.new(); bm.from_mesh(face.data); bm.faces.ensure_lookup_table()
    lo, hi = NECK_BAND
    levels = [lo + (hi - lo) * k / (rings - 1) for k in range(rings)]
    fits = _neck_fits(bm, mw, skin_slots, levels, NECK_FIT_TOP)     # (before anything is taken away)
    peeled = 0
    for _ in range(10):
        bad = []
        for f in bm.faces:
            if f.material_index not in skin_slots: continue
            z = (mw @ f.calc_center_median()).z
            if not (PEEL_BAND[0] <= z <= PEEL_BAND[1]): continue     # (the cone's open bottom is inside the shirt: leave it)
            if sum(1 for e in f.edges if e.is_boundary) >= 2 or any(len(v.link_faces) == 1 for v in f.verts): bad.append(f)
        if not bad: break
        peeled += len(bad)
        bmesh.ops.delete(bm, geom=bad, context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        bm.faces.ensure_lookup_table()
    bm.verts.ensure_lookup_table()
    good = [i for i, f in enumerate(fits) if f]
    if not good: bm.free(); return {"neck": "no fit", "peeled": peeled}
    for i in range(rings):     # holes in the fits take their nearest neighbour's (above the fit, the last one)
        if fits[i] is None: fits[i] = fits[min(good, key=lambda j: abs(j - i))]
    fits = [tuple(sum(fits[min(rings - 1, max(0, i + k))][c] for k in (-1, 0, 1)) / 3 for c in range(3)) for i in range(rings)]

    # thin sheets of skin flaring out sideways from the neck at the collar (the tears): anything of the neck's own
    # skin standing well outside the circle goes, and the tube takes its place
    def circle_at(z):
        t = (z - lo) / (hi - lo) * (rings - 1)
        i = int(max(0, min(rings - 2, math.floor(t)))); u = min(1.0, max(0.0, t - i))
        return tuple(fits[i][c] * (1 - u) + fits[i + 1][c] * u for c in range(3))
    flares = []
    for f in bm.faces:
        if f.material_index != neck_slot: continue
        c = mw @ f.calc_center_median()
        if not (1.21 <= c.z <= 1.34): continue
        ca, cb, r = circle_at(c.z)
        # behind the neck's top (where the head's nape is) the cone's rim is all scraps: the tube stands in for it
        behind = c.z >= 1.283 and c.y > 0.06
        if behind or (c.z <= 1.30 and math.hypot(c.x - ca, c.y - cb) > r * 1.04 + 0.003): flares.append(f)
    bmesh.ops.delete(bm, geom=flares, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    peeled += len(flares)
    # the mask goes (the head is the shell's), and the neck cone's top with it
    mask = [f for f in bm.faces if (mw @ f.calc_center_median()).z > HEAD_CUT and abs((mw @ f.calc_center_median()).x) < 0.3]
    bmesh.ops.delete(bm, geom=mask, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    skin_verts = [(mw @ v.co, v) for v in bm.verts if any(f.material_index in skin_slots for f in v.link_faces)]

    # the tube: skinned like the nearest neck vertex, one flat skin texel, faces out
    kd = KDTree(len(skin_verts))
    for i, (p, _) in enumerate(skin_verts): kd.insert(p, i)
    kd.balance()
    dvl = bm.verts.layers.deform.active
    shape_layers = list(bm.verts.layers.shape.values())
    uvl = bm.loops.layers.uv.active
    ca, cb, r = fits[rings // 2]
    _, ni, _ = kd.find(Vector((ca, cb - r, 1.22)))
    uv0 = skin_verts[ni][1].link_loops[0][uvl].uv.copy() if uvl else None
    rows = []
    for (ca, cb, r), z in zip(fits, levels):
        ring = []
        for j in range(segments):
            th = 2 * math.pi * j / segments
            p = Vector((ca + math.cos(th) * r * tuck, cb + math.sin(th) * r * tuck, z))
            v = bm.verts.new(inv @ p)
            for L in shape_layers: v[L] = v.co.copy()
            if dvl:
                _, si, _ = kd.find(p)
                for g, w in skin_verts[si][1][dvl].items(): v[dvl][g] = w
            ring.append(v)
        rows.append(ring)
    tube = []
    for k in range(rings - 1):
        for j in range(segments):
            f = bm.faces.new((rows[k][j], rows[k][(j + 1) % segments], rows[k + 1][(j + 1) % segments], rows[k + 1][j]))
            f.material_index = neck_slot; f.smooth = True
            if uv0 is not None:
                for lp in f.loops: lp[uvl].uv = uv0
            tube.append((k, f))
    bm.normal_update()
    flipped = 0     # faces out: away from the ring's centre (Unity culls the inside)
    for k, f in tube:
        mid = sum((v.co for v in rows[k]), Vector()) / segments
        c = f.calc_center_median()
        if f.normal.x * (c.x - mid.x) + f.normal.y * (c.y - mid.y) < 0: f.normal_flip(); flipped += 1
    bm.to_mesh(face.data); bm.free()
    face.data.update()
    return {"neck": {"peeled": peeled, "mask": len(mask), "rings": rings, "flipped": flipped, "circle_mid": tuple(round(x, 3) for x in fits[rings // 2])}}


def _bind_to_head(obj, arm):
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()
    g = obj.vertex_groups.new(name="Head")
    g.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    mod = obj.modifiers.new("Armature", 'ARMATURE'); mod.object = arm


class Radial:
    """A surface seen from CENTER: its radius in any direction (first hit), for the scalp and the hats."""
    def __init__(self, objs, poly_filter=None):
        self.tree = _world_bvh(objs, poly_filter)

    def __call__(self, d, far=1.0):
        return _ray(self.tree, d, far)


def uv_from(target, source):
    """UVs for `target` from the nearest face of `source` (the buzz cut's shell, for the scalp's short-hair map):
    each of target's faces takes the affine map of the one source triangle nearest its centre, so a source seam
    stays a clean seam instead of a smear."""
    from mathutils.geometry import barycentric_transform
    bm = bmesh.new(); bm.from_mesh(source.data)
    bmesh.ops.transform(bm, matrix=source.matrix_world, verts=bm.verts)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.faces.ensure_lookup_table()
    uvl = bm.loops.layers.uv.active
    tree = BVHTree.FromBMesh(bm)
    me = target.data
    uv = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
    mw = target.matrix_world
    for p in me.polygons:
        _, _, fi, _ = tree.find_nearest(mw @ p.center)
        f = bm.faces[fi]
        tri = [l.vert.co for l in f.loops]; tuv = [l[uvl].uv.to_3d() for l in f.loops]
        for li in p.loop_indices:
            co = mw @ me.vertices[me.loops[li].vertex_index].co
            u = barycentric_transform(co, *tri, *tuv)
            uv.data[li].uv = (u.x, u.y)
    bm.free()


def build_head(arm, face, buzz):
    """The whole head: the round shell (with ears, mouth and brows; UVs from `buzz`, Adnan's buzz cut, for the
    short-hair maps) and, with it made, his mask taken off the body and its neck mended. Returns (the shell, a
    report). The haircuts (hero_hair.py) and hats (hero_hats.py) are made on it."""
    scalp = build_round_head(arm)
    uv_from(scalp, buzz)
    return scalp, {"head": {"verts": len(scalp.data.vertices), "tris": sum(len(p.vertices) - 2 for p in scalp.data.polygons)}, **fix_neck(face)}
