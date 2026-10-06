"""postcard_verify: the mesh-level gate checker for a built postcard hole (8 Needle, 9 Split, 10 Crater).

It measures what the artist made (the opened .blend) and what Unity will actually get (the exported FBX, re-imported
into a CLEAN scene), against the PURE design module and the pure-Python scoring mirror postcard_check.Hole (which
mirrors Hole.cs LieAt). Every gate is a measurement; nothing is assumed from how the library builds things.

    /Applications/Blender.app/Contents/MacOS/Blender -b blender/hole_08.blend \
        --python blender/scripts/postcard_verify.py -- hole08_design [--fbx PATH] [--blend-only] [--fbx-only] [--json out.json]

    design            hole08_design (a module in blender/scripts) or a path to a design .py
    --fbx PATH        the FBX to check (default Unity/Assets/Resources/Course/hole_NN.fbx, NN = design NUMBER)
    --blend-only      check only the opened .blend, skip the FBX stage
    --fbx-only        skip the opened .blend (also implied when no .blend is open), check only the FBX
    --json FILE       also write every gate (name, pass, detail) and the totals as JSON
    test hooks (only the self tests use them): --hole07-blend PATH --hole07-fbx PATH point HOLE07_UNTOUCHED and the
    hole 7 reference palette / transform conventions at copies instead of the real hole 7 files.

Output: one line per gate, `GATE: <NAME>@<stage> PASS|FAIL - <detail>` (stage = blend or fbx; gates that do not depend
on a stage have no suffix), then `RESULT: ALL PASS` or `RESULT: FAILURES [...]`. Exit code 1 on any failure (also when a
gate itself raises: the checker fails closed), 2 on bad usage. Runtime is a few seconds per hole (about 1-3 s for the checks, plus Blender start-up).

Gates (per stage unless noted)
  MARKERS             MARKER_TEE/PIN/UP exist once; tee XY (0,0) within 0.02 m; pin XY = design pin yards * 0.9144 within
                      0.05 m; UP exactly 50 m above the tee marker (1 mm); tee->pin horizontal distance equals the design
                      within 0.1 yd. Measured on matrix_world, so the FBX stage checks the real import (axis conversion).
  CONTRACT_OBJECTS    one HOLE_NN_ROOT parenting everything; HOLE_CUP FLAG_POLE FLAG TEE_MARKER_1/2 BALL_START and the water
                      plane exist, cup and pole sit on the pin marker and the ball on the tee marker (0.05 m in plan);
                      (fbx) no camera or light in the file.
  GROUND_MESHES       TERRAIN_* FAIRWAY GREEN TEE_BOX BUNKER_nn exist; bunker sand meshes = design bunker count and each lip
                      has its sand; every object whose name starts with a ground prefix (TERRAIN FAIRWAY GREEN TEE_BOX
                      BUNKER CART_PATH) is on the allowed list; no ground-named object carries scenery mesh data (rock,
                      arch, flag, water, lava, foam ...).
  ONE_HEIGHT          TERRAIN faces with nz > 0.5 all at play_z +- 1 mm; every other ground vertex in [play_z - 1 mm,
                      play_z + 0.60 m]; no up-facing play-surface face tilted more than 1 degree except the bunker lips;
                      reports the z relief (max - min).
  NO_UPFACE_OUTSIDE_LAND   single-sided (front-face only) ray casts from z = 1000 over the BVH of every ground-prefix mesh at
                      >= 4000 points that the scoring mirror calls Water (inside water ellipses, outside the shore, a 1.0 yd
                      band around the rims ignored) never hit anything; plus no front-facing ground triangle whose
                      centroid is in the water beyond that band (so GroundHeight is 0 there, as in Unity).
  LIES_MATCH_MESH     >= 6000 jittered points (all 25 neighbours within 1.5 yd share the lie, i.e. a 1.5 yd band round every
                      region boundary is ignored): water -> no hit; green -> GREEN; bunker -> BUNKER_nn sand; fairway ->
                      FAIRWAY; tee -> TEE_BOX; rough / out-of-bounds -> TERRAIN. Zero mismatches. Legal overlays (measured,
                      not assumed): the bunker lip inside 1.30 x the ellipse, the TEE_BOX pad over fairway within its own
                      measured extent, FAIRWAY_FIRSTCUT over rough within the first-cut band, GREEN_APRON over rough /
                      out-of-bounds within green + apron. (The brief says "rough -> TERRAIN only"; hole 7 has the first cut
                      and the apron and the library builds them, so they are accepted in those bands only.)
  SHORE_MATCH         the TERRAIN top-surface boundary equals the design shore minus the water-ellipse cuts within 0.5 yd,
                      two-sided Hausdorff on samples every 0.1 m, and every water ellipse cut within 0.5 yd of its ellipse.
  HAZARD_PLACEMENT    each bunker sand mesh matched to a design bunker: area centroid within 0.5 yd of the ellipse centre,
                      extent inside the ellipse + 0.5 yd, plan area 0.9-1.1 x the ellipse, its lip inside 1.30 x + 0.5 yd.
  PADS                connected components of the TERRAIN top surface (shared vertices) = the dry components
                      postcard_check finds on the 1 yd lie grid (same Grid.components), and LIMITS['pads'] when given.
  MATERIALS           every material is a contract name (MAT_LAVA only on hole 10's WATER_LAVA, where it must exist and be
                      orange); colours of the shared MAT_* names within 3/255 (sRGB) of hole 7 (blend stage: hole_07.blend
                      read through libraries.load; fbx stage: DiffuseColor parsed from both FBX files).
  NO_ANIMATION        no animation data, action, shape key, armature or driver anywhere.
  WATER_LEVEL         the water plane (WATER_OCEAN, or WATER_LAVA for the lava hole) lies at z = 0 and covers the shore;
                      every WATER* mesh is within z 0..0.2 and faces up.
  FBX_FILE (fbx)      the FBX and its .meta exist; fresh 32-hex guid, isReadable 1, every other import setting equal to
                      hole_07.fbx.meta.
  FBX_GLOBALS (fbx)   GlobalSettings (UpAxis, FrontAxis, CoordAxis + signs, UnitScaleFactor, OriginalUnitScaleFactor) and
                      the FBX version equal hole_07.fbx; the root and the objects that must be unrotated carry the same
                      transform conventions as their hole 7 counterparts. Method: both binary files are read with Blender's
                      own FBX reader (io_scene_fbx.parse_fbx), Model Properties70 compared (translation of markers and
                      meshes is object specific and skipped); the imported world coordinates are checked by MARKERS.
  FBX_BUDGET (fbx)    file <= 4 MB and imported triangles <= 150000 (reported).
  LIE_MIRROR          (once) the numpy scoring mirror used here equals postcard_check.Hole.lie_at at 3000 random and
                      boundary-hugging points: the sampling gates therefore measure the real scoring.
  HOLE07_UNTOUCHED    (once) sha256 of hole_07.blend and hole_07.fbx are the README hashes; those two files and hole_07.fbx.meta are unchanged by this run.
"""
import sys

sys.dont_write_bytecode = True
import os
import re
import math
import json
import time
import hashlib
import importlib
import importlib.util
import traceback

import numpy as np
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

# ----------------------------------------------------------------------------- paths and constants
try:
    HERE = os.path.dirname(os.path.abspath(__file__))
except NameError:  # exec() without __file__
    HERE = os.getcwd()
REPO = os.path.dirname(os.path.dirname(HERE))
BLENDER_DIR = os.path.join(REPO, "blender")
COURSE_DIR = os.path.join(REPO, "Unity", "Assets", "Resources", "Course")
HOLE07_BLEND = os.path.join(BLENDER_DIR, "hole_07.blend")
HOLE07_FBX = os.path.join(COURSE_DIR, "hole_07.fbx")
if HERE not in sys.path:
    sys.path.insert(0, HERE)

YD = 0.9144
H07_BLEND_SHA = "a7842f25ca64a1a91be9d881611ff84de63eb1f2e1ad8111330b9834edc4c4af"
H07_FBX_SHA = "813850bff6d107c0424dd9ca22eeaedf1ba9d3fc7ef6e9f0eed54bc67856c6ed"
GROUND_PREFIXES = ("TERRAIN", "FAIRWAY", "GREEN", "TEE_BOX", "BUNKER", "CART_PATH")
ALLOWED_GROUND = [re.compile(p) for p in (r"^TERRAIN_[A-Za-z0-9_]+$", r"^FAIRWAY$", r"^FAIRWAY_FIRSTCUT$", r"^GREEN$",
                                          r"^GREEN_APRON$", r"^TEE_BOX$", r"^BUNKER_\d\d$", r"^BUNKER_\d\d_LIP$")]
SCENERY_DATA = re.compile(r"^(ROCK|CLIFF_ROCK|ARCH|PILLAR|RUIN|FLAG|HOLE_CUP|WATER|LAVA|FOAM|TEE_MARKER|BALL|MARKER|HOLE_\d+_ROOT)")
CONTRACT_MATS = {"MAT_ROUGH", "MAT_FAIRWAY", "MAT_FAIRWAY_STRIPE", "MAT_FIRSTCUT", "MAT_GREEN", "MAT_BUNKER_LIP", "MAT_SAND",
                 "MAT_WATER", "MAT_WATER_SHALLOW", "MAT_FOAM", "MAT_CLIFF", "MAT_CLIFF_DARK", "MAT_ROCK", "MAT_ROCK_DARK",
                 "MAT_FLAG", "MAT_POLE", "MAT_CUP", "MAT_BALL"}
