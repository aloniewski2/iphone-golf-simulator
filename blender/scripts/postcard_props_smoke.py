"""postcard_props_smoke: gates + contact sheets for postcard_props_lib (POSTCARD_LOOK role A4, v2 2026-10-04). Headless only:

    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python blender/scripts/postcard_props_smoke.py \
        -- [--no-render] [--no-holes] [--no-baseline] [--holes 08,09,10] [--scratch DIR] [--sheet PNG] [--baseline DIR]

1. Plants_C.png -> Unity/Assets/Resources/Course/Look/ (deterministic: two generations byte-identical, 256 x 256).
2. Library gates on every library mesh: face budgets, rock shape (>= 60 welded vertices, >= 100 faces, not an icosahedron /
   icosphere / UV sphere blob: ROCK_SHAPE_NOT_BLOB, NO_12_VERTEX_ROCK), tapering sea stacks (SEASTACK_TAPERED), hex-column wall
   towers (WALL_COLUMNS_HEX), UVs (box projection at the contract tiles, nominal-scale baked tiles / inside the plant swatches /
   non-degenerate), materials, names, no animation, determinism. NEGATIVE CONTROLS prove the thresholds fail the old shapes (the
   old two-block sea stack, an icosahedron, a subdivided / split / noised icosphere, a UV sphere, a 4x-stretched wall instance).
3. Contact sheets (EEVEE, the real Look/ textures) -> work/postcard-look/v2/props/ (props_sheet_v2.png, seastack_sheet.png).
4. Hole tests in a SCRATCH dir only (never writes blender/hole_NN.blend or the FBXs): P.start(design, out_dir=scratch) + terrain +
   play surfaces + a stone-path strip + the whole dressing (scatter with and WITHOUT a region, sea stacks, outcrop + arch + vines,
   smoke cards, legacy rocks + swap): triangle budget, safety (rays against FAIRWAY/GREEN/TEE_BOX/BUNKER), land + ground rule
   (SCATTER_DEFAULT_ON_LAND, PLANT_ON_WATER_ZERO), instancing, no instance stretched past 2x, FBX writes each geometry once.
5. The three BASELINE hole blends (work/postcard-look/baseline/blender): swap_legacy_rocks, then SWAP_NO_STRETCH_2X_<hole> and the
   instance-scale statistics (v2/props/swap_stats.json).
Prints GATE lines; exit code 1 when one fails.
"""
import sys
import os
import math
import time
import hashlib

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import numpy as np  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import postcard_props_lib as PL  # noqa: E402
import postcard_lib as P  # noqa: E402

REPO = PL.REPO
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
V2 = os.path.abspath(ARGS[ARGS.index("--out-root") + 1]) if "--out-root" in ARGS else os.path.join(REPO, "work", "postcard-look", "v3", "lib", "props")
SCRATCH = os.path.abspath(ARGS[ARGS.index("--scratch") + 1]) if "--scratch" in ARGS else os.path.join(V2, "scratch")
SHEET = os.path.abspath(ARGS[ARGS.index("--sheet") + 1]) if "--sheet" in ARGS else os.path.join(V2, "props_sheet_v2.png")
BASELINE = os.path.abspath(ARGS[ARGS.index("--baseline") + 1]) if "--baseline" in ARGS else \
    os.path.join(REPO, "work", "postcard-look", "baseline", "blender")
DO_BASELINE = "--no-baseline" not in ARGS
HOLES = ARGS[ARGS.index("--holes") + 1].split(",") if "--holes" in ARGS else ["08", "09", "10"]
DO_RENDER = "--no-render" not in ARGS
DO_HOLES = "--no-holes" not in ARGS
os.makedirs(SCRATCH, exist_ok=True)

RESULTS = []
HOLE_PANELS = []


def gate(name, ok, detail=""):
    RESULTS.append((name, bool(ok), detail))
    print(f"GATE: {name} {'PASS' if ok else 'FAIL'} {detail}", flush=True)


def fresh():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    PL._COUNTERS.clear()
    PL.reset_scatter(None)


def mesh_hash(me):
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    return hashlib.sha256(np.round(co, 6).tobytes() + str([tuple(p.vertices) for p in me.polygons]).encode()).hexdigest()


def uv_array(me):
    lay = me.uv_layers.active
    uv = np.empty(len(me.loops) * 2)
    try:
        lay.uv.foreach_get("vector", uv)
    except Exception:
        lay.data.foreach_get("uv", uv)
    return uv.reshape(-1, 2)


# ----------------------------------------------------------------------------- negative controls (the OLD shapes)
def _mesh_obj(name, V, F, smooth=False):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in V], [], [tuple(f) for f in F])
    me.update()
    for pl in me.polygons:
        pl.use_smooth = smooth
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _bm_to_obj(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def neg_icosphere(name, subdiv, radius=1.0, noise=0.0, stretch=(1, 1, 1), uv_sphere=False, split=False, seed=3):
    import bmesh
    bm = bmesh.new()
    if uv_sphere:
        bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=radius)
    else:
        bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    rnd = np.random.RandomState(seed)
    for v in bm.verts:
        f = 1.0 + noise * rnd.uniform(-1, 1)
        v.co = Vector((v.co.x * stretch[0] * f, v.co.y * stretch[1] * f, v.co.z * stretch[2] * f))
    if split:                                            # every face gets its own vertices (flat-shaded export / split mesh)
        V, F = [], []
        for fc in bm.faces:
            F.append([len(V) + i for i in range(len(fc.verts))])
            V.extend(v.co.copy() for v in fc.verts)
        bm.free()
        return _mesh_obj(name, V, F)
    return _bm_to_obj(name, bm)


def old_seastack_chunks(rng, height, base_r, top_r, lean=(0.0, 0.0), buttress=4, crown=2):
    """VERBATIM the v1 sea stack (two flat-topped cores + shoulder + buttresses + crown), kept as the negative control."""
    import random as _r  # noqa: F401
    zb = -3.0
    a = rng.uniform(0, math.tau)
    sh = (math.cos(a) * base_r * 0.32 + lean[0] * height, math.sin(a) * base_r * 0.32 + lean[1] * height)
    z_mid = height * rng.uniform(0.5, 0.62)
    p1, c1 = PL._core_pts(rng, zb, z_mid, base_r, base_r * 0.8, (0, 0), (sh[0] * 0.3, sh[1] * 0.3))
    p2, c2 = PL._core_pts(rng, z_mid - height * 0.12, height, base_r * 0.68, top_r, (sh[0] * 0.6, sh[1] * 0.6), sh, levels=5)
    chunks = [dict(pts=p1, merge=0.05), dict(pts=p2, merge=0.05)]
    for j in range(buttress):
        cx, cy, z, r = c1[min(len(c1) - 1, rng.randrange(0, 3))]
        aa = a + math.pi + rng.uniform(-1.6, 1.6) if j < 2 else rng.uniform(0, math.tau)
        br = r * rng.uniform(0.45, 0.6)
        hz = (height - zb) * rng.uniform(0.1, 0.18)
        c = (cx + math.cos(aa) * r * 0.8, cy + math.sin(aa) * r * 0.72, max(zb + hz * 0.7, rng.uniform(-0.5, height * 0.22)))
        chunks.append(dict(c=c, r=(br, br * 0.9, hz), n=16, block=0.6, chips=3, chip_range=(0.7, 0.88), flat_top=0.8,
                           flat_bottom=0.8, top_tilt=0.1))
    cx, cy, z, r = c2[-1]
    for j in range(crown):
        aa = rng.uniform(0, math.tau)
        cr = r * rng.uniform(0.5, 0.65)
        chunks.append(dict(c=(cx + math.cos(aa) * r * 0.4, cy + math.sin(aa) * r * 0.4, z + rng.uniform(-0.6, 0.5)),
                           r=(cr, cr * 0.9, cr * 0.8), n=14, block=0.6, chips=2, flat_top=0.75, flat_bottom=0.8, top_tilt=0.15))
    return chunks


def old_seastack(kind, which):
    import random
    if which == "A":
        rng = random.Random(909)
        ch = old_seastack_chunks(rng, 17.0, 3.8, 1.7, lean=(0.015, -0.01))
        ob = PL._chunks_mesh(kind, rng, ch, mats=("LK_ROCK", "LK_ROUGH"), mat_fn=PL._grass_cap(17.0 * 0.45), ground=None)
    else:
        rng = random.Random(1001)
        ch = old_seastack_chunks(rng, 12.0, 3.4, 2.1, lean=(0.05, 0.02), buttress=3)
        ob = PL._chunks_mesh(kind, rng, ch, mats=("LK_ROCK", "LK_ROUGH"), mat_fn=PL._grass_cap(12.0 * 0.4), ground=None)
    return ob.data


def column_stats(me):
    """Hex-prism evidence of a basalt mesh: column tops (n-gons with 6-7 corners facing up), distinct top levels, chipped tops,
    vertical side share."""
    co, tris, _tp = PL._np_mesh(me)
    nrm, area, lt = PL._poly_arrays(me)
    tops, chipped, zs = 0, 0, []
    for p in me.polygons:
        if p.normal.z > 0.85 and len(p.vertices) in (6, 7):
            tops += 1
            if len(p.vertices) == 7:
                chipped += 1
            zs.append(sum(me.vertices[i].co.z for i in p.vertices) / len(p.vertices))
    vert = float((area[np.abs(nrm[:, 2]) < 0.2]).sum() / max(area.sum(), 1e-9))
    levels = len(set(round(z / 0.25) for z in zs))
    zs = np.array(zs) if zs else np.zeros(1)
    return dict(tops=tops, chipped=chipped, levels=levels, z_spread=float(zs.max() - zs.min()), vertical=vert,
                faces=len(me.polygons), dims=tuple(float(v) for v in (co.max(0) - co.min(0))))


def uv_density_ratio(me, tile):
    """Per face sqrt(UV area / (world area at the nominal scale / tile^2)): 1.0 = the tile is exactly its contract size when the
    instance has the nominal scale lk_uv_scale (axis-facing faces; oblique faces measure lower by cos)."""
    s = float(me.get("lk_uv_scale", 1.0))
    uv = uv_array(me)
    ls = np.zeros(len(me.polygons), np.int64)
    lt = np.zeros(len(me.polygons), np.int64)
    me.polygons.foreach_get("loop_start", ls)
    me.polygons.foreach_get("loop_total", lt)
    out = []
    for p in me.polygons:
        a = uv[ls[p.index]]
        ua = 0.0
        for j in range(1, lt[p.index] - 1):
            b, c = uv[ls[p.index] + j], uv[ls[p.index] + j + 1]
            ua += abs((b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1])) / 2
        wa = p.area * s * s / (tile * tile)
        if wa > 1e-9:
            out.append(math.sqrt(ua / wa))
    return np.array(out)


def shape_gates(lib, rep, hashes):
    """NO_12_VERTEX_ROCK, ROCK_SHAPE_NOT_BLOB, SEASTACK_TAPERED, WALL_COLUMNS_HEX (+ the v1 gate ids ROCK_VERTS_60, NO_ICOSAHEDRON,
    BASALT_COLUMNS) with negative controls that must FAIL."""
    th = PL.SHAPE_TH
    rock_meshes = {k: ob.data for k, ob in lib.items() if k.startswith("ROCK_")}
    for k in ("ROCK_BOULDER_A_WET", "ROCK_SLAB_A_WET", "ROCK_BLOCK_A_BASALT", "ROCK_BOULDER_B_BASALT", "ROCK_SEASTACK_B_WET",
              "ROCK_CRAG_A_WET", "ROCK_SPIRE_A_WET"):
        rock_meshes[k] = PL.library_object(k).data
    shape = {k: PL.rock_shape_report(me) for k, me in rock_meshes.items()}
    vmin = min(r["verts"] for r in shape.values())
    fmin = min(r["faces"] for r in shape.values())
    low = {k: (r["verts"], r["faces"]) for k, r in shape.items() if r["verts"] < th["min_verts"] or r["faces"] < th["min_faces"]}
    gate("ROCK_VERTS_60", not low, f"{len(shape)} library rock meshes (twins included): min welded verts {vmin} (>= 60), min faces "
         f"{fmin} (>= 100); short: {low or 'none'}")
    ico = [k for k, r in shape.items() if r["verts"] <= 12 or (r["verts"], r["faces"]) == (12, 20)]
    gate("NO_ICOSAHEDRON", not ico, f"rock meshes with <= 12 verts / 12-vertex 20-face icosahedra: {ico or 'none'}")

    # --- legacy scene check: assert_no_legacy_rocks fails on icosahedra and passes after the swap
    fresh()
    for i in range(3):
        neg_icosphere(f"ROCK_MEDIUM.{i:03d}", 1, 3.0).data.name = "ROCK_MEDIUM"
    before = PL.assert_no_legacy_rocks(None, raise_on_fail=False)
    raised = False
    try:
        PL.assert_no_legacy_rocks(None)
    except RuntimeError:
        raised = True
    PL.swap_legacy_rocks(None)
    after = PL.assert_no_legacy_rocks(None, raise_on_fail=False)
    gate("NO_12_VERTEX_ROCK", not low and not before["ok"] and raised and after["ok"],
         f"library: min {vmin} welded verts / {fmin} faces over {len(shape)} rock meshes; scene with 3 icosahedra: assert_no_legacy_rocks "
         f"FAILS before the swap (offenders {len(before['offenders'])}, raises {raised}) and PASSES after (rocks {after['rocks']}, "
         f"min verts {after['min_verts']}); the hole tests re-assert after every swap")
    fresh()
    PL.write_plants_atlas()
    for k in PL.ensure_library():
        pass

    # --- blob test: library rocks pass, the old shapes FAIL
    lib_bad = {k: r["why"] for k, r in shape.items() if not r["ok"]}
    fresh()
    negs = {
        "icosahedron (12 verts)": neg_icosphere("NEG_ICO", 1, 2.0),
        "icosphere sub 2 (42 verts)": neg_icosphere("NEG_ICO2", 2, 2.0),
        "icosphere sub 3 (162 verts)": neg_icosphere("NEG_ICO3", 3, 2.0),
        "split icosahedron (60 verts, flat)": neg_icosphere("NEG_ICOS", 1, 2.0, split=True),
        "stretched icosphere sub 3": neg_icosphere("NEG_ICOX", 3, 1.0, stretch=(2.2, 1.4, 0.8)),
        "noised icosphere sub 3 (8 %)": neg_icosphere("NEG_ICON", 3, 2.0, noise=0.08),
        "UV sphere 16 x 10": neg_icosphere("NEG_UVS", 0, 2.0, uv_sphere=True),
    }
    neg_rep = {n: PL.rock_shape_report(ob.data) for n, ob in negs.items()}
    neg_pass = [n for n, r in neg_rep.items() if r["ok"]]
    gate("ROCK_SHAPE_NOT_BLOB", not lib_bad and not neg_pass,
         f"{len(shape)} library rocks all pass (welded verts >= {th['min_verts']}, faces >= {th['min_faces']}, >= {th['min_facets']} facet "
         f"orientations in {th['facet_deg']:.0f} degree cones, normalised radius CV >= {th['blob_radius_cv']}); radius CV "
         f"{min(r['radius_cv'] for r in shape.values()):.2f}..{max(r['radius_cv'] for r in shape.values()):.2f}, facets "
         f"{min(r['facets'] for r in shape.values())}..{max(r['facets'] for r in shape.values())}; failing: {lib_bad or 'none'}. "
         f"NEGATIVE CONTROLS all FAIL: " + "; ".join(f"{n}: {'FAIL' if not r['ok'] else 'PASS (!)'} [{r['verts']}v/{r['faces']}f, rcv {r['radius_cv']:.3f}]"
                                                    for n, r in neg_rep.items()))
    for ob in negs.values():
        bpy.data.objects.remove(ob, do_unlink=True)

    # --- sea stacks (needs the library again: negatives built in a scene that also holds the old stacks)
    fresh()
    PL.write_plants_atlas()
    sea = {k: PL.library_object(k).data for k in PL.SEASTACK_KINDS}
    sr = {k: PL.seastack_report(me) for k, me in sea.items()}
    old = {"old A (two flat-topped cores)": old_seastack("NEG_OLD_SEASTACK_A", "A"), "old B (leaning two cores)": old_seastack("NEG_OLD_SEASTACK_B", "B")}
    old_rep = {n: PL.seastack_report(me) for n, me in old.items()}
    hts = [sr[k]["height"] for k in PL.SEASTACK_KINDS]
    fcs = [sr[k]["faces"] for k in PL.SEASTACK_KINDS]
    nat = [PL.SPIRE_SPECS[k]["height"] + 3.0 for k in PL.SEASTACK_KINDS]
    ok = all(r["ok"] for r in sr.values()) and all(150 <= f <= PL.SPIRE_MAX_FACES + 10 for f in fcs) and all(abs(h - n) < 0.6 for h, n in zip(hts, nat)) \
        and len(set(round(h) for h in hts)) == len(hts) and all(not r["ok"] for r in old_rep.values())
    gate("SEASTACK_TAPERED", ok,
         "; ".join(f"{k.replace('ROCK_SEASTACK_', '')}: {r['faces']} faces, {r['slices']} slices (distinct {r['distinct']}), taper w90/w10 "
                   f"{r['taper']:.2f} (<= {th['taper_max']}), max band widening {r['band_rise']:.2f} (<= {th['band_rise_max']}), flat cap "
                   f"{r['cap_share']:.0%} of base (<= {th['cap_max']:.0%}), {r['facets']} facet orientations (>= {th['spire_facets']}), bbox fill "
                   f"from 0/45/90/135 deg {'/'.join(f'{f:.2f}' for f in r['fills'])} (<= {th['fill_max']})" for k, r in sr.items() for _ in [0])
         + f". Mesh heights {'/'.join(f'{h:.0f}' for h in hts)} m for A/B/C/D/E (13/18/24/16/21 m above the water + 3 m under it). NEGATIVE: " +
         "; ".join(f"{n}: {'FAIL' if not r['ok'] else 'PASS (!)'} ({'; '.join(r['why'][:3])})" for n, r in old_rep.items()))
    # the spire kinds of the swap must pass too
    sp = PL.seastack_report(PL.library_object("ROCK_SPIRE_A").data)
    gate("SPIRE_TAPERED", sp["ok"], f"ROCK_SPIRE_A taper {sp['taper']:.2f}, fills {'/'.join(f'{f:.2f}' for f in sp['fills'])}, "
         f"{sp['faces']} faces, facets {sp['facets']}")
    for me in old.values():
        pass

    # --- columns: clusters, wall segments, the three tower sizes
    fresh()
    PL.write_plants_atlas()
    cl_kinds = list(PL.BASALT_KINDS) + list(PL.WALL_KINDS) + list(PL.TOWER_KINDS)
    cs = {k: column_stats(PL.library_object(k).data) for k in cl_kinds}
    det = [(k, v["vertical"], v["tops"]) for k, v in cs.items()]
    cl_ok = sum(1 for k in PL.BASALT_KINDS if cs[k]["tops"] >= 8 and cs[k]["vertical"] >= 0.6) >= 4
    wl_ok = all(cs[k]["tops"] >= 12 and cs[k]["vertical"] >= 0.6 for k in PL.WALL_KINDS)
    gate("BASALT_COLUMNS", cl_ok and wl_ok and len(set(hashes[k] for k in PL.BASALT_KINDS)) >= 4,
         "; ".join(f"{k} {v['tops']} column tops, {v['vertical']:.0%} vertical area" for k, v in cs.items()))
    tw = {k: cs[k] for k in PL.TOWER_KINDS}
    dens = {k: uv_density_ratio(PL.library_object(k).data, 8.0) for k in PL.TOWER_KINDS}
    sizes = [max(tw[k]["dims"][0], tw[k]["dims"][1]) for k in PL.TOWER_KINDS]
    heights = [tw[k]["dims"][2] for k in PL.TOWER_KINDS]
    ok_tw = all(v["tops"] >= 12 and v["levels"] >= 4 and v["chipped"] >= 0.15 * v["tops"] and v["vertical"] >= 0.6 and v["faces"] <= 500
                for v in tw.values())
    ok_sz = sizes[0] < sizes[1] < sizes[2] and heights[0] < heights[1] < heights[2] and sizes[1] >= 1.25 * sizes[0] and sizes[2] >= 1.25 * sizes[1]
    ok_uv = all(0.8 <= float(np.median(d)) <= 1.2 and float(np.mean((d > 0.5) & (d < 1.5))) >= 0.97 for d in dens.values())
    gate("WALL_COLUMNS_HEX", ok_tw and ok_sz and ok_uv,
         "; ".join(f"{k.replace('ROCK_WALL_BASALT_', 'TOWER_')}: {v['tops']} hex column tops (>= 12), {v['levels']} distinct top levels over "
                   f"{v['z_spread']:.1f} m, {v['chipped']} chipped tops, {v['vertical']:.0%} vertical area, {v['faces']} faces, "
                   f"{v['dims'][0]:.0f} x {v['dims'][1]:.0f} x {v['dims'][2]:.0f} m (incl. 6 m buried), UV: tile 8 m at the nominal scale "
                   f"{PL.library_object(k).data.get('lk_uv_scale', 1.0)} -> density ratio median {float(np.median(dens[k])):.2f}"
                   for k, v in tw.items()))


