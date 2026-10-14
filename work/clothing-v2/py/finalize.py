"""Finish built garments for Unity: grain-aligned UVs (split at seams, packed per piece), skin weights from the real body (restricted to
the bones that garment may follow, smoothed for loose cloth), and the binary export the Unity installer reads.
Blender -b --factory-startup --python finalize.py -- <hero>"""
import sys, os, json, math, glob
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import cv2io, glib as G
from mathutils import Vector
from mathutils.bvhtree import BVHTree

TOP_BONES = ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck', 'LeftShoulder', 'RightShoulder', 'LeftUpperArm', 'RightUpperArm']
TOP_BLENDS = ['ArmBlend_3_7_', 'ArmBlend_3_8_', 'ArmBlend_4_7_', 'ArmBlend_4_8_', 'ArmBlend_7_8_', 'ArmBlend_3_26_', 'ArmBlend_3_27_', 'ArmBlend_4_26_',
              'ArmBlend_4_27_', 'ArmBlend_26_27_', 'ArmBlend_7_26_']
LEG_BONES = ['Hips', 'Spine', 'LeftUpperLeg', 'RightUpperLeg', 'LeftLowerLeg', 'RightLowerLeg']
FOOT_BONES = ['LeftLowerLeg', 'RightLowerLeg', 'LeftFoot', 'RightFoot', 'LeftToes', 'RightToes']
WAIST_BONES = ['Hips', 'Spine']


def allowed_for(piece, bones):
    if piece == 'Kit_Top': names = lambda n: n in TOP_BONES or any(n.startswith(b) for b in TOP_BLENDS)
    elif piece == 'Kit_Bottom': names = lambda n: n in LEG_BONES
    elif piece.startswith('Kit_Shoe') or piece.startswith('Kit_Sock'):
        side = 'Left' if piece.endswith('_L') else 'Right'
        names = lambda n: n in FOOT_BONES and n.startswith(side)
    elif piece == 'Kit_Belt': names = lambda n: n in WAIST_BONES
    elif piece.startswith('Kit_Glove'):
        side = 'Left' if piece.endswith('_L') else 'Right'
        a, b, c = ('8_9', '9_10', '') if side == 'Left' else ('27_28', '28_29', '')
        names = lambda n: (n.startswith(side) and any(k in n for k in ['LowerArm', 'Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little'])) or n.startswith('ArmBlend_' + b + '_')
    else: names = lambda n: True
    return np.array([names(n) for n in bones])


# ------------------------------------------------------------------ UVs

def cyl_uv(x, origin, axis, ref, R):
    axis = axis / np.linalg.norm(axis); ref = ref - axis * (ref @ axis); ref /= np.linalg.norm(ref); side = np.cross(axis, ref)
    d = x - origin
    s = d @ axis; th = np.arctan2(d @ side, d @ ref)
    return th * R, s


def corner_uvs(pos, tri, proj, A, frames):
    """per-corner UVs (m) by projection id; cylinders unwrap per face so a face never straddles the seam."""
    nf = len(tri); uv = np.zeros((nf, 3, 2))
    for pid in np.unique(proj):
        fm = proj == pid
        t = tri[fm]; x = pos[t.reshape(-1)]
        if pid in frames:
            o, ax, ref, R, flipv = frames[pid]
            if callable(o): o = o(x)
            u, v = cyl_uv(x, o, ax, ref, R)
            u = u.reshape(-1, 3); v = v.reshape(-1, 3) * (-1 if flipv else 1)
            # unwrap: if a face spans the seam, move its negative corners by one turn
            span = u.max(1) - u.min(1)
            bad = span > math.pi * R
            u[bad] = np.where(u[bad] < 0, u[bad] + 2 * math.pi * R, u[bad])
            uv[fm, :, 0] = u; uv[fm, :, 1] = v
        elif pid == 6 and 'pu' in A:
            uv[fm, :, 0] = A['pu'][t]; uv[fm, :, 1] = A['pv'][t]
        else:   # box projection by the face normal
            fn = G.face_normals(pos, t); k = np.argmax(np.abs(fn), 1)
            for c in range(3):
                pc = pos[t[:, c]]
                a = np.where(k == 0, pc[:, 1], pc[:, 0]); b = np.where(k == 2, pc[:, 1], pc[:, 2])
                uv[fm, c, 0] = a + k * 10.0; uv[fm, c, 1] = b          # separate the three axis groups
    return uv


