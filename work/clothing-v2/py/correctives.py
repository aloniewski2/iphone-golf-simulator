"""Pose correctives for the top (cloth yields where the forearm / hand presses into it): anchor poses are chosen from every clip, at each one the
shirt is pushed in behind the arm (never into the torso), smoothed, turned into a bind-space blend shape. At runtime KitV2PoseCorrectives blends
them by the same 28 segment-direction features GolfGarmentCorrectives uses.  Blender -b --factory-startup --python correctives.py -- <hero> [K]"""
import sys, os, json, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, cv2io, glib as G
from mathutils import Vector
from mathutils.bvhtree import BVHTree

SEG = [('Spine', 'Chest'), ('Chest', 'Neck'), ('LeftShoulder', 'LeftUpperArm'), ('LeftUpperArm', 'LeftLowerArm'), ('LeftLowerArm', 'LeftHand'),
       ('RightShoulder', 'RightUpperArm'), ('RightUpperArm', 'RightLowerArm'), ('RightLowerArm', 'RightHand')]


def features(hero, bones_world):
    d = []
    for a, b in SEG:
        pa = bones_world[hero.body.bones.index(a)][:3, 3]; pb = bones_world[hero.body.bones.index(b)][:3, 3]
        v = pb - pa; d.append(v / (np.linalg.norm(v) + 1e-12))
    f = [float(d[i] @ d[j]) for i in range(8) for j in range(i + 1, 8)]
    return np.array(f)