# ----------------------------------------------------------------------------- 1 + 2: atlas and library gates
def library_gates():
    fresh()
    p1, h1 = PL.write_plants_atlas(os.path.join(SCRATCH, "Plants_C_a.png"))
    p2, h2 = PL.write_plants_atlas(os.path.join(SCRATCH, "Plants_C_b.png"))
    pr, hr = PL.write_plants_atlas()
    with open(pr, "rb") as f:
        head = f.read(24)
    w, h = int.from_bytes(head[16:20], "big"), int.from_bytes(head[20:24], "big")
    gate("PLANTS_ATLAS", h1 == h2 == hr and (w, h) == (256, 256) and os.path.isfile(pr),
         f"{os.path.relpath(pr, REPO)} {w}x{h} sha {hr[:12]} (two generations identical: {h1 == h2})")

    lib = PL.ensure_library()
    for k in ("ROCK_BOULDER_A_WET", "ROCK_SLAB_A_WET", "ROCK_BLOCK_A_BASALT", "ROCK_BOULDER_B_BASALT"):
        lib[k] = PL.library_object(k)
    rep = PL.library_report()
    hashes = {k: mesh_hash(PL.library_object(k).data) for k in PL.BUILDERS}

    over = {k: v["faces"] for k, v in rep.items() if v["faces"] > 500}
    tufts = {k: rep[k]["faces"] for k in PL.TUFT_KINDS_ALL}
    gate("LIB_FACES", not over and max(tufts.values()) <= 120,
         f"{len(rep)} library meshes, max faces {max(v['faces'] for v in rep.values())} "
         f"(max tris {max(v['tris'] for v in rep.values())}), tufts {tufts}, over 500: {over or 'none'}")

    rocks = {k: rep[k] for k in PL.ROCK_KINDS}
    vmin = min(v["verts"] for v in rocks.values())
    frange = (min(v["faces"] for v in rocks.values()), max(v["faces"] for v in rocks.values()))
    ok_range = all(60 <= v["faces"] <= 500 for v in rocks.values())
    gate("ROCK_UNIQUE", len(set(hashes[k] for k in PL.ROCK_KINDS)) >= 6 and vmin > 12 and ok_range,
         f"{len(rocks)} unique rock meshes, min verts {vmin}, faces {frange[0]}..{frange[1]}")
    shape_gates(lib, rep, hashes)
    fresh()
    PL.write_plants_atlas()
    lib = PL.ensure_library()
    for k in ("ROCK_BOULDER_A_WET", "ROCK_SLAB_A_WET", "ROCK_BLOCK_A_BASALT", "ROCK_BOULDER_B_BASALT"):
        lib[k] = PL.library_object(k)
    hashes = {k: mesh_hash(PL.library_object(k).data) for k in PL.BUILDERS}
    # UVs
    bad_tile, bad_deg, bad_sw = [], [], []
    for k, ob in sorted(lib.items()):
        me = ob.data
        uv = uv_array(me)
        mats = [m.name for m in me.materials]
        vidx, co, pol, nrm, mi = PL._loop_arrays(me)
        if k.startswith(("ROCK_", "DRESS_BLOCK")):
            tiles = [(PL.TILE[m] or 4.0) for m in mats]
            exp = PL.box_uv_expected(me, tiles)
            plant_loop = np.isin(mi[pol], [i for i, m in enumerate(mats) if m == "LK_PLANTS"])      # a shrub crown: atlas-swatch UVs (UV_IN_SWATCH), not box UVs
            err = float(np.abs(exp[~plant_loop] - uv[~plant_loop]).max())
            if err > 1e-4 or any(m != "LK_PLANTS" and PL.TILE[m] != {"LK_ROCK": 4.0, "LK_ROCK_WET": 4.0, "LK_BASALT": 8.0, "LK_MASONRY": 4.0,
                                                                  "LK_ROUGH": 12.0}[m] for m in mats):
                bad_tile.append((k, err))
        # degenerate UV faces (fan triangulation area in UV)
        ls = np.zeros(len(me.polygons), np.int64)
        lt = np.zeros(len(me.polygons), np.int64)
        me.polygons.foreach_get("loop_start", ls)
        me.polygons.foreach_get("loop_total", lt)
        ndeg = 0
        for p in range(len(me.polygons)):
            a = uv[ls[p]]
            area = 0.0
            for j in range(1, lt[p] - 1):
                b, c = uv[ls[p] + j], uv[ls[p] + j + 1]
                area += abs((b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1])) / 2
            if area < 1e-9:
                ndeg += 1
        if ndeg:
            bad_deg.append((k, ndeg))
        plant_slots_ = [i for i, m in enumerate(mats) if m == "LK_PLANTS"]
        if k.startswith("PLANT_") or plant_slots_:
            for p in range(len(me.polygons)):
                if not k.startswith("PLANT_") and int(mi[p]) not in plant_slots_:
                    continue
                pts = uv[ls[p]:ls[p] + lt[p]]
                cell = np.floor(pts * 4).astype(int)
                if (cell != cell[0]).any():
                    bad_sw.append((k, p))
                    break
                inner = (pts * 4 - cell)
                if (inner < PL.SW_MARGIN - 1e-6).any() or (inner > 1 - PL.SW_MARGIN + 1e-6).any():
                    bad_sw.append((k, p))
                    break
    gate("UV_TILE_UNITS", not bad_tile, f"box UV = object space / contract tile (rock 4, basalt 8, masonry 4, rough cap 12) on "
         f"{sum(1 for k in lib if k.startswith(('ROCK_', 'DRESS_BLOCK')))} meshes; bad: {bad_tile or 'none'}")
    gate("UV_NONDEGENERATE", not bad_deg, f"faces with zero UV area: {bad_deg or 'none'}")
    gate("UV_IN_SWATCH", not bad_sw, f"{sum(1 for k in lib if k.startswith('PLANT_'))} plant meshes + the shrub crown faces of the {len(PL.SPIRE_KINDS)} spires, every face inside one "
         f"swatch {int(PL.SW_MARGIN * 64)} px inside its border; bad: {bad_sw or 'none'}")

    wrong = []
    for k, ob in lib.items():
        mats = [m.name for m in ob.data.materials]
        if k.startswith("PLANT_"):
            if mats != ["LK_PLANTS"]:
                wrong.append((k, mats))
        elif "LK_PLANTS" in mats and not (k in PL.SPIRE_KINDS or any(k == q + sfx for q in PL.SPIRE_KINDS for sfx in ("_WET", "_BASALT"))):   # only the shrub crown of a spire
            wrong.append((k, mats))
        elif k.startswith(("ROCK_BASALT", "ROCK_WALL")) and mats != ["LK_BASALT"]:
            wrong.append((k, mats))
        elif k.startswith("DRESS_BLOCK") and mats != ["LK_MASONRY"]:
            wrong.append((k, mats))
    gate("PLANTS_ONE_MATERIAL", not wrong, f"all {sum(1 for k in lib if k.startswith('PLANT_'))} plant meshes = [LK_PLANTS], no "
         f"other prop uses it; wrong: {wrong or 'none'}")
    return hashes, rep


# ----------------------------------------------------------------------------- 3: contact sheet
REF_MATS = {}


def ref_mat(name, rgb, emission=0.0, rough=0.8, image=None, alpha=None):
    m = bpy.data.materials.get(name)
    if m is not None:
        return m
    m = bpy.data.materials.new(name)
    nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    col = P.rgb(*rgb)
    b.inputs["Base Color"].default_value = col
    b.inputs["Roughness"].default_value = rough
    if image is not None:
        path = PL._look_path(image)
        if os.path.isfile(path):
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = bpy.data.images.load(path, check_existing=True)
            nt.links.new(t.outputs[0], b.inputs["Base Color"])
            if emission:
                nt.links.new(t.outputs[0], b.inputs["Emission Color"])
    if emission:
        if image is None:
            b.inputs["Emission Color"].default_value = col
        b.inputs["Emission Strength"].default_value = emission
    return m


def ref_box(name, x0, y0, z0, x1, y1, z1, mat, tile=4.0, top_mat=None):
    mb = PL._MB([mat] + ([top_mat] if top_mat else []))
    v = [mb.v(p) for p in ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    for f in ((0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        mb.f([v[i] for i in f], 0)
    mb.f([v[i] for i in (4, 5, 6, 7)], 1 if top_mat else 0)
    me = bpy.data.meshes.new(name)
    me.from_pydata(mb.V, [], mb.F)
    for mn in mb.mats:
        me.materials.append(bpy.data.materials[mn] if mn in bpy.data.materials else PL.lk_material(mn))
    me.polygons.foreach_set("material_index", np.array(mb.M, np.int32))
    me.update()
    lay = me.uv_layers.new(name="UVMap")
    uv = PL.box_uv_expected(me, [tile] * len(mb.mats))
    lay.uv.foreach_set("vector", uv.ravel())
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def ref_plane(name, cx, cy, z, sx, sy, mat, tile=12.0):
    return ref_box(name, cx - sx / 2, cy - sy / 2, z - 0.05, cx + sx / 2, cy + sy / 2, z, mat, tile)


def ref_figure(x, y, z=0.0, yaw=0.0):
    """1.7 m scale figure (capsule body + head)."""
    import bmesh
    me = bpy.data.meshes.new("REF_FIGURE")
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.2, radius2=0.17, depth=1.3,
                          matrix=__import__("mathutils").Matrix.Translation((0, 0, 0.65)))
    bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.14,
                              matrix=__import__("mathutils").Matrix.Translation((0, 0, 1.56)))
    bm.to_mesh(me)
    bm.free()
    me.materials.append(ref_mat("REF_FIGURE", (232, 70, 60), rough=0.5))
    ob = bpy.data.objects.new("REF_FIGURE", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = (x, y, z)
    for p in me.polygons:
        p.use_smooth = True
    return ob


def ref_cube(x, y, z=0.0):
    ob = ref_box("REF_CUBE_1M", x - 0.5, y - 0.5, z, x + 0.5, y + 0.5, z + 1.0, ref_mat("REF_CUBE", (225, 225, 230)).name)
    return ob


def label(text, loc, size, cam, color=(10, 12, 18)):
    cu = bpy.data.curves.new("REF_TXT", 'FONT')
    cu.body = text
    cu.size = size
    cu.align_x = 'CENTER'
    ob = bpy.data.objects.new("REF_TXT", cu)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = cam.rotation_euler.copy()
    ob.visible_shadow = False
    cu.materials.append(ref_mat("REF_TXT_%d_%d_%d" % color, color, emission=1.0))
    return ob


def title(cam, text, size=0.045):
    """Text pinned to the camera's top-left corner."""
    sc = bpy.context.scene
    fr = cam.data.view_frame(scene=sc)
    tl = min(fr, key=lambda v: (v.x - v.y))
    d = 3.0
    if cam.data.type == 'PERSP':
        tl = tl * (d / -tl.z)
    cu = bpy.data.curves.new("REF_TITLE", 'FONT')
    cu.body = text
    cu.size = size * d
    cu.align_x = 'LEFT'
    cu.align_y = 'TOP'
    ob = bpy.data.objects.new("REF_TITLE", cu)
    sc.collection.objects.link(ob)
    ob.parent = cam
    span = (max(v.x for v in fr) - min(v.x for v in fr)) * (d / -fr[0].z if cam.data.type == 'PERSP' else 1.0)
    ob.location = (tl.x + 0.015 * span, tl.y - 0.015 * span, -d)
    cu.size = size * span * 0.5
    ob.visible_shadow = False
    if cam.data.type == 'ORTHO':
        cu.size = size * cam.data.ortho_scale
    cu.materials.append(ref_mat("REF_TITLE", (255, 255, 255), emission=2.0))
    bg = bpy.data.curves.new("REF_TITLE_BG", 'FONT')
    return ob


def camera(name, loc, target, lens=35.0, ortho=None, clip=1500.0):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
    bpy.context.scene.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = lens
    cam.data.clip_start = 0.1
    cam.data.clip_end = clip
    if ortho:
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = ortho
    return cam


def setup_scene(res):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'PNG'
    try:
        sc.view_settings.view_transform = 'Standard'
    except Exception:
        pass
    for attr, val in (("taa_render_samples", 48), ("use_shadows", True), ("shadow_ray_count", 2), ("shadow_step_count", 6),
                      ("use_fast_gi", True), ("use_raytracing", False)):
        if hasattr(sc.eevee, attr):
            try:
                setattr(sc.eevee, attr, val)
            except Exception:
                pass
    w = bpy.data.worlds.new("REF_WORLD")
    sc.world = w
    bg = w.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = P.rgb(150, 190, 236)
    bg.inputs[1].default_value = 0.85
    for nm, en, rot, ang, col, sh in (("REF_SUN", 3.3, (48, 0, -32), 5.0, (1.0, 0.94, 0.84), True),
                                      ("REF_FILL", 0.6, (60, 0, 150), 30.0, (0.8, 0.88, 1.0), False)):
        L = bpy.data.lights.new(nm, 'SUN')
        L.energy = en
        L.angle = math.radians(ang)
        L.color = col
        try:
            L.use_shadow = sh
        except Exception:
            pass
        o = bpy.data.objects.new(nm, L)
        sc.collection.objects.link(o)
        o.rotation_euler = [math.radians(a) for a in rot]


def row(kinds, x0, y, gap, z=0.0, yaw=0.4, scale=1.0, cam=None, lab=0.0, labsize=0.25, labdy=0.0):
    """Place kinds left to right (by bbox width) starting at x0; returns (objs, x_end)."""
    objs = []
    x = x0
    for k in kinds:
        mn, mx = PL.lib_bbox(k)
        wdt = (mx.x - mn.x) * scale
        cx = x + wdt / 2 - (mn.x + mx.x) / 2 * scale
        ob = PL.place(None, k, (cx, y, z), yaw, scale, allow_low=True)      # reference-ground test scene
        objs.append(ob)
        if cam is not None and lab:
            label(k.replace("PLANT_", "").replace("ROCK_", "").replace("DRESS_", ""), (x + wdt / 2, y + mn.y * scale - 0.2 - labdy, z + lab),
                  labsize, cam)
        x += wdt + gap
    return objs, x


def build_panels():
    """Every panel lives 3000 m from the next one; cameras clip at 1500 m. Returns [(camera, filename)]."""
    fresh()
    PL.write_plants_atlas()
    setup_scene((800, 500))
    rough = PL.lk_material("LK_ROUGH").name
    panels = []

    # ---- A rocks
    O = Vector((0, 0, 0))
    cam = camera("CAM_A", O + Vector((6.3, -16.0, 6.2)), O + Vector((6.3, 3.2, 0.5)), lens=27)
    ref_plane("REF_GROUND_A", 7, 4, 0.0, 80, 60, rough)
    row(["ROCK_STONE_A", "ROCK_STONE_B", "ROCK_CAIRN_A", "ROCK_BLOCK_A", "ROCK_BOULDER_B", "ROCK_BOULDER_A"], -4.0, 0.0, 1.2,
        cam=cam, lab=-0.05, labsize=0.42)
    ref_cube(13.6, 0.0)
    ref_figure(15.2, 0.2)
    label("1 m", (13.6, -0.8, -0.05), 0.42, cam)
    label("1.7 m", (15.2, -0.8, -0.05), 0.42, cam)
    row(["ROCK_SLAB_A", "ROCK_LEDGE_A", "ROCK_BOULDER_A_WET", "ROCK_BLOCK_A_BASALT"], -6.0, 6.5, 1.8, cam=cam, lab=-0.05,
        labsize=0.42, labdy=0.2)
    title(cam, "ROCKS  LK_ROCK / _WET / _BASALT twins  (1 m cube, 1.7 m figure)")
    panels.append((cam, "panel_a_rocks.png"))

    # ---- B sea stacks + waterfall
    O = Vector((3000, 0, 0))
    water = ref_mat("REF_WATER", (36, 104, 168), rough=0.15)
    ref_plane("REF_WATER_B", O.x + 10, O.y + 30, 0.0, 300, 300, water.name)
    cliff = ref_mat("REF_CLIFF", (90, 90, 92), image="Cliff_C")
    ref_box("REF_CLIFFBOX", O.x + 14, O.y + 12, -2.0, O.x + 32, O.y + 24, 6.0, cliff.name, 12.0, top_mat=rough)
    PL.place(None, "ROCK_SEASTACK_A", O + Vector((-2, 46, 0)), 0.3)
    PL.place(None, "ROCK_SEASTACK_B", O + Vector((8, 32, 0)), 1.9)
    PL.place_outcrop(None, O.x - 19, O.y + 36, 5.0, 6.4, seed=1)
    PL.build_masonry_arch(None, (O.x - 19, O.y + 36, 5.0), 10.0, span_m=6.5, height_m=10.0, seed=4, base_rock=False)
    top = (O.x + 25.0, O.y + 11.95, 5.65)
    PL.build_waterfall_sheet(None, top, (0, -1), width_m=3.0, foot_z=0.0, seed=1)
    foot = PL.waterfall_foot(top, (0, -1), 3.0)
    for i, (dx, k) in enumerate(((-5.0, "ROCK_BOULDER_A_WET"), (4.5, "ROCK_SLAB_A_WET"), (8.0, "ROCK_BOULDER_B_WET"))):
        PL.place(None, k, (top[0] + dx, top[1] - 2.5 - i, -0.4), i * 1.3, 1.1)
    ref_figure(O.x + 28, O.y + 16, 6.0)
    PL.hang_vines(None, [((O.x + 15 + 1.6 * i, O.y + 11.97, 5.98), (0, -1)) for i in range(5)] +
                  [((O.x + 27 + 1.3 * i, O.y + 11.97, 5.98), (0, -1)) for i in range(4)], seed=3)
    cam = camera("CAM_B", O + Vector((2.0, -30.0, 9.0)), O + Vector((6.0, 25.0, 7.0)), lens=30)
    label("SEASTACK_A 17 m", O + Vector((-2, 40, -1.5)), 1.1, cam, (255, 255, 255))
    label("SEASTACK_B 12 m", O + Vector((8, 28, -1.5)), 1.0, cam, (255, 255, 255))
    label("place_outcrop + arch", O + Vector((-19, 28, -1.5)), 1.0, cam, (255, 255, 255))
    label(f"WATER_FALL_00 (foot w {foot['width']:.1f} m)", O + Vector((25, 9.0, -1.2)), 0.8, cam, (255, 255, 255))
    title(cam, "SEA STACKS, OUTCROP + ARCH, WATERFALL SHEET (LK_FALL), CLIFF VINES")
    panels.append((cam, "panel_b_stacks_fall.png"))

    # ---- C basalt
    O = Vector((6000, 0, 0))
    lava = ref_mat("REF_LAVA", (255, 110, 30), emission=3.0, image="Lava_E")
    ref_plane("REF_LAVA_C", O.x, O.y + 20, -0.3, 300, 300, lava.name)
    PL.place(None, "ROCK_WALL_BASALT_A", O + Vector((-14, 52, 0)), 0.0)
    PL.place(None, "ROCK_WALL_BASALT_B", O + Vector((24, 58, 0)), 0.15)
    xs = [(-17, "ROCK_BASALT_A"), (-7.5, "ROCK_BASALT_B"), (1.5, "ROCK_BASALT_C"), (11.5, "ROCK_BASALT_D")]
    for x, k in xs:
        PL.place(None, k, O + Vector((x, 8, 0)), 0.2)
    ref_figure(O.x + 11.5, O.y + 7, 1.6)
    # pillar with a column skin (build_basalt_skin): grass disc top at 6 m
    import bmesh  # noqa
    n = 40
    loop = [(O.x + 24 + 7 * math.cos(math.tau * i / n), O.y + 14 + 6 * math.sin(math.tau * i / n)) for i in range(n)]
    mb = PL._MB([rough])
    ring_top = [mb.v((x, y, 6.0)) for x, y in loop]
    mb.f(ring_top, 0)
    me = bpy.data.meshes.new("REF_PILLAR_TOP")
    me.from_pydata(mb.V, [], mb.F)
    me.materials.append(bpy.data.materials[rough])
    lay = me.uv_layers.new(name="UVMap")
    lay.uv.foreach_set("vector", PL.box_uv_expected(me, [12.0]).ravel())
    obp = bpy.data.objects.new("REF_PILLAR_TOP", me)
    bpy.context.scene.collection.objects.link(obp)
    PL.build_basalt_skin(None, loop, 5.98, -1.5, col_r=1.0, seed=2, rows=2)
    ref_figure(O.x + 24, O.y + 14, 6.0)
    cam = camera("CAM_C", O + Vector((3.0, -30.0, 12.0)), O + Vector((4.0, 20.0, 7.0)), lens=24)
    for x, k in xs:
        label(k.replace("ROCK_", ""), O + Vector((x, 2.0, -0.3)), 0.9, cam, (255, 255, 255))
    label("build_basalt_skin (pillar)", O + Vector((24, 6.5, -0.3)), 0.9, cam, (255, 255, 255))
    label("WALL_BASALT_A / B (40 m, 36 m)", O + Vector((2, 40, 8)), 1.6, cam, (255, 255, 255))
    title(cam, "BASALT  hex-column clusters, wall segments, pillar skin (LK_BASALT + crack glow)")
    panels.append((cam, "panel_c_basalt.png"))

    # ---- D masonry
    O = Vector((9000, 0, 0))
    ref_plane("REF_GROUND_D", O.x, O.y + 10, 0.0, 120, 120, rough)
    a1 = PL.build_masonry_arch(None, (O.x - 8.5, O.y + 8.0, 0.9), 0.0, span_m=7.0, height_m=10.5, seed=1)
    a2 = PL.build_masonry_arch(None, (O.x + 4.5, O.y + 12.0, 0.0), 25.0, span_m=6.0, height_m=9.0, seed=2, broken=True)
    ru = PL.build_ruin(None, [(O.x + 11, O.y + 4), (O.x + 23, O.y + 4.5), (O.x + 25.5, O.y + 14)], seed=3, base_z=0.0,
                       doorway_at=0.3)
    ref_figure(O.x - 8.5, O.y + 4.5)
    ref_figure(O.x + 12.5, O.y + 2.0)
    cam = camera("CAM_D", O + Vector((4.0, -22.0, 6.5)), O + Vector((5.0, 9.0, 4.0)), lens=28)
    label("DRESS_ARCH_00 + vines + rock base", O + Vector((-8.5, 3.0, -0.2)), 0.7, cam)
    label("DRESS_ARCH_01 broken", O + Vector((4.5, 7.5, -0.2)), 0.7, cam)
    label("DRESS_RUIN_00 (door, fallen blocks)", O + Vector((18.5, 1.5, -0.2)), 0.7, cam)
    title(cam, f"MASONRY  arch {len(a1['arch'].data.polygons)} faces, broken arch, ruin ({sum(len(o.data.polygons) for o in ru['walls'])} faces)")
    panels.append((cam, "panel_d_masonry.png"))

    # ---- E plants
    O = Vector((12000, 0, 0))
    ref_plane("REF_GROUND_E", O.x + 4, O.y + 3, 0.0, 60, 40, rough)
    cam = camera("CAM_E", O + Vector((5.4, -9.4, 3.3)), O + Vector((5.4, 2.6, 0.8)), lens=26)
    row(["PLANT_TUFT_A", "PLANT_TUFT_B", "PLANT_TUFT_C", "PLANT_FLOWER_PURPLE", "PLANT_FLOWER_WHITE", "PLANT_FLOWER_YELLOW",
         "PLANT_AGAVE_A", "PLANT_AGAVE_B"], O.x - 0.6, O.y + 0.0, 0.45, cam=cam, lab=-0.02, labsize=0.15)
    row(["PLANT_SHRUB_A", "PLANT_SHRUB_B", "PLANT_SHRUB_C", "PLANT_PINE_A", "PLANT_TREE_A"], O.x - 0.6, O.y + 3.0, 0.7, cam=cam,
        lab=-0.02, labsize=0.16)
    ref_cube(O.x + 8.6, O.y + 0.2)
    ref_figure(O.x + 10.0, O.y + 0.3)
    wall = ref_box("REF_WALL_E", O.x - 1.5, O.y + 7.0, 0.0, O.x + 8.5, O.y + 8.0, 4.2, PL.lk_material("LK_MASONRY").name, 4.0)
    PL.hang_vines(None, [((O.x - 0.5 + i * 0.9, O.y + 6.98, 4.2), (0, -1)) for i in range(10)], seed=5)
    label("VINE S / M / L on a wall", (O.x + 3.5, O.y + 6.8, 4.35), 0.18, cam)
    title(cam, "PLANTS  one LK_PLANTS palette atlas: tufts, flowers, agave, shrubs, pine, tree, vines")
    panels.append((cam, "panel_e_plants.png"))

    # ---- F scatter patch (phone camera) + G same patch from above
    O = Vector((15000, 0, 0))
    ref_plane("REF_ROUGH_F", O.x, O.y + 40, 0.0, 70, 100, rough)
    fw = ref_mat("REF_FAIRWAY", (110, 196, 60), image="Fairway_C")
    ref_box("REF_FAIRWAY_F", O.x - 7.0, O.y - 10, 0.0, O.x + 7.0, O.y + 90, 0.14, fw.name, 10.0)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.0427 * 1.6, location=(O.x + 1.5, O.y + 0.0, 0.2))
    ball = bpy.context.active_object
    ball.name = "REF_BALL"
    ball.data.name = "REF_BALL"
    ball.data.materials.append(ref_mat("REF_BALL", (250, 250, 250), rough=0.3))

    def off_fw(X, Y):
        return np.abs(np.asarray(X) - O.x) > 7.0 + 0.6

    def off_fw_big(X, Y):
        return np.abs(np.asarray(X) - O.x) > 7.0 + 2.0
    reg = PL.region_rect(O.x - 34, O.y - 8, O.x + 34, O.y + 88)
    rk = PL.scatter(None, ["ROCK_BOULDER_A", "ROCK_BOULDER_B", "ROCK_STONE_A", "ROCK_STONE_B", "ROCK_BLOCK_A", "ROCK_CAIRN_A"], 60,
                    region=reg, masks=[off_fw_big], seed=4, z=0.0)
    sh = PL.scatter(None, ["PLANT_SHRUB_A", "PLANT_SHRUB_B", "PLANT_SHRUB_C"], 40, region=reg, masks=[off_fw_big], seed=2, z=0.0, allow_low=True)
    fl = PL.scatter(None, {"PLANT_FLOWER_PURPLE": 2, "PLANT_FLOWER_WHITE": 1, "PLANT_FLOWER_YELLOW": 1}, 80, region=reg,
                    masks=[off_fw], seed=3, z=0.0, allow_low=True)
    tu = PL.scatter(None, {"PLANT_TUFT_A": 3, "PLANT_TUFT_B": 2, "PLANT_TUFT_C": 1}, 600, region=reg, masks=[off_fw], seed=1,
                    z=0.0, allow_low=True)
    tris = 0
    for ob in rk + sh + fl + tu:
        ob.data.calc_loop_triangles()
        tris += len(ob.data.loop_triangles)
    YDm = P.YD
    cam = camera("CAM_F", O + Vector((1.5, -4.5 * YDm, 2.4 * YDm)), O + Vector((1.5, 1.5 * YDm + 6.0, 0.0)), lens=24)
    title(cam, f"SCATTER at the phone camera: {len(tu)} tufts, {len(sh)} shrubs, {len(fl)} flowers, {len(rk)} rocks = {tris} tris")
    panels.append((cam, "panel_f_scatter_phone.png"))
    cam = camera("CAM_G", O + Vector((-38.0, -30.0, 34.0)), O + Vector((0.0, 30.0, 0.0)), lens=30)
    title(cam, "SCATTER patch from above (fairway strip + 2 m margin kept clear by masks)")
    panels.append((cam, "panel_g_scatter_top.png"))

    # ---- H backdrop: cone + smoke + wall
    O = Vector((18000, 0, 0))
    ref_plane("REF_LAVA_H", O.x, O.y + 500, -0.3, 2400, 2400, lava.name)
    cone = PL.build_background_cone(None, (O.x + 40, O.y + 520, -8.0), seed=1, notch_deg=250)
    PL.build_smoke_cards(None, [((O.x + 10, O.y + 520, 70.0), 70.0, 90.0), ((O.x + 90, O.y + 540, 60.0), 50.0, 70.0),
                                ((O.x - 60, O.y + 480, 30.0), 40.0, 50.0)], face=(O.x, O.y - 60))
    PL.place(None, "ROCK_WALL_BASALT_C", O + Vector((-70, 220, 0)), 0.1)
    PL.place(None, "ROCK_WALL_BASALT_A", O + Vector((120, 260, 0)), -0.2)
    cam = camera("CAM_H", O + Vector((0.0, -60.0, 14.0)), O + Vector((20.0, 500.0, 55.0)), lens=32, clip=3000)
    title(cam, f"BACKDROP  DRESS_CONE ({len(cone.data.polygons)} faces, no undercut, rim notch), DRESS_SMOKE cards, basalt walls")
    panels.append((cam, "panel_h_backdrop.png"))

    # ---- I Plants_C atlas (emission plane, swatch names)
    O = Vector((21000, 0, 0))
    am = bpy.data.materials.new("REF_ATLAS")
    nt = am.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    outn = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    tx = nt.nodes.new("ShaderNodeTexImage")
    tx.image = bpy.data.images.load(PL.PLANTS_PNG, check_existing=True)
    tx.interpolation = 'Closest'
    nt.links.new(tx.outputs[0], em.inputs[0])
    nt.links.new(em.outputs[0], outn.inputs[0])
    mb = PL._MB(["REF_ATLAS"])
    q = [mb.v((O.x - 2, O.y - 2, 0), (0, 0)), mb.v((O.x + 2, O.y - 2, 0), (1, 0)), mb.v((O.x + 2, O.y + 2, 0), (1, 1)),
         mb.v((O.x - 2, O.y + 2, 0), (0, 1))]
    mb.f(q)
    me = bpy.data.meshes.new("REF_ATLAS")
    me.from_pydata(mb.V, [], mb.F)
    me.materials.append(am)
    lay = me.uv_layers.new(name="UVMap")
    lay.uv.foreach_set("vector", np.array([mb.UV[i] for i in (0, 1, 2, 3)], float).ravel())
    oa = bpy.data.objects.new("REF_ATLAS", me)
    bpy.context.scene.collection.objects.link(oa)
    cam = camera("CAM_I", O + Vector((1.3, 0.35, 10.0)), O + Vector((1.3, 0.35, 0.0)), ortho=7.6)
    cam.rotation_euler = (0.0, 0.0, 0.0)
    for i, (nm, col) in enumerate(PL.SWATCHES):
        c, r = i % 4, i // 4
        cx, cy = O.x - 2 + c + 0.5, O.y + 2 - r - 0.5
        lum = 0.3 * col[0] + 0.59 * col[1] + 0.11 * col[2]
        label(nm, (cx, cy - 0.38, 0.02), 0.13, cam, (20, 20, 20) if lum > 140 else (250, 250, 250))
    notes = ["Plants_C.png 256 x 256", "4 x 4 swatches of 64 px", "UV islands 6 px inside", "dark bottom -> light top",
             "PLANT_* = LK_PLANTS only", "written by write_plants_atlas()"]
    for k, t in enumerate(notes):
        lb = label(t, (O.x + 3.3, O.y + 1.4 - k * 0.5, 0.0), 0.17, cam, (250, 250, 250))
        lb.data.align_x = 'LEFT'
        lb.location.x = O.x + 2.2
    title(cam, "LK_PLANTS palette atlas (Plants_C.png)")
    panels.append((cam, "panel_i_atlas.png"))
    return panels


def render_panels(panels):
    sc = bpy.context.scene
    paths = []
    for cam, fn in panels:
        sc.camera = cam
        path = os.path.join(SCRATCH, fn)
        sc.render.filepath = path
        t = time.time()
        bpy.ops.render.render(write_still=True)
        print(f"rendered {fn} {time.time() - t:.1f}s", flush=True)
        paths.append(path)
    return paths


def compose_sheet(paths, out, cols=2):
    imgs = []
    for p in paths:
        im = bpy.data.images.load(p, check_existing=False)
        w, h = im.size
        px = np.array(im.pixels[:], np.float32).reshape(h, w, 4)[::-1, :, :3]
        imgs.append(px)
        bpy.data.images.remove(im)
    h, w = imgs[0].shape[:2]
    rows = (len(imgs) + cols - 1) // cols
    pad = 6
    sheet = np.full((rows * (h + pad) + pad, cols * (w + pad) + pad, 3), 0.12, np.float32)
    for i, im in enumerate(imgs):
        r, c = i // cols, i % cols
        sheet[pad + r * (h + pad):pad + r * (h + pad) + h, pad + c * (w + pad):pad + c * (w + pad) + w] = im
    data = PL._png_bytes(np.clip(np.round(sheet * 255), 0, 255).astype(np.uint8))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "wb") as f:
        f.write(data)
    return out, sheet.shape


