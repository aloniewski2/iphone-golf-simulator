"""The Hero's haircuts, built here from smooth locks instead of Adnan's Tripo sculpts (called from hero_parts_export.py).

His hair meshes were 10-12k-vertex scans of noisy clay: faceted, torn at the edges, and built round a visor ring, so
on the closed head they read as ragged black blobs. These are made to sit on that head (hero_head.py) and to be
layered with hats:

  * A haircut is a hem (how low the hair reaches at each angle round the head) and a set of locks. Each lock is a
    tapered, flattened tube that starts at a point on the hem and is followed UP the head (the way hair falls,
    backwards), pushed off the head, the face and the shirt so it never sinks in, then reversed so it runs from the
    crown to the hem. Locks overlap like shingles; a thin cap under them closes any gap at the crown.
  * A lock rides `clr` off the surface; the hair's outer envelope is what the hats fit over (hero_hats.py).
  * Vertex colour R is the lock's flex (0 at the root, 1 at the free end): the game's shader swings the hair by it and
    darkens the roots a little, so each lock reads as its own strand. G, B, A (which hat holds the hair) are set
    by hero_hats.py.

Everything is in the head's world space: metres, Blender axes (the face looks down -Y, Z up), CENTER as in hero_head.
"""
import bpy, bmesh, math, random
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

import hero_head as H

CENTER = H.CENTER
UP = Vector((0.0, 0.0, 1.0))
DOWN = Vector((0.0, 0.0, -1.0))


