"""Everything that goes on the head: haircuts, hats, glasses, beards. All of it is made in HEAD SPACE (the head's own
centre and size, before avatar_base.place_head scales and moves it onto the body), against the same skull block, so
any haircut works with any hat and any glasses on any head.

The promises that make parts interchangeable (docs/avatar-kit.md):
  * hair stands at most HAIR_TOP off the skull at the crown, so every hat clears every haircut (a hat's inside is
    HAT_CLEAR or more off the skull); a haircut can ask the game to tuck its crown under a hat (`under_hat=True`
    builds the same mesh with the crown pressed down: the game ships that as a blend shape)
  * glasses rest on the nose socket and end at the ears; hats' brims stay above the brows
  * every part has only Hero_* material roles, so it is recoloured, never re-textured
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix
import avatar_kit as K
import avatar_base as B
from avatar_kit import tube, blob, join, lathe, ring_tube, ellipse_plate, Surface
from avatar_base import HX, HY, HZ

HAIR_TOP = 0.036       # how far hair may stand off the skull at the crown (not counting a part's own locks' tips)
HAT_CLEAR = 0.044      # how far a hat's inside is off the skull: more than HAIR_TOP


# ------------------------------------------------------------------------------------------------ shells
def _quad(front, side, back):
    """A smooth line round the head as a function of c = cos(azimuth from the front): front, side, back heights."""
    b = (front - back) / 2; d = front - side - b
    return lambda c: side + b * c + d * c * c


def _c(p):
    r = math.hypot(p.x, p.y) or 1e-6
    return -p.y / r


def shell(name, keep, off, mat, passes=6, rim=True, rim_sink=1.25):
    """A skin-tight cover over the part of the skull where keep(face centre) is true, stood off the skull by
    off(point), its edge rolled back down to the skin (so it reads as a thick cap, with no gap under it)."""
    bm = B.head_bm()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not keep(f.calc_center_median())], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.normal_update(); bm.verts.ensure_lookup_table()
    for _ in range(passes):                                   # the edge, smoothed along itself
        mv = {}
        for v in bm.verts:
            nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
            if len(nb) == 2: mv[v] = ((nb[0].co + nb[1].co) / 2 - v.co) * 0.5
        for v, d in mv.items(): v.co += d
    bm.normal_update()
    for v in bm.verts: v.co += v.normal * off(v.co)
    if rim:
        ret = bmesh.ops.extrude_edge_only(bm, edges=[e for e in bm.edges if e.is_boundary])
        for g in ret["geom"]:
            if isinstance(g, bmesh.types.BMVert): g.co -= g.co.normalized() * off(g.co) * rim_sink
    # faces outward
    if sum(f.normal.dot(f.calc_center_median()) for f in bm.faces) < 0: bmesh.ops.reverse_faces(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return K.new_obj(name, me, mat)


def _cap(name="HairCap", front=0.52, side=0.26, back=-0.22, thick=0.022, crown=0.014, mat="Hero_Hair"):
    line = _quad(front, side, back)
    return shell(name, lambda p: p.z / HZ > line(_c(p)), lambda p: thick + crown * max(0.0, p.z / HZ), mat)


def _locker(cap, seed=11, mats=("Hero_Hair", "Hero_HairB"), streak=0.35, max_el=None):
    """Returns lock(): add a tapered lock of hair rooted on the cap at (azimuth, elevation) degrees: az 0 is the
    front, + toward +x; it lies along the surface, downhill, turned by `sweep` radians toward +x, and curls off it.
    With max_el set (a hat is on), nothing is rooted higher than that, so no hair can reach through the hat."""
    surf = Surface(cap); rnd = random.Random(seed); objs = []
    def lock(az, el, length, hw, thick=0.02, sweep=0.0, curl=0.012, mat=None, lift=0.0):
        if max_el is not None and el > max_el: return None
        a, e = math.radians(az), math.radians(el)
        d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))).normalized()
        p, n = surf.ray(d * 0.6, -d, 0.8)
        if p is None: return None
        down = -(Vector((0, 0, 1)) - n * n.z)
        if down.length < 1e-3: down = Vector((math.sin(a), -math.cos(a), 0))
        down.normalize(); side = n.cross(down).normalized()
        axis = (down * math.cos(sweep) + side * math.sin(sweep)).normalized()
        root = p - axis * 0.004 + n * 0.002
        path = [root, root + axis * length * 0.5 + n * (0.010 + lift), root + axis * length + n * (0.004 + lift - curl)]
        m = mat or (mats[1] if rnd.random() < streak else mats[0])
        o = tube("Lock", path, [(hw, thick), (hw * 0.97, thick * 0.98), (hw * 0.70, thick * 0.78)], m, segs=8, per=2, ref=n, cap_rings=2)
        objs.append(o); return o
    lock.objs = objs; lock.rnd = rnd
    return lock


def ribbon(surf, pts, w0, w1, thick, mat="Hero_Hair", name="Ribbon", segs=8, base=0.0):
    """A broad flowing lock that follows the head: pts are (azimuth, elevation, lift) on the hair cap (az 0 the front,
    + toward +x; el 0 the ear line, 90 the crown; lift how far it stands off the cap), w0 -> w1 its half width."""
    path = []
    for az, el, lift in pts:
        a, e = math.radians(az), math.radians(el)
        d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))).normalized()
        p, n = surf.ray(d * 0.6, -d, 0.8)
        if p is None: return None
        path.append(p + n * (lift + base))
    k = len(path)
    radii = [(w0 + (w1 - w0) * (i / (k - 1)) ** 0.8, thick * (1.0 - 0.28 * (i / (k - 1)) ** 2)) for i in range(k)]
    radii[-1] = (radii[-1][0] * 0.55, radii[-1][1] * 0.65)             # the tip draws in
    return tube(name, path, radii, mat, segs=segs, per=2, ref=lambda c: c.normalized(), cap_rings=2)


# ------------------------------------------------------------------------------------------------ haircuts
def swept(head, under_hat=False):
    """The Hero's own: a thick golden cap under three broad ribbons that sweep up off the crown, across the brow and
    down over the right temple, a couple of short locks over the ears and at the nape, a cowlick."""
    cap = _cap(thick=0.020, crown=0.004 if under_hat else 0.010)
    surf = Surface(head); objs = [cap]; R = lambda *a, **k: ribbon(surf, *a, base=0.020, **k)        # ribbons ride the skull, on top of the cap
    if not under_hat:
        objs += [R([(-46, 82, 0.020), (-22, 70, 0.036), (8, 58, 0.042), (38, 46, 0.032), (66, 34, 0.018)], 0.050, 0.040, 0.028, "Hero_Hair", "SwoopC"),
                 R([(-62, 76, 0.012), (-38, 62, 0.030), (-8, 50, 0.038), (26, 38, 0.030), (58, 26, 0.018), (80, 18, 0.008)], 0.050, 0.040, 0.026, "Hero_HairB", "SwoopA"),
                 R([(-78, 62, 0.006), (-52, 52, 0.020), (-20, 42, 0.028), (14, 32, 0.024), (46, 22, 0.014), (66, 14, 0.006)], 0.048, 0.038, 0.024, "Hero_Hair", "SwoopB"),
                 R([(-30, 40, 0.004), (-6, 32, 0.014), (20, 26, 0.014), (42, 20, 0.006)], 0.040, 0.032, 0.020, "Hero_HairB", "Fringe"),
                 R([(112, 62, 0.004), (150, 52, 0.014), (196, 50, 0.016), (236, 56, 0.008)], 0.066, 0.058, 0.024, "Hero_HairB", "Nape"),
                 R([(-70, 84, 0.020), (-56, 78, 0.042), (-44, 74, 0.060)], 0.030, 0.010, 0.020, "Hero_Hair", "Cowlick")]
    for az in (-100, 100):
        objs.append(R([(az, 52, 0.004), (az * 1.02, 36, 0.012), (az * 1.04, 20, 0.014)], 0.050, 0.042, 0.020, "Hero_Hair", "Temple"))
    objs = [o for o in objs if o]
    return join("Hair_Swept", objs)


def crop(head, under_hat=False):
    """Short and neat: a close cap with a little quiff at the front."""
    cap = _cap(front=0.46, side=0.20, back=-0.12, thick=0.016, crown=0.008 if not under_hat else 0.002)
    surf = Surface(head); objs = [cap]
    if not under_hat:
        objs += [ribbon(surf, [(-46, 68, 0.004), (-14, 56, 0.020), (22, 46, 0.022), (54, 34, 0.012)], 0.046, 0.038, 0.020, "Hero_Hair", "QuiffA", base=0.014),
                 ribbon(surf, [(-52, 50, 0.002), (-20, 42, 0.012), (14, 34, 0.012), (40, 27, 0.006)], 0.040, 0.032, 0.016, "Hero_HairB", "QuiffB", base=0.014)]
    return join("Hair_Crop", [o for o in objs if o])


def curls(head, under_hat=False):
    """Big soft curls: a ball for each, all over the top and back."""
    cap = _cap(front=0.52, side=0.30, back=-0.18, thick=0.012, crown=0.004)
    surf = Surface(head); r = random.Random(4); objs = [cap]; line = _quad(0.50, 0.30, -0.15)
    for el, n_az in ((80, 1), (62, 6), (44, 10), (26, 12), (8, 13)):
        for k in range(n_az):
            az = (360 / n_az) * k + r.uniform(-6, 6) + (el * 3)
            a, e = math.radians(az), math.radians(el)
            d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
            p, n = surf.ray(d * 0.6, -d, 0.8)
            if p is None or p.z / HZ < line(_c(p)) + 0.06: continue
            if under_hat and el > 32: continue                      # nothing tall under a hat
            rad = 0.056 + r.uniform(-0.005, 0.007)
            objs.append(blob("Curl", p + n * (rad * 0.55 if not under_hat else rad * 0.25), (rad, rad, rad * 0.92), "Hero_HairB" if r.random() < 0.3 else "Hero_Hair", segs=10, rings=7))
    return join("Hair_Curls", objs)


def bob(head, under_hat=False, length=0.17, name="Hair_Bob"):
    """A cap, a straight fringe, and curtains that fall past the ears; longer `length` makes it long hair."""
    cap = _cap(front=0.50, side=0.05, back=-0.35, thick=0.022, crown=0.008 if not under_hat else 0.002)
    surf = Surface(head); objs = [cap]
    if not under_hat:                                      # a straight fringe: two broad bands across the brow
        objs += [ribbon(surf, [(-64, 44, 0.004), (-32, 37, 0.014), (0, 34, 0.016), (32, 37, 0.014), (64, 44, 0.004)], 0.050, 0.050, 0.022, "Hero_Hair", "FringeA", base=0.014),
                 ribbon(surf, [(-60, 58, 0.004), (-30, 50, 0.012), (0, 47, 0.014), (30, 50, 0.012), (60, 58, 0.004)], 0.050, 0.050, 0.022, "Hero_HairB", "FringeB", base=0.014)]
    objs = [o for o in objs if o]
    for side in (1, -1):                                   # the curtains down each side of the face
        objs.append(tube("Curtain", [(side * 0.172, -0.010, 0.120), (side * 0.205, 0.020, -0.020), (side * 0.198, 0.050, -length + 0.030), (side * 0.184, 0.075, -length)],
                         [(0.026, 0.090), (0.027, 0.092), (0.028, 0.092), (0.022, 0.078)], "Hero_Hair", segs=12, per=3))
    objs.append(tube("BackCurtain", [(0, 0.150, 0.060), (0, 0.205, -0.050), (0, 0.205, -length + 0.030), (0, 0.193, -length)],
                     [(0.150, 0.030), (0.172, 0.032), (0.168, 0.030), (0.150, 0.026)], "Hero_Hair", segs=16, per=3))
    return join(name, objs)


def long_hair(head, under_hat=False):
    return bob(head, under_hat, length=0.36, name="Hair_Long")


def spikes(head, under_hat=False):
    """Spiky hair: a close cap and a crown of cones."""
    cap = _cap(front=0.50, side=0.26, back=-0.16, thick=0.014, crown=0.004)
    objs = [cap]; r = random.Random(9); surf = Surface(head)
    ring = [(84, 1, 0.12), (66, 6, 0.11), (46, 9, 0.10), (26, 11, 0.085)]
    line = _quad(0.5, 0.26, -0.15)
    for el, n_az, ln in ring:
        for k in range(n_az):
            az = (360 / n_az) * k + el
            a, e = math.radians(az), math.radians(el)
            d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
            p, n = surf.ray(d * 0.6, -d, 0.8)
            if p is None or p.z / HZ < line(_c(p)) + 0.04: continue
            if under_hat and el > 32: continue
            dirv = (n * 0.8 + Vector((0, 0, 0.6)) + Vector((math.sin(a), -math.cos(a), 0)) * 0.3).normalized()
            l = ln * (0.4 if under_hat else 1.0) * (1 + r.uniform(-0.12, 0.12))
            objs.append(tube("Spike", [p - n * 0.01, p + dirv * l * 0.5, p + dirv * l], [(0.030, 0.030), (0.022, 0.022), (0.004, 0.004)], "Hero_HairB" if r.random() < 0.3 else "Hero_Hair", segs=8, per=2, cap_rings=2))
    return join("Hair_Spikes", objs)


def ponytail(head, under_hat=False):
    """A tidy cap with a high ponytail: a tie and a swinging tail."""
    cap = _cap(front=0.50, side=0.22, back=-0.22, thick=0.020, crown=0.008 if not under_hat else 0.002)
    surf = Surface(head); objs = [cap]
    if not under_hat:
        objs += [ribbon(surf, [(-60, 70, 0.004), (-30, 56, 0.016), (4, 46, 0.020), (36, 38, 0.014), (62, 30, 0.006)], 0.050, 0.042, 0.020, "Hero_Hair", "SwoopA", base=0.014),
                 ribbon(surf, [(-64, 52, 0.002), (-32, 42, 0.010), (2, 36, 0.012), (34, 30, 0.008)], 0.042, 0.034, 0.016, "Hero_HairB", "SwoopB", base=0.014)]
    objs = [o for o in objs if o]
    objs.append(tube("Tail", [(0, 0.150, 0.130), (0, 0.235, 0.120), (0, 0.285, 0.040), (0, 0.275, -0.070), (0, 0.250, -0.170)],
                     [(0.048, 0.044), (0.046, 0.046), (0.052, 0.050), (0.042, 0.040), (0.012, 0.012)], "Hero_Hair", segs=12, per=3))
    objs.append(blob("TieBall", (0, 0.160, 0.130), (0.050, 0.046, 0.046), "Hero_Hair", segs=10, rings=6))
    objs.append(ring_tube("Tie", (0, 0.205, 0.127), 0.052, 0.050, 0.011, "Hero_HatB", segs=12, sides=5, p=2.0))   # round the tail (an x-z ring)
    return join("Hair_Ponytail", objs)


# ------------------------------------------------------------------------------------------------ hats
def _bill(reach=0.36, width=0.118, z=0.082, drop=0.030, mat="Hero_HatA"):
    """The bill: a flat curved tongue that leaves the front of the hat just over the brows and dips toward its tip. Its
    underside is the hat's second colour (HatB), so seen from the front it reads as a bill, not a dark slab."""
    o = tube("Bill", [(0, -0.218, z), (0, -(0.218 + (reach - 0.218) * 0.5), z - drop * 0.30), (0, -reach, z - drop)],
             [(0.011, width), (0.010, width * 0.97), (0.009, width * 0.80)], mat, segs=14, p=3.4, per=3, ref=Vector((1, 0, 0)))
    o.data.materials.append(K.role("Hero_HatB"))
    for poly in o.data.polygons:
        if poly.normal.z < -0.35: poly.material_index = 1
    return o


