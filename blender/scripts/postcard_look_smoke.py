"""postcard_look_smoke.py - smoke test + gates for postcard_look_lib (role A3 of POSTCARD_LOOK).

Builds a postcard hole with its EXISTING build script into a scratch dir (postcard_lib.start is patched so nothing in the
repo is written), snapshots every ground mesh, runs the look passes, gates the result, renders Blender previews and checks
the exported FBX by re-importing it. Blender renders are NOT proof for the Game view; they are for judging texture scale
and shapes at the phone camera.

    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python blender/scripts/postcard_look_smoke.py -- \
        --hole 09 [--out DIR] [--no-render] [--live-build] [--verify]

    --hole NN        08, 09 (default) or 10
    --out DIR        default work/postcard-look/lib_ground/smoke_holeNN/ (base build in DIR/base, FBX in DIR/look/)
    --live-build     run blender/scripts/holeNN_build.py (the per-hole agents edit it) instead of the frozen baseline copy
                     work/postcard-look/baseline/scripts/holeNN_build.py
    --no-render      skip the EEVEE previews
    --verify         also run postcard_verify.py (blend + FBX) on the base build and on the look build in sub-processes

Writes DIR/smoke_holeNN.blend (hole 9: also work/postcard-look/lib_ground/smoke_hole09.blend), DIR/look/hole_NN.fbx,
renders DIR/render_*.png and DIR/gates_holeNN.json. Prints `GATE: <name> PASS|FAIL - detail` lines; exit 1 on a FAIL
(postcard_verify's MATERIALS / FBX_BUDGET lines are reported separately: the LK names and the look budget supersede them).
"""
import json
import math
import os
import re
import runpy
import shutil
import subprocess
import sys
import time

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
REPO = os.path.dirname(os.path.dirname(HERE))

import numpy as np  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import postcard_lib as P  # noqa: E402
import postcard_look_lib as L  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def opt(name, default=None):
    return args[args.index(name) + 1] if name in args else default


HOLE = opt("--hole", "09").zfill(2)
OUT = os.path.abspath(opt("--out", os.path.join(REPO, "work", "postcard-look", "v3", "lib", "ground", f"smoke_hole{HOLE}")))
BASE = os.path.join(OUT, "base")
LOOKDIR = os.path.join(OUT, "look")
RENDER = "--no-render" not in args
VERIFY = "--verify" in args
SCRIPT = (os.path.join(HERE, f"hole{HOLE}_build.py") if "--live-build" in args
          else os.path.join(REPO, "work", "postcard-look", "baseline", "scripts", f"hole{HOLE}_build.py"))
GATES = []


def gate(name, ok, detail):
    GATES.append(dict(name=name, ok=bool(ok), detail=str(detail)))
    print(f"GATE: {name} {'PASS' if ok else 'FAIL'} - {detail}", flush=True)


# ------------------------------------------------------------------------------------------------ 1. baseline build
for d in (OUT, BASE, LOOKDIR):
    os.makedirs(d, exist_ok=True)
_orig_start = P.start
P.start = lambda name, out_dir=None: _orig_start(name, out_dir=BASE)
t0 = time.time()
argv_keep = sys.argv
sys.argv = [SCRIPT]
try:
    runpy.run_path(SCRIPT, run_name="__main__")
except SystemExit as e:
    print(f"(build script exit code {e.code})")
sys.argv = argv_keep
P.start = _orig_start
D = P._ST["D"]
t_build = time.time() - t0
print(f"[smoke] baseline build of hole {HOLE} with {os.path.relpath(SCRIPT, REPO)}: {t_build:.1f}s")

# ------------------------------------------------------------------------------------------------ 2. look passes
snap = L.snapshot_ground(D)
t1 = time.time()
L.look_materials(D)
if HOLE == "10":
    L.retarget_materials(D, overrides=L.CRATER_OVERRIDES)
else:
    L.retarget_materials(D)
if HOLE == "09":
    R = D.SCENERY["ridge"]
    w = R.get("width_yd", 40.0)
    poly = L.spine_polygon(R["spine"], float(max(w) if isinstance(w, (list, tuple)) else w) / 2 + 12.0)
    n_scrub = L.assign_region_material(D, poly, "LK_SCRUB")
loops = L.terrain_top_loops(D)
POCKETS, fall_foot = [], None
if HOLE == "10":
    pin = P.m(*D.hole.pin)
    pillar = [i for i, (Pl, _) in enumerate(loops) if P.point_in_poly(pin, [tuple(p) for p in Pl])]
    rim = [i for i in range(len(loops)) if i not in pillar]
    skins = L.build_cliff_skin(D, style="basalt", loops=rim, follow_undercut=0.0)
    skins += L.build_cliff_skin(D, style="basalt", loops=pillar, follow_undercut=1.0, seed=1)
    L.build_lava(D, lights=4)
