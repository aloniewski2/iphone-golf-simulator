"""hole09_stone.py - planar-faced rough stones for the Split ruin (POSTCARD_LOOK, hole 9 repair round 3, 2026-10-05).

Why this module exists. The round-2 stones were convex HULLS of 8..30 jittered points (postcard_props_lib._hull). A hull face of a twisted / tapered / sheared box is not planar: the hull splits it into
two triangles whose normals differ by 1..7 deg, and every triangle got its OWN random UV window (a different brick, a different flip). Measured on the round-2 stage (work/postcard-look/v2/hole09/r7/
face_split_stats.py): of 4,477 masonry faces 2,467 are triangles, 1,411 pairs of adjacent triangles are near-coplanar (< 20 deg, median 1.6 deg) and 1,092 of those 1,411 (77 %) have a UV mismatch
along the shared edge: that is the "diagonal triangle shading" of the review (two bricks meeting along a diagonal).

Here every stone is the intersection of half-spaces {x : n . x <= d} (a convex polyhedron defined by its PLANES), so every face is exactly planar, comes out as ONE n-gon, and gets ONE UV mapping.
The planes are: the six best-fit planes of a tapered / sheared / twisted / tilted hexahedron (the hand-hewn block), plus corner chips (a plane through three points of the three edges at a corner),
edge bevels (arris chamfers) and, on the broken tops, fracture planes. Faces: 6 + 3..9 = 9..16 per stone, 14..26 vertices (30..50 triangles).

All geometry is local (x along the wall, y thickness, z up, origin at the centre of the bottom face): the caller rotates / places the stone.
"""
import itertools
import math

import numpy as np

_COMB = {}


def _combos(m):
    if m not in _COMB:
        _COMB[m] = np.array(list(itertools.combinations(range(m), 3)), dtype=np.int32)
    return _COMB[m]


def _dedupe(P, eps=2e-5):
    """greedy clustering of points closer than eps (the same vertex found from several plane triplets)."""
    keep = []
    for i in range(len(P)):
        for j in keep:
            if abs(P[i, 0] - P[j, 0]) < eps and abs(P[i, 1] - P[j, 1]) < eps and abs(P[i, 2] - P[j, 2]) < eps:
                break
        else:
            keep.append(i)
    return P[keep]


def polyhedron(N, d, tol=1e-6):
    """Convex polyhedron {x : N x <= d}. Returns (V (n, 3), faces) with faces = [(plane index, [vertex indices ccw seen from outside])], or None when empty / unbounded.
    Every face is exactly planar (all its vertices lie on its plane within 3 * tol)."""
    N = np.asarray(N, float)
    d = np.asarray(d, float)
    m = len(d)
    if m < 4:
        return None
    idx = _combos(m)
    A = N[idx]
    b = d[idx]
    det = np.linalg.det(A)
    ok = np.abs(det) > 1e-7
    if not ok.any():
        return None
    sol = np.linalg.solve(A[ok], b[ok][..., None])[..., 0]
    inside = ((sol @ N.T) - d[None, :] <= tol).all(axis=1)
    P = sol[inside]
    if len(P) < 4:
        return None
    P = _dedupe(P)
    if len(P) < 4:
        return None
    faces = []
    for i in range(m):
        on = np.flatnonzero(np.abs(P @ N[i] - d[i]) <= tol * 3)
        if len(on) < 3:
            continue
        pts = P[on]
        c = pts.mean(0)
        n = N[i] / np.linalg.norm(N[i])
        a = np.cross(n, [1.0, 0.0, 0.0]) if abs(n[0]) < 0.9 else np.cross(n, [0.0, 1.0, 0.0])
        a /= np.linalg.norm(a)
        bb = np.cross(n, a)
        ang = np.arctan2((pts - c) @ bb, (pts - c) @ a)
        order = on[np.argsort(ang)]
        faces.append((i, [int(k) for k in order]))
    return P, faces


