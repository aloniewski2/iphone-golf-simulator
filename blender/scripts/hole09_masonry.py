"""hole09_masonry.py - rough broken masonry for the Split ruin (POSTCARD_LOOK; repair round 1 + repair round 2 + repair round 3, 2026-10-04 / 05).

GRASS + RUIN PASS (2026-10-05, carry-over review "the ruin is still a pile of straight-cut boxes ... the arch crown reads as two floating blocks"):
    stones      BLOCK_L 0.70-1.50 -> 0.85-1.65 m, LEVEL_STEP 0.20-0.42 -> 0.24-0.46 m (chunkier, fewer courses), wall depth 0.78-1.0 T (more relief); every wall stone gets its four VERTICAL edges chamfered 5-12 cm
                (STONE_V_EDGES: the top faces lose their rectangular outline), arris bevels 2.2-6 cm at p 0.62 (was 1.2-3.5 cm at 0.35), corner chips 6-21 cm (bottom corners keep 5-17 cm: RUIN_BASE_BURIED),
                up to 23 faces; no plane may make a near-coplanar (< 14 deg) pair of side faces (hole09_stone.bad_pairs: a chamfer within 12 deg of a side face would get its own brick window = RUIN_NO_UV_SPLIT)
    arch        build_arch(missing=(), slip=(6,)): the crown wedge is SLIPPED 13 cm toward the opening instead of lying on the ground (a radial slide keeps both side planes in face contact, so the ring is closed: no two
                floating halves with a gap); its inner corners are knocked off by the fall (4-10 cm); the heap of rubble under the arch stays
REPAIR ROUND 3 (2026-10-05, review: "the ruin still reads as stacked boxes: straight-cut block courses, triangle-split shading on the faces, a floating arch crown, flat-colour green cap slabs"):
    cause found  the round-2 stones were convex HULLS of jittered corner points: a twisted / tapered quad is not planar, the hull split it into two triangles of different normals and every triangle got its own random
                 UV window (work/postcard-look/v2/hole09/r7/face_split_stats.py on the round-2 stage: of 4,477 faces 2,467 triangles, 1,411 near-coplanar adjacent pairs, 1,092 of them (77 %) with a UV mismatch along the
                 shared edge = the 'diagonal triangle shading'); the cap slabs were one magnified brick window per face (flat colour, moss bricks drawn at random); the stones were 1.4x brighter than split.jpg's ruin (Game view, luminance 134 against 93)
    stones        hole09_stone.py: every stone is an intersection of half-spaces (planes), so every face is ONE exactly planar n-gon with ONE UV mapping (RUIN_PLANAR_FACES 0.014 mm; RUIN_NO_UV_SPLIT: 15 near-coplanar pairs,
                 0 mismatching; round 2: 1,092). Hexahedron planes (taper / shear / twist / tilt) + corner chips (4-17 cm) + arris bevels + fracture planes on the broken tops + quarry-split folds (15-23 deg) = 9..17 faces
    material      LK_MASONRY brick-interior windows on the sides (dark, moss-free bricks preferred: z-score weights over the 8-row table), LK_ROCK with ONE continuous world-axis box projection per stone on every top
                 and on ~1 stone in 4 (darker-leaning window of Rock_C, the moss patches stay as natural weathering); per mesh LK_ROCK <= 36 % of the area (the verifier calls a mesh with >= 50 % rock a ROCK; PROP_MATERIALS
                 needs >= 50 % LK_MASONRY on DRESS_RUIN / DRESS_ARCH)
    arch          the ring keeps its 11 wedges (one missing, the same <= 1 m gap) but the wedges now bear by face contact (joint corners chipped <= 3.5 cm, the free sides of the gap up to 12 cm), and an OUTER COURSE of
                 irregular haunch stones (0.45-0.8 m thick, joints off the ring's) fills the angle between the ring and the piers on both sides, so no ring stone hangs over a hole (the review's 'floating crown block');
                 the left pier is 5.8 m (the thin 6.2 m spike is gone), the right pier 4.9 m; the piers have TWIN stones (two half-depth stones side by side across the thickness in 38 % of the courses: no
                 full-depth slab stack seen end-on)
    blocks        DRESS_BLOCK variants: ONE continuous projection per axis region inside ONE brick interior (no per-triangle windows)

Replaces PL.build_ruin / PL.build_masonry_arch for hole 9 (the props library is not edited).

Round 1 fixed "stacked Lego boxes, floating slabs and an unsupported lintel" (hulls of tapered stones, support by construction, buried first course, a lintel stone bearing on both
jambs). The round-2 review still found "stacked bricks": 8-9 rectangular courses of near-identical boxes with straight horizontal cut lines, diagonal triangle splits in the shading of
every block face, a regular paving-brick pattern on the tops, a crown that hangs with a gap to the right pier, ruler-straight tops. Causes measured on the round-1 stage (face_stats):
20.7 % of the faces had a loop normal more than 3 deg (up to 28 deg) off their own face normal (smooth shading averaged across the chamfer facets, so a large quad is shaded by 4 different
vertex normals and the triangulation shows as a diagonal), the UV windows were whole brick ROWS (a vertical mortar line ran through most faces), all stones of a course shared one top height.

What this module does now (every number is a constant below):

    shading     every stone is FLAT shaded (loop normal == face normal, so no triangle can shade differently from its neighbour)
    stone       a hull of a tapered, sheared, twisted box (no two faces parallel) with random corner chips (4-13 cm; <= 4 cm on the top of a stone that carries another one). A pillow apex on a face
                (rough_points p_bulge) was tried first and rejected: the hull turns it into a pyramid of 4-6 triangles and flat shading shows them as an X on every face (the review's triangle splits again);
                it is kept in the code at p_bulge = 0
    layout      SKYLINE rubble on wavy bed levels: stones are laid on the lowest run of the wall; a stone's top lands on one of 16 wavy levels of the wall (2-4 cm wave, 7-12 cm jogs, 27-44 cm apart) and
                it spans 1, 2 or 3 of them (height 0.27-0.85 m, length 0.34-2.4 m; 24 % short, 16 % long), so beds are NOT ruled courses; a bed rests on the highest cell of its run (hollow <= 4.5 cm
                + the tilt / chip of the stone below), joints keep >= 12 cm off the joints below where the run allows, depth 0.86-1.0 of the wall with a random out-of-line face (relief)
    tops        the last stone of a column is cut to the broken profile with a steeply sloped / chipped top (ragged), the wall profile is seeded (full stretches, stubs, collapsed gaps)
    doorway     jamb rows (quoin pattern, exact top) + ONE lintel stone bearing 0.55 m on each jamb
    arch        two rubble piers (6.2 m left, 4.4 m right) + a ring of 11 wedge voussoirs: the left half springs from the tall pier and rings up to the crown, the right four wedges are seated IN the
                right pier (its top is above the highest right wedge: nothing pokes out of it); ONE crown wedge (6) is missing and lies in a rubble heap under the gap (round 1: two wedges missing,
                the crown ended in the air beside the right pier and a tilted wedge stood on top of it)
    UV          every face has its OWN UV window inside ONE brick interior of Masonry_C.png (u AND v: no mortar line crosses a face; the interiors are the intersection of a pixel scan and the
                texture generator's own joint table; every face of a stone prefers the same brick, so a stone keeps one tone), 0.3-1.5 x the contract density (tops 0.26 x), random sub-window /
                flip / 90-degree turn for tops: the painted grid cannot read across blocks and the tops are not paving
    blocks      DRESS_BLOCK_A/B variants (make_block_variant): rounded, subdivided, noise-displaced, flat shaded, >= 64 welded vertices, per-face brick-interior UVs

Everything is visual only (DRESS_RUIN_nn_pp / DRESS_ARCH_nn_pp, no collider prefix).
"""
import math
import random
import sys

sys.dont_write_bytecode = True
import numpy as np
import bpy
import bmesh
from mathutils import Vector, Matrix

import postcard_lib as P
import postcard_look_lib as L
import postcard_props_lib as PL
import hole09_stone as ST

YD = P.YD

# --------------------------------------------------------------------------------------------- constants
BLOCK_L = (0.85, 1.65)        # ordinary stone length along the wall (m) (grass pass 2026-10-05: 0.70-1.50 -> 0.85-1.65: chunkier, fewer stones, closer to split.jpg's big rounded ruin stones)
LONG_L = (1.7, 2.4)           # 11 % of the stones are long stretchers (round 3: was 16 %)
SHORT_L = (0.34, 0.62)        # 31 % are short headers / chinks (round 3: was 24 %)
LEVEL_STEP = (0.24, 0.46)     # spacing of the (wavy) bed levels of a wall (m); a stone spans 1 level (60 %), 2 (32 %) or 3 (8 %): heights 0.22-0.85 m (round 3: was 0.27-0.44 m steps, 72 / 24 / 4 %: more small stones, less regular courses)
LEVEL_WAVE = (0.02, 0.055)    # broken, gently changing beds; each next stone still rests on the measured skyline
LEVEL_JOG = (0.07, 0.12)      # a level jogs by this much at one place along the wall (70 % of the levels): the course steps up / down
GAP = 0.025                   # gap between neighbouring stones (m, each side)
BURY = (0.25, 0.45)           # the first row sits this far under the lowest ground of its span
FOUND_ABOVE = (0.12, 0.38)    # the first row stands this far over the highest ground of its span
MIN_STACK = 0.30              # a stone is laid at a place only if the broken profile leaves >= 0.30 m of height
MIN_STONE_H = 0.22
LINTEL_H = (0.50, 0.58)
LINTEL_BEARING = 0.55         # the lintel stone bears this far on each jamb (m)
JAMB_ZONE = 1.15              # the quoin rows beside the doorway cover at least this width from the door edge (m)
TIGHT_JOINT = 0.12            # a joint nearer than this to a joint of the stone below is avoided where the run allows
CELL = 0.05                   # skyline resolution (m)
SINK = 0.0                   # a bed is laid this far INTO the highest cell under it. 0.03 was tried (no hollow at all) and reverted: the beds then overlap the stones below and the round-1 ray test calls 94 of them floating
TOP_CHIP = 0.03               # corner chips on the TOP of a stone that carries another stone are at most this big (a notch under a bridging stone is a hollow: 0.05 left 2 of 353 stones with a hollow)
TOL = 0.06                    # a run = neighbouring cells whose tops lie within 6 cm of the lowest one (the hollow under a bed is <= 6 cm)
UV_K_TOP = 0.26
UV_K = (0.30, 1.50)           # per-face UV density as a factor of the contract density (1 / 4 m); verify allows 1/3 .. 3 on the mean and 1/4 .. 4 per edge
MAS_ROW = dict(y0=31.0, period=64.0, pad=9.0, h=46.0, size=512.0)   # px of the brick rows of Masonry_C.png (mortar line centres at y = 31 + 64 k from the bottom)
BRICK_PAD_PX = 6              # a window keeps this far from a mortar column (px)
MIN_SEG_PX = 30               # a brick interior narrower than this is not used

