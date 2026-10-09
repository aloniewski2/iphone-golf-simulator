"""Hero V5 body pass (runs after the hair): stocky plate legs, knee weights, shorts that follow the thighs,
visor refit over the new hair, sock cuffs, armpit weight smoothing. All in the rest pose, world space.
Called from build_v5.py: run(context, steps, report)."""
import bpy, bmesh, math
from mathutils import Vector
from mathutils.bvhtree import BVHTree

def ss(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)

def leg_axis(rig, side, z):
    """Point on the leg bone chain (UpperLeg head -> LowerLeg tail) at height z, world space."""
    mw = rig.matrix_world
    ul = rig.data.bones['UpperLeg.' + side]; ll = rig.data.bones['LowerLeg.' + side]
    pts = [mw @ ul.head_local, mw @ ll.head_local, mw @ ll.tail_local]
    for a, b in ((pts[0], pts[1]), (pts[1], pts[2])):
        if min(a.z, b.z) - 1e-6 <= z <= max(a.z, b.z) + 1e-6:
            t = (z - a.z) / (b.z - a.z) if abs(b.z - a.z) > 1e-6 else 0
            return a.lerp(b, t)
    return pts[2] if z < pts[2].z else pts[0]

# radial profile along the leg (height z -> scale of the offset from the leg axis); plate: full calf, narrow
# ankle into the sock, knee slightly narrower with a kneecap, fuller thigh up under the shorts
PROFILE = [(.20, 1.0), (.24, 1.06), (.29, 1.09), (.34, 1.15), (.37, 1.18), (.41, 1.14), (.44, 1.10), (.48, 1.13), (.52, 1.15), (.62, 1.15), (.70, 1.0)]
def profile(z):
    if z <= PROFILE[0][0]: return PROFILE[0][1]
    for (z0, s0), (z1, s1) in zip(PROFILE, PROFILE[1:]):
        if z <= z1: return s0 + (s1 - s0) * ss(z0, z1, z)
    return PROFILE[-1][1]

def leg_weight(o, v):
    return sum(g.weight for g in v.groups if o.vertex_groups[g.group].name.startswith(('UpperLeg', 'LowerLeg', 'Foot')))

def reshape_legs(rig, o, report, margin=0.0, zmax=.68, only_leg_weighted=True, calf_back=.05, constant=None):
    mw = o.matrix_world; inv = mw.inverted(); moved = 0
    for v in o.data.vertices:
        p = mw @ v.co
        if p.z > zmax or p.z < .18 or abs(p.x) < .015: continue
        if only_leg_weighted and leg_weight(o, v) < .5: continue
        side = 'L' if p.x > 0 else 'R'
        a = leg_axis(rig, side, p.z)
        off = Vector((p.x - a.x, p.y - a.y, 0))
        s = profile(p.z) if constant is None else 1 + (constant - 1) * (1 - ss(.60, .70, p.z))
        # calf bulges to the back (+Y), kneecap to the front (-Y)
        back = max(0.0, off.normalized().y) if off.length > 1e-5 else 0
        s *= 1 + calf_back * back * ss(.30, .36, p.z) * (1 - ss(.40, .44, p.z))
        front = max(0.0, -off.normalized().y) if off.length > 1e-5 else 0
        knee = .006 * front ** 2 * ss(.40, .43, p.z) * (1 - ss(.46, .49, p.z))
        n = off * s + (off.normalized() * margin if off.length > 1e-5 else Vector()) + Vector((0, -knee, 0))
        q = Vector((a.x + n.x, a.y + n.y, p.z))
        v.co = inv @ q; moved += 1
    report[o.name + '_leg_verts_reshaped'] = moved

def knee_weights(o, report, lo=.40, hi=.48):
    """Smooth UpperLeg/LowerLeg blend across the knee band (no hard split -> no candy-wrapper collapse)."""
    mw = o.matrix_world; changed = 0
    for side in ('L', 'R'):
        gu = o.vertex_groups.get('UpperLeg.' + side); gl = o.vertex_groups.get('LowerLeg.' + side)
        if not gu or not gl: continue
        for v in o.data.vertices:
            p = mw @ v.co
            if not (lo - .03 <= p.z <= hi + .03) or (p.x > 0) != (side == 'L'): continue
            ws = {o.vertex_groups[g.group].name: g.weight for g in v.groups}
            wl = ws.get('LowerLeg.' + side, 0); wu = ws.get('UpperLeg.' + side, 0)
            tot = wl + wu
            if tot < .6: continue
            t = ss(lo, hi, p.z)                           # 0 below (lower leg) .. 1 above (thigh)
            nu = tot * t; nl = tot * (1 - t)
            gu.add([v.index], nu, 'REPLACE'); gl.add([v.index], nl, 'REPLACE'); changed += 1
    report[o.name + '_knee_weights'] = changed

