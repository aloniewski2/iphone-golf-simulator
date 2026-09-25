"""Hole 18 — Harvest Run (par 5), Maple Bay's closer: three islands in a zig-zag, each higher
than the last. Drive from the low tee islet across the water to the fairway island, then carry
the second channel up to a green on a cliff-top mesa among the maples. Boardwalks join them.

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache
from maple_bay_palette import PALETTE  # noqa: F401

NUMBER, PAR, NAME = 18, 5, "Harvest Run"
BLURB = "Three islands, each higher than the last: drive across to the fairway island, then carry the channel up to the green on its cliff-top."
GRID = 4.0
SEED = 18

TEE_ISLE = (-50.0, -215.0)
MESA = (-35.0, 205.0)


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


@lru_cache(maxsize=None)
def _height(x, y):
    if math.hypot(x - TEE_ISLE[0], y - TEE_ISLE[1]) < 60:        # the low tee islet
        return 7.0 + 0.4 * math.cos(x * 0.1) * math.cos(y * 0.1)
    if math.hypot(x - MESA[0], y - MESA[1]) < 75:                 # the mesa: a flat top, high
        return 27.0 + 0.6 * math.sin(x * 0.08) * math.cos(y * 0.07)
    # the fairway island climbs from its southern end to its northern
    return 12.0 + 7.0 * smoothstep((y + 110) / 210.0) + 0.9 * math.sin(x * 0.06 + 0.5) * math.cos(y * 0.05)


def height(x, y):
    return _height(round(x, 2), round(y, 2))


ISLANDS = [
    [(TEE_ISLE[0] + math.cos(a) * (30 + 2 * math.sin(3 * a)), TEE_ISLE[1] + math.sin(a) * (26 + 2 * math.cos(4 * a)))
     for a in [2 * math.pi * i / 14 for i in range(14)]],
    [(0, -120), (40, -128), (78, -96), (96, -40), (100, 20), (92, 80), (70, 108), (40, 104), (22, 70), (14, 20), (4, -40), (-8, -90)],
    [(-80, 180), (-60, 150), (-20, 150), (10, 172), (15, 215), (-5, 250), (-45, 258), (-78, 236)],
]

TEE = dict(cx=TEE_ISLE[0], cy=TEE_ISLE[1] - 4, rx=10, ry=12, rot=math.radians(28))
TEE_MARKER = (TEE_ISLE[0] - 2, TEE_ISLE[1] - 10)
# the fairway is the middle island's; the tee shot and the second both carry water
FAIRWAY_CL = [(20, -92, 14), (40, -50, 21), (56, 0, 23), (62, 45, 19), (58, 78, 12)]
GREENS = [dict(cx=MESA[0], cy=MESA[1], rx=19, ry=17, rot=math.radians(-25))]
PIN = (MESA[0] + 3.0, MESA[1] + 3.0)
BUNKERS = [
    (74, -60, 9, 6, 25, 1),
    (32, 20, 8, 6, -10, 2),
    (82, 60, 8, 5, 20, 3),
    (-12, 196, 7, 5, 0, 4),
    (-58, 222, 7, 5, 35, 5),
]
BRIDGES = [[(-30, -196, 7.3), (-2, -106, 12.3)], [(52, 100, 19.3), (-4, 164, 27.3)]]
STAIRS = []
LANDMARKS = [("TOWER", -66, 240)]


def _mesa(x, y): return math.hypot(x - MESA[0], y - MESA[1]) < 75


def _middle(x, y): return -130 < y < 115 and x > -20


TREES = [
    (30, "TREE_ROUND", 7, _mesa),
    (30, "TREE_ROUND", 10, _middle),
    (10, "TREE_PINE_MEDIUM", 11, _middle),
    (10, "BUSH_SMALL", 7, None),
]