def face_area(P, vs):
    p = P[vs]
    c = p.mean(0)
    s = np.zeros(3)
    for k in range(len(p)):
        s += np.cross(p[k] - c, p[(k + 1) % len(p)] - c)
    return 0.5 * float(np.linalg.norm(s))


def valid(P, faces, min_area=4e-4, min_edge=0.012):
    """no sliver faces: every face >= 4 cm^2 and every edge >= 1.2 cm."""
    for _i, vs in faces:
        if face_area(P, vs) < min_area:
            return False
        for k in range(len(vs)):
            if float(np.linalg.norm(P[vs[k]] - P[vs[(k + 1) % len(vs)]])) < min_edge:
                return False
    return True


def bad_pairs(faces, N, min_deg=14.0, up_thr=0.70):
    """grass pass 2026-10-05 (RUIN_NO_UV_SPLIT): True when two faces that share an edge are within min_deg of coplanar and NOT both tops (normal z > up_thr). Near-coplanar pairs of tops are fine (both take ONE continuous
    LK_ROCK box projection); a chamfer / chip within 14 deg of a side face would get its own random brick window = a visible UV jump along the shared edge. faces = [(plane index, [vertex ids])], N = plane normals."""
    cmax = math.cos(math.radians(min_deg))
    em = {}
    for fi, (pi, vs) in enumerate(faces):
        for k in range(len(vs)):
            em.setdefault(frozenset((vs[k], vs[(k + 1) % len(vs)])), []).append(fi)
    for fl in em.values():
        if len(fl) == 2:
            na = np.asarray(N[faces[fl[0]][0]], float)
            nb = np.asarray(N[faces[fl[1]][0]], float)
            na, nb = na / np.linalg.norm(na), nb / np.linalg.norm(nb)
            if float(na @ nb) > cmax and not (na[2] > up_thr and nb[2] > up_thr):
                return True
    return False


def hex_planes(C):
    """Six best-fit planes of a hexahedron given by its corners C[(i, j, k)] (i, j, k in {0, 1}); returns (N (6, 3), d (6,)) in the order i0, i1, j0, j1, k0, k1 (outward normals)."""
    ctr = np.mean([C[k] for k in C], axis=0)
    N, d = [], []
    for ax in range(3):
        for side in (0, 1):
            o1, o2 = [a for a in range(3) if a != ax]
            loop = []
            for (b, c) in ((0, 0), (1, 0), (1, 1), (0, 1)):
                key = [0, 0, 0]
                key[ax], key[o1], key[o2] = side, b, c
                loop.append(np.asarray(C[tuple(key)], float))
            n = np.cross(loop[2] - loop[0], loop[3] - loop[1])
            n = n / max(float(np.linalg.norm(n)), 1e-12)
            mid = np.mean(loop, axis=0)
            if float(n @ (mid - ctr)) < 0:
                n = -n
            N.append(n)
            d.append(float(n @ mid))
    return np.array(N), np.array(d)


def _corner_point(N, d, key):
    """intersection of the three planes adjacent to hexahedron corner `key` (plane order i0, i1, j0, j1, k0, k1)."""
    ids = [2 * 0 + key[0], 2 * 1 + key[1], 2 * 2 + key[2]]
    A = N[ids]
    return np.linalg.solve(A, d[ids])


