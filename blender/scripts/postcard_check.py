#!/usr/bin/env python3
"""POSTCARD_HOLES design gates: a pure-Python mirror of Unity/Assets/Scripts/Course/Hole.cs scoring.

No bpy, no mathutils, no numpy: run with the system `python3`.

    python3 blender/scripts/postcard_check.py hole08_design            # gates, PASS/FAIL per line
    python3 blender/scripts/postcard_check.py hole08_design --map      # + ASCII lie map (4 yd cells)
    python3 blender/scripts/postcard_check.py hole08_design hole09_design hole10_design --fast

A design module (blender/scripts/holeNN_design.py) is pure data in COURSE YARDS (X right of the tee
line, D down the hole). See blender/scripts/POSTCARDS_README.md for the schema. Every number in
SHORE / CENTERLINE / HAZARDS is rounded to 0.1 yd so Hole.cs can carry exactly what the mesh was
built from (postcard_emit_cs.py prints the Course.Postcards() block).

Exit code 0 only when every gate line of every module passes.
"""
import importlib
import importlib.util
import math
import os
import sys
import heapq

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

TEE, FAIRWAY, ROUGH, BUNKER, GREEN, WATER, OOB = "Tee", "Fairway", "Rough", "Bunker", "Green", "Water", "OutOfBounds"
CUP_TEE_RADIUS = 4.0            # Hole.LieAt: within 4 yd of the tee is Tee
LAND_OK = {TEE, FAIRWAY, ROUGH, BUNKER, GREEN}   # the ball can sit here without a penalty stroke


# ----------------------------------------------------------------------------- Hole.cs mirror
class Hole:
    def __init__(self, mod):
        self.mod = mod
        self.number = mod.NUMBER
        self.par = mod.PAR
        self.name = getattr(mod, "NAME", "?")
        self.center = [tuple(map(float, p)) for p in mod.CENTERLINE]
        self.fw = float(mod.FAIRWAY_WIDTH)
        self.gr = float(mod.GREEN_RADIUS)
        self.rough = float(mod.ROUGH_WIDTH)
        self.shore = [tuple(map(float, p)) for p in mod.SHORE]
        self.hazards = [(h["kind"], float(h["x"]), float(h["d"]), float(h["width"]), float(h["length"]), h.get("tag", ""))
                        for h in mod.HAZARDS]
        self.tee = self.center[0]
        self.pin = self.center[-1]

    # Hole.Length
    def length(self):
        return sum(math.dist(self.center[i - 1], self.center[i]) for i in range(1, len(self.center)))

    # Hole.DistanceFromCenterline
    def dist_center(self, p):
        best = float("inf")
        for i in range(1, len(self.center)):
            a, b = self.center[i - 1], self.center[i]
            dx, dd = b[0] - a[0], b[1] - a[1]
            l2 = max(dx * dx + dd * dd, 0.0001)
            t = min(max(((p[0] - a[0]) * dx + (p[1] - a[1]) * dd) / l2, 0.0), 1.0)
            best = min(best, math.dist(p, (a[0] + dx * t, a[1] + dd * t)))
        return best

    # CourseHazard.Contains
    @staticmethod
    def in_ellipse(h, p):
        dx = (p[0] - h[1]) / max(h[3] / 2, 0.001)
        dz = (p[1] - h[2]) / max(h[4] / 2, 0.001)
        return dx * dx + dz * dz <= 1

    # Hole.Inside (even-odd, identical expression to the C#)
    @staticmethod
    def inside(poly, p):
        inside = False
        j = len(poly) - 1
        for i in range(len(poly)):
            a, b = poly[i], poly[j]
            if (a[1] > p[1]) != (b[1] > p[1]) and p[0] < (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0]:
                inside = not inside
            j = i
        return inside

    def on_land(self, p):
        return self.inside(self.shore, p)

    # Hole.LieAt
    def lie_at(self, p):
        for h in self.hazards:
            if self.in_ellipse(h, p):
                return WATER if h[0] == "water" else BUNKER
        if not self.on_land(p):
            return WATER
        if math.dist(p, self.pin) <= self.gr:
            return GREEN
        if math.dist(p, self.tee) <= CUP_TEE_RADIUS:
            return TEE
        off = self.dist_center(p)
        if off <= self.fw / 2:
            return FAIRWAY
        if off <= self.fw / 2 + self.rough:
            return ROUGH
        return OOB