COLOUR_TOL_SRGB = 3.0 / 255.0
LIE_NAMES = ["Water", "Bunker", "Green", "Tee", "Fairway", "Rough", "OutOfBounds"]
LW, LB, LG, LT, LF, LR, LO = range(7)
FBX_GLOBAL_KEYS = (b"UpAxis", b"UpAxisSign", b"FrontAxis", b"FrontAxisSign", b"CoordAxis", b"CoordAxisSign",
                   b"UnitScaleFactor", b"OriginalUnitScaleFactor")
Z_BAND_TOP = 0.60

RES = []


# ----------------------------------------------------------------------------- reporting
def gate(name, ok, detail, stage=None):
    full = f"{name}@{stage}" if stage else name
    detail = " ".join(str(detail).split())
    RES.append(dict(name=full, ok=bool(ok), detail=detail))
    print(f"GATE: {full} {'PASS' if ok else 'FAIL'} - {detail}", flush=True)


def run_gate(stage, name, fn, *args):
    """fn returns (ok, detail). A raising gate FAILS (the checker fails closed)."""
    t0 = time.time()
    try:
        ok, detail = fn(*args)
    except Exception as e:  # noqa: BLE001
        tb = traceback.extract_tb(e.__traceback__)[-1]
        ok, detail = False, f"EXCEPTION {type(e).__name__}: {e} (at {os.path.basename(tb.filename)}:{tb.lineno})"
        traceback.print_exc()
    TIMES[f"{name}@{stage}" if stage else name] = round(time.time() - t0, 2)
    gate(name, ok, detail, stage)


TIMES = {}


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


# ----------------------------------------------------------------------------- design + scoring mirror
def load_design(name):
    if name.endswith(".py") or os.sep in name:
        path = name if os.path.isabs(name) else (os.path.abspath(name) if os.path.exists(name) else os.path.join(HERE, name))
        spec = importlib.util.spec_from_file_location(os.path.basename(path)[:-3], path)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        return mod
    return importlib.reload(importlib.import_module(name))


class Mirror:
    """Vectorised numpy mirror of Hole.LieAt in COURSE YARDS, written here (not borrowed from postcard_lib) so the
    checker is independent of the library under test; LIE_MIRROR proves it equals postcard_check.Hole.lie_at."""

    def __init__(self, h):
        self.h = h
        self.shore = np.array(h.shore, float)
        self.haz = h.hazards
        self.tee = h.tee
        self.pin = h.pin

    def off(self, X, Y):
        off = np.full(X.shape, np.inf)
        cl = self.h.center
        for i in range(1, len(cl)):
            ax, ay = cl[i - 1]
            bx, by = cl[i]
            dx, dd = bx - ax, by - ay
            l2 = max(dx * dx + dd * dd, 0.0001)
            t = np.clip(((X - ax) * dx + (Y - ay) * dd) / l2, 0.0, 1.0)
            off = np.minimum(off, np.hypot(X - (ax + dx * t), Y - (ay + dd * t)))
        return off

    def inside(self, X, Y):
        poly = self.shore
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

    def codes(self, X, Y):
        X = np.asarray(X, float)
        Y = np.asarray(Y, float)
        shape = X.shape
        X = X.ravel()
        Y = Y.ravel()
        h = self.h
        off = self.off(X, Y)
        c = np.full(X.shape, LO, np.int8)
        c[off <= h.fw / 2 + h.rough] = LR
        c[off <= h.fw / 2] = LF
        c[np.hypot(X - self.tee[0], Y - self.tee[1]) <= 4.0] = LT
        c[np.hypot(X - self.pin[0], Y - self.pin[1]) <= h.gr] = LG
        c[~self.inside(X, Y)] = LW
        for kind, hx, hd, w, l, _t in reversed(self.haz):          # earlier hazards win, as in LieAt
            ex = (X - hx) / max(w / 2, 0.001)
            ez = (Y - hd) / max(l / 2, 0.001)
            c[ex * ex + ez * ez <= 1] = LW if kind == "water" else LB
        return c.reshape(shape)

    def codes_chunked(self, X, Y, chunk=40000):
        X = np.asarray(X, float).ravel()
        Y = np.asarray(Y, float).ravel()
        return np.concatenate([self.codes(X[s:s + chunk], Y[s:s + chunk]) for s in range(0, len(X), chunk)]) if len(X) else \
            np.zeros(0, np.int8)


def ellipse_val(X, Y, hz):
    return ((X - hz[1]) / max(hz[3] / 2, 0.001)) ** 2 + ((Y - hz[2]) / max(hz[4] / 2, 0.001)) ** 2


def dist_pts_segs(P, A, B, chunk=300):
    """Exact distance from points P (N,2) to the nearest of the segments A->B (M,2)."""
    P = np.asarray(P, float)
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


def densify(A, B, step):
    """Points every <= step along the segments A->B (start points included)."""
    L = np.hypot(*(B - A).T)
    n = np.maximum(1, np.ceil(L / step)).astype(np.int64)
    idx = np.repeat(np.arange(len(A)), n)
    k = np.arange(int(n.sum())) - np.repeat(np.cumsum(n) - n, n)
    t = (k / n[idx])[:, None]
    return A[idx] + (B[idx] - A[idx]) * t


def ellipse_polyline(hz, n=360):
    t = np.linspace(0, 2 * np.pi, n, endpoint=False)
    return np.stack([hz[1] + hz[3] / 2 * np.cos(t), hz[2] + hz[4] / 2 * np.sin(t)], 1)


class Ctx:
    """Everything the gates need about the design (yards) and the contract."""

    def __init__(self, mod, opts):
        pc = importlib.import_module("postcard_check")
        self.pc = pc
        self.mod = mod
        self.h = pc.Hole(mod)
        self.mirror = Mirror(self.h)
        self.number = int(mod.NUMBER)
        self.name = str(mod.NAME)
        self.tag = f"{self.number:02d}"
        self.pz = float(mod.PLAY_Z)
        self.root_name = f"HOLE_{self.tag}_ROOT"
        self.scenery = dict(getattr(mod, "SCENERY", {}) or {})
        self.limits = dict(getattr(mod, "LIMITS", {}) or {})
        self.lava = bool(self.scenery.get("lava")) or self.number == 10
        self.water_name = str(self.scenery.get("lava_mesh", "WATER_LAVA")) if self.lava else "WATER_OCEAN"
        self.bunkers = [k for k in self.h.hazards if k[0] == "bunker"]
        self.waters = [k for k in self.h.hazards if k[0] == "water"]
        self.rng = np.random.default_rng(self.number * 7919 + 13)
        self.opts = opts
        xs = [p[0] for p in self.h.shore]
        ds = [p[1] for p in self.h.shore]
        self.bbox = (min(xs), min(ds), max(xs), max(ds))                      # yards
        rim = [np.array(self.h.shore, float)] + [ellipse_polyline(k, 360) for k in self.waters]
        self.rim_A = np.vstack(rim)
        self.rim_B = np.vstack([np.roll(r, -1, axis=0) for r in rim])
        self.rim_polys = rim
        self.ref_pal = {}
        self.ref_pal_src = ""


# ----------------------------------------------------------------------------- scene snapshot (blend or imported FBX)
def family_of(name):
    if name.startswith("TERRAIN"):
        return "TERRAIN"
    if name == "FAIRWAY":
        return "FAIRWAY"
    if name == "FAIRWAY_FIRSTCUT":
        return "FIRSTCUT"
    if name == "GREEN":
        return "GREEN"
    if name == "GREEN_APRON":
        return "APRON"
    if name == "TEE_BOX":
        return "TEE_BOX"
    if re.fullmatch(r"BUNKER_\d\d", name):
        return "SAND"
    if re.fullmatch(r"BUNKER_\d\d_LIP", name):
        return "LIP"
    return "?"


def tri_data(W, T):
    a, b, c = W[T[:, 0]], W[T[:, 1]], W[T[:, 2]]
    n = np.cross(b - a, c - a)
    ln = np.linalg.norm(n, axis=1)
    N = np.zeros_like(n)
    ok = ln > 1e-12
    N[ok] = n[ok] / ln[ok, None]
    return N, 0.5 * ln


class Snap:
    """The scene as Unity would see it: every MESH/EMPTY (and camera/light) object linked to the scene except the hidden
    ASSET_LIBRARY. Mesh data is world-space numpy arrays (evaluated mesh in the blend stage, so modifiers count)."""

    def __init__(self, stage):
        self.stage = stage
        bpy.context.view_layer.update()
        self.dg = bpy.context.evaluated_depsgraph_get()
        lib = bpy.data.collections.get("ASSET_LIBRARY")
        libset = set(lib.all_objects) if lib else set()
        self.all = {}
        self.lib_skipped = 0
        for ob in bpy.context.scene.objects:
            if ob in libset and all(c.name == "ASSET_LIBRARY" for c in ob.users_collection):
                self.lib_skipped += 1
                continue
            self.all[ob.name] = ob
        self.meshes = {n: o for n, o in self.all.items() if o.type == "MESH"}
        self.ground_named = {n: o for n, o in self.all.items() if n.startswith(GROUND_PREFIXES)}
        self.ground = {n: o for n, o in self.ground_named.items() if o.type == "MESH"}
        self._cache = {}

    def mesh(self, ob):
        """(W (n,3) world verts, T (m,3) tris, N (m,3) unit normals, A (m,) areas) of a mesh object."""
        c = self._cache.get(ob.name)
        if c is not None:
            return c
        me, holder = None, None
        if self.stage == "blend":
            try:
                holder = ob.evaluated_get(self.dg)
                me = holder.to_mesh()
            except Exception:  # noqa: BLE001
                me, holder = None, None
        if me is None:
            me = ob.data
        n = len(me.vertices)
        co = np.empty(n * 3)
        me.vertices.foreach_get("co", co)
        mw = np.array(ob.matrix_world)
        W = co.reshape(-1, 3) @ mw[:3, :3].T + mw[:3, 3]
        me.calc_loop_triangles()
        nt = len(me.loop_triangles)
        tv = np.empty(nt * 3, dtype=np.int64)
        me.loop_triangles.foreach_get("vertices", tv)
        T = tv.reshape(-1, 3)
        if np.linalg.det(mw[:3, :3]) < 0:                      # a mirrored object flips its winding
            T = T[:, [0, 2, 1]]
        if holder is not None:
            holder.to_mesh_clear()
        N, A = tri_data(W, T) if nt else (np.zeros((0, 3)), np.zeros(0))
        c = (W, T, N, A)
        self._cache[ob.name] = c
        return c

    def world(self, name):
        return Vector(self.all[name].matrix_world.translation)

    def tri_count(self, ob):
        me = ob.data
        me.calc_loop_triangles()
        return len(me.loop_triangles)


