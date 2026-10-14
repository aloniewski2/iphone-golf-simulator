"""SOCK v2: a knitted sock (a tight garment: it stretches to the leg, so its tube follows the real leg and ankle, smoothed so no tendon shows) lofted as a clean
ring grid (a cross-section every few mm, radial profile smoothed), a separate rib cuff band (slightly proud and a little looser), optional pom at the heel
tab (ankle socks). The clean quad grid has an exact top edge, no decimation artefacts.
Blender -b --factory-startup --python sock2.py -- <hero> <design> <side L|R> <out.blend>"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from polo2 import apply_modifier, smoothstep, set_vattr
import shoe2

KIND = {'shell': 0, 'band': 12, 'pompom': 16, 'tab': 17}
FABRIC = dict(pique=7, rib=9)


def design(name):
    D = dict(male_crew=dict(z_lo=.062, z_top=.288, cuff=.036, gap=.0013, thick=.0018, flare=.0012, pom=False, roles=('Kit_Sock',), dz=.0075),
             female_ankle=dict(z_lo=.058, z_top=.122, cuff=.024, gap=.0013, thick=.0018, flare=.0010, pom=True, roles=('Kit_Sock',), dz=.0065))
    return D[name]


def loft_object(name, rings, uvs, kind, fabric, role, panel, extra_v=None):
    """rings: (nr, N, 3) closed in the second axis; returns object (quad grid, welded around)."""
    nr, N, _ = rings.shape
    V = rings.reshape(-1, 3)
    F = []; corners = []
    for j in range(nr - 1):
        for i in range(N):
            i2 = (i + 1) % N
            F.append((j * N + i, j * N + i2, (j + 1) * N + i2, (j + 1) * N + i))
            ua, ub = i, i + 1                                   # unwrapped column coordinates (the wrap closes at N)
            corners.append([(ua, j), (ub, j), (ub, j + 1), (ua, j + 1)])
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in V], [], F); me.update()
    layer = me.uv_layers.new(name='UVMap')
    uv = np.array([[uvs['u'][j, i] if i < N else uvs['u_wrap'][j], uvs['v'][j, 0]] for c in corners for (i, j) in c], np.float32)
    layer.data.foreach_set('uv', uv.ravel())
    for k_, v_ in (('role', role), ('face_kind', kind), ('fabric', fabric), ('panel', panel)):
        a = me.attributes.new(k_, 'INT', 'FACE'); a.data.foreach_set('value', np.full(len(me.polygons), v_, np.int32))
    for k_, v_ in (extra_v or {}).items():
        a = me.attributes.new(k_, 'FLOAT', 'POINT'); a.data.foreach_set('value', np.asarray(v_, np.float32))
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def build(hero_id, name, side, outp):
    D = design(name)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import cv2io, glib as G, fit
    hero = cv2io.Hero(hero_id); body = G.Body(hero)
    fr = shoe2.Frame(body, side)
    p = body.pos
    sel = (fr.wf + fr.wl) > .45
    t = body.tri[sel[body.tri].all(1)]
    tree = BVHTree.FromPolygons([tuple(v) for v in p], [tuple(tt) for tt in t], all_triangles=True)
    zs = np.arange(D['z_lo'], D['z_top'] + 1e-6, D['dz']); zs[-1] = D['z_top']
    N = 44; ang = np.linspace(0, 2 * math.pi, N, endpoint=False)
    R = np.zeros((len(zs), N)); C = np.zeros((len(zs), 2))
    cpts = p[sel]
    for j, z in enumerate(zs):
        m = np.abs(cpts[:, 2] - z) < .008
        c = cpts[m][:, :2].mean(0) if m.sum() else C[j - 1]
        C[j] = c
        for a_, th in enumerate(ang):
            d = np.array([math.cos(th), math.sin(th), 0.0]); o = np.array([c[0], c[1], z])
            far = 0.0; oo = o.copy()
            for _ in range(4):
                r = tree.ray_cast(Vector(oo), Vector(d), .4)
                if r[0] is None: break
                far = float(np.linalg.norm(np.array(r[0])[:2] - c)); oo = np.array(r[0]) + d * 1e-4
            R[j, a_] = far if far > 0 else np.nan
        nn = np.isnan(R[j])
        if nn.any() and (~nn).any(): R[j, nn] = np.interp(np.nonzero(nn)[0], np.nonzero(~nn)[0], R[j, ~nn], period=N)
    for _ in range(3): R = (np.roll(R, 1, 1) + 2 * R + np.roll(R, -1, 1)) / 4
    for _ in range(8): R[1:-1] = (R[:-2] + 2 * R[1:-1] + R[2:]) / 4
    for _ in range(6): C[1:-1] = (C[:-2] + 2 * C[1:-1] + C[2:]) / 4
    off = D['gap'] + D['thick']
    rings = np.zeros((len(zs), N, 3))
    for j, z in enumerate(zs):
        rr = R[j] + off
        rings[j, :, 0] = C[j, 0] + rr * np.cos(ang); rings[j, :, 1] = C[j, 1] + rr * np.sin(ang); rings[j, :, 2] = z
    # keep the tube outside the skin: push_clear on the grid triangulation
    nr = len(zs)
    tri = []
    for j in range(nr - 1):
        for i in range(N):
            i2 = (i + 1) % N; a_, b_, c_, d_ = j * N + i, j * N + i2, (j + 1) * N + i2, (j + 1) * N + i
            tri += [(a_, b_, c_), (a_, c_, d_)]
    tri = np.array(tri)
    xv, _ = fit.push_clear(body, rings.reshape(-1, 3), tri, gap=D['gap'] + D['thick'] * .8, passes=4, cap=.01)
    rings = xv.reshape(rings.shape)
    zc = D['z_top'] - D['cuff']
    jc = int(np.argmin(np.abs(zs - zc)))
    def uvs_for(rg):
        nr_ = rg.shape[0]
        circ = np.array([np.linalg.norm(np.diff(np.vstack([rg[j], rg[j][:1]]), axis=0), axis=1).sum() for j in range(nr_)])
        du = (circ / N)[:, None] * np.arange(N)[None, :]
        return dict(u=du, u_wrap=circ, v=rg[:, :, 2].mean(1)[:, None])
    body_rings = rings[:jc + 1]; cuff_rings = rings[jc:].copy()
    # the cuff is a little looser and proud: grow radially a hair, flaring toward the top
    cc = cuff_rings.mean(1, keepdims=True)
    k = D['flare'] * (.5 + .5 * np.linspace(0, 1, len(cuff_rings))[:, None, None])
    cd = cuff_rings - cc; cd[..., 2] = 0
    cuff_rings = cuff_rings + cd / np.maximum(np.linalg.norm(cd, axis=2, keepdims=True), 1e-6) * k
    sock = loft_object('Sock', body_rings, uvs_for(body_rings), KIND['shell'], FABRIC['pique'], 0, 0,
                       dict(f_top=(D['z_top'] - body_rings[:, :, 2]).ravel(), f_cuff=(zc - body_rings[:, :, 2]).ravel(), q_z=body_rings[:, :, 2].ravel()))
    cuff = loft_object('Cuff', cuff_rings, uvs_for(cuff_rings), KIND['band'], FABRIC['rib'], 0, 1,
                       dict(f_top=(D['z_top'] - cuff_rings[:, :, 2]).ravel(), f_cuff=(zc - cuff_rings[:, :, 2]).ravel(), q_z=cuff_rings[:, :, 2].ravel()))
    objs = [sock, cuff]
    for o in objs:                                                # flip a loft whose normals point toward the leg axis
        me = o.data
        bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
        f0 = bm.faces[len(bm.faces) // 2]; c0 = f0.calc_center_median(); ax = Vector((C[:, 0].mean(), C[:, 1].mean(), c0.z))
        if f0.normal.dot(c0 - ax) < 0: bmesh.ops.reverse_faces(bm, faces=bm.faces)
        bm.to_mesh(me); bm.free(); me.update()
    if D['pom']:
        # pom at the back of the cuff: ray from behind at the cuff height
        back = -fr.fwd
        zt = D['z_top'] - .008
        o_start = np.array([C[-1, 0], C[-1, 1], zt]) + back * .2
        tree2 = BVHTree.FromPolygons([tuple(v) for v in rings.reshape(-1, 3)], [tuple(tt) for tt in tri], all_triangles=True)
        rr = tree2.ray_cast(Vector(o_start), Vector(fr.fwd), .5)
        if rr[0] is not None:
            hp = np.array(rr[0]); nn = np.array(rr[1]); nn = nn if nn @ back > 0 else -nn
            bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=10, radius=.0105)
            for v in bm.verts: v.co = Vector(np.array(v.co) + hp + nn * .010 + np.array([0, 0, -.002]))
            me = bpy.data.meshes.new('Pom'); bm.to_mesh(me); bm.free()
            pom = bpy.data.objects.new('Pom', me); bpy.context.scene.collection.objects.link(pom)
            uvl = me.uv_layers.new(name='UVMap')
            li = np.zeros(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', li)
            pv = np.zeros(len(me.vertices) * 3); me.vertices.foreach_get('co', pv); pv = pv.reshape(-1, 3)
            uvl.data.foreach_set('uv', (pv[li][:, :2] * 8 + 3.0).astype(np.float32).ravel())
            for k_, v_ in (('role', 0), ('face_kind', KIND['pompom']), ('fabric', 7), ('panel', 2)):
                a = me.attributes.new(k_, 'INT', 'FACE'); a.data.foreach_set('value', np.full(len(me.polygons), v_, np.int32))
            objs.append(pom)
    # the shoe's front half is a designed last LOWER than the foot (the foot skin under it is hidden): the sock must not wrap the instep / forefoot, it ends in the collar
    bmc = bmesh.new(); bmc.from_mesh(sock.data); bmc.faces.ensure_lookup_table()
    cut_y = D.get('front_cut', .47) * fr.L
    kill = []
    for f_ in bmc.faces:
        c_ = f_.calc_center_median(); q_ = fr.to(np.array([[c_.x, c_.y, c_.z]]))[0]
        if q_[1] > cut_y and q_[2] < .135: kill.append(f_)
    bmesh.ops.delete(bmc, geom=kill, context='FACES'); bmesh.ops.delete(bmc, geom=[v_ for v_ in bmc.verts if not v_.link_faces], context='VERTS')
    bmc.to_mesh(sock.data); bmc.free(); sock.data.update()
    for o in objs:
        o.data.shade_smooth()
        for nm in ('outer', 'inner', 'rim'): o.data.materials.append(bpy.data.materials.get(nm) or bpy.data.materials.new(nm))
        if o.name in ('Sock', 'Cuff'):
            sm = o.modifiers.new('solid', 'SOLIDIFY'); sm.thickness = D['thick'] if o.name == 'Sock' else D['thick'] * 1.5; sm.offset = -1; sm.use_even_offset = True
            sm.use_quality_normals = True; sm.use_rim = True; sm.material_offset = 1; sm.material_offset_rim = 2
            apply_modifier(o, sm)
            bv = o.modifiers.new('bevel', 'BEVEL'); bv.width = .0006; bv.segments = 2; bv.limit_method = 'ANGLE'; bv.angle_limit = math.radians(55)
            apply_modifier(o, bv)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = sock
    bpy.ops.object.join()
    sock.name = 'Kit_Sock_' + side; sock['roles'] = list(D['roles'])
    bpy.ops.wm.save_as_mainfile(filepath=outp)
    print('SOCK2 %s faces %d (cuff rings %d)' % (side, len(sock.data.polygons), len(cuff_rings)))


if __name__ == '__main__' or 'sock2.py' in ' '.join(sys.argv):
    a = sys.argv[sys.argv.index('--') + 1:]
    build(a[0], a[1], a[2], a[3])