else:
    skins = L.build_cliff_skin(D, style="strata")
    if HOLE == "08":                                   # waterfall foot on the middle pad's east cliff (needle.jpg): a shelf pocket + a surf patch
        fp = L.open_shore_point(D, (30.0, 160.0), free_m=40.0)
        fall_foot = (fp[0] + fp[2] * 3.0, fp[1] + fp[3] * 3.0)
        POCKETS = [(fall_foot[0], fall_foot[1], 6.0)]
    L.build_sea(D, pockets=POCKETS)
    if HOLE == "08":
        L.surf_patch(D, fall_foot, radius_m=4.0)
        print(f"[smoke] waterfall foot (shelf pocket + surf patch) at ({fall_foot[0]:.1f}, {fall_foot[1]:.1f}) m", flush=True)
if HOLE == "08":
    L.build_path(D, [(-9.0, -6.0), (-8.5, 12.0), (-6.0, 28.0), (-3.0, 40.0)], width_m=2.6)
L.assign_uvs(D)
t_look = time.time() - t1

# ------------------------------------------------------------------------------------------------ 3. gates on the scene
ok, detail = L.compare_ground(D, snap)
gate("LOOK_TOP_SURFACES_UNCHANGED", ok, detail)
for name, ok, detail in L.audit_look(D):
    gate(name, ok, detail)
for name, ok, detail in L.v2_gates(D, pockets=L.sea_pockets(D, POCKETS) if HOLE != "10" else None):
    gate(name, ok, detail)
tort = L.skin_tortuosity(D)
if tort is not None:
    gate("SKIN_JAGGED", tort >= 1.15, f"strata skin mid-row plan tortuosity (reviewer's measure, row 6 of 13 vs a 9-column moving average) {tort:.3f} (>= 1.15; the old skin 1.03-1.07)")
if L.LOOK and D.__dict__.get("look_missing"):
    print(f"[smoke] textures missing (flat fallback colours used): {', '.join(D.look_missing)}")

# wall hidden: rays from the sea toward the wall at random heights must hit a ROCK_SKIN before the TERRAIN wall
from mathutils.bvhtree import BVHTree  # noqa: E402


def bvh_of(obs):
    V, F = [], []
    for o in obs:
        W, ls, lt, lv, mi, pn = L._mesh_arrays(o)
        base = len(V)
        V += [tuple(p) for p in W.tolist()]
        F += [[base + int(v) for v in lv[a:a + n]] for a, n in zip(ls.tolist(), lt.tolist())]
    return BVHTree.FromPolygons(V, F)


skin_bvh = bvh_of([o for o in bpy.data.objects if o.name.startswith("ROCK_SKIN")])
ter_bvh = L._terrain_bvh(D)
rng = np.random.default_rng(int(HOLE))
hit_skin, hit_wall, tested = 0, 0, 0
for Pl, _ in loops:
    Pc, s, Lp, N, _ = L._loop_columns(Pl, 1.0)
    for i in rng.choice(len(Pc), size=min(len(Pc), 300), replace=False):
        z = rng.uniform(0.3, D.play_z - 0.6)
        o = Vector((Pc[i, 0] + N[i, 0] * 12.0, Pc[i, 1] + N[i, 1] * 12.0, z))
        dvec = Vector((-N[i, 0], -N[i, 1], rng.uniform(-0.25, 0.25))).normalized()
        hs = L._first_front(skin_bvh, o, dvec, 40.0)           # front faces only: Unity materials are single-sided
        hs = hs if hs is not None else (None, None, None, 0.0)
        hw = ter_bvh.ray_cast(o, dvec, 40.0)
        # only wall points the skin must cover: above the sea / lava and below the lowest skin top (the grass lip above it
        # is meant to show; under the water nothing is visible)
        if hw[0] is None or not (0.05 <= hw[0].z <= D.play_z - (0.12 if HOLE == "10" else 0.5)):
            continue
        tested += 1
        if hs[0] is not None and hs[3] <= hw[3] + 1e-4:
            hit_skin += 1
        else:
            hit_wall += 1
share = hit_skin / max(tested, 1)
gate("LOOK_WALL_HIDDEN_BY_SKIN", share >= 0.97,
     f"{tested} rays from 12 m out toward the TERRAIN wall that land on it between z 0.05 and the lowest skin top "
     f"(play - {0.12 if HOLE == '10' else 0.5}): {hit_skin} hit ROCK_SKIN first "
     f"({share * 100:.1f} %, >= 97 %), {hit_wall} reach the smooth wall")

