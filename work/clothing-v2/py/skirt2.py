"""SKIRT v2: a knife-pleated skirt (tennis female: white, rib waistband, worn over tight under-shorts) built as a product.
Rings (silhouette from the waist down, a gentle A-line) -> every ring is resampled by arc length so every pleat is the same width; each pleat is a flat outer panel
and a recessed underfold (depth grows from the stitched-down yoke to the hem) -> a closed grid with its own pleat fields (phase, depth) for the maps; rib waistband as a
separate piece; the tight under-shorts are appended from their own build and joined, so the whole thing is one Kit_Bottom piece.
Blender -b --factory-startup --python skirt2.py -- <hero> <skirt design> <under blend or -> <out.blend>"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from polo2 import new_object, apply_modifier, smoothstep, set_vattr, get_vattr, dist_to_polyline

KIND = {'skirt': 11, 'band': 12, 'under': 18}


def design_female_tennis_skirt():
    return dict(
        name='female_tennis_skirt', n=2.35, pleats=40,
        # z, half width a, front y, back y   (silhouette of the skirt's outer face, above the pleat depth)
        rings=[(.955, .131, -.123, .083), (.935, .146, -.126, .100), (.900, .170, -.130, .122), (.860, .190, -.133, .134), (.820, .203, -.135, .136), (.780, .212, -.137, .136), (.745, .219, -.139, .137), (.715, .2235, -.1405, .1375), (.688, .2275, -.142, .138)],
        z_wb=(.950, .988), wb_t=.0034, thick=.0022, depth=.0125, z_stitch=.905, z_free=.80, fit_gap=.007, rows=12,
        pleat_u=[0.0, .78, .84, .93, .97], roles=('Kit_Shorts', 'Kit_ShortsBand', 'Kit_Buckle'),
    )


def design_female_golf_skort():
    """GolfKitHero_Female cream pleated skort: wider pleats, flat waistband with belt loops (the belt is its own piece), under-shorts in the same cloth."""
    D = design_female_tennis_skirt()
    D.update(name='female_golf_skort', pleats=26, depth=.0150, z_wb=(.952, .990), wb_t=.0030, loops=dict(angles=(-30, 30, -104, 104, 180)), band_fabric=6, band_role=0, under_role=0)
    return D


DESIGNS = {'female_tennis_skirt': design_female_tennis_skirt, 'female_golf_skort': design_female_golf_skort}


def ring_curve(a, yf, yb, n, k=1440):
    th = np.linspace(0, 2 * math.pi, k, endpoint=False); s, c = np.sin(th), np.cos(th)
    ym = (yf + yb) / 2; h = (yb - yf) / 2
    x = a * np.sign(s) * np.abs(s) ** (2 / n); y = ym - h * np.sign(c) * np.abs(c) ** (2 / n)
    return np.stack([x, y], 1)


def resample_ring(curve, M):
    """M points at equal arc length starting at the front centre; returns points, outward 2D normals, perimeter"""
    pts = np.vstack([curve, curve[:1]]); seg = np.linalg.norm(np.diff(pts, axis=0), axis=1); cum = np.concatenate([[0], np.cumsum(seg)]); per = cum[-1]
    t = np.linspace(0, per, M, endpoint=False)
    x = np.interp(t, cum, pts[:, 0]); y = np.interp(t, cum, pts[:, 1])
    P = np.stack([x, y], 1)
    tg = np.roll(P, -1, 0) - np.roll(P, 1, 0); tg /= np.linalg.norm(tg, axis=1, keepdims=True)
    nrm = np.stack([tg[:, 1], -tg[:, 0]], 1)
    cen = P.mean(0)
    if np.mean(np.sum(nrm * (P - cen), axis=1)) < 0: nrm = -nrm
    return P, nrm, per


def build(hero_id, design, under_path, outp):
    D = DESIGNS[design]()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import cv2io, glib as G, fit
    hero = cv2io.Hero(hero_id); body = G.body_no_arms(G.Body(hero))
    zr = np.array([r[0] for r in D['rings']])
    rows = D['rows']; z0 = D['z_wb'][0]; z1 = D['rings'][-1][0]
    zs = np.linspace(z0, z1, rows)
    def prm(z, i):
        return np.interp(z, zr[::-1], [r[i] for r in D['rings']][::-1])
    pl = D['pleats']; U = np.array(D['pleat_u']); nu = len(U)
    N = pl * nu
    V = np.zeros((rows, N, 3)); PU = np.zeros((rows, N)); PD = np.zeros((rows, N)); ARC = np.zeros((rows, N))
    perims = []
    for j, z in enumerate(zs):
        crv = ring_curve(prm(z, 1), prm(z, 2), prm(z, 3), D['n'])
        # arc-length parameters of the pleat stops: pleat k covers [k*P, (k+1)*P)
        pts = np.vstack([crv, crv[:1]]); seg = np.linalg.norm(np.diff(pts, axis=0), axis=1); cum = np.concatenate([[0], np.cumsum(seg)]); per = cum[-1]; perims.append(per)
        Pp = per / pl
        s_ = np.concatenate([(k + U) * Pp for k in range(pl)])
        x = np.interp(s_, cum, pts[:, 0]); y = np.interp(s_, cum, pts[:, 1])
        P2 = np.stack([x, y], 1)
        # normals from a dense neighbourhood
        eps = Pp * .1
        xa = np.interp(s_ + eps, cum, pts[:, 0], period=per); ya = np.interp(s_ + eps, cum, pts[:, 1], period=per)
        xb = np.interp(s_ - eps, cum, pts[:, 0], period=per); yb_ = np.interp(s_ - eps, cum, pts[:, 1], period=per)
        tg = np.stack([xa - xb, ya - yb_], 1); tg /= np.linalg.norm(tg, axis=1, keepdims=True) + 1e-12
        nrm = np.stack([tg[:, 1], -tg[:, 0]], 1)
        if np.mean(np.sum(nrm * (P2 - P2.mean(0)), axis=1)) < 0: nrm = -nrm
        u = np.tile(U, pl)
        depth = D['depth'] * smoothstep(D['z_stitch'], D['z_free'], z)                      # stitched flat above the yoke, free below
        r = -depth * (smoothstep(.78, .84, u) - smoothstep(.93, 1.0, u))                    # recessed underfold, flat outer panel
        P3 = P2 + nrm * r[:, None]
        V[j, :, 0] = P3[:, 0]; V[j, :, 1] = P3[:, 1]; V[j, :, 2] = z
        PU[j] = u; PD[j] = depth / D['depth']; ARC[j] = s_
    # fit: only where the skirt touches the body (the A-line is roomy; the waist is snug)
    tri = []
    for j in range(rows - 1):
        for i in range(N):
            i2 = (i + 1) % N; a_, b_, c_, d_ = j * N + i, j * N + i2, (j + 1) * N + i2, (j + 1) * N + i
            tri += [(a_, b_, c_), (a_, c_, d_)]
    tri = np.array(tri)
    xv, dcl = fit.push_clear(body, V.reshape(-1, 3), tri, gap=D['fit_gap'], passes=5, cap=.03)
    print('FIT skirt: moved %d verts, max %.1f mm, min clearance %.1f mm' % (int((np.linalg.norm(xv - V.reshape(-1, 3), axis=1) > 1e-4).sum()), np.linalg.norm(xv - V.reshape(-1, 3), axis=1).max() * 1000, dcl.min() * 1000))
    V = xv.reshape(V.shape)
    # ---- the grid object; UV: 4 panels (islands) so the long ring never becomes one 1.2 m strip
    Vf = V.reshape(-1, 3)
    me = bpy.data.meshes.new('Skirt')
    F = []; corners = []
    npan = 4; per_pan = N // npan
    vrow = np.zeros(rows)
    for j in range(1, rows): vrow[j] = vrow[j - 1] + np.mean(np.linalg.norm(V[j] - V[j - 1], axis=1))
    for j in range(rows - 1):
        for i in range(N):
            i2 = (i + 1) % N
            F.append((j * N + i, j * N + i2, (j + 1) * N + i2, (j + 1) * N + i))
            pan = i // per_pan
            def ucoord(ii, jj):                                           # arc length from the panel's first column, wrapped within the panel
                base = ARC[jj, pan * per_pan]
                a_ = ARC[jj, ii % N]
                if ii >= N: a_ += perims[jj]
                return a_ - base
            corners.append([(ucoord(i, j), vrow[j]), (ucoord(i + 1, j), vrow[j]), (ucoord(i + 1, j + 1), vrow[j + 1]), (ucoord(i, j + 1), vrow[j + 1])])
    me.from_pydata([tuple(v) for v in Vf], [], F); me.update()
    layer = me.uv_layers.new(name='UVMap'); layer.data.foreach_set('uv', np.array([c for f in corners for c in f], np.float32).ravel())
    for nm, v in (('face_kind', KIND['skirt']), ('fabric', 6), ('role', 0), ('panel', 0)):
        a = me.attributes.new(nm, 'INT', 'FACE'); a.data.foreach_set('value', np.full(len(me.polygons), v, np.int32))
    set_vattr_me = lambda nm, vals: (me.attributes.new(nm, 'FLOAT', 'POINT').data.foreach_set('value', np.asarray(vals, np.float32)))
    set_vattr_me('pl_u', PU.ravel()); set_vattr_me('pl_depth', PD.ravel())
    zv = Vf[:, 2]
    set_vattr_me('d_hem', zv - zs[-1]); set_vattr_me('d_top', zs[0] - zv)
    skirt = bpy.data.objects.new('Skirt', me); bpy.context.scene.collection.objects.link(skirt)
    # orientation: outward from the axis
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    f0 = bm.faces[len(bm.faces) // 2]; c0 = f0.calc_center_median(); cx = Vector((Vf[:, 0].mean(), Vf[:, 1].mean(), c0.z))
    if f0.normal.dot(c0 - cx) < 0: bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free(); me.update()
    # ---- waistband: rib band (ring loft), overlapping the skirt top by 3 mm
    wz0, wz1 = D['z_wb']
    bands = 6; bz = np.linspace(wz0 - .003, wz1, bands); Nb = 160
    BV = np.zeros((bands, Nb, 3))
    for j, z in enumerate(bz):
        crv = ring_curve(np.interp(z, [wz0, wz1], [prm(wz0, 1), .129]) + .0008, prm(wz0, 2), prm(wz0, 3), D['n'])
        P_, n_, per_ = resample_ring(crv, Nb)
        BV[j, :, :2] = P_ + n_ * .0012; BV[j, :, 2] = z
    xb, _ = fit.push_clear(body, BV.reshape(-1, 3), np.array([t for j in range(bands - 1) for i in range(Nb) for t in (((j * Nb + i), (j * Nb + (i + 1) % Nb), ((j + 1) * Nb + (i + 1) % Nb)), ((j * Nb + i), ((j + 1) * Nb + (i + 1) % Nb), ((j + 1) * Nb + i)))]), gap=.0035, passes=4, cap=.02)
    BV = xb.reshape(BV.shape)
    bme = bpy.data.meshes.new('Waistband'); BF = []; bcorn = []
    arcb = np.array([np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(np.vstack([BV[j], BV[j][:1]]), axis=0), axis=1))]) for j in range(bands)])
    for j in range(bands - 1):
        for i in range(Nb):
            i2 = (i + 1) % Nb; BF.append((j * Nb + i, j * Nb + i2, (j + 1) * Nb + i2, (j + 1) * Nb + i))
            bcorn.append([(arcb[j, i], bz[j]), (arcb[j, i + 1], bz[j]), (arcb[j + 1, i + 1], bz[j + 1]), (arcb[j + 1, i], bz[j + 1])])
    bme.from_pydata([tuple(v) for v in BV.reshape(-1, 3)], [], BF); bme.update()
    bl = bme.uv_layers.new(name='UVMap'); bl.data.foreach_set('uv', np.array([c for f in bcorn for c in f], np.float32).ravel())
    for nm, v in (('face_kind', KIND['band']), ('fabric', D.get('band_fabric', 9)), ('role', D.get('band_role', 1)), ('panel', 1)):
        a = bme.attributes.new(nm, 'INT', 'FACE'); a.data.foreach_set('value', np.full(len(bme.polygons), v, np.int32))
    bz_v = BV.reshape(-1, 3)[:, 2]
    for nm, vals in (('d_top', wz1 - bz_v), ('d_hem', bz_v - wz0)):
        a = bme.attributes.new(nm, 'FLOAT', 'POINT'); a.data.foreach_set('value', np.asarray(vals, np.float32))
    band = bpy.data.objects.new('Waistband', bme); bpy.context.scene.collection.objects.link(band)
    bm = bmesh.new(); bm.from_mesh(bme); bm.faces.ensure_lookup_table()
    f0 = bm.faces[len(bm.faces) // 2]; c0 = f0.calc_center_median()
    if f0.normal.dot(c0 - Vector((BV[..., 0].mean(), BV[..., 1].mean(), c0.z))) < 0: bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(bme); bm.free(); bme.update()
    objs = [skirt, band]
    thick = {'Skirt': D['thick'], 'Waistband': D['wb_t']}
    for o in objs:
        o.data.shade_smooth()
        for nm in ('outer', 'inner', 'rim'): o.data.materials.append(bpy.data.materials.get(nm) or bpy.data.materials.new(nm))
        sm = o.modifiers.new('solid', 'SOLIDIFY'); sm.thickness = thick[o.name]; sm.offset = -1; sm.use_even_offset = True
        sm.use_quality_normals = True; sm.use_rim = True; sm.material_offset = 1; sm.material_offset_rim = 2
        apply_modifier(o, sm)
        if o.name == 'Waistband':
            bv = o.modifiers.new('bevel', 'BEVEL'); bv.width = .0007; bv.segments = 2; bv.limit_method = 'ANGLE'; bv.angle_limit = math.radians(50)
            apply_modifier(o, bv)
    if D.get('loops'):
        wtree = BVHTree.FromObject(band, bpy.context.evaluated_depsgraph_get())
        yc_ = float(np.mean([(r[2] + r[3]) / 2 for r in D['rings'][:2]]))
        zlo, zhi = wz0 - .003, wz1 + .006
        for ang in D['loops']['angles']:
            th = math.radians(ang); dv = np.array([math.sin(th), -math.cos(th), 0.0]); zmid = (zlo + zhi) / 2
            far = None; oo = np.array([0, yc_, zmid]) + dv * .02
            r_ = wtree.ray_cast(Vector(oo), Vector(dv), 1.0)
            for _ in range(5):
                if r_[0] is None: break
                far = (np.array(r_[0]), np.array(r_[1])); oo = far[0] + dv * 1e-4; r_ = wtree.ray_cast(Vector(oo), Vector(dv), 1.0)
            if far is None: continue
            pt, nn = far; nn = nn / np.linalg.norm(nn); tg = np.cross(nn, [0, 0, 1.]); tg /= np.linalg.norm(tg)
            LV = []; LUV = []; nzs, nws = 8, 4
            for iz in range(nzs):
                s_ = iz / (nzs - 1)
                for iw in range(nws):
                    u_ = (iw / (nws - 1) - .5) * .0105
                    lift = .0048 * math.sin(math.pi * s_) ** .55 + .0006
                    LV.append(pt + tg * u_ + np.array([0, 0, 1.]) * (zlo + (zhi - zlo) * s_ - pt[2]) + nn * lift); LUV.append((u_, s_ * .04))
            lme = bpy.data.meshes.new('BeltLoop'); LF = []
            for i_ in range(nzs - 1):
                for j_ in range(nws - 1): LF.append((i_ * nws + j_, i_ * nws + j_ + 1, (i_ + 1) * nws + j_ + 1, (i_ + 1) * nws + j_))
            lme.from_pydata([tuple(v) for v in LV], [], LF); lme.update()
            lay = lme.uv_layers.new(name='UVMap'); li_ = np.zeros(len(lme.loops), np.int32); lme.loops.foreach_get('vertex_index', li_)
            lay.data.foreach_set('uv', np.array(LUV, np.float32)[li_].ravel())
            for k_, v_ in (('face_kind', 5), ('fabric', D.get('band_fabric', 9)), ('role', D.get('band_role', 1)), ('panel', 8)):
                a_ = lme.attributes.new(k_, 'INT', 'FACE'); a_.data.foreach_set('value', np.full(len(lme.polygons), v_, np.int32))
            lo = bpy.data.objects.new('BeltLoop', lme); bpy.context.scene.collection.objects.link(lo)
            bmx = bmesh.new(); bmx.from_mesh(lme); bmx.faces.ensure_lookup_table()
            f0_ = bmx.faces[len(bmx.faces) // 2]; c0_ = f0_.calc_center_median()
            if f0_.normal.dot(c0_ - Vector((0, yc_, c0_.z))) < 0: bmesh.ops.reverse_faces(bmx, faces=bmx.faces)
            bmx.to_mesh(lme); bmx.free()
            lo.data.shade_smooth()
            for nm in ('outer', 'inner', 'rim'): lo.data.materials.append(bpy.data.materials.get(nm) or bpy.data.materials.new(nm))
            sml = lo.modifiers.new('solid', 'SOLIDIFY'); sml.thickness = .0016; sml.offset = -1; sml.use_even_offset = True; sml.use_quality_normals = True; sml.use_rim = True
            sml.material_offset = 1; sml.material_offset_rim = 2
            apply_modifier(lo, sml)
            objs.append(lo)
    # ---- the tight under-shorts, appended from their own build
    if under_path and under_path != '-':
        with bpy.data.libraries.load(under_path) as (src, dst): dst.objects = [n for n in src.objects if n == 'Kit_Bottom']
        for o in dst.objects:
            bpy.context.scene.collection.objects.link(o); o.name = 'Under'; objs.append(o)
            if D.get('under_role') is not None:
                ra = o.data.attributes['role']; arr = np.full(len(o.data.polygons), D['under_role'], np.int32); ra.data.foreach_set('value', arr)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = skirt
    bpy.ops.object.join()
    skirt.name = 'Kit_Bottom'; skirt['roles'] = list(D['roles'])
    bpy.ops.wm.save_as_mainfile(filepath=outp)
    print('SKIRT faces %d (waistband %d)' % (len(skirt.data.polygons), len(bme.polygons)))


if __name__ == '__main__' or 'skirt2.py' in ' '.join(sys.argv):
    a = sys.argv[sys.argv.index('--') + 1:]
    build(a[0], a[1], a[2], a[3])
