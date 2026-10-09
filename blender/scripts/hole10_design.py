"""Hole 10 "Crater" design data. Pure python (no bpy / mathutils / numpy), COURSE YARDS: X right of the tee line,
D down the hole, tee at (0, 0). The numbers are baked literals rounded to 0.1 yd; the helpers at the bottom of
the file re-derive them (`python3 hole10_design.py --verify`), nothing is computed at import.

Gates:   python3 blender/scripts/postcard_check.py ./blender/scripts/hole10_design.py --map
         (give postcard_check.py the file PATH: with the bare module name `hole10_design` it dies with an
         UnboundLocalError in run(), because `import importlib.util` inside the `if` makes `importlib` a local name)
Plan:    python3 blender/scripts/postcard_plan_png.py ./blender/scripts/hole10_design.py out.png --scale 3

TOPOLOGY
    One shore polygon, one centerline, one pin. A rim ribbon curves round part of a crater (a crescent: fitted rim
    radius 171.5 yd, centre of curvature on the +X side of the tee, 75.8 degrees of heading change from the tee,
    12.2 deg, to the breach, 88.0 deg). It starts at a flat tee slab (28 yd wide, 15 yd behind the tee), runs as a
    33-44 yd wide land strip (22 yd fairway + rough, cliff tops meander) and pinches to a 29 yd neck just before the
    end of the fairway. The shore polygon stays continuous through the neck and ends in a round green bulb (shore
    radius 22.8 yd round the pin). The one lava ellipse (HAZARDS[0], axis-aligned, 112 yd wide along X, 76 yd long
    along D) swallows the whole neck, so the dry ground is exactly two pads:
        pad 1 = tee slab + rim ribbon (about 8,560 yd2), pad 2 = 11 yd neck stub + green bulb (about 2,050 yd2).
    Inside the ellipse the mesh drops to the lava (the build cuts the TERRAIN out under the ellipse), so the bulb
    stands alone as a pillar whose top is the same height as the fairway (PLAY_Z). Lava (z = 0) lies under everything
    outside the shore and inside the ellipse; it scores as Water everywhere (outside the shore, or inside a hazard).

STATIONS (CENTERLINE; every one is dry Fairway, the last is the pin)
    tee   (  0.0,   0.0)
    A     ( 14.3,  66.2)   chord 67.7 yd, heading 12.2 deg
    B     ( 50.2, 123.4)   chord 67.5 yd, heading 32.1 deg   (turn 19.9 deg at A)
    C     (107.1, 159.3)   chord 67.3 yd, heading 57.8 deg   (turn 25.6 deg at B); last dry station, 8 yd before the lava
    pin   (261.0, 164.7)   chord 154.0 yd, heading 88.0 deg  (turn 30.2 deg at C); the breach, straight across the lava
    Length 356.5 yd (brief 340-380), tee->pin straight line 308.6 yd.
    The three rim chords are about 68 yd so the fairway stripe (the polyline, one constant 22 yd) bends in 20-30
    degree steps and reads as a curve; the rim curvature rises from 1/270 to 1/125 per yd, it tightens into the breach.

INTENDED PLAY LINES
    Tee shot (par 4, 357 yd): a driver of about 190-205 yd to C, D 159.3 (the straight tee->C line is 192 yd at
        heading 34 deg and flies across the crater; only the roll is scored, so cutting the corner is legal).
        The dry fairway ends 210.8 yd along the centerline, so a drive that rolls past C goes into the lava.
        The default aim marker (RecommendedTarget = first station >= 35 yd away) is A, 67.7 yd out: the player is
        expected to re-aim at C, or to walk the rim A -> B -> C.
    Lay-up spots (dry, clean fairway): B, D 123.4 (leaves 215 yd to the pin) or A, D 66.2; each still needs a rim
        shot up to C (D 159.3) before the carry.
    Second shot / approach: from C, 154 yd to the pin (AutoClub picks the Iron, reference carry 160 yd): the lava
        carry is 112.0 yd (centerline run, 8 to 120 yd after C), then 34 yd of dry neck + bulb to the pin.
        The ball has to touch down east of x = 227 (the lava's east edge); the green disc (radius 16) spans x 245-277.
    Bunkers (HAZARDS[1], [2]): where a bailout drifts. A player who aims away from the lava lands on the rim shoulders:
        "rim inside" (57.4, 110.3, D 110) on the crater side, 13.0 yd off the centerline at s = 128, and
        "rim outside" (74.3, 154.0, D 154) on the outer side, 13.0 yd off at s = 172 (staggered 44 yd, no pinch).
        Neither touches the centerline (5.2 and 5.7 yd away), a station, or the last 25 yd before the lava: their far
        edges are at s = 142 and s = 180, the lava starts at s = 210.8.

NUMBERS NOT IN THE BRIEF, AND WHY
    PLAY_Z 6.0 m (was 26, changed by the lead after hole 8's flight-height finding): Unity draws the ball at GroundHeight(p) +
        flight height and GroundHeight is 0 over the cut-away lava, so every carry pops the ball by PLAY_Z at both edges. 6 m keeps the
        pop small; the pillar is 6 m tall (still a pillar from the ball's eye height), the far wall ring and stacks carry the drama.
        wall_ring h_min/h_max stay 30/52 m above the lava (absolute); stack h values are unchanged (9-21 m above the lava, so most
        stand ABOVE the play surface now: the builder may scale them down so none blocks a line of play); pillar top_z == PLAY_Z.
    FAIRWAY_WIDTH 22 / ROUGH_WIDTH 13: fairway/2 + rough = 24.0 yd is the out-of-bounds limit; the shore sits 14.4-23.1 yd
        from the polyline (measured), so the dry area is 0.00 % out-of-bounds grass.
    GREEN_RADIUS 16: pillar top 45.6 yd across, a 6.8 yd rough collar all round (the lead wanted 15-17).
    "Shore 2-6 yd beyond the green's rough" was NOT used: the code makes everything past fairway/2 + rough = 24 yd of the
        centerline out-of-bounds, so the bulb edge sits at 22.8 yd from the pin (6.8 yd beyond the green disc) instead.
    Lava ellipse 112 x 76 at (171.1, 161.5): chord 112.0 yd (aim 105-135, limit 150); 8 yd of fairway after C, 34 yd after
        the lava; 76 yd long (+-38) so it covers the +-14.5 yd neck; its rim meets the shore at 56-67 degrees (no horns).
    Neck half width 14.5 (29 yd): fairway 22 + 3.5 yd of rough per side; the bulb is a disc of 22.8 yd so it reads as a pillar.
    Bunkers 17 x 12 and 18 x 13: about 40 % of the fairway width, big enough to read at phone size; each has a 3-4 yd
        pocket in the cliff top beside it so it keeps 2.1-2.3 yd to the shore (the gate wants >= 1.0).
    Tee slab: 28 yd wide, 15 yd behind the tee and 6 forward (the brief's flat slab), rounded 4 yd corners.

SCENERY (read only by the hole-10 build script; yards and degrees unless a key says metres)
    lava (bool), lava_mesh "WATER_LAVA", lava_material "MAT_LAVA" (orange, the only new material, not animated),
    lava_z (m, 0.0 = the water level of Hole.cs / HoleView).
    crater: cx, cd (yd, centre of the rim circle), rim_radius (yd, least-squares fit of the rim arc).
    wall_ring: cx, cd (same centre), r_inner / r_outer (yd, the far basalt wall = a ring of ROCK_* library instances,
        88 yd beyond the farthest shore point), h_min / h_max (m ABOVE THE LAVA, i.e. above z = 0: tops 4 to 26 m above PLAY_Z).
    stacks: 11 basalt pillars standing in the lava (scenery only, names ROCK_*): x, d (yd, centre), r (yd, base radius),
        h (m above the lava; 9-21 m; builder may scale them). All centres are outside the shore and the lava ellipse,
        >= 28 yd from the shore and >= 43 yd from the centerline (nothing on a line of play).
    pillar: x, d (yd, the pin), r_top (yd; the bulb pad round the pin), top_z (m, == PLAY_Z), base_inset (m: the column's base
        radius is this much SMALLER than the top, i.e. undercut, never wider: ground colliders are single sided).
    tee_box: x, d (yd, centre of the TEE_BOX mesh), width, depth (yd), heading (deg from +D toward +X, the first chord).
    Build notes: SHORE is the TERRAIN top outline at PLAY_Z; subtract the lava ellipse from it (inside the ellipse the
        terrain is removed, no upward face may remain there). The cut ends of pad 1 and pad 2 are the ellipse's rim.
        FAIRWAY / GREEN / bunkers are the Hole.cs lies at the usual small z offsets.

Regenerate:  python3 hole10_design.py --verify   (exit 0 when the baked data equals the generator)
             python3 hole10_design.py --regen    (prints fresh literals)
"""
import math