# box corner loops (ix, iy, iz): the faces that get a pillow apex
_FACES = {
    "front": [(0, 1, 0), (1, 1, 0), (1, 1, 1), (0, 1, 1)],
    "back": [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)],
    "top": [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)],
    "left": [(0, 0, 0), (0, 1, 0), (0, 1, 1), (0, 0, 1)],
    "right": [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)],
}


# --------------------------------------------------------------------------------------------- brick interiors of Masonry_C.png
_SEGS = None
_SEG_TONE = []


def _generator_joints(seed=1001, courses=8):
    """u of the vertical mortar joints of every course of Masonry_C.png, re-derived from the texture generator's own random tables (golf_look_textures.mat_masonry draws, per course,
    nb = integers(4, 8), the brick widths uniform(0.6, 1.4, nb) and a phase, from default_rng([seed, 1])). Generator course j is centred on v = j / courses."""
    rng_ = np.random.default_rng([seed, 1])
    out = []
    for _k in range(courses):
        nb = int(rng_.integers(4, 8))
        w = rng_.uniform(0.6, 1.4, nb)
        cb = np.concatenate([[0.0], np.cumsum(w) / w.sum()])
        ph = float(rng_.random())
        out.append(sorted(((cb[i] - ph) % 1.0) for i in range(nb)))
    return out


def brick_segments():
    """Brick interiors of Masonry_C.png as [(u0, u1, v0, v1)] (uv units; v1 may exceed 1 for the top row: the texture tiles). Two independent sources are intersected: (a) the pixels: the 8 brick
    rows (MAS_ROW) are scanned for vertical mortar columns (darker than the row median by 0.07), each widened by BRICK_PAD_PX; (b) the generator's own joint table (_generator_joints), each joint
    widened by BRICK_PAD_PX + 1 px. A run between joints that is wider than MIN_SEG_PX in both is a brick interior (the pixel scan alone missed 5 of the 46 joints)."""
    global _SEGS, _SEG_TONE
    if _SEGS is not None:
        return _SEGS
    import os
    path = os.path.join(L.LOOK_DIR, "Masonry_C.png")
    had = bpy.data.images.get("Masonry_C.png")
    img = bpy.data.images.load(path, check_existing=True)
    W, H = img.size
    a = np.empty(W * H * 4, np.float32)
    img.pixels.foreach_get(a)
    rgb_ = a.reshape(H, W, 4)[..., :3]
    lum = rgb_.mean(-1)                                              # row 0 = bottom of the image
    moss_ = rgb_[..., 1] - 0.5 * (rgb_[..., 0] + rgb_[..., 2])         # > 0: greener than grey (moss / lichen)
    if had is None:
        bpy.data.images.remove(img)
    tone = []
    joints = _generator_joints()
    segs = []
    h_px = int(MAS_ROW["h"])
    for k in range(8):
        y0 = int(MAS_ROW["y0"] + MAS_ROW["period"] * k + MAS_ROW["pad"])
        prof = lum[[(y0 + j) % H for j in range(h_px)]].mean(0)
        sm = np.convolve(np.r_[prof[-2:], prof, prof[:2]], np.ones(5) / 5.0, mode="valid")
        mort = sm < (np.median(sm) - 0.07)
        for jb in joints[(k + 1) % 8]:                               # my row k (bed + 9 .. bed + 55 px) is generator course k + 1
            c = int(round(jb * W)) % W
            for d_ in range(-2, 3):
                mort[(c + d_) % W] = True
        wide = mort.copy()
        for sft in range(1, BRICK_PAD_PX + 2):
            wide |= np.roll(mort, sft) | np.roll(mort, -sft)
        j = 0
        while j < W:
            if wide[j]:
                j += 1
                continue
            e = j
            while e < W and not wide[e]:
                e += 1
            if e - j >= MIN_SEG_PX and j > 0 and e < W:                  # runs that touch the tile edge wrap around: skipped
                segs.append((j / float(W), e / float(W), y0 / float(H), (y0 + h_px) / float(H)))
                yy = [(y0 + q) % H for q in range(h_px)]
                tone.append((float(lum[yy][:, j:e].mean()), float(moss_[yy][:, j:e].mean())))   # mean luminance (0..1) and moss excess of this brick interior
            j = e
    _SEGS = segs
    _SEG_TONE = tone
    return segs


def _seg_weights():
    """selection weights of the brick interiors: darker bricks (the Game-view stones of the round-2 stage are 1.4x as bright as split.jpg's ruin: low-saturation pixels of the arch crop, mean luminance 134 against 93) and bricks without a moss patch are preferred.
    z-scores over the 8-row table; w = exp(-0.9 z_lum) * exp(-0.8 max(0, z_moss))."""
    brick_segments()
    t = np.array(_SEG_TONE, float)
    zl = (t[:, 0] - t[:, 0].mean()) / max(float(t[:, 0].std()), 1e-6)
    zm = (t[:, 1] - t[:, 1].mean()) / max(float(t[:, 1].std()), 1e-6)
    return list(np.exp(-0.9 * zl) * np.exp(-0.8 * np.maximum(zm, 0.0)))


_W_SEG = None


def pick_seg(rng):
    """index of a brick interior of Masonry_C.png, drawn with the dark / no-moss weights."""
    global _W_SEG
    if _W_SEG is None:
        _W_SEG = _seg_weights()
    return rng.choices(range(len(_W_SEG)), weights=_W_SEG, k=1)[0]


def uv_window(rng, w, h, kmin, kmax, widest=False, pref=None):
    """(u0, v0, k): a window of size (k*w, k*h) uv inside one brick interior, kmin <= k <= kmax (uv per metre). pref = index of the brick the stone prefers (every face of a stone takes
    a window of the SAME brick when it fits, so a stone keeps one tone and neighbouring stones differ); widest: the lintel's long faces take the widest interior at kmin."""
    segs = brick_segments()
    if widest:
        s = max(segs, key=lambda q: (q[1] - q[0]))
        k = kmin
        return rng.uniform(s[0], max(s[0], s[1] - k * w)), rng.uniform(s[2], max(s[2], s[3] - k * h)), k
    order = ([pref] if pref is not None else []) + [pick_seg(rng) for _ in range(60)]
    for idx in order:
        s = segs[idx]
        k = min((s[1] - s[0]) / max(w, 1e-3), (s[3] - s[2]) / max(h, 1e-3), kmax)
        if k < kmin:
            continue
        k = rng.uniform(max(kmin, 0.75 * k), k)
        return rng.uniform(s[0], s[1] - k * w), rng.uniform(s[2], s[3] - k * h), k
    s = max(segs, key=lambda q: (q[1] - q[0]) * (q[3] - q[2]))
    return s[0], s[2], kmin


def _face_uv(rng, pts, widest=False, pref=None):
    """UV of one polygon (list of Vector / xyz): a brick-interior window in the polygon's own plane axes (a standing face: u along the face, v up; a top / bottom face: a random 90-degree turn).
    The polygon is planar (hole09_stone), so this single affine map is the UV of the whole face: no triangle of it can differ from its neighbour."""
    p = np.array([tuple(q) for q in pts], float)
    n = np.zeros(3)
    for i in range(len(p)):
        a, b = p[i], p[(i + 1) % len(p)]
        n += np.array([(a[1] - b[1]) * (a[2] + b[2]), (a[2] - b[2]) * (a[0] + b[0]), (a[0] - b[0]) * (a[1] + b[1])])
    ln = float(np.linalg.norm(n))
    n = n / ln if ln > 1e-12 else np.array([0.0, 0.0, 1.0])
    kd = 1.0 / L.LOOK["LK_MASONRY"][2]
    if abs(n[2]) < 0.75:
        up = np.array([0.0, 0.0, 1.0])
        vax = up - n * float(up @ n)
        vax /= max(float(np.linalg.norm(vax)), 1e-9)
        uax = np.cross(vax, n)
        a, b = p @ uax, p @ vax
        flip = rng.random() < 0.5
    else:
        ang = rng.choice((0.0, 0.5 * math.pi, math.pi, 1.5 * math.pi)) + rng.uniform(-0.25, 0.25)
        c, s = math.cos(ang), math.sin(ang)
        a, b = c * p[:, 0] - s * p[:, 1], s * p[:, 0] + c * p[:, 1]
        flip = rng.random() < 0.5
    w, h = float(a.max() - a.min()), float(b.max() - b.min())
    kmin = (UV_K[0] if abs(n[2]) < 0.75 else UV_K_TOP) * kd                 # a top face may be up to 1.7 m across: it gets a slightly lower density (UV_DENSITY: per-edge floor is 1/4)
    u0, v0, k = uv_window(rng, max(w, 0.04), max(h, 0.04), kmin, UV_K[1] * kd, widest=widest, pref=pref)
    if flip:
        return [(u0 + k * float(a.max() - a[i]), v0 + k * float(b[i] - b.min())) for i in range(len(p))]
    return [(u0 + k * float(a[i] - a.min()), v0 + k * float(b[i] - b.min())) for i in range(len(p))]


# --------------------------------------------------------------------------------------------- Rock_C.png (the LK_ROCK faces: tops and some stones)
_ROCK = None


def _rock_tex():
    """Rock_C.png as (integral image of the luminance over a 2 x 2 tiling, integral image of the moss excess, size)."""
    global _ROCK
    if _ROCK is not None:
        return _ROCK
    import os
    path = os.path.join(L.LOOK_DIR, "Rock_C.png")
    had = bpy.data.images.get("Rock_C.png")
    img = bpy.data.images.load(path, check_existing=True)
    W, H = img.size
    a = np.empty(W * H * 4, np.float32)
    img.pixels.foreach_get(a)
    rgb_ = a.reshape(H, W, 4)[..., :3].astype(np.float64)
    if had is None:
        bpy.data.images.remove(img)
    lum = rgb_.mean(-1)
    moss = rgb_[..., 1] - 0.5 * (rgb_[..., 0] + rgb_[..., 2])
    out = []
    for arr in (lum, moss):
        t = np.tile(arr, (2, 2))
        ii = np.zeros((t.shape[0] + 1, t.shape[1] + 1))
        ii[1:, 1:] = t.cumsum(0).cumsum(1)
        out.append(ii)
    _ROCK = (out[0], out[1], W, H)
    return _ROCK


