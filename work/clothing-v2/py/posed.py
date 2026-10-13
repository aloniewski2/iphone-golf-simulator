"""Render a hero posed at one clip frame (dumped body + LBS garments).  Blender ... -- <hero> <clip> <frame> <out.png> [az] [el] [tx,ty,tz] [dist]"""
import sys, os, glob, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import cv2io, bl
a = sys.argv[sys.argv.index('--') + 1:]
hero_id, clip, frame, outp = a[0], a[1], int(a[2]), a[3]
az = float(a[4]) if len(a) > 4 else 0; el = float(a[5]) if len(a) > 5 else 5
tgt = [float(v) for v in a[6].split(',')] if len(a) > 6 else None; dist = float(a[7]) if len(a) > 7 else 4.6
hero = cv2io.Hero(hero_id)
bodyf, bones = hero.clip(clip)
skin = hero.skin_matrices(bones[frame].astype(np.float64))
inv = np.linalg.inv(hero.body.r2w)
toMesh = lambda w: (np.concatenate([w, np.ones((len(w), 1))], 1) @ inv.T)[:, :3]
bl.reset()
root = bpy.data.objects.new('Mirror', None); bpy.context.scene.collection.objects.link(root); root.scale = (-1, 1, 1)
udir = os.path.join(cv2io.ROOT, 'out', hero_id, 'unity')
cv = np.load(os.path.join(udir, 'cover.npz')) if os.path.exists(os.path.join(udir, 'cover.npz')) else None
bt = hero.body.tri
if cv is not None and os.environ.get('CV2_HIDE', '1') == '1' and os.environ.get('CV2_OLD') != '1':
    hid = np.zeros(len(hero.body.pos), bool)
    for k in cv.files:
        if k != 'Kit_Glove_R': hid |= cv[k]
    bt = bt[~hid[bt].all(1)]
b = bl.mesh_object('Body', toMesh(bodyf[frame].astype(np.float64)), bt); b.parent = root; b.data.materials.append(bl.material('Skin', bl.srgb('B7744A'), .55))
if os.environ.get('CV2_MARK'):
    # poke-through skin of this frame in red (all pieces), computed like poke.py
    import glib as G
    from mathutils import Vector as V
    from mathutils.bvhtree import BVHTree
    import correctives_rt as RT
    bp = bodyf[frame].astype(np.float64); bn = G.vertex_normals(bp, hero.body.tri)
    cfz = os.path.join(udir, 'correctives.npz'); CORR = np.load(cfz) if os.path.exists(cfz) else None
    red = np.zeros(len(bp), bool)
    for f in sorted(glob.glob(os.path.join(udir, 'Kit_*_final.npz'))):
        if f.endswith('Kit_Glove_R_final.npz'): continue
        gg = np.load(f); outer = gg['tri'][gg['part'] == 0]
        base = gg['pos'] + (RT.blend(hero, bones[frame].astype(np.float64), CORR) if (CORR is not None and 'Kit_Top' in f and len(CORR['shapes'])) else 0)
        gp = cv2io.lbs(base, gg['bi'], gg['bw'], skin)
        tr = BVHTree.FromPolygons([tuple(p) for p in gp], [tuple(t) for t in outer], all_triangles=True)
        vis = np.unique(bt)
        zc_ = np.percentile(gg['pos'][:, 2], 1) + .03
        vis = vis[hero.body.pos[vis, 2] > zc_]          # skin below the shirt hem is the shorts' business
        for v in vis:
            if tr.ray_cast(V(bp[v] + bn[v] * 1e-4), V(bn[v]), .08)[0] is not None: continue
            if tr.ray_cast(V(bp[v] - bn[v] * 1e-4), V(-bn[v]), .03)[0] is not None: red[v] = True
    col = np.ones((len(bp), 4)); col[red] = (1, 0, 0, 1)
    a = b.data.color_attributes.new('C', 'FLOAT_COLOR', 'POINT'); a.data.foreach_set('color', col.astype(np.float32).ravel())
    m = b.data.materials[0]; nt = m.node_tree; at = nt.nodes.new('ShaderNodeAttribute'); at.attribute_name = 'C'
    nt.links.new(at.outputs['Color'], nt.nodes['Principled BSDF'].inputs['Base Color'])
    print('POKED', int(red.sum()))
cols = {'Kit_Top': 'F4F0E6', 'Kit_Bottom': '22315A', 'Kit_Belt': '6B4429'}
if os.environ.get('CV2_OLD') == '1':
    for name, p in hero.kit.items():
        if name == 'Kit_Glove_R': continue
        idx = np.array([hero.body.bones.index(n) for n in p.bones])
        gp = toMesh(cv2io.lbs(p.pos, idx[p.bi], p.bw, skin))
        o = bl.mesh_object(name, gp, p.tri); o.parent = root
        o.data.materials.append(bl.material(name, bl.srgb(cols.get(name, 'F6F6F4')), .75))
import correctives_rt as _RT
_cf = os.path.join(udir, 'correctives.npz'); _CORR = np.load(_cf) if os.path.exists(_cf) else None
for f in ([] if os.environ.get('CV2_OLD') == '1' else sorted(glob.glob(os.path.join(udir, 'Kit_*_final.npz')))):
    name = os.path.basename(f)[:-10]
    if name == 'Kit_Glove_R': continue
    g = np.load(f)
    _base = g['pos'] + (_RT.blend(hero, bones[frame].astype(np.float64), _CORR) if (_CORR is not None and name == 'Kit_Top' and len(_CORR['shapes'])) else 0)
    gp = toMesh(cv2io.lbs(_base, g['bi'], g['bw'], skin))
    o = bl.mesh_object(name, gp, g['tri']); o.parent = root
    o.data.materials.append(bl.material(name, bl.srgb(cols.get(name, 'F6F6F4')), .75))
bl.studio()
c = tgt if tgt else (0, -.02, .86)
bl.camera(c, az, el, dist, 70)
bl.render(outp)
