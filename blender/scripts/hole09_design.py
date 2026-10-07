"""Hole 9 "Split" - par 5, 494.1 yd. Design data in COURSE YARDS (X right of the tee line, D down the hole).

PURE PYTHON (no bpy / mathutils / numpy). Everything below the helpers is generated deterministically at import time
(< 0.1 s). Every number in CENTERLINE / SHORE / HAZARDS / SCENERY is rounded to 0.1 yd. Gate it with
    python3 blender/scripts/postcard_check.py /abs/path/blender/scripts/hole09_design.py --map
(use the path form: the bare module-name form `hole09_design` currently dies inside postcard_check.py with an
UnboundLocalError on `importlib`, a bug in the checker, not in this module).

REVISION 2 (two verifiers rejected revision 1; everything not listed here is unchanged)
    1. WATER INSIDE THE TEE ROLL-OUT. Revision 1 had the first water at D 276.4, but a calm full Driver on the default aim
       rests at D 269 and rolls on with a tailwind: 21 % of Wind.Random holes put a full tee shot into the water on
       DISTANCE alone (about 20 %; 31.6 % wet in all). The gap moved 24 yd south (ellipse centre D 328 -> 352) and the green end 14 yd
       south (pin D 478 -> 492, centerline 480.6 -> 494.1 yd, brief 460-500). The first water is now at D 301.0 on the axis
       (300.7 along the centerline) and the longest unobstructed full-Driver rest over every wind (0-20 mph, any direction,
       tee headings -2..8) is D 298.9, so distance alone wets 0.0 % of 20,000 Wind.Random holes.
    2. RUIN ON THE DRIVE LANDING BAND. It sat at D 260-291, where a full drive aimed along the ridge rests (D 257-266,
       x -47..-74; heading -14 stopped inside a 4.5 m wall). It is now at the quiet north end of the ridge, D 93-121, east
       half, 7+ yd inside the cliff; nothing of it lies in x -75..-35, D 225-275. Of 855 swept ridge tee shots none rests
       within 6 yd of it from power 0.55 up (only 0.50 drives, ~125 yd, do) and none rolls through it from power 0.65 up; of
       18,506 swept second/third ridge shots one rests within 6 yd (5.9 yd) and none passes through it. The tall wall (4.5 m)
       and the arch are at its SOUTH end, so a ball north of it, looking down the hole, is not hidden.
    3. THE RIDGE IS NOW CLEARLY SLOWER. Revision 1's prose "reachable in three" was wrong: the real CourseShot reached the green
       in two on either route (35 % vs 67 % of on-route tee outcomes at pin D 478). With the pin at D 492 a two-stroke
       finish exists for 4 of 37 dry ridge tee outcomes (11 %, all tee power 1.00 plus a full second Driver) against 26 of 48
       (54 %) on the ribbon, where a 0.80-0.90 tee shot is enough. With every touchdown, rest and roll >= 29-33 yd off the
       centerline (the real ridge, not the ribbon's rough) the planner needs 3 strokes: Driver, Driver, Wedge.
    4. THE RIBBON NOW RUNS ALONG THE DEFAULT AIM LINE. Revision 1 bowed right to x 17, but the aim assist sends the tee shot
       straight at station 1, so a calm full drive rested at x 22.4, the east edge of the fairway, 3 deg from the east
       cliff. Station 1 is now (3.0, 60) = heading 2.86 deg and the axis is straight along that line to D 235, then swings
       left to the far landing station; the default drive rests at (13.5, 269) on the fairway and the default line threads
       the middle of the pinch bunkers (x 9.2-10.0 at D 184-200; the gap between them is x 3.1-18.2).
    5. SCENERY widths, tips, tee yaw and the green bulb are computed from the final polygon instead of typed in.

TOPOLOGY (in words)
    The shore is ONE simple polygon, so the "split" is a C-shaped RING island whose only opening is the one water
    ellipse. Going round: a broad tee end (cliffs 34 yd behind the tee) -> the narrow RIGHT RIBBON (41-45 yd of land: 18 yd
    of fairway and about 12 yd of rough each side) runs down to D 301 and stops on a cliff face -> a 102 yd gap of sea (the
    water ellipse "channel_gap") -> the ribbon resumes at D 404 and runs on to the green end. The big LEFT RIDGE (30-42 yd
    wide, rugged outer cliffs) runs the whole length on the left and is joined to the ribbon at BOTH ends (a tee bridge
    D -34..34, and a green block D 460..528). Between ridge and ribbon is a sea channel 28-36 yd wide that is OUTSIDE the
    polygon (water, "lagoon") and is open to the ocean through the gap in the ribbon only.
    So: one island (one 4-connected dry component), one pin, one water ellipse, and the ridge is a fully dry,
    bunker-free route from tee to green. There is no second green and no second fairway: the centerline follows the
    ribbon only; the ridge is simply the rough band (ROUGH_WIDTH is big enough that every dry cell is Fairway/Rough).
    The channel's inner walls (ridge east wall, ribbon west wall) are the cliffs the builder sees across the lagoon. The
    two ribbon end faces at the gap follow the water ellipse (offset 1.0 yd to the land side, corners filleted ~9 yd), so
    the terrain cut for the ellipse and the shore agree: NO land lies inside the ellipse (0 of 107,657 cells on a 0.25 yd
    grid, and no shore vertex lies inside it). The ellipse is therefore redundant for scoring (the shore already cuts the
    ribbon 1 yd outside it); it is kept so the Hole.cs hazard list, the terrain cut and the brief's "one water ellipse" agree.

THE CENTERLINE (stations are the aim-assist targets, all dry fairway)
    (0,0) tee -> (3,60) -> (6.3,125) -> (9.5,190) -> (11.8,235) -> [water on D 300.7..404.1, carry 103.7 yd]
    -> (6,416) -> (-1,454) -> (-10,492) pin. Straight down the tee line (heading 2.9 deg) to D 235, then a gentle swing left
    to the green, which sits where ribbon and ridge meet. Legs 60.1, 65.1, 65.1, 45.1, 181.1 (crosses the water), 38.6,
    39.1 yd.

INTENDED PLAY (D in yards; clubs per GolfClub.ReferenceDistanceYards: Driver 250, 7-iron 160, wedge 90). All numbers measured
on the REAL CourseShot / Hole.cs through Tools/PostcardCheck (calm wind unless noted); the game has only Driver, Iron, Wedge.
    Ribbon route (risk/reward, the par 5 is reachable in two):
      Tee shot: Driver at the default aim rests at (13.5, 269), 32 yd short of the water; a 0.80 drive rests near D 218, level
        with the pinch bunkers (D 172-212, 15.1-16.2 yd of open fairway between them, the default line runs down the middle;
        a drive pulled or pushed 6+ yd finds one). Cross-wind is the remaining tee risk (about 1.9 yd of drift per mph at full
        power; the land is 41 yd wide and the yellow HUD ring shows the wind): a full-power drive on the DEFAULT aim, ignoring
        the wind, is wet in 14.6 % of random-wind holes (8.6 % east cliff, 6.0 % west channel, 0.0 % distance), 11.3 % at power
        0.95, 8.4 % at 0.90, 5.6 % at 0.85, 3.2 % at 0.80.
      Second shot: the water must be flown so that the ball is already past the ellipse when the roll starts (the flight is not
        water-checked, the roll is). From the tee rest (13.5, 269) on the auto aim (to (6,416)): Driver 0.50-0.675 clears and
        rests dry, 0.70-0.725 is the front bunker, 0.80-0.85 is the green, 0.975-1.00 flies the island (south sea); 7-iron
        0.80-1.00 clears (rest D 405-437), below that is water. From D 235 the Driver clears from 0.625; the 7-iron's
        full 160 yd rests at D 402, short. Short or pulled = water, +1 stroke, dropped 2-3 yd short of the water on the ribbon.
      Approach: wedge from D ~420-450 to the pin at D 492; the front bunker (centre (9,466), 16 x 16, 7.2 yd short of the green
        disc, 4.5 yd off the approach line) guards the right of the approach, the left stays open.
    Ridge route (safe, slower): aim the tee shot 10-16 degrees left of the ribbon across the dry tee bridge. A full drive at
      heading -16..-10 rests on the ridge at D 258-265, x -74..-47 (heading -18 and lower is the west ocean, -8 and higher
      the channel); power 0.8 (heading -18..-12) rests at D 207-213. The whole ridge is Rough, so every shot from it is at 0.85
      power (about 0.72 of the distance). No hazard is on the ridge route; its widest bunker-free, water-free route has
      half-width 15 yd. A ball over the east cliff of the ridge lands in the channel (water, +1): do not hug the inside edge.
    Aim assist (Hole.RecommendedTarget): it only knows the centerline, so from a ball on the ridge it aims at the next ribbon
      station across the channel. That is the brief's "centerline follows the ribbon only"; the ridge player has to aim by
      hand (down the ridge, not at the suggested target).
    Engine edge (not fixable in the design): Hole.Drop can return a point that is still wet at a shore corner; 19 of 150,000
      random shots (0.013 %) end with NextPosition in water, all at the ribbon end faces or the west shore (Cliffside: 0.011 %).

SCENERY (read only by hole09_build.py; yards in course space unless noted; angles in degrees; heights in metres)
    units               text reminder
    sea_level_m         0.0 (water plane); cliff_top_m = PLAY_Z (every shore stretch drops vertically from the play height)
    shore_stretches     [dict(name, first, last, kind)] inclusive index spans into SHORE (no wrap-around), in polygon order;
                        kind in {"outer_cliff", "channel_inner_wall", "gap_face", "channel_tip"}. The CHANNEL INNER WALL is
                        channel_wall_ridge (the ridge's east cliff) + channel_wall_ribbon_north/_south (the ribbon's west cliff)
                        + the two channel_tip_* arcs; the gap faces are gap_north_end / gap_south_end.
    channel             dict(inner_wall_stretches=[names], width_yd=[min, max], mouth_hazard="channel_gap",
                        tips=[dict(x, d, radius_yd, end="north"|"south")])
    gap                 dict(x, d, width_yd, length_yd, north_face, south_face, land_in_ellipse=False, face_offset_yd)
    ridge               dict(spine=[(x, d), ...] centre of the ridge land every 40 yd, width_yd=[min, max],
                        route=[(x, d), ...] the dry safe route tee -> pin: keep rocks >= 4 yd off it)
    ruin                dict(rock_mesh, walls=[dict(p0, p1, height_m, thick_yd)], fallen_arch=dict(x, d, yaw_deg, span_yd,
                        height_m), rubble=[dict(x, d, radius_yd)]): on the EAST half of the ridge at the quiet north end,
                        D 93-121, x -68..-57, 7+ yd inside the cliff; visual only, no collider. The tall wall (4.5 m) and the
                        arch are at its south end. yaw 0 = the arch's span runs along +D, positive = clockwise seen from above
    sea_stacks          [dict(x, d, radius_yd, height_m)] rock stacks standing in the sea: 7 outside the shore (>= 15 yd off it)
                        and one inside the channel mouth ellipse (visual only; no collider). BUILD-STAGE MOVE (SCENERY only, no
                        scoring number): the mouth stack was at (-26, 362); an 8 m stack there stood in the ground-level sight
                        lines from the ridge to the far landing station (6, 416) (aim-assist target for a ridge player), so it
                        now stands on the east side of the mouth at (34, 372), 24 yd off the carry line and every sight line
    tee_box             dict(x, d, yaw_deg, width_yd, length_yd); yaw = heading of the first centerline leg
    green_bulb          dict(x, d, radius_yd, apron_radius_yd); pin = centerline end; the whole bulb sits at PLAY_Z
    surf_stretches      names of the shore stretches that get foam/surf

RATIONALE for every number that is not in the brief
    PLAY_Z 6.0 m         FLIGHT HEIGHT: Unity draws the ball at GroundHeight(p) + flight height and GroundHeight is 0 over the cut-away
                         water (no collider), so every carry pops the ball by PLAY_Z at both edges of the water. Hole 8's designer measured it
                         (6 m is the highest PLAY_Z for which no shot struck from a station is drawn below the pad it left); the lead applied
                         the same 6 m here (was 24). Only scenery heights moved; no scoring number changed.
    GREEN_RADIUS 17.0    lead range 16-18; a wedge from 90 yd can hold a disc this size. The pin is 33.8 yd from the nearest
                         shore, so the disc edge keeps 17 yd of land all round (gate wants 1.5).
    FAIRWAY 18 / land    the brief says ~18 yd. Ribbon land is 41-45 yd wide on the axis (21 each side, +-0.9 yd shore wobble) so the
    21 + 21              rough is ~12 yd to each shore (lead: about 10): bunker ellipses keep >= 3.6 yd margin (gate: 1.0).
    ROUGH_WIDTH 90.0     the ridge's outer cliff reaches 97.2 yd from the centerline (at D 402); fairway/2 + rough = 99 gives
                         0.00 % out-of-bounds land (0 of 693,873 land cells at 0.25 yd; ROUGH_WIDTH 82 leaves 1.3 % OOB grass
                         on the ridge and fails the 1 % gate, 85 leaves 0.24 %).
    ridge 30-42 yd,      lead ranges 28-45 and 25-40, measured on the final polygon along X. The channel is ~28 yd near the
    channel 28-36 yd     tips and ~36 yd at the gap bay so it opens like a bay toward the ocean.
    water ellipse        centre (8.8, 352.0), 84 x 102. Chord on the centerline 103.7 yd (limit 140, lead 100-125), starts at
                         D 300.7: 300.7 yd of dry fairway before it (limit 40) and 90.0 yd after it (limit 30). The centre was
                         chosen so the first water is beyond the longest possible tee roll-out (D 298.9); it is 84 wide so its
                         arcs are shallow where they meet the 42 yd ribbon (the ends of the ribbon are shallow concave cliff faces,
                         not pointed notches). Its west tip (x -33) stays 14 yd off the ridge east wall, so the ridge loses
                         nothing to it.
    bunkers              pinch pair 11 x 24: left (-2.5, 184.0), right (21.8, 200.0), staggered 16 yd along D so the line is not
                         a straight corridor; each intrudes 2.8 yd into the 18 yd fairway, 15.3 yd between the ellipses (the
                         centerline passes 6.2 / 6.2 yd from the two bunker edges); both end by D 212, 89 yd short of
                         the water, so the 25 yd clean zone before it is untouched. Front bunker 16 x 16 at (9.0, 466.0):
                         7.2 yd short of the green disc and 4.5 yd off the approach line (no station or segment crosses it).
    stations             8 stations; legs 60.1, 65.1, 65.1, 45.1, 181.1 (crosses the water: from the tee rest the aim assist
                         aims at the far landing station (6,416)), 38.6, 39.1 yd. Total 494.1 yd (brief 460-500).
    channel tips         radius 16.4 (north, centre (-34.2, 50.0)) and 17.0 (south, centre (-37.6, 450.0)) semicircles: no spike
                         or sliver narrower than 6 yd anywhere (3 yd morphological opening on a 1 yd grid loses no land or sea
                         cluster of 6+ cells).
    shore sampling       control outline -> Chaikin x2 -> resampled to 5.0 yd arc length: 415 points, edges 4.7-5.1 yd.
"""
import math