def _rock_window_mean(u0, v0, wu, hv):
    """(mean luminance, mean moss excess) of the Rock_C window [u0, u0 + wu] x [v0, v0 + hv] (uv units, tiling; a window of a tile or more is the whole texture)."""
    il, im, W, H = _rock_tex()
    if wu >= 0.98 or hv >= 0.98:
        x0, x1, y0, y1 = 0, W, 0, H
    else:
        x0, y0 = int((u0 % 1.0) * W), int((v0 % 1.0) * H)
        x1, y1 = x0 + max(1, int(wu * W)), y0 + max(1, int(hv * H))
    area = float((x1 - x0) * (y1 - y0))
    res = []
    for ii in (il, im):
        res.append((ii[y1, x1] - ii[y0, x1] - ii[y1, x0] + ii[y0, x0]) / area)
    return res[0], res[1]


def rock_offset(rng, w_m, h_m, tile, dark=1.0, moss_ok=False, n=7):
    """(u0, v0) of a Rock_C window of w_m x h_m metres (density 1 / tile): the darkest-leaning of n random candidates (luminance z-score weights, moss penalised unless moss_ok)."""
    cands = []
    for _ in range(n):
        u0, v0 = rng.random(), rng.random()
        cands.append((u0, v0) + _rock_window_mean(u0, v0, w_m / tile, h_m / tile))
    lum = np.array([c[2] for c in cands])
    mo = np.array([c[3] for c in cands])
    zl = (lum - lum.mean()) / max(float(lum.std()), 1e-6)
    zm = (mo - mo.mean()) / max(float(mo.std()), 1e-6)
    w = np.exp(-0.9 * dark * zl) * (1.0 if moss_ok else np.exp(-0.8 * np.maximum(zm, 0.0)))
    i = rng.choices(range(n), weights=list(w), k=1)[0]
    return cands[i][0], cands[i][1]


def _rock_face_uv(pts, n, st):
    """UV of one LK_ROCK polygon: a continuous box projection in WORLD axes (the dominant axis of the face normal n), one density k, one 90-degree turn and one offset per stone `st`, so all faces of
    a stone that lean the same way share ONE continuous mapping (no seam, no triangle split) and a neighbouring stone differs."""
    ax = int(np.argmax(np.abs(n)))
    cols = {2: (0, 1), 0: (1, 2), 1: (0, 2)}[ax]
    tile = L.LOOK["LK_ROCK"][2]
    out = []
    for q in pts:
        a, b = float(q[cols[0]]) * st["k"] / tile, float(q[cols[1]]) * st["k"] / tile
        if st["turn"]:
            a, b = b, a
        out.append((a + st["u0"], b + st["v0"]))
    return out


# --------------------------------------------------------------------------------------------- stone shapes
def _box_corners(rng, sx, sy, sz, top_rag=0.0):
    """8 corners (ix, iy, iz in {0, 1}) of a hand-hewn block: origin = centre of the bottom face, x along the wall, y thickness, z up. A box pushed through a random taper + shear (no two faces
    parallel) with a tilted top plane; top_rag lowers and slopes the top steeply (a broken top: up to ~40 % of the height at one corner)."""
    tx, ty = rng.uniform(-0.04, 0.05), rng.uniform(-0.05, 0.05)
    shx, shy = rng.uniform(-0.05, 0.05) * sz, rng.uniform(-0.05, 0.05) * sz
    twist = rng.uniform(-0.03, 0.03)
    sl_x, sl_y = rng.uniform(-0.06, 0.06), rng.uniform(-0.06, 0.06)
    drop = rng.uniform(0.0, 0.04) + top_rag * rng.uniform(0.15, 0.45)
    rag_x, rag_y = top_rag * rng.uniform(-1.1, 1.1), top_rag * rng.uniform(-0.8, 0.8)
    C = {}
    for ix in (0, 1):
        for iy in (0, 1):
            for iz in (0, 1):
                u, v = (ix - 0.5), (iy - 0.5)
                x, y = u * sx * (1.0 + tx * (iz * 2 - 1)), v * sy * (1.0 + ty * (iz * 2 - 1))
                if iz:
                    c, sn = math.cos(twist), math.sin(twist)
                    x, y = c * x - sn * y, sn * x + c * y
                    x, y = x + shx, y + shy
                    z = sz * (1.0 - drop - (sl_x + rag_x) * u - (sl_y + rag_y) * v)
                    z = max(z, 0.18 * sz)
                else:
                    z = sz * 0.012 * (u + v)                       # the bed is almost flat
                C[(ix, iy, iz)] = np.array([x, y, z])
    return C


def _fracture_planes(rng, C, sx, sy, sz, rag):
    """a ragged, steeply cut top for a broken stone: ONE fracture plane 10-30 deg from horizontal that takes off one side / corner of the top (up to ~28 % of the height removed at the far edge), and in 30 % of the
    stones a second, shallower one (6-14 deg) on another side. Two planes that face each other would make a gable roof (seen on the first round-3 build), so the second plane is turned 70-110 deg from the first."""
    tops = [C[(i, j, 1)] for i in (0, 1) for j in (0, 1)]
    t0 = np.mean(tops, axis=0)
    out = []
    ph0 = rng.uniform(0.0, math.tau)
    for k in range(1 + (1 if rng.random() < 0.30 else 0)):
        al = rng.uniform(0.17, 0.52) if k == 0 else rng.uniform(0.10, 0.25)
        ph = ph0 if k == 0 else ph0 + rng.choice((-1.0, 1.0)) * rng.uniform(math.radians(70), math.radians(110))
        n = np.array([math.sin(al) * math.cos(ph), math.sin(al) * math.sin(ph), math.cos(al)])
        q = t0 + np.array([math.cos(ph) * rng.uniform(0.10, 0.34) * sx, math.sin(ph) * rng.uniform(0.10, 0.34) * sy, -rng.uniform(0.02, 0.22) * sz])
        out.append((n, float(n @ q)))
    return out


def _split_planes(rng, C, sx, sy, sz):
    """a shallow quarry-split on the front or the back face: a plane through a point of the face tilted 5-10 deg (up to ~4 cm deep at the far end) - the face shows two planes at an obtuse angle."""
    out = []
    side = 1 if rng.random() < 0.5 else -1
    be = rng.uniform(0.26, 0.40)                                 # 15-23 deg: a visible fold of the face (a smaller angle would be a near-coplanar pair of faces with different UV windows)
    ps = rng.uniform(0.0, math.tau)
    n = np.array([math.sin(be) * math.cos(ps), side * math.cos(be), math.sin(be) * math.sin(ps)])
    q = np.array([rng.uniform(-0.25, 0.25) * sx, side * 0.5 * sy * rng.uniform(0.84, 0.96), rng.uniform(0.3, 0.7) * sz])
    out.append((n / np.linalg.norm(n), float(n / np.linalg.norm(n) @ q)))
    return out


def _poly_area(pts):
    p = np.array(pts, float)
    c = p.mean(0)
    t = np.zeros(3)
    for k in range(len(p)):
        t += np.cross(p[k] - c, p[(k + 1) % len(p)] - c)
    return 0.5 * float(np.linalg.norm(t))


STONE_V_EDGES = (1.0, (0.08, 0.17))      # larger weathered arrises break the rectangular plan silhouette
STONE_BEVEL_P, STONE_BEVEL_DEPTH, STONE_MAX_FACES = 0.72, (0.026, 0.065), 23
ROCK_MAX = 0.36                # LK_ROCK share of the area of one DRESS_RUIN / DRESS_ARCH mesh (the tops and ~1 stone in 4 are rock; the verifier needs LK_MASONRY >= 50 % per mesh and < 50 % LK_ROCK)