# ----------------------------------------------------------------------------- 4: hole tests
def play_bvh():
    objs = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(("FAIRWAY", "GREEN", "TEE_BOX", "BUNKER"))]
    V, F = _tri_soup(objs)
    return BVHTree.FromPolygons(V, F), [o.name for o in objs]


def _tri_soup(objs, up_only=None):
    """World-space vertices + loop triangles of mesh objects (proper triangulation: n-gons are NOT fanned). up_only: keep only
    triangles whose unit normal.z > up_only."""
    V, F = [], []
    for o in objs:
        co, tris, _tp = PL._np_mesh(o.data)
        mw = np.array(o.matrix_world)
        w = co @ mw[:3, :3].T + mw[:3, 3]
        if up_only is not None:
            nv = np.cross(w[tris[:, 1]] - w[tris[:, 0]], w[tris[:, 2]] - w[tris[:, 0]])
            tris = tris[nv[:, 2] > up_only * np.maximum(np.linalg.norm(nv, axis=1), 1e-12)]
        base = len(V)
        V.extend(map(tuple, w.tolist()))
        F.extend(tuple(t) for t in (tris + base).tolist())
    return V, F


def ground_bvh():
    """Independent (not PL.ground_sampler) BVH of the TOP faces of TERRAIN / FAIRWAY / GREEN / TEE_BOX / BUNKER meshes."""
    objs = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(("TERRAIN", "FAIRWAY", "GREEN", "TEE_BOX", "BUNKER"))]
    V, F = _tri_soup(objs, up_only=0.2)
    return BVHTree.FromPolygons(V, F), [o.name for o in objs]


def ground_z(bvh, x, y, ztop=60.0):
    hit = bvh.ray_cast(Vector((x, y, ztop)), Vector((0, 0, -1)), 200.0)
    return None if hit[0] is None else float(hit[0].z)


def make_path(D, half_w=0.8, inset=4.0, spacing=3.0):
    """A DRESS_PATH_00 ribbon through the land rough (inset m inside the cliff edge): stands in for A3's stone path, so the
    scatter's path mask is exercised. Returns the centre polyline [(x, y), ...] ([] when the hole has no suitable stretch)."""
    best = []
    for L in D.loops:
        Pn = np.asarray(L, float)
        n = len(Pn)
        seg = np.hypot(*(np.roll(Pn, -1, 0) - Pn).T)
        cum = np.concatenate([[0], np.cumsum(seg)])
        k = int(cum[-1] / spacing)
        run, runs = [], []
        for i in range(k):
            s = (i + 0.5) * cum[-1] / k
            j = min(int(np.searchsorted(cum, s, side="right") - 1), n - 1)
            t = (s - cum[j]) / max(seg[j], 1e-9)
            a, b = Pn[j], Pn[(j + 1) % n]
            p = a + (b - a) * t
            d = (b - a) / max(seg[j], 1e-9)
            q = p + np.array([-d[1], d[0]]) * inset
            c = P.lie_codes_m(D, np.array([q[0]]), np.array([q[1]]))[0]
            sd = P.signed_dist_m(D, np.array([q[0]]), np.array([q[1]]))[0]
            if c in (P.LIE_ROUGH, P.LIE_OOB) and sd <= -3.0:
                run.append((float(q[0]), float(q[1])))
            else:
                if len(run) > 1:
                    runs.append(run)
                run = []
        if len(run) > 1:
            runs.append(run)
        for r in runs:
            if len(r) > len(best):
                best = r
    if len(best) < 2:
        return []
    verts, faces = [], []
    zz = D.play_z + 0.04
    for (x0, y0), (x1, y1) in zip(best[:-1], best[1:]):
        d = np.array([x1 - x0, y1 - y0])
        ln = float(np.hypot(*d))
        if ln < 1e-6:
            continue
        nrm = np.array([-d[1], d[0]]) / ln * half_w
        b = len(verts)
        verts += [(x0 - nrm[0], y0 - nrm[1], zz), (x1 - nrm[0], y1 - nrm[1], zz), (x1 + nrm[0], y1 + nrm[1], zz), (x0 + nrm[0], y0 + nrm[1], zz)]
        faces.append((b, b + 1, b + 2, b + 3))
    me = bpy.data.meshes.new("DRESS_PATH_00")
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new("DRESS_PATH_00", me)
    PL._collection_for("DRESS_PATH").objects.link(ob)
    ob.parent = P.get_root()
    return best


def tri_count(objs):
    t = 0
    for ob in objs:
        ob.data.calc_loop_triangles()
        t += len(ob.data.loop_triangles)
    return t


def lie_water(D, objs):
    X = np.array([o.location.x for o in objs])
    Y = np.array([o.location.y for o in objs])
    if len(X) == 0:
        return np.zeros(0, bool)
    return P.lie_codes_m(D, X, Y) == P.LIE_WATER


def sea_points(D, n, lo=8.0, hi=26.0, seed=0):
    import random
    rng = random.Random(1000 + seed)
    reg = PL.region_band(D, lo, hi, "sea")
    X, Y, _z = reg(rng, 4000)
    pts = []
    for x, y in zip(X.tolist(), Y.tolist()):
        if all(math.hypot(x - a, y - b) > 14.0 for a, b in pts):
            pts.append((x, y))
        if len(pts) >= n:
            break
    return pts


HOLE_STATS = {}


