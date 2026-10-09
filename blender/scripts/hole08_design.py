"""Hole 08 NEEDLE: design data in COURSE YARDS (x right of the tee line, d down the hole). Pure python.

Run the gates:  python3 blender/scripts/postcard_check.py hole08_design --map
Plan picture:   python3 blender/scripts/postcard_plan_png.py hole08_design out.png --scale 3
(If the module-name form crashes with UnboundLocalError 'importlib', pass the file name: hole08_design.py.)

TOPOLOGY
    One shore polygon (about 200 points, edges 4 yd, closed, no self-intersection) wraps a chain of three
    dry pads strung along a gently meandering centerline: the TEE PAD, the MIDDLE PAD (landing pad) and the
    GREEN PAD. Between the pads the polygon stays continuous as a thin "neck" (half-width 5.5 yd at the
    thinnest), but each neck lies entirely inside a WATER ELLIPSE, so by Hole.LieAt it is water and the
    builder drops the terrain to the sea there (SCENERY["necks"] lists the shore vertices that sit inside
    the ellipses; the builder may ignore their cliff walls). Removing the two ellipses from the shore leaves
    exactly three dry pads (tee pad 1,745 yd2, middle pad 2,378, green pad 1,635; no slivers).
    PAD WIDTH: the ribbon is 2 * (16/2 + 8) = 32 yd, the shore sits 15.9 yd either side. The cliff-top wobble
    is COMPLEMENTARY: the right inset and the left inset always add up to AMP = 2.0 yd, so the shore weaves
    left and right but a pad is never narrower than 2 * 15.9 - 2.0 = 29.8 yd between its noses (29.5 measured
    across the perpendicular, 30.0 at the landing station M). Measured perpendicular dry width >= 28 yd runs
    for 45 yd on the tee pad, 66 yd on the middle pad (arclength 145 to 210, centred on M) and 41 yd on the
    green pad. (The first version had independent wobbles up to 3.9 yd per side: 26-27 yd at M, a 17 yd
    run >= 28 on the middle pad: rejected, fixed here.)
    PAD NOSES: the shore keeps an 11 yd half-width as it meets each ellipse (it only narrows to the neck
    7 yd INSIDE the ellipse), so the pad corners sit 10.6 to 11.1 yd from the centerline: the whole 8 yd
    fairway half-width is inside the shore at the corners (0.00 yd2 of fairway band outside shore and
    ellipses; the first version lost 0.8 yd2 there). The noses taper from 11 to 15.9 yd over 10-11 yd.
    Shore half-width is at most 15.95 yd (= FAIRWAY_WIDTH/2 + ROUGH_WIDTH - 0.05), so every dry cell
    is Fairway, Rough or Green: out-of-bounds grass is 0.00 %, and a big miss in any direction is water.
    The land strip between the fairway edge and the shore averages 6.6 yd (the brief's rough band is 8;
    it is 7.9 around the tee and green caps). The shore is held at exactly 15.9 beside the bunker.

CENTERLINE (3 stations; RecommendedTarget aims at the next station >= 35 yd away, so there is NO station
    on the tee pad or on the green pad other than the pin)
    tee   (  0.0,   0.0)
    M     ( 19.0, 178.0)   the landing station on the middle pad (the aim assist target of the tee shot)
    pin   ( -6.0, 340.0)
    Length 342.9 yd (brief 320-360). Lateral offsets: M sits 22.1 yd right of the straight tee-pin line,
    segment 1 leans +6.1 deg, segment 2 leans -8.8 deg (a 14.9 deg kink at M inside the middle pad), so
    the chain is an S-curve, not a ruler line.

HAZARDS
    water  "gap 1"   x 10.1  d  90.0  width 48.0  length 94.0   centerline crossing 92.4 yd (s 44.5 to 136.9)
    water  "gap 2"   x  6.6  d 258.0  width 48.0  length 88.0   centerline crossing 85.7 yd (s 217.2 to 302.8)
    bunker "front-left" x -12.0 d 316.0 width 9.0 length 20.0   greenside, 4.9 yd left of the centerline
    Ellipses are axis aligned. Width 48 = the 32 yd ribbon cross-section + 8 yd of sea each side, so the
    ellipse swallows the neck and the tilted pad ends (the centerline leans up to 8.8 deg off the D axis).
    CARRIES. Along the centerline each water crossing is 92 and 86 yd, far under 140 (brief), inside the
    lead's 85-115 yd target. STRICT reading, from the tee: the first dry point beyond gap 1 is 136.9 yd from
    the tee (3.1 under 140); the first version had 141.8 (gap 1 centre d 95), so gap 1 moved 5 yd up the
    hole. From the lay-up edge of the middle pad the second carry is 85.7 yd. Dry fairway short of each
    carry: 44.5 yd on the tee pad, 80.2 yd on the middle pad (>= 40 required), and 40.1 yd after gap 2.
    BUNKER: 15.1 yd from the pin (the green disc is 14), so it does NOT overlap the green and the builder
    needs no sand-over-green cut (the first version sat 10.8 yd from the pin and ate 15.8 yd2 of green).
    It spans D 306 to 326 on the left of the approach line, 4.9 yd from the centerline (it bites 3.1 yd
    into the 8 yd fairway half-width, so it is beside the line, not on it), 1.6 yd inside the shore.

INTENDED PLAY (yards along the centerline; the windows below were measured on the real CourseShot with the
    aim assist heading, no wind, no swing error, using Tools/PostcardCheck)
    Tee shot: carry gap 1 (touchdown beyond D 137) and finish on the middle pad, D 137 to 217, aim M at D 178.
        Driver at power 0.50 (rest D 141) to 0.81 rests dry on the middle pad; 0.82 and up lands in gap 2
        (+1 stroke, drop on the far end of the middle pad). Iron reaches it from power 0.81 to 1.00, the
        wedge never does. Power 0.13 to 0.49 splashes in gap 1 (+1, drop on the tee pad).
    Approach: carry gap 2 (touchdown beyond D 303) onto the green pad, 164 yd from M to the pin. From M the
        Iron is on the green at power 0.89 to 1.00 (0.73 to 0.88 stops short of the green, on the green
        pad's fairway); the Driver at 0.54 to 0.62 is on the green. From the far end of the middle pad
        (D 200-217) it is a 124 to 142 yd shot.
    Lay-up spots: tee pad D 25 to 41 (dry, 44 yd of room before gap 1), middle pad far end D 200-214
        (dry, before gap 2). The green pad has 26 yd of fairway between gap 2 (D 303) and the green disc
        (D 326), a bunker front-left and the green (radius 14, pin (-6, 340)) with the shore 15.9 yd round it.
    A driver at full power from the tee flies about 250 yd and lands in gap 2: the hole punishes it.
    Known limit of the unchanged shot code (CourseShot.Drop walks back toward the origin 1 yd at a time and
    adds 2 yd): on pads this thin, about 0.2 % of the shots that end in water drop back onto a point that is
    water again (the next stroke is then played from the sea, no penalty, no lock). Measured: 124 of
    52,991 water-ending random shots (the first version 111 of 53,613), 0.17 % of water drops in a uniform geometric
    sample for both versions, natural tee shots 0.26 % (first version 0.60 %). A shore with no wobble at all
    still gives 0.27 % in the random-origin test, so it comes from the thin pads, not from the wobble.

FLIGHT HEIGHT IN UNITY (round 3: verifier B's failure, and what the design can and cannot do about it)
    GolfGame.State.Flight puts the ball at HoleView.ToWorld(p, p.h + 0.06) = GroundHeight(p) + h, and GroundHeight is
    the topmost collider under the ball or 0 when the ray misses (HoleView.cs). Inside a water ellipse the terrain is
    cut away, so the ground under a ball that is carrying a gap is the SEA (0), and the ball's drawn height drops by
    PLAY_Z at the near edge and jumps back by PLAY_Z at the far edge (the aim line, which is laid on the ground
    in 24 samples, does the same). The size of that pop IS PLAY_Z, so the design lever is PLAY_Z:
        PLAY_Z 24 m (round 2): 26.2 yd pop; Driver 0.66 tee->M: ball 17.1 yd BELOW the near pad top right after the
        near edge and 16.2 yd inside the far cliff face at the far edge; the other three sampled shots the same.
        PLAY_Z  6 m (round 3): 6.6 yd pop; the same four shots: ball +2.6, +3.5, +5.8 yd ABOVE the near pad top
        and +3.5, +13.6, +12.8, +8.5 yd above the far rim; only the 12 yd-from-the-edge iron (h 3.3 yd at the
        near edge) still dips 3.2 yd below the pad top.
    Why 6 m and not lower: over every good shot that starts at a station (tee, M) the ball is at least 6.8 yd
    above the ground at the near edge (Driver from M 0.46-0.64; Driver from the tee 7.9; Iron from M 10.8), and
    6 m = 6.56 yd, so no shot from a station is ever drawn below the pad it just left. Above 6.2 m that stops
    being true (8 m: 29% of the Driver tee shots dip below the near pad top). Below 6 m the share of far-edge
    intrusions only falls slowly (Driver tee shots 38.7% at 6 m, 29.0% at 3 m) while the pads stop reading as
    sea cliffs. What the design CANNOT remove: a shot that touches down shortly beyond the far edge (up to 18
    yd in the cases measured, mean 2-5 yd) is still drawn below the rim at the edge (share of the shots that
    finish dry on the far pad, 6 m: Driver from the tee 38.7%, Iron 50%, approach Iron from M 37%, 7-iron
    lay-up 28%; at 24 m: 93-100%), and a shot struck from closer than about 25 yd (Iron) or 35 yd (Driver)
    to a near edge is below the pad top as it crosses that edge (ball 3-6 yd up at 12-20 yd back). Removing
    that fully needs a Unity-side change (HoleView.GroundHeight keeping the launch pad's play height while the
    ball is airborne over a cut-away), which is outside the brief's one-line GolfGame edit and is the lead's
    call. (Measured on the real CourseShot with a scratch harness; calm air, no swing error.)

SCENERY KEYS (read only by hole08_build.py; yards in course coordinates unless the key ends in _m; angles
    in degrees measured in plan from +D (down the hole) towards +X)
    play_z_m        play height above the sea in metres (= PLAY_Z = 6.0), sea level is z = 0; play_z_yd is the
                    same in yards, for the scenery heights below (all other heights here are above the SEA)
    arch            the ruined arch, visual only, copy an existing ROCK_ mesh, NO collision, no new type:
                    x, d position of the arch centre; facing_deg direction its front faces (171.2 = faces
                    the tee, along the approach, so the opening frames the pin as in needle.jpg);
                    span_yd, height_yd, thickness_yd overall size; base_radius_yd + base_top_m: a rock
                    outcrop under it. Its circle (radius 7 yd) just touches the green pad's left cliff
                    (centre 6.9 yd from the shore) beside the bunker, and its top is 1 m below PLAY_Z (5 m).
                    The arch itself stands ~11 m above the outcrop, a landmark above the play surface (it is
                    not terrain and has no collider).
    stacks          list of dict(x, d, radius_yd, height_yd, seed): 9 sea stacks in open sea, their edges
                    9.9 to 33.7 yd off the shore, all outside the water ellipses, one instanced rock each
                    (seed = tilt/scale variation); the ones beyond the green are the tall backdrop.
                    height_yd is the stack's top above the sea: 12 to 30 yd (11 to 27 m), every stack taller than
                    the pads (PLAY_Z 6 m), so they read as needles as in needle.jpg. READING OF THE BRIEF: "cliffs,
                    lava and the ocean sit below the play height as scenery" is applied to everything the ball can
                    touch or the terrain is made of (pad cliffs and walls, all at or below PLAY_Z); the stacks are
                    free-standing instanced rocks (ROCK_*, no collider, outside the shore, 25+ yd from the
                    centerline). If the lead wants the literal reading, clamp height_yd to play_z_yd (6.6).
    necks           list of (first, last) SHORE vertex indices (inclusive) lying inside a water ellipse:
                    water by the code, no cliff wall needed there (the terrain is cut by the ellipse)
    pads            list of dict(name, outline): the exact dry outline of each pad (shore arcs outside the
                    ellipses + ellipse arcs), closed, no repeated first point, in yards, 0.1 rounded:
                    build TERRAIN_* from these (flat at PLAY_Z), walls drop to the sea. A few outline edges
                    are short (down to 0.9 yd where a shore vertex sits next to an ellipse crossing): weld
                    points closer than 1 yd when triangulating.
    lay_up          list of dict(name, x, d): the lay-up / landing spots described above
    pad_s           (start, end) arclength along the centerline of each dry pad
    notes           short text for the builder

WHY THESE NUMBERS (not in the brief)
    PLAY_Z 6 m (was 24): the Unity ball is drawn at GroundHeight + h and GroundHeight is 0 over the cut-away
        water ellipses, so every carry pops the ball by PLAY_Z at both edges (see FLIGHT HEIGHT above). 6 m is
        the highest sea cliff for which no shot from a station (tee, M) is drawn below the pad it just left,
        and it still reads as a stepping-stone pad over the sea. One flat height, no slope anywhere.
    Shore 15.9 yd: the ribbon half-width is 8 + 8 = 16 yd; staying 0.1 inside makes the whole pad playable
        and the out-of-bounds count zero. ROUGH_WIDTH stays at the brief's 8.
    AMP 2.0, complementary: keeps a living cliff edge (the shore swings 2 yd) while every pad stays
        >= 29.8 yd wide; larger or independent wobble narrowed the landing zone below 28 yd.
    Station M 178 yd: a mid-iron/driver-lite tee shot, and 164 yd left for a 7-iron approach (160 yd club).
    Gap 1 centre d 90: puts the first dry point beyond gap 1 at 136.9 yd from the tee (under 140).
    Carries 92 and 86 yd: well under the 140 limit and inside the lead's 85-115 yd target.
    Bunker 9 x 20 at (-12.0, 316.0): the pad is only 32 yd wide, so the bunker is a long pot strip on the
        left of the approach that leaves the right of the green open; it keeps 1.6 yd from the shore and
        15.1 yd from the pin (the code only needs >= 1 and >= 8.4), clear of the green disc.
"""
import math

