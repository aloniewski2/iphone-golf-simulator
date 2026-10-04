"""Shapes for the Magma Open's design modules (holes 21-23): pure geometry, no Blender. Metres, +Y north.

    ribbon(spine, widths)      the outline of an island that follows a curve: a causeway, a ring, a ridge
    dist_to_polyline(...)      how far a point is from the curve, and how far along it it lies
"""
import math


def catmull(pts, per=10):
    """A smooth curve through pts (Catmull-Rom), per samples between neighbours."""
    p = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for k in range(per):
            t = k / per
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(len(p1))))
    out.append(tuple(pts[-1]))
    return out


def ribbon(spine, widths, per=10, cap_steps=7):
    """The outline (counter-clockwise) of the band that follows `spine` (control points) with half-width `widths`
    (one per control point, smoothly varied between), closed at both ends by a round cap of that end's width."""
    curve = catmull([(x, y) for x, y in spine], per)
    w = [v[0] for v in catmull([(v, 0.0) for v in widths], per)]
    n = len(curve)
    left, right = [], []
    for i in range(n):
        a, b = curve[max(0, i - 1)], curve[min(n - 1, i + 1)]
        dx, dy = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / L, dx / L
        left.append((curve[i][0] + nx * w[i], curve[i][1] + ny * w[i]))
        right.append((curve[i][0] - nx * w[i], curve[i][1] - ny * w[i]))

    def cap(i, forward):
        """A half circle round curve[i] from one side to the other, out past the end."""
        cx, cy = curve[i]
        a, b = curve[max(0, i - 1)], curve[min(n - 1, i + 1)]
        ang = math.atan2(b[1] - a[1], b[0] - a[0])
        r = w[i]
        pts = []
        for k in range(1, cap_steps):
            t = k / cap_steps * math.pi
            th = ang + math.pi / 2 - t if forward else ang - math.pi / 2 - t
            pts.append((cx + math.cos(th) * r, cy + math.sin(th) * r))
        return pts

    return left + cap(n - 1, True) + right[::-1] + cap(0, False)


def dist_to_polyline(px, py, poly):
    """(distance, fraction along) from (px, py) to the polyline poly."""
    best, best_t, total, run = 1e9, 0.0, 0.0, []
    for a, b in zip(poly, poly[1:]):
        total += math.hypot(b[0] - a[0], b[1] - a[1]); run.append(total)
    acc = 0.0
    for i, (a, b) in enumerate(zip(poly, poly[1:])):
        dx, dy = b[0] - a[0], b[1] - a[1]
        seg = math.hypot(dx, dy) or 1e-9
        t = max(0.0, min(1.0, ((px - a[0]) * dx + (py - a[1]) * dy) / (seg * seg)))
        d = math.hypot(px - a[0] - dx * t, py - a[1] - dy * t)
        if d < best:
            best, best_t = d, (acc + seg * t) / (total or 1.0)
        acc += seg
    return best, best_t


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def frame(spine, widths, per=12):
    """Samples along a ribbon: [(x, y, half_width, nx, ny, tx, ty)], (nx, ny) pointing left of the way along."""
    curve = catmull([(x, y) for x, y in spine], per)
    w = [v[0] for v in catmull([(v, 0.0) for v in widths], per)]
    out = []
    n = len(curve)
    for i in range(n):
        a, b = curve[max(0, i - 1)], curve[min(n - 1, i + 1)]
        dx, dy = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dy) or 1.0
        out.append((curve[i][0], curve[i][1], w[i], -dy / L, dx / L, dx / L, dy / L))
    return out


def place(fr, t, k=0.0):
    """The point a fraction t of the way along the ribbon and k of its half-width to the left (negative: the right).
    |k| < 1 is on the ribbon; a bigger k is out in the lava."""
    i = max(0, min(len(fr) - 1, int(round(t * (len(fr) - 1)))))
    x, y, w, nx, ny, _tx, _ty = fr[i]
    return (round(x + nx * w * k, 2), round(y + ny * w * k, 2))


def heading(fr, t):
    """The way the ribbon runs at t, degrees."""
    i = max(0, min(len(fr) - 1, int(round(t * (len(fr) - 1)))))
    return math.degrees(math.atan2(fr[i][6], fr[i][5]))


def polar(cx, cy, deg, r):
    a = math.radians(deg)
    return (round(cx + math.cos(a) * r, 2), round(cy + math.sin(a) * r, 2))