def _smooth(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


class Head:
    """What the hair grows on and must clear: the scalp, the face and the eyes (the head), plus the neck and the
    shirt's top (the body)."""

    def __init__(self, scalp, face, eyes, shirt=None, body_min_z=1.02):
        self.radial = H.Radial([scalp, face, *eyes], H._face_filter(face))
        verts, polys = [], []

        def add(o, keep=None):
            base = len(verts)
            mw = o.matrix_world
            verts.extend(mw @ v.co for v in o.data.vertices)
            for p in o.data.polygons:
                if keep and not keep(mw @ p.center): continue
                polys.append(tuple(base + i for i in p.vertices))

        add(scalp); add(face, lambda c: c.z > body_min_z and abs(c.x) < 0.45)
        for e in eyes: add(e)
        if shirt is not None: add(shirt, lambda c: c.z > body_min_z)
        self.tree = BVHTree.FromPolygons(verts, polys)

    bridge = 0.05

    def env(self, d, spread=None):
        """The head's radius toward d as hair sees it: the most it reaches within a few degrees, so a lock passes over
        an ear or a nose instead of hugging it."""
        spread = self.bridge if spread is None else spread
        best = self.radial(d) or 0.0
        a = d.orthogonal().normalized(); b = d.cross(a)
        for k in range(8):
            t = k * math.pi / 4
            r = self.radial((d + (a * math.cos(t) + b * math.sin(t)) * spread).normalized())
            if r is not None and r > best: best = r
        return best or 0.16

    def hem_point(self, phi_deg, z, clear, z_eq, curl=0.0):
        """Where a lock's hem end sits: on the head's envelope if the hem is above the equator, else hanging from
        the equator (at the radius the hair leaves the head, curling in by `curl` toward its tip)."""
        a = math.radians(phi_deg)
        h = Vector((math.sin(a), -math.cos(a), 0.0))
        if z >= z_eq:
            d = Vector((h.x * 0.16, h.y * 0.16, z - CENTER.z)).normalized()
            return CENTER + d * (self.env(d) + clear)
        d = Vector((h.x, h.y, (z_eq - CENTER.z) / 0.16)).normalized()
        r = self.env(d) + clear
        inward = curl * (1.0 - _smooth((z_eq - z) / 0.10)) if False else curl
        base = Vector((CENTER.x + h.x * (r - inward), CENTER.y + h.y * (r - inward), z))
        return base

    def push_out(self, p, clear):
        """p moved off the surface so it is at least `clear` outside it; returns (point, outward normal, in contact)."""
        loc, nor, _, dist = self.tree.find_nearest(p)
        if loc is None: return p, UP, False
        inside = (p - loc).dot(nor) < 0
        if inside or dist < clear:
            return loc + nor * clear, nor, True
        return p, (p - loc).normalized(), dist < clear * 1.4


def hem_fn(table):
    """A periodic piecewise-linear hem: table of (azimuth degrees -180..180, z)."""
    pts = sorted(table)
    if pts[0][0] > -180: pts = [(pts[-1][0] - 360, pts[-1][1])] + pts
    if pts[-1][0] < 180: pts = pts + [(pts[0][0] + 360, pts[0][1])]

    def f(phi):
        while phi < -180: phi += 360
        while phi > 180: phi -= 360
        for (a0, z0), (a1, z1) in zip(pts, pts[1:]):
            if a0 <= phi <= a1: return z0 + (z1 - z0) * (phi - a0) / max(a1 - a0, 1e-6)
        return pts[-1][1]
    return f


class Lock:
    """One tapered, flattened tube: its centre line (root .. tip) and how thick it is."""
    __slots__ = ("pts", "half_w", "half_h", "taper", "flex", "blunt", "seg")

    def __init__(self, pts, half_w, half_h, taper=0.85, flex=0.5, blunt=False):
        self.pts, self.half_w, self.half_h, self.taper, self.flex, self.blunt = pts, half_w, half_h, taper, flex, blunt


def _resample(pts, step):
    """The polyline resampled at (about) equal spacing."""
    out = [pts[0]]
    acc = 0.0
    for a, b in zip(pts, pts[1:]):
        seg = (b - a).length
        while acc + seg >= step:
            t = (step - acc) / seg
            a = a.lerp(b, t); seg = (b - a).length; acc = 0.0
            out.append(a)
        acc += seg
    if (out[-1] - pts[-1]).length > step * 0.3: out.append(pts[-1])
    return out


def _rdp(pts, tol):
    """Ramer-Douglas-Peucker: the polyline with the points that a straight run does not need removed."""
    if len(pts) < 3: return list(pts)
    a, b = pts[0], pts[-1]; ab = b - a; L = ab.length
    idx, dmax = -1, 0.0
    for i in range(1, len(pts) - 1):
        d = (pts[i] - a).cross(ab).length / L if L > 1e-9 else (pts[i] - a).length
        if d > dmax: dmax, idx = d, i
    if dmax > tol:
        left = _rdp(pts[:idx + 1], tol); right = _rdp(pts[idx:], tol)
        return left[:-1] + right
    return [a, b]


def _subdivide(pts, longest):
    out = [pts[0]]
    for a, b in zip(pts, pts[1:]):
        n = max(1, int(math.ceil((b - a).length / longest)))
        for k in range(1, n + 1): out.append(a.lerp(b, k / n))
    return out


def _relax(pts, iterations=2):
    pts = list(pts)
    for _ in range(iterations):
        new = list(pts)
        for i in range(1, len(pts) - 1): new[i] = pts[i].lerp((pts[i - 1] + pts[i + 1]) * 0.5, 0.5)
        pts = new
    return pts


def climb(head, start, clear_fn, bias=None, max_len=0.3, step=0.008, stop_e=80.0, wiggle=None, z_eq=None, hang=1.0, goal=None, goal_radius=0.04):
    """Follow the hair UP from `start` (a point on its hem) until elevation stop_e (degrees from CENTER) or
    max_len. Below the head's equator the lock hangs (straight up from its hem, pushed off the neck, the shirt
    and the head); above it the lock follows the head, up its slope, pushed `clear_fn` off it. `bias` drifts the
    lock sideways (its own sweep is the opposite). Returns the points, hem first."""
    z_eq = CENTER.z + 0.005 if z_eq is None else z_eq
    p = Vector(start)
    pts = [p.copy()]
    v = UP.copy()
    length = 0.0
    while length < max_len:
        clr = clear_fn(p, length)
        if p.z >= z_eq:
            d = (p - CENTER).normalized()
            up_t = UP - d * UP.dot(d)
            h = up_t.normalized() if up_t.length > 0.12 else v
        else:
            h = UP * hang + v * (1 - hang)
        if bias is not None: h = h + bias(p, length)
        if wiggle is not None: h = h + wiggle(p, length)
        v = (v * 0.5 + h.normalized() * 0.5).normalized()
        p = p + v * step
        if p.z >= z_eq:
            d = (p - CENTER).normalized()
            p = CENTER + d * (head.env(d) + clr)
            p, _, _ = head.push_out(p, clr * 0.6)     # never through the collar or the face
        else:
            p, _, _ = head.push_out(p, clr)
        pts.append(p.copy())
        length += step
        el = math.degrees(math.asin(max(-1.0, min(1.0, (p - CENTER).normalized().z))))
        if goal is not None:
            if (p - goal).length < goal_radius: break
        elif el >= stop_e: break
    return pts


def build_lock_mesh(bm, lock, col_layer, sides=5, tip_rings=2, rng=None):
    """Add the lock to bm: rings of `sides` vertices along its centre line (root first), tapering to a point (or a
    rounded blunt end), the root closed with a fan (it is buried in the cap or the head)."""
    pts = list(reversed(lock.pts))            # root .. tip: the climb ran hem first
    pts = _relax(_resample(pts, 0.010), 3)
    pts = _subdivide(_rdp(pts, 0.0011), 0.03)
    n = len(pts)
    if n < 3: return []
    # frames: tangent, across (B) and thickness (N) directions
    T = [((pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)])).normalized() for i in range(n)]
    up_ref = UP
    rings = []
    for i, p in enumerate(pts):
        s = i / (n - 1)
        # width: full through the root, a little fuller mid-lock, then the taper to the tip
        w = lock.half_w * (0.9 + 0.2 * math.sin(math.pi * min(1.0, s * 1.15)))
        h = lock.half_h * (0.95 + 0.1 * math.sin(math.pi * min(1.0, s * 1.15)))
        tp = _smooth((s - 0.45) / 0.55)
        rt = 1.0 - _smooth(s / 0.14)              # the root narrows and sinks into the layer below
        w *= (1 - lock.taper * tp) * (1 - 0.6 * rt)
        h *= (1 - 0.55 * lock.taper * tp) * (1 - 0.5 * rt)
        # across = T x (away from the head, taken from the head's centre)
        out = (p - CENTER); out.z *= 0.35
        Nn = (out - T[i] * out.dot(T[i])).normalized() if (out - T[i] * out.dot(T[i])).length > 1e-4 else up_ref
        B = T[i].cross(Nn).normalized()
        Nn = B.cross(T[i]).normalized()
        ring = []
        flex = lock.flex * (s ** 1.4)
        for k in range(sides):
            th = 2 * math.pi * k / sides
            v = bm.verts.new(p + B * (w * math.cos(th)) + Nn * (h * math.sin(th)))
            v[col_layer] = (flex, 0.0, 0.0, 1.0)
            ring.append(v)
        rings.append((ring, p, T[i], B, Nn, w, h))
    faces = []
    # root fan
    r0, p0 = rings[0][0], rings[0][1]
    c0 = bm.verts.new(p0 - rings[0][2] * lock.half_h * 0.5); c0[col_layer] = (0.0, 0.0, 0.0, 1.0)
    for k in range(sides): faces.append(bm.faces.new((c0, r0[(k + 1) % sides], r0[k])))
    for i in range(n - 1):
        a, b = rings[i][0], rings[i + 1][0]
        for k in range(sides):
            faces.append(bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k])))
    # tip cap
    rl, pl, Tl = rings[-1][0], rings[-1][1], rings[-1][2]
    ext = 0.004 if not lock.blunt else 0.0015
    tipv = bm.verts.new(pl + Tl * ext); tipv[col_layer] = (lock.flex, 0.0, 0.0, 1.0)
    for k in range(sides): faces.append(bm.faces.new((tipv, rl[k], rl[(k + 1) % sides])))
    return faces


