"""Hairstyles for Adnan's match heroes, made against his own heads.

Imported by matchhero_golf.py (it runs inside Blender, with his body already loaded). Every style is one mesh in the rig's
rest pose (metres, the face looks down -Y, Z up), rigid to the Head bone; the golfer FBX carries them as Hair_<Name> and the
game shows the picked one. Only two material roles: Hero_Hair (the hair colour) and Hero_HairB (a shade deeper, a few strands),
which the caller renames to the FBX's material names (Hair_M / Hair_F and Hair_Dark).

How: the skull is the body mesh's own head (decimated for the cap); a "cap" is that surface above a hairline, stood off it and
rolled down to the skin at its edge; locks are tubes laid along the skull at (azimuth, elevation) seen from its centre
(azimuth 0 the face, + toward +X; elevation 0 the ear line, 90 the crown). The same tools as the icon kit (avatar_kit.py), on
a realistic head. A style never stands more than a few centimetres off the skull except its own locks, tails and curls.
"""
import bpy, bmesh, math, random
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import avatar_kit as K
from avatar_kit import tube, blob, join

STYLES = ("Classic", "Afro")      # Classic is his own hair, given a scalp underneath; the Afro is made here (the free models are fitted by matchhero_mhhair.py)


class Skull:
    """His head, as something to lay hair on."""

    def __init__(self, body, female, neck_z):
        self.female = female
        src = bmesh.new(); src.from_mesh(body.data)
        src.transform(body.matrix_world)
        keep = {f for f in src.faces if f.calc_center_median().z > neck_z + 0.02}
        bmesh.ops.delete(src, geom=[f for f in src.faces if f not in keep], context='FACES')
        bmesh.ops.delete(src, geom=[v for v in src.verts if not v.link_faces], context='VERTS')
        src.verts.ensure_lookup_table(); src.faces.ensure_lookup_table()
        self.full = BVHTree.FromBMesh(src)
        me = bpy.data.meshes.new("skull"); src.to_mesh(me); src.free()

        def decimated(mesh, ratio):
            m2 = mesh.copy()
            o = bpy.data.objects.new("skull", m2); bpy.context.scene.collection.objects.link(o)
            mod = o.modifiers.new("d", 'DECIMATE'); mod.decimate_type = 'COLLAPSE'; mod.ratio = ratio; mod.use_collapse_triangulate = True
            with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]): bpy.ops.object.modifier_apply(modifier="d")
            bm = bmesh.new(); bm.from_mesh(o.data)
            bpy.data.objects.remove(o, do_unlink=True); bpy.data.meshes.remove(m2)
            return bm
        # the cap is cut from a lighter copy of the head; the soft scalp under a hair (matchhero_mhhair.fade_cap) from a finer one, its triangles hugging the real skin
        self.light = decimated(me, 0.24)
        self.fine = bmesh.new(); self.fine.from_mesh(me)             # (not decimated: a fade a centimetre wide needs triangles of a few millimetres)
        bpy.data.meshes.remove(me)
        zs = [v.co.z for v in self.light.verts]
        self.top = max(zs)
        # the middle of the skull: half way up the cranium, between the front of the forehead and the back
        cr = [v.co for v in self.light.verts if v.co.z > self.top - 0.13]
        self.C = Vector((0.0, (min(v.y for v in cr) + max(v.y for v in cr)) / 2, self.top - 0.115))

    # ---- directions and surface points
    def dir(self, az, el):
        a, e = math.radians(az), math.radians(el)
        return Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))).normalized()

    def hit(self, az, el, lift=0.0):
        """The skull's surface where it is seen at (az, el) from its middle, and its normal there; `lift` stands off it."""
        d = self.dir(az, el)
        r = self.full.ray_cast(self.C + d * 0.6, -d, 0.8)
        if r[0] is None: return None, None
        n = r[1].normalized()
        if n.dot(d) < 0: n = -n
        return r[0] + n * lift, n

    def back_at(self, z):
        """The y of the back of the head or neck at height z (the first surface met coming forward from behind)."""
        r = self.full.ray_cast(Vector((0.0, 0.6, z)), Vector((0, -1, 0)), 1.0)
        return r[0].y if r[0] is not None else 0.06

    def az_el(self, p):
        v = p - self.C
        return math.degrees(math.atan2(v.x, -v.y)), math.degrees(math.atan2(v.z, math.hypot(v.x, v.y)))

    # ---- a cap
    def cap(self, name, front, side, back, thick, crown, mat="Hero_Hair", rim_sink=1.2, passes=4, skirt=None, ear=0.0):
        """The skull above a hairline (elevation `front` at the face, `side` at the ears, `back` at the nape: a smooth curve round it),
        stood off it by thick + crown * sin(elevation), the edge rolled down to the skin."""
        b = (front - back) / 2; d = front - side - b
        line = lambda c: side + b * c + d * c * c
        bm = self.light.copy()
        def above(p):
            az, el = self.az_el(p)
            return el - line(math.cos(math.radians(az)))
        # cut the skull along the hairline itself (an edge that crosses it is split there), not along the triangles nearest it: a cut by
        # triangle gave a stepped edge that showed as a saw-tooth hairline and, on long hair, ragged strips
        s = {v: above(v.co) for v in bm.verts}
        cut = []
        for e in [e for e in bm.edges if s[e.verts[0]] * s[e.verts[1]] < 0]:
            v0, v1 = e.verts
            nv = bmesh.utils.edge_split(e, v0, s[v0] / (s[v0] - s[v1]))[1]
            s[nv] = 0.0; cut.append(nv)
        bmesh.ops.connect_verts(bm, verts=cut)
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if any(s[v] < -1e-6 for v in f.verts)], context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        for v in cut:                                                  # and the cut laid on the true skull
            if v.is_valid:
                az, el = self.az_el(v.co)
                p, _ = self.hit(az, line(math.cos(math.radians(az))))
                if p is not None: v.co = p
        bm.normal_update(); bm.verts.ensure_lookup_table()
        for _ in range(passes):                                        # the edge, smoothed along itself
            mv = {}
            for v in bm.verts:
                nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
                if len(nb) == 2: mv[v] = ((nb[0].co + nb[1].co) / 2 - v.co) * 0.5
            for v, dv in mv.items(): v.co += dv
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        if sum(f.normal.dot(f.calc_center_median() - self.C) for f in bm.faces) < 0: bmesh.ops.reverse_faces(bm, faces=bm.faces)
        bm.normal_update()
        def bump(p):
            """Stand off the ears: hair that covers one must clear it (the skin of an ear stands about 2 cm off the head)."""
            if not ear: return 0.0
            az, el = self.az_el(p)
            return ear * math.exp(-(((abs(az) - 100) / 24) ** 2) - (((el + 4) / 26) ** 2))
        off = lambda p: thick + crown * max(0.0, math.sin(math.radians(self.az_el(p)[1]))) + bump(p)
        for v in bm.verts: v.co += v.normal * off(v.co)
        hem = set()
        if skirt:
            # hair that hangs: from the sides and the back of the cap's edge, three strips down (the length at the back, to the chin at the sides),
            # drawn in toward the neck as they go
            length, az0 = skirt
            def drop(p):
                a = abs(self.az_el(p)[0])
                return length * (0.50 + 0.50 * max(0.0, min(1.0, (a - 90) / 70)))
            edges = [e for e in bm.edges if e.is_boundary and all(abs(self.az_el(v.co)[0]) > az0 for v in e.verts)]
            rings = []
            for step in range(3):
                ret = bmesh.ops.extrude_edge_only(bm, edges=edges)
                for g in ret["geom"]:
                    if isinstance(g, bmesh.types.BMVert):
                        g.co = Vector((g.co.x * 0.96, g.co.y + (0.012 if g.co.y > self.C.y else 0.0), g.co.z - drop(g.co) / 3.0))
                        if ear:
                            h = Vector((g.co.x - self.C.x, g.co.y - self.C.y, 0.0))
                            if h.length > 1e-6: g.co += h.normalized() * bump(g.co)
                edges = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMEdge) and g.is_boundary]
                newv = {g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)}
                rings.append([e for e in edges if all(v in newv for v in e.verts)])
            for _ in range(10):                                        # each ring drawn straight along itself: the hem is a line, not teeth
                for ring in rings:
                    nbs = {}
                    for e in ring:
                        for v in e.verts: nbs.setdefault(v, []).append(e.other_vert(v))
                    mv = {v: ((n[0].co + n[1].co) / 2 - v.co) * 0.6 for v, n in nbs.items() if len(n) == 2}
                    for v, dv in mv.items(): v.co += dv
            hem = set(edges)
        ret = bmesh.ops.extrude_edge_only(bm, edges=[e for e in bm.edges if e.is_boundary and e not in hem])
        for g in ret["geom"]:
            if isinstance(g, bmesh.types.BMVert): g.co -= (g.co - self.C).normalized() * off(g.co) * rim_sink
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        if sum(f.normal.dot(f.calc_center_median() - self.C) for f in bm.faces) < 0: bmesh.ops.reverse_faces(bm, faces=bm.faces)
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
        return K.new_obj(name, me, mat)

    # ---- a lock
    def ribbon(self, pts, w0, w1, thick, mat="Hero_Hair", name="Ribbon", base=0.0, segs=8):
        """A broad lock along the skull: pts are (azimuth, elevation, lift), w0 -> w1 its half width."""
        path = []
        for az, el, lift in pts:
            p, n = self.hit(az, el, lift + base)
            if p is None: return None
            path.append(p)
        k = len(path)
        radii = [(w0 + (w1 - w0) * (i / (k - 1)) ** 0.8, thick * (1.0 - 0.28 * (i / (k - 1)) ** 2)) for i in range(k)]
        radii[-1] = (radii[-1][0] * 0.55, radii[-1][1] * 0.65)             # the tip draws in
        return tube(name, path, radii, mat, segs=segs, per=2, ref=lambda c: (c - self.C).normalized(), cap_rings=2)