NUMBER = 10
NAME = "Crater"
PAR = 4
PLAY_Z = 6.0                        # metres above the lava: the ONE play height (6 m, see PLAY_Z note in the docstring)
FAIRWAY_WIDTH = 22.0
GREEN_RADIUS = 16.0
ROUGH_WIDTH = 13.0

LIMITS = dict(number=10, length_min=340, length_max=380, par=4, carry_max=150.0, water=1, bunker=2, forced=True,
              pads=2, min_pad_area=500.0)

# ----------------------------------------------------------------------------- baked data (yards, 0.1)
CENTERLINE = [
    (0.0, 0.0), (14.3, 66.2), (50.2, 123.4), (107.1, 159.3), (261.0, 164.7),
]

HAZARDS = [
    dict(kind="water", x=171.1, d=161.5, width=112.0, length=76.0, tag="lava"),
    dict(kind="bunker", x=57.4, d=110.3, width=17.0, length=12.0, tag="rim inside"),
    dict(kind="bunker", x=74.3, d=154.0, width=18.0, length=13.0, tag="rim outside"),
]

SHORE = [
    (-15.6, -8.6), (-15.5, -3.8), (-14.8, 1.0), (-14.0, 5.7), (-13.1, 10.5), (-12.3, 15.2),
    (-12.0, 20.0), (-11.7, 24.8), (-11.2, 29.6), (-10.5, 34.4), (-9.7, 39.1), (-8.7, 43.8),
    (-7.8, 48.5), (-7.0, 53.3), (-6.3, 58.0), (-5.7, 62.8), (-5.0, 67.6), (-3.8, 72.2),
    (-1.9, 76.6), (0.5, 80.8), (3.0, 84.9), (5.6, 88.9), (8.3, 93.0), (10.9, 97.0),
    (13.5, 101.1), (15.9, 105.2), (18.3, 109.4), (20.5, 113.7), (22.7, 118.0), (25.0, 122.2),
    (27.3, 126.4), (29.8, 130.5), (32.6, 134.5), (35.7, 138.1), (39.5, 141.0), (43.7, 143.4),
    (47.9, 145.7), (52.1, 148.1), (56.0, 150.8), (59.7, 153.9), (63.2, 157.2), (66.8, 160.4),
    (70.7, 163.2), (75.0, 165.4), (79.5, 167.2), (83.9, 168.9), (88.4, 170.7), (92.9, 172.4),
    (97.4, 174.2), (102.0, 175.7), (106.7, 176.3), (111.5, 175.9), (116.2, 175.1), (121.0, 174.6),
    (125.8, 174.5), (130.6, 174.6), (135.4, 174.8), (140.2, 175.0), (145.1, 175.1), (149.9, 175.3),
    (154.7, 175.5), (159.5, 175.6), (164.3, 175.8), (169.1, 176.0), (173.9, 176.2), (178.7, 176.3),
    (183.5, 176.5), (188.3, 176.7), (193.1, 176.8), (198.0, 177.0), (202.8, 177.2), (207.6, 177.3),
    (212.4, 177.5), (217.2, 177.7), (222.0, 177.8), (226.8, 178.0), (231.6, 178.1), (236.4, 178.5),
    (241.1, 179.6), (245.4, 181.7), (249.5, 184.2), (253.8, 186.2), (258.5, 187.3), (263.3, 187.4),
    (268.0, 186.4), (272.4, 184.4), (276.3, 181.6), (279.4, 178.0), (281.8, 173.8), (283.2, 169.2),
    (283.7, 164.5), (283.2, 159.7), (281.6, 155.2), (279.2, 151.1), (275.9, 147.6), (272.0, 144.8),
    (267.6, 142.9), (262.9, 142.0), (258.1, 142.1), (253.4, 143.3), (249.1, 145.3), (244.8, 147.5),
    (240.2, 148.9), (235.4, 149.3), (230.6, 149.2), (225.8, 149.0), (221.0, 148.8), (216.2, 148.6),
    (211.4, 148.4), (206.6, 148.3), (201.8, 148.1), (196.9, 147.9), (192.1, 147.8), (187.3, 147.6),
    (182.5, 147.4), (177.7, 147.3), (172.9, 147.1), (168.1, 146.9), (163.3, 146.8), (158.5, 146.6),
    (153.7, 146.4), (148.9, 146.3), (144.0, 146.1), (139.2, 145.9), (134.4, 145.8), (129.6, 145.6),
    (124.8, 145.3), (120.0, 144.7), (115.5, 143.0), (111.5, 140.4), (107.6, 137.6), (103.5, 135.0),
    (99.4, 132.5), (95.5, 129.7), (91.7, 126.8), (87.9, 123.8), (84.2, 120.7), (80.4, 117.7),
    (76.6, 114.9), (72.7, 112.1), (69.0, 108.9), (66.0, 105.2), (63.1, 101.3), (60.2, 97.5),
    (57.1, 93.8), (54.0, 90.1), (51.1, 86.3), (48.2, 82.5), (45.3, 78.7), (42.3, 74.9),
    (39.4, 71.0), (36.5, 67.2), (34.0, 63.1), (32.4, 58.6), (31.7, 53.8), (31.2, 49.0),
    (30.6, 44.2), (29.7, 39.5), (28.5, 34.9), (26.8, 30.4), (24.9, 25.9), (22.9, 21.5),
    (21.0, 17.2), (19.0, 12.7), (17.3, 8.2), (16.1, 3.6), (15.0, -1.1), (13.8, -5.7),
    (12.4, -10.4), (10.2, -14.5), (6.0, -16.3), (1.2, -15.8), (-3.5, -14.8), (-8.2, -13.8),
    (-12.7, -12.2),
]