class StoneSet:
    """Collects stones into DRESS_<kind>_<nn>_<pp> meshes of <= max_faces faces: LK_MASONRY (per-face brick-interior UV window of the face's own plane) with LK_ROCK on the tops and on some stones
    (one continuous box projection per stone, darker-leaning window), FLAT shading. Every face is an exactly planar n-gon (hole09_stone)."""

    def __init__(self, D, prefix, seed, clip=None, max_faces=300, rock_share=0.26, rock_tops=True):
        self.D, self.prefix = D, prefix
        self.rng = random.Random(seed)
        self.clip = clip                       # clip(X, Y) -> bool array: every vertex of a stone must pass
        self.base = L._next_name(D, prefix)
        self.mat = L.look_material("LK_MASONRY", D)
        self.mat_rock = L.look_material("LK_ROCK", D)
        self.max_faces = max_faces
        self.rock_share = rock_share
        self.rock_tops = rock_tops
        self.part = 0
        self.objs = []
        self.n_blocks = 0
        self.n_refused = 0
        self.refused = []                      # (x, y) of every refused stone
        self.records = []                      # (kind, bottom z, centre x, y) of every stone (for the report)
        self.tris = 0
        self.n_faces = 0
        self._reset()

    def _reset(self):
        self.V, self.F, self.UV, self.MI = [], [], [], []
        self.ar = [0.0, 0.0]                   # area on LK_MASONRY / LK_ROCK of the mesh being collected: LK_ROCK stays <= ROCK_MAX of it (the verifier calls a mesh with >= 50 % LK_ROCK a ROCK and holds it to the rock-shape
                                               # gates; DRESS_RUIN / DRESS_ARCH must keep >= 50 % LK_MASONRY: PROP_MATERIALS)

    def flush(self):
        if not self.F:
            return
        name = f"{self.base}_{self.part:02d}"
        mats = [self.mat, self.mat_rock] if 1 in self.MI else [self.mat]
        ob = L._new_piece(self.D, name, self.V, self.F, self.UV, list(self.MI), mats, col="STRUCTURES", sharp_deg=32.0)
        ob.data.shade_flat()                   # loop normal == face normal: no diagonal shading steps inside a face
        ob.data.update()
        ob["h9_class"] = "masonry"
        self.objs.append(ob)
        self.part += 1
        self._reset()

    def add_poly(self, P_loc, faces, normals, origin, R, kind="block", widest_uv=False, force_masonry=False, rock_sides=None):
        """Add one stone given as a planar-faced polyhedron (local vertices P_loc (n, 3), faces = vertex id lists ccw seen from outside, their outward normals), placed at origin (world, m) with the 3x3
        rotation R. Returns True when it was kept (every vertex passes the clip)."""
        W = [Vector(origin) + R @ Vector(tuple(v)) for v in P_loc]
        if self.clip is not None:
            xy = np.array([(w.x, w.y) for w in W])
            if not bool(np.all(self.clip(xy[:, 0], xy[:, 1]))):
                self.n_refused += 1
                self.refused.append((float(origin[0]), float(origin[1])))
                return False
        if len(self.F) + len(faces) > self.max_faces:
            self.flush()
        rng = self.rng
        base = len(self.V)
        self.V.extend((float(w.x), float(w.y), float(w.z)) for w in W)
        pref = pick_seg(rng)
        if rock_sides is None:
            rock_sides = (not force_masonry) and (rng.random() < self.rock_share)
        wp = np.array([tuple(w) for w in W])
        ext = wp.max(axis=0) - wp.min(axis=0)
        tile = L.LOOK["LK_ROCK"][2]
        st = dict(k=rng.uniform(0.85, 1.45), turn=rng.random() < 0.5)
        st["u0"], st["v0"] = rock_offset(rng, float(max(ext[0], ext[1])), float(max(ext[1], ext[2])), tile, moss_ok=(kind not in ("lintel", "voussoir") and rng.random() < 0.35))
        nws = [np.array(tuple(R @ Vector(tuple(nl)))) for nl in normals]
        ars = [_poly_area([tuple(W[i]) for i in f]) for f in faces]
        a_top = sum(a for a, nw in zip(ars, nws) if nw[2] > 0.70)
        a_side = sum(a for a, nw in zip(ars, nws) if -0.6 < nw[2] <= 0.70)
        tops_rock = self.rock_tops and not force_masonry
        tot_ = self.ar[0] + self.ar[1] + a_top + a_side
        if rock_sides and (self.ar[1] + (a_top if tops_rock else 0.0) + a_side) > ROCK_MAX * tot_:
            rock_sides = False
        if tops_rock and (self.ar[1] + a_top) > (ROCK_MAX + 0.06) * tot_:
            tops_rock = False
        for f, nw, a_ in zip(faces, nws, ars):
            pts = [W[i] for i in f]
            top = nw[2] > 0.70
            use_rock = (top and tops_rock) or (rock_sides and -0.6 < nw[2] <= 0.70)
            self.F.append(tuple(base + i for i in f))
            if use_rock:
                self.UV.append(_rock_face_uv([tuple(p_) for p_ in pts], nw, st))
                self.MI.append(1)
                self.ar[1] += a_
            else:
                self.UV.append(_face_uv(rng, pts, widest=widest_uv, pref=pref))
                self.MI.append(0)
                self.ar[0] += a_
            self.tris += max(1, len(f) - 2)
        self.n_faces += len(faces)
        self.n_blocks += 1
        self.records.append((kind, float(min(w.z for w in W)), float(origin[0]), float(origin[1])))
        return True

    def stone(self, origin, yaw, sx, sy, sz, top_rag=0.0, chamfer=0.35, tilt=0.015, kind="block", bulge_faces=(), widest_uv=False, top_chip=None, chip_scale=1.0, split_p=0.30):
        """One hand-hewn stone (hole09_stone.build_stone): `chamfer` = the old chip probability knob (mapped to 0.35 + 0.7 x chamfer per corner), `chip_scale` scales the corner chips, `top_chip` caps the chips of
        the top corners (a stone that carries another one), `split_p` = chance of a quarry-split on the front or back face (not on stones that carry another)."""
        rng = self.rng
        R = Matrix.Rotation(yaw, 3, 'Z') @ Matrix.Rotation(rng.uniform(-tilt, tilt), 3, 'X') @ Matrix.Rotation(rng.uniform(-tilt, tilt), 3, 'Y')
        C = _box_corners(rng, sx, sy, sz, top_rag)
        extra = []
        if top_rag > 0.0:
            extra += _fracture_planes(rng, C, sx, sy, sz, top_rag)
        if top_chip is None and rng.random() < split_p and min(sx, sz) > 0.3:
            extra += _split_planes(rng, C, sx, sy, sz)
        res = ST.build_stone(rng, C, chip_p=min(0.95, 0.35 + 0.7 * chamfer), chip_leg=(0.08 * chip_scale, 0.24 * chip_scale), top_chip=top_chip, bevel_p=STONE_BEVEL_P, bevel_depth=STONE_BEVEL_DEPTH, extra_planes=extra, top_axis=2, max_faces=STONE_MAX_FACES, v_edges=(None if kind in ('lintel',) else STONE_V_EDGES), check_side=True, min_cuts=2,
                             corner_legs={(i_, j_, 0): (0.05 * chip_scale, 0.17 * chip_scale) for i_ in (0, 1) for j_ in (0, 1)})
        if res is None:
            return False
        P_loc, faces, normals = res
        return self.add_poly(P_loc, faces, normals, origin, R, kind, widest_uv, force_masonry=(kind in ("lintel", "voussoir")))


# --------------------------------------------------------------------------------------------- walls (skyline rubble)
def make_profile(rng, total, hmax_of_s, door=None):
    """Broken height profile h(s) (m above the ground) along the path of length `total`: random control points every ~1.9 m (full stretches, stubs, collapsed gaps),
    low at both free ends, never lower than `door[2]` within door[1]/2 + 0.6 m of the doorway axis door[0] (then stepping down over 2.4 m)."""
    n = max(5, int(total / 1.9))
    vals = []
    for _ in range(n + 1):
        r = rng.random()
        vals.append(rng.uniform(0.80, 1.0) if r < 0.35 else rng.uniform(0.46, 0.76) if r < 0.65 else rng.uniform(0.2, 0.4) if r < 0.88 else rng.uniform(0.0, 0.14))
    vals[0] = min(vals[0], 0.5)
    vals[-1] = min(vals[-1], 0.5)

    def h(s):
        f = min(max(s / total, 0.0), 1.0) * n
        i = min(int(f), n - 1)
        t = f - i
        v = vals[i] * (1 - t) + vals[i + 1] * t
        out = v * hmax_of_s(s)
        if door is not None:
            k = min(1.0, max(0.0, 1.0 - (abs(s - door[0]) - (door[1] / 2 + 0.6)) / 2.4))
            out = max(out, door[2] * k)
        return out
    return h


def _draw_len(rng, block_l):
    r = rng.random()
    if r < 0.11:
        return rng.uniform(*LONG_L)
    if r < 0.42:
        return rng.uniform(*SHORT_L)
    return rng.uniform(*block_l)