def main():
    a = sys.argv[sys.argv.index('--') + 1:]
    hero_id = a[0]; K = int(a[1]) if len(a) > 1 else 14
    hero = cv2io.Hero(hero_id); body = hero.body
    udir = os.path.join(cv2io.ROOT, 'out', hero_id, 'unity')
    g = np.load(os.path.join(udir, 'Kit_Top_final.npz'))
    gpos, gtri, gbi, gbw = g['pos'], g['tri'], g['bi'], g['bw']
    W = np.zeros((len(body.pos), len(body.bones)))
    for k in range(4): np.add.at(W, (np.arange(len(body.pos)), body.bi[:, k]), body.bw[:, k])
    def grp(pred): return W[:, [i for i, n in enumerate(body.bones) if pred(n)]].sum(1)
    fore = grp(lambda n: any(k in n for k in ['LowerArm', 'Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little']) or any(n.startswith('ArmBlend_' + x) for x in ['8_9', '27_28', '9_10', '28_29'])) > .5
    torso = grp(lambda n: n in ('Hips', 'Spine', 'Chest', 'UpperChest')) > .6
    arm_idx = np.nonzero(fore)[0][::2]
    ttri = body.tri[torso[body.tri].all(1)]
    # garment vertices that belong to the torso part of the shirt (the sleeves follow the arm already)
    gW = np.zeros((len(gpos), len(body.bones)))
    for k in range(4): np.add.at(gW, (np.arange(len(gpos)), gbi[:, k]), gbw[:, k])
    gtorso = gW[:, [i for i, n in enumerate(body.bones) if n in ('Hips', 'Spine', 'Chest', 'UpperChest', 'Neck')]].sum(1) > .5
    nb = G.neighbours(len(gpos), gtri); E = G.unique_edges(gtri)
    cands = []
    for c in hero.clips:
        bf, bones = hero.clip(c['name'])
        for fi in range(len(bf)):
            cands.append((c['name'], fi))
    print('candidate frames', len(cands))

    # the garment's outer wall drives everything; inner wall / rim vertices copy the displacement of the outer surface nearest to them
    outer_tri = gtri[g['part'] == 0] if 'part' in g.files else gtri
    outer_v = np.zeros(len(gpos), bool); outer_v[outer_tri.ravel()] = True
    otree0 = BVHTree.FromPolygons([tuple(x) for x in gpos], [tuple(t) for t in outer_tri], all_triangles=True)
    map_tri = np.zeros((len(gpos), 3), np.int64); map_bc = np.zeros((len(gpos), 3))
    for i in range(len(gpos)):
        if outer_v[i]: map_tri[i] = (i, i, i); map_bc[i] = (1, 0, 0); continue
        r = otree0.find_nearest(Vector(gpos[i]))
        t = outer_tri[r[2]]; a, b, c = gpos[t]; q = np.array(r[0])
        v0, v1, v2 = b - a, c - a, q - a
        d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
        den = d00 * d11 - d01 * d01 or 1e-20
        vv = (d11 * d20 - d01 * d21) / den; ww = (d00 * d21 - d01 * d20) / den
        bc = np.clip([1 - vv - ww, vv, ww], 0, 1); map_tri[i] = t; map_bc[i] = bc / bc.sum()
    # skin that may poke out: visible skin of the torso / shoulders / upper arms near the shirt
    cvf = os.path.join(udir, 'cover.npz')
    hidden = np.zeros(len(body.pos), bool)
    if os.path.exists(cvf):
        for k, v in np.load(cvf).items():
            if k != 'Kit_Glove_R': hidden |= v
    upper = grp(lambda n: n in ('Spine', 'Chest', 'UpperChest', 'Neck') or 'Shoulder' in n or 'UpperArm' in n or n.startswith('ArmBlend_3_') or n.startswith('ArmBlend_4_')
                or n.startswith('ArmBlend_7_') or n.startswith('ArmBlend_26_')) > .5
    lo, hi = gpos.min(0) - .03, gpos.max(0) + .03
    poke_idx = np.nonzero(~hidden & ~fore & ((body.pos > lo) & (body.pos < hi)).all(1))[0]

    def solve(cname, fi):
        bf, bones = hero.clip(cname)
        bw = bones[fi].astype(np.float64)
        skin = hero.skin_matrices(bw)
        gp = cv2io.lbs(gpos, gbi, gbw, skin)
        gn = G.vertex_normals(gp, outer_tri)
        bp = bf[fi].astype(np.float64)
        tt = BVHTree.FromPolygons([tuple(x) for x in bp], [tuple(t) for t in ttri], all_triangles=True)
        fn = G.face_normals(bp, ttri); fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12
        # arm points outside the torso skin and within 7 cm of it
        Q = []
        for v in arm_idx:
            q = bp[v]; r = tt.find_nearest(Vector(q), .07)
            if r[0] is None: continue
            if np.dot(q - np.array(r[0]), fn[r[2]]) > 0: Q.append(q)
        Q = np.array(Q).reshape(-1, 3)
        S = bp[poke_idx]
        depth = np.zeros(len(gp)); lift = np.zeros(len(gp))
        cand = np.nonzero(outer_v & gtorso)[0]
        cand_all = np.nonzero(outer_v)[0]
        c_ = .004
        for i0 in range(0, len(cand), 3000):
            ids = cand[i0:i0 + 3000]
            v = gp[ids]; n = gn[ids]
            if len(Q):
                d = Q[None, :, :] - v[:, None, :]
                along = np.einsum('mqk,mk->mq', d, n)                   # + = arm point in front of the garment
                lat = np.linalg.norm(d - along[:, :, None] * n[:, None, :], axis=2)
                w = np.clip((.05 - lat) / .03, 0, 1); w = w * w * (3 - 2 * w)
                ok = (along < c_) & (along > -.06)
                depth[ids] = np.where(ok, (-along + c_) * w, 0).max(1)
        # skin poking through: no cloth in front of it (along its normal) but cloth right behind it -> lift that cloth past the skin
        otree = BVHTree.FromPolygons([tuple(x) for x in gp], [tuple(t) for t in outer_tri], all_triangles=True)
        bn = G.vertex_normals(bp, body.tri)
        for v in poke_idx:
            q = bp[v]; n = bn[v]
            if otree.ray_cast(Vector(q + n * 1e-4), Vector(n), .08)[0] is not None: continue
            r = otree.ray_cast(Vector(q - n * 1e-4), Vector(-n), .03)
            if r[0] is None: continue
            t = outer_tri[r[2]]
            lift[t] = np.maximum(lift[t], r[3] + c_)
        if depth.max() <= 1e-4 and lift.max() <= 1e-4: return None, 0
        # cannot go into the torso: available room along -n
        room = np.full(len(gp), 0.0)
        for i in np.nonzero(depth > 0)[0]:
            r = tt.ray_cast(Vector(gp[i] - gn[i] * 1e-4), Vector(-gn[i]), .1)
            room[i] = max(0.0, (r[3] - .0035) if r[0] is not None else .03)
        depth = np.minimum(depth, room)
        moved = int((depth > .002).sum() * (os.environ.get('CV2_CORR_MODE') != 'lift') + 3 * (lift > .002).sum())     # skin poking out counts most
        # soft falloff over the cloth (outer wall): grow and smooth both fields
        for fld in (depth, lift):
            for _ in range(6):
                m = fld.copy()
                np.maximum.at(m, E[:, 0], .75 * fld[E[:, 1]]); np.maximum.at(m, E[:, 1], .75 * fld[E[:, 0]])
                fld[:] = m * outer_v
            for _ in range(8):
                fld[:] = np.maximum(0, fld + .5 * G.umbrella(fld[:, None], nb)[:, 0]) * outer_v
        depth = np.minimum(depth, room + .0005)
        if os.environ.get('CV2_CORR_MODE') == 'lift': depth[:] = 0
        dpos_o = gn * (lift - depth)[:, None]
        dpos = (dpos_o[map_tri] * map_bc[:, :, None]).sum(1)
        # to bind space: inverse of the weighted linear part of the skinning matrices
        M = np.zeros((len(gp), 3, 3))
        for k in range(4): M += gbw[:, k, None, None] * skin[gbi[:, k]][:, :3, :3]
        dbind = np.linalg.solve(M, dpos[:, :, None])[:, :, 0]
        return dbind, moved

    # greedy anchor selection: frames with the most cloth to move first, skipping poses close to an anchor already taken
    feats = {}
    scored = []
    for (cname, fi) in [c for i_, c in enumerate(cands) if i_ % 3 == 0 or 'ReadyIdle' in c[0]]:     # every ReadyIdle frame (the stance seen most)
        bf, bones = hero.clip(cname)
        f = features(hero, bones[fi].astype(np.float64)); feats[(cname, fi)] = f
        db, count = solve(cname, fi)
        scored.append((count, cname, fi, db))
    scored.sort(key=lambda s: -s[0])
    anchors = []
    for count, cname, fi, db in scored:
        if count < 25 or len(anchors) >= K: break
        f = feats[(cname, fi)]
        if any(np.mean((f - a['feature']) ** 2) < .002 for a in anchors): continue
        anchors.append(dict(clip=cname, frame=fi, feature=f, delta=db, count=count))
    # neutral anchors: frames with nothing to fix, spread over the clips (they pull the blend to zero in ordinary poses)
    neutral = [s for s in scored if s[0] == 0]
    take = []
    for count, cname, fi, db in neutral:
        f = feats[(cname, fi)]
        if any(np.mean((f - t['feature']) ** 2) < .01 for t in take + anchors): continue
        take.append(dict(clip=cname, frame=fi, feature=f, delta=None, count=0))
        if len(take) >= 2 * K: break
    print('anchors', [(x['clip'], x['frame'], x['count']) for x in anchors], 'neutral', len(take))
    # normals of each shape: from the displaced bind mesh
    base_n = G.vertex_normals(gpos, gtri)
    shapes = []
    for k, x in enumerate(anchors):
        dn = np.zeros_like(gpos)          # small cloth offsets: keep the base shading normals (no faceted normal deltas)
        shapes.append(('CV2_Pose%02d' % k, x['delta'], dn))
    # file: int K, then per shape: name[16], pos deltas (n*3 f32), normal deltas (n*3 f32); profile json with features
    with open(os.path.join(udir, 'Kit_Top_shapes.bin'), 'wb') as fh:
        fh.write(np.int32(len(shapes)).tobytes()); fh.write(np.int32(len(gpos)).tobytes())
        for name, dp, dn in shapes:
            fh.write(name.encode().ljust(16, b'\0')); fh.write(dp.astype(np.float32).tobytes()); fh.write(dn.astype(np.float32).tobytes())
    prof = {'anchors': [{'shape': '', 'feature': [float(v) for v in features(hero, hero.rest_bones)]}] +
                       [{'shape': 'CV2_Pose%02d' % k, 'feature': [float(v) for v in x['feature']]} for k, x in enumerate(anchors)] +
                       [{'shape': '', 'feature': [float(v) for v in x['feature']]} for x in take]}
    json.dump(prof, open(os.path.join(udir, 'Kit_Top_correctives.json'), 'w'))
    np.savez(os.path.join(udir, 'correctives.npz'), feats=np.array([a['feature'] for a in prof['anchors']]),
             shapes=np.array([s[1] for s in shapes]) if shapes else np.zeros((0, len(gpos), 3)))
    print('shapes', len(shapes))


if __name__ == '__main__' or 'correctives.py' in ' '.join(sys.argv): main()
