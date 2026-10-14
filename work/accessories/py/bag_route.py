"""BAG: pick the strap route from DATA: a clearance map of the torso offset surface over the critical clips (strap vertices follow the skin with weights fitted to its real
motion), then a least-cost path from the clavicle to the rear flank. 
Blender -b --factory-startup --python bag_route.py -- <Male|Female> [offset_mm]
Writes out/<Sex>/golf_bag/route.json (list of [z, theta_deg, offset_m]) and prints the map (rows z, cols theta; min clearance in mm)."""
import sys, os, json, math, heapq
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
from mathutils import Vector
import acc_common as A
import bag_fit as BF, bag_strap_eval as SE

ZS = np.round(np.arange(1.00, 1.37, 0.02), 3)
THS = np.arange(0, 181, 10)


def occupancy(sex, ev, d=0.012):
    import acc_fit as F
    hd = ev.hd; tree = BF.body_tree(hd); clear = F.Clearance(hd.body_pos, hd.body_tri)
    nb = len(ev.bidx)
    M = np.full((len(ZS), len(THS)), np.nan)
    armv = np.array([v for v in range(len(hd.body_pos)) if sum(hd.bw[v, k] for k in range(4) if hd.bones[hd.bi[v, k]].startswith(('RightUpperArm', 'RightLowerArm', 'RightHand'))) > 0.5])
    armP = hd.body_pos[armv]
    for iz, z in enumerate(ZS):
        for it, th in enumerate(THS):
            r = BF.torso_point(tree, hd, float(z), float(th))
            if r is None: continue
            loc, nrm, fi, dist = tree.find_nearest(Vector(r))
            p_rest = r + np.array(nrm) * d
            tri = hd.body_tri[fi]; v = tri[np.argmin(np.linalg.norm(hd.body_pos[tri] - np.array(loc), axis=1))]
            armw = sum(hd.bw[v, k] for k in range(4) if hd.bones[hd.bi[v, k]].startswith(('RightUpperArm', 'RightLowerArm', 'RightHand', 'RightThumb', 'RightIndex', 'RightMiddle', 'RightRing', 'RightLittle', 'LeftUpperArm', 'LeftLowerArm')))
            if armw > 0.5: continue                   # the ray hit the arm, not the torso
            if np.linalg.norm(armP - p_rest, axis=1).min() < 0.032: continue      # too close to the (right) arm at rest: no room for a strap that wide
            s4 = np.append(A.N_to_mesh(hd.body_pos[v][None])[0], 1.0)
            cols = []; tgt = []
            for name, fi_, tr, sk, bp in ev.frames:
                tgt.append(bp[v]); cols.append(np.stack([A.unity_to_N(((sk[1 + k] @ s4)[:3])[None])[0] for k in range(nb)], 1))
            Aa = np.concatenate(cols, 0); b = np.concatenate(tgt, 0)
            lam = 200.0
            w = SE.nnls(np.vstack([Aa, lam * np.ones((1, nb))]), np.append(b, lam)); w = w / max(w.sum(), 1e-9)
            worst = 9.0
            for dzz in (-0.014, 0.0, 0.014):                       # the webbing is 27 mm wide: test its top and bottom edge as well
                pr = p_rest + np.array([0, 0, dzz])
                pm = A.N_to_mesh(pr[None])[0]; p4 = np.append(pm, 1.0)
                worst = min(worst, float(clear(pr[None])[0]))      # rest pose
                for name, fi_, tr, sk, bp in ev.frames:
                    q = sum(w[k] * (sk[1 + k] @ p4)[:3] for k in range(nb))
                    q = A.unity_to_N(q[None])[0]
                    loc2, n2, i2, d2 = tr.find_nearest(Vector(q))
                    s = d2 if np.dot(n2, q - np.array(loc2)) >= 0 else -d2
                    worst = min(worst, s)
            M[iz, it] = worst
    return M


