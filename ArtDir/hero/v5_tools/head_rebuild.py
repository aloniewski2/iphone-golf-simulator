"""Hero head rebuild: one real head instead of a face shell on a hidden egg.
The V4 body's head is two separate pieces: a FACE SHELL (face, cheeks, sides, open at the back and top, with a
saw-tooth rim) and a separate hidden narrow 'egg' (the back and top of the head, shaped for the old hair to rest on).
Bare / short haircuts exposed both. This step:
  1. deletes the egg, the old baked side-hair tufts and the floating hair-edge fringe,
  2. builds a SKULL: a head-sized ellipsoid (sized from the face shell) as a lat-long grid; wherever the face shell
     exists the skull stays 4 mm under it (hidden), elsewhere it is the back and crown of the head,
  3. eases the face shell's jagged rim (and two rings in) back onto the skull so the edge lies flush,
  4. joins the skull into Body_Skin: forehead skin (UVs on the forehead, so the skin tint recolours it), 100% Head
     bone, visible through the outfit mask,
  5. seats the floating eyebrow slabs onto the face.
Run first in build_v5.py ('head' step) so the caps, hair and hats that follow fit the real skull."""
import bpy, bmesh, math
from mathutils import Vector
from mathutils.bvhtree import BVHTree

# skull ellipsoid (world metres): half-width from the face shell at the temples, a rounded chibi crown, a back of
# the head ~1.1x the face depth -- not the old 26 cm egg
SKULL = dict(a=.188, b=.200, c=.205, yc=.035, zc=1.425)
LON, LAT = 144, 72
RINGS_BACK = 18
EASE_RIM = False
SIDE_CUT = 70      # deg from the front: face shell kept inside this

def r_ell(d, S=SKULL):
    return 1.0 / math.sqrt((d.x / S['a']) ** 2 + (d.y / S['b']) ** 2 + (d.z / S['c']) ** 2)