def transfer_weights(src, dst, report, zmax=.70, name='legs'):
    """Copy all vertex-group weights from the nearest point of src's surface onto dst vertices below zmax."""
    for g in src.vertex_groups:
        if g.name not in dst.vertex_groups: dst.vertex_groups.new(name=g.name)
    sel = dst.vertex_groups.get('_xfer') or dst.vertex_groups.new(name='_xfer')
    mw = dst.matrix_world
    idx = [v.index for v in dst.data.vertices if (mw @ v.co).z < zmax]
    sel.add(idx, 1.0, 'REPLACE')
    m = dst.modifiers.new('xfer', 'DATA_TRANSFER'); m.object = src
    m.use_vert_data = True; m.data_types_verts = {'VGROUP_WEIGHTS'}; m.vert_mapping = 'POLYINTERP_NEAREST'
    m.layers_vgroup_select_src = 'ALL'; m.layers_vgroup_select_dst = 'NAME'; m.mix_mode = 'REPLACE'; m.vertex_group = '_xfer'
    bpy.context.view_layer.objects.active = dst
    # the body's covered skin (torso / thighs under the clothes) is hidden by a Mask modifier: read the full body
    masks = [mm for mm in src.modifiers if mm.type == 'MASK']; state = [(mm.show_viewport, mm.show_render) for mm in masks]
    for mm in masks: mm.show_viewport = mm.show_render = False
    dst.modifiers.move(dst.modifiers.find(m.name), 0)          # before the armature (rest pose)
    bpy.context.view_layer.update()
    with bpy.context.temp_override(object=dst, active_object=dst):
        bpy.ops.object.modifier_apply(modifier=m.name)
    for mm, (a1, a2) in zip(masks, state): mm.show_viewport, mm.show_render = a1, a2
    g_ = dst.vertex_groups.get('_xfer')
    if g_: dst.vertex_groups.remove(g_)
    report[dst.name + '_weights_from_' + src.name] = len(idx)

def smooth_weights(o, pick, groups, iters=6, report=None, key='smooth'):
    """Laplacian smoothing of the listed groups over the picked vertices (normalised afterwards)."""
    bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
    mw = o.matrix_world
    sel = [v.index for v in bm.verts if pick(mw @ v.co)]
    if not sel: bm.free(); return
    nb = {i: [e.other_vert(bm.verts[i]).index for e in bm.verts[i].link_edges] for i in sel}
    bm.free()
    gi = {g: o.vertex_groups[g].index for g in groups if g in o.vertex_groups}
    def w(i, g):
        for x in o.data.vertices[i].groups:
            if x.group == gi[g]: return x.weight
        return 0.0
    W = {i: {g: w(i, g) for g in gi} for i in set(sel) | {j for i in sel for j in nb[i]}}
    for _ in range(iters):
        N = {}
        for i in sel:
            ns = nb[i] or [i]
            N[i] = {g: .5 * W[i][g] + .5 * sum(W[j][g] for j in ns) / len(ns) for g in gi}
        for i in sel: W[i] = N[i]
    for i in sel:
        tot0 = sum(w(i, g) for g in gi); tot = sum(W[i].values())
        for g in gi:
            val = W[i][g] * (tot0 / tot if tot > 1e-6 else 0)
            o.vertex_groups[g].add([i], val, 'REPLACE')
    if report is not None: report[o.name + '_' + key] = len(sel)