def golf_cap(head):
    """A six-panel golf cap: a dome that clears any haircut, a curved bill, a button on top."""
    dome = shell("Dome", lambda p: p.z / HZ > _quad(0.40, 0.12, -0.10)(_c(p)), lambda p: HAT_CLEAR, "Hero_HatA")
    return join("Hat_Cap", [dome, _bill(), blob("Button", (0, 0, HZ + HAT_CLEAR - 0.004), (0.016, 0.016, 0.012), "Hero_HatB", segs=8, rings=5)])


def visor(head):
    """A visor: a band round the brow (open on top, so hair shows) and a bill."""
    lo, hi = _quad(0.40, 0.14, 0.02), _quad(0.72, 0.52, 0.40)
    band = shell("Band", lambda p: lo(_c(p)) < p.z / HZ < hi(_c(p)), lambda p: 0.030, "Hero_HatA", rim=False)
    return join("Hat_Visor", [band, _bill(reach=0.34, z=0.082)])


def _flat_ring(name, z, r, tube_r, mat, segs=28, sides=6):
    """A horizontal band round the head at height z."""
    o = ring_tube(name, (0, 0, 0), r, r, tube_r, mat, segs=segs, sides=sides)
    o.data.transform(Matrix.Rotation(math.radians(90), 4, 'X'))
    o.data.transform(Matrix.Translation((0, 0, z)))
    return o