def build(context, report):
    body = bpy.data.objects['Body_Skin']; me = body.data; mw = body.matrix_world; inv = mw.inverted()
    g = body.vertex_groups['Default_VisibleSkin'].index; hg = body.vertex_groups['Head'].index
    mats = [m.name if m else '' for m in me.materials]
    skin_i = next(i for i, n in enumerate(mats) if n.startswith('skin_WarmSkin'))
    cf = [i for i, n in enumerate(mats) if n.startswith('skin_CoveredFoundation')]
    tuft = [i for i, n in enumerate(mats) if n.startswith(('skin_BlondHair', 'Hero_01_HairTuft'))]
    O = Vector((0, SKULL['yc'], SKULL['zc']))
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table(); dl = bm.verts.layers.deform.active
    uvl = bm.loops.layers.uv.active
    W = lambda v: mw @ v.co
    head = lambda v: v[dl].get(hg, 0) > .5
    vis = lambda v: v[dl].get(g, 0) > .5
    fore = min((f for f in bm.faces if f.material_index == skin_i and all(vis(v) for v in f.verts)
                and 1.40 < W(f.verts[0]).z < 1.46 and W(f.verts[0]).y < -.05), key=lambda f: abs(W(f.verts[0]).x), default=None)
    fuv = sum((l[uvl].uv for l in fore.loops), Vector((0, 0))) / len(fore.loops) if fore else Vector((.71, .34))
    # ---- 1. delete the egg, tufts, fringe
    egg = [f for f in bm.faces if f.material_index in cf and all(head(v) and not vis(v) for v in f.verts)]
    fringe = [f for f in bm.faces if f.material_index in cf and all(vis(v) for v in f.verts) and (mw @ f.calc_center_median()).z > 1.42]
    tufts = [f for f in bm.faces if f.material_index in tuft]
    report['head_deleted'] = dict(egg=len(egg), fringe=len(fringe), tufts=len(tufts))
    # the sides of the face shell (behind the eyes: the old ear / side-hair zone, lumpy, never meant to be seen) go
    # too; the skull forms the sides of the head
    def side(f):
        c = mw @ f.calc_center_median(); a = abs(math.degrees(math.atan2(c.x, -(c.y - O.y))))
        return f.material_index == skin_i and a > SIDE_CUT and c.z > 1.31 and all(head(v) for v in f.verts)
    sides = [f for f in bm.faces if side(f)]
    report['head_deleted']['face_sides'] = len(sides)
    bmesh.ops.delete(bm, geom=list(set(egg + fringe + tufts + sides)), context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.verts.ensure_lookup_table()
    # face shell = visible head skin faces (for the 'stay under the face' test)
    shell = [f for f in bm.faces if all(head(v) and vis(v) for v in f.verts) and f.material_index != skin_i or
             (f.material_index == skin_i and all(head(v) for v in f.verts))]
    sv = []; idx = {}
    for f in shell:
        for v in f.verts:
            if v not in idx: idx[v] = len(sv); sv.append(W(v))
    ftree = BVHTree.FromPolygons(sv, [[idx[v] for v in f.verts] for f in shell])
    # ---- 3. ease the jagged face rim (+2 rings) onto the skull where it stands outside it
    rimv = set(v for e in bm.edges if e.is_boundary and all(head(v) and vis(v) for v in e.verts) for v in e.verts)
    ring = set(rimv); front = set(rimv)
    for _ in range(2):
        nxt = set(e.other_vert(v) for v in front for e in v.link_edges if head(e.other_vert(v)) and vis(e.other_vert(v))) - ring
        ring |= nxt; front = nxt
    eased = 0
    for v in (ring if EASE_RIM else ()):   # off: the skull now follows the face edge instead (easing made a fold)
        w = W(v); d = w - O
        if d.length < 1e-4 or w.z < 1.36: continue
        re = r_ell(d.normalized())
        if d.length > re - .002:
            k = 1.0 if v in rimv else .6
            v.co = inv @ (O + d.normalized() * (d.length + (re - .002 - d.length) * k)); eased += 1
    report['rim_verts_eased'] = eased
    # ---- 3b. smooth the face shell's OUTER edge loops (saw-teeth -> a clean curve), then tuck the edge 3 mm in
    bset_e = set(e for e in bm.edges if e.is_boundary and all(head(v) and vis(v) for v in e.verts))
    used = set(); edge_loops = []
    for e in bset_e:
        if e in used: continue
        L = [e.verts[0], e.verts[1]]; used.add(e); cur = e.verts[1]
        while True:
            nx = [x for x in cur.link_edges if x in bset_e and x not in used]
            if not nx: break
            used.add(nx[0]); cur = nx[0].other_vert(cur)
            if cur == L[0]: break
            L.append(cur)
        edge_loops.append(L)
    def browish(L): return all(all(f.material_index in cf for f in v.link_faces) for v in L)
    outer = [L for L in edge_loops if len(L) >= 30 and not browish(L)]   # eye / mouth holes and the eyebrow slabs stay
    for L in outer:
        n = len(L); P = [W(v) for v in L]
        # Taubin (lambda / mu) smoothing: removes the saw-teeth WITHOUT shrinking the loop (plain averaging pulled
        # the forehead edge down over the painted brows)
        for _ in range(30):
            for lam in (.5, -.53):
                P = [P[i] + ((P[i - 1] + P[(i + 1) % n]) * .5 - P[i]) * lam for i in range(n)]
        for v, p in zip(L, P): v.co = inv @ p
    edge = set(v for L in outer for v in L)
    # soften the face shell near its edge (4 rows in): the temple / jaw zone was lumpy under the old hair.
    # Eyes, brows, nose and mouth (front 50 deg, z 1.30-1.46) are left exactly as they are.
    zone = set(edge); front_ = set(edge)
    for _ in range(4):
        nx = set(e.other_vert(v) for v in front_ for e in v.link_edges if head(e.other_vert(v)) and vis(e.other_vert(v))) - zone
        zone |= nx; front_ = nx
    def feature(v):
        w = W(v); a = abs(math.degrees(math.atan2(w.x, -(w.y - O.y))))
        return a < 50 and 1.30 < w.z < 1.46
    zone = [v for v in zone if not feature(v)]
    for _ in range(15): bmesh.ops.smooth_vert(bm, verts=zone, factor=.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    report['face_edge_softened'] = len(zone)
    report['face_edge_loops'] = [len(L) for L in outer]
    rimv = edge
    # ---- 2. skull grown from the face edge: ring 0 IS the face's outer loop; each ring steps along the head's
    # ellipsoid toward a pole at the back-top of the head; radius blends from the face edge's own radius into the
    # ellipsoid. One continuous surface (nothing overlapping, nothing to hide).
    L = max(outer, key=len); n = len(L)
    # orient the loop consistently and start from the vertex nearest the forehead centre
    P0 = [W(v) for v in L]
    pole = Vector((0, .62, .78)).normalized()
    rings = [L]
    K = RINGS_BACK
    def slerp(a, b, t):
        c = max(-1.0, min(1.0, a.dot(b))); om = math.acos(c)
        if om < 1e-5: return a.copy()
        return (a * math.sin((1 - t) * om) + b * math.sin(t * om)) / math.sin(om)
    base = []
    for p in P0:
        d = (p - O).normalized(); base.append((d, (p - O).length - r_ell(d)))
    for k in range(1, K):
        t = k / K; te = t ** 0.85; ring = []
        for d0, gap in base:
            d = slerp(d0, pole, te).normalized()
            r = r_ell(d) + gap * (1 - t) ** 2
            nv = bm.verts.new(inv @ (O + d * r)); nv[dl][hg] = 1.0; nv[dl][g] = 1.0; ring.append(nv)
        rings.append(ring)
    top = bm.verts.new(inv @ (O + pole * r_ell(pole))); top[dl][hg] = 1.0; top[dl][g] = 1.0
    nf = []
    for k in range(len(rings) - 1):
        a, b = rings[k], rings[k + 1]
        for i2 in range(n):
            j2 = (i2 + 1) % n
            try: nf.append(bm.faces.new((a[i2], a[j2], b[j2], b[i2])))
            except ValueError: pass
    for i2 in range(n):
        try: nf.append(bm.faces.new((rings[-1][i2], rings[-1][(i2 + 1) % n], top)))
        except ValueError: pass
    bm.normal_update()
    for f in nf:
        if f.normal.dot((mw @ f.calc_center_median()) - O) < 0: f.normal_flip()
        f.material_index = skin_i; f.smooth = True
        for l in f.loops: l[uvl].uv = fuv
    # relax the new skull (not ring 0): even spacing, no pinching toward the pole, then back onto the head shape
    newv = [v for r_ in rings[1:] for v in r_] + [top]
    for _ in range(12):
        bmesh.ops.smooth_vert(bm, verts=newv, factor=.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        for v in newv:
            w = W(v); dd = w - O
            if dd.length > 1e-4:
                dn = dd.normalized(); v.co = inv @ (O + dn * max(dd.length, r_ell(dn) * .98))
    report['skull_faces'] = len(nf); report['skull_rings'] = K; report['face_loop'] = n
    # ---- 5. eyebrows
    brow = [f for f in bm.faces if f.material_index in cf and all(vis(v) for v in f.verts) and 1.28 < W(f.verts[0]).z < 1.42]
    bset = set(v for f in brow for v in f.verts)
    skin_f = [f for f in bm.faces if f.material_index == skin_i and f not in set(nf) and all(vis(v) for v in f.verts) and W(f.verts[0]).z > 1.25]
    if brow and skin_f:
        sv2 = []; idx2 = {}
        for f in skin_f:
            for v in f.verts:
                if v not in idx2: idx2[v] = len(sv2); sv2.append(W(v))
        tree = BVHTree.FromPolygons(sv2, [[idx2[v] for v in f.verts] for f in skin_f])
        seated = 0
        for v in bset:
            d = (W(v) - O).normalized(); hit = tree.ray_cast(O + d * .5, -d, .6)
            if hit[0]: v.co = inv @ (hit[0] + d * .0015); seated += 1
        report['brow_verts_seated'] = seated
    bm.to_mesh(me); bm.free(); me.update()
