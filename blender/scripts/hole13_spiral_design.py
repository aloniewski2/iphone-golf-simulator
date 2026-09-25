"""Hole 13 — The Spiral (par 5), after the "Spiral" hole in Rising Impact: the fairway winds once
round a rock pinnacle, climbing as it goes, to a green on the summit. A straight ball runs off the
curling fairway; the hole wants a draw. Mock: blender/holes_mock/spiral.png.

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache

NUMBER, PAR, NAME = 13, 5, "The Spiral"
BLURB = "A par 5 that winds once round the pinnacle, climbing all the way: bend it left with the fairway, then pitch up to the summit green."
GRID = 4.0            # terrain sample spacing: fine enough that the terraces read as cliffs
SEED = 13

# ---------------------------------------------------------------- the spiral
R0, R1 = 112.0, 52.0          # radius at the tee, radius at the end of the fairway
TH0 = -math.pi / 2            # the tee is due south of the pinnacle
TURN = 2 * math.pi            # one full turn, anticlockwise (it bends left: a draw)
Z0, Z1 = 18.0, 42.0           # fairway height at the tee, at the end
SUMMIT_R, SUMMIT_Z = 34.0, 64.0


def spiral(u):
    th = TH0 + TURN * u
    r = R0 - (R0 - R1) * u
    return (r * math.cos(th), r * math.sin(th))


_SAMPLES = [(u / 720.0,) + spiral(u / 720.0) for u in range(721)]


def nearest_u(x, y):
    best, bu = 1e18, 0.0
    for u, sx, sy in _SAMPLES[::8]:
        d = (sx - x) ** 2 + (sy - y) ** 2
        if d < best: best, bu = d, u
    i0 = int(bu * 720)
    for u, sx, sy in _SAMPLES[max(0, i0 - 8):min(721, i0 + 9)]:
        d = (sx - x) ** 2 + (sy - y) ** 2
        if d < best: best, bu = d, u
    return bu


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


@lru_cache(maxsize=None)
def _height(x, y):
    r = math.hypot(x, y)
    roll = 0.5 * math.sin(x * 0.07 + 0.3) * math.cos(y * 0.05)
    if r < SUMMIT_R:
        return SUMMIT_Z + 0.8 * (1 - (r / SUMMIT_R) ** 2) + 0.15 * roll
    z = Z0 + (Z1 - Z0) * nearest_u(x, y) + roll
    # the pinnacle's wall: a steep rise to the summit rim
    return z + (SUMMIT_Z - z) * smoothstep((SUMMIT_R + 6.0 - r) / 6.0)


def height(x, y):
    return _height(round(x, 2), round(y, 2))


# ---------------------------------------------------------------- layout
ISLANDS = [[(math.cos(a) * (138 + 7 * math.sin(3 * a + 0.4) + 5 * math.sin(7 * a)), math.sin(a) * (134 + 7 * math.sin(3 * a + 0.4) + 5 * math.cos(5 * a)))
            for a in [2 * math.pi * i / 28 for i in range(28)]]]

TEE = dict(cx=spiral(0)[0], cy=spiral(0)[1] - 4, rx=11, ry=14, rot=0.0)
TEE_MARKER = (TEE["cx"], TEE["cy"] - 4)
FAIRWAY_CL = [spiral(u) + (15.0,) for u in [0.05 + i * 0.95 / 18 for i in range(19)]]
GREENS = [dict(cx=0, cy=3, rx=22, ry=19, rot=math.radians(10))]
PIN = (4.0, 7.0)


def _off(u, side):
    """A point beside the spiral: side > 0 outside the turn, < 0 inside."""
    x, y = spiral(u); r = math.hypot(x, y)
    return (x + x / r * side, y + y / r * side)


BUNKERS = [
    _off(0.30, 22) + (10, 6, 30, 1),
    _off(0.58, -21) + (9, 6, -20, 2),
    _off(0.84, 21) + (10, 6, 10, 3),
    (-23, 12, 7, 5, 30, 4),
    (21, -13, 6, 4.5, -20, 5),
]

# a stair of planks from the end of the fairway up the pinnacle wall to the summit
STAIRS = [((spiral(1.0)[0], spiral(1.0)[1] + 2), (0.0, -SUMMIT_R + 2), 3.2)]
BRIDGES = []
LANDMARKS = []

# trees: (count, kind, min spacing, region) — regions are predicates on (x, y)
TREES = [
    (9, "TREE_PINE_MEDIUM", 7, lambda x, y: SUMMIT_R - 6 < math.hypot(x, y) < SUMMIT_R - 1 and y > -20),   # the summit's crown, behind the green
    (26, "TREE_PINE_LARGE", 14, None),
    (18, "TREE_PINE_MEDIUM", 12, None),
    (12, "TREE_PINE_SMALL", 9, None),
    (10, "BUSH_SMALL", 7, None),
]