def m_xy(P):
    """(n,2) yards -> metres."""
    return np.asarray(P, float) * YD


# ----------------------------------------------------------------------------- gates: markers / contract / ground meshes
def g_markers(S, C):
    miss = [n for n in ("MARKER_TEE", "MARKER_PIN", "MARKER_UP") if n not in S.all]
    if miss:
        return False, f"missing {miss}"
    extra = sorted(n for n in S.all if n.startswith("MARKER_") and n not in ("MARKER_TEE", "MARKER_PIN", "MARKER_UP"))
    tee, pin, up = S.world("MARKER_TEE"), S.world("MARKER_PIN"), S.world("MARKER_UP")
    h = C.h
    e_tee = math.hypot(tee.x, tee.y)
    e_pin = math.hypot(pin.x - h.pin[0] * YD, pin.y - h.pin[1] * YD)
    e_up = (up - tee - Vector((0, 0, 50.0))).length
    dist = math.hypot(pin.x - tee.x, pin.y - tee.y) / YD
    want = math.dist(h.tee, h.pin)
    ok = e_tee <= 0.02 and e_pin <= 0.05 and e_up <= 1e-3 and abs(dist - want) <= 0.1 and not extra
    types = {n: S.all[n].type for n in ("MARKER_TEE", "MARKER_PIN", "MARKER_UP")}
    return ok, (f"tee XY error {e_tee * 1000:.2f} mm (<=20), pin XY error {e_pin * 1000:.2f} mm (<=50) vs design "
                f"({h.pin[0] * YD:.3f}, {h.pin[1] * YD:.3f}) m, UP-TEE off (0,0,50) by {e_up * 1000:.3f} mm (<=1), "
                f"tee->pin {dist:.3f} yd vs design {want:.3f} yd (<=0.1)"
                + (f", EXTRA marker objects {extra}" if extra else "") + f", types {sorted(set(types.values()))}")


def g_contract_objects(S, C):
    problems = []
    roots = [n for n, o in S.all.items() if re.fullmatch(r"HOLE_\d+_ROOT", n)]
    if roots != [C.root_name]:
        problems.append(f"root objects {roots}, want exactly [{C.root_name}]")
    root = S.all.get(C.root_name)
    if root is not None and root.type != "EMPTY":
        problems.append(f"{C.root_name} is a {root.type}, want EMPTY")
    need = ["HOLE_CUP", "FLAG_POLE", "FLAG", "TEE_MARKER_1", "TEE_MARKER_2", "BALL_START", C.water_name]
    problems += [f"missing {n}" for n in need if n not in S.all]
    if C.lava and "WATER_OCEAN" in S.all:
        problems.append("lava hole still has a WATER_OCEAN plane")
    for n, ref in (("HOLE_CUP", "MARKER_PIN"), ("FLAG_POLE", "MARKER_PIN"), ("BALL_START", "MARKER_TEE")):
        if n in S.all and ref in S.all:
            a, b = S.world(n), S.world(ref)
            if math.hypot(a.x - b.x, a.y - b.y) > 0.05:
                problems.append(f"{n} is {math.hypot(a.x - b.x, a.y - b.y):.2f} m from {ref} in plan (<= 0.05)")
    orphans = []
    for n, o in S.all.items():
        if n == C.root_name or o.type in ("CAMERA", "LIGHT") and S.stage == "blend":
            continue
        p = o.parent
        depth = 0
        while p is not None and p.name != C.root_name and depth < 50:
            p, depth = p.parent, depth + 1
        if p is None:
            orphans.append(n)
    if orphans:
        problems.append(f"{len(orphans)} objects not under {C.root_name}: {orphans[:6]}")
    if S.stage == "fbx":
        cam = [n for n, o in S.all.items() if o.type in ("CAMERA", "LIGHT")]
        if cam:
            problems.append(f"cameras/lights in the FBX: {cam}")
        other = sorted({o.type for o in S.all.values()} - {"MESH", "EMPTY"})
        if other:
            problems.append(f"object types besides MESH/EMPTY: {other}")
    kinds = {}
    for o in S.all.values():
        kinds[o.type] = kinds.get(o.type, 0) + 1
    return not problems, ("; ".join(problems) if problems else
                          f"root {C.root_name} parents all {len(S.all)} objects {kinds}; gameplay placeholders and {C.water_name} present"
                          + (f" ({S.lib_skipped} ASSET_LIBRARY objects not counted)" if S.lib_skipped else ""))


def g_ground_meshes(S, C):
    problems = []
    gn = S.ground_named
    pre = {p: sorted(n for n in S.ground if n.startswith(p)) for p in ("TERRAIN", "FAIRWAY", "GREEN", "TEE_BOX", "BUNKER")}
    for p, lst in pre.items():
        if not lst:
            problems.append(f"no mesh named {p}*")
    for n in ("FAIRWAY", "GREEN", "TEE_BOX"):
        if n not in S.ground:
            problems.append(f"no mesh named exactly {n}")
    not_allowed = sorted(n for n in gn if not any(r.match(n) for r in ALLOWED_GROUND))
    if not_allowed:
        problems.append(f"ground-prefixed names not on the allowed list: {not_allowed}")
    nonmesh = sorted(n for n, o in gn.items() if o.type != "MESH")
    if nonmesh:
        problems.append(f"ground-prefixed objects that are not meshes: {nonmesh}")
    sand = sorted(n for n in S.ground if re.fullmatch(r"BUNKER_\d\d", n))
    lips = sorted(n for n in S.ground if re.fullmatch(r"BUNKER_\d\d_LIP", n))
    if len(sand) != len(C.bunkers):
        problems.append(f"{len(sand)} bunker sand meshes {sand}, design has {len(C.bunkers)} bunkers")
    orphan_lips = [n for n in lips if n[:-4] not in sand]
    if orphan_lips:
        problems.append(f"lips without a sand mesh: {orphan_lips}")
    scen = []
    for n, o in S.ground.items():
        dn = o.data.name if o.data else ""
        if SCENERY_DATA.match(dn) or SCENERY_DATA.match(re.sub(r"\.\d+$", "", dn)):
            scen.append(f"{n} (mesh data '{dn}')")
    if scen:
        problems.append(f"scenery mesh data under a ground name: {scen}")
    dup = sorted(n for n in gn if re.search(r"\.\d{3}$", n))
    if dup:
        problems.append(f"duplicate-suffixed ground names {dup}")
    # no scenery object may START with a ground prefix (it would get a MeshCollider in Unity)
    for n in S.all:
        if re.match(r"^(ROCK_|CLIFF_ROCK|WATER|FLAG|HOLE_CUP|TEE_MARKER|BALL_START|MARKER_|LAVA|FOAM|ARCH|PILLAR|RUIN)", n) \
                and n.startswith(GROUND_PREFIXES):
            problems.append(f"scenery object {n} starts with a ground prefix")
    return not problems, ("; ".join(problems) if problems else
                          f"{len(gn)} ground meshes, all on the allowed list: terrain {pre['TERRAIN']}, FAIRWAY, GREEN, TEE_BOX, "
                          f"{len(sand)} sand + {len(lips)} lips for {len(C.bunkers)} design bunkers; no scenery under a ground name")


# ----------------------------------------------------------------------------- gate: one height
def g_one_height(S, C):
    pz = C.pz
    problems, notes = [], []
    terr = {n: o for n, o in S.ground.items() if n.startswith("TERRAIN")}
    if not terr:
        return False, "no TERRAIN mesh"
    top_n = 0
    top_z = []
    max_tilt = 0.0
    for n, o in terr.items():
        W, T, N, A = S.mesh(o)
        top = N[:, 2] > 0.5
        top_n += int(top.sum())
        if top.any():
            z = W[T[top]][:, :, 2]
            top_z.append((z.min(), z.max()))
            bad = np.abs(z - pz).max(axis=1) > 1e-3
            if bad.any():
                problems.append(f"{n}: {int(bad.sum())} of {int(top.sum())} top faces off play_z {pz} (z {z.min():.3f}..{z.max():.3f})")
            max_tilt = max(max_tilt, float(np.degrees(np.arccos(np.clip(N[top][:, 2], -1, 1))).max()))
        ramps = (N[:, 2] > 0.02) & (N[:, 2] <= 0.5)
        if ramps.any():
            problems.append(f"{n}: {int(ramps.sum())} terrain faces slope between 1 and 60 degrees from vertical walls (a ramp the ball could stand on)")
    if top_n == 0:
        problems.append("TERRAIN has no up-facing faces")
    zlo = [pz]
    zhi = [pz]
    per = []
    for n, o in S.ground.items():
        if n.startswith("TERRAIN"):
            continue
        W, T, N, A = S.mesh(o)
        if len(W) == 0:
            problems.append(f"{n}: empty mesh")
            continue
        lo, hi = float(W[:, 2].min()), float(W[:, 2].max())
        zlo.append(lo)
        zhi.append(hi)
        per.append(f"{n} {lo - pz:+.2f}..{hi - pz:+.2f}")
        if lo < pz - 1e-3 or hi > pz + Z_BAND_TOP + 1e-3:
            problems.append(f"{n}: z {lo:.3f}..{hi:.3f} outside play_z..play_z+{Z_BAND_TOP}")
        if re.fullmatch(r"BUNKER_\d\d_LIP", n):
            continue
        ok_tri = A > 1e-9
        tilt = np.degrees(np.arccos(np.clip(N[ok_tri][:, 2], -1, 1)))
        if len(tilt) and tilt.max() > 1.0:
            problems.append(f"{n}: max face tilt {tilt.max():.2f} deg (> 1) on {int((tilt > 1.0).sum())} faces")
        max_tilt = max(max_tilt, float(tilt.max()) if len(tilt) else 0.0)
    tz = (min(a for a, _ in top_z), max(b for _, b in top_z)) if top_z else (float("nan"), float("nan"))
    return not problems, ("; ".join(problems) if problems else
                          f"{top_n} terrain top faces all at z {tz[0]:.4f}..{tz[1]:.4f} (play_z {pz}); other ground in play_z"
                          f"{min(zlo) - pz:+.2f}..{max(zhi) - pz:+.2f} m; max tilt of flat play surfaces {max_tilt:.3f} deg; "
                          f"relief max-min of all play surfaces {max(zhi) - min(zlo):.3f} m ({'; '.join(per)})")