def build_stone(rng, C, chip_p=0.65, chip_leg=(0.05, 0.17), top_chip=None, bevel_p=0.4, bevel_depth=(0.012, 0.035), extra_planes=(), max_faces=17, top_axis=2, corner_legs=None, v_edges=None, check_side=False, min_cuts=0):
    """A planar-faced stone from the corners C of a hexahedron (corner key (i, j, k); top_axis = the index of the 'up' axis, i.e. the k of a box stone; the wedge ring passes its own ordering).
    chip_p     probability per corner of a corner chip (a plane through the points chip_leg[0]..chip_leg[1] m along the three edges; capped at 42 % of the edge; `top_chip` caps the legs of the corners on the
               top (axis top_axis side 1) when the stone carries another one)
    bevel_p    probability per edge of an arris bevel (plane at 12-35 mm depth bisecting the two faces), only edges of the faces that are not the bottom
    extra_planes  [(n, d)] added first (fracture planes of a broken top, a split front face)
    corner_legs   {corner key: (lo, hi) leg range, or None = never chip this corner}: overrides chip_leg per corner (an arch wedge keeps its corners on the joints, chips only the free side)
    check_side    True: no plane is accepted that makes a near-coplanar (< 14 deg) pair of faces unless both are tops (bad_pairs); the base hexahedron itself is never checked (wall stones set it)
    v_edges       (p, (lo, hi)) or None: grass pass 2026-10-05, the four VERTICAL edges of a box stone (planes i0/i1 x j0/j1, only when top_axis is the z axis) are chamfered with probability p each by a plane
                  lo..hi m deep: the top face of a stone gets cut corners (an octagon-ish outline instead of a rectangle), the bearing faces (bed and top) are only trimmed, so a stone that carries another keeps its support
    min_cuts      minimum real corner cuts beyond the base six planes; preserves
                  planar faces and bearing top limits when random cuts miss
    Returns (V (n, 3), faces [[vertex ids]]) with exactly planar faces, or None if the base hexahedron is degenerate."""
    N0, d0 = hex_planes(C)
    base = polyhedron(N0, d0)
    if base is None:
        return None
    N = [n for n in N0]
    d = [x for x in d0]
    ctr = np.mean([C[k] for k in C], axis=0)
    P, faces = base
    cur = (P, faces)
    # extra planes first (accepted only when they keep a valid solid)
    for (n, dd) in extra_planes:
        n = np.asarray(n, float)
        n = n / np.linalg.norm(n)
        res = polyhedron(np.array(N + [n]), np.array(d + [float(dd)]))
        if res is not None and len(res[1]) == len(cur[1]) + 1 and valid(*res) and not (check_side and bad_pairs(res[1], N + [n])):
            N.append(n)
            d.append(float(dd))
            cur = res
    cps = {}
    corner_list = list(itertools.product((0, 1), repeat=3))
    rng.shuffle(corner_list)
    for key in corner_list:
        if len(cur[1]) >= max_faces:
            break
        if rng.random() > chip_p:
            continue
        leg_rng = chip_leg
        if corner_legs is not None and key in corner_legs:
            leg_rng = corner_legs[key]
            if leg_rng is None:
                continue
        p = _corner_point(N0, d0, key)
        legs = []
        pts = []
        for ax in range(3):
            nb = list(key)
            nb[ax] ^= 1
            e = np.asarray(C[tuple(nb)], float) - np.asarray(C[key], float)
            ln = float(np.linalg.norm(e))
            cap = leg_rng[1]
            if top_chip is not None and top_axis is not None and key[top_axis] == 1:
                cap = min(cap, top_chip)
            lg = min(rng.uniform(leg_rng[0], max(leg_rng[0], cap)), 0.42 * ln)
            if top_chip is not None and top_axis is not None and key[top_axis] == 1:
                lg = min(lg, top_chip)
            pts.append(p + e / max(ln, 1e-9) * lg)
        n = np.cross(pts[1] - pts[0], pts[2] - pts[0])
        ln_ = float(np.linalg.norm(n))
        if ln_ < 1e-9:
            continue
        n = n / ln_
        if float(n @ (p - ctr)) < 0:
            n = -n
        dd = float(n @ pts[0])
        res = polyhedron(np.array(N + [n]), np.array(d + [dd]))
        if res is not None and len(res[1]) == len(cur[1]) + 1 and valid(*res) and not (check_side and bad_pairs(res[1], N + [n])):
            N.append(n)
            d.append(dd)
            cur = res
    # vertical-edge chamfers (v_edges): the rounded, weathered plan of a hand-worn stone
    if v_edges is not None and top_axis == 2:
        pv, (dlo, dhi) = v_edges
        vp = [(a, b) for a in (0, 1) for b in (2, 3)]
        rng.shuffle(vp)
        for (a, b) in vp:
            if len(cur[1]) >= max_faces:
                break
            if rng.random() > pv:
                continue
            n = N0[a] + N0[b]
            n = n / float(np.linalg.norm(n))
            t = np.cross(N0[a], N0[b])
            t = t / max(float(np.linalg.norm(t)), 1e-12)
            A = np.array([N0[a], N0[b], t])
            pe = np.linalg.solve(A, np.array([d0[a], d0[b], float(t @ ctr)]))
            dd = float(n @ pe) - rng.uniform(dlo, dhi)
            res = polyhedron(np.array(N + [n]), np.array(d + [dd]))
            if res is not None and len(res[1]) == len(cur[1]) + 1 and valid(*res) and not (check_side and bad_pairs(res[1], N + [n])):
                N.append(n)
                d.append(dd)
                cur = res
    # arris bevels on the visible edges (not the bottom bed): between two planes of the base hexahedron
    edges = []
    for a in range(6):
        for b in range(a + 1, 6):
            if a // 2 == b // 2:
                continue
            if top_axis is not None and (a == 2 * top_axis or b == 2 * top_axis):          # the 'bottom' side of the up axis is plane 2 * top_axis (k0)
                continue
            edges.append((a, b))
    rng.shuffle(edges)
    for (a, b) in edges:
        if len(cur[1]) >= max_faces:
            break
        if rng.random() > bevel_p:
            continue
        n = N0[a] + N0[b]
        n = n / float(np.linalg.norm(n))
        # a point on the edge: intersection of planes a, b and the plane through the stone centre perpendicular to the edge
        t = np.cross(N0[a], N0[b])
        t = t / max(float(np.linalg.norm(t)), 1e-12)
        A = np.array([N0[a], N0[b], t])
        pe = np.linalg.solve(A, np.array([d0[a], d0[b], float(t @ ctr)]))
        dep = rng.uniform(*bevel_depth)
        dd = float(n @ pe) - dep
        res = polyhedron(np.array(N + [n]), np.array(d + [dd]))
        if res is not None and len(res[1]) == len(cur[1]) + 1 and valid(*res) and not (check_side and bad_pairs(res[1], N + [n])):
            N.append(n)
            d.append(dd)
            cur = res
    # A weathered stone must not fall back to an intact six-plane block merely
    # because its random attempts were rejected. Deterministic corner cuts keep
    # the original bearing planes and all the same validity tests.
    for key in itertools.product((0, 1), repeat=3):
        if len(cur[1]) >= min(6 + min_cuts, max_faces):
            break
        leg_rng = chip_leg if corner_legs is None else corner_legs.get(key, chip_leg)
        if leg_rng is None:
            continue
        p = _corner_point(N0, d0, key)
        points = []
        for ax in range(3):
            nb = list(key); nb[ax] ^= 1
            edge = np.asarray(C[tuple(nb)], float) - np.asarray(C[key], float)
            length = float(np.linalg.norm(edge))
            depth = min(0.5 * (leg_rng[0] + leg_rng[1]), 0.24 * length)
            if top_chip is not None and top_axis is not None and key[top_axis] == 1:
                depth = min(depth, top_chip)
            points.append(p + edge / max(length, 1e-9) * depth)
        n = np.cross(points[1] - points[0], points[2] - points[0])
        length = float(np.linalg.norm(n))
        if length < 1e-9:
            continue
        n /= length
        if float(n @ (p - ctr)) < 0:
            n = -n
        dd = float(n @ points[0])
        res = polyhedron(np.array(N + [n]), np.array(d + [dd]))
        if res is not None and len(res[1]) == len(cur[1]) + 1 and valid(*res) and not (check_side and bad_pairs(res[1], N + [n])):
            N.append(n); d.append(dd); cur = res
    P, faces = cur
    # drop the unused plane indices: faces as vertex id lists, plus the outward normal of each
    out_faces = [vs for _i, vs in faces]
    out_n = [np.asarray(N[i]) / np.linalg.norm(N[i]) for i, _vs in faces]
    return P, out_faces, out_n