def fit_visor(hat, hair, report, clearance=.005, fracs=(.1, .3, .5, .7, .9)):
    """Push the visor out per horizontal angle so its band sits ON the new hair (strap over the hair)."""
    dg = bpy.context.evaluated_depsgraph_get()
    hme = hair.evaluated_get(dg).to_mesh(); hvs = [hair.matrix_world @ v.co for v in hme.vertices]
    tree = BVHTree.FromPolygons(hvs, [list(p.vertices) for p in hme.polygons])
    mw = hat.matrix_world; inv = mw.inverted()
    pts = [mw @ v.co for v in hat.data.vertices]
    cx = sum(p.x for p in pts) / len(pts); cy = sum(p.y for p in pts) / len(pts)
    N = 72; inner = [9.0] * N; zlo = [9.0] * N; zhi = [-9.0] * N
    def bin_(p): return int(((math.atan2(p.y - cy, p.x - cx) + math.pi) / (2 * math.pi)) * N) % N
    for p in pts:
        k = bin_(p); r = math.hypot(p.x - cx, p.y - cy)
        inner[k] = min(inner[k], r); zlo[k] = min(zlo[k], p.z); zhi[k] = max(zhi[k], p.z)
    push = [0.0] * N
    for k in range(N):
        ang = (k + .5) / N * 2 * math.pi - math.pi; dv = Vector((math.cos(ang), math.sin(ang), 0))
        need = 0.0
        for z in [zlo[k] + (zhi[k] - zlo[k]) * f for f in fracs]:
            o = Vector((cx, cy, z)) + dv * .4
            hit = tree.ray_cast(o, -dv, .4)
            if hit[0]:
                hr = math.hypot(hit[0].x - cx, hit[0].y - cy)
                need = max(need, hr + clearance - inner[k])
        push[k] = max(0.0, need)
    sm = [max(push[(k + d) % N] for d in range(-2, 3)) for k in range(N)]    # no dents: widen with neighbours
    for v, p in zip(hat.data.vertices, pts):
        k = bin_(p); dv = Vector((p.x - cx, p.y - cy, 0)); dv = dv.normalized() if dv.length > 1e-6 else Vector((0, -1, 0))
        v.co = inv @ (p + dv * sm[k])
    report['visor_push_max_mm'] = round(max(sm) * 1000, 1); report['visor_push_mean_mm'] = round(sum(sm) / N * 1000, 1)

def hair_under_band(hat, hair, report, clearance=.004, falloff=.012):
    """The visor stays where the V4 fit put it; the hair is pressed flat under its band (and bulges above it),
    so the strap sits ON the hair with no slit and no lock poking through the band."""
    mw = hat.matrix_world; pts = [mw @ v.co for v in hat.data.vertices]
    cx = sum(p.x for p in pts) / len(pts); cy = sum(p.y for p in pts) / len(pts)
    N = 72; inner = [9.0] * N; zlo = [9.0] * N; zhi = [-9.0] * N
    def bin_(x, y): return int(((math.atan2(y - cy, x - cx) + math.pi) / (2 * math.pi)) * N) % N
    for p in pts:
        k = bin_(p.x, p.y); r = math.hypot(p.x - cx, p.y - cy)
        if r < .08: continue
        inner[k] = min(inner[k], r); zlo[k] = min(zlo[k], p.z); zhi[k] = max(zhi[k], p.z)
    # the brim reaches far forward: its inner edge, not the brim tip, is the band there (inner[] already min radius)
    hm = hair.matrix_world; hinv = hm.inverted(); n = 0
    for v in hair.data.vertices:
        p = hm @ v.co; k = bin_(p.x, p.y)
        if zlo[k] > 8: continue
        lo, hi = zlo[k], zhi[k]
        w = ss(lo - falloff, lo + .003, p.z) * (1 - ss(hi - .003, hi + falloff, p.z))
        if w <= 0: continue
        r = math.hypot(p.x - cx, p.y - cy); rmax = inner[k] - clearance
        if r < .05: continue
        # outer hair pressed in to the band, and the hair surface pulled out to meet it (no slit under the strap):
        # only verts already within 2.5 cm of the band are pulled out, so the skull-side cap stays put
        if r <= rmax and r < rmax - .025: continue
        nr = r + (rmax - r) * w
        f = nr / r; q = Vector((cx + (p.x - cx) * f, cy + (p.y - cy) * f, p.z)); v.co = hinv @ q; n += 1
    report['hair_verts_under_band'] = n

