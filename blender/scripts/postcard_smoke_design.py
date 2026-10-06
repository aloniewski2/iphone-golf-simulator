"""Synthetic design used ONLY by postcard_lib_smoke.py (hole number 90, never shipped).

Pure Python (no bpy, no mathutils), course yards, schema of POSTCARDS_README.md:
a needle-like ribbon (the shore hugs the scoring centerline at 15.8 yd, so there is no out-of-bounds
land), ONE water ellipse that swallows the whole neck (it sticks out past the shore on both sides, so the
ribbon splits into two separate pads), ONE bunker in front of the green and a green bulb at the end.
Run:  python3 blender/scripts/postcard_check.py postcard_smoke_design --map
"""
import math

NUMBER = 90
NAME = "Smoke"
PAR = 4
PLAY_Z = 22.0

CENTERLINE = [(0.0, 0.0), (3.0, 55.0), (6.0, 100.0), (8.0, 168.0), (3.0, 215.0), (-3.0, 255.0)]
FAIRWAY_WIDTH = 16.0
GREEN_RADIUS = 14.0
ROUGH_WIDTH = 8.0
SHORE_HALF_WIDTH = 15.8          # < FAIRWAY_WIDTH/2 + ROUGH_WIDTH: every dry cell is fairway or rough

HAZARDS = [
    dict(kind="water", x=7.5, d=130.0, width=58.0, length=56.0, tag="gap"),
    dict(kind="bunker", x=6.4, d=225.3, width=9.0, length=12.0, tag="front"),
]

LIMITS = dict(length_min=240, length_max=300, par=4, carry_max=140.0, water=1, bunker=1, forced=True, pads=2)
SCENERY = dict(lava=False, note="smoke test only")


def _unit(a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    n = math.hypot(dx, dy)
    return (dx / n, dy / n)


def _offset_left(pts, r, step=5.0):
    """Left offset of a polyline with round joins on the convex side and mitred joins on the concave side."""
    out = []
    n = len(pts)
    segs = [_unit(pts[i], pts[i + 1]) for i in range(n - 1)]
    norm = [(-d[1], d[0]) for d in segs]

    def line(a, b):
        L = math.dist(a, b)
        k = max(1, int(math.ceil(L / step)))
        return [(a[0] + (b[0] - a[0]) * t / k, a[1] + (b[1] - a[1]) * t / k) for t in range(k)]

    cur = (pts[0][0] + norm[0][0] * r, pts[0][1] + norm[0][1] * r)
    for i in range(1, n - 1):
        d0, d1, n0, n1 = segs[i - 1], segs[i], norm[i - 1], norm[i]
        cross = d0[0] * d1[1] - d0[1] * d1[0]
        p = pts[i]
        if cross > 1e-9:          # left turn: the left side is the inside of the bend, mitre
            m = (n0[0] + n1[0], n0[1] + n1[1])
            k = 1.0 + (n0[0] * n1[0] + n0[1] * n1[1])
            nxt = (p[0] + m[0] / k * r, p[1] + m[1] / k * r)
            out += line(cur, nxt)
            cur = nxt
        else:                     # right turn: round join
            a0, a1 = math.atan2(n0[1], n0[0]), math.atan2(n1[1], n1[0])
            sweep = a1 - a0
            while sweep > 0:
                sweep -= 2 * math.pi
            k = max(1, int(abs(sweep) * r / 3.0))
            first = (p[0] + n0[0] * r, p[1] + n0[1] * r)
            out += line(cur, first)
            for s in range(1, k + 1):
                a = a0 + sweep * s / k
                out.append((p[0] + math.cos(a) * r, p[1] + math.sin(a) * r)) if s < k else None
            cur = (p[0] + n1[0] * r, p[1] + n1[1] * r)
    last = (pts[-1][0] + norm[-1][0] * r, pts[-1][1] + norm[-1][1] * r)
    out += line(cur, last)
    out.append(last)
    return out


def _cap(center, d_in, r, step=3.0):
    """Half circle on the front of `center` (direction d_in), from the left offset to the right offset."""
    n = (-d_in[1], d_in[0])
    a0 = math.atan2(n[1], n[0])
    k = max(4, int(math.pi * r / step))
    return [(center[0] + math.cos(a0 - math.pi * s / k) * r, center[1] + math.sin(a0 - math.pi * s / k) * r) for s in range(1, k)]


def _build_shore():
    r = SHORE_HALF_WIDTH
    c = CENTERLINE
    fwd = _offset_left(c, r)
    back = _offset_left(list(reversed(c)), r)
    d_end = _unit(c[-2], c[-1])
    d_start = _unit(c[1], c[0])
    pts = fwd[:-1] + [fwd[-1]] + _cap(c[-1], d_end, r) + back[:-1] + [back[-1]] + _cap(c[0], d_start, r)
    out = []
    for p in pts:
        q = (round(p[0], 1), round(p[1], 1))
        if not out or math.dist(out[-1], q) >= 0.5:
            out.append(q)
    if math.dist(out[0], out[-1]) < 0.5:
        out.pop()
    return out


SHORE = _build_shore()