SCENERY = dict(
    lava=True,
    lava_mesh="WATER_LAVA",
    lava_material="MAT_LAVA",
    lava_z=0.0,
    crater=dict(cx=172.8, cd=2.6, rim_radius=171.5),
    wall_ring=dict(cx=172.8, cd=2.6, r_inner=295.0, r_outer=355.0, h_min=30.0, h_max=52.0),
    stacks=[
        dict(x=138.3, d=-13.4, r=6.0, h=14.0),
        dict(x=204.8, d=-52.8, r=4.5, h=9.0),
        dict(x=126.8, d=82.3, r=7.5, h=19.0),
        dict(x=269.8, d=58.6, r=5.0, h=11.0),
        dict(x=269.4, d=-23.3, r=6.5, h=16.0),
        dict(x=-58.1, d=51.7, r=7.0, h=21.0),
        dict(x=60.1, d=214.5, r=5.5, h=15.0),
        dict(x=205.1, d=232.4, r=6.0, h=18.0),
        dict(x=338.3, d=141.5, r=4.5, h=12.0),
        dict(x=160.7, d=118.0, r=5.0, h=12.0),
        dict(x=187.3, d=210.1, r=5.5, h=14.0),
    ],
    pillar=dict(x=261.0, d=164.7, r_top=22.8, top_z=6.0, base_inset=2.0),
    tee_box=dict(x=-0.4, d=-2.0, width=14.0, depth=12.0, heading=12.2),
)