def _make(name, objs):
    return join(name, [o for o in objs if o])


# ---------------------------------------------------------------------------------------------------- the styles
def afro(sk, seed=4):
    """Big soft curls: a ball for each, all over the top and round the back, on a thick close cap (the free afro model is a lattice of small cards with
    open ear cut-outs: it cannot be made solid, so this one is built here)."""
    cap = sk.cap("AfroCap", front=36, side=0, back=-16, thick=0.018, crown=0.010)
    r = random.Random(seed); o = [cap]
    b = (36 + 16) / 2; d = 36 - 0 - b
    line = lambda c: 0 + b * c + d * c * c
    rad = 0.036 if not sk.female else 0.034
    for el, n_az in ((84, 1), (72, 6), (60, 9), (48, 12), (36, 15), (24, 17), (12, 18), (0, 18), (-10, 16)):
        for k in range(n_az):
            az = (360 / n_az) * k + r.uniform(-7, 7) + el * 3
            p, n = sk.hit(az, el, 0.0)
            if p is None: continue
            if el < line(math.cos(math.radians(az))) + 5: continue
            rr = rad * (1 + r.uniform(-0.10, 0.22))
            o.append(blob("Curl", p + n * (rr * 0.5), (rr, rr, rr * 0.92), "Hero_HairB" if r.random() < 0.25 else "Hero_Hair", segs=10, rings=7))
    return _make("Hair_Afro", o)