def lay_wall(ss, rng, a, d, ln, T, ground, prof, s_lo, s_hi, door=None, block_l=BLOCK_L, end_jitter=0.0, tag=0, twin_p=0.0):
    """Skyline-laid rubble wall along the segment a + d*s (s in [s_lo, s_hi], a = start point (world m), d = unit direction, ln = segment length). prof(s) = broken profile (m above the ground,
    s from a). door = (s_door, width, top z (world, m)) or None. Stones are laid on the LOWEST open run of the wall: its bed rests on the highest cell under it (hollow <= TOL), its height is drawn
    per stone (so beds are not ruled courses), the last stone of a column is cut to the profile with a steep sloped top. Returns dict(stones=[(s0, s1, zbot, ztop)], lintel, jamb, drops, ...)."""
    nrm = np.array([-d[1], d[0]])
    yaw0 = math.atan2(d[1], d[0])
    n = max(2, int(round((s_hi - s_lo) / CELL)))
    sc = s_lo + (np.arange(n) + 0.5) * CELL
    gx, gy = a[0] + d[0] * sc, a[1] + d[1] * sc
    g = np.asarray(ground(gx, gy), float)
    if not np.isfinite(g).all():
        g = np.where(np.isfinite(g), g, float(np.nanmean(g)))
    target = g + np.array([prof(float(min(max(s, 0.0), ln))) for s in sc])
    sky = g.copy()
    is_gr = np.ones(n, bool)
    closed = np.zeros(n, bool)
    joint = np.zeros(n, bool)
    stones, drops = [], []
    out = dict(stones=stones, drops=drops, lintel=0, jamb=None)

    def cells(s0, s1):
        return max(0, int(round((s0 - s_lo) / CELL))), min(n, int(round((s1 - s_lo) / CELL)))

    def put(s0, s1, zb, h, depth, dy, rag, kind="wall", chamfer=0.35, tilt=0.015, yaw_j=0.025):
        """One stone over [s0, s1] (along the wall, joints included): returns True when it was made."""
        sm = 0.5 * (s0 + s1)
        cpos = np.array(a) + d * sm + nrm * dy
        ok = ss.stone((cpos[0], cpos[1], zb), yaw0 + rng.uniform(-yaw_j, yaw_j), (s1 - s0) - 2 * GAP, depth, max(MIN_STONE_H, h), top_rag=rag, chamfer=chamfer, tilt=tilt, kind=kind,
                      widest_uv=(kind == "lintel"), top_chip=(None if rag > 0.0 else TOP_CHIP))
        if ok:
            c0, c1 = cells(s0, s1)
            sky[c0:c1] = zb + max(MIN_STONE_H, h) * 0.97          # a little below the nominal top: the next bed interpenetrates the real (tilted, chipped) top instead of floating over it
            is_gr[c0:c1] = False
            joint[c0:c1] = False
            joint[c0] = True
            joint[max(c0, c1 - 1)] = True
            stones.append((s0, s1, zb, zb + h))
        return ok

    # ---------------------------------------------------------------- phase 1: the doorway (quoin rows beside the opening, then ONE lintel stone)
    if door is not None:
        sd, w, z_door = door
        e_l, e_r = sd - w / 2, sd + w / 2
        c0, c1 = cells(e_l, e_r)
        closed[c0:c1] = True                                           # the opening stays clear below the lintel
        sky[c0:c1] = z_door
        is_gr[c0:c1] = False
        rows_zb = None
        covered = {-1: e_l, 1: e_r}
        for side in (-1, 1):
            edge = e_l if side < 0 else e_r
            far = edge + side * 1.9
            ca, cb = cells(min(edge, far), max(edge, far))
            gmax = float(g[ca:cb].max()) if cb > ca else float(g[min(max(c0, 0), n - 1)])
            top0 = gmax + rng.uniform(*FOUND_ABOVE)
            nrow = max(3, int(round((z_door - top0) / 0.47)))
            wts = [rng.uniform(0.85, 1.15) for _ in range(nrow)]
            span = z_door - top0
            bounds = [top0]
            for x_ in wts:
                bounds.append(bounds[-1] + span * x_ / sum(wts))
            bounds[-1] = z_door
            x_prev = None
            for k in range(-1, nrow):                                  # row -1 = the foundation row (buried), row k >= 0 = bounds[k] .. bounds[k + 1]
                # every row ends at or before the end of the row below (its last stone always rests on it: no cantilevered stone) and at least JAMB_ZONE from the door edge; the outer ends differ row to row (no ruler joint line)
                X = rng.uniform(1.5, 2.1) if x_prev is None else min(max(rng.uniform(x_prev - 0.6, x_prev), JAMB_ZONE), x_prev)
                first = min(rng.uniform(1.0, 1.3) if (k % 2 == 0) else rng.uniform(0.8, 1.0), X)
                lens, x_ = [first], first
                while x_ < X - 0.45:
                    ln_ = min(rng.uniform(*block_l), X - x_)
                    if X - x_ - ln_ < 0.45:
                        ln_ = X - x_
                    lens.append(ln_)
                    x_ += ln_
                x_prev = x_
                pos = edge
                for ln_ in lens:
                    s_a, s_b = (pos, pos + side * ln_) if side > 0 else (pos - ln_, pos)
                    s_a, s_b = max(s_a, s_lo), min(s_b, s_hi)
                    pos += side * ln_
                    if s_b - s_a < 0.3:
                        continue
                    ia, ib = cells(s_a, s_b)
                    if k < 0:
                        gmin_ = float(g[ia:ib].min())
                        zb = gmin_ - rng.uniform(*BURY)
                        top = bounds[0] - rng.uniform(0.0, 0.03)
                    else:
                        zb = bounds[k] - 0.02
                        top = bounds[k + 1] - rng.uniform(0.0, 0.02)
                    ok = put(s_a, s_b, zb, top - zb, T * rng.uniform(0.95, 1.0), rng.uniform(-0.02, 0.02), 0.0, kind="jamb", chamfer=0.30, tilt=0.008, yaw_j=0.012)
                    if side > 0 and ok:
                        covered[1] = max(covered[1], s_b)
                    if side < 0 and ok:
                        covered[-1] = min(covered[-1], s_a)
        l0, l1 = max(s_lo, e_l - LINTEL_BEARING), min(s_hi, e_r + LINTEL_BEARING)
        ia, ib = cells(l0 + 0.25, e_l)
        ja, jb = cells(e_r, l1 - 0.25)
        jam_ok = bool(ib > ia and jb > ja and (sky[ia:ib] >= z_door - 0.04).all() and (sky[ja:jb] >= z_door - 0.04).all())
        out["jamb"] = (jam_ok, round(l0, 2), round(l1, 2), [round(covered[-1], 2), round(covered[1], 2)])
        if jam_ok:
            lh = rng.uniform(*LINTEL_H)
            if put(l0, l1, z_door + 0.006, lh, T * rng.uniform(0.96, 1.0), 0.0, 0.0, kind="lintel", chamfer=0.25, tilt=0.006, yaw_j=0.01):
                out["lintel"] = 1
                ia, ib = cells(l0, l1)
                sky[ia:ib] = z_door + lh
                closed[c0:c1] = False                                   # the wall continues over the lintel
        out["door_s"] = sd

    # ---------------------------------------------------------------- phase 2: skyline fill up to the broken profile
    # bed levels of this wall: level k is a WAVY line (amplitude 2-6 cm, wavelength 5-11 m) with one JOG (7-12 cm step) at a random place for 70 % of the levels, spaced 27-44 cm apart.
    # A stone's top lands on one of them (+-1.5 cm) and a stone may span 2 or 3 levels, so neighbouring stones of a course share a bed line within the run tolerance (a running bond is possible)
    # but no bed is a ruler line and no two courses are alike.
    gref = float(np.median(g))
    lev = []
    Bk = gref + rng.uniform(0.16, 0.30)
    for _ in range(16):
        lev.append(dict(B=Bk, amp=rng.uniform(*LEVEL_WAVE), lam=rng.uniform(7.0, 14.0), ph=rng.uniform(0.0, 2 * math.pi), sp=rng.uniform(s_lo, s_hi),
                        sa=(rng.choice((-1.0, 1.0)) * rng.uniform(*LEVEL_JOG)) if rng.random() < 0.7 else 0.0))
        Bk += rng.uniform(*LEVEL_STEP)

    def level(k, s):
        q = lev[k]
        return q["B"] + q["amp"] * math.sin(2 * math.pi * s / q["lam"] + q["ph"]) + (q["sa"] if s > q["sp"] else 0.0)
    open_ = (~closed) & ((target - sky) >= MIN_STACK)
    guard = 0
    while open_.any() and guard < 6000:
        guard += 1
        idx = np.flatnonzero(open_)
        m = float(sky[idx].min())
        i = int(idx[np.flatnonzero(sky[idx] <= m + 0.03)[0]])
        gr = bool(is_gr[i])
        j0 = j1 = i
        while j0 > 0 and open_[j0 - 1] and bool(is_gr[j0 - 1]) == gr and (gr or sky[j0 - 1] <= m + TOL + 0.03):
            j0 -= 1
        while j1 < n - 1 and open_[j1 + 1] and bool(is_gr[j1 + 1]) == gr and (gr or sky[j1 + 1] <= m + TOL + 0.03):
            j1 += 1
        wr = (j1 - j0 + 1) * CELL
        if wr < 0.2:                                                    # a crack between two stones: left open (nothing fits)
            closed[j0:j1 + 1] = True
            open_[j0:j1 + 1] = False
            drops.append(("crack", round(s_lo + j0 * CELL, 2), round(wr, 2)))
            continue
        best = None
        for _ in range(8):
            wl = _draw_len(rng, block_l)
            if wr - wl < 0.45 or wl > wr:
                wl = wr
            elif wr > 2.4 and wl > wr - 0.45:
                wl = wr - 0.45
            side_left = (j0 == 0) or (j1 < n - 1 and rng.random() < 0.5)
            x0 = (s_lo + j0 * CELL) if side_left else (s_lo + (j1 + 1) * CELL - wl)
            c_a, c_b = cells(x0, x0 + wl)
            pen = 0
            if not gr:
                for cj in (c_a, c_b - 1):
                    lo_, hi_ = max(0, cj - int(TIGHT_JOINT / CELL)), min(n, cj + int(TIGHT_JOINT / CELL) + 1)
                    pen += int(joint[lo_:hi_].any())
            cand = (pen, rng.random(), x0, wl)
            if best is None or cand[:2] < best[:2]:
                best = cand
        _, _, x0, wl = best
        c_a, c_b = cells(x0, x0 + wl)
        c_b = max(c_b, c_a + 1)
        s_mid = x0 + 0.5 * wl
        rag = 0.0
        if gr:
            gmin, gmax = float(g[c_a:c_b].min()), float(g[c_a:c_b].max())
            zb = gmin - rng.uniform(*BURY)
            top = max(level(0, s_mid) + rng.uniform(-0.015, 0.015), gmax + 0.12)
            rem = float(target[c_a:c_b].mean()) - gmax
            if rem < top - gmax:                                       # the broken profile ends inside the foundation row
                top = gmax + max(0.12, rem * rng.uniform(0.9, 1.1))
            h = top - zb
        else:
            zb = float(sky[c_a:c_b].max()) - SINK
            rem = float(target[c_a:c_b].mean()) - zb
            if rem < MIN_STACK:
                closed[c_a:c_b] = True
                open_[c_a:c_b] = False
                drops.append(("profile", round(x0, 2), round(wl, 2)))
                continue
            k_ = 1
            while k_ < len(lev) - 1 and level(k_, s_mid) - zb < 0.26:
                k_ += 1
            r_ = rng.random()
            span_ = 1 if r_ < 0.60 else 2 if r_ < 0.92 else 3
            kk_ = min(len(lev) - 1, k_ + span_ - 1)
            while kk_ > k_ and level(kk_, s_mid) - zb > 0.85:
                kk_ -= 1
            h = level(kk_, s_mid) + rng.uniform(-0.015, 0.015) - zb
            if h > rem - 0.02 or rem - h < MIN_STACK:                  # the top of the column: cut to the broken profile (a steeply sloped, chipped top)
                h = min(0.80, max(0.25, rem * rng.uniform(0.80, 1.10)))
                rag = rng.uniform(0.40, 0.85)
        x1 = x0 + wl
        ej = end_jitter
        if ej > 0.0:                                                    # ragged wall / pier ends: the end stones stick out or stand back
            if j0 == 0 and x0 <= s_lo + 1e-6:
                x0 -= rng.uniform(-ej, ej)
            if j1 == n - 1 and x1 >= s_hi - 1e-6:
                x1 += rng.uniform(-ej, ej)
        depth = T * rng.uniform(0.78, 1.0)                              # >= 0.78 of the wall (grass pass: 0.82 -> 0.78, round 3: was 0.86, more relief in the wall face): two stones on top of each other always share >= 64 % of their depth
        dy = rng.uniform(-1.0, 1.0) * (T - depth) * 0.5 + rng.uniform(-0.03, 0.03)
        if twin_p > 0.0 and (x1 - x0) >= 0.5 and rng.random() < twin_p:
            # round 3 (the piers of the arch seen end-on were stacks of thin full-depth slabs): TWIN stones side by side across the thickness (each 0.42-0.50 T deep, bed shared, tops within 5 cm), so no
            # joint plane runs through the whole thickness in this course and the end face of the wall shows two columns of stones. The taller twin is laid first: the skyline keeps the lower top.
            tw = []
            for sg in (-1, 1):
                dd = T * rng.uniform(0.42, 0.50)
                tw.append((h + rng.uniform(-0.025, 0.025), dd, sg * (T * 0.5 - dd * 0.5) + rng.uniform(-0.02, 0.02)))
            tw.sort(key=lambda q: -q[0])
            made = [put(x0, x1 + rng.uniform(-0.04, 0.04), zb, hh, dd, dyy, rag) for hh, dd, dyy in tw]
            ok_put = all(made)
        else:
            ok_put = put(x0, x1, zb, h, depth, dy, rag)
        if not ok_put:
            closed[c_a:c_b] = True
            open_[c_a:c_b] = False
            drops.append(("clip", round(x0, 2), round(wl, 2)))
            continue
        if rag > 0.0:                                                   # a broken top: nothing is ever laid on it (its real top is tilted / chipped far below the nominal one)
            closed[c_a:c_b] = True
        open_[c_a:c_b] = (~closed[c_a:c_b]) & ((target[c_a:c_b] - sky[c_a:c_b]) >= MIN_STACK)
    out["guard"] = guard
    out["levels"] = len(lev)
    return out