# ============================================================================= generator
# Everything above the banner is baked data. The code below re-derives it (nothing here runs at import):
#     python3 hole10_design.py --verify     -> prints OK when the baked literals equal what the helpers produce
#     python3 hole10_design.py --regen      -> prints fresh literals (paste them over the baked block)
# The shore is the 0-level contour of a smooth signed field (marching squares), so it follows the centerline
# polyline exactly like Hole.cs does (round joins), then it is resampled to ~4.8 yd edges and Taubin-smoothed.

_RIM_H0 = math.radians(4.0)                 # rim heading at the tee, from +D toward +X
_RIM_K0, _RIM_K1 = 1 / 270.0, 1 / 125.0     # rim curvature (1/yd) at the tee and at station C: it tightens toward the breach
_RIM_LEN = 204.0                            # arclength tee -> C
_RIM_STATIONS = (0.0, 68.0, 136.0, 204.0)   # tee, A, B, C
_NECK_HEADING = math.radians(88.0)          # the breach C -> pin, a straight chord
_NECK_LEN = 154.0
_BULB_R = 22.8                              # shore radius round the pin (fairway/2 + rough = 24.0 is the out-of-bounds limit)
_NECK_W = 14.5                              # shore half width of the neck and of the stub after the lava
_TEE_W = 14.0                               # shore half width of the tee slab
_W_MAX = 23.3                               # never wider than 24.0 - 0.7 (no out-of-bounds grass)
_STACK_POLAR = [                            # (radius from the crater centre yd, angle deg from +X toward +D, base radius yd, height m)
    (38, 205, 6.0, 14), (64, 300, 4.5, 9), (92, 120, 7.5, 19), (112, 30, 5.0, 11), (100, 345, 6.5, 16),
    (236, 168, 7.0, 21), (240, 118, 5.5, 15), (232, 82, 6.0, 18), (216, 40, 4.5, 12),
    (116, 96, 5.0, 12), (208, 86, 5.5, 14),
]


