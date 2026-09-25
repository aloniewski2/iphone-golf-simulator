"""Hole 16 — Maple Point (par 4), the first of Maple Bay: a low crescent of dunes wrapped round
the bay. The tee is out on one horn and the green on the other, so the hole bends round the
water the whole way: follow the crescent, or cut across the bay as far as you dare.

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache
from maple_bay_palette import PALETTE  # noqa: F401  (course_builder reads it off this module)

NUMBER, PAR, NAME = 16, 4, "Maple Point"
BLURB = "A dogleg round the bay, horn to horn along a crescent of dunes: follow the sand, or cut across the water as far as you dare."
GRID = 4.0
SEED = 16

R_MID = 122.0                      # the crescent's middle line round the bay (centred on the origin)
A_TEE, A_GREEN = -45.0, 112.0      # degrees, anticlockwise from east


def polar(a_deg, r):
    a = math.radians(a_deg)
    return (r * math.cos(a), r * math.sin(a))


@lru_cache(maxsize=None)
def _height(x, y):
    # low links ground: dunes rolling along the crescent, a little higher to seaward
    r = math.hypot(x, y); a = math.atan2(y, x)
    z = 7.0 + 2.2 * max(0.0, (r - R_MID) / 40.0)
    z += 1.1 * math.sin(a * 9.0 + 0.6) * math.cos(r * 0.07) + 0.5 * math.sin(a * 23.0)
    # the green's end of the crescent rises to a knob over the bay
    gx, gy = polar(A_GREEN, R_MID)
    z += 3.0 * math.exp(-(((x - gx) / 38) ** 2 + ((y - gy) / 38) ** 2))
    return z


def height(x, y):
    return _height(round(x, 2), round(y, 2))


def _crescent():
    outer = [polar(a, 162 + 7 * math.sin(math.radians(a) * 5)) for a in range(-62, 131, 8)]
    inner = [polar(a, 84 + 5 * math.sin(math.radians(a) * 7 + 1)) for a in range(128, -61, -8)]
    return outer + inner


ISLANDS = [_crescent()]

_tx, _ty = polar(A_TEE, R_MID)
TEE = dict(cx=_tx, cy=_ty, rx=11, ry=14, rot=math.radians(A_TEE))
TEE_MARKER = polar(A_TEE - 3, R_MID)
FAIRWAY_CL = [polar(a, R_MID) + (w,) for a, w in [(-32, 14), (-15, 19), (5, 22), (25, 22), (45, 21), (65, 19), (82, 15), (94, 12), (99, 11)]]
_gx, _gy = polar(A_GREEN, R_MID)
GREENS = [dict(cx=_gx, cy=_gy, rx=19, ry=16, rot=math.radians(A_GREEN - 90))]
PIN = (_gx + 2.0, _gy + 1.0)
# links: pot bunkers all along the bay side, where the corner is cut
BUNKERS = [(*polar(a, r), rx, ry, rot, i + 1) for i, (a, r, rx, ry, rot) in enumerate([
    (0, 100, 7, 5, 20), (22, 98, 6, 5, -10), (48, 101, 7, 5, 30), (70, 102, 6, 4, 0),
    (35, 146, 10, 6, 40), (104, 142, 7, 5, 10), (120, 104, 6, 4, -30)])]
STAIRS = []
BRIDGES = []
LANDMARKS = [("LIGHTHOUSE", *polar(30, 152))]


def _seaward(x, y): return math.hypot(x, y) > R_MID + 26


TREES = [
    (18, "TREE_ROUND", 11, _seaward),
    (26, "BUSH_SMALL", 7, None),
    (8, "TREE_PINE_SMALL", 9, _seaward),
]
