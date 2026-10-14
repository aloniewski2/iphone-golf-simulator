"""TANK v2 (female): a racerback V-neck tank built as a product. A closed vest blank (rings: hem, waist, bust, underarm, shoulder yoke, neck) from DESIGN numbers,
Catmull-Clark x3, then the openings are cut by smooth implicit fields (armhole ellipsoids that reach further inboard at the back = racerback, V neckline in front,
scoop behind), boundary vertices snapped onto the cut curves, binding (neck + armholes) as a distance field for the maps, rib hem band as separate geometry.
Body mesh space: metres, Z up, hero faces -Y, hero's right = +X.   Blender -b --factory-startup --python tank2.py -- <hero> <design> <out.blend>"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import polo2 as P2
from polo2 import new_object, polys, boundary_loops_poly, dist_to_polyline, dist_to_segments, smoothstep, vnormals, set_vattr, get_vattr, apply_modifier

FRONT, BACK, HEMBAND = 0, 1, 2
KIND = {'shell': 0, 'band': 12}


def design_female_tank():
    return dict(
        name='female_tank', n=2.45,
        # z, half width a, front y, back y
        rings=[(.992, .127, -.126, .083), (1.030, .121, -.126, .077), (1.070, .117, -.127, .080), (1.130, .121, -.129, .083), (1.190, .139, -.136, .090),
               (1.230, .145, -.144, .096), (1.270, .146, -.136, .100), (1.310, .156, -.110, .099), (1.345, .152, -.088, .093), (1.372, .118, -.074, .080), (1.395, .074, -.060, .068)],
        thick=.0023, hem_band=.030, band_thick=.0030, fit_gap=.0042,
        # armholes: ellipsoids (centre, semi-axes) per side; the back one reaches further inboard (racerback)
        arm_front=dict(c=(.176, -.048, 1.334), s=(.082, .105, .090)),
        arm_back=dict(c=(.176, .060, 1.336), s=(.128, .112, .092)),
        # neckline: V in front (tip z, slope), scoop behind
        v_tip=1.250, v_slope=2.05, back_z=1.356, back_k=7.0, trim_w=.0075, roles=('Kit_Shirt', 'Kit_ShirtTrim'),
    )


DESIGNS = {'female_tank': design_female_tank}


def ring_pts(a, yf, yb, z, n, k=16):
    th = np.radians(360.0 / k * np.arange(k)); s, c = np.sin(th), np.cos(th)
    ym = (yf + yb) / 2; h = (yb - yf) / 2
    return np.stack([a * np.sign(s) * np.abs(s) ** (2 / n), ym - h * np.sign(c) * np.abs(c) ** (2 / n), np.full(k, z)], 1)


def build_cage(D):
    V = []; rings = []
    for (z, a, yf, yb) in D['rings']:
        rings.append([len(V) + i for i in range(16)]); V.extend(ring_pts(a, yf, yb, z, D['n']))
    V = np.array(V); F = []; panel = []
    for r in range(len(rings) - 1):
        for k in range(16):
            k2 = (k + 1) % 16
            F.append((rings[r][k], rings[r][k2], rings[r + 1][k2], rings[r + 1][k]))
            panel.append(FRONT if (k >= 12 or k <= 3) else BACK)
    # panel coordinates: u = signed arc along the ring from the panel's centre line, v = arc up the column from the hem
    cum = []
    for r in range(len(rings)):
        pts = V[rings[r]]; seg = np.linalg.norm(np.diff(np.vstack([pts, pts[:1]]), axis=0), axis=1); cum.append(np.concatenate([[0], np.cumsum(seg)]))
    vv = [np.zeros(16)]
    for r in range(1, len(rings)): vv.append(vv[-1] + np.linalg.norm(V[rings[r]] - V[rings[r - 1]], axis=1))
    uvmap = {}
    for r in range(len(rings)):
        tot = cum[r][16]
        for k in range(16):
            sf = cum[r][k] if k <= 8 else cum[r][k] - tot           # signed from the front centre (k = 0): right +, left -
            sb = (cum[r][k] - cum[r][8]) if k >= 0 else 0           # signed from the back centre (k = 8)
            uvmap[(rings[r][k], FRONT)] = (sf, vv[r][k]); uvmap[(rings[r][k], BACK)] = (sb, vv[r][k])
    corner = [[uvmap[(v, pn)] for v in f] for f, pn in zip(F, panel)]
    return V, F, np.array(panel), corner, rings


def make_cage_object(name, V, F, panel, corner):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in V], [], [tuple(f) for f in F]); me.update()
    at = me.attributes.new('panel', 'INT', 'FACE'); at.data.foreach_set('value', np.asarray(panel, np.int32))
    layer = me.uv_layers.new(name='UVMap'); layer.data.foreach_set('uv', np.array([c for f in corner for c in f], np.float32).ravel())
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def f_ellipsoid(pos, c, s, side):
    d = (pos - np.array([side * c[0], c[1], c[2]])) / np.array(s)
    return (np.sqrt((d * d).sum(1)) - 1.0) * min(s) * .85          # ~signed distance in metres


def cut_fields(pos, D, yc):
    """f > 0 keeps. Armhole ellipsoids (|x| side), V / scoop neckline."""
    out = np.full(len(pos), 9.0)
    for side in (1, -1):
        on = (np.sign(pos[:, 0]) == side) | (np.abs(pos[:, 0]) < .02)
        for e in ('arm_front', 'arm_back'):
            f = f_ellipsoid(pos, D[e]['c'], D[e]['s'], side)
            out = np.minimum(out, np.where(on, f, 9.0))
    ax = np.abs(pos[:, 0])
    zf = D['v_tip'] + D['v_slope'] * ax                              # V in front
    zb = D['back_z'] + D['back_k'] * ax ** 2                           # scoop behind
    w = smoothstep(-.012, .012, pos[:, 1] - yc)
    zn = (1 - w) * zf + w * zb
    fneck = (zn - pos[:, 2]) * .45                                     # ~distance to the neckline curve
    return np.minimum(out, fneck)


def build(hero_id, design, outp):
    D = DESIGNS[design]()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import cv2io, glib as G, fit
    from mathutils.bvhtree import BVHTree
    V, F, panel, corner, rings = build_cage(D)
    cage = make_cage_object('Tank_cage', V, F, panel, corner)
    yc = float(np.mean([(r[2] + r[3]) / 2 for r in D['rings']]))
    # outward orientation
    me = cage.data; bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    ax = Vector((0, yc, 0)); flip = [f for f in bm.faces if f.normal.dot(f.calc_center_median() - Vector((0, yc, f.calc_center_median().z))) < 0]
    bmesh.ops.reverse_faces(bm, faces=flip); bm.to_mesh(me); bm.free(); me.update()
    sub = cage.modifiers.new('sub', 'SUBSURF'); sub.levels = 3; sub.render_levels = 3; sub.uv_smooth = 'PRESERVE_BOUNDARIES'
    shell = cage.copy(); shell.data = cage.data.copy(); shell.name = 'Tank'; bpy.context.scene.collection.objects.link(shell)
    cage.modifiers.clear(); cage.hide_render = True
    apply_modifier(shell, shell.modifiers['sub'])
    hero = cv2io.Hero(hero_id); body = G.Body(hero)
    pos, faces = polys(shell)
    tri = np.array([t for f in faces for t in ([f[0], f[1], f[2]], [f[0], f[2], f[3]])])
    x, d = fit.push_clear_multi(body, pos, tri, gap=D['fit_gap'])
    mv = np.linalg.norm(x - pos, axis=1)
    print('FIT moved %d verts, max %.1f mm, min clearance %.1f mm' % (int((mv > 1e-4).sum()), mv.max() * 1000, d.min() * 1000))
    pos = x
    shell.data.vertices.foreach_set('co', pos.astype(np.float32).ravel()); shell.data.update()
    # ---- openings: kill faces by field, then snap the boundary onto the implicit curves
    f = cut_fields(pos, D, yc)
    bm = bmesh.new(); bm.from_mesh(shell.data); bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    kill = [fc for fc in bm.faces if np.mean([f[v.index] for v in fc.verts]) < 0]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    # drop loose verts / small islands: keep the largest connected piece
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(shell.data); bm.free()
    pos, faces = polys(shell)
    loops = boundary_loops_poly(faces)
    hem = min(loops, key=lambda l: pos[l, 2].mean())
    # Newton snap of the opening boundaries (not the hem) onto f = 0, moving ALONG the surface (tangent plane) only
    bnd = np.unique(np.concatenate([l for l in loops if l is not hem]))
    nrm0 = vnormals(pos, faces)
    p0 = pos.copy()
    for it in range(5):
        fb = cut_fields(pos[bnd], D, yc)
        eps = 1e-4; g = np.zeros((len(bnd), 3))
        for a_ in range(3):
            dp = np.zeros(3); dp[a_] = eps
            g[:, a_] = (cut_fields(pos[bnd] + dp, D, yc) - fb) / eps
        nb_ = nrm0[bnd]; g = g - (g * nb_).sum(1, keepdims=True) * nb_
        gn = (g * g).sum(1) + 1e-7
        step = np.clip((fb / gn)[:, None] * g, -.004, .004)
        pos[bnd] = pos[bnd] - step
    # a snap that wants to travel far is chasing a degenerate gradient: cap it, then drop faces the snap stretched into fins
    dd_ = pos[bnd] - p0[bnd]; ln_ = np.linalg.norm(dd_, axis=1)
    pos[bnd] = p0[bnd] + dd_ * np.minimum(1.0, .011 / np.maximum(ln_, 1e-9))[:, None]
    shell.data.vertices.foreach_set('co', pos.astype(np.float32).ravel()); shell.data.update()
    bmx = bmesh.new(); bmx.from_mesh(shell.data); bmx.faces.ensure_lookup_table()
    fins = [fc for fc in bmx.faces if max(e.calc_length() for e in fc.edges) > .028]
    bmesh.ops.delete(bmx, geom=fins, context='FACES')
    bmesh.ops.delete(bmx, geom=[v for v in bmx.verts if not v.link_faces], context='VERTS')
    bmx.to_mesh(shell.data); bmx.free()
    pos, faces = polys(shell)
    loops = boundary_loops_poly(faces); hem = min(loops, key=lambda l: pos[l, 2].mean())
    print('FINS removed', len(fins))
    print('SNAP boundary verts %d (moves capped at 11 mm)' % len(bnd))
    # smooth each opening's boundary along itself (Taubin: no shrink): the Newton snap leaves small kinks where the V neckline crosses the grid
    for l in [l for l in loops if l is not hem]:
        if len(l) < 6: continue
        edge_set = {(min(a_, b_), max(a_, b_)) for fc in faces for a_, b_ in zip(fc, list(fc[1:]) + [fc[0]])}
        closed = (min(l[0], l[-1]), max(l[0], l[-1])) in edge_set
        for it in range(10):
            for lam in (.5, -.53):
                P_ = pos[l]
                if closed: nxt = (np.roll(P_, -1, 0) + np.roll(P_, 1, 0)) / 2; pos[l] = P_ + lam * (nxt - P_)
                else:
                    nxt = (P_[2:] + P_[:-2]) / 2; pos[l[1:-1]] = P_[1:-1] + lam * (nxt - P_[1:-1])
    shell.data.vertices.foreach_set('co', pos.astype(np.float32).ravel()); shell.data.update()
    # ---- fields
    pan = np.zeros(len(faces), np.int32); shell.data.attributes['panel'].data.foreach_get('value', pan)
    ef = {}
    for fi, fc in enumerate(faces):
        for i in range(len(fc)):
            a_, b_ = int(fc[i]), int(fc[(i + 1) % len(fc)]); ef.setdefault((min(a_, b_), max(a_, b_)), []).append(pan[fi])
    segs = np.array([k for k, v in ef.items() if len(v) == 2 and v[0] != v[1]])
    d_seam = dist_to_segments(pos, pos[segs[:, 0]], pos[segs[:, 1]]) if len(segs) else np.full(len(pos), 1.0)
    d_hem = dist_to_polyline(pos, pos[hem])
    other = [l for l in loops if l is not hem]
    d_bind = np.minimum.reduce([dist_to_polyline(pos, pos[l]) for l in other]) if other else np.full(len(pos), 1.0)
    n = vnormals(pos, faces)
    # soft vertical drape + a turned edge on the binding (rolled, 0.8 mm proud)
    thv = np.arctan2(pos[:, 0], -(pos[:, 1] - yc)); z = pos[:, 2]
    disp = .0006 * np.sin(thv * 9 + z * 40) * smoothstep(1.2, 1.05, z) + .0008 * (1 - smoothstep(D['trim_w'] - .002, D['trim_w'] + .001, d_bind))
    pos = pos + n * disp[:, None]
    shell.data.vertices.foreach_set('co', pos.astype(np.float32).ravel()); shell.data.update()
    for k, v in (('d_seam', d_seam), ('d_hem', d_hem), ('d_bind', d_bind)): set_vattr(shell, k, v)
    # ---- rib hem band as a separate piece (lifted 1 mm)
    bm = bmesh.new(); bm.from_mesh(shell.data); bm.verts.ensure_lookup_table()
    dh = bm.verts.layers.float.get('d_hem'); hw = D['hem_band']
    cut = [fc for fc in bm.faces if all(v[dh] < hw for v in fc.verts)]
    bb = bmesh.new(); vmap = {}
    for fc in cut:
        vs = []
        for v in fc.verts:
            if v.index not in vmap: vmap[v.index] = bb.verts.new(v.co + v.normal * .001)
            vs.append(vmap[v.index])
        bb.faces.new(vs)
    band_me = bpy.data.meshes.new('HemBand'); bb.to_mesh(band_me); bb.free()
    band = bpy.data.objects.new('HemBand', band_me); bpy.context.scene.collection.objects.link(band)
    bp_ = np.zeros(len(band_me.vertices) * 3); band_me.vertices.foreach_get('co', bp_); bp_ = bp_.reshape(-1, 3)
    thb = np.arctan2(bp_[:, 0], -(bp_[:, 1] - yc))
    layer = band_me.uv_layers.new(name='UVMap')
    li = np.zeros(len(band_me.loops), np.int32); band_me.loops.foreach_get('vertex_index', li)
    lst = np.zeros(len(band_me.polygons), np.int32); band_me.polygons.foreach_get('loop_start', lst)
    lto = np.zeros(len(band_me.polygons), np.int32); band_me.polygons.foreach_get('loop_total', lto)
    cu = np.zeros((len(li), 2))
    for s0, t0 in zip(lst, lto):
        th_ = thb[li[s0:s0 + t0]].copy(); th_ = th_[0] + np.angle(np.exp(1j * (th_ - th_[0])))
        cu[s0:s0 + t0, 0] = th_ * .13; cu[s0:s0 + t0, 1] = bp_[li[s0:s0 + t0], 2]
    layer.data.foreach_set('uv', cu.astype(np.float32).ravel())
    set_vattr(band, 'd_hem', dist_to_polyline(bp_, pos[hem], closed=True))
    for nm, v in (('face_kind', KIND['band']), ('fabric', 9), ('role', 0), ('panel', HEMBAND)):
        a = band_me.attributes.new(nm, 'INT', 'FACE'); a.data.foreach_set('value', np.full(len(band_me.polygons), v, np.int32))
    bmesh.ops.delete(bm, geom=cut, context='FACES'); bm.to_mesh(shell.data); bm.free()
    nf = len(shell.data.polygons)
    for nm, v in (('face_kind', KIND['shell']), ('fabric', 7), ('role', 0)):
        a = shell.data.attributes.new(nm, 'INT', 'FACE'); a.data.foreach_set('value', np.full(nf, v, np.int32))
    parts = [shell, band]
    thick = {shell.name: D['thick'], band.name: D['band_thick']}
    for o in parts:
        o.data.shade_smooth()
        for nm in ('outer', 'inner', 'rim'): o.data.materials.append(bpy.data.materials.get(nm) or bpy.data.materials.new(nm))
        sm = o.modifiers.new('solid', 'SOLIDIFY'); sm.thickness = thick[o.name]; sm.offset = -1; sm.use_even_offset = True
        sm.use_quality_normals = True; sm.use_rim = True; sm.material_offset = 1; sm.material_offset_rim = 2
        apply_modifier(o, sm)
        if o is shell:
            me_ = o.data
            fl = {k: get_vattr(o, k) for k in ('d_hem', 'd_bind')}
            farv = np.minimum(fl['d_hem'], fl['d_bind']) > .06
            bm = bmesh.new(); bm.from_mesh(me_); bm.verts.ensure_lookup_table()
            drop = [fc for fc in bm.faces if fc.material_index == 1 and all(farv[v.index] for v in fc.verts)]
            bmesh.ops.delete(bm, geom=drop, context='FACES'); bm.to_mesh(me_); bm.free()
        bv = o.modifiers.new('bevel', 'BEVEL'); bv.width = min(.0010, thick[o.name] * .42); bv.segments = 2; bv.limit_method = 'ANGLE'; bv.angle_limit = math.radians(55)
        apply_modifier(o, bv)
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = shell
    bpy.ops.object.join()
    shell.name = 'Kit_Top'; shell['roles'] = list(D['roles'])
    bpy.ops.wm.save_as_mainfile(filepath=outp)
    print('TANK faces %d, band %d' % (nf, len(band_me.polygons)))


if __name__ == '__main__' or 'tank2.py' in ' '.join(sys.argv):
    a = sys.argv[sys.argv.index('--') + 1:]
    build(a[0], a[1], a[2])