def _rim_heading(s):
    return _RIM_H0 + _RIM_K0 * s + (_RIM_K1 - _RIM_K0) * s * s / (2 * _RIM_LEN)


def _rim_point(s, step=0.25):
    x = d = 0.0
    for i in range(int(round(s / step))):
        ph = _rim_heading((i + 0.5) * step)
        x += math.sin(ph) * step
        d += math.cos(ph) * step
    return x, d


def _stations():
    pts = [_rim_point(s) for s in _RIM_STATIONS]
    cx, cd = pts[-1]
    pts.append((cx + _NECK_LEN * math.sin(_NECK_HEADING), cd + _NECK_LEN * math.cos(_NECK_HEADING)))
    return [(round(x, 1), round(d, 1)) for x, d in pts]


def _smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def _bump(s, c, r):
    return math.exp(-((s - c) / r) ** 2)


def _half_widths(s):
    """Shore distance from the centerline on the crater side (w_in) and the outer side (w_out) at arclength s."""
    w_in = 19.9 + 1.0 * math.sin(0.055 * s + 0.3) + 0.6 * math.sin(0.12 * s + 1.9) + 3.0 * _bump(s, 128.0, 15.0)
    w_out = 19.4 + 1.0 * math.sin(0.047 * s + 2.2) + 0.7 * math.sin(0.105 * s + 0.4) + 4.4 * _bump(s, 172.0, 15.0)
    w_in, w_out = min(w_in, _W_MAX), min(w_out, _W_MAX)
    k_open = _smooth(s / 45.0)
    k_neck = _smooth((s - 190.0) / 32.0)
    out = []
    for w in (w_in, w_out):
        w = _TEE_W + (w - _TEE_W) * k_open
        out.append(w + (_NECK_W - w) * k_neck)
    return out[0], out[1]


