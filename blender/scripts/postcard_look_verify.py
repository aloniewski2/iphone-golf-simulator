"""postcard_look_verify: Blender-side gates for the POSTCARD_LOOK pass (holes 8 Needle, 9 Split, 10 Crater).

Authority: ArtDir/environments/14_POSTCARD_LOOK.txt + 14b_POSTCARD_LOOK_CONTINUE.txt (GATE POSTCARD_LOOK + the per-hole FAIL lines) and
work/postcard-look/LOOK_CONTRACT.md (names, materials, budgets). Every gate, its threshold and why: work/postcard-look/GATES.md (section
"v2 2026-10-04" for the gates of the continue pass). The gates that need eyes (in-game shots) are in work/postcard-look/VISUAL_CHECKLIST.md.
A Blender pass is never the Game-view pass: the LIT grass hue and the lava reading orange without bloom are Unity-side checks.

    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python blender/scripts/postcard_look_verify.py -- 8
        [--blend PATH] [--fbx PATH] [--no-blend] [--no-fbx] [--json OUT] [--look-dir DIR] [--sig-dir DIR] [--design PATH]

    <hole>            8, 9 or 10
    --blend PATH      the hole scene (default blender/hole_0N.blend); opened by this script
    --fbx PATH        the export (default Unity/Assets/Resources/Course/hole_0N.fbx); re-imported into an EMPTY scene
    --no-blend/--no-fbx  skip that stage
    --json OUT        write every gate {name, ok, detail} + info lines + totals
    --look-dir DIR    the texture folder (default Unity/Assets/Resources/Course/Look)
    --sig-dir DIR     baseline signatures (default work/postcard-look/baseline_signature)

    --build-baseline [--force]        build the frozen play-surface signature (vertex sets + 1.5 m ray grid) ONCE from work/postcard-look/baseline/
    --build-face-baseline [--force]   v2: store the baseline's up-facing TRIANGLES per collision mesh (hole_0N_faces.npz/json next to the signature),
                                      ONCE; the PLAY_SURFACE_FACES gate compares against it
    --build-nonup-baseline [--force]  v2 round 3: the same for the NON-up-facing triangles (walls, steep slopes, undersides: hole_0N_nonup.npz/json); the
                                      PLAY_SURFACE_WALLS gate compares against it
    --selftest [--selftest-dir DIR] [--keep] [--selftest-fbx] [--selftest-only a,b]
                                      build a synthetic POSITIVE scene per hole from the baseline (textures, LK_ materials, faceted rocks, tapering sea
                                      stacks, plants, broken surf, thin shelf, flat path, wall cladding ...; written ONLY under DIR, default a temp dir),
                                      check it (blend + FBX: every gate must PASS), then apply negative mutations (and controls that must still pass).
                                      --selftest-fbx also exports every mutant to FBX and checks the FBX stage; --selftest-only runs only the mutations
                                      whose names contain one of the fragments. <hole> may be "all". Ends with one SELFTEST_GATE line per v2 gate.

Output: `GATE: <NAME>@<stage> PASS|FAIL - <measured detail>` per gate (stage = blend | fbx; file-level gates have none),
`INFO: ...` measurement lines, then `RESULT: ALL PASS` or `RESULT: FAILURES [...]`. Exit 1 on any FAIL (a gate that raises FAILS:
fail closed), 2 on bad usage.
"""
import sys

sys.dont_write_bytecode = True
import os
import re
import math
import json
import time
import shutil
import colorsys
import hashlib
import itertools
import tempfile
import traceback
import importlib.util

import numpy as np
import bpy
import bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import convex_hull_2d

# ----------------------------------------------------------------------------- paths and constants
try:
    HERE = os.path.dirname(os.path.abspath(__file__))
except NameError:  # exec() without __file__
    HERE = os.getcwd()
REPO = os.path.dirname(os.path.dirname(HERE))
BLENDER_DIR = os.path.join(REPO, "blender")
COURSE_DIR = os.path.join(REPO, "Unity", "Assets", "Resources", "Course")
LOOK_DIR = os.path.join(COURSE_DIR, "Look")
JOB_DIR = os.path.join(REPO, "work", "postcard-look")
STILLS_DIR = os.path.join(REPO, "ArtDir", "screenshots", "golf_postcards", "look")        # the Game-view proof stills (PostcardLookStills): holeNN_tee.png / holeNN_approach.png / landmarks
REF_STILLS_DIR = os.path.join(REPO, "ArtDir", "environments", "refs")                    # the user's target stills: needle.jpg / split.jpg / crater.jpg
SIG_DIR = os.path.join(JOB_DIR, "baseline_signature")
BASE_DIR = os.path.join(JOB_DIR, "baseline")
HOLE07 = {"blend": (os.path.join(BLENDER_DIR, "hole_07.blend"), "a7842f25ca64a1a91be9d881611ff84de63eb1f2e1ad8111330b9834edc4c4af"),
          "fbx": (os.path.join(COURSE_DIR, "hole_07.fbx"), "813850bff6d107c0424dd9ca22eeaedf1ba9d3fc7ef6e9f0eed54bc67856c6ed"),
          "meta": (os.path.join(COURSE_DIR, "hole_07.fbx.meta"), "18e346e0e2f4c02be07ea608bccca0d4391776165bab69b375819b67f8a0a216")}

YD = 0.9144                       # metres per course yard (Blender metres = yards * 0.9144)
COLL_PREFIXES = ("TERRAIN", "FAIRWAY", "GREEN", "TEE_BOX", "BUNKER", "CART_PATH")   # HoleView.GroundPrefixes: get MeshColliders
NEW_PREFIXES = ("ROCK_", "PLANT_", "DRESS_", "WATER_", "LAVA_")                    # contract section 2: every NEW object
RESERVED_PREFIXES = ("FLAG", "HOLE_CUP", "BALL_START", "MARKER_")                  # HoleView hides these names
MARKERS = ("MARKER_TEE", "MARKER_PIN", "MARKER_UP")
PLACEHOLDERS = ("FLAG", "FLAG_POLE", "HOLE_CUP", "BALL_START", "TEE_MARKER_1", "TEE_MARKER_2")
GAMEPLAY_MATS = {"MAT_FLAG", "MAT_POLE", "MAT_CUP", "MAT_BALL"}                   # keep going through HoleView.Palette
PLAY_OBJECT_RE = re.compile(r"^(FAIRWAY|FAIRWAY_FIRSTCUT|GREEN|GREEN_APRON|TEE_BOX|BUNKER_\d\d|BUNKER_\d\d_LIP)$")
GRID_STEP = 1.5                   # metres, ray grid over the shore bbox (brief: heights identical on the whole grid)
GRID_MARGIN = 3.0
HEIGHT_TOL = 1e-3                 # metres
MARKER_TOL = 1e-4                 # metres
VERT_TOL = 2e-4                   # metres: vertex sets "identical" when every vertex has a partner this close (rounding noise only)
TRI_BUDGET = 250_000
FBX_MB = 20.0
PROP_FACES = 500
TUFT_FACES = 120
LIP_BAND = 1.0                    # metres below PLAY_Z where a grass lip may hang over a cliff wall
OFF_PLAY_INSET = 1.0              # a prop is ON a play surface when its origin is >= this far inside it (edge tufts allowed)

# material -> (tile metres | "atlas" | "sheet" | "card" | None, albedo, normal, emission)   (LOOK_CONTRACT section 3)
LK = {
    "LK_FAIRWAY": (10.0, "Fairway_C", "Fairway_N", None),
    "LK_GREEN": (6.0, "Green_C", "Green_N", None),
    "LK_ROUGH": (12.0, "Rough_C", "Rough_N", None),
    "LK_SCRUB": (12.0, "Scrub_C", "Scrub_N", None),
    "LK_SAND": (6.0, "Sand_C", "Sand_N", None),
    "LK_CLIFF": (12.0, "Cliff_C", "Cliff_N", None),
    "LK_CLIFF_DARK": (12.0, "CliffDark_C", "CliffDark_N", None),
    "LK_ROCK": (4.0, "Rock_C", "Rock_N", None),
    "LK_ROCK_WET": (4.0, "Rock_C", "Rock_N", None),
    "LK_PATH": (5.0, "Path_C", "Path_N", None),
    "LK_MASONRY": (4.0, "Masonry_C", "Masonry_N", None),
    "LK_BASALT": (8.0, "Basalt_C", "Basalt_N", "Basalt_E"),
    "LK_LAVA": (24.0, "Lava_C", "Lava_N", "Lava_E"),
    "LK_PLANTS": ("atlas", "Plants_C", None, None),
    "LK_WATER": (None, None, None, None),
    "LK_WATER_SHALLOW": (None, None, None, None),
    "LK_SURF": (8.0, "Surf_C", None, None),
    "LK_FALL": ("sheet", "Fall_C", None, None),
    "LK_SMOKE": ("card", "Smoke_C", None, None),
}
ROCKISH = {"LK_ROCK", "LK_ROCK_WET", "LK_CLIFF", "LK_CLIFF_DARK", "LK_BASALT"}
UV_RATIO = (1.0 / 3.0, 3.0)       # authored UV density / contract density
UV_STRETCH_MAX = 0.15             # max area fraction of a material whose per-triangle density is outside 1/4..4 x
REQUIRED_LK = {
    8: {"LK_FAIRWAY", "LK_GREEN", "LK_ROUGH", "LK_SAND", "LK_PLANTS", "LK_CLIFF", "LK_ROCK", "LK_WATER", "LK_WATER_SHALLOW",
        "LK_SURF", "LK_PATH", "LK_MASONRY", "LK_FALL"},
    9: {"LK_FAIRWAY", "LK_GREEN", "LK_ROUGH", "LK_SAND", "LK_PLANTS", "LK_CLIFF", "LK_ROCK", "LK_WATER", "LK_WATER_SHALLOW",
        "LK_SURF", "LK_SCRUB", "LK_MASONRY"},
    10: {"LK_FAIRWAY", "LK_GREEN", "LK_ROUGH", "LK_SAND", "LK_PLANTS", "LK_BASALT", "LK_LAVA", "LK_SMOKE"},
}
# per-hole numeric thresholds that are this gate owner's decisions (the brief names the thing, not the count); see GATES.md
TH = dict(flowers=20, shrubs=6, tufts8=40, arch_blocks=12, arch_vines=6, wall_vines8=6, stacks8=4, surf_pieces=8,
          surf_cov8=0.40, surf_cov9=0.60, skin_cov8=0.20, skin_cov9=0.50, scrub_cov=0.60, scrub_in_ridge=0.95,
          ridge_plants=30, ridge_rocks=8, ruin_blocks=12, lava_tris=500, lava_lights=(3, 5), lava_drop=3.0, smoke=2,
          shelf_plants=10, columns=5, ring_cov=0.80, rock_unique=4, rock_instanced=3, plants_instanced=0.80)

# v2 (2026-10-04) thresholds: this pass's user lines + the repaired gates. Every one is documented in GATES.md ("v2 2026-10-04").
TH2 = dict(
    win_frac=0.90, win_sd=0.012, win_foot_m=1.2, normal_dev=0.04, normal_ac=0.30, card_alpha_sd=0.05,             # LOOK_FILES: spatial structure. win_sd / win_foot_m are the STILLS' OWN window statistic (v2 area U, lead decision): luminance sd >= 0.012 in >= 90 % of the ~1.2 m ground windows of a grass albedo = the 10th percentile of the 8 px windows of the 11 mown-grass crops of needle / split / crater.jpg (lowest crop 0.0121), see GATES.md and calib/still_floor.txt
    distinct_corr=0.85,                                                              # ALBEDO_DISTINCT
    hue_spread=(40.0, 105.0), lit_shift=7.0,                                         # GRASS_ALBEDO_HUE
    lava_vsd=0.10, lava_win_sd=0.04, lava_win=0.75, lava_bright=0.60, lava_dark=0.25, lava_e_bright=0.40, lava_e_peak=(255, 190, 70), cell_min=12, cell_share=0.40, seam=1.6,   # LAVA_TEXTURE
    glow_thr=0.15, glow_cov=0.06, glow_small=0.04, glow_elong=2.0, glow_bad=0.10, glow_run=0.12, joint_m=3.0,                  # BASALT_PRISMS
    bevel_nx=0.25, plane_min_w=0.025, plane_share=0.08,
    smoke_border=0.02, smoke_amax=0.60, smoke_elong=1.6, smoke_grad=0.030,                                                    # SMOKE_SOFT
    foam_cov=0.25, foam_run=10.0, foam_gap=8.0, foam_reach=10.0, foam_area_m=1.0, surf_min_cov=0.05,                            # NO_FOAM_STRIP / SURF_SHORE
    shelf_med=2.5, shelf_p95=4.0, ring_cover=0.90, ring_cv=0.20,                                                               # SHELF_THIN
    stack_taper=0.45, stack_cap=0.25, stack_fill=0.62,                                                                         # SEASTACK_TAPERED
    wall_exposed=0.01, wall_patch_m2=12.0, wall_link=4.0, wall_sample_m2=4.0, uv_aniso=4.0, uv_aniso_frac=0.15, skin_relief=0.15, lawn_m2=10.0,                                       # CRATER_WALL_COVERED, UV_DENSITY, skin, ONE_GREEN
)

# v2 repair round 1 (2026-10-04) SMOKE_IN_FRAME / SMOKE_VISIBLE_STILLS: a plume has to be something a phone user can pick out in the proof stills (review: "no readable smoke plume in any of
# the three stills" while the per-card SMOKE_SOFT passed on 0.44 % / 0.07 % of the frame). share_min = 1.5 % of the frame (the reviewer's example), per Crater proof still (tee, approach, lavarim);
# alpha_readable = the effective alpha (shader formula, LK_SMOKE read from GolfLook.cs) at which a plume moves a pixel by >= 10 luminance levels over the Crater sky (calibration: GATES.md);
# readable_delta = that 10 levels in the Game-view measurement (PostcardLookStills "share_readable").
# v2 repair round 3 (2026-10-05): alpha_readable .30 -> .20, re-calibrated with the SAME rule as the first calibration (the geometric share must not exceed the Game-view share, so the twin stays a conservative floor) after
# LK_SMOKE changed (GolfLook.cs: per-card peak .58 -> .33 so three stacked cards over the near-black wall stay under the |dLum| 110 cap): Game view (PostcardLookStills, TennisURP) share_readable tee 1.89 % / approach 3.23 % /
# lava rim 3.88 %; geometric share at alpha >= .30: 0.97 / 1.31 / 2.12 % (under-reports by 2x: over the dark cone and wall a plume is readable at a much lower alpha than over the sky), at >= .25 1.43 / 1.57 / 2.48, at >= .20
# 1.84 / 1.91 / 2.94 (<= the Game view in all three: the highest threshold that keeps the rule), at >= .15 2.40 / 2.26 / 3.42 (tee above the Game view: no longer conservative). Table: GATES.md "v2 repair round 3".
SMOKE_VIS = dict(share_min=0.015, alpha_readable=0.20, readable_delta=10.0, grid=(72, 128), fov=60.0, addr_back=4.5, addr_up=2.4, addr_ahead=1.5)
STILL_CFG = os.path.join(JOB_DIR, "v2", "proof", "stills_cfg_9.json")                                  # the proof shots (PostcardLookStills schema), hole 10 = tee / approach / lavarim
STILL_CFG_FALLBACK = {10: [dict(name="hole10_1_tee", kind="address", ball=[0, 0], aim=[14.3, 66.2]),
                           dict(name="hole10_2_approach", kind="address", ball=[107.1, 159.3], aim=[261, 164.7]),
                           dict(name="hole10_3_lavarim", kind="landmark", pos=[70, 14, 108], look=[140, 1, 150], fov=55)]}

RES = []
INFO = []
TIMES = {}


# ----------------------------------------------------------------------------- reporting
def gate(name, ok, detail, stage=None):
    full = f"{name}@{stage}" if stage else name
    detail = " ".join(str(detail).split())
    RES.append(dict(name=full, ok=bool(ok), detail=detail))
    print(f"GATE: {full} {'PASS' if ok else 'FAIL'} - {detail}", flush=True)


def info(name, detail, stage=None):
    full = f"{name}@{stage}" if stage else name
    detail = " ".join(str(detail).split())
    INFO.append(dict(name=full, detail=detail))
    print(f"INFO: {full} - {detail}", flush=True)


def run_gate(stage, name, fn, *args):
    """fn returns (ok, detail). A raising gate FAILS (fail closed)."""
    t0 = time.time()
    try:
        ok, detail = fn(*args)
    except Exception as e:  # noqa: BLE001
        tb = traceback.extract_tb(e.__traceback__)[-1]
        ok, detail = False, f"EXCEPTION {type(e).__name__}: {e} (at {os.path.basename(tb.filename)}:{tb.lineno})"
        traceback.print_exc()
    TIMES[f"{name}@{stage}" if stage else name] = round(time.time() - t0, 2)
    gate(name, ok, detail, stage)


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def short(lst, n=8):
    lst = list(lst)
    return f"{lst[:n]}" + (f" (+{len(lst) - n} more)" if len(lst) > n else "")


def rel(p):
    p = os.path.realpath(p)
    r = os.path.realpath(REPO)
    return os.path.relpath(p, r) if p.startswith(r + os.sep) else p


def base_name(n):
    """Blender/FBX duplicate suffix stripped: 'PLANT_TUFT.012' -> 'PLANT_TUFT'."""
    return re.sub(r"\.\d{3,}$", "", n)


# ----------------------------------------------------------------------------- small geometry helpers (numpy)
def tri_geom(W, T):
    if len(T) == 0:
        return np.zeros((0, 3)), np.zeros(0)
    a, b, c = W[T[:, 0]], W[T[:, 1]], W[T[:, 2]]
    n = np.cross(b - a, c - a)
    ln = np.linalg.norm(n, axis=1)
    N = np.zeros_like(n)
    ok = ln > 1e-12
    N[ok] = n[ok] / ln[ok, None]
    return N, 0.5 * ln


def pip(poly, X, Y):
    """Even-odd point in polygon, vectorised (poly (n,2))."""
    X = np.asarray(X, float)
    Y = np.asarray(Y, float)
    ins = np.zeros(X.shape, bool)
    n = len(poly)
    j = n - 1
    with np.errstate(divide="ignore", invalid="ignore"):
        for i in range(n):
            ax, ay = poly[i]
            bx, by = poly[j]
            cond = (ay > Y) != (by > Y)
            if cond.any():
                ins ^= cond & (X < (bx - ax) * (Y - ay) / (by - ay) + ax)
            j = i
    return ins


def dist_polyline(P, L, closed=False, chunk=400):
    """Distance from points P (n,2) to the polyline L (m,2)."""
    P = np.asarray(P, float).reshape(-1, 2)
    L = np.asarray(L, float)
    A = L[:-1] if not closed else L
    B = L[1:] if not closed else np.roll(L, -1, axis=0)
    out = np.empty(len(P))
    ab = B - A
    l2 = np.maximum((ab ** 2).sum(1), 1e-12)[None]
    for s in range(0, len(P), chunk):
        px = P[s:s + chunk, 0:1]
        py = P[s:s + chunk, 1:2]
        t = np.clip(((px - A[:, 0][None]) * ab[:, 0][None] + (py - A[:, 1][None]) * ab[:, 1][None]) / l2, 0.0, 1.0)
        dx = px - (A[:, 0][None] + ab[:, 0][None] * t)
        dy = py - (A[:, 1][None] + ab[:, 1][None] * t)
        out[s:s + chunk] = np.sqrt((dx * dx + dy * dy).min(axis=1))
    return out


def sample_polyline(L, step, closed=True):
    L = np.asarray(L, float)
    A = L if closed else L[:-1]
    B = np.roll(L, -1, axis=0) if closed else L[1:]
    out = []
    for a, b in zip(A, B):
        n = max(1, int(math.ceil(np.hypot(*(b - a)) / step)))
        t = np.arange(n)[:, None] / n
        out.append(a + (b - a) * t)
    return np.vstack(out) if out else np.zeros((0, 2))


def tri_components(T):
    """Label of the connected component (shared vertex indices) of every triangle."""
    if len(T) == 0:
        return np.zeros(0, np.int64)
    parent = {}

    def find(a):
        root = a
        while parent.get(root, root) != root:
            root = parent[root]
        while parent.get(a, a) != root:
            parent[a], a = root, parent[a]
        return root
    for a, b, c in T.tolist():
        ra = find(a)
        for v in (b, c):
            rv = find(v)
            if rv != ra:
                parent[rv] = ra
    return np.array([find(t) for t in T[:, 0].tolist()], np.int64)


def corner_count(xy, min_turn=25.0, merge_frac=0.12):
    """Corners of the 2D convex hull of xy: hull vertices joined by short edges (< merge_frac of a hexagon side) are merged
    into one corner (chamfers/chips), a corner counts when its summed turn is >= min_turn degrees."""
    if len(xy) < 3:
        return 0, []
    idx = convex_hull_2d([tuple(p) for p in xy.tolist()])
    H = xy[idx]
    n = len(H)
    if n < 3:
        return 0, []
    e = np.hypot(*(np.roll(H, -1, 0) - H).T)                  # e[i] = edge i -> i+1
    v1 = H - np.roll(H, 1, 0)
    v2 = np.roll(H, -1, 0) - H
    turn = np.abs(np.degrees(np.arctan2(v1[:, 0] * v2[:, 1] - v1[:, 1] * v2[:, 0], (v1 * v2).sum(1))))
    lim = merge_frac * e.sum() / 6.0
    start = next((i for i in range(n) if e[i - 1] >= lim), 0)  # a vertex whose incoming edge is long
    groups, acc = [], 0.0
    for k in range(n):
        i = (start + k) % n
        acc += turn[i]
        if e[i] >= lim:
            groups.append(acc)
            acc = 0.0
    if acc:
        if groups:
            groups[0] += acc
        else:
            groups.append(acc)
    return int(sum(g >= min_turn for g in groups)), [round(g, 1) for g in groups]


def count_columns(V, T):
    """Basalt column count of a mesh given in world-oriented coordinates (object linear part applied, no translation).
    A column = a connected group of up-facing cap triangles (nz > 0.8) whose plan hull has 5..8 corners (hexagon-like,
    chamfers merged), with near-vertical side faces (|nz| < 0.3) under it within 0.75 x its diameter, reaching down at least
    0.6 x its diameter. Returns (columns, side-area fraction)."""
    N, A = tri_geom(V, T)
    if len(T) == 0:
        return 0, 0.0
    tot = A.sum()
    side = (np.abs(N[:, 2]) < 0.3) & (A > 1e-12)
    side_frac = float(A[side].sum() / tot) if tot > 0 else 0.0
    cap = (N[:, 2] > 0.8) & (A > 1e-12)
    if not cap.any():
        return 0, side_frac
    Tc = T[cap]
    lab = tri_components(Tc)
    sc = V[T[side]].mean(axis=1) if side.any() else np.zeros((0, 3))
    szmin = V[T[side]][:, :, 2].min(axis=1) if side.any() else np.zeros(0)
    cols = 0
    for c in np.unique(lab):
        vi = np.unique(Tc[lab == c])
        P = V[vi]
        ncorner, _g = corner_count(P[:, :2])
        if not (5 <= ncorner <= 8):
            continue
        idx = convex_hull_2d([tuple(p) for p in P[:, :2].tolist()])
        H = P[idx, :2]
        area = 0.5 * abs(np.dot(H[:, 0], np.roll(H[:, 1], -1)) - np.dot(H[:, 1], np.roll(H[:, 0], -1)))
        if area <= 1e-8:
            continue
        diam = 2.0 * math.sqrt(area / math.pi)
        ztop = float(P[:, 2].mean())
        cxy = H.mean(axis=0)
        if not len(sc):
            continue
        near = (np.hypot(sc[:, 0] - cxy[0], sc[:, 1] - cxy[1]) <= 0.75 * diam) & (sc[:, 2] < ztop)
        if not near.any():
            continue
        if ztop - float(szmin[near].min()) < 0.6 * diam:
            continue
        cols += 1
    return cols, side_frac


def rgb_hsv(c):
    return colorsys.rgb_to_hsv(float(c[0]), float(c[1]), float(c[2]))


def is_purple(c):
    h, s, v = rgb_hsv(c)
    return 250.0 / 360.0 <= h <= 325.0 / 360.0 and s >= 0.20 and v >= 0.20


# ----------------------------------------------------------------------------- design (pure python module, course yards)
def load_module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def load_design(hole, path=None):
    tag = f"{hole:02d}"
    cands = [path] if path else [os.path.join(HERE, f"hole{tag}_design.py"), os.path.join(BASE_DIR, "scripts", f"hole{tag}_design.py")]
    err = None
    for p in cands:
        if p and os.path.isfile(p):
            try:
                return load_module(p, f"hole{tag}_design_look"), p
            except Exception as e:  # noqa: BLE001
                err = e
    raise RuntimeError(f"cannot load the hole {hole} design from {cands}: {err}")


class Ctx:
    def __init__(self, hole, design, design_path, opts):
        self.hole = hole
        self.tag = f"{hole:02d}"
        self.d = design
        self.design_path = design_path
        self.opts = opts
        self.pz = float(design.PLAY_Z)
        self.shore_yd = np.array(design.SHORE, float)
        self.shore_m = self.shore_yd * YD
        self.haz = [dict(h) for h in design.HAZARDS]
        self.waters = [h for h in self.haz if h["kind"] == "water"]
        self.bunkers = [h for h in self.haz if h["kind"] == "bunker"]
        self.center_yd = np.array(design.CENTERLINE, float)
        self.fw = float(design.FAIRWAY_WIDTH)
        self.gr = float(design.GREEN_RADIUS)
        self.rw = float(design.ROUGH_WIDTH)
        self.pin_yd = tuple(design.CENTERLINE[-1])
        self.sc = dict(getattr(design, "SCENERY", {}) or {})
        self.look_dir = opts.get("look_dir") or LOOK_DIR
        lo = self.shore_m.min(axis=0) - GRID_MARGIN
        hi = self.shore_m.max(axis=0) + GRID_MARGIN
        for h in self.waters:
            lo = np.minimum(lo, np.array([(h["x"] - h["width"] / 2) * YD, (h["d"] - h["length"] / 2) * YD]) - GRID_MARGIN)
            hi = np.maximum(hi, np.array([(h["x"] + h["width"] / 2) * YD, (h["d"] + h["length"] / 2) * YD]) + GRID_MARGIN)
        self.grid_lo, self.grid_hi = lo, hi

    def dry_yd(self, X, Y):
        X = np.asarray(X, float)
        Y = np.asarray(Y, float)
        d = pip(self.shore_yd, X, Y)
        for h in self.waters:
            d &= ((X - h["x"]) / (h["width"] / 2)) ** 2 + ((Y - h["d"]) / (h["length"] / 2)) ** 2 > 1.0
        return d

    def dry_m(self, X, Y):
        return self.dry_yd(np.asarray(X, float) / YD, np.asarray(Y, float) / YD)


# ----------------------------------------------------------------------------- scene snapshot (opened blend or imported FBX)
EXPORT_COLLECTIONS = ("COURSE", "ENVIRONMENT", "STRUCTURES", "GAMEPLAY")


def export_set(root_name):
    """The objects postcard_lib.export_fbx exports: every MESH/EMPTY in COURSE, ENVIRONMENT, STRUCTURES, GAMEPLAY (recursive)
    + the root. Without those collections: every scene MESH/EMPTY not only in ASSET_LIBRARY."""
    out, seen = [], set()

    def walk(col):
        for ob in col.objects:
            if ob.name not in seen and ob.type in ("MESH", "EMPTY"):
                seen.add(ob.name)
                out.append(ob)
        for ch in col.children:
            walk(ch)
    have = [bpy.data.collections.get(c) for c in EXPORT_COLLECTIONS if bpy.data.collections.get(c)]
    if have:
        for c in have:
            walk(c)
        root = bpy.data.objects.get(root_name)
        if root and root.name not in seen:
            out.append(root)
        return out, "export set (COURSE/ENVIRONMENT/STRUCTURES/GAMEPLAY + root, as postcard_lib.export_fbx)"
    for ob in bpy.context.scene.objects:
        if ob.type not in ("MESH", "EMPTY"):
            continue
        if ob.users_collection and all(c.name == "ASSET_LIBRARY" for c in ob.users_collection):
            continue
        out.append(ob)
    return out, "all scene MESH/EMPTY objects (no export collections found)"


class Snap:
    def __init__(self, stage, C):
        self.stage = stage
        self.C = C
        bpy.context.view_layer.update()
        self.dg = bpy.context.evaluated_depsgraph_get()
        if stage == "blend":
            objs, self.scope = export_set(f"HOLE_{C.tag}_ROOT")
            inset = {o.name for o in objs}
            lib = bpy.data.collections.get("ASSET_LIBRARY")
            libset = set(lib.all_objects) if lib else set()
            self.not_exported = sorted(o.name for o in bpy.context.scene.objects if o.type in ("MESH", "EMPTY")
                                       and o.name not in inset and o not in libset)
        else:
            objs = [o for o in bpy.context.scene.objects]
            self.scope = "every object of the re-imported FBX"
            self.not_exported = []
        self.all = {o.name: o for o in objs}
        self.meshes = {n: o for n, o in self.all.items() if o.type == "MESH"}
        self._loc = {}
        self._wld = {}

    # local (mesh space) arrays, shared by every object that uses the same mesh + the same slot materials
    def key(self, ob):
        if self.stage == "blend" and len(ob.modifiers):
            return ("OB", ob.name)
        return ("ME", ob.data.name, tuple(s.material.name if s.material else "" for s in ob.material_slots))

    def loc(self, ob):
        k = self.key(ob)
        c = self._loc.get(k)
        if c is not None:
            return c
        holder = None
        if k[0] == "OB":
            holder = ob.evaluated_get(self.dg)
            me = holder.to_mesh()
        else:
            me = ob.data
        nv = len(me.vertices)
        co = np.empty(nv * 3)
        me.vertices.foreach_get("co", co)
        V = co.reshape(-1, 3)
        me.calc_loop_triangles()
        nt = len(me.loop_triangles)
        tv = np.empty(nt * 3, np.int64)
        me.loop_triangles.foreach_get("vertices", tv)
        tl = np.empty(nt * 3, np.int64)
        me.loop_triangles.foreach_get("loops", tl)
        mi = np.empty(nt, np.int64)
        me.loop_triangles.foreach_get("material_index", mi)
        tpoly = np.empty(nt, np.int64)
        me.loop_triangles.foreach_get("polygon_index", tpoly)
        names = [s.material.name if s.material else "" for s in ob.material_slots] or [""]
        mi = np.clip(mi, 0, len(names) - 1)
        uv = None
        if len(me.uv_layers):
            lay = me.uv_layers[0]
            u = np.empty(len(me.loops) * 2)
            lay.data.foreach_get("uv", u)
            uv = u.reshape(-1, 2)[tl.reshape(-1, 3)]
        T = tv.reshape(-1, 3)
        N, A = tri_geom(V, T)
        cols = [a.name for a in me.color_attributes]
        alpha = None
        alpha_tri = None
        if cols:
            try:
                ca = me.color_attributes[0]
                arr = np.empty(len(ca.data) * 4)
                ca.data.foreach_get("color", arr)
                alpha = arr.reshape(-1, 4)[:, 3]
                alpha_tri = alpha[T].mean(axis=1) if ca.domain == "POINT" else alpha[tl.reshape(-1, 3)].mean(axis=1)
            except Exception:  # noqa: BLE001
                alpha = None
                alpha_tri = None
        c = dict(V=V, T=T, N=N, A=A, mi=mi, names=names, uv=uv, nv=nv, npoly=len(me.polygons), ntri=nt, cols=cols, alpha=alpha,
                 data=ob.data.name, tp=tpoly, alpha_tri=alpha_tri)
        if holder is not None:
            holder.to_mesh_clear()
        self._loc[k] = c
        return c

    def wld(self, ob):
        c = self._wld.get(ob.name)
        if c is not None:
            return c
        L = self.loc(ob)
        mw = np.array(ob.matrix_world)
        W = L["V"] @ mw[:3, :3].T + mw[:3, 3]
        T = L["T"]
        if np.linalg.det(mw[:3, :3]) < 0:
            T = T[:, [0, 2, 1]]
        N, A = tri_geom(W, T)
        c = dict(W=W, T=T, N=N, A=A, mi=L["mi"], names=L["names"], uv=L["uv"])
        self._wld[ob.name] = c
        return c

    def oriented(self, ob):
        """Mesh vertices with the object's linear part applied (world orientation and scale, no translation)."""
        L = self.loc(ob)
        m3 = np.array(ob.matrix_world)[:3, :3]
        T = L["T"] if np.linalg.det(m3) >= 0 else L["T"][:, [0, 2, 1]]
        return L["V"] @ m3.T, T

    def mat_area(self, ob, world=False):
        """{material name: area} (local mesh units unless world)."""
        L = self.wld(ob) if world else self.loc(ob)
        names = L["names"]
        ar = np.bincount(L["mi"], weights=L["A"], minlength=len(names))
        out = {}
        for i, nm in enumerate(names):
            out[nm] = out.get(nm, 0.0) + float(ar[i])
        return out

    def used_mats(self, ob):
        L = self.loc(ob)
        return {L["names"][i] for i in np.unique(L["mi"]).tolist()} if L["ntri"] else set()

    def wbbox(self, ob):
        """World AABB (lo, hi) of an object (EMPTY: its position)."""
        mw = np.array(ob.matrix_world)
        if ob.type != "MESH" or ob.data is None:
            p = mw[:3, 3]
            return p.copy(), p.copy()
        L = self.loc(ob)
        if L["nv"] == 0:
            p = mw[:3, 3]
            return p.copy(), p.copy()
        lo, hi = L["V"].min(0), L["V"].max(0)
        corners = np.array([[x, y, z] for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])])
        Wc = corners @ mw[:3, :3].T + mw[:3, 3]
        return Wc.min(0), Wc.max(0)

    def pos(self, ob):
        return np.array(ob.matrix_world.translation)

    def centre(self, ob):
        """World bbox centre (meshes baked in place have their origin anywhere; library instances at their footprint)."""
        lo, hi = self.wbbox(ob)
        return (lo + hi) / 2.0

    def named(self, *prefixes):
        return {n: o for n, o in self.all.items() if n.startswith(prefixes)}


class Rays:
    """Single-sided downward ray casts over a set of mesh objects (a Unity MeshCollider ignores back faces)."""

    def __init__(self, S, objs):
        V, T, own, mat, off = [], [], [], [], 0
        self.names = []
        self.matnames = []
        midx = {}
        for o in objs:
            w = S.wld(o)
            if len(w["T"]) == 0:
                continue
            V.append(w["W"])
            T.append(w["T"] + off)
            own.extend([len(self.names)] * len(w["T"]))
            for i in w["mi"].tolist():
                nm = w["names"][i]
                if nm not in midx:
                    midx[nm] = len(self.matnames)
                    self.matnames.append(nm)
                mat.append(midx[nm])
            self.names.append(o.name)
            off += len(w["W"])
        if not V:
            self.bvh = None
            return
        Vv = np.vstack(V)
        Tt = np.vstack(T)
        self.own = np.array(own)
        self.mat = np.array(mat)
        self.bvh = BVHTree.FromPolygons([tuple(v) for v in Vv.tolist()], [tuple(t) for t in Tt.tolist()], all_triangles=True)

    def cast(self, x, y, z0=1000.0):
        """Topmost FRONT-facing hit going down from z0: (z, triangle index) or None."""
        if self.bvh is None:
            return None
        d = Vector((0.0, 0.0, -1.0))
        o = Vector((x, y, z0))
        for _ in range(64):
            loc, nor, idx, _dist = self.bvh.ray_cast(o, d)
            if loc is None:
                return None
            if nor.z > 1e-7:
                return loc.z, idx
            o = Vector((x, y, loc.z - 1e-4))
        return None

    def obj_of(self, idx):
        return self.names[int(self.own[idx])]

    def mat_of(self, idx):
        return self.matnames[int(self.mat[idx])]


def bvh_of(S, objs):
    V, T, off = [], [], 0
    for o in objs:
        w = S.wld(o)
        if len(w["T"]) == 0:
            continue
        V.append(w["W"])
        T.append(w["T"] + off)
        off += len(w["W"])
    if not V:
        return None
    return BVHTree.FromPolygons([tuple(v) for v in np.vstack(V).tolist()], [tuple(t) for t in np.vstack(T).tolist()], all_triangles=True)


def coll_objects(S):
    return [S.all[n] for n in sorted(S.all) if n.startswith(COLL_PREFIXES) and S.all[n].type == "MESH"]


def ground_rays(S):
    if "_ground_rays" not in S.__dict__:
        S._ground_rays = Rays(S, coll_objects(S))
    return S._ground_rays


def on_land(S, C, x, y):
    """(hit object name or None, z): what Unity's GroundHeight would hit at (x, y)."""
    r = ground_rays(S).cast(x, y)
    if r is None:
        return None, None
    return ground_rays(S).obj_of(r[1]), r[0]


def over_play(name):
    return bool(name) and bool(PLAY_OBJECT_RE.match(name)) and name not in ("FAIRWAY_FIRSTCUT", "GREEN_APRON")


# ----------------------------------------------------------------------------- frozen play-surface signature
def qhash(P):
    if len(P) == 0:
        return "empty"
    Q = np.round(np.asarray(P, float) / 1e-4).astype(np.int64)
    Q = Q[np.lexsort((Q[:, 2], Q[:, 1], Q[:, 0]))]
    Q = np.unique(Q, axis=0)
    return hashlib.sha256(Q.tobytes()).hexdigest()


def top_verts(w):
    up = (w["N"][:, 2] > 0.5) & (w["A"] > 1e-12)
    if not up.any():
        return np.zeros((0, 3))
    return w["W"][np.unique(w["T"][up])]


def grid_axes(sig_grid):
    X = sig_grid["x0"] + sig_grid["step"] * np.arange(sig_grid["nx"])
    Y = sig_grid["y0"] + sig_grid["step"] * np.arange(sig_grid["ny"])
    return X, Y


def cast_grid(S, sig_grid):
    rays = ground_rays(S)
    X, Y = grid_axes(sig_grid)
    z = np.full((len(X), len(Y)), np.nan)
    ob = np.full((len(X), len(Y)), -1, np.int64)
    for i, x in enumerate(X.tolist()):
        for j, y in enumerate(Y.tolist()):
            r = rays.cast(x, y)
            if r is not None:
                z[i, j] = r[0]
                ob[i, j] = int(rays.own[r[1]])
    return z, ob, list(rays.names)


def stage_signature(S, C, sig_grid):
    coll = coll_objects(S)
    js = dict(coll_names=sorted(n for n in S.all if n.startswith(COLL_PREFIXES)), all_names=sorted(S.all),
              reserved_names=sorted(n for n in S.all if n.startswith(RESERVED_PREFIXES)), markers={}, tops_hash={}, verts_hash={},
              n_verts={})
    arrays = {}
    for n in MARKERS + PLACEHOLDERS:
        if n in S.all:
            js["markers"][n] = [float(v) for v in S.pos(S.all[n])]
    for o in coll:
        w = S.wld(o)
        tv = top_verts(w)
        js["tops_hash"][o.name] = qhash(tv)
        js["verts_hash"][o.name] = qhash(w["W"])
        js["n_verts"][o.name] = int(len(w["W"]))
        arrays[f"tops::{o.name}"] = tv.astype(np.float64)
        arrays[f"verts::{o.name}"] = w["W"].astype(np.float64)
    z, ob, names = cast_grid(S, sig_grid)
    arrays["grid_z"] = z
    arrays["grid_obj"] = ob
    js["grid_obj_names"] = names
    js["grid_hits"] = int(np.isfinite(z).sum())
    return js, arrays


def sig_paths(sig_dir, hole):
    return os.path.join(sig_dir, f"hole_{hole:02d}.json"), os.path.join(sig_dir, f"hole_{hole:02d}.npz")


def build_baseline(hole, opts):
    design, dpath = load_design(hole, opts.get("design"))
    C = Ctx(hole, design, dpath, opts)
    jp, npz = sig_paths(opts["sig_dir"], hole)
    if os.path.exists(jp) and not opts.get("force"):
        print(f"SIGNATURE: {jp} exists; the baseline signature is built ONCE (pass --force to rebuild)")
        return 2
    blend = os.path.join(BASE_DIR, "blender", f"hole_{C.tag}.blend")
    fbx = os.path.join(BASE_DIR, "fbx", f"hole_{C.tag}.fbx")
    step = GRID_STEP
    nx = int(math.floor((C.grid_hi[0] - C.grid_lo[0]) / step)) + 1
    ny = int(math.floor((C.grid_hi[1] - C.grid_lo[1]) / step)) + 1
    sig_grid = dict(x0=round(float(C.grid_lo[0]), 4), y0=round(float(C.grid_lo[1]), 4), step=step, nx=nx, ny=ny)
    out = dict(hole=hole, built=time.strftime("%Y-%m-%d %H:%M:%S"), tool="postcard_look_verify.py --build-baseline",
               baseline_blend=os.path.relpath(blend, REPO), baseline_blend_sha256=sha256_file(blend),
               baseline_fbx=os.path.relpath(fbx, REPO), baseline_fbx_sha256=sha256_file(fbx),
               design=os.path.relpath(dpath, REPO), grid=sig_grid, play_z=C.pz, stages={})
    arrays = {}
    bpy.ops.wm.open_mainfile(filepath=blend, load_ui=False)
    S = Snap("blend", C)
    js, ar = stage_signature(S, C, sig_grid)
    out["stages"]["blend"] = js
    arrays.update({f"blend::{k}": v for k, v in ar.items()})
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    S = Snap("fbx", C)
    js, ar = stage_signature(S, C, sig_grid)
    out["stages"]["fbx"] = js
    arrays.update({f"fbx::{k}": v for k, v in ar.items()})
    # sanity: blend vs fbx grid agree (same scene, two pipelines)
    zb, zf = arrays["blend::grid_z"], arrays["fbx::grid_z"]
    same_hit = np.isfinite(zb) == np.isfinite(zf)
    both = np.isfinite(zb) & np.isfinite(zf)
    dz = float(np.abs(zb[both] - zf[both]).max()) if both.any() else 0.0
    out["blend_vs_fbx"] = dict(hit_mismatch=int((~same_hit).sum()), max_dz=dz)
    os.makedirs(opts["sig_dir"], exist_ok=True)
    np.savez_compressed(npz, **arrays)
    out["npz_sha256"] = sha256_file(npz)
    with open(jp, "w") as f:
        json.dump(out, f, indent=1)
    print(f"SIGNATURE: hole {hole}: grid {nx} x {ny} @ {step} m ({nx * ny} rays/stage), blend hits {out['stages']['blend']['grid_hits']}, "
          f"fbx hits {out['stages']['fbx']['grid_hits']}, blend-vs-fbx hit mismatches {out['blend_vs_fbx']['hit_mismatch']}, "
          f"max dz {dz:.2e} m; collision objects {out['stages']['blend']['coll_names']}; written {jp} + {os.path.basename(npz)}")
    return 0


def load_signature(sig_dir, hole):
    jp, npz = sig_paths(sig_dir, hole)
    if not (os.path.isfile(jp) and os.path.isfile(npz)):
        return None
    with open(jp) as f:
        js = json.load(f)
    js["_arrays"] = dict(np.load(npz))
    js["_npz_ok"] = sha256_file(npz) == js.get("npz_sha256")
    return js


# ----------------------------------------------------------------------------- gates: frozen scoring geometry
def g_play_names(S, C, B):
    names = sorted(n for n in S.all if n.startswith(COLL_PREFIXES))
    want = B["coll_names"]
    nonmesh = [n for n in names if S.all[n].type != "MESH"]
    extra = sorted(set(names) - set(want))
    miss = sorted(set(want) - set(names))
    ok = not extra and not miss and not nonmesh
    return ok, (f"{len(names)} collision-prefix objects equal the baseline set {names}" if ok else
                f"extra {extra}, missing {miss}, not meshes {nonmesh} (baseline {want})")


def g_play_grid(S, C, B, sig):
    z0 = sig["_arrays"][f"{S.stage}::grid_z"]
    o0 = sig["_arrays"][f"{S.stage}::grid_obj"]
    names0 = B["grid_obj_names"]
    z1, o1, names1 = cast_grid(S, sig["grid"])
    h0, h1 = np.isfinite(z0), np.isfinite(z1)
    hit_mis = h0 != h1
    both = h0 & h1
    dz = np.abs(np.where(both, z1 - z0, 0.0))
    n0 = np.array(names0 + ["-"], object)[np.where(o0 >= 0, o0, len(names0))]
    n1 = np.array(names1 + ["-"], object)[np.where(o1 >= 0, o1, len(names1))]
    name_mis = both & (n0 != n1)
    bad_z = both & (dz > HEIGHT_TOL)
    nbad = int(hit_mis.sum() + bad_z.sum() + (name_mis & ~bad_z).sum())
    X, Y = grid_axes(sig["grid"])
    ex = []
    for mask, what in ((hit_mis, "hit/miss"), (bad_z, "height"), (name_mis, "object")):
        if mask.any():
            i, j = np.argwhere(mask)[0]
            ex.append(f"{what} at ({X[i]:.1f}, {Y[j]:.1f}) m: baseline {n0[i, j]} z {z0[i, j]:.4f} now {n1[i, j]} z {z1[i, j]:.4f}")
    ok = nbad == 0
    return ok, (f"{z0.size} rays on a {GRID_STEP} m grid over the shore bbox ({int(h1.sum())} hit ground): hit/miss mismatches "
                f"{int(hit_mis.sum())}, height mismatches > {HEIGHT_TOL} m {int(bad_z.sum())} (max |dz| "
                f"{float(dz.max()) if both.any() else 0:.2e} m), hit-object mismatches {int(name_mis.sum())}"
                + ("" if ok else "; e.g. " + " | ".join(ex)))


def kd_match(P, Q, tol):
    """Fraction of P with a partner in Q within tol (both (n,3))."""
    if len(P) == 0 or len(Q) == 0:
        return 0 if len(P) else len(P), len(P)
    from mathutils.kdtree import KDTree
    kd = KDTree(len(Q))
    for i, q in enumerate(Q.tolist()):
        kd.insert(q, i)
    kd.balance()
    miss = 0
    for p in P.tolist():
        _co, _i, d = kd.find(p)
        if d is None or d > tol:
            miss += 1
    return miss, len(P)


def g_play_tops(S, C, B, sig):
    problems, notes, walls = [], [], []
    for o in coll_objects(S):
        if o.name not in B["tops_hash"]:
            continue
        w = S.wld(o)
        tv = top_verts(w)
        if qhash(tv) == B["tops_hash"][o.name]:
            same = "hash"
        else:
            T0 = sig["_arrays"].get(f"{S.stage}::tops::{o.name}")
            m1, n1 = kd_match(tv, T0, VERT_TOL)
            m0, n0 = kd_match(T0, tv, VERT_TOL)
            if m1 == 0 and m0 == 0:
                same = "within 0.2 mm"
                notes.append(o.name)
            else:
                problems.append(f"{o.name}: {m1} of {n1} top vertices have no baseline partner, {m0} of {n0} baseline top vertices lost")
                continue
        if qhash(w["W"]) != B["verts_hash"].get(o.name):
            walls.append(f"{o.name} ({len(w['W'])} verts vs {B['n_verts'].get(o.name)})")
    if walls:
        info("PLAY_SURFACE_ALLVERTS", f"vertex sets differ OFF the up-facing top surfaces (walls / undersides; scoring unaffected): {walls}", S.stage)
    else:
        info("PLAY_SURFACE_ALLVERTS", "every collision mesh's full vertex set equals the baseline (hash)", S.stage)
    ok = not problems
    return ok, ("; ".join(problems) if problems else
                f"up-facing (nz > 0.5) vertex sets of {len(B['tops_hash'])} collision meshes equal the baseline "
                f"(sorted, rounded 1e-4 m hash{'; within 0.2 mm after rounding noise: ' + str(notes) if notes else ''})")


def g_markers(S, C, B):
    problems, worst = [], 0.0
    for n, p0 in B["markers"].items():
        if n not in S.all:
            problems.append(f"{n} missing")
            continue
        d = float(np.linalg.norm(S.pos(S.all[n]) - np.array(p0)))
        worst = max(worst, d)
        if d > MARKER_TOL:
            problems.append(f"{n} moved {d * 1000:.2f} mm")
    dup = sorted(n for n in S.all if base_name(n) in B["markers"] and n not in B["markers"])
    if dup:
        problems.append(f"duplicates {dup}")
    return not problems, ("; ".join(problems) if problems else
                          f"{len(B['markers'])} markers/placeholders {sorted(B['markers'])} at their baseline positions (max "
                          f"{worst * 1000:.4f} mm <= {MARKER_TOL * 1000:.1f} mm), no duplicates")


def g_reserved(S, C, B):
    bad = sorted(n for n in S.all if n.startswith(RESERVED_PREFIXES) and n not in B["reserved_names"])
    return not bad, (f"objects named FLAG*/HOLE_CUP*/BALL_START*/MARKER_* are exactly the baseline placeholders {B['reserved_names']}"
                     if not bad else f"new objects with a name HoleView hides: {short(bad)}")


def g_new_prefixes(S, C, B):
    base = set(B["all_names"])
    new = sorted(set(S.all) - base)
    bad = [n for n in new if not n.startswith(NEW_PREFIXES) or n.startswith(COLL_PREFIXES)]
    cnt = {}
    for n in new:
        p = n.split("_")[0] + "_"
        cnt[p] = cnt.get(p, 0) + 1
    if S.not_exported:
        info("NOT_EXPORTED", f"{len(S.not_exported)} scene MESH/EMPTY objects are outside the export collections (not in the FBX): "
             f"{short(S.not_exported)}", S.stage)
    return not bad, (f"{len(new)} new objects, all prefixed {NEW_PREFIXES} {cnt}; no new collision prefix" if not bad else
                     f"{len(bad)} new objects without a ROCK_/PLANT_/DRESS_/WATER_/LAVA_ prefix (or with a collision prefix): {short(bad, 12)}")


# ----------------------------------------------------------------------------- v2: face-level frozen play surfaces
def top_tris(w):
    """Up-facing (nz > 0.5) triangles of a world mesh as (n, 3, 3) coordinates."""
    up = (w["N"][:, 2] > 0.5) & (w["A"] > 1e-12)
    return w["W"][w["T"][up]] if up.any() else np.zeros((0, 3, 3))


def tri_keys(tris):
    """Order-independent, rounded (1e-4 m) identity of triangles: each triangle's 3 corner rows sorted, 9 integers per triangle."""
    if len(tris) == 0:
        return set()
    Q = np.round(tris / 1e-4).astype(np.int64)
    out = set()
    for q in Q.tolist():
        out.add(tuple(sorted(map(tuple, q))))
    return out


def face_signature(S, C):
    arrays = {}
    for o in coll_objects(S):
        arrays[f"faces::{o.name}"] = top_tris(S.wld(o)).astype(np.float64)
    return arrays


def face_paths(sig_dir, hole):
    return os.path.join(sig_dir, f"hole_{hole:02d}_faces.json"), os.path.join(sig_dir, f"hole_{hole:02d}_faces.npz")


def build_face_baseline(hole, opts):
    """Store, per collision mesh and per stage, the up-facing baseline triangles (coordinates), from work/postcard-look/baseline/*. Additive:
    never touches the existing vertex / grid signature; refuses to overwrite without --force."""
    design, dpath = load_design(hole, opts.get("design"))
    C = Ctx(hole, design, dpath, opts)
    jp, npz = face_paths(opts["sig_dir"], hole)
    if os.path.exists(jp) and not opts.get("force"):
        print(f"FACE SIGNATURE: {jp} exists; built ONCE (pass --force to rebuild)")
        return 2
    blend = os.path.join(BASE_DIR, "blender", f"hole_{C.tag}.blend")
    fbx = os.path.join(BASE_DIR, "fbx", f"hole_{C.tag}.fbx")
    arrays, counts = {}, {}
    bpy.ops.wm.open_mainfile(filepath=blend, load_ui=False)
    S = Snap("blend", C)
    for k, v in face_signature(S, C).items():
        arrays[f"blend::{k}"] = v
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    S = Snap("fbx", C)
    for k, v in face_signature(S, C).items():
        arrays[f"fbx::{k}"] = v
    for k, v in arrays.items():
        counts[k] = int(len(v))
    os.makedirs(opts["sig_dir"], exist_ok=True)
    np.savez_compressed(npz, **arrays)
    out = dict(hole=hole, built=time.strftime("%Y-%m-%d %H:%M:%S"), tool="postcard_look_verify.py --build-face-baseline",
               baseline_blend=os.path.relpath(blend, REPO), baseline_blend_sha256=sha256_file(blend),
               baseline_fbx=os.path.relpath(fbx, REPO), baseline_fbx_sha256=sha256_file(fbx), npz_sha256=sha256_file(npz), up_triangles=counts)
    with open(jp, "w") as f:
        json.dump(out, f, indent=1)
    print(f"FACE SIGNATURE: hole {hole}: {sum(counts.values())} up-facing triangles in {len(counts)} (stage, mesh) sets; written {jp} + {os.path.basename(npz)}")
    return 0


def load_face_baseline(sig_dir, hole):
    jp, npz = face_paths(sig_dir, hole)
    if not (os.path.isfile(jp) and os.path.isfile(npz)):
        return None
    with open(jp) as f:
        js = json.load(f)
    js["_arrays"] = dict(np.load(npz))
    js["_npz_ok"] = sha256_file(npz) == js.get("npz_sha256")
    return js


def cover_check(src_tris, dst_tris, tol=1e-3):
    """How many source triangle centroids have NO destination triangle surface under them (a vertical ray from above onto the destination
    triangles) at the same height (+-tol)? Re-triangulating the same planar polygon passes; deleting / flipping / adding a polygon fails."""
    if len(src_tris) == 0:
        return 0, 0
    if len(dst_tris) == 0:
        return len(src_tris), len(src_tris)
    V = dst_tris.reshape(-1, 3)
    T = np.arange(len(V)).reshape(-1, 3)
    bvh = BVHTree.FromPolygons([tuple(v) for v in V.tolist()], [tuple(t) for t in T.tolist()], all_triangles=True)
    miss = 0
    for c in src_tris.mean(axis=1).tolist():
        loc, _n, _i, _d = bvh.ray_cast(Vector((c[0], c[1], c[2] + 5.0)), Vector((0.0, 0.0, -1.0)))
        if loc is None or abs(loc.z - c[2]) > tol:
            miss += 1
    return miss, len(src_tris)


def g_play_faces(S, C, FB):
    """Face-level frozen scoring geometry (v2): the up-facing triangles of every TERRAIN / FAIRWAY / GREEN / TEE_BOX / BUNKER mesh equal the baseline's.
    Exact set equality of the rounded triangles passes; otherwise every baseline triangle must still have a current top surface under its centroid
    at the same height and vice versa (re-triangulation only), and the total top area must match. A deleted, flipped or added polygon fails."""
    arrays = FB["_arrays"]
    problems, notes, ntot = [], [], 0
    names = sorted(n for n in (o.name for o in coll_objects(S)))
    base_names = sorted(k.split("::", 2)[2] for k in arrays if k.startswith(f"{S.stage}::faces::"))
    for n in sorted(set(base_names) - set(names)):
        problems.append(f"{n}: collision mesh missing")
    for o in coll_objects(S):
        n = o.name
        base = arrays.get(f"{S.stage}::faces::{n}")
        if base is None:
            continue
        cur = top_tris(S.wld(o))
        ntot += len(base)
        kb, kc = tri_keys(base), tri_keys(cur)
        if kb == kc and len(base) == len(cur):
            continue
        ab = float(np.linalg.norm(np.cross(base[:, 1] - base[:, 0], base[:, 2] - base[:, 0]), axis=1).sum() / 2) if len(base) else 0.0
        ac = float(np.linalg.norm(np.cross(cur[:, 1] - cur[:, 0], cur[:, 2] - cur[:, 0]), axis=1).sum() / 2) if len(cur) else 0.0
        miss_b, nb = cover_check(base, cur)
        miss_c, nc = cover_check(cur, base)
        area_ok = abs(ab - ac) <= 1e-5 * max(ab, 1.0) + 1e-6
        if miss_b == 0 and miss_c == 0 and area_ok:
            notes.append(f"{n}: re-triangulated ({len(base)} -> {len(cur)} triangles, same surface)")
        else:
            problems.append(f"{n}: {miss_b} of {nb} baseline top triangles lost (deleted / flipped / moved), {miss_c} of {nc} current top triangles are new, "
                            f"top area {ab:.3f} -> {ac:.3f} m2")
    return not problems, ("; ".join(problems) if problems else
                          f"up-facing triangles of {len(names)} collision meshes ({ntot} baseline triangles) equal the baseline"
                          + (f" ({'; '.join(notes[:3])})" if notes else " (exact rounded-triangle sets)"))


# ----------------------------------------------------------------------------- v2 round 3: face-level frozen WALLS / UNDERSIDES of the collision meshes
def nonup_tris(w):
    """Every non-degenerate triangle of a world mesh that is NOT up-facing (nz <= 0.5): cliff walls, steep slopes, undersides, as (n, 3, 3) coordinates. Together with
    top_tris this is the whole collision body (the v2 round-3 review: deleting a TERRAIN wall polygon passed because only the up-facing faces were compared)."""
    nz = (w["N"][:, 2] <= 0.5) & (w["A"] > 1e-12)
    return w["W"][w["T"][nz]] if nz.any() else np.zeros((0, 3, 3))


def nonup_paths(sig_dir, hole):
    return os.path.join(sig_dir, f"hole_{hole:02d}_nonup.json"), os.path.join(sig_dir, f"hole_{hole:02d}_nonup.npz")


def tri_keys_oriented(tris):
    """Like tri_keys but keeps the WINDING (the corners rotated so the smallest rounded corner comes first): a flipped wall polygon has another key."""
    if len(tris) == 0:
        return set()
    Q = np.round(tris / 1e-4).astype(np.int64)
    out = set()
    for q in Q.tolist():
        rows = [tuple(r) for r in q]
        k = min(range(3), key=lambda i: rows[i])
        out.add((rows[k], rows[(k + 1) % 3], rows[(k + 2) % 3]))
    return out


def tri_area(tris):
    return float(np.linalg.norm(np.cross(tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0]), axis=1).sum() / 2) if len(tris) else 0.0


def build_nonup_baseline(hole, opts):
    """Store, per collision mesh and per stage, the non-up-facing baseline triangles (coordinates) from work/postcard-look/baseline/*. Additive (a new pair of files, the
    vertex / grid / up-facing signatures are untouched); built ONCE, refuses to overwrite without --force."""
    design, dpath = load_design(hole, opts.get("design"))
    C = Ctx(hole, design, dpath, opts)
    jp, npz = nonup_paths(opts["sig_dir"], hole)
    if os.path.exists(jp) and not opts.get("force"):
        print(f"NONUP SIGNATURE: {jp} exists; built ONCE (pass --force to rebuild)")
        return 2
    blend = os.path.join(BASE_DIR, "blender", f"hole_{C.tag}.blend")
    fbx = os.path.join(BASE_DIR, "fbx", f"hole_{C.tag}.fbx")
    arrays, counts, areas = {}, {}, {}
    bpy.ops.wm.open_mainfile(filepath=blend, load_ui=False)
    S = Snap("blend", C)
    for o in coll_objects(S):
        arrays[f"blend::nonup::{o.name}"] = nonup_tris(S.wld(o)).astype(np.float64)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    S = Snap("fbx", C)
    for o in coll_objects(S):
        arrays[f"fbx::nonup::{o.name}"] = nonup_tris(S.wld(o)).astype(np.float64)
    for k, v in arrays.items():
        counts[k] = int(len(v))
        areas[k] = tri_area(v)
    os.makedirs(opts["sig_dir"], exist_ok=True)
    np.savez_compressed(npz, **arrays)
    out = dict(hole=hole, built=time.strftime("%Y-%m-%d %H:%M:%S"), tool="postcard_look_verify.py --build-nonup-baseline",
               baseline_blend=os.path.relpath(blend, REPO), baseline_blend_sha256=sha256_file(blend),
               baseline_fbx=os.path.relpath(fbx, REPO), baseline_fbx_sha256=sha256_file(fbx), npz_sha256=sha256_file(npz),
               nonup_triangles=counts, nonup_area_m2=areas)
    with open(jp, "w") as f:
        json.dump(out, f, indent=1)
    print(f"NONUP SIGNATURE: hole {hole}: {sum(counts.values())} non-up-facing triangles in {len(counts)} (stage, mesh) sets; written {jp} + {os.path.basename(npz)}")
    return 0


def load_nonup_baseline(sig_dir, hole):
    jp, npz = nonup_paths(sig_dir, hole)
    if not (os.path.isfile(jp) and os.path.isfile(npz)):
        return None
    with open(jp) as f:
        js = json.load(f)
    js["_arrays"] = dict(np.load(npz))
    js["_npz_ok"] = sha256_file(npz) == js.get("npz_sha256")
    return js


def surface_cover3d(src_tris, dst_tris, tol=1e-3):
    """How many source triangle centroids lie farther than tol from the SURFACE of the destination triangles (3D nearest point, BVH), or face the other way than the
    destination triangle that is nearest? Walls are vertical, so the vertical-ray test of cover_check cannot see them; re-triangulating the same polygon passes,
    deleting / moving / flipping one fails."""
    if len(src_tris) == 0:
        return 0, 0
    if len(dst_tris) == 0:
        return len(src_tris), len(src_tris)
    V = dst_tris.reshape(-1, 3)
    T = np.arange(len(V)).reshape(-1, 3)
    bvh = BVHTree.FromPolygons([tuple(v) for v in V.tolist()], [tuple(t) for t in T.tolist()], all_triangles=True)
    nrm = np.cross(src_tris[:, 1] - src_tris[:, 0], src_tris[:, 2] - src_tris[:, 0])
    nrm = nrm / np.maximum(np.linalg.norm(nrm, axis=1), 1e-30)[:, None]
    miss = 0
    for c, n0 in zip(src_tris.mean(axis=1).tolist(), nrm.tolist()):
        loc, nn, _i, dist = bvh.find_nearest(Vector(c))
        if loc is None or dist > tol or float(nn.dot(Vector(n0))) < 0.5:
            miss += 1
    return miss, len(src_tris)


def g_play_walls(S, C, NB):
    """Face-level frozen collision BODY (v2 round 3): the NON-up-facing triangles (cliff walls, steep slopes, undersides) of every TERRAIN / FAIRWAY / GREEN / TEE_BOX /
    BUNKER / CART_PATH mesh equal the baseline's, so a collision polygon cannot be deleted, flipped or moved anywhere. Exact rounded-triangle set equality passes; otherwise
    (re-triangulation) every baseline triangle centroid must lie on a current non-up surface (3D, 1 mm) and vice versa and the non-up area must match (1e-6 relative)."""
    arrays = NB["_arrays"]
    problems, notes, ntot = [], [], 0
    names = sorted(o.name for o in coll_objects(S))
    base_names = sorted(k.split("::", 2)[2] for k in arrays if k.startswith(f"{S.stage}::nonup::"))
    for n in sorted(set(base_names) - set(names)):
        problems.append(f"{n}: collision mesh missing")
    for o in coll_objects(S):
        n = o.name
        base = arrays.get(f"{S.stage}::nonup::{n}")
        if base is None:
            continue
        cur = nonup_tris(S.wld(o))
        ntot += len(base)
        if tri_keys_oriented(base) == tri_keys_oriented(cur) and len(base) == len(cur):
            continue
        ab, ac = tri_area(base), tri_area(cur)
        miss_b, nb = surface_cover3d(base, cur)
        miss_c, nc = surface_cover3d(cur, base)
        area_ok = abs(ab - ac) <= 1e-6 * max(ab, 1.0) + 1e-6
        if miss_b == 0 and miss_c == 0 and area_ok:
            notes.append(f"{n}: re-triangulated walls ({len(base)} -> {len(cur)} triangles, same surface)")
        else:
            problems.append(f"{n}: {miss_b} of {nb} baseline wall / underside triangles lost (deleted / flipped / moved), {miss_c} of {nc} current ones are new, "
                            f"non-up area {ab:.3f} -> {ac:.3f} m2")
    return not problems, ("; ".join(problems) if problems else
                          f"non-up-facing triangles (walls, steep slopes, undersides) of {len(names)} collision meshes ({ntot} baseline triangles) equal the baseline"
                          + (f" ({'; '.join(notes[:3])})" if notes else " (exact rounded-triangle sets)"))


# ----------------------------------------------------------------------------- gates: materials
def mats_on(S, objs=None):
    """{material name: [object names]} over meshes."""
    out = {}
    for n, o in (objs or S.meshes).items():
        for m in S.used_mats(o):
            out.setdefault(m, []).append(n)
    return out


def g_no_foam(S, C):
    m = mats_on(S)
    foam_m = {k: v for k, v in m.items() if k.upper().startswith("MAT_FOAM")}
    foam_o = sorted(n for n in S.all if "FOAM" in n.upper())
    ok = not foam_m and not foam_o
    return ok, ("no MAT_FOAM material on any mesh and no *FOAM* object" if ok else
                f"MAT_FOAM on {[(k, short(v, 4)) for k, v in foam_m.items()]}; FOAM objects {short(foam_o)}")


def g_legacy_mats(S, C):
    m = mats_on(S)
    bad = {k: v for k, v in m.items() if not (k.startswith("LK_") and k in LK) and k not in GAMEPLAY_MATS}
    ok = not bad
    used_lk = sorted(k for k in m if k.startswith("LK_"))
    return ok, (f"every mesh material is a contract LK_ name {used_lk} or a gameplay MAT_ (flag/pole/cup/ball)" if ok else
                "non-contract materials: " + "; ".join(f"{k or '<empty slot>'} on {short(sorted(v), 5)}" for k, v in sorted(bad.items())))


def g_lk_required(S, C):
    used = set(mats_on(S))
    need = REQUIRED_LK[C.hole]
    miss = sorted(need - used)
    return not miss, (f"all {len(need)} required LK_ materials used: {sorted(need)}" if not miss else f"missing {miss}")


def face_class(w, pz):
    N = w["N"]
    top = N[:, 2] > 0.5
    cz = w["W"][w["T"]][:, :, 2].mean(axis=1) if len(w["T"]) else np.zeros(0)
    lip = ~top & (cz >= pz - LIP_BAND)                     # grass lip band hanging over the edge (walls or overhang undersides)
    down = (N[:, 2] < -0.5) & ~lip
    wall = ~top & ~(N[:, 2] < -0.5) & ~lip
    return top, wall, down, lip


def g_ground_mats(S, C):
    problems, rep = [], []
    h = C.hole
    top_ok = {8: {"LK_ROUGH"}, 9: {"LK_ROUGH", "LK_SCRUB"}, 10: {"LK_ROUGH", "LK_BASALT"}}[h]
    wall_ok = {8: {"LK_CLIFF", "LK_CLIFF_DARK", "LK_ROCK", "LK_ROCK_WET"}, 9: {"LK_CLIFF", "LK_CLIFF_DARK", "LK_ROCK", "LK_ROCK_WET"},
               10: {"LK_BASALT"}}[h]
    per_name = [(re.compile(r"^FAIRWAY$"), {"LK_FAIRWAY"}), (re.compile(r"^FAIRWAY_FIRSTCUT$"), {"LK_FAIRWAY", "LK_ROUGH"}),
                (re.compile(r"^GREEN$"), {"LK_GREEN"}), (re.compile(r"^GREEN_APRON$"), {"LK_FAIRWAY", "LK_GREEN"}),
                (re.compile(r"^TEE_BOX$"), {"LK_GREEN", "LK_FAIRWAY"}), (re.compile(r"^BUNKER_\d\d$"), {"LK_SAND"}),
                (re.compile(r"^BUNKER_\d\d_LIP$"), {"LK_GREEN", "LK_FAIRWAY", "LK_ROUGH", "LK_SAND"})]
    wall_area = {}
    if not any(o.name.startswith("TERRAIN") for o in coll_objects(S)):
        problems.append("no TERRAIN mesh to check")
    for o in coll_objects(S):
        n = o.name
        w = S.wld(o)
        if n.startswith("TERRAIN"):
            top, wall, down, lip = face_class(w, C.pz)
            names = np.array(w["names"], object)[w["mi"]] if len(w["mi"]) else np.zeros(0, object)
            for mask, allowed, what in ((top, top_ok, "top"), (wall, wall_ok, "wall"), (lip, wall_ok | top_ok, "lip band"),
                                        (down, wall_ok, "underside")):
                if not mask.any():
                    continue
                got = set(names[mask].tolist())
                bad = sorted(got - allowed)
                if bad:
                    a = {b: round(float(w["A"][mask & (names == b)].sum())) for b in bad}
                    problems.append(f"{n} {what} faces use {a} m2 (allowed {sorted(allowed)})")
            for mask in (wall, down):
                for nm, a in zip(names[mask].tolist(), w["A"][mask].tolist()):
                    wall_area[nm] = wall_area.get(nm, 0.0) + a
            continue
        rule = next((allowed for rx, allowed in per_name if rx.match(n)), None)
        if rule is None:
            continue
        got = S.used_mats(o)
        bad = sorted(got - rule)
        if bad:
            problems.append(f"{n} uses {bad} (allowed {sorted(rule)})")
        else:
            rep.append(f"{n}:{'/'.join(sorted(got))}")
    tot = sum(wall_area.values())
    if tot > 0:
        good = sum(a for k, a in wall_area.items() if (k in ("LK_CLIFF", "LK_CLIFF_DARK") if h in (8, 9) else k == "LK_BASALT"))
        need = 0.5 if h in (8, 9) else 0.8
        frac = good / tot
        if frac < need:
            problems.append(f"terrain wall+underside area {tot:.0f} m2 is only {frac:.0%} "
                            f"{'LK_CLIFF/LK_CLIFF_DARK' if h in (8, 9) else 'LK_BASALT'} (need {need:.0%})")
        rep.append(f"terrain walls {frac:.0%} {'cliff' if h in (8, 9) else 'basalt'} of {tot:.0f} m2")
    return not problems, ("; ".join(problems) if problems else
                          f"play surfaces + terrain use their contract materials: {', '.join(rep)}; terrain tops {sorted(top_ok)}, "
                          f"walls {sorted(wall_ok)} (+ a {LIP_BAND} m grass lip band)")


def is_loose_rock(n):
    return (n.startswith("ROCK_") or n.startswith("CLIFF_ROCK")) and not n.startswith(("ROCK_SKIN_", "ROCK_WALL_"))


def g_rock_mats(S, C):
    problems, seen, n_ok = [], set(), 0
    allowed = ROCKISH | {"LK_ROUGH", "LK_SCRUB", "LK_PLANTS", "LK_MASONRY"}
    for n, o in S.meshes.items():
        if not (n.startswith("ROCK_") or n.startswith("CLIFF_ROCK")):
            continue
        k = S.key(o)
        if k in seen:
            continue
        seen.add(k)
        ma = S.mat_area(o)
        tot = sum(ma.values()) or 1.0
        bad = sorted(set(ma) - allowed)
        if C.hole == 10 and n.startswith("ROCK_WALL_"):
            frac = ma.get("LK_BASALT", 0.0) / tot
            need = "LK_BASALT"
        else:
            frac = sum(a for m, a in ma.items() if m in ROCKISH) / tot
            need = "rock materials"
        if bad or frac < 0.6:
            problems.append(f"{n} (mesh {o.data.name}): {('uses ' + str(bad) + ' ') if bad else ''}{need} {frac:.0%} of its area (need 60%)")
        else:
            n_ok += 1
    if not seen:
        return False, "no ROCK_*/CLIFF_ROCK* meshes"
    return not problems, ("; ".join(problems[:10]) + (f" (+{len(problems) - 10} more)" if len(problems) > 10 else "") if problems else
                          f"{n_ok} unique rock meshes: only {sorted(allowed)}, >= 60% of each mesh's area rock"
                          + (" (ROCK_WALL_*: LK_BASALT)" if C.hole == 10 else ""))


PROP_RULES = [
    (re.compile(r"^PLANT_"), {"LK_PLANTS"}, None),
    (re.compile(r"^DRESS_PATH"), {"LK_PATH", "LK_ROUGH", "LK_PLANTS"}, ("LK_PATH", 0.6)),
    (re.compile(r"^DRESS_(ARCH|RUIN)"), {"LK_MASONRY", "LK_ROCK", "LK_PLANTS", "LK_CLIFF"}, ("LK_MASONRY", 0.5)),
    (re.compile(r"^DRESS_CONE"), {"LK_BASALT", "LK_LAVA", "LK_ROCK"}, None),
    (re.compile(r"^DRESS_SMOKE"), {"LK_SMOKE"}, None),
    (re.compile(r"^DRESS_SAND"), {"LK_SAND"}, None),
    (re.compile(r"^DRESS_"), set(LK) - {"LK_GREEN", "LK_FAIRWAY", "LK_WATER", "LK_WATER_SHALLOW", "LK_SURF", "LK_FALL"}, None),   # v2: no white water as dressing
    (re.compile(r"^WATER_OCEAN$"), {"LK_WATER"}, None),
    (re.compile(r"^WATER_SHELF"), {"LK_WATER_SHALLOW"}, None),
    (re.compile(r"^WATER_SURF"), {"LK_SURF"}, None),
    (re.compile(r"^WATER_FALL"), {"LK_FALL", "LK_SURF"}, None),
    (re.compile(r"^WATER_LAVA$"), {"LK_LAVA"}, None),
    (re.compile(r"^LAVA_"), {"LK_LAVA", "LK_BASALT"}, None),
    (re.compile(r"^WATER_"), {"LK_WATER", "LK_WATER_SHALLOW", "LK_SURF"}, None),
]


def g_prop_mats(S, C):
    problems, seen, cnt = [], set(), 0
    for n, o in S.meshes.items():
        rule = next(((rx, allowed, maj) for rx, allowed, maj in PROP_RULES if rx.match(n)), None)
        if rule is None:
            continue
        k = (rule[0].pattern, S.key(o))
        if k in seen:
            continue
        seen.add(k)
        cnt += 1
        ma = S.mat_area(o)
        bad = sorted(set(ma) - rule[1])
        if bad:
            problems.append(f"{n} uses {bad} (allowed {sorted(rule[1])})")
        if rule[2]:
            tot = sum(ma.values()) or 1.0
            f = ma.get(rule[2][0], 0.0) / tot
            if f < rule[2][1]:
                problems.append(f"{n}: {rule[2][0]} only {f:.0%} of its area (need {rule[2][1]:.0%})")
    return not problems, ("; ".join(problems[:10]) if problems else
                          f"{cnt} PLANT_/DRESS_/WATER_/LAVA_ mesh variants use only their contract materials "
                          f"(PLANT_ = LK_PLANTS, DRESS_PATH = LK_PATH, DRESS_ARCH/RUIN = LK_MASONRY, WATER_OCEAN = LK_WATER, ...)")


def find_upstream(sock, types, depth=0):
    out = []
    for link in sock.links:
        nd = link.from_node
        if nd.type in types:
            out.append(nd)
        if depth < 10:
            for inp in nd.inputs:
                out += find_upstream(inp, types, depth + 1)
    return out


def img_file(nd):
    if nd.image is None:
        return None, None
    p = bpy.path.abspath(nd.image.filepath, library=nd.image.library) if nd.image.filepath else ""
    return os.path.basename(nd.image.filepath.replace("\\", "/")), os.path.realpath(p) if p else None


def g_lk_textured(S, C):
    """blend only: the material node trees carry the contract textures (Image Texture -> Base Color, Image -> Normal Map -> Normal,
    Image -> Emission Color for basalt/lava), each image file is the contract PNG inside the Look folder."""
    present = {m for m in mats_on(S) if m.startswith("LK_") and m in LK}
    used = sorted(present | REQUIRED_LK[C.hole])
    look = os.path.realpath(C.look_dir)
    problems, okl = [], []
    for m in used:
        tile, cf, nf, ef = LK[m]
        mat = bpy.data.materials.get(m)
        if m not in present:
            problems.append(f"{m}: not on any exported mesh")
            continue
        if cf is None:
            okl.append(f"{m}(shader-only)")
            continue
        if mat is None or mat.node_tree is None:
            problems.append(f"{m}: no node tree")
            continue
        bs = [nd for nd in mat.node_tree.nodes if nd.type == "BSDF_PRINCIPLED"]
        if not bs:
            problems.append(f"{m}: no Principled BSDF")
            continue
        p = []
        best = None
        for b in bs:
            q = []
            base = [nd for nd in find_upstream(b.inputs["Base Color"], {"TEX_IMAGE"})]
            if not base:
                q.append("no Image Texture into Base Color")
            elif not any(img_file(nd)[0] == f"{cf}.png" for nd in base):
                q.append(f"Base Color image is {[img_file(nd)[0] for nd in base]}, want {cf}.png")
            if nf:
                nms = find_upstream(b.inputs["Normal"], {"NORMAL_MAP"})
                imgs = [i for nm in nms for i in find_upstream(nm.inputs["Color"], {"TEX_IMAGE"})]
                if not nms:
                    q.append("no Normal Map node into Normal")
                elif not any(img_file(nd)[0] == f"{nf}.png" for nd in imgs):
                    q.append(f"Normal Map image is {[img_file(nd)[0] for nd in imgs]}, want {nf}.png")
            if ef:
                em = find_upstream(b.inputs["Emission Color"], {"TEX_IMAGE"})
                if not any(img_file(nd)[0] == f"{ef}.png" for nd in em):
                    q.append(f"Emission Color image is {[img_file(nd)[0] for nd in em]}, want {ef}.png")
            if best is None or len(q) < len(best):
                best = q
        p += best
        for nd in mat.node_tree.nodes:
            if nd.type == "TEX_IMAGE":
                bn, rp = img_file(nd)
                if bn is None:
                    p.append(f"image node {nd.name} has no image")
                elif not rp or not os.path.isfile(rp):
                    p.append(f"{bn} not found on disk ({rp})")
                elif not rp.startswith(look + os.sep):
                    p.append(f"{bn} resolves outside the Look folder ({rp})")
        if p:
            problems.append(f"{m}: " + ", ".join(p))
        else:
            okl.append(m)
    return not problems, ("; ".join(problems) if problems else
                          f"{len(okl)} LK_ materials carry their contract maps from {rel(look)}: {okl}")


IMG_CACHE = {}


def image_stats(path):
    """Pixels of a PNG as float32 (h, w, 4): the STORED (sRGB-encoded) values 0..1, row 0 = bottom. Cached by (path, mtime, size)."""
    try:
        stt = os.stat(path)
        key = (path, stt.st_mtime_ns, stt.st_size)
    except OSError:
        key = (path, 0, 0)
    c = IMG_CACHE.get(path)
    if c is not None and c.get("key") == key:
        return c
    img = bpy.data.images.load(path, check_existing=False)
    try:
        w, h = img.size
        arr = np.empty(w * h * 4, np.float32)
        img.pixels.foreach_get(arr)
    finally:
        bpy.data.images.remove(img)
    st = dict(w=w, h=h, px=arr.reshape(h, w, 4), key=key)
    IMG_CACHE[path] = st
    return st


# ----------------------------------------------------------------------------- texture analysis helpers (numpy only)
def lum709(px):
    return px[..., 0] * 0.2126 + px[..., 1] * 0.7152 + px[..., 2] * 0.0722


def hsv_arr(rgb):
    """Vectorised RGB (..., 3) in 0..1 -> (hue degrees 0..360, saturation, value)."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = rgb.max(axis=-1)
    mn = rgb.min(axis=-1)
    d = mx - mn
    ok = d > 1e-6
    dd = np.maximum(d, 1e-9)
    rc, gc, bc = (mx - r) / dd, (mx - g) / dd, (mx - b) / dd
    h = np.where(mx == r, bc - gc, np.where(mx == g, 2.0 + rc - bc, 4.0 + gc - rc))
    h = np.where(ok, (h / 6.0) % 1.0, 0.0) * 360.0
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-9), 0.0)
    return h, s, mx


def mean_hsv(rgb_mean):
    h, s, v = colorsys.rgb_to_hsv(float(rgb_mean[0]), float(rgb_mean[1]), float(rgb_mean[2]))
    return h * 360.0, s, v


def srgb_to_lab(rgb):
    c = np.asarray(rgb, float)
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    M = np.array([[0.4124564, 0.3575761, 0.1804375], [0.2126729, 0.7151522, 0.0721750], [0.0193339, 0.1191920, 0.9503041]])
    xyz = M @ lin / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16.0 / 116.0)
    return np.array([116.0 * f[1] - 16.0, 500.0 * (f[0] - f[1]), 200.0 * (f[1] - f[2])])


def pool(a, k):
    """Mean-pool a (H, W[, C]) by an integer factor k (the remainder rows/columns are cropped)."""
    if k <= 1:
        return a
    H, W = a.shape[:2]
    H2, W2 = H // k, W // k
    b = a[:H2 * k, :W2 * k]
    return b.reshape((H2, k, W2, k) + a.shape[2:]).mean(axis=(1, 3))


def window_sd(a, win):
    """Standard deviation of every non-overlapping win x win window of a 2D array (flat array)."""
    H, W = a.shape
    h2, w2 = H // win, W // win
    if h2 == 0 or w2 == 0:
        return np.array([float(a.std())])
    return a[:h2 * win, :w2 * win].reshape(h2, win, w2, win).std(axis=(1, 3)).ravel()


def window_sd_overlap(a, win, stride):
    """Standard deviation of every win x win window of a 2D array placed every `stride` pixels (integral images: exact, fast), flat array. A window as large as the array = the global sd."""
    H, W = a.shape
    if win >= min(H, W):
        return np.array([float(a.std())])
    a64 = a.astype(np.float64)
    S1 = np.zeros((H + 1, W + 1))
    S2 = np.zeros((H + 1, W + 1))
    S1[1:, 1:] = a64.cumsum(0).cumsum(1)
    S2[1:, 1:] = (a64 * a64).cumsum(0).cumsum(1)
    ys = np.arange(0, H - win + 1, max(1, stride))[:, None]
    xs = np.arange(0, W - win + 1, max(1, stride))[None, :]

    def box(Sx):
        return Sx[ys + win, xs + win] - Sx[ys, xs + win] - Sx[ys + win, xs] + Sx[ys, xs]
    n = float(win * win)
    m = box(S1) / n
    v = box(S2) / n - m * m
    return np.sqrt(np.maximum(v, 0.0)).ravel()


def resample_n(a, n):
    """Area-ish resample of a 2D array to n x n (pool by the integer factor, then nearest)."""
    k = max(1, min(a.shape) // n)
    b = pool(a, k)
    yi = (np.arange(n) * b.shape[0] // n)
    xi = (np.arange(n) * b.shape[1] // n)
    return b[yi][:, xi]


def lag1(a):
    a0 = a - a.mean()
    v = float((a0 ** 2).mean())
    if v < 1e-14:
        return 0.0
    return float(0.5 * ((a0[:, :-1] * a0[:, 1:]).mean() + (a0[:-1] * a0[1:]).mean()) / v)


def label_mask(mask):
    """4-connected components of a 2D bool mask (run-length union-find, pure python, fast up to 512^2).
    Returns (labels int32 (H, W), 0 = background; n components)."""
    H, W = mask.shape
    runs, row_runs = [], []
    for y in range(H):
        d = np.diff(np.concatenate(([0], mask[y].astype(np.int8), [0])))
        st = np.flatnonzero(d == 1)
        en = np.flatnonzero(d == -1)
        idx = []
        for a, b in zip(st.tolist(), en.tolist()):
            idx.append(len(runs))
            runs.append((y, a, b))
        row_runs.append(idx)
    parent = list(range(len(runs)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for y in range(1, H):
        prev, cur = row_runs[y - 1], row_runs[y]
        i = 0
        for c in cur:
            _, a0, a1 = runs[c]
            while i < len(prev) and runs[prev[i]][2] <= a0:
                i += 1
            j = i
            while j < len(prev) and runs[prev[j]][1] < a1:
                ra, rb = find(c), find(prev[j])
                if ra != rb:
                    parent[ra] = rb
                j += 1
    lab = np.zeros((H, W), np.int32)
    roots = {}
    for k, (y, a, b) in enumerate(runs):
        r = find(k)
        if r not in roots:
            roots[r] = len(roots) + 1
        lab[y, a:b] = roots[r]
    return lab, len(roots)


def comp_stats(lab, n):
    """Per labelled component: area, bounding box, border contact and convex-hull area (solidity = area / hull)."""
    H, W = lab.shape
    out = []
    ys, xs = np.nonzero(lab)
    ids = lab[ys, xs]
    order = np.argsort(ids, kind="stable")
    ys, xs, ids = ys[order], xs[order], ids[order]
    bounds = np.searchsorted(ids, np.arange(1, n + 2))
    for k in range(1, n + 1):
        a, b = int(bounds[k - 1]), int(bounds[k])
        y, x = ys[a:b], xs[a:b]
        area = len(y)
        touches = bool(y.min() == 0 or x.min() == 0 or y.max() == H - 1 or x.max() == W - 1)
        if area >= 3:
            pts = np.unique(np.stack([np.concatenate([x, x + 1, x, x + 1]), np.concatenate([y, y, y + 1, y + 1])], 1), axis=0)
            idx = convex_hull_2d([tuple(p) for p in pts.astype(float).tolist()])
            P = pts[idx].astype(float)
            ha = 0.5 * abs(np.dot(P[:, 0], np.roll(P[:, 1], -1)) - np.dot(P[:, 1], np.roll(P[:, 0], -1)))
        else:
            ha = float(area)
        out.append(dict(area=area, touches=touches, hull=max(ha, float(area)), w=int(x.max() - x.min() + 1),
                        h=int(y.max() - y.min() + 1), x0=int(x.min()), x1=int(x.max()) + 1))
    return out


def longest_row_run(mask):
    """Longest run of True along a row of a 2D bool mask (px)."""
    best = 0
    for y in range(mask.shape[0]):
        d = np.diff(np.concatenate(([0], mask[y].astype(np.int8), [0])))
        st = np.flatnonzero(d == 1)
        en = np.flatnonzero(d == -1)
        if len(st):
            best = max(best, int((en - st).max()))
    return best


def seam_ratio(px):
    """Tiling seam error: mean |first column - last column| (and rows) over the mean |neighbour difference| inside the image.
    About 1 for a texture that wraps; far above 1 where the tile edge is a visible cut."""
    a = px[..., :3]
    ix = float(np.abs(a[:, 1:] - a[:, :-1]).mean())
    iy = float(np.abs(a[1:] - a[:-1]).mean())
    sx = float(np.abs(a[:, 0] - a[:, -1]).mean())
    sy = float(np.abs(a[0] - a[-1]).mean())
    return max(sx / max(ix, 1e-6), sy / max(iy, 1e-6))


TEX_WIN_FRAC = 1.0 / 16.0                                 # window = 1/16 of the tile edge: 64 px of a 1024 px texture (the lead's number), 8 px of a 128 px one.
                                                          # v2 review: a fixed 64 px window made the verdict depend on the resolution (the same artwork at 128 px passed)
TEX_GROUND = ("Fairway_C", "Green_C", "Rough_C", "Scrub_C")  # grass ground albedos: the strict rule
TEX_SD_MIN = {"Sand_C": 0.008}                            # soft sand: its own floor; every other opaque albedo 0.012
TEX_BLUR_K = 8                                            # iid-noise killer: sd of 8 x 8 block means (of a 64 px window at 1024)
CARD_TEX = ("Surf_C", "Fall_C", "Smoke_C")                # alpha-blended maps: structure is in the ALPHA
TEX_MIN_PX = dict(ground=512, other=256, card=128)        # contract sizes are 1024 / 512: below these the window rule is not comparable (a 128 px copy of a 1024 px map passed)
TEX_PERIODIC_MAX = 0.25                                   # share of the >= 5 cycle/tile power in ONE frequency bin pair: an 8 px checkerboard holds ~100 %, grass < 1 %


def tex_min_px(stem):
    if stem.endswith("_E") or stem == "Plants_C":
        return 64
    if stem in CARD_TEX:
        return TEX_MIN_PX["card"]
    return TEX_MIN_PX["ground"] if stem.replace("_N", "_C") in TEX_GROUND else TEX_MIN_PX["other"]


def periodic_peak(lum):
    """Largest share of the spectral power above 5 cycles per tile that sits in a single frequency bin (with its mirror): ~0 for noise / grass,
    ~1 for a checkerboard or stripes at pixel scale. Measured at <= 512 px."""
    a = pool(lum, max(1, min(lum.shape) // 512))
    a = a - a.mean()
    P = np.abs(np.fft.fft2(a)) ** 2
    fy = np.fft.fftfreq(a.shape[0])[:, None] * a.shape[0]
    fx = np.fft.fftfreq(a.shape[1])[None, :] * a.shape[1]
    P = np.where(np.hypot(fx, fy) >= 5.0, P, 0.0)
    tot = float(P.sum())
    if tot <= 1e-18:
        return 0.0
    P2 = P + np.roll(np.flip(P, (0, 1)), 1, (0, 1))               # bin + its mirror (real signals)
    return float(P2.max() / (2.0 * tot))


TEX_REPEAT_MAX = 0.5                                      # high-passed autocorrelation at any shift >= 10 px: a tiled 16 px cell repeats at ~1.0, grass < 0.2


def repeat_peak(lum):
    """Largest normalised autocorrelation of the high-passed luminance (gaussian sigma 4 px removed) at any circular shift of 10 px or more, measured at <= 512 px:
    ~1.0 for a texture made of one small cell tiled over the map (its spectrum is spread over many harmonics, so periodic_peak does not see it), < 0.2 for grass."""
    a = pool(lum, max(1, min(lum.shape) // 512))
    H, W = a.shape
    F = np.fft.fft2(a - a.mean())
    fy = np.fft.fftfreq(H)[:, None]
    fx = np.fft.fftfreq(W)[None, :]
    G = np.exp(-2.0 * (math.pi ** 2) * 16.0 * (fx ** 2 + fy ** 2))
    P = np.abs(F * (1.0 - G)) ** 2
    ac = np.real(np.fft.ifft2(P))
    if ac[0, 0] <= 1e-18:
        return 0.0
    ac = ac / ac[0, 0]
    dy = np.minimum(np.arange(H), H - np.arange(H))[:, None]
    dx = np.minimum(np.arange(W), W - np.arange(W))[None, :]
    return float(ac[np.maximum(dy, dx) >= 10].max())


TEX_TILE_M = {v[1]: float(v[0]) for v in LK.values() if isinstance(v[0], float) and v[1]}     # albedo stem -> tile edge in metres (LOOK_CONTRACT section 3)


def albedo_structure(px, name):
    """Spatial structure of an opaque albedo: share of windows whose luminance sd (RGB only, alpha ignored) reaches the floor, and the same on block means (dither / white noise has
    no block-mean structure), plus the periodicity of the spectrum. Grass ground albedos (TEX_GROUND): windows are a GROUND footprint of TH2['win_foot_m'] 1.2 m (= the 8 px windows of
    the target stills, which are 0.13-0.18 m per pixel by the green radii of the hole designs) so the texture is compared with the stills at the SAME window scale, windows placed every half
    window (overlapping), floor TH2['win_sd'] = the stills' own 10th percentile. Every other opaque albedo keeps the windows of 1/16 of the tile edge and its own floor."""
    lum = lum709(px)
    if name in TEX_GROUND and name in TEX_TILE_M:
        floor = TH2["win_sd"]
        win = max(8, int(round(TH2["win_foot_m"] / TEX_TILE_M[name] * min(lum.shape))))
        k = max(1, win // 8)
        raw = window_sd_overlap(lum, win, win // 2)
        blur = window_sd_overlap(pool(lum, k), max(2, win // k), max(1, win // k // 2))
    else:
        floor = TH2["win_sd"] if name in TEX_GROUND else TEX_SD_MIN.get(name, 0.012)
        win = max(4, int(round(min(lum.shape) * TEX_WIN_FRAC)))
        k = max(1, win // 8)
        raw = window_sd(lum, win)
        blur = window_sd(pool(lum, k), max(2, win // k))
    return dict(floor=floor, frac_raw=float((raw >= floor).mean()), frac_blur=float((blur >= 0.4 * floor).mean()),
                p10=float(np.percentile(raw, 10)), gsd=float(lum.std()), n=int(raw.size), win=win, foot=(name in TEX_GROUND and name in TEX_TILE_M),
                periodic=periodic_peak(lum) if name in TEX_GROUND else 0.0, repeat=repeat_peak(lum) if name in TEX_GROUND else 0.0)


def normal_structure(px):
    nx, ny = px[..., 0] * 2.0 - 1.0, px[..., 1] * 2.0 - 1.0
    return dict(dev=float(math.sqrt(nx.var() + ny.var())), ac=0.5 * (lag1(nx) + lag1(ny)), blue=float(px[..., 2].mean()))


def g_look_files(S, C):
    """Every contract texture of every used + required LK_ material exists, is >= 64 px and has SPATIAL STRUCTURE (v2): a flat fill,
    a flat colour with random alpha, dither noise and a flat (128,128,255) normal with noise all FAIL."""
    used = sorted({m for m in mats_on(S) if m.startswith("LK_") and m in LK} | REQUIRED_LK[C.hole])
    problems, rep, sizes = [], [], 0
    files = set()
    for m in used:
        for f in LK[m][1:]:
            if f:
                files.add(f)
    for f in sorted(files):
        p = os.path.join(C.look_dir, f + ".png")
        if not os.path.isfile(p):
            problems.append(f"{f}.png missing")
            continue
        sizes += os.path.getsize(p)
        try:
            st = image_stats(p)
        except Exception as e:  # noqa: BLE001
            problems.append(f"{f}.png unreadable: {e}")
            continue
        px = st["px"]
        mp = tex_min_px(f)
        if st["w"] < mp or st["h"] < mp:
            problems.append(f"{f}.png is {st['w']}x{st['h']} (< {mp}: the contract size is 1024 / 512)")
        if f.endswith("_N"):
            ns = normal_structure(px)
            bad = []
            if ns["dev"] < TH2["normal_dev"]:
                bad.append(f"xy deviation sd {ns['dev']:.3f} < {TH2['normal_dev']}")
            if ns["ac"] < TH2["normal_ac"]:
                bad.append(f"neighbour autocorrelation {ns['ac']:.2f} < {TH2['normal_ac']} (white-noise dither, not relief)")
            if ns["blue"] < 0.55:
                bad.append(f"mean blue {ns['blue']:.2f} < 0.55")
            if bad:
                problems.append(f"{f}.png is not a relief normal map: " + ", ".join(bad))
            rep.append(f"{f} dev {ns['dev']:.2f}")
        elif f.endswith("_E"):
            mx, mean = float(px[..., :3].max()), float(px[..., :3].mean())
            if mx < 0.25 or mean > 0.5:
                problems.append(f"{f}.png emission mask max {mx:.2f} (need >= .25) mean {mean:.2f} (need <= .5)")
            rep.append(f"{f} max {mx:.2f}")
        elif f in CARD_TEX:
            a = px[..., 3]
            sd, sdb = float(a.std()), float(pool(a, 4).std())
            if sd < TH2["card_alpha_sd"] or sdb < 0.6 * TH2["card_alpha_sd"]:
                problems.append(f"{f}.png alpha is flat (sd {sd:.3f}, 4x4 block sd {sdb:.3f}; need >= {TH2['card_alpha_sd']}): the alpha IS this map")
            rep.append(f"{f} alpha sd {sd:.2f}")
        elif f == "Plants_C":
            sw = pool(px[..., :3], max(1, st["w"] // 4)) if st["w"] >= 4 else px[..., :3]
            q = {tuple(np.round(c * 25).astype(int).tolist()) for c in sw.reshape(-1, 3)}
            gs = float(lum709(px).std())
            if len(q) < 12 or gs < 0.06:
                problems.append(f"Plants_C.png atlas has {len(q)} distinct swatches (need >= 12) and luminance sd {gs:.3f} (>= 0.06)")
            rep.append(f"{f} swatches {len(q)}")
        else:
            a = albedo_structure(px, f)
            if a["frac_raw"] < TH2["win_frac"] or a["frac_blur"] < TH2["win_frac"]:
                unit = f"{TH2['win_foot_m']:g} m ground windows" if a["foot"] else "1/16 of the tile"
                problems.append(f"{f}.png is flat: only {a['frac_raw']:.0%} of its {a['n']} windows of {a['win']} px ({unit}) have luminance sd >= {a['floor']} "
                                f"(need {TH2['win_frac']:.0%}; window p10 {a['p10']:.4f}) and {a['frac_blur']:.0%} keep sd >= {0.4 * a['floor']:.4f} "
                                f"after block averaging (dither noise does not) [the floor is the stills' own: the 10th percentile of the 8 px windows of the target stills' mown grass, "
                                f"v2/gates/calib/still_floor.txt]")
            if a["periodic"] > TEX_PERIODIC_MAX:
                problems.append(f"{f}.png is a periodic pattern, not grass: {a['periodic']:.0%} of its spectral power (>= 5 cycles/tile) is in one frequency (need <= {TEX_PERIODIC_MAX:.0%})")
            if a["repeat"] > TEX_REPEAT_MAX:
                problems.append(f"{f}.png repeats a small cell: high-passed autocorrelation {a['repeat']:.2f} at a shift >= 10 px (need <= {TEX_REPEAT_MAX}): tiled noise, not grass")
            rep.append(f"{f} win {a['frac_raw']:.0%}")
    return not problems, ("; ".join(problems) if problems else
                          f"{len(files)} contract textures for {len(used)} LK_ materials exist in {rel(C.look_dir)} "
                          f"({sizes / 1048576:.1f} MB), all with spatial structure: {', '.join(rep)}")



# ----------------------------------------------------------------------------- v2 area U: the stills' own floors (LOOK_FILES) and the Game-view luminance floor
STILL_CROPS = {   # (still, crop name): (kind, (x0, y0, x1, y1) in the 1280 x 720 stills, y from the TOP). Only clean material: no rock in a grass crop, no ball / flag.
    ("needle.jpg", "mid_green"): ("grass", (770, 265, 930, 310)), ("needle.jpg", "top_green"): ("grass", (885, 62, 1055, 95)),
    ("split.jpg", "upper_green"): ("grass", (655, 112, 790, 150)), ("split.jpg", "lower_green"): ("grass", (600, 575, 700, 640)),
    ("split.jpg", "fairway_strip"): ("grass", (862, 235, 898, 328)), ("split.jpg", "fairway_long"): ("grass", (842, 165, 878, 228)),
    ("split.jpg", "lower_fairway"): ("grass", (735, 475, 815, 535)),
    ("crater.jpg", "pillar_green"): ("grass", (800, 352, 990, 418)), ("crater.jpg", "tee_box_a"): ("grass", (140, 462, 200, 512)),
    ("crater.jpg", "tee_box_b"): ("grass", (245, 462, 296, 512)), ("crater.jpg", "fairway_mid"): ("grass", (540, 238, 610, 275)),
    ("split.jpg", "sand_big"): ("sand", (930, 225, 1000, 285)), ("split.jpg", "sand_small"): ("sand", (962, 340, 1005, 375)), ("split.jpg", "sand_mid"): ("sand", (725, 385, 800, 455)),
    ("crater.jpg", "sand_a"): ("sand", (455, 365, 565, 420)), ("crater.jpg", "sand_b"): ("sand", (660, 215, 740, 262)),
    ("split.jpg", "rock_cliff"): ("stone", (600, 150, 700, 230)), ("needle.jpg", "rock_wall"): ("stone", (650, 380, 740, 450)), ("needle.jpg", "path"): ("stone", (300, 380, 420, 470)),
    ("crater.jpg", "basalt_a"): ("stone", (70, 150, 180, 260)), ("crater.jpg", "basalt_b"): ("stone", (880, 170, 1000, 250)), ("crater.jpg", "basalt_c"): ("stone", (80, 530, 200, 640)),
}
STILL_TH = dict(win=8, flat_sd=0.006, block_sd=0.003, flat_max=0.15, lower=0.60)
# win = the stills' finest resolved window (8 px = ~1.1-1.4 m of ground at 0.13-0.18 m per pixel); flat_sd = HALF the stills' own lowest 10th percentile at that window (0.0121 / 2): the Game
# view's 8 px window covers 0.05-0.4 m of ground, and in the stills' own w8 / w16 / w32 series the window sd grows ~ footprint^0.4, so a 1/4..1/20 footprint is worth ~0.5; block_sd = the
# same for the 4 x 4 block means of a window (white noise of sd s has block-mean sd s / 4: sd 0.0095 noise = 0.0024 < 0.003); flat_max = the share of flat windows allowed in the lower 60 % of a
# tee / approach still = the Unity probe's PROBE_GROUND_TEXTURED cap (15 %); the stills' own clean crops have <= 5 % flat windows (grass 5 %, sand 4 %, stone 3 %).


def lum_of(px):
    return lum709(px[..., :3])


def window_flat_mask(lum, win=None, flat_sd=None, block_sd=None):
    """Per non-overlapping win x win window of a luminance array: True when it is FLAT (window sd < flat_sd) or only NOISE (the sd of its 4 x 4 block means < block_sd). Returns (mask, window sd)."""
    win = win or STILL_TH["win"]
    flat_sd = STILL_TH["flat_sd"] if flat_sd is None else flat_sd
    block_sd = STILL_TH["block_sd"] if block_sd is None else block_sd
    H, W = lum.shape
    h2, w2 = H // win, W // win
    if h2 == 0 or w2 == 0:
        return np.zeros(0, bool), np.zeros(0)
    a = lum[:h2 * win, :w2 * win].reshape(h2, win, w2, win)
    sd = a.std(axis=(1, 3))
    b = a.reshape(h2, win // 4, 4, w2, win // 4, 4).mean(axis=(2, 5))
    bsd = b.std(axis=(1, 3))
    return ((sd < flat_sd) | (bsd < block_sd)).ravel(), sd.ravel()


def ref_crop_lum(stem, box):
    """Luminance of a crop of a reference still (box in 1280 x 720 top-down coordinates, scaled to the file's size)."""
    st = image_stats(os.path.join(REF_STILLS_DIR, stem))
    H, W = st["h"], st["w"]
    sx, sy = W / 1280.0, H / 720.0
    x0, y0, x1, y1 = int(box[0] * sx), int(box[1] * sy), int(box[2] * sx), int(box[3] * sy)
    lum = lum_of(st["px"])[::-1]                    # row 0 = top
    return lum[y0:y1, x0:x1]


def g_still_floor_selfcheck():
    """The stills must PASS the floors the verifier applies (v2 area U, lead decision: 'do NOT use a number that the stills themselves fail'): re-measures every clean crop of the target
    stills with the verifier's own window statistic at the stills' finest resolved window (8 px): mown-grass crops reach luminance sd >= TH2['win_sd'] in >= TH2['win_frac'] of their windows,
    sand crops >= the Sand floor, stone / basalt crops >= 0.012 (the other opaque albedos' floor), and every crop has <= STILL_TH['flat_max'] flat windows (the Game-view floor)."""
    problems, rep = [], []
    worst = {}
    for (stem, name), (kind, box) in STILL_CROPS.items():
        if not os.path.isfile(os.path.join(REF_STILLS_DIR, stem)):
            problems.append(f"{stem} missing")
            continue
        lum = ref_crop_lum(stem, box)
        sd = window_sd(lum, STILL_TH["win"])
        floor = {"grass": TH2["win_sd"], "sand": TEX_SD_MIN["Sand_C"], "stone": 0.012}[kind]
        frac = float((sd >= floor).mean())
        flat, _sd = window_flat_mask(lum)
        fshare = float(flat.mean()) if flat.size else 0.0
        w = worst.setdefault(kind, [1.0, 0.0, 9.0])
        w[0] = min(w[0], frac)
        w[1] = max(w[1], fshare)
        w[2] = min(w[2], float(np.percentile(sd, 10)))
        if frac < TH2["win_frac"]:
            problems.append(f"{stem}:{name} ({kind}) only {frac:.0%} of its 8 px windows reach sd {floor} (need {TH2['win_frac']:.0%}; p10 {np.percentile(sd, 10):.4f})")
        if fshare > STILL_TH["flat_max"]:
            problems.append(f"{stem}:{name} ({kind}) {fshare:.0%} flat windows > {STILL_TH['flat_max']:.0%}")
    for k, (fr, fl, p10) in sorted(worst.items()):
        rep.append(f"{k}: worst crop {fr:.0%} of windows >= floor, lowest p10 {p10:.4f}, flat share {fl:.0%}")
    return not problems, ("; ".join(problems[:6]) if problems else
                          f"the target stills pass the verifier's own floors: {len(STILL_CROPS)} clean crops at the stills' finest window (8 px): " + "; ".join(rep)
                          + f" (grass floor {TH2['win_sd']}, 90 % of windows; Game-view flat window = sd < {STILL_TH['flat_sd']} or 4x4 block-mean sd < {STILL_TH['block_sd']}, <= {STILL_TH['flat_max']:.0%})")


def g_look_stills(S_or_C=None, C=None):
    """GAME-VIEW luminance floor (v2 area U, lead decision): the lower 60 % of every Game-view tee / approach still of this hole (ArtDir/screenshots/golf_postcards/look/holeNN_tee.png,
    holeNN_approach.png) holds <= 15 % flat windows (8 px windows whose luminance sd < 0.006, or only noise: sd of the 4 x 4 block means < 0.003). The stills' own clean crops have <= 5 %; a
    flat-colour hole (the baseline) has 94-100 %, flat colour + noise 91 %, the textured probe tee frames 0.1-5 % (the close putting-green views 15-27 %: smooth by nature, not what this gate reads). When the stills do not exist yet the gate is SKIPPED (PASS, said so) unless --require-stills."""
    C = C or S_or_C
    need = bool(C.opts.get("require_stills"))
    d = C.opts.get("stills_dir") or STILLS_DIR
    found, problems, rep = 0, [], []
    for kind in ("tee", "approach"):
        pth = os.path.join(d, f"hole{C.tag}_{kind}.png")
        if not os.path.isfile(pth):
            continue
        found += 1
        st = image_stats(pth)
        lum = lum_of(st["px"])
        rows = int(st["h"] * STILL_TH["lower"])
        flat, sd = window_flat_mask(lum[:rows])                  # row 0 = bottom: the first 60 % of the rows are the lower 60 % of the frame
        share = float(flat.mean()) if flat.size else 1.0
        rep.append(f"hole{C.tag}_{kind}.png {flat.size} windows, flat {share:.1%}, median window sd {float(np.median(sd)):.4f}")
        if share > STILL_TH["flat_max"]:
            problems.append(f"hole{C.tag}_{kind}.png: {share:.0%} of the lower 60 % is flat (window sd < {STILL_TH['flat_sd']} or noise-only; need <= {STILL_TH['flat_max']:.0%}): the hole still reads as flat colour")
    if not found:
        info("LOOK_STILLS_TEXTURED", f"SKIPPED: no Game-view tee / approach still of hole {C.hole} in {rel(d)} yet" + ("" if not need else " (--require-stills)"))
        return (not need), (f"SKIPPED (no Game-view tee / approach still of hole {C.hole} in {rel(d)} yet; --require-stills makes this a FAIL)" if not need else
                            f"no Game-view tee / approach still of hole {C.hole} in {rel(d)} (--require-stills)")
    if need and found < 2:
        problems.append(f"only {found} of the 2 tee / approach stills exist (--require-stills)")
    return not problems, ("; ".join(problems) if problems else "Game-view stills are textured: " + "; ".join(rep) + f" (<= {STILL_TH['flat_max']:.0%} flat windows in the lower 60 %)")

# ----------------------------------------------------------------------------- v2 texture gates (this pass's user lines)
DISTINCT_SET = (("LK_FAIRWAY", "Fairway_C"), ("LK_GREEN", "Green_C"), ("LK_ROUGH", "Rough_C"), ("LK_SCRUB", "Scrub_C"),
                ("LK_SAND", "Sand_C"), ("LK_CLIFF", "Cliff_C"), ("LK_ROCK", "Rock_C"), ("LK_PATH", "Path_C"),
                ("LK_MASONRY", "Masonry_C"), ("LK_BASALT", "Basalt_C"))


def look_png(C, stem):
    p = os.path.join(C.look_dir, stem + ".png")
    return image_stats(p)["px"] if os.path.isfile(p) else None


def tex_fields(px, n=64):
    """The structure fields of an albedo: luminance + R + G + B at n x n (a recoloured copy keeps the pattern in at least one channel)."""
    return [resample_n(x, n) for x in (lum709(px), px[..., 0], px[..., 1], px[..., 2])]


def field_correlation(FA, FB):
    """Largest normalised correlation between any channel field of A and any channel field of B over all 8 rotations / mirror images of B and every circular
    shift (tiles wrap): 1.0 for a copy, a rotated, mirrored, shifted, tinted or HUE-SHIFTED copy (the pattern survives in some channel); about 0.3-0.5 for two
    independent textures. Flat channels carry no pattern and are skipped; two flat albedos count as a copy (1.0)."""
    X = []
    for a in FA:
        x = a - a.mean()
        nx = math.sqrt(float((x * x).sum()))
        if nx > 1e-9:
            X.append((np.fft.fft2(x), nx))
    Y = [b for b in FB if float(b.std()) > 1e-6]
    if not X or not Y:
        return 1.0
    best = 0.0
    for k in range(4):
        for flip in (False, True):
            for b in Y:
                y = np.rot90(b, k)
                y = y[:, ::-1] if flip else y
                y = y - y.mean()
                ny = math.sqrt(float((y * y).sum()))
                Fy = np.conj(np.fft.fft2(y))
                for Fx, nx in X:
                    cc = np.real(np.fft.ifft2(Fx * Fy))
                    best = max(best, float(np.abs(cc).max() / (nx * ny)))
    return best


_PAIR_CACHE = {}


def stem_key(C, stem):
    p = os.path.join(C.look_dir, stem + ".png")
    try:
        stt = os.stat(p)
        return (p, stt.st_mtime_ns, stt.st_size)
    except OSError:
        return (p, 0, 0)


def pair_correlation(C, sa, sb):
    """field_correlation of two Look albedos, cached by file identity (the same files are re-read by every stage and every mutant)."""
    k = (stem_key(C, sa), stem_key(C, sb))
    if k not in _PAIR_CACHE:
        _PAIR_CACHE[k] = field_correlation(tex_fields(look_png(C, sa)), tex_fields(look_png(C, sb)))
    return _PAIR_CACHE[k]


def g_albedo_distinct(S, C):
    """'each get a unique albedo': no two surface albedos are copies, tints, rotations, shifts or RECOLOURS of one another: the channel fields (luminance, R, G, B at
    64 x 64) of every pair correlate < 0.85 at every rotation, mirror and shift, WHATEVER their mean colours (v2 review: Scrub_C = Fairway_C with the hue
    shifted 28 degrees passed when a colour difference was enough). The mean-colour distance is reported, not used."""
    used = {m for m in mats_on(S) if m.startswith("LK_") and m in LK} | REQUIRED_LK[C.hole]
    items = []
    for mat, stem in DISTINCT_SET:
        if mat not in used:
            continue
        px = look_png(C, stem)
        if px is None:
            return False, f"{stem}.png missing"
        items.append((stem, srgb_to_lab(px[..., :3].reshape(-1, 3).mean(0))))
    bad, worst, npair = [], None, 0
    for i in range(len(items)):
        for j in range(i + 1, len(items)):
            a, b = items[i], items[j]
            de = float(np.linalg.norm(a[1] - b[1]))
            corr = pair_correlation(C, a[0], b[0])
            npair += 1
            if worst is None or corr > worst[1]:
                worst = (de, corr, a[0], b[0])
            if corr >= TH2["distinct_corr"]:
                bad.append(f"{a[0]} vs {b[0]} (pattern correlation {corr:.2f} >= {TH2['distinct_corr']}; mean colours dE {de:.1f})")
    return not bad, ("; ".join(bad) if bad else
                     f"{npair} pairs among {[i[0] for i in items]} are independent textures (channel-field correlation < {TH2['distinct_corr']} at every rotation, mirror and shift, whatever the colour)"
                     + (f"; closest {worst[2]} vs {worst[3]} correlation {worst[1]:.2f} (dE {worst[0]:.1f})" if worst else ""))


GRASS_BANDS = {  # albedo-space bands (the stills measure the LIT grass; the golf key shifts hue -6..-8 deg and lifts V: see GATES.md)
    "Fairway_C": dict(h=(66.0, 82.0), s=(0.50, 0.90), v=(0.40, 0.75)),
    "Green_C": dict(h=(66.0, 82.0), s=(0.50, 0.90), v=(0.40, 0.80)),
    "Rough_C": dict(h=(58.0, 78.0), s=(0.45, 0.90), v=(0.28, 0.65)),
}


def grass_metrics(px):
    rgb = px[..., :3].reshape(-1, 3)
    mh, ms, mv = mean_hsv(rgb.mean(0))
    h, s, v = hsv_arr(rgb[::7])
    sel = s >= 0.2
    hh = h[sel] if sel.any() else h
    return dict(h=mh, s=ms, v=mv, med=float(np.median(hh)), p10=float(np.percentile(hh, 10)), p90=float(np.percentile(hh, 90)))


def g_grass_hue(S, C):
    """Static half of 'the grass reads ~20 degrees too blue-green': mean hue / saturation / value of Fairway_C, Green_C and Rough_C inside
    the bands derived from the stills (hue 64-73 lit -> 66-82 in the albedo). The lit Game-view hue is measured in Unity (GATES.md)."""
    problems, rep = [], []
    for stem, band in GRASS_BANDS.items():
        px = look_png(C, stem)
        if px is None:
            problems.append(f"{stem}.png missing")
            continue
        m = grass_metrics(px)
        bad = []
        if not (band["h"][0] <= m["h"] <= band["h"][1]) or not (band["h"][0] <= m["med"] <= band["h"][1]):
            bad.append(f"hue mean {m['h']:.1f} / median {m['med']:.1f} outside {band['h'][0]:g}-{band['h'][1]:g}")
        if not (band["s"][0] <= m["s"] <= band["s"][1]):
            bad.append(f"S {m['s']:.2f} outside {band['s'][0]:g}-{band['s'][1]:g}")
        if not (band["v"][0] <= m["v"] <= band["v"][1]):
            bad.append(f"V {m['v']:.2f} outside {band['v'][0]:g}-{band['v'][1]:g}")
        if m["p10"] < TH2["hue_spread"][0] or m["p90"] > TH2["hue_spread"][1]:
            bad.append(f"pixel hue p10..p90 {m['p10']:.0f}..{m['p90']:.0f} leaves {TH2['hue_spread'][0]:g}-{TH2['hue_spread'][1]:g} (two-tone texture)")
        if bad:
            problems.append(f"{stem}: " + ", ".join(bad))
        lit_h = m["h"] - TH2["lit_shift"]
        rep.append(f"{stem} h {m['h']:.1f} S {m['s']:.2f} V {m['v']:.2f} (est. lit hue {lit_h:.0f})")
    return not problems, ("; ".join(problems) if problems else "grass albedos on the stills' hue: " + ", ".join(rep))


def lava_metrics(Cpx, Epx):
    comp = np.clip(Cpx[..., :3] + Epx[..., :3], 0.0, 1.0)
    k = max(1, min(comp.shape[:2]) // 256)
    cp = pool(comp, k)
    h, s, v = hsv_arr(comp)
    he, se, ve = hsv_arr(Epx[..., :3])
    bright = float(((v >= 0.55) & (h >= 5) & (h <= 40)).mean())
    dark = float((v < 0.35).mean())
    e_bright = float(((ve >= 0.50) & (he >= 5) & (he <= 40)).mean())
    peak = Epx[..., :3].reshape(-1, 3).max(0) * 255.0
    h2, s2, v2 = hsv_arr(cp)
    cells = {}
    for tag, mask in (("dark", v2 < 0.35), ("bright", (v2 >= 0.55) & (h2 >= 5) & (h2 <= 40))):
        lab, n = label_mask(mask)
        st = comp_stats(lab, n) if n else []
        tot = float(mask.sum())
        comp_ok = [c for c in st if not c["touches"] and c["area"] / c["hull"] >= 0.80 and 0.001 * mask.size <= c["area"] <= 0.06 * mask.size]
        cells[tag] = (len(comp_ok), sum(c["area"] for c in comp_ok) / max(tot, 1.0))
    nwin = 8
    ws = window_sd(v, max(1, min(v.shape) // nwin))
    return dict(bright=bright, dark=dark, e_bright=e_bright, peak=peak, cells=cells,
                seam_c=seam_ratio(Cpx), seam_e=seam_ratio(Epx), vsd=float(v.std()), win=float((ws >= TH2["lava_win_sd"]).mean()))


def g_lava_tex(S, C):
    """Lava_C / Lava_E: swirling molten flow, mostly bright orange from the emission map, not dark polygonal crust cells."""
    C_, E_ = look_png(C, "Lava_C"), look_png(C, "Lava_E")
    if C_ is None or E_ is None:
        return False, "Lava_C.png / Lava_E.png missing"
    if C_.shape[:2] != E_.shape[:2]:
        return False, f"Lava_C {C_.shape[:2]} and Lava_E {E_.shape[:2]} differ in size"
    m = lava_metrics(C_, E_)
    p = []
    if m["bright"] < TH2["lava_bright"]:
        p.append(f"bright orange (V >= .55, hue 5-40) only {m['bright']:.0%} of the C+E composite (need >= {TH2['lava_bright']:.0%})")
    if m["dark"] > TH2["lava_dark"]:
        p.append(f"dark crust (V < .35) {m['dark']:.0%} of the composite (need <= {TH2['lava_dark']:.0%})")
    if m["e_bright"] < TH2["lava_e_bright"]:
        p.append(f"emission alone is bright orange on only {m['e_bright']:.0%} (need >= {TH2['lava_e_bright']:.0%}: the lava must read orange without bloom or albedo light)")
    if m["vsd"] < TH2["lava_vsd"] or m["win"] < TH2["lava_win"]:
        p.append(f"no flow structure: composite value sd {m['vsd']:.3f} (need >= {TH2['lava_vsd']}), {m['win']:.0%} of the 1/8-tile windows reach sd {TH2['lava_win_sd']} "
                 f"(need >= {TH2['lava_win']:.0%}): a flat orange fill")
    pk = TH2["lava_e_peak"]
    if m["peak"][1] > pk[1] or m["peak"][2] > pk[2]:
        p.append(f"emission peak texel ({m['peak'][0]:.0f},{m['peak'][1]:.0f},{m['peak'][2]:.0f}) exceeds ({pk[0]},{pk[1]},{pk[2]}): yellow/cream clipping")
    for tag in ("dark", "bright"):
        n, share = m["cells"][tag]
        if n >= TH2["cell_min"] and share >= TH2["cell_share"]:
            p.append(f"cell network: {n} compact {tag} cells carry {share:.0%} of the {tag} area (>= {TH2['cell_min']} cells and >= {TH2['cell_share']:.0%} = Voronoi plates)")
    for nm, sr in (("Lava_C", m["seam_c"]), ("Lava_E", m["seam_e"])):
        if sr > TH2["seam"]:
            p.append(f"{nm} tiling seam error {sr:.1f}x the neighbour difference (> {TH2['seam']})")
    return not p, ("; ".join(p) if p else
                   f"flow, not cells: bright {m['bright']:.0%}, dark {m['dark']:.0%}, emission-alone bright {m['e_bright']:.0%}, value sd {m['vsd']:.2f}, peak "
                   f"({m['peak'][0]:.0f},{m['peak'][1]:.0f},{m['peak'][2]:.0f}), compact dark cells {m['cells']['dark'][0]}/bright {m['cells']['bright'][0]}, "
                   f"seam {max(m['seam_c'], m['seam_e']):.2f}x")


def basalt_metrics(Epx, Npx, tile_m):
    k = max(1, min(Epx.shape[:2]) // 512)
    L = pool(lum709(Epx), k)
    G = L >= TH2["glow_thr"]
    H, W = G.shape
    cov = float(G.mean())
    lab, n = label_mask(G)
    st = comp_stats(lab, n) if n else []
    small = TH2["glow_small"] * max(H, W)
    bad_area, tot_area = 0, 0
    for c in st:
        tot_area += c["area"]
        elong = c["h"] >= TH2["glow_elong"] * c["w"]
        if not (elong or max(c["w"], c["h"]) <= small):
            bad_area += c["area"]
    bad_frac = bad_area / max(tot_area, 1)
    run = longest_row_run(G) / W
    # cross joints and bevel planes from the normal map
    kn = max(1, min(Npx.shape[:2]) // 512)
    nx = pool(Npx[..., 0] * 2.0 - 1.0, kn)
    ny = pool(Npx[..., 1] * 2.0 - 1.0, kn)
    Hm = np.abs(ny) >= 0.25
    labn, nn = label_mask(Hm)
    cnt = np.zeros(Hm.shape[1])
    if nn:
        for c in comp_stats(labn, nn):
            if c["w"] >= 0.04 * Hm.shape[1] and c["h"] <= 0.03 * Hm.shape[0]:
                cnt[c["x0"]:c["x1"]] += 1
    jmean = float(cnt.mean())
    spacing = tile_m / max(jmean, 1e-9)
    prof = np.median(nx, axis=0)
    minrun = max(2, int(round(TH2["plane_min_w"] * len(prof))))

    def plane_frac(mask):
        d = np.diff(np.concatenate(([0], mask.astype(np.int8), [0])))
        st_, en_ = np.flatnonzero(d == 1), np.flatnonzero(d == -1)
        return float(sum(b - a for a, b in zip(st_.tolist(), en_.tolist()) if b - a >= minrun)) / len(prof)
    pos, neg = plane_frac(prof >= TH2["bevel_nx"]), plane_frac(prof <= -TH2["bevel_nx"])
    return dict(cov=cov, bad_frac=bad_frac, ncomp=len(st), run=run, jmean=jmean, spacing=spacing, pos=pos, neg=neg)


def g_basalt_tex(S, C):
    """Basalt reads as PRISMS with glow only in the joints: no brickwork cross-joints, no glowing ladders, planar bevel faces."""
    E_, N_ = look_png(C, "Basalt_E"), look_png(C, "Basalt_N")
    if E_ is None or N_ is None:
        return False, "Basalt_E.png / Basalt_N.png missing"
    m = basalt_metrics(E_, N_, LK["LK_BASALT"][0])
    p = []
    if m["cov"] >= TH2["glow_cov"]:
        p.append(f"glow covers {m['cov']:.1%} of the tile (need < {TH2['glow_cov']:.0%})")
    if m["bad_frac"] > TH2["glow_bad"]:
        p.append(f"{m['bad_frac']:.0%} of the glow area sits in components that are neither vertical-elongated (h/w >= {TH2['glow_elong']}) nor small "
                 f"(<= {TH2['glow_small']:.0%} of the tile): ladders / boxes (need <= {TH2['glow_bad']:.0%})")
    if m["run"] > TH2["glow_run"]:
        p.append(f"a horizontal glow run spans {m['run']:.0%} of the tile width (need <= {TH2['glow_run']:.0%}): ladder rungs")
    if m["spacing"] < TH2["joint_m"]:
        p.append(f"cross-joint spacing {m['spacing']:.1f} m ({m['jmean']:.1f} joints per column per tile) < {TH2['joint_m']:g} m: brickwork, not prisms")
    if m["pos"] < TH2["plane_share"] or m["neg"] < TH2["plane_share"]:
        p.append(f"bevel planes with |nx| >= {TH2['bevel_nx']}: +{m['pos']:.0%} / -{m['neg']:.0%} of the tile width (need >= {TH2['plane_share']:.0%} each; "
                 f"plane = run >= {TH2['plane_min_w']:.1%} wide)")
    return not p, ("; ".join(p) if p else
                   f"prisms: glow {m['cov']:.1%}, {m['bad_frac']:.0%} of glow in non-vertical blobs, longest horizontal run {m['run']:.1%}, cross-joint "
                   f"spacing {m['spacing']:.1f} m, bevel planes +{m['pos']:.0%}/-{m['neg']:.0%}")


def smoke_metrics(px):
    a = px[..., 3]
    H, W = a.shape
    border = float(max(a[:2].max(), a[-2:].max(), a[:, :2].max(), a[:, -2:].max()))
    yy, xx = np.mgrid[0:H, 0:W]
    tot = float(a.sum())
    if tot <= 1e-9:
        return dict(border=border, amax=0.0, elong=0.0, grad=0.0)
    w = a / tot
    cx, cy = float((w * xx).sum()), float((w * yy).sum())
    cxx, cyy, cxy = float((w * (xx - cx) ** 2).sum()), float((w * (yy - cy) ** 2).sum()), float((w * (xx - cx) * (yy - cy)).sum())
    ev = np.linalg.eigvalsh(np.array([[cxx, cxy], [cxy, cyy]]))
    elong = math.sqrt(max(ev[1], 1e-12) / max(ev[0], 1e-12))
    gy, gx = np.gradient(a)
    grad = float(np.hypot(gx, gy).max()) * (W / 512.0)
    return dict(border=border, amax=float(a.max()), elong=elong, grad=grad)


def g_smoke_tex(S, C):
    """Smoke_C is a soft rising wisp, not a hard grey ball: transparent border, alpha <= .6, elongated, no hard edge."""
    px = look_png(C, "Smoke_C")
    if px is None:
        return False, "Smoke_C.png missing"
    m = smoke_metrics(px)
    p = []
    if m["border"] > TH2["smoke_border"]:
        p.append(f"border alpha {m['border']:.3f} > {TH2['smoke_border']} (a card edge shows)")
    if m["amax"] > TH2["smoke_amax"]:
        p.append(f"alpha max {m['amax']:.2f} > {TH2['smoke_amax']} (opaque core)")
    if m["elong"] < TH2["smoke_elong"]:
        p.append(f"alpha elongation {m['elong']:.2f} < {TH2['smoke_elong']} (a round ball, not a rising wisp)")
    if m["grad"] > TH2["smoke_grad"]:
        p.append(f"max alpha gradient {m['grad']:.3f}/texel (at 512 px) > {TH2['smoke_grad']} (hard edge)")
    return not p, ("; ".join(p) if p else
                   f"soft wisp: border alpha {m['border']:.3f}, alpha max {m['amax']:.2f}, elongation {m['elong']:.2f}, max gradient {m['grad']:.3f}/texel")


# ----------------------------------------------------------------------------- v2 repair round 1: smoke must be READABLE in the proof frames
def smoke_runtime_spec():
    """LK_SMOKE's runtime parameters, parsed from GolfLook.cs (the gate follows the Unity owner's edits): the alpha the shader draws is
    pow(saturate(tex.a x _Color.a x AlphaGain), AlphaPower) x smoothstep(0, EdgeFade, uv edge distance)."""
    d = dict(gain=1.0, power=1.0, fade=0.0, ca=1.0, src="defaults (GolfLook.cs not found)")
    pth = os.path.join(REPO, "Unity", "Assets", "Scripts", "Course", "GolfLook.cs")
    try:
        txt = open(pth).read()
        k = txt.index('["LK_SMOKE"]')
        row = txt[k:txt.index("\n", k)]                                   # the table row is one line
        def num(name, default):
            m = re.search(name + r"\s*=\s*(-?[0-9.]+)f?", row)
            return float(m.group(1)) if m else default
        d.update(gain=num("AlphaGain", 1.0), power=num("AlphaPower", 1.0), fade=num("EdgeFade", 0.0), src="GolfLook.cs LK_SMOKE")
        mt = re.search(r"Tint\s*=\s*new Color\(([^)]*)\)", row)
        if mt:
            parts = [x.strip().rstrip("f") for x in mt.group(1).split(",")]
            if len(parts) >= 4:
                d["ca"] = float(parts[3])
    except (OSError, ValueError):
        pass
    return d


def smoke_effective_alpha(uv, a_tex, spec):
    """Effective alpha of the card at UV points (n, 2): bilinear Smoke_C alpha (row 0 = v 0) through the shader formula."""
    H, W = a_tex.shape
    u = np.clip(uv[:, 0], 0.0, 1.0) * (W - 1)
    v = np.clip(uv[:, 1], 0.0, 1.0) * (H - 1)
    x0, y0 = np.floor(u).astype(int), np.floor(v).astype(int)
    x1, y1 = np.minimum(x0 + 1, W - 1), np.minimum(y0 + 1, H - 1)
    fx, fy = u - x0, v - y0
    a = (a_tex[y0, x0] * (1 - fx) * (1 - fy) + a_tex[y0, x1] * fx * (1 - fy) + a_tex[y1, x0] * (1 - fx) * fy + a_tex[y1, x1] * fx * fy)
    a = np.power(np.clip(a * spec["ca"] * spec["gain"], 0.0, 1.0), spec["power"])
    if spec["fade"] > 1e-4:
        edge = np.minimum(np.minimum(uv[:, 0], 1.0 - uv[:, 0]), np.minimum(uv[:, 1], 1.0 - uv[:, 1]))
        t = np.clip(edge / spec["fade"], 0.0, 1.0)
        a = a * (t * t * (3.0 - 2.0 * t))
    return a


def smoke_shot_cameras(S, C, shots=None):
    """[(name, pos, forward, up, right, fov_v)] in Blender metres for the hole's proof shots. Course yards -> Blender metres: (x, d, y) * YD (the tee is the course origin and
    the world origin); an address shot is CameraRig.FrameAddress (4.5 yd behind the ball on the aim line, 2.4 yd up, looking 1.5 yd ahead of the ball), a landmark shot is the camera
    of the cfg (Unity world yards x, y up, z down the hole). The ball rests on what Unity's GroundHeight hits."""
    if shots is None:
        shots = None
        try:
            with open(STILL_CFG) as fh:
                cfg = json.load(fh)
            shots = [sh for sh in cfg.get("shots", []) if int(sh.get("hole", 0)) == C.hole]
        except (OSError, ValueError):
            shots = None
        if not shots:
            shots = [dict(sh, hole=C.hole) for sh in STILL_CFG_FALLBACK.get(C.hole, [])]
    cams = []
    Vz = Vector((0.0, 0.0, 1.0))
    for sh in shots:
        fov = float(sh.get("fov", SMOKE_VIS["fov"]) or SMOKE_VIS["fov"])
        if sh.get("kind") == "landmark" or (not sh.get("kind") and sh.get("pos")):
            px, py, pz = sh["pos"]
            lx, ly, lz = sh["look"]
            pos = Vector((px * YD, pz * YD, py * YD))
            fwd = Vector((lx * YD, lz * YD, ly * YD)) - pos
        else:
            bx, bd = sh["ball"]
            ax, ad = sh["aim"]
            hit, gz = on_land(S, C, bx * YD, bd * YD)
            ball = Vector((bx * YD, bd * YD, (gz if gz is not None else C.pz) + 0.06 * YD))
            aim = Vector(((ax - bx) * YD, (ad - bd) * YD, 0.0)).normalized()
            pos = ball - aim * SMOKE_VIS["addr_back"] * YD + Vz * SMOKE_VIS["addr_up"] * YD
            fwd = (ball + aim * SMOKE_VIS["addr_ahead"] * YD) - pos
        fwd.normalize()
        right = fwd.cross(Vz).normalized()
        up = right.cross(fwd).normalized()
        cams.append((sh.get("name", "shot"), pos, fwd, up, right, fov))
    return cams


def smoke_frame_alpha(S, C, cams=None, spec=None):
    """For every proof camera: the (Hp, Wp) grid of the accumulated effective alpha of the UNOCCLUDED smoke cards (1 - product of (1 - alpha) over up to 4 overlapping cards, each seen at its UV
    through the shader formula), by a ray grid. Occluders = every other opaque mesh of the hole (walls, terrain, rocks, plants, lava, water; not LK_SURF / LK_FALL cards).
    Returns ({name: array}, "") or (None / {}, reason)."""
    spec = spec or smoke_runtime_spec()
    a_png = look_png(C, "Smoke_C")
    if a_png is None:
        return None, "Smoke_C.png missing"
    a_tex = a_png[..., 3].astype(np.float64)
    cards = {n: o for n, o in S.meshes.items() if n.startswith("DRESS_SMOKE")}
    if not cards:
        return {}, "no DRESS_SMOKE_nn"
    V, T, UVt, off = [], [], [], 0
    for o in cards.values():
        w = S.wld(o)
        if len(w["T"]) == 0 or w["uv"] is None:
            continue
        V.append(w["W"])
        T.append(w["T"] + off)
        UVt.append(w["uv"])
        off += len(w["W"])
    if not V:
        return {}, "DRESS_SMOKE cards without UVs or faces"
    CV, CT, CUV = np.vstack(V), np.vstack(T), np.vstack(UVt)
    cbvh = BVHTree.FromPolygons([tuple(v) for v in CV.tolist()], [tuple(t) for t in CT.tolist()], all_triangles=True)
    occ = [o for n, o in S.meshes.items() if not n.startswith("DRESS_SMOKE") and not (S.used_mats(o) and S.used_mats(o) <= {"LK_SURF", "LK_FALL"})]
    obvh = bvh_of(S, occ)
    cams = cams if cams is not None else smoke_shot_cameras(S, C, C.opts.get("smoke_shots"))
    Wp, Hp = SMOKE_VIS["grid"]
    out = {}
    for (name, pos, fwd, up, right, fov) in cams:
        th = math.tan(math.radians(fov) / 2.0)
        aspect = 900.0 / 1600.0
        grid = np.zeros((Hp, Wp))
        for j in range(Hp):
            yv = (1.0 - (j + 0.5) / Hp * 2.0) * th
            for i in range(Wp):
                xv = ((i + 0.5) / Wp * 2.0 - 1.0) * th * aspect
                d = (fwd + right * xv + up * yv).normalized()
                dist_occ = 1e9
                if obvh is not None:
                    loc, _n, _idx, dd = obvh.ray_cast(pos, d)
                    if loc is not None:
                        dist_occ = dd
                o_ = Vector(pos)
                trans, travelled = 1.0, 0.0
                for _ in range(4):
                    loc, _n, idx, dd = cbvh.ray_cast(o_, d)
                    if loc is None or travelled + dd >= dist_occ:
                        break
                    t = CT[idx]
                    A_, B_, C_ = CV[t[0]], CV[t[1]], CV[t[2]]
                    p_ = np.array(loc)
                    v0, v1, v2 = B_ - A_, C_ - A_, p_ - A_
                    d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
                    den = d00 * d11 - d01 * d01
                    if abs(den) > 1e-12:
                        bv = (d11 * d20 - d01 * d21) / den
                        bw = (d00 * d21 - d01 * d20) / den
                        bu = 1.0 - bv - bw
                        uvp = CUV[idx][0] * bu + CUV[idx][1] * bv + CUV[idx][2] * bw
                        trans *= 1.0 - float(smoke_effective_alpha(np.array([uvp]), a_tex, spec)[0])
                    travelled += dd + 1e-3
                    o_ = Vector(loc) + d * 1e-3
                grid[j, i] = 1.0 - trans
        out[name] = grid
    return out, ""


def smoke_frame_shares(S, C, cams=None, spec=None, alpha_min=None):
    """{shot name: share of the frame covered by an unoccluded plume at accumulated effective alpha >= alpha_readable}."""
    grids, why = smoke_frame_alpha(S, C, cams, spec)
    if not grids:
        return grids, why
    am = SMOKE_VIS["alpha_readable"] if alpha_min is None else alpha_min
    return {k: float((g >= am).mean()) for k, g in grids.items()}, ""


def g_smoke_frames(S, C):
    """SMOKE_IN_FRAME (hole 10): a plume the user can pick out in EACH proof still (tee, approach, lavarim): >= 1.5 % of the frame covered by an unoccluded smoke card at effective alpha >= .30
    (LK_SMOKE read from GolfLook.cs). The geometric twin of SMOKE_VISIBLE_STILLS (Unity numbers): it runs on the .blend / FBX in seconds, so the hole builder can iterate without Unity."""
    shares, why = smoke_frame_shares(S, C)
    if shares is None or not shares:
        return False, why
    bad = {k: v for k, v in shares.items() if v < SMOKE_VIS["share_min"]}
    rep = ", ".join(f"{k} {v * 100:.2f} %" for k, v in shares.items())
    if bad:
        return False, (f"smoke is not readable in {len(bad)} of {len(shares)} proof frames (need >= {SMOKE_VIS['share_min'] * 100:.1f} % of the frame covered by an unoccluded plume at effective alpha >= "
                       f"{SMOKE_VIS['alpha_readable']}): {rep}; the cards stand behind the wall / above the frame or are too small: place them inside the frames (above the cone, in the wall window, behind the pillar) and size them up")
    return True, f"a readable plume in every proof frame (>= {SMOKE_VIS['share_min'] * 100:.1f} % of the frame at effective alpha >= {SMOKE_VIS['alpha_readable']}): {rep}"


def g_smoke_stills(S_or_C=None, C=None):
    """SMOKE_VISIBLE_STILLS (hole 10, Game view): PostcardLookStills renders every proof shot twice (with and without the DRESS_SMOKE cards) and writes stills_stats.json "smoke": share_readable =
    the share of the frame the plume moves by >= 10 luminance levels. Each Crater still needs >= 1.5 %. SKIPPED (PASS, said so) while there are no stats, FAIL with --require-stills."""
    C = C or S_or_C
    need = bool(C.opts.get("require_stills"))
    d0 = C.opts.get("stills_dir")
    cands = [os.path.join(d0 or STILLS_DIR, "stills_stats.json")]
    if d0 and os.path.isdir(d0):                                  # a copy of the PNGs (run_proof.sh gate_stills) carries no stats: the tool's own stats next to the real stills
        cands.append(os.path.join(STILLS_DIR, "stills_stats.json"))
    stats = None
    for pth in cands:
        if os.path.isfile(pth):
            try:
                with open(pth) as fh:
                    stats = (json.load(fh), pth)
                break
            except (OSError, ValueError):
                continue
    if stats is None:
        info("SMOKE_VISIBLE_STILLS", f"SKIPPED: no stills_stats.json in {rel(cands[0])}" + ("" if not need else " (--require-stills)"))
        return (not need), (f"SKIPPED (no stills_stats.json yet; --require-stills makes this a FAIL)" if not need else f"no stills_stats.json in {rel(cands[0])} (--require-stills)")
    data, pth = stats
    shots = [sh for sh in data.get("shots", []) if int(sh.get("hole", 0)) == C.hole and not sh.get("name", "").endswith("aerial")]
    if not shots:
        return (not need), f"stats {rel(pth)} hold no shot of hole {C.hole}" + ("" if not need else " (--require-stills)")
    problems, rep = [], []
    for sh in shots:
        sm = sh.get("smoke")
        if sm is None:
            problems.append(f"{sh['name']}: stats carry no 'smoke' block (stills tool older than v2 repair round 1: re-shoot)")
            continue
        sr = float(sm.get("share_readable", 0.0))
        rep.append(f"{sh['name']} {sr * 100:.2f} % readable ({sm.get('changed_px', 0)} px change, |dLum| median {sm.get('delta_lum_median', 0)} p95 {sm.get('delta_lum_p95', 0)})")
        if sr < SMOKE_VIS["share_min"]:
            problems.append(f"{sh['name']}: the plume moves only {sr * 100:.2f} % of the frame by >= {SMOKE_VIS['readable_delta']:.0f} levels (need >= {SMOKE_VIS['share_min'] * 100:.1f} %)")
    return not problems, ("; ".join(problems) if problems else "a readable plume in every Game-view still: " + "; ".join(rep))


def uv_density(L, mask):
    """area-weighted UV density (uv units per mesh metre) of the triangles in mask, and the area fraction whose own density is
    outside 1/4..4 x that mean."""
    uv = L["uv"][mask]
    a = L["A"][mask]
    e1 = uv[:, 1] - uv[:, 0]
    e2 = uv[:, 2] - uv[:, 0]
    ua = 0.5 * np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0])
    tot = a.sum()
    if tot <= 1e-12:
        return None, 0.0, ua
    dens = math.sqrt(ua.sum() / tot)
    per = np.sqrt(ua / np.maximum(a, 1e-12))
    return dens, ua, per


def g_uv(S, C):
    problems, rep, seen = [], {}, set()
    nouv = sorted(n for n, o in S.meshes.items() if (n.startswith(COLL_PREFIXES) or n.startswith(("ROCK_", "CLIFF_ROCK")))
                  and S.loc(o)["uv"] is None)
    if nouv:
        problems.append(f"{len(nouv)} ground/rock meshes have no UV map (a texture would render as one flat colour): {short(nouv, 6)}")
    for n, o in S.meshes.items():
        k = S.key(o)
        if k in seen:
            continue
        seen.add(k)
        L = S.loc(o)
        if L["ntri"] == 0:
            continue
        for mi in np.unique(L["mi"]).tolist():
            m = L["names"][mi]
            if m not in LK:
                continue
            tile = LK[m][0]
            if tile is None:
                continue
            mask = (L["mi"] == mi) & (L["A"] > 1e-10)
            if not mask.any():
                continue
            if L["uv"] is None:
                problems.append(f"{n}: no UV map ({m})")
                continue
            if tile == "atlas":
                u = L["uv"][mask].reshape(-1, 2)
                inside = ((u >= -0.01) & (u <= 1.01)).all(axis=1).mean()
                if inside < 0.99:
                    problems.append(f"{n}: {m} atlas UVs outside 0..1 ({1 - inside:.1%})")
                continue
            if tile in ("sheet", "card"):
                u = L["uv"][mask].reshape(-1, 2)
                if float(np.ptp(u[:, 0])) < 1e-3 and float(np.ptp(u[:, 1])) < 1e-3:
                    problems.append(f"{n}: {m} UVs collapsed")
                continue
            dens, ua, per = uv_density(L, mask)
            if dens is None:
                continue
            want = 1.0 / tile
            r = dens / want
            mean_ratio_ok = UV_RATIO[0] <= r <= UV_RATIO[1]
            a = L["A"][mask]
            bad = (per < want / 4.0) | (per > want * 4.0)
            sf = float(a[bad].sum() / a.sum())
            rep.setdefault(m, []).append(r)
            an_mean, an_frac = uv_anisotropy(L, mask)
            if not mean_ratio_ok or sf > UV_STRETCH_MAX:
                problems.append(f"{n}: {m} UV density {dens:.4f}/m = {r:.2f} x contract 1/{tile:g} m"
                                f"{'' if mean_ratio_ok else ' (need 1/3..3 x)'}, {sf:.0%} of its area stretched/collapsed (> 4x off)")
            elif an_frac > TH2["uv_aniso_frac"]:
                problems.append(f"{n}: {m} UVs are smeared: {an_frac:.0%} of the area has UV anisotropy > {TH2['uv_aniso']:g}:1 (need <= {TH2['uv_aniso_frac']:.0%}; mean {an_mean:.1f}:1)")
    core = {"LK_FAIRWAY", "LK_GREEN", "LK_ROUGH", "LK_SAND"} | ({"LK_CLIFF", "LK_ROCK"} if C.hole in (8, 9) else {"LK_BASALT"})
    unmeasured = sorted(core - set(rep))
    if unmeasured:
        problems.append(f"core surface materials not measured (absent): {unmeasured}")
    summ = {m: f"{min(v):.2f}..{max(v):.2f}x" for m, v in sorted(rep.items())}
    return not problems, ("; ".join(problems[:10]) + (f" (+{len(problems) - 10} more)" if len(problems) > 10 else "") if problems else
                          f"every tiled LK_ surface has UV0 at its contract density (ratio range per material {summ}), no smear (UV anisotropy <= {TH2['uv_aniso']:g}:1 on "
                          f">= {1 - TH2['uv_aniso_frac']:.0%} of every material's area); atlas/sheet/card UVs valid")


# v2 repair round 2 (2026-10-04, review "near grass smeared horizontally", hole 10 lava-rim still): UV_DENSITY lets 15 % of a material's AREA be smeared beyond 4:1, which is right for a
# whole mesh (a wall draped over rocks) and blind to a LOCAL defect on the play surfaces: hole 10's installed FAIRWAY_FIRSTCUT has 3 triangles (103 m2, the largest 72.8 m2, 1.8 % of its area)
# whose UV Jacobian is 26:1 / 4.7:1 / collapsed, and the far fairway of the lava-rim still shows them as horizontal streaks that no filtering level removes. Measured on the three installed holes
# (blend, `v2/unity_r2/uv_ground.py`, worst triangle ratio per ground mesh): holes 8 and 9 every FAIRWAY / FIRSTCUT / GREEN / APRON / TEE_BOX / BUNKER mesh <= 1.3:1, TERRAIN <= 3.3:1 (0 triangles
# over 4:1); hole 10 every mesh <= 3.4:1 except FAIRWAY_FIRSTCUT (above) and TERRAIN_CRATER (3 triangles, 5.2 m2 in all, largest 2.3 m2, worst 6.4:1). The limits sit between: 15 m2 in all and
# 10 m2 for any one triangle (a 3 x 3 m patch is what a golfer's camera, 2.2 m up, resolves as a streak), i.e. 2.9x above the largest legal mesh and 6.9x below the defect.
UV_SMEAR = dict(ratio=4.0, total_m2=15.0, single_m2=10.0)


def uv_aniso_world(W, T, uv):
    """Per triangle: UV -> WORLD Jacobian singular-value ratio (1e3 where the UV triangle collapses to a line) and the world area. Closed form: the 2 x 2 Gram matrix of dP/du, dP/dv."""
    a, b, c = W[T[:, 0]], W[T[:, 1]], W[T[:, 2]]
    e1, e2 = b - a, c - a
    area = 0.5 * np.linalg.norm(np.cross(e1, e2), axis=1)
    d1, d2 = uv[:, 1] - uv[:, 0], uv[:, 2] - uv[:, 0]
    det = d1[:, 0] * d2[:, 1] - d2[:, 0] * d1[:, 1]
    good = np.abs(det) > 1e-12
    sd = np.where(good, det, 1.0)[:, None]
    Pu = (e1 * d2[:, 1:2] - e2 * d1[:, 1:2]) / sd
    Pv = (e2 * d1[:, 0:1] - e1 * d2[:, 0:1]) / sd
    g11, g22, g12 = (Pu * Pu).sum(1), (Pv * Pv).sum(1), (Pu * Pv).sum(1)
    tr, dt = g11 + g22, g11 * g22 - g12 * g12
    disc = np.sqrt(np.maximum(tr * tr / 4.0 - dt, 0.0))
    lmax, lmin = tr / 2.0 + disc, np.maximum(tr / 2.0 - disc, 1e-30)
    ratio = np.where(good, np.sqrt(lmax / lmin), 1e3)
    return np.minimum(ratio, 1e3), area


def g_uv_local_smear(S, C):
    """GROUND_UV_LOCAL_SMEAR: no play-surface (collision-prefix) mesh carries a patch of smeared UVs: triangles with UV anisotropy > 4:1 add up to <= 15 m2 per mesh, none larger than 10 m2."""
    th = UV_SMEAR
    problems, rows, nouv = [], [], []
    for n, o in sorted(S.meshes.items()):
        if not n.startswith(COLL_PREFIXES):
            continue
        Wd = S.wld(o)
        if Wd["uv"] is None:
            nouv.append(n)                       # a missing UV map is UV_DENSITY's finding
            continue
        ratio, ar = uv_aniso_world(Wd["W"], Wd["T"], Wd["uv"])
        keep = ar > 1e-9
        ratio, ar = ratio[keep], ar[keep]
        if not len(ar):
            continue
        big = ratio > th["ratio"]
        tot = float(ar[big].sum())
        single = float(ar[big].max()) if big.any() else 0.0
        worst = float(ratio.max())
        rows.append((n, tot, single, worst, int(big.sum())))
        if tot > th["total_m2"] or single > th["single_m2"]:
            problems.append(f"{n}: {int(big.sum())} triangle(s) with UV anisotropy > {th['ratio']:g}:1 cover {tot:.0f} m2 (largest {single:.0f} m2, worst {worst:.0f}:1; "
                            f"need <= {th['total_m2']:g} m2 in all and <= {th['single_m2']:g} m2 each): a streak in the Game view at any filtering level")
    if not rows:
        return False, "no play-surface mesh with a UV map to measure" + (f" (no UV: {short(nouv, 6)})" if nouv else "")
    worst_row = max(rows, key=lambda r: r[1])
    return not problems, ("; ".join(problems[:6]) if problems else
                          f"{len(rows)} play-surface meshes: UV anisotropy > {th['ratio']:g}:1 covers at most {worst_row[1]:.1f} m2 per mesh ({worst_row[0]}; limit {th['total_m2']:g} m2 in all, {th['single_m2']:g} m2 for one triangle), "
                          f"worst triangle {max(r[3] for r in rows):.1f}:1" + (f"; no UV map (UV_DENSITY judges): {short(nouv, 4)}" if nouv else ""))


# ----------------------------------------------------------------------------- gates: rocks and props
ROCK_TH = dict(min_verts=60, min_faces=100, facet_deg=16.0, facet_share=0.02, min_facets=6, blob_radius_cv=0.15, tess_edge_cv=0.30,
               tess_area_cv=0.45, tess_radius_cv=0.25, box_share=0.85, comp_share=0.15, tiny_face=0.01, regular_area_cv=0.05,
               regular_edge_cv=0.12,
               # v2 review fix: EVERY component of a rock is judged on its own vertex-valence topology (a bundle of jittered icosahedra is not a rock)
               comp_min_verts=6, comp_max_valences=2, comp_regular_max_verts=64, comp_geo_frac=0.95, comp_geo_min_verts=30, comp_geo_min5=8, comp_geo_max5=16, prism_vertical=0.60,
               stretch_r3_min=0.15, max_open_edge=0.10,
               fam_ratio=0.04, fam_nv_tol=0.10, fam_nfac=2)

# v2 area U (2026-10-04, review of the rock gates): a rock is recognised by its SHAPE and its MATERIAL, never by its name alone. Every constant below is documented in GATES.md ("v2 area U").
ROCK_MATS = ROCKISH | {"MAT_ROCK", "MAT_ROCK_DARK", "MAT_CLIFF", "MAT_CLIFF_DARK"}   # a mesh that wears these IS a rock (legacy MAT_ names too: renaming a material is not an escape)
ROCK_MAT_SHARE = 0.5                          # share of a mesh's area on ROCK_MATS that makes it a rock, whatever the object is called (DRESS_BOULDER, PROP_3, ...)
SKIN_SHELL = dict(min_open=0.05, min_extent=3.0)   # cliff cladding (ROCK_SKIN_*) is an OPEN SHELL: >= 5 % open polygon edges and >= 3 m across. A CLOSED compact solid called ROCK_SKIN_xx is a rock
TINY_SOLID_MAX_VERTS = 64                     # a closed solid of <= 64 welded vertices that is not a plant / masonry block / gameplay placeholder is a toy rock (12-vertex icosahedron, 13-vertex poked icosahedron, cube ...)
TINY_SOLID_EXEMPT_MATS = ("LK_PLANTS", "LK_MASONRY")   # shrubs / tufts are blobs; masonry blocks are boxes (the arch / ruin gates judge them)
ROCK_BACKDROP_M = 100.0                       # a rock-material mesh larger than this in any direction is a backdrop (the Crater cone), not a rock of the course: no variety credit, no PCA cap
STRETCH_TH = dict(aniso=2.0, upper=2.0, lower=0.4)   # NO_ROCK_STRETCH_2X: STRETCH = anisotropy (max/min scale) > 2.0; the absolute cap is 2.0; a uniform shrink is not a stretch and only has a lower bound (0.4)


def weld_ids(V, tol=1e-4):
    """Welded vertex id per vertex (merge by distance ~tol). Two half-cell shifted rounding grids; the one that merges more wins, so a
    pair that straddles a rounding boundary in one grid cannot straddle it in the other."""
    best = None
    for off in (0.0, 0.5):
        q = np.floor(V / tol + off).astype(np.int64)
        u, inv = np.unique(q, axis=0, return_inverse=True)
        if best is None or len(u) < best[0]:
            best = (len(u), inv.ravel())
    return best[1]


def facet_clusters(N, A, deg, share):
    """Greedy area-weighted clustering of face normals into cones of `deg` degrees (seeds = largest area first): clusters that hold
    >= `share` of the total area as [(unit normal, area share)]."""
    tot = float(A.sum()) or 1.0
    order = np.argsort(-A)
    left = np.ones(len(N), bool)
    cos_t = math.cos(math.radians(deg))
    out = []
    for i in order.tolist():
        if not left[i]:
            continue
        sel = left & (N @ N[i] >= cos_t)
        sh = float(A[sel].sum()) / tot
        left &= ~sel
        if sh >= share:
            v = (N[sel] * A[sel][:, None]).sum(0)
            out.append((v / (np.linalg.norm(v) or 1.0), sh))
    return out


def tess_numbers(P, T, A):
    """(radius cv of the PCA-whitened vertices = 0 for any sphere whatever its stretch, edge-length cv, face-area cv, PCA std per axis)."""
    used = np.unique(T)
    Q = P[used]
    c = Q.mean(0)
    cov = np.cov((Q - c).T) if len(Q) > 3 else np.eye(3)
    ev, evec = np.linalg.eigh(cov)
    ev = np.maximum(ev, 1e-12)
    r = np.linalg.norm(((Q - c) @ evec) / np.sqrt(ev), axis=1)
    rcv = float(r.std() / max(r.mean(), 1e-9))
    e = np.unique(np.sort(np.concatenate([T[:, [0, 1]], T[:, [1, 2]], T[:, [2, 0]]]), axis=1), axis=0)
    el = np.linalg.norm(P[e[:, 0]] - P[e[:, 1]], axis=1)
    ecv = float(el.std() / max(el.mean(), 1e-9)) if len(el) else 0.0
    acv = float(A.std() / max(A.mean(), 1e-9))
    return rcv, ecv, acv, np.sqrt(ev[::-1])


def mesh_shape(P, T, N, A, th, PN=None, PA=None):
    """Shape numbers of a welded triangle soup (P points, T triangles into P, N unit normals, A areas): planar-facet orientations
    (clustered on the POLYGONS PN / PA when given: a facet is a polygon), sphericity, edge / area variation, boxiness.
    Returns dict(why = list of failed tests)."""
    cl = facet_clusters(N if PN is None else PN, A if PA is None else PA, th["facet_deg"], th["facet_share"])
    rcv, ecv, acv, ext = tess_numbers(P, T, A)
    box = 0.0
    if cl:
        n1 = cl[0][0]
        n2 = next((n for n, _s in cl[1:] if abs(float(n @ n1)) < 0.25), None)
        if n2 is not None:
            n3 = np.cross(n1, n2)
            n3 = n3 / (np.linalg.norm(n3) or 1.0)
            cs = math.cos(math.radians(10.0))
            inside = (np.abs(N @ n1) >= cs) | (np.abs(N @ n2) >= cs) | (np.abs(N @ n3) >= cs)
            box = float(A[inside].sum() / max(A.sum(), 1e-12))
    why = []
    if len(cl) < th["min_facets"]:
        why.append(f"{len(cl)} planar facet orientations < {th['min_facets']}")
    if rcv < th["blob_radius_cv"]:
        why.append(f"sphere-like (whitened radius cv {rcv:.3f} < {th['blob_radius_cv']})")
    elif ecv < th["tess_edge_cv"] and acv < th["tess_area_cv"] and rcv < th["tess_radius_cv"]:
        why.append(f"regular tessellation of a round solid (edge cv {ecv:.2f}, area cv {acv:.2f}, radius cv {rcv:.2f})")
    if box >= th["box_share"]:
        why.append(f"box ({box:.0%} of the area on three orthogonal planes)")
    top = sorted((s for _n, s in cl), reverse=True)[:6]
    return dict(nfacets=len(cl), rcv=rcv, ecv=ecv, acv=acv, box=box, why=why, ext=tuple(ext.tolist()), top=top)


def poly_valences(T, tp):
    """Polygon-level topology of a welded triangle soup: T (n, 3) welded vertex ids, tp (n,) polygon id of each triangle. A diagonal (the two
    triangles of one quad / n-gon) is not an edge of the polygon mesh. Returns (valence {vertex: number of polygon edges}, set of vertices on an
    OPEN edge (used by a single triangle))."""
    ed = {}
    for t, pid in zip(T.tolist(), tp.tolist()):
        for a, b in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
            k = (a, b) if a < b else (b, a)
            e = ed.get(k)
            if e is None:
                ed[k] = [1, {pid}]
            else:
                e[0] += 1
                e[1].add(pid)
    val, bnd = {}, set()
    for (a, b), (n, pids) in ed.items():
        if n == 2 and len(pids) == 1:
            continue                                                  # diagonal inside one polygon
        val[a] = val.get(a, 0) + 1
        val[b] = val.get(b, 0) + 1
        if n == 1:
            bnd.add(a)
            bnd.add(b)
    return val, bnd


def open_edge_fraction(Tk, tpk):
    """Share of the POLYGON edges (a diagonal inside one polygon is not an edge) of a welded mesh that belong to a single polygon. A rock is a closed solid (the library's
    hull rocks and stacks: 0 %); faces that were split off by a few millimetres - beating the 1e-4 weld - leave every edge open (100 %)."""
    cnt = {}
    for t, pid in zip(Tk.tolist(), tpk.tolist()):
        for a, b in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
            k = (a, b) if a < b else (b, a)
            c = cnt.get(k)
            if c is None:
                cnt[k] = [1, {pid}]
            else:
                c[0] += 1
                c[1].add(pid)
    n = o = 0
    for c in cnt.values():
        if c[0] == 2 and len(c[1]) == 1:
            continue
        n += 1
        o += 1 if c[0] == 1 else 0
    return o / max(n, 1)


def manifold_patches(P, Tk, tpk):
    """Split a welded polygon mesh into SURFACE PATCHES (the v2 round-3 review: 6-14 exact 12-vertex icosahedra that merely TOUCH at shared vertices weld into
    ONE connected component whose valences are {5, 10, 15, ...}: no valence rule on the welded component can see the cells). Polygons are joined only across
    MANIFOLD edges (an edge used by exactly two polygons); a vertex (or an edge used by 3+ polygons) shared by cells that only touch is DUPLICATED per patch, so
    every touching icosahedron / icosphere / octahedron / cube comes back out as its own closed, regular solid. A real hull stone is one manifold patch, so a
    natural rock (and the library's welded bundles) is unchanged. P (n, 3) welded points, Tk (m, 3) welded triangle ids, tpk (m,) polygon id per triangle.
    Returns (P2, T2, n_patches): points / triangle ids renumbered per patch (T2 rows align with Tk rows)."""
    ed = {}
    for t, pid in zip(Tk.tolist(), tpk.tolist()):
        for a, b in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
            k = (a, b) if a < b else (b, a)
            ed.setdefault(k, set()).add(pid)
    par = {}

    def find(a):
        par.setdefault(a, a)
        while par[a] != a:
            par[a] = par[par[a]]
            a = par[a]
        return a
    for pids in ed.values():
        if len(pids) == 2:
            a, b = tuple(pids)
            par[find(a)] = find(b)
    patch = np.array([find(pid) for pid in tpk.tolist()], np.int64)
    key = {}
    old = []
    T2 = np.zeros_like(Tk)
    for i, (t, pa) in enumerate(zip(Tk.tolist(), patch.tolist())):
        for j in range(3):
            kk = (pa, t[j])
            v = key.get(kk)
            if v is None:
                v = len(old)
                key[kk] = v
                old.append(t[j])
            T2[i, j] = v
    return P[np.array(old, np.int64)], T2, int(len(np.unique(patch)))


def rock_components(P, Tk, tpk, Nk, Ak, th):
    """Judge every connected component of a rock on its own (the v2 review: a bundle of 5-8 jittered 12-vertex icosahedra welded into one mesh has
    60-96 vertices, 100-160 faces and passed the whole-mesh tests). Per component, on its INTERIOR vertices (not on an open edge) in the polygon mesh:
      * regular solid = 6..64 interior vertices that take <= 2 distinct valences (icosahedron {5}, octahedron {4}, cube / dodecahedron / football {3},
        any stellated / capped variant of them: jitter does not change valences), or a larger such lattice that is ROUND (whitened radius cv < 0.15: a
        UV sphere); a lofted spire / tube (quad rings, valence 4) is not round and is fine;
      * geodesic sphere = >= 30 interior vertices, >= 95 % valence 5 or 6 with 8..16 vertices of valence 5 (a subdivided icosahedron, noised or not;
        below 30 vertices a natural small hull is often 5/6-only too: the library's 20-vertex ROCK_CRAG_A component is {3:1, 5:11, 6:8}).
    Basalt prisms (>= 60 % of the area on vertical faces) are exempt: a column is a valence-3 prism by definition.
    The library's hull stones (15-38 vertices per component, valences 3..8, >= 4 distinct) and its spires pass. Returns dict(n, regular, geodesic)."""
    val, bnd = poly_valences(Tk, tpk)
    lab = tri_components(Tk)
    vcomp = {}
    for l, t in zip(lab.tolist(), Tk.tolist()):
        for v in t:
            vcomp[v] = l
    byc = {}
    for v, c in vcomp.items():
        if v not in bnd:
            byc.setdefault(c, []).append(val.get(v, 0))
    reg, geo, ncomp = [], [], 0
    for c in np.unique(lab).tolist():
        ncomp += 1
        sel = lab == c
        vert = float(Ak[sel][np.abs(Nk[sel][:, 2]) < 0.15].sum() / max(Ak[sel].sum(), 1e-12))
        vals = byc.get(c, [])
        if len(vals) < th["comp_min_verts"] or vert >= th["prism_vertical"]:
            continue
        distinct = sorted(set(vals))
        # a small closed solid with <= 2 valences is a platonic-type solid; a LARGE regular lattice (a lofted spire, a tube) is only a sphere in disguise when it is round
        if len(distinct) <= th["comp_max_valences"] and (len(vals) <= th["comp_regular_max_verts"] or tess_numbers(P, Tk[sel], Ak[sel])[0] < th["blob_radius_cv"]):
            reg.append((len(vals), {k: vals.count(k) for k in distinct}))
        elif len(vals) >= th["comp_geo_min_verts"]:
            f56 = sum(1 for x in vals if x in (5, 6)) / len(vals)
            if f56 >= th["comp_geo_frac"] and th["comp_geo_min5"] <= vals.count(5) <= th["comp_geo_max5"]:
                geo.append((len(vals), {k: vals.count(k) for k in distinct}))
    return dict(n=ncomp, regular=reg, geodesic=geo)


def rock_shape(L, th=None):
    """Judge the WELDED shape of a mesh (loc() arrays), not the raw vertex count: merge by distance 1e-4, drop degenerate / tiny /
    duplicate faces, then require on the real surface >= 60 welded vertices and >= 100 faces, >= 6 facet orientations (16 degree cones
    holding >= 2 % of the area), not sphere-like (PCA-whitened radius cv < 0.15), not a regular tessellation of a round solid (edge cv
    < 0.30, area cv < 0.45), not a box, and no connected component holding >= 15 % of the area that is an exact regular solid (an
    icosahedron, an octahedron: all edges / all faces equal). Icosahedra split or not, subdivided or noised icospheres, UV spheres and
    'icosahedron + junk' all fail one of these; the library's chipped hull rocks (cv 0.26-0.38, area cv 0.5-1.2) pass."""
    th = th or ROCK_TH
    out = dict(nv=0, nf=0, ok=False, why=["empty"], hash=None, nfac=0, rcv=0.0)
    if L["ntri"] == 0:
        return out
    wid = weld_ids(L["V"])
    nw = int(wid.max()) + 1
    cnt = np.bincount(wid, minlength=nw).astype(float)
    P = np.zeros((nw, 3))
    np.add.at(P, wid, L["V"])
    P /= np.maximum(cnt, 1)[:, None]
    Tw = wid[L["T"]]
    A = L["A"]
    ok = (Tw[:, 0] != Tw[:, 1]) & (Tw[:, 1] != Tw[:, 2]) & (Tw[:, 0] != Tw[:, 2]) & (A > 1e-12)
    if ok.any():
        ok &= A >= th["tiny_face"] * float(np.median(A[ok]))
    key = np.sort(Tw, axis=1)
    _u, first = np.unique(key, axis=0, return_index=True)
    dup = np.ones(len(Tw), bool)
    dup[first] = False
    ok &= ~dup
    if not ok.any():
        return out
    Tk, Ak, Nk, tpk = Tw[ok], A[ok], L["N"][ok], L["tp"][ok]
    nv = int(len(np.unique(Tk)))
    nf = int(len(np.unique(tpk)))
    up, uinv = np.unique(tpk, return_inverse=True)
    PA = np.bincount(uinv, weights=Ak)
    PN = np.zeros((len(up), 3))
    np.add.at(PN, uinv, Nk * Ak[:, None])
    PN /= np.maximum(np.linalg.norm(PN, axis=1), 1e-12)[:, None]
    ms = mesh_shape(P, Tk, Nk, Ak, th, PN, PA)
    why = list(ms["why"])
    # components = SURFACE PATCHES (manifold_patches): cells that only touch at a vertex / an edge are separate solids (the v2 round-3 review: welded clusters of
    # 12-vertex icosahedra read as one component with valences {5, 10, 15, ...})
    P2, T2, npatch = manifold_patches(P, Tk, tpk)
    lab = tri_components(T2)
    tot = float(Ak.sum())
    for c in np.unique(lab):
        m = lab == c
        share = float(Ak[m].sum()) / tot
        if share < th["comp_share"]:
            continue
        rcv, ecv, acv, _e = tess_numbers(P2, T2[m], Ak[m])
        if acv < th["regular_area_cv"] and ecv < th["regular_edge_cv"]:
            why.append(f"a component holding {share:.0%} of the area is an exact regular solid (edge cv {ecv:.2f}, area cv {acv:.2f}): icosahedron / octahedron")
    rc = rock_components(P2, T2, tpk, Nk, Ak, th)
    if rc["regular"]:
        why.append(f"{len(rc['regular'])} of {rc['n']} components are regular solids (<= {th['comp_max_valences']} distinct vertex valences, e.g. {rc['regular'][0][1]} on {rc['regular'][0][0]} vertices): a bundle of icosahedra / octahedra / cubes")
    if rc["geodesic"]:
        why.append(f"{len(rc['geodesic'])} of {rc['n']} components are geodesic spheres (>= {th['comp_geo_frac']:.0%} valence 5/6, {rc['geodesic'][0][1]}): subdivided icosahedra")
    ofr = open_edge_fraction(Tk, tpk)
    vert_all = float(Ak[np.abs(Nk[:, 2]) < 0.15].sum() / max(Ak.sum(), 1e-12))
    if ofr > th["max_open_edge"] and vert_all < th["prism_vertical"]:
        why.append(f"{ofr:.0%} of the polygon edges are open (a rock is a closed solid, the library's hull rocks are 0 %; faces split off by a few millimetres beat the weld): need <= {th['max_open_edge']:.0%}")
    if nv < th["min_verts"]:
        why.append(f"{nv} welded vertices < {th['min_verts']}")
    if nf < th["min_faces"]:
        why.append(f"{nf} faces < {th['min_faces']}")
    # shape hash: welded vertex / face counts + the vertex-valence histogram. It survives ANY scale (uniform or not), rotation and renaming, so
    # the same mesh duplicated into 'unique' datablocks or stretched differently is still ONE shape.
    ed = np.unique(np.sort(np.concatenate([Tk[:, [0, 1]], Tk[:, [1, 2]], Tk[:, [2, 0]]]), axis=1), axis=0)
    deg = np.bincount(ed.ravel(), minlength=nw)
    deg = deg[deg > 0]
    h = (nv, nf, tuple(sorted(np.unique(deg, return_counts=True)[0].tolist())), tuple(np.unique(deg, return_counts=True)[1].tolist()))
    ex = ms["ext"]
    r2, r3 = ex[1] / max(ex[0], 1e-12), ex[2] / max(ex[0], 1e-12)
    out.update(nv=nv, nf=nf, ok=not why, why=why, hash=h, open=ofr, nfac=ms["nfacets"], rcv=ms["rcv"], ecv=ms["ecv"], acv=ms["acv"], box=ms["box"],
               r2=r2, r3=r3, ncomp=rc["n"], top=ms["top"][:3])
    return out


def quick_open_fraction(L):
    """Share of the polygon edges of a mesh (welded 1e-4, degenerate and duplicate faces dropped) that belong to exactly one polygon: 0 for a closed solid, > 0.05 for a shell."""
    if L["ntri"] == 0:
        return 0.0
    wid = weld_ids(L["V"])
    Tw = wid[L["T"]]
    ok = (Tw[:, 0] != Tw[:, 1]) & (Tw[:, 1] != Tw[:, 2]) & (Tw[:, 0] != Tw[:, 2]) & (L["A"] > 1e-12)
    if not ok.any():
        return 0.0
    Tk, tpk = Tw[ok], L["tp"][ok]
    _u, first = np.unique(np.sort(Tk, axis=1), axis=0, return_index=True)
    return float(open_edge_fraction(Tk[first], tpk[first]))


def rock_candidates(S):
    """{object name: why} meshes that are judged as ROCKS: by NAME (ROCK_* / CLIFF_ROCK*, as before) OR by MATERIAL: >= 50 % of the mesh's area wears LK_ROCK / LK_ROCK_WET / LK_BASALT /
    LK_CLIFF / LK_CLIFF_DARK (or their legacy MAT_ names) - a DRESS_BOULDER, a DRESS_STACK or a PROP_7 made of the rock material is a rock whatever it is called (v2 area U: the review's
    noisy 42-vertex icosphere named DRESS_BOULDER, the 13-vertex icosahedra, the welded icosahedron clusters and the stacked-box ziggurat named DRESS_STACK all escaped the old
    name-only selection). Collision meshes and the game's hidden placeholders are not rocks."""
    key = "_rock_cands"
    if key in S.__dict__:
        return S.__dict__[key]
    out = {}
    for n, o in S.meshes.items():
        if n.startswith(COLL_PREFIXES) or n in PLACEHOLDERS or n.startswith(RESERVED_PREFIXES) or n == "BALL_START":
            continue
        L = S.loc(o)
        if L["ntri"] == 0:
            continue
        ma = S.mat_area(o)
        tot = sum(ma.values()) or 1.0
        share = sum(a for m_, a in ma.items() if m_ in ROCK_MATS) / tot
        by_name = n.startswith(("ROCK_", "CLIFF_ROCK"))
        if not by_name and share >= ROCK_MAT_SHARE:
            lo_, hi_ = S.wbbox(o)
            if float((hi_ - lo_).max()) > ROCK_BACKDROP_M:       # a backdrop (the Crater cone is 265 m across) is scenery, not a rock of the course
                continue
        if by_name or share >= ROCK_MAT_SHARE:
            out[n] = ("name" if by_name else "") + (" + " if by_name and share >= ROCK_MAT_SHARE else "") + (f"material {share:.0%}" if share >= ROCK_MAT_SHARE else "")
    S.__dict__[key] = out
    return out


def is_cladding(S, n, o):
    """Cliff cladding is judged by GEOMETRY, not by its name alone: a ROCK_SKIN_* mesh that is an OPEN SHELL (>= 5 % of its polygon edges open, >= 3 m across) is the pad-edge
    cladding the skin gates measure and is not a rock; a CLOSED compact solid called ROCK_SKIN_xx (an icosphere, a boulder) is judged as the rock it is."""
    if not n.startswith("ROCK_SKIN_"):
        return False
    lo, hi = S.wbbox(o)
    return quick_open_fraction(S.loc(o)) >= SKIN_SHELL["min_open"] and float(max(hi[0] - lo[0], hi[1] - lo[1])) >= SKIN_SHELL["min_extent"]


def rock_shapes(S):
    """{object name: rock_shape dict} for every rock (rock_candidates: by name OR by material) except open-shell cladding (is_cladding); cached per mesh variant on the snapshot."""
    if "_rock_shapes" in S.__dict__:
        return S._rock_shapes
    cache, out = {}, {}
    for n in rock_candidates(S):
        o = S.meshes[n]
        if is_cladding(S, n, o):
            continue
        k = S.key(o)
        if k not in cache:
            cache[k] = rock_shape(S.loc(o))
        out[n] = cache[k]
    S._rock_shapes = out
    return out


def g_rock_verts(S, C):
    sh = rock_shapes(S)
    bad, ico = [], []
    seen = set()
    for n, r in sh.items():
        k = S.key(S.meshes[n])
        if k in seen:
            continue
        seen.add(k)
        if not r["ok"]:
            bad.append(f"{n} (mesh {S.meshes[n].data.name}): {r['nv']} welded verts / {r['nf']} faces: {'; '.join(r['why'][:3])}")
    # v2 area U: tiny closed solids anywhere (any name, any material but plants / masonry): a 12-vertex icosahedron, the 13-vertex one with a poked face, a cube, an octahedron.
    # The old test caught only welded 12 vertices / 20 faces, and only when the object was not called PLANT_*: one poked face (13 vertices / 22 faces) or a PLANT_ name beat it.
    for n, o in S.meshes.items():
        if n in sh or n.startswith(COLL_PREFIXES) or n in PLACEHOLDERS or n.startswith(RESERVED_PREFIXES) or n == "BALL_START":
            continue
        L = S.loc(o)
        if L["ntri"] == 0 or L["nv"] > 4 * TINY_SOLID_MAX_VERTS:
            continue
        ma = S.mat_area(o)
        tot = sum(ma.values()) or 1.0
        if sum(a for m_, a in ma.items() if m_ in TINY_SOLID_EXEMPT_MATS) / tot >= 0.5:
            continue
        r = rock_shape(L)
        if 4 <= r["nv"] <= TINY_SOLID_MAX_VERTS and r.get("open", 1.0) <= ROCK_TH["max_open_edge"]:
            ico.append(f"{n} ({r['nv']} v / {r['nf']} f)")
    nu = len(seen)
    ok = not bad and not ico
    return ok, (f"{len(sh)} rock objects / {nu} unique rock meshes (by name ROCK_*/CLIFF_ROCK* OR by material: >= {ROCK_MAT_SHARE:.0%} of the area on LK_ROCK / LK_ROCK_WET / LK_BASALT / LK_CLIFF*, "
                f"whatever the object is called; open-shell ROCK_SKIN_* cladding aside) are real faceted rocks on the WELDED mesh: >= {ROCK_TH['min_verts']} "
                f"verts, >= {ROCK_TH['min_faces']} faces, >= {ROCK_TH['min_facets']} planar facet orientations, not sphere-like, not a box; no tiny closed solid (<= {TINY_SOLID_MAX_VERTS} welded vertices) elsewhere"
                if ok else f"{len(bad)} of {nu} rock meshes are blobs/icosahedra/boxes: " + " | ".join(bad[:5]) + (f" (+{len(bad) - 5} more)" if len(bad) > 5 else "")
                + (f"; tiny closed solids on {short(ico)}" if ico else ""))


def shape_families(shapes):
    """Union-find over shape records [(hash, nv, r2, r3, nfac)]: two shapes are ONE family when their topology hash is equal OR they are near-duplicates
    by geometry (PCA extent ratios within fam_ratio on both axes, welded vertex counts within fam_nv_tol, facet-orientation counts within fam_nfac).
    A bundle with another number of copies, one extra chip or a re-jittered copy is a family variant, not a new shape. Returns the family id per record."""
    th = ROCK_TH
    par = list(range(len(shapes)))

    def find(a):
        while par[a] != a:
            par[a] = par[par[a]]
            a = par[a]
        return a
    for i in range(len(shapes)):
        for j in range(i + 1, len(shapes)):
            a, b = shapes[i], shapes[j]
            same = a[0] == b[0] or (abs(a[2] - b[2]) <= th["fam_ratio"] and abs(a[3] - b[3]) <= th["fam_ratio"]
                                    and abs(a[1] - b[1]) <= th["fam_nv_tol"] * max(a[1], b[1]) and abs(a[4] - b[4]) <= th["fam_nfac"])
            if same:
                par[find(i)] = find(j)
    return [find(i) for i in range(len(shapes))]


def g_rock_variety(S, C):
    sh = rock_shapes(S)
    meshes = {}                                  # mesh key -> [shape record, {object names}]
    for n, r in sh.items():
        if not r["ok"] or r["hash"] is None:
            continue
        lo_, hi_ = S.wbbox(S.meshes[n])
        if float((hi_ - lo_).max()) > ROCK_BACKDROP_M:          # the background volcano (DRESS_CONE, 265 m) wears the basalt material but is no rock of the course: it adds no variety
            continue
        k = S.key(S.meshes[n])
        e = meshes.setdefault(k, [(r["hash"], r["nv"], r["r2"], r["r3"], r["nfac"]), 0, r["nf"]])
        e[1] += 1
    keys = list(meshes)
    fam = shape_families([meshes[k][0] for k in keys])
    groups = {}
    for k, f in zip(keys, fam):
        g = groups.setdefault(f, [])
        g.append(k)
    uniq = len(groups)
    inst = sorted((f for f, g in groups.items() if max(meshes[k][1] for k in g) >= 2), key=lambda f: -max(meshes[k][1] for k in groups[f]))
    bad = sorted({o.data.name for n, o in S.meshes.items() if n in sh and not sh[n]["ok"]})
    ok = uniq >= TH["rock_unique"] and len(inst) >= TH["rock_instanced"]
    merged = len(keys) - uniq
    return ok, (f"{uniq} unique rock SHAPE FAMILIES among ROCK_* ({len(keys)} distinct meshes; families merge equal topology hashes (welded verts/faces + vertex-valence histogram) and "
                f"geometric near-duplicates (PCA extent ratios +-{ROCK_TH['fam_ratio']}, vertex counts +-{ROCK_TH['fam_nv_tol']:.0%}, facet orientations +-{ROCK_TH['fam_nfac']}); "
                f"{merged} merged; independent of scale, rotation and mesh name; need >= {TH['rock_unique']}), {len(inst)} families instanced >= 2 times on shared mesh data "
                f"(need >= {TH['rock_instanced']}): " + ", ".join(f"{meshes[groups[f][0]][0][1]}v/{meshes[groups[f][0]][2]}f x{max(meshes[k][1] for k in groups[f])}" for f in inst[:6])
                + (f"; not counted (fail the rock shape test): {short(bad, 5)}" if bad else ""))


def is_tuft(n):
    return n.startswith(("PLANT_TUFT", "PLANT_GRASS"))


def g_prop_budget(S, C):
    problems, worst = [], {}
    seen = set()
    for n, o in S.meshes.items():
        if not (n.startswith("PLANT_") or is_loose_rock(n)):
            continue
        if o.data.name in seen:
            continue
        seen.add(o.data.name)
        L = S.loc(o)
        lim = TUFT_FACES if is_tuft(n) else PROP_FACES
        kind = "tuft" if is_tuft(n) else ("plant" if n.startswith("PLANT_") else "rock")
        worst[kind] = max(worst.get(kind, 0), L["npoly"])
        if L["npoly"] > lim:
            problems.append(f"{n} mesh {o.data.name}: {L['npoly']} faces (> {lim})")
    return not problems, ("; ".join(problems[:10]) if problems else
                          f"{len(seen)} prop meshes within budget (plants/rocks <= {PROP_FACES} faces, tufts <= {TUFT_FACES}); "
                          f"largest {worst}")


def g_plants_material(S, C):
    plants = S.named("PLANT_")
    bad = []
    for n, o in plants.items():
        slots = [s.material.name if s.material else "" for s in o.material_slots]
        if slots != ["LK_PLANTS"] and set(slots) != {"LK_PLANTS"}:
            bad.append(f"{n} {slots}")
    if not plants:
        return False, "no PLANT_* objects"
    return not bad, (f"all {len(plants)} PLANT_* objects use the one shared LK_PLANTS material" if not bad else
                     f"{len(bad)} plants with other/extra materials: {short(bad)}")


def g_plants_instanced(S, C):
    plants = {n: o for n, o in S.named("PLANT_").items() if o.type == "MESH"}
    if not plants:
        return False, "no PLANT_* meshes"
    users = {}
    for n, o in plants.items():
        users.setdefault(o.data.name, []).append(n)
    shared = sum(len(u) for u in users.values() if len(u) >= 2)
    frac = shared / len(plants)
    ok = frac >= TH["plants_instanced"]
    return ok, (f"{len(plants)} PLANT_* objects use {len(users)} meshes; {frac:.0%} of them share their mesh with another plant "
                f"(need >= {TH['plants_instanced']:.0%})")


# ----------------------------------------------------------------------------- v2 2026-10-05 area U: plant sway data (GolfArcade/GolfPlants, RUNTIME.md "v2 2026-10-05 area U")
# Every PLANT_* mesh carries ONE colour attribute "Col": R = sway weight (0 at the root / pinned point, rising to the free tip), G = per-vertex phase, B = 0 (marks "sway data"), A = 1.
# A mesh without it is static in Unity (white = B 1 = weight 0). The shader displaces by AmpFull x w x up to sqrt(1 + cross^2) (the user's "weight x amplitude x waves"), so the per-kind cap is a cap on the tip weight.
# tip_min: the user's "tip weight > .6"; shrubs and trees are "low" in the same sentence and their cap (.03 yd) is half a tuft's, so for them the line is > .25 (PLANT_SWAY_CAPS ties every weight to its cap).
SWAY_TH = dict(root_max=0.05, tip_min=0.6, tip_min_low=0.25, b_max=0.02, a_min=0.98, rise_corr=0.5, root_frac=0.04, n_attr=1)
SWAY_KINDS = (("tuft", ("PLANT_TUFT", "PLANT_GRASS"), 0.06), ("flower", ("PLANT_FLOWER", "PLANT_AGAVE"), 0.05), ("shrub", ("PLANT_SHRUB", "PLANT_BUSH"), 0.03), ("tree", ("PLANT_TREE", "PLANT_PINE"), 0.03),
              ("vine", ("PLANT_VINE",), 0.08))
SWAY_LOW_KINDS = ("shrub", "tree")
SWAY_CS = os.path.join(REPO, "Unity", "Assets", "Scripts", "Course", "GolfWindSway.cs")


def sway_constants():
    """(AmpFullYards, CrossShare) read from GolfWindSway.cs (the C# is the source of truth; 0.055 / 0.2 if the file is missing)."""
    amp, cross = 0.055, 0.2
    try:
        with open(SWAY_CS, errors="ignore") as fh:
            txt = fh.read()
        m = re.search(r"AmpFullYards\s*=\s*([0-9.]+)f", txt)
        c = re.search(r"CrossShare\s*=\s*([0-9.]+)f", txt)
        amp = float(m.group(1)) if m else amp
        cross = float(c.group(1)) if c else cross
    except OSError:
        pass
    return amp, cross


def sway_kind(name):
    n = base_name(name)
    for kind, prefixes, cap in SWAY_KINDS:
        if n.startswith(prefixes):
            return kind, cap
    return "other", 0.06            # an unknown PLANT_ kind is held to the tuft cap


def sway_colors(me):
    """(per-vertex RGBA float32 array (nv, 4) of the colour attribute AS UNITY READS IT, attribute count, name) or (None, n, None).
    FLOAT_COLOR: the stored value (colors_type LINEAR writes it raw). BYTE_COLOR (the attribute the FBX re-import makes): color_srgb = the raw byte / 255 (checked: v2/sway/blender/fbx_color_roundtrip.py).
    A CORNER-domain attribute is averaged per vertex."""
    cas = me.color_attributes
    n = len(cas)
    if n == 0:
        return None, 0, None
    ca = cas["Col"] if "Col" in cas else cas[0]
    cnt = len(ca.data)
    prop = "color_srgb" if ca.data_type == "BYTE_COLOR" else "color"
    buf = np.empty(cnt * 4, np.float32)
    try:
        ca.data.foreach_get(prop, buf)
    except Exception:  # noqa: BLE001
        buf = np.array([tuple(getattr(d, prop)) for d in ca.data], np.float32).ravel()
    buf = buf.reshape(-1, 4)
    nv = len(me.vertices)
    if ca.domain == "POINT":
        return (buf if len(buf) == nv else None), n, ca.name
    vi = np.empty(len(me.loops), np.int64)
    me.loops.foreach_get("vertex_index", vi)
    acc = np.zeros((nv, 4))
    cntv = np.zeros(nv)
    np.add.at(acc, vi, buf[:len(vi)])
    np.add.at(cntv, vi, 1.0)
    return (acc / np.maximum(cntv, 1)[:, None]).astype(np.float32), n, ca.name


def sway_mesh_stats(S, o):
    """Per unique plant mesh: dict(kind, cap, ncol, rgba, root, tip, corr, ...) or dict(ncol=0)."""
    me = o.data
    rgba, ncol, cname = sway_colors(me)
    kind, cap = sway_kind(o.name)
    out = dict(kind=kind, cap=cap, ncol=ncol, name=cname, mesh=me.name, obj=o.name)
    if rgba is None:
        return out
    V, _T = S.oriented(o)
    z = V[:, 2]
    h = max(float(z.max() - z.min()), 1e-6)
    t = (z.max() - z) / h if kind == "vine" else (z - z.min()) / h          # distance from the pinned end, 0..1 (vine: the attach point is the top)
    R, G, B, A = rgba[:, 0], rgba[:, 1], rgba[:, 2], rgba[:, 3]
    root = t <= SWAY_TH["root_frac"]
    # "rises with the distance from the root": measured along the height AND as the distance from the root's centroid (an agave rosette's weight grows outward from its centre, a vine's along its length): the better of the two
    rc = V[root].mean(0) if root.any() else V[np.argmin(t)]
    d = np.linalg.norm(V - rc, axis=1)
    d = d / max(float(d.max()), 1e-9)
    cz = float(np.corrcoef(R, t)[0, 1]) if R.std() > 1e-6 and t.std() > 1e-6 else 0.0
    cd = float(np.corrcoef(R, d)[0, 1]) if R.std() > 1e-6 and d.std() > 1e-6 else 0.0
    out.update(rgba=rgba, root=float(R[root].max()) if root.any() else float(R[np.argmin(t)]), tip=float(R.max()), b=float(B.max()), a=float(A.min()),
               rmin=float(R.min()), gmin=float(G.min()), gmax=float(G.max()), nv=len(R), corr=max(cz, cd))
    return out


def sway_unique(S):
    seen, out = set(), []
    for n, o in sorted(S.named("PLANT_").items()):
        if o.type != "MESH":
            continue
        k = o.data.name
        if k in seen:
            continue
        seen.add(k)
        out.append(o)
    return out


def g_plant_sway_colors(S, C):
    """Requirement 3 (user 2026-10-04): every PLANT_* mesh carries the sway colour attribute: exactly one attribute, R = weight 0 at the root (<= .05 on the lowest 4 % of the height; vines: the
    top) rising with the distance from it (corr >= .5) to a tip > .6, G in 0..1, B = 0, A = 1. Run on the blend AND on the FBX re-import. A mesh without it FAILS (it would stand still)."""
    objs = sway_unique(S)
    if not objs:
        return False, "no PLANT_* meshes"
    bad, per = [], {}
    for o in objs:
        st = sway_mesh_stats(S, o)
        k = st["kind"]
        per.setdefault(k, []).append(st)
        if st["ncol"] == 0 or "rgba" not in st:
            bad.append(f"{st['mesh']} has no colour attribute")
            continue
        if st["ncol"] != SWAY_TH["n_attr"]:
            bad.append(f"{st['mesh']} has {st['ncol']} colour attributes (exactly 1: the exporter writes every layer and Unity reads the first)")
        if st["root"] > SWAY_TH["root_max"]:
            bad.append(f"{st['mesh']} root weight {st['root']:.2f} > {SWAY_TH['root_max']}")
        tmin = SWAY_TH["tip_min_low"] if st["kind"] in SWAY_LOW_KINDS else SWAY_TH["tip_min"]
        if st["tip"] <= tmin:
            bad.append(f"{st['mesh']} tip weight {st['tip']:.2f} <= {tmin} ({st['kind']})")
        if st["b"] > SWAY_TH["b_max"]:
            bad.append(f"{st['mesh']} B {st['b']:.2f} (B = 0 marks sway data)")
        if st["a"] < SWAY_TH["a_min"]:
            bad.append(f"{st['mesh']} A {st['a']:.2f} (A = 1)")
        if st["rmin"] < -1e-4 or st["gmin"] < -1e-4 or st["gmax"] > 1.0001:
            bad.append(f"{st['mesh']} R/G outside 0..1")
        if st["corr"] < SWAY_TH["rise_corr"]:
            bad.append(f"{st['mesh']} weight does not rise with the distance from the root (corr {st['corr']:.2f} < {SWAY_TH['rise_corr']})")
    kinds = "; ".join(f"{k} {len(v)} mesh(es): root<= {max(x.get('root', 0) for x in v):.2f}, tip {min(x.get('tip', 0) for x in v):.2f}..{max(x.get('tip', 0) for x in v):.2f}" for k, v in sorted(per.items()))
    return not bad, (f"all {len(objs)} unique PLANT_* meshes carry the sway colours (one attribute, root <= {SWAY_TH['root_max']}, tip > {SWAY_TH['tip_min']} (shrub / tree > {SWAY_TH['tip_min_low']}), rising, B 0, A 1): {kinds}" if not bad
                     else f"{len(bad)} problem(s) on {len(objs)} unique PLANT_* meshes: {short(bad, 6)}")


def g_plant_sway_caps(S, C):
    """The user's caps at 20 mph (tuft tip <= .06 yd, flower <= .05, shrub <= .03, vine <= .08) as a cap on the tip weight: displacement <= AmpFull x w_tip^2 x sqrt(1 + cross^2) (the shader's own bound,
    GolfWindSway.MaxDisplacement; the Unity check SWAY_GENTLE samples the real meshes through the C# mirror of the shader)."""
    amp, cross = sway_constants()
    k = amp * math.sqrt(1 + cross * cross)
    objs = sway_unique(S)
    if not objs:
        return False, "no PLANT_* meshes"
    bad, per = [], {}
    for o in objs:
        st = sway_mesh_stats(S, o)
        if "rgba" not in st:
            bad.append(f"{st['mesh']}: no colours (not measurable)")
            continue
        d = k * st["tip"]
        per.setdefault(st["kind"], []).append(d)
        if d > st["cap"] + 1e-6:
            bad.append(f"{st['mesh']} ({st['kind']}) tip weight {st['tip']:.2f} -> {d:.4f} yd > cap {st['cap']:.2f}")
    kinds = "; ".join(f"{kk}: max {max(v):.4f} yd at 20 mph (cap {dict((a, c) for a, _p, c in SWAY_KINDS).get(kk, 0.06):.2f})" for kk, v in sorted(per.items()))
    return not bad, (f"every plant's tip displacement at 20 mph stays under its kind's cap (AmpFull {amp} yd x w x {math.sqrt(1 + cross * cross):.3f}): {kinds}" if not bad else f"{len(bad)} over / unmeasurable: {short(bad, 5)}")


# ----------------------------------------------------------------------------- v2 prop / play-surface gates
OVERLAY_OK = ("DRESS_PATH", "DRESS_SAND")                        # the explicit allow-list of overlays that may lie on play surfaces
SUPPORT_RE = re.compile(r"^(ROCK_(?!SKIN_)|DRESS_(ARCH|RUIN|BLOCK))")   # surfaces a plant may stand on besides the ground


def prop_objects(S):
    """Visible non-collision scenery that must stay off the play surfaces: every ROCK_/CLIFF_ROCK/PLANT_/DRESS_/WATER_FALL/LAVA_GLOW mesh
    except the explicit overlays (DRESS_PATH_*, DRESS_SAND_*)."""
    out = {}
    for n, o in S.meshes.items():
        if n.startswith(COLL_PREFIXES) or n.startswith(OVERLAY_OK):
            continue
        if n.startswith(("ROCK_", "CLIFF_ROCK", "PLANT_", "DRESS_", "WATER_FALL", "LAVA_GLOW")):
            out[n] = o
    return out


def play_samples(S, C, step=1.5):
    """Grid points (x, y, ground z) over the play surfaces (FAIRWAY, GREEN, TEE_BOX, BUNKER and its lip), found with the same single-sided
    downward ray Unity's GroundHeight uses."""
    if "_play_samples" in S.__dict__:
        return S._play_samples
    objs = [o for n, o in S.meshes.items() if PLAY_OBJECT_RE.match(n) and n not in ("FAIRWAY_FIRSTCUT", "GREEN_APRON")]
    out = []
    if objs:
        rays = ground_rays(S)
        lo, hi = union_bbox(S, {o.name: o for o in objs})
        X = np.arange(lo[0], hi[0] + step, step)
        Y = np.arange(lo[1], hi[1] + step, step)
        for x in X.tolist():
            for y in Y.tolist():
                r = rays.cast(x, y)
                if r is not None and over_play(rays.obj_of(r[1])):
                    out.append((x, y, r[0]))
    S._play_samples = out
    return out


def g_props_off_play(S, C):
    """Contract section 6, now for EVERYTHING visible: no ROCK_/PLANT_/DRESS_/WATER_FALL object (walls, cladding and dressing included)
    stands on or over a play surface (rays from above onto the FAIRWAY/GREEN/TEE_BOX/BUNKER grid, 1.5 m), nothing stands within 3 m of the
    pin, and plants / loose rocks keep the green radius + 1 m around it. Only DRESS_PATH_* and DRESS_SAND_* may overlay play surfaces
    (their height is PATH_UNDER_BALL's job)."""
    pin = S.pos(S.all["MARKER_PIN"]) if "MARKER_PIN" in S.all else None
    rad = C.gr * YD + 1.0
    bad_play, bad_green, n = [], [], 0
    for nm, o in S.all.items():
        if not (nm.startswith("PLANT_") or is_loose_rock(nm)):
            continue
        n += 1
        p = S.centre(o)
        top = S.wbbox(o)[1][2]
        hit, hz = on_land(S, C, float(p[0]), float(p[1]))
        if over_play(hit) and top >= hz - 0.05:
            ring = [on_land(S, C, float(p[0] + OFF_PLAY_INSET * math.cos(a)), float(p[1] + OFF_PLAY_INSET * math.sin(a)))[0]
                    for a in np.linspace(0, 2 * math.pi, 8, endpoint=False)]
            if all(over_play(h) for h in ring):
                bad_play.append(f"{nm} over {hit}")
        if pin is not None and math.hypot(p[0] - pin[0], p[1] - pin[1]) <= rad:
            bad_green.append(nm)
    props = prop_objects(S)
    idx = TriIndex(S, list(props.values()))
    cover = {}
    ns = 0
    if idx.bvh is not None:
        for x, y, zg in play_samples(S, C):
            ns += 1
            r = idx.hit_down(x, y, 1000.0)
            if r is not None and zg + 0.03 < r[0] < zg + 8.0:
                nm = idx.own[r[1]]
                cover[nm] = cover.get(nm, 0) + 1
    # small / thin scenery can hide between the 1.5 m grid points: DRESS_ and similar objects whose footprint is narrower than 3 m in x or y are
    # re-sampled every 0.25 m inside their own bounding box (ROCK_ / PLANT_ stay on the centre rule above, which allows edge tufts)
    fine_n, fine_objs = 0, 0
    if idx.bvh is not None:
        rays_ = ground_rays(S)
        for nm, o in props.items():
            if nm.startswith(("ROCK_", "CLIFF_ROCK", "PLANT_")):
                continue
            lo_, hi_ = S.wbbox(o)
            ext = hi_[:2] - lo_[:2]
            if float(ext.min()) >= 3.0 or float(ext.max()) > 60.0:
                continue
            fine_objs += 1
            X = np.arange(lo_[0] + 0.07, hi_[0] + 0.25, 0.25)
            Y = np.arange(lo_[1] + 0.07, hi_[1] + 0.25, 0.25)
            for x in X.tolist():
                for y in Y.tolist():
                    g = rays_.cast(x, y)
                    if g is None or not over_play(rays_.obj_of(g[1])):
                        continue
                    fine_n += 1
                    r = idx.hit_down(x, y, 1000.0)
                    if r is not None and g[0] + 0.03 < r[0] < g[0] + 8.0:
                        nm2 = idx.own[r[1]]
                        cover[nm2] = cover.get(nm2, 0) + 1
    near_pin = []
    if pin is not None and idx.bvh is not None:
        d = np.hypot(idx.V[:, 0] - pin[0], idx.V[:, 1] - pin[1])
        z = idx.V[:, 2]
        sel = (d <= 3.0) & (z >= C.pz - 0.1)
        if sel.any():
            vi = np.flatnonzero(sel)
            tri_hit = np.flatnonzero(np.isin(idx.T, vi).any(axis=1))
            near_pin = sorted({idx.own[t] for t in tri_hit.tolist()})
    ok = not bad_play and not bad_green and not cover and not near_pin
    msg = []
    if bad_play:
        msg.append(f"{len(bad_play)} plants/loose rocks over play surfaces {short(bad_play, 6)}")
    if bad_green:
        msg.append(f"{len(bad_green)} plants/loose rocks inside the green disc {short(bad_green, 6)}")
    if cover:
        msg.append(f"{sum(cover.values())} of {ns} + {fine_n} (0.25 m, small objects) play-surface sample points have scenery above them: " + ", ".join(f"{k} x{v}" for k, v in sorted(cover.items(), key=lambda kv: -kv[1])[:5]))
    if near_pin:
        msg.append(f"scenery within 3 m of the pin: {short(near_pin, 5)}")
    return ok, ("; ".join(msg) if msg else
                f"{len(props)} scenery meshes (ROCK_/PLANT_/DRESS_/WATER_FALL, walls and cladding included): none above the {ns} play-surface grid points (+ {fine_n} samples at 0.25 m under {fine_objs} small DRESS_-type objects), none within 3 m of "
                f"the pin, plants / loose rocks outside {rad:.1f} m of it ({n} checked); overlays allowed: {OVERLAY_OK}")


def mesh_components_world(S, o):
    """Connected pieces (shared welded vertices) of an object in world space: [(low z, high z, bbox lo (3,), bbox hi (3,))]. Plants / rocks merged from
    several pieces are judged piece by piece."""
    L = S.loc(o)
    k = ("_comp",) + S.key(o)
    lab = S.__dict__.setdefault("_comp_cache", {}).get(k)
    if lab is None:
        wid = weld_ids(L["V"], 1e-3)
        lab = (wid, tri_components(wid[L["T"]]))
        S._comp_cache[k] = lab
    wid, tl = lab
    W = S.wld(o)["W"]
    out = []
    T = L["T"]
    for c in np.unique(tl).tolist():
        vi = np.unique(T[tl == c])
        P = W[vi]
        out.append((float(P[:, 2].min()), float(P[:, 2].max()), P.min(0), P.max(0)))
    return out


def g_no_plant_on_water(S, C):
    """No plant on the sea (the brief's fail line), judged PIECE BY PIECE (a plant mesh merged from copies on land and over the sea fails): every
    PLANT_ object's lowest piece stands on ground (down-ray onto TERRAIN / FAIRWAY / GREEN / ... tops or onto a rock / masonry top, base z >= PLAY_Z - 1 = 5 m),
    inside the dry land (not past the shore, not in a water ellipse), not on a play surface, not on DRESS_PATH; every other piece has dry ground
    or a rock top within 6 m below it. A rock that carries a plant must itself stand on ground (ground within 0.6 m below it at the plant) or reach
    below z 3 (stacks, outcrops): a slab floating over the sea carries nothing. Hanging vines: attach point at z >= 3 m within 1.5 m of ground, rock,
    masonry or skin."""
    plants = {n: o for n, o in S.meshes.items() if n.startswith("PLANT_")}
    if not plants:
        return True, "no PLANT_ objects on this hole (their presence is LK_REQUIRED / PLANTS_*)"
    rays = ground_rays(S)
    sup_objs = {n: o for n, o in S.meshes.items() if SUPPORT_RE.match(n)}
    sup = TriIndex(S, list(sup_objs.values()), up_only=True)
    path = TriIndex(S, [o for n, o in S.meshes.items() if n.startswith("DRESS_PATH")], up_only=True)
    anyidx = TriIndex(S, [o for n, o in S.meshes.items() if n.startswith(COLL_PREFIXES) or SUPPORT_RE.match(n) or n.startswith("ROCK_SKIN_")])
    bad, nv, nb, npieces = [], 0, 0, 0

    def rock_stands(on, bx, by):
        """does the supporting rock `on` stand on ground at (bx, by) (the ground at the plant lies between 0.6 m under the rock's lowest point and its top: resting or sunk in) or reach into the sea?"""
        lo_, hi_ = S.wbbox(sup_objs[on])
        if lo_[2] < 3.0:
            return True
        g = rays.cast(bx, by, float(hi_[2]))
        return g is not None and lo_[2] - 0.6 <= g[0] <= hi_[2] and g[0] >= C.pz - 1.0

    def judge(bx, by, bz, strict):
        why = []
        if bz < C.pz - 1.0:
            why.append(f"base z {bz:.2f} < {C.pz - 1.0:g}")
        g = rays.cast(bx, by, bz + 1.0)
        s_ = sup.hit_down(bx, by, bz + 1.0)
        lo_, hi_ = (bz - 0.5, bz + 0.4) if strict else (bz - 6.0, bz + 0.4)
        zs = [(g[0], "ground", rays.obj_of(g[1])) if g else None, (s_[0], "rock", sup.own[s_[1]]) if s_ else None]
        near = [q for q in zs if q is not None and lo_ <= q[0] <= hi_]
        top = max(near, key=lambda q: q[0]) if near else None
        if top is None:
            why.append("no ground or rock under it")
        else:
            if top[1] == "ground":
                if strict and over_play(top[2]):
                    why.append(f"stands on the play surface {top[2]}")
                if not C.dry_m(np.array([bx]), np.array([by]))[0]:
                    why.append("outside the dry land (past the shore or inside a water hazard)")
            else:
                if top[2] in sup_objs and not rock_stands(top[2], bx, by):
                    why.append(f"carried by {top[2]}, a rock that floats (no ground at its foot, lowest z >= 3)")
            if strict:
                pr = path.hit_down(bx, by, bz + 1.0)
                if pr is not None and bz - 0.3 <= pr[0] <= bz + 0.4:
                    why.append("stands on the stone path")
        return why
    for n, o in sorted(plants.items()):
        pcs = mesh_components_world(S, o)
        if not pcs:
            continue
        lo, hi = S.wbbox(o)
        if n.startswith("PLANT_VINE"):
            nv += 1
            for (zl, zh, plo, phi) in pcs:
                c = (plo + phi) / 2
                att = (float(c[0]), float(c[1]), float(phi[2]))
                r = anyidx.nearest(att[0], att[1], att[2], 1.5)
                if att[2] < 3.0 or r is None:
                    bad.append(f"{n} (vine attach z {att[2]:.1f}{', nothing within 1.5 m' if r is None else ''})")
                    break
            continue
        nb += 1
        lowest = min(range(len(pcs)), key=lambda i: pcs[i][0])
        for i, (zl, zh, plo, phi) in enumerate(pcs):
            npieces += 1
            c = (plo + phi) / 2
            why = judge(float(c[0]), float(c[1]), float(zl), i == lowest)
            if why:
                bad.append(f"{n}{' piece ' + str(i) if len(pcs) > 1 else ''} at ({c[0]:.0f},{c[1]:.0f},{zl:.1f}): " + ", ".join(why))
                break
    return not bad, (f"{nb} plants ({npieces} pieces) stand on ground/rock tops at z >= {C.pz - 1.0:g} m inside the dry land, off the play surfaces and the path (rocks that carry them "
                     f"stand on ground or reach the sea); {nv} vines attached to a surface"
                     if not bad else f"{len(bad)} of {len(plants)} plants fail: " + " | ".join(bad[:5]) + (f" (+{len(bad) - 5} more)" if len(bad) > 5 else ""))


def inst_scale(o, ref=1.0):
    """World scale of an instance as the SINGULAR VALUES of its 3x3 (so a sheared parent / child rotation chain cannot hide a stretch: the column norms of
    R(45) diag(.6, 1.9, 1) R(-45) read 1.41, 1.41, 1.0, the singular values 0.6, 1.9, 1.0), normalised by the ground meshes' own scale."""
    m3 = np.array(o.matrix_world)[:3, :3]
    return np.linalg.svd(m3, compute_uv=False) / ref


_LIBSIG = {}


def lib_signature():
    """{kind: dict(hash, dims)} for every ROCK_ / DRESS_BLOCK kind of the props library (postcard_props_lib, imported read-only: library_object() builds the
    meshes in memory; ensure_library() is NOT used because it writes Plants_C.png), built in an EMPTY scene before any hole is opened. The topology hash
    (welded vertices / faces / valence histogram) identifies a library mesh whatever the object is called; dims = its bounding box (mesh axes).
    Returns (signature, error text or None)."""
    if "v" in _LIBSIG:
        return _LIBSIG["v"]
    out, err = {}, None
    try:
        if HERE not in sys.path:
            sys.path.insert(0, HERE)
        import postcard_props_lib as PL
        bpy.ops.wm.read_factory_settings(use_empty=True)
        S0 = Snap.__new__(Snap)
        S0.stage, S0._loc, S0._wld = "fbx", {}, {}
        for kind in sorted(PL.BUILDERS):
            if not kind.startswith(("ROCK_", "DRESS_BLOCK")):
                continue
            ob = PL.library_object(kind)
            L = S0.loc(ob)
            if L["ntri"] == 0:
                continue
            out[kind] = dict(hash=rock_shape(L)["hash"], dims=L["V"].max(0) - L["V"].min(0), V=L["V"].copy(),
                             F=[tuple(int(i) for i in pl.vertices) for pl in ob.data.polygons], Un=norm_unique(L["V"]))
    except Exception as e:  # noqa: BLE001
        err = f"{type(e).__name__}: {e}"
        out = {}
    _LIBSIG["v"] = (out, err)
    return _LIBSIG["v"]


def norm_unique(V):
    """Unique (1e-4 rounded) mesh vertices normalised per axis into the unit cube of their own bounding box: a stretch along the mesh axes leaves them unchanged."""
    U = np.unique(np.round(np.asarray(V, float) / 1e-4).astype(np.int64), axis=0) * 1e-4
    lo, hi = U.min(0), U.max(0)
    return (U - lo) / np.maximum(hi - lo, 1e-3 * max(float((hi - lo).max()), 1e-9))


_PERMS = [(p_, f_) for p_ in itertools.permutations(range(3)) for f_ in itertools.product((0, 1), repeat=3)]
_FUZZY = {}


def lib_fuzzy_match(L, kinds):
    """Which props-library kind (among `kinds`) is this mesh, up to a stretch along its (possibly 90-degree-turned / mirrored) mesh axes and up to a few missing faces?
    Both unique-vertex sets are normalised into their own bounding box; for every axis permutation + mirror the per-axis coordinate distributions must agree
    (quantile prefilter, 0.06), then >= 85 % of the mesh vertices must lie within 0.03 of a library vertex AND >= 85 % of the library vertices within 0.03 of a mesh
    vertex. A library mesh with one chip / one face removed and baked 4 : 0.6 therefore still matches. Returns (kind, perm, flips) or None."""
    sig, _err = lib_signature()
    Un = norm_unique(L["V"])
    ck = (hashlib.sha1(np.round(Un, 3).tobytes()).hexdigest(), tuple(kinds))
    if ck in _FUZZY:
        return _FUZZY[ck]
    q = (np.arange(48) + 0.5) / 48
    mq = [np.quantile(Un[:, a], q) for a in range(3)]
    found = None
    for kind in kinds:
        W = sig[kind]["Un"]
        if abs(len(W) - len(Un)) > 0.3 * max(len(W), len(Un)):
            continue
        lq = [np.quantile(W[:, a], q) for a in range(3)]
        for perm, fl in _PERMS:
            if any(float(np.abs(mq[a] - (1.0 - lq[perm[a]][::-1] if fl[a] else lq[perm[a]])).max()) > 0.06 for a in range(3)):
                continue
            X = np.empty_like(Un)
            for a in range(3):
                X[:, perm[a]] = 1.0 - Un[:, a] if fl[a] else Un[:, a]
            D = np.linalg.norm(X[:, None, :] - W[None, :, :], axis=2)
            if float((D.min(1) <= 0.03).mean()) >= 0.85 and float((D.min(0) <= 0.03).mean()) >= 0.85:
                found = (kind, perm, fl)
                break
        if found:
            break
    _FUZZY[ck] = found
    return found


def g_rock_stretch(S, C):
    """No rock stretched past 2x. STRETCH = ANISOTROPY, and the caps are judged separately (v2 area U, review: one scale window [0.5, 2.0] made a uniform 0.4 pebble 'stretched'):
      * anisotropy: max / min of the SINGULAR VALUES of matrix_world (normalised by the ground meshes' own scale so the FBX unit conversion cancels) <= 2.0;
      * absolute upper cap: the largest component <= 2.0 (a rock is never scaled up past 2x);
      * lower bound: the smallest component >= 0.4 (a library kind may shrink uniformly to 0.4: a pebble is not a stretched rock).
    Instances judged: every rock (rock_candidates: ROCK_* / CLIFF_ROCK* by name OR any mesh whose area is >= 50 % rock material, whatever it is called) and every DRESS_* mesh
    (smoke cards by material, the > 100 m background cone and open-shell ROCK_SKIN_* cladding excepted).
    Baked stretch (scale applied to the vertices, any name, both stages): a mesh that IS a props-library kind (topology hash, or the fuzzy match of lib_fuzzy_match: unique
    vertices in the bounding-box frame, so one removed face or a 90-degree turn do not hide it) is compared with that library mesh's bounding box, mesh axes x instance scale:
    the same three rules on the per-axis size ratios. A rock mesh that is NOT a library kind cannot be compared, so its own PCA extent ratio is capped: r3 (third / first
    principal std) >= ROCK_TH['stretch_r3_min'] 0.15 - the most elongated / flattest library mesh (ROCK_SEASTACK_E, ROCK_WALL_BASALT_A) measures 0.17; a hand-made rock baked
    4 : 0.5 (r3 0.06-0.10) or 5 : 0.4 fails, one baked 3 : 1 (r3 0.18-0.25) is as elongated as the library's own ledges and cannot be told from a natural rock."""
    refs = [np.linalg.norm(np.array(o.matrix_world)[:3, :3], axis=0).mean() for n, o in S.meshes.items() if n.startswith(("TERRAIN", "FAIRWAY"))]
    ref = float(np.median(refs)) if refs else 1.0
    ref = ref if ref > 1e-9 else 1.0
    sig, err = lib_signature()
    by_hash = {}
    for kind, d in sig.items():
        by_hash.setdefault(d["hash"], []).append(kind)
    shapes = rock_shapes(S)
    bad, n, worst, nbaked, unknown, nfuzzy, ncap = [], 0, (1.0, 1.0), 0, 0, 0, 0
    cache = {}
    r3min = 9.0
    cands = rock_candidates(S)
    for nm, o in S.meshes.items():
        judged = nm in shapes or nm in cands or nm.startswith(("ROCK_", "CLIFF_ROCK", "DRESS_"))
        if not judged or nm.startswith(COLL_PREFIXES) or nm in PLACEHOLDERS or nm.startswith(RESERVED_PREFIXES):
            continue
        if is_cladding(S, nm, o):                                   # open-shell cliff cladding: baked at scale 1 along the shore, not a rock instance
            continue
        ma_ = S.mat_area(o)
        tot_ = sum(ma_.values()) or 1.0
        if ma_.get("LK_SMOKE", 0.0) / tot_ >= 0.5:                   # smoke cards are billboards sized by their own geometry
            continue
        lo_, hi_ = S.wbbox(o)
        if float((hi_ - lo_).max()) > ROCK_BACKDROP_M and not nm.startswith(("ROCK_", "CLIFF_ROCK")):   # the background cone
            continue
        n += 1
        s = inst_scale(o, ref)
        mx, mn = float(s.max()), float(s.min())
        aniso = mx / max(mn, 1e-9)
        worst = (max(worst[0], mx), max(worst[1], aniso))
        why = []
        if aniso > STRETCH_TH["aniso"] + 1e-6:
            why.append(f"STRETCHED: anisotropy {aniso:.2f} > {STRETCH_TH['aniso']:g} (scale components {s[2]:.2f}, {s[1]:.2f}, {s[0]:.2f})")
        if mx > STRETCH_TH["upper"] + 1e-6:
            why.append(f"scaled UP past {STRETCH_TH['upper']:g}x (largest component {mx:.2f})")
        if mn < STRETCH_TH["lower"] - 1e-3:
            why.append(f"shrunk below {STRETCH_TH['lower']:g}x (smallest component {mn:.2f}; a uniform shrink is not a stretch but a library kind keeps >= {STRETCH_TH['lower']:g})")
        L = S.loc(o)
        kind = None
        if 0 < L["ntri"] <= 800 and sig:                              # the library's largest mesh has 464 triangles
            k = S.key(o)
            if k not in cache:
                h_ = shapes[nm]["hash"] if nm in shapes else rock_shape(L)["hash"]
                kinds = by_hash.get(h_)
                fm = lib_fuzzy_match(L, kinds or [kk for kk in sorted(sig)])
                cache[k] = (h_, kinds, fm)
            h_, kinds, fm = cache[k]
            kind = fm[0] if fm else (kinds[0] if kinds else None)
            if kind is not None:
                nbaked += 1
                nfuzzy += 1 if (fm and not kinds) else 0
                perm = fm[1] if fm else (0, 1, 2)
                d = L["V"].max(0) - L["V"].min(0)
                ls = np.linalg.norm(np.array(o.matrix_world)[:3, :3], axis=0) / ref                  # per mesh axis (no rotation involved)
                ld = sig[kind]["dims"]
                r = np.array([d[a] * ls[a] / max(ld[perm[a]], 1e-3 * float(ld.max())) for a in range(3)])
                ra = float(r.max() / max(r.min(), 1e-9))
                tag_ = f"mesh {o.data.name} is a copy of {kind}{' (matched by shape, not by topology hash)' if not kinds else ''} (size ratio per axis {r.min():.2f}..{r.max():.2f}, anisotropy {ra:.2f})"
                if ra > STRETCH_TH["aniso"] + 1e-6:
                    why.append(f"{tag_}: STRETCHED, anisotropy {ra:.2f} > {STRETCH_TH['aniso']:g}")
                if float(r.max()) > STRETCH_TH["upper"] + 1e-6:
                    why.append(f"{tag_}: baked UP past {STRETCH_TH['upper']:g}x")
                if float(r.min()) < STRETCH_TH["lower"] - 1e-3:
                    why.append(f"{tag_}: baked below {STRETCH_TH['lower']:g}x")
            else:
                unknown += 1
        if kind is None and nm in shapes and "r3" in shapes[nm]:
            r3 = float(shapes[nm]["r3"])
            ncap += 1
            r3min = min(r3min, r3)
            if r3 < ROCK_TH["stretch_r3_min"]:
                why.append(f"mesh {o.data.name} is no props-library mesh and is baked flat / long (PCA extent ratio r3 {r3:.2f} < {ROCK_TH['stretch_r3_min']}; "
                           f"the flattest library mesh measures 0.17): a stretch past 2x applied to the vertices")
        if why:
            bad.append(f"{nm}: " + ", ".join(why))
    if err and n and not bad:
        return False, f"props library signature unavailable ({err}): baked stretch cannot be measured"
    return not bad, (f"{n} rock / DRESS_ instances: anisotropy (max/min singular value) <= {STRETCH_TH['aniso']:g}, largest scale component <= {STRETCH_TH['upper']:g}, smallest >= {STRETCH_TH['lower']:g} "
                     f"(largest component {worst[0]:.2f}, largest anisotropy {worst[1]:.2f}); {nbaked} of them are props-library meshes (topology hash, or shape match {nfuzzy}) and keep their proportions within the same rules; "
                     f"{ncap} other rock meshes keep PCA r3 >= {ROCK_TH['stretch_r3_min']} (lowest {r3min if ncap else float('nan'):.2f}; {unknown} DRESS_/rock meshes are not library meshes)"
                     if not bad else f"{len(bad)} of {n} instances stretched past 2x: " + " | ".join(bad[:5])
                     + (f" (+{len(bad) - 5} more)" if len(bad) > 5 else ""))


# ---- path / sand overlays stay under the ball
BALL_R = 0.055                                                    # the game ball (0.12 yd diameter) as a 0.055 m radius sphere


def fib_sphere(n):
    i = np.arange(n) + 0.5
    phi = np.arccos(1 - 2 * i / n)
    th = math.pi * (1 + 5 ** 0.5) * i
    return np.stack([np.cos(th) * np.sin(phi), np.sin(th) * np.sin(phi), np.cos(phi)], 1)


def g_path_under_ball(S, C):
    """The cart path / sand overlays keep UNDER the ball: the top of every DRESS_PATH_* / DRESS_SAND_* up-facing face is <= 1.5 cm above the
    ground beneath at every sample (<= 1.2 cm at its vertices), it actually rises >= 3 mm over the ground on >= 60% of its samples (no z-fight), and a 0.055 m ball
    resting on the ground anywhere on it is hidden by it at <= 10 % of its camera-facing surface (camera 4.1 m behind, 2.2 m up)."""
    objs = {n: o for n, o in S.meshes.items() if n.startswith(OVERLAY_OK)}
    if not objs:
        return True, "no DRESS_PATH_* / DRESS_SAND_* overlay on this hole (NEEDLE_PATH requires the path on hole 8)"
    rays = ground_rays(S)
    P, d_vert, d_all = [], [], []
    for n, o in objs.items():
        w = S.wld(o)
        up = w["N"][:, 2] > 0.5
        if not up.any():
            continue
        T = w["T"][up]
        W = w["W"]
        a, b, c = W[T[:, 0]], W[T[:, 1]], W[T[:, 2]]
        samples = np.concatenate([a, b, c, (a + b + c) / 3, (a + b) / 2, (b + c) / 2, (c + a) / 2, (2 * a + b + c) / 4, (a + 2 * b + c) / 4, (a + b + 2 * c) / 4])
        nvtx = len(a) * 3
        step = max(1, len(samples) // 40000)
        for k in range(0, len(samples), step):
            x, y, z = samples[k]
            r = rays.cast(float(x), float(y))
            if r is None:
                continue
            dz = float(z) - r[0]
            d_all.append(dz)
            if k < nvtx:
                d_vert.append(dz)
            P.append((float(x), float(y), r[0], n))
    if not d_all:
        return False, "overlay has no sample over land"
    d_all, d_vert = np.array(d_all), np.array(d_vert if d_vert else [0.0])
    top_all, top_v = float(d_all.max()), float(d_vert.max())
    emerge = float((d_all >= 0.003).mean())
    # ball-hide test
    occ = TriIndex(S, list(objs.values()), up_only=False)
    cells, pos = set(), []
    for x, y, zg, n in P:
        k = (int(math.floor(x / 1.0)), int(math.floor(y / 1.0)))
        if k not in cells:
            cells.add(k)
            pos.append((x, y, zg))
    pin = S.pos(S.all["MARKER_PIN"]) if "MARKER_PIN" in S.all else np.array([0.0, 100.0, C.pz])
    pts = fib_sphere(48)
    worst, nbad = 0.0, 0
    for x, y, zg in pos:
        fwd = np.array([pin[0] - x, pin[1] - y])
        ln = np.linalg.norm(fwd)
        fwd = fwd / ln if ln > 1e-6 else np.array([0.0, 1.0])
        cen = np.array([x, y, zg + BALL_R])
        cam = np.array([x - fwd[0] * 4.1, y - fwd[1] * 4.1, zg + 2.2])
        vis = pts[(pts @ (cam - cen)) > 0]
        hid = 0
        for q in vis:
            tgt = cen + q * BALL_R
            dvec = tgt - cam
            dist = float(np.linalg.norm(dvec))
            r = occ.first_front(cam.tolist(), (dvec / dist).tolist(), dist - 1e-3)
            if r is not None:
                hid += 1
        fr = hid / max(len(vis), 1)
        worst = max(worst, fr)
        nbad += fr > 0.10
    p = []
    if top_all > 0.015:
        p.append(f"overlay top reaches {top_all * 100:.1f} cm above the ground (need <= 1.5 cm at every sample)")
    if top_v > 0.012:
        p.append(f"overlay vertices reach {top_v * 100:.1f} cm above the ground (need <= 1.2 cm)")
    if emerge < 0.6:
        p.append(f"only {emerge:.0%} of the overlay samples rise >= 3 mm over the ground (need >= 60%: buried / z-fighting)")
    if nbad:
        p.append(f"{nbad} of {len(pos)} ball positions have > 10% of the ball hidden (worst {worst:.0%})")
    return not p, ("; ".join(p) if p else
                   f"{len(objs)} overlay meshes ({len(d_all)} samples): top at most {top_all * 100:.2f} cm above the ground, vertices {top_v * 100:.2f} cm, "
                   f"{emerge:.0%} of samples >= 3 mm up; ball (r {BALL_R} m) hidden at most {worst:.1%} over {len(pos)} positions (need <= 10%)")


# ---- sea stacks must taper
def section_pts(W, T, z):
    a, b, c = W[T[:, 0]], W[T[:, 1]], W[T[:, 2]]
    pts = []
    for p, q in ((a, b), (b, c), (c, a)):
        cross = (p[:, 2] - z) * (q[:, 2] - z) < 0
        if cross.any():
            t = (z - p[cross, 2]) / (q[cross, 2] - p[cross, 2])
            pts.append((p[cross] + (q[cross] - p[cross]) * t[:, None])[:, :2])
    return np.vstack(pts) if pts else np.zeros((0, 2))


def hull_area(pts):
    if len(pts) < 3:
        return 0.0
    idx = convex_hull_2d([tuple(q) for q in pts.tolist()])
    H = pts[idx]
    return 0.5 * abs(float(np.dot(H[:, 0], np.roll(H[:, 1], -1)) - np.dot(H[:, 1], np.roll(H[:, 0], -1))))


def silhouette_fill(W, T, az_deg, z0):
    a = math.radians(az_deg)
    u = W[:, 0] * math.cos(a) + W[:, 1] * math.sin(a)
    v = np.maximum(W[:, 2], z0)
    keep = (W[T, 2] > z0).any(axis=1)
    if not keep.any():
        return 0.0
    Tk = T[keep]
    V2 = np.stack([u, v], 1)
    P = V2[np.unique(Tk)]
    wd, ht = float(np.ptp(P[:, 0])), float(np.ptp(P[:, 1]))
    if wd < 1e-6 or ht < 1e-6:
        return 0.0
    cell = max(wd, ht) / 90.0
    mask, _x, _y = raster_mask(V2, Tk, cell, pad=0)
    return float(mask.sum()) * cell * cell / (wd * ht)


def stack_metrics(S, o):
    """Taper numbers of a tall sea stack in world space (height measured above the waterline z = 0): diameter of the section at 90 % /
    at 10 % of the height, largest flat (nz >= 0.9) face group above 20 % height relative to the 10 % section area, worst silhouette
    bbox fill over 4 azimuths."""
    w = S.wld(o)
    W, T, N, A = w["W"], w["T"], w["N"], w["A"]
    zmin, zmax = float(W[:, 2].min()), float(W[:, 2].max())
    z0 = max(zmin, 0.0)
    H = zmax - z0
    if H < 1.0 or len(T) == 0:
        return dict(ok=False, why=["no height above the waterline"], H=H, taper=0.0, cap=0.0, fill=0.0)
    p10, p90 = section_pts(W, T, z0 + 0.1 * H), section_pts(W, T, z0 + 0.9 * H)
    a10, a90 = hull_area(p10), hull_area(p90)
    d10, d90 = 2 * math.sqrt(a10 / math.pi), 2 * math.sqrt(a90 / math.pi)
    taper = d90 / max(d10, 1e-9)
    flat = (N[:, 2] >= 0.9) & (A > 1e-12) & (W[T][:, :, 2].mean(axis=1) >= z0 + 0.2 * H)
    cap = 0.0
    if flat.any():
        Tf, Af = T[flat], A[flat]
        lab = tri_components(Tf)
        cap = max(float(Af[lab == c].sum()) for c in np.unique(lab))
    capr = cap / max(a10, 1e-9)
    fills = [silhouette_fill(W, T, az, z0) for az in (0.0, 45.0, 90.0, 135.0)]
    # a rock ISLAND (flat-topped outcrop with a grass cap, wider than it is tall) is not a stack. The test is the geometry, never the object name:
    # H / d10 < 1 AND the upward faces of the top fifth carry LK_ROUGH
    top_up = (N[:, 2] > 0.5) & (W[T][:, :, 2].mean(axis=1) >= z0 + 0.8 * H) & (A > 1e-12)
    nm_ = np.array(w["names"], object)[w["mi"]]
    cap_rough = float(A[top_up & (nm_ == "LK_ROUGH")].sum() / max(float(A[top_up].sum()), 1e-12)) if top_up.any() else 0.0
    island = bool(H / max(d10, 1e-9) < 1.0 and cap_rough >= 0.5)
    why = []
    if taper > TH2["stack_taper"]:
        why.append(f"width at 90% height / 10% height = {taper:.2f} > {TH2['stack_taper']}")
    if capr > TH2["stack_cap"]:
        why.append(f"flat cap {capr:.0%} of the base area > {TH2['stack_cap']:.0%}")
    if max(fills) > TH2["stack_fill"]:
        why.append(f"silhouette bbox fill {max(fills):.2f} > {TH2['stack_fill']}")
    return dict(ok=not why, why=why, H=H, taper=taper, cap=capr, fill=max(fills), d10=d10, d90=d90, island=island)


def sea_stack_objects(S, C):
    """Tall rocks standing in the water. v2 area U: the set is the ROCKS of rock_shapes (by name OR by material, open-shell cladding excluded), not the objects called ROCK_*: a
    stacked-box ziggurat named DRESS_STACK made of the rock material is a sea stack too, and a tall boxy stack renamed ROCK_WALL_xx no longer escapes by its prefix."""
    out = {}
    for n in rock_shapes(S):
        o = S.meshes[n]
        lo, hi = S.wbbox(o)
        if hi[2] - max(lo[2], 0.0) < C.pz:
            continue
        c = (lo + hi) / 2
        if C.dry_m(np.array([c[0]]), np.array([c[1]]))[0]:
            continue
        out[n] = o
    return out


def stack_cached(S, C):
    if "_stacks" not in S.__dict__:
        res, cache = {}, {}
        for n, o in sea_stack_objects(S, C).items():
            k = (S.key(o), tuple(np.round(np.array(o.matrix_world).ravel(), 3).tolist()))
            if k not in cache:
                cache[k] = stack_metrics(S, o)
            if not cache[k].get("island"):
                res[n] = cache[k]
        S._stacks = res
    return S._stacks


def g_seastack_tapered(S, C):
    """'Sea stacks read as stacked boxes' -> every tall rock standing in the water tapers like a spire: width at 90 % height / width at 10 % <= 0.45,
    flat cap <= 25 % of the base area, silhouette bbox fill <= 0.62."""
    if C.hole == 10:
        return True, "not applicable: the crater's stacks are basalt columns (CRATER_COLUMNS)"
    st = stack_cached(S, C)
    if not st:
        return C.hole != 8, ("no sea stack on this hole (NEEDLE_SEA_STACKS requires them on hole 8)" if C.hole != 8 else "no sea stack found: hole 8 needs >= 4")
    bad = {n: r for n, r in st.items() if not r["ok"]}
    ok = not bad
    tmax = max(r["taper"] for r in st.values())
    return ok, (f"{len(st)} sea stacks taper: worst width90/width10 {tmax:.2f} (<= {TH2['stack_taper']}), worst flat cap {max(r['cap'] for r in st.values()):.0%} "
                f"(<= {TH2['stack_cap']:.0%}), worst bbox fill {max(r['fill'] for r in st.values()):.2f} (<= {TH2['stack_fill']})" if ok else
                f"{len(bad)} of {len(st)} sea stacks are boxes/pillars: " + " | ".join(f"{n} (H {r['H']:.0f} m): {'; '.join(r['why'])}" for n, r in sorted(bad.items())[:4]))


def g_needle_stacks(S, C):
    out, bare = [], []
    sh = rock_shapes(S)
    st = stack_cached(S, C)
    for n in st:
        if sh.get(n, {}).get("ok") and st[n]["ok"]:
            out.append(n)
        else:
            bare.append(n)
    ok = len(out) >= TH["stacks8"]
    return ok, (f"{len(out)} sea stacks (ROCK_* in the water rising >= PLAY_Z {C.pz:g} m) that are faceted rocks (welded >= 60 verts / 100 faces, not a blob) AND taper "
                f"(width90/width10 <= {TH2['stack_taper']}); need >= {TH['stacks8']}): {short(out, 6)}" + (f"; not counted: {len(bare)} {short(bare, 4)}" if bare else ""))


def g_one_green(S, C, B):
    problems = []
    for n in ("GREEN", "MARKER_PIN", "FLAG", "FLAG_POLE", "HOLE_CUP"):
        c = sum(1 for k in S.all if base_name(k) == n)
        if c != 1:
            problems.append(f"{c} x {n}")
    greenish = sorted(n for n in S.all if n.startswith("GREEN") and n not in ("GREEN", "GREEN_APRON"))
    if greenish:
        problems.append(f"extra GREEN* objects {greenish}")
    m = mats_on(S)
    for mat in ("LK_GREEN", "LK_FAIRWAY"):
        bad = sorted(n for n in m.get(mat, []) if not PLAY_OBJECT_RE.match(n))
        if bad:
            problems.append(f"{mat} on non-play objects {short(bad, 5)} (a second green/fairway look-alike)")
    # v2: a lawn or a pole dressed as scenery
    grassy = {"LK_PLANTS", "LK_GREEN", "LK_FAIRWAY", "LK_ROUGH", "LK_SCRUB"}
    lawns, poles, lawn_total = [], [], 0.0
    base_set = set(B["all_names"])
    for n, o in S.meshes.items():
        if n in base_set:
            continue
        if n.startswith(("DRESS_", "PLANT_", "ROCK_")) and (set(re.split(r"[_.0-9]+", n.upper())) & {"FLAG", "FLAGSTICK", "POLE", "PIN", "CUP", "LAWN", "GREEN"}):
            poles.append(n)
        if not n.startswith(("DRESS_", "PLANT_")) or n.startswith(("DRESS_PATH", "DRESS_SAND", "DRESS_ARCH", "DRESS_RUIN", "DRESS_CONE", "DRESS_BLOCK", "DRESS_SMOKE")):
            continue
        w = S.wld(o)
        nm = np.array(w["names"], object)[w["mi"]] if len(w["mi"]) else np.zeros(0, object)
        flat = (w["N"][:, 2] > 0.95) & np.isin(nm, list(grassy))
        if flat.any():
            lawn_total += float(w["A"][flat].sum()) if n.startswith("DRESS_") else 0.0
            if float(w["A"][flat].sum()) >= TH2["lawn_m2"]:
                lawns.append(f"{n} ({float(w['A'][flat].sum()):.0f} m2)")
    if lawns or lawn_total >= 2 * TH2["lawn_m2"]:
        problems.append(f"flat grass-coloured scenery (a second green dressed as DRESS_/PLANT_): {short(lawns, 4)}; all DRESS_ together {lawn_total:.0f} m2 "
                        f"(each object < {TH2['lawn_m2']:g} m2 and the sum < {2 * TH2['lawn_m2']:g} m2)")
    if poles:
        problems.append(f"scenery named like a pin / flag / lawn: {short(poles, 4)}")
    return not problems, ("; ".join(problems) if problems else
                          "exactly one GREEN, one MARKER_PIN, one FLAG/FLAG_POLE/HOLE_CUP; LK_GREEN/LK_FAIRWAY only on the play objects")


def g_sand_names(S, C):
    m = mats_on(S)
    bad = sorted(n for n in m.get("LK_SAND", []) if not (re.match(r"^BUNKER_\d\d(_LIP)?$", n) or n.startswith("DRESS_SAND")))
    sand = sorted(n for n in S.all if re.match(r"^BUNKER_\d\d$", n))
    ok = not bad and len(sand) == len(C.bunkers)
    return ok, (f"{len(sand)} scoring bunker meshes {sand} (design {len(C.bunkers)}); every other LK_SAND mesh is DRESS_SAND_* "
                f"({len([n for n in m.get('LK_SAND', []) if n.startswith('DRESS_SAND')])})" if ok else
                f"bunker meshes {sand} vs design {len(C.bunkers)}; LK_SAND on {short(bad)} (visual sand must be DRESS_SAND_*)")


def g_no_anim(S, C):
    bad = []
    for ob in bpy.data.objects:
        if ob.animation_data is not None and (ob.animation_data.action or len(ob.animation_data.drivers)):
            bad.append(f"{ob.name} animated")
        if ob.type == "ARMATURE":
            bad.append(f"{ob.name} armature")
        d = ob.data
        if d is not None and getattr(d, "shape_keys", None) is not None:
            bad.append(f"{ob.name} shape keys")
    for mat in bpy.data.materials:
        if (mat.animation_data and mat.animation_data.action) or (mat.node_tree and mat.node_tree.animation_data
                                                                    and (mat.node_tree.animation_data.action or len(mat.node_tree.animation_data.drivers))):
            bad.append(f"material {mat.name} animated")
    if len(bpy.data.actions):
        bad.append(f"{len(bpy.data.actions)} actions {[a.name for a in bpy.data.actions][:4]}")
    if len(bpy.data.armatures):
        bad.append(f"{len(bpy.data.armatures)} armature datablocks")
    return not bad, ("; ".join(bad[:8]) if bad else
                     f"static: no action, driver, shape key or armature on {len(bpy.data.objects)} objects / {len(bpy.data.materials)} materials")


def g_tri_budget(S, C):
    tot = sum(S.loc(o)["ntri"] for o in S.meshes.values())
    uniq = {}
    for o in S.meshes.values():
        uniq[S.key(o)] = S.loc(o)["ntri"]
    by = {}
    for n, o in S.meshes.items():
        p = n.split("_")[0]
        by[p] = by.get(p, 0) + S.loc(o)["ntri"]
    top = sorted(by.items(), key=lambda kv: -kv[1])[:6]
    return tot <= TRI_BUDGET, (f"{tot} triangles over {len(S.meshes)} mesh objects (<= {TRI_BUDGET}; {sum(uniq.values())} in unique "
                               f"meshes); by prefix {top}")


# ----------------------------------------------------------------------------- gates: water / lava
def g_ocean(S, C):
    problems = []
    for n, mat in (("WATER_OCEAN", "LK_WATER"), ("WATER_SHELF", "LK_WATER_SHALLOW")):
        o = S.meshes.get(n)
        if o is None:
            problems.append(f"{n} missing")
            continue
        um = S.used_mats(o)
        if um != {mat}:
            problems.append(f"{n} uses {sorted(um)} (want only {mat})")
    foam = sorted(n for n in S.all if "FOAM" in n.upper())
    if foam:
        problems.append(f"foam objects {foam}")
    return not problems, ("; ".join(problems) if problems else "WATER_OCEAN = LK_WATER (TennisWater), WATER_SHELF = LK_WATER_SHALLOW, no *FOAM* object")


def shore_samples(C, step=4.0, stretches=None):
    if stretches:
        segs = []
        idx = {s["name"]: s for s in C.sc.get("shore_stretches", [])}
        n = len(C.shore_m)
        for nm in stretches:
            s = idx.get(nm)
            if s is None:
                continue
            ids = list(range(s["first"], s["last"] + 2))
            pts = C.shore_m[[i % n for i in ids]]
            segs.append(sample_polyline(pts, step, closed=False))
        if segs:
            return np.vstack(segs)
    return sample_polyline(C.shore_m, step, closed=True)


def coverage(bvh, P2, z, dmax):
    if bvh is None or len(P2) == 0:
        return 0.0
    hit = 0
    for x, y in P2.tolist():
        r = bvh.find_nearest(Vector((x, y, z)), dmax)
        if r[0] is not None:
            hit += 1
    return hit / len(P2)


# ----------------------------------------------------------------------------- v2 water gates: foam strip, surf patches, shelf width
def shore_frame(C, step=2.0):
    """Samples every ~step m along the design shore (closed polyline, metres): P (n,2), N (n,2) unit normals pointing INTO the water,
    Tg (n,2) tangents, s (n,) cumulative arc length."""
    P = sample_polyline(C.shore_m, step, closed=True)
    D = np.roll(P, -1, 0) - np.roll(P, 1, 0)
    D /= np.maximum(np.linalg.norm(D, axis=1), 1e-9)[:, None]
    N = np.stack([D[:, 1], -D[:, 0]], 1)
    wet = ~C.dry_m(P[:, 0] + 1.5 * N[:, 0], P[:, 1] + 1.5 * N[:, 1])
    N = np.where(wet[:, None], N, -N)
    seg = np.linalg.norm(np.roll(P, -1, 0) - P, axis=1)
    s = np.concatenate([[0.0], np.cumsum(seg)[:-1]])
    return P, N, D, s, float(seg.sum())


def terrain_loops(S):
    """Boundary loops of the TERRAIN top (up-facing faces) read from the mesh itself: [(points (n,2) closed polyline, outward (n,2) unit normals
    pointing away from the land)]. This is the real shoreline (Needle: 3 pad loops; the design SHORE polygon also spans the water between pads)."""
    key = "_terrain_loops"
    if key in S.__dict__:
        return S.__dict__[key]
    edges = {}
    cen_of = {}
    for o in coll_objects(S):
        if not o.name.startswith("TERRAIN"):
            continue
        w = S.wld(o)
        up = (w["N"][:, 2] > 0.5) & (w["A"] > 1e-12)
        W = w["W"]
        wid = weld_ids(W, 1e-3)
        T = wid[w["T"][up]]
        for t, (a, b, c) in zip(w["T"][up].tolist(), T.tolist()):
            cen = W[t].mean(axis=0)[:2]
            for u, v in ((a, b), (b, c), (c, a)):
                k = (u, v) if u < v else (v, u)
                edges.setdefault(k, []).append(cen)
        xy = {}
        for i, vid in enumerate(wid.tolist()):
            xy.setdefault(vid, W[i, :2])
        cen_of.update(xy)
    bnd = {k: v[0] for k, v in edges.items() if len(v) == 1}
    adj = {}
    for (u, v) in bnd:
        adj.setdefault(u, []).append(v)
        adj.setdefault(v, []).append(u)
    seen, loops = set(), []
    for (u0, v0) in bnd:
        if (u0, v0) in seen:
            continue
        loop, a, b = [u0], u0, v0
        seen.add((u0, v0))
        guard = 0
        while b != u0 and guard < 100000:
            guard += 1
            loop.append(b)
            nxt = [x for x in adj.get(b, []) if x != a and ((b, x) if b < x else (x, b)) not in seen]
            if not nxt:
                break
            seen.add((b, nxt[0]) if b < nxt[0] else (nxt[0], b))
            a, b = b, nxt[0]
        if len(loop) >= 6:
            loops.append(loop)
    out = []
    for loop in loops:
        P = np.array([cen_of[v] for v in loop], float)
        n = len(P)
        N = np.zeros((n, 2))
        for i in range(n):
            a, b = loop[i], loop[(i + 1) % n]
            k = (a, b) if a < b else (b, a)
            c = bnd.get(k)
            e = P[(i + 1) % n] - P[i]
            ln = float(np.hypot(*e))
            if ln < 1e-9 or c is None:
                continue
            nrm = np.array([e[1], -e[0]]) / ln
            if float(np.dot(nrm, (P[i] + P[(i + 1) % n]) / 2 - c)) < 0:
                nrm = -nrm                                  # away from the triangle's centroid = out of the land
            N[i] = nrm
        out.append((P, N))
    S.__dict__[key] = out
    return out


def terrain_shore_frame(S, C, step=2.0):
    """shore_frame() from the TERRAIN loops: samples every ~step m, outward normals, tangents, arc length, total length and the loop id of every
    sample. Falls back to the design SHORE polygon when the scene has no TERRAIN mesh."""
    loops = terrain_loops(S)
    if not loops:
        P_, N_, T_, s_, tot_ = shore_frame(C, step)
        return P_, N_, T_, s_, tot_, np.zeros(len(P_), int)
    Ps, Ns, Ts, ss, ls, total = [], [], [], [], [], 0.0
    for li, (P, N) in enumerate(loops):
        Pc = np.vstack([P, P[:1]])
        seg = np.hypot(*np.diff(Pc, axis=0).T)
        cum = np.concatenate([[0.0], np.cumsum(seg)])
        L = float(cum[-1])
        if L < step:
            continue
        m = max(2, int(round(L / step)))
        t = np.arange(m) * (L / m)
        idx = np.clip(np.searchsorted(cum, t, side="right") - 1, 0, len(P) - 1)
        fr = (t - cum[idx]) / np.maximum(seg[idx], 1e-9)
        pts = P[idx] + (Pc[idx + 1] - P[idx]) * fr[:, None]
        nrm = N[idx]
        # smooth the normal over neighbouring samples (the boundary is polyline-noisy)
        k = 2
        sm = sum(np.roll(nrm, j, axis=0) for j in range(-k, k + 1))
        sm /= np.maximum(np.linalg.norm(sm, axis=1), 1e-9)[:, None]
        tg = np.stack([-sm[:, 1], sm[:, 0]], 1)
        Ps.append(pts)
        Ns.append(sm)
        Ts.append(tg)
        ss.append(t + total)
        ls.append(np.full(m, li))
        total += L
    return np.vstack(Ps), np.vstack(Ns), np.vstack(Ts), np.concatenate(ss), total, np.concatenate(ls)


class TriIndex:
    """BVH over the triangles of several mesh objects (world space) with per-triangle data, so a query can say WHICH triangle it hit:
    own (object name), matn (material name), nz (normal z), alpha (vertex alpha mean, 1 when the mesh has none).
    up_only keeps triangles with nz > 0.5; mat keeps triangles of one material."""

    def __init__(self, S, objs, up_only=False, mat=None):
        V, T, off = [], [], 0
        own, matn, nz, alpha = [], [], [], []
        for o in objs:
            w = S.wld(o)
            L = S.loc(o)
            if len(w["T"]) == 0:
                continue
            names = np.array(w["names"], object)[w["mi"]]
            keep = np.ones(len(w["T"]), bool)
            if up_only:
                keep &= w["N"][:, 2] > 0.5
            if mat is not None:
                keep &= names == mat
            if not keep.any():
                continue
            al = L["alpha_tri"] if L.get("alpha_tri") is not None else np.ones(len(w["T"]))
            V.append(w["W"])
            T.append(w["T"][keep] + off)
            alpha.append(al[keep])
            own.extend([o.name] * int(keep.sum()))
            matn.extend(names[keep].tolist())
            nz.append(w["N"][keep, 2])
            off += len(w["W"])
        self.n = int(sum(len(t) for t in T))
        if not V:
            self.bvh, self.alpha, self.V, self.T = None, np.zeros(0), np.zeros((0, 3)), np.zeros((0, 3), np.int64)
            self.own, self.matn, self.nz = [], [], np.zeros(0)
            return
        self.V, self.T = np.vstack(V), np.vstack(T)
        self.alpha = np.concatenate(alpha)
        self.own, self.matn, self.nz = own, matn, np.concatenate(nz)
        self.bvh = BVHTree.FromPolygons([tuple(v) for v in self.V.tolist()], [tuple(t) for t in self.T.tolist()], all_triangles=True)

    def nearest(self, x, y, z, dmax):
        if self.bvh is None:
            return None
        loc, _n, idx, d = self.bvh.find_nearest(Vector((x, y, z)), dmax)
        return None if loc is None else (float(d), int(idx), loc)

    def hit_down(self, x, y, z0=1000.0):
        """First triangle hit by a vertical ray from (x, y, z0) going down (any facing): (z, index) or None."""
        if self.bvh is None:
            return None
        loc, _nor, idx, _d = self.bvh.ray_cast(Vector((x, y, z0)), Vector((0.0, 0.0, -1.0)))
        return None if loc is None else (float(loc.z), int(idx))

    def first_front(self, origin, direction, maxd=1e9, skip=12):
        """First FRONT-facing hit of a ray (back faces are culled like a single-sided material): (distance, index, location) or None."""
        if self.bvh is None:
            return None
        o = Vector(origin)
        d = Vector(direction).normalized()
        travelled = 0.0
        for _ in range(skip):
            loc, nor, idx, dist = self.bvh.ray_cast(o, d, maxd - travelled)
            if loc is None:
                return None
            if nor.dot(d) < 0.0:
                return travelled + float(dist), int(idx), loc
            o = loc + d * 1e-4
            travelled += float(dist) + 1e-4
        return None


def circular_runs(mask, step):
    """Runs of True on a CIRCULAR sample list: [(start index, length in samples)], and the gaps between consecutive runs (samples)."""
    n = len(mask)
    if n == 0 or not mask.any():
        return [], []
    if mask.all():
        return [(0, n)], []
    k = int(np.flatnonzero(~mask)[0])
    m = np.roll(mask, -k)
    d = np.diff(np.concatenate(([0], m.astype(np.int8), [0])))
    st, en = np.flatnonzero(d == 1), np.flatnonzero(d == -1)
    runs = [((int(a) + k) % n, int(b - a)) for a, b in zip(st.tolist(), en.tolist())]
    gaps = [int(st[i + 1] - en[i]) for i in range(len(st) - 1)]
    gaps.append(int(n - en[-1] + st[0]))
    return runs, gaps


def raster_mask(V2, T, cell, pad=1, edges=False):
    """Boolean footprint raster of triangles (V2 (n,2) xy, T (m,3)): returns (mask, x0, y0). edges=True also draws the projected edges of triangles whose plan
    area is a sliver (< 0.05 cell2: a vertical or near-vertical wall projects to a LINE, which an area fill would drop)."""
    P = V2[np.unique(T)]
    x0, y0 = float(P[:, 0].min()) - pad * cell, float(P[:, 1].min()) - pad * cell
    nx = int(math.ceil((float(P[:, 0].max()) - x0) / cell)) + 1 + pad
    ny = int(math.ceil((float(P[:, 1].max()) - y0) / cell)) + 1 + pad
    mask = np.zeros((ny, nx), bool)
    for t in T:
        A, B, Cc = V2[t[0]], V2[t[1]], V2[t[2]]
        i0 = max(0, int(math.floor((min(A[0], B[0], Cc[0]) - x0) / cell)))
        i1 = min(nx, int(math.ceil((max(A[0], B[0], Cc[0]) - x0) / cell)) + 1)
        j0 = max(0, int(math.floor((min(A[1], B[1], Cc[1]) - y0) / cell)))
        j1 = min(ny, int(math.ceil((max(A[1], B[1], Cc[1]) - y0) / cell)) + 1)
        if i1 <= i0 or j1 <= j0:
            continue
        X, Y = np.meshgrid(x0 + (np.arange(i0, i1) + 0.5) * cell, y0 + (np.arange(j0, j1) + 0.5) * cell)
        d = (B[1] - Cc[1]) * (A[0] - Cc[0]) + (Cc[0] - B[0]) * (A[1] - Cc[1])
        if edges and abs(d) < 0.1 * cell * cell:
            for P0, P1 in ((A, B), (B, Cc), (Cc, A)):
                n_ = max(2, int(math.ceil(math.hypot(P1[0] - P0[0], P1[1] - P0[1]) / (0.5 * cell))) + 1)
                ii = np.floor((np.linspace(P0[0], P1[0], n_) - x0) / cell).astype(int)
                jj = np.floor((np.linspace(P0[1], P1[1], n_) - y0) / cell).astype(int)
                okc = (ii >= 0) & (ii < nx) & (jj >= 0) & (jj < ny)
                mask[jj[okc], ii[okc]] = True
        if abs(d) < 1e-12:
            continue
        l1 = ((B[1] - Cc[1]) * (X - Cc[0]) + (Cc[0] - B[0]) * (Y - Cc[1])) / d
        l2 = ((Cc[1] - A[1]) * (X - Cc[0]) + (A[0] - Cc[0]) * (Y - Cc[1])) / d
        mask[j0:j1, i0:i1] |= (l1 >= -1e-9) & (l2 >= -1e-9) & ((1.0 - l1 - l2) >= -1e-9)
    return mask, x0, y0


def surf_objects(S):
    """Meshes that draw whitewater: WATER_SURF* and anything carrying LK_SURF (a DRESS_ strip with the surf material is still foam)."""
    return {n: o for n, o in S.meshes.items() if n.startswith("WATER_SURF") or (S.used_mats(o) & {"LK_SURF", "LK_FALL"} and not n.startswith("WATER_FALL"))
            or ("LK_FALL" in S.used_mats(o) and S.loc(o)["ntri"] and float((S.wld(o)["N"][:, 2] > 0.8).mean()) > 0.2)}


def surf_cover_mask(S, C, step=1.0, dmax=1.0, amin=0.3):
    """Per shore sample (TERRAIN loops): is there LK_SURF (up-facing, vertex alpha >= amin) within dmax m? Returns (covered, shore length, P, loop ids)."""
    P, N, Tg, s, total, lid = terrain_shore_frame(S, C, step)
    objs = list(surf_objects(S).values())
    idx = TriIndex(S, objs, up_only=True, mat="LK_SURF")
    cov = np.zeros(len(P), bool)
    if idx.bvh is None:
        return cov, total, P, lid
    for i, (x, y) in enumerate(P.tolist()):
        r = idx.nearest(x, y, 0.12, dmax)
        if r is not None and idx.alpha[r[1]] >= amin:
            cov[i] = True
    return cov, total, P, lid


def loop_runs(cov, lid, step):
    """Runs of covered samples and the gaps between them, per shoreline loop (each loop is circular): ([run length in samples], [gap in samples])."""
    runs, gaps = [], []
    for li in np.unique(lid):
        r, g = circular_runs(cov[lid == li], step)
        runs += [l for _s, l in r]
        gaps += g
    return runs, gaps


def srgb_encode(c):
    """Linear scene value -> the sRGB-encoded value a viewer / Unity shows (Blender stores Base Color LINEAR: the default_value of a pale material reads darker than it looks)."""
    c = min(1.0, max(0.0, float(c)))
    return 12.92 * c if c <= 0.0031308 else 1.055 * (c ** (1.0 / 2.4)) - 0.055


def is_whitish(rgb, s_max=0.20, v_min=0.80):
    h_, s_, v_ = colorsys.rgb_to_hsv(*[min(1.0, max(0.0, float(c))) for c in rgb])
    return v_ >= v_min and s_ <= s_max


def white_materials(S, C):
    """Material names whose albedo is white-ish (mean V >= 0.80 and S <= 0.15 over the opaque pixels of the contract texture; an unlinked Principled base colour with V >= 0.8, S <= 0.2
    **as stored AND as displayed (sRGB-encoded: v2 area U, review: a pale non-LK band with a linear base colour of 0.75 reads 0.88 on screen and slipped through the raw test)**; a base
    colour fed by an image texture whose mean is white-ish) plus anything called FOAM / SURF / SPRAY / WHITE: the foam test must not depend on a material being called LK_SURF."""
    key = "_white_mats"
    if key in S.__dict__:
        return S.__dict__[key]
    white = set()
    for mat, (_tile, cf, _nf, _ef) in LK.items():
        px = look_png(C, cf) if cf else None
        if px is not None:
            op = px[..., 3] > 0.05
            if op.any():
                _h, ws_, wv_ = mean_hsv(px[..., :3][op].mean(0))
                if wv_ >= 0.80 and ws_ <= 0.15:
                    white.add(mat)
    for name in mats_on(S):
        if any(t in name.upper() for t in ("FOAM", "SURF", "SPRAY", "WHITE")) and name != "LK_WATER_SHALLOW":
            white.add(name)
        mt = bpy.data.materials.get(name)
        if mt is None or name in LK:
            continue
        try:
            cols, imgs = [], []
            if mt.node_tree:
                for nd in mt.node_tree.nodes:
                    if nd.type == "BSDF_PRINCIPLED":
                        bc = nd.inputs["Base Color"]
                        if not bc.is_linked:
                            cols.append(tuple(bc.default_value[:3]))
                        else:
                            imgs += find_upstream(bc, ("TEX_IMAGE",))
            if not cols and not imgs:
                cols.append(tuple(mt.diffuse_color[:3]))
            for col in cols:
                # the stored (linear) value AND its sRGB-encoded display value: a pale colour must be caught in either
                if is_whitish(col) or is_whitish([srgb_encode(c) for c in col]):
                    white.add(name)
            for nd in imgs:
                _fn, ap = img_file(nd)
                if ap and os.path.isfile(ap):
                    px = image_stats(ap)["px"]
                    op = px[..., 3] > 0.05
                    if op.any():
                        _h, ws_, wv_ = mean_hsv(px[..., :3][op].mean(0))
                        if wv_ >= 0.80 and ws_ <= 0.15:
                            white.add(name)
        except Exception:  # noqa: BLE001
            pass
    S.__dict__[key] = white
    return white


FALL_SHEET_MAX_EXTENT = 8.0          # m: plan extent of a waterfall sheet component (the library's WATER_FALL sheet is 3 m wide)
FALL_SHEET_MAX_NZ = 0.6              # the NEEDLE_WATERFALL gate's own definition of a sheet: mean |nz| <= 0.6
FALL_SHEET_MAX_COMPONENTS = 2        # sheets exempt per hole (a waterfall is one sheet; frame / second sheet = 2)
FALL_SHEET_MAX_AREA = 60.0           # m2 of exempt sheet in total (3 m x 12 m = 36 m2 in the library)


def fall_sheets(S):
    """{object name: bool per triangle} = the LK_FALL triangles that belong to a WATERFALL SHEET and are exempt from the foam rules: a welded component of LK_FALL
    triangles (any object, any name) whose plan extent is <= 8 m and whose area-weighted mean |nz| is <= 0.6 (steep: a falling sheet, the NEEDLE_WATERFALL
    definition), at most 2 such components and 60 m2 in total, largest first. A sloped white skirt around the shore is one long component (or many pieces
    beyond the budget) and is judged as foam in full. Everything else carrying LK_FALL counts as whitewater whatever its slope."""
    key = "_fall_sheets"
    if key in S.__dict__:
        return S.__dict__[key]
    comps = []
    out = {}
    for n, o in S.meshes.items():
        if n.startswith(COLL_PREFIXES) or "LK_FALL" not in S.used_mats(o):
            continue
        w = S.wld(o)
        names = np.array(w["names"], object)[w["mi"]]
        m_ = (names == "LK_FALL") & (w["A"] > 1e-12)
        out[n] = np.zeros(len(w["T"]), bool)
        if not m_.any():
            continue
        idx = np.flatnonzero(m_)
        lab = tri_components(weld_ids(w["W"], 1e-3)[w["T"][idx]])
        for c in np.unique(lab).tolist():
            ti = idx[lab == c]
            P = w["W"][np.unique(w["T"][ti])]
            ext = float(max(np.ptp(P[:, 0]), np.ptp(P[:, 1])))
            area = float(w["A"][ti].sum())
            nz = float((np.abs(w["N"][ti, 2]) * w["A"][ti]).sum() / max(area, 1e-12))
            if ext <= FALL_SHEET_MAX_EXTENT and nz <= FALL_SHEET_MAX_NZ:
                comps.append((area, n, ti))
    used = 0.0
    for k, (area, n, ti) in enumerate(sorted(comps, key=lambda c: -c[0])):
        if k >= FALL_SHEET_MAX_COMPONENTS or used + area > FALL_SHEET_MAX_AREA:
            continue
        used += area
        out[n][ti] = True
    S.__dict__[key] = out
    return out


def white_geometry(S, C, amin=0.3):
    """Foam-like triangles of every visible non-collision mesh, ANY ORIENTATION (the v2 review: a white LK_SURF skirt at nz 0.44 or a vertical white wall around the
    shore is as visible from the air as a flat strip): LK_SURF with vertex alpha >= amin; LK_FALL except a waterfall sheet (fall_sheets: only the flat part of a
    sheet counts, nz > 0.8); LK_SMOKE on anything but the DRESS_SMOKE cards (cards keep the up-facing rule: they are billboards); any other white-ish
    material (white_materials, whatever it is called, MAT_* included) of any facing; only the game's hidden placeholders (FLAG, HOLE_CUP, BALL_START, MARKER_*) and the
    collision meshes are skipped. Returns (V (n,3), T (m,3), area (m,) (3D area),
    owner object names (m,))."""
    key = "_white_geo"
    if key in S.__dict__:
        return S.__dict__[key]
    wm = white_materials(S, C) | {"LK_SURF", "LK_FALL"}
    sheets = fall_sheets(S)
    V, T, A, own, off = [], [], [], [], 0
    for n, o in S.meshes.items():
        if n.startswith(COLL_PREFIXES) or n in PLACEHOLDERS or n.startswith(RESERVED_PREFIXES):
            continue                                   # (collision meshes; the game's own hidden placeholders FLAG / HOLE_CUP / BALL_START / MARKER_*. A strip named
            #                                            WATER_FALL_xx or carrying a MAT_* material is judged like any other: only a short steep LK_FALL component is a sheet)
        um = S.used_mats(o)
        if not (um & wm):
            continue
        w = S.wld(o)
        L = S.loc(o)
        names = np.array(w["names"], object)[w["mi"]]
        alpha = L["alpha_tri"] if L.get("alpha_tri") is not None else np.ones(len(w["T"]))
        nz = w["N"][:, 2]
        sel = np.zeros(len(w["T"]), bool)
        for mname in um & wm:
            m_ = names == mname
            if mname == "LK_SURF":
                sel |= m_ & (alpha >= amin)
                # a PIECE that never reaches the visibility threshold (vertex alpha 0.29 everywhere) still draws as a pale band: it counts whole. The faded fringe
                # (alpha < amin) of a piece that does reach it does not.
                if (m_ & (alpha < amin)).any():
                    lab_ = tri_components(weld_ids(w["W"], 1e-3)[w["T"]][m_])
                    ai = alpha[m_]
                    ghost = np.zeros(len(ai), bool)
                    for c_ in np.unique(lab_).tolist():
                        if float(ai[lab_ == c_].max()) < amin:
                            ghost |= lab_ == c_
                    gi = np.flatnonzero(m_)[ghost]
                    sel[gi] = True
            elif mname == "LK_FALL":
                ex = sheets.get(n)
                ex = ex if ex is not None else np.zeros(len(w["T"]), bool)
                sel |= m_ & ~ex
                sel |= m_ & ex & (nz > 0.8)
            elif mname == "LK_SMOKE" and n.startswith("DRESS_SMOKE"):
                sel |= m_ & (nz > 0.5)
            else:
                sel |= m_
        sel &= w["A"] > 1e-12
        if not sel.any():
            continue
        V.append(w["W"])
        T.append(w["T"][sel] + off)
        A.append(w["A"][sel])
        own.extend([n] * int(sel.sum()))
        off += len(w["W"])
    if not V:
        out = (np.zeros((0, 3)), np.zeros((0, 3), np.int64), np.zeros(0), [])
    else:
        out = (np.vstack(V), np.vstack(T), np.concatenate(A), own)
    S.__dict__[key] = out
    return out


def white_reach_cover(S, C, step=1.0, reach=10.0):
    """Per TERRAIN shore sample: does foam-like geometry (any orientation) cross the vertical plane strip over its outward normal segment, 0..`reach` m? Exact
    3D triangle-triangle overlap of each strip with the foam triangles (BVH overlap), so a vertical wall, a sloped skirt and a flat strip all count; a strip laid
    2-8 m off the shore is as much a shoreline band as one at the cliff foot (the v2 review: with a 1 m reach a continuous band at 2.5 m passed).
    Returns (covered bool per sample, shore length, P, loop ids)."""
    P, N, _Tg, _s, total, lid = terrain_shore_frame(S, C, step)
    V, T, _A, _own = white_geometry(S, C)
    cov = np.zeros(len(P), bool)
    if len(T) == 0 or len(P) == 0:
        return cov, total, P, lid
    bvh = BVHTree.FromPolygons([tuple(v) for v in V.tolist()], [tuple(t) for t in T.tolist()], all_triangles=True)
    zlo, zhi = float(V[:, 2].min()) - 5.0, float(V[:, 2].max()) + 5.0
    qv, qt = [], []
    for (x, y), (nx, ny) in zip(P.tolist(), N.tolist()):
        b = len(qv)
        qv += [(x, y, zlo), (x + nx * reach, y + ny * reach, zlo), (x + nx * reach, y + ny * reach, zhi), (x, y, zhi)]
        qt += [(b, b + 1, b + 2), (b, b + 2, b + 3)]
    qb = BVHTree.FromPolygons(qv, qt, all_triangles=True)
    for qi, _ti in qb.overlap(bvh):
        cov[qi // 2] = True
    return cov, total, P, lid


def dilate_disc(mask, r):
    """Binary dilation of a 2D bool mask by a disc of r cells (numpy shifts)."""
    out = mask.copy()
    H, W = mask.shape
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if (dy or dx) and dy * dy + dx * dx <= r * r:
                ys0, ys1 = max(0, dy), min(H, H + dy)
                xs0, xs1 = max(0, dx), min(W, W + dx)
                out[ys0:ys1, xs0:xs1] |= mask[ys0 - dy:ys1 - dy, xs0 - dx:xs1 - dx]
    return out


def geodesic_length(mask_c):
    """Length (cells) of the longest shortest path inside a connected 2D bool mask (8-neighbour Dijkstra, diagonal = sqrt 2, two sweeps from an extreme)."""
    import heapq
    ys, xs = np.nonzero(mask_c)
    if len(ys) == 0:
        return 0.0
    H, W = mask_c.shape
    idx = {(int(y), int(x)) for y, x in zip(ys.tolist(), xs.tolist())}
    nb = [(-1, -1, 1.4142135), (-1, 0, 1.0), (-1, 1, 1.4142135), (0, -1, 1.0), (0, 1, 1.0), (1, -1, 1.4142135), (1, 0, 1.0), (1, 1, 1.4142135)]

    def sweep(src):
        dist = {src: 0.0}
        h = [(0.0, src)]
        while h:
            d, (y, x) = heapq.heappop(h)
            if d > dist.get((y, x), 1e18):
                continue
            for dy, dx, w in nb:
                q = (y + dy, x + dx)
                if q in idx and d + w < dist.get(q, 1e18):
                    dist[q] = d + w
                    heapq.heappush(h, (d + w, q))
        far = max(dist, key=dist.get)
        return far, dist[far]
    a = (int(ys[0]), int(xs[0]))
    b, _d = sweep(a)
    _c, d2 = sweep(b)
    return float(d2)


def white_union_runs(S, C, gap_m=8.0, cell=0.5, beyond=None):
    """Whitewater pieces merged when they lie closer than gap_m (raster at `cell`, dilated by gap_m / 2): the length (m) of every merged piece = geodesic length of
    the dilated footprint minus the dilation (gap_m). Independent of how the pieces are named or split (touching squares, a dashed ring) and of the width
    variation (a ragged band is still a band). `beyond` = (shore loops, reach m): only triangles farther than `reach` from every shore loop are merged here
    (nearer ones are the shore-normal runs / gaps, which measure along the shore: two patches on the two banks of a narrow strait are not one band).
    Returns [(length m, area m2, x, y)] largest first."""
    V, T, A, _own = white_geometry(S, C)
    if len(T) and beyond is not None and beyond[0]:
        cen = V[T].mean(axis=1)[:, :2]
        d = np.min(np.stack([dist_polyline(cen, L_[0], closed=True) for L_ in beyond[0]]), axis=0)
        T = T[d > beyond[1]]
    if len(T) == 0:
        return []
    V2 = V[:, :2]
    P = V2[np.unique(T)]
    span = float(max(np.ptp(P[:, 0]), np.ptp(P[:, 1])))
    cell = max(cell, span / 1800.0)                                    # bounded raster
    mask, x0, y0 = raster_mask(V2, T, cell, pad=int(math.ceil(gap_m / cell)) + 2, edges=True)
    r = max(1, int(round(gap_m / 2.0 / cell)))
    dil = dilate_disc(mask, r)
    lab, n = label_mask(dil)
    out = []
    for c in range(1, n + 1):
        mc = lab == c
        if not (mc & mask).any():
            continue
        L = max(0.0, geodesic_length(mc) * cell - 2.0 * r * cell)
        ys, xs = np.nonzero(mc & mask)
        out.append((L, float((mc & mask).sum()) * cell * cell, x0 + float(xs.mean()) * cell, y0 + float(ys.mean()) * cell))
    return sorted(out, reverse=True)


def g_no_foam_strip(S, C):
    """'Delete the white foam strips. A continuous white surf band is not a pass.': no MAT_FOAM / FOAM object; no DRESS_ object carrying LK_SURF / LK_FALL;
    on holes 8/9 the foam-like geometry of ANY ORIENTATION (LK_SURF with alpha, every LK_FALL except a short steep waterfall sheet, ANY white-ish material: a 63 degree
    skirt or a vertical wall is as visible from the air as a flat strip) reached by the shore normals within 10 m (exact 3D overlap) covers <= 25 % of the
    shoreline, in runs <= 10 m, >= 8 m apart; offshore (> 10 m from the shore) whitewater pieces closer than 8 m merge and no merged piece is longer than 11 m (10 m + raster
    tolerance); the white area (3D) is <= 1.0 m2 per metre of shoreline."""
    p = []
    m = mats_on(S)
    foam_m = [k for k in m if k.upper().startswith("MAT_FOAM")]
    foam_o = sorted(n for n in S.all if "FOAM" in n.upper())
    if foam_m or foam_o:
        p.append(f"MAT_FOAM {foam_m} / FOAM objects {short(foam_o)}")
    dress_white = sorted(n for n, o in S.meshes.items() if n.startswith("DRESS_") and (S.used_mats(o) & {"LK_SURF", "LK_FALL"}))
    if dress_white:
        p.append(f"white-water material on DRESS_ objects {short(dress_white)} (a foam strip dressed as scenery)")
    white = white_materials(S, C)
    loops = terrain_loops(S) if C.hole in (8, 9) else []
    follow = []
    if white and loops:
        for n, o in S.meshes.items():
            if not n.startswith("DRESS_") or n.startswith(OVERLAY_OK) or not (S.used_mats(o) & white):
                continue
            Wp = S.wld(o)["W"][:, :2]
            d = np.min(np.stack([dist_polyline(Wp, L_[0], closed=True) for L_ in loops]), axis=0)
            if float((d <= 6.0).mean()) >= 0.30:
                follow.append(f"{n} ({sorted(S.used_mats(o) & white)})")
    if follow:
        p.append(f"white-ish DRESS_ objects follow the shore (>= 30 % of their vertices within 6 m of it): {short(follow, 4)}")
    step = 1.0
    V, T, A, _own = white_geometry(S, C)
    summary = "no whitewater geometry"
    if C.hole in (8, 9):
        cov, total, _P, lid = white_reach_cover(S, C, step, TH2["foam_reach"])
        runs, gaps = loop_runs(cov, lid, step)
        frac = float(cov.mean()) if len(cov) else 0.0
        longest = max(runs, default=0) * step
        mingap = min(gaps, default=10 ** 6) * step if len(runs) >= 2 and gaps else None
        if frac > TH2["foam_cov"]:
            p.append(f"whitewater lies along the shore normals (within {TH2['foam_reach']:g} m) of {frac:.0%} of the {total:.0f} m shoreline (need <= {TH2['foam_cov']:.0%})")
        if longest > TH2["foam_run"]:
            p.append(f"longest unbroken surf run {longest:.0f} m (need <= {TH2['foam_run']:g} m)")
        if mingap is not None and mingap < TH2["foam_gap"]:
            p.append(f"gap between two surf runs only {mingap:.0f} m (need >= {TH2['foam_gap']:g} m)")
        area = float(A.sum())
        apm = area / max(total, 1.0)
        if apm > TH2["foam_area_m"]:
            p.append(f"{area:.0f} m2 of whitewater on {total:.0f} m of shore = {apm:.2f} m2 per metre (need <= {TH2['foam_area_m']:g})")
        summary = (f"whitewater (within {TH2['foam_reach']:g} m of the shore normal) covers {frac:.0%} of {total:.0f} m of shore in {len(runs)} runs (longest {longest:.0f} m, min gap "
                   f"{('%.0f m' % mingap) if mingap is not None else 'n/a'}), {apm:.2f} m2 per metre")
    pieces = white_union_runs(S, C, TH2["foam_gap"], beyond=(loops, TH2["foam_reach"])) if len(T) else []
    if pieces:
        lim = TH2["foam_run"] + 1.0
        longp = [q for q in pieces if q[0] > lim]
        if longp:
            p.append(f"{len(longp)} merged OFFSHORE whitewater pieces (> {TH2['foam_reach']:g} m from the shore; pieces closer than {TH2['foam_gap']:g} m merge) longer than {lim:g} m: "
                     + ", ".join(f"{q[0]:.0f} m ({q[1]:.0f} m2) near ({q[2]:.0f},{q[3]:.0f})" for q in longp[:3]))
        summary += f"; {len(pieces)} merged offshore pieces, longest {pieces[0][0]:.1f} m"
    return not p, ("; ".join(p) if p else summary)


def g_surf(S, C):
    """SURF_SHORE (v2): the whitewater that is allowed is present and well formed: >= 8 separate WATER_SURF pieces, LK_SURF only, a vertex
    colour whose alpha ramps (range >= .5), and at least a little of the shore touched by it (the CAP lives in NO_FOAM_STRIP)."""
    surf = {n: o for n, o in S.meshes.items() if n.startswith("WATER_SURF")}
    problems = []
    pieces = 0
    for o in surf.values():
        L = S.loc(o)
        pieces += len(np.unique(tri_components(L["T"]))) if L["ntri"] else 0
    if pieces < TH["surf_pieces"]:
        problems.append(f"{pieces} WATER_SURF pieces (separate mesh islands over {len(surf)} objects; need >= {TH['surf_pieces']})")
    nocol, flat = [], []
    for n, o in surf.items():
        L = S.loc(o)
        if not L["cols"]:
            nocol.append(n)
        elif L["alpha"] is not None and float(np.ptp(L["alpha"])) < 0.5:
            flat.append(n)
        if S.used_mats(o) != {"LK_SURF"}:
            problems.append(f"{n} uses {sorted(S.used_mats(o))}")
    if nocol:
        problems.append(f"no vertex colour (A = shoreline closeness) on {short(nocol, 5)}")
    if flat:
        problems.append(f"vertex alpha range < 0.5 on {short(flat, 5)}")
    cov, total, _P, _lid = surf_cover_mask(S, C, 2.0)
    frac = float(cov.mean()) if len(cov) else 0.0
    if frac < TH2["surf_min_cov"]:
        problems.append(f"surf touches only {frac:.0%} of the shore (need >= {TH2['surf_min_cov']:.0%}: breaking waves at the cliff bases)")
    return not problems, ("; ".join(problems) if problems else
                          f"{pieces} WATER_SURF pieces in {len(surf)} objects (LK_SURF, vertex alpha ramps), {frac:.0%} of the shore touched "
                          f"(range {TH2['surf_min_cov']:.0%}..{TH2['foam_cov']:.0%}; the upper cap is NO_FOAM_STRIP)")


def shelf_widths(S, C, step=2.0, reach=40.0):
    """Visible shelf width beyond the cliff skin at every shore sample: (shelf outer edge distance - skin outer offset), metres."""
    shelves = [o for n, o in S.meshes.items() if n.startswith("WATER_SHELF") or "LK_WATER_SHALLOW" in S.used_mats(o)]     # every shelf-like mesh, any name
    P, N, Tg, s, total, _lid = terrain_shore_frame(S, C, step)
    if not shelves:
        return np.zeros(len(P)), np.zeros(len(P)), total, False
    idx = TriIndex(S, shelves, up_only=True)
    skin = [o for n, o in S.meshes.items() if n.startswith("ROCK_SKIN_")]
    SV = np.vstack([S.wld(o)["W"] for o in skin]) if skin else np.zeros((0, 3))
    kd = None
    if len(SV):
        from mathutils.kdtree import KDTree
        kd = KDTree(len(SV))
        for i, v in enumerate(SV.tolist()):
            kd.insert(v, i)
        kd.balance()
    dout = np.zeros(len(P))
    dskin = np.zeros(len(P))
    ts = np.arange(0.0, reach, 0.5)
    for i, ((x, y), (nx, ny), (tx_, ty_)) in enumerate(zip(P.tolist(), N.tolist(), Tg.tolist())):
        hit = np.zeros(len(ts), bool)
        for k, t in enumerate(ts.tolist()):
            hit[k] = idx.hit_down(x + nx * t, y + ny * t, 3.0) is not None
        if hit.any():
            first = int(np.flatnonzero(hit)[0])
            last = first
            for k in range(first, len(ts)):
                if hit[k]:
                    last = k
                elif k - last > 4:
                    break
            dout[i] = float(ts[last])
        if kd is not None:
            best = 0.0
            for co, _j, d in kd.find_range((x, y, 3.0), 8.0):
                dx, dy = co[0] - x, co[1] - y
                off, lat = dx * nx + dy * ny, dx * tx_ + dy * ty_
                if abs(lat) <= 2.0 and 0.0 < off < 8.0:
                    best = max(best, off)
            dskin[i] = best
    return np.maximum(dout - dskin, 0.0), dout, total, True


def g_shelf_thin(S, C):
    """'Narrow the shelf until it is a thin edge, not a halo': WATER_SHELF (and any other mesh with LK_WATER_SHALLOW) width visible beyond the cliff
    skin, measured from the mesh along the shore normal every 2 m: median <= 2.5 m, p95 <= 4 m, and not a closed uniform ring."""
    if C.hole == 10:
        return True, "not applicable: the lava hole has no WATER_SHELF"
    if "WATER_SHELF" not in S.meshes:
        return False, "WATER_SHELF missing"
    extra = sorted(n for n, o in S.meshes.items() if n != "WATER_SHELF" and (n.startswith("WATER_SHELF") or "LK_WATER_SHALLOW" in S.used_mats(o)))
    w, dout, total, _ok = shelf_widths(S, C, 2.0)
    med, p95 = float(np.median(w)), float(np.percentile(w, 95))
    vis = w > 0.5
    cover = float(vis.mean())
    cv = float(w[vis].std() / max(w[vis].mean(), 1e-9)) if vis.sum() > 3 else 1.0
    p = []
    if med > TH2["shelf_med"]:
        p.append(f"median visible shelf width {med:.1f} m (need <= {TH2['shelf_med']})")
    if p95 > TH2["shelf_p95"]:
        p.append(f"p95 visible shelf width {p95:.1f} m (need <= {TH2['shelf_p95']})")
    if cover >= TH2["ring_cover"] and cv < TH2["ring_cv"]:
        p.append(f"a closed uniform ring: visible along {cover:.0%} of the shore with width cv {cv:.2f} (< {TH2['ring_cv']})")
    return not p, ("; ".join(p) if p else
                   f"visible shelf beyond the skin: median {med:.1f} m, p95 {p95:.1f} m (max {float(w.max()):.1f}), present along {cover:.0%} of the {total:.0f} m shore, width cv {cv:.2f}"
                   + (f" (union with {extra})" if extra else ""))


def lava_ellipse_m(C):
    lv = C.waters[0]
    return lv["x"] * YD, lv["d"] * YD, lv["width"] / 2 * YD, lv["length"] / 2 * YD


def g_lava_mesh(S, C):
    o = S.meshes.get("WATER_LAVA")
    if o is None:
        return False, "WATER_LAVA missing"
    L = S.loc(o)
    w = S.wld(o)
    problems = []
    if L["ntri"] <= TH["lava_tris"]:
        problems.append(f"{L['ntri']} triangles (need > {TH['lava_tris']}: a subdivided crust, not one polygon)")
    um = S.used_mats(o)
    if um != {"LK_LAVA"}:
        problems.append(f"materials {sorted(um)} (want LK_LAVA)")
    zmax = float(w["W"][:, 2].max())
    if zmax > C.pz - TH["lava_drop"]:
        problems.append(f"top z {zmax:.2f} m is not below the play band (need <= PLAY_Z - {TH['lava_drop']} = {C.pz - TH['lava_drop']:.1f})")
    cx, cy, ax, ay = lava_ellipse_m(C)
    lo, hi = w["W"].min(0), w["W"].max(0)
    if not (lo[0] <= cx - ax and hi[0] >= cx + ax and lo[1] <= cy - ay and hi[1] >= cy + ay):
        problems.append("does not cover the scoring lava ellipse")
    return not problems, ("; ".join(problems) if problems else
                          f"WATER_LAVA {L['ntri']} triangles, LK_LAVA only, z {float(w['W'][:, 2].min()):.2f}..{zmax:.2f} m "
                          f"(<= {C.pz - TH['lava_drop']:.1f}), covers the lava ellipse")


def g_lava_lights(S, C):
    L = {n: o for n, o in S.all.items() if re.match(r"^LAVA_LIGHT_\d+$", n)}
    lo_n, hi_n = TH["lava_lights"]
    problems = []
    if not (lo_n <= len(L) <= hi_n):
        problems.append(f"{len(L)} LAVA_LIGHT_nn (need {lo_n}..{hi_n})")
    lava = S.meshes.get("WATER_LAVA")
    ztop = float(S.wld(lava)["W"][:, 2].max()) if lava is not None else 0.0
    cr = C.sc.get("crater", {})
    ccx, ccy = cr.get("cx", 0.0) * YD, cr.get("cd", 0.0) * YD
    rr = cr.get("rim_radius", 200.0) * YD + 20.0
    for n, o in L.items():
        p = S.pos(o)
        if o.type != "EMPTY":
            problems.append(f"{n} is a {o.type} (want EMPTY)")
        if not (ztop + 0.2 < p[2] <= C.pz + 15.0):
            problems.append(f"{n} z {p[2]:.1f} not in ({ztop + 0.2:.1f}, {C.pz + 15:.1f}]")
        if math.hypot(p[0] - ccx, p[1] - ccy) > rr:
            problems.append(f"{n} is {math.hypot(p[0] - ccx, p[1] - ccy):.0f} m from the crater centre (> {rr:.0f})")
    return not problems, ("; ".join(problems) if problems else
                          f"{len(L)} LAVA_LIGHT_nn empties above the lava (z {', '.join(f'{S.pos(o)[2]:.1f}' for o in L.values())}) inside the crater")


# ----------------------------------------------------------------------------- hole 8 Needle
def objs_over_land(S, C, objs):
    out = []
    for n, o in objs.items():
        p = S.centre(o)
        hit, z = on_land(S, C, float(p[0]), float(p[1]))
        if hit is not None and C.pz - 0.05 <= z <= C.pz + 0.65:
            out.append(n)
    return out


def g_needle_path(S, C):
    paths = {n: o for n, o in S.meshes.items() if n.startswith("DRESS_PATH")}
    if not paths:
        return False, "no DRESS_PATH_* mesh"
    problems = []
    area = 0.0
    cen, zs = [], []
    for n, o in paths.items():
        w = S.wld(o)
        names = np.array(w["names"], object)[w["mi"]]
        up = w["N"][:, 2] > 0.5
        area += float(w["A"][up & (names == "LK_PATH")].sum())
        cen.append(w["W"][w["T"][up]].mean(axis=1))
        zs.append(w["W"][:, 2])
    cen = np.vstack(cen) if cen else np.zeros((0, 3))
    z = np.concatenate(zs)
    if area < 30.0:
        problems.append(f"LK_PATH top area {area:.1f} m2 (need >= 30)")
    ext = float(np.hypot(*np.ptp(cen[:, :2], axis=0))) if len(cen) else 0.0
    if ext < 15.0:
        problems.append(f"path extent {ext:.1f} m (need >= 15)")
    if z.min() < C.pz - 0.05 or z.max() > C.pz + 0.6:
        problems.append(f"path z {z.min():.2f}..{z.max():.2f} (must sit on the pad: {C.pz - 0.05:.2f}..{C.pz + 0.6:.2f})")
    land = 0
    for x, y, _ in cen.tolist():
        hit, hz = on_land(S, C, x, y)
        land += hit is not None
    frac = land / max(1, len(cen))
    if frac < 0.95:
        problems.append(f"only {frac:.0%} of the path faces are over land (need 95%)")
    return not problems, ("; ".join(problems) if problems else
                          f"{len(paths)} DRESS_PATH_* meshes: {area:.0f} m2 of LK_PATH, {ext:.0f} m long, z {z.min():.2f}..{z.max():.2f}, "
                          f"{frac:.0%} over land (visual overlay, no collider)")


def atlas_pixels(C):
    p = os.path.join(C.look_dir, "Plants_C.png")
    if not os.path.isfile(p):
        return None
    return image_stats(p)["px"]


def purple_fraction(L, px):
    if L["uv"] is None or px is None or L["ntri"] == 0:
        return 0.0
    h, w = px.shape[:2]
    c = L["uv"].mean(axis=1)
    xs = np.clip((c[:, 0] % 1.0) * w, 0, w - 1).astype(int)
    ys = np.clip((c[:, 1] % 1.0) * h, 0, h - 1).astype(int)
    rgb = px[ys, xs, :3]
    pur = np.array([is_purple(v) for v in rgb.tolist()])
    return float(L["A"][pur].sum() / max(L["A"].sum(), 1e-12))


def g_needle_flowers(S, C):
    px = atlas_pixels(C)
    flowers = {n: o for n, o in S.meshes.items() if n.startswith("PLANT_FLOWER")}
    shrubs = {n: o for n, o in S.meshes.items() if n.startswith(("PLANT_SHRUB", "PLANT_BUSH"))}
    tufts = {n: o for n, o in S.meshes.items() if is_tuft(n)}
    problems = []
    if px is None:
        problems.append("Plants_C.png missing: cannot prove the flowers are purple")
    pf = {}
    for o in flowers.values():
        if o.data.name not in pf:
            pf[o.data.name] = purple_fraction(S.loc(o), px)
    purple = {n: o for n, o in flowers.items() if pf.get(o.data.name, 0) >= 0.10}
    land_p = objs_over_land(S, C, purple)
    land_s = objs_over_land(S, C, shrubs)
    land_t = objs_over_land(S, C, tufts)
    if len(land_p) < TH["flowers"]:
        problems.append(f"{len(land_p)} purple PLANT_FLOWER_* on land (need >= {TH['flowers']}; {len(flowers)} flowers, purple share per mesh "
                        f"{ {k: round(v, 2) for k, v in pf.items()} })")
    if len(land_s) < TH["shrubs"]:
        problems.append(f"{len(land_s)} PLANT_SHRUB_*/PLANT_BUSH_* on land (need >= {TH['shrubs']})")
    if len(land_t) < TH["tufts8"]:
        problems.append(f"{len(land_t)} grass tufts PLANT_TUFT_*/PLANT_GRASS_* on land (need >= {TH['tufts8']})")
    return not problems, ("; ".join(problems) if problems else
                          f"{len(land_p)} purple flowers (purple share {', '.join(f'{v:.0%}' for v in pf.values())}), {len(land_s)} shrubs, "
                          f"{len(land_t)} tufts on the pads")


def masonry_blocks(S, objs):
    blocks = 0
    seen = {}
    for n, o in objs.items():
        k = S.key(o)
        if k not in seen:
            L = S.loc(o)
            mk = np.array([L["names"][i] == "LK_MASONRY" for i in L["mi"].tolist()], bool) if L["ntri"] else np.zeros(0, bool)
            if not mk.any():
                seen[k] = 0
            else:
                lab = tri_components(L["T"][mk])
                u, cnt = np.unique(lab, return_counts=True)
                seen[k] = int((cnt >= 8).sum())
        blocks += seen[k]
    return blocks


def union_bbox(S, objs):
    lo = np.full(3, np.inf)
    hi = np.full(3, -np.inf)
    for o in objs.values():
        a, b = S.wbbox(o)
        lo = np.minimum(lo, a)
        hi = np.maximum(hi, b)
    return lo, hi


# ----------------------------------------------------------------------------- v2: crater wall cover, UV anisotropy, arch opening, fake green, shaders
def crater_viewpoints(S, C):
    """Aerial / low / oblique camera positions for the crater wall test (metres)."""
    cr = C.sc.get("crater", {})
    cx, cy = cr.get("cx", 0.0) * YD, cr.get("cd", 0.0) * YD
    rim = cr.get("rim_radius", 150.0) * YD
    tee = S.pos(S.all["MARKER_TEE"]) if "MARKER_TEE" in S.all else np.array([0.0, 0.0, C.pz])
    pin = S.pos(S.all["MARKER_PIN"]) if "MARKER_PIN" in S.all else np.array([cx, cy, C.pz])
    vps = []
    for dx, dy in ((0, 0), (40, 0), (-40, 0), (0, 40), (0, -40)):             # aerial: above the bowl
        vps.append(("aerial", (cx + dx, cy + dy, C.pz + 60.0)))
    for t in (0.0, 0.25, 0.5, 0.75, 1.0):                                       # low: the ball camera along the line of play
        vps.append(("low", (tee[0] + (pin[0] - tee[0]) * t, tee[1] + (pin[1] - tee[1]) * t, C.pz + 2.2)))
    for k in range(8):                                                            # oblique: around the rim, 25 m up
        a = 2 * math.pi * k / 8
        vps.append(("oblique", (cx + 0.6 * rim * math.cos(a), cy + 0.6 * rim * math.sin(a), C.pz + 25.0)))
    return vps


def g_crater_wall_cover(S, C):
    """'The crater wall still shows a band of smooth grass texture at the top. Cover it.': rays from aerial / low / oblique viewpoints at EVERY terrain
    wall face below PLAY_Z, sampled on a 4 m2 lattice (up to 3 viewpoints that face it); of the rays whose first (front-facing) hit is a surface below the play height,
    <= 1 % may hit a smooth TERRAIN wall face or a vertical LK_ROUGH face (the skin / columns must be what the camera sees), AND no connected patch of
    exposed wall faces (face centres within 4 m of each other) may exceed 12 m2 (a gap in the cladding hides inside a global 1 %)."""
    terr = [o for n, o in S.meshes.items() if n.startswith("TERRAIN")]
    if not terr:
        return False, "no TERRAIN mesh"
    vis = [o for n, o in S.meshes.items() if not n.startswith(("WATER_", "LAVA_", "PLANT_", "DRESS_SMOKE"))]
    idx = TriIndex(S, vis)
    if idx.bvh is None:
        return False, "no geometry"
    tp, tn, ta = [], [], []
    for o in terr:
        w = S.wld(o)
        up = w["N"][:, 2] > 0.5
        cen = w["W"][w["T"]].mean(axis=1)
        sel = ~up & (cen[:, 2] < C.pz - 0.02) & (cen[:, 2] > -6.5) & (w["A"] > 1e-9)
        # sample every wall triangle on a ~4 m2 lattice (R2 low-discrepancy points), not only at its centroid: a big triangle whose centroid hides behind the
        # cladding must not hide a bare corner (the v2 review: a 24 m wide hole in the cladding passed)
        for t, nrm_, area_ in zip(w["T"][sel].tolist(), w["N"][sel].tolist(), w["A"][sel].tolist()):
            a_, b_, c_ = w["W"][t[0]], w["W"][t[1]], w["W"][t[2]]
            k = int(min(64, max(1, math.ceil(area_ / TH2["wall_sample_m2"]))))
            for j in range(k):
                u, v = (0.5 + j * 0.7548776662) % 1.0, (0.5 + j * 0.5698402910) % 1.0
                if k == 1:
                    u, v = 1.0 / 3.0, 1.0 / 3.0
                elif u + v > 1.0:
                    u, v = 1.0 - u, 1.0 - v
                q = a_ + u * (b_ - a_) + v * (c_ - a_)
                if q[2] < C.pz - 0.02:
                    tp.append(q[None, :])
                    tn.append(np.array([nrm_]))
                    ta.append(np.array([area_ / k]))
    T_pts = np.vstack(tp) if tp else np.zeros((0, 3))
    T_nrm = np.vstack(tn) if tn else np.zeros((0, 3))
    T_area = np.concatenate(ta) if ta else np.zeros(0)
    if len(T_pts) == 0:
        return False, "no terrain wall faces below PLAY_Z"
    vps = crater_viewpoints(S, C)
    n_vis = n_exp = 0
    by_kind = {k: [0, 0] for k in ("aerial", "low", "oblique")}
    where = {}
    exposed_t = set()
    VP = np.array([vp for _k, vp in vps], float)
    for ti, (p, nr) in enumerate(zip(T_pts.tolist(), T_nrm.tolist())):
        pv = np.array(p)
        D = pv[None, :] - VP
        dist_all = np.linalg.norm(D, axis=1)
        facing = (D @ np.array(nr)) < 0.0
        order = [i for i in np.argsort(dist_all).tolist() if facing[i]][:3]
        for i in order:
            kind, vp = vps[i]
            dist = float(dist_all[i])
            d = D[i] / dist
            r = idx.first_front(vp, d.tolist(), dist + 25.0)
            if r is None:
                continue
            hz = r[2].z
            if hz >= C.pz - 0.02:
                continue                                          # the camera sees a top surface / lip here, not wall
            n_vis += 1
            by_kind[kind][0] += 1
            own, mat, nz = idx.own[r[1]], idx.matn[r[1]], idx.nz[r[1]]
            exposed = (own.startswith("TERRAIN") and nz < 0.5) or (mat == "LK_ROUGH" and abs(nz) < 0.5)
            if exposed:
                n_exp += 1
                by_kind[kind][1] += 1
                key = f"{own}/{mat}"
                where[key] = where.get(key, 0) + 1
                exposed_t.add(ti)
    frac = n_exp / max(n_vis, 1)
    # connected patches of exposed wall faces
    patch_max, npatch = 0.0, 0
    if exposed_t:
        ids = sorted(exposed_t)
        P2 = T_pts[ids]
        par = list(range(len(ids)))

        def find(a):
            while par[a] != a:
                par[a] = par[par[a]]
                a = par[a]
            return a
        for i in range(len(ids)):
            d = np.linalg.norm(P2[i + 1:] - P2[i], axis=1)
            for j in (np.flatnonzero(d <= TH2["wall_link"]) + i + 1).tolist():
                par[find(i)] = find(j)
        tot = {}
        for k, t in enumerate(ids):
            tot[find(k)] = tot.get(find(k), 0.0) + float(T_area[t])
        patch_max, npatch = max(tot.values()), len(tot)
    ok = n_vis >= 200 and frac <= TH2["wall_exposed"] and patch_max <= TH2["wall_patch_m2"]
    return ok, (f"{n_exp} of {n_vis} camera rays (all {len(T_pts)} wall faces) that hit the wall below PLAY_Z see exposed smooth terrain / vertical grass = {frac:.1%} (need <= {TH2['wall_exposed']:.0%}); "
                f"largest connected exposed patch {patch_max:.0f} m2 of {npatch} (need <= {TH2['wall_patch_m2']:g} m2); "
                f"by view {{{', '.join(f'{k} {v[1]}/{v[0]}' for k, v in by_kind.items())}}}" + (f"; exposed: {where}" if where else "")
                + ("" if n_vis >= 200 else f"; only {n_vis} wall rays (need >= 200)"))


def uv_anisotropy(L, mask):
    """Per-triangle UV Jacobian singular value ratio of the masked triangles: area fraction above 4:1."""
    V, T, uv = L["V"], L["T"][mask], L["uv"][mask]
    a, b, c = V[T[:, 0]], V[T[:, 1]], V[T[:, 2]]
    e1, e2 = b - a, c - a
    n1 = np.linalg.norm(e1, axis=1)
    ok = n1 > 1e-9
    x = e1 / np.maximum(n1, 1e-12)[:, None]
    nrm = np.cross(e1, e2)
    ln = np.linalg.norm(nrm, axis=1)
    ok &= ln > 1e-12
    y = np.cross(nrm / np.maximum(ln, 1e-12)[:, None], x)
    P = np.stack([np.stack([(e1 * x).sum(1), (e1 * y).sum(1)], 1), np.stack([(e2 * x).sum(1), (e2 * y).sum(1)], 1)], 1)   # (n, 2 edges, 2 coords)
    Uv = np.stack([uv[:, 1] - uv[:, 0], uv[:, 2] - uv[:, 0]], 1)                                                             # (n, 2 edges, 2 uv)
    det = P[:, 0, 0] * P[:, 1, 1] - P[:, 0, 1] * P[:, 1, 0]
    ok &= np.abs(det) > 1e-14
    if not ok.any():
        return 1.0, 0.0
    Pi = np.linalg.inv(P[ok])
    J = np.matmul(Pi, Uv[ok])                                      # U = P J: J maps plane coords to uv
    sv = np.linalg.svd(J, compute_uv=False)
    ratio = sv[:, 0] / np.maximum(sv[:, 1], 1e-12)
    ar = L["A"][mask][ok]
    return float(np.average(np.minimum(ratio, 1e3), weights=ar)), float(ar[ratio > TH2["uv_aniso"]].sum() / ar.sum())


def g_needle_arch(S, C):
    arch = {n: o for n, o in S.meshes.items() if n.startswith("DRESS_ARCH")}
    if not arch:
        return False, "no DRESS_ARCH_* mesh (still ROCK_LARGE icosahedra?)"
    problems = []
    blocks = masonry_blocks(S, arch)
    if blocks < TH["arch_blocks"]:
        problems.append(f"{blocks} LK_MASONRY blocks (need >= {TH['arch_blocks']})")
    lo, hi = union_bbox(S, arch)
    a = C.sc.get("arch", {})
    if a:
        ax, ay = a["x"] * YD, a["d"] * YD
        c = (lo + hi) / 2
        d = math.hypot(c[0] - ax, c[1] - ay)
        if d > 15.0:
            problems.append(f"arch centre is {d:.1f} m from SCENERY arch ({ax:.1f}, {ay:.1f}) (<= 15)")
    if hi[2] - lo[2] < 6.0:
        problems.append(f"arch only {hi[2] - lo[2]:.1f} m tall (need >= 6)")
    vines = {n: o for n, o in S.all.items() if n.startswith("PLANT_VINE")}
    on = []
    for n, o in vines.items():
        vlo, vhi = S.wbbox(o)
        if (vhi >= lo - 1.5).all() and (vlo <= hi + 1.5).all():
            on.append(n)
    if len(on) < TH["arch_vines"]:
        problems.append(f"{len(on)} PLANT_VINE_* on the arch (bbox within 1.5 m; need >= {TH['arch_vines']})")
    base = []
    for n, o in S.meshes.items():
        if not n.startswith("ROCK_") or n.startswith("ROCK_SKIN_"):      # cliff cladding is the pad edge, not "its own rock"
            continue
        rlo, rhi = S.wbbox(o)
        overlap = rlo[0] <= hi[0] and rhi[0] >= lo[0] and rlo[1] <= hi[1] and rhi[1] >= lo[1]
        if overlap and lo[2] - 1.5 <= rhi[2] <= lo[2] + 1.5:
            base.append(n)
    if not base:
        problems.append(f"no ROCK_* pedestal under the arch (plan overlap, top within 1.5 m of the arch foot z {lo[2]:.1f})")
    op = arch_opening(S, arch)
    if not op[0]:
        problems.append(f"no opening: {op[1]}")
    return not problems, ("; ".join(problems) if problems else
                          f"{len(arch)} DRESS_ARCH_* objects: {blocks} masonry blocks, {hi[2] - lo[2]:.1f} m tall, on rock {base[:2]}, "
                          f"{len(on)} vines on it, {op[1]}")


def arch_opening(S, arch):
    """An ARCH has a through-opening: horizontal rays through the thickness of the masonry, over a (span, height) grid of 0.25 m, find >= 1.5 m
    of clear span x 2 m of clear height with masonry above it (the crown) and on both sides (the piers)."""
    idx = TriIndex(S, list(arch.values()))
    if idx.bvh is None:
        return False, "no arch triangles"
    P = idx.V
    c = P[:, :2].mean(0)
    ev, evec = np.linalg.eigh(np.cov((P[:, :2] - c).T))
    best = None
    for span_axis in (evec[:, 1], evec[:, 0]):                        # the long horizontal axis = the span, the other = thickness
        t_axis = np.array([-span_axis[1], span_axis[0]])
        u = (P[:, :2] - c) @ span_axis
        v = (P[:, :2] - c) @ t_axis
        z0, z1 = float(P[:, 2].min()), float(P[:, 2].max())
        us = np.arange(float(u.min()) + 0.125, float(u.max()), 0.25)
        zs = np.arange(z0 + 0.125, z1, 0.25)
        if len(us) < 8 or len(zs) < 8:
            continue
        free = np.zeros((len(us), len(zs)), bool)
        r0, r1 = float(v.min()) - 2.0, float(v.max()) + 2.0
        for i, uu in enumerate(us.tolist()):
            for j, zz in enumerate(zs.tolist()):
                o = c + span_axis * uu + t_axis * r0
                d = t_axis
                hit = idx.bvh.ray_cast(Vector((o[0], o[1], zz)), Vector((d[0], d[1], 0.0)), r1 - r0)
                free[i, j] = hit[0] is None
        need_w, need_h = 6, 8
        ok = False
        zb = int(0.3 / 0.25)
        for i in range(len(us) - need_w):
            col = free[i:i + need_w, zb + 1:zb + 1 + need_h].all()
            if not col:
                continue
            above = (~free[i:i + need_w, zb + 1 + need_h:]).any(axis=1).all() if zb + 1 + need_h < len(zs) else False
            left = (~free[max(0, i - 16):i, zb + 1:zb + 1 + need_h]).any() if i > 0 else False
            right = (~free[i + need_w:i + need_w + 16, zb + 1:zb + 1 + need_h]).any() if i + need_w < len(us) else False
            if above and left and right:
                ok = True
                break
        if ok:
            return True, f"through-opening >= {need_w * 0.25:g} m wide x {need_h * 0.25:g} m high with crown and piers"
        best = best or "every ray line through the thickness hits masonry or no crown / piers frame an opening"
    return False, best or "arch too small to test"


SWAY_CLOCK_FILES = ("GolfPlants.shader", "GolfWindSway.cs")       # v2 2026-10-05 (area U): the ONLY files that may read the clock for visuals


def g_shaders_static(S=None, C=None, files=None):
    """File gate, 'No animated lava, no flowing shaders': the golf surface / lava shaders and the golf look kit (GolfLook, GolfAtmosphere = the sky, HoleView) contain no time input and no texture
    scrolling (the ocean's own TennisWater swell is the tennis shader, untouched). v2 2026-10-05: the plant sway (user: "the gentle sway of the plants with the wind") is the ONE exception: only
    GolfPlants.shader and GolfWindSway.cs may read the clock, and only they may touch the _GolfWind global; lava, surf, fall, sky, water, ground and rocks stay static."""
    cdir = os.path.join(REPO, "Unity", "Assets", "Scripts", "Course")
    real = files is None
    if real:
        sdir = os.path.join(COURSE_DIR, "Shaders")
        files = sorted(os.path.join(sdir, f) for f in os.listdir(sdir) if f.endswith(".shader")) if os.path.isdir(sdir) else []
        files += [os.path.join(cdir, "GolfLook.cs"), os.path.join(cdir, "GolfAtmosphere.cs"), os.path.join(cdir, "HoleView.cs"), os.path.join(cdir, "GolfWindSway.cs")]
    pat = re.compile(r"_Time\b|Time\.time|Time\.deltaTime|Time\.unscaledTime|SetTextureOffset|mainTextureOffset|_ScrollSpeed|DOTween|Update\s*\(")
    wind = re.compile(r"_GolfWind\b")
    bad, seen, allowed_seen = [], [], []
    for f in files:
        if not os.path.isfile(f):
            continue
        base = os.path.basename(f)
        allowed = base in SWAY_CLOCK_FILES
        (allowed_seen if allowed else seen).append(base)
        with open(f, errors="ignore") as fh:
            for i, ln in enumerate(fh, 1):
                code = ln.split("//")[0]
                if allowed:
                    continue
                if pat.search(code):
                    bad.append(f"{base}:{i} {code.strip()[:70]}")
                if wind.search(code):
                    bad.append(f"{base}:{i} touches _GolfWind (only GolfPlants.shader / GolfWindSway.cs may) {code.strip()[:50]}")
    if not seen:
        return False, "GolfSurf.shader and GolfLook.cs not found"
    if real:
        shp = os.path.join(COURSE_DIR, "Shaders", "GolfPlants.shader")
        if os.path.isfile(shp):
            with open(shp, errors="ignore") as fh:
                txt = fh.read()
            if "_GolfWind" not in txt:
                bad.append("GolfPlants.shader does not read _GolfWind (no sway data source)")
        if not os.path.isfile(SWAY_CS):
            bad.append("GolfWindSway.cs missing")
    return not bad, (f"{', '.join(seen)} contain no _Time / Time.time / texture offset / Update and no _GolfWind; only {', '.join(allowed_seen) or 'GolfPlants.shader / GolfWindSway.cs'} may read the clock (plants only)" if not bad else "animation in golf look files: " + " | ".join(bad[:4]))


def g_needle_wall_vines(S, C):
    vines = {n: o for n, o in S.all.items() if n.startswith("PLANT_VINE")}
    hang = []
    for n, o in vines.items():
        lo, hi = S.wbbox(o)
        c = (lo + hi) / 2
        d = float(dist_polyline(np.array([[c[0], c[1]]]), np.vstack([C.shore_m, C.shore_m[:1]]))[0])
        if d <= 4.0 and C.pz - 2.0 <= hi[2] <= C.pz + 1.5 and lo[2] < C.pz - 0.5:
            hang.append(n)
    ok = len(hang) >= TH["wall_vines8"]
    return ok, f"{len(hang)} PLANT_VINE_* hanging on the terrain walls (within 4 m of the shore, top near PLAY_Z; need >= {TH['wall_vines8']})"


def g_needle_waterfall(S, C):
    falls = {n: o for n, o in S.meshes.items() if n.startswith("WATER_FALL")}
    if not falls:
        return False, "no WATER_FALL_* mesh"
    problems = []
    sheet_area, zs, vz = 0.0, [], []
    plat, total = 0.0, 0.0
    for n, o in falls.items():
        w = S.wld(o)
        L = S.loc(o)
        sh = np.abs(w["N"][:, 2]) <= 0.6
        sheet_area += float(w["A"][sh].sum())
        total += float(w["A"].sum())
        plat += float(w["A"][(w["N"][:, 2] > 0.5) & (w["W"][w["T"]][:, :, 2].mean(axis=1) > 1.0)].sum())
        if sh.any():
            zs.append(w["W"][w["T"][sh]].reshape(-1, 3))
            if L["uv"] is not None:
                vz.append(np.stack([L["uv"][sh][:, :, 1].ravel(), w["W"][w["T"][sh]][:, :, 2].ravel()], 1))
    if sheet_area < 8.0:
        problems.append(f"near-vertical sheet area {sheet_area:.1f} m2 (need >= 8)")
    if total > 0 and plat / total > 0.20:
        problems.append(f"{plat:.1f} m2 = {plat / total:.0%} of the waterfall faces up above z 1 m (a flat platform, not a falling sheet; <= 20%)")
    foot = None
    if zs:
        P = np.vstack(zs)
        top, bot = float(P[:, 2].max()), float(P[:, 2].min())
        if top < C.pz - 1.5 or bot > 1.5:
            problems.append(f"sheet spans z {bot:.1f}..{top:.1f} (need from >= {C.pz - 1.5:.1f} down to <= 1.5)")
        foot = P[P[:, 2] <= bot + 0.5][:, :2].mean(axis=0)
        topxy = P[P[:, 2] >= top - 0.5][:, :2].mean(axis=0)
        dtop = float(dist_polyline(topxy[None], np.vstack([C.shore_m, C.shore_m[:1]]))[0])
        if dtop > 4.0:
            problems.append(f"sheet top is {dtop:.1f} m from a pad edge (need <= 4)")
    if vz:
        A = np.vstack(vz)
        r = float(np.corrcoef(A[:, 0], A[:, 1])[0, 1]) if np.ptp(A[:, 0]) > 0 else 0.0
        if not r <= -0.5:
            problems.append(f"UV v vs z correlation {r:.2f} (v must run down the sheet: <= -0.5)")
    else:
        problems.append("no UVs on the sheet")
    if foot is not None:
        surf = [o for n, o in S.meshes.items() if re.match(r"^WATER_SURF_\d+$", n)]
        b = bvh_of(S, surf)
        r = b.find_nearest(Vector((float(foot[0]), float(foot[1]), 0.05)), 15.0) if b else (None,)
        if r[0] is None:
            problems.append(f"no WATER_SURF_nn within 15 m of the foot ({foot[0]:.1f}, {foot[1]:.1f})")
    return not problems, ("; ".join(problems) if problems else
                          f"{len(falls)} WATER_FALL_* (LK_FALL): {sheet_area:.0f} m2 near-vertical sheet from the pad edge to the sea, "
                          f"UV v runs down, whitewater WATER_SURF within 15 m of its foot, {plat / max(total, 1e-9):.0%} of its area faces up "
                          f"above z 1 m (<= 20%)")


def g_skin(S, C, need):
    skins = {n: o for n, o in S.meshes.items() if n.startswith("ROCK_SKIN_")}
    if not skins:
        return False, "no ROCK_SKIN_* mesh"
    P = terrain_shore_frame(S, C, 4.0)[0]
    cov = coverage(bvh_of(S, list(skins.values())), P, C.pz * 0.5, 3.0)
    # v2: 'jagged, not a smooth extruded wall': spread of the skin's outward offset from the design shore (mid-height vertices near the shore)
    SV = np.vstack([S.wld(o)["W"] for o in skins.values()])
    SV = SV[(SV[:, 2] > -2.0) & (SV[:, 2] < C.pz - 0.05)]
    relief = 0.0
    if len(SV):
        loops = terrain_loops(S)
        dist = np.min(np.stack([dist_polyline(SV[:, :2], L_[0], closed=True) for L_ in loops]), axis=0) if loops else \
            dist_polyline(SV[:, :2], np.vstack([C.shore_m, C.shore_m[:1]]))
        near = dist <= 6.0
        if near.sum() > 20:
            sign = np.where(C.dry_m(SV[near, 0], SV[near, 1]), -1.0, 1.0)
            relief = float(np.std(sign * dist[near]))
    ok = cov >= need and relief >= TH2["skin_relief"]
    return ok, (f"{len(skins)} ROCK_SKIN_* meshes clad {cov:.0%} of the shore (nearest surface within 3 m of the wall at mid "
                f"height z {C.pz * 0.5:g}; need >= {need:.0%}); relief: sd of the outward offset from the shore {relief:.2f} m (need >= {TH2['skin_relief']} m: not a smooth curtain)")


# ----------------------------------------------------------------------------- hole 9 Split
def split_regions(C, P_yd):
    r = C.sc.get("ridge", {})
    spine = np.array(r.get("spine", []), float)
    wmax = float(max(r.get("width_yd", [40.0])))
    dc = dist_polyline(P_yd, C.center_yd)
    ds = dist_polyline(P_yd, spine) if len(spine) >= 2 else np.full(len(P_yd), np.inf)
    dry = C.dry_yd(P_yd[:, 0], P_yd[:, 1])
    ridge_core = dry & (ds <= wmax / 2) & (dc >= C.fw / 2 + 30.0)
    ribbon_core = dry & (dc <= C.fw / 2 + 10.0) & (ds >= 30.0)
    ridge_side = dry & (ds < dc)                       # Voronoi: closer to the ridge spine than to the ribbon centerline
    return ridge_core, ribbon_core, ridge_side, dc, ds


def split_samples(C, step=2.0):
    lo = C.shore_yd.min(axis=0)
    hi = C.shore_yd.max(axis=0)
    rng = np.random.default_rng(909)
    X, Y = np.meshgrid(np.arange(lo[0], hi[0], step), np.arange(lo[1], hi[1], step), indexing="ij")
    P = np.stack([X.ravel(), Y.ravel()], 1) + rng.uniform(0, step, (X.size, 2))
    return P


def surface_rays(S):
    if "_surf_rays" not in S.__dict__:
        objs = coll_objects(S) + [o for n, o in sorted(S.meshes.items()) if n.startswith("DRESS_")]
        S._surf_rays = Rays(S, objs)
    return S._surf_rays


def g_split_scrub(S, C):
    P = split_samples(C)
    ridge_core, ribbon_core, ridge_side, dc, ds = split_regions(C, P)
    rays = surface_rays(S)
    z0 = C.pz + 0.8
    hits_r, n_r, hits_b, n_b = 0, 0, 0, 0
    ex = None
    for (x, y), rc, bc in zip(P.tolist(), ridge_core.tolist(), ribbon_core.tolist()):
        if not (rc or bc):
            continue
        r = rays.cast(x * YD, y * YD, z0)
        m = rays.mat_of(r[1]) if r is not None else None
        if rc:
            n_r += 1
            hits_r += m == "LK_SCRUB"
        else:
            n_b += 1
            if m == "LK_SCRUB":
                hits_b += 1
                ex = ex or (round(x, 1), round(y, 1))
    cov = hits_r / max(1, n_r)
    # where does LK_SCRUB lie: up-facing scrub area on the ridge side of the ridge/ribbon split
    area_in, area_all = 0.0, 0.0
    for n, o in S.meshes.items():
        w = S.wld(o)
        if "LK_SCRUB" not in w["names"]:
            continue
        names = np.array(w["names"], object)[w["mi"]]
        msk = (names == "LK_SCRUB") & (w["N"][:, 2] > 0.5)
        if not msk.any():
            continue
        cen = w["W"][w["T"][msk]].mean(axis=1)[:, :2] / YD
        _rc, _bc, side, _dc, _ds = split_regions(C, cen)
        area_all += float(w["A"][msk].sum())
        area_in += float(w["A"][msk][side].sum())
    frac_in = area_in / area_all if area_all > 0 else 0.0
    problems = []
    if n_r < 200:
        problems.append(f"only {n_r} ridge-core samples (design ridge spine missing?)")
    if cov < TH["scrub_cov"]:
        problems.append(f"LK_SCRUB is the visible ground at {cov:.0%} of {n_r} ridge-core points (need >= {TH['scrub_cov']:.0%})")
    if area_all == 0 or frac_in < TH["scrub_in_ridge"]:
        problems.append(f"{frac_in:.0%} of {area_all:.0f} m2 up-facing LK_SCRUB lies on the ridge side (need >= {TH['scrub_in_ridge']:.0%})")
    # v2: 'FAIL if that ridge is still the same flat green as the fairway': the SCRUB albedo itself must be olive / dry and differ from the fairway
    sp_, fp_ = look_png(C, "Scrub_C"), look_png(C, "Fairway_C")
    if sp_ is None or fp_ is None:
        problems.append("Scrub_C.png / Fairway_C.png missing: cannot prove the ridge is not fairway green")
    else:
        ms, mf = grass_metrics(sp_), grass_metrics(fp_)
        hd, vd = abs(ms["h"] - mf["h"]), abs(ms["v"] - mf["v"])
        cc_ = pair_correlation(C, "Scrub_C", "Fairway_C")
        if cc_ >= TH2["distinct_corr"]:
            problems.append(f"Scrub_C is the Fairway_C pattern recoloured (channel-field correlation {cc_:.2f} >= {TH2['distinct_corr']})")
        if not (hd >= 12.0 or vd >= 0.12):
            problems.append(f"Scrub_C (hue {ms['h']:.0f}, V {ms['v']:.2f}) is a copy / tint of Fairway_C (hue {mf['h']:.0f}, V {mf['v']:.2f}): hue differs {hd:.0f} deg (need >= 12) "
                            f"and value {vd:.2f} (need >= 0.12)")
        if not (32.0 <= ms["h"] <= 70.0 and 0.20 <= ms["s"] <= 0.65 and 0.28 <= ms["v"] <= 0.75):
            problems.append(f"Scrub_C mean hue {ms['h']:.0f} / S {ms['s']:.2f} / V {ms['v']:.2f} is not olive-khaki-dry (hue 32-70, S .20-.65, V .28-.75)")
    ok = not problems
    return ok, ("; ".join(problems) if problems else
                f"LK_SCRUB is the visible ground at {cov:.0%} of {n_r} ridge-core points; {frac_in:.0%} of {area_all:.0f} m2 scrub on the ridge "
                f"side; Scrub_C hue {ms['h']:.0f} S {ms['s']:.2f} V {ms['v']:.2f} vs Fairway_C hue {mf['h']:.0f} V {mf['v']:.2f}") + f" [ribbon-core scrub hits {hits_b}/{n_b}]"


def g_split_ribbon(S, C):
    P = split_samples(C)
    _rc, ribbon_core, _side, _dc, _ds = split_regions(C, P)
    rays = surface_rays(S)
    z0 = C.pz + 0.8
    n, bad, ex = 0, 0, []
    vis = {}
    for (x, y), bc in zip(P.tolist(), ribbon_core.tolist()):
        if not bc:
            continue
        r = rays.cast(x * YD, y * YD, z0)
        n += 1
        m = rays.mat_of(r[1]) if r is not None else "-"
        vis[m] = vis.get(m, 0) + 1
        if m == "LK_SCRUB":
            bad += 1
            if len(ex) < 3:
                ex.append((round(x, 1), round(y, 1)))
    play_scrub = sorted(nm for nm, o in S.meshes.items() if PLAY_OBJECT_RE.match(nm) and "LK_SCRUB" in S.used_mats(o))
    ok = bad == 0 and n >= 200 and not play_scrub
    return ok, (f"{n} ribbon-core points (within fairway/2 + 10 yd of the centerline, >= 30 yd from the ridge spine): visible ground "
                f"{vis}; LK_SCRUB at {bad} (need 0){(' e.g. ' + str(ex)) if ex else ''}; play meshes with LK_SCRUB {play_scrub}")


def g_split_scatter(S, C):
    plants = {n: o for n, o in S.all.items() if n.startswith("PLANT_")}
    rocks = {n: o for n, o in S.all.items() if is_loose_rock(n)}
    out = {}
    for what, objs in (("plants", plants), ("rocks", rocks)):
        if not objs:
            out[what] = 0
            continue
        P = np.array([S.centre(o)[:2] for o in objs.values()]) / YD
        _rc, _bc, side, _dc, _ds = split_regions(C, P)
        out[what] = int(side.sum())
    ok = out["plants"] >= TH["ridge_plants"] and out["rocks"] >= TH["ridge_rocks"]
    return ok, (f"ridge scatter: {out['plants']} PLANT_* (need >= {TH['ridge_plants']}) and {out['rocks']} loose ROCK_* "
                f"(need >= {TH['ridge_rocks']}) on the ridge side")


def g_split_ruin(S, C):
    ruin = {n: o for n, o in S.meshes.items() if n.startswith("DRESS_RUIN")}
    if not ruin:
        return False, "no DRESS_RUIN_* mesh"
    problems = []
    blocks = masonry_blocks(S, ruin)
    if blocks < TH["ruin_blocks"]:
        problems.append(f"{blocks} LK_MASONRY pieces (need >= {TH['ruin_blocks']})")
    lo, hi = union_bbox(S, ruin)
    walls = C.sc.get("ruin", {}).get("walls", [])
    if walls:
        pts = np.array([w["p0"] for w in walls] + [w["p1"] for w in walls], float) * YD
        c = pts.mean(axis=0)
        cc = (lo + hi) / 2
        d = math.hypot(cc[0] - c[0], cc[1] - c[1])
        if d > 20.0:
            problems.append(f"ruin centre {d:.1f} m from the SCENERY ruin (<= 20)")
    if hi[2] - C.pz < 2.0:
        problems.append(f"ruin top only {hi[2] - C.pz:.1f} m above the play height (need >= 2)")
    return not problems, ("; ".join(problems) if problems else
                          f"{len(ruin)} DRESS_RUIN_* objects: {blocks} masonry pieces, top {hi[2] - C.pz:.1f} m above the ridge, at the SCENERY ruin")


# ----------------------------------------------------------------------------- hole 10 Crater
def g_crater_columns(S, C):
    wr = C.sc.get("wall_ring", {})
    cx, cy = wr.get("cx", 0.0) * YD, wr.get("cd", 0.0) * YD
    r0, r1 = wr.get("r_inner", 295.0) * YD - 40.0, wr.get("r_outer", 355.0) * YD + 40.0
    cache = {}
    problems = []
    named = {n: o for n, o in S.meshes.items() if n.startswith("ROCK_WALL_")}
    ring = {}
    for n, o in S.meshes.items():
        if not n.startswith("ROCK_") or n.startswith("ROCK_SKIN_"):
            continue
        p = S.pos(o)
        if r0 <= math.hypot(p[0] - cx, p[1] - cy) <= r1 or n in named:
            ring[n] = o
    ncol = {}
    for n, o in ring.items():
        m3 = tuple(np.round(np.array(o.matrix_world)[:3, :3], 3).ravel().tolist())
        k = (o.data.name, m3)
        if k not in cache:
            V, T = S.oriented(o)
            cache[k] = count_columns(V, T)
        ncol[n] = cache[k]
    bad_named = sorted(n for n in named if ncol.get(n, (0, 0))[0] < TH["columns"] or ncol[n][1] < 0.45)
    if not named:
        problems.append("no ROCK_WALL_* objects")
    if bad_named:
        problems.append(f"{len(bad_named)} ROCK_WALL_* are not column clusters (>= {TH['columns']} hex columns, side area >= 45%): "
                        + ", ".join(f"{n} {ncol[n][0]} cols/{ncol[n][1]:.0%} side" for n in bad_named[:5]))
    bins = np.zeros(72, bool)
    for n, o in ring.items():
        if ncol[n][0] < TH["columns"]:
            continue
        lo, hi = S.wbbox(o)
        ang = [math.atan2(y - cy, x - cx) for x in (lo[0], hi[0]) for y in (lo[1], hi[1])]
        a0 = math.atan2((lo[1] + hi[1]) / 2 - cy, (lo[0] + hi[0]) / 2 - cx)
        rel = [((a - a0 + math.pi) % (2 * math.pi)) - math.pi for a in ang]
        for b in range(72):
            mid = -math.pi + (b + 0.5) * 2 * math.pi / 72
            d = ((mid - a0 + math.pi) % (2 * math.pi)) - math.pi
            if min(rel) - 1e-9 <= d <= max(rel) + 1e-9:
                bins[b] = True
    cov = float(bins.mean())
    if cov < TH["ring_cov"]:
        problems.append(f"column clusters cover {cov:.0%} of the wall ring's 360 deg (need >= {TH['ring_cov']:.0%})")
    cnts = sorted({v[0] for v in ncol.values()})
    return not problems, ("; ".join(problems) if problems else
                          f"{len(named)} ROCK_WALL_* + {len(ring) - len(named)} other ring rocks are column clusters (columns per mesh "
                          f"{cnts[:6]}...), covering {cov:.0%} of the ring")


def g_crater_pillar(S, C):
    p = C.sc.get("pillar", {})
    if not p:
        return False, "no SCENERY pillar"
    px = p["x"] * YD
    py = (p["d"] if "d" in p else p["y"]) * YD
    r = p["r_top"] * YD + 3.0
    lava = S.meshes.get("WATER_LAVA")
    lz = float(S.wld(lava)["W"][:, 2].max()) if lava is not None else 0.0
    side_a, bas_a, zmin = 0.0, 0.0, np.inf
    under_a, under_bas = 0.0, 0.0
    for o in coll_objects(S):
        if not o.name.startswith("TERRAIN"):
            continue
        w = S.wld(o)
        cen = w["W"][w["T"]].mean(axis=1)
        near = np.hypot(cen[:, 0] - px, cen[:, 1] - py) <= r
        top, wall, down, lip = face_class(w, C.pz)
        names = np.array(w["names"], object)[w["mi"]]
        sm = near & wall
        side_a += float(w["A"][sm].sum())
        bas_a += float(w["A"][sm & (names == "LK_BASALT")].sum())
        um = near & down
        under_a += float(w["A"][um].sum())
        under_bas += float(w["A"][um & (names == "LK_BASALT")].sum())
        if sm.any():
            zmin = min(zmin, float(w["W"][w["T"][sm]][:, :, 2].min()))
    problems = []
    if side_a <= 0:
        problems.append("no pillar side faces found")
    else:
        f = bas_a / side_a
        if f < 0.95:
            problems.append(f"pillar sides {f:.0%} LK_BASALT (need 95%)")
        if zmin > lz + 0.05:
            problems.append(f"pillar sides end at z {zmin:.2f} above the lava top {lz:.2f} (a floating disc)")
    if under_a > 0 and under_bas / under_a < 0.95:
        problems.append(f"pillar underside {under_bas / under_a:.0%} LK_BASALT (need 95%)")
    pin = S.pos(S.all["MARKER_PIN"]) if "MARKER_PIN" in S.all else None
    if pin is not None:
        hit, z = on_land(S, C, float(pin[0]), float(pin[1]))
        if hit != "GREEN" or not (C.pz - 1e-3 <= z <= C.pz + 0.6):
            problems.append(f"at the pin GroundHeight hits {hit} z {z} (want GREEN at PLAY_Z..+0.6)")
    return not problems, ("; ".join(problems) if problems else
                          f"pillar top = GREEN at the pin at play height; sides {side_a:.0f} m2 {bas_a / side_a:.0%} LK_BASALT reaching z "
                          f"{zmin:.1f} (lava top {lz:.2f}); underside {under_a:.0f} m2 basalt")


def g_crater_cone(S, C):
    cones = {n: o for n, o in S.meshes.items() if n.startswith("DRESS_CONE")}
    smoke = {n: o for n, o in S.meshes.items() if n.startswith("DRESS_SMOKE")}
    problems = []
    cr = C.sc.get("crater", {})
    wr = C.sc.get("wall_ring", {})
    ccx, ccy = cr.get("cx", 0.0) * YD, cr.get("cd", 0.0) * YD
    if not cones:
        problems.append("no DRESS_CONE")
    else:
        lo, hi = union_bbox(S, cones)
        c = (lo + hi) / 2
        dist = math.hypot(c[0] - ccx, c[1] - ccy)
        need = wr.get("r_inner", 295.0) * YD - 10.0
        if dist < need:
            problems.append(f"cone {dist:.0f} m from the crater centre (behind the bowl: >= {need:.0f})")
        tee = S.pos(S.all["MARKER_TEE"]) + np.array([0, 0, 2.2]) if "MARKER_TEE" in S.all else np.array([0, 0, C.pz + 2.2])
        az = math.atan2(c[1] - tee[1], c[0] - tee[0])
        el_cone = math.degrees(math.atan2(hi[2] - tee[2], math.hypot(c[0] - tee[0], c[1] - tee[1])))
        el_wall = -90.0
        for n, o in S.meshes.items():
            if not n.startswith("ROCK_") or n.startswith("ROCK_SKIN_"):
                continue
            rlo, rhi = S.wbbox(o)
            rc = (rlo + rhi) / 2
            a = math.atan2(rc[1] - tee[1], rc[0] - tee[0])
            if abs(((a - az + math.pi) % (2 * math.pi)) - math.pi) <= math.radians(4.0):
                dd = math.hypot(rc[0] - tee[0], rc[1] - tee[1])
                if dd < math.hypot(c[0] - tee[0], c[1] - tee[1]):
                    el_wall = max(el_wall, math.degrees(math.atan2(rhi[2] - tee[2], max(dd - 0.5 * float(np.hypot(*(rhi - rlo)[:2])), 1.0))))
        if el_cone < el_wall + 0.5:
            problems.append(f"cone top elevation {el_cone:.1f} deg from the tee is not above the walls in front of it ({el_wall:.1f} deg)")
    if len(smoke) < TH["smoke"]:
        problems.append(f"{len(smoke)} DRESS_SMOKE_nn (need >= {TH['smoke']})")
    far = cr.get("rim_radius", 150.0) * YD
    for n, o in smoke.items():
        lo, hi = S.wbbox(o)
        c = (lo + hi) / 2
        if math.hypot(c[0] - ccx, c[1] - ccy) < far:
            problems.append(f"{n} inside the bowl ({math.hypot(c[0] - ccx, c[1] - ccy):.0f} m < rim {far:.0f})")
        if S.used_mats(o) != {"LK_SMOKE"}:
            problems.append(f"{n} uses {sorted(S.used_mats(o))}")
    return not problems, ("; ".join(problems) if problems else
                          f"DRESS_CONE behind the bowl, its top {el_cone:.1f} deg above the tee horizon vs walls {el_wall:.1f} deg; "
                          f"{len(smoke)} LK_SMOKE cards beyond the rim")


def g_crater_plants(S, C):
    plants = {n: o for n, o in S.all.items() if n.startswith("PLANT_")}
    shelf = []
    for n, o in plants.items():
        p = S.centre(o)
        hit, z = on_land(S, C, float(p[0]), float(p[1]))
        if hit and (hit.startswith("TERRAIN") or hit in ("FAIRWAY_FIRSTCUT", "GREEN_APRON")) and C.pz - 0.05 <= z <= C.pz + 0.65:
            shelf.append(n)
    ok = len(shelf) >= TH["shelf_plants"]
    return ok, f"{len(shelf)} PLANT_* on the grass shelf (rough land at play height; need >= {TH['shelf_plants']}) of {len(plants)} plants"


def g_crater_tee(S, C):
    o = S.meshes.get("TEE_BOX")
    if o is None:
        return False, "TEE_BOX missing"
    w = S.wld(o)
    xy = w["W"][:, :2]
    idx = convex_hull_2d([tuple(p) for p in xy.tolist()])
    H = xy[idx]
    area = 0.5 * abs(np.dot(H[:, 0], np.roll(H[:, 1], -1)) - np.dot(H[:, 1], np.roll(H[:, 0], -1)))
    best = None
    for i in range(len(H)):
        e = H[(i + 1) % len(H)] - H[i]
        L = np.hypot(*e)
        if L < 1e-6:
            continue
        u = e / L
        v = np.array([-u[1], u[0]])
        a = H @ u
        b = H @ v
        ra = (np.ptp(a), np.ptp(b))
        if best is None or ra[0] * ra[1] < best[0] * best[1]:
            best = ra
    fill = area / (best[0] * best[1])
    aspect = max(best) / min(best)
    um = S.used_mats(o)
    problems = []
    if fill < 0.9 or aspect > 1.3:
        problems.append(f"plan fill {fill:.2f} (need >= .90) / aspect {aspect:.2f} (need <= 1.3): not a square pad")
    if not um <= {"LK_GREEN", "LK_FAIRWAY"} or not um:
        problems.append(f"materials {sorted(um)} (want a textured grass LK_GREEN)")
    return not problems, ("; ".join(problems) if problems else
                          f"TEE_BOX {best[0]:.1f} x {best[1]:.1f} m, fill {fill:.2f}, aspect {aspect:.2f}, {sorted(um)} (UVs: UV_DENSITY)")


# ----------------------------------------------------------------------------- stage runner
def run_stage(stage, C, sig, FB=None, NB=None):
    print(f"--- stage {stage}: {len(bpy.context.scene.objects)} objects in the scene", flush=True)
    S = Snap(stage, C)
    info("SCOPE", f"{S.scope}: {len(S.all)} objects, {len(S.meshes)} meshes", stage)
    B = sig["stages"][stage] if sig else None
    if B is not None:
        run_gate(stage, "PLAY_SURFACE_NAMES", g_play_names, S, C, B)
        run_gate(stage, "PLAY_SURFACE_GRID", g_play_grid, S, C, B, sig)
        run_gate(stage, "PLAY_SURFACE_TOPS", g_play_tops, S, C, B, sig)
        if FB is not None:
            run_gate(stage, "PLAY_SURFACE_FACES", g_play_faces, S, C, FB)
        else:
            gate("PLAY_SURFACE_FACES", False, "no face baseline (hole_0N_faces.npz): build it once with --build-face-baseline", stage)
        if NB is not None:
            run_gate(stage, "PLAY_SURFACE_WALLS", g_play_walls, S, C, NB)
        else:
            gate("PLAY_SURFACE_WALLS", False, "no wall baseline (hole_0N_nonup.npz): build it once with --build-nonup-baseline", stage)
        run_gate(stage, "MARKERS_FROZEN", g_markers, S, C, B)
        run_gate(stage, "RESERVED_NAMES", g_reserved, S, C, B)
        run_gate(stage, "NEW_OBJECT_PREFIXES", g_new_prefixes, S, C, B)
        run_gate(stage, "ONE_GREEN", g_one_green, S, C, B)
    else:
        gate("BASELINE_SIGNATURE", False, "no baseline signature: the frozen-geometry gates cannot run (build it with --build-baseline)", stage)
    run_gate(stage, "NO_MAT_FOAM", g_no_foam, S, C)
    run_gate(stage, "NO_LEGACY_MATERIALS", g_legacy_mats, S, C)
    run_gate(stage, "LK_REQUIRED", g_lk_required, S, C)
    run_gate(stage, "GROUND_MATERIALS", g_ground_mats, S, C)
    run_gate(stage, "ROCK_MATERIALS", g_rock_mats, S, C)
    run_gate(stage, "PROP_MATERIALS", g_prop_mats, S, C)
    if stage == "blend":
        run_gate(stage, "LK_TEXTURED", g_lk_textured, S, C)
    run_gate(stage, "LOOK_FILES", g_look_files, S, C)
    run_gate(stage, "ALBEDO_DISTINCT", g_albedo_distinct, S, C)
    run_gate(stage, "GRASS_ALBEDO_HUE", g_grass_hue, S, C)
    if C.hole == 10:
        run_gate(stage, "LAVA_TEXTURE", g_lava_tex, S, C)
        run_gate(stage, "BASALT_PRISMS", g_basalt_tex, S, C)
        run_gate(stage, "SMOKE_SOFT", g_smoke_tex, S, C)
        run_gate(stage, "SMOKE_IN_FRAME", g_smoke_frames, S, C)
    run_gate(stage, "UV_DENSITY", g_uv, S, C)
    run_gate(stage, "GROUND_UV_LOCAL_SMEAR", g_uv_local_smear, S, C)
    run_gate(stage, "SAND_NAMES", g_sand_names, S, C)
    run_gate(stage, "ROCK_NOT_ICOSAHEDRON", g_rock_verts, S, C)
    run_gate(stage, "ROCK_VARIETY", g_rock_variety, S, C)
    run_gate(stage, "PROP_FACE_BUDGET", g_prop_budget, S, C)
    run_gate(stage, "PLANTS_SHARED_MATERIAL", g_plants_material, S, C)
    run_gate(stage, "PLANTS_INSTANCED", g_plants_instanced, S, C)
    run_gate(stage, "PLANT_SWAY_COLORS", g_plant_sway_colors, S, C)
    run_gate(stage, "PLANT_SWAY_CAPS", g_plant_sway_caps, S, C)
    run_gate(stage, "PROPS_OFF_PLAY", g_props_off_play, S, C)
    run_gate(stage, "NO_PLANT_ON_WATER", g_no_plant_on_water, S, C)
    run_gate(stage, "NO_ROCK_STRETCH_2X", g_rock_stretch, S, C)
    run_gate(stage, "PATH_UNDER_BALL", g_path_under_ball, S, C)
    if C.hole in (8, 9):
        run_gate(stage, "SEASTACK_TAPERED", g_seastack_tapered, S, C)
    run_gate(stage, "NO_ANIMATION", g_no_anim, S, C)
    run_gate(stage, "TRIANGLE_BUDGET", g_tri_budget, S, C)
    run_gate(stage, "NO_FOAM_STRIP", g_no_foam_strip, S, C)
    if C.hole in (8, 9):
        run_gate(stage, "OCEAN_WATER", g_ocean, S, C)
        run_gate(stage, "SURF_SHORE", g_surf, S, C)
        run_gate(stage, "SHELF_THIN", g_shelf_thin, S, C)
    if C.hole == 8:
        run_gate(stage, "NEEDLE_PATH", g_needle_path, S, C)
        run_gate(stage, "NEEDLE_FLOWERS_SHRUBS_TUFTS", g_needle_flowers, S, C)
        run_gate(stage, "NEEDLE_ARCH", g_needle_arch, S, C)
        run_gate(stage, "NEEDLE_WALL_VINES", g_needle_wall_vines, S, C)
        run_gate(stage, "NEEDLE_WATERFALL", g_needle_waterfall, S, C)
        run_gate(stage, "NEEDLE_SEA_STACKS", g_needle_stacks, S, C)
        run_gate(stage, "NEEDLE_CLIFF_SKIN", g_skin, S, C, TH["skin_cov8"])
    if C.hole == 9:
        run_gate(stage, "SPLIT_SCRUB_RIDGE", g_split_scrub, S, C)
        run_gate(stage, "SPLIT_NO_SCRUB_RIBBON", g_split_ribbon, S, C)
        run_gate(stage, "SPLIT_RIDGE_SCATTER", g_split_scatter, S, C)
        run_gate(stage, "SPLIT_RUIN", g_split_ruin, S, C)
        run_gate(stage, "SPLIT_SHORE_SKIN", g_skin, S, C, TH["skin_cov9"])
    if C.hole == 10:
        run_gate(stage, "CRATER_COLUMNS", g_crater_columns, S, C)
        run_gate(stage, "CRATER_WALL_COVERED", g_crater_wall_cover, S, C)
        run_gate(stage, "CRATER_LAVA", g_lava_mesh, S, C)
        run_gate(stage, "CRATER_LAVA_LIGHTS", g_lava_lights, S, C)
        run_gate(stage, "CRATER_PILLAR", g_crater_pillar, S, C)
        run_gate(stage, "CRATER_CONE_SMOKE", g_crater_cone, S, C)
        run_gate(stage, "CRATER_SHELF_PLANTS", g_crater_plants, S, C)
        run_gate(stage, "CRATER_TEE_PAD", g_crater_tee, S, C)
    return S


def read_meta(path):
    d = {}
    with open(path) as f:
        for ln in f.read().splitlines():
            if ":" in ln:
                k, v = ln.split(":", 1)
                d.setdefault(k.strip(), []).append(v.strip())
    return d


def g_fbx_file(fbx):
    if not os.path.isfile(fbx):
        return False, f"{fbx} missing"
    mb = os.path.getsize(fbx) / 1048576
    problems = []
    if mb > FBX_MB:
        problems.append(f"{mb:.2f} MB (> {FBX_MB})")
    meta = fbx + ".meta"
    if not os.path.isfile(meta):
        problems.append(".meta missing")
    else:
        m = read_meta(meta)
        if m.get("isReadable", [""])[0] != "1":
            problems.append(f"isReadable {m.get('isReadable')}")
        if m.get("tangentImportMode", [""])[0] != "3":
            problems.append(f"tangentImportMode {m.get('tangentImportMode')}")
    return not problems, ("; ".join(problems) if problems else f"{rel(fbx)} "
                          f"{mb:.2f} MB (<= {FBX_MB}); .meta isReadable 1, tangentImportMode 3")


def g_hole07(test_dir=None):
    """sha256 of the frozen hole 7 files (test hook: test_dir holds copies with the same basenames)."""
    bad = []
    for k, (p, want) in HOLE07.items():
        if test_dir:
            p = os.path.join(test_dir, os.path.basename(p))
        got = sha256_file(p) if os.path.isfile(p) else None
        if got != want:
            bad.append(f"{os.path.basename(p)} {str(got)[:12]} (want {want[:12]})")
    return not bad, ("hole_07.blend, hole_07.fbx, hole_07.fbx.meta sha256 unchanged" if not bad else "; ".join(bad))


def check(hole, opts):
    """Run every gate for one hole. Returns (failures, results)."""
    RES.clear()
    INFO.clear()
    TIMES.clear()
    t0 = time.time()
    lib_signature()                                  # props-library topology hashes / sizes, built in an empty scene BEFORE any hole is opened
    design, dpath = load_design(hole, opts.get("design"))
    C = Ctx(hole, design, dpath, opts)
    sig = load_signature(opts["sig_dir"], hole)
    FB = load_face_baseline(opts["sig_dir"], hole)
    NB = load_nonup_baseline(opts["sig_dir"], hole)
    tag = C.tag
    blend = opts.get("blend") or os.path.join(BLENDER_DIR, f"hole_{tag}.blend")
    fbx = opts.get("fbx") or os.path.join(COURSE_DIR, f"hole_{tag}.fbx")
    print(f"postcard_look_verify: hole {hole} {design.NAME} (play_z {C.pz}); blend {blend}; fbx {fbx}; look {C.look_dir}; design {dpath}", flush=True)
    if sig is None:
        gate("BASELINE_SIGNATURE", False, f"{sig_paths(opts['sig_dir'], hole)[0]} missing (run --build-baseline once)")
    else:
        gate("BASELINE_SIGNATURE", sig["_npz_ok"], f"{os.path.relpath(sig_paths(opts['sig_dir'], hole)[0], REPO)} built {sig['built']} from "
             f"{sig['baseline_blend']} ({sig['baseline_blend_sha256'][:12]}) + {sig['baseline_fbx']} ({sig['baseline_fbx_sha256'][:12]}); "
             f"npz sha256 {'matches' if sig['_npz_ok'] else 'DOES NOT match'} the json")
    run_gate(None, "HOLE07_UNTOUCHED", g_hole07, opts.get("hole07_dir"))
    run_gate(None, "SHADERS_STATIC", g_shaders_static)
    run_gate(None, "STILL_FLOOR_SELFCHECK", g_still_floor_selfcheck)
    run_gate(None, "LOOK_STILLS_TEXTURED", g_look_stills, C)
    if C.hole == 10:
        run_gate(None, "SMOKE_VISIBLE_STILLS", g_smoke_stills, C)
    if not opts.get("no_blend"):
        if not os.path.isfile(blend):
            gate("BLEND_OPEN", False, f"{blend} missing", "blend")
        else:
            try:
                bpy.ops.wm.open_mainfile(filepath=blend, load_ui=False)
                run_stage("blend", C, sig, FB, NB)
            except Exception as e:  # noqa: BLE001
                traceback.print_exc()
                gate("BLEND_STAGE", False, f"{type(e).__name__}: {e}", "blend")
    if not opts.get("no_fbx"):
        run_gate("fbx", "FBX_FILE", g_fbx_file, fbx)
        if os.path.isfile(fbx):
            try:
                bpy.ops.wm.read_factory_settings(use_empty=True)
                IMG_CACHE.clear()
                t = time.time()
                bpy.ops.import_scene.fbx(filepath=fbx)
                n = len(bpy.context.scene.objects)
                miss = [m for m in MARKERS if m not in bpy.data.objects]
                gate("FBX_REIMPORT", n > 0 and not miss, f"re-imported {n} objects in {time.time() - t:.1f} s; markers present: {not miss} {miss}", "fbx")
                run_stage("fbx", C, sig, FB, NB)
            except Exception as e:  # noqa: BLE001
                traceback.print_exc()
                gate("FBX_REIMPORT", False, f"{type(e).__name__}: {e}", "fbx")
    fails = [r["name"] for r in RES if not r["ok"]]
    total = time.time() - t0
    print(f"postcard_look_verify: hole {hole}: {len(RES)} gates, {len(fails)} failed, {total:.1f} s", flush=True)
    print("RESULT:", "ALL PASS" if not fails else f"FAILURES {fails}", flush=True)
    if opts.get("json"):
        with open(opts["json"], "w") as f:
            json.dump(dict(hole=hole, name=design.NAME, blend=blend, fbx=fbx, look_dir=C.look_dir, seconds=round(total, 1),
                           result="ALL PASS" if not fails else "FAILURES", failures=fails, gates=list(RES), info=list(INFO),
                           gate_seconds=dict(TIMES)), f, indent=1)
    return fails, list(RES)


# ----------------------------------------------------------------------------- CLI
def parse_args(argv):
    o = dict(hole=None, blend=None, fbx=None, json=None, no_blend=False, no_fbx=False, build_baseline=False, force=False,
             build_face_baseline=False, build_nonup_baseline=False, selftest_fbx=False, selftest_only=None,
             sig_dir=SIG_DIR, look_dir=LOOK_DIR, design=None, selftest=False, selftest_dir=None, keep=False, hole07_dir=None, stills_dir=None, require_stills=False)
    i = 0
    vals = {"--blend": "blend", "--fbx": "fbx", "--json": "json", "--sig-dir": "sig_dir", "--look-dir": "look_dir",
            "--design": "design", "--selftest-dir": "selftest_dir", "--hole07-dir": "hole07_dir", "--stills-dir": "stills_dir"}
    flags = {"--no-blend": "no_blend", "--no-fbx": "no_fbx", "--build-baseline": "build_baseline", "--force": "force",
             "--selftest": "selftest", "--keep": "keep", "--build-face-baseline": "build_face_baseline", "--build-nonup-baseline": "build_nonup_baseline", "--selftest-fbx": "selftest_fbx", "--require-stills": "require_stills"}
    while i < len(argv):
        a = argv[i]
        if a == "--selftest-only":
            if i + 1 >= len(argv):
                raise SystemExit("usage error: --selftest-only needs a comma list of mutation name fragments")
            o["selftest_only"] = [t for t in argv[i + 1].split(",") if t]
            i += 2
        elif a in vals:
            if i + 1 >= len(argv):
                raise SystemExit(f"usage error: {a} needs a value")
            o[vals[a]] = os.path.abspath(argv[i + 1])
            i += 2
        elif a in flags:
            o[flags[a]] = True
            i += 1
        elif a.startswith("--"):
            raise SystemExit(f"usage error: unknown option {a}")
        else:
            o["hole"] = a
            i += 1
    return o


def holes_of(arg):
    if arg == "all":
        return [8, 9, 10]
    h = int(arg)
    if h not in (8, 9, 10):
        raise SystemExit("usage error: hole must be 8, 9, 10 (or all for --selftest/--build-baseline)")
    return [h]


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    try:
        opts = parse_args(argv)
        if not opts["hole"]:
            print(__doc__)
            return 2
        holes = holes_of(opts["hole"])
    except SystemExit as e:
        print(e)
        return 2
    if opts["build_baseline"]:
        rc = 0
        for h in holes:
            rc = max(rc, build_baseline(h, opts))
        return rc
    if opts["build_face_baseline"]:
        rc = 0
        for h in holes:
            rc = max(rc, build_face_baseline(h, opts))
        return rc
    if opts["build_nonup_baseline"]:
        rc = 0
        for h in holes:
            rc = max(rc, build_nonup_baseline(h, opts))
        return rc
    if opts["selftest"]:
        return selftest(holes, opts)
    if len(holes) != 1:
        print("usage error: one hole at a time for a check")
        return 2
    fails, _ = check(holes[0], opts)
    return 0 if not fails else 1


# ============================================================================= SELF TEST (synthetic positive + negative mutations)
# Everything below writes ONLY under the self-test directory. It never touches blender/hole_*.blend, the Unity FBXs or the Look folder.
# The synthetic scene is NOT a look proposal: it is the cheapest scene that satisfies every gate, built on the frozen baseline
# geometry, so each gate is shown able to PASS; the mutations then show each gate able to FAIL.

# ----------------------------------------------------------------------------- synthetic textures (numpy): the cheapest set that satisfies every texture gate
def st_png(path, rgba):
    h, w = rgba.shape[:2]
    img = bpy.data.images.new("st_" + os.path.basename(path), w, h, alpha=True)
    img.pixels.foreach_set(np.clip(rgba, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def st_noise(n, rng, beta=1.0, lo=1, hi=None):
    """Periodic isotropic noise (n x n), zero mean, unit sd: white noise filtered to lo..hi cycles per tile with a 1/f^beta spectrum."""
    F = np.fft.fft2(rng.normal(0, 1, (n, n)))
    fy = np.fft.fftfreq(n)[:, None] * n
    fx = np.fft.fftfreq(n)[None, :] * n
    r = np.hypot(fx, fy)
    filt = np.where((r >= lo) & (r <= (hi if hi else n / 2)), 1.0 / np.maximum(r, 1.0) ** beta, 0.0)
    f = np.real(np.fft.ifft2(F * filt))
    f -= f.mean()
    return f / max(f.std(), 1e-12)


def st_hsv_img(h_deg, s, v, n_h, n_s, n_v):
    """Colour image from HSV maps (h in degrees); the noise maps are already scaled."""
    h = (h_deg + n_h) % 360.0 / 60.0
    s = np.clip(s + n_s, 0, 1)
    v = np.clip(v + n_v, 0, 1)
    i = np.floor(h).astype(int) % 6
    f = h - np.floor(h)
    p, q, t = v * (1 - s), v * (1 - s * f), v * (1 - s * (1 - f))
    r = np.choose(i, [v, q, p, p, t, v])
    g = np.choose(i, [t, v, v, q, p, p])
    b = np.choose(i, [p, p, t, v, v, q])
    return np.stack([r, g, b], -1)


ST_ALBEDO = {   # stem: (hue deg, S, V, V-noise sd, hue wobble deg, noise seed)
    "Fairway": (74.0, 0.62, 0.56, 0.085, 3.0, 11), "Green": (72.0, 0.60, 0.62, 0.085, 3.0, 12), "Rough": (68.0, 0.64, 0.42, 0.075, 4.0, 13),
    "Scrub": (47.0, 0.40, 0.46, 0.08, 4.0, 14), "Sand": (40.0, 0.27, 0.86, 0.040, 1.0, 15), "Cliff": (35.0, 0.10, 0.28, 0.07, 6.0, 16),
    "CliffDark": (160.0, 0.12, 0.17, 0.045, 5.0, 17), "Rock": (45.0, 0.20, 0.44, 0.075, 4.0, 18), "Path": (52.0, 0.28, 0.66, 0.095, 3.0, 19),
    "Masonry": (42.0, 0.17, 0.52, 0.075, 3.0, 20), "Basalt": (330.0, 0.08, 0.16, 0.035, 6.0, 21),
}


ST_GROUND = ("Fairway", "Green", "Rough", "Scrub")      # ground textures are 512 px in the synthetic scene (the gate wants >= 512; the contract is 1024)


def st_albedo_img(stem, n=256, hue=None, seed=None):
    h, s, v, vsd, hw, sd = ST_ALBEDO[stem]
    rng = np.random.default_rng(seed if seed is not None else sd)
    nv = vsd * (0.55 * st_noise(n, rng, 0.6, 4, 40) + 0.45 * st_noise(n, rng, 0.9, 3, 12))
    if stem in ST_GROUND:
        nv = nv + vsd * 0.5 * st_noise(n, rng, 0.3, 16, 120)             # blade-scale detail: windows of 1/16 of the tile carry sd >= 0.03
    nh = hw * st_noise(n, rng, 1.0, 2, 10)
    ns = 0.04 * st_noise(n, rng, 1.0, 2, 10)
    return st_hsv_img(h if hue is None else hue, s, v, nh, ns, nv)


def st_normal_img(n, seed, strength=0.13):
    rng = np.random.default_rng(seed)
    ht = st_noise(n, rng, 1.0, 3, 40)
    dx = (np.roll(ht, -1, 1) - np.roll(ht, 1, 1)) / 2.0
    dy = (np.roll(ht, -1, 0) - np.roll(ht, 1, 0)) / 2.0
    k = strength / math.sqrt((dx * dx).mean() + (dy * dy).mean())
    nx, ny = np.clip(-dx * k, -0.9, 0.9), np.clip(-dy * k, -0.9, 0.9)
    nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0, 1))
    return np.stack([0.5 + 0.5 * nx, 0.5 + 0.5 * ny, 0.5 + 0.5 * nz], -1)


def st_voronoi_edges(n, rng, cells=36, width=2.0):
    """Wrapped Voronoi: (border mask 0..1, cell id) on an n x n torus."""
    pts = rng.uniform(0, n, (cells, 2))
    yy, xx = np.mgrid[0:n, 0:n].astype(float)
    d = []
    for p in pts:
        dx = np.abs(xx - p[0])
        dy = np.abs(yy - p[1])
        dx = np.minimum(dx, n - dx)
        dy = np.minimum(dy, n - dy)
        d.append(np.hypot(dx, dy))
    d = np.sort(np.stack(d, 0), axis=0)
    border = np.clip(1.0 - (d[1] - d[0]) / width, 0, 1)
    return border


def st_lava_flow(n=512, seed=7):
    """Periodic swirling molten flow: bright orange everywhere with a few dark patches (C, E)."""
    rng = np.random.default_rng(seed)
    base = st_noise(n, rng, 1.2, 1, 12)
    warp = 30.0 * st_noise(n, rng, 1.5, 1, 6)
    yy, xx = np.mgrid[0:n, 0:n]
    f = base[(yy + warp.astype(int)) % n, (xx + np.roll(warp, 97, 1).astype(int)) % n]
    f = 0.5 + 0.5 * np.tanh(1.4 * f)                                      # 0..1
    t = np.clip((f - 0.08) / 0.92, 0, 1)
    cold, hot = np.array([0.56, 0.10, 0.02]), np.array([1.0, 0.62, 0.12])
    E = cold + (hot - cold) * t[..., None]
    crust = f < 0.05
    E[crust] *= 0.28
    E[..., 1] = np.minimum(E[..., 1], 190 / 255.0)
    E[..., 2] = np.minimum(E[..., 2], 60 / 255.0)
    nn = st_noise(n, rng, 0.8, 2, 30)[..., None]
    C = np.array([0.12, 0.04, 0.02]) * (1.0 + 0.45 * nn)
    return np.clip(C, 0, 1), np.clip(E, 0, 1)


def st_lava_voronoi(n=512, seed=8, dark_plates=True):
    """The failure the brief names: dark polygonal crust cells with glowing borders (or bright cells with dark cracks)."""
    rng = np.random.default_rng(seed)
    border = st_voronoi_edges(n, rng, 70, 5.0)
    glow = np.array([1.0, 0.55, 0.10])
    if dark_plates:
        E = border[..., None] * glow[None, None, :]
        C = np.full((n, n, 3), 0.05)
    else:
        E = (1 - border)[..., None] * np.array([0.95, 0.5, 0.08])[None, None, :]
        C = np.full((n, n, 3), 0.05)
    return C, E


def st_basalt_maps(n=512, seed=3, ladder=False, bricks=False, flat_bevel=False):
    """Basalt prisms: columns with left / right bevel planes, few cross-joints, glow only in vertical joints. Variants for the mutations."""
    rng = np.random.default_rng(seed)
    widths, x = [], 0
    while x < n:
        w = int(rng.integers(64, 110))
        w = min(w, n - x)
        if n - x - w < 48:
            w = n - x
        widths.append(w)
        x += w
    N = np.zeros((n, n, 3))
    N[..., 2] = 1.0
    Cc = np.zeros((n, n, 3))
    Ee = np.zeros((n, n, 3))
    base = st_noise(n, rng, 1.0, 3, 40)
    x0 = 0
    for w in widths:
        bev = 22 if not flat_bevel else 6
        nxv = 0.45 if not flat_bevel else 0.10
        N[:, x0:x0 + bev, 0] = -nxv
        N[:, x0 + w - bev:x0 + w, 0] = nxv
        ncj = int(rng.integers(0, 2)) if not bricks else 9
        for _ in range(ncj):
            y = int(rng.integers(8, n - 8))
            N[y:y + 2, x0 + 4:x0 + w - 4, 1] = 0.5
        gl = int(rng.integers(40, 200))
        gy = int(rng.integers(0, n - gl))
        if rng.random() < 0.7:
            Ee[gy:gy + gl, x0:x0 + 3, :] = np.array([0.9, 0.38, 0.07])
        if ladder:
            for r_ in range(gy, gy + gl, 36):
                Ee[r_:r_ + 3, x0:min(n, x0 + w + 40), :] = np.array([0.9, 0.38, 0.07])
        x0 += w
    for _ in range(14):                                                            # small hot dots
        y, xx = int(rng.integers(0, n - 4)), int(rng.integers(0, n - 4))
        Ee[y:y + 4, xx:xx + 4] = np.array([0.8, 0.3, 0.05])
    Cc[:] = (0.12 + 0.02 * base)[..., None] * np.array([1.0, 0.95, 1.0])
    # normal map: encode
    Nn = np.zeros((n, n, 3))
    nx_, ny_ = N[..., 0], N[..., 1]
    nz_ = np.sqrt(np.clip(1 - nx_ * nx_ - ny_ * ny_, 0, 1))
    Nn[..., 0], Nn[..., 1], Nn[..., 2] = 0.5 + 0.5 * nx_, 0.5 + 0.5 * ny_, 0.5 + 0.5 * nz_
    return Cc, Nn, Ee


def st_smoke_img(n=256, hard=False, border=False, opaque=False, ball=False, faint=False):
    yy, xx = np.mgrid[0:n, 0:n].astype(float)
    cx, cy = n / 2, n / 2
    sx, sy = (28.0, 28.0) if ball else (28.0, 62.0)
    g = np.exp(-(((xx - cx) / sx) ** 2 + ((yy - cy) / sy) ** 2) / 2.0)
    a = 0.5 * g
    if opaque:
        a = 0.85 * g
    if faint:
        a = 0.06 * g
    if hard:
        a = np.where(g > 0.22, 0.5, 0.0)
    edge = np.minimum.reduce([xx, yy, n - 1 - xx, n - 1 - yy])
    if not border:
        a = a * np.clip(edge / 12.0, 0, 1)
    else:
        a = np.maximum(a, 0.12)
    out = np.ones((n, n, 4))
    out[..., :3] = 0.86
    out[..., 3] = a
    return out


def st_card_img(n_w, n_h, seed, mean=0.5):
    rng = np.random.default_rng(seed)
    f = st_noise(max(n_w, n_h), rng, 0.8, 2, 30)[:n_h, :n_w]
    out = np.ones((n_h, n_w, 4))
    out[..., :3] = 0.9
    out[..., 3] = np.clip(mean + 0.22 * f, 0, 1)
    return out


ST_SWATCH = {"green": (0.25, 0.25), "purple": (0.75, 0.25), "olive": (0.25, 0.75), "brown": (0.75, 0.75)}


def st_textures(look):
    os.makedirs(look, exist_ok=True)
    files = set()
    for m, (tile, cf, nf, ef) in LK.items():
        files |= {f for f in (cf, nf, ef) if f}
    seed = 100
    for f in sorted(files):
        stem, suf = f.rsplit("_", 1)
        seed += 1
        if f == "Plants_C":
            n = 256
            yy, xx = np.mgrid[0:n, 0:n] / n
            a = np.ones((n, n, 4))
            sw = np.zeros((n, n, 3))
            cols = [(0.2, 0.6, 0.2), (0.55, 0.22, 0.75), (0.45, 0.45, 0.2), (0.4, 0.28, 0.15)]
            for k, c in enumerate(cols):
                sx, sy = (k % 2) * 0.5, (k // 2) * 0.5
                for j in range(4):                                  # 16 swatches: 4 colours x 4 shades
                    m = (xx >= sx + (j % 2) * 0.25) & (xx < sx + (j % 2) * 0.25 + 0.25) & (yy >= sy + (j // 2) * 0.25) & (yy < sy + (j // 2) * 0.25 + 0.25)
                    sw[m] = np.array(c) * (0.7 + 0.1 * j)
            rng = np.random.default_rng(5)
            a[..., :3] = sw + 0.02 * rng.normal(0, 1, (n, n, 1))
        elif f == "Lava_C" or f == "Lava_E":
            C_, E_ = st_lava_flow()
            a = np.ones((512, 512, 4))
            a[..., :3] = C_ if f == "Lava_C" else E_
        elif f == "Basalt_E" or f == "Basalt_N":
            C_, N_, E_ = st_basalt_maps()
            a = np.ones((512, 512, 4))
            a[..., :3] = E_ if f == "Basalt_E" else N_
        elif f == "Smoke_C":
            a = st_smoke_img()
        elif f == "Surf_C":
            a = st_card_img(256, 256, seed, 0.48)
        elif f == "Fall_C":
            a = st_card_img(256, 256, seed, 0.62)
        elif suf == "C":
            nn_ = 512 if stem in ST_GROUND else 256
            a = np.ones((nn_, nn_, 4))
            a[..., :3] = st_albedo_img(stem, nn_)
        elif suf == "N":
            nn_ = 512 if stem in ST_GROUND else 256
            a = np.ones((nn_, nn_, 4))
            a[..., :3] = st_normal_img(nn_, seed)
        else:                                                         # a generic emission mask not covered above
            a = np.ones((128, 128, 4))
            a[..., :3] = 0.0
            a[10:14, 10:14, :3] = 1.0
        st_png(os.path.join(look, f + ".png"), a)


def st_material(name, look):
    tile, cf, nf, ef = LK[name]
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    if m.node_tree is None:
        m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(b.outputs["BSDF"], out.inputs["Surface"])

    def tex(f, non_color=False):
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(os.path.join(look, f + ".png"), check_existing=True)
        if non_color:
            t.image.colorspace_settings.name = "Non-Color"
        return t
    if cf:
        nt.links.new(tex(cf).outputs["Color"], b.inputs["Base Color"])
    if nf:
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tex(nf, True).outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    if ef:
        nt.links.new(tex(ef).outputs["Color"], b.inputs["Emission Color"])
        b.inputs["Emission Strength"].default_value = 1.0
    return m


def st_box_uv(me, M=None):
    """Per-face box projection in tile units of each face's material (M = 4x4 applied first, e.g. world for ground)."""
    if not len(me.uv_layers):
        me.uv_layers.new(name="UVMap")
    nv = len(me.vertices)
    co = np.empty(nv * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    if M is not None:
        Mn = np.array(M)
        co = co @ Mn[:3, :3].T + Mn[:3, 3]
    npl = len(me.polygons)
    ls = np.empty(npl, np.int64)
    lt = np.empty(npl, np.int64)
    mi = np.empty(npl, np.int64)
    nrm = np.empty(npl * 3)
    me.polygons.foreach_get("loop_start", ls)
    me.polygons.foreach_get("loop_total", lt)
    me.polygons.foreach_get("material_index", mi)
    me.polygons.foreach_get("normal", nrm)
    nrm = np.abs(nrm.reshape(-1, 3))
    names = [m.name if m else "" for m in me.materials] or [""]
    tiles = np.array([LK[n][0] if n in LK and isinstance(LK[n][0], float) else 4.0 for n in names])
    tl = tiles[np.clip(mi, 0, len(names) - 1)]
    ax = nrm.argmax(axis=1)
    lv = np.empty(len(me.loops), np.int64)
    me.loops.foreach_get("vertex_index", lv)
    pidx = np.repeat(np.arange(npl), lt)
    P = co[lv]
    a = ax[pidx]
    t = tl[pidx]
    u = np.where(a == 0, P[:, 1], P[:, 0])
    v = np.where(a == 2, P[:, 1], P[:, 2])
    me.uv_layers[0].data.foreach_set("uv", np.stack([u / t, v / t], 1).ravel())


def st_atlas_uv(me, swatch):
    if not len(me.uv_layers):
        me.uv_layers.new(name="UVMap")
    cx, cy = ST_SWATCH[swatch]
    rng = np.random.default_rng(len(me.loops))
    uv = np.stack([cx + rng.uniform(-.08, .08, len(me.loops)), cy + rng.uniform(-.08, .08, len(me.loops))], 1)
    me.uv_layers[0].data.foreach_set("uv", uv.ravel())


def st_mesh(name, verts, faces, mat, uv="box", look=None):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(map(float, v)) for v in verts], [], [tuple(int(i) for i in f) for f in faces])
    me.update()
    me.materials.append(bpy.data.materials[mat])
    if uv == "box":
        st_box_uv(me)
    elif uv in ST_SWATCH:
        st_atlas_uv(me, uv)
    return me


def st_bm_mesh(name, bm, mat, uv="box"):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(bpy.data.materials[mat])
    if uv == "box":
        st_box_uv(me)
    elif uv in ST_SWATCH:
        st_atlas_uv(me, uv)
    return me


def st_sway(me, kind, tip=None, power=1.0, b=0.0, a=1.0, root_one=False, extra_layer=False):
    """Give a synthetic plant mesh the sway colours of the contract (RUNTIME.md "v2 2026-10-05 area U"): ONE attribute "Col" (FLOAT_COLOR, point), R = tip x t^power with t = the distance from the
    pinned end (vine: the top) over the height, G = a per-vertex phase, B = 0, A = 1. The keyword arguments are the mutations of the sway gates."""
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    z = co.reshape(-1, 3)[:, 2]
    h = max(float(z.max() - z.min()), 1e-6)
    t = (z.max() - z) / h if kind == "vine" else (z - z.min()) / h
    tw = {"tuft": 1.0, "flower": 0.80, "shrub": 0.48, "tree": 0.30, "vine": 1.0}.get(kind, 1.0) if tip is None else tip
    r = np.full(len(z), tw) if root_one else tw * np.power(t, power)
    for nm in [x.name for x in me.color_attributes]:
        me.color_attributes.remove(me.color_attributes[nm])
    att = me.color_attributes.new(name="Col", type="FLOAT_COLOR", domain="POINT")
    g = np.random.default_rng(len(z)).uniform(0, 1, len(z))
    att.data.foreach_set("color", np.stack([r, g, np.full(len(z), b), np.full(len(z), a)], 1).astype(np.float32).ravel())
    me.color_attributes.active_color_name = "Col"
    me.color_attributes.render_color_index = 0
    if extra_layer:
        ex = me.color_attributes.new(name="Col2", type="FLOAT_COLOR", domain="POINT")
        ex.data.foreach_set("color", np.ones(len(z) * 4, np.float32))
        me.color_attributes.active_color_name = "Col"
        me.color_attributes.render_color_index = 0


def st_fit(new, old):
    """Scale/shift the vertices of `new` to the local bbox of `old` (a swapped library mesh keeps its size), then re-UV."""
    co = np.empty(len(new.vertices) * 3)
    new.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    oc = np.empty(len(old.vertices) * 3)
    old.vertices.foreach_get("co", oc)
    oc = oc.reshape(-1, 3)
    nlo, nhi, olo, ohi = co.min(0), co.max(0), oc.min(0), oc.max(0)
    co = (co - (nlo + nhi) / 2) * ((ohi - olo) / np.maximum(nhi - nlo, 1e-9)) + (olo + ohi) / 2
    new.vertices.foreach_set("co", co.ravel())
    new.update()
    st_box_uv(new)


def st_obj(name, me, loc=(0, 0, 0), scale=(1, 1, 1), rot_z=0.0, coll="ENVIRONMENT", root=None):
    ob = bpy.data.objects.new(name, me)
    (bpy.data.collections.get(coll) or bpy.context.scene.collection).objects.link(ob)
    if root is not None:
        ob.parent = root
    ob.location = Vector(loc)
    ob.scale = Vector(scale)
    ob.rotation_euler = (0.0, 0.0, rot_z)
    return ob


# ----------------------------------------------------------------------------- synthetic geometry generators
def st_hull_rock(name, seed, mat="LK_ROCK", dims=(2.0, 1.6, 1.2), hulls=4, pts=22):
    """A faceted rock the way the props library makes them: several overlapping convex hulls of random points (>= 60 welded vertices, >= 100
    faces, irregular big facets, nothing like an icosphere)."""
    rng = np.random.default_rng(seed)
    bm = bmesh.new()
    d = np.array(dims, float)
    for _h in range(hulls):
        c = rng.uniform(-0.35, 0.35, 3) * d
        P = rng.normal(0, 1, (pts, 3))
        P /= np.linalg.norm(P, axis=1)[:, None]
        P = P * rng.uniform(0.55, 1.0, (pts, 1)) * d * rng.uniform(0.45, 0.75) + c
        vs = [bm.verts.new(tuple(p)) for p in P]
        res = bmesh.ops.convex_hull(bm, input=vs, use_existing_faces=True)
        dead = list(res.get("geom_interior", [])) + list(res.get("geom_unused", []))
        dead = list({g for g in dead if isinstance(g, bmesh.types.BMVert)})
        if dead:
            bmesh.ops.delete(bm, geom=dead, context="VERTS")
    return st_bm_mesh(name, bm, mat)


def st_spire(name, seed, height=21.0, r0=3.4, top_r=0.07, mat="LK_ROCK", rings=16, sides=8, jag=0.07, flat_top=0.0, below=3.0):
    """A tapering sea stack: stacked jittered polygon rings from -below (under the sea) to height; flat_top > 0 puts a wide flat lid on it."""
    rng = np.random.default_rng(seed)
    V, F = [], []
    for k in range(rings):
        t = k / (rings - 1)
        z = -below + (height + below) * t
        r = r0 * (1.0 - (1.0 - top_r) * t ** 0.85)
        if flat_top and t > 0.93:
            r = max(r, r0 * flat_top)
        rot = 0.5 * t + 0.1 * rng.normal()
        for s in range(sides):
            a = 2 * math.pi * s / sides + rot
            rr = r * (1.0 + jag * rng.normal())
            V.append((rr * math.cos(a), rr * math.sin(a), z))
    for k in range(rings - 1):
        for s in range(sides):
            a, b = k * sides + s, k * sides + (s + 1) % sides
            F.append((a, b, b + sides, a + sides))
    F.append(tuple(range(sides - 1, -1, -1)))
    F.append(tuple((rings - 1) * sides + s for s in range(sides)))
    return st_mesh(name, V, F, mat)


def st_box_stack(name, mat="LK_ROCK"):
    """Two stacked boxes: the shape the reviewer saw ('stacked boxes') with 100+ faces so only the TAPER gate can fail it."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((0, 0, 4.0)) @ Matrix.Diagonal((7, 6, 14, 1)))
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((0.8, 0.5, 14.0)) @ Matrix.Diagonal((5, 4.5, 12, 1)))
    bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=2, use_grid_fill=True)
    return st_bm_mesh(name, bm, mat)


def st_shift_to_ground(me):
    """Translate a mesh so its lowest vertex is at z = 0 (plants stand on their origin)."""
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    co[:, 2] -= co[:, 2].min()
    me.vertices.foreach_set("co", co.ravel())
    me.update()


def st_cols(name, mat, ncol=15, r=0.16, seed=0):
    """A cluster of separate hexagonal prisms on a hex lattice (>= 100 faces, >= 60 vertices, tops 0.2..1, base z = -1)."""
    rng = np.random.default_rng(seed)
    ap = r * math.sqrt(3) / 2
    centres = [(0.0, 0.0)]
    ring = 1
    while len(centres) < ncol:
        for k in range(6 * ring):
            if len(centres) >= ncol:
                break
            a = 2 * math.pi * k / (6 * ring)
            centres.append(((2 * ap + 0.02) * ring * math.cos(a), (2 * ap + 0.02) * ring * math.sin(a)))
        ring += 1
    V, F = [], []
    for cx, cy in centres:
        top = rng.uniform(0.2, 1.0)
        b = len(V)
        for z in (-1.0, top):
            for k in range(6):
                a = math.radians(60 * k)
                V.append((cx + r * math.cos(a), cy + r * math.sin(a), z))
        for k in range(6):
            F.append((b + k, b + (k + 1) % 6, b + 6 + (k + 1) % 6, b + 6 + k))
        F.append(tuple(b + 6 + k for k in range(6)))
        F.append(tuple(b + 5 - k for k in range(6)))
    return st_mesh(name, V, F, mat)


def st_wall_cladding(name, terr_ob, out=0.12, cap=0.15):
    """Cladding that hides every non-top face of a TERRAIN mesh: a copy of those faces pushed `out` metres along their vertex normals, its top
    edge lifted `cap` metres over the lip."""
    bm = bmesh.new()
    bm.from_mesh(terr_ob.data)
    bm.transform(terr_ob.matrix_world)
    bm.normal_update()
    dead = [f for f in bm.faces if f.normal.z > 0.5]
    bmesh.ops.delete(bm, geom=dead, context="FACES")
    for v in [v for v in bm.verts if not v.link_faces]:
        bm.verts.remove(v)
    bm.normal_update()
    for v in bm.verts:
        n = Vector((0, 0, 0))
        for f in v.link_faces:
            n += f.normal * f.calc_area()
        if n.length > 1e-9:
            v.co += n.normalized() * out
        if v.co.z >= 5.99:
            v.co.z += cap                                      # the cladding caps the lip: steep views over its edge still meet it
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(bpy.data.materials["LK_BASALT"])
    return me


def st_strip(name, P, N, o0, o1, z0, z1=None, mat="LK_SURF", vcol=False, uv_world=None):
    """A strip along P (offsets o0..o1 along N) at height z0 (flat) or a vertical curtain z0..z1 at offset o0."""
    V, F = [], []
    for i, (p, nrm) in enumerate(zip(P, N)):
        if z1 is None:
            V += [(p[0] + nrm[0] * o0, p[1] + nrm[1] * o0, z0), (p[0] + nrm[0] * o1, p[1] + nrm[1] * o1, z0)]
        else:
            V += [(p[0] + nrm[0] * o0, p[1] + nrm[1] * o0, z0), (p[0] + nrm[0] * o0, p[1] + nrm[1] * o0, z1)]
        if i:
            b = 2 * (i - 1)
            F.append((b, b + 2, b + 3, b + 1) if z1 is None else (b, b + 1, b + 3, b + 2))
    me = st_mesh(name, V, F, mat, uv="box" if uv_world is None else None)
    if uv_world is not None:
        me.uv_layers.new(name="UVMap")
        lv = np.empty(len(me.loops), np.int64)
        me.loops.foreach_get("vertex_index", lv)
        Vv = np.array(V)[lv]
        me.uv_layers[0].data.foreach_set("uv", (Vv[:, :2] / uv_world).ravel())
    if vcol:
        ca = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
        cols = []
        for i in range(len(V)):
            inner = i % 2 == 0
            cols += [1.0, (i * 0.37) % 1.0, 0.0, 1.0 if inner else 0.0]
        ca.data.foreach_set("color", cols)
    # flat strips must face up (+z)
    me.update()
    if z1 is None and len(me.polygons) and me.polygons[0].normal.z < 0:
        me.flip_normals()
    return me


def st_ctx(hole, opts):
    design, dpath = load_design(hole, opts.get("design"))
    return Ctx(hole, design, dpath, opts)


def st_candidates(C, S, want, rng, kind="rough", side=None, clear=0.0):
    """Prop spots (metres, at play height) on rough land, off the play surfaces, verified by the ground ray; `clear` yards of extra margin."""
    lo, hi = C.shore_yd.min(0), C.shore_yd.max(0)
    X, Y = np.meshgrid(np.arange(lo[0], hi[0], 1.5), np.arange(lo[1], hi[1], 1.5), indexing="ij")
    P = np.stack([X.ravel(), Y.ravel()], 1) + rng.uniform(-.5, .5, (X.size, 2))
    ok = C.dry_yd(P[:, 0], P[:, 1])
    dc = dist_polyline(P, C.center_yd)
    ok &= dc > C.fw / 2 + 1.5 + clear
    ok &= np.hypot(P[:, 0] - C.pin_yd[0], P[:, 1] - C.pin_yd[1]) > C.gr + 4 + clear
    ok &= np.hypot(P[:, 0], P[:, 1]) > 8 + clear
    for b in C.bunkers:
        ok &= ((P[:, 0] - b["x"]) / (b["width"] * .8 + clear)) ** 2 + ((P[:, 1] - b["d"]) / (b["length"] * .8 + clear)) ** 2 > 1
    ok &= dist_polyline(P, np.vstack([C.shore_yd, C.shore_yd[:1]])) > 1.5 + clear
    if side is not None:
        _rc, _bc, rside, _dc, _ds = split_regions(C, P)
        ok &= rside if side == "ridge" else ~rside
    P = P[ok]
    rng.shuffle(P)
    out = []
    for x, y in (P * YD).tolist():
        hit, z = on_land(S, C, x, y)
        if hit and (hit.startswith("TERRAIN") or hit in ("FAIRWAY_FIRSTCUT", "GREEN_APRON")):
            out.append((x, y, C.pz))
        if len(out) >= want:
            break
    return out


def st_curtain(name, P, N, offs, z0, z1, mat="LK_CLIFF"):
    """A vertical curtain along the shore samples P at per-sample outward offsets `offs` (relief), from z0 up to z1, facing the water."""
    V, F = [], []
    for i, (p, nrm, o) in enumerate(zip(P, N, offs)):
        V += [(p[0] + nrm[0] * o, p[1] + nrm[1] * o, z0), (p[0] + nrm[0] * o, p[1] + nrm[1] * o, z1)]
        if i:
            b = 2 * (i - 1)
            F.append((b, b + 1, b + 3, b + 2))
    return st_mesh(name, V, F, mat)


def st_shelf(P, N, widths, gaps_every=100, gap_len=12, name="WATER_SHELF", lid=None):
    """A flat WATER_SHELF (z .06) along the shore: per sample from 1.0 m under the cliff out to `widths[i]` m, broken by a gap every gaps_every samples."""
    V, F, started = [], [], False
    keep = [(i % gaps_every) >= gap_len for i in range(len(P))]
    prev = None
    for i, (p, nrm, w) in enumerate(zip(P, N, widths)):
        if not keep[i] or (lid is not None and i and lid[i] != lid[i - 1]):
            prev = None
            if not keep[i]:
                continue
        a = (p[0] - nrm[0] * 1.0, p[1] - nrm[1] * 1.0, 0.06)
        b = (p[0] + nrm[0] * w, p[1] + nrm[1] * w, 0.06)
        base = len(V)
        V += [a, b]
        if prev is not None:
            F.append((prev, prev + 1, base + 1, base))
        prev = base
    me = st_mesh(name, V, F, "LK_WATER_SHALLOW", uv=None)
    me.update()
    if len(me.polygons) and me.polygons[0].normal.z < 0:
        me.flip_normals()
    return me


def split_by_loop(idx, lid):
    """Split an index array into runs that stay on one shoreline loop."""
    idx = np.asarray(idx)
    if len(idx) == 0:
        return []
    cut = np.flatnonzero(np.diff(lid[idx]) != 0) + 1
    return np.split(idx, cut)


def st_flat_line(C, S, rng, length_m):
    """A straight line (metres) over ONE flat surface (same mesh, same height) with 1.5 m clear on both sides: where a stone path can lie 8 mm
    over the ground (the Needle play surfaces sit at different heights: rough 6.00, first cut 6.08, fairway 6.14, ...)."""
    starts = [(x, y) for x, y, _z in play_samples(S, C, 3.0)]
    rng.shuffle(starts)
    for x, y in starts[:400]:
        for _k in range(16):
            a = float(rng.uniform(0, 2 * math.pi))
            d = np.array([math.cos(a), math.sin(a)])
            n = np.array([-d[1], d[0]])
            ok, ref = True, None
            for t in np.linspace(0, length_m, 12):
                for w in (-1.5, 0.0, 1.5):
                    hit, hz = on_land(S, C, x + d[0] * t + n[0] * w, y + d[1] * t + n[1] * w)
                    if hit is None or (ref is not None and (hit != ref[0] or abs(hz - ref[1]) > 5e-3)):
                        ok = False
                        break
                    ref = ref or (hit, hz)
                if not ok:
                    break
            if ok:
                return np.array([(x, y), (x + d[0] * length_m, y + d[1] * length_m)])
    raise RuntimeError("no flat line for the path")


def st_set_scale(ob, s):
    ob.scale = Vector((s, s, s))


def st_build(hole, root_dir, look, opts):
    """Synthetic positive: the baseline blend + LK materials, UVs, rocks, plants, dressing that satisfy every gate."""
    tag = f"{hole:02d}"
    bpy.ops.wm.open_mainfile(filepath=os.path.join(BASE_DIR, "blender", f"hole_{tag}.blend"), load_ui=False)
    C = st_ctx(hole, dict(opts, look_dir=look))
    for m in LK:
        st_material(m, look)
    root = bpy.data.objects.get(f"HOLE_{tag}_ROOT")
    pz = C.pz
    remap = {"MAT_FAIRWAY": "LK_FAIRWAY", "MAT_FAIRWAY_STRIPE": "LK_FAIRWAY", "MAT_FIRSTCUT": "LK_FAIRWAY", "MAT_GREEN": "LK_GREEN",
             "MAT_BUNKER_LIP": "LK_GREEN", "MAT_SAND": "LK_SAND", "MAT_ROUGH": "LK_ROUGH", "MAT_WATER": "LK_WATER",
             "MAT_WATER_SHALLOW": "LK_WATER_SHALLOW", "MAT_LAVA": "LK_LAVA",
             "MAT_CLIFF": "LK_BASALT" if hole == 10 else "LK_CLIFF", "MAT_CLIFF_DARK": "LK_BASALT" if hole == 10 else "LK_CLIFF_DARK",
             "MAT_ROCK": "LK_BASALT" if hole == 10 else "LK_ROCK", "MAT_ROCK_DARK": "LK_BASALT" if hole == 10 else "LK_ROCK"}
    for n in ("WATER_FOAM", "WATER_FOAM_CREST"):
        if n in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[n])
    if "WATER_SHALLOW" in bpy.data.objects:
        bpy.data.objects["WATER_SHALLOW"].name = "WATER_SHELF"
    for me in bpy.data.meshes:
        for i, m in enumerate(me.materials):
            if m is not None and m.name in remap:
                me.materials[i] = bpy.data.materials[remap[m.name]]
    # terrain: per-face materials (tops rough / scrub on Split's ridge; walls cliff / basalt below the lip band)
    for ob in [o for o in bpy.data.objects if o.name.startswith("TERRAIN") and o.type == "MESH"]:
        me = ob.data
        me.materials.clear()
        slots = ["LK_ROUGH", "LK_BASALT"] if hole == 10 else ["LK_ROUGH", "LK_CLIFF", "LK_CLIFF_DARK", "LK_SCRUB"]
        for s in slots:
            me.materials.append(bpy.data.materials[s])
        npl = len(me.polygons)
        nrm = np.empty(npl * 3)
        cen = np.empty(npl * 3)
        me.polygons.foreach_get("normal", nrm)
        me.polygons.foreach_get("center", cen)
        nrm, cen = nrm.reshape(-1, 3), cen.reshape(-1, 3)
        mi = np.zeros(npl, np.int64)
        wall = nrm[:, 2] <= 0.5
        lip = wall & (nrm[:, 2] >= -0.5) & (cen[:, 2] >= pz - LIP_BAND)
        if hole == 10:
            mi[wall & ~lip] = 1
        else:
            mi[wall & ~lip] = 1
            mi[wall & ~lip & (cen[:, 2] < 1.5)] = 2
            if hole == 9:
                top = ~wall
                _rc, _bc, side, _dc, _ds = split_regions(C, cen[:, :2] / YD)
                mi[top & side] = 3
        me.polygons.foreach_set("material_index", mi)
        me.update()
    # ---- rocks: the legacy 12-vertex icosahedra are replaced by faceted hull rocks (curated placements, scale 1) and column clusters
    rock_mat = "LK_BASALT" if hole == 10 else "LK_ROCK"
    legacy = ("ROCK_LARGE", "ROCK_MEDIUM", "ROCK_SMALL", "CLIFF_ROCK")
    for ob in [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("ROCK_") and base_name(o.name).split(".")[0] in legacy]:
        bpy.data.objects.remove(ob)
    for ob in [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("CLIFF_ROCK")]:
        bpy.data.objects.remove(ob)
    cols = {"ROCK_WALL_BLUNT": st_cols("COL_A", "LK_BASALT", 15, seed=1), "ROCK_WALL_FLAT": st_cols("COL_B", "LK_BASALT", 19, seed=2)} if hole == 10 else {}
    for old, new in cols.items():
        if old in bpy.data.meshes:
            st_fit(new, bpy.data.meshes[old])
    for ob in list(bpy.data.objects):
        if ob.type == "MESH" and ob.data.name in cols:
            sc = min(2.0, max(0.5, float(np.mean(ob.scale))))
            ob.data = cols[ob.data.name]
            st_set_scale(ob, sc)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)
    # every remaining mesh gets box UVs in tile units (ground in world space)
    for ob in bpy.data.objects:
        if ob.type == "MESH" and ob.data.users == 1 and not ob.name.startswith(("WATER_OCEAN", "WATER_SHELF")):
            st_box_uv(ob.data, ob.matrix_world if ob.name.startswith(COLL_PREFIXES) else None)
    rocks = [st_hull_rock(f"RK_{k}", 40 + k, rock_mat, dims=(1.6 + 0.2 * k, 1.4 + 0.15 * k, 1.1), hulls=4 + (k % 3)) for k in range(5)]
    for me in rocks:
        st_box_uv(me)
    S = Snap("blend", C)
    rng = np.random.default_rng(hole)
    n = 0

    def put(me, spots, base, scale=1.0):
        nonlocal n
        out = []
        for p in spots:
            o = st_obj(f"{base}.{n:03d}", me, p, scale=(scale, scale, scale), rot_z=float(rng.uniform(0, 6.28)), root=root)
            out.append(o)
            n += 1
        return out
    # curated loose rocks on the rough, well off the play surfaces (>= 4 shapes, each instanced >= 2 times)
    rs = []
    for clr in (14.0, 8.0, 4.0, 2.0, 1.0):
        rs = st_candidates(C, S, 30, rng, clear=clr)
        if len(rs) >= 20:
            break
    print(f"SELFTEST: hole {hole} curated rocks: {len(rs)} spots (clear {clr} yd)", flush=True)
    for k, p in enumerate(rs):
        put(rocks[k % 5], [p], f"ROCK_B{k % 5}", scale=1.0 + 0.3 * (k % 3))
    # plants: one library mesh per kind, instanced, standing ON their origin
    tuft = st_mesh("PLANT_TUFT_A", [(0, 0, 0)] + [(0.15 * math.cos(a), 0.15 * math.sin(a), 0.35) for a in np.linspace(0, 6.28, 9)[:-1]] +
                   [(0.05 * math.cos(a), 0.05 * math.sin(a), 0) for a in np.linspace(0, 6.28, 9)[:-1]],
                   [(0, 1 + k, 9 + k) for k in range(8)], "LK_PLANTS", uv="green")
    shrub = st_hull_rock("PLANT_SHRUB_A", 11, "LK_PLANTS", dims=(1.4, 1.4, 1.0), hulls=1, pts=40)
    st_shift_to_ground(shrub)
    st_atlas_uv(shrub, "green")
    flower = st_hull_rock("PLANT_FLOWER_PURPLE", 12, "LK_PLANTS", dims=(0.5, 0.5, 0.6), hulls=1, pts=30)
    st_shift_to_ground(flower)
    st_atlas_uv(flower, "purple")
    vine = st_mesh("PLANT_VINE_M", [(-.15, 0, -3 + 0.5 * k) for k in range(7)] + [(.15, 0, -3 + 0.5 * k) for k in range(7)],
                   [(k, k + 1, 8 + k, 7 + k) for k in range(6)], "LK_PLANTS", uv="green")
    for me_, kind_ in ((tuft, "tuft"), (shrub, "shrub"), (flower, "flower"), (vine, "vine")):       # v2 2026-10-05 (area U): plants carry the sway colours
        st_sway(me_, kind_)
    if hole == 8:
        sp = st_candidates(C, S, 90, rng)
        put(tuft, sp[:50], "PLANT_TUFT_A")
        put(flower, sp[50:75], "PLANT_FLOWER_PURPLE")
        put(shrub, sp[75:85], "PLANT_SHRUB_A")
    elif hole == 9:
        put(shrub, st_candidates(C, S, 20, rng, side="ridge"), "PLANT_SHRUB_A")
        put(tuft, st_candidates(C, S, 25, rng, side="ridge"), "PLANT_TUFT_A")
        put(tuft, st_candidates(C, S, 15, rng, side="ribbon"), "PLANT_TUFT_A")
    else:
        sp = st_candidates(C, S, 16, rng)
        put(shrub, sp[:8], "PLANT_SHRUB_A")
        put(tuft, sp[8:], "PLANT_TUFT_A")
    # sea dressing (Needle / Split): broken surf patches, relief rock skin, a thin ragged shelf (all along the real TERRAIN shoreline loops)
    P = N = LID = None
    if hole in (8, 9):
        P, N, _Tg, _s, _tot, LID = terrain_shore_frame(S, C, 2.0)
        gp = np.array(C.pin_yd) * YD
        iw = int(np.argmin(np.hypot(P[:, 0] - gp[0], P[:, 1] - gp[1]))) if hole == 8 else -10 ** 6
        starts = [i0 for i0 in range(3, len(P) - 6, 20) if abs(i0 - iw) > 12]
        if hole == 8:
            starts.append(max(0, iw - 1))                   # the waterfall foot stands in its own whitewater patch
        k = 0
        for i0 in starts:
            idx = np.arange(i0, i0 + 4)
            if len(set(LID[idx].tolist())) != 1:
                continue
            k += 1
            me = st_strip(f"WATER_SURF_{k:02d}", P[idx], N[idx], 0.3, 4.0, 0.12, mat="LK_SURF", vcol=True, uv_world=8.0)
            st_obj(f"WATER_SURF_{k:02d}", me, root=root)
        frac = 0.35 if hole == 8 else 0.65
        ids = np.arange(int(len(P) * frac))
        k = 0
        for idx in np.array_split(ids, 6):
            for sub in split_by_loop(idx, LID):
                if len(sub) < 3:
                    continue
                k += 1
                offs = 0.4 + 0.5 * (0.5 + 0.5 * np.sin(0.7 * sub))
                me = st_curtain(f"ROCK_SKIN_{k:02d}", P[sub], N[sub], offs, 0.2, pz - 0.4)
                st_obj(f"ROCK_SKIN_{k:02d}", me, root=root)
        widths = 1.0 + 1.0 * (0.5 + 0.5 * np.sin(0.31 * np.arange(len(P))))
        if "WATER_SHELF" in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects["WATER_SHELF"])
        st_obj("WATER_SHELF", st_shelf(P, N, widths, lid=LID), root=root)
    if hole == 8:
        # path on the tee pad (0.8 cm above the ground: under the ball)
        line = st_flat_line(C, S, rng, 17.0)
        pts = sample_polyline(line, 1.5, closed=False)
        d = np.gradient(pts, axis=0)
        d /= np.linalg.norm(d, axis=1)[:, None]
        nn = np.stack([-d[:, 1], d[:, 0]], 1)
        me = st_strip("DRESS_PATH_01", pts - nn * 1.3, nn, 0.0, 2.6, pz + 0.008, mat="LK_PATH")
        for v in me.vertices:                                          # follow the real play-surface heights (the fairway is not at PLAY_Z exactly)
            hit, hz = on_land(S, C, v.co.x, v.co.y)
            if hit is not None:
                v.co.z = hz + 0.008
        me.update()
        st_obj("DRESS_PATH_01", me, coll="STRUCTURES", root=root)
        # arch: 15 cut blocks, a pedestal rock under them, vines on it
        a = C.sc["arch"]
        ax, ay, z0 = a["x"] * YD, a["d"] * YD, a["base_top_m"]
        bm = bmesh.new()
        for side in (-1, 1):
            for k in range(6):
                bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((ax + side * 3.0, ay, z0 + 0.5 + 1.05 * k)))
        for k in range(3):
            bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((ax - 1.05 + 1.05 * k, ay, z0 + 6.8)))
        st_obj("DRESS_ARCH_01", st_bm_mesh("DRESS_ARCH_01", bm, "LK_MASONRY"), coll="STRUCTURES", root=root)
        ped = st_hull_rock("RK_PED", 77, "LK_ROCK", dims=(9.0, 6.0, 2.6), hulls=6)
        st_box_uv(ped)
        st_obj("ROCK_PEDESTAL.000", ped, (ax, ay, z0 - 1.2), root=root)
        for k in range(8):
            st_obj(f"PLANT_VINE_M.{900 + k:03d}", vine, (ax + (-3.0 if k % 2 else 3.0), ay - 0.6, z0 + 6.0 - 0.5 * (k // 2)), root=root)
        dry_in = C.dry_m(P[:, 0] - N[:, 0] * 1.0, P[:, 1] - N[:, 1] * 1.0)
        cand = [i for i in range(5, len(P), max(1, len(P) // 40)) if dry_in[i] and on_land(S, C, float(P[i][0] - N[i][0] * 1.0), float(P[i][1] - N[i][1] * 1.0))[0]]
        for k, i in enumerate(cand[:8]):
            st_obj(f"PLANT_VINE_M.{700 + k:03d}", vine, (P[i][0] + N[i][0] * 0.3, P[i][1] + N[i][1] * 0.3, pz - 0.1), root=root)
        # waterfall off a shore point of the green pad, its foot in the surf
        i = iw
        p, nrm = P[i], N[i]
        tdir = np.array([-nrm[1], nrm[0]])
        V, F = [], []
        for r in range(7):
            for c in range(5):
                V.append((p[0] + nrm[0] * 0.8 + tdir[0] * (c - 2), p[1] + nrm[1] * 0.8 + tdir[1] * (c - 2), pz - pz * r / 6))
        for r in range(6):
            for c in range(4):
                F.append((r * 5 + c, r * 5 + c + 1, (r + 1) * 5 + c + 1, (r + 1) * 5 + c))
        me = st_mesh("WATER_FALL_01", V, F, "LK_FALL", uv=None)
        me.uv_layers.new(name="UVMap")
        lv = np.empty(len(me.loops), np.int64)
        me.loops.foreach_get("vertex_index", lv)
        Vv = np.array(V)[lv]
        me.uv_layers[0].data.foreach_set("uv", np.stack([(np.arange(len(lv)) % 5) / 4.0, (pz - Vv[:, 2]) / pz], 1).ravel())
        st_obj("WATER_FALL_01", me, root=root)
        # sea stacks: tapering spires standing in the water
        for k, s in enumerate(C.sc["stacks"][:5]):
            h = min(24.0, float(s["height_yd"]) * YD)
            sp_ = st_spire(f"RK_STACK{k}", 21 + k, height=h, r0=0.5 * float(s["radius_yd"]) * YD * 1.5)
            st_box_uv(sp_)
            st_obj(f"ROCK_SEASTACK_A.{k:03d}", sp_, (s["x"] * YD, s["d"] * YD, 0.0), root=root)
    if hole == 9:
        w = C.sc["ruin"]["walls"]
        bm = bmesh.new()
        k = 0
        for wl in w:
            p0, p1 = np.array(wl["p0"]) * YD, np.array(wl["p1"]) * YD
            for t in np.linspace(0.1, 0.9, 4):
                q = p0 + (p1 - p0) * t
                bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((q[0], q[1], pz + 0.5 + (k % 3) * 1.05)))
                k += 1
        st_obj("DRESS_RUIN_01_00", st_bm_mesh("DRESS_RUIN_01_00", bm, "LK_MASONRY"), coll="STRUCTURES", root=root)
        for ob in [o for o in bpy.data.objects if o.name.startswith("ROCK_STACK_") and o.type == "MESH"]:      # the baseline's boxy custom stacks
            d = np.array(ob.dimensions)
            bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
            cx_, cy_ = sum(v.x for v in bb) / 8.0, sum(v.y for v in bb) / 8.0
            new = st_spire(f"SP_{ob.name}", int(ob.name[-2:]), height=float(d[2]) * 0.9, r0=float(max(d[0], d[1])) * 0.28)
            st_box_uv(new)
            ob.data = new
            ob.matrix_world = Matrix.Translation((cx_, cy_, 0.0))
    if hole == 10:
        lava = bpy.data.objects["WATER_LAVA"]
        W = S.wld(lava)["W"]
        lo, hi = W.min(0), W.max(0)
        nx = 30
        xs, ys = np.linspace(lo[0], hi[0], nx + 1), np.linspace(lo[1], hi[1], nx + 1)
        V = [(x, y, 0.0) for y in ys for x in xs]
        F = [(j * (nx + 1) + i, j * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i) for j in range(nx) for i in range(nx)]
        me = st_mesh("WATER_LAVA_GRID", V, F, "LK_LAVA", uv=None)
        me.uv_layers.new(name="UVMap")
        lv = np.empty(len(me.loops), np.int64)
        me.loops.foreach_get("vertex_index", lv)
        me.uv_layers[0].data.foreach_set("uv", (np.array(V)[lv][:, :2] / 24.0).ravel())
        lava.data = me
        lava.matrix_world = Matrix.Identity(4)
        cx, cy = C.waters[0]["x"] * YD, C.waters[0]["d"] * YD
        for k, (dx, dy) in enumerate(((-30, 0), (30, 0), (0, -20), (0, 20))):
            e = bpy.data.objects.new(f"LAVA_LIGHT_{k + 1:02d}", None)
            bpy.data.collections["ENVIRONMENT"].objects.link(e)
            e.parent = root
            e.location = (cx + dx, cy + dy, 3.0)
        cr = C.sc["crater"]
        ccx, ccy = cr["cx"] * YD, cr["cd"] * YD
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=150.0, radius2=20.0, depth=180.0,
                              matrix=Matrix.Translation((0, 0, 90.0)))
        cone = st_bm_mesh("DRESS_CONE", bm, "LK_BASALT")
        st_obj("DRESS_CONE", cone, (ccx + 600.0, ccy + 300.0, 0.0), root=root)
        card = st_mesh("DRESS_SMOKE_CARD", [(-20, 0, 0), (20, 0, 0), (20, 0, 40), (-20, 0, 40)], [(0, 1, 2, 3)], "LK_SMOKE", uv=None)
        card.uv_layers.new(name="UVMap")
        card.uv_layers[0].data.foreach_set("uv", [0, 0, 1, 0, 1, 1, 0, 1])
        for k in range(2):
            st_obj(f"DRESS_SMOKE_{k + 1:02d}", card, (ccx + 600.0 + 30 * k, ccy + 300.0 - 20 * k, 170.0), scale=(1.0, 1.0, 1.0), rot_z=0.4, root=root)
        terr = next(o for o in bpy.data.objects if o.name.startswith("TERRAIN") and o.type == "MESH")
        st_obj("ROCK_SKIN_WALL", st_wall_cladding("ROCK_SKIN_WALL", terr), root=root)
    for ob in bpy.data.objects:
        if ob.parent is None and root is not None and ob is not root and ob.type in ("MESH", "EMPTY") and \
                any(c.name in EXPORT_COLLECTIONS for c in ob.users_collection):
            ob.parent = root
    blend = os.path.join(root_dir, f"hole_{tag}_pos.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend, copy=False)
    fbx = os.path.join(root_dir, f"hole_{tag}_pos.fbx")
    st_export(hole, fbx)
    return blend, fbx


def st_export(hole, fbx):
    vl = bpy.context.view_layer
    for ob in vl.objects:
        ob.select_set(False)
    objs, _ = export_set(f"HOLE_{hole:02d}_ROOT")
    for ob in objs:
        ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"MESH", "EMPTY"}, axis_forward="-Z", axis_up="Y",
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_anim=False, use_mesh_modifiers=True,
                             add_leaf_bones=False, path_mode="STRIP", embed_textures=False, colors_type="LINEAR")     # LINEAR = raw values, the way postcard_look_lib.export_look_fbx writes them (the sway weights are data, not colours)
    shutil.copyfile(os.path.join(BASE_DIR, "fbx", f"hole_{hole:02d}.fbx.meta"), fbx + ".meta")


# ---- negative mutations: (name, function(C), gates that MUST fail at the blend stage)
def _ob(prefix):
    """Exported mesh objects (never the hidden ASSET_LIBRARY originals) whose name starts with prefix."""
    lib = bpy.data.collections.get("ASSET_LIBRARY")
    libset = set(lib.all_objects) if lib else set()
    return sorted((o for o in bpy.data.objects if o.name.startswith(prefix) and o.type == "MESH" and o not in libset), key=lambda o: o.name)


def _del(prefix):
    for o in [o for o in bpy.data.objects if o.name.startswith(prefix)]:
        bpy.data.objects.remove(o)


def _mat(name, C):
    return bpy.data.materials.get(name) or st_material(name, C.look_dir)


def _root():
    return bpy.data.objects.get([o.name for o in bpy.data.objects if re.match(r"HOLE_\d+_ROOT", o.name)][0])


def _env(name, me, loc=(0, 0, 0), scale=(1, 1, 1), coll="ENVIRONMENT"):
    return st_obj(name, me, loc, scale, root=_root(), coll=coll)


def _snap(C):
    bpy.context.view_layer.update()
    return Snap("blend", C)


def _pin():
    p = bpy.data.objects["MARKER_PIN"].matrix_world.translation
    return float(p.x), float(p.y)


def _shore(C, step=2.0):
    """(P, N, loop id) of the real TERRAIN shoreline of the scene being mutated."""
    S = _snap(C)
    P, N, _t, _s, _tot, lid = terrain_shore_frame(S, C, step)
    return P, N, lid


def _water_point(C, far=30.0):
    """A point in open water off the shore with NO land under it (verified with the ground rays), metres."""
    S = _snap(C)
    rays = ground_rays(S)
    P, N, _lid = _shore(C, 4.0)
    for i in range(len(P) // 3, len(P) + len(P) // 3, 5):
        i %= len(P)
        for f in (far, far + 15.0, far - 10.0, far + 30.0):
            x, y = float(P[i][0] + N[i][0] * f), float(P[i][1] + N[i][1] * f)
            if not C.dry_m(np.array([x]), np.array([y]))[0] and rays.cast(x, y) is None:
                return x, y
    raise RuntimeError("no open-water point found")


def _rock_names():
    """Names of the loose rock mesh datablocks of the exported scene (stacks and pedestals included, cladding and walls excluded)."""
    return sorted({o.data.name for o in _ob("ROCK_") if not o.name.startswith(("ROCK_SKIN_", "ROCK_WALL_"))})


def _swap_all_rocks(make, fit=True):
    for k, nm in enumerate(_rock_names()):
        new = make(k)
        if fit:
            st_fit(new, bpy.data.meshes[nm])
        for ob in list(bpy.data.objects):
            if ob.type == "MESH" and ob.data == bpy.data.meshes[nm]:
                ob.data = new


def _ico(name, sub=1, split=False, extra=False, uvs=None, cube=None, mat="LK_ROCK"):
    bm = bmesh.new()
    if uvs:
        bmesh.ops.create_uvsphere(bm, u_segments=uvs[0], v_segments=uvs[1], radius=1.0)
    elif cube:
        bmesh.ops.create_cube(bm, size=2.0)
        for _ in range(cube):
            bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=1, use_grid_fill=True)
    else:
        bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
    if split:
        bmesh.ops.split_edges(bm, edges=list(bm.edges))
    if extra:
        for i in range(60):
            bm.verts.new((5 + 0.01 * i, 5, 5))
    return st_bm_mesh(name, bm, mat)


def _rm(C):
    return "LK_BASALT" if C.hole == 10 else "LK_ROCK"


def m_green_up(C):
    me = bpy.data.objects["GREEN"].data
    for v in me.vertices:
        v.co.z += 0.002


def m_pin(C):
    bpy.data.objects["MARKER_PIN"].location.x += 0.01


def m_plant_bunker(C):
    _ob("PLANT_")[0].name = "BUNKER_PLANT"


def m_new_arch(C):
    _env("ARCH_STONE", bpy.data.meshes["RK_0"], (0, 0, 30))


def m_marker_prop(C):
    _env("MARKER_DECOR", bpy.data.meshes["RK_0"], (0, 0, 30))


def m_foam(C):
    me = bpy.data.meshes.new("WATER_FOAM_X")
    me.from_pydata([(0, -40, .1), (10, -40, .1), (10, -30, .1), (0, -30, .1)], [], [(0, 1, 2, 3)])
    me.materials.append(bpy.data.materials.new("MAT_FOAM"))
    _env("WATER_FOAM_X", me)


def m_fairway_flat(C):
    bpy.data.objects["FAIRWAY"].data.materials[0] = bpy.data.materials.new("MAT_FAIRWAY")


def m_rock_ico(C):
    _ob("ROCK_B0")[0].data = _ico("ICO12", 1, mat=_rm(C))


def m_rocks_one(C):
    me = bpy.data.meshes["RK_0"]
    for o in _ob("ROCK_"):
        if not o.name.startswith(("ROCK_SKIN_", "ROCK_WALL_", "ROCK_SEASTACK", "ROCK_STACK")):
            o.data = me


def m_rocks_ico_split(C):
    _swap_all_rocks(lambda k: _ico(f"ICOS{k}", 1, split=True, mat=_rm(C)))


def m_rocks_ico_junk(C):
    _swap_all_rocks(lambda k: _ico(f"ICOJ{k}", 1, extra=True, mat=_rm(C)))


def m_rocks_ico3(C):
    _swap_all_rocks(lambda k: _ico(f"ICO3_{k}", 3, mat=_rm(C)))


def m_rocks_uvsphere(C):
    _swap_all_rocks(lambda k: _ico(f"UVS{k}", uvs=(24, 12), mat=_rm(C)))


def m_rocks_cube(C):
    _swap_all_rocks(lambda k: _ico(f"CUBE{k}", cube=2, mat=_rm(C)))


def m_rocks_copies(C):
    def make(k):
        bm = bmesh.new()
        bm.from_mesh(bpy.data.meshes["RK_0"])
        me = bpy.data.meshes.new(f"RKCOPY{k}")
        bm.to_mesh(me)
        bm.free()
        me.materials.append(bpy.data.materials[_rm(C)])
        st_box_uv(me)
        return me
    _swap_all_rocks(make, fit=False)


def m_stretch_x3(C):
    o = _ob("ROCK_B0")[0]
    o.scale = Vector((3.0, 1.0, 1.0))


def m_stretch_uniform(C):
    st_set_scale(_ob("ROCK_B1")[0], 2.6)


def m_stretch_aniso(C):
    o = _ob("ROCK_B2")[0]
    o.scale = Vector((1.0, 1.0, 2.5))


def m_ctl_scale_ok(C):
    o = _ob("ROCK_B0")[0]
    o.scale = Vector((1.8, 1.5, 1.2))


def m_dress_boulder_pin(C):
    px, py = _pin()
    _env("DRESS_BOULDER", bpy.data.meshes["RK_0"], (px + 0.5, py, C.pz + 1.0), (1.9, 1.9, 1.9))


def m_wall_pile_pin(C):
    px, py = _pin()
    _env("ROCK_WALL_PILE", bpy.data.meshes["RK_1"], (px, py, C.pz + 1.0), (1.9, 1.9, 1.9))


def m_dress_slab_fairway(C):
    S = _snap(C)
    pin = _pin()
    best = None
    for x, y, z in play_samples(S, C, 3.0):
        d = math.hypot(x - pin[0], y - pin[1])
        if d > 40.0 and (best is None or d < best[0]):
            best = (d, x, y, z)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Diagonal((6, 6, 1.0, 1)))
    _mat("LK_MASONRY", C)
    _env("DRESS_SLAB", st_bm_mesh("DRESS_SLAB", bm, "LK_MASONRY"), (best[1], best[2], best[3] + 0.5))


def m_ctl_sand_rough(C):
    """A legal DRESS_SAND_nn overlay: 1.0 cm over flat rough ground, off the play surfaces."""
    S = _snap(C)
    rng = np.random.default_rng(5)
    cands = st_candidates(C, S, 60, rng)
    for hs_ in (2.0, 1.2, 0.6):
        for x, y, z in cands:
            probe = [on_land(S, C, x + dx, y + dy) for dx in np.linspace(-hs_ - 0.3, hs_ + 0.3, 5) for dy in np.linspace(-hs_ - 0.3, hs_ + 0.3, 5)]
            if all(h[0] is not None and h[0] == probe[0][0] and abs(h[1] - probe[0][1]) < 1.5e-3 for h in probe):
                z = probe[0][1]
                me = st_mesh("DRESS_SAND_01", [(x - hs_, y - hs_, z + 0.010), (x + hs_, y - hs_, z + 0.010), (x + hs_, y + hs_, z + 0.010), (x - hs_, y + hs_, z + 0.010)],
                             [(0, 1, 2, 3)], "LK_SAND")
                _env("DRESS_SAND_01", me)
                return
    raise RuntimeError("no flat rough patch for the sand overlay")


def m_plant_on_green(C):
    p = bpy.data.objects["MARKER_PIN"].matrix_world.translation
    _ob("PLANT_")[0].location = (p.x + 3.0, p.y, C.pz)


def m_plant_on_sea(C):
    x, y = _water_point(C)
    _ob("PLANT_TUFT")[0].location = (x, y, 0.0)


def m_plant_floating(C):
    x, y = _water_point(C)
    _ob("PLANT_TUFT")[0].location = (x, y, C.pz)


def m_plant_on_fairway(C):
    S = _snap(C)
    pin = _pin()
    best = None
    for x, y, z in play_samples(S, C, 3.0):
        d = math.hypot(x - pin[0], y - pin[1])
        if d > 40.0 and (best is None or d < best[0]):
            best = (d, x, y, z)
    _ob("PLANT_TUFT")[0].location = (best[1], best[2], best[3])


def m_vine_floating(C):
    x, y = _water_point(C)
    me = bpy.data.meshes.get("PLANT_VINE_M")
    if me is None:
        me = st_mesh("PLANT_VINE_M", [(-.15, 0, -3 + 0.5 * k) for k in range(7)] + [(.15, 0, -3 + 0.5 * k) for k in range(7)],
                     [(k, k + 1, 8 + k, 7 + k) for k in range(6)], "LK_PLANTS", uv="green")
    _env("PLANT_VINE_M.990", me, (x, y, 8.0))


def m_plant_on_path(C):
    o = _ob("DRESS_PATH")[0]
    S = _snap(C)
    w = S.wld(o)
    c = w["W"][w["T"][len(w["T"]) // 2]].mean(axis=0)
    _ob("PLANT_TUFT")[0].location = (float(c[0]), float(c[1]), float(c[2]))


def m_lawn_dress(C):
    S = _snap(C)
    x, y, z = st_candidates(C, S, 1, np.random.default_rng(5))[0]
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, segments=32, radius=8.0)
    _env("DRESS_LAWN", st_bm_mesh("DRESS_LAWN", bm, "LK_PLANTS", uv="green"), (x, y, z + 0.03))
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.05, radius2=0.05, depth=2.4, matrix=Matrix.Translation((0, 0, 1.2)))
    _env("DRESS_FLAGSTICK", st_bm_mesh("DRESS_FLAGSTICK", bm, "LK_PLANTS", uv="brown"), (x, y, z))


def m_normal_unlinked(C):
    nt = bpy.data.materials["LK_FAIRWAY"].node_tree
    for l in list(nt.links):
        if l.to_socket.name == "Normal":
            nt.links.remove(l)


def m_fairway_nouv(C):
    me = bpy.data.objects["FAIRWAY"].data
    while len(me.uv_layers):
        me.uv_layers.remove(me.uv_layers[0])


def m_fairway_uv01(C):
    me = bpy.data.objects["FAIRWAY"].data
    u = np.empty(len(me.loops) * 2)
    me.uv_layers[0].data.foreach_get("uv", u)
    u = u.reshape(-1, 2)
    u = (u - u.min(0)) / np.maximum(np.ptp(u, 0), 1e-9)
    me.uv_layers[0].data.foreach_set("uv", u.ravel())


def m_wall_uv_16(C):
    mname = "LK_BASALT" if C.hole == 10 else "LK_CLIFF"
    ob = [o for o in bpy.data.objects if o.name.startswith("TERRAIN") and o.type == "MESH"][0]
    me = ob.data
    k = [m.name for m in me.materials].index(mname)
    uv = me.uv_layers[0].data
    for p in me.polygons:
        if p.material_index == k:
            for li in p.loop_indices:
                u, v = uv[li].uv
                uv[li].uv = (u * 4.0, v * 0.25)


def _smear_patch(factor, area_m2=45.0):
    """Scale the UVs of the FAIRWAY polygons nearest the mesh centre by (factor, 1/factor) about each polygon's own centre until ~area_m2 of ground is covered: a LOCAL smear (a few triangles), not a whole-mesh one."""
    me = bpy.data.objects["FAIRWAY"].data
    uv = me.uv_layers[0].data
    cen = np.mean([np.array(v.co) for v in me.vertices], axis=0)
    order = sorted(me.polygons, key=lambda p: np.linalg.norm(np.array(p.center) - cen))
    got = 0.0
    for p in order:
        if got >= area_m2:
            break
        got += p.area
        loops = list(p.loop_indices)
        c = np.mean([np.array(uv[li].uv) for li in loops], axis=0)
        for li in loops:
            d = np.array(uv[li].uv) - c
            uv[li].uv = (c[0] + d[0] * factor, c[1] + d[1] / factor)


def m_ground_uv_smear_local(C):
    _smear_patch(8.0)


def m_ctl_ground_uv_aniso_2p9(C):
    _smear_patch(1.7)


def m_plant_600(C):
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=25, y_segments=25, size=0.5)
    _ob("PLANT_SHRUB")[0].data = st_bm_mesh("PLANT_BIG", bm, "LK_PLANTS", uv="green")


def m_plant_2mat(C):
    o = _ob("PLANT_")[0]
    o.data = o.data.copy()
    o.data.materials.append(_mat("LK_ROCK", C))


def _sway_each(**kw):
    for o in {x.data.name: x for x in _ob("PLANT_")}.values():
        st_sway(o.data, sway_kind(o.name)[0], **kw)


def m_sway_none(C):
    for o in _ob("PLANT_"):
        for nm in [x.name for x in o.data.color_attributes]:
            o.data.color_attributes.remove(o.data.color_attributes[nm])


def m_sway_one_missing(C):
    o = _ob("PLANT_SHRUB")[0]
    for nm in [x.name for x in o.data.color_attributes]:
        o.data.color_attributes.remove(o.data.color_attributes[nm])


def m_sway_root_one(C):
    _sway_each(root_one=True)


def m_sway_tip_low(C):
    _sway_each(tip=0.4)


def m_sway_b_one(C):
    _sway_each(b=1.0)


def m_sway_two_layers(C):
    _sway_each(extra_layer=True)


def m_sway_shrub_over(C):
    for o in {x.data.name: x for x in _ob("PLANT_SHRUB")}.values():
        st_sway(o.data, "shrub", tip=1.0)


def m_sway_flower_over(C):
    fl = _ob("PLANT_FLOWER")
    if not fl:                                   # holes 9 and 10 carry no flowers: one tuft instance becomes a flower (its own mesh) with the tip weight of a tuft
        o = _ob("PLANT_TUFT")[0]
        o.data = o.data.copy()
        o.name = "PLANT_FLOWER_X.000"
        fl = [o]
    for o in {x.data.name: x for x in fl}.values():
        st_sway(o.data, "flower", tip=1.0)


def m_sway_quadratic(C):
    _sway_each(power=1.4, tip=None)


def m_plants_unique(C):
    for o in _ob("PLANT_"):
        o.data = o.data.copy()


def m_second_green(C):
    me = bpy.data.objects["GREEN"].data.copy()
    o = _env("DRESS_GREEN_2", me, (0, 0, 0))
    o.location.x += 60.0


def m_bunker_patch(C):
    me = bpy.data.meshes.new("SANDX")
    me.from_pydata([(0, -40, .1), (10, -40, .1), (10, -30, .1), (0, -30, .1)], [], [(0, 1, 2, 3)])
    me.materials.append(bpy.data.materials["LK_SAND"])
    _env("BUNKER_PATCH", me)


def m_animated(C):
    o = _ob("ROCK_B0")[0]
    o.keyframe_insert("location", frame=1)
    o.location.z += 1
    o.keyframe_insert("location", frame=20)


def m_heavy(C):
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=400, y_segments=400, size=5.0)
    _mat("LK_ROCK", C)
    _env("DRESS_HEAVY", st_bm_mesh("DRESS_HEAVY", bm, "LK_ROCK"), (0, -60, 0.5))


# -- frozen face-level geometry
def _terrain_top_face(C, mode):
    """An up-facing collision polygon (any TERRAIN / FAIRWAY / GREEN / BUNKER mesh) whose vertices are shared with its neighbours and that no 1.5 m
    grid ray hits (the reviewer's red-team face): deleting or flipping it leaves every grid ray and every vertex set untouched."""
    SIG = load_signature(SIG_DIR, C.hole)
    g = SIG["grid"]
    S = _snap(C)
    best = None
    for ob in [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith(COLL_PREFIXES) and o.name in S.all]:
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bm.faces.ensure_lookup_table()
        mw = np.array(ob.matrix_world)
        upf = [f for f in bm.faces if (mw[:3, :3] @ np.array(f.normal))[2] > 0.5]
        vcount = {}
        for f in upf:
            for v in f.verts:
                vcount[v.index] = vcount.get(v.index, 0) + 1
        for f in upf:
            if any(vcount[v.index] < 2 for v in f.verts):
                continue
            W = np.array([(mw[:3, :3] @ np.array(v.co)) + mw[:3, 3] for v in f.verts])
            lo, hi = W[:, :2].min(0), W[:, :2].max(0)
            i0, i1 = int(math.ceil((lo[0] - g["x0"]) / g["step"])), int(math.floor((hi[0] - g["x0"]) / g["step"]))
            j0, j1 = int(math.ceil((lo[1] - g["y0"]) / g["step"])), int(math.floor((hi[1] - g["y0"]) / g["step"]))
            inside = False
            for i in range(i0, i1 + 1):
                for j in range(j0, j1 + 1):
                    if pip(W[:, :2], np.array([g["x0"] + i * g["step"]]), np.array([g["y0"] + j * g["step"]]))[0]:
                        inside = True
            if inside:
                continue
            area = f.calc_area()
            if best is None or area > best[2]:
                best = (ob.name, f.index, area)
        bm.free()
    if best is None:
        raise RuntimeError("no up-facing collision polygon without a grid point")
    ob = bpy.data.objects[best[0]]
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    f = bm.faces[best[1]]
    if mode == "delete":
        bmesh.ops.delete(bm, geom=[f], context="FACES_ONLY")
    else:
        f.normal_flip()
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
    print(f"SELFTEST: {mode} face of {best[0]} ({best[2]:.2f} m2)", flush=True)


def m_face_delete(C):
    _terrain_top_face(C, "delete")


def m_face_flip(C):
    _terrain_top_face(C, "flip")


def m_face_extra(C):
    ob = [o for o in bpy.data.objects if o.name.startswith("TERRAIN") and o.type == "MESH"][0]
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    x, y = _water_point(C, 14.0)
    mw = ob.matrix_world.inverted()
    vs = [bm.verts.new(mw @ Vector(p)) for p in ((x, y, C.pz), (x + 2.0, y, C.pz), (x, y + 2.0, C.pz))]
    bm.faces.new(vs)
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()


def m_ctl_face_retri(C):
    """Re-triangulate: replace two coplanar up-facing triangles that form a convex quad by the two triangles of the OTHER diagonal: the same surface
    made of different triangles (every vertex set and every height is unchanged)."""
    for ob in [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith(COLL_PREFIXES)]:
        mw = np.array(ob.matrix_world)
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bm.edges.ensure_lookup_table()
        for e in bm.edges:
            if len(e.link_faces) != 2:
                continue
            f1, f2 = e.link_faces
            if len(f1.verts) != 3 or len(f2.verts) != 3:
                continue
            n1, n2 = np.array(f1.normal), np.array(f2.normal)
            if (mw[:3, :3] @ n1)[2] < 0.9 or float(n1 @ n2) < 0.9999999:
                continue
            u, w = e.verts[0], e.verts[1]
            a = [v for v in f1.verts if v not in (u, w)][0]
            b = [v for v in f2.verts if v not in (u, w)][0]
            old_area = f1.calc_area() + f2.calc_area()
            t1 = 0.5 * np.linalg.norm(np.cross(np.array(u.co - a.co), np.array(b.co - a.co)))
            t2 = 0.5 * np.linalg.norm(np.cross(np.array(b.co - a.co), np.array(w.co - a.co)))
            if abs(t1 + t2 - old_area) > 1e-9 * max(old_area, 1.0) or min(t1, t2) < 0.05 * old_area:
                continue                                        # not a convex quad
            nrm = f1.normal.copy()
            bmesh.ops.delete(bm, geom=[f1, f2], context="FACES_ONLY")
            for tri in ((a, u, b), (a, b, w)):
                f = bm.faces.new(tri)
                f.normal_update()
                if f.normal.dot(nrm) < 0:
                    f.normal_flip()
            bm.to_mesh(ob.data)
            bm.free()
            ob.data.update()
            return
        bm.free()
    raise RuntimeError("no coplanar triangle pair to re-triangulate")


# -- water
def _patches(C, starts, length=4, mat="LK_SURF", prefix="WATER_SURF", base=50):
    P, N, LID = _shore(C, 2.0)
    k = 0
    for i0 in starts:
        idx = np.arange(i0, i0 + length)
        if idx[-1] >= len(P) or len(set(LID[idx].tolist())) != 1:
            continue
        me = st_strip(f"{prefix}_{base + k:02d}", P[idx], N[idx], 0.3, 4.0, 0.12, mat=mat, vcol=True, uv_world=8.0)
        _env(f"{prefix}_{base + k:02d}", me)
        k += 1


def m_white_band_dress(C):
    P, N, LID = _shore(C, 2.0)
    for k, sub in enumerate(split_by_loop(np.arange(len(P)), LID)):
        me = st_strip(f"DRESS_SHOREBAND_{k}", P[sub], N[sub], 0.3, 3.0, 0.15, mat="LK_SURF", uv_world=8.0)
        _env(f"DRESS_SHOREBAND_{k}", me)


def m_surf_band_alpha1(C):
    P, N, LID = _shore(C, 2.0)
    for k, sub in enumerate(split_by_loop(np.arange(len(P)), LID)):
        me = st_strip(f"WATER_SURF_9{k}", P[sub], N[sub], 0.3, 3.0, 0.16, mat="LK_SURF", vcol=True, uv_world=8.0)
        ca = me.color_attributes[0]
        cols = np.ones(len(ca.data) * 4)
        cols[3] = 0.0
        ca.data.foreach_set("color", cols)
        _env(f"WATER_SURF_9{k}", me)


def m_surf_long_patch(C):
    _patches(C, [3 + 20 * 1 + 8], length=8)               # a 14 m unbroken run next to the regular patches (>= 10 m away from them)


def m_surf_dense(C):
    P = _shore(C, 2.0)[0]
    _patches(C, [i0 + 10 for i0 in range(3, len(P) - 6, 20)])    # patches in between every regular one: > 25 % of the shore


def m_surf_close_gap(C):
    _patches(C, [3 + 6])                                        # a patch 2 samples (4 m) after the first one


def m_surf_ribbon_offshore(C):
    P, N, LID = _shore(C, 2.0)
    best = max(split_by_loop(np.arange(len(P)), LID), key=len)
    idx = best[5:25]
    me = st_strip("WATER_SURF_98", P[idx], N[idx], 8.0, 11.0, 0.12, mat="LK_SURF", vcol=True, uv_world=8.0)
    _env("WATER_SURF_98", me)


def m_white_dress_smoke(C):
    """A white-textured (LK_SMOKE) DRESS_ mist strip hugging the shore: not LK_SURF, not named foam, only the white albedo gives it away."""
    P, N, LID = _shore(C, 2.0)
    best = max(split_by_loop(np.arange(len(P)), LID), key=len)
    idx = best[3:30]
    _mat("LK_SMOKE", C)
    me = st_strip("DRESS_SHOREMIST", P[idx], N[idx], 0.5, 3.5, 0.2, mat="LK_SMOKE", uv_world=8.0)
    _env("DRESS_SHOREMIST", me)


def m_fall_flat_strip(C):
    P, N, LID = _shore(C, 2.0)
    best = max(split_by_loop(np.arange(len(P)), LID), key=len)
    idx = best[5:30]
    _mat("LK_FALL", C)
    me = st_strip("WATER_FALL_98", P[idx], N[idx], 8.0, 11.0, 0.12, mat="LK_FALL", uv_world=8.0)
    _env("WATER_FALL_98", me)


def m_ocean_flat(C):
    bpy.data.objects["WATER_OCEAN"].data.materials[0] = bpy.data.materials.new("MAT_WATER")


def m_surf_gone(C):
    _del("WATER_SURF")


def m_surf_nocol(C):
    for o in _ob("WATER_SURF"):
        me = o.data
        while len(me.color_attributes):
            me.color_attributes.remove(me.color_attributes[0])


def _set_shelf(C, widths_fn, gaps_every=100, gap_len=12):
    P, N, LID = _shore(C, 2.0)
    bpy.data.objects.remove(bpy.data.objects["WATER_SHELF"])
    _env("WATER_SHELF", st_shelf(P, N, widths_fn(np.arange(len(P))), gaps_every=gaps_every, gap_len=gap_len, lid=LID))


def m_shelf_halo(C):
    _set_shelf(C, lambda i: np.full(len(i), 16.0), gaps_every=10 ** 9, gap_len=0)


def m_shelf_second_halo_object(C):
    """The thin WATER_SHELF stays; a second mesh WATER_SHELF_HALO (same material) carries the 16 m halo."""
    P, N, LID = _shore(C, 2.0)
    _env("WATER_SHELF_HALO", st_shelf(P, N, np.full(len(P), 16.0), gaps_every=10 ** 9, gap_len=0, name="WATER_SHELF_HALO", lid=LID))


def m_shelf_thin_ring(C):
    _del("ROCK_SKIN")
    _set_shelf(C, lambda i: np.full(len(i), 1.5), gaps_every=10 ** 9, gap_len=0)


def m_shelf_wide_patches(C):
    _set_shelf(C, lambda i: np.where((i % 100) < 24, 12.0, 1.2), gaps_every=10 ** 9, gap_len=0)


def m_skin_smooth(C):
    P, N, LID = _shore(C, 2.0)
    frac = 0.35 if C.hole == 8 else 0.65
    _del("ROCK_SKIN")
    ids = np.arange(int(len(P) * frac))
    k = 0
    for idx in np.array_split(ids, 6):
        for sub in split_by_loop(idx, LID):
            if len(sub) < 3:
                continue
            k += 1
            me = st_curtain(f"ROCK_SKIN_{k:02d}", P[sub], N[sub], np.full(len(sub), 0.5), 0.2, C.pz - 0.4)
            _env(f"ROCK_SKIN_{k:02d}", me)


def m_skin_gone(C):
    _del("ROCK_SKIN")


def m_skin_partial(C):
    for o in _ob("ROCK_SKIN_")[1:]:
        bpy.data.objects.remove(o)


# -- sea stacks / path / arch / crater
def _stacks():
    return [o for o in _ob("ROCK_") if o.name.startswith(("ROCK_SEASTACK", "ROCK_STACK"))]


def _replace_stacks(make):
    for k, o in enumerate(_stacks()):
        o.data = make(k)


def m_stack_boxes(C):
    _replace_stacks(lambda k: _stk(st_box_stack(f"BOXSTK{k}"), 1.0))


def _stk(me, s):
    st_box_uv(me)
    return me


def m_stack_cylinder(C):
    _replace_stacks(lambda k: _stk(st_spire(f"CYL{k}", 5 + k, height=18.0, r0=3.2, top_r=1.0, jag=0.03), 1.0))


def m_stack_flat_lid(C):
    _replace_stacks(lambda k: _stk(st_spire(f"LID{k}", 6 + k, height=18.0, r0=3.2, top_r=0.1, flat_top=0.8, jag=0.03), 1.0))


def m_stacks_gone(C):
    for o in _stacks():
        bpy.data.objects.remove(o)


def m_path_gone(C):
    _del("DRESS_PATH")


def m_path_float(C):
    for o in _ob("DRESS_PATH"):
        o.location.z += 2.0


def m_path_3p5(C):
    _ob("DRESS_PATH")[0].location.z += 0.027


def m_path_11(C):
    _ob("DRESS_PATH")[0].location.z += 0.102


def m_path_buried(C):
    _ob("DRESS_PATH")[0].location.z -= 0.03


def m_ctl_path_1(C):
    _ob("DRESS_PATH")[0].location.z += 0.002


def m_arch_solid(C):
    a = C.sc["arch"]
    ax, ay, z0 = a["x"] * YD, a["d"] * YD, a["base_top_m"]
    bm = bmesh.new()
    for lvl in range(7):
        for col in range(6):
            bmesh.ops.create_cube(bm, size=0.98, matrix=Matrix.Translation((ax - 2.5 + col, ay, z0 + 0.5 + 1.0 * lvl)))
    bpy.data.objects["DRESS_ARCH_01"].data = st_bm_mesh("ARCH_WALL", bm, "LK_MASONRY")


def m_arch_icos(C):
    o = _ob("DRESS_ARCH")[0]
    bm = bmesh.new()
    for k in range(3):
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=2.0, matrix=Matrix.Translation(o.data.vertices[0].co + Vector((0, 0, 3 * k))))
    o.data = st_bm_mesh("ARCH_ICOS", bm, "LK_MASONRY")


def m_arch_novines(C):
    for o in _ob("PLANT_VINE_M.9"):
        bpy.data.objects.remove(o)


def m_arch_nopedestal(C):
    bpy.context.view_layer.update()
    arch = _ob("DRESS_ARCH")[0]
    bb = [arch.matrix_world @ Vector(c) for c in arch.bound_box]
    x0, x1 = min(v.x for v in bb), max(v.x for v in bb)
    y0, y1 = min(v.y for v in bb), max(v.y for v in bb)
    for o in _ob("ROCK_"):
        rb = [o.matrix_world @ Vector(c) for c in o.bound_box]
        if min(v.x for v in rb) <= x1 and max(v.x for v in rb) >= x0 and min(v.y for v in rb) <= y1 and max(v.y for v in rb) >= y0:
            bpy.data.objects.remove(o)


def m_wall_vines_gone(C):
    _del("PLANT_VINE")


def m_flowers_white(C):
    st_atlas_uv(bpy.data.meshes["PLANT_FLOWER_PURPLE"], "green")


def m_flowers_few(C):
    for o in _ob("PLANT_FLOWER")[10:]:
        bpy.data.objects.remove(o)


def m_fall_platform(C):
    me = _ob("WATER_FALL")[0].data
    for v in me.vertices:
        v.co.z = C.pz + 0.05
    me.update()


def m_fall_uv_up(C):
    me = _ob("WATER_FALL")[0].data
    u = np.empty(len(me.loops) * 2)
    me.uv_layers[0].data.foreach_get("uv", u)
    u = u.reshape(-1, 2)
    u[:, 1] = 1 - u[:, 1]
    me.uv_layers[0].data.foreach_set("uv", u.ravel())


def m_scrub_ribbon(C):
    o = _ob("TERRAIN")[0]
    me = o.data
    k = [m.name for m in me.materials].index("LK_SCRUB")
    cen = np.empty(len(me.polygons) * 3)
    me.polygons.foreach_get("center", cen)
    cen = cen.reshape(-1, 3)
    mi = np.empty(len(me.polygons), np.int64)
    me.polygons.foreach_get("material_index", mi)
    _rc, bc, _s, _dc, _ds = split_regions(C, cen[:, :2] / YD)
    nz = np.array([p.normal.z for p in me.polygons])
    mi[bc & (nz > 0.5)] = k
    me.polygons.foreach_set("material_index", mi)


def m_ridge_green(C):
    me = _ob("TERRAIN")[0].data
    k = [m.name for m in me.materials].index("LK_SCRUB")
    me.materials[k] = bpy.data.materials["LK_ROUGH"]


def m_ridge_bare(C):
    _del("PLANT_")


def m_ruin_gone(C):
    _del("DRESS_RUIN")


def m_walls_blobs(C):
    blob = _ico("BLOB", 3, mat="LK_BASALT")
    for o in _ob("ROCK_WALL_"):
        o.data = blob


def m_lava_one(C):
    me = bpy.data.meshes.new("LAVA1")
    me.from_pydata([(-2000, -2000, 0), (2000, -2000, 0), (2000, 2000, 0), (-2000, 2000, 0)], [], [(0, 1, 2, 3)])
    me.materials.append(bpy.data.materials["LK_LAVA"])
    bpy.data.objects["WATER_LAVA"].data = me


def m_lava_raised(C):
    bpy.data.objects["WATER_LAVA"].location.z = C.pz


def m_lights_gone(C):
    _del("LAVA_LIGHT_")


def m_lights_many(C):
    for k in range(5, 10):
        e = bpy.data.objects.new(f"LAVA_LIGHT_{k:02d}", None)
        bpy.data.collections["ENVIRONMENT"].objects.link(e)
        e.location = bpy.data.objects["LAVA_LIGHT_01"].location


def m_pillar_rock(C):
    o = _ob("TERRAIN")[0]
    me = o.data
    me.materials.append(_mat("LK_ROCK", C))
    k = len(me.materials) - 1
    p = C.sc["pillar"]
    cen = np.empty(len(me.polygons) * 3)
    me.polygons.foreach_get("center", cen)
    cen = cen.reshape(-1, 3)
    mi = np.empty(len(me.polygons), np.int64)
    me.polygons.foreach_get("material_index", mi)
    nz = np.array([q.normal.z for q in me.polygons])
    near = np.hypot(cen[:, 0] - p["x"] * YD, cen[:, 1] - p["d"] * YD) <= p["r_top"] * YD + 3
    mi[near & (nz <= 0.5) & (cen[:, 2] < C.pz - LIP_BAND)] = k
    me.polygons.foreach_set("material_index", mi)


def m_cone_low(C):
    bpy.data.objects["DRESS_CONE"].scale.z = 0.05


def m_smoke_gone(C):
    _del("DRESS_SMOKE")


def st_smoke_shots(opts):
    """SMOKE_IN_FRAME self-test camera: the synthetic hole 10 stands its two smoke cards (40 x 40 m, rot_z .4, bottom at z 170) 600 m / 300 m beyond the crater centre, far from the real proof
    cameras, so the positive build is given its own shot: 80 m in front of the first card, looking at its centre (Unity world yards, the cfg schema)."""
    C = st_ctx(10, opts)
    cr = C.sc["crater"]
    ccx, ccy = cr["cx"] * YD, cr["cd"] * YD
    yaw = 0.4
    ctr = np.array([ccx + 600.0, ccy + 300.0, 190.0])
    cam = ctr + np.array([-math.sin(yaw) * 80.0, math.cos(yaw) * 80.0, 0.0])
    return [dict(name="synthetic_card_view", kind="landmark", pos=[cam[0] / YD, cam[2] / YD, cam[1] / YD], look=[ctr[0] / YD, ctr[2] / YD, ctr[1] / YD], fov=60)], ctr, cam


def m_smoke_far(C):
    """smoke cards 500 m away from every frame"""
    for o in [o for o in bpy.data.objects if o.name.startswith("DRESS_SMOKE")]:
        o.location.x += 500.0


def m_smoke_tiny(C):
    """smoke cards scaled to a quarter: a plume in frame but no bigger than a thumbnail"""
    for o in [o for o in bpy.data.objects if o.name.startswith("DRESS_SMOKE")]:
        o.scale = (0.25, 0.25, 0.25)


def m_smoke_walled(C):
    """the cards are in frame but an opaque wall stands between the camera and them"""
    shots, ctr, cam = st_smoke_shots(dict(C.opts))
    mid = (ctr + cam) / 2.0
    v = [(-100, 0, -100), (100, 0, -100), (100, 0, 100), (-100, 0, 100)]
    me = st_mesh("ROCK_WALL_SMOKETEST", v, [(0, 1, 2, 3)], "LK_BASALT")
    me.uv_layers.new(name="UVMap")
    me.uv_layers[0].data.foreach_set("uv", [0, 0, 1, 0, 1, 1, 0, 1])
    st_obj("ROCK_WALL_SMOKETEST", me, (mid[0], mid[1], mid[2]), rot_z=0.4, root=bpy.data.objects.get("HOLE_10_ROOT"))


def t_smoke_faint(d):
    _wr(d, "Smoke_C", st_smoke_img(faint=True))


def m_tee_flat(C):
    bpy.data.objects["TEE_BOX"].data.materials[0] = bpy.data.materials.new("MAT_GREEN")


def m_crater_bare(C):
    _del("ROCK_SKIN_WALL")


def m_crater_lip_exposed(C):
    o = bpy.data.objects["ROCK_SKIN_WALL"]
    bm = bmesh.new()
    bm.from_mesh(o.data)
    dead = [f for f in bm.faces if f.calc_center_median().z > C.pz - 1.6]
    bmesh.ops.delete(bm, geom=dead, context="FACES")
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()


# ---- texture mutations: edit one or more PNGs of a copy of the synthetic Look folder
def _wr(d, stem, rgba):
    st_png(os.path.join(d, stem + ".png"), rgba)


def _rd(d, stem):
    return image_stats(os.path.join(d, stem + ".png"))["px"].copy()


def t_flat_alpha(d):
    rng = np.random.default_rng(1)
    a = np.ones((512, 512, 4))
    a[..., :3] = (0.20, 0.55, 0.18)
    a[..., 3] = rng.uniform(0.0, 1.0, (512, 512))
    _wr(d, "Fairway_C", a)


def t_near_flat(d):
    rng = np.random.default_rng(2)
    a = np.ones((512, 512, 4))
    a[..., :3] = np.array((0.30, 0.50, 0.20)) + 0.0095 * rng.normal(0, 1, (512, 512))[..., None]
    _wr(d, "Fairway_C", a)


def t_flat_normal(d):
    rng = np.random.default_rng(3)
    a = np.ones((512, 512, 4))
    a[..., 0] = 0.5 + 0.0055 * rng.normal(0, 1, (512, 512))
    a[..., 1] = 0.5 + 0.0055 * rng.normal(0, 1, (512, 512))
    a[..., 2] = 1.0
    _wr(d, "Fairway_N", a)


def t_flat_fill(d):
    a = np.ones((512, 512, 4))
    a[..., :3] = 0.3
    _wr(d, "Fairway_C", a)


def t_look_downsampled_128(d):
    """The real artwork (the synthetic Fairway) averaged down 4x to 128 px: the old fixed 64 px window passed it, the footprint window and the size floor must not."""
    a = _rd(d, "Fairway_C")
    _wr(d, "Fairway_C", pool(a, 4))


def t_look_tiled_cell(d):
    """A random 16 x 16 cell of grass colours tiled 32 x 32 times over the map: sd, block means and the single-frequency test all pass; the repetition does not."""
    rng = np.random.default_rng(11)
    cell = rng.uniform(0.80, 1.20, (16, 16, 1))
    base = np.array((0.30, 0.45, 0.14))
    a = np.ones((512, 512, 4))
    a[..., :3] = np.clip(base[None, None, :] * np.tile(cell, (32, 32, 1)), 0, 1)
    _wr(d, "Fairway_C", a)


def t_look_checkerboard(d):
    """An 8 px checkerboard of 14 % contrast in the grass colour: structure by sd, but not grass (one spatial frequency)."""
    yy, xx = np.mgrid[0:512, 0:512]
    chk = (((yy // 4) + (xx // 4)) % 2).astype(float)
    base = np.array((0.30, 0.45, 0.14))
    a = np.ones((512, 512, 4))
    a[..., :3] = base[None, None, :] * (0.93 + 0.14 * chk[..., None])
    _wr(d, "Fairway_C", a)


def t_fairway_smooth_below_floor(d):
    """A SMOOTH structured fairway (low-frequency noise, window sd ~0.007 at 1.2 m): real spatial structure but flatter than the stills' own grass (floor 0.012): must FAIL."""
    rng = np.random.default_rng(21)
    a = np.ones((512, 512, 4))
    a[..., :3] = np.array((0.30, 0.45, 0.14)) * 0.0 + np.array((0.30, 0.45, 0.14)) + (0.010 * st_noise(512, rng, beta=2.0, lo=1, hi=24))[..., None]
    _wr(d, "Fairway_C", a)


def t_fairway_at_stills_level(d):
    """A fairway whose 1.2 m windows sit just above the stills' own floor (window p10 ~0.016, smooth structure + a little blade noise): must PASS (the stills pass the same floor)."""
    rng = np.random.default_rng(22)
    a = np.ones((512, 512, 4))
    n = 0.034 * st_noise(512, rng, beta=1.4, lo=1, hi=64) + 0.006 * st_noise(512, rng, beta=0.5, lo=40, hi=200)
    a[..., :3] = np.array((0.30, 0.45, 0.14)) + n[..., None]
    _wr(d, "Fairway_C", a)


def t_green_copy(d):
    shutil.copyfile(os.path.join(d, "Fairway_C.png"), os.path.join(d, "Green_C.png"))


def t_green_tint(d):
    a = _rd(d, "Fairway_C")
    a[..., :3] *= 0.97
    _wr(d, "Green_C", a)


def t_fairway_bluegreen(d):
    _wr(d, "Fairway_C", np.dstack([st_albedo_img("Fairway", 512, hue=97.0), np.ones((512, 512))]))


def t_rough_bluegreen(d):
    _wr(d, "Rough_C", np.dstack([st_albedo_img("Rough", 512, hue=95.0), np.ones((512, 512))]))


def t_fairway_desat(d):
    a = np.dstack([st_albedo_img("Fairway", 512), np.ones((512, 512))])
    g = a[..., :3].mean(-1, keepdims=True)
    a[..., :3] = g + (a[..., :3] - g) * 0.25
    _wr(d, "Fairway_C", a)


def t_scrub_copy(d):
    shutil.copyfile(os.path.join(d, "Fairway_C.png"), os.path.join(d, "Scrub_C.png"))


def t_scrub_green(d):
    _wr(d, "Scrub_C", np.dstack([st_albedo_img("Fairway", 512, hue=75.0, seed=999), np.ones((512, 512))]))


def t_scrub_hueshifted_fairway(d):
    """Scrub_C = the Fairway_C pattern with the hue moved 28 degrees to olive and the value x0.85 (the v2 review: passed when a colour difference was enough)."""
    a = _rd(d, "Fairway_C")
    h, s_, v = hsv_arr(a[..., :3])
    h2 = (h - 28.0) % 360.0
    _wr(d, "Scrub_C", np.dstack([st_hsv_img(h2, s_ * 0.9, v * 0.85, 0.0, 0.0, 0.0), np.ones(a.shape[:2])]))


def t_scrub_tinted_fairway(d):
    """Scrub_C = the Fairway_C pattern with the channels scaled (redder, G x0.88): the pattern correlates ~0.9 with the fairway."""
    a = _rd(d, "Fairway_C")
    a[..., 0] = np.minimum(1.0, a[..., 0] * 1.35 + 0.02)
    a[..., 1] *= 0.88
    a[..., 2] *= 0.9
    _wr(d, "Scrub_C", a)


def t_green_hueshifted_fairway(d):
    a = _rd(d, "Fairway_C")
    h, s_, v = hsv_arr(a[..., :3])
    _wr(d, "Green_C", np.dstack([st_hsv_img((h + 9.0) % 360.0, s_, np.minimum(1.0, v * 1.12), 0.0, 0.0, 0.0), np.ones(a.shape[:2])]))


OLD_LOOK = os.path.join(JOB_DIR, "v2", "gates", "fixtures", "old_look")


def t_lava_old(d):
    for f in ("Lava_C", "Lava_E"):
        shutil.copyfile(os.path.join(OLD_LOOK, f + ".png"), os.path.join(d, f + ".png"))


def _lava_pair(d, C_, E_):
    a = np.ones(C_.shape[:2] + (4,))
    b = np.ones(C_.shape[:2] + (4,))
    a[..., :3], b[..., :3] = C_, E_
    _wr(d, "Lava_C", a)
    _wr(d, "Lava_E", b)


def t_lava_voronoi_dark(d):
    _lava_pair(d, *st_lava_voronoi(dark_plates=True))


def t_lava_voronoi_bright(d):
    _lava_pair(d, *st_lava_voronoi(dark_plates=False))


def t_lava_flat(d):
    rng = np.random.default_rng(4)
    C_ = np.full((256, 256, 3), 0.25) + 0.0095 * rng.normal(0, 1, (256, 256, 1))
    E_ = np.zeros((256, 256, 3))
    E_[60:63, 60:63] = 1.0
    _lava_pair(d, C_, E_)


def t_lava_flat_emission(d):
    E_ = np.zeros(_rd(d, "Lava_E").shape[:2] + (3,))
    E_[...] = (0.9, 0.5, 0.1)
    _lava_pair(d, _rd(d, "Lava_C")[..., :3], E_)


def t_green_rotated_copy(d):
    a = _rd(d, "Fairway_C")
    _wr(d, "Green_C", np.ascontiguousarray(np.rot90(a, 1)))


def t_green_shifted_copy(d):
    a = _rd(d, "Fairway_C")
    _wr(d, "Green_C", np.ascontiguousarray(np.roll(np.roll(a, 53, axis=0), 91, axis=1)))


def t_lava_yellow(d):
    E_ = _rd(d, "Lava_E")[..., :3]
    E_[..., 1] = np.minimum(1.0, E_[..., 1] * 1.55 + 0.05)
    E_[..., 2] = np.minimum(1.0, E_[..., 2] * 3.0 + 0.15)
    _lava_pair(d, _rd(d, "Lava_C")[..., :3], E_)


def t_lava_seam(d):
    C_, E_ = _rd(d, "Lava_C")[..., :3], _rd(d, "Lava_E")[..., :3]
    ramp = np.linspace(0.45, 1.0, E_.shape[1])[None, :, None]
    _lava_pair(d, C_, np.clip(E_ * ramp, 0, 1))


def t_lava_darkcrust(d):
    C_, E_ = _rd(d, "Lava_C")[..., :3], _rd(d, "Lava_E")[..., :3]
    v = E_[..., 0]
    E_ = np.where((v < np.percentile(v, 42))[..., None], E_ * 0.2, E_)
    _lava_pair(d, C_, E_)


def _basalt(d, **kw):
    C_, N_, E_ = st_basalt_maps(**kw)
    for stem, arr in (("Basalt_C", C_), ("Basalt_N", N_), ("Basalt_E", E_)):
        a = np.ones(arr.shape[:2] + (4,))
        a[..., :3] = arr
        _wr(d, stem, a)


def t_basalt_old(d):
    for f in ("Basalt_C", "Basalt_N", "Basalt_E"):
        shutil.copyfile(os.path.join(OLD_LOOK, f + ".png"), os.path.join(d, f + ".png"))


def t_basalt_ladder(d):
    _basalt(d, ladder=True)


def t_basalt_bricks(d):
    _basalt(d, bricks=True)


def t_basalt_flatbevel(d):
    _basalt(d, flat_bevel=True)


def t_smoke_old(d):
    shutil.copyfile(os.path.join(OLD_LOOK, "Smoke_C.png"), os.path.join(d, "Smoke_C.png"))


def t_smoke_hard(d):
    _wr(d, "Smoke_C", st_smoke_img(hard=True))


def t_smoke_border(d):
    _wr(d, "Smoke_C", st_smoke_img(border=True))


def t_smoke_opaque(d):
    _wr(d, "Smoke_C", st_smoke_img(opaque=True))


def t_smoke_ball(d):
    _wr(d, "Smoke_C", st_smoke_img(ball=True))


# ---- v2 review-fix mutations (each one is an attack from the skeptic review of 2026-10-04 or a boundary control)
def _land_spot(C, k=0):
    S = _snap(C)
    return st_candidates(C, S, 4 + k, np.random.default_rng(3), kind="rough", clear=0.0)[k]


def _ico_bundle(n, jitter, seed, sub=1, mat="LK_ROCK"):
    """n icosahedra (12 vertices; sub=2: 42 vertices) with radial + per-axis jitter merged into ONE mesh: 60-96 vertices, 100-160 faces."""
    bm = bmesh.new()
    rng = np.random.default_rng(seed)
    for _k in range(n):
        tmp = bmesh.new()
        bmesh.ops.create_icosphere(tmp, subdivisions=sub, radius=1.0)
        me = bpy.data.meshes.new("tmp")
        tmp.to_mesh(me)
        tmp.free()
        c = rng.uniform(-1.2, 1.2, 3)
        vs = {}
        for v in me.vertices:
            vs[v.index] = bm.verts.new(np.array(v.co) * (0.5 + rng.uniform(-jitter, jitter)) * (1 + rng.uniform(-jitter, jitter, 3)) + c)
        for pl in me.polygons:
            bm.faces.new([vs[i] for i in pl.vertices])
        bpy.data.meshes.remove(me)
    me = st_bm_mesh(f"BUNDLE{n}_{seed}", bm, mat)
    st_box_uv(me)
    return me


def m_rocks_bundle_ico12(C):
    _swap_all_rocks(lambda k: _ico_bundle(5 + k % 4, 0.12, 100 + k, mat=_rm(C)))


def m_rocks_bundle_loose_only(C):
    for k, nm in enumerate(_rock_names()):
        new = _ico_bundle(5 + k % 4, 0.12, 100 + k, mat=_rm(C))
        st_fit(new, bpy.data.meshes[nm])
        for ob in list(bpy.data.objects):
            if ob.type == "MESH" and ob.data == bpy.data.meshes[nm] and not ob.name.startswith(("ROCK_SEASTACK", "ROCK_PEDESTAL")):
                ob.data = new


def m_rocks_bundle_ico42(C):
    _swap_all_rocks(lambda k: _ico_bundle(3 + k % 2, 0.15, 200 + k, sub=2, mat=_rm(C)))


def m_rocks_ico3_noise40(C):
    def mk(k):
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1.0)
        rng = np.random.default_rng(k)
        for v in bm.verts:
            v.co *= 1.0 + rng.uniform(-0.4, 0.4)
        return st_bm_mesh(f"NOISE{k}", bm, _rm(C))
    _swap_all_rocks(mk)


def _lib_object(C, kind, name, sx=1.0, sy=1.0, sz=1.0, loc=None, scale=(1, 1, 1), parent=None):
    sig, err = lib_signature()
    if kind not in sig:
        raise RuntimeError(f"props library kind {kind} unavailable ({err})")
    me = bpy.data.meshes.new(name)
    me.from_pydata((sig[kind]["V"] * np.array([sx, sy, sz])).tolist(), [], sig[kind]["F"])
    me.update()
    me.materials.append(_mat(_rm(C), C))
    st_box_uv(me)
    p = loc or _land_spot(C)
    return st_obj(name, me, (p[0], p[1], p[2] + 0.1), scale, root=parent or _root())


def m_rocks_family_variants(C):
    """One hull rock re-issued as six 'unique' meshes: an edge split at a different place (topology hash differs) + 2 % jitter: geometrically ONE shape family."""
    def mk(k):
        me = st_hull_rock(f"FAM{k}", 5, _rm(C), dims=(2.0, 1.6, 1.2))
        bm = bmesh.new()
        bm.from_mesh(me)
        rng = np.random.default_rng(k)
        edges = list(bm.edges)
        bmesh.ops.subdivide_edges(bm, edges=[edges[(7 * k + 3) % len(edges)]], cuts=1)
        for v in bm.verts:
            v.co *= 1.0 + rng.uniform(-0.02, 0.02)
        bm.to_mesh(me)
        bm.free()
        st_box_uv(me)
        return me
    _swap_all_rocks(mk, fit=False)


def m_stretch_sheared_parent(C):
    p = _land_spot(C)
    em = bpy.data.objects.new("DRESS_SHEARPARENT", None)
    bpy.context.scene.collection.objects.link(em)
    em.parent = _root()
    em.location = (p[0], p[1], p[2] + 0.4)
    em.rotation_euler = (0, 0, math.radians(45))
    em.scale = (0.6, 1.9, 1.0)
    me = st_hull_rock("HULL_SHEAR", 77, _rm(C), dims=(2.0, 1.6, 1.2))
    st_box_uv(me)
    st_obj("ROCK_TEST_SHEAR", me, (0, 0, 0), (1, 1, 1), math.radians(-45), root=em)


def m_stretch_baked_lib_renamed(C):
    _lib_object(C, "ROCK_BOULDER_A", "ROCK_CAIRN_01", sx=4.0, sy=0.5)


def m_ctl_lib_scaled_ok(C):
    _lib_object(C, "ROCK_BOULDER_A", "ROCK_BOULDER_A.900", sx=1.5, sy=1.3, sz=1.1, scale=(1.2, 1.1, 1.0))


def m_plant_merged_far_copy(C):
    p = _land_spot(C)
    x, y = _water_point(C, 40.0)
    base = _ob("PLANT_")[0].data
    bm = bmesh.new()
    bm.from_mesh(base)
    for f in list(bm.faces)[3:]:
        bmesh.ops.delete(bm, geom=[f], context="FACES")
    parts = []
    for sgn in (1, -1):
        bm2 = bm.copy()
        bmesh.ops.translate(bm2, vec=(sgn * (x - p[0]), sgn * (y - p[1]), 0.0), verts=list(bm2.verts))
        tmp = bpy.data.meshes.new("t2")
        bm2.to_mesh(tmp)
        bm2.free()
        parts.append(tmp)
    bm.from_mesh(parts[0])
    bm.from_mesh(parts[1])
    me = bpy.data.meshes.new("PLANT_MERGED")
    bm.to_mesh(me)
    me.materials.append(_mat("LK_PLANTS", C))
    st_atlas_uv(me, "green")
    st_obj("PLANT_TUFT_MERGED", me, (p[0], p[1], p[2]), root=_root())


def m_plant_on_floating_slab(C):
    x, y = _water_point(C, 20.0)
    me = st_hull_rock("HULL_FLOAT", 5, _rm(C), dims=(3.0, 3.0, 0.5))
    st_box_uv(me)
    top = max(v.co.z for v in me.vertices)
    for v in me.vertices:
        v.co.z += 5.2 - top
    _env("ROCK_MEDIUM.088", me, (x, y, 0.0))
    _env("PLANT_TUFT_ONSLAB", _ob("PLANT_")[0].data, (x, y, 5.2))


def m_ctl_plant_on_rock(C):
    p = _land_spot(C)
    me = st_hull_rock("HULL_PEDROCK", 9, _rm(C), dims=(3.0, 3.0, 1.2))
    st_box_uv(me)
    lo = min(v.co.z for v in me.vertices)
    top = max(v.co.z for v in me.vertices)
    for v in me.vertices:
        v.co.z += 0.0 - lo - 0.05
    st_obj("ROCK_MEDIUM.089", me, (p[0], p[1], p[2]), root=_root())
    # the plant stands on the rock's top facet at its centre (BVHTree.FromObject is OBJECT space: transform the ray in, the hit out)
    bpy.context.view_layer.update()
    ob = bpy.data.objects["ROCK_MEDIUM.089"]
    bvh = BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())
    loc_ = ob.matrix_world.inverted() @ Vector((p[0], p[1], p[2] + 20.0))
    hit = bvh.ray_cast(loc_, Vector((0, 0, -1)))
    hz = (ob.matrix_world @ hit[0]).z if hit[0] is not None else p[2] + (top - lo)
    _env("PLANT_TUFT_ONROCK", _ob("PLANT_")[0].data, (p[0], p[1], hz))


def m_dress_small_boulder_between_grid(C):
    S = _snap(C)
    pin = np.array(_pin())
    best = None
    for (x, y, z) in play_samples(S, C):
        if math.hypot(x - pin[0], y - pin[1]) < 25:
            continue
        cx, cy = x + 0.75, y + 0.75
        hit, hz = on_land(S, C, cx, cy)
        if hit and hit.startswith("FAIRWAY") and all(on_land(S, C, cx + dx, cy + dy)[0] == hit for dx, dy in ((.4, 0), (-.4, 0), (0, .4), (0, -.4))):
            best = (cx, cy, hz)
            break
    me = st_hull_rock("HULL_SMALL", 4, _rm(C), dims=(0.7, 0.7, 0.6))
    st_box_uv(me)
    _env("DRESS_BOULDER_FW", me, best)


def m_crater_wall_hole12(C):
    o = bpy.data.objects["ROCK_SKIN_WALL"]
    bm = bmesh.new()
    bm.from_mesh(o.data)
    cens = [f.calc_center_median() for f in bm.faces]
    # the hole sits where the camera set actually looks: the face seen from most viewpoints (the wall faces that look away from every viewpoint hide nothing)
    VP = np.array([vp for _k, vp in crater_viewpoints(_snap(C), C)], float)
    seen = [int(((np.array(c[:]) - VP) @ np.array(f.normal[:]) < 0).sum()) if f.calc_area() > 1e-6 else -1 for f, c in zip(bm.faces, cens)]
    best = max(range(len(cens)), key=lambda i: (seen[i], cens[i].z))
    c0 = cens[best]
    dead = [f for f, c in zip(bm.faces, cens) if math.hypot(c.x - c0.x, c.y - c0.y) <= 12.0]
    bmesh.ops.delete(bm, geom=dead, context="FACES")
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()


def _island_cone(name, rough_cap):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=14, radius1=8.0, radius2=6.5, depth=11.0, matrix=Matrix.Translation((0, 0, 2.5)))
    bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=1, use_grid_fill=True)
    me = st_bm_mesh(name, bm, "LK_ROCK")
    me.materials.append(bpy.data.materials["LK_ROUGH"])
    if rough_cap:
        for pl in me.polygons:
            if pl.normal.z > 0.5 and pl.center.z > 7.0:
                pl.material_index = 1
    st_box_uv(me)
    return me


def m_stack_pedestal_named_tower(C):
    x, y = _water_point(C, 14.0)
    me = st_spire("TOWER99", 31, height=20.0, r0=3.2, top_r=1.0, jag=0.03)
    st_box_uv(me)
    _env("ROCK_PEDESTAL_99", me, (x, y, 0.0))


def m_stack_island_without_cap(C):
    x, y = _water_point(C, 22.0)
    _env("ROCK_OUTCROP_99", _island_cone("ISLE_BARE", False), (x, y, 0.0))


def m_ctl_island_with_grass_cap(C):
    x, y = _water_point(C, 22.0)
    _env("ROCK_OUTCROP_98", _island_cone("ISLE_CAP", True), (x, y, 0.0))


def m_foam_squares_touching(C):
    P, N, LID = _shore(C, 2.0)
    k = 0
    for lid in sorted(set(LID.tolist())):
        idx = np.flatnonzero(LID == lid)
        i = int(idx[0]) + 1
        while i + 3 < idx[-1]:
            ii = np.arange(i, i + 3)
            me = st_strip(f"WATER_SURF_{120 + k}", P[ii], N[ii], 2.0, 6.0, 0.12, mat="LK_SURF", vcol=True, uv_world=8.0)
            _env(f"WATER_SURF_{120 + k}", me)
            k += 1
            i += 3


def _ragged_band(C, name, o_in, wmin, wmax, seed=4, z=0.12, alpha_fixed=None):
    P, N, LID = _shore(C, 2.0)
    rng = np.random.default_rng(seed)
    V, F = [], []
    prev = None
    for i in range(len(P)):
        if prev is not None and LID[i] != LID[i - 1]:
            prev = None
        w = rng.uniform(wmin, wmax)
        base = len(V)
        V += [(P[i][0] + N[i][0] * o_in, P[i][1] + N[i][1] * o_in, z), (P[i][0] + N[i][0] * (o_in + w), P[i][1] + N[i][1] * (o_in + w), z)]
        if prev is not None:
            F.append((prev, prev + 1, base + 1, base))
        prev = base
    me = st_mesh(name, V, F, "LK_SURF", uv=None)
    me.update()
    if len(me.polygons) and me.polygons[0].normal.z < 0:
        me.flip_normals()
    ca = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    cols = np.ones(len(ca.data) * 4)
    if alpha_fixed is None:
        cols[3::8] = 0.0                                 # an alpha ramp so SURF_SHORE is satisfied
    else:
        cols[3::4] = alpha_fixed                         # a constant faint alpha
    ca.data.foreach_set("color", cols)
    me.uv_layers.new(name="UVMap")
    lv = np.empty(len(me.loops), np.int64)
    me.loops.foreach_get("vertex_index", lv)
    co = np.array([v.co[:] for v in me.vertices])
    me.uv_layers[0].data.foreach_set("uv", np.stack([co[lv][:, 0] / 8.0, co[lv][:, 1] / 8.0], 1).ravel())
    _env(name, me)


def m_foam_ragged_band_off2p5(C):
    _ragged_band(C, "WATER_SURF_95", 2.5, 0.3, 5.5)


def m_foam_ghost_band_alpha_0p29(C):
    _ragged_band(C, "WATER_SURF_96", 0.3, 2.0, 4.0, seed=7, alpha_fixed=0.29)


def m_foam_offshore_dashes_gap7(C):
    P, N, LID = _shore(C, 1.0)
    k = 0
    for lid in sorted(set(LID.tolist())):
        idx = np.flatnonzero(LID == lid)
        i = int(idx[0]) + 1
        while i + 9 < idx[-1]:
            ii = np.arange(i, i + 9)
            me = st_strip(f"WATER_SURF_{140 + k}", P[ii], N[ii], 14.0, 17.0, 0.12, mat="LK_SURF", vcol=True, uv_world=8.0)
            _env(f"WATER_SURF_{140 + k}", me)
            k += 1
            i += 9 + 7 - 1
        # (dashes 8 m long, 7 m apart, 14-17 m off the shore: beyond the shore-normal reach, a continuous band in effect)


def m_foam_white_material_strip(C):
    """A white strip at the shore made of a material that is not LK_SURF / not named foam (white base colour): only the albedo gives it away."""
    mt = bpy.data.materials.new("MAT_SPRAY_TEST")
    mt.use_nodes = True
    for nd in mt.node_tree.nodes:
        if nd.type == "BSDF_PRINCIPLED":
            nd.inputs["Base Color"].default_value = (0.96, 0.97, 0.98, 1.0)
    P, N, LID = _shore(C, 2.0)
    best = max(split_by_loop(np.arange(len(P)), LID), key=len)
    idx = best[3:40]
    me = st_strip("WATER_WHITEBAND_01", P[idx], N[idx], 3.0, 6.0, 0.14, mat="LK_SAND", uv_world=8.0)
    me.materials.clear()
    me.materials.append(mt)
    for pl in me.polygons:
        pl.material_index = 0
    _env("WATER_WHITEBAND_01", me)


# ---- v2 round-3 review mutations (the skeptic's attacks of the second review + boundary controls)
def _skirt(C, name, mat, nz=None, run=1.5, drop=None, z_top=3.0, o_in=0.0, alpha_lo=0.4, white_material=None):
    """A continuous white skirt around EVERY shoreline loop: top edge at z_top on the shore line (offset o_in), bottom edge `run` m further out and `drop` m lower
    (nz = run / hypot(run, drop): nz 0.45 is a 63 degree slope; run 0 = a vertical wall). Vertex alpha 1 on top, `alpha_lo` at the bottom (so SURF_SHORE is satisfied)."""
    P, N, LID = _shore(C, 2.0)
    if drop is None:
        drop = run * math.sqrt(1.0 - nz * nz) / nz if nz else 2.0
    V, F, alpha = [], [], []
    for lid in sorted(set(LID.tolist())):
        idx = np.flatnonzero(LID == lid)
        prev = None
        first = None
        for i in idx.tolist() + [int(idx[0])]:
            b = len(V)
            V += [(P[i][0] + N[i][0] * o_in, P[i][1] + N[i][1] * o_in, z_top),
                  (P[i][0] + N[i][0] * (o_in + run), P[i][1] + N[i][1] * (o_in + run), max(z_top - drop, 0.02))]
            alpha += [1.0, alpha_lo]
            if prev is not None:
                F.append((prev, b, b + 1, prev + 1))
            prev = b
    if white_material is not None:
        me = st_mesh(name, V, F, "LK_SAND", uv=None)
        me.materials.clear()
        me.materials.append(white_material)
    else:
        _mat(mat, C)
        me = st_mesh(name, V, F, mat, uv=None)
    me.update()
    ca = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
    cols = []
    for a in alpha:
        cols += [1.0, 1.0, 1.0, a]
    ca.data.foreach_set("color", cols)
    me.uv_layers.new(name="UVMap")
    lv = np.empty(len(me.loops), np.int64)
    me.loops.foreach_get("vertex_index", lv)
    co = np.array([v.co[:] for v in me.vertices])
    me.uv_layers[0].data.foreach_set("uv", np.stack([co[lv][:, 0] / 8.0, co[lv][:, 1] / 8.0], 1).ravel())
    _env(name, me)
    n = np.array([pl.normal.z for pl in me.polygons])
    print(f"SELFTEST: skirt {name} {mat if white_material is None else white_material.name}: {len(me.polygons)} polygons, mean |nz| {float(np.abs(n).mean()):.2f}", flush=True)


def m_foam_skirt_sloped_nz0p45(C):
    _skirt(C, "WATER_SURF_90", "LK_SURF", nz=0.45, run=1.5, z_top=3.0)


def m_foam_wall_vertical_surf(C):
    _skirt(C, "WATER_SURF_91", "LK_SURF", nz=None, run=0.0, drop=2.0, z_top=2.0, o_in=0.3)


def m_foam_ramp_steep_lk_fall(C):
    _skirt(C, "WATER_FALL_92", "LK_FALL", nz=0.1, run=0.3, z_top=3.0)


def m_foam_white_material_skirt_nz0p3(C):
    mt = bpy.data.materials.new("MAT_SPRAY_SKIRT")
    mt.use_nodes = True
    for nd in mt.node_tree.nodes:
        if nd.type == "BSDF_PRINCIPLED":
            nd.inputs["Base Color"].default_value = (0.96, 0.97, 0.98, 1.0)
    _skirt(C, "WATER_WHITEWALL_93", None, nz=0.3, run=1.0, z_top=3.0, white_material=mt)


def _cluster_template(kind, mat):
    """(points, polygons) of a regular solid / a natural hull rock."""
    if kind == "octa":
        P = np.array([(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)], float)
        return P, [(0, 2, 4), (2, 1, 4), (1, 3, 4), (3, 0, 4), (2, 0, 5), (1, 2, 5), (3, 1, 5), (0, 3, 5)]
    if kind == "hull":
        me = st_hull_rock("CLUSTER_HULL_T", 77, mat, dims=(1.6, 1.3, 1.0))
    else:
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=1 if kind == "ico12" else 2, radius=1.0)
        me = bpy.data.meshes.new("CLUSTER_T")
        bm.to_mesh(me)
        bm.free()
    P = np.array([v.co[:] for v in me.vertices], float)
    F = [tuple(int(i) for i in pl.vertices) for pl in me.polygons]
    bpy.data.meshes.remove(me)
    return P, F


def _welded_cluster(kind, n, seed, mat, jitter_after=0.0):
    """n copies of one regular solid (sizes 0.7..1.1) glued to an earlier copy by ONE shared vertex and welded into a single connected mesh: valences {5, 10, 15, ...}
    for icosahedra - the skeptic's round-3 rock (a bundle that no per-component valence test can take apart)."""
    rng = np.random.default_rng(seed)
    P0, F0 = _cluster_template(kind, mat)
    placed = []
    for k in range(n):
        sc = rng.uniform(0.7, 1.1)
        Pn = P0 * sc
        if k:
            Pj = placed[int(rng.integers(0, k))]
            Pn = Pn + (Pj[int(rng.integers(0, len(Pj)))] - Pn[int(rng.integers(0, len(Pn)))])
        placed.append(Pn)
    bm = bmesh.new()
    verts = {}
    for Pn in placed:
        loc = []
        for p in Pn:
            key = tuple(np.round(p, 5))
            if key not in verts:
                verts[key] = bm.verts.new(tuple(p))
            loc.append(verts[key])
        for f in F0:
            try:
                bm.faces.new([loc[i] for i in f])
            except ValueError:
                pass
    if jitter_after:
        for v in bm.verts:
            v.co = v.co + Vector(rng.uniform(-jitter_after, jitter_after, 3) * 0.5)
    me = st_bm_mesh(f"WCL_{kind}_{n}_{seed}", bm, mat)
    st_box_uv(me)
    return me


def _split_bundle(n, seed, mat, off=0.002, sub=1):
    """n icosahedra (jitter 12 %) merged into one mesh whose faces are ALL split apart: every face owns its 3 vertices, each moved by up to `off` m (2 mm: beyond the 1e-4
    weld, invisible on a 2 m rock) - a triangle soup that no welded-topology test can take apart."""
    me = _ico_bundle(n, 0.12, seed, sub=sub, mat=mat)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.split_edges(bm, edges=list(bm.edges))
    rng = np.random.default_rng(seed)
    for v in bm.verts:
        v.co = v.co + Vector(rng.uniform(-off, off, 3))
    bm.to_mesh(me)
    bm.free()
    st_box_uv(me)
    return me


def m_rocks_split_bundles_2mm(C):
    _swap_all_rocks(lambda k: _split_bundle(5 + k % 4, 700 + k, _rm(C)))


def m_rocks_split_bundles_2mm_loose_only(C):
    for k, nm in enumerate(_rock_names()):
        new = _split_bundle(5 + k % 4, 700 + k, _rm(C))
        st_fit(new, bpy.data.meshes[nm])
        for ob in list(bpy.data.objects):
            if ob.type == "MESH" and ob.data == bpy.data.meshes[nm] and not ob.name.startswith(("ROCK_SEASTACK", "ROCK_PEDESTAL")):
                ob.data = new


def m_rocks_welded_ico12_varied(C):
    _swap_all_rocks(lambda k: _welded_cluster("ico12", 6 + (k * 3) % 9, 100 + 7 * k, _rm(C)))


def m_rocks_welded_ico12_jitter12(C):
    _swap_all_rocks(lambda k: _welded_cluster("ico12", 7 + k % 5, 300 + k, _rm(C), jitter_after=0.12))


def m_rocks_welded_ico42(C):
    _swap_all_rocks(lambda k: _welded_cluster("ico42", 3 + k % 3, 400 + k, _rm(C)))


def m_rocks_welded_octahedra(C):
    _swap_all_rocks(lambda k: _welded_cluster("octa", 9 + k % 6, 500 + k, _rm(C)))


def m_rocks_welded_loose_only_stacks_kept(C):
    for k, nm in enumerate(_rock_names()):
        new = _welded_cluster("ico12", 6 + (k * 3) % 9, 100 + 7 * k, _rm(C))
        st_fit(new, bpy.data.meshes[nm])
        for ob in list(bpy.data.objects):
            if ob.type == "MESH" and ob.data == bpy.data.meshes[nm] and not ob.name.startswith(("ROCK_SEASTACK", "ROCK_PEDESTAL")):
                ob.data = new


def m_ctl_rocks_welded_natural_hull_stones(C):
    """Control: natural (library-style) hull stones that merely touch at a vertex are still rocks (every patch is a manifold hull with 4+ distinct valences)."""
    _swap_all_rocks(lambda k: _welded_cluster("hull", 2 + k % 3, 600 + k, _rm(C)))


def _lib_mesh(C, kind, name, bake=(1.0, 1.0, 1.0), drop_face=False, turn90=False):
    sig, err = lib_signature()
    if kind not in sig:
        raise RuntimeError(f"props library kind {kind} unavailable ({err})")
    V = sig[kind]["V"].copy()
    if turn90:
        V = np.stack([-V[:, 1], V[:, 0], V[:, 2]], 1)
    me = bpy.data.meshes.new(name)
    me.from_pydata((V * np.array(bake)).tolist(), [], sig[kind]["F"])
    me.update()
    me.materials.append(_mat(_rm(C), C))
    if drop_face:
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[bm.faces[0]], context="FACES_ONLY")
        bm.to_mesh(me)
        bm.free()
    st_box_uv(me)
    return me


def m_stretch_lib_baked_4to0p6_one_face_removed(C):
    p = _land_spot(C)
    st_obj("ROCK_SLAB_77", _lib_mesh(C, "ROCK_SLAB_A", "LIB_SLAB_CHIP", (4.0, 0.6, 1.0), drop_face=True), (p[0], p[1], p[2] + 0.1), root=_root())


def m_stretch_lib_turned_90_baked_3to1(C):
    p = _land_spot(C)
    st_obj("ROCK_BOULDER_A.903", _lib_mesh(C, "ROCK_BOULDER_A", "LIB_BOULDER_TURNED", (3.0, 1.0, 1.0), turn90=True), (p[0], p[1], p[2] + 0.1), root=_root())


def _handmade_baked(C, name, bake, seed=77):
    p = _land_spot(C)
    me = st_hull_rock(f"HULL_{name}", seed, _rm(C), dims=(2.0, 1.6, 1.2))
    for v in me.vertices:
        v.co.x *= bake[0]
        v.co.y *= bake[1]
        v.co.z *= bake[2]
    me.update()
    st_box_uv(me)
    st_obj(name, me, (p[0], p[1], p[2] + 0.1), root=_root())


def m_stretch_handmade_baked_4to0p5(C):
    _handmade_baked(C, "ROCK_WALL_77", (4.0, 0.5, 1.0))


def m_stretch_handmade_baked_5to0p4(C):
    _handmade_baked(C, "ROCK_LARGE.099", (5.0, 0.4, 1.0), seed=5)


def m_ctl_handmade_baked_1p5(C):
    _handmade_baked(C, "ROCK_MEDIUM.098", (1.5, 1.0, 1.0))


def m_ctl_lib_one_face_removed_unstretched(C):
    p = _land_spot(C)
    st_obj("ROCK_SLAB_A.904", _lib_mesh(C, "ROCK_SLAB_A", "LIB_SLAB_CHIP2", (1.0, 1.0, 1.0), drop_face=True), (p[0], p[1], p[2] + 0.1), root=_root())


def _terrain_wall_face(C, mode):
    """A non-up-facing TERRAIN polygon (cliff wall / underside) whose vertices belong to NO up-facing face (so every top-surface gate stays blind to it):
    deleted, flipped, one vertex moved 5 cm, or poked (control: the same surface re-triangulated)."""
    ob = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("TERRAIN")][0]
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    mw = np.array(ob.matrix_world)
    isup = {f.index: (mw[:3, :3] @ np.array(f.normal))[2] > 0.5 for f in bm.faces}
    best = None
    for f in bm.faces:
        if isup[f.index] or f.calc_area() < 1e-4:
            continue
        if mode == "poke":                                            # a poked non-planar polygon is NOT the same surface: the control needs a flat one (< 0.2 mm, far under the gate's 1 mm)
            Q = np.array([v.co[:] for v in f.verts])
            if len(Q) > 3 and float(np.linalg.svd(Q - Q.mean(0), compute_uv=False)[-1]) > 2e-4:
                continue
        if mode != "poke" and any(isup[g.index] for v in f.verts for g in v.link_faces):
            continue
        if best is None or f.calc_area() > best.calc_area():
            best = f
    if best is None:
        raise RuntimeError("no isolated non-up-facing TERRAIN polygon")
    area = best.calc_area()
    if mode == "delete":
        bmesh.ops.delete(bm, geom=[best], context="FACES_ONLY")
    elif mode == "flip":
        best.normal_flip()
    elif mode == "move":
        best.verts[0].co.x += 0.05
    elif mode == "poke":
        bmesh.ops.poke(bm, faces=[best], offset=0.0)
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
    print(f"SELFTEST: {mode} TERRAIN wall polygon ({area:.2f} m2)", flush=True)


def m_terrain_wall_deleted(C):
    _terrain_wall_face(C, "delete")


def m_terrain_wall_flipped(C):
    _terrain_wall_face(C, "flip")


def m_terrain_wall_vertex_moved(C):
    _terrain_wall_face(C, "move")


def m_ctl_terrain_wall_poked(C):
    _terrain_wall_face(C, "poke")


# ---- v2 area U mutations (2026-10-04, review of the rock / foam / look gates): rocks are recognised by SHAPE and MATERIAL, not by name; stretch = anisotropy; pale non-LK foam
def _dress_at(C, name, me, k=0, scale=(1.0, 1.0, 1.0), dz=0.5):
    p = _land_spot(C, k)
    return st_obj(name, me, (p[0], p[1], p[2] + dz), scale, root=_root())


def _poked_ico(name, mat):
    """An icosahedron with ONE face poked: 13 vertices, 22 faces (the review's '13-vertex icosahedra': one vertex more than the old 12 / 20 test looked for)."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    bm.faces.ensure_lookup_table()
    bmesh.ops.poke(bm, faces=[bm.faces[0]], offset=0.15)
    return st_bm_mesh(name, bm, mat)


def m_dress_boulder_ico42(C):
    """A noisy 42-vertex icosphere (+-30 % radial noise) named DRESS_BOULDER, rock material: the old gates selected rocks by the ROCK_ prefix and never looked at it."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    rng = np.random.default_rng(42)
    for v in bm.verts:
        v.co *= 1.0 + rng.uniform(-0.3, 0.3)
    _dress_at(C, "DRESS_BOULDER", st_bm_mesh("NOISY42", bm, _rm(C)), 0, (1.3, 1.3, 1.3))


def m_dress_ico13_poked(C):
    _dress_at(C, "DRESS_PEBBLE", _poked_ico("POKED13", _rm(C)), 1, (1.2, 1.2, 1.2))


def m_dress_ico13_poked_rough(C):
    """The same toy rock wearing the grass material (neither a rock name nor a rock material): only its SHAPE (a closed solid of 13 welded vertices) gives it away."""
    _dress_at(C, "DRESS_PEBBLE_G", _poked_ico("POKED13G", "LK_ROUGH"), 2, (1.2, 1.2, 1.2))


def m_dress_welded_cluster(C):
    _dress_at(C, "DRESS_CLUSTER", _welded_cluster("ico12", 8, 33, _rm(C)), 3)


def m_skin_named_ico(C):
    """A closed 12-vertex icosahedron called ROCK_SKIN_99: the old selection skipped every ROCK_SKIN_* name; cladding is an open shell, this is a toy rock."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    _dress_at(C, "ROCK_SKIN_99", st_bm_mesh("SKINICO", bm, _rm(C)), 4, (1.2, 1.2, 1.2))


def m_prop_named_stretch(C):
    """A real library rock called PROP_7 (neither ROCK_ nor DRESS_) stretched 3 : 1 : 1 - the stretch gate looked only at ROCK_ / CLIFF_ROCK / DRESS_ names."""
    _lib_object(C, "ROCK_BOULDER_A", "PROP_7", scale=(3.0, 1.0, 1.0))


def m_shrink_0p3(C):
    _lib_object(C, "ROCK_BOULDER_A", "ROCK_STONE_X1", scale=(0.3, 0.3, 0.3))


def m_ctl_pebble_0p4(C):
    """A library rock shrunk UNIFORMLY to 0.4: a pebble, not a stretched rock (the old 0.5 lower bound called it stretched)."""
    _lib_object(C, "ROCK_BOULDER_A", "ROCK_STONE_X2", scale=(0.4, 0.4, 0.4))


def m_ctl_dress_real_rock(C):
    """A real library rock under a DRESS_ name: classified by shape + material, judged, and fine."""
    _lib_object(C, "ROCK_BOULDER_A", "DRESS_BOULDER_OK", scale=(1.2, 1.1, 1.0))


def m_dress_stack_boxes(C):
    """The sea stacks replaced by stacked boxes and renamed DRESS_STACK_nn (rock material): the old tall-rock selection took ROCK_* names only, so the taper gate found no stack."""
    for k, o in enumerate(_stacks()):
        o.data = _stk(st_box_stack(f"ZIG{k}", _rm(C)), 1.0)
        o.name = f"DRESS_STACK_{k:02d}"


def m_stack_boxes_wall_named(C):
    """The same boxes called ROCK_WALL_STK_nn: the old selection exempted ROCK_WALL_*."""
    for k, o in enumerate(_stacks()):
        o.data = _stk(st_box_stack(f"WZIG{k}", _rm(C)), 1.0)
        o.name = f"ROCK_WALL_STK_{k:02d}"


def _foam_band(C, matname, linear, objname):
    mt = bpy.data.materials.new(matname)
    mt.use_nodes = True
    for nd in mt.node_tree.nodes:
        if nd.type == "BSDF_PRINCIPLED":
            nd.inputs["Base Color"].default_value = (linear[0], linear[1], linear[2], 1.0)
    P, N, LID = _shore(C, 2.0)
    best = max(split_by_loop(np.arange(len(P)), LID), key=len)
    idx = best[3:40]
    me = st_strip(objname, P[idx], N[idx], 3.0, 6.0, 0.14, mat="LK_SAND", uv_world=8.0)
    me.materials.clear()
    me.materials.append(mt)
    for pl in me.polygons:
        pl.material_index = 0
    _env(objname, me)


def m_foam_pale_srgb(C):
    """A band at the shore in a pale non-LK material: base colour LINEAR 0.75 = sRGB 0.88 (V 0.88, S 0): the raw test (V >= 0.8 on the stored value) read 0.75 and let it through."""
    _foam_band(C, "MAT_PALE_BAND_TEST", (0.75, 0.75, 0.75), "WATER_PALEBAND_01")


def m_ctl_foam_sand_band(C):
    """The same band in a sand colour (linear 0.50 / 0.40 / 0.22 = sRGB 0.74 / 0.67 / 0.50: V .74, S .32): not white, must not be counted as foam."""
    _foam_band(C, "MAT_SAND_BAND_TEST", (0.50, 0.40, 0.22), "WATER_SANDBAND_01")


MUT_COMMON = [
    ("green_up_2mm", m_green_up, ["PLAY_SURFACE_GRID", "PLAY_SURFACE_TOPS", "PLAY_SURFACE_FACES"]),
    ("pin_moved_1cm", m_pin, ["MARKERS_FROZEN"]),
    ("plant_renamed_BUNKER", m_plant_bunker, ["PLAY_SURFACE_NAMES", "NEW_OBJECT_PREFIXES"]),
    ("new_object_ARCH_prefix", m_new_arch, ["NEW_OBJECT_PREFIXES"]),
    ("new_object_MARKER_prefix", m_marker_prop, ["RESERVED_NAMES"]),
    ("mat_foam_strip", m_foam, ["NO_MAT_FOAM", "NO_LEGACY_MATERIALS", "NO_FOAM_STRIP"]),
    ("fairway_flat_MAT", m_fairway_flat, ["GROUND_MATERIALS", "NO_LEGACY_MATERIALS"]),
    ("rock_icosahedron_12v", m_rock_ico, ["ROCK_NOT_ICOSAHEDRON"]),
    ("rocks_one_mesh", m_rocks_one, ["ROCK_VARIETY"]),
    ("rocks_all_icosahedra_split_60v", m_rocks_ico_split, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_all_icosahedra_plus_junk_verts", m_rocks_ico_junk, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_all_icospheres_subdiv3", m_rocks_ico3, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_all_uv_spheres", m_rocks_uvsphere, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_all_subdivided_cubes", m_rocks_cube, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_same_shape_in_unique_meshes", m_rocks_copies, ["ROCK_VARIETY"]),
    ("rocks_bundles_of_5to8_jittered_icosahedra", m_rocks_bundle_ico12, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_one_shape_as_edge_split_variants", m_rocks_family_variants, ["ROCK_VARIETY"]),
    ("rocks_bundles_loose_only_stacks_kept", m_rocks_bundle_loose_only, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_bundles_of_42v_icospheres", m_rocks_bundle_ico42, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_icospheres_subdiv3_noise40", m_rocks_ico3_noise40, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_bundles_of_icosahedra_every_face_split_2mm", m_rocks_split_bundles_2mm, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_bundles_of_icosahedra_every_face_split_2mm_loose_only", m_rocks_split_bundles_2mm_loose_only, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_welded_clusters_of_12v_icosahedra_varied", m_rocks_welded_ico12_varied, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_welded_clusters_of_12v_icosahedra_jitter12_after_weld", m_rocks_welded_ico12_jitter12, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_welded_clusters_of_42v_icospheres", m_rocks_welded_ico42, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_welded_clusters_of_octahedra", m_rocks_welded_octahedra, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rocks_welded_clusters_loose_only_stacks_kept", m_rocks_welded_loose_only_stacks_kept, ["ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY"]),
    ("rock_library_mesh_baked_4to0p6_one_face_removed", m_stretch_lib_baked_4to0p6_one_face_removed, ["NO_ROCK_STRETCH_2X"]),
    ("rock_library_mesh_turned_90_baked_3to1", m_stretch_lib_turned_90_baked_3to1, ["NO_ROCK_STRETCH_2X"]),
    ("rock_handmade_hull_baked_4to0p5", m_stretch_handmade_baked_4to0p5, ["NO_ROCK_STRETCH_2X"]),
    ("rock_handmade_hull_baked_5to0p4", m_stretch_handmade_baked_5to0p4, ["NO_ROCK_STRETCH_2X"]),
    ("dress_boulder_noisy_icosphere_42v", m_dress_boulder_ico42, ["ROCK_NOT_ICOSAHEDRON"]),
    ("dress_pebble_icosahedron_13v_poked_face", m_dress_ico13_poked, ["ROCK_NOT_ICOSAHEDRON"]),
    ("dress_pebble_icosahedron_13v_on_grass_material", m_dress_ico13_poked_rough, ["ROCK_NOT_ICOSAHEDRON"]),
    ("dress_cluster_welded_icosahedra", m_dress_welded_cluster, ["ROCK_NOT_ICOSAHEDRON"]),
    ("rock_skin_named_closed_icosahedron", m_skin_named_ico, ["ROCK_NOT_ICOSAHEDRON"]),
    ("rock_named_PROP_7_stretched_3to1", m_prop_named_stretch, ["NO_ROCK_STRETCH_2X"]),
    ("rock_uniform_shrunk_0p3", m_shrink_0p3, ["NO_ROCK_STRETCH_2X"]),
    ("terrain_wall_polygon_deleted", m_terrain_wall_deleted, ["PLAY_SURFACE_WALLS"]),
    ("terrain_wall_polygon_flipped", m_terrain_wall_flipped, ["PLAY_SURFACE_WALLS"]),
    ("terrain_wall_vertex_moved_5cm", m_terrain_wall_vertex_moved, ["PLAY_SURFACE_WALLS"]),
    ("rock_stretched_x3", m_stretch_x3, ["NO_ROCK_STRETCH_2X"]),
    ("rock_uniform_2p6", m_stretch_uniform, ["NO_ROCK_STRETCH_2X"]),
    ("rock_anisotropy_2p5", m_stretch_aniso, ["NO_ROCK_STRETCH_2X"]),
    ("rock_sheared_parent_3p17", m_stretch_sheared_parent, ["NO_ROCK_STRETCH_2X"]),
    ("rock_baked_stretch_library_mesh_renamed", m_stretch_baked_lib_renamed, ["NO_ROCK_STRETCH_2X"]),
    ("dress_small_boulder_between_grid_points", m_dress_small_boulder_between_grid, ["PROPS_OFF_PLAY"]),
    ("plant_merged_with_far_copy_over_sea", m_plant_merged_far_copy, ["NO_PLANT_ON_WATER"]),
    ("plant_on_floating_rock_slab_z5p2", m_plant_on_floating_slab, ["NO_PLANT_ON_WATER"]),
    ("dress_boulder_on_pin", m_dress_boulder_pin, ["PROPS_OFF_PLAY"]),
    ("rock_wall_pile_on_pin", m_wall_pile_pin, ["PROPS_OFF_PLAY"]),
    ("dress_slab_over_fairway", m_dress_slab_fairway, ["PROPS_OFF_PLAY"]),
    ("plant_on_sea_level_water", m_plant_on_sea, ["NO_PLANT_ON_WATER"]),
    ("plant_floating_over_water_z6", m_plant_floating, ["NO_PLANT_ON_WATER"]),
    ("plant_on_fairway", m_plant_on_fairway, ["NO_PLANT_ON_WATER"]),
    ("vine_attached_to_nothing", m_vine_floating, ["NO_PLANT_ON_WATER"]),
    ("plant_on_green", m_plant_on_green, ["PROPS_OFF_PLAY", "NO_PLANT_ON_WATER"]),
    ("lawn_and_flagstick_as_dress", m_lawn_dress, ["ONE_GREEN"]),
    ("face_deleted", m_face_delete, ["PLAY_SURFACE_FACES"]),
    ("face_flipped", m_face_flip, ["PLAY_SURFACE_FACES"]),
    ("face_added", m_face_extra, ["PLAY_SURFACE_FACES"]),
    ("fairway_normal_unlinked", m_normal_unlinked, ["LK_TEXTURED"]),
    ("fairway_no_uv", m_fairway_nouv, ["UV_DENSITY"]),
    ("fairway_uv_0to1", m_fairway_uv01, ["UV_DENSITY"]),
    ("ground_uv_smear_local_64to1", m_ground_uv_smear_local, ["GROUND_UV_LOCAL_SMEAR"]),
    ("wall_uv_smear_16to1", m_wall_uv_16, ["UV_DENSITY"]),
    ("plant_600_faces", m_plant_600, ["PROP_FACE_BUDGET"]),
    ("plant_second_material", m_plant_2mat, ["PLANTS_SHARED_MATERIAL"]),
    ("plants_not_instanced", m_plants_unique, ["PLANTS_INSTANCED"]),
    ("second_green_dress", m_second_green, ["ONE_GREEN"]),
    ("sand_patch_named_BUNKER", m_bunker_patch, ["SAND_NAMES", "PLAY_SURFACE_NAMES"]),
    ("animated_rock", m_animated, ["NO_ANIMATION"]),
    ("300k_triangles", m_heavy, ["TRIANGLE_BUDGET"]),
    # v2 2026-10-05 area U: the sway data of the plants (a plant without it stands still; the caps are caps on the tip weight)
    ("plants_without_sway_colors", m_sway_none, ["PLANT_SWAY_COLORS"]),
    ("plant_one_shrub_mesh_without_sway_colors", m_sway_one_missing, ["PLANT_SWAY_COLORS"]),
    ("plants_root_weight_one_everywhere", m_sway_root_one, ["PLANT_SWAY_COLORS"]),
    ("plants_tip_weight_0p4", m_sway_tip_low, ["PLANT_SWAY_COLORS"]),
    ("plants_B_one_no_sway_marker", m_sway_b_one, ["PLANT_SWAY_COLORS"]),
    ("plants_two_colour_layers", m_sway_two_layers, ["PLANT_SWAY_COLORS"]),
    ("shrub_tip_weight_1p0_over_the_0p03_cap", m_sway_shrub_over, ["PLANT_SWAY_CAPS"]),
    ("flower_tip_weight_1p0_over_the_0p05_cap", m_sway_flower_over, ["PLANT_SWAY_CAPS"]),
]
# controls: (name, function, gates that must PASS afterwards)
CTL_COMMON = [
    ("ctl_plants_sway_power_1p4_ramp", m_sway_quadratic, ["PLANT_SWAY_COLORS", "PLANT_SWAY_CAPS"]),
    ("ctl_uniform_0p4_pebble", m_ctl_pebble_0p4, ["NO_ROCK_STRETCH_2X"]),
    ("ctl_dress_boulder_real_library_rock", m_ctl_dress_real_rock, ["ROCK_NOT_ICOSAHEDRON", "NO_ROCK_STRETCH_2X"]),
    ("ctl_rock_welded_natural_hull_stones", m_ctl_rocks_welded_natural_hull_stones, ["ROCK_NOT_ICOSAHEDRON"]),
    ("ctl_handmade_hull_baked_1p5", m_ctl_handmade_baked_1p5, ["NO_ROCK_STRETCH_2X"]),
    ("ctl_library_mesh_one_face_removed_unstretched", m_ctl_lib_one_face_removed_unstretched, ["NO_ROCK_STRETCH_2X"]),
    ("ctl_terrain_wall_polygon_poked_same_surface", m_ctl_terrain_wall_poked, ["PLAY_SURFACE_WALLS"]),
    ("ctl_rock_scale_1p8_1p5_1p2", m_ctl_scale_ok, ["NO_ROCK_STRETCH_2X"]),
    ("ctl_library_mesh_scaled_within_2x", m_ctl_lib_scaled_ok, ["NO_ROCK_STRETCH_2X"]),
    ("ctl_plant_on_a_rock_on_the_ground", m_ctl_plant_on_rock, ["NO_PLANT_ON_WATER"]),
    ("ctl_dress_sand_overlay_on_rough", m_ctl_sand_rough, ["PROPS_OFF_PLAY", "SAND_NAMES", "PATH_UNDER_BALL"]),
    ("ctl_face_retriangulated", m_ctl_face_retri, ["PLAY_SURFACE_FACES", "PLAY_SURFACE_TOPS"]),
    ("ctl_ground_uv_aniso_2p9", m_ctl_ground_uv_aniso_2p9, ["GROUND_UV_LOCAL_SMEAR"]),
]
MUT_SEA = [
    ("dress_stack_ziggurat_boxes_named_DRESS_STACK", m_dress_stack_boxes, ["SEASTACK_TAPERED", "ROCK_NOT_ICOSAHEDRON"]),
    ("stack_boxes_named_ROCK_WALL", m_stack_boxes_wall_named, ["SEASTACK_TAPERED"]),
    ("foam_pale_non_LK_band_srgb_0p88", m_foam_pale_srgb, ["NO_FOAM_STRIP"]),
    ("ocean_flat_MAT_WATER", m_ocean_flat, ["OCEAN_WATER"]),
    ("surf_removed", m_surf_gone, ["SURF_SHORE"]),
    ("surf_no_vertex_colour", m_surf_nocol, ["SURF_SHORE"]),
    ("white_band_as_DRESS_LK_SURF", m_white_band_dress, ["NO_FOAM_STRIP", "PROP_MATERIALS"]),
    ("surf_strip_alpha1_whole_shore", m_surf_band_alpha1, ["NO_FOAM_STRIP"]),
    ("surf_unbroken_14m_patch", m_surf_long_patch, ["NO_FOAM_STRIP"]),
    ("surf_patches_every_20m_over_25pct", m_surf_dense, ["NO_FOAM_STRIP"]),
    ("surf_two_patches_4m_apart", m_surf_close_gap, ["NO_FOAM_STRIP"]),
    ("surf_ribbon_8m_offshore_constant_width", m_surf_ribbon_offshore, ["NO_FOAM_STRIP"]),
    ("flat_LK_FALL_ribbon_8m_offshore", m_fall_flat_strip, ["NO_FOAM_STRIP"]),
    ("white_textured_DRESS_mist_along_shore", m_white_dress_smoke, ["NO_FOAM_STRIP"]),
    ("shelf_halo_16m_closed_ring", m_shelf_halo, ["SHELF_THIN"]),
    ("shelf_thin_closed_uniform_ring_1p5m", m_shelf_thin_ring, ["SHELF_THIN"]),
    ("shelf_halo_hidden_in_a_second_mesh", m_shelf_second_halo_object, ["SHELF_THIN"]),
    ("shelf_12m_wide_patches", m_shelf_wide_patches, ["SHELF_THIN"]),
    ("stacks_stacked_boxes", m_stack_boxes, ["SEASTACK_TAPERED"]),
    ("stacks_cylinders", m_stack_cylinder, ["SEASTACK_TAPERED"]),
    ("stacks_flat_lid", m_stack_flat_lid, ["SEASTACK_TAPERED"]),
    ("stack_tower_named_ROCK_PEDESTAL", m_stack_pedestal_named_tower, ["SEASTACK_TAPERED"]),
    ("stack_flat_island_without_grass_cap", m_stack_island_without_cap, ["SEASTACK_TAPERED"]),
    ("foam_squares_touching_2to6m_offshore", m_foam_squares_touching, ["NO_FOAM_STRIP"]),
    ("foam_ragged_continuous_band_2p5m_offshore", m_foam_ragged_band_off2p5, ["NO_FOAM_STRIP"]),
    ("foam_offshore_dashes_gap7m_14to17m", m_foam_offshore_dashes_gap7, ["NO_FOAM_STRIP"]),
    ("foam_continuous_band_at_vertex_alpha_0p29", m_foam_ghost_band_alpha_0p29, ["NO_FOAM_STRIP"]),
    ("foam_white_material_strip_not_LK_SURF", m_foam_white_material_strip, ["NO_FOAM_STRIP"]),
    ("foam_continuous_sloped_skirt_nz0p45_LK_SURF", m_foam_skirt_sloped_nz0p45, ["NO_FOAM_STRIP"]),
    ("foam_continuous_vertical_wall_LK_SURF", m_foam_wall_vertical_surf, ["NO_FOAM_STRIP"]),
    ("foam_continuous_steep_ramp_nz0p1_LK_FALL_named_WATER_FALL", m_foam_ramp_steep_lk_fall, ["NO_FOAM_STRIP"]),
    ("foam_continuous_sloped_skirt_nz0p3_white_material", m_foam_white_material_skirt_nz0p3, ["NO_FOAM_STRIP"]),
]
CTL_SEA = [
    ("ctl_foam_sand_coloured_non_LK_band", m_ctl_foam_sand_band, ["NO_FOAM_STRIP"]),
    ("ctl_flat_island_with_grass_cap", m_ctl_island_with_grass_cap, ["SEASTACK_TAPERED"]),
]
MUT_HOLE = {
    8: [("path_removed", m_path_gone, ["NEEDLE_PATH"]), ("path_floating_2m", m_path_float, ["NEEDLE_PATH"]),
        ("path_3p5cm_over_ground", m_path_3p5, ["PATH_UNDER_BALL"]), ("path_11cm_over_ground", m_path_11, ["PATH_UNDER_BALL"]),
        ("path_buried_2cm", m_path_buried, ["PATH_UNDER_BALL"]), ("plant_on_stone_path", m_plant_on_path, ["NO_PLANT_ON_WATER"]),
        ("flowers_not_purple", m_flowers_white, ["NEEDLE_FLOWERS_SHRUBS_TUFTS"]), ("flowers_only_10", m_flowers_few, ["NEEDLE_FLOWERS_SHRUBS_TUFTS"]),
        ("arch_icosahedra", m_arch_icos, ["NEEDLE_ARCH"]), ("arch_without_vines", m_arch_novines, ["NEEDLE_ARCH"]),
        ("arch_without_rock", m_arch_nopedestal, ["NEEDLE_ARCH"]), ("arch_solid_wall_no_opening", m_arch_solid, ["NEEDLE_ARCH"]),
        ("vines_removed", m_wall_vines_gone, ["NEEDLE_WALL_VINES", "NEEDLE_ARCH"]),
        ("waterfall_as_platform", m_fall_platform, ["NEEDLE_WATERFALL"]), ("waterfall_uv_upward", m_fall_uv_up, ["NEEDLE_WATERFALL"]),
        ("waterfall_no_surf", m_surf_gone, ["NEEDLE_WATERFALL"]), ("sea_stacks_removed", m_stacks_gone, ["NEEDLE_SEA_STACKS"]),
        ("stacks_stacked_boxes_not_counted", m_stack_boxes, ["NEEDLE_SEA_STACKS"]),
        ("rock_skin_removed", m_skin_gone, ["NEEDLE_CLIFF_SKIN"]), ("rock_skin_smooth_curtain", m_skin_smooth, ["NEEDLE_CLIFF_SKIN"])],
    9: [("scrub_on_ribbon", m_scrub_ribbon, ["SPLIT_NO_SCRUB_RIBBON"]), ("ridge_rough_green", m_ridge_green, ["SPLIT_SCRUB_RIDGE"]),
        ("ridge_without_plants", m_ridge_bare, ["SPLIT_RIDGE_SCATTER"]), ("ruin_removed", m_ruin_gone, ["SPLIT_RUIN"]),
        ("rock_skin_one_piece", m_skin_partial, ["SPLIT_SHORE_SKIN"]), ("rock_skin_smooth_curtain", m_skin_smooth, ["SPLIT_SHORE_SKIN"])],
    10: [("walls_smooth_blobs", m_walls_blobs, ["CRATER_COLUMNS"]), ("lava_one_polygon", m_lava_one, ["CRATER_LAVA"]),
         ("lava_raised_to_play", m_lava_raised, ["CRATER_LAVA"]), ("lava_lights_removed", m_lights_gone, ["CRATER_LAVA_LIGHTS"]),
         ("lava_lights_9", m_lights_many, ["CRATER_LAVA_LIGHTS"]), ("pillar_sides_LK_ROCK", m_pillar_rock, ["CRATER_PILLAR"]),
         ("cone_below_walls", m_cone_low, ["CRATER_CONE_SMOKE"]), ("smoke_removed", m_smoke_gone, ["CRATER_CONE_SMOKE", "SMOKE_IN_FRAME"]),
         ("smoke_cards_off_every_frame", m_smoke_far, ["SMOKE_IN_FRAME"]), ("smoke_cards_a_quarter_size", m_smoke_tiny, ["SMOKE_IN_FRAME"]),
         ("smoke_cards_behind_an_opaque_wall", m_smoke_walled, ["SMOKE_IN_FRAME"]),
         ("shelf_without_plants", m_ridge_bare, ["CRATER_SHELF_PLANTS"]), ("tee_flat_MAT_GREEN", m_tee_flat, ["CRATER_TEE_PAD"]),
         ("crater_wall_not_covered", m_crater_bare, ["CRATER_WALL_COVERED"]),
         ("crater_lip_grass_band_exposed", m_crater_lip_exposed, ["CRATER_WALL_COVERED"]),
         ("crater_wall_cladding_12m_hole", m_crater_wall_hole12, ["CRATER_WALL_COVERED"])],
}
CTL_HOLE = {8: [("ctl_path_1cm_over_ground", m_ctl_path_1, ["PATH_UNDER_BALL", "NEEDLE_PATH"])], 9: [], 10: []}
# texture mutations: (name, hole set, edit(look dir), gates that must fail)
TEX_MUT = [
    ("tex_fairway_flat_rgb_noisy_alpha", (8, 9, 10), t_flat_alpha, ["LOOK_FILES"]),
    ("tex_fairway_near_flat_dither", (8, 9, 10), t_near_flat, ["LOOK_FILES"]),
    ("tex_fairway_flat_fill", (8, 9, 10), t_flat_fill, ["LOOK_FILES"]),
    ("tex_fairway_artwork_downsampled_to_128px", (8, 9, 10), t_look_downsampled_128, ["LOOK_FILES"]),
    ("tex_fairway_8px_checkerboard", (8, 9, 10), t_look_checkerboard, ["LOOK_FILES"]),
    ("tex_fairway_random_16px_cell_tiled", (8, 9, 10), t_look_tiled_cell, ["LOOK_FILES"]),
    ("tex_fairway_normal_flat_dither", (8, 9, 10), t_flat_normal, ["LOOK_FILES"]),
    ("tex_green_is_copy_of_fairway", (8, 9, 10), t_green_copy, ["ALBEDO_DISTINCT"]),
    ("tex_green_is_tint_of_fairway", (8, 9, 10), t_green_tint, ["ALBEDO_DISTINCT"]),
    ("tex_green_is_rotated_copy_of_fairway", (8, 9, 10), t_green_rotated_copy, ["ALBEDO_DISTINCT"]),
    ("tex_green_is_shifted_copy_of_fairway", (8, 9, 10), t_green_shifted_copy, ["ALBEDO_DISTINCT"]),
    ("tex_fairway_smooth_below_stills_floor", (8, 9, 10), t_fairway_smooth_below_floor, ["LOOK_FILES"]),
    ("tex_fairway_hue_97_blue_green", (8, 9, 10), t_fairway_bluegreen, ["GRASS_ALBEDO_HUE"]),
    ("tex_rough_hue_95_blue_green", (8, 9, 10), t_rough_bluegreen, ["GRASS_ALBEDO_HUE"]),
    ("tex_fairway_desaturated", (8, 9, 10), t_fairway_desat, ["GRASS_ALBEDO_HUE"]),
    ("tex_scrub_is_copy_of_fairway", (9,), t_scrub_copy, ["SPLIT_SCRUB_RIDGE", "ALBEDO_DISTINCT"]),
    ("tex_scrub_is_fairway_green_other_noise", (9,), t_scrub_green, ["SPLIT_SCRUB_RIDGE"]),
    ("tex_scrub_is_hue_shifted_fairway_pattern", (9,), t_scrub_hueshifted_fairway, ["SPLIT_SCRUB_RIDGE", "ALBEDO_DISTINCT"]),
    ("tex_scrub_is_channel_tinted_fairway_pattern", (9,), t_scrub_tinted_fairway, ["SPLIT_SCRUB_RIDGE", "ALBEDO_DISTINCT"]),
    ("tex_green_is_hue_shifted_fairway_pattern", (8, 9, 10), t_green_hueshifted_fairway, ["ALBEDO_DISTINCT"]),
    ("tex_lava_old_voronoi_crust", (10,), t_lava_old, ["LAVA_TEXTURE"]),
    ("tex_lava_voronoi_dark_plates", (10,), t_lava_voronoi_dark, ["LAVA_TEXTURE"]),
    ("tex_lava_voronoi_bright_cells", (10,), t_lava_voronoi_bright, ["LAVA_TEXTURE"]),
    ("tex_lava_flat_orange", (10,), t_lava_flat, ["LAVA_TEXTURE", "LOOK_FILES"]),
    ("tex_lava_flat_orange_emission_structured_albedo", (10,), t_lava_flat_emission, ["LAVA_TEXTURE"]),
    ("tex_lava_emission_yellow_peak", (10,), t_lava_yellow, ["LAVA_TEXTURE"]),
    ("tex_lava_tile_seam", (10,), t_lava_seam, ["LAVA_TEXTURE"]),
    ("tex_lava_dark_crust_40pct", (10,), t_lava_darkcrust, ["LAVA_TEXTURE"]),
    ("tex_basalt_old_bricks_and_ladders", (10,), t_basalt_old, ["BASALT_PRISMS"]),
    ("tex_basalt_glow_ladders", (10,), t_basalt_ladder, ["BASALT_PRISMS"]),
    ("tex_basalt_cross_joint_bricks", (10,), t_basalt_bricks, ["BASALT_PRISMS"]),
    ("tex_basalt_flat_bevels", (10,), t_basalt_flatbevel, ["BASALT_PRISMS"]),
    ("tex_smoke_old_hard_ball", (10,), t_smoke_old, ["SMOKE_SOFT"]),
    ("tex_smoke_hard_edge", (10,), t_smoke_hard, ["SMOKE_SOFT"]),
    ("tex_smoke_visible_card_border", (10,), t_smoke_border, ["SMOKE_SOFT"]),
    ("tex_smoke_opaque_core", (10,), t_smoke_opaque, ["SMOKE_SOFT"]),
    ("tex_smoke_round_ball", (10,), t_smoke_ball, ["SMOKE_SOFT"]),
    ("tex_smoke_faint_alpha_0p06", (10,), t_smoke_faint, ["SMOKE_IN_FRAME"]),
]
# texture controls: (name, hole set, edit(look dir), gates that must still PASS)
TEX_CTL = [
    ("tex_ctl_fairway_at_stills_level", (8, 9, 10), t_fairway_at_stills_level, ["LOOK_FILES"]),
]
BLEND_ONLY = {"LK_TEXTURED", "NO_ANIMATION"}      # not meaningful in the FBX stage (the export bakes no animation; LK nodes are stripped)
# mutants whose bad content does not survive the FBX export, so the FBX stage is not expected to fail: the FBX exporter writes a TRS per object and has no shear,
# so a rock under a rotated, non-uniformly scaled parent reaches Unity as the nearest shear-free matrix (scale 1.41 / 1.41 / 1.0, no stretch past 2x)
MUT_BLEND_ONLY_NAMES = {"rock_sheared_parent_3p17"}


def quiet_check(hole, o):
    import contextlib
    import io
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        fails, res = check(hole, o)
    return fails, res, buf.getvalue()


V2_GATES = ["PLAY_SURFACE_WALLS", "PROP_MATERIALS", "ROCK_NOT_ICOSAHEDRON", "ROCK_VARIETY", "LOOK_FILES", "NO_FOAM_STRIP", "SURF_SHORE", "SPLIT_SCRUB_RIDGE", "PROPS_OFF_PLAY", "PLAY_SURFACE_FACES",
            "GRASS_ALBEDO_HUE", "ALBEDO_DISTINCT", "SHELF_THIN", "LAVA_TEXTURE", "BASALT_PRISMS", "SMOKE_SOFT", "NO_PLANT_ON_WATER", "NO_ROCK_STRETCH_2X",
            "PATH_UNDER_BALL", "CRATER_WALL_COVERED", "SEASTACK_TAPERED", "UV_DENSITY", "GROUND_UV_LOCAL_SMEAR", "NEEDLE_ARCH", "ONE_GREEN", "NEEDLE_SEA_STACKS",
            "NEEDLE_CLIFF_SKIN", "SPLIT_SHORE_SKIN", "SHADERS_STATIC", "PLANT_SWAY_COLORS", "PLANT_SWAY_CAPS", "HOLE07_UNTOUCHED", "STILL_FLOOR_SELFCHECK", "LOOK_STILLS_TEXTURED", "SMOKE_IN_FRAME", "SMOKE_VISIBLE_STILLS"]


def selftest(holes, opts):
    root_dir = opts.get("selftest_dir") or tempfile.mkdtemp(prefix="postcard_look_selftest_")
    os.makedirs(root_dir, exist_ok=True)
    opts = dict(opts, stills_dir=os.path.join(root_dir, "no_game_view_stills_in_selftest"), require_stills=False)       # the self-test never reads the repo's proof stills
    look = os.path.join(root_dir, "Look")
    fbx_mode = bool(opts.get("selftest_fbx"))
    only = opts.get("selftest_only")
    print(f"SELFTEST: writing only under {root_dir}; FBX stage for every mutant: {fbx_mode}", flush=True)
    st_textures(look)
    lines, bad = [], 0
    rec = []                                   # per-mutant records for the per-gate summary
    good = {}                                  # gate -> [passed positives, positives]

    def say(ok, what):
        nonlocal bad
        bad += 0 if ok else 1
        ln = f"SELFTEST: {what} {'PASS' if ok else 'FAIL'}"
        lines.append(ln)
        print(ln, flush=True)

    def stage_fails(res, names):
        """Failed gate names split by stage: ({blend-stage or file-level names}, {fbx names})."""
        b = {r["name"].split("@")[0] for r in res if not r["ok"] and not r["name"].endswith("@fbx")}
        f = {r["name"].split("@")[0] for r in res if not r["ok"] and r["name"].endswith("@fbx")}
        return b, f

    for h in holes:
        if h == 10:
            opts = dict(opts, smoke_shots=st_smoke_shots(dict(opts, look_dir=look))[0])           # SMOKE_IN_FRAME: the synthetic cards are 600 m away from the real proof cameras
        blend, fbx = st_build(h, root_dir, look, opts)
        o = dict(opts, blend=blend, fbx=fbx, look_dir=look, json=os.path.join(root_dir, f"hole_{h:02d}_pos.json"), no_blend=False, no_fbx=False)
        fails, res = check(h, o)
        say(not fails, f"hole {h} synthetic positive (blend + FBX): {len(res)} gates, expected ALL PASS, failed {fails}")
        for r in res:
            g = r["name"].split("@")[0]
            e = good.setdefault(g, [0, 0])
            e[1] += 1
            e[0] += 1 if r["ok"] else 0
        muts = MUT_COMMON + (MUT_SEA if h in (8, 9) else []) + MUT_HOLE[h]
        ctls = CTL_COMMON + CTL_HOLE[h] + (CTL_SEA if h in (8, 9) else [])
        if only:
            muts = [m for m in muts if any(k in m[0] for k in only)]
            ctls = [m for m in ctls if any(k in m[0] for k in only)]

        def run_variant(name, fn, expect, must_pass, kind):
            bpy.ops.wm.open_mainfile(filepath=blend, load_ui=False)
            C = st_ctx(h, dict(opts, look_dir=look))
            try:
                fn(C)
            except Exception as e:  # noqa: BLE001
                traceback.print_exc()
                say(False, f"hole {h} {kind} {name}: the mutation itself raised {type(e).__name__}: {e}")
                return
            mb = os.path.join(root_dir, f"hole_{h:02d}_mut_{name}.blend")
            bpy.ops.wm.save_as_mainfile(filepath=mb, copy=True)
            mf = None
            if fbx_mode:
                mf = os.path.join(root_dir, f"hole_{h:02d}_mut_{name}.fbx")
                st_export(h, mf)
            f2, r2, _out = quiet_check(h, dict(opts, blend=mb, fbx=mf, no_fbx=not fbx_mode, look_dir=look, json=None))
            fb, ff = stage_fails(r2, None)
            passed_b = {r["name"].split("@")[0] for r in r2 if r["ok"] and not r["name"].endswith("@fbx")}
            passed_f = {r["name"].split("@")[0] for r in r2 if r["ok"] and r["name"].endswith("@fbx")}
            if kind == "mutation":
                got_b = [g for g in expect if g in fb]
                got_f = [g for g in expect if g in ff or g in BLEND_ONLY or name in MUT_BLEND_ONLY_NAMES] if fbx_mode else got_b
                ok = len(got_b) == len(expect) and len(got_f) == len(expect)
                other = sorted((fb | ff) - set(expect))
                say(ok, f"hole {h} mutation {name}: must FAIL {expect}, failed {got_b}" + (f" / FBX {[g for g in expect if g in ff]}" if fbx_mode else "")
                    + (f" (also failed: {other})" if other else ""))
                rec.append(dict(hole=h, name=name, kind="bad", expect=expect, ok=ok))
            else:
                okp = all(g in passed_b for g in must_pass) and (not fbx_mode or all(g in passed_f or g in BLEND_ONLY for g in must_pass))
                say(okp, f"hole {h} control {name}: must still PASS {must_pass}, failed now {sorted(fb | ff)}")
                rec.append(dict(hole=h, name=name, kind="good", expect=must_pass, ok=okp))
            if not opts.get("keep"):
                for pth in (mb, mf):
                    if pth and os.path.exists(pth):
                        os.remove(pth)
        for name, fn, expect in muts:
            run_variant(name, fn, expect, None, "mutation")
        for name, fn, must_pass in ctls:
            run_variant(name, fn, None, must_pass, "control")
        # texture mutations: a copy of the synthetic Look folder with files edited
        for name, hs, edit, expect in TEX_MUT:
            if h not in hs or (only and not any(k in name for k in only)):
                continue
            d = os.path.join(root_dir, f"Look_{name}")
            if os.path.isdir(d):
                shutil.rmtree(d)
            shutil.copytree(look, d)
            IMG_CACHE.clear()
            try:
                edit(d)
            except Exception as e:  # noqa: BLE001
                traceback.print_exc()
                say(False, f"hole {h} texture mutation {name}: raised {type(e).__name__}: {e}")
                continue
            IMG_CACHE.clear()
            f2, r2, _out = quiet_check(h, dict(opts, blend=blend, fbx=fbx, no_fbx=not fbx_mode, look_dir=d, json=None))
            fb, ff = stage_fails(r2, None)
            got_b = [g for g in expect if g in fb]
            got_f = [g for g in expect if g in ff] if fbx_mode else got_b
            ok = len(got_b) == len(expect) and len(got_f) == len(expect)
            other = sorted((fb | ff) - set(expect) - BLEND_ONLY)
            say(ok, f"hole {h} mutation {name}: must FAIL {expect}, failed {got_b}" + (f" / FBX {got_f}" if fbx_mode else "") + (f" (also failed: {other})" if other else ""))
            rec.append(dict(hole=h, name=name, kind="bad", expect=expect, ok=ok))
            shutil.rmtree(d, ignore_errors=True)
            IMG_CACHE.clear()
        for name, hs, edit, must_pass in TEX_CTL:
            if h not in hs or (only and not any(k in name for k in only)):
                continue
            d = os.path.join(root_dir, f"Look_{name}")
            if os.path.isdir(d):
                shutil.rmtree(d)
            shutil.copytree(look, d)
            IMG_CACHE.clear()
            try:
                edit(d)
            except Exception as e:  # noqa: BLE001
                traceback.print_exc()
                say(False, f"hole {h} texture control {name}: raised {type(e).__name__}: {e}")
                continue
            IMG_CACHE.clear()
            f2, r2, _out = quiet_check(h, dict(opts, blend=blend, fbx=fbx, no_fbx=not fbx_mode, look_dir=d, json=None))
            passed_b = {r["name"].split("@")[0] for r in r2 if r["ok"] and not r["name"].endswith("@fbx")}
            passed_f = {r["name"].split("@")[0] for r in r2 if r["ok"] and r["name"].endswith("@fbx")}
            okp = all(g in passed_b for g in must_pass) and (not fbx_mode or all(g in passed_f or g in BLEND_ONLY for g in must_pass))
            fb, ff = stage_fails(r2, None)
            say(okp, f"hole {h} control {name}: must still PASS {must_pass}, failed now {sorted(fb | ff)}")
            rec.append(dict(hole=h, name=name, kind="good", expect=must_pass, ok=okp))
            shutil.rmtree(d, ignore_errors=True)
            IMG_CACHE.clear()
        # file-level negatives
        mf = os.path.join(root_dir, f"hole_{h:02d}_meta.fbx")
        shutil.copyfile(fbx, mf)
        with open(fbx + ".meta") as fsrc:
            txt = fsrc.read().replace("isReadable: 1", "isReadable: 0")
        with open(mf + ".meta", "w") as fdst:
            fdst.write(txt)
        f2, _r, _o = quiet_check(h, dict(opts, fbx=mf, no_blend=True, look_dir=look, json=None))
        say("FBX_FILE@fbx" in f2, f"hole {h} mutation meta_isReadable_0: must FAIL ['FBX_FILE'], failed {'FBX_FILE' if 'FBX_FILE@fbx' in f2 else '-'}")
        cf = os.path.join(root_dir, f"hole_{h:02d}_corrupt.fbx")
        with open(cf, "wb") as fh:
            fh.write(b"Kaydara FBX Binary  \x00\x1a\x00" + os.urandom(4000))
        shutil.copyfile(fbx + ".meta", cf + ".meta")
        f2, _r, _o = quiet_check(h, dict(opts, fbx=cf, no_blend=True, look_dir=look, json=None))
        say("FBX_REIMPORT@fbx" in f2, f"hole {h} mutation corrupt_fbx: must FAIL ['FBX_REIMPORT'], failed {'FBX_REIMPORT' if 'FBX_REIMPORT@fbx' in f2 else '-'}")
        h7 = os.path.join(root_dir, "hole07_copy")
        os.makedirs(h7, exist_ok=True)
        for _k, (src, _sha) in HOLE07.items():
            shutil.copyfile(src, os.path.join(h7, os.path.basename(src)))
        with open(os.path.join(h7, "hole_07.fbx.meta"), "a") as fh:
            fh.write("\n")
        f2, _r, _o = quiet_check(h, dict(opts, blend=blend, no_fbx=True, look_dir=look, hole07_dir=h7, json=None))
        say("HOLE07_UNTOUCHED" in f2, f"hole {h} mutation hole07_meta_copy_changed: must FAIL ['HOLE07_UNTOUCHED'], failed {'HOLE07_UNTOUCHED' if 'HOLE07_UNTOUCHED' in f2 else '-'}")
        rec.append(dict(hole=h, name="hole07_meta_copy_changed", kind="bad", expect=["HOLE07_UNTOUCHED"], ok="HOLE07_UNTOUCHED" in f2))
        nb = os.path.join(root_dir, f"hole_{h:02d}_nosig")
        os.makedirs(nb, exist_ok=True)
        f2, _r, _o = quiet_check(h, dict(opts, blend=blend, no_fbx=True, look_dir=look, sig_dir=nb, json=None))
        say("BASELINE_SIGNATURE" in f2, f"hole {h} mutation signature_missing: must FAIL ['BASELINE_SIGNATURE'], failed {'BASELINE_SIGNATURE' if 'BASELINE_SIGNATURE' in f2 else '-'}")
    # SHADERS_STATIC unit test: a temp shader with _Time and a temp C# with SetTextureOffset must FAIL, the real files PASS
    tmp = os.path.join(root_dir, "shader_unit")
    os.makedirs(tmp, exist_ok=True)
    with open(os.path.join(tmp, "GolfSurf.shader"), "w") as fh:
        fh.write("// static\nfloat t = _Time.y * 0.1;\n")
    with open(os.path.join(tmp, "GolfLook.cs"), "w") as fh:
        fh.write("void Update() { mat.SetTextureOffset(\"_BaseMap\", Vector2.zero); }\n")
    okb, _d = g_shaders_static(files=[os.path.join(tmp, "GolfSurf.shader"), os.path.join(tmp, "GolfLook.cs")])
    okg, _d2 = g_shaders_static()
    say(not okb, "mutation shader_with__Time_and_texture_scroll: must FAIL ['SHADERS_STATIC']")
    say(okg, "control real GolfSurf.shader + GolfLook.cs: must PASS ['SHADERS_STATIC']")
    rec.append(dict(hole=0, name="shader_with__Time_and_texture_scroll", kind="bad", expect=["SHADERS_STATIC"], ok=not okb))
    # v2 2026-10-05 (area U): the clock is allowed in the plant shader and its driver ONLY
    with open(os.path.join(tmp, "GolfLava.shader"), "w") as fh:
        fh.write("// lava\nfloat3 w = _GolfWind.xyz;\n")
    okl, _dl = g_shaders_static(files=[os.path.join(tmp, "GolfLava.shader")])
    say(not okl, "mutation lava_shader_reads__GolfWind: must FAIL ['SHADERS_STATIC']")
    rec.append(dict(hole=0, name="lava_shader_reads__GolfWind", kind="bad", expect=["SHADERS_STATIC"], ok=not okl))
    with open(os.path.join(tmp, "GolfLava.shader"), "w") as fh:
        fh.write("// lava\nfloat t = _Time.y;\n")
    okt, _dt = g_shaders_static(files=[os.path.join(tmp, "GolfLava.shader")])
    say(not okt, "mutation lava_shader_reads__Time: must FAIL ['SHADERS_STATIC']")
    rec.append(dict(hole=0, name="lava_shader_reads__Time", kind="bad", expect=["SHADERS_STATIC"], ok=not okt))
    with open(os.path.join(tmp, "AtmoSky.cs"), "w") as fh:
        fh.write("void LateUpdate() { sky.rotation = Time.time; }\n")
    oks, _ds = g_shaders_static(files=[os.path.join(tmp, "AtmoSky.cs")])
    say(not oks, "mutation sky_script_reads_Time_time: must FAIL ['SHADERS_STATIC']")
    rec.append(dict(hole=0, name="sky_script_reads_Time_time", kind="bad", expect=["SHADERS_STATIC"], ok=not oks))
    with open(os.path.join(tmp, "GolfPlants.shader"), "w") as fh:
        fh.write("// plants\nfloat t = _GolfWind.w; float u = _Time.y;\n")
    with open(os.path.join(tmp, "GolfWindSway.cs"), "w") as fh:
        fh.write("void LateUpdate() { Shader.SetGlobalVector(id, v * Time.time); }\n")
    with open(os.path.join(tmp, "GolfSurf.shader.ok"), "w") as fh:
        fh.write("// static\n")
    okp2, _dp2 = g_shaders_static(files=[os.path.join(tmp, "GolfPlants.shader"), os.path.join(tmp, "GolfWindSway.cs"), os.path.join(tmp, "GolfSurf.shader.ok")])
    say(okp2, "control GolfPlants.shader + GolfWindSway.cs read the clock next to a static shader: must PASS ['SHADERS_STATIC']")
    rec.append(dict(hole=0, name="plants_shader_and_driver_read_the_clock", kind="good", expect=["SHADERS_STATIC"], ok=okp2))
    # LOOK_STILLS_TEXTURED unit test (Game-view stills of a hole): flat colour, flat colour + noise (sd 2.4 levels) must FAIL; a textured frame and 'no stills yet' must PASS; --require-stills must FAIL when absent
    sd_ = os.path.join(root_dir, "stills_unit")
    for sub in ("flat", "noise", "textured"):
        os.makedirs(os.path.join(sd_, sub), exist_ok=True)
    rng_ = np.random.default_rng(5)
    Hh, Ww = 1600, 900
    base_ = np.ones((Hh, Ww, 4))
    base_[..., :3] = np.array((120, 150, 60)) / 255.0
    nz_ = st_noise(1600, rng_, beta=1.0, lo=2, hi=400)[:, :Ww]
    for sub, img in (("flat", base_.copy()), ("noise", base_.copy()), ("textured", base_.copy())):
        if sub == "noise":
            img[..., :3] += (2.4 / 255.0) * rng_.normal(0, 1, (Hh, Ww))[..., None]
        if sub == "textured":
            img[..., :3] += (0.03 * nz_)[..., None]
        for kind_ in ("tee", "approach"):
            st_png(os.path.join(sd_, sub, f"hole08_{kind_}.png"), img)
    IMG_CACHE.clear()
    Cs = st_ctx(8, dict(opts, look_dir=look))
    res_ = {}
    for sub in ("flat", "noise", "textured"):
        Cs.opts = dict(opts, stills_dir=os.path.join(sd_, sub))
        res_[sub] = g_look_stills(Cs)[0]
    Cs.opts = dict(opts, stills_dir=os.path.join(sd_, "absent"))
    res_["absent"] = g_look_stills(Cs)[0]
    Cs.opts = dict(opts, stills_dir=os.path.join(sd_, "absent"), require_stills=True)
    res_["absent_required"] = g_look_stills(Cs)[0]
    say(not res_["flat"], "mutation game_view_still_flat_colour: must FAIL ['LOOK_STILLS_TEXTURED']")
    say(not res_["noise"], "mutation game_view_still_flat_colour_plus_noise_2p4_levels: must FAIL ['LOOK_STILLS_TEXTURED']")
    say(not res_["absent_required"], "mutation game_view_stills_missing_with_require_stills: must FAIL ['LOOK_STILLS_TEXTURED']")
    say(res_["textured"], "control game_view_still_textured: must PASS ['LOOK_STILLS_TEXTURED']")
    say(res_["absent"], "control game_view_stills_not_yet_there (skipped): must PASS ['LOOK_STILLS_TEXTURED']")
    for nm_, okk, kd in (("game_view_still_flat_colour", not res_["flat"], "bad"), ("game_view_still_flat_colour_plus_noise_2p4_levels", not res_["noise"], "bad"),
                         ("game_view_stills_missing_with_require_stills", not res_["absent_required"], "bad"),
                         ("game_view_still_textured", res_["textured"], "good"), ("game_view_stills_not_yet_there", res_["absent"], "good")):
        rec.append(dict(hole=0, name=nm_, kind=kd, expect=["LOOK_STILLS_TEXTURED"], ok=okk))
    # SMOKE_VISIBLE_STILLS unit test (Game-view stats of the Crater stills): a readable plume in all three passes; one frame under 1.5 % fails; stats of an older stills tool (no 'smoke' block) fail; absent = SKIPPED pass, --require-stills = FAIL
    ss_ = os.path.join(root_dir, "smoke_stats_unit")

    def _smoke_stats(sub, shares, with_block=True):
        os.makedirs(os.path.join(ss_, sub), exist_ok=True)
        shots_ = []
        for nm_, sh_ in zip(("hole10_1_tee", "hole10_2_approach", "hole10_3_lavarim"), shares):
            e_ = dict(name=nm_, hole=10)
            if with_block:
                e_["smoke"] = dict(cards=3, changed_px=int(sh_ * 2.88e6), share_changed=sh_ * 2, readable_px=int(sh_ * 1.44e6), share_readable=sh_, delta_lum_median=14.0, delta_lum_p95=40.0, delta_lum_p99=60.0)
            shots_.append(e_)
        with open(os.path.join(ss_, sub, "stills_stats.json"), "w") as fh_:
            json.dump(dict(shots=shots_), fh_)
    _smoke_stats("good", (0.021, 0.018, 0.034))
    _smoke_stats("faint", (0.021, 0.0004, 0.034))
    _smoke_stats("edge", (0.0151, 0.0151, 0.0151))
    _smoke_stats("below_edge", (0.0149, 0.021, 0.021))
    _smoke_stats("old", (0.02, 0.02, 0.02), with_block=False)
    C10 = st_ctx(10, dict(opts, look_dir=look))
    res_ss = {}
    for sub in ("good", "faint", "edge", "below_edge", "old", "absent"):
        C10.opts = dict(opts, stills_dir=os.path.join(ss_, sub), look_dir=look)
        res_ss[sub] = g_smoke_stills(C10)[0]
    C10.opts = dict(opts, stills_dir=os.path.join(ss_, "absent"), require_stills=True, look_dir=look)
    res_ss["absent_required"] = g_smoke_stills(C10)[0]
    for nm_, okk, kd in (("smoke_stills_one_frame_0p04pct", not res_ss["faint"], "bad"), ("smoke_stills_one_frame_1p49pct", not res_ss["below_edge"], "bad"),
                         ("smoke_stats_without_smoke_block", not res_ss["old"], "bad"), ("smoke_stats_missing_with_require_stills", not res_ss["absent_required"], "bad"),
                         ("smoke_stills_all_frames_readable", res_ss["good"], "good"), ("smoke_stills_all_frames_1p51pct", res_ss["edge"], "good"), ("smoke_stats_not_yet_there", res_ss["absent"], "good")):
        say(okk, f"{'mutation' if kd == 'bad' else 'control'} {nm_}: must {'FAIL' if kd == 'bad' else 'PASS'} ['SMOKE_VISIBLE_STILLS']")
        rec.append(dict(hole=0, name=nm_, kind=kd, expect=["SMOKE_VISIBLE_STILLS"], ok=okk))
    ok_sf, d_sf = g_still_floor_selfcheck()
    say(ok_sf, "control the target stills pass the verifier's own floors (STILL_FLOOR_SELFCHECK): must PASS")
    rec.append(dict(hole=0, name="target_stills_pass_the_floors", kind="good", expect=["STILL_FLOOR_SELFCHECK"], ok=ok_sf))
    # the self-check must CATCH a floor the stills themselves fail: the old LEAD number (sd 0.03 in 90 % of the windows) and a Game-view flat threshold above the stills' own windows
    keep_w, keep_f = TH2["win_sd"], STILL_TH["flat_sd"]
    try:
        TH2["win_sd"] = 0.03
        ok_hi, _d = g_still_floor_selfcheck()
        TH2["win_sd"] = keep_w
        STILL_TH["flat_sd"] = 0.03
        ok_hf, _d2 = g_still_floor_selfcheck()
    finally:
        TH2["win_sd"], STILL_TH["flat_sd"] = keep_w, keep_f
    say(not ok_hi, "mutation grass_floor_0p03_in_90pct_of_windows (the old floor the stills fail): must FAIL ['STILL_FLOOR_SELFCHECK']")
    say(not ok_hf, "mutation game_view_flat_window_threshold_0p03 (above the stills' own windows): must FAIL ['STILL_FLOOR_SELFCHECK']")
    rec.append(dict(hole=0, name="grass_floor_0p03_in_90pct_of_windows", kind="bad", expect=["STILL_FLOOR_SELFCHECK"], ok=not ok_hi))
    rec.append(dict(hole=0, name="game_view_flat_window_threshold_0p03", kind="bad", expect=["STILL_FLOOR_SELFCHECK"], ok=not ok_hf))
    # per-gate summary of the v2 gates
    print("SELFTEST_V2 per-gate summary (MUT_FAILS_ON_BAD = mutants that must fail it and do; MUT_PASSES_ON_GOOD = synthetic positives + controls that pass it)", flush=True)
    for g in V2_GATES:
        bads = [r for r in rec if g in r["expect"] and r["kind"] == "bad"]
        okb = sum(1 for r in bads if r["ok"])
        ctl = [r for r in rec if g in (r["expect"] or []) and r["kind"] == "good"]
        gp = good.get(g, [0, 0])
        gok = gp[0] + sum(1 for r in ctl if r["ok"])
        gn = gp[1] + len(ctl)
        ln = (f"SELFTEST_GATE {g} MUT_FAILS_ON_BAD {okb}/{len(bads)} {'PASS' if bads and okb == len(bads) else 'FAIL'}; "
              f"MUT_PASSES_ON_GOOD {gok}/{gn} {'PASS' if gn and gok == gn else 'FAIL'}")
        lines.append(ln)
        print(ln, flush=True)
    print(f"SELFTEST: {len(lines)} lines, {bad} expectations not met; files under {root_dir}", flush=True)
    print("RESULT:", "SELFTEST ALL PASS" if not bad else f"SELFTEST FAILURES {bad}", flush=True)
    with open(os.path.join(root_dir, "selftest_summary.txt"), "w") as f:
        f.write("\n".join(lines) + "\n")
    if not opts.get("keep") and not opts.get("selftest_dir"):
        shutil.rmtree(root_dir, ignore_errors=True)
    return 0 if not bad else 1


if __name__ == "__main__":
    code = 1
    try:
        code = main()
    except SystemExit as e:
        code = e.code if isinstance(e.code, int) else 1
        if not isinstance(e.code, int):
            print(e)
    except Exception:  # noqa: BLE001
        traceback.print_exc()
        print("RESULT: FAILURES ['CHECKER_CRASHED']")
        code = 1
    sys.stdout.flush()
    sys.exit(code)