def build_cap(bm, head, hem, col_layer, thick=0.007, segments=72, rings=40, floor=0.0, lift=None):
    """A thin star-shaped layer of hair on the head, from the hem up over the crown: it closes the gaps between the
    locks. `hem` is a function of azimuth -> z. Vertices on the head's surface pushed `thick` out."""
    grid = {}
    faces = []
    for j in range(rings + 1):
        e = math.radians(-35 + 125 * j / rings)          # elevation from below the ears to over the crown
        for i in range(segments):
            phi = 360 * i / segments - 180
            a = math.radians(phi)
            d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
            r = head.radial(d)
            if r is None: continue
            p = CENTER + d * r
            if p.z < hem(phi) + floor: continue
            t = thick if lift is None else thick + lift(p)
            grid[(i, j)] = (p + d * t, d)
    verts = {}
    for k, (p, d) in grid.items():
        v = bm.verts.new(p); v[col_layer] = (0.0, 0.0, 0.0, 1.0); verts[k] = v
    for j in range(rings):
        for i in range(segments):
            ks = [(i, j), ((i + 1) % segments, j), ((i + 1) % segments, j + 1), (i, j + 1)]
            if all(k in verts for k in ks):
                faces.append(bm.faces.new([verts[k] for k in ks]))
    top = [v for k, v in verts.items() if k[1] == rings]
    return faces