def _polyline_frame(pts):
    segs, acc = [], 0.0
    for i in range(1, len(pts)):
        a, b = pts[i - 1], pts[i]
        ln = math.dist(a, b)
        segs.append((a, b, ln, acc, ((b[0] - a[0]) / ln, (b[1] - a[1]) / ln)))
        acc += ln
    return segs, acc


def _nearest(segs, p):
    """(distance to the centerline, arclength of the nearest point, +1 crater side / -1 outer side); open behind the tee."""
    best = (1e9, 0.0, 1.0)
    for k, (a, b, ln, s0, u) in enumerate(segs):
        t = (p[0] - a[0]) * u[0] + (p[1] - a[1]) * u[1]
        tc = t if (k == 0 and t < 0) else min(max(t, 0.0), ln)
        q = (a[0] + u[0] * tc, a[1] + u[1] * tc)
        dd = math.dist(p, q)
        if dd < best[0]:
            side = (p[0] - q[0]) * u[1] - (p[1] - q[1]) * u[0]
            best = (dd, s0 + tc, 1.0 if side >= 0 else -1.0)
    return best


def _smin(a, b, k):
    h = max(k - abs(a - b), 0.0) / k
    return min(a, b) - h * h * k * 0.25


def _contour(cl):
    segs, _ = _polyline_frame(cl)
    pin = cl[-1]
    u0 = segs[0][4]
    nrm0 = (u0[1], -u0[0])

    def field(p):
        dist, s, side = _nearest(segs, p)
        wi, wo = _half_widths(s)
        w = wi if side > 0 else wo
        if dist > w + 30:
            return dist - w
        g_rib = dist - w
        if s < 0:
            g_rib = max(g_rib, -s)          # the ribbon starts at the tee; the slab covers the 15 yd behind it
        lx = (p[0] * u0[0] + p[1] * u0[1]) + 4.5     # tee slab: rounded box, s in [-15, +6], corner radius 4
        ly = p[0] * nrm0[0] + p[1] * nrm0[1]
        qx, qy = abs(lx) - (10.5 - 4.0), abs(ly) - (_TEE_W - 4.0)
        g_tee = math.hypot(max(qx, 0.0), max(qy, 0.0)) + min(max(qx, qy), 0.0) - 4.0
        g_disc = math.dist(p, pin) - _BULB_R
        return _smin(_smin(g_rib, g_tee, 5.0), g_disc, 5.0)

    h = 2.0
    xs, ds = [p[0] for p in cl], [p[1] for p in cl]
    x0, d0 = min(xs) - 40.0, min(ds) - 40.0
    nx, nd = int((max(xs) + 40.0 - x0) / h) + 1, int((max(ds) + 40.0 - d0) / h) + 1
    val = [[field((x0 + i * h, d0 + j * h)) for j in range(nd)] for i in range(nx)]
    edge_pt, adj = {}, {}

    def link(e1, e2):
        adj.setdefault(e1, []).append(e2)
        adj.setdefault(e2, []).append(e1)

    for i in range(nx - 1):
        for j in range(nd - 1):
            c = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            ins = [val[a][b] < 0 for a, b in c]
            if all(ins) or not any(ins):
                continue
            es = []
            for k in range(4):
                a, b = c[k], c[(k + 1) % 4]
                if ins[k] != ins[(k + 1) % 4]:
                    e = (min(a[0], b[0]), min(a[1], b[1]), 0 if a[1] == b[1] else 1)
                    va, vb = val[a[0]][a[1]], val[b[0]][b[1]]
                    t = va / (va - vb)
                    edge_pt[e] = (x0 + (a[0] + (b[0] - a[0]) * t) * h, d0 + (a[1] + (b[1] - a[1]) * t) * h)
                    es.append(e)
            if len(es) == 2:
                link(es[0], es[1])
            elif (sum(val[a][b] for a, b in c) / 4.0 < 0) == ins[0]:
                link(es[0], es[3]); link(es[1], es[2])
            else:
                link(es[0], es[1]); link(es[2], es[3])
    seen, loops = set(), []
    for e in adj:
        if e in seen:
            continue
        loop, prev, cur = [], None, e
        while cur not in seen:
            seen.add(cur)
            loop.append(edge_pt[cur])
            nxt = [n for n in adj[cur] if n != prev]
            if not nxt:
                break
            prev, cur = cur, nxt[0]
        loops.append(loop)
    return max(loops, key=len)