# no skin face pointing up over the dry land above play-0.1
up_land = 0
for o in bpy.data.objects:
    if not o.name.startswith("ROCK_SKIN"):
        continue
    W, ls, lt, lv, mi, pn = L._mesh_arrays(o)
    pol = L._poly_of_loop(ls, lt, len(lv))
    cen = np.zeros((len(ls), 3))
    np.add.at(cen, pol, W[lv])
    cen /= lt[:, None]
    up = (pn[:, 2] > 0.3) & (cen[:, 2] > D.play_z - 0.05)
    up_land += int(up.sum())
gate("LOOK_SKIN_NO_UPFACE_IN_PLAY", up_land == 0, f"{up_land} up-facing ROCK_SKIN faces with centre above play_z - 0.05")

# ------------------------------------------------------------------------------------------------ 4. save + export
expect_ground = sorted(snap)
smoke_blend = os.path.join(OUT, f"smoke_hole{HOLE}.blend")
fbx = os.path.join(LOOKDIR, f"hole_{HOLE}.fbx")
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=smoke_blend)
L.export_look_fbx(D, fbx)
# All generated evidence stays under OUT; baseline paths are read-only.
st = P.stats(D)
gate("LOOK_BUDGET", st["tris"] <= 250000 and os.path.getsize(fbx) <= 20 * 1024 * 1024,
     f"{st['tris']} tris in the export set (<= 250000), FBX {os.path.getsize(fbx) / 1048576:.2f} MB (<= 20)")
skin_tris = sum(v[2] for k, v in st["by_object"].items() if k.startswith("ROCK_SKIN"))
water_tris = {k: v[2] for k, v in st["by_object"].items() if k.startswith(("WATER_OCEAN", "WATER_SHELF", "WATER_LAVA"))}
surf_tris = sum(v[2] for k, v in st["by_object"].items() if k.startswith("WATER_SURF"))
print(f"[smoke] tris: skin {skin_tris}, water {water_tris}, surf {surf_tris}, total {st['tris']}")


# ------------------------------------------------------------------------------------------------ 5. renders
def cam(name, loc, target, ortho=None, fov_v=None, res=None):
    P.remove_object(name)
    c = bpy.data.cameras.new(name)
    ob = bpy.data.objects.new(name, c)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    c.clip_start = 0.27
    c.clip_end = 6000
    if ortho:
        c.type = 'ORTHO'
        c.ortho_scale = ortho
    if fov_v:
        c.sensor_fit = 'VERTICAL'
        c.angle_y = math.radians(fov_v)
    return ob


def render(cam_ob, path, res, samples=24):
    P.render_camera(cam_ob.name, path, res, samples)
    return path


renders = []
if RENDER:
    sc = bpy.context.scene
    try:
        sc.view_settings.view_transform = 'AgX'
    except Exception:
        pass
    world = sc.world
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = P.rgb(150, 196, 240)
    bg.inputs[1].default_value = 1.0
    bx0, by0, bx1, by1 = P._shore_bbox_m(D)
    cx, cy = (bx0 + bx1) / 2, (by0 + by1) / 2
    span = max(bx1 - bx0, by1 - by0) * 1.12
    top = cam("CAM_LK_TOP", (cx, cy, 800.0), (cx, cy, 0.0), ortho=span)
    renders.append(render(top, os.path.join(OUT, "render_top.png"), (1100, int(1100 * min(2.0, (by1 - by0) / max(bx1 - bx0, 1)))), 16))
    for nm_ in ("BALL_START", "HOLE_CUP", "MARKER_TEE", "MARKER_PIN", "MARKER_UP"):   # Unity hides these placeholders
        if bpy.data.objects.get(nm_) is not None:
            bpy.data.objects[nm_].hide_render = True
    ov = bpy.data.objects.get("CAM_HOLE_OVERVIEW")
    if ov is not None:
        renders.append(render(ov, os.path.join(OUT, "render_oblique.png"), (1000, 1400) if HOLE != "10" else (1400, 900), 16))
    pz = D.play_z
    tee = Vector((0.0, 0.0, pz + P.Z_TEE + 0.04))
    stations = [Vector((*P.m(*p), pz + P.Z_FAIRWAY + 0.04)) for p in D.hole.center]
    shots = [("tee", tee, stations[1])]
    if len(stations) > 2:
        mid = stations[1] if HOLE != "09" else Vector((*P.m(*D.hole.center[2]), pz + P.Z_FAIRWAY + 0.04))
        shots.append(("mid", mid, stations[2] if HOLE != "09" else stations[3]))
    for nm, ball, aim_at in shots:
        aim = (aim_at - ball)
        aim.z = 0
        aim.normalize()
        loc = ball - aim * (4.5 * P.YD) + Vector((0, 0, 2.4 * P.YD))
        look = ball + aim * (1.5 * P.YD)
        c = cam(f"CAM_LK_PHONE_{nm.upper()}", loc, look, fov_v=60.0)
        renders.append(render(c, os.path.join(OUT, f"render_phone_{nm}.png"), (900, 1600), 24))
    # low shoreline view: from the sea, 32 m out, 2.5 m up, looking obliquely at the loop point nearest to a target
    # target: the shore point nearest to a hole-specific spot whose outward normal has >= 60 m of open water in front
    tgt = {"08": P.m(25.0, 20.0), "09": P.m(20.0, -30.0), "10": P.m(*D.hole.pin)}[HOLE]
    best = None
    for Pl, _ in loops:
        Pc_, s_, Lp_, N_, _ = L._loop_columns(Pl, 1.0)
        free = np.ones(len(Pc_), bool)
        for dd in (10.0, 25.0, 40.0, 60.0):
            free &= L.P.signed_dist_m(D, Pc_[:, 0] + N_[:, 0] * dd, Pc_[:, 1] + N_[:, 1] * dd) > dd * 0.8
        dist = np.hypot(Pc_[:, 0] - tgt[0], Pc_[:, 1] - tgt[1]) + np.where(free, 0.0, 1e6)
        k_ = int(np.argmin(dist))
        if best is None or dist[k_] < best[0]:
            best = (dist[k_], Pc_, N_, k_)
    _, Pc, N, k = best
    n = Vector((N[k, 0], N[k, 1], 0.0))
    t = Vector((-N[k, 1], N[k, 0], 0.0))
    T = Vector((Pc[k, 0], Pc[k, 1], 2.8))
    c = cam("CAM_LK_SHORE", T + n * 42.0 + t * 18.0 + Vector((0, 0, 0.9)), T + Vector((0, 0, 0.3)), fov_v=45.0)
    renders.append(render(c, os.path.join(OUT, "render_shore.png"), (1400, 900), 24))
    print("[smoke] renders:", renders)