def split_by_uv(pos, tri, uv, q=1e-5):
    """vertices duplicated where corner UVs differ (seams). Returns new tri, per-new-vertex source index, uv."""
    key = {}
    src = []; uvs = []; nt = np.zeros_like(tri)
    for f in range(len(tri)):
        for c in range(3):
            v = int(tri[f, c]); k = (v, int(round(uv[f, c, 0] / q)), int(round(uv[f, c, 1] / q)))
            i = key.get(k)
            if i is None:
                i = len(src); key[k] = i; src.append(v); uvs.append(uv[f, c])
            nt[f, c] = i
    return nt, np.array(src), np.array(uvs)


def islands(n, tri):
    parent = np.arange(n)
    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]; a = parent[a]
        return a
    for a, b, c in tri:
        for x, y in ((a, b), (b, c)):
            rx, ry = find(x), find(y)
            if rx != ry: parent[rx] = ry
    return np.array([find(i) for i in range(n)])


def pack(uv, tri, margin):
    """shelf-pack the islands (uv in metres) into the unit square, one uniform scale. Returns uv in [0,1] and metres per uv unit."""
    lab = islands(len(uv), tri)
    ids = np.unique(lab)
    boxes = []
    for i in ids:
        m = lab == i
        lo = uv[m].min(0); hi = uv[m].max(0)
        boxes.append((i, lo, hi - lo))
    boxes.sort(key=lambda b: -b[2][1])
    total = sum((b[2][0] + margin) * (b[2][1] + margin) for b in boxes)
    S = math.sqrt(total) * 1.05
    for _ in range(60):
        x = y = 0.0; row_h = 0.0; ok = True; place = {}
        for (i, lo, sz) in boxes:
            if x + sz[0] + margin > S:
                x = 0.0; y += row_h; row_h = 0.0
            place[i] = (x + margin * .5 - lo[0], y + margin * .5 - lo[1])
            x += sz[0] + margin; row_h = max(row_h, sz[1] + margin)
            if y + row_h > S or x > S: ok = False
        if ok: break
        S *= 1.04
    out = uv.copy()
    for i in ids:
        m = lab == i; ox, oy = place[i]
        out[m, 0] += ox; out[m, 1] += oy
    return out / S, S


# ------------------------------------------------------------------ weights

def restricted_weights(body, x, piece, nrm=None):
    allowed = allowed_for(piece, body.bones)
    dom_ok = (body.W[:, allowed].sum(1) > .5)
    t = body.tri[dom_ok[body.tri].all(1)]
    tree = BVHTree.FromPolygons([tuple(p) for p in body.pos], [tuple(a) for a in t], all_triangles=True)
    W = np.zeros((len(x), len(body.bones)))
    for i, p in enumerate(x):
        r = tree.find_nearest(Vector(p))
        f = t[r[2]]
        a, b, c = body.pos[f[0]], body.pos[f[1]], body.pos[f[2]]
        q = np.array(r[0])
        v0, v1, v2 = b - a, c - a, q - a
        d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
        den = d00 * d11 - d01 * d01 or 1e-20
        vv = (d11 * d20 - d01 * d21) / den; ww = (d00 * d21 - d01 * d20) / den; uu = 1 - vv - ww
        bc = np.clip([uu, vv, ww], 0, 1); bc /= bc.sum()
        W[i] = bc[0] * body.W[f[0]] + bc[1] * body.W[f[1]] + bc[2] * body.W[f[2]]
    W[:, ~allowed] = 0
    s = W.sum(1, keepdims=True); s[s == 0] = 1
    return W / s


def smooth_weights(W, tri, iters, mask=None):
    if iters <= 0: return W
    nb = G.neighbours(len(W), tri)
    for _ in range(iters):
        u = G.umbrella(W, nb)
        W = W + .5 * (u if mask is None else u * mask[:, None])
    return W