def _resample(poly, step):
    n = len(poly)
    cum = [0.0]
    for i in range(n):
        cum.append(cum[-1] + math.dist(poly[i], poly[(i + 1) % n]))
    total = cum[-1]
    m = max(12, int(round(total / step)))
    out, k = [], 0
    for q in range(m):
        t = total * q / m
        while cum[k + 1] < t:
            k += 1
        a, b = poly[k], poly[(k + 1) % n]
        f = (t - cum[k]) / max(cum[k + 1] - cum[k], 1e-9)
        out.append((a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f))
    return out


def _taubin(poly, iters=3, lam=0.5, mu=-0.53):
    p = list(poly)
    n = len(p)
    for _ in range(iters):
        for f in (lam, mu):
            p = [(p[i][0] + f * ((p[i - 1][0] + p[(i + 1) % n][0]) / 2 - p[i][0]),
                  p[i][1] + f * ((p[i - 1][1] + p[(i + 1) % n][1]) / 2 - p[i][1])) for i in range(n)]
    return p


def _shore(cl):
    p = _taubin(_resample(_contour(cl), 4.5), 3)
    return [(round(x, 1), round(d, 1)) for x, d in _resample(p, 4.8)]


def _on_line(cl, s, off):
    """Point at arclength s along the centerline, `off` yd toward the crater centre (+) or away from it (-)."""
    segs, _ = _polyline_frame(cl)
    for a, b, ln, s0, u in segs:
        if s <= s0 + ln + 1e-9:
            t = s - s0
            return a[0] + u[0] * t + u[1] * off, a[1] + u[1] * t - u[0] * off


def _hazards(cl):
    c, u = cl[-2], (math.sin(_NECK_HEADING), math.cos(_NECK_HEADING))
    entry, chord, half_len = 8.0, 112.0, 38.0       # the centerline enters the lava 8 yd after C and runs 112 yd through it
    mid = entry + chord / 2.0
    lava = dict(kind="water", x=round(c[0] + u[0] * mid, 1), d=round(c[1] + u[1] * mid, 1), width=112.0,
                length=2 * half_len, tag="lava")

    def bunker(s, off, w, ln, tag):
        x, d = _on_line(cl, s, off)
        return dict(kind="bunker", x=round(x, 1), d=round(d, 1), width=w, length=ln, tag=tag)

    return [lava, bunker(128.0, 13.0, 17.0, 12.0, "rim inside"), bunker(172.0, -13.0, 18.0, 13.0, "rim outside")]


def _fit_circle(pts):
    n = len(pts)
    sx = sum(p[0] for p in pts); sy = sum(p[1] for p in pts)
    sxx = sum(p[0] ** 2 for p in pts); syy = sum(p[1] ** 2 for p in pts); sxy = sum(p[0] * p[1] for p in pts)
    z = [p[0] ** 2 + p[1] ** 2 for p in pts]
    sxz = sum(p[0] * zz for p, zz in zip(pts, z)); syz = sum(p[1] * zz for p, zz in zip(pts, z)); sz = sum(z)
    m = [[sxx, sxy, sx, sxz], [sxy, syy, sy, syz], [sx, sy, n, sz]]
    for i in range(3):
        piv = max(range(i, 3), key=lambda r: abs(m[r][i]))
        m[i], m[piv] = m[piv], m[i]
        for r in range(3):
            if r != i:
                f = m[r][i] / m[i][i]
                m[r] = [a - f * b for a, b in zip(m[r], m[i])]
    a, b, c = (m[i][3] / m[i][i] for i in range(3))
    cx, cd = a / 2, b / 2
    return cx, cd, math.sqrt(c + cx * cx + cd * cd)