NUMBER = 8
NAME = "Needle"
PAR = 4
PLAY_Z = 6.0

CENTERLINE = [(0.0, 0.0), (19.0, 178.0), (-6.0, 340.0)]
FAIRWAY_WIDTH = 16.0
GREEN_RADIUS = 14.0
ROUGH_WIDTH = 8.0

HAZARDS = [
    dict(kind="water", x=10.1, d=90.0, width=48.0, length=94.0, tag="gap 1"),
    dict(kind="water", x=6.6, d=258.0, width=48.0, length=88.0, tag="gap 2"),
    dict(kind="bunker", x=-12.0, d=316.0, width=9.0, length=20.0, tag="front-left"),
]

LIMITS = dict(number=8, length_min=320, length_max=360, par=4, carry_max=140.0, water=2, bunker=1, forced=True,
              pads=3, min_pad_area=500.0)

# ----------------------------------------------------------------------------- shore generator
FULL = 15.9          # shore half-width at full pad width
TAPER = {1: 10.0, -1: 11.0}      # nose taper length per side (side +1 = right of travel, -1 = left)
NOSE = {1: 11.0, -1: 11.0}       # shore half-width where the centerline meets the water ellipse
NECK = 5.5           # half-width of the neck inside an ellipse
NECKHOLD = 7.0       # the shore keeps the NOSE half-width this far into the ellipse (the pad corners sit ~4.5 yd inside it, so
                     # they stay >= 10.6 yd from the centerline and the 8 yd fairway band is never clipped)