def route_path(M, z0, th0, z1, th1, safe=0.002):
    """Dijkstra on the (z, theta) grid; cost 1 per step where the clearance >= safe, rising steeply below"""
    nz, nt = M.shape
    nanm = np.isnan(M)
    near = nanm.copy()
    for dz in (-1, 0, 1):
        for dt in (-1, 0, 1):
            near |= np.roll(np.roll(nanm, dz, 0), dt, 1)           # cells next to the arm (masked) are not usable: the strap is wider than one cell
    def cost(iz, it):
        c = M[iz, it]
        if np.isnan(c) or near[iz, it]: return 1e6
        return 1.0 + (0 if c >= safe else 40.0 * (safe - c) * 100) * 0.25
    s = (int(np.argmin(np.abs(ZS - z0))), int(np.argmin(np.abs(THS - th0))))
    g = (int(np.argmin(np.abs(ZS - z1))), int(np.argmin(np.abs(THS - th1))))
    dist = {s: cost(*s)}; prev = {}
    pq = [(dist[s], s)]
    while pq:
        dcur, u = heapq.heappop(pq)
        if u == g: break
        if dcur > dist.get(u, 1e18): continue
        for dz in (-1, 0, 1):
            for dt in (-1, 0, 1):
                if dz == 0 and dt == 0: continue
                if dt < 0: continue                      # theta never goes back
                v = (u[0] + dz, u[1] + dt)
                if not (0 <= v[0] < nz and 0 <= v[1] < nt): continue
                step = math.hypot(dz * 0.02 / 0.02, dt * 10 / 10) * cost(*v)
                nd = dcur + step
                if nd < dist.get(v, 1e18): dist[v] = nd; prev[v] = u; heapq.heappush(pq, (nd, v))
    path = [g]
    while path[-1] != s: path.append(prev[path[-1]])
    return path[::-1]


def main():
    a = A.args_after_dashes()
    sex = a[0]
    d = float(a[1]) / 1000 if len(a) > 1 else 0.012
    z0 = float(a[2]) if len(a) > 2 else 1.32
    th0 = float(a[3]) if len(a) > 3 else 50.0
    wo = float(a[4]) if len(a) > 4 else 0.012                 # web offset (m) below the pad
    ev = SE.StrapEval(sex)
    M = occupancy(sex, ev, d)
    print(sex, 'clearance map, strap inner face at %.0f mm, skin-following weights; rows z, cols theta (0 front .. 180 back), mm' % (d * 1000))
    print('       ' + ' '.join('%4d' % t for t in THS))
    for iz in range(len(ZS) - 1, -1, -1):
        print('z=%.2f ' % ZS[iz] + ' '.join(('%4.0f' % (M[iz, it] * 1000)) if not np.isnan(M[iz, it]) else '   .' for it in range(len(THS))))
    np.save(os.path.join(A.ROOT, 'out', sex, 'golf_bag', 'route_map.npy'), M)
    path = route_path(M, z0, th0, 1.06, 140)
    print('path', [(float(ZS[i]), int(THS[j]), round(float(M[i, j]) * 1000)) for i, j in path])
    zz = np.array([ZS[i] for i, j in path], float); tt = np.array([THS[j] for i, j in path], float)
    for _ in range(8):                                   # the grid path is a staircase: smooth it (ends fixed)
        zz[1:-1] = 0.25 * zz[:-2] + 0.5 * zz[1:-1] + 0.25 * zz[2:]; tt[1:-1] = 0.25 * tt[:-2] + 0.5 * tt[1:-1] + 0.25 * tt[2:]
    ix = list(range(0, len(zz), 2)) + ([len(zz) - 1] if (len(zz) - 1) % 2 else [])
    route = [[round(float(zz[i]), 4), round(float(tt[i]), 2), 0.0105 if zz[i] > 1.262 else wo] for i in ix]
    json.dump(route, open(os.path.join(A.ROOT, 'out', sex, 'golf_bag', 'route.json'), 'w'))
    print('route', route)


if __name__ == '__main__':
    main()