def lock_tips(hem, n, rng, phi0=-180.0, jitter=0.35):
    """Azimuths (degrees) round the head spaced (about) evenly along the hem's own length, jittered."""
    # arc length along the hem, at radius ~ 0.16 (the horizontal run) plus the hem's rise
    phis = np.linspace(-180, 180, 361)
    arc = [0.0]
    for a, b in zip(phis, phis[1:]):
        arc.append(arc[-1] + math.hypot(math.radians(b - a) * 0.16, hem(b) - hem(a)))
    arc = np.array(arc)
    out = []
    for k in range(n):
        u = (k + 0.5 + rng.uniform(-jitter, jitter)) / n * arc[-1]
        out.append(float(np.interp(u, arc, phis)))
    return out


# ------------------------------------------------------------------------------------------------------ recipes

class Cut:
    """A haircut: a hem, and how its locks are laid. Subclasses of this data only (see CUTS)."""

    def __init__(self, name, hem, tips=44, half_w=0.021, half_h=0.0085, clr=0.010, layers=3, layer_gap=0.0045,
                 taper=0.8, flex=0.35, blunt=False, sweep=(0.0, 0.0, 0.0), sweep_span=None, max_len=0.30, stop_e=80.0,
                 bell=0.0, bell_z=(1.34, 1.50), hang=1.0, quiff=None, wave=0.0, cap_thick=0.007, cap_floor=0.03, extra=None,
                 stop_jitter=8.0, curl=0.0, swirl=0.0, bridge=0.05, hem_jitter=0.0, width_jitter=0.15, goal=None, puffs=None):
        self.__dict__.update(locals()); del self.__dict__["self"]