NECKRUN = 9.0
AMP = 2.0            # the two side insets always add up to AMP: a pad is never narrower than 2 * FULL - AMP = 29.8 yd
STEP = 4.0           # shore vertex spacing along the polygon


def _ss(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def _frame(c):
    segs, cum = [], [0.0]
    for i in range(1, len(c)):
        dx, dd = c[i][0] - c[i - 1][0], c[i][1] - c[i - 1][1]
        length = math.hypot(dx, dd)
        segs.append((c[i - 1], (dx / length, dd / length)))
        cum.append(cum[-1] + length)
    return segs, cum


_SEGS, _CUM = _frame(CENTERLINE)
TOTAL = _CUM[-1]


def _at(s):
    s = min(max(s, 0.0), TOTAL)
    k = 0
    while k < len(_SEGS) - 1 and s > _CUM[k + 1]:
        k += 1
    a, t = _SEGS[k]
    return (a[0] + t[0] * (s - _CUM[k]), a[1] + t[1] * (s - _CUM[k])), t


def _normal_at(s):
    """point, tangent, right-hand normal; the tangent is blended across the kink so the offsets stay smooth."""
    p, t = _at(s)
    for k in range(1, len(_SEGS)):
        ks = _CUM[k]
        if abs(s - ks) < 7.0:
            t0, t1 = _SEGS[k - 1][1], _SEGS[k][1]
            f = (s - (ks - 7.0)) / 14.0
            tx, td = t0[0] * (1 - f) + t1[0] * f, t0[1] * (1 - f) + t1[1] * f
            n = math.hypot(tx, td)
            t = (tx / n, td / n)
    return p, t, (t[1], -t[0])


def _inside(h, p):
    return ((p[0] - h["x"]) / (h["width"] / 2)) ** 2 + ((p[1] - h["d"]) / (h["length"] / 2)) ** 2 <= 1


def _chords():
    out = []
    for h in HAZARDS:
        if h["kind"] != "water":
            continue
        a = b = None
        for i in range(int(TOTAL / 0.05) + 1):
            s = i * 0.05
            if _inside(h, _at(s)[0]):
                if a is None:
                    a = s
                b = s
        out.append((a, b))
    return sorted(out)


CHORDS = _chords()      # arclength interval of each water crossing on the centerline


def _u(s):
    """signed distance into the dry pad from the nearest water chord end (negative inside a gap)."""
    for a, b in CHORDS:
        if a <= s <= b:
            return -min(s - a, b - s)
    return min((a - s) if s < a else (s - b) for a, b in CHORDS)


def _wob(s, side):
    """Inset of the shore from FULL, 0..AMP. The two sides are complementary (right + left = AMP everywhere),
    so the cliff top weaves left and right but the pad keeps a constant width."""
    f = 0.78 * math.sin(2 * math.pi * (s / 43.0 + 0.12)) + 0.22 * math.sin(2 * math.pi * (s / 19.0 + 0.40))
    return AMP * (0.5 + 0.5 * side * f)


def _half_width(s, side):
    u = _u(s)
    nose = NOSE[side]
    if u >= 0:
        w = nose + (FULL - nose) * _ss(u / TAPER[side])
        win = _ss(u / 14.0) * _ss((TOTAL - 16.0 - s) / 8.0) * _ss((s - 10.0) / 8.0)
        if side < 0 and s > 288.0:
            win *= 1.0 - _ss((s - 288.0) / 6.0)      # left shore stays full width beside the bunker
        return w - _wob(s, side) * win
    return NECK + (nose - NECK) * _ss((u + NECKHOLD + NECKRUN) / NECKRUN)


def _resample(poly, step):
    n = len(poly)
    cum = [0.0]
    for i in range(n):
        cum.append(cum[-1] + math.dist(poly[i], poly[(i + 1) % n]))
    per = cum[-1]
    k = max(12, int(round(per / step)))
    out, j = [], 0
    for i in range(k):
        target = per * i / k
        while cum[j + 1] < target:
            j += 1
        f = (target - cum[j]) / max(cum[j + 1] - cum[j], 1e-9)
        a, b = poly[j], poly[(j + 1) % n]
        out.append((a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f))
    return out


def _shore():
    n = int(TOTAL / 0.5)
    right, left = [], []
    for i in range(n + 1):
        s = i * TOTAL / n
        p, t, nrm = _normal_at(s)
        wr, wl = _half_width(s, 1), _half_width(s, -1)
        right.append((p[0] + nrm[0] * wr, p[1] + nrm[1] * wr))
        left.append((p[0] - nrm[0] * wl, p[1] - nrm[1] * wl))
    poly = list(right)
    m = 40
    p, t, nrm = _normal_at(TOTAL)                      # green cap, radius FULL around the pin
    a0 = math.atan2(nrm[1], nrm[0])
    for i in range(1, m):
        poly.append((p[0] + FULL * math.cos(a0 + math.pi * i / m), p[1] + FULL * math.sin(a0 + math.pi * i / m)))
    poly += left[::-1]
    p, t, nrm = _normal_at(0.0)                        # tee cap, 15.9 yd behind the tee
    a0 = math.atan2(-nrm[1], -nrm[0])
    for i in range(1, m):
        poly.append((p[0] + FULL * math.cos(a0 + math.pi * i / m), p[1] + FULL * math.sin(a0 + math.pi * i / m)))
    return [(round(x, 1), round(y, 1)) for x, y in _resample(poly, STEP)]


SHORE = _shore()

# ----------------------------------------------------------------------------- derived scenery data
_WATER = [h for h in HAZARDS if h["kind"] == "water"]


def _any_water(p):
    return any(_inside(h, p) for h in _WATER)


def _necks():
    n = len(SHORE)
    ins = [_any_water(p) for p in SHORE]
    runs, i = [], 0
    while i < n:
        if ins[i]:
            j = i
            while j + 1 < n and ins[j + 1]:
                j += 1
            runs.append((i, j))
            i = j + 1
        else:
            i += 1
    return runs


def _ell_hits(a, b, h):
    cx, cd, ax, bd = h["x"], h["d"], h["width"] / 2, h["length"] / 2
    dx, dd = b[0] - a[0], b[1] - a[1]
    ox, od = (a[0] - cx) / ax, (a[1] - cd) / bd
    ex, ed = dx / ax, dd / bd
    qa, qb, qc = ex * ex + ed * ed, 2 * (ox * ex + od * ed), ox * ox + od * od - 1
    disc = qb * qb - 4 * qa * qc
    if disc <= 0:
        return []
    sq = math.sqrt(disc)
    return sorted(t for t in ((-qb - sq) / (2 * qa), (-qb + sq) / (2 * qa)) if 0 < t < 1)


def _pads():
    """Dry outline of every pad: the shore runs outside the water ellipses closed by ellipse arcs."""
    n = len(SHORE)
    ring = []            # ('v', p) or ('x', p, ellipse index, entering)
    for i in range(n):
        a, b = SHORE[i], SHORE[(i + 1) % n]
        ring.append(("v", a))
        ev = []
        for k, h in enumerate(_WATER):
            for t in _ell_hits(a, b, h):
                ev.append((t, k))
        for t, k in sorted(ev):
            q = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
            mid = (a[0] + (b[0] - a[0]) * min(1.0, t + 1e-3), a[1] + (b[1] - a[1]) * min(1.0, t + 1e-3))
            ring.append(("x", q, k, _inside(_WATER[k], mid)))
    first = next(i for i, e in enumerate(ring) if e[0] == "x" and not e[3])      # an exit crossing
    ring = ring[first:] + ring[:first]
    runs, cur = [], None
    for e in ring:
        if e[0] == "x" and not e[3]:
            cur = [e]
        elif cur is not None:
            cur.append(e)
            if e[0] == "x" and e[3]:
                runs.append(cur)
                cur = None

    def side(e):
        return -1 if e[1][1] < _WATER[e[2]]["d"] else 1       # -1 upper (tee side), +1 lower

    def arc(e0, e1):
        h = _WATER[e0[2]]
        cx, cd, ax, bd = h["x"], h["d"], h["width"] / 2, h["length"] / 2
        th0 = math.atan2((e0[1][1] - cd) / bd, (e0[1][0] - cx) / ax)
        th1 = math.atan2((e1[1][1] - cd) / bd, (e1[1][0] - cx) / ax)
        r = (ax + bd) / 2
        k = max(2, int(abs(th1 - th0) * r / 3.5))
        return [(cx + ax * math.cos(th0 + (th1 - th0) * j / k), cd + bd * math.sin(th0 + (th1 - th0) * j / k)) for j in range(1, k)]

    used, pads = set(), []
    for i0 in range(len(runs)):
        if i0 in used:
            continue
        outline, i = [], i0
        while i not in used:
            used.add(i)
            r = runs[i]
            outline += [e[1] for e in r]
            end = r[-1]
            # the partner crossing: the other exit crossing on the same ellipse and side
            j = next(j for j, q in enumerate(runs) if q[0][2] == end[2] and side(q[0]) == side(end) and q[0][1] != end[1])
            outline += arc(end, runs[j][0])
            i = j
        pads.append(outline)
    pads.sort(key=lambda o: sum(p[1] for p in o) / len(o))
    return pads


def _round(pts):
    return [(round(x, 1), round(y, 1)) for x, y in pts]


_NAMES = ["tee pad", "middle pad", "green pad"]
_PADS = _pads()

SCENERY = dict(
    play_z_m=PLAY_Z,
    play_z_yd=round(PLAY_Z * 1.0936133, 1),
    arch=dict(x=-25.5, d=317.0, facing_deg=171.2, span_yd=8.0, height_yd=12.0, thickness_yd=3.0,
              base_radius_yd=7.0, base_top_m=PLAY_Z - 1.0, beside="bunker front-left"),
    stacks=[
        dict(x=-22.0, d=70.0, radius_yd=5.0, height_yd=16.0, seed=1),
        dict(x=44.0, d=112.0, radius_yd=4.5, height_yd=21.0, seed=2),
        dict(x=-14.0, d=160.0, radius_yd=6.0, height_yd=15.0, seed=3),
        dict(x=49.0, d=190.0, radius_yd=7.0, height_yd=24.0, seed=4),
        dict(x=-24.0, d=236.0, radius_yd=4.0, height_yd=12.0, seed=5),
        dict(x=40.0, d=264.0, radius_yd=5.0, height_yd=19.0, seed=6),
        dict(x=-40.0, d=345.0, radius_yd=8.0, height_yd=28.0, seed=7),
        dict(x=30.0, d=362.0, radius_yd=5.0, height_yd=16.0, seed=8),
        dict(x=-14.0, d=398.0, radius_yd=9.0, height_yd=30.0, seed=9),
    ],
    necks=_necks(),
    pads=[dict(name=_NAMES[i], outline=_round(o)) for i, o in enumerate(_PADS)],
    lay_up=[dict(name="tee pad lay-up", x=2.0, d=38.0), dict(name="middle pad landing station", x=19.0, d=178.0),
            dict(name="middle pad far end", x=14.0, d=205.0), dict(name="green pad approach", x=2.0, d=312.0)],
    pad_s=[(-FULL, round(CHORDS[0][0], 1)), (round(CHORDS[0][1], 1), round(CHORDS[1][0], 1)),
           (round(CHORDS[1][1], 1), round(TOTAL + FULL, 1))],
    notes="three flat pads at PLAY_Z (6 m above the sea, so the pad cliffs are 6 m walls); terrain dropped to the sea "
          "inside both water ellipses; arch/stacks are visual only, no collision, taller than the pads; "
          "water is the whole sea outside the pad outlines",
)
