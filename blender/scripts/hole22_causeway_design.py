"""Hole 22 — Ember Causeway (par 4): a long, winding causeway of black rock across the crater's lava lake. It
swells into a landing zone, pinches to a neck where the lava bites in from both sides, swells again and ends in
a round plateau with the green. Lava ponds sit beside the landing zones, basalt stacks stand in the lake, and the
sea is lava, so a ball that leaves the rock burns.

Design data for course_builder.py. Metres; +Y north, +X east; the lava lake at z = 0.
"""
import math
from functools import lru_cache
from magma_shapes import ribbon, catmull, dist_to_polyline, smoothstep, frame, place, heading, polar

NUMBER, PAR, NAME = 22, 4, "Ember Causeway"
BLURB = "A par 4 along a winding causeway of black rock: thread the neck where the lava bites in, and hit the plateau green."
THEME = "magma"
LAVA_SEA = True
ROUGH_WIDTH = 300
GRID = 4.0
SEED = 22

SPINE = [(0, -30), (3, 15), (16, 58), (36, 100), (54, 146), (56, 192), (40, 236), (14, 272), (-4, 302)]
#          tee pad  fairway ...   landing 1 (wide)         neck (narrow)      landing 2 (wide)    approach   green plateau
WIDTHS = [17, 14, 16, 22, 26, 13, 15, 26, 42]
SPINE_CURVE = catmull(SPINE, 12)
F = frame(SPINE, WIDTHS)
HEAD = heading(F, 0.99)     # the way the causeway runs into the green
GREEN_C = (-4.0, 300.0)


@lru_cache(maxsize=None)
def _height(x, y):
    d, t = dist_to_polyline(x, y, SPINE_CURVE)
    z = 18.0 + 1.3 * math.sin(x * 0.08 + 0.9) * math.cos(y * 0.05) + 0.7 * math.sin(y * 0.16 + x * 0.04)
    z += 1.6 * math.sin(t * 9.0)                      # the causeway rises and dips along its length
    dg = math.hypot(x - GREEN_C[0], y - GREEN_C[1])
    z += 3.0 * smoothstep((30.0 - dg) / 10.0)         # the green's ledge
    return z


def height(x, y):
    return _height(round(x, 2), round(y, 2))


ISLANDS = [ribbon(SPINE, WIDTHS, per=6)]

TEE = dict(cx=0, cy=-24, rx=10, ry=13, rot=0.0)
TEE_MARKER = (0.0, -28.0)
FAIRWAY_CL = [(2, 10, 12), (14, 55, 13), (33, 97, 17), (51, 140, 19), (55, 184, 9), (44, 226, 9), (20, 264, 14), (2, 288, 12)]
GREENS = [dict(cx=GREEN_C[0], cy=GREEN_C[1] + 2, rx=20, ry=17, rot=math.radians(-12))]
PIN = (-2.0, 304.0)


def _b(p, rx, ry, rot, seed):
    return (p[0], p[1], rx, ry, rot, seed)


BUNKERS = [
    _b(place(F, .38, .55), 9, 6.5, 30, 1),
    _b(place(F, .51, .55), 8, 6, 10, 2),
    _b(place(F, .74, -.42), 8, 6, -20, 3),
    _b(place(F, .82, .5), 8, 6, 0, 4),
    _b(polar(GREEN_C[0], GREEN_C[1], HEAD + 145, 30), 8, 6, 10, 5),
    _b(polar(GREEN_C[0], GREEN_C[1], HEAD - 145, 30), 7, 5.5, -20, 6),
    _b(polar(GREEN_C[0], GREEN_C[1], HEAD + 8, 32), 7, 5, 0, 7),
]
STAIRS = []
BRIDGES = []
# a lava pond beside the first landing zone: in play
_p1 = place(F, .44, -.55)
POOLS = [dict(kind="lava", cx=_p1[0], cy=_p1[1], rx=8.0, ry=6.0, z=17.6)]
RIVERS = []
FALLS = []
WATER_HAZARDS = [(_p1[0], _p1[1], 8.0, 6.0, "lava")]

LANDMARKS = [
    ("BRAZIER", *place(F, .02, .72)),
    ("BRAZIER", *place(F, .02, -.72)),
    ("BRAZIER", *polar(GREEN_C[0], GREEN_C[1], HEAD + 100, 34)),
    ("BRAZIER", *polar(GREEN_C[0], GREEN_C[1], HEAD - 100, 34)),
    ("GEYSER", *place(F, .60, 2.3), 5.0),
    ("GEYSER", *place(F, .66, -2.6), 5.0),
    ("STACKS", *place(F, .28, 2.6), 0, 20, 22),
    ("STACKS", *place(F, .72, -2.8), 0, 20, 22),
    ("STACKS", *place(F, .50, -2.8), 0, 16, 18),
    ("STEAM_VENT", *place(F, .33, -.45)),
    ("STEAM_VENT", *place(F, .70, .5)),
]

TREES = [
    (10, "DEAD_TREE", 12, None),
    (6, "PALM", 14, None),
    (7, "OBSIDIAN_SPIRE", 18, None),
    (14, "BUSH_SMALL", 6, None),
]