NUMBER = 9
NAME = "Split"
PAR = 5
PLAY_Z = 6.0                        # metres above the sea: the ONE play height (6 m, see FLIGHT HEIGHT in the docstring)
FAIRWAY_WIDTH = 18.0
GREEN_RADIUS = 17.0
ROUGH_WIDTH = 90.0

CENTERLINE = [(0.0, 0.0), (3.0, 60.0), (6.3, 125.0), (9.5, 190.0), (11.8, 235.0),
              (6.0, 416.0), (-1.0, 454.0), (-10.0, 492.0)]

# ---------------------------------------------------------------- helpers (deterministic, import-time cheap)
_AX = [(-60.0, 0.0), (0.0, 0.0), (60.0, 3.0), (125.0, 6.3), (190.0, 9.5), (235.0, 11.8), (301.0, 10.9),
       (403.0, 6.5), (416.0, 6.0), (454.0, -1.0), (492.0, -10.0), (554.0, -10.0)]


def _hermite(knots, d):
    """C1 cubic Hermite through (d, v) knots with Catmull-Rom slopes."""
    n = len(knots)
    if d <= knots[0][0]:
        return knots[0][1]
    if d >= knots[-1][0]:
        return knots[-1][1]
    for i in range(n - 1):
        d0, v0 = knots[i]
        d1, v1 = knots[i + 1]
        if d <= d1:
            m0 = (knots[i + 1][1] - knots[max(i - 1, 0)][1]) / (knots[i + 1][0] - knots[max(i - 1, 0)][0])
            m1 = (knots[min(i + 2, n - 1)][1] - knots[i][1]) / (knots[min(i + 2, n - 1)][0] - knots[i][0])
            h = d1 - d0
            t = (d - d0) / h
            t2, t3 = t * t, t * t * t
            return ((2 * t3 - 3 * t2 + 1) * v0 + (t3 - 2 * t2 + t) * h * m0
                    + (-2 * t3 + 3 * t2) * v1 + (t3 - t2) * h * m1)
    return knots[-1][1]