# ----------------------------------------------------------------------------- the ground BVH (front faces only)
class GroundRays:
    def __init__(self, S):
        names, V, T, own, off = [], [], [], [], 0
        for n, o in sorted(S.ground.items()):
            W, TT, N, A = S.mesh(o)
            if len(TT) == 0:
                continue
            V.append(W)
            T.append(TT + off)
            own.extend([len(names)] * len(TT))
            names.append(n)
            off += len(W)
        self.names = names
        self.fam = [family_of(n) for n in names]
        if not V:
            self.bvh = None
            return
        Vv = np.vstack(V)
        Tt = np.vstack(T)
        self.own = np.array(own)
        self.bvh = BVHTree.FromPolygons([tuple(v) for v in Vv.tolist()], [tuple(t) for t in Tt.tolist()], all_triangles=True)

    def cast(self, x, y):
        """Topmost FRONT-facing hit going down from z = 1000 (a single-sided collider ignores back faces): (z, object index) or None."""
        if self.bvh is None:
            return None
        d = Vector((0.0, 0.0, -1.0))
        o = Vector((x, y, 1000.0))
        for _ in range(16):
            loc, nor, idx, _dist = self.bvh.ray_cast(o, d)
            if loc is None:
                return None
            if nor.z > 1e-7:
                return loc.z, int(self.own[idx])
            o = Vector((x, y, loc.z - 1e-4))
        return None


def jittered(rng, x0, d0, x1, d1, n):
    s = math.sqrt(max((x1 - x0) * (d1 - d0), 1e-6) / max(n, 1))
    gx = np.arange(x0, x1, s)
    gd = np.arange(d0, d1, s)
    GX, GD = np.meshgrid(gx, gd, indexing="ij")
    return np.stack([GX.ravel() + rng.uniform(0, s, GX.size), GD.ravel() + rng.uniform(0, s, GX.size)], 1)


def in_ellipse_samples(rng, hz, n, scale=1.0, inner=0.0):
    """Uniform samples in the annulus inner*scale .. scale of the axis-aligned ellipse hz (yards)."""
    r = np.sqrt(rng.uniform(inner * inner, 1.0, n)) * scale
    a = rng.uniform(0, 2 * np.pi, n)
    return np.stack([hz[1] + hz[3] / 2 * r * np.cos(a), hz[2] + hz[4] / 2 * r * np.sin(a)], 1)


def rim_dist(C, P):
    return dist_pts_segs(P, C.rim_A, C.rim_B)


# ----------------------------------------------------------------------------- gate: nothing upward outside the land
def g_no_upface(S, C, rays):
    h = C.h
    rng = C.rng
    x0, d0, x1, d1 = C.bbox
    pts = [jittered(rng, x0 - 40, d0 - 40, x1 + 40, d1 + 40, 4500)]
    for k in C.waters:
        pts.append(in_ellipse_samples(rng, k, 900))
        pts.append(in_ellipse_samples(rng, k, 600, scale=1.5, inner=0.7))
    shore = np.array(h.shore)
    v = shore[rng.integers(0, len(shore), 3000)]
    ang = rng.uniform(0, 2 * np.pi, len(v))
    rad = rng.uniform(1.0, 14.0, len(v))
    pts.append(v + np.stack([rad * np.cos(ang), rad * np.sin(ang)], 1))
    P = np.vstack(pts)
    lie = C.mirror.codes_chunked(P[:, 0], P[:, 1])
    keep = (lie == LW) & (rim_dist(C, P) > 1.0)
    P = P[keep]
    problems = []
    if len(P) < 4000:
        problems.append(f"only {len(P)} water sample points (need >= 4000)")
    bad = []
    for x, d in P:
        r = rays.cast(float(x * YD), float(d * YD))
        if r is not None:
            bad.append((round(float(x), 1), round(float(d), 1), rays.names[r[1]], round(r[0], 2)))
    if bad:
        problems.append(f"{len(bad)} of {len(P)} water points hit a ground mesh, first {bad[:5]}")
    # deterministic backstop: any front-facing ground triangle whose centroid is water, beyond the 1 yd rim band
    stray = []
    for n, o in S.ground.items():
        W, T, N, A = S.mesh(o)
        up = (N[:, 2] > 1e-3) & (A > 1e-9)
        if not up.any():
            continue
        cen = W[T[up]].mean(axis=1)
        cyd = cen[:, :2] / YD
        wat = C.mirror.codes_chunked(cyd[:, 0], cyd[:, 1]) == LW
        if wat.any():
            far = rim_dist(C, cyd[wat]) > 1.0
            if far.any():
                c2 = cyd[wat][far]
                stray.append(f"{n}: {int(far.sum())} up-facing triangles over water, e.g. ({c2[0][0]:.1f}, {c2[0][1]:.1f}) yd")
    if stray:
        problems.append("; ".join(stray))
    return not problems, ("; ".join(problems) if problems else
                          f"{len(P)} sample points in the water (inside {len(C.waters)} water ellipses and outside the shore, 1.0 yd rim band "
                          f"ignored): single-sided ray casts from z 1000 hit nothing; no up-facing ground triangle over water")


# ----------------------------------------------------------------------------- gate: lie vs mesh
def ring_dirs(n=12):
    a = np.linspace(0, 2 * np.pi, n, endpoint=False)
    return np.cos(a), np.sin(a)


EXPECT_NAME = {LW: "no hit", LB: "BUNKER_nn sand", LG: "GREEN", LT: "TEE_BOX", LF: "FAIRWAY", LR: "TERRAIN", LO: "TERRAIN"}


def depth_of(mirror, x, d, c0, radii=(3.0, 6.0, 12.0)):
    cs, sn = ring_dirs(12)
    best = 1.5
    for r in radii:
        X = x + r * cs
        D = d + r * sn
        if (mirror.codes(X, D) == c0).all():
            best = r
        else:
            break
    return best