def build_wall_path(ss, pts, seg_h, T, door, seed, ground, G0, block_l=BLOCK_L, end_jitter=0.05):
    """Rubble wall along the polyline pts (world metres): segment i = pts[i] -> pts[i + 1], seg_h[i] = max height of the segment (m above the ground), T = wall thickness, door = (segment index,
    centre s along that segment, clear width, clear height above G0) or None. The corners are butt joints: the earlier segment runs T/2 past the corner point, the next one starts T/2 after it.
    Returns dict(blocks, joints, tight, lintels, jamb, door_world, per_course, drops)."""
    rng = random.Random(seed)
    segs = []
    for i in range(len(pts) - 1):
        a, b = np.array(pts[i], float), np.array(pts[i + 1], float)
        ln = float(np.hypot(*(b - a)))
        segs.append((a, b, ln, (b - a) / ln))
    s0g, tot = [], 0.0
    for sg in segs:
        s0g.append(tot)
        tot += sg[2]
    door_g = None if door is None else (s0g[door[0]] + door[1], door[2], door[3] + 0.58 + 0.60)

    def hmax_of_s(s):
        i = max(k for k in range(len(segs)) if s0g[k] <= s + 1e-9)
        return seg_h[i]
    prof = make_profile(rng, tot, hmax_of_s, door=door_g)
    stat = dict(blocks=0, joints=0, tight=0, lintels=0, per_course={}, drops=[])
    all_st = []
    for i, (a, b, ln, d) in enumerate(segs):
        s_lo = 0.0 if i == 0 else 0.5 * T
        s_hi = ln + 0.5 * T if i < len(segs) - 1 else ln
        dr = None if (door is None or door[0] != i) else (door[1], door[2], G0 + door[3])
        r = lay_wall(ss, rng, a, d, ln, T, ground, lambda s, i=i: prof(s0g[i] + min(max(s, 0.0), ln)), s_lo, s_hi, door=dr, block_l=block_l, end_jitter=end_jitter if i in (0, len(segs) - 1) else 0.0)
        st = r["stones"]
        stat["blocks"] += len(st)
        stat["lintels"] += r["lintel"]
        stat["per_course"][i] = len(st)
        stat["drops"] += [(i,) + tuple(x_) for x_ in r["drops"]]
        if r["jamb"] is not None:
            stat["jamb"] = r["jamb"]
        all_st.append(st)
        for k, (s0, s1, zb, zt) in enumerate(st):                       # joints within TIGHT_JOINT of a joint of a stone directly below
            for (c0, c1, zb2, zt2) in st:
                if abs(zt2 - zb) < 0.08 and c0 < s1 and c1 > s0:
                    for x in (s0, s1):
                        if c0 + 0.0 < x < c1 - 0.0:
                            stat["tight"] += int(abs(x - c0) < TIGHT_JOINT or abs(x - c1) < TIGHT_JOINT)
            stat["joints"] += 2
    if door is not None:
        a, b, ln, d = segs[door[0]]
        stat["door_world"] = tuple(a + d * door[1])
    stat["segments"] = len(segs)
    return stat


# --------------------------------------------------------------------------------------------- arch
def _wedge_corners(rng, th0, th1, r_in, r_out, spring, dy, jit=0.012):
    """corners {(ia, iy, ir)} of a ring stone between the angles th0 < th1 (rad, measured from the left springer, x = -R cos th, z = spring + R sin th) and the radii r_in .. r_out."""
    C = {}
    for ia, th in ((0, th0), (1, th1)):
        for iy in (0, 1):
            for ir, rad in ((0, r_in), (1, r_out)):
                rr_ = rad + rng.uniform(-jit, jit)
                th_j = th + rng.uniform(-0.004, 0.004)
                C[(ia, iy, ir)] = np.array([-rr_ * math.cos(th_j), (iy - 0.5) * dy * (1.0 + rng.uniform(-0.04, 0.04)), spring + rr_ * math.sin(th_j)])
    return C


def build_arch(ss, center, phi_deg, ground, G0, r=2.0, t=0.75, depth=1.3, pier_w=1.7, spring=2.7, left_top=5.8, right_top=4.9, seed=0, missing=(6,), outer=True, slip=(), slip_m=0.13):
    """Broken masonry arch: two rubble piers + a ring of 11 wedge voussoirs + (repair round 3) an outer course of rubble stones laid on the haunches of the ring. center = (x, y) midway between the piers
    (world, m); phi_deg = the direction you look THROUGH the opening (clockwise from +Y, the postcard_lib build_arch convention). The wedges in `missing` (6, just right of the crown) lie in a rubble heap
    under the gap; the left ring (0..5) rings up to the crown from the tall left pier, the right springers (7..10) are seated IN the right pier + the haunch stones that fill the angle between the ring and
    the pier top, so no ring stone hangs over a visible hole (round 2: a cantilevered crown block over a gap, the review's 'floating crown'). Returns dict(missing, height, centre, heap, ...)."""
    rng = random.Random(seed)
    phi = math.radians(phi_deg)
    xl = np.array([math.cos(phi), -math.sin(phi)])            # across the opening (local x)
    yl = np.array([math.sin(phi), math.cos(phi)])             # through the opening (local y)
    c0 = np.array(center, float)
    st_piers = []
    for side, top in ((-1, left_top), (1, right_top)):
        a = c0 + xl * side * (r + pier_w)
        b = c0 + xl * side * r
        p0, p1 = (a, b) if side < 0 else (b, a)
        dd = (p1 - p0) / np.hypot(*(p1 - p0))
        ph = rng.uniform(0, 6.0)
        # the pier is a short wall across the opening, `depth` thick; its broken top swings +-18 % (never below the springer on the opening side)
        prof = (lambda s_, top=top, ph=ph: top * (0.82 + 0.18 * math.sin(2.1 * s_ + ph)))
        rr = lay_wall(ss, rng, p0, dd, pier_w, depth, ground, prof, 0.0, pier_w, door=None, block_l=(0.45, 1.0), end_jitter=0.07, twin_p=0.38)
        st_piers.append(len(rr["stones"]))
    n = 11
    Rl = Matrix.Rotation(-phi, 3, 'Z')
    origin = (c0[0], c0[1], G0)
    fallen = []
    for i in range(n):
        th0 = i * math.pi / n + 0.006
        th1 = (i + 1) * math.pi / n - 0.006
        dy = depth * rng.uniform(0.9, 1.0)
        C = _wedge_corners(rng, th0, th1, r - 0.03, r + t, spring, dy)
        free_l, free_r = (i - 1) in missing, (i + 1) in missing                       # a side that faces the gap may be chipped hard; a side that touches the next wedge keeps its joint corners (<= 3.5 cm chips): the ring bears by face contact
        cl = {(ia, iy, ir): ((0.05, 0.12) if ((ia == 0 and free_l) or (ia == 1 and free_r)) else (0.012, 0.035)) for ia in (0, 1) for iy in (0, 1) for ir in (0, 1)}
        if i in slip:                                                                   # grass pass: the crown wedge SLIPPED: it keeps its side planes (a radial slide stays in face contact with both neighbours) and has dropped slip_m toward the opening,
            cl = {(ia, iy, ir): ((0.04, 0.10) if ir == 0 else (0.012, 0.035)) for ia in (0, 1) for iy in (0, 1) for ir in (0, 1)}      # its inner corners knocked off by the fall
        res = ST.build_stone(rng, C, chip_p=0.80, chip_leg=(0.012, 0.035), bevel_p=0.30, top_axis=None, max_faces=15, corner_legs=cl)
        if res is None:
            continue
        P_loc, faces, normals = res
        if i in missing:
            fallen.append((i, P_loc, faces, normals))
        else:
            if i in slip:
                thm = 0.5 * (th0 + th1)
                P_loc = np.asarray(P_loc, float) - np.array([-math.cos(thm), 0.0, math.sin(thm)]) * slip_m
            ss.add_poly(P_loc, faces, normals, origin, Rl, kind="voussoir", rock_sides=False)
    # outer course: irregular stones on the haunches of the ring (left 14..76 deg, right 112..166 deg), radial thickness 0.45-0.8 m, joints not in line with the ring's: they bear on the ring below
    n_outer = 0
    if outer:
        for (a0, a1) in ((math.radians(14.0), math.radians(78.0)), (math.radians(112.0), math.radians(166.0))):
            th = a0 + rng.uniform(0.0, 0.05)
            while th < a1 - 0.10:
                span = rng.uniform(0.17, 0.30)
                th_e = min(a1, th + span)
                if a1 - th_e < 0.12:
                    th_e = a1
                rad_o = r + t + rng.uniform(0.45, 0.80)
                dy = depth * rng.uniform(0.86, 1.0)
                C = _wedge_corners(rng, th + 0.004, th_e - 0.004, r + t - 0.02, rad_o, spring, dy, jit=0.02)
                res = ST.build_stone(rng, C, chip_p=0.7, chip_leg=(0.05, 0.16), bevel_p=0.35, top_axis=None, max_faces=15)
                if res is not None:
                    P_loc, faces, normals = res
                    if ss.add_poly(P_loc, faces, normals, origin, Rl, kind="haunch"):
                        n_outer += 1
                th = th_e
    # the fallen crown wedge: lies in the heap under the gap, tilted, half buried
    heap, first = [], []
    for k, (i, P_loc, faces, normals) in enumerate(fallen):
        mid = np.mean(P_loc, axis=0)
        loc = P_loc - mid
        rel = np.array([rng.uniform(-0.55, 0.55), rng.uniform(-0.7, -0.2) if k == 0 else rng.uniform(0.2, 0.7)])
        posx = c0 + xl * rel[0] + yl * rel[1]
        Rf = Matrix.Rotation(rng.uniform(0, math.tau), 3, 'Z') @ Matrix.Rotation(rng.uniform(-0.45, 0.45), 3, 'X') @ Matrix.Rotation(rng.uniform(-0.45, 0.45), 3, 'Y')
        gz = float(ground(np.array([posx[0]]), np.array([posx[1]]))[0])
        ss.add_poly(loc, faces, normals, (posx[0], posx[1], gz + 0.16), Rf, kind="fallen", rock_sides=False)
    # a heap of small stones round the fallen wedge (under the broken crown): a half-buried first layer, then a few stones resting on it
    for k in range(9):
        sx = rng.uniform(0.45, 0.85)
        ang = rng.uniform(0, math.tau)
        rad_ = rng.uniform(0.2, 1.5)
        rel = np.array([math.cos(ang) * rad_ * 1.1, math.sin(ang) * rad_ * 0.5])
        p = c0 + xl * rel[0] + yl * rel[1]
        gz = float(ground(np.array([p[0]]), np.array([p[1]]))[0])
        sz_h = sx * rng.uniform(0.55, 0.8)
        if ss.stone((p[0], p[1], gz - 0.14), rng.uniform(0, math.tau), sx, sx * rng.uniform(0.6, 1.0), sz_h, top_rag=0.0, chamfer=0.6, tilt=0.06, kind="rubble", split_p=0.0, chip_scale=1.4):
            first.append((float(p[0]), float(p[1]), gz - 0.14 + sz_h * 0.95))
            heap.append((float(p[0]), float(p[1])))
    for (px, py, tz) in first[:4]:
        sx = rng.uniform(0.32, 0.5)
        ss.stone((px + rng.uniform(-0.1, 0.1), py + rng.uniform(-0.1, 0.1), tz - 0.05), rng.uniform(0, math.tau), sx, sx * rng.uniform(0.6, 1.0), sx * rng.uniform(0.5, 0.75),
                 top_rag=0.0, chamfer=0.6, tilt=0.06, kind="rubble", split_p=0.0, chip_scale=1.4)
    return dict(missing=len(fallen), height=max(left_top, right_top), center=tuple(c0), phi=phi_deg, heap=heap, pier_stones=st_piers, outer_stones=n_outer)