def bucket(head):
    """A bucket hat: a soft crown and a floppy brim all round."""
    prof = [(0, 0.262), (0.11, 0.258), (0.19, 0.236), (0.232, 0.180), (0.242, 0.095), (0.262, 0.078), (0.330, 0.056), (0.392, 0.030),
            (0.392, 0.014), (0.330, 0.034), (0.262, 0.062), (0.238, 0.066), (0.226, 0.090), (0.222, 0.150)]
    hat = lathe("Bucket", prof, "Hero_HatA", segs=28, sx=1.0, sy=1.0)
    return join("Hat_Bucket", [hat, _flat_ring("Band", 0.098, 0.244, 0.012, "Hero_HatB")])


def beanie(head):
    """A knit beanie with a turned cuff and a pom-pom."""
    prof = [(0, 0.270), (0.11, 0.264), (0.19, 0.240), (0.226, 0.180), (0.236, 0.130), (0.238, 0.110)]
    dome = lathe("Dome", prof, "Hero_HatA", segs=24)
    cuff = lathe("Cuff", [(0.226, 0.150), (0.250, 0.142), (0.256, 0.112), (0.250, 0.082), (0.226, 0.074), (0.218, 0.112)], "Hero_HatB", segs=24)
    return join("Hat_Beanie", [dome, cuff, blob("Pom", (0, 0, 0.300), (0.050, 0.050, 0.050), "Hero_HatB", segs=10, rings=7)])