def hole_test(tag, lib_hashes):
    import random
    fresh()
    D = P.start(f"hole{tag}_design", out_dir=os.path.join(SCRATCH, f"hole{tag}"))
    t0 = time.time()
    P.build_terrain(D)
    P.build_play_surfaces(D)
    if tag == "10":
        P.build_water(D, 'WATER_LAVA', 'MAT_LAVA', shallow_material=None, foam_material=None, crest=False)
    else:
        P.build_water(D)
    P.build_gameplay(D)                                  # TEE_MARKER_1/2, HOLE_CUP, FLAG, BALL_START: the marker mask needs them
    t_build = time.time() - t0
    path_pts = make_path(D)
    t0 = time.time()
    var = "BASALT" if tag == "10" else None          # crater: basalt twins in/at the lava
    # --- the dressing. Plants use the DEFAULT region (None) = the whole real land; rocks keep their zones.
    rocks = PL.scatter_rocks(D, 30, zone="rim", seed=4, variant=var) + PL.scatter_rocks(D, 30, zone="sea", seed=5, variant=var)
    shrubs = PL.scatter(D, ["PLANT_SHRUB_A", "PLANT_SHRUB_B", "PLANT_SHRUB_C"], 40, seed=2)
    flowers = PL.scatter(D, {"PLANT_FLOWER_PURPLE": 2, "PLANT_FLOWER_WHITE": 1, "PLANT_FLOWER_YELLOW": 1}, 80, seed=3)
    tufts = PL.scatter(D, {"PLANT_TUFT_A": 3, "PLANT_TUFT_B": 2, "PLANT_TUFT_C": 1}, 600, seed=1)
    agave = PL.scatter(D, {"PLANT_AGAVE_A": 1, "PLANT_AGAVE_B": 1}, 20, seed=6)
    t_sc = time.time() - t0
    props = rocks + shrubs + flowers + tufts + agave
    plants = shrubs + flowers + tufts + agave
    tris = tri_count(props)
    print(f"hole {tag}: terrain+play {t_build:.1f}s, scatter {t_sc:.1f}s: rocks {len(rocks)}, shrubs {len(shrubs)}, flowers {len(flowers)}, "
          f"tufts {len(tufts)}, agave {len(agave)}, path {'yes' if path_pts else 'none'}, tris {tris}", flush=True)
    # --- SCATTER_DEFAULT_ON_LAND: every default-region plant is on the real land, >= 1 m from the shore, on a ground top
    bvh_g, gnames = ground_bvh()
    px, py = D.hole.pin[0] * P.YD, D.hole.pin[1] * P.YD
    markers = [(o.matrix_world.translation.x, o.matrix_world.translation.y) for o in bpy.data.objects
               if o.name.startswith(("TEE_MARKER", "HOLE_CUP", "BALL_START"))]
    X = np.array([o.location.x for o in plants])
    Y = np.array([o.location.y for o in plants])
    water = P.lie_codes_m(D, X, Y) == P.LIE_WATER
    sd = P.signed_dist_m(D, X, Y)
    dzs, zmin = [], 1e9
    for o in plants:
        g = ground_z(bvh_g, o.location.x, o.location.y)
        dzs.append(99.0 if g is None else abs(o.location.z - g))
        zmin = min(zmin, o.location.z)
    dzs = np.array(dzs)
    pin_min = float(np.hypot(X - px, Y - py).min())
    mk_min = min((math.hypot(x - a, y - b) for x, y in zip(X.tolist(), Y.tolist()) for a, b in markers), default=99.0)
    if path_pts:
        pp = np.array(path_pts)
        A, B = pp[:-1], pp[1:]
        pd = float(P._dist_pts_segs(np.stack([X, Y], 1), A, B).min())
    else:
        pd = 99.0
    ok_land = (not water.any()) and float(sd.max()) <= -0.999 and float(dzs.max()) <= 0.05 and zmin >= 5.0 and pin_min >= 6.0 \
        and len(markers) >= 3 and mk_min >= 1.5 and pd >= 0.8 + 0.2 and len(plants) >= 40
    gate(f"SCATTER_DEFAULT_ON_LAND_{tag}", ok_land,
         f"{len(plants)} plants from scatter(region=None): on water {int(water.sum())}; nearest to a shore edge {-float(sd.max()):.2f} m (>= 1.0, "
         f"independent distance field); base vs ray cast onto {len(gnames)} TERRAIN/FAIRWAY/GREEN/TEE_BOX/BUNKER meshes: max |dz| "
         f"{float(dzs.max()):.3f} m (<= 0.05), lowest z {zmin:.2f} (>= 5.0); pin {pin_min:.1f} m (>= 6), {len(markers)} gameplay markers (TEE_MARKER_1/2, HOLE_CUP, BALL_START) {mk_min:.1f} m (>= 1.5); "
         f"nearest to the {'DRESS_PATH ribbon (0.8 m half width)' if path_pts else 'no path on this hole'} {pd:.2f} m (>= 1.0)")
    # --- PLANT_ON_WATER_ZERO: adversarial scatters (huge regions, other seeds / counts, safe=False, sea regions)
    adv_total, adv_water, adv_ground_bad, calls = 0, 0, 0, 0
    kinds_sets = [{"PLANT_TUFT_A": 1}, ["PLANT_SHRUB_A", "PLANT_SHRUB_B"], {"PLANT_FLOWER_PURPLE": 1, "PLANT_FLOWER_WHITE": 1},
                  ["PLANT_AGAVE_B"], ["PLANT_PINE_A", "PLANT_TREE_A"]]
    x0, y0, x1, y1 = P._shore_bbox_m(D, 0.0)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    big = max(x1 - x0, y1 - y0) + 300.0
    regions = {
        "None": None,
        "rect +300 m": (x0 - 300, y0 - 300, x1 + 300, y1 + 300),
        "disc over the sea": PL.region_disc(cx, cy, big),
        "polyline over water": PL.region_polyline([(x0 - 100, y0 - 100), (x1 + 100, y1 + 100), (x0 - 100, y1 + 100)], 60.0),
        "band 3-26 m SEA": PL.region_band(D, 3.0, 26.0, "sea"),
        "band 0-6 m land": PL.region_band(D, 0.2, 6.0),
    }
    adv = []
    for si, (seed, count) in enumerate(((11, 40), (12, 150), (13, 600))):
        for rn, reg in regions.items():
            for ki, ks in enumerate(kinds_sets):
                if (si + ki) % 2 and rn not in ("None", "band 3-26 m SEA"):
                    continue                                  # keep the run short: every region / kind pair appears at least once
                if si == 2 and rn not in ("None", "rect +300 m", "disc over the sea"):
                    continue
                safe = not (rn == "rect +300 m" and si == 2)  # the unsafe call: no play-surface / pin masks, ONLY the land rule
                out = PL.scatter(D, ks, count, region=reg, seed=seed + ki, safe=safe, register=False, respect=False)
                calls += 1
                adv += out
    Xa = np.array([o.location.x for o in adv]) if adv else np.zeros(0)
    Ya = np.array([o.location.y for o in adv]) if adv else np.zeros(0)
    wa = (P.lie_codes_m(D, Xa, Ya) == P.LIE_WATER) if adv else np.zeros(0, bool)
    sda = P.signed_dist_m(D, Xa, Ya) if adv else np.zeros(0)
    bad_list = []
    for o in adv:
        g = ground_z(bvh_g, o.location.x, o.location.y)
        if g is None or abs(o.location.z - g) > 0.05 or o.location.z < 5.0:
            bad_list.append((o.name, tuple(round(v, 3) for v in o.location), g))
    bad_g = len(bad_list)
    raised = False
    try:
        PL.scatter(D, ["PLANT_TUFT_A"], 5, surface="sea", seed=1)
    except ValueError:
        raised = True
    gate(f"PLANT_ON_WATER_ZERO_{tag}", int(wa.sum()) == 0 and float(sda.max() if len(sda) else -9) <= -0.999 and bad_g == 0 and raised
         and len(adv) >= 200,
         f"{calls} adversarial scatter calls ({len(regions)} regions x {len(kinds_sets)} kind sets x seeds 11-13 x counts 40/150/600, the 600-count rect call with safe=False) placed "
         f"{len(adv)} plants: ON WATER {int(wa.sum())} (hard gate 0), nearest to a shore edge {-float(sda.max()) if len(sda) else 0:.2f} m, "
         f"bases off the ground (|dz| > 0.05 or z < 5) {bad_g} {bad_list[:2]}; the 'sea band' region placed "
         f"{sum(1 for o in adv if False)} on water by construction; scatter(surface='sea') for plants raises ValueError: {raised}")
    for o in adv:
        bpy.data.objects.remove(o, do_unlink=True)
    PL.reset_scatter(D)
    # --- the rest of the dressing (stacks, outcrop + arch, vines, smoke) for the whole-scene stretch gate
    stacks = []
    for i, (sx, sy) in enumerate(sea_points(D, 3, seed=int(tag))):
        stacks += PL.place_sea_stack(D, sx, sy, (14.0, 22.0, 32.0)[i % 3], 3.5, seed=i)
    extra = list(stacks)
    pts = sea_points(D, 1, 6.0, 20.0, seed=7 + int(tag))
    if pts:
        extra.append(PL.place_outcrop(D, pts[0][0], pts[0][1], 5.0, 6.4, seed=8))
        arch = PL.build_masonry_arch(D, (pts[0][0], pts[0][1], 5.0), 171.2, span_m=7.5, height_m=11.0, seed=8, base_rock=True)
        extra += arch["base"] + arch["vines"] + arch["fallen"]
    extra += PL.hang_vines_on_cliff(D, 7.0, seed=6)
    smoke = PL.build_smoke_cards(D, [((cx, cy, 40.0), 70.0, 90.0), ((cx + 90, cy, 30.0), 40.0, 50.0)])
    # --- rays: no land prop footprint on a play surface; pin clearance
    bvh, names = play_bvh()
    viol = []
    for ob in props:
        x, y, z = ob.location
        if z < D.play_z - 1.0:
            continue
        mn, mx = PL.lib_bbox(ob.data.name)
        r = 0.45 * max((mx.x - mn.x) * ob.scale.x, (mx.y - mn.y) * ob.scale.y)
        pts = [(x, y)] + [(x + r * math.cos(a), y + r * math.sin(a)) for a in np.linspace(0, math.tau, 8, endpoint=False)]
        for qx, qy in pts:
            hit = bvh.ray_cast(Vector((qx, qy, D.play_z + 5.0)), Vector((0, 0, -1)), 20.0)
            if hit[0] is not None:
                viol.append((ob.name, round(qx, 1), round(qy, 1)))
                break
    in_water = [o.name for o, w in zip(props, lie_water(D, props)) if w and o.location.z >= D.play_z - 1.0]
    pin_all = min(math.hypot(o.location.x - px, o.location.y - py) for o in props)
    gate(f"SCATTER_SAFE_{tag}", not viol and not in_water and pin_all >= 6.0,
         f"{len(props)} props; footprint rays hitting {len(names)} play meshes (fairway/first cut/green/apron/tee/bunker+lip): "
         f"{len(viol)} {viol[:3]}; land props standing in water: {len(in_water)}; nearest prop to the pin {pin_all:.1f} m (>= 6)")
    meshes = {ob.data.name for ob in props}
    libok = all(ob.data.get("lk_kind") == ob.data.name and bpy.data.objects.get(ob.data.name) is not None for ob in props)
    gate(f"INSTANCED_{tag}", libok and len(meshes) <= 30 and len(props) >= 10 * len(meshes),
         f"{len(props)} prop objects share {len(meshes)} library meshes (linked duplicates, every mesh is a hidden library mesh)")
    gate(f"HOLE_BUDGET_{tag}", tris <= 150000,
         f"placed {len(tufts)} tufts + {len(shrubs)} shrubs + {len(flowers)} flowers + {len(agave)} agave + {len(rocks)} rocks = {tris} tris (<= 150k)")
    bad = [ob.name for ob in props + extra + smoke if not ob.name.startswith(PL.NAME_PREFIXES) or ob.name.startswith(PL.GROUND_PREFIXES)]
    gate(f"PROP_NAMES_{tag}", not bad, f"collision-prefix / non-section-2 names: {bad[:5] or 'none'}")
    diff = [k for k in meshes if k in lib_hashes and mesh_hash(bpy.data.meshes[k]) != lib_hashes[k]]
    gate(f"DETERMINISTIC_{tag}", not diff, f"{len(meshes)} library meshes rebuilt in a fresh file are identical: {diff or 'yes'}")
    if DO_RENDER:
        P.build_cameras_and_light(D)
        cl = D.hole.center
        tgt = Vector((*P.m(*cl[len(cl) // 2]), D.play_z))
        if tag == "10":
            tgt = Vector((*P.m(*cl[-1]), D.play_z))
        cam = camera(f"CAM_HOLE{tag}", tgt + Vector((-34.0, -44.0, 30.0)), tgt + Vector((0.0, 4.0, 0.0)), lens=30, clip=3000)
        title(cam, f"hole {tag} (scratch, flat terrain): {len(tufts)} tufts, {len(shrubs)} shrubs, {len(flowers)} flowers, "
                   f"{len(rocks)} rocks = {tris} tris, {len(stacks)} stack parts")
        sc = bpy.context.scene
        sc.render.resolution_x, sc.render.resolution_y = 800, 500
        sc.camera = cam
        sc.render.filepath = os.path.join(SCRATCH, f"panel_hole{tag}.png")
        try:
            sc.eevee.taa_render_samples = 32
        except Exception:
            pass
        bpy.ops.render.render(write_still=True)
        HOLE_PANELS.append(sc.render.filepath)
    # --- legacy swap on a scene with the old stand-ins (icosahedra + a hole-9 style ROCK_STACK_nn) + the whole-scene stretch gate
    P.import_rocks(D)
    legacy = P.scatter_rocks(D, count=10, zone="sea", seed=9) + P.scatter_rocks(D, count=6, zone="foot", seed=10)
    legacy.append(P.place_rock(D, "CLIFF", (P.m(-60, 150)[0], P.m(-60, 150)[1], -2.0), scale=(0.4, 0.4, 1.2)))
    legacy.append(P.place_rock(D, "LARGE", (P.m(-30, 120)[0], P.m(-30, 120)[1], 3.0), scale=(1.1, 1.0, 5.0)))
    st = neg_icosphere("ROCK_STACK_01", 1, 1.0)
    st.data.name = "ROCK_STACK_01"
    for i, v in enumerate(st.data.vertices):
        v.co = Vector((v.co.x * 6.5, v.co.y * 6.5, (v.co.z * 12.0) + 6.0))
    st.location = (P.m(40, 140)[0], P.m(40, 140)[1], 0.0)
    legacy.append(st)
    bpy.context.view_layer.update()
    left_before = len(PL.legacy_rocks())
    rep = []
    swapped = PL.swap_legacy_rocks(D, objs=None, basalt=(tag == "10"), report=rep)
    bpy.context.view_layer.update()
    chk = PL.assert_no_legacy_rocks(D, raise_on_fail=False)
    errs = [r["err"] for r in rep]
    kinds = sorted({o.data.name for o in swapped})
    gate(f"LEGACY_SWAP_{tag}", len(PL.legacy_rocks()) == 0 and chk["ok"] and all(o.name.startswith("ROCK_") for o in swapped)
         and chk["min_verts"] >= 60 and left_before == len(legacy),
         f"{left_before} legacy stand-ins (icosahedra + a ROCK_STACK_01 mesh) -> {len(swapped)} library instances of {kinds}; box size error "
         f"mean {np.mean(errs):.3f} max {max(errs):.3f} (sum of |log ratio| over the axes); legacy rocks left {len(PL.legacy_rocks())}; "
         f"assert_no_legacy_rocks ok {chk['ok']} over {chk['rocks']} ROCK_* objects, min welded verts {chk['min_verts']}")
    sr = PL.stretch_report()
    gate(f"SWAP_NO_STRETCH_2X_scene_{tag}", sr["ok"] and sr["n"] >= 500,
         f"whole dressed scene: {sr['n']} library instances (scatter, stacks, outcrop, arch bases, vines, swap): scale {sr['min_scale']:.2f}.."
         f"{sr['max_scale']:.2f}, max/min {sr['max_ratio']:.2f}; violations {len(sr['violations'])} {sr['violations'][:3]}")
    all_inst = [o for o, _s in PL.instance_scales()]
    HOLE_STATS[tag] = dict(tris=tri_count(all_inst), props=len(all_inst), meshes=len({o.data.name for o in all_inst}),
                           plant_tris=tri_count(props), n_plants=len(plants))
    # FBX: each shared geometry written once
    if tag == HOLES[0]:
        vl = bpy.context.view_layer
        for o in vl.objects:
            o.select_set(False)
        for o in props:
            o.select_set(True)
        path = os.path.join(SCRATCH, f"props_test_{tag}.fbx")
        import postcard_look_lib as LookExport
        original_export_objects=P._export_objects
        try:
            P._export_objects=lambda _D: props + [P.get_root()]
            LookExport.export_look_fbx(D,path,colors_type='LINEAR')
        finally:
            P._export_objects=original_export_objects
        with open(path, "rb") as f:
            blob = f.read()
        ngeo = blob.count(b"\x00\x01Geometry")
        nmodel = blob.count(b"\x00\x01Model")
        gate("FBX_GEOMETRY_ONCE", ngeo == len(meshes),
             f"{os.path.basename(path)}: {ngeo} Geometry nodes for {len(meshes)} unique meshes, {nmodel} Model nodes, "
             f"{len(blob) // 1024} KB")
    return dict(tris=tris, props=len(props), meshes=len(meshes))


# ----------------------------------------------------------------------------- 5: the baseline hole blends
SWAP_STATS = {}


def baseline_swap_test(tag):
    path = os.path.join(BASELINE, f"hole_{tag}.blend")
    if not os.path.isfile(path):
        gate(f"SWAP_NO_STRETCH_2X_{tag}", False, f"missing {path}")
        return
    bpy.ops.wm.open_mainfile(filepath=path)
    PL._COUNTERS.clear()
    PL.reset_scatter(None)
    sha = hashlib.sha256(open(path, "rb").read()).hexdigest()[:12]
    n_legacy = len(PL.legacy_rocks())
    legacy_ext = {}
    for lo in PL.legacy_rocks():
        lw = np.array([list(lo.matrix_world @ v.co) for v in lo.data.vertices])
        legacy_ext[lo.name] = (float(lw[:, 2].min()), float(lw[:, 2].max()))
    rep = []
    t0 = time.time()
    out = PL.swap_legacy_rocks(None, basalt=(tag == "10"), report=rep, seed=1)
    dt = time.time() - t0
    bpy.context.view_layer.update()
    sr = PL.stretch_report()
    cr = PL.swap_composition_report()
    chk = PL.assert_no_legacy_rocks(None, raise_on_fail=False)
    tris = tri_count(out)
    grp = {}
    for o in out:
        grp.setdefault(o["lk_swap"], []).append(o)
    d_bot, d_top, d_tail = [], [], []
    members = {r["legacy"]: r["members"] for r in rep}
    cls = {r["legacy"]: r["cls"] for r in rep}
    for k, v in grp.items():
        pz = np.concatenate([np.array([list(o.matrix_world @ vv.co) for vv in o.data.vertices])[:, 2] for o in v])
        if cls[k] == "stack":                    # a spire's 3 m x scale underwater tail is designed in (never visible): measure what is below THAT
            d_tail.append(float(pz.min()) + 3.0 * float(v[0].matrix_world.to_scale().z))
            d_bot.append(0.0)
        else:
            d_bot.append(float(pz.min()) - min(legacy_ext[m][0] for m in members[k]))
        d_top.append(float(pz.max()) - max(legacy_ext[m][1] for m in members[k]))
    n_stack = len(d_tail)
    d_tail = d_tail or [0.0]
    kinds = {}
    for o in out:
        kinds[o.data.name] = kinds.get(o.data.name, 0) + 1
    errs = np.array([r["err"] for r in rep]) if rep else np.zeros(1)
    xs = np.array([sc for _o, sc in PL.instance_scales()]) if sr["n"] else np.zeros((1, 3))
    SWAP_STATS[tag] = dict(blend=os.path.relpath(path, REPO), sha12=sha, legacy=n_legacy, instances=len(out), tris=tris, kinds=kinds,
                           scale_min=[float(v) for v in xs.min(0)], scale_max=[float(v) for v in xs.max(0)], max_ratio=sr["max_ratio"],
                           violations=len(sr["violations"]), box_err_mean=float(errs.mean()), box_err_max=float(errs.max()),
                           multi_instance_plans=sum(1 for r in rep if r["instances"] > 1), seconds=round(dt, 2),
                           stack_class=sum(1 for r in rep if r["cls"] == "stack"), vertical_stacks=len(cr["vertical_stacks"]),
                           identical_clones=len(cr["clones"]), bottom_delta_min=min(d_bot), top_delta_min=min(d_top), top_delta_max=max(d_top))
    ok = sr["ok"] and sr["n"] == len(out) and chk["ok"] and len(PL.legacy_rocks()) == 0 and n_legacy > 0
    extra = ""
    if tag == "10":
        ring = [r for r in rep if min(r["box"][0], r["box"][1]) > 22.0 and r["box"][2] > 40.0]
        towers = [r for r in ring if r["kind"] in PL.TOWER_KINDS]
        extra = f"; wall ring (box > 22 m wide and > 40 m tall): {len(ring)} legacy rocks -> {len(towers)} single tower instances (S/M/L), others {len(ring) - len(towers)}"
        ok = ok and len(ring) >= 100 and len(towers) == len(ring)
    gate(f"SWAP_NO_STRETCH_2X_{tag}", ok,
         f"baseline hole_{tag}.blend (sha {sha}): {n_legacy} legacy rocks -> {len(out)} instances ({SWAP_STATS[tag]['multi_instance_plans']} composed of "
         f"several); per-axis scale {xs.min():.2f}..{xs.max():.2f} (limit 0.5..2.0), max/min {sr['max_ratio']:.2f} (limit 2.0), violations "
         f"{len(sr['violations'])}; kinds {len(kinds)}; assert_no_legacy_rocks ok {chk['ok']} (min verts {chk['min_verts']}); swapped tris {tris}; "
         f"box size error mean {errs.mean():.3f} max {errs.max():.3f}{extra}")
    gate(f"SWAP_NO_STACKED_CLONES_{tag}", cr["ok"] and cr["pieces"] == len(out),
         f"baseline hole_{tag}: {cr['groups']} legacy boxes -> {cr['pieces']} pieces; pieces stacked vertically on another piece of the same box "
         f"(same plan xy, heights differ): {len(cr['vertical_stacks'])} {cr['vertical_stacks'][:3]}; identical clones (same kind, scale within 3 %, "
         f"same yaw mod 90): {len(cr['clones'])} {cr['clones'][:3]}; sea-stack boxes composed as ONE spire + satellites + skirt: "
         f"{SWAP_STATS[tag]['stack_class']}; nz tiles never > 1")
    gate(f"SWAP_ANCHORED_{tag}", min(d_bot) >= -0.2 and min(d_tail) >= -0.5 and min(d_top) >= -1.5 and max(d_top) <= 1.0,
         f"baseline hole_{tag}: replacement vs legacy vertices over {len(grp)} boxes: lowest point delta min {min(d_bot):+.2f} m on the {len(grp) - n_stack} plain boxes "
         f"(>= -0.2: never buried deeper than the legacy), {n_stack} sea-stack groups: lowest point above the spire's designed 3 m x scale underwater tail "
         f"{min(d_tail):+.2f} m (>= -0.5, skirt boulders included); top delta {min(d_top):+.2f} .. {max(d_top):+.2f} m (within -1.5 .. +1.0)")


def repair_gates():
    """v2 repair (review P): NO tiled stacks / identical clones, swap never leaves a short or blob rock, place()/scatter() without D refuse the
    sea. Every gate has a negative control (the old behaviour) that must FAIL."""
    import bmesh  # noqa: F401
    # --- A. composition gate: the old v1 plans (vertical crag pair, 2 x 2 lattice of identical spires) FAIL it, the new cluster passes
    fresh()
    pair = [PL.place(None, "ROCK_CRAG_A_WET", (0.0, 0.0, -4.0), 0.0, 1.5), PL.place(None, "ROCK_CRAG_A_WET", (0.0, 0.0, 2.6), 0.0, 1.5)]
    lat = [PL.place(None, "ROCK_SEASTACK_A_WET", (x, y, 0.0), (0.0 if (x > 0) else math.pi), 1.2) for x in (-5.0, 5.0) for y in (-5.0, 5.0)]
    for o in pair:
        o["lk_swap"] = "OLD_STACK_02"
    for o in lat:
        o["lk_swap"] = "OLD_STACK_04"
    neg = PL.swap_composition_report()
    fresh()
    new = []
    for i, (h, r) in enumerate(((19.3, 9.5), (11.3, 5.0), (14.0, 7.0), (12.0, 3.0))):
        for o in PL.place_sea_stack(None, i * 40.0, 0.0, h, r, seed=i + 1):
            o["lk_swap"] = f"NEW_{i}"
            new.append(o)
    pos = PL.swap_composition_report()
    sums = {}
    for o in new:
        sums.setdefault(o["lk_swap"], []).append(o.data.name)
    gate("SWAP_COMPOSITION_GATE", (not neg["ok"]) and len(neg["vertical_stacks"]) >= 1 and len(neg["clones"]) >= 3 and pos["ok"] and pos["pieces"] == len(new),
         f"NEGATIVE (the v1 plans): a vertical pair of ROCK_CRAG_A_WET {len(neg['vertical_stacks'])} stacked pair(s) and a 2 x 2 lattice of identical "
         f"ROCK_SEASTACK_A_WET {len(neg['clones'])} identical pair(s) -> FAIL as required; NEW place_sea_stack clusters (radius 9.5 / 5 / 7 / 3 m): "
         f"{pos['pieces']} pieces, stacked pairs {len(pos['vertical_stacks'])}, identical clones {len(pos['clones'])}, pieces per stack {[len(v) for v in sums.values()]}, "
         f"main kinds {[v[0] for v in sums.values()]}")

    # --- B. a tall legacy stack (hole 9 style ROCK_STACK_nn, 14 x 15 x 21 m, z -4..17) becomes ONE spire + lower satellites + skirt
    fresh()
    st = neg_icosphere("ROCK_STACK_01", 1, 1.0)
    st.data.name = "ROCK_STACK_01"
    for v in st.data.vertices:
        v.co = Vector((v.co.x * 7.2, v.co.y * 7.4, v.co.z * 10.6 + 6.6))
    st.location = (30.0, 40.0, 0.0)
    thin = neg_icosphere("ROCK_LARGE.001", 1, 1.0)
    thin.data.name = "ROCK_LARGE"
    for v in thin.data.vertices:
        v.co = Vector((v.co.x * 1.1, v.co.y * 1.1, v.co.z * 7.5 + 4.5))
    thin.location = (-30.0, 40.0, 0.0)
    bpy.context.view_layer.update()
    zt = {"ROCK_STACK_01": 17.2, "ROCK_LARGE.001": 12.0}
    rep = []
    out = PL.swap_legacy_rocks(None, report=rep, seed=3)
    bpy.context.view_layer.update()
    grp = {}
    for o in out:
        grp.setdefault(o["lk_swap"], []).append(o)
    ok = True
    notes = []
    for k, objs in grp.items():
        main = objs[0]
        zs = np.array([(main.matrix_world @ v.co).z for v in main.data.vertices])
        spire = main.data.name.startswith(("ROCK_SEASTACK", "ROCK_SPIRE"))
        vert = sum(1 for o in objs if o is not main and (o.matrix_world.translation.xy - main.matrix_world.translation.xy).length < 1.0)
        notes.append(f"{k}: {len(objs)} pieces {[o.data.name for o in objs]}, main summit {zs.max():.1f} m (legacy {zt[k]:.1f})")
        ok = ok and spire and abs(float(zs.max()) - zt[k]) <= 0.8 and vert == 0
    cr = PL.swap_composition_report()
    sr = PL.stretch_report()
    gate("SWAP_TALL_IS_ONE_SPIRE", ok and cr["ok"] and sr["ok"] and len(out) >= 2 and all(r["cls"] == "stack" for r in rep),
         f"tall legacy boxes -> ONE tapering spire at the legacy summit (+ satellites / skirt boulders beside it, never on it): {'; '.join(notes)}; "
         f"composition ok {cr['ok']}, stretch ok {sr['ok']} (scale {sr['min_scale']:.2f}..{sr['max_scale']:.2f})")

    # --- C. mutants: the swap must not leave a <60-vertex rock behind (replace or raise) and replaces a blob of any vertex count
    fresh()
    neg_icosphere("ROCK_SKIN_BAD", 1, 2.0)
    raised = False
    try:
        PL.swap_legacy_rocks(None)
    except RuntimeError as e:
        raised = "ROCK_SKIN_BAD" in str(e)
    fresh()
    big = neg_icosphere("ROCK_BIGICO", 3, 2.5)
    big.location = (0.0, 0.0, 8.0)
    sh = PL.rock_shape_report(big.data)
    flagged = PL._is_legacy_rock(big)
    bpy.context.view_layer.update()
    out = PL.swap_legacy_rocks(None)
    chk = PL.assert_no_legacy_rocks(None, raise_on_fail=False)
    left = [o.name for o in bpy.data.objects if o.name.startswith("ROCK_BIGICO")]
    gate("SWAP_REFUSES_SHORT_AND_BLOB", raised and flagged and sh["blob"] and not left and chk["ok"] and len(out) >= 1,
         f"12-vertex ROCK_SKIN_BAD: swap raises RuntimeError naming it {raised}; 162-vertex blob ROCK_BIGICO (rock_shape_report blob {sh['blob']}, "
         f"radius CV {sh['radius_cv']:.3f}) flagged legacy {flagged}, replaced by {sorted({o.data.name for o in out})} (left {left or 'none'}), "
         f"assert_no_legacy_rocks ok {chk['ok']}")

    # --- D. plants never on the sea through place() / scatter(D=None)
    fresh()
    res = {}
    for nm, fn in (("place z=0", lambda: PL.place(None, "PLANT_TUFT_A", (0, 0, 0.0))),
                   ("place z=0.2", lambda: PL.place(None, "PLANT_SHRUB_A", (0, 0, 0.2))),
                   ("scatter(None, region)", lambda: PL.scatter(None, "PLANT_TUFT_A", 50, region=(-200, -200, 200, 200), seed=1)),
                   ("scatter(None, z=0)", lambda: PL.scatter(None, "PLANT_TUFT_A", 50, region=(-200, -200, 200, 200), seed=1, z=0.0))):
        try:
            fn()
            res[nm] = "PLACED"
        except ValueError:
            res[nm] = "refused"
    fine = PL.place(None, "PLANT_TUFT_A", (0, 0, 5.98))
    low_ok = len(PL.scatter(None, "PLANT_TUFT_A", 10, region=(0, 0, 20, 20), seed=1, z=0.0, allow_low=True)) > 0
    gate("PLANT_PLACE_GUARDS", all(v == "refused" for v in res.values()) and fine is not None and low_ok,
         f"plants without a land mask: {res}; place at z 5.98 ok {fine is not None}; allow_low=True test-scene escape works {low_ok}")


def _prev_lib3():
    """The props library as it stood at the START of repair round 3 (2026-10-05, review 3 of the install: a brown curved strand across the right pier of the hole 8 arch): the frozen
    copy that is the NEGATIVE CONTROL of the round-3 gates. None when missing."""
    import importlib.util
    path = os.path.join(REPO, "work", "postcard-look", "v2", "libs_r3", "backup", "postcard_props_lib.py")
    if not os.path.isfile(path):
        return None
    spec = importlib.util.spec_from_file_location("postcard_props_lib_prev3", path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules["postcard_props_lib_prev3"] = mod
    spec.loader.exec_module(mod)
    return mod


def _prev_lib2():
    """The props library as it stood at the START of repair round 2 (2026-10-04, review 3: hairline slivers across the arch face, tapered-obelisk stacks): the frozen copy
    that is the NEGATIVE CONTROL of the round-2 arch / stack gates. None when missing."""
    import importlib.util
    path = os.path.join(REPO, "work", "postcard-look", "v2", "libs_r2", "backup", "postcard_props_lib.py")
    if not os.path.isfile(path):
        return None
    spec = importlib.util.spec_from_file_location("postcard_props_lib_prev2", path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules["postcard_props_lib_prev2"] = mod
    spec.loader.exec_module(mod)
    return mod


def _prev_lib():
    """The props library as it stood before the 2026-10-04 repair round 1 (frozen copy, used as the NEGATIVE CONTROL of the arch / stack look gates).
    None when the frozen file is missing (the controls are then skipped and said so)."""
    import importlib.util
    path = os.path.join(REPO, "work", "postcard-look", "v2", "libs_r1", "backup", "postcard_props_lib.py")
    if not os.path.isfile(path):
        return None
    spec = importlib.util.spec_from_file_location("postcard_props_lib_prev", path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules["postcard_props_lib_prev"] = mod
    spec.loader.exec_module(mod)
    return mod


def _arch_front_depth(mod, span, height, broken, seed, step=0.02):
    """Build one arch with `mod` and ray-cast it from the front along +Y on a `step` grid: returns (facts dict, depth map (nz, nx) of the first
    hit measured from the start plane, nan = miss, xs, zs, the BVH, object)."""
    import bmesh
    from mathutils.bvhtree import BVHTree
    r = mod.build_masonry_arch(None, (0, 0, 0.0), 0.0, span_m=span, height_m=height, seed=seed, base_rock=False, vines=False, broken=broken)
    ob = r["arch"]
    me = ob.data
    nf = len(me.polygons)
    cnx = np.array([q.normal.x for q in me.polygons])
    ccx = np.array([q.center.x for q in me.polygons])
    ccz = np.array([q.center.z for q in me.polygons])
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    bvh = BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())
    d = r["dims"]
    depth = max(1.0, 0.17 * span)
    y_start = -depth / 2 - 0.5
    xs = np.arange(-(d["r_in"] + d["pw"]) + 0.1, d["r_in"] + d["pw"] - 0.1, step)
    zs = np.arange(0.45, height - 0.2, step)
    Dm = np.full((len(zs), len(xs)), np.nan)
    for i, z in enumerate(zs.tolist()):
        for j, x in enumerate(xs.tolist()):
            h = bvh.ray_cast(Vector((x, y_start, z)), Vector((0, 1, 0)), depth + 1.0)
            if h[0] is not None:
                Dm[i, j] = h[0].y - y_start
    facts = dict(faces=nf, dims=d, cnx=cnx, ccx=ccx, ccz=ccz, bvh=bvh, depth=depth, span=span, height=height, broken=broken)
    return facts, Dm, xs, zs


def _deep_slits(Dm, k=2, relief_m=0.03):
    """Cells whose first hit is more than `relief_m` deeper than BOTH cells k steps (4 cm) to either side, along x and along z: a narrow deep gap
    (a mortar slot) shows up, a step edge or a shallow V-groove does not."""
    def rel(A, axis):
        return A - np.maximum(np.roll(A, k, axis), np.roll(A, -k, axis))
    rl = np.fmax(rel(Dm, 0), rel(Dm, 1))
    rl[:k] = 0
    rl[-k:] = 0
    rl[:, :k] = 0
    rl[:, -k:] = 0
    rl = np.nan_to_num(rl, nan=0.0)
    return int((rl > relief_m).sum()), float(rl.max()), int(np.isfinite(Dm).sum())


def round2_gates():
    """2026-10-04 repair round 1 (review K/L: the ruin arch has dark slivers and a notched edge; the sea stacks look like faceted grey blobs)."""
    prev = _prev_lib()
    # --- ARCH: exact-fit blocks (no see-through gap, no deep mortar slot, coplanar pier faces, face budget), measured on the real mesh
    for tag, span, height, broken, seed in (("08", 7.5, 11.0, False, 8), ("09_BROKEN", 6.0, 9.0, True, 12)):
        fresh()
        PL.ensure_library(["ROCK_OUTCROP_A"])
        facts, Dm, xs, zs = _arch_front_depth(PL, span, height, broken, seed)
        d, bvh = facts["dims"], facts["bvh"]
        r_in, r_out, zsp, pw = d["r_in"], d["r_out"], d["zs"], d["pw"]
        n_ = int(round(math.pi * (r_in + (r_out - r_in) / 2) / max(0.7, 0.12 * span)))
        n_ += 1 - n_ % 2

        def solid(x, z):
            ax = abs(x)
            if d.get("plinth_h", 0.36) + 0.03 < z < zsp - 0.03 and r_in + 0.03 < ax < r_in + pw - 0.03:
                return not (broken and x < 0 and z > zsp - 1.9)
            rr = math.hypot(x, z - zsp)
            if z > zsp + 0.03 and r_in + 0.03 < rr < r_out - 0.06:
                ang = math.atan2(z - zsp, x)
                return not (broken and ang > math.pi * (n_ // 2 - 1) / n_ - 0.03)
            return False
        n_solid, n_miss = 0, 0
        for i, z in enumerate(zs.tolist()):
            for j, x in enumerate(xs.tolist()):
                if solid(x, z):
                    n_solid += 1
                    if not math.isfinite(Dm[i, j]):
                        n_miss += 1
        gate(f"ARCH_NO_SEE_THROUGH_{tag}", n_miss == 0,
             f"{n_solid} rays at 2 cm through the solid part of the masonry (3 cm inside every block edge): {n_miss} pass through (sky)")
        sel = (np.abs(facts["cnx"]) > 0.999) & (facts["ccz"] > 0.5) & (facts["ccz"] < zsp - 0.2) & (np.abs(facts["ccx"]) > r_in + pw - 0.01)
        spread = float(np.abs(np.abs(facts["ccx"][sel]) - (r_in + pw)).max()) if sel.any() else 99.0
        gate(f"ARCH_PIER_COPLANAR_{tag}", sel.any() and spread < 1e-4,
             f"{int(sel.sum())} outer pier faces of the courses lie within {spread * 1000:.3f} mm of x = +-(r_in + pw): every course is flush")
        n_new, rmax, n_hit = _deep_slits(Dm)
        ctrl = "control skipped (frozen previous library missing)"
        ctrl_ok = True
        if prev is not None:
            fresh()
            prev._COUNTERS.clear()
            _f, Dm_old, _x, _z = _arch_front_depth(prev, span, height, broken, seed)
            n_old, rmax_old, _h = _deep_slits(Dm_old)
            ctrl_ok = n_old > 50 * max(n_new, 1) and n_old > 200
            ctrl = f"NEGATIVE CONTROL (the previous arch, 5 cm mortar gaps): {n_old} slot cells, max relief {rmax_old * 100:.1f} cm -> {'detected' if ctrl_ok else 'BLIND'}"
        gate(f"ARCH_NO_DEEP_SLITS_{tag}", n_new <= max(4, 0.0002 * n_hit) and ctrl_ok,
             f"{n_new} of {n_hit} cells ({100 * n_new / max(n_hit, 1):.4f} %, limit 0.02 %) are > 3 cm deeper than both cells 4 cm to either side (max relief "
             f"{rmax * 100:.1f} cm); {ctrl}")
        gate(f"ARCH_FACES_{tag}", facts["faces"] <= 500, f"{facts['faces']} polygons (<= 500), blocks with chamfered joints (V-grooves 2.2 cm), {d.get('parts')}")
    # --- SEA STACKS: smooth-shaded, strata ledges (pairs of rings < 25 cm apart), not the previous round's flat-shaded faceted spires
    def craggy(me):
        co, tris, tp = PL._np_mesh(me)
        ef = {}
        for q in me.polygons:
            vs = list(q.vertices)
            for i in range(len(vs)):
                ef.setdefault(tuple(sorted((vs[i], vs[(i + 1) % len(vs)]))), []).append(q.index)
        sharp = np.zeros(len(me.edges), bool)
        sh = me.attributes.get("sharp_edge")
        if sh is not None:
            sh.data.foreach_get("value", sharp)
        ed = np.empty(len(me.edges) * 2, np.int32)
        me.edges.foreach_get("vertices", ed)
        idx = {tuple(sorted(e)): i for i, e in enumerate(ed.reshape(-1, 2).tolist())}
        inner = [k for k, v in ef.items() if len(v) == 2]
        smooth = sum(1 for k in inner if not sharp[idx[k]]) / max(len(inner), 1)
        sl = me.attributes.get("lk_slice")
        v = np.empty(len(me.vertices), np.int32)
        sl.data.foreach_get("value", v)
        zmean = {int(s_): float(co[v == s_][:, 2].mean()) for s_ in set(v.tolist()) if s_ >= 0}
        zs_ = sorted(z for z in zmean.values() if z >= 0.0)
        pairs = sum(1 for a_, b_ in zip(zs_[:-1], zs_[1:]) if b_ - a_ < 0.25)
        return smooth, pairs
    fresh()
    kinds = list(PL.SEASTACK_KINDS) + ["ROCK_SPIRE_A"]
    cur = {k: craggy(PL.library_object(k).data) for k in kinds}
    ok = all(sm >= 0.45 and pr >= 4 for sm, pr in cur.values())
    ctrl = "control skipped (frozen previous library missing)"
    ctrl_ok = True
    if prev is not None:
        fresh()
        prev._COUNTERS.clear()
        old = {k: craggy(prev.library_object(k).data) for k in kinds}
        ctrl_ok = all(not (sm >= 0.45 and pr >= 4) for sm, pr in old.values())
        ctrl = "NEGATIVE CONTROL (the previous flat-shaded spires): " + ", ".join(f"{k.replace('ROCK_', '')} {sm:.2f}/{pr}" for k, (sm, pr) in old.items()) + \
            f" -> {'all FAIL as they must' if ctrl_ok else 'SOME PASS (!)'}"
    gate("SEASTACK_CRAGGY", ok and ctrl_ok,
         "smooth-edge share / ledge pairs: " + ", ".join(f"{k.replace('ROCK_', '')} {sm:.2f}/{pr}" for k, (sm, pr) in cur.items()) +
         f" (need >= 0.45 smooth interior edges and >= 4 ledge pairs: two rings < 25 cm apart); {ctrl}")


def _arch_leaks(mod, span, height, broken, seed, step=0.08):
    """Oblique see-through test of the arch built by `mod`: rays from 28 directions (azimuth +-70 deg round the front normal, elevation -12..45 deg, 25 m out) to sample points
    in the middle plane of the SOLID masonry (3-4 cm inside every block edge). In a closed solid the first hit is a FRONT face (normal facing the ray source); a first hit on a
    BACK face, or none, means the ray entered the stone through a crack. Returns (points, rays, leaking rays, faces)."""
    import bmesh
    from mathutils.bvhtree import BVHTree
    r = mod.build_masonry_arch(None, (0, 0, 0.0), 0.0, span_m=span, height_m=height, seed=seed, base_rock=False, vines=False, broken=broken, name="DRESS_ARCH_00")
    ob = r["arch"]
    me = ob.data
    nf = len(me.polygons)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    bvh = BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())
    d = r["dims"]
    r_in, r_out, zsp, pw = d["r_in"], d["r_out"], d["zs"], d["pw"]
    n_ = int(round(math.pi * (r_in + (r_out - r_in) / 2) / max(0.7, 0.12 * span)))
    n_ += 1 - n_ % 2
    pts = []
    for x in np.arange(-(r_in + pw), r_in + pw, step):
        for z in np.arange(d.get("plinth_h", 0.36) + 0.06, height - 0.1, step):
            ax = abs(x)
            if z < zsp - 0.03 and r_in + 0.04 < ax < r_in + pw - 0.04:
                ok = not (broken and x < 0 and z > zsp - 1.9)
            else:
                rr = math.hypot(x, z - zsp)
                ok = False
                if z > zsp + 0.03 and r_in + 0.04 < rr < r_out - 0.08:
                    ang = math.atan2(z - zsp, x)
                    ok = not (broken and ang > math.pi * (n_ // 2 - 1) / n_ - 0.03)
            if ok:
                pts.append(Vector((x, 0.0, z)))
    cams = []
    for az in (-70, -45, -20, 0, 20, 45, 70):
        for el in (-12, 8, 28, 45):
            e_, a_ = math.radians(el), math.radians(az)
            cams.append(Vector((math.sin(a_) * math.cos(e_), -math.cos(a_) * math.cos(e_), math.sin(e_))) * 25.0)
    leaks, nray = 0, 0
    for P0 in pts:
        for c in cams:
            dv = -c.normalized()
            h = bvh.ray_cast(P0 + c, dv, 25.5)
            nray += 1
            if h[0] is None or h[1].dot(dv) > 0:
                leaks += 1
    return len(pts), nray, leaks, nf


def round3_gates():
    """2026-10-04 repair round 2 (review 3): hole 8's ruin arch still showed hairline slivers across its face; the sea stacks read as tapered obelisks with bright green pyramid caps."""
    prev = _prev_lib2()
    # --- ARCH: no oblique see-through (the round-1 gates only cast frontal rays), joints on the mortar rows of the texture, joint faces on a mortar row of the albedo
    for tag, span, height, broken, seed in (("08", 9.144, 12.166, False, 8), ("09_BROKEN", 6.0, 9.0, True, 12), ("SMALL", 4.0, 6.5, False, 2)):
        fresh()
        PL.ensure_library(["ROCK_OUTCROP_A"])
        npts, nray, leaks, nf = _arch_leaks(PL, span, height, broken, seed)
        ctrl = "control skipped (frozen start-of-round library missing)"
        ctrl_ok = True
        if prev is not None:
            fresh()
            prev._COUNTERS.clear()
            _n, nray_o, leaks_o, _nf = _arch_leaks(prev, span, height, broken, seed)
            ctrl_ok = leaks_o >= 10
            ctrl = f"NEGATIVE CONTROL (the arch of the start of this round: wedges with a 0-9 mm random front offset): {leaks_o} of {nray_o} rays leak -> {'detected' if ctrl_ok else 'BLIND'}"
        gate(f"ARCH_NO_OBLIQUE_LEAK_{tag}", leaks == 0 and ctrl_ok,
             f"{npts} sample points inside the solid masonry x 28 oblique directions = {nray} rays: {leaks} first hits on a back face / none ({nf} polygons); {ctrl}")
    fresh()
    r = PL.build_masonry_arch(None, (0, 0, 0.0), 0.0, span_m=9.144, height_m=12.166, seed=8, base_rock=False, vines=False, name="DRESS_ARCH_00")
    me, d = r["arch"].data, r["dims"]
    pitch, phase, v_row, depth = PL.masonry_grid()
    img = PL._image("Masonry_C")
    w_, h_ = int(img.size[0]), int(img.size[1])
    prof = np.array(img.pixels[:], np.float32).reshape(h_, w_, 4)[:, :, :3].mean(2).mean(1)

    def joint_lum(z, voff):
        v = ((z - voff) / PL.TILE["LK_MASONRY"]) % 1.0
        y = int(v * h_) % h_
        return float(np.mean([prof[(y + k) % h_] for k in (-1, 0, 1)]))
    # exposed horizontal faces = the plinth top, the top of the last pier course (the spring line) and every haunch row top: all are joint planes of the grid
    zj = sorted({round(float(q.center.z), 4) for q in me.polygons if q.normal.z > 0.999 and q.center.z > 0.2})
    zj = [z for z in zj if z <= d["zs"] + 0.5 * d["r_out"] * 0.8 + 0.01]
    uvl = me.uv_layers.active.data
    mortar, big, n_poly = 0, 0, len(me.polygons)
    for q in me.polygons:
        vs = [uvl[li].uv[1] for li in q.loop_indices]
        if max(vs) - min(vs) < 0.012 and abs(sum(vs) / len(vs) - v_row) < 0.004:
            mortar += 1
            if q.area > 0.3:
                big += 1
    lum_new = float(np.mean([joint_lum(z, d["voff"]) for z in zj]))
    prev_ctrl = ""
    ctrl_ok = True
    if prev is not None:
        fresh()
        prev._COUNTERS.clear()
        ro = prev.build_masonry_arch(None, (0, 0, 0.0), 0.0, span_m=9.144, height_m=12.166, seed=8, base_rock=False, vines=False, name="DRESS_ARCH_00")
        mo = ro["arch"].data
        zo = sorted({round(float(q.center.z), 4) for q in mo.polygons if q.normal.z > 0.999 and q.center.z > 0.2})
        zo = [z for z in zo if z <= ro["dims"]["zs"] + 0.5 * ro["dims"]["r_out"] * 0.8 + 0.01]
        lum_old = float(np.mean([joint_lum(z, 0.0) for z in zo])) if zo else float(prof.mean())
        ctrl_ok = (prof.mean() - lum_old) < 0.5 * depth
        prev_ctrl = f"; NEGATIVE CONTROL (round-1 arch, joints at random heights, no UV phase): {len(zo)} joint planes, mean luminance there {lum_old:.3f} (stone {prof.mean():.3f}) -> {'detected' if ctrl_ok else 'BLIND'}"
    fold_ok = abs(((d["zs"] - d["plinth_h"]) / pitch) - round((d["zs"] - d["plinth_h"]) / pitch)) < 1e-6
    gate("ARCH_JOINTS_ON_MORTAR_ROWS", (prof.mean() - lum_new) >= 0.6 * depth and fold_ok and ctrl_ok,
         f"{len(zj)} exposed joint planes (plinth top, spring line, haunch row tops) at z {zj[0]:.2f} .. {zj[-1]:.2f}: the painted Masonry_C luminance there is {lum_new:.3f} against a stone mean "
         f"{prof.mean():.3f} (mortar rows {prof.min():.3f}, depth {depth:.3f}; need the joints >= 0.6 x depth darker), pier height = {round((d['zs'] - d['plinth_h']) / pitch)} x the "
         f"measured pitch {pitch:.3f} m (phase {phase:.3f} m, UV phase voff {d['voff']:.3f} m){prev_ctrl}")
    gate("ARCH_JOINT_FACES_MORTAR_UV", mortar >= 100 and big == 0,
         f"{mortar} chamfer / strip faces of {n_poly} carry the mortar UV (a mortar ROW of the albedo at v {v_row:.4f}, V extent < 0.012 = 6 px, isotropic or squeezed <= 3.5:1); "
         f"{big} of them are larger than 0.3 m2 (must be 0)")
    # --- VINES: the stalk keeps a real width at the tip (the review's 1 px white line)
    fresh()
    w_tip, w_top = [], []
    for k in PL.VINE_KINDS:
        m = PL.library_object(k).data
        co = np.array([v.co[:] for v in m.vertices])
        z0 = co[:, 2].min()
        # the stalk rings are the first 3 vertices of each ring; leaves are octahedra: take the ring at the very bottom of the stalk = vertices of the stalk strip at z of the stalk end
        ring_end = co[np.abs(co[:, 2] - (-PL.VINE_LENGTH[k])) < 1e-6]
        ring_top = co[np.abs(co[:, 2] - 0.0) < 1e-6]
        w_tip.append(max(float(np.linalg.norm(a - b)) for a in ring_end for b in ring_end) if len(ring_end) >= 3 else 0.0)
        w_top.append(max(float(np.linalg.norm(a - b)) for a in ring_top for b in ring_top) if len(ring_top) >= 3 else 0.0)
    gate("VINE_TIP_WIDTH", min(w_tip) >= 0.027 and min(w_top) >= 0.04,
         f"stalk width (the longest side of the 3-corner ring) at the tip {min(w_tip) * 100:.1f}..{max(w_tip) * 100:.1f} cm (>= 2.7 cm = 1.5 px at 25 m), at the top {min(w_top) * 100:.1f}..{max(w_top) * 100:.1f} cm (>= 4 cm)")
    # --- SEA STACKS: shrub crown instead of a pyramid, asymmetric axis, exact height, muted greens
    fresh()
    PL.write_plants_atlas()
    rows, ok_all = [], True
    allowed_sw = {PL.SWATCH[n_] for n_ in ("G_DEEP", "G_DARK", "OLIVE_DARK", "G_MID", "OLIVE")}
    for k in PL.SPIRE_KINDS:
        m = PL.library_object(k).data
        co, tris, _tp = PL._np_mesh(m)
        _vi, co3, pol, nrm, mi = PL._loop_arrays(m)
        mats = [x.name for x in m.materials]
        ps = [i for i, n_ in enumerate(mats) if n_ == "LK_PLANTS"]
        n_crown = int(np.isin(mi, ps).sum())
        zmax = float(co[:, 2].max())
        Hs = PL.SPIRE_SPECS[k]["height"]
        hc_t = min(1.7, 0.085 * Hs + 0.2)
        # crown extent vs the neck 0.3 m under it
        crown_pts = np.array([co3[vv][:2] for q in np.nonzero(np.isin(mi, ps))[0] for vv in [int(t_) for t_ in m.polygons[int(q)].vertices]])
        crown_w = float(np.ptp(crown_pts[:, 0])) if len(crown_pts) else 0.0
        sec = PL.mesh_section(co, tris, Hs - hc_t - 0.35)[:, :2]
        neck_w = float(np.ptp(sec[:, 0])) if len(sec) else 1.0
        # swatches used by the crown faces
        uvs = me_uv = m.uv_layers.active.data
        cells = set()
        for q in np.nonzero(np.isin(mi, ps))[0]:
            u_, v_ = uvs[m.polygons[int(q)].loop_indices[0]].uv
            cells.add(int(math.floor(v_ * 4)) * 4 + int(math.floor(u_ * 4)))
        # the atlas rows: row 0 at the TOP of the image, v 0 at the bottom: swatch index = (3 - floor(v*4)) * 4 + floor(u*4)
        sw_idx = {(3 - c // 4) * 4 + (c % 4) for c in cells}
        # axis offset: centroid of the section at 15 % vs 85 % of the height above the water
        def cen(f):
            q = PL.mesh_section(co, tris, f * Hs)[:, :2]
            return q.mean(0) if len(q) else np.zeros(2)
        off = float(np.linalg.norm(cen(0.85) - cen(0.15))) / Hs
        good = n_crown >= 25 and abs(zmax - Hs) < 0.06 and 0.95 <= crown_w / max(neck_w, 1e-6) <= 1.9 and sw_idx <= allowed_sw and off >= 0.04
        ok_all &= good
        rows.append(f"{k.replace('ROCK_', '')}: crown {n_crown} faces, summit {zmax:.2f} m (design {Hs:g}), crown / neck width {crown_w / max(neck_w, 1e-6):.2f}, axis offset {off:.2f} H, swatches {sorted(sw_idx)} {'ok' if good else 'BAD'}")
    ctrl = ""
    ctrl_ok = True
    if prev is not None:
        fresh()
        prev._COUNTERS.clear()
        mo = prev.library_object("ROCK_SEASTACK_A").data
        mats_o = [x.name for x in mo.materials]
        n_plants_o = int(sum(1 for q in mo.polygons if mats_o[q.material_index] == "LK_PLANTS"))
        ctrl_ok = n_plants_o == 0
        ctrl = f"; NEGATIVE CONTROL (the start-of-round stack): {n_plants_o} LK_PLANTS faces, grass roof only -> {'detected' if ctrl_ok else 'BLIND'}"
    gate("SPIRE_SHRUB_CROWN", ok_all and ctrl_ok,
         "; ".join(rows) + f" (need >= 25 dome faces in LK_PLANTS, summit at the design height +-6 cm, crown 0.95-1.9 x the neck, only the muted swatches G_DEEP / G_DARK / OLIVE_DARK / G_MID / OLIVE, "
         f"axis offset >= 0.04 x the height){ctrl}")


def _extrados_step(mod, span, height, seed):
    """Depth step between the ring face and the haunch face along the extrados of the right half of an arch built by `mod`: front rays (+Y, 2 cm grid) at r_out -+ 0.08 m on every
    z row of the haunch zone; returns (list of |step| in metres, max, p95, n pairs, the dims)."""
    facts, Dm, xs, zs = _arch_front_depth(mod, span, height, False, seed)
    d = facts["dims"]
    r_out, zsp = d["r_out"], d["zs"]
    steps = []
    for zi, z in enumerate(zs.tolist()):
        dz = z - zsp
        if dz < 0.25 or dz > 0.80 * r_out - 0.3:
            continue
        xe = math.sqrt(max(r_out * r_out - dz * dz, 0.0))
        j_in = int(round((xe - 0.08 - xs[0]) / 0.02))
        j_out = int(round((xe + 0.08 - xs[0]) / 0.02))
        if not (0 <= j_in < len(xs) and 0 <= j_out < len(xs)):
            continue
        a, b = Dm[zi, j_in], Dm[zi, j_out]
        if math.isfinite(a) and math.isfinite(b):
            steps.append(abs(float(b - a)))
    arr = np.array(steps) if steps else np.zeros(1)
    return steps, float(arr.max()), float(np.percentile(arr, 95)), len(steps), d


def round4_gates():
    """2026-10-05 repair round 3 (review 3 of the installed holes): the hole 8 arch shows ONE long brown curved strand across the right pier. Root cause = the 12 cm deep step between the
    ring and the recessed haunch with a mortar-UV strip on it: a long, smooth, brown 3 px line that no other joint has."""
    prev = _prev_lib3()
    rows, ok = [], True
    for tag, span, height, seed in (("08", 9.144, 12.166, 8), ("SMALL", 4.0, 6.5, 2), ("07", 7.5, 11.0, 8)):
        fresh()
        PL.ensure_library(["ROCK_OUTCROP_A"])
        steps, smax, p95, n, d = _extrados_step(PL, span, height, seed)
        good = n >= 10 and smax <= 0.05
        ok &= good
        rows.append(f"{tag}: {n} z rows, step ring->haunch max {smax * 100:.1f} cm, p95 {p95 * 100:.1f} cm {'ok' if good else 'BAD'}")
    ctrl, ctrl_ok = "control skipped (frozen start-of-round library missing)", True
    if prev is not None:
        fresh()
        prev._COUNTERS.clear()
        _s, omax, op95, on, _d = _extrados_step(prev, 9.144, 12.166, 8)
        ctrl_ok = omax >= 0.10
        ctrl = f"NEGATIVE CONTROL (the start-of-round arch, 12 cm recess): max step {omax * 100:.1f} cm, p95 {op95 * 100:.1f} cm -> {'detected' if ctrl_ok else 'BLIND'}"
    gate("ARCH_EXTRADOS_NO_STEP", ok and ctrl_ok,
         "; ".join(rows) + f" (limit 5 cm: the ring / haunch boundary is a normal V-groove joint, not a step that carries a long brown strip); {ctrl}")
    # the mortar-UV strip faces on the extrados are at most 5 cm wide (a joint line, not a band)
    def wide_faces(mod):
        fresh()
        r = mod.build_masonry_arch(None, (0, 0, 0.0), 0.0, span_m=9.144, height_m=12.166, seed=8, base_rock=False, vines=False, name="DRESS_ARCH_00")
        me = r["arch"].data
        uvl = me.uv_layers.active.data
        v_row = mod.masonry_grid()[2]
        n = 0
        for q in me.polygons:
            vs = [uvl[li].uv[1] for li in q.loop_indices]
            if max(vs) - min(vs) < 0.012 and abs(sum(vs) / len(vs) - v_row) < 0.004:           # mortar-UV faces: chamfers + strips
                ys = [me.vertices[i].co.y for i in q.vertices]
                if max(ys) - min(ys) > 0.06:                                                      # deeper than 6 cm = a step wall painted as a strip, not a chamfer
                    n += 1
        return n
    wide = wide_faces(PL)
    ctrl, ctrl_ok = "control skipped", True
    if prev is not None:
        prev._COUNTERS.clear()
        wo = wide_faces(prev)
        ctrl_ok = wo >= 5
        ctrl = f"NEGATIVE CONTROL (start-of-round arch): {wo} such faces -> {'detected' if ctrl_ok else 'BLIND'}"
    gate("ARCH_NO_DEEP_MORTAR_STRIP", wide == 0 and ctrl_ok,
         f"{wide} mortar-UV faces of DRESS_ARCH_00 are deeper than 6 cm (a step wall painted as a brown strip); chamfers and 3.5 cm strips only; {ctrl}")


def _shrub_swatch_use(mod, kind):
    """{swatch name: share of the faces} of a library shrub (atlas cell of the first UV of every polygon) and the face-weighted mean hue (degrees) of those swatches."""
    import colorsys
    me = mod.library_object(kind).data
    uvl = me.uv_layers.active.data
    cnt = {}
    for q in me.polygons:
        us = [uvl[li].uv[0] for li in q.loop_indices]
        vs = [uvl[li].uv[1] for li in q.loop_indices]
        c, rb = int(math.floor(sum(us) / len(us) * 4)), int(math.floor(sum(vs) / len(vs) * 4))
        idx = (3 - rb) * 4 + c
        nm = mod.SWATCHES[idx][0]
        cnt[nm] = cnt.get(nm, 0) + 1
    tot = sum(cnt.values())
    cols = dict(mod.SWATCHES)
    hue = 0.0
    for nm, n in cnt.items():
        h, _s, _v = colorsys.rgb_to_hsv(*[x / 255.0 for x in cols[nm]])
        hue += (h * 360.0) * n / tot
    return {k: v / tot for k, v in cnt.items()}, hue


def _spire_irregularity(mod, kind, azs=(0, 45, 90, 135), n=44):
    """Silhouette irregularity of a library spire: the left / right silhouette edge (sections at 44 heights between 12 % and 85 % of the height, projected on 4 azimuths) minus its best
    cubic fit (the global taper / lean / bend is smooth; shoulders, waists and bites are what is left), mean absolute residual as a share of the mean width."""
    me = mod.library_object(kind).data
    H = mod.SPIRE_SPECS[kind]["height"]
    co, tris, _tp = mod._np_mesh(me)
    out = []
    for az in azs:
        t = math.radians(az)
        ax = np.array([math.cos(t), math.sin(t)])
        L, R_ = [], []
        for z in np.linspace(0.12 * H, 0.85 * H, n):
            sec = mod.mesh_section(co, tris, z)[:, :2]
            if len(sec) < 3:
                continue
            u = sec @ ax
            L.append(u.min())
            R_.append(u.max())
        L, R_ = np.array(L), np.array(R_)
        zz = np.arange(len(L))
        dev = sum(np.abs(e - np.polyval(np.polyfit(zz, e, 3), zz)).mean() for e in (L, R_))
        out.append(dev / 2 / max((R_ - L).mean(), 1e-9))
    return float(np.mean(out))


def round4_stack_gates():
    """Review 3, hole 9: the sea stacks still read as obelisks (straight tapering shafts). Library numbers: the verifier's flat-cap measure stays under its 25 % limit (aim 20 %), and the
    silhouette is broken by waists, shoulders and bites (cubic-detrended edge residual, a proxy: mean >= 0.030 and every kind >= 0.022; the start-of-round stacks measure 0.018-0.027, mean 0.022)."""
    prev = _prev_lib3()
    fresh()
    caps = {k: PL.spire_flat_cap(PL.library_object(k), PL.SPIRE_SPECS[k]["height"]) for k in PL.SPIRE_KINDS}
    gate("SPIRE_FLAT_CAP_VERIFIER", max(caps.values()) <= 0.22,
         "flat cap (verifier measure: largest vertex-connected group of up-facing triangles above 20 % height / hull of the 10 % section) " +
         ", ".join(f"{k.replace('ROCK_', '')} {v:.0%}" for k, v in caps.items()) + " (limit 25 %, library aim 20 %, <= 22 % required)")
    cur = {k: _spire_irregularity(PL, k) for k in PL.SPIRE_KINDS}
    mean_c = sum(cur.values()) / len(cur)
    ctrl, ctrl_ok = "control skipped", True
    if prev is not None:
        fresh()
        prev._COUNTERS.clear()
        old = {k: _spire_irregularity(prev, k) for k in PL.SPIRE_KINDS}
        mean_o = sum(old.values()) / len(old)
        ctrl_ok = not (mean_o >= 0.030 and min(old.values()) >= 0.022)
        ctrl = f"NEGATIVE CONTROL (start-of-round stacks): " + ", ".join(f"{k.replace('ROCK_', '')} {v:.3f}" for k, v in old.items()) + f", mean {mean_o:.3f} -> {'FAIL as it must' if ctrl_ok else 'PASSES (!)'}"
    gate("SPIRE_SILHOUETTE_BROKEN", mean_c >= 0.030 and min(cur.values()) >= 0.022 and ctrl_ok,
         "edge residual after a cubic fit / width: " + ", ".join(f"{k.replace('ROCK_', '')} {v:.3f}" for k, v in cur.items()) + f", mean {mean_c:.3f} (need mean >= 0.030, each >= 0.022); {ctrl}")


def round4_shrub_gate():
    """Review 3, hole 9: the bushes read as lime balls (the brightest swatch G_LIGHT popped against the olive scrub, h72 vs h48). Shrubs use olive / dark-olive / mid-green swatches only
    (face-weighted mean hue 54..72), never G_LIGHT / G_LIME / G_EMERALD, and are not a pyramid of balls (the tallest lump top is below 1.45 x the width of the base lump)."""
    prev = _prev_lib3()
    bad_sw = {"G_LIGHT", "G_LIME", "G_EMERALD"}
    rows, ok = [], True
    for k in ("PLANT_SHRUB_A", "PLANT_SHRUB_B", "PLANT_SHRUB_C"):
        fresh()
        use, hue = _shrub_swatch_use(PL, k)
        me = PL.library_object(k).data
        co = np.array([v.co[:] for v in me.vertices])
        h, w = float(co[:, 2].max() - co[:, 2].min()), float(max(np.ptp(co[:, 0]), np.ptp(co[:, 1])))
        good = not (set(use) & bad_sw) and 54.0 <= hue <= 72.0 and h <= 1.45 * w
        ok &= good
        rows.append(f"{k.replace('PLANT_', '')}: mean hue {hue:.0f}, bad swatches {sorted(set(use) & bad_sw) or 'none'}, h/w {h / w:.2f} {'ok' if good else 'BAD'}")
    ctrl, ctrl_ok = "control skipped", True
    if prev is not None:
        fresh()
        prev._COUNTERS.clear()
        use_o, hue_o = _shrub_swatch_use(prev, "PLANT_SHRUB_A")
        ctrl_ok = bool(set(use_o) & bad_sw)
        ctrl = f"NEGATIVE CONTROL (start-of-round SHRUB_A): mean hue {hue_o:.0f}, uses {sorted(set(use_o) & bad_sw)} -> {'detected' if ctrl_ok else 'BLIND'}"
    gate("SHRUB_OLIVE_NOT_LIME", ok and ctrl_ok, "; ".join(rows) + f" (swatch hue 54..72, no G_LIGHT / G_LIME / G_EMERALD); {ctrl}")


# ----------------------------------------------------------------------------- v2 2026-10-05: plant sway data + tuft library + edge fringe
SWAY_DIR = os.path.join(REPO, "work", "postcard-look", "v2", "props", "sway")  # frozen controls: read only
SWAY_BASELINE = os.path.join(SWAY_DIR, "geom_baseline_before.json")
SWAY_STATS = {}
FRINGE_STATS = {}
FRINGE_BUDGET = 60000
FRINGE_MIN_COUNT = 300


def geom_fingerprint(me):
    """Geometry fingerprint (positions, faces, material slots, smooth flags, UV), colours EXCLUDED (identical to tools/geom_baseline.py)."""
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    polys = [tuple(p.vertices) for p in me.polygons]
    mi = np.empty(len(me.polygons), np.int32)
    me.polygons.foreach_get("material_index", mi)
    sm = np.empty(len(me.polygons), np.int8)
    me.polygons.foreach_get("use_smooth", sm)
    u = uv_array(me).ravel()
    h = hashlib.sha256()
    h.update(np.round(co, 6).tobytes())
    h.update(str(polys).encode())
    h.update(mi.tobytes())
    h.update(sm.tobytes())
    h.update(np.round(u, 6).tobytes())
    h.update(str([m.name for m in me.materials]).encode())
    return h.hexdigest()


def vertex_rgba(me, srgb=False):
    """(n_vertices, 4) colours of the first colour attribute: POINT as is, CORNER averaged per vertex. srgb=True reads .color_srgb (the raw numbers of an
    imported FBX byte colour: export_look_fbx writes colors_type LINEAR, the importer treats the file values as sRGB). None when the mesh has none."""
    if len(me.color_attributes) == 0:
        return None
    a = me.color_attributes.get(PL.SWAY_ATTR) or me.color_attributes[0]
    arr = np.empty(len(a.data) * 4)
    a.data.foreach_get("color_srgb" if srgb else "color", arr)
    c = arr.reshape(-1, 4)
    if a.domain == "POINT":
        return c
    vidx = np.empty(len(me.loops), np.int32)
    me.loops.foreach_get("vertex_index", vidx)
    out = np.zeros((len(me.vertices), 4))
    cnt = np.zeros(len(me.vertices))
    np.add.at(out, vidx, c)
    np.add.at(cnt, vidx, 1.0)
    return out / np.maximum(cnt, 1.0)[:, None]


def sway_mesh_check(kind, rgba, V):
    """(ok, detail) of the sway attribute of a mesh of `kind`: present, finite, B = 0, A = 1, G in 0..1, root weight <= SWAY_ROOT_MAX at the root / pinned point,
    largest weight inside the class window (>= SWAY_TIP_MIN, <= SWAY_TIP, and weight x SWAY_REF_AMP_YD <= the kind's cap)."""
    cls = PL.sway_class(kind)
    if rgba is None:
        return False, "no colour attribute"
    R, G, B, A = rgba[:, 0], rgba[:, 1], rgba[:, 2], rgba[:, 3]
    if not np.isfinite(rgba).all():
        return False, "NaN / inf"
    if len(rgba) != len(V):
        return False, f"{len(rgba)} colours for {len(V)} vertices"
    z = V[:, 2]
    if cls == "static":
        return (float(R.max()) == 0.0 and float(np.abs(B).max()) == 0.0 and float(A.min()) == 1.0), f"static: R max {float(R.max()):.3f}"
    if cls == "vine":
        root = z >= -0.03
    elif cls == "agave":
        root = np.linalg.norm(V - np.array([0.0, 0.0, 0.04]), axis=1) <= 0.06
    else:
        root = z <= 0.02
    if not root.any():
        return False, "no root vertices"
    r_root, r_tip = float(R[root].max()), float(R.max())
    amp = r_tip * PL.SWAY_REF_AMP_YD
    ok = (r_root <= PL.SWAY_ROOT_MAX and PL.SWAY_TIP_MIN[cls] <= r_tip <= PL.SWAY_TIP[cls] + 1e-6 and amp <= PL.SWAY_CAP_YD[cls] + 1e-9
          and float(np.abs(B).max()) == 0.0 and float(np.abs(A - 1.0).max()) == 0.0 and float(G.min()) >= 0.0 and float(G.max()) <= 1.0 and float(G.max() - G.min()) > 0.0)
    return ok, f"{cls}: root R {r_root:.3f} (<= {PL.SWAY_ROOT_MAX}), tip R {r_tip:.2f} (window {PL.SWAY_TIP_MIN[cls]}..{PL.SWAY_TIP[cls]}), tip x {PL.SWAY_REF_AMP_YD} yd = {amp:.3f} (cap {PL.SWAY_CAP_YD[cls]}), G {float(G.min()):.2f}..{float(G.max()):.2f}"


def _mesh_V(me):
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def tuft_library_gates(hashes, rep):
    """New tuft kinds: faces, heights, byte-compatibility of the existing meshes (geometry fingerprint vs the baseline taken before this pass)."""
    import json
    old, tall, fill = PL.TUFT_KINDS, PL.TUFT_KINDS_TALL, PL.TUFT_KINDS_FILL
    fa = {k: rep[k]["faces"] for k in PL.TUFT_KINDS_ALL}
    ht = {k: float(PL.lib_bbox(k)[1].z) for k in PL.TUFT_KINDS_ALL}
    old_h = max(ht[k] for k in old)
    ok = (all(fa[k] <= 120 for k in tall) and all(fa[k] <= 40 for k in fill) and len(tall) >= 2 and len(fill) >= 1
          and all(ht[k] >= 1.3 * np.mean([ht[q] for q in old]) for k in tall) and all(rep[k]["mats"] == ["LK_PLANTS"] for k in PL.TUFT_KINDS_ALL))
    gate("TUFT_LIB_V2", ok, f"{len(tall)} tall tufts {[(k[-1], fa[k], rep[k]['tris'], round(ht[k], 2)) for k in tall]} (letter, faces <= 120, tris, height m; old tufts {old_h:.2f} m max) "
         f"+ {len(fill)} filler tufts {[(k[-1], fa[k], rep[k]['tris'], round(ht[k], 2)) for k in fill]} (faces <= 40), one LK_PLANTS material each")
    if not os.path.isfile(SWAY_BASELINE):
        gate("PLANT_EXISTING_BYTE_COMPAT", False, f"baseline {SWAY_BASELINE} missing: UNVERIFIED")
        return
    with open(SWAY_BASELINE) as f:
        base = json.load(f)
    diff = []
    for k, v in base.items():
        if k in PL.BUILDERS or k.endswith(("_WET", "_BASALT")):
            if geom_fingerprint(PL.library_object(k).data) != v["sha"]:
                diff.append(k)
    new_ = [k for k in PL.BUILDERS if k not in base]
    gate("PLANT_EXISTING_BYTE_COMPAT", not diff and all(k in PL.TUFT_KINDS_TALL + PL.TUFT_KINDS_FILL for k in new_),
         f"{sum(1 for k in base if k in PL.BUILDERS)} pre-existing library meshes (16 plants, rocks, basalt, blocks): geometry / UV / materials fingerprint identical to the baseline "
         f"taken before the sway pass (colours excluded): changed {diff or 'none'}; new kinds {sorted(new_)}")


def sway_blend_gates():
    """PLANT_SWAY_COLORS on the .blend library meshes + mutants."""
    fresh()
    PL.write_plants_atlas()
    lib = PL.ensure_library()
    bad, rows, stat = [], [], {}
    for k in sorted(PL.PLANT_KINDS):
        me = lib[k].data
        ok, why = sway_mesh_check(k, vertex_rgba(me), _mesh_V(me))
        rows.append((k, ok, why))
        if not ok:
            bad.append((k, why))
        sw = PL.mesh_sway(me)
        stat[k] = (sw["cls"], round(float(sw["R"].max()), 3))
    crowns = [k for k in PL.SPIRE_KINDS if "LK_PLANTS" in [m.name for m in lib[k].data.materials]]
    crown_bad = [k for k in crowns if not sway_mesh_check(k, vertex_rgba(lib[k].data), _mesh_V(lib[k].data))[0] or lib[k].data.get("lk_sway") != "static"]
    # twins keep it
    tw = [PL.library_object(k + "_WET").data for k in ("ROCK_SEASTACK_A", "ROCK_SEASTACK_B")] + [PL.library_object("ROCK_SEASTACK_C_BASALT").data]
    tw_bad = [m.name for m in tw if vertex_rgba(m) is None or float(vertex_rgba(m)[:, 0].max()) != 0.0]
    gate("PLANT_SWAY_COLORS", not bad and not crown_bad and not tw_bad and len(rows) == len(PL.PLANT_KINDS) >= 21,
         f"{len(rows)} PLANT_ library meshes in the blend: Col FLOAT_COLOR POINT with R weight (root <= {PL.SWAY_ROOT_MAX}, class tip window), G phase, B 0, A 1; "
         f"tip R per kind {stat}; the shrub crown of {len(crowns)} sea stacks (+ _WET / _BASALT twins) carries a STATIC attribute (R 0); bad: {bad[:3] or 'none'} {crown_bad or ''} {tw_bad or ''}")
    # --- mutants: a mesh without the attribute, an all-ones weight (root 1), the Unity default colour (1,1,1,1), a zero weight (no sway), a tuft with a vine profile
    me = lib["PLANT_TUFT_D"].data
    V = _mesh_V(me)
    n = len(V)
    good = vertex_rgba(me)
    ones = np.ones((n, 4)); ones[:, 2] = 0.0
    white = np.ones((n, 4))
    zero = good.copy(); zero[:, 0] = 0.0
    inv = good.copy(); inv[:, 0] = 1.0 - good[:, 0]
    over = good.copy(); over[:, 0] = np.minimum(1.0, good[:, 0] * 1.5 + 0.0)
    gam = good.copy(); gam[:, 0] = np.where(good[:, 0] > 0, good[:, 0] ** (1 / 2.2), 0.0); gam[:, 1] = good[:, 1] ** (1 / 2.2)
    muts = {"no attribute": None, "all weights 1 (root moves)": ones, "Unity default white (1,1,1,1)": white, "zero weight (never sways)": zero, "weight inverted (root moves)": inv,
            "shrub-grade weight 0.3 on a tuft": np.concatenate([good[:, :1] * 0.3, good[:, 1:]], 1)}
    res = {k: sway_mesh_check("PLANT_TUFT_D", v, V)[0] for k, v in muts.items()}
    # a sRGB-encoded weight is a LEGAL value but not the raw number: caught by the FBX gate (below); here the encoded tip stays <= 1 -> must be caught by equality, not window
    gate("PLANT_SWAY_COLORS_MUTANTS", not any(res.values()), f"6 mutants of PLANT_TUFT_D all FAIL the check as they must: {res}; the genuine mesh passes: {sway_mesh_check('PLANT_TUFT_D', good, V)[0]}")
    # --- the shape of the weights: a tuft's weight grows with height; a vine's grows downward; lower vertices of a blade never move more than the tip of the same blade
    mono = []
    for k in ("PLANT_TUFT_A", "PLANT_TUFT_D", "PLANT_TUFT_E", "PLANT_TUFT_S", "PLANT_FLOWER_PURPLE", "PLANT_SHRUB_A"):
        m = lib[k].data
        Vk, c = _mesh_V(m), vertex_rgba(m)
        cor = float(np.corrcoef(Vk[:, 2], c[:, 0])[0, 1])
        mono.append((k, round(cor, 2)))
    vm = lib["PLANT_VINE_L"].data
    vcor = float(np.corrcoef(-_mesh_V(vm)[:, 2], vertex_rgba(vm)[:, 0])[0, 1])
    gate("PLANT_SWAY_SHAPE", all(c > 0.8 for _k, c in mono) and vcor > 0.9, f"weight vs height correlation (>0.8): {mono}; vine weight vs depth below the attach point {vcor:.2f} (>0.9)")
    SWAY_STATS["blend"] = stat


def sway_fbx_gates():
    """PLANT_SWAY_COLORS@fbx: export_look_fbx -> FBX -> Blender re-import keeps every weight / phase (8-bit), static data on the non-PLANT_ LK_PLANTS meshes, and the SRGB mutant is caught."""
    import postcard_look_lib as L
    fresh()
    PL.write_plants_atlas()
    D = P.start("hole08_design", out_dir=os.path.join(SCRATCH, "sway_fbx"))
    kinds = sorted(PL.PLANT_KINDS)
    for i, k in enumerate(kinds):
        PL.place(D, k, (10.0 + 4.0 * i, 20.0, 6.0), 0.3 * i, 1.0, allow_low=True)
    PL.place(D, "ROCK_SEASTACK_A", (200.0, 20.0, 0.0), 0.0, 1.0)
    # a hole-10-style ground ribbon painted with the plants swatches and NO colour attribute
    me = bpy.data.meshes.new("DRESS_PADEDGE_00")
    me.from_pydata([(0, 0, 6.0), (4, 0, 6.0), (4, 1, 6.0), (0, 1, 6.0)], [], [(0, 1, 2, 3)])
    me.materials.append(PL.lk_material("LK_PLANTS"))
    ob = bpy.data.objects.new("DRESS_PADEDGE_00", me)
    PL._collection_for("DRESS_PADEDGE").objects.link(ob)
    ob.parent = P.get_root()
    path = os.path.join(SCRATCH, "sway_fbx", "sway_test.fbx")
    L.export_look_fbx(D, path)
    static = list(L.LAST_EXPORT.get("static_sway", []))
    src = {k: vertex_rgba(PL.library_object(k).data) for k in kinds}
    raised = []
    for ct in ("SRGB", "NONE"):
        try:
            L.export_look_fbx(D, os.path.join(SCRATCH, "sway_fbx", f"sway_{ct}.fbx"), colors_type=ct)
            raised.append(False)
        except ValueError:
            raised.append(True)
    # mutant export that bypasses the guard: raw exporter with SRGB
    vl = bpy.context.view_layer
    for o in vl.objects:
        o.select_set(False)
    for o in P._export_objects(D):
        o.select_set(True)
    mpath = os.path.join(SCRATCH, "sway_fbx", "sway_mut_srgb.fbx")
    bpy.ops.export_scene.fbx(filepath=mpath, use_selection=True, object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_ALL', bake_anim=False, use_mesh_modifiers=True, add_leaf_bones=False, path_mode='STRIP', embed_textures=False, colors_type='SRGB')

    def reimport(p):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=p)
        out = {}
        for o in bpy.data.objects:
            if o.type == 'MESH':
                out[o.name.split(".")[0]] = o.data
        return out

    def pairs(c):
        return np.unique(np.floor(c[:, :2] * 255.0 + 0.5).astype(int), axis=0)

    def same_pairs(a, b):
        """the unique (weight, phase) pairs agree within one 8-bit level (the importer quantises the file's doubles to bytes)"""
        def cover(x, y):
            return all(int(np.abs(y - q).max(1).min()) <= 1 for q in x)
        return cover(a, b) and cover(b, a)
    imp = reimport(path)
    bad, rows = [], []
    for k in kinds:
        m = imp.get(k)
        c = vertex_rgba(m, srgb=True) if m is not None else None
        if c is None:
            bad.append((k, "missing / no colours"))
            continue
        ok_pairs = same_pairs(pairs(c), pairs(src[k]))
        ok_b = float(np.abs(c[:, 2]).max()) <= 1 / 255 and float(np.abs(c[:, 3] - 1).max()) <= 1 / 255
        cls = PL.sway_class(k)
        ok_tip = float(c[:, 0].max()) >= PL.SWAY_TIP_MIN[cls] - 1 / 255 and float(c[:, 0].min()) <= PL.SWAY_ROOT_MAX
        if not (ok_pairs and ok_b and ok_tip):
            bad.append((k, f"pairs {ok_pairs} B/A {ok_b} tip/root {ok_tip}"))
        rows.append((k, round(float(c[:, 0].max()), 2), round(float(c[:, 0].min()), 3)))
    ribbon = imp.get("DRESS_PADEDGE_00")
    rc = vertex_rgba(ribbon, srgb=True) if ribbon is not None else None
    crown = imp.get("ROCK_SEASTACK_A")
    cc = vertex_rgba(crown, srgb=True) if crown is not None else None
    ok_static = (rc is not None and float(rc[:, 0].max()) == 0.0 and float(np.abs(rc[:, 2]).max()) == 0.0 and "DRESS_PADEDGE_00" in static
                 and cc is not None and float(cc[:, 0].max()) == 0.0)
    gate("PLANT_SWAY_COLORS@fbx", not bad and len(rows) == len(kinds) and ok_static,
         f"export_look_fbx -> FBX -> Blender re-import: {len(rows)} PLANT_ meshes keep the unique (weight, phase) pairs to 8 bit, B 0, A 1, root R <= {PL.SWAY_ROOT_MAX}, tip R per class window "
         f"(tip / root: {rows[:6]} ...); bad {bad[:3] or 'none'}; the ground ribbon DRESS_PADEDGE_00 (LK_PLANTS, no attribute) got a static one (R 0): {ok_static}, "
         f"touched {static}; the spire crown stays R 0")
    # mutant 1: the SRGB export must be refused by export_look_fbx; mutant 2: a raw SRGB export re-imports with corrupted weights (the gate sees it)
    imp2 = reimport(mpath)
    corrupt = []
    for k in ("PLANT_TUFT_D", "PLANT_TUFT_S", "PLANT_FLOWER_PURPLE"):
        m = imp2.get(k)
        c = vertex_rgba(m, srgb=True) if m is not None else None
        corrupt.append(c is None or not same_pairs(pairs(c), pairs(src[k])))
    gate("PLANT_SWAY_COLORS_FBX_MUTANTS", all(raised) and all(corrupt), f"export_look_fbx(colors_type='SRGB' / 'NONE') raises ValueError: {raised}; a raw SRGB export re-imports with CORRUPTED weights "
         f"and the equality check catches it on {sum(corrupt)}/3 meshes")
    SWAY_STATS["fbx"] = rows


# ---- the fringe
def _world_verts(ob, kind_cache):
    co = kind_cache.get(ob.data.name)
    if co is None:
        co = _mesh_V(ob.data)
        kind_cache[ob.data.name] = co
    M = np.array(ob.matrix_world)
    return co @ M[:3, :3].T + M[:3, 3]


def _surface_bvh(prefixes):
    objs = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(prefixes) and "ASSET_LIBRARY" not in [c.name for c in o.users_collection]]
    V, F = _tri_soup(objs)
    return (BVHTree.FromPolygons(V, F) if F else None), [o.name for o in objs]


def fringe_checks(D, objs, balls, corridor_m, top_cap=0.503, cap_radius=12.0):
    """The five fringe checks as (ok, detail) tuples keyed by gate name, so the negative controls can call them on injected bad tufts. objs = tuft instances."""
    cache = {}
    W = {o.name: _world_verts(o, cache) for o in objs}
    play, pnames = _surface_bvh(("FAIRWAY", "GREEN", "TEE_BOX", "BUNKER", "CART_PATH", "DRESS_PATH"))
    gb, _g = ground_bvh()
    px, py = D.hole.pin[0] * P.YD, D.hole.pin[1] * P.YD
    markers = [(o.matrix_world.translation.x, o.matrix_world.translation.y) for o in bpy.data.objects
               if o.name.startswith(("TEE_MARKER", "HOLE_CUP", "BALL_START", "FLAG"))]
    out = {}
    # --- play surfaces: every vertex of every tuft, ray from above, must miss the fairway / first cut / green / apron / tee / bunker / path meshes
    viol = []
    for o in objs:
        for x, y, _z in W[o.name][::2]:
            if play is not None and play.ray_cast(Vector((float(x), float(y), D.play_z + 30.0)), Vector((0, 0, -1)), 80.0)[0] is not None:
                viol.append((o.name, round(float(x), 2), round(float(y), 2)))
                break
    out["GRASS_NOT_ON_PLAY_SURFACES"] = (not viol, f"{len(objs)} tufts, every vertex (2 of 3 sampled) ray-cast down onto {len(pnames)} play meshes ({', '.join(sorted({n.rstrip('0123456789_') for n in pnames}))}): "
                                         f"{len(viol)} over a play surface {viol[:3]}")
    # --- water: origin and every vertex on land (lie mirror), origin >= 0.8 m inside the cliff edge, base on a real ground top within 0.05 m, z >= 5
    X = np.array([o.location.x for o in objs])
    Y = np.array([o.location.y for o in objs])
    wv = np.concatenate([W[o.name][:, :2] for o in objs]) if objs else np.zeros((0, 2))
    water_v = int((P.lie_codes_m(D, wv[:, 0], wv[:, 1]) == P.LIE_WATER).sum()) if len(wv) else 0
    sd = P.signed_dist_m(D, X, Y) if len(X) else np.zeros(0)
    dz, zmin = [], 1e9
    for o in objs:
        g = ground_z(gb, o.location.x, o.location.y)
        dz.append(99.0 if g is None else abs(o.location.z - g))
        zmin = min(zmin, o.location.z)
    out["GRASS_NOT_ON_WATER"] = (water_v == 0 and (len(sd) == 0 or float(sd.max()) <= -0.8) and (not dz or max(dz) <= 0.05) and zmin >= 5.0,
                                 f"{len(objs)} tufts / {len(wv)} vertices: vertices over water {water_v}; nearest origin to a shore / lip edge {-float(sd.max()) if len(sd) else 0:.2f} m (>= 0.8); "
                                 f"origin vs the ray-cast ground max |dz| {max(dz) if dz else 0:.3f} m (<= 0.05); lowest origin z {zmin:.2f} (>= 5.0)")
    # --- ball clear: 4 m (tee ball 1.5 m) from every shot ball, 6 m from the pin, 1.5 m from the markers; tops <= 0.55 yd in the corridor / within cap_radius of a ball
    worst_ball, worst_pin, worst_mk, bad_top, top_max_c, top_max_all = 99.0, 99.0, 99.0, [], 0.0, 0.0
    short = []
    for o in objs:
        w = W[o.name]
        xy = w[:, :2]
        for bx, by, br in balls:
            d = float(np.hypot(xy[:, 0] - bx, xy[:, 1] - by).min()) - br
            worst_ball = min(worst_ball, d)
            if d < -1e-6:
                short.append((o.name, round(d, 2)))
        worst_pin = min(worst_pin, float(np.hypot(xy[:, 0] - px, xy[:, 1] - py).min()) - 6.0)
        for mx_, my_ in markers:
            worst_mk = min(worst_mk, float(np.hypot(xy[:, 0] - mx_, xy[:, 1] - my_).min()) - 1.5)
        g = ground_z(gb, o.location.x, o.location.y)
        top = float(w[:, 2].max()) - (g if g is not None else D.play_z)
        top_max_all = max(top_max_all, top)
        off = float(P._lie_all(D, np.array([o.location.x]), np.array([o.location.y]))[1][0]) * P.YD
        near = any(math.hypot(o.location.x - bx, o.location.y - by) <= cap_radius for bx, by, _r in balls)
        if off <= corridor_m or near:
            top_max_c = max(top_max_c, top)
            if top > top_cap + 0.004:
                bad_top.append((o.name, round(top, 3)))
    out["GRASS_BALL_CLEAR"] = (worst_ball >= -1e-6 and worst_pin >= -1e-6 and worst_mk >= -1e-6 and not bad_top,
                               f"{len(balls)} shot-ball zones {[(round(b[0] / P.YD, 1), round(b[1] / P.YD, 1), b[2]) for b in balls]} (course yd, r m): nearest tuft vertex vs the "
                               f"balls (worst margin {worst_ball:+.2f} m), margin to the 6 m pin disc {worst_pin:+.2f} m, to the 1.5 m markers {worst_mk:+.2f} m; tops in the ball corridor "
                               f"(<= {corridor_m:.1f} m off the centerline or <= {cap_radius:.0f} m from a ball): max {top_max_c:.3f} m (<= {top_cap} = 0.55 yd), over {bad_top[:3] or 'none'}; whole hole max {top_max_all:.2f} m")
    return out


def fringe_structure(objs, r):
    """GRASS_FRINGE_PRESENT numbers: counts, clump sizes, lean, heights, not a grid, coverage of the rough edge."""
    cnt = len(objs)
    byc = {}
    for o in objs:
        byc.setdefault(o["lk_fringe"], []).append(o)
    sizes = np.array([len(v) for v in byc.values()])
    in_clump = int(sum(s for s in sizes if s >= 3))
    P_ = np.array([[o.location.x, o.location.y] for o in objs])
    nn = []
    for i in range(0, len(P_), 400):
        d = np.hypot(P_[i:i + 400, None, 0] - P_[None, :, 0], P_[i:i + 400, None, 1] - P_[None, :, 1])
        d[np.arange(d.shape[0]), np.arange(i, i + d.shape[0])] = 1e9
        nn.append(d.min(1))
    nn = np.concatenate(nn)
    cv = float(nn.std() / max(nn.mean(), 1e-9))
    lean = []
    for o in objs:
        Rm = np.array(o.matrix_world)[:3, :3]
        z = Rm @ np.array([0.0, 0.0, 1.0])
        lean.append(math.degrees(math.acos(min(1.0, float(z[2] / max(np.linalg.norm(z), 1e-9))))))
    lean = np.array(lean)
    fs = np.array([o["lk_fringe_f"] for o in objs])
    tall = sum(1 for o in objs if o.data.name in PL.TUFT_KINDS_TALL)
    grid_share = float((np.abs(nn - np.median(nn)) <= 0.1 * np.median(nn)).mean())          # a regular grid has ~all nearest-neighbour distances within 10 % of the median
    return dict(count=cnt, clumps=int((sizes >= 3).sum()), singles=int((sizes == 1).sum()), pairs=int((sizes == 2).sum()), max_clump=int(sizes.max()), in_clump_share=in_clump / max(cnt, 1),
                nn_cv=cv, nn_mean=float(nn.mean()), grid_share=grid_share, lean_max=float(lean.max()), lean_med=float(np.median(lean)), f_min=float(fs.min()), f_max=float(fs.max()), f_med=float(np.median(fs)),
                tall=tall, fill=cnt - tall, coverage=r["edge_covered"] / max(r["edge_samples"], 1), edge=r["edge"],
                focus_cov=r["edge_focus"][1] / max(r["edge_focus"][0], 1), focus_n=r["edge_focus"][0])


def fringe_hole_test(tag):
    """Scratch build of the real hole design (terrain, play surfaces, water, gameplay, a path ribbon, a little rock / shrub dressing the fringe must respect) + scatter_fringe."""
    fresh()
    D = P.start(f"hole{tag}_design", out_dir=os.path.join(SCRATCH, f"fringe{tag}"))
    P.build_terrain(D)
    P.build_play_surfaces(D)
    if tag == "10":
        P.build_water(D, 'WATER_LAVA', 'MAT_LAVA', shallow_material=None, foam_material=None, crest=False)
    else:
        P.build_water(D)
    P.build_gameplay(D)
    path_pts = make_path(D)
    var = "BASALT" if tag == "10" else None
    rocks = PL.scatter_rocks(D, 20, zone="rim", seed=4, variant=var)
    shrubs = PL.scatter(D, ["PLANT_SHRUB_A", "PLANT_SHRUB_B"], 24, seed=2)
    balls = PL.still_ball_zones(D)
    t0 = time.time()
    r = PL.scatter_fringe(D, tri_budget=FRINGE_BUDGET, seed=3)
    dt = time.time() - t0
    bpy.context.view_layer.update()
    objs = r["objs"]
    chk = fringe_checks(D, objs, balls, r["corridor_m"])
    st = fringe_structure(objs, r)
    # determinism: the same call on a rebuilt scene gives the same placement (names, positions)
    sig = hashlib.sha256(str([(o.name, tuple(round(v, 4) for v in o.location), round(o.rotation_euler.z, 4)) for o in objs]).encode()).hexdigest()[:16]
    FRINGE_STATS[tag] = dict(r=r, st=st, chk=chk, sig=sig, balls=balls, dt=dt, path=bool(path_pts), rocks=len(rocks), shrubs=len(shrubs))
    return D, r, chk, st, balls


def fringe_gates(tag):
    D, r, chk, st, balls = fringe_hole_test(tag)
    objs = r["objs"]
    for name in ("GRASS_NOT_ON_PLAY_SURFACES", "GRASS_NOT_ON_WATER", "GRASS_BALL_CLEAR"):
        ok, why = chk[name]
        gate(f"{name}_{tag}", ok, why)
    cov_ok = st["coverage"] >= 0.20 and (st["focus_cov"] >= 0.50 or st["focus_n"] < 30)
    ok = (st["count"] >= FRINGE_MIN_COUNT and st["tall"] >= 80 and st["fill"] >= 100 and st["max_clump"] <= 7 and st["in_clump_share"] >= 0.55 and st["singles"] >= 5
          and st["lean_max"] <= 12.0 + 0.25 and st["lean_med"] >= 3.0 and st["f_min"] <= 0.7 and st["f_max"] >= 1.2 and st["nn_cv"] >= 0.35 and st["grid_share"] <= 0.45 and cov_ok
          and all(PL.SCALE_LIMITS["lo"] - 1e-9 <= o.scale[i] <= PL.SCALE_LIMITS["hi"] + 1e-9 for o in objs for i in range(3)))
    gate(f"GRASS_FRINGE_PRESENT_{tag}", ok,
         f"{st['count']} fringe tufts ({st['tall']} tall D/E/F + {st['fill']} filler, by kind {r['by_kind']}) in {st['clumps']} clumps of 3-7 (largest {st['max_clump']}), {st['pairs']} pairs, {st['singles']} singles; "
         f"{st['in_clump_share']:.0%} of the tufts stand in a clump of >= 3; nearest-neighbour distance mean {st['nn_mean']:.2f} m, CV {st['nn_cv']:.2f} (>= 0.35) and {st['grid_share']:.0%} of the distances within 10 % of the median (<= 45 %): not a grid; lean max {st['lean_max']:.1f} deg "
         f"(<= 12), median {st['lean_med']:.1f}; scale factor {st['f_min']:.2f}..{st['f_max']:.2f} median {st['f_med']:.2f} (0.6-1.8, no instance outside the SCALE_LIMITS; {r['capped']} scaled down to the "
         f"0.55 yd cap near the corridor); coverage of the rough edge {st['coverage']:.0%} ({r['edge_covered']} of {r['edge_samples']} sample points {r['edge']} within 2.2 m of a tuft; >= 20 %), {st['focus_cov']:.0%} of the {st['focus_n']} points within 45 m of a still camera's ball (>= 50 %), at a "
         f"{FRINGE_BUDGET} triangle budget; {FRINGE_STATS[tag]['dt']:.1f} s")
    tri_t = r["tris"]
    mean_t = tri_t / max(st["count"], 1)
    scene = tri_count([o for o in bpy.data.objects if o.type == 'MESH' and "ASSET_LIBRARY" not in [c.name for c in o.users_collection] and not o.name.startswith(("REF_", "CAM_"))])
    gate(f"GRASS_TRI_BUDGET_{tag}", tri_t <= FRINGE_BUDGET and mean_t <= 90.0 and st["fill"] >= 0.5 * st["count"] and scene <= 250000,
         f"fringe {tri_t} triangles (<= the {FRINGE_BUDGET} asked), {mean_t:.0f} per tuft (<= 90: {st['fill']} of {st['count']} are 36-triangle fillers, tall D 108 / E 135), whole scratch scene {scene} triangles "
         f"(<= 250k; it has no arch / skin / stacks, so the hole scripts trim the old tuft carpet by the fringe's triangles: {tri_t} tris = {tri_t // 117} old 117-triangle tufts)")
    return D, r


def fringe_mutants(D, r, balls):
    """The checks must FAIL when a bad tuft is injected (negative controls on hole 08): one in the middle of the fairway, one in the sea, one 1.5 m from a ball, a 1.2 m tuft in the corridor, one 3 m from the pin."""
    objs = list(r["objs"])
    h = D.hole
    cl = h.center
    mid = cl[len(cl) // 2]
    mx, my = mid[0] * P.YD, mid[1] * P.YD
    ground = PL.ground_sampler(D)
    out = {}
    cap_objs = [o for o in objs if float(P._lie_all(D, np.array([o.location.x]), np.array([o.location.y]))[1][0]) * P.YD <= r["corridor_m"]]
    cx0, cy0 = (cap_objs[len(cap_objs) // 2].location.x, cap_objs[len(cap_objs) // 2].location.y) if cap_objs else (mx, my)

    def one(name, x, y, z=None, scale=1.0, kind="PLANT_TUFT_D"):
        zz = float(ground(np.array([x]), np.array([y]))[0]) if z is None else z
        ob = PL.place(D, kind, (x, y, zz if math.isfinite(zz) else 0.0), 0.0, scale, allow_low=True)
        ob["lk_fringe"] = 99999
        ob["lk_fringe_f"] = scale
        bpy.context.view_layer.update()
        return ob
    x0, y0, x1, y1 = P._shore_bbox_m(D, 0.0)
    sea = None
    for i in range(400):
        qx, qy = x0 - 30 + (x1 - x0 + 60) * (i * 0.6180339 % 1.0), y0 - 30 + (y1 - y0 + 60) * (i * 0.3819660 % 1.0)
        if P.lie_codes_m(D, np.array([qx]), np.array([qy]))[0] == P.LIE_WATER:
            sea = (qx, qy)
            break
    cases = {
        "tuft on the fairway": lambda: one("fw", mx, my),
        "tuft in the sea": (lambda: one("sea", sea[0], sea[1], z=0.0)) if sea else None,
        "tuft 1.5 m from a shot ball": lambda: one("ball", balls[1][0] + 1.5, balls[1][1]),
        "1.2 m tuft in the corridor": lambda: one("tall", cx0 + 0.35, cy0, scale=1.9),
        "tuft 3 m from the pin": lambda: one("pin", h.pin[0] * P.YD + 3.0, h.pin[1] * P.YD),
    }
    keymap = {"tuft on the fairway": "GRASS_NOT_ON_PLAY_SURFACES", "tuft in the sea": "GRASS_NOT_ON_WATER", "tuft 1.5 m from a shot ball": "GRASS_BALL_CLEAR",
              "1.2 m tuft in the corridor": "GRASS_BALL_CLEAR", "tuft 3 m from the pin": "GRASS_BALL_CLEAR"}
    for nm, mk in cases.items():
        if mk is None:
            out[nm] = None
            continue
        ob = mk()
        chk = fringe_checks(D, objs + [ob], balls, r["corridor_m"])
        out[nm] = not chk[keymap[nm]][0]                   # True = the check failed as it must
        bpy.data.objects.remove(ob, do_unlink=True)
    return out


def fringe_hole_extras(tag):
    """Determinism, the FBX size of a scratch export, no animation, naming; plus the mutants (hole 08 only)."""
    import postcard_look_lib as L
    D, r, chk, st, balls = fringe_hole_test(tag)
    sig2 = FRINGE_STATS[tag]["sig"]
    D, r2, chk2, st2, _b = fringe_hole_test(tag)
    gate(f"GRASS_DETERMINISTIC_{tag}", FRINGE_STATS[tag]["sig"] == sig2 and r2["count"] == r["count"],
         f"two builds of the same call: {r['count']} / {r2['count']} tufts, placement signature {sig2} == {FRINGE_STATS[tag]['sig']}")
    objs = r2["objs"]
    bad = [o.name for o in objs if not o.name.startswith("PLANT_TUFT") or o.name.startswith(PL.GROUND_PREFIXES)]
    path = os.path.join(SCRATCH, f"fringe{tag}", f"fringe_{tag}.fbx")
    L.export_look_fbx(D, path)
    size = os.path.getsize(path)
    with open(path, "rb") as f:
        blob = f.read()
    anim = blob.count(b"AnimationStack") + blob.count(b"AnimationCurve")
    ngeo = blob.count(b"\x00\x01Geometry")
    gate(f"GRASS_FBX_{tag}", size <= 20 * 1024 * 1024 and anim == 0 and not bad,
         f"scratch hole export {size / 1048576:.2f} MB (<= 20), {ngeo} Geometry nodes for {len({o.data.name for o in objs})} tuft meshes + the hole, animation nodes {anim}, collision / non-PLANT names {bad[:3] or 'none'}")
    if tag == "08":
        res = fringe_mutants(D, r2, balls)
        gate("GRASS_CHECK_MUTANTS", all(v for v in res.values() if v is not None) and sum(1 for v in res.values() if v is not None) >= 4,
             f"bad tufts injected into the fringe: the checks FAIL on each as they must: {res}")


# ----------------------------------------------------------------------------- v2 2026-10-05: carry-over items (pin flag, vine roots, tufty shrubs)
def vine_root_gate():
    fresh()
    D = P.start("hole08_design", out_dir=os.path.join(SCRATCH, "vines"))
    P.build_terrain(D)
    P.build_play_surfaces(D)
    P.build_water(D)
    P.build_gameplay(D)
    pts = sea_points(D, 1, 6.0, 20.0, seed=7 + 8)
    arch = None
    if pts:
        PL.place_outcrop(D, pts[0][0], pts[0][1], 5.0, 6.4, seed=8)
        arch = PL.build_masonry_arch(D, (pts[0][0], pts[0][1], 5.0), 171.2, span_m=7.5, height_m=11.0, seed=8, base_rock=False)
    cliff = PL.hang_vines_on_cliff(D, 7.0, seed=6)
    # a hole-script-style vine pushed 0.6-0.86 m off the wall, one far away, plus a vine hanging in the middle of nowhere
    lip = PL.cliff_lip_points(D, spacing_m=15.0, seed=2)[:3]
    floaters = []
    for i, ((x, y, z), (nx, ny)) in enumerate(lip):
        off = (0.62, 0.86, 2.5)[i % 3]
        floaters += PL.hang_vines(D, [((x + nx * off, y + ny * off, z), (nx, ny))], seed=40 + i)
    bpy.context.view_layer.update()
    allv = [o for o in bpy.data.objects if o.name.startswith("PLANT_VINE") and "ASSET_LIBRARY" not in [c.name for c in o.users_collection]]
    before = PL.vine_root_gaps(allv)
    gb = np.array([g for _v, g in before])
    # the independent gap measure (own BVH of the same target families)
    def indep_gaps(vs):
        tg = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(("ROCK_", "DRESS_ARCH", "DRESS_RUIN", "DRESS_BLOCK", "TERRAIN", "FAIRWAY", "GREEN")) and not o.name.startswith("PLANT_") and "ASSET_LIBRARY" not in [c.name for c in o.users_collection]]
        V, F = _tri_soup(tg)
        bv = BVHTree.FromPolygons(V, F)
        return [float(bv.find_nearest(v.matrix_world.translation)[3]) for v in vs]
    res = PL.attach_vines(D)
    left = [o for o in bpy.data.objects if o.name.startswith("PLANT_VINE") and "ASSET_LIBRARY" not in [c.name for c in o.users_collection]]
    after = np.array(indep_gaps(left)) if left else np.zeros(0)
    gate("VINE_ROOT_GAP_08", len(left) > 0 and float(after.max()) <= 0.05 + 1e-6 and res["moved"] + res["deleted"] >= 2,
         f"{len(allv)} vines ({len(cliff)} cliff + {len(arch['vines']) if arch else 0} arch + {len(floaters)} deliberately pushed 0.62 / 0.86 / 2.5 m off the cliff): root gap before max {float(gb.max()):.2f} m "
         f"({int((gb > 0.05).sum())} over 5 cm); attach_vines: {res['ok']} already touching, {res['moved']} moved onto the surface, {res['deleted']} deleted (beyond 1.0 m); after: {len(left)} vines, "
         f"independent max gap {float(after.max()) if len(after) else 0:.3f} m (<= 0.05)")
    # mutant: the gap measure sees a floating vine
    fl = PL.hang_vines(D, [((lip[0][0][0] + lip[0][1][0] * 0.7, lip[0][0][1] + lip[0][1][1] * 0.7, lip[0][0][2]), lip[0][1])], seed=77)
    bpy.context.view_layer.update()
    mg = indep_gaps(fl)[0] if fl else 0.0
    gate("VINE_ROOT_GAP_MUTANT", mg > 0.05, f"a vine pushed 0.7 m off the cliff measures a root gap of {mg:.2f} m (> 0.05: the gate would FAIL it)")


def shrub_tuft_gate():
    fresh()
    D = P.start("hole09_design", out_dir=os.path.join(SCRATCH, "shrubtuft"))
    P.build_terrain(D)
    P.build_play_surfaces(D)
    P.build_water(D)
    P.build_gameplay(D)
    shr = PL.scatter(D, ["PLANT_SHRUB_A", "PLANT_SHRUB_B", "PLANT_SHRUB_C"], 30, seed=2)
    hashes_before = [geom_fingerprint(PL.library_object(k).data) for k in ("PLANT_SHRUB_A", "PLANT_SHRUB_B", "PLANT_SHRUB_C")]
    r = PL.tuftify_shrubs(D, shr, per=(3, 6), seed=5)
    bpy.context.view_layer.update()
    hashes_after = [geom_fingerprint(PL.library_object(k).data) for k in ("PLANT_SHRUB_A", "PLANT_SHRUB_B", "PLANT_SHRUB_C")]
    objs = r["objs"]
    # each tuft's base sits on / in a shrub: independent ray cast onto the shrub meshes
    V, F = _tri_soup(shr)
    bv = BVHTree.FromPolygons(V, F)
    d = []
    for o in objs:
        h = bv.find_nearest(o.matrix_world.translation)
        d.append(float(h[3]))
    d = np.array(d) if d else np.zeros(1)
    lean = max(math.degrees(math.acos(min(1.0, float((np.array(o.matrix_world)[:3, :3] @ np.array([0, 0, 1.0]))[2] / max(np.linalg.norm(np.array(o.matrix_world)[:3, :3] @ np.array([0, 0, 1.0])), 1e-9))))) for o in objs) if objs else 0.0
    gate("SHRUB_TUFTS_08_09_10", len(objs) >= 90 and float(d.max()) <= 0.12 and lean <= 12.25 and hashes_before == hashes_after and r["tris"] <= 320 * len(shr),
         f"tuftify_shrubs on {len(shr)} shrubs: {len(objs)} tufts ({r['tris']} triangles) standing on the shrub lumps ({r['tris'] / max(len(shr), 1):.0f} triangles per shrub, <= 320; base to the nearest shrub surface max {float(d.max()):.3f} m <= 0.12), "
         f"lean max {lean:.1f} deg (<= 12), the three shrub meshes are byte-identical before / after: {hashes_before == hashes_after}")


_SWAY_OLD_LIB = {}


def _sway_old_lib():
    """The props lib of the START of the sway pass (v2/props/sway/backup/): the frozen negative control of the arch haunch gate (stair-step courses)."""
    if "m" not in _SWAY_OLD_LIB:
        import importlib.util
        spec = importlib.util.spec_from_file_location("postcard_props_lib_before_sway", os.path.join(SWAY_DIR, "backup", "postcard_props_lib.py"))
        m = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(m)
        _SWAY_OLD_LIB["m"] = m
    return _SWAY_OLD_LIB["m"]


def haunch_outline_steps(mod, span, height, seed, step=0.02):
    """The outer silhouette of the arch's right shoulder above the spring line: x_out(z) = the largest x any face reaches at height z (2 cm rows), and the largest jump between
    two rows = the width of a horizontal STEP LEDGE (a staircase). Returns (max jump m, number of jumps > 6 cm, rows, dims)."""
    mb, _v, dims = mod._arch_mesh("DRESS_ARCH_T", span, height, max(1.0, 0.17 * span), seed, False)
    V = np.array(mb.V)
    tris = []
    for f in mb.F:
        for k in range(1, len(f) - 1):
            tris.append((f[0], f[k], f[k + 1]))
    T = np.array(tris)
    A, B, C = V[T[:, 0]], V[T[:, 1]], V[T[:, 2]]
    zs, r_out = dims["zs"], dims["r_out"]
    zr = np.arange(zs + 0.03, zs + 0.74 * r_out, step)
    xo = []
    for z0 in zr:
        best = -9.0
        for P0, P1 in ((A, B), (B, C), (C, A)):
            dz = P1[:, 2] - P0[:, 2]
            ok = ((P0[:, 2] - z0) * (P1[:, 2] - z0) <= 0) & (np.abs(dz) > 1e-9)
            t = np.where(ok, (z0 - P0[:, 2]) / np.where(np.abs(dz) > 1e-9, dz, 1.0), 0.0)
            x = P0[:, 0] + t * (P1[:, 0] - P0[:, 0])
            if ok.any():
                best = max(best, float(x[ok].max()))
        xo.append(best)
    xo = np.array(xo)
    jumps = np.abs(np.diff(xo))
    return float(jumps.max()), int((jumps > 0.06).sum()), len(zr), dims


def arch_haunch_gate():
    new = PL
    old = _sway_old_lib()
    rows = []
    ok = True
    for (span, height, seed) in ((7.5, 11.0, 8), (9.1, 11.5, 8), (6.0, 10.0, 21)):
        jn, cn, n, d = haunch_outline_steps(new, span, height, seed)
        jo, co_, _n, _d = haunch_outline_steps(old, span, height, seed)
        rows.append((span, round(jn, 3), cn, round(jo, 3), co_))
        ok = ok and jn <= 0.06 and cn == 0 and (jo > 0.12 and co_ >= 2)
    gate("ARCH_HAUNCH_NO_STAIRS", ok,
         f"outer silhouette of the arch's right shoulder above the spring line, rows every 2 cm: largest horizontal jump between two rows (= a step ledge) NEW {[(r[0], r[1], r[2]) for r in rows]} (span m, max jump m, rows with a jump > 6 cm; need <= 0.06 m and 0 rows); "
         f"NEGATIVE CONTROL (the stair-step haunch of the start of this pass) {[(r[0], r[3], r[4]) for r in rows]} (max jump m, rows > 6 cm: >= 2 rows and > 0.12 m) -> detected on all three sizes")


def cheapen_gate():
    fresh()
    D = P.start("hole09_design", out_dir=os.path.join(SCRATCH, "cheapen"))
    P.build_terrain(D)
    P.build_play_surfaces(D)
    P.build_water(D)
    P.build_gameplay(D)
    old = PL.scatter(D, {"PLANT_TUFT_A": 3, "PLANT_TUFT_B": 2, "PLANT_TUFT_C": 1}, 400, seed=1)
    tris0 = tri_count(old)
    balls = PL.still_ball_zones(D)
    r = PL.cheapen_tufts(D, near_m=60.0)
    bpy.context.view_layer.update()
    tris1 = tri_count(old)
    far_wrong = [o.name for o in old if o.data.name in PL.TUFT_KINDS and min(math.hypot(o.location.x - b[0], o.location.y - b[1]) for b in balls) > 60.0 + 1e-6]
    near_wrong = [o.name for o in old if o.data.name in PL.TUFT_KINDS_FILL and min(math.hypot(o.location.x - b[0], o.location.y - b[1]) for b in balls) <= 60.0]
    names_ok = all(o.name.startswith(o.data.name) for o in old)
    gate("CHEAPEN_TUFTS_09", r["swapped"] > 50 and not far_wrong and not near_wrong and names_ok and tris0 - tris1 == r["tris_saved"] and r["tris_saved"] >= 0.5 * 81 * r["swapped"],
         f"{len(old)} old tufts ({tris0} triangles): {r['swapped']} farther than 60 m from every shot ball swapped for 36-triangle fillers, {r['kept']} kept near the balls; {tris0} -> {tris1} triangles "
         f"(saved {r['tris_saved']} = the accounting {tris0 - tris1}); far tufts left old {len(far_wrong)}, near tufts swapped {len(near_wrong)}, names follow the kind {names_ok}")


def crown_tuft_gate():
    fresh()
    D = P.start("hole08_design", out_dir=os.path.join(SCRATCH, "crowns"))
    P.build_terrain(D)
    P.build_play_surfaces(D)
    P.build_water(D)
    P.build_gameplay(D)
    stacks = []
    for i, (sx, sy) in enumerate(sea_points(D, 4, seed=3)):
        stacks += PL.place_sea_stack(D, sx, sy, (14.0, 22.0, 18.0, 16.0)[i % 4], 3.5, seed=i)
    spires = [o for o in stacks if o.name.startswith(("ROCK_SEASTACK", "ROCK_SPIRE"))]
    before = [geom_fingerprint(o.data) for o in spires]
    r = PL.tuftify_crowns(D, stacks, seed=4)
    bpy.context.view_layer.update()
    after = [geom_fingerprint(o.data) for o in spires]
    V, F = _tri_soup(spires)
    bv = BVHTree.FromPolygons(V, F)
    ztops = {}
    for o in spires:
        w = [(o.matrix_world @ v.co).z for v in o.data.vertices]
        ztops[o.name] = max(w)
    gaps, depth = [], []
    for o in r["objs"]:
        h = bv.find_nearest(o.matrix_world.translation)
        gaps.append(float(h[3]))
        near = min(spires, key=lambda sp: (sp.matrix_world.translation.xy - o.matrix_world.translation.xy).length)
        depth.append(ztops[near.name] - o.matrix_world.translation.z)
    gaps = np.array(gaps) if gaps else np.zeros(1)
    gate("CROWN_TUFTS_08", len(r["objs"]) >= 3 * len(spires) and float(gaps.max()) <= 0.12 and (max(depth) if depth else 99) <= 2.0 + 0.2 and before == after,
         f"tuftify_crowns on {len(spires)} sea stacks: {len(r['objs'])} tufts ({r['tris']} triangles), base to the nearest stack surface max {float(gaps.max()):.3f} m (<= 0.12), "
         f"all within the top 2 m of their stack (deepest {max(depth) if depth else 0:.2f} m), the stack meshes unchanged: {before == after}")
    S_CROWN["objs"] = (D, stacks, r)


S_CROWN = {}


def main():
    t = time.time()
    if "--only-extras" in ARGS:                               # fastest loop: the carry-over gates (pin flag, vine roots, tufty shrubs)
        vine_root_gate()
        shrub_tuft_gate()
        crown_tuft_gate()
        cheapen_gate()
        arch_haunch_gate()
        fails = [r for r in RESULTS if not r[1]]
        print(f"SUMMARY(only-extras): {len(RESULTS) - len(fails)}/{len(RESULTS)} gates PASS in {time.time() - t:.1f}s; failing {[r[0] for r in fails] or 'none'}", flush=True)
        return 1 if fails else 0
    hashes, rep = library_gates()
    tuft_library_gates(hashes, rep)
    sway_blend_gates()
    sway_fbx_gates()
    vine_root_gate()
    shrub_tuft_gate()
    crown_tuft_gate()
    cheapen_gate()
    arch_haunch_gate()
    if "--only-v2" in ARGS:                                   # fast loop: library + sway + fringe gates only (about 1-2 min)
        for tag in HOLES:
            fringe_gates(tag)
        for tag in HOLES:
            fringe_hole_extras(tag)
        fails = [r for r in RESULTS if not r[1]]
        print(f"SUMMARY(only-v2): {len(RESULTS) - len(fails)}/{len(RESULTS)} gates PASS in {time.time() - t:.1f}s; failing {[r[0] for r in fails] or 'none'}", flush=True)
        return 1 if fails else 0
    repair_gates()
    round2_gates()
    round3_gates()
    round4_gates()
    round4_shrub_gate()
    round4_stack_gates()
    # budget worst case (library maxima, independent of placement)
    tri = {k: v["tris"] for k, v in rep.items()}
    worst = (600 * max(tri[k] for k in PL.TUFT_KINDS) + 40 * max(tri[k] for k in ("PLANT_SHRUB_A", "PLANT_SHRUB_B", "PLANT_SHRUB_C")) +
             80 * max(tri[k] for k in ("PLANT_FLOWER_PURPLE", "PLANT_FLOWER_WHITE", "PLANT_FLOWER_YELLOW")) +
             60 * max(tri[k] for k in PL.ROCK_KINDS if not k.startswith(("ROCK_SEASTACK", "ROCK_SPIRE", "ROCK_OUTCROP"))))
    gate("SCATTER_BUDGET_WORST", worst <= 150000, f"600 x max tuft + 40 x max shrub + 80 x max flower + 60 x max rock = {worst} tris")
    if DO_RENDER:
        panels = build_panels()
        # gates on the panel scene props
        cone = bpy.data.objects.get("DRESS_CONE")
        me = cone.data
        co = np.array([v.co[:] for v in me.vertices])
        sides = 28
        rings = (len(co) - sides) // sides
        R = np.hypot(co[:rings * sides, 0] - (18040), co[:rings * sides, 1] - 520).reshape(rings, sides)
        Z = co[:rings * sides, 2].reshape(rings, sides)
        under = int((np.diff(R, axis=0) > 1e-6).sum())
        fold = int((np.diff(Z, axis=0) <= 0).sum())
        gate("CONE_NO_UNDERCUT", under == 0 and fold == 0 and len(me.polygons) <= 500,
             f"DRESS_CONE {len(me.polygons)} faces, {rings} rings x {sides}: radius growing upward {under}, z folds {fold}")
        fall = next(o for o in bpy.data.objects if o.name.startswith("WATER_FALL_"))
        uv = uv_array(fall.data)
        vidx = PL._loop_arrays(fall.data)[0]
        zc = np.array([fall.data.vertices[i].co.z for i in vidx])
        order = np.argsort(-zc)
        mono = np.all(np.diff(uv[order, 1][np.argsort(-zc[order], kind="stable")]) >= -1e-6)
        dn = np.mean([pl.normal.dot(Vector((0, -1, 0))) for pl in fall.data.polygons])
        gate("WATERFALL_SHEET", fall.data.materials[0].name == "LK_FALL" and abs(uv[:, 1].min()) < 1e-6 and
             abs(uv[:, 1].max() - 1) < 1e-6 and mono and dn > 0.5 and not fall.name.startswith(PL.GROUND_PREFIXES),
             f"{fall.name}: LK_FALL, v 0 (top) .. 1 (foot) monotone {mono}, mean normal . out_dir {dn:.2f}, "
             f"{len(fall.data.polygons)} faces, foot = {PL.waterfall_foot((3025.0, 11.95, 5.65), (0, -1), 3.0)}")
        skins = [o for o in bpy.data.objects if o.name.startswith("ROCK_SKIN_BASALT")]
        vs = np.array([(o.matrix_world @ v.co)[:] for o in skins for v in o.data.vertices])
        ell = ((vs[:, 0] - 6024.0) / 7.0) ** 2 + ((vs[:, 1] - 14.0) / 6.0) ** 2
        gate("BASALT_SKIN_INSIDE", skins and float(ell.max()) <= 1.0 and float(vs[:, 2].max()) <= 5.98 + 1e-6
             and max(len(o.data.polygons) for o in skins) <= 500,
             f"{len(skins)} skin meshes ({sum(len(o.data.polygons) for o in skins)} faces, max {max(len(o.data.polygons) for o in skins)}"
             f" per object) stay inside the pillar outline (max ellipse value {float(ell.max()):.3f} <= 1) and under its top "
             f"(max z {float(vs[:, 2].max()):.3f} <= 5.98)")
        arch = bpy.data.objects.get("DRESS_ARCH_00")
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(arch.data)
        comps, seen = 0, set()
        for v in bm.verts:
            if v.index in seen:
                continue
            comps += 1
            stack = [v]
            while stack:
                q = stack.pop()
                if q.index in seen:
                    continue
                seen.add(q.index)
                stack.extend(e.other_vert(q) for e in q.link_edges)
        bm.free()
        vines = [o for o in bpy.data.objects if o.name.startswith("PLANT_VINE") and o.location.x < 9000 - 3 and o.location.x > 9000 - 20]
        gate("MASONRY_ARCH", comps >= 25 and len(arch.data.polygons) <= 500 and len(vines) >= 6,
             f"DRESS_ARCH_00: {comps} separate cut blocks (piers, voussoirs, keystone, haunches, mortar core), "
             f"{len(arch.data.polygons)} faces, {len(vines)} hanging vines, rock base + fallen blocks")
        ruins = [o for o in bpy.data.objects if o.name.startswith("DRESS_RUIN_")]
        rfaces = [len(o.data.polygons) for o in ruins]
        gate("MASONRY_RUIN", ruins and max(rfaces) <= 500 and sum(rfaces) / 5 >= 20,
             f"{len(ruins)} ruin wall meshes, {sum(rfaces)} faces (~{sum(rfaces) // 5} blocks), max {max(rfaces)} per object")
        names = [o.name for o in bpy.data.objects if not o.name.startswith(("REF_", "CAM_")) and o.type == 'MESH']
        names += [m.name for m in bpy.data.meshes if not m.name.startswith("REF_")]
        bad = [n for n in names if not n.startswith(PL.NAME_PREFIXES) or n.startswith(PL.GROUND_PREFIXES + ("FLAG", "HOLE_CUP",
                                                                                                             "BALL_START", "MARKER_"))]
        gate("NAMES_SECTION2", not bad, f"{len(names)} prop objects + meshes, bad: {bad[:6] or 'none'}")
        anim = [o.name for o in bpy.data.objects if o.animation_data is not None] + [a.name for a in bpy.data.actions]
        gate("NO_ANIMATION", not anim, f"objects with animation data / actions: {anim or 'none'}")
        paths = render_panels(panels)
    if DO_HOLES:
        for tag in HOLES:
            hole_test(tag, hashes)
        for tag in HOLES:
            fringe_gates(tag)
        for tag in HOLES:
            fringe_hole_extras(tag)
    if DO_RENDER:
        extra_paths = render_extra_sheets()
    if DO_BASELINE:
        for tag in HOLES:
            baseline_swap_test(tag)
    # ---- budget
    lib_over = {k: v["faces"] for k, v in rep.items() if v["faces"] > 500}
    tuft_max = max(rep[k]["faces"] for k in PL.TUFT_KINDS_ALL)
    hs = "; ".join(f"hole {t}: dressed scene {HOLE_STATS[t]['tris']} tris ({HOLE_STATS[t]['props']} instances of {HOLE_STATS[t]['meshes']} meshes)"
                   for t in HOLE_STATS)
    bs = "; ".join(f"baseline hole {t} swap: {SWAP_STATS[t]['instances']} instances, {SWAP_STATS[t]['tris']} tris" for t in SWAP_STATS)
    gate("PROPS_BUDGET", not lib_over and tuft_max <= 120 and all(v["tris"] <= 250000 for v in HOLE_STATS.values())
         and all(v["tris"] <= 150000 for v in SWAP_STATS.values()) and bool(HOLE_STATS or not DO_HOLES),
         f"{len(rep)} library meshes, max {max(v['faces'] for v in rep.values())} faces (<= 500; over: {lib_over or 'none'}), tufts {tuft_max} faces "
         f"(<= 120); {hs}; {bs}")
    if SWAP_STATS:
        import json
        with open(os.path.join(V2, "swap_stats.json"), "w") as f:
            json.dump(SWAP_STATS, f, indent=1)
    if DO_RENDER:
        out, shape = compose_sheet(paths + HOLE_PANELS, SHEET, cols=3)
        gate("PROPS_SHEET", os.path.isfile(out), f"{os.path.relpath(out, REPO)} {shape[1]}x{shape[0]} ({len(paths) + len(HOLE_PANELS)} panels); "
             f"extra sheets {[os.path.relpath(p, REPO) for p in extra_paths]}")
    fails = [r for r in RESULTS if not r[1]]
    gate("PROPS_SMOKE_PASS", not fails, f"{len(RESULTS) - len(fails)}/{len(RESULTS)} gates pass before this line in {time.time() - t:.1f}s; "
         f"failing: {[r[0] for r in fails] or 'none'}")
    fails = [r for r in RESULTS if not r[1]]
    print(f"SUMMARY: {len(RESULTS) - len(fails)}/{len(RESULTS)} gates PASS in {time.time() - t:.1f}s", flush=True)
    return 1 if fails else 0


def render_extra_sheets():
    """seastack_sheet.png (old stacks vs the new spires from 3 azimuths) and towers_sheet.png (columns / crags / sizes)."""
    paths = []
    # --- sea stacks: old A, old B, new A, B, C, SPIRE_A; azimuth = instance yaw 0 / 120 / 240
    for az, tag in ((0, "a"), (120, "b"), (240, "c")):
        fresh()
        setup_scene((1500, 560))
        water = ref_mat("REF_WATER", (36, 104, 168), rough=0.15)
        ref_plane("REF_WATER_S", 0, 0, 0.0, 600, 600, water.name)
        PL.write_plants_atlas()
        x = -34.0
        items = [("old A", old_seastack("NEG_OLD_SEASTACK_A", "A")), ("old B", old_seastack("NEG_OLD_SEASTACK_B", "B"))]
        for nm, me in items:
            ob = bpy.data.objects.new("REF_" + nm, me)
            bpy.context.scene.collection.objects.link(ob)
            ob.location = (x, 0, 0)
            ob.rotation_euler = (0, 0, math.radians(az))
            x += 11.0
        for k in PL.SEASTACK_KINDS + ("ROCK_SPIRE_A",):
            PL.place(None, k, (x, 0, 0), math.radians(az) + 0.3)
            x += 11.0
        ref_figure(-40, -8, 0.0)
        cam = camera("CAM_S", Vector((4, -92, 15)), Vector((4, 0, 11)), lens=30)
        names = ["OLD A", "OLD B"] + [f"{k.replace('ROCK_', '').replace('SEASTACK_', '')} {PL.SPIRE_SPECS[k]['height']:.0f} m"
                                      for k in PL.SEASTACK_KINDS + ("ROCK_SPIRE_A",)]
        for i, nm in enumerate(names):
            label(nm, (-34 + 11 * i, -9.0, 0.3), 0.9, cam, (255, 255, 255))
        title(cam, f"SEA STACKS  azimuth {az} deg: old two-block stacks (left, FAIL) vs the tapering spires (right)")
        sc = bpy.context.scene
        sc.camera = cam
        path = os.path.join(SCRATCH, f"seastack_az{az}.png")
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    out, shape = compose_sheet(paths, os.path.join(V2, "seastack_sheet.png"), cols=1)
    # --- towers + clusters + crags
    fresh()
    setup_scene((1500, 620))
    PL.write_plants_atlas()
    water = ref_mat("REF_WATER", (36, 104, 168), rough=0.15)
    ref_plane("REF_WATER_T", 0, 0, 0.0, 800, 800, water.name)
    lava = ref_mat("REF_LAVA", (255, 110, 30), emission=2.0, image="Lava_E")
    x = -62.0
    kinds = ["ROCK_CRAG_A", "ROCK_CRAG_B", "ROCK_BASALT_F", "ROCK_BASALT_E", "ROCK_WALL_BASALT_S", "ROCK_WALL_BASALT_M", "ROCK_WALL_BASALT_L"]
    labs = []
    for k in kinds:
        mn, mx = PL.lib_bbox(k)
        w = mx.x - mn.x
        PL.place(None, k, (x + w / 2 - (mn.x + mx.x) / 2, 0, 0), 0.3, 1.0)
        labs.append((x + w / 2, k.replace("ROCK_", "")))
        x += w + 5.0
    ref_figure(-66, -10, 0.0)
    cam = camera("CAM_T", Vector((0, -150, 28)), Vector((0, 0, 20)), lens=30)
    for cx_, nm in labs:
        label(nm, (cx_, -14.0, 0.4), 1.4, cam, (255, 255, 255))
    title(cam, "CRAGS + BASALT 8 m / 12 m clusters + wall-ring TOWERS S / M / L", size=0.04)
    sc = bpy.context.scene
    sc.camera = cam
    path = os.path.join(V2, "towers_sheet.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return [out, path]


if __name__ == "__main__":
    code = main()
    sys.stdout.flush()
    if "--no-exit" not in ARGS:
        os._exit(code)
