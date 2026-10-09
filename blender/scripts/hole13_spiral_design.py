"""Hole 13 — The Spiral (par 5), after the "Spiral" hole in Rising Impact: the fairway is a road
that winds three-quarters of the way round a round green hill, climbing as it goes, onto the
summit plateau where the green sits. Follow the bend left with the road, or cut the corner across
the open hillside (shorter, uphill, through the pines). Mock: blender/holes_mock/spiral.png.

Revamped 2026-09-26: the first Spiral took the heights of whichever turn of the fairway was
nearest, so each turn ended in a sheer 24 m wall (straight across the tee) and the green sat up
another one: balls stuck on the cliff faces, and it played three over. Now the hill is one smooth
cone and the road a level bench cut into it, with grassy banks either side; it reaches the top.

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache

NUMBER, PAR, NAME = 13, 5, "The Spiral"
BLURB = "A par 5 up the road that winds round the hill: follow the bend left, or cut across the slope through the pines, then pitch onto the summit green."
GRID = 4.0
SEED = 13

# ---------------------------------------------------------------- the hill and its road
R0, R1 = 116.0, 42.0          # the road's radius at the tee, where it reaches the summit
TH0 = -math.pi / 2            # the tee is due south of the summit
TURN = 1.5 * math.pi          # three-quarters of a turn, anticlockwise (it bends left)
FOOT_R, FOOT_Z = 128.0, 13.0  # the hill's foot: the flat ground round it
SUMMIT_R, SUMMIT_Z = 46.0, 38.0
ROAD_HALF, BANK = 30.0, 15.0  # the road's level bench, and the bank either side of it


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def cone(r):
    """The hill: flat round its foot, an even slope up, and a gently domed summit plateau."""
    if r <= SUMMIT_R:
        return SUMMIT_Z + 1.0 * (1 - (r / SUMMIT_R) ** 2)
    t = max(0.0, min(1.0, (FOOT_R - r) / (FOOT_R - SUMMIT_R)))
    return FOOT_Z + (SUMMIT_Z - FOOT_Z) * t


def spiral(u):
    th = TH0 + TURN * u
    r = R0 - (R0 - R1) * u
    return (r * math.cos(th), r * math.sin(th))


def road_z(u):
    return cone(R0 - (R0 - R1) * u)


_SAMPLES = [(u / 600.0,) + spiral(u / 600.0) for u in range(601)]


def nearest(x, y):
    """The road's nearest point: its u along the road, and how far away it is."""
    best, bu = 1e18, 0.0
    for u, sx, sy in _SAMPLES[::6]:
        d = (sx - x) ** 2 + (sy - y) ** 2
        if d < best: best, bu = d, u
    i0 = int(round(bu * 600))
    for u, sx, sy in _SAMPLES[max(0, i0 - 6):min(601, i0 + 7)]:
        d = (sx - x) ** 2 + (sy - y) ** 2
        if d < best: best, bu = d, u
    return bu, math.sqrt(best)


@lru_cache(maxsize=None)
def _height(x, y):
    r = math.hypot(x, y)
    z = cone(r)
    # the road: a level bench across its width, blending back into the hillside over the banks
    u, d = nearest(x, y)
    w = smoothstep((ROAD_HALF + BANK - d) / BANK)
    z += (road_z(u) - z) * w
    # a gentle roll so the ground isn't a lathe
    z += 0.45 * math.sin(x * 0.07 + 0.3) * math.cos(y * 0.05) * (1 - w * 0.6)
    return z


def height(x, y):
    return _height(round(x, 2), round(y, 2))


# ---------------------------------------------------------------- layout
ISLANDS = [[(math.cos(a) * (142 + 7 * math.sin(3 * a + 0.4) + 5 * math.sin(7 * a)), math.sin(a) * (140 + 7 * math.sin(3 * a + 0.4) + 5 * math.cos(5 * a)))
            for a in [2 * math.pi * i / 32 for i in range(32)]]]

TEE = dict(cx=spiral(0)[0] - 18, cy=spiral(0)[1], rx=11, ry=14, rot=math.radians(90))
TEE_MARKER = (TEE["cx"] - 5, TEE["cy"])
# the road, up onto the plateau (the green is a pitch on from its end)
FAIRWAY_CL = [spiral(u) + (18.0,) for u in [i / 16 for i in range(17)]]
GREENS = [dict(cx=-4, cy=4, rx=21, ry=18, rot=math.radians(-20))]
PIN = (-2.0, 7.0)


def _off(u, side):
    """A point beside the road: side > 0 outside the turn (downhill), < 0 inside (uphill)."""
    x, y = spiral(u); r = math.hypot(x, y)
    return (x + x / r * side, y + y / r * side)


BUNKERS = [
    _off(0.30, 20) + (10, 6, 30, 1),     # outside the first bend, where a drive runs out
    _off(0.58, -19) + (9, 6, -20, 2),    # inside the second, for the corner-cutter
    _off(0.86, 19) + (8, 5, 10, 3),
    (-26, 14, 7, 5, 30, 4),              # round the green
    (18, -10, 6, 4.5, -20, 5),
]

STAIRS = []
BRIDGES = []
LANDMARKS = []


def _hillside(x, y):
    """Up the slope between the road's turns: the corner the brave cut across."""
    r = math.hypot(x, y)
    return SUMMIT_R + 4 < r < FOOT_R - 6 and nearest(x, y)[1] > ROAD_HALF + BANK


# trees: (count, kind, min spacing, region) — regions are predicates on (x, y)
TREES = [
    (8, "TREE_PINE_MEDIUM", 8, lambda x, y: SUMMIT_R - 10 < math.hypot(x, y) < SUMMIT_R - 2 and y > 0),   # behind the green
    (22, "TREE_PINE_LARGE", 13, _hillside),
    (16, "TREE_PINE_MEDIUM", 11, _hillside),
    (22, "TREE_PINE_LARGE", 14, None),
    (12, "TREE_PINE_SMALL", 9, None),
    (10, "BUSH_SMALL", 7, None),
]