def _scenery(cl, shore):
    cx, cd, rr = _fit_circle([_rim_point(float(s)) for s in range(0, int(_RIM_LEN) + 1, 8)])
    far = max(math.dist(p, (cx, cd)) for p in shore)
    r_in = 5.0 * math.ceil((far + 85.0) / 5.0)
    stacks = []
    for r, ang, rad, ht in _STACK_POLAR:
        a = math.radians(ang)
        stacks.append(dict(x=round(cx + r * math.cos(a), 1), d=round(cd + r * math.sin(a), 1), r=rad, h=float(ht)))
    u0 = _polyline_frame(cl)[0][0][4]
    return dict(
        lava=True, lava_mesh="WATER_LAVA", lava_material="MAT_LAVA", lava_z=0.0,
        crater=dict(cx=round(cx, 1), cd=round(cd, 1), rim_radius=round(rr, 1)),
        wall_ring=dict(cx=round(cx, 1), cd=round(cd, 1), r_inner=r_in, r_outer=r_in + 60.0,
                       h_min=30.0, h_max=52.0),
        stacks=stacks,
        pillar=dict(x=cl[-1][0], d=cl[-1][1], r_top=_BULB_R, top_z=PLAY_Z, base_inset=2.0),
        tee_box=dict(x=round(-2.0 * u0[0], 1), d=round(-2.0 * u0[1], 1), width=14.0, depth=12.0,
                     heading=round(math.degrees(math.atan2(u0[0], u0[1])), 1)),
    )


def _generate():
    cl = _stations()
    shore = _shore(cl)
    hz = _hazards(cl)
    return dict(CENTERLINE=cl, SHORE=shore, HAZARDS=hz, SCENERY=_scenery(cl, shore))


def _literal(v, indent=0):
    pad = " " * indent
    if isinstance(v, str):
        return '"%s"' % v
    if isinstance(v, dict):
        if indent == 0 and len(v) > 5:           # the top-level SCENERY dict: one key per line
            return "dict(\n" + "".join(f"{pad}    {k}={_literal(x, indent + 4)},\n" for k, x in v.items()) + pad + ")"
        return "dict(" + ", ".join(f"{k}={_literal(x, indent)}" for k, x in v.items()) + ")"
    if isinstance(v, list) and v and isinstance(v[0], tuple):
        rows = [", ".join(f"({x}, {d})" for x, d in v[i:i + 6]) for i in range(0, len(v), 6)]
        return "[\n" + "".join(f"{pad}    {r},\n" for r in rows) + pad + "]"
    if isinstance(v, list):
        return "[\n" + "".join(f"{pad}    {_literal(x, indent + 4)},\n" for x in v) + pad + "]"
    return repr(v)


if __name__ == "__main__":
    import sys
    fresh = _generate()
    if "--regen" in sys.argv:
        for key in ("CENTERLINE", "HAZARDS", "SHORE", "SCENERY"):
            print(f"{key} = {_literal(fresh[key])}\n")
    else:
        mine = dict(CENTERLINE=CENTERLINE, SHORE=SHORE, HAZARDS=HAZARDS, SCENERY=SCENERY)
        bad = [k for k in mine if mine[k] != fresh[k]]
        print("hole10_design.py baked data matches the generator" if not bad else f"MISMATCH in {bad}: run --regen")
        sys.exit(1 if bad else 0)