def g_lies(S, C, rays, tee_extent_yd):
    h = C.h
    mir = C.mirror
    rng = C.rng
    pz = C.pz
    x0, d0, x1, d1 = C.bbox
    cs, sn = ring_dirs(12)
    elig_P, elig_c = [], []
    n_raw = 0
    for rnd in range(5):
        pts = [jittered(rng, x0 - 8, d0 - 8, x1 + 8, d1 + 8, 7000)]
        a = rng.uniform(0, 2 * np.pi, 700)
        r = np.sqrt(rng.uniform(0, 1, 700)) * 5.5            # the tee lie has radius 4 yd, its interior 2.5 yd
        pts.append(np.stack([h.tee[0] + r * np.cos(a), h.tee[1] + r * np.sin(a)], 1))
        a = rng.uniform(0, 2 * np.pi, 900)
        r = np.sqrt(rng.uniform(0, 1, 900)) * (h.gr + 6.0)
        pts.append(np.stack([h.pin[0] + r * np.cos(a), h.pin[1] + r * np.sin(a)], 1))
        for k in C.bunkers:
            pts.append(in_ellipse_samples(rng, k, 700, scale=1.6))
        for k in C.waters:
            pts.append(in_ellipse_samples(rng, k, 500))
            pts.append(in_ellipse_samples(rng, k, 400, scale=1.4, inner=0.8))
        cl = np.array(h.center)
        seg_len = np.hypot(*(cl[1:] - cl[:-1]).T)
        si = rng.choice(len(seg_len), 3000, p=seg_len / seg_len.sum())
        t = rng.uniform(0, 1, 3000)
        base = cl[si] + (cl[si + 1] - cl[si]) * t[:, None]
        dv = (cl[si + 1] - cl[si]) / seg_len[si][:, None]
        nv = np.stack([-dv[:, 1], dv[:, 0]], 1)
        off = rng.uniform(-(h.fw / 2 + h.rough + 4), h.fw / 2 + h.rough + 4, 3000)
        pts.append(base + nv * off[:, None])
        P = np.vstack(pts)
        n_raw += len(P)
        c0 = mir.codes_chunked(P[:, 0], P[:, 1])
        uni = np.ones(len(P), bool)
        for rr in (0.75, 1.5):
            for dx, dd in zip(cs * rr, sn * rr):
                uni &= mir.codes_chunked(P[:, 0] + dx, P[:, 1] + dd) == c0
        elig_P.append(P[uni])
        elig_c.append(c0[uni])
        if sum(len(e) for e in elig_P) >= 6000:
            break
    P = np.vstack(elig_P)
    c0 = np.concatenate(elig_c)
    counts = {LIE_NAMES[i]: int((c0 == i).sum()) for i in range(7) if (c0 == i).any()}
    problems = []
    if len(P) < 6000:
        problems.append(f"only {len(P)} eligible sample points (need >= 6000)")
    for lie, need in ((LG, 50), (LT, 20), (LF, 100)):
        if counts.get(LIE_NAMES[lie], 0) < need:
            problems.append(f"only {counts.get(LIE_NAMES[lie], 0)} eligible {LIE_NAMES[lie]} points (need >= {need}): the sampling does not cover it")
    for i, k in enumerate(C.bunkers):
        inside = (ellipse_val(P[:, 0], P[:, 1], k) <= 1) & (c0 == LB)
        if inside.sum() < 10:
            problems.append(f"bunker {k[5] or i + 1}: only {int(inside.sum())} interior sample points")
    # per point context for the legal overlays
    off_all = mir.off(P[:, 0], P[:, 1])
    dpin = np.hypot(P[:, 0] - h.pin[0], P[:, 1] - h.pin[1])
    dtee = np.hypot(P[:, 0] - h.tee[0], P[:, 1] - h.tee[1])
    lipfoot = np.zeros(len(P), bool)
    for k in C.bunkers:
        lipfoot |= ellipse_val(P[:, 0], P[:, 1], k) <= 1.30 ** 2
    fc_yd = max(3.0, float(C.scenery.get("firstcut_yd", 3.0))) / YD + 1.0
    ap_yd = float(C.scenery.get("apron_yd", 3.0)) + 1.0
    bad = []
    nhit = 0
    for i in range(len(P)):
        lie = int(c0[i])
        r = rays.cast(float(P[i, 0] * YD), float(P[i, 1] * YD))
        if lie == LW:
            if r is None:
                continue
            bad.append((i, "Water expects no hit", rays.names[r[1]], r[0]))
            continue
        nhit += 1
        if r is None:
            bad.append((i, f"{LIE_NAMES[lie]} expects {EXPECT_NAME[lie]}", "no hit", None))
            continue
        z, oi = r
        fam = rays.fam[oi]
        ok = False
        if lie == LG:
            ok = fam == "GREEN"
        elif lie == LB:
            ok = fam == "SAND"
        elif lie == LT:
            ok = fam == "TEE_BOX"
        elif lie == LF:
            ok = fam == "FAIRWAY" or (fam == "LIP" and lipfoot[i]) or (fam == "TEE_BOX" and dtee[i] <= tee_extent_yd)
        else:      # rough, out of bounds
            ok = (fam == "TERRAIN" or (fam == "LIP" and lipfoot[i]) or (fam == "APRON" and dpin[i] <= h.gr + ap_yd)
                  or (fam == "FIRSTCUT" and lie == LR and off_all[i] <= h.fw / 2 + fc_yd))
        if ok and not (pz - 1e-3 <= z <= pz + Z_BAND_TOP + 1e-3):
            ok = False
        if not ok:
            bad.append((i, f"{LIE_NAMES[lie]} expects {EXPECT_NAME[lie]}", rays.names[oi], z))
    if bad:
        deep = []
        cand = bad if len(bad) <= 400 else bad[::len(bad) // 400 + 1]       # rank at most ~400 offenders (keeps a broken hole fast)
        for i, want, got, z in cand:
            dp = depth_of(mir, float(P[i, 0]), float(P[i, 1]), int(c0[i]))
            deep.append((dp, i, want, got, z))
        deep.sort(key=lambda t: -t[0])
        worst = [f"({P[i, 0]:.1f}, {P[i, 1]:.1f}) yd: lie {want}, found {got}"
                 f"{'' if z is None else f' at z {z:.2f}'} ({dp:g}+ yd inside the region)" for dp, i, want, got, z in deep[:5]]
        problems.append(f"{len(bad)} mismatches of {len(P)} points; worst: " + " | ".join(worst))
    return not problems, ("; ".join(problems) if problems else
                          f"{len(P)} eligible points of {n_raw} drawn (all 24 neighbours within 1.5 yd share the lie), by lie {counts}: "
                          f"water -> no hit, green -> GREEN, bunker -> sand, fairway -> FAIRWAY, tee -> TEE_BOX, rough/OOB -> TERRAIN "
                          f"(overlays only inside lip 1.3x / tee pad {tee_extent_yd:.1f} yd / first cut / apron); 0 mismatches")


# ----------------------------------------------------------------------------- terrain top surface: boundary, islands
def terrain_top(S):
    """(W, T) of every up-facing (nz > 0.5) TERRAIN triangle, vertices unified by position (1 mm)."""
    Ws, Ts, off = [], [], 0
    for n, o in S.ground.items():
        if not n.startswith("TERRAIN"):
            continue
        W, T, N, A = S.mesh(o)
        top = N[:, 2] > 0.5
        if top.any():
            Ws.append(W)
            Ts.append(T[top] + off)
            off += len(W)
    if not Ws:
        return None, None
    W = np.vstack(Ws)
    T = np.vstack(Ts)
    used = np.unique(T)
    key = np.round(W[used] * 1000).astype(np.int64)
    _u, inv = np.unique(key, axis=0, return_inverse=True)
    mp = np.full(len(W), -1, np.int64)
    mp[used] = inv.ravel()
    T2 = mp[T]
    Wu = np.zeros((len(_u), 3))
    Wu[inv.ravel()] = W[used]
    keep = (T2[:, 0] != T2[:, 1]) & (T2[:, 1] != T2[:, 2]) & (T2[:, 0] != T2[:, 2])
    return Wu, T2[keep]


def g_shore(S, C):
    Wt, Tt = terrain_top(S)
    if Wt is None:
        return False, "TERRAIN has no top surface"
    e = np.vstack([Tt[:, [0, 1]], Tt[:, [1, 2]], Tt[:, [2, 0]]])
    e.sort(axis=1)
    ue, cnt = np.unique(e, axis=0, return_counts=True)
    bnd = ue[cnt == 1]
    A = Wt[bnd[:, 0], :2]
    B = Wt[bnd[:, 1], :2]
    meas = densify(A, B, 0.1)
    meas = np.vstack([meas, B])
    h = C.h
    shore = np.array(h.shore, float) * YD
    ref_parts = [densify(shore, np.roll(shore, -1, axis=0), 0.1)]
    arcs = []
    for k in C.waters:
        a, b = k[3] / 2 * YD, k[4] / 2 * YD
        per = math.pi * (3 * (a + b) - math.sqrt((3 * a + b) * (a + 3 * b)))
        n = max(720, int(per / 0.1))
        t = np.linspace(0, 2 * np.pi, n, endpoint=False)
        arcs.append(np.stack([k[1] * YD + a * np.cos(t), k[2] * YD + b * np.sin(t)], 1))
    # reference boundary of the land L = inside(shore) and not inside any water ellipse
    sh = ref_parts[0]
    keep = np.ones(len(sh), bool)
    for k in C.waters:
        keep &= ellipse_val(sh[:, 0] / YD, sh[:, 1] / YD, k) >= 1 - 1e-4
    ref = [sh[keep]]
    arc_ref = []
    for i, (k, ar) in enumerate(zip(C.waters, arcs)):
        yd = ar / YD
        ok = C.mirror.inside(yd[:, 0], yd[:, 1])
        for j, k2 in enumerate(C.waters):
            if j != i:
                ok &= ellipse_val(yd[:, 0], yd[:, 1], k2) >= 1 - 1e-4
        arc_ref.append(ar[ok])
        ref.append(ar[ok])
    ref = np.vstack(ref)

    def kd(P):
        t = KDTree(len(P))
        for i, p in enumerate(P):
            t.insert((float(p[0]), float(p[1]), 0.0), i)
        t.balance()
        return t

    def nn(tree, Q):
        f = tree.find
        return np.array([f((float(q[0]), float(q[1]), 0.0))[2] for q in Q])
    d_mr = nn(kd(ref), meas) / YD            # measured boundary -> design
    d_rm = nn(kd(meas), ref) / YD            # design boundary -> measured
    problems = []
    if d_mr.max() > 0.5:
        i = int(d_mr.argmax())
        problems.append(f"mesh top boundary strays {d_mr.max():.2f} yd from the design (at {meas[i][0] / YD:.1f}, {meas[i][1] / YD:.1f} yd)")
    if d_rm.max() > 0.5:
        i = int(d_rm.argmax())
        problems.append(f"design boundary missing in the mesh by {d_rm.max():.2f} yd (at {ref[i][0] / YD:.1f}, {ref[i][1] / YD:.1f} yd)")
    cut_txt = []
    mt = kd(meas)
    for k, ar in zip(C.waters, arc_ref):
        if len(ar) == 0:
            cut_txt.append(f"{k[5] or 'water'}: no arc inside the shore")
            continue
        dd = nn(mt, ar) / YD
        cut_txt.append(f"{k[5] or 'water'} cut max {dd.max():.3f} yd")
        if dd.max() > 0.5:
            problems.append(f"water ellipse '{k[5]}' is not cut out of the top surface (max {dd.max():.2f} yd, {int((dd > 0.5).sum())} of {len(dd)} arc points)")
    return not problems, ("; ".join(problems) if problems else
                          f"{len(bnd)} top boundary edges ({len(meas)} samples) vs {len(ref)} design boundary samples: Hausdorff mesh->design "
                          f"{d_mr.max():.3f} yd, design->mesh {d_rm.max():.3f} yd (<= 0.5); " + ", ".join(cut_txt))


def g_pads(S, C):
    Wt, Tt = terrain_top(S)
    if Wt is None:
        return False, "TERRAIN has no top surface"
    parent = list(range(len(Wt)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for a, b, c in Tt.tolist():
        ra = find(a)
        for v in (b, c):
            rv = find(v)
            if rv != ra:
                parent[rv] = ra
    _n, A = tri_data(Wt, Tt)
    area = {}
    for tri, a in zip(Tt.tolist(), A):
        r = find(tri[0])
        area[r] = area.get(r, 0.0) + float(a)
    n_mesh = len(area)
    # the dry components postcard_check finds: its own Grid.components on the 1 yd lie grid
    pc = C.pc
    cell, margin = 1.0, 4.0
    g = pc.Grid.__new__(pc.Grid)
    g.h, g.cell = C.h, cell
    xs = [p[0] for p in C.h.shore]
    ds = [p[1] for p in C.h.shore]
    g.x0, g.d0 = min(xs) - margin, min(ds) - margin
    g.nx = int(math.ceil((max(xs) + margin - g.x0) / cell))
    g.nd = int(math.ceil((max(ds) + margin - g.d0) / cell))
    I, J = np.meshgrid(np.arange(g.nx), np.arange(g.nd), indexing="ij")
    codes = C.mirror.codes_chunked(g.x0 + (I.ravel() + 0.5) * cell, g.d0 + (J.ravel() + 0.5) * cell)
    names = np.array(LIE_NAMES, dtype=object)[codes]
    g.lie = dict(zip(zip(I.ravel().tolist(), J.ravel().tolist()), names.tolist()))
    pads_ok = [c for c in g.components(lambda l: l in pc.LAND_OK) if len(c) * cell ** 2 >= 1]
    pads_dry = [c for c in g.components(lambda l: l != pc.WATER) if len(c) * cell ** 2 >= 1]
    want = len(pads_ok)
    lim = C.limits.get("pads")
    problems = []
    if n_mesh != want:
        problems.append(f"TERRAIN top has {n_mesh} islands, postcard_check finds {want} dry components")
    if len(pads_dry) != want:
        problems.append(f"(design note: {len(pads_dry)} components when out-of-bounds grass counts as land)")
    if lim is not None and n_mesh != lim:
        problems.append(f"design LIMITS pads={lim} but the mesh has {n_mesh}")
    return not problems, ("; ".join(problems) if problems else
                          f"TERRAIN top surface has {n_mesh} separate islands, areas {[round(a / YD ** 2) for a in sorted(area.values(), reverse=True)]} yd2 "
                          f"= {want} dry components in postcard_check" + (f" = LIMITS pads {lim}" if lim is not None else ""))


# ----------------------------------------------------------------------------- bunkers
def g_hazards(S, C):
    sand = {n: o for n, o in S.ground.items() if re.fullmatch(r"BUNKER_\d\d", n)}
    lips = {n: o for n, o in S.ground.items() if re.fullmatch(r"BUNKER_\d\d_LIP", n)}
    if len(sand) != len(C.bunkers):
        return False, f"{len(sand)} sand meshes for {len(C.bunkers)} design bunkers"
    info = {}
    for n, o in sand.items():
        W, T, N, A = S.mesh(o)
        plan = A * np.abs(N[:, 2])
        cen3 = W[T].mean(axis=1)
        tot = float(plan.sum())
        c = (cen3[:, :2] * plan[:, None]).sum(0) / max(tot, 1e-12) / YD
        info[n] = (c, tot / YD ** 2, W[:, :2] / YD)
    pairs = []
    for bi, k in enumerate(C.bunkers):
        for n, (c, ar, V) in info.items():
            pairs.append((math.hypot(c[0] - k[1], c[1] - k[2]), bi, n))
    pairs.sort()
    used_b, used_n, match = set(), set(), {}
    for d, bi, n in pairs:
        if bi in used_b or n in used_n:
            continue
        used_b.add(bi)
        used_n.add(n)
        match[bi] = n
    problems, txt = [], []
    for bi, k in enumerate(C.bunkers):
        n = match[bi]
        c, ar, V = info[n]
        d = math.hypot(c[0] - k[1], c[1] - k[2])
        grow = ((V[:, 0] - k[1]) / (k[3] / 2 + 0.5)) ** 2 + ((V[:, 1] - k[2]) / (k[4] / 2 + 0.5)) ** 2
        ell_area = math.pi * (k[3] / 2) * (k[4] / 2)
        ratio = ar / ell_area
        msg = f"{n}~'{k[5] or bi + 1}': centroid {d:.3f} yd, extent {math.sqrt(grow.max()):.3f}x the ellipse+0.5 yd, area {ratio:.2f}x"
        if d > 0.5:
            problems.append(f"{n}: centroid {d:.2f} yd from the ellipse centre of '{k[5] or bi + 1}' (<= 0.5)")
        if grow.max() > 1.0:
            problems.append(f"{n}: sand extends beyond the ellipse + 0.5 yd (factor {math.sqrt(grow.max()):.2f})")
        if not 0.9 <= ratio <= 1.1:
            problems.append(f"{n}: sand plan area is {ratio:.2f} x the ellipse (want 0.9-1.1)")
        ln = n + "_LIP"
        if ln in lips:
            Wl, Tl, Nl, Al = S.mesh(lips[ln])
            V2 = Wl[:, :2] / YD
            g2 = ((V2[:, 0] - k[1]) / (k[3] / 2 * 1.30 + 0.5)) ** 2 + ((V2[:, 1] - k[2]) / (k[4] / 2 * 1.30 + 0.5)) ** 2
            if g2.max() > 1.0:
                problems.append(f"{ln}: lip leaves 1.30x the ellipse + 0.5 yd")
            msg += ", lip ok"
        txt.append(msg)
    return not problems, ("; ".join(problems) if problems else
                          f"{len(C.bunkers)} bunkers matched one-to-one to sand meshes: " + " | ".join(txt))


# ----------------------------------------------------------------------------- materials, animation, water level
def srgb_of(c):
    c = float(c)
    return 12.92 * c if c <= 0.0031308 else 1.055 * (max(c, 0.0) ** (1 / 2.4)) - 0.055


def principled_colour(mat):
    nt = mat.node_tree
    if nt is not None:
        for n in nt.nodes:
            if n.type == "BSDF_PRINCIPLED":
                return tuple(float(v) for v in n.inputs["Base Color"].default_value[:3])
    return tuple(float(v) for v in mat.diffuse_color[:3])


def load_h07_blend_palette(path):
    """MAT_* name -> linear base colour from hole_07.blend, read through libraries.load (read-only) and removed again."""
    pal = {}
    try:
        with bpy.data.libraries.load(path, link=False) as (df, dt):
            dt.materials = [n for n in df.materials if n.startswith("MAT_")]
        for mat in dt.materials:
            if mat is not None:                                  # a name already in the open file loads as MAT_X.001
                pal[re.sub(r"\.\d{3}$", "", mat.name)] = principled_colour(mat)
        for mat in dt.materials:
            if mat is not None:
                bpy.data.materials.remove(mat)
    except Exception as e:  # noqa: BLE001
        print("NOTE: could not read the hole 7 palette from the blend:", e)
    return pal


def g_materials(S, C, parsed_fbx=None):
    problems = []
    if S.stage == "blend":
        mats = {m.name: m for m in bpy.data.materials if m.users > 0 or m.use_fake_user}
        cols = {n: principled_colour(m) for n, m in mats.items()}
        names = set(mats)
        ref = C.ref_pal
    else:
        names = set(parsed_fbx["mats"])
        cols = {n: c for n, c in parsed_fbx["mats"].items()}
        ref = C.ref_pal
        mats = {m.name: m for m in bpy.data.materials}
    allowed = set(CONTRACT_MATS) | ({"MAT_LAVA"} if C.lava else set())
    extra = sorted(names - allowed)
    if extra:
        problems.append(f"materials outside the contract: {extra}" + ("" if "MAT_LAVA" not in extra else " (MAT_LAVA is only for the lava hole)"))
    # who uses MAT_LAVA, and does the lava hole use it where it must
    lava_users = sorted(n for n, o in S.meshes.items() if any(sl.material is not None and sl.material.name == "MAT_LAVA" for sl in o.material_slots))
    if C.lava:
        if lava_users != [C.water_name]:
            problems.append(f"MAT_LAVA must be on exactly {C.water_name}, it is on {lava_users}")
        lv = S.meshes.get(C.water_name)
        if lv is not None and not lv.name.startswith("WATER"):
            problems.append("lava mesh name does not start with WATER")
        col = cols.get("MAT_LAVA")
        if col is None:
            problems.append("MAT_LAVA missing")
        elif not (col[0] >= 0.8 and col[0] > 2 * col[1] and col[2] <= 0.25):
            problems.append(f"MAT_LAVA colour {tuple(round(v, 3) for v in col)} is not orange")
    elif lava_users:
        problems.append(f"MAT_LAVA used on {lava_users} in a hole without lava")
    # hole 7's palette colours unchanged
    dev, worst = [], 0.0
    shared = [n for n in names if n in ref]
    for n in shared:
        d = max(abs(srgb_of(a) - srgb_of(b)) for a, b in zip(cols[n], ref[n]))
        worst = max(worst, d)
        if d > COLOUR_TOL_SRGB:
            problems.append(f"{n} differs from hole 7 by {d * 255:.1f}/255 in sRGB (> 3)")
        elif d > 0.5 / 255:
            dev.append(f"{n} {d * 255:.1f}/255")
    if len(shared) < 8:
        problems.append(f"only {len(shared)} MAT_* names could be compared with hole 7 (reference palette has {len(ref)}): need >= 8")
    return not problems, ("; ".join(problems) if problems else
                          f"{len(names)} materials, all contract names ({'MAT_LAVA only on ' + C.water_name + ', orange' if C.lava else 'no MAT_LAVA'}); "
                          f"{len(shared)} shared MAT_* colours equal hole 7 ({C.ref_pal_src}) within 3/255, worst {worst * 255:.1f}/255"
                          + (f" [small differences: {', '.join(dev)}]" if dev else ""))


def g_no_anim(S, C):
    bad = []
    for ob in bpy.data.objects:
        if ob.animation_data is not None:
            bad.append(f"{ob.name} has animation_data")
        if ob.type == "ARMATURE":
            bad.append(f"{ob.name} is an armature")
        d = ob.data
        if d is not None and getattr(d, "shape_keys", None) is not None:
            bad.append(f"{ob.name} has shape keys")
        if d is not None and getattr(d, "animation_data", None) is not None:
            bad.append(f"{ob.name} data has animation_data")
    for mat in bpy.data.materials:
        if mat.animation_data is not None or (mat.node_tree is not None and mat.node_tree.animation_data is not None):
            bad.append(f"material {mat.name} is animated")
    if len(bpy.data.actions):
        bad.append(f"{len(bpy.data.actions)} actions: {[a.name for a in bpy.data.actions][:4]}")
    if len(bpy.data.shape_keys):
        bad.append(f"{len(bpy.data.shape_keys)} shape key datablocks")
    return not bad, ("; ".join(bad[:8]) if bad else
                     f"no animation data, actions, shape keys, armatures or drivers on {len(bpy.data.objects)} objects and {len(bpy.data.materials)} materials")


def g_water_level(S, C):
    problems = []
    pl = S.meshes.get(C.water_name)
    if pl is None:
        return False, f"{C.water_name} missing"
    W, T, N, A = S.mesh(pl)
    if W[:, 2].max() > 1e-3 or W[:, 2].min() < -1e-3:
        problems.append(f"{C.water_name} z {W[:, 2].min():.4f}..{W[:, 2].max():.4f}, the water level must be z = 0")
    bx = (np.array(C.h.shore) * YD)
    lo, hi = bx.min(0) - 20.0, bx.max(0) + 20.0
    if not (W[:, 0].min() <= lo[0] and W[:, 0].max() >= hi[0] and W[:, 1].min() <= lo[1] and W[:, 1].max() >= hi[1]):
        problems.append(f"{C.water_name} does not cover the shore plus 20 m")
    nwat = 0
    for n, o in S.meshes.items():
        if not n.startswith("WATER"):
            continue
        nwat += 1
        W, T, N, A = S.mesh(o)
        if len(W) and (W[:, 2].min() < -1e-3 or W[:, 2].max() > 0.2):
            problems.append(f"{n} z {W[:, 2].min():.3f}..{W[:, 2].max():.3f} (want 0..0.2)")
        if len(T) and (N[A > 1e-9][:, 2] <= 0).any():
            problems.append(f"{n} has faces that do not face up")
    return not problems, ("; ".join(problems) if problems else
                          f"{C.water_name} at z 0 covering the shore + 20 m; {nwat} WATER* meshes within z 0..0.2, all facing up")


# ----------------------------------------------------------------------------- FBX file level
def fbx_parse(path):
    from io_scene_fbx import parse_fbx
    root, ver = parse_fbx.parse(path)
    out = dict(ver=ver, globals={}, models={}, mats={})
    for e in root.elems:
        if e.id == b"GlobalSettings":
            for p in e.elems:
                if p.id == b"Properties70":
                    for q in p.elems:
                        if q.props[0] in FBX_GLOBAL_KEYS:
                            out["globals"][q.props[0]] = q.props[4]
        elif e.id == b"Objects":
            for m in e.elems:
                if m.id not in (b"Model", b"Material"):
                    continue
                nm = m.props[1].split(b"\x00\x01")[0].decode()
                d = {}
                for p in m.elems:
                    if p.id == b"Properties70":
                        for q in p.elems:
                            d[q.props[0].decode()] = tuple(q.props[4:])
                if m.id == b"Model":
                    out["models"][nm] = d
                else:
                    c = d.get("DiffuseColor") or d.get("Diffuse")
                    if c:
                        out["mats"][nm] = tuple(float(v) for v in c[:3])
    return out


def read_meta(path):
    d = {}
    with open(path) as f:
        for ln in f.read().splitlines():
            if ":" in ln:
                k, v = ln.split(":", 1)
                d.setdefault(k.strip(), []).append(v.strip())
    return d


def g_fbx_file(fbx, h07_fbx):
    meta = fbx + ".meta"
    problems = []
    if not os.path.isfile(fbx) or os.path.getsize(fbx) < 1000:
        return False, f"{fbx} missing or empty"
    if not os.path.isfile(meta):
        return False, f"{meta} missing (copy hole_07.fbx.meta with a fresh guid)"
    a, b = read_meta(meta), read_meta(h07_fbx + ".meta")
    guid = (a.get("guid") or [""])[0]
    if not re.fullmatch(r"[0-9a-f]{32}", guid):
        problems.append(f"guid '{guid}' is not 32 hex chars")
    if guid == (b.get("guid") or [""])[0]:
        problems.append("guid equals hole 7's guid")
    if "isReadable" not in a or a["isReadable"][0] != "1":
        problems.append(f"isReadable is {a.get('isReadable')}, must stay 1")
    diff = sorted(k for k in set(a) | set(b) if k != "guid" and a.get(k) != b.get(k))
    if diff:
        problems.append(f"import settings differ from hole_07.fbx.meta in {diff[:6]}")
    return not problems, ("; ".join(problems) if problems else
                          f"{os.path.basename(fbx)} {os.path.getsize(fbx) // 1024} KB; .meta guid {guid[:8]}... fresh, isReadable 1, all other settings equal hole_07.fbx.meta")


def close(a, b, tol=1e-6):
    if len(a) != len(b):
        return False
    return all(isinstance(x, (int, float)) and isinstance(y, (int, float)) and abs(x - y) <= tol * max(1.0, abs(x), abs(y))
               or x == y for x, y in zip(a, b))


def g_fbx_globals(new, old, C):
    problems = []
    for k in FBX_GLOBAL_KEYS:
        if new["globals"].get(k) != old["globals"].get(k):
            problems.append(f"GlobalSettings {k.decode()}: new {new['globals'].get(k)} vs hole 7 {old['globals'].get(k)}")
    if len(new["globals"]) != len(FBX_GLOBAL_KEYS):
        problems.append(f"only {len(new['globals'])} of {len(FBX_GLOBAL_KEYS)} GlobalSettings keys found")
    if new["ver"] != old["ver"]:
        problems.append(f"FBX version {new['ver']} vs hole 7 {old['ver']}")
    conv = ("Lcl Rotation", "Lcl Scaling", "PreRotation", "PostRotation", "RotationOrder", "InheritType", "RotationActive",
            "RotationPivot", "ScalingPivot", "Lcl Translation")
    cmp_n = 0

    def props(model, keys):
        return {k: v for k, v in model.items() if k in keys}
    r_new, r_old = new["models"].get(C.root_name), old["models"].get("HOLE_07_ROOT")
    if r_new is None or r_old is None:
        problems.append(f"root Model missing (new {C.root_name}: {r_new is not None}, hole 7: {r_old is not None})")
    else:
        a, b = props(r_new, conv), props(r_old, conv)
        cmp_n += 1
        if a.keys() != b.keys() or not all(close(a[k], b[k]) for k in a):
            problems.append(f"root transform convention differs: new {a} vs hole 7 {b}")
    unrot = ("MARKER_TEE", "MARKER_PIN", "MARKER_UP", "FAIRWAY", "FAIRWAY_FIRSTCUT", "GREEN", "GREEN_APRON", "TEE_BOX", "BUNKER_01",
             "BUNKER_01_LIP", "WATER_OCEAN")
    no_t = tuple(k for k in conv if k != "Lcl Translation")
    pairs = [(n, n) for n in unrot if n in new["models"] and n in old["models"]]
    t_new = [n for n in new["models"] if n.startswith("TERRAIN")]
    t_old = [n for n in old["models"] if n.startswith("TERRAIN")]
    if t_new and t_old:
        pairs.append((t_new[0], t_old[0]))
    for n_new, n_old in pairs:
        a, b = props(new["models"][n_new], no_t), props(old["models"][n_old], no_t)
        cmp_n += 1
        if n_new == "WATER_OCEAN":
            a.pop("Lcl Scaling", None)          # hole 7 scales a unit plane to 4000 m, the library writes real vertices
            b.pop("Lcl Scaling", None)
        if a.keys() != b.keys() or not all(close(a[k], b[k]) for k in a):
            problems.append(f"{n_new} transform convention differs from hole 7's {n_old}: {a} vs {b}")
    for n in new["models"]:
        if n.startswith(GROUND_PREFIXES):
            bad = props(new["models"][n], ("Lcl Rotation", "Lcl Scaling", "PreRotation"))
            if bad:
                problems.append(f"ground mesh {n} carries a node transform {bad} (hole 7 ground meshes have none)")
    return not problems, ("; ".join(problems) if problems else
                          f"GlobalSettings {{{', '.join(f'{k.decode()}={new['globals'][k]:g}' for k in FBX_GLOBAL_KEYS)}}} == hole_07.fbx, FBX version "
                          f"{new['ver']} equal; root {C.root_name} Lcl Rotation {r_new.get('Lcl Rotation')} equals HOLE_07_ROOT; {cmp_n} equivalent "
                          f"objects have identical transform conventions")


def g_fbx_budget(fbx, S):
    size = os.path.getsize(fbx)
    inst = 0
    uniq = {}
    for o in S.meshes.values():
        t = S.tri_count(o)
        inst += t
        uniq[o.data.name] = t
    ok = size <= 4 * 1024 * 1024 and inst <= 150000
    return ok, (f"{size / 1048576:.2f} MB (<= 4) and {inst} triangles counting every object (<= 150000; {sum(uniq.values())} "
                f"in unique meshes, {len(S.meshes)} mesh objects)")


# ----------------------------------------------------------------------------- the stage runners
def tee_extent(S, C):
    if "TEE_BOX" not in S.ground:
        return 0.0
    W, T, N, A = S.mesh(S.ground["TEE_BOX"])
    if len(W) == 0:
        return 0.0
    return float(np.hypot(W[:, 0] / YD - C.h.tee[0], W[:, 1] / YD - C.h.tee[1]).max()) + 0.5


def run_stage(stage, C, h07_fbx_parsed=None, new_fbx_parsed=None):
    print(f"--- stage {stage}: {len(bpy.context.scene.objects)} objects in the scene", flush=True)
    S = Snap(stage)
    run_gate(stage, "MARKERS", g_markers, S, C)
    run_gate(stage, "CONTRACT_OBJECTS", g_contract_objects, S, C)
    run_gate(stage, "GROUND_MESHES", g_ground_meshes, S, C)
    run_gate(stage, "ONE_HEIGHT", g_one_height, S, C)
    try:
        rays = GroundRays(S)
    except Exception as e:  # noqa: BLE001
        rays = None
        gate("GROUND_BVH", False, f"could not build the ground BVH: {e}", stage)
    if rays is not None:
        run_gate(stage, "NO_UPFACE_OUTSIDE_LAND", g_no_upface, S, C, rays)
        run_gate(stage, "LIES_MATCH_MESH", g_lies, S, C, rays, tee_extent(S, C))
    run_gate(stage, "SHORE_MATCH", g_shore, S, C)
    run_gate(stage, "HAZARD_PLACEMENT", g_hazards, S, C)
    run_gate(stage, "PADS", g_pads, S, C)
    run_gate(stage, "MATERIALS", g_materials, S, C, new_fbx_parsed)
    run_gate(stage, "NO_ANIMATION", g_no_anim, S, C)
    run_gate(stage, "WATER_LEVEL", g_water_level, S, C)
    return S


def lie_mirror_gate(C):
    rng = np.random.default_rng(99)
    x0, d0, x1, d1 = C.bbox
    P = [np.stack([rng.uniform(x0 - 15, x1 + 15, 2000), rng.uniform(d0 - 15, d1 + 15, 2000)], 1)]
    h = C.h
    sh = np.array(h.shore)
    near = sh[rng.integers(0, len(sh), 300)] + rng.normal(0, 0.4, (300, 2))
    P.append(near)
    for k in C.h.hazards:
        a = rng.uniform(0, 2 * np.pi, 120)
        s = rng.uniform(0.97, 1.03, 120)
        P.append(np.stack([k[1] + k[3] / 2 * s * np.cos(a), k[2] + k[4] / 2 * s * np.sin(a)], 1))
    cl = np.array(h.center)
    i = rng.integers(1, len(cl), 200)
    t = rng.uniform(0, 1, 200)
    base = cl[i - 1] + (cl[i] - cl[i - 1]) * t[:, None]
    dv = (cl[i] - cl[i - 1]) / np.hypot(*(cl[i] - cl[i - 1]).T)[:, None]
    nv = np.stack([-dv[:, 1], dv[:, 0]], 1)
    for off in (h.fw / 2, h.fw / 2 + h.rough, h.gr, 4.0):
        P.append(base + nv * (off * rng.uniform(0.98, 1.02, 200))[:, None])
    P = np.vstack(P)
    codes = C.mirror.codes(P[:, 0], P[:, 1])
    bad = [(round(float(p[0]), 2), round(float(p[1]), 2), LIE_NAMES[c], h.lie_at((float(p[0]), float(p[1])))) for p, c in zip(P, codes)
           if LIE_NAMES[c] != h.lie_at((float(p[0]), float(p[1])))]
    return not bad, f"numpy scoring mirror vs postcard_check.Hole.lie_at at {len(P)} random and boundary-hugging points: {len(bad)} mismatches {bad[:3]}"


def main():
    t_start = time.time()
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(design=None, fbx=None, blend_only=False, fbx_only=False, json=None, h07_blend=HOLE07_BLEND, h07_fbx=HOLE07_FBX)
    i = 0
    while i < len(argv):
        a = argv[i]
        if a in ("--fbx", "--json", "--hole07-blend", "--hole07-fbx"):
            if i + 1 >= len(argv):
                print(f"usage error: {a} needs a value")
                return 2
            key = {"--fbx": "fbx", "--json": "json", "--hole07-blend": "h07_blend", "--hole07-fbx": "h07_fbx"}[a]
            opts[key] = argv[i + 1]
            i += 2
        elif a == "--blend-only":
            opts["blend_only"] = True
            i += 1
        elif a == "--fbx-only":
            opts["fbx_only"] = True
            i += 1
        elif a.startswith("--"):
            print(f"usage error: unknown option {a}")
            return 2
        else:
            opts["design"] = a
            i += 1
    if not opts["design"] or (opts["blend_only"] and opts["fbx_only"]):
        print(__doc__)
        return 2
    blend_path = bpy.data.filepath
    mod = load_design(opts["design"])
    C = Ctx(mod, opts)
    fbx = os.path.abspath(opts["fbx"]) if opts["fbx"] else os.path.join(COURSE_DIR, f"hole_{C.tag}.fbx")
    do_blend = not opts["fbx_only"] and bool(blend_path)
    do_fbx = not opts["blend_only"]
    print(f"postcard_verify: hole {C.number} {C.name} (par {C.h.par}, play_z {C.pz}, {len(C.bunkers)} bunker(s), {len(C.waters)} water, "
          f"lava {C.lava}); blend {blend_path or '(none open)'}; fbx {fbx}", flush=True)
    if not do_blend and not do_fbx:
        print("usage error: nothing to check (--blend-only needs a .blend opened with `Blender -b <file>.blend --python ...`)")
        return 2
    if not do_blend and not opts["fbx_only"]:
        print("NOTE: no .blend is open, the blend stage is skipped (open one with `Blender -b <file>.blend --python ...`)")
    if opts["fbx_only"]:
        print("NOTE: --fbx-only, the blend stage is skipped")
    if opts["blend_only"]:
        print("NOTE: --blend-only, the FBX stage is skipped")

    h07_files = (("blend", opts["h07_blend"]), ("fbx", opts["h07_fbx"]), ("meta", opts["h07_fbx"] + ".meta"))
    sha_before = {key: (sha256_file(p) if os.path.isfile(p) else None) for key, p in h07_files}
    run_gate(None, "LIE_MIRROR", lie_mirror_gate, C)

    if do_blend:
        C.ref_pal = load_h07_blend_palette(opts["h07_blend"])
        C.ref_pal_src = "hole_07.blend"
        run_stage("blend", C)
    if do_fbx:
        if not os.path.isfile(fbx):
            gate("FBX_FILE", False, f"{fbx} does not exist", "fbx")
        else:
            run_gate("fbx", "FBX_FILE", g_fbx_file, fbx, opts["h07_fbx"])
            new = fbx_parse(fbx)
            old = fbx_parse(opts["h07_fbx"])
            C.ref_pal = old["mats"]
            C.ref_pal_src = "hole_07.fbx DiffuseColor"
            run_gate("fbx", "FBX_GLOBALS", g_fbx_globals, new, old, C)
            try:
                bpy.ops.wm.read_factory_settings(use_empty=True)
                bpy.ops.import_scene.fbx(filepath=fbx)
                S = run_stage("fbx", C, old, new)
                run_gate("fbx", "FBX_BUDGET", g_fbx_budget, fbx, S)
            except Exception as e:  # noqa: BLE001
                traceback.print_exc()
                gate("FBX_IMPORT", False, f"{type(e).__name__}: {e}", "fbx")
    after = {key: (sha256_file(p) if os.path.isfile(p) else None) for key, p in h07_files}
    real = (opts["h07_blend"] == HOLE07_BLEND and opts["h07_fbx"] == HOLE07_FBX)
    ok = after == sha_before and after["blend"] == H07_BLEND_SHA and after["fbx"] == H07_FBX_SHA
    gate("HOLE07_UNTOUCHED", ok, f"hole_07.blend {str(after['blend'])[:12]} (want {H07_BLEND_SHA[:12]}), hole_07.fbx {str(after['fbx'])[:12]} "
         f"(want {H07_FBX_SHA[:12]}), .meta {str(after['meta'])[:12]}; none of the three changed by this run: {after == sha_before}" + ("" if real else " [test hook: other files checked]"))
    fails = [r["name"] for r in RES if not r["ok"]]
    total = time.time() - t_start
    print(f"postcard_verify: {len(RES)} gates, {len(fails)} failed, {total:.1f} s", flush=True)
    print("RESULT:", "ALL PASS" if not fails else f"FAILURES {fails}", flush=True)
    if opts["json"]:
        with open(opts["json"], "w") as f:
            json.dump(dict(design=opts["design"], hole=C.number, name=C.name, blend=blend_path, fbx=fbx, seconds=round(total, 1),
                           result="ALL PASS" if not fails else "FAILURES", failures=fails, gates=RES, gate_seconds=TIMES), f, indent=1)
    return 0 if not fails else 1


if __name__ == "__main__":
    code = 1
    try:
        code = main()
    except SystemExit as e:
        code = int(e.code or 0)
    except Exception:  # noqa: BLE001
        traceback.print_exc()
        print("RESULT: FAILURES ['CHECKER_CRASHED']")
        code = 1
    sys.stdout.flush()
    sys.exit(code)