def cxr(d):
    """x of the right-ribbon axis at depth d."""
    return _hermite(_AX, d)


HW_W, HW_E = 21.0, 21.0


def ribbon_w(d):
    return cxr(d) - HW_W + 0.9 * math.sin(d / 31.0 + 0.5)


def ribbon_e(d):
    return cxr(d) + HW_E + 0.9 * math.sin(d / 27.0 + 1.0)


def wch(d):
    """channel width between ribbon west wall and ridge east wall."""
    return 30.0 + 3.0 * math.sin(d / 61.0 + 0.4) + 5.5 * math.exp(-((d - 352.0) / 75.0) ** 2)


def xe(d):
    """ridge east wall (channel inner wall)."""
    return ribbon_w(d) - wch(d)


def rw(d):
    """ridge width."""
    return 37.0 + 3.0 * math.sin(d / 52.0 + 0.9) + 2.0 * math.sin(d / 23.0 + 2.0)


def xw(d):
    """ridge west (outer) shore, rugged."""
    return xe(d) - rw(d) + 2.2 * math.sin(d / 7.3 + 0.3) + 1.6 * math.sin(d / 3.9 + 1.7)


# water ellipse (the one gap): centre, axes
WX, WD, WW, WL = 8.8, 352.0, 84.0, 102.0