# --------------------------------------------------------------------------------------------- rubble round the wall feet
def scatter_rubble(ss, pts, T, ground, keep_out, seed, n_per_100m=34.0, along_clear=None):
    """Small irregular stones (0.28-0.65 m) 0.15-1.5 m off the wall faces, half buried. keep_out(x, y) -> True where nothing may lie (the doorway)."""
    rng = random.Random(seed)
    n_made = 0
    for i in range(len(pts) - 1):
        a, b = np.array(pts[i], float), np.array(pts[i + 1], float)
        ln = float(np.hypot(*(b - a)))
        d = (b - a) / ln
        nrm = np.array([-d[1], d[0]])
        for _ in range(max(1, int(round(ln * n_per_100m / 100.0 * 2.2)))):
            s = rng.uniform(0.2, ln - 0.2)
            side = rng.choice((-1, 1))
            off = side * (T * 0.5 + rng.uniform(0.15, 1.5))
            p = a + d * s + nrm * off
            if keep_out(p[0], p[1]):
                continue
            sx = rng.uniform(0.28, 0.65)
            gz = float(ground(np.array([p[0]]), np.array([p[1]]))[0])
            if ss.stone((p[0], p[1], gz - 0.10), rng.uniform(0, math.tau), sx, sx * rng.uniform(0.6, 1.0), sx * rng.uniform(0.45, 0.8), top_rag=0.1, chamfer=0.6, tilt=0.12, kind="rubble",
                        bulge_faces=("top",) if sx > 0.4 else ()):
                n_made += 1
    return n_made