def build_cut(head, cut, seed=0):
    """The hair mesh's bmesh for one haircut (world space)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    col = bm.verts.layers.float_color.new("Col")

    def clear_for(layer):
        def fn(p, length):
            c = cut.clr + layer * cut.layer_gap
            if cut.bell:
                c += cut.bell * _smooth((p.z - cut.bell_z[0]) / (cut.bell_z[1] - cut.bell_z[0])) * _smooth((cut.bell_z[1] + 0.12 - p.z) / 0.12)
            if cut.quiff is not None: c += cut.quiff(p)
            return c
        return fn

    # the cap first (under everything)
    if cut.cap_thick:
        build_cap(bm, head, cut.hem, col, thick=cut.cap_thick, floor=cut.cap_floor,
                  lift=(cut.quiff if cut.quiff is not None else None))

    locks = []
    head.bridge = cut.bridge
    tips = lock_tips(cut.hem, cut.tips, rng)
    goal = cut.goal(head) if cut.goal else None
    for idx, phi in enumerate(tips):
        z = cut.hem(phi) + (rng.uniform(-cut.hem_jitter, cut.hem_jitter) if cut.hem_jitter else 0.0)
        layer = idx % cut.layers
        clr_fn = clear_for(layer)
        z_eq = CENTER.z + 0.005
        start = head.hem_point(phi, z, clr_fn(Vector((0, 0, z)), 0.0), z_eq, cut.curl)
        sw = Vector(cut.sweep)

        def bias(pp, length, sw=sw):
            b = -sw * (0.6 + 0.4 * _smooth(length / 0.15))
            if cut.swirl:
                d = (pp - CENTER).normalized()
                el = math.degrees(math.asin(max(-1.0, min(1.0, d.z))))
                k = cut.swirl * _smooth((el - 48) / 38)
                b = b + Vector((-d.y, d.x, 0.0)).normalized() * k
            if goal is not None:
                to = goal - pp
                b = b + to.normalized() * 0.9 * _smooth((0.35 - to.length) / 0.25 + 0.6)
            return b

        def wig(pp, length, ph=rng.uniform(0, 6.28)):
            if not cut.wave: return Vector()
            return Vector((math.sin(length * 45 + ph), math.cos(length * 33 + ph), 0.0)) * cut.wave

        stop = cut.stop_e - rng.uniform(0, cut.stop_jitter)
        pts = climb(head, start, clr_fn, bias=bias, max_len=cut.max_len, stop_e=stop,
                    wiggle=wig, hang=cut.hang, goal=goal, goal_radius=0.045)
        wj = cut.width_jitter
        lock = Lock(pts, cut.half_w * rng.uniform(1 - wj, 1 + wj), cut.half_h * rng.uniform(0.9, 1.1), cut.taper, cut.flex, cut.blunt)
        locks.append(lock)
    if cut.puffs: build_puffs(bm, head, cut, rng, col)
    if cut.extra: locks.extend(cut.extra(head, rng))
    for lock in locks:
        build_lock_mesh(bm, lock, col)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def drop(head, start, v0, length, clear, gravity=1.0, step=0.011, wiggle=None):
    """A lock thrown from `start` along v0 and bent down by gravity, kept off the head and the shirt (a ponytail)."""
    p = Vector(start); v = v0.normalized()
    pts = [p.copy()]
    n = int(length / step)
    for i in range(n):
        v = (v + DOWN * (gravity * 0.09)).normalized()
        if wiggle: v = (v + wiggle(p, i * step)).normalized()
        p = p + v * step
        p, _, _ = head.push_out(p, clear)
        pts.append(p.copy())
    return pts


def ponytail_locks(head, rng, root, count=13, length=0.30, half_w=0.028, half_h=0.016, spread=0.022):
    """The tail: a bundle thrown from the root, up and back and falling."""
    locks = []
    for k in range(count):
        a = 2 * math.pi * k / count + rng.uniform(-0.2, 0.2)
        off = Vector((math.cos(a) * spread, 0.0, math.sin(a) * spread))
        v0 = Vector((rng.uniform(-0.08, 0.08) + off.x * 3, 0.32, 0.1 + off.z * 2))
        ph = rng.uniform(0, 6.28)
        pts = drop(head, root + off, v0, length * rng.uniform(0.85, 1.1), 0.02 + 0.004 * (k % 3), gravity=0.85,
                   wiggle=lambda p, s, ph=ph: Vector((math.sin(s * 18 + ph) * 0.06, 0.0, 0.0)))
        # this lock is built root-first; build_lock_mesh expects the hem-first order the climb gives
        locks.append(Lock(list(reversed(pts)), half_w * rng.uniform(0.85, 1.15), half_h, 0.8, 1.0, False))
    return locks


def build_puffs(bm, head, cut, rng, col, sides=9, rings=6):
    """Curls as overlapping lumpy puffs: two layers of soft blobs, flattened against the head and smaller toward
    the hem, so the outline is a cloud of curls and not a heap of balls."""
    P = cut.puffs
    dirs = []
    n = P["count"]
    for i in range(n):                      # a Fibonacci spread over the sphere
        y = 1 - 2 * (i + 0.5) / n
        rad = math.sqrt(1 - y * y)
        th = i * math.pi * (3 - math.sqrt(5))
        dirs.append(Vector((math.cos(th) * rad, math.sin(th) * rad, y)))
    for d in dirs:
        phi = math.degrees(math.atan2(d.x, -d.y))
        base = CENTER + d * head.env(d)
        if base.z < cut.hem(phi) + 0.012 or d.z < -0.05: continue
        for layer in (0, 1):
            if layer == 1 and (d.z < 0.2 or rng.random() < 0.35): continue
            near_hem = base.z < cut.hem(phi) + 0.05
            rp = rng.uniform(*P["size"]) * (0.75 if near_hem else 1.0) * (0.72 if layer == 1 else 1.0)
            # squash along the outward direction, jitter the position a little sideways
            t1 = d.orthogonal().normalized(); t2 = d.cross(t1)
            jit = (t1 * rng.uniform(-1, 1) + t2 * rng.uniform(-1, 1)) * rp * 0.35
            centre = base + jit + d * (rp * (0.28 if layer == 0 else 0.55) + P["clear"] + layer * P["layer"])
            flat = rng.uniform(0.62, 0.8)
            lump = [(rng.uniform(0.07, 0.15), rng.randint(2, 4), rng.uniform(0, 6.28)) for _ in range(2)]
            def pos(ph, th):
                r = rp
                for amp, freq, off in lump: r *= 1 + amp * math.sin(freq * th + off + 2 * ph)
                local = t1 * (math.sin(ph) * math.cos(th) * r) + t2 * (math.sin(ph) * math.sin(th) * r) + d * (math.cos(ph) * r * flat)
                return centre + local
            top = bm.verts.new(pos(0, 0)); top[col] = (0.1 + 0.15 * layer, 0, 0, 1)
            rows = []
            for j in range(1, rings):
                ph = math.pi * j / rings
                ring = []
                for k in range(sides):
                    th = 2 * math.pi * k / sides + (0.35 if j % 2 else 0.0)
                    v = bm.verts.new(pos(ph, th)); v[col] = (0.15 + 0.2 * layer, 0, 0, 1)
                    ring.append(v)
                rows.append(ring)
            bot = bm.verts.new(pos(math.pi, 0)); bot[col] = (0.1, 0, 0, 1)
            for k in range(sides): bm.faces.new((top, rows[0][(k + 1) % sides], rows[0][k]))
            for j in range(len(rows) - 1):
                for k in range(sides):
                    bm.faces.new((rows[j][k], rows[j][(k + 1) % sides], rows[j + 1][(k + 1) % sides], rows[j + 1][k]))
            for k in range(sides): bm.faces.new((bot, rows[-1][k], rows[-1][(k + 1) % sides]))


def to_object(bm, name, arm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def bind(obj, arm, material="Hero_01_HairTuft"):
    """Skin the hair to the rig: the head above the ears, the neck below them, the chest for the ends that reach
    the shoulders (so long hair rides on the back instead of through it), and give it the hair material (the game
    picks its colour by that name)."""
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()
    groups = {n: obj.vertex_groups.new(name=n) for n in ("Head", "Neck", "Chest")}
    mw = obj.matrix_world
    for v in obj.data.vertices:
        z = (mw @ v.co).z
        h = _smooth((z - 1.28) / 0.14)
        c = 1.0 - _smooth((z - 1.10) / 0.14)
        n = max(0.0, 1.0 - h - c)
        for name, w in (("Head", h), ("Neck", n), ("Chest", c)):
            if w > 0.002: groups[name].add([v.index], w, 'REPLACE')
    mod = obj.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
    obj.data.materials.append(bpy.data.materials.get(material) or bpy.data.materials.new(material))


# ------------------------------------------------------------------------------------------------------ the cuts

def _bump(centre_phi, centre_el, sigma, amp):
    """Extra clearance (metres): a soft mound on the head round one direction (a quiff, a puff)."""
    a = math.radians(centre_phi); e = math.radians(centre_el)
    c = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))

    def fn(p):
        d = (p - CENTER).normalized()
        ang = math.acos(max(-1.0, min(1.0, d.dot(c))))
        return amp * math.exp(-(ang / sigma) ** 2)
    return fn


SWEPT_HEM = hem_fn([(-180, 1.365), (-150, 1.385), (-120, 1.415), (-100, 1.445), (-80, 1.475), (-55, 1.53), (-35, 1.575),
                    (-10, 1.578), (15, 1.555), (40, 1.53), (60, 1.50), (80, 1.47), (100, 1.445), (120, 1.415), (150, 1.385), (180, 1.365)])


def _hem(*rows):
    """A symmetric hem from (azimuth 0..180, z) rows, with an optional extra for the left (negative) side."""
    return hem_fn([(a, z) for a, z in rows] + [(-a, z) for a, z in rows if a not in (0, 180)])


def _volume(amp, lo=-0.02, hi=0.03):
    """A general puff over the whole crown: extra clearance growing from the ears (z lo/hi relative to CENTER)."""
    return lambda p: amp * _smooth((p.z - (CENTER.z + lo)) / (hi - lo))


def _sum(*fns):
    return lambda p: sum(f(p) for f in fns)


BOB_HEM = _hem((0, 1.525), (35, 1.525), (55, 1.50), (72, 1.36), (90, 1.305), (130, 1.29), (180, 1.285))
LONG_HEM = _hem((0, 1.525), (35, 1.525), (55, 1.49), (72, 1.36), (90, 1.28), (125, 1.13), (155, 1.06), (180, 1.03))
PONY_HEM = hem_fn([(-180, 1.375), (-150, 1.39), (-120, 1.42), (-100, 1.45), (-80, 1.48), (-55, 1.51), (-30, 1.53),
                   (0, 1.535), (30, 1.53), (55, 1.51), (80, 1.48), (100, 1.45), (120, 1.42), (150, 1.39), (180, 1.375)])
CURLY_HEM = hem_fn([(-180, 1.35), (-120, 1.40), (-90, 1.44), (-60, 1.50), (-30, 1.55), (0, 1.56), (30, 1.55), (60, 1.50),
                    (90, 1.44), (120, 1.40), (180, 1.35)])


def pony_root(head):
    """Where the ponytail is tied: low on the back of the head, under a cap's edge and a band's."""
    d = Vector((0.0, 0.16, 1.44 - CENTER.z)).normalized()
    return CENTER + d * (head.env(d, 0.05) + 0.03)


def make_cuts():
    return {
        "Hair_Default": Cut("Swept", SWEPT_HEM, tips=50, half_w=0.025, half_h=0.0105, clr=0.015, layer_gap=0.006,
                            sweep=(0.9, -0.15, 0.0), quiff=_sum(_bump(-12, 50, 0.45, 0.028), _volume(0.010)),
                            flex=0.3, stop_e=89, stop_jitter=14, cap_thick=0.014, max_len=0.5, swirl=0.7, bridge=0.03,
                            hem_jitter=0.006),
        "Hair_Ponytail": Cut("Ponytail", PONY_HEM, tips=46, half_w=0.024, half_h=0.0105, clr=0.014, layer_gap=0.006,
                             quiff=_volume(0.008), flex=0.3, cap_thick=0.014, max_len=0.7, bridge=0.03, hem_jitter=0.006,
                             goal=pony_root, extra=lambda head, rng: ponytail_locks(head, rng, pony_root(head))),
        "Hair_Bob": Cut("Bob", BOB_HEM, tips=46, half_w=0.031, half_h=0.0125, clr=0.016, layer_gap=0.007, taper=0.7,
                        quiff=_volume(0.012), bell=0.020, bell_z=(1.32, 1.46), flex=0.6, stop_e=89, stop_jitter=16,
                        cap_thick=0.014, max_len=0.7, curl=0.012, swirl=0.8, bridge=0.10, hem_jitter=0.012, width_jitter=0.2,
                        wave=0.10),
        "Hair_Long": Cut("Long", LONG_HEM, tips=42, half_w=0.031, half_h=0.0125, clr=0.016, layer_gap=0.007, taper=0.75,
                         quiff=_volume(0.012), bell=0.018, bell_z=(1.30, 1.46), flex=0.9, stop_e=89, stop_jitter=16, max_len=0.9,
                         cap_thick=0.014, swirl=0.8, bridge=0.10, hem_jitter=0.02, width_jitter=0.2, wave=0.14),
        "Hair_Curly": Cut("Curly", CURLY_HEM, tips=0, cap_thick=0.02, flex=0.3, bridge=0.05,
                          puffs=dict(count=190, size=(0.036, 0.05), clear=0.002, layer=0.018)),
    }