def main():
    argv = sys.argv[sys.argv.index('--') + 1:]
    hero_id = argv[0]
    hero = cv2io.Hero(hero_id); body = G.Body(hero)
    out = os.path.join(cv2io.ROOT, 'out', hero_id); udir = os.path.join(out, 'unity'); os.makedirs(udir, exist_ok=True)
    zs = np.array([body.head(b)[2] for b in ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck']])
    ys = np.array([body.head(b)[1] for b in ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck']])
    torso_o = lambda x: np.stack([np.zeros(len(x)), np.interp(x[:, 2], zs, ys), np.zeros(len(x))], 1)
    frames = {0: (torso_o, np.array([0, 0, 1.]), np.array([0, -1., 0]), .16, False)}
    for pid, sd in ((1, 'Left'), (2, 'Right')):
        sh = body.head(sd + 'UpperArm'); el = body.head(sd + 'LowerArm')
        frames[pid] = (sh, el - sh, np.array([0, 0, 1.]), .06, True)
    for pid, sd in ((3, 'Left'), (4, 'Right')):
        hip = body.head(sd + 'UpperLeg'); an = body.head(sd + 'Foot')
        frames[pid] = (hip, an - hip, np.array([1. if sd == 'Left' else -1., 0, 0]), .08, True)
    manifest = {'hero': hero_id, 'pieces': []}
    for f in sorted(glob.glob(os.path.join(out, 'Kit_*.npz'))):
        piece = os.path.basename(f)[:-4]
        g = dict(np.load(f, allow_pickle=True))
        pos, tri = g['pos'], g['tri']
        A = {k[2:]: v for k, v in g.items() if k.startswith('a_') and v.ndim == 1}
        A2 = {k[2:]: v for k, v in g.items() if k.startswith('a_') and v.ndim == 2}
        proj = g.get('proj', np.full(len(tri), 7))
        fr = dict(frames)
        if piece.startswith('Kit_Shoe'):
            import shoes
            sd = -1 if piece.endswith('_L') else 1
            heel, fwd, lat, L = shoes.foot_frame(body, sd)
            fr[5] = (heel + np.array([0, 0, .035]), fwd, np.array([0, 0, 1.]), .05, False)
        cuv = corner_uvs(pos, tri, proj, A, fr)
        nt, src, uvm = split_by_uv(pos, tri, cuv)
        res = {'Kit_Top': 2048, 'Kit_Bottom': 2048, 'Kit_Belt': 1024}.get(piece, 1024 if 'Shoe' in piece else 512)
        uv01, S = pack(uvm, nt, margin=12.0 / res * 1.0 * max(1.0, 1.0))
        # margin is in metres here: 12 texels of the final map (S unknown before packing): iterate once
        uv01, S = pack(uvm, nt, margin=12.0 / res * S)
        # weights on the unsplit mesh, smoothed (loose cloth more), then copied to the split vertices
        W = restricted_weights(body, pos, piece)
        if 'W' in A2 and A2['W'].shape == W.shape:
            Wc = A2['W'].copy()
            allowed = allowed_for(piece, body.bones)
            Wc[:, ~allowed] = 0
            ok = Wc.sum(1) > .5
            Wc[ok] /= Wc[ok].sum(1, keepdims=True)
            W[ok] = Wc[ok]
            # parts without an origin on the skin (collar, placket, buttons, cords...) get smoothed nearest weights; the shell keeps its own
            W = smooth_weights(W, tri, 2, mask=(~ok).astype(float))
        else:
            W = smooth_weights(W, tri, 2)
        if piece == 'Kit_Bottom' and 'pleat_fold' in A:
            sk = np.zeros(len(pos), bool)
            fm = (g['fabric'] == 6) & (proj == 6)
            sk[tri[fm].ravel()] = True
            W = smooth_weights(W, tri, 40, mask=sk.astype(float))
        if piece == 'Kit_Bottom' and 'kind' in g and (g['kind'] == 11).any():
            # a pleated skirt hangs from the hips: the hip bone carries it, the thighs only swing the hem a little (no umbrella between the legs)
            sv = np.zeros(len(pos), bool); sv[tri[g['kind'] == 11].ravel()] = True
            zt, zb = pos[sv, 2].max(), pos[sv, 2].min()
            t = np.clip((zt - pos[:, 2]) / max(1e-6, zt - zb), 0, 1)
            sL = np.clip(.5 - pos[:, 0] / .07, 0, 1)
            hi, li, ri = body.bones.index('Hips'), body.bones.index('LeftUpperLeg'), body.bones.index('RightUpperLeg')
            Ws = np.zeros_like(W); leg = .38 * t ** 1.5
            Ws[:, hi] = 1 - leg; Ws[:, li] = leg * sL; Ws[:, ri] = leg * (1 - sL)
            W[sv] = Ws[sv]
            W = smooth_weights(W, tri, 6, mask=sv.astype(float))
        if piece.startswith('Kit_Shoe'):
            # a shoe is stiff: the Foot bone carries it, the forefoot flexes with the toes from the ball forward, the shin only touches the collar
            import shoes
            sd = -1 if piece.endswith('_L') else 1
            heel, fwd, lat, L = shoes.foot_frame(body, sd)
            q = shoes.to_frame(pos, heel, fwd, lat)
            side = 'Left' if sd < 0 else 'Right'
            fi, ti, li = body.bones.index(side + 'Foot'), body.bones.index(side + 'Toes'), body.bones.index(side + 'LowerLeg')
            wt = np.clip((q[:, 1] / L - .66) / .22, 0, 1) * .55
            wl = np.clip((q[:, 2] - .05) / .04, 0, 1) * np.clip((.4 * L - q[:, 1]) / (.15 * L), 0, 1) * .3
            W = np.zeros_like(W); W[:, ti] = wt; W[:, li] = wl; W[:, fi] = 1 - wt - wl
        bi, bw = G.prune(W, 4)
        used = np.unique(bi[bw > 0])
        remap = -np.ones(len(body.bones), np.int64); remap[used] = np.arange(len(used))
        bi2 = np.where(bw > 0, remap[bi], 0)
        nrm = G.vertex_normals(pos, tri)
        V = len(src)
        roles = [str(r) for r in g['roles']]
        mat = g['mat']
        subs = []
        tri_out = []
        for ri, r in enumerate(roles):
            tt = nt[mat == ri]
            if len(tt) == 0: continue
            subs.append({'role': r, 'start': int(sum(len(x) for x in tri_out)), 'count': int(len(tt))}); tri_out.append(tt)
        tri_cat = np.concatenate(tri_out).astype(np.int32)
        binf = os.path.join(udir, piece + '.bin')
        with open(binf, 'wb') as fh:
            fh.write(pos[src].astype(np.float32).tobytes()); fh.write(nrm[src].astype(np.float32).tobytes())
            fh.write(uv01.astype(np.float32).tobytes())
            fh.write(bi2[src].astype(np.int32).tobytes()); fh.write(bw[src].astype(np.float32).tobytes())
            fh.write(tri_cat.tobytes())
        bones = [body.bones[i] for i in used]
        manifest['pieces'].append({'name': piece, 'vertexCount': int(V), 'triangleCount': int(len(tri_cat)), 'submeshes': subs,
                                   'bones': bones, 'bindposes': [float(v) for i in used for v in body.hero.body.bind[i].T.reshape(-1)],
                                   'metresPerUv': float(S), 'texture': res})
        # keep everything the map baker needs (split mesh)
        np.savez(os.path.join(udir, piece + '_final.npz'), pos=pos[src], nrm=nrm[src], uv=uv01, tri=tri_cat, mat=np.concatenate([np.full(len(t), i) for i, t in enumerate(tri_out)]),
                 fabric=np.concatenate([g['fabric'][mat == ri] for ri in range(len(roles)) if (mat == ri).any()]),
                 part=np.concatenate([g['part'][mat == ri] for ri in range(len(roles)) if (mat == ri).any()]),
                 kind=np.concatenate([g['kind'][mat == ri] for ri in range(len(roles)) if (mat == ri).any()]) if 'kind' in g else np.zeros(len(tri_cat), np.int32),
                 src=src, S=S, res=res, bi=bi[src], bw=bw[src], **{'a_' + k: v[src] for k, v in A.items() if v.ndim == 1})
        print(f'{piece}: v={V} (from {len(pos)}) tris={len(tri_cat)} bones={len(used)} metres/uv={S:.3f} subs={[s["role"] for s in subs]}')
    with open(os.path.join(udir, 'manifest.json'), 'w') as fh: json.dump(manifest, fh)


if __name__ == "__main__" or "--" in sys.argv and "finalize.py" in " ".join(sys.argv): main()