# --------------------------------------------------------------------------------------------- DRESS_BLOCK variants (fallen blocks)
def make_block_variant(name, seed, dims=(1.1, 0.6, 0.5), grid=(3, 2, 2), p_norm=5.0):
    """A fallen cut block as a mesh: a rounded (superellipsoid-ish), subdivided, noise-displaced box (>= 64 welded vertices), flat shaded, one continuous LK_ROCK box projection (repair round 3; the caller gives it the LK_ROCK material). The mesh
    stands on z = 0 (origin at the centre of the bottom, like the library DRESS_BLOCK_x). Returns the bpy mesh (no object)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=max(grid), use_grid_fill=True)
    sx, sy, sz = dims
    # rounded cube: pull the corners / edges in along the diagonal (p-norm), then non-uniform scale, taper, shear, noise
    tx, ty = rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05)
    shx, shy = rng.uniform(-0.04, 0.04) * sz, rng.uniform(-0.04, 0.04) * sz
    for v in bm.verts:
        x, y, z = v.co
        nrm = (abs(x) ** p_norm + abs(y) ** p_norm + abs(z) ** p_norm) ** (1.0 / p_norm)
        k = 1.0 / max(nrm, 1e-6)
        x, y, z = x * k, y * k, z * k
        zt = (z + 1.0) * 0.5
        X = x * 0.5 * sx * (1.0 + tx * (zt * 2 - 1)) + shx * zt
        Y = y * 0.5 * sy * (1.0 + ty * (zt * 2 - 1)) + shy * zt
        Z = zt * sz
        v.co = Vector((X, Y, Z))
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * rng.uniform(-0.010, 0.010)
    bm.verts.ensure_lookup_table()
    # flat bed: the lowest ring is flattened onto z = 0
    for v in bm.verts:
        if v.co.z < 0.04 * sz:
            v.co.z = 0.0
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    uv_layer = bm.loops.layers.uv.verify()
    # repair round 3: a CONTINUOUS box projection per block. Round 2 gave every one of the block's ~190 triangles its own random brick window: a patchwork whose seams ran along the triangulation (the
    # 'triangle shear' again, at small scale). Now each of the three projection axes (top x-y, front x-z, side y-z) has ONE window inside ONE brick interior of Masonry_C.png (the block's preferred brick
    # when it fits), so a triangle can never differ from its neighbour inside a region and no mortar line crosses the block.
    kd = 1.0 / L.LOOK["LK_MASONRY"][2]
    pref = pick_seg(rng)
    win = {}
    for ax, (wa, ha) in {2: (dims[0], dims[1]), 1: (dims[0], dims[2]), 0: (dims[1], dims[2])}.items():
        win[ax] = uv_window(rng, max(float(wa), 0.04) * 1.08, max(float(ha), 0.04) * 1.08, 0.40 * kd, UV_K[1] * kd, pref=pref)      # kmin 0.40 (not UV_K[0] = 0.30): the rounded faces are bigger than their projection, the mean density must stay >= 1/3 (UV_DENSITY)
    cols = {2: (0, 1), 1: (0, 2), 0: (1, 2)}
    lo = np.array([min(v.co[i] for v in bm.verts) for i in range(3)])
    for f in bm.faces:
        nrm = np.array(tuple(f.normal))
        ax = int(np.argmax(np.abs(nrm)))
        u0, v0, k = win[ax]
        a_i, b_i = cols[ax]
        for lp, v in zip(f.loops, f.verts):
            lp[uv_layer].uv = (u0 + k * (v.co[a_i] - lo[a_i]), v0 + k * (v.co[b_i] - lo[b_i]))
        f.smooth = False
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.update()
    return me


def merge_static_masonry(D, objs, max_faces=490, max_span_m=12.0, name=None):
    """Batch whole disconnected stones without changing their faces or UVs.

    ARCH/RUIN categories remain separate. The span is the full world AABB
    diagonal. Every batch keeps >=50% masonry and <=36% rock by actual area.
    Default490 is conservative; genuine DRESS masonry is not a loose rock
    under postcard_look_verify.g_prop_budget or hole09_qa.g_budget, so a caller
    may use larger static batches while retaining the whole-hole budgets.
    Returns objects, source-name replacements, piece ranges and measured stats.
    All packing/validation happens before source objects are removed.
    Custom-normal source meshes remain unchanged: Blender's float normal setter
    re-encodes their opaque split-normal data, so copying it is not bit-exact.
    """
    import json
    if max_faces < 1 or max_span_m <= 0:
        raise ValueError('masonry batch limits must be positive')
    sources = sorted({o.name: o for o in objs}.values(), key=lambda o: o.name)
    if not sources:
        return dict(objects=[], replacements={}, pieces=[], stats=dict(before=0, after=0))
    pieces, uv_names, uv_active = [], None, None
    parent = sources[0].parent
    parent_world = parent.matrix_world.copy() if parent else Matrix.Identity(4)
    for ob in sources:
        if (ob.type != 'MESH' or not ob.name.startswith(('DRESS_RUIN', 'DRESS_ARCH'))
                or ob.parent != parent or ob.children or ob.modifiers or ob.constraints
                or ob.data.shape_keys or ob.data.color_attributes):
            raise ValueError(f'{ob.name}: only static, childless masonry meshes may be batched')
        me = ob.data
        names = tuple(l.name for l in me.uv_layers)
        if not names or (uv_names is not None and names != uv_names):
            raise ValueError(f'{ob.name}: masonry UV layer sets differ or are missing')
        uv_names = names
        uv_active = me.uv_layers.active.name if uv_active is None else uv_active
        if any(m is None or m.name not in ('LK_MASONRY', 'LK_ROCK', 'LK_PLANTS', 'LK_CLIFF') for m in me.materials):
            raise ValueError(f'{ob.name}: unexpected masonry material')
        local = np.array([tuple(v.co) for v in me.vertices], float)
        mw = np.array(ob.matrix_world, float)
        world = local @ mw[:3, :3].T + mw[:3, 3]
        relative = np.array(parent_world.inverted() @ ob.matrix_world, float)
        co = local if np.array_equal(relative, np.eye(4)) else local @ relative[:3, :3].T + relative[:3, 3]
        nmat = np.linalg.inv(relative[:3, :3]).T
        corner = np.array([tuple(n.vector) for n in me.corner_normals], float)
        if len(corner) != len(me.loops):
            raise ValueError(f'{ob.name}: corner normals unavailable')
        if not np.array_equal(relative[:3, :3], np.eye(3)):
            corner = corner @ nmat.T
            corner /= np.maximum(np.linalg.norm(corner, axis=1)[:, None], 1e-12)
        uf = {l.name: np.array([tuple(u.uv) for u in l.data], np.float32) for l in me.uv_layers}
        par = list(range(len(local)))
        def find(i):
            while par[i] != i:
                par[i] = par[par[i]]
                i = par[i]
            return i
        for pg in me.polygons:
            v = list(pg.vertices)
            for i in v[1:]:
                par[find(i)] = find(v[0])
        groups = {}
        for pg in me.polygons:
            groups.setdefault(find(pg.vertices[0]), []).append(pg.index)
        category = 'DRESS_ARCH' if ob.name.startswith('DRESS_ARCH') else 'DRESS_RUIN'
        for ci, polys in enumerate(groups.values()):
            vids = sorted({i for pi in polys for i in me.polygons[pi].vertices})
            ix = {v: i for i, v in enumerate(vids)}
            area = {}
            for pi in polys:
                pg = me.polygons[pi]
                mat = me.materials[pg.material_index]
                area[mat.name] = area.get(mat.name, 0.) + _poly_area(world[list(pg.vertices)])
            lo, hi = world[vids].min(0), world[vids].max(0)
            faces = [tuple(ix[i] for i in me.polygons[pi].vertices) for pi in polys]
            loops = [i for pi in polys for i in me.polygons[pi].loop_indices]
            edges = [(ix[e.vertices[0]], ix[e.vertices[1]], e.use_edge_sharp, e.use_seam)
                     for e in me.edges if e.vertices[0] in ix and e.vertices[1] in ix]
            pieces.append(dict(id=len(pieces), source=ob.name, component=ci, category=category,
                               co=co[vids], faces=faces, materials=[me.materials[me.polygons[pi].material_index] for pi in polys],
                               uv={k: v[loops] for k, v in uf.items()}, normals=corner[loops],
                               custom=me.has_custom_normals, smooth=[me.polygons[pi].use_smooth for pi in polys],
                               edges=edges, lo=lo, hi=hi, center=(lo+hi)/2, area=area,
                               total=sum(area.values()), faces_n=len(polys),
                               source_vertices=vids, source_polygons=polys))
    def shares(bin_):
        tot = sum(p['total'] for p in bin_) or 1.
        return (sum(p['area'].get('LK_MASONRY', 0.) for p in bin_) / tot,
                sum(p['area'].get('LK_ROCK', 0.) for p in bin_) / tot)
    def bounds(bin_):
        lo = np.minimum.reduce([p['lo'] for p in bin_])
        hi = np.maximum.reduce([p['hi'] for p in bin_])
        return lo, hi, float(np.linalg.norm(hi-lo))
    # Keep opaque custom-normal data on the original mesh. A float read/write
    # round trip changes its quantized representation even at identity scale.
    retained = [o for o in sources if o.data.has_custom_normals]
    retained_names = {o.name for o in retained}
    retained_info = {}
    for ob in retained:
        pp = [p for p in pieces if p['source'] == ob.name]
        ma, rk = shares(pp)
        span = bounds(pp)[2]
        if len(ob.data.polygons) > max_faces or span > max_span_m or ma < .5 or rk > ROCK_MAX + 1e-10:
            raise ValueError(f'{ob.name}: custom-normal mesh cannot be changed without losing exact data; '
                             'its existing face/span/material bounds do not fit')
        retained_info[ob.name] = dict(span=span, masonry=ma, rock=rk)
    bins = []
    for category in ('DRESS_RUIN', 'DRESS_ARCH'):
        remaining = [p for p in pieces if p['category'] == category and p['source'] not in retained_names]
        while remaining:
            # Start with the most rock-heavy stone so it receives its actual
            # nearby masonry companions instead of becoming a rock-only tail.
            seed = max(remaining, key=lambda p: (p['area'].get('LK_ROCK', 0.)/max(p['total'], 1e-12), -p['id']))
            current = [seed]
            remaining.remove(seed)
            while True:
                ma, rk = shares(current)
                valid = ma >= .5 and rk <= ROCK_MAX + 1e-10
                nface = sum(p['faces_n'] for p in current)
                candidates = []
                for p in remaining:
                    if nface + p['faces_n'] > max_faces or bounds(current+[p])[2] > max_span_m:
                        continue
                    nm, nr = shares(current+[p])
                    if valid and (nm < .5 or nr > ROCK_MAX + 1e-10):
                        continue
                    if valid:
                        rest = [q for q in remaining if q is not p]
                        # Do not spend the last low-rock companions on an
                        # already-safe batch and strand rock-heavy stones.
                        rm, rr = shares(rest) if rest else (1.,0.)
                        if rm < .5 or rr > ROCK_MAX + 1e-10:
                            continue
                    dist = float(np.linalg.norm(p['center'] - seed['center']))
                    candidates.append(((-nr if valid else dist, dist if valid else nr, p['id']), p))
                if not candidates:
                    break
                p = min(candidates, key=lambda q: q[0])[1]
                current.append(p)
                remaining.remove(p)
            ma, rk = shares(current)
            if (sum(p['faces_n'] for p in current) > max_faces or bounds(current)[2] > max_span_m
                    or ma < .5 or rk > ROCK_MAX + 1e-10):
                raise ValueError(f'{category}: cannot pack whole stones within face/span/material bounds; '
                                 f'sources {[p["source"] for p in current]}, masonry {ma:.4%}, rock {rk:.4%}')
            bins.append(current)
    made, metadata, replacements = [], [], {o.name: [] for o in sources}
    try:
        for bi, current in enumerate(bins):
            category = current[0]['category']
            prefix = name if name and name.startswith(category) else category + '_BATCH'
            nm = L._next_name(D, prefix)
            me = bpy.data.meshes.new(nm)
            ob = bpy.data.objects.new(nm, me)
            made.append(ob)
            if sources[0].users_collection:
                sources[0].users_collection[0].objects.link(ob)
            else:
                bpy.context.scene.collection.objects.link(ob)
            ob.parent = parent
            verts, faces, mi, mats, normals, smooth, edge_flags, piece_ids = [], [], [], [], [], [], [], []
            uv = {k: [] for k in uv_names}
            rec = []
            for p in current:
                vi, fi = len(verts), len(faces)
                verts.extend(p['co'].tolist())
                faces.extend(tuple(vi+i for i in f) for f in p['faces'])
                for mat in p['materials']:
                    if mat not in mats:
                        mats.append(mat)
                    mi.append(mats.index(mat))
                for k in uv_names:
                    uv[k].extend(p['uv'][k].tolist())
                normals.extend(p['normals'].tolist())
                smooth.extend(p['smooth'])
                edge_flags.extend((vi+a, vi+b, sh, se) for a,b,sh,se in p['edges'])
                piece_ids.extend([p['id']]*len(p['faces']))
                record = dict(id=p['id'], source=p['source'], component=p['component'], batch=nm,
                              vertices=[vi,len(verts)], polygons=[fi,len(faces)])
                rec.append(record)
                metadata.append(record)
                if ob not in replacements[p['source']]:
                    replacements[p['source']].append(ob)
            me.from_pydata(verts, [], faces)
            for mat in mats:
                me.materials.append(mat)
            me.polygons.foreach_set('material_index', mi)
            me.polygons.foreach_set('use_smooth', smooth)
            for k in uv_names:
                lay = me.uv_layers.new(name=k)
                lay.data.foreach_set('uv', np.asarray(uv[k], np.float32).ravel())
                lay.active_render = sources[0].data.uv_layers[k].active_render
            me.uv_layers.active_index = list(uv_names).index(uv_active)
            flags = {tuple(sorted((a,b))): (sh,se) for a,b,sh,se in edge_flags}
            for edge in me.edges:
                edge.use_edge_sharp, edge.use_seam = flags.get(tuple(sorted(edge.vertices)), (False,False))
            me.update()
            if any(p['custom'] for p in current):
                me.normals_split_custom_set(normals)
            attr = me.attributes.new('h9_piece_id', 'INT', 'FACE')
            attr.data.foreach_set('value', piece_ids)
            ob['h9_class'] = 'masonry'
            ob['h9_batch_pieces'] = json.dumps(rec)
            ob['h9_batch_span_m'] = bounds(current)[2]
            ob['h9_batch_masonry_share'], ob['h9_batch_rock_share'] = shares(current)
            if len(me.vertices) != len(verts) or len(me.polygons) != len(faces):
                raise RuntimeError(f'{nm}: batching altered stone topology')
    except Exception:
        for ob in made:
            mesh = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            if mesh.users == 0:
                bpy.data.meshes.remove(mesh)
        raise
    for ob in retained:
        replacements[ob.name] = [ob]
        metadata.extend(dict(id=p['id'], source=p['source'], component=p['component'], batch=ob.name,
                             vertex_indices=p['source_vertices'], polygon_indices=p['source_polygons'], unchanged=True)
                        for p in pieces if p['source'] == ob.name)
    old_names = [o.name for o in sources if o.name not in retained_names]
    source_faces = sum(len(o.data.polygons) for o in sources)
    for ob in sources:
        if ob.name in retained_names:
            continue
        D.objects.pop(ob.name, None)
        mesh = ob.data
        bpy.data.objects.remove(ob, do_unlink=True)
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    D.look_objects = [n for n in getattr(D, 'look_objects', []) if n not in old_names]
    for ob in made:
        D.objects[ob.name] = ob
        L._track(D, ob)
    output = made + retained
    stats = dict(before=len(sources), after=len(output), pieces=len(pieces), polygons_before=source_faces,
                 polygons_after=sum(len(o.data.polygons) for o in output), max_faces=max_faces,
                 retained_custom_normals=len(retained), max_span_m=max_span_m,
                 span_max=max([float(o['h9_batch_span_m']) for o in made]+[v['span'] for v in retained_info.values()]),
                 masonry_min=min([float(o['h9_batch_masonry_share']) for o in made]+[v['masonry'] for v in retained_info.values()]),
                 rock_max=max([float(o['h9_batch_rock_share']) for o in made]+[v['rock'] for v in retained_info.values()]))
    print('[hole09_masonry] static batches '+json.dumps(stats), flush=True)
    return dict(objects=output, replacements=replacements, pieces=metadata, stats=stats)