# ------------------------------------------------------------------------------------------------ 6. FBX re-import (destroys the scene)
for name, ok, detail in L.fbx_check(fbx, expect_ground):
    gate(name, ok, detail)


# ------------------------------------------------------------------------------------------------ 7. postcard_verify on base + look
def run_verify(blend, fbx_path, tag):
    exe = bpy.app.binary_path
    cmd = [exe, "-b", blend, "--python", os.path.join(HERE, "postcard_verify.py"), "--", f"hole{HOLE}_design", "--fbx", fbx_path,
           "--json", os.path.join(OUT, f"verify_{tag}.json")]
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=900)
    lines = [ln for ln in r.stdout.splitlines() if ln.startswith(("GATE:", "RESULT:"))]
    return lines


if VERIFY:
    base_lines = run_verify(os.path.join(BASE, f"hole_{HOLE}.blend"), os.path.join(BASE, f"hole_{HOLE}.fbx"), "base")
    look_lines = run_verify(smoke_blend, fbx, "look")
    base_res = {re.match(r"GATE: (\S+) (PASS|FAIL)", ln).group(1): ln.split()[2] for ln in base_lines if ln.startswith("GATE:")}
    look_res = {re.match(r"GATE: (\S+) (PASS|FAIL)", ln).group(1): ln.split()[2] for ln in look_lines if ln.startswith("GATE:")}
    for ln in look_lines:
        print("  verify(look):", ln[:260])
    superseded = ("MATERIALS@blend", "MATERIALS@fbx", "FBX_BUDGET@fbx")
    regress = [k for k, v in look_res.items() if v == "FAIL" and base_res.get(k) == "PASS" and k not in superseded]
    still = [k for k, v in look_res.items() if v == "FAIL" and k in superseded]
    gate("POSTCARD_VERIFY_NO_REGRESSION", not regress and len(look_res) >= 20,
         f"{len(look_res)} postcard_verify gates on the look build; base build failed {[k for k, v in base_res.items() if v == 'FAIL']}; "
         f"look build fails {[k for k, v in look_res.items() if v == 'FAIL']}; new failures outside the superseded "
         f"MATERIALS/FBX_BUDGET: {regress}")
    print(f"[smoke] superseded postcard_verify gates failing on the look build (LK_* names / look budget replace them): {still}")

fails = [g["name"] for g in GATES if not g["ok"]]
with open(os.path.join(OUT, f"gates_hole{HOLE}.json"), "w") as f:
    json.dump(dict(hole=HOLE, script=os.path.relpath(SCRIPT, REPO), build_s=round(t_build, 1), look_s=round(t_look, 1),
                   tris=st["tris"], skin_tris=skin_tris, surf_tris=surf_tris, water_tris=water_tris, renders=renders,
                   missing_textures=D.__dict__.get("look_missing", []), gates=GATES, fails=fails), f, indent=1)
print(f"[smoke] hole {HOLE}: build {t_build:.1f}s, look passes {t_look:.1f}s, {len(GATES)} gates, {len(fails)} failed {fails}")
sys.stdout.flush()
sys.exit(1 if fails else 0)