def scalp_under(sk):
    """A close cap in the hair colour, to go under his own hair: wherever his locks leave the scalp bare the cap shows (hair colour, not skin)."""
    return sk.cap("Hair_Scalp", front=29, side=5, back=-8, thick=0.007, crown=0.003)


BUILDERS = {"Afro": afro}
# each style's hairline (front, side, back elevations): the skull above it must never show
HAIRLINE = {"Classic": (29, 5, -8), "Afro": (36, 0, -16)}
LAST = {}


def exposed(obj, name, step_az=3, step_el=3, margin=4.0):
    """Directions above the style's hairline from which the first surface seen is skin: bald spots. Returns [(az, el)]."""
    sk = LAST["skull"]
    front, side, back = HAIRLINE[name]
    b = (front - back) / 2; d = front - side - b
    line = lambda c: side + b * c + d * c * c
    verts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    tree = BVHTree.FromPolygons(verts, [tuple(p.vertices) for p in obj.data.polygons])
    bad = []
    for az in range(-180, 180, step_az):
        for el in range(-40, 90, step_el):
            if el < line(math.cos(math.radians(az))) + margin: continue
            dv = sk.dir(az, el)
            o = sk.C + dv * 0.9
            rs = sk.full.ray_cast(o, -dv, 1.2)
            if rs[0] is None: continue
            rh = tree.ray_cast(o, -dv, 1.2)
            if rh[0] is None or rh[3] > rs[3] + 1e-4: bad.append((az, el))
    return bad


def build(body, female, neck_z):
    """{style name: object} for the styles made here, and the scalp cap ('Scalp') for the classic."""
    sk = Skull(body, female, neck_z)
    out = {n: f(sk) for n, f in BUILDERS.items()}
    out["Scalp"] = scalp_under(sk)
    LAST["skull"] = sk
    return out