def band_fill(hat, hair, report, reach=.012, rows=6, name='Hair_BandFill'):
    """'Hair_BandFill': a hair-coloured ring that bridges the band strip, per angle, from the outer hair surface just
    below the band to just above it (slight bulge), so bare hair (no headwear) has no groove where the band sits.
    Shown only with no headwear (HeroKit / locker tag 'None'). Same material and Head weights as the hair."""
    from mathutils.bvhtree import BVHTree
    mw = hat.matrix_world; pts = [mw @ v.co for v in hat.data.vertices]
    cx = sum(p.x for p in pts) / len(pts); cy = sum(p.y for p in pts) / len(pts)
    N = 72; zlo = [9.0] * N; zhi = [-9.0] * N
    def bin_(x, y): return int(((math.atan2(y - cy, x - cx) + math.pi) / (2 * math.pi)) * N) % N
    for p in pts:
        if math.hypot(p.x - cx, p.y - cy) < .08: continue
        k = bin_(p.x, p.y); zlo[k] = min(zlo[k], p.z); zhi[k] = max(zhi[k], p.z)
    # fill empty bins from neighbours (brim-only directions)
    for k in range(N):
        if zlo[k] > 8:
            for d in range(1, N):
                for kk in ((k + d) % N, (k - d) % N):
                    if zlo[kk] < 8: zlo[k], zhi[k] = zlo[kk], zhi[kk]; break
                if zlo[k] < 8: break
    dg = bpy.context.evaluated_depsgraph_get(); hme = hair.evaluated_get(dg).to_mesh()
    hv = [hair.matrix_world @ v.co for v in hme.vertices]
    tree = BVHTree.FromPolygons(hv, [list(p.vertices) for p in hme.polygons])
    def outer_r(ang, z, fallback):
        d = Vector((math.cos(ang), math.sin(ang), 0)); hit = tree.ray_cast(Vector((cx, cy, z)) + d * .5, -d, .5)
        return math.hypot(hit[0].x - cx, hit[0].y - cy) - .002 if hit[0] else fallback
    verts = []; faces = []
    for k in range(N):
        ang = (k + .5) / N * 2 * math.pi - math.pi
        z0, z1 = zlo[k] - reach, zhi[k] + reach
        r0 = outer_r(ang, z0, .12); r1 = outer_r(ang, z1, .12)
        for m in range(rows + 1):
            t = m / rows; z = z0 + (z1 - z0) * t
            r = r0 + (r1 - r0) * t + .006 * math.sin(math.pi * t)          # gentle bulge, reads as hair under nothing
            verts.append(Vector((cx + math.cos(ang) * r, cy + math.sin(ang) * r, z)))
    for k in range(N):
        k2 = (k + 1) % N
        for m in range(rows):
            a, b = k * (rows + 1) + m, k2 * (rows + 1) + m
            faces.append((a, b, b + 1, a + 1))
    old = bpy.data.objects.get(name)
    if old: bpy.data.objects.remove(old)
    me = bpy.data.meshes.new(name); me.from_pydata([hair.matrix_world.inverted() @ v for v in verts], [], faces)
    for p in me.polygons: p.use_smooth = True
    import bmesh as _bm
    bm = _bm.new(); bm.from_mesh(me); _bm.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    for m in hair.data.materials: me.materials.append(m)
    o = bpy.data.objects.new(name, me)
    for c in hair.users_collection: c.objects.link(o)
    o.parent = hair.parent; o.matrix_world = hair.matrix_world.copy()
    vg = o.vertex_groups.new(name='Head'); vg.add(list(range(len(me.vertices))), 1.0, 'REPLACE')
    for mod in hair.modifiers:
        if mod.type == 'ARMATURE': a = o.modifiers.new('Armature', 'ARMATURE'); a.object = mod.object
    report[name + '_tris'] = len(faces) * 2

def arm_axis(rig, side, p):
    """Closest point on the arm chain (UpperArm head -> LowerArm tail) and the chain parameter 0..2."""
    mw = rig.matrix_world
    ua = rig.data.bones['UpperArm.' + side]; la = rig.data.bones['LowerArm.' + side]
    best = None
    for seg, (a, b) in enumerate(((mw @ ua.head_local, mw @ ua.tail_local), (mw @ la.head_local, mw @ la.tail_local))):
        ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared)); q = a + ab * t
        d = (p - q).length
        if best is None or d < best[0]: best = (d, q, seg + t, ab.normalized())
    return best[1], best[2], best[3]

def chubby_arms(rig, o, report, key, gain=.12, margin=0.0):
    """Plate arms: a fuller upper arm and forearm (x1.12, easing to x1.04 at the wrist); hands untouched."""
    mw = o.matrix_world; inv = mw.inverted(); n = 0
    for v in o.data.vertices:
        ws = {o.vertex_groups[g.group].name: g.weight for g in v.groups}
        side = 'L' if ws.get('UpperArm.L', 0) + ws.get('LowerArm.L', 0) > .5 else 'R' if ws.get('UpperArm.R', 0) + ws.get('LowerArm.R', 0) > .5 else None
        if not side: continue
        p = mw @ v.co; q, t, ax = arm_axis(rig, side, p)
        off = p - q; off = off - ax * off.dot(ax)
        if t < .15: s = 1 + gain * ss(0, .15, t)              # blend in from the shoulder
        elif t > 1.7: s = 1 + gain * (1 - .65 * ss(1.7, 2.0, t))  # ease toward the wrist
        else: s = 1 + gain
        v.co = inv @ (q + off * s + (off.normalized() * margin if off.length > 1e-6 else Vector())); n += 1
    report[o.name + '_' + key] = n