def straw(head):
    """A wide straw sun hat with a band."""
    prof = [(0, 0.246), (0.10, 0.244), (0.18, 0.226), (0.224, 0.172), (0.232, 0.095), (0.30, 0.078), (0.420, 0.052), (0.500, 0.020),
            (0.500, 0.006), (0.420, 0.038), (0.300, 0.062), (0.226, 0.080), (0.216, 0.170)]
    hat = lathe("Straw", prof, "Hero_Metal", segs=32)
    return join("Hat_Straw", [hat, _flat_ring("Band", 0.106, 0.236, 0.014, "Hero_HatB")])


# ------------------------------------------------------------------------------------------------ glasses
def _glasses(head, rx, rz, p, frame_r, lens_mat, name, dx=0.080, dz=-0.008):
    surf = Surface(head)
    p0, n0 = surf.hit(dx, dz)
    yf = p0.y - 0.020
    parts = []
    for side in (1, -1):
        c = (side * dx, yf, dz)
        parts.append(ring_tube("Rim", c, rx, rz, frame_r, "Hero_Frame", segs=24, sides=6, p=p))
        parts.append(ellipse_plate("Lens", c, rx - frame_r * 0.4, rz - frame_r * 0.4, lens_mat, segs=20, thick=0.002, p=p))
        x_out = side * (dx + rx)
        pts = [Vector((x_out, yf, dz + rz * 0.25))]
        for yy in (-0.05, 0.0, 0.05):                           # the temple arm: hugs the side of the skull back to the ear
            q, _ = surf.ray((side * 1.0, yy, dz + rz * 0.15), (-side, 0, 0), 2.0)
            if q is not None: pts.append(Vector((q.x + side * 0.012, yy, dz + rz * 0.15)))
        parts.append(tube("Arm", pts, frame_r * 0.95, "Hero_Frame", segs=6, per=3, cap_rings=2))
    parts.append(tube("Bridge", [(-(dx - rx), yf - 0.004, dz + rz * 0.40), (0, yf - 0.012, dz + rz * 0.55), ((dx - rx), yf - 0.004, dz + rz * 0.40)], frame_r * 0.9, "Hero_Frame", segs=6, per=3, cap_rings=2))
    return join(name, parts)