def _arc_d(x, upper, grow=1.0):
    a, b = WW / 2 + grow, WL / 2 + grow
    u = b * math.sqrt(max(0.0, 1.0 - ((x - WX) / a) ** 2))
    return WD - u if upper else WD + u


def _end_face(upper, x_from, x_to, rc=11.0):
    """Points along the ellipse arc (offset 1 yd to the land side) from x_from to x_to, corners filleted.

    Entering from a wall at x_from (wall runs along D) and leaving into a wall at x_to."""
    sgn = 1.0 if x_to > x_from else -1.0
    out = []

    def bez(a, v, b, ts=(0.0, 0.25, 0.5, 0.75, 1.0)):
        return [((1 - t) ** 2 * a[0] + 2 * (1 - t) * t * v[0] + t * t * b[0],
                 (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * v[1] + t * t * b[1]) for t in ts]

    wall = -rc if upper else rc          # the wall point sits rc before the corner, on the far side of the arc
    # entering corner
    v0 = (x_from, _arc_d(x_from, upper))
    xb = x_from + sgn * rc * 0.9
    out += bez((x_from, v0[1] + wall), v0, (xb, _arc_d(xb, upper)))
    # the arc proper
    xa, xz = xb + sgn * 5.0, x_to - sgn * (rc * 0.9 + 0.0)
    n = max(2, int(abs(xz - xa) / 5.0))
    out += [(xa + (xz - xa) * k / n, _arc_d(xa + (xz - xa) * k / n, upper)) for k in range(n + 1)]
    # leaving corner
    v1 = (x_to, _arc_d(x_to, upper))
    out += bez((xz, _arc_d(xz, upper)), v1, (x_to, v1[1] + wall))[1:]
    return out


def _build_shore():
    pts, lab = [], []

    def add(label, seq):
        for p in seq:
            if pts and math.dist(pts[-1], p) < 0.05:
                continue
            pts.append(p)
            lab.append(label)

    frange = lambda a, b, s: [a + i * s for i in range(int(math.floor((b - a) / s + 1e-9)) + 1)]
    # 1 north shore: from the ridge west shore round the tee end to the ribbon east shore
    add("north_shore", [(xw(34.0), 34.0), (xw(34.0) + 0.5, 12.0), (xw(34.0) + 5.0, -6.0), (-60.0, -21.0), (-38.0, -30.0),
                        (-14.0, -34.0), (6.0, -33.0), (19.0, -28.0), (24.0, -17.0)])
    # 2 ribbon east shore (north piece)
    xen = ribbon_e(302.0)
    xwn = ribbon_w(302.0)
    dn_e = _arc_d(xen, True)
    add("ribbon_east_north", [(ribbon_e(d), d) for d in frange(2.0, dn_e - 22.0, 18.0)])
    # 3 gap north end follows the water ellipse arc (east -> west) with rounded corners
    add("gap_north_end", _end_face(True, xen, xwn))
    # 4 ribbon west wall (channel inner wall, north piece), going up
    d_tip_n = 50.0
    add("channel_wall_ribbon_north", [(ribbon_w(d), d) for d in reversed(frange(d_tip_n, _arc_d(xwn, True) - 20.0, 18.0))])
    # 5 north tip of the channel (semicircle over the top, east -> west)
    xa, xb = ribbon_w(d_tip_n), xe(d_tip_n)
    cx, r = (xa + xb) / 2, (xa - xb) / 2
    add("channel_tip_north", [(cx + r * math.cos(math.radians(a)), d_tip_n - r * math.sin(math.radians(a))) for a in range(0, 181, 30)])
    # 6 ridge east wall (channel inner wall), going down
    d_tip_s = 450.0
    add("channel_wall_ridge", [(xe(d), d) for d in frange(d_tip_n + 14.0, d_tip_s, 16.0)])
    # 7 south tip of the channel (semicircle round the bottom, west -> east)
    xa, xb = xe(d_tip_s), ribbon_w(d_tip_s)
    cx, r = (xa + xb) / 2, (xb - xa) / 2
    add("channel_tip_south", [(cx - r * math.cos(math.radians(a)), d_tip_s + r * math.sin(math.radians(a))) for a in range(0, 181, 30)])
    # 8 ribbon west wall (south piece), going up
    xws = ribbon_w(400.0)
    xes = ribbon_e(400.0)
    ds_w = _arc_d(xws, False)
    add("channel_wall_ribbon_south", [(ribbon_w(d), d) for d in reversed(frange(ds_w + 20.0, d_tip_s - 14.0, 16.0))])
    # 9 gap south end follows the water ellipse arc (west -> east) with rounded corners
    add("gap_south_end", _end_face(False, xws, xes))
    # 10 ribbon east shore (south piece) swelling round the green bulb
    add("ribbon_east_south", [(ribbon_e(d), d) for d in frange(_arc_d(xes, False) + 14.0, 432.0, 16.0)]
        + [(27.0, 454.0), (31.5, 474.0), (30.0, 494.0), (21.0, 513.0)])
    # 11 south shore round the green end, east -> west
    add("south_shore", [(6.0, 525.0), (-20.0, 529.0), (-48.0, 524.0), (-68.0, 512.0), (xw(490.0) + 5.0, 492.0)])
    # 12 ridge west shore, going up the island back to the start
    add("ridge_west_shore", [(xw(d), d) for d in reversed(frange(48.0, 474.0, 12.0))])
    return pts, lab


def _chaikin(pts, lab, it):
    for _ in range(it):
        n = len(pts)
        npts, nlab = [], []
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            npts.append((0.75 * a[0] + 0.25 * b[0], 0.75 * a[1] + 0.25 * b[1]))
            nlab.append(lab[i])
            npts.append((0.25 * a[0] + 0.75 * b[0], 0.25 * a[1] + 0.75 * b[1]))
            nlab.append(lab[i])
        pts, lab = npts, nlab
    return pts, lab


def _resample(pts, lab, step):
    n = len(pts)
    cum = [0.0]
    for i in range(n):
        cum.append(cum[-1] + math.dist(pts[i], pts[(i + 1) % n]))
    total = cum[-1]
    count = max(12, int(round(total / step)))
    out, ol = [], []
    seg = 0
    for k in range(count):
        s = total * k / count
        while cum[seg + 1] < s:
            seg += 1
        a, b = pts[seg], pts[(seg + 1) % n]
        t = (s - cum[seg]) / max(cum[seg + 1] - cum[seg], 1e-9)
        out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
        ol.append(lab[seg])
    return out, ol


_p, _l = _build_shore()
_p, _l = _chaikin(_p, _l, 2)
_p, _l = _resample(_p, _l, 5.0)
SHORE = [(round(x, 1) + 0.0, round(y, 1) + 0.0) for x, y in _p]
SHORE_LABELS = list(_l)

HAZARDS = [
    dict(kind="water", x=WX, d=WD, width=WW, length=WL, tag="channel_gap"),
    dict(kind="bunker", x=round(cxr(184.0) - 11.7, 1), d=184.0, width=11.0, length=24.0, tag="pinch_left"),
    dict(kind="bunker", x=round(cxr(200.0) + 11.7, 1), d=200.0, width=11.0, length=24.0, tag="pinch_right"),
    dict(kind="bunker", x=9.0, d=466.0, width=16.0, length=16.0, tag="front_of_green"),
]

def _r(v):
    return round(v, 1) + 0.0


def _pt(x, d):
    return (_r(x), _r(d))


def _stretches():
    kind = {"north_shore": "outer_cliff", "ribbon_east_north": "outer_cliff", "ribbon_east_south": "outer_cliff",
            "south_shore": "outer_cliff", "ridge_west_shore": "outer_cliff",
            "gap_north_end": "gap_face", "gap_south_end": "gap_face",
            "channel_wall_ribbon_north": "channel_inner_wall", "channel_wall_ribbon_south": "channel_inner_wall",
            "channel_wall_ridge": "channel_inner_wall",
            "channel_tip_north": "channel_tip", "channel_tip_south": "channel_tip"}
    out, start = [], 0
    for i in range(1, len(SHORE_LABELS) + 1):
        if i == len(SHORE_LABELS) or SHORE_LABELS[i] != SHORE_LABELS[start]:
            out.append(dict(name=SHORE_LABELS[start], first=start, last=i - 1, kind=kind[SHORE_LABELS[start]]))
            start = i
    return out


def _land_intervals(d):
    """Land intervals [(x0, x1), ...] of the final SHORE polygon along the line D = d (scanline, sorted by x)."""
    xs = []
    n = len(SHORE)
    for i in range(n):
        a, b = SHORE[i], SHORE[(i + 1) % n]
        if (a[1] > d) != (b[1] > d):
            xs.append(a[0] + (d - a[1]) * (b[0] - a[0]) / (b[1] - a[1]))
    xs.sort()
    return [(xs[k], xs[k + 1]) for k in range(0, len(xs) - 1, 2)]


def _measured_widths():
    """Ridge land width and sea-channel width measured along X on the final polygon (0.1 yd)."""
    ridge, chan = [], []
    for d in range(60, 461, 4):
        iv = _land_intervals(float(d))
        if iv:
            ridge.append(iv[0][1] - iv[0][0])
        if len(iv) >= 2 and d <= 440 and iv[1][1] - iv[1][0] >= 10.0:     # skip the corner tips of the gap end faces
            chan.append(iv[1][0] - iv[0][1])
    return [_r(min(ridge)), _r(max(ridge))], [_r(min(chan)), _r(max(chan))]


_RIDGE_W, _CHAN_W = _measured_widths()

# The dry safe route tee -> pin down the ridge. It bends WEST of the ruin (the ruin sits on the east half of the ridge near
# the tee end, D 93-121, like split.jpg's ruin on the inner slope) and keeps >= 7 yd inside the shore.
_RIDGE_ROUTE = ([(0.0, 0.0), (-30.0, 6.0), (-60.0, 22.0), (-70.0, 60.0), (-76.0, 92.0), (-77.0, 110.0), (-75.0, 126.0),
                 (-66.0, 142.0)]
                + [(round((xe(d) + xw(d)) / 2, 1), float(d)) for d in range(180, 461, 40)]
                + [(-52.0, 484.0), (-10.0, 492.0)])

SCENERY = dict(
    units="course yards (x right of the tee line, d down the hole); heights in metres; angles in degrees, yaw clockwise seen "
          "from above, 0 = along +D",
    sea_level_m=0.0,
    cliff_top_m=PLAY_Z,
    shore_stretches=_stretches(),
    channel=dict(
        inner_wall_stretches=["channel_wall_ribbon_north", "channel_tip_north", "channel_wall_ridge",
                              "channel_tip_south", "channel_wall_ribbon_south"],
        width_yd=_CHAN_W,
        mouth_hazard="channel_gap",
        tips=[dict(x=_r((ribbon_w(50.0) + xe(50.0)) / 2), d=50.0, radius_yd=_r((ribbon_w(50.0) - xe(50.0)) / 2), end="north"),
              dict(x=_r((ribbon_w(450.0) + xe(450.0)) / 2), d=450.0, radius_yd=_r((ribbon_w(450.0) - xe(450.0)) / 2), end="south")],
    ),
    gap=dict(x=WX, d=WD, width_yd=WW, length_yd=WL, north_face="gap_north_end", south_face="gap_south_end",
             land_in_ellipse=False, face_offset_yd=1.0),
    ridge=dict(
        spine=[_pt((xe(d) + xw(d)) / 2, d) for d in range(60, 481, 40)],
        width_yd=_RIDGE_W,
        route=[_pt(x, d) for x, d in _RIDGE_ROUTE],
    ),
    # The ruin is NOT on the ridge's natural landing bands. A full drive aimed along the ridge rests at D 250-266
    # (x -45..-65) and the second/third shots rest at D 300-470, so the ruin sits at the quiet north end of the ridge, D 93-121,
    # on its east half (the inner slope above the channel, as in split.jpg). Tallest wall and the fallen arch are at its
    # SOUTH end, so a ball north of it, looking down the hole, is not hidden behind a 4.5 m wall.
    ruin=dict(
        rock_mesh="existing library rock (a copy of one of the hole 7 ROCK_*/CLIFF_ROCK instances), visual only, no collider, "
                  "name must not start with TERRAIN/FAIRWAY/GREEN/TEE_BOX/BUNKER",
        walls=[dict(p0=(-67.0, 94.0), p1=(-58.0, 93.0), height_m=2.0, thick_yd=1.6),
               dict(p0=(-67.0, 94.0), p1=(-68.0, 109.0), height_m=3.0, thick_yd=1.6),
               dict(p0=(-68.0, 109.0), p1=(-58.0, 112.0), height_m=4.5, thick_yd=1.6),
               dict(p0=(-58.0, 93.0), p1=(-57.0, 99.0), height_m=2.5, thick_yd=1.6)],
        fallen_arch=dict(x=-63.5, d=119.0, yaw_deg=70.0, span_yd=8.0, height_m=5.0),
        rubble=[dict(x=-64.0, d=101.0, radius_yd=2.2), dict(x=-61.0, d=106.0, radius_yd=1.6),
                dict(x=-67.0, d=115.0, radius_yd=1.8)],
    ),
    sea_stacks=[dict(x=54.0, d=262.0, radius_yd=4.0, height_m=17.0),
                dict(x=58.0, d=120.0, radius_yd=3.5, height_m=10.0),
                dict(x=50.0, d=428.0, radius_yd=3.5, height_m=11.0),
                dict(x=22.0, d=562.0, radius_yd=5.5, height_m=15.0),
                dict(x=-112.0, d=118.0, radius_yd=5.0, height_m=19.0),
                dict(x=-118.0, d=354.0, radius_yd=4.5, height_m=14.0),
                dict(x=-112.0, d=466.0, radius_yd=4.0, height_m=12.0),
                dict(x=34.0, d=372.0, radius_yd=3.0, height_m=8.0)],
    tee_box=dict(x=0.0, d=0.0, yaw_deg=_r(math.degrees(math.atan2(CENTERLINE[1][0], CENTERLINE[1][1]))), width_yd=12.0, length_yd=16.0),
    green_bulb=dict(x=CENTERLINE[-1][0], d=CENTERLINE[-1][1], radius_yd=GREEN_RADIUS, apron_radius_yd=22.0),
    surf_stretches=["gap_north_end", "gap_south_end", "channel_tip_south", "south_shore", "ridge_west_shore"],
)

LIMITS = dict(number=9, length_min=460, length_max=500, par=5, carry_max=140.0, water=1, bunker=3, forced=False,
              route_clearance_min=6.0, min_pad_area=500.0)