# ----------------------------------------------------------------------------- geometry helpers
def seg_intersect(a, b, c, d):
    def orient(p, q, r):
        return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    o1, o2, o3, o4 = orient(a, b, c), orient(a, b, d), orient(c, d, a), orient(c, d, b)
    return (o1 * o2 < 0) and (o3 * o4 < 0)


def polygon_self_intersections(poly):
    n = len(poly)
    hits = []
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        for j in range(i + 2, n):
            if (j + 1) % n == i:
                continue
            c, d = poly[j], poly[(j + 1) % n]
            if seg_intersect(a, b, c, d):
                hits.append((i, j))
    return hits


def poly_area(poly):
    s = 0.0
    for i in range(len(poly)):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % len(poly)]
        s += x1 * y2 - x2 * y1
    return s / 2


def dist_to_poly_edge(p, poly):
    best = float("inf")
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        ab = (b[0] - a[0], b[1] - a[1])
        l2 = ab[0] ** 2 + ab[1] ** 2 + 1e-12
        t = min(max(((p[0] - a[0]) * ab[0] + (p[1] - a[1]) * ab[1]) / l2, 0.0), 1.0)
        best = min(best, math.dist(p, (a[0] + ab[0] * t, a[1] + ab[1] * t)))
    return best


def ellipse_pts(h, n=72, grow=0.0):
    return [(h[1] + (h[3] / 2 + grow) * math.cos(2 * math.pi * k / n), h[2] + (h[4] / 2 + grow) * math.sin(2 * math.pi * k / n))
            for k in range(n)]


def ellipse_gap(h1, h2):
    """Approximate min distance between two axis-aligned ellipses (0 when they overlap)."""
    for p in ellipse_pts(h1, 90):
        if Hole.in_ellipse(h2, p):
            return 0.0
    for p in ellipse_pts(h2, 90):
        if Hole.in_ellipse(h1, p):
            return 0.0
    best = float("inf")
    for p in ellipse_pts(h1, 90):
        for q in ellipse_pts(h2, 90):
            best = min(best, math.dist(p, q))
    return best


def polyline_samples(pts, step=0.25):
    out = []
    acc = 0.0
    for i in range(1, len(pts)):
        a, b = pts[i - 1], pts[i]
        L = math.dist(a, b)
        n = max(1, int(math.ceil(L / step)))
        for k in range(n):
            t = k / n
            out.append((acc + L * t, (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)))
        acc += L
    out.append((acc, pts[-1]))
    return out