def armpits(o, report, key):
    """Torso-side underarm verts shed UpperArm weight to the Chest (the torso panel doesn't lift with the arm)."""
    mw = o.matrix_world; n = 0
    for side, sx in (('L', 1), ('R', -1)):
        ga = o.vertex_groups.get('UpperArm.' + side); gc = o.vertex_groups.get('Chest')
        if not ga or not gc: continue
        for v in o.data.vertices:
            p = mw @ v.co
            if p.x * sx <= 0 or not (.95 < p.z < 1.16): continue
            wa = 0; wc = 0
            for x in v.groups:
                if x.group == ga.index: wa = x.weight
                if x.group == gc.index: wc = x.weight
            if wa <= 0: continue
            keep = ss(.17, .23, abs(p.x))            # full arm weight only past the shoulder line
            if keep >= .999: continue
            move = wa * (1 - keep)
            ga.add([v.index], wa - move, 'REPLACE'); gc.add([v.index], wc + move, 'REPLACE'); n += 1
    report[o.name + '_' + key] = n

def visible_thighs(o, report, zmax=.64):
    g = o.vertex_groups.get('Default_VisibleSkin')
    if not g: return
    mw = o.matrix_world
    idx = [v.index for v in o.data.vertices if (mw @ v.co).z < zmax and leg_weight(o, v) > .6]
    g.add(idx, 1.0, 'REPLACE'); report['thigh_skin_kept_under_shorts'] = len(idx)

def socks(rig, shoes, report):
    """Chunky sock cuff: the sock top flares a touch over the (bigger) lower calf so the ankle steps into the shoe."""
    mw = shoes.matrix_world; inv = mw.inverted(); n = 0
    for v in shoes.data.vertices:
        p = mw @ v.co
        if not (.17 < p.z < .30): continue
        side = 'L' if p.x > 0 else 'R'; a = leg_axis(rig, side, min(p.z, .165 + .1))
        off = Vector((p.x - a.x, p.y - a.y, 0))
        s = 1 + .10 * ss(.19, .27, p.z)          # cuff a touch wider than the ankle (1.06-1.09) it meets
        q = Vector((a.x + off.x * s, a.y + off.y * s, p.z)); v.co = inv @ q; n += 1
    report['sock_verts'] = n

def run(context, steps, report):
    rig = bpy.data.objects['Hero_01_Rig']; body = bpy.data.objects['Body_Skin']
    shorts = bpy.data.objects['Shorts_Default']; shoes = bpy.data.objects['Shoes_Default']
    hat = bpy.data.objects['Hat_Visor']; hair = bpy.data.objects['Hair_Default']; shirt = bpy.data.objects['Shirt_Default']
    if 'legs' in steps:
        reshape_legs(rig, body, report)
        knee_weights(body, report)
        # (visible_thighs() is not used: the dive "patch" was a foreshortened thigh, not a hole, and revealing the
        #  covered thigh doubled the body's triangles; the shorts now follow the thigh weights instead)
    if 'shorts' in steps:
        # one scale per leg for the whole shorts leg (hem layers / piping stay together): the thigh's own + ease
        reshape_legs(rig, shorts, report, margin=.004, zmax=.70, only_leg_weighted=False, calf_back=0, constant=1.16)
        transfer_weights(body, shorts, report, zmax=.70)
    if 'socks' in steps:
        socks(rig, shoes, report)
        # mobile budget: the shoes were the heaviest clothing piece (10.4k tris); collapse-decimate to ~65%
        dm = shoes.modifiers.new('budget', 'DECIMATE'); dm.ratio = .65; dm.use_collapse_triangulate = False
        shoes.modifiers.move(shoes.modifiers.find(dm.name), 0)
        with bpy.context.temp_override(object=shoes, active_object=shoes):
            bpy.ops.object.modifier_apply(modifier=dm.name)
        report['shoe_tris'] = sum(len(p.vertices) - 2 for p in shoes.data.polygons)
    if 'visor' in steps:
        # sculpted (Tripo) hair: press only the strip under the band (tight falloff) so the strap sits over the hair
        # while the locks above / below keep their sculpted overhang
        if 'tripo' in steps:
            band_fill(hat, hair, report)                                 # the natural hair strip, shown only with no hat
            hair_under_band(hat, hair, report, falloff=.004)             # strap over the hair when a hat is on
        else:
            hair_under_band(hat, hair, report)
    if 'arms' in steps:
        chubby_arms(rig, body, report, 'arms'); chubby_arms(rig, shirt, report, 'arms', margin=.003)
    if 'armpit' in steps:
        armpits(body, report, 'armpit'); armpits(shirt, report, 'armpit')