def round_glasses(head): return _glasses(head, 0.046, 0.046, 2.0, 0.0055, "Hero_Lens", "Glasses_Round")
def square_glasses(head): return _glasses(head, 0.052, 0.040, 3.4, 0.0062, "Hero_Lens", "Glasses_Square")
def sunglasses(head): return _glasses(head, 0.056, 0.042, 2.7, 0.0085, "Hero_LensDark", "Glasses_Shades")


# ------------------------------------------------------------------------------------------------ facial hair
def mustache(head):
    surf = Surface(head); parts = []
    for side in (1, -1):
        pts = []
        for k, (x, z, rr) in enumerate(((0.004, -0.080, 0.014), (0.030, -0.087, 0.0135), (0.058, -0.084, 0.011), (0.078, -0.072, 0.008))):
            p, n = surf.hit(side * x, z)
            if p is None: return None
            pts.append(p + n * (rr * 0.6))
        parts.append(tube("Stache", pts, [(0.014, 0.012), (0.0135, 0.012), (0.011, 0.010), (0.007, 0.007)], "Hero_Hair", segs=8, per=3, cap_rings=2))
    return join("Face_Mustache", parts)


def beard(head):
    """A full beard: along the jaw and round the chin, leaving the mouth clear."""
    def keep(p):
        z = p.z / HZ
        if z < -0.46: return p.y < 0.08
        return z < -0.10 and abs(p.x) > 0.112 and p.y < 0.06 and z > -0.80
    b = shell("Beard", keep, lambda p: 0.016, "Hero_Hair", passes=5)
    return join("Face_Beard", [b, mustache(head)])