# ----------------------------------------------------------------------------- grid analysis
class Grid:
    def __init__(self, hole, cell=1.0, margin=4.0):
        self.h = hole
        self.cell = cell
        xs = [p[0] for p in hole.shore]
        ds = [p[1] for p in hole.shore]
        self.x0, self.d0 = min(xs) - margin, min(ds) - margin
        self.nx = int(math.ceil((max(xs) + margin - self.x0) / cell))
        self.nd = int(math.ceil((max(ds) + margin - self.d0) / cell))
        self.lie = {}
        for i in range(self.nx):
            for j in range(self.nd):
                self.lie[(i, j)] = hole.lie_at(self.centre(i, j))

    def centre(self, i, j):
        return (self.x0 + (i + 0.5) * self.cell, self.d0 + (j + 0.5) * self.cell)

    def cell_of(self, p):
        return (int((p[0] - self.x0) // self.cell), int((p[1] - self.d0) // self.cell))

    def components(self, ok):
        """4-connected components of cells where ok(lie) is true -> list of sets."""
        seen, comps = set(), []
        for c, lie in self.lie.items():
            if c in seen or not ok(lie):
                continue
            stack, comp = [c], set()
            seen.add(c)
            while stack:
                cur = stack.pop()
                comp.add(cur)
                for dx, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    n = (cur[0] + dx, cur[1] + dj)
                    if n in self.lie and n not in seen and ok(self.lie[n]):
                        seen.add(n)
                        stack.append(n)
            comps.append(comp)
        return comps

    def clearance(self, ok):
        """Approximate Euclidean distance (yd) from each ok-cell to the nearest not-ok cell (2-pass chamfer)."""
        INF = 1e9
        dist = {c: (INF if ok(l) else 0.0) for c, l in self.lie.items()}
        order = sorted(self.lie)
        a, b = 1.0, math.sqrt(2)
        for (i, j) in order:
            if dist[(i, j)] == 0:
                continue
            best = dist[(i, j)]
            for di, dj, w in ((-1, 0, a), (0, -1, a), (-1, -1, b), (1, -1, b)):
                n = (i + di, j + dj)
                best = min(best, dist.get(n, 0.0) + w)      # off-grid counts as not-ok
            dist[(i, j)] = best
        for (i, j) in reversed(order):
            if dist[(i, j)] == 0:
                continue
            best = dist[(i, j)]
            for di, dj, w in ((1, 0, a), (0, 1, a), (1, 1, b), (-1, 1, b)):
                n = (i + di, j + dj)
                best = min(best, dist.get(n, 0.0) + w)
            dist[(i, j)] = best
        return {c: v * self.cell for c, v in dist.items()}

    def widest_path(self, start, goal, ok):
        """Max over paths start->goal of the min clearance along the path (8-neighbour). Returns (bottleneck_yd, reachable)."""
        clr = self.clearance(ok)
        if start not in clr or goal not in clr or clr[start] <= 0 or clr[goal] <= 0:
            return 0.0, False
        best = {start: clr[start]}
        pq = [(-clr[start], start)]
        while pq:
            neg, cur = heapq.heappop(pq)
            if cur == goal:
                return -neg, True
            if -neg < best.get(cur, 0):
                continue
            for di in (-1, 0, 1):
                for dj in (-1, 0, 1):
                    if not di and not dj:
                        continue
                    n = (cur[0] + di, cur[1] + dj)
                    if n not in clr or clr[n] <= 0:
                        continue
                    val = min(-neg, clr[n])
                    if val > best.get(n, 0):
                        best[n] = val
                        heapq.heappush(pq, (-val, n))
        return 0.0, False

    def ascii(self, step=4):
        sym = {TEE: "T", FAIRWAY: "=", ROUGH: ".", BUNKER: "b", GREEN: "G", WATER: " ", OOB: "#"}
        rows = []
        for j in range(0, self.nd, step):
            row = ""
            for i in range(0, self.nx, step):
                lie = self.lie[(i, j)]
                row += sym[lie] if lie != WATER else ("~" if self.h.on_land(self.centre(i, j)) is False else "w")
            rows.append(f"{self.d0 + (j + .5) * self.cell:7.0f} |{row}")
        header = f"        x from {self.x0:.0f} to {self.x0 + self.nx * self.cell:.0f}, one char = {step} yd; ~ sea, w = water hazard over land, = fairway, . rough, b bunker, G green, T tee, # OOB land"
        return header + "\n" + "\n".join(rows)


# ----------------------------------------------------------------------------- the gates
def rounded_ok(v, places=1):
    return abs(v * 10 ** places - round(v * 10 ** places)) < 1e-6


def run(modname, fast=False, show_map=False):
    if modname.endswith(".py") or os.sep in modname:       # a design file by path
        spec = importlib.util.spec_from_file_location(os.path.basename(modname)[:-3], modname)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
    else:
        mod = importlib.import_module(modname)
        importlib.reload(mod)
    h = Hole(mod)
    L = dict(length_min=0, length_max=1e9, par=h.par, carry_max=140.0, water=0, bunker=0, forced=True, pads=None,
             min_dry_before=40.0, min_dry_after=30.0, min_pad_area=500.0, route_clearance_min=6.0,
             oob_land_max_pct=1.0, shore_edge_margin=1.0)
    L.update(getattr(mod, "LIMITS", {}))
    lines = []

    def gate(name, ok, detail=""):
        lines.append((name, bool(ok), detail))

    # --- numbers, rounding
    coords = list(h.center) + list(h.shore) + [(x, d) for _, x, d, _, _, _ in h.hazards] + [(w, l) for _, _, _, w, l, _ in h.hazards]
    bad = [c for c in coords if not (rounded_ok(c[0]) and rounded_ok(c[1]))]
    gate("NUMBERS_ROUNDED_0.1", not bad and all(rounded_ok(v) for v in (h.fw, h.gr, h.rough)),
         f"{len(bad)} coordinate(s) not on a 0.1 yd grid" if bad else "all coordinates on a 0.1 yd grid")
    gate("NUMBER_AND_PAR", h.number == L.get("number", h.number) and h.par == L["par"], f"hole {h.number} '{h.name}' par {h.par}")
    ln = h.length()
    gate("LENGTH", L["length_min"] <= ln <= L["length_max"], f"centerline {ln:.1f} yd, brief {L['length_min']}-{L['length_max']}")

    # --- one shore, one centerline, one pin
    sh = h.shore
    hits = polygon_self_intersections(sh)
    minedge = min(math.dist(sh[i], sh[(i + 1) % len(sh)]) for i in range(len(sh)))
    gate("ONE_SHORE", len(sh) >= 12 and not hits and abs(poly_area(sh)) > 1000 and minedge >= 0.5,
         f"{len(sh)} pts, area {abs(poly_area(sh)):.0f} yd2, self-intersections {len(hits)}, min edge {minedge:.2f} yd")
    dup = any(math.dist(h.center[i - 1], h.center[i]) < 1.0 for i in range(1, len(h.center)))
    gate("ONE_CENTERLINE", len(h.center) >= 3 and h.tee == (0.0, 0.0) and not dup,
         f"{len(h.center)} stations, tee {h.tee}, pin {h.pin}")
    station_lies = [h.lie_at(p) for p in h.center]
    gate("STATIONS_DRY", station_lies[0] == TEE and station_lies[-1] == GREEN and all(l in (FAIRWAY, GREEN, TEE) for l in station_lies),
         f"station lies {station_lies} (RecommendedTarget aims at stations: they must be dry fairway)")
    tee_ring = [(h.tee[0] + 8 * math.cos(a / 8 * math.pi), h.tee[1] + 8 * math.sin(a / 8 * math.pi)) for a in range(16)]
    gate("TEE_ON_LAND", all(h.lie_at(p) in LAND_OK for p in tee_ring + [h.tee]), "8 yd around the tee is land with no hazard")
    green_ring = [(h.pin[0] + (h.gr + 1.5) * math.cos(a / 18 * math.pi), h.pin[1] + (h.gr + 1.5) * math.sin(a / 18 * math.pi)) for a in range(36)]
    gate("GREEN_ON_LAND", h.lie_at(h.pin) == GREEN and all(h.on_land(p) for p in green_ring),
         f"pin is Green; the green disc + 1.5 yd is inside the shore")

    # --- hazards
    nw = sum(1 for k in h.hazards if k[0] == "water")
    nb = sum(1 for k in h.hazards if k[0] == "bunker")
    gate("HAZARD_COUNTS", nw == L["water"] and nb == L["bunker"], f"{nw} water + {nb} bunker, brief {L['water']} + {L['bunker']}")
    on_land_issues = []
    for k in h.hazards:
        if k[0] == "bunker":
            pts = ellipse_pts(k, 48)
            if not all(h.on_land(p) for p in pts) or min(dist_to_poly_edge(p, sh) for p in pts) < L["shore_edge_margin"]:
                on_land_issues.append(f"bunker {k[5] or (k[1], k[2])} touches the shore")
    gate("BUNKERS_ON_LAND", not on_land_issues, "; ".join(on_land_issues) or "every bunker ellipse sits inside the shore with margin")
    clash = []
    hz = h.hazards
    for i in range(len(hz)):
        for j in range(i + 1, len(hz)):
            if hz[i][0] == "water" and hz[j][0] == "water":
                continue
            gap = ellipse_gap(hz[i], hz[j])
            if gap < 2.0:
                clash.append(f"{hz[i][5] or i}/{hz[j][5] or j} gap {gap:.1f} yd")
    for k in hz:
        if any(Hole.in_ellipse(k, p) for p in h.center) or Hole.in_ellipse(k, h.tee) or any(Hole.in_ellipse(k, p) for p in tee_ring):
            clash.append(f"{k[5] or k[:3]} covers the tee area or a station")
        if k[0] == "bunker" and any(Hole.in_ellipse(k, (h.pin[0] + 0.6 * h.gr * math.cos(a / 8 * math.pi), h.pin[1] + 0.6 * h.gr * math.sin(a / 8 * math.pi))) for a in range(16)):
            clash.append(f"bunker {k[5] or k[:3]} eats the inner green")
    gate("HAZARD_CLEARANCE", not clash, "; ".join(clash) or "bunkers/waters apart (>= 2 yd), tee, stations and the inner green are clear")

    # --- carries along the centerline (what the aim assist and the brief call the carry)
    samples = polyline_samples(h.center, 0.25)
    runs, cur = [], None
    for s, p in samples:
        w = h.lie_at(p) == WATER
        if w and cur is None:
            cur = [s, s]
        elif w:
            cur[1] = s
        elif cur is not None:
            runs.append(tuple(cur))
            cur = None
    if cur is not None:
        runs.append(tuple(cur))
    total = samples[-1][0]
    carries = [b - a + 0.25 for a, b in runs]
    expected_runs = nw if L["forced"] else nw
    gate("CARRY_MAX", len(runs) == expected_runs and all(c <= L["carry_max"] for c in carries),
         f"{len(runs)} water crossing(s) on the centerline, carries {[round(c, 1) for c in carries]} yd, max allowed {L['carry_max']} (brief: no forced carry over {L['carry_max']})")
    dry_issues = []
    prev_end = 0.0
    for idx, (a, b) in enumerate(runs):
        before = a - prev_end
        nxt = runs[idx + 1][0] if idx + 1 < len(runs) else total
        after = nxt - b
        if before < L["min_dry_before"]:
            dry_issues.append(f"crossing {idx + 1}: only {before:.1f} yd dry ground short of the water")
        if after < L["min_dry_after"]:
            dry_issues.append(f"crossing {idx + 1}: only {after:.1f} yd dry ground after it")
        prev_end = b
    # dry ground short of the water must be Fairway/Tee, not a bunker or rough-only sliver
    for idx, (a, b) in enumerate(runs):
        short_lies = {h.lie_at(pt) for s, pt in samples if a - 25 <= s < a}
        if not short_lies <= {FAIRWAY, TEE, GREEN}:
            dry_issues.append(f"crossing {idx + 1}: the 25 yd short of it is not clean fairway ({sorted(short_lies)})")
    gate("DRY_GROUND_SHORT_OF_EACH_CARRY", runs and not dry_issues or (not runs and nw == 0),
         "; ".join(dry_issues) or f"lay-up room >= {L['min_dry_before']} yd short, landing room >= {L['min_dry_after']} yd after, every crossing")

    # --- grid: pads, routes, slivers, OOB land
    g = Grid(h, 2.0 if fast else 1.0)
    pads = [c for c in g.components(lambda l: l in LAND_OK) if len(c) * g.cell ** 2 >= 1]
    areas = sorted((len(c) * g.cell ** 2 for c in pads), reverse=True)
    tee_cell, pin_cell = g.cell_of(h.tee), g.cell_of(h.pin)
    connected = any(tee_cell in c and pin_cell in c for c in pads)
    if L["forced"]:
        want = L["pads"] if L["pads"] is not None else nw + 1
        gate("PADS_SEPARATED", not connected and len(pads) == want and all(a >= L["min_pad_area"] for a in areas),
             f"{len(pads)} separate dry pads (want {want}), areas {[round(a) for a in areas]} yd2 (min {L['min_pad_area']}), tee->pin dry route exists: {connected}")
    else:
        slivers = [a for a in areas if a < L["min_pad_area"]]
        gate("ONE_ISLAND_NO_SLIVERS", len(pads) == 1 and connected,
             f"{len(pads)} dry component(s), areas {[round(a) for a in areas]} yd2, tee->pin connected: {connected} (one island, one pin)")
        route_ok = lambda l: l in (TEE, FAIRWAY, ROUGH, GREEN)
        bott, reach = g.widest_path(tee_cell, pin_cell, route_ok)
        gate("DRY_ROUTE_AROUND_THE_WATER", reach and bott >= L["route_clearance_min"],
             f"widest bunker-free, water-free route tee->pin has half-width {bott:.1f} yd (need >= {L['route_clearance_min']}): the safe route exists")
    oob = sum(1 for c, l in g.lie.items() if l == OOB)
    land = sum(1 for c, l in g.lie.items() if l in LAND_OK or l == OOB)
    pct = 100.0 * oob / max(1, land)
    gate("NO_OUT_OF_BOUNDS_LAND", pct <= L["oob_land_max_pct"],
         f"{pct:.2f}% of the dry area is out-of-bounds grass (max {L['oob_land_max_pct']}%): the shore must sit inside fairway/2 + rough or RoughWidth is too small")

    if show_map:
        print(g.ascii(4))
    return h, lines, g


def main(argv):
    fast = "--fast" in argv
    show_map = "--map" in argv
    mods = [a for a in argv if not a.startswith("--")]
    if not mods:
        print(__doc__)
        return 2
    allok = True
    for m in mods:
        h, lines, g = run(m, fast, show_map)
        print(f"=== {m}: hole {h.number} {h.name} par {h.par} ===")
        for name, ok, detail in lines:
            print(f"GATE: {name} {'PASS' if ok else 'FAIL'} - {detail}")
            allok &= ok
    print("RESULT:", "ALL PASS" if allok else "FAILURES")
    return 0 if allok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
