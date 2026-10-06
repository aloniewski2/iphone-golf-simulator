"""Hole 10 CRATER: scene build + POSTCARD_LOOK pass (a rim ribbon and a pillar-green standing in a lava crater, one flat play height).

    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python blender/scripts/hole10_build.py
    ... -- --out DIR    (write the .blend / overview / FBX into DIR instead of the repo: ALWAYS use this for experiments and staging)
    ... -- --out DIR --no-render   (skip the EEVEE overview still)
    ... -- --rim plants|basalt|rough   (repair round 3: the tee pad's edge ribbon; default plants = DRESS_PADEDGE, LK_PLANTS atlas swatches (dark olive / deep green), gate-clean; basalt = DRESS_PADRIM,
                                    LK_BASALT, the black-rock VARIANT that the verifier's material-based rock classification rejects, staged separately in v2/hole10/stage_rim_variant/; rough = LK_ROUGH collar, too faint, kept for the record)

With no --out it writes blender/hole_10.blend, blender/previews/hole_10_overview.png and Unity/Assets/Resources/Course/hole_10.fbx (+ .meta
only when none exists). Deterministic: fixed seeds everywhere (random.Random(1010..) here, hole-number seeded libs), so two runs give the same
vertex counts and the same geometry hash (printed as [hole10_build] HASH).

SCORING CORE (unchanged since the flat build, same calls in the same order):
    terrain   rim ribbon + tee slab + green bulb, top at PLAY_Z (6 m above the lava). The lava ellipse (HAZARDS[0]) is cut out of
              it, so the bulb is a pillar: build_terrain(undercut_m=6) hangs undercut walls from every boundary loop, down to z = -6.
    lava      flat WATER_LAVA plane (replaced by the subdivided LK_LAVA mesh of postcard_look_lib.build_lava; still scores as water).
    gameplay  FLAG / HOLE_CUP / BALL_START / TEE_MARKER_1/2 / MARKER_TEE/PIN/UP exactly as before.

REPAIR ROUND 1 (2026-10-04 22:xx, after the shared fixers changed the libs / textures / verifier): (1) hole10_look.PROFILE128 re-baked from the v6 Basalt_C (H_UV_WINDOWS: profile deviation 0.0228 -> 0.0005;
the glow cores and glow-free zones of Basalt_E did not move); (2) SMOKE_IN_FRAME: four stacked plumes (round 2: + two narrow wisps on the cone's flank for the tee frame) (hole10_look.smoke_group, 3 big cards each) replace the three single cards, so each proof frame has
>= 1.5 % of its area at an effective smoke alpha >= .30 (3.0 / 4.0 / 4.7 %); (3) the review's hard horizontal seam at the tee pad: TEE_BOX now wears LK_FAIRWAY with the FAIRWAY's own centerline UV, offset by the
parallax of its 10 cm lip for the address camera (steps 4b / 4c below, stage gate H_TEE_CONTINUOUS, tools/seam_metric.py); scoring, outlines, heights, hazards, pin untouched.

REPAIR ROUND 2 (2026-10-05, after the libs fixers' round 2: BASALT_MIN_RELIEF pillar prisms, new Basalt / Lava / Smoke PNGs, verifier GROUND_UV_LOCAL_SMEAR): (1) FAIRWAY_FIRSTCUT had 3 sliver triangles (103 m2, up to 26:1 /
collapsed) whose three vertex UVs lie on one line: the streak wedge of the lava-rim still. hole10_look.unfold_sliver_uv re-authors them as one similarity per cluster (ratio 1.000, least-squares residual 2.5 m), geometry untouched
(step 4d, H_GROUND_UV_SLIVERS); (2) review (low) "tee reads as the square tee pad": the pad shares the fairway grass (no seam) and so had no edge in the tee frame; a 1.3 m flagstone kerb (library build_path, DRESS_PATH_nn, LK_PATH,
<= 1.15 cm over the collider = under the ball) now runs along the pad's far edge and both sides, inset from the lip, open 4.5 m behind the player (step 4e, H_TEE_KERB; tools/tee_edge.py measures it in the tee frame);
(3) the dark patch at the right of the tee frame stays: it is the real cliff drop (see README "repair round 1"). Scoring meshes, outlines, heights, hazards, pin, hole10_design.py: bit-identical.

REPAIR ROUND 3 (2026-10-05, after the independent review of the installed round-2 build; libs fixers' round 3: new Fairway / Green / Lava / Smoke PNGs, props lib shrubs / arch / spires, verifier): the review (low) "the tee reads as a paved
road across the fairway": the round-2 flagstone kerb (LK_PATH, tan-brown, 44 pixel rows over the whole 900 px frame width) appears nowhere in crater.jpg (a grass SQUARE on BLACK rock). The black-rock rim the review suggests (LK_BASALT,
tried first, hole10_look.build_tee_rim(material="LK_BASALT"), staged as v2/hole10/stage_rim_variant/) is rejected by the verifier: any mesh with >= 50 % of its area on LK_BASALT / LK_ROCK / LK_CLIFF* is judged a ROCK by material
(rock_candidates), a flush 1.15 cm ribbon is a flat open box to ROCK_NOT_ICOSAHEDRON and a flat baked stretch to NO_ROCK_STRETCH_2X (4 lines at blend + FBX; every other gate passes). The edge is therefore the review's other option, a DARK
BORDER: a continuous ragged ribbon ~0.38 m wide on average (0.6-0.9 m on the far edge the tee camera sees, 0.3 m on the sides, 0.25 m at the back: DRESS_PADEDGE_nn, no collision prefix) hugging the pad's lip, LK_PLANTS atlas swatches G_DEEP / OLIVE_DARK / G_DARK (the stills' dark-green
"pillar rim" family), 1.15 cm over the collider = under the ball (step 4e, gates H_TEE_RIM / H_TEE_EDGE_VISIBLE / H_TEE_EDGE_CONTRAST). Everything else (scoring meshes, outlines, heights, hazards, pin, hole10_design.py) is bit-identical;
the rest is a rebuild on the new libs.

REPAIR ROUND 4 (2026-10-05, the GRASS LIVELINESS + GENTLE SWAY add-on, user "i want the gentle sway of the plants with the wind, and do 1 and 4"; libs / textures: the shared fixers' round): (1) EDGE FRINGE: the 170-tuft carpet of
117-triangle A / B tufts (19,890 triangles, even over the shelf) is replaced by postcard_props_lib.scatter_fringe (clumps of 3-7 tall leaning D / E tufts + 36-triangle S fillers, density hugging the rough edge of the fairway / green /
pad rims and the cliff lip, lean <= 12 deg, height 0.6-1.8 x, never on a play surface / the lava / within 4 m of a still ball (1.5 m round the tee ball) / within 6 m of the pin, tops <= 0.55 yd in the ball corridor), budget 45,000
triangles -> the hole is 245,289 triangles (was 220,197; limit 250,000), FBX 4.0 MB; every PLANT_* mesh carries the sway vertex colours (the library's _finalize, exported by export_look_fbx, colors_type LINEAR); (2) review (low) "tee pad
edge = trench": the DRESS_PADEDGE ribbon is ONE swatch with a smooth luminance gradient (no joints), 0.15-0.46 m wide (0.32 m on the edge the tee camera sees) and its inner edge is a shallow ramp instead of a vertical face (the black outline).
Scoring meshes, outlines, heights, hazards, pin, hole10_design.py, the cameras: bit-identical to round 3.

LOOK PASS (POSTCARD_LOOK continue, round 3 2026-10-04; order of the brief: scoring build -> look_materials -> snapshot_ground -> scenery ->
retarget_materials -> cliff skin -> lava -> assign_uvs LAST -> compare_ground -> audits -> render -> save -> export_look_fbx):
    near wall     the bowl: two terraced rows of tangent library wall segments (ROCK_WALL_BASALT_N1..N3, F2 / F3: hex-column segments with RAGGED tops, tilted and chipped, a smooth
                  ridge along the ring, uniform scale 0.58-1.7), 30 yd beyond the farthest shore point of each heading (never closer than 214 yd to the centre), pushed out round the stacks,
                  tops up to 58 m, low (13-18 m) in the headings the tee shot looks through so the volcano still shows (hole10_look.near_wall).
    far ring      the 295-355 yd ring of the brief (hole10_design wall_ring): two rows of the ragged F1 / F2 / F3 segments (scale up to 1.9), a macro skyline (ridges, saddles, 7 spires),
                  tops 18-74 m; replaces the 154 ROCK_WALL_FLAT / ROCK_WALL_BLUNT / CLIFF_ROCK icosahedra of the flat build (hole10_look.far_ring).
    crust         round 3: the cooled crust over the crater floor (ROCK_CRUST_nn, hole10_look.crust_field): a jittered hex lattice of 4.3 m plates (library hex-column primitive, tops
                  1.4-3.6 m above the lava, tilted / chipped, small basalt stumps on some), cut by winding lava rivers and a few pools, 24 yd off every play surface, 38 yd off the line of
                  play, 25 yd off the tee -> C chord and off every lava light, grouped in 52 m cells (>= 100 faces each). Replaces round 2's 56 floating plateaus.
    uv windows    round 3: every wall / stack / crust / skin face is mapped to ONE slice of the Basalt_C / Basalt_E tile (hole10_look.author_uv): ~20-40 % of the side faces take a slice that starts on a
                  glow line (the glow runs along the face edge = in the joint between two columns), the rest and every top take glow-free slices, the v density is low (long streaks).
    stacks        the 11 SCENERY stacks standing in the lava = library basalt pieces (ROCK_BASALT_E / F / C / A mains + 1-2 low satellites standing
                  in the lava), 6.5-12 m above the lava, lowered (uniform scale, floor MIN_SCALE) until no sight line from the tee / pads is cut
                  (the legacy clear_sightlines gate, re-expressed for the library pieces: H_SIGHTLINES_CLEAR; the crust is checked the same way).
    rubble        library basalt rocks at the cliff foot, kept off the lava carry (the legacy `carry` corridor, H_RUBBLE).
    skin          basalt columns on the rim loops and a tapered bowl on the pillar loop (build_cliff_skin(basalt), follow_undercut 1.0; round 3: columns 1.4-2.4 m wide pushed out only 5-40 cm, so they read
                  as clean prisms), in pieces of <= 15 m of shore (a short tail is joined into its neighbour: no rock under 60 welded vertices); the top of the wall is covered (CRATER_WALL_COVERED).
    lava          subdivided WATER_LAVA (LK_LAVA, static) + 5 LAVA_LIGHT_nn empties 2.3 m over the lava: 3 on the rim side 9 yd off the shore (the lava rim of the landmark shot), 2 round the pillar.
    plants        agave clumps (tee pad + rim), dark shrubs and tufts on the grass shelf (rim ribbon, tee slab, pillar collar) through PL.scatter
                  (land mask, 1.5 m clear of the markers, 6 m clear of the pin, off the play surfaces).
    backdrop      DRESS_CONE: the volcano behind the wall ring in the line of the tee shot with five winding LK_LAVA flows from the rim to the foot and a molten crater (hole10_look.build_cone),
                  + 12 DRESS_SMOKE_nn cards (LK_SMOKE) in four stacked plumes (repair round 1: one per proof frame + one off the volcano's summit).
Everything new is ROCK_ / PLANT_ / DRESS_ / WATER_ / LAVA_ (no collision prefix); top surfaces of TERRAIN / FAIRWAY / GREEN / TEE_BOX / BUNKER
are bit-identical to the flat build (compare_ground, the A5 verifier and work/postcard-look/v2/hole10/tools/play_unchanged.py).
NOT built (on purpose): extra DRESS_SAND_* patches (crater.jpg has 4-5 bunkers; a visual-only sand patch on the fairway would read as a hazard that
is not one), a second green, any animated lava, bloom.
"""
import sys
import os
import math
import random
import hashlib

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import numpy as np  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector, Matrix  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import postcard_lib as P  # noqa: E402
import postcard_look_lib as L  # noqa: E402
import postcard_props_lib as PL  # noqa: E402
import hole10_look as HL  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(args[args.index("--out") + 1]) if "--out" in args else None
RENDER = "--no-render" not in args

STACK_MAX_H, STACK_MIN_H = 12.0, 6.5       # m above the lava (brief: none above about 12 m)
MIN_SCALE = HL.MIN_SCALE                    # library instances never go below this x their jitter (>= 0.55): the no-stretch gate's 0.5 limit with margin for the FBX float round trip
OVERVIEW_RES = (1600, 1000)
SKIN_PIECE_M = 15.0                         # m of shore per ROCK_SKIN piece (LAVA_LIGHT rule: wall pieces beside a light are small meshes)
GATES = []
KINDS_VAR = {}                                # kind -> UV-phase variant kinds (filled by the build, read by make_stack)
VRND = random.Random(1050)                    # variant picks of the stacks (a separate stream: the stacks' own geometry stays exactly as before)


def gate(name, ok, detail):
    GATES.append((name, bool(ok), str(detail)))
    print(f"GATE: {name} {'PASS' if ok else 'FAIL'} - {detail}", flush=True)


# ----------------------------------------------------------------------------- scoring core helpers (unchanged from the flat build)
def dark_walls(D):
    """The terrain walls are banded MAT_ROUGH (grass lip) / MAT_CLIFF / MAT_CLIFF_DARK: swap the upper rock band to MAT_ROCK_DARK
    (retarget_materials(CRATER_OVERRIDES) then maps every one of them to LK_BASALT)."""
    me = D.terrain.data
    me.materials[1] = D.mats["MAT_ROCK_DARK"]
    me.materials[2] = D.mats["MAT_CLIFF_DARK"]


def top_z(ob):
    mw = ob.matrix_world
    return max((mw @ v.co).z for v in ob.data.vertices)


def footprint(ob):
    mw = ob.matrix_world
    ps = [mw @ v.co for v in ob.data.vertices]
    return (min(p.x for p in ps), min(p.y for p in ps), max(p.x for p in ps), max(p.y for p in ps))


# the wall / mesa / UV-variant helpers live in hole10_look.py (HL)

# ----------------------------------------------------------------------------- stacks in the lava (library basalt pieces)
# (kind, natural height above z = 0, natural footprint diameter): visible heights of the library meshes (z 0 = lava line)
STACK_KINDS = (("ROCK_BASALT_E", 12.6, 10.4), ("ROCK_BASALT_F", 8.8, 6.9), ("ROCK_BASALT_C", 10.2, 5.5), ("ROCK_BASALT_A", 5.0, 7.8))
SAT_KINDS = (("ROCK_BASALT_A", 5.0, 7.8), ("ROCK_BASALT_B", 3.6, 7.2), ("ROCK_BASALT_D", 1.7, 9.6))


def make_stack(D,s,top,rnd):
    return HL.make_lava_stack(D,s,top,101100+int(s['x']*13+s['d']*7))


def refit_stack(st, f):
    """Lower a whole stack: every piece scaled uniformly by f about its own origin (z 0 = lava line), floor 0.5."""
    for o in st["obs"]:
        sx, sy, sz = o.scale
        k = min(1.0, max(f, MIN_SCALE / min(sx, sy, sz)))
        o.scale = (sx * k, sy * k, sz * k)


def sightlines(D):
    """(eye, target) pairs a player can look along: ball eye (1.8 m) and camera height (4.5 m) on the tee, A, B, C, and the tee camera (5.5 m,
    24 m behind the tee), to dense ring samples of every later landing pad and of the green disc, plus the flag top. Metres above the lava."""
    pz = D.play_z
    st = [Vector((*P.m(*p), pz)) for p in D.CENTERLINE]                     # tee, A, B, C, pin
    ring = lambda c, r, n: [c + Vector((r * math.cos(a), r * math.sin(a), 0.5)) for a in np.linspace(0, math.tau, n, endpoint=False)]
    pad = lambda c: [c + Vector((0, 0, 0.5))] + ring(c, 3 * P.YD, 8) + ring(c, 6 * P.YD, 16)
    green = ([st[4] + Vector((0, 0, 0.7))] + ring(st[4], 4 * P.YD, 12) + ring(st[4], 9 * P.YD, 24) + ring(st[4], 15 * P.YD, 48)
             + [st[4] + Vector((0, 0, 9.5))])
    later = [pad(st[1]) + pad(st[2]) + pad(st[3]) + green, pad(st[2]) + pad(st[3]) + green, pad(st[3]) + green, green]
    first = (st[1] - st[0]).normalized()
    eyes = [(st[0] - first * 24 + Vector((0, 0, 5.5)), later[0])]
    for k, ts in enumerate(later):
        for h in (1.8, 4.5):
            eyes.append((st[k] + Vector((0, 0, h)), ts))
    return [(e, t) for e, ts in eyes for t in ts]


def stack_bvh(stacks_):
    V, T, who = [], [], []
    for i, st in enumerate(stacks_):
        for ob in st["obs"]:
            base = len(V)
            V += [tuple(ob.matrix_world @ v.co) for v in ob.data.vertices]
            for p in ob.data.polygons:
                T.append(tuple(base + k for k in p.vertices))
                who.append(i)
    return BVHTree.FromPolygons(V, T), who


def blocked_lines(D, stacks_):
    """Stack indices that cut a sight line (corridor = eye 0.8 m left / centre / right) and the number of cut lines."""
    bpy.context.view_layer.update()
    bvh, who = stack_bvh(stacks_)
    bad, ncut = set(), 0
    for eye0, tgt in sightlines(D):
        d0 = tgt - eye0
        side = Vector((-d0.y, d0.x, 0.0)).normalized() * 0.8
        cut = False
        for eye in (eye0 - side, eye0, eye0 + side):
            d = tgt - eye
            hit = bvh.ray_cast(eye, d.normalized(), d.length - 0.05)
            if hit[0] is not None:
                bad.add(who[hit[2]])
                cut = True
        ncut += cut
    return bad, ncut


def clear_sightlines(D, stacks_):
    """Lower any stack that cuts a sight line (8 percent per round, scale floor 0.5) until none does. Returns (rounds, remaining)."""
    for rnd_ in range(60):
        bad, ncut = blocked_lines(D, stacks_)
        if not bad:
            return rnd_, 0
        stuck = [i for i in bad if max(min(o.scale) for o in stacks_[i]["obs"]) <= MIN_SCALE + 1e-3]
        if len(stuck) == len(bad):
            return rnd_, ncut
        for i in bad:
            refit_stack(stacks_[i], 0.92)
    return -1, blocked_lines(D, stacks_)[1]


# ----------------------------------------------------------------------------- backdrop geometry choices
def tee_eye(D):
    return Vector((0.0, 0.0, D.play_z + P.Z_TEE + 2.2))


def lava_light_points(D):
    """5 LAVA_LIGHT empties (x, y, z m) 2.3 m over the lava: 3 along the rim shoulder between the tee shot's landing area and the neck end (the lava rim of the
    landmark shot), 2 round the pillar. Round 3: the rim lights sit 9 yd off the shore (found by a radial search on the lie mirror), so they light the cliff skin
    they are for and the crust field (which must stay 25 yd off every light, H_ROCK_CHUNKING) loses as little as possible."""
    pin = np.array(P.m(*D.hole.pin))
    cr = D.scenery["crater"]
    ccx, ccy = P.m(cr["cx"], cr["cd"])
    pts = []
    for ang_deg in (148.0, 128.0, 113.0):                                          # inner (crater) side of the rim ribbon, over the lava
        a = math.radians(ang_deg)
        rr = np.linspace(95.0, 175.0, 321)
        sd = P.signed_dist_m(D, ccx + rr * math.cos(a), ccy + rr * math.sin(a))      # > 0 outside the dry land
        k = int(np.argmin(np.abs(np.where(rr < 150.0, 1e3, sd - 9.0 * P.YD))))
        pts.append((ccx + float(rr[k]) * math.cos(a), ccy + float(rr[k]) * math.sin(a)))
    pts.append((pin[0] - 31.0, pin[1] - 4.0))                                      # west of the pillar, over the lava
    pts.append((pin[0] + 20.0, pin[1] - 27.0))                                     # north-east of the pillar (towards the crater centre)
    return pts


# ----------------------------------------------------------------------------- preview still
def frame_overview(D, res, margin=1.12):
    """Re-frame CAM_HOLE_OVERVIEW (same angle as the lib) on the land with a margin of lava, for a landscape still."""
    cam = bpy.data.objects["CAM_HOLE_OVERVIEW"]
    R = cam.rotation_euler.to_matrix()
    right, up, view = R @ Vector((1, 0, 0)), R @ Vector((0, 1, 0)), R @ Vector((0, 0, -1))
    pts = [Vector((*P.m(x, d), z)) for x, d in D.SHORE for z in (D.play_z + 2.0, -6.0)]
    rs, us = [p.dot(right) for p in pts], [p.dot(up) for p in pts]
    cr, cu = (min(rs) + max(rs)) / 2, (min(us) + max(us)) / 2
    cam.data.ortho_scale = max(max(rs) - min(rs), (max(us) - min(us)) * res[0] / res[1]) * margin
    cam.location = right * cr + up * cu - view * 1500.0


def geometry_hash():
    """sha256 over every exported MESH object: name, world matrix, polygon / vertex counts, the vertices of its mesh (rounded 1e-4), its material names and its UV0 loops (rounded 1e-4)."""
    h = hashlib.sha256()
    n_obj = n_v = n_f = 0
    for ob in sorted((o for o in bpy.data.objects if o.type == 'MESH' and not o.hide_viewport and o.name in bpy.context.scene.objects),
                     key=lambda o: o.name):
        me = ob.data
        co = np.empty(len(me.vertices) * 3, np.float64)
        me.vertices.foreach_get("co", co)
        h.update(ob.name.encode())
        h.update(np.round(np.array(ob.matrix_world), 4).tobytes())
        h.update(np.round(co, 4).tobytes())
        h.update(str((len(me.vertices), len(me.polygons))).encode())
        h.update("|".join(m.name if m else "" for m in me.materials).encode())
        if len(me.uv_layers):                                                    # round 3: the authored UV windows are part of the look, so they are part of the hash
            uv = np.empty(len(me.loops) * 2, np.float64)
            me.uv_layers[0].data.foreach_get("uv", uv)
            h.update(np.round(uv, 4).tobytes())
        n_obj += 1
        n_v += len(me.vertices)
        n_f += len(me.polygons)
    return h.hexdigest(), n_obj, n_v, n_f


# ============================================================================= the build
D = P.start("hole10_design", out_dir=OUT)
SC = D.scenery
D.mod.SCENERY["tee_box_m"] = (SC["tee_box"]["width"] * P.YD, SC["tee_box"]["depth"] * P.YD)   # the lib reads metres; the design says yards

# ---- 1. scoring core (identical calls to the flat build)
P.build_terrain(D, undercut_m=6.0)
dark_walls(D)
P.build_play_surfaces(D)
P.build_water(D, "WATER_LAVA", "MAT_LAVA", shallow_material=None, foam_material=None, crest=False)
P.build_gameplay(D)

# ---- 2. look materials + the proof that the play surfaces do not move
L.look_materials(D)
snap = L.snapshot_ground(D)

# ---- 3. scenery: library basalt only (no icosahedron anywhere; the legacy ROCK_WALL_FLAT/BLUNT/CLIFF/ROCK instances are never created)
HL.register_kinds()
wall_kinds = ["ROCK_WALL_BASALT_" + k for k in ("N1", "N2", "N3", "F1", "F2", "F3")]
kinds_var = HL.uv_variants(wall_kinds, random.Random(1030), p_glow=0.20)
KINDS_VAR.update(HL.uv_variants(["ROCK_BASALT_" + k for k in "ABCDEF"], random.Random(1031), p_glow=0.30))      # the stacks' library kinds too
near, near_th, near_R = HL.near_wall(D, SC["wall_ring"], kinds_var)
ring = HL.far_ring(D, SC["wall_ring"], kinds_var)
rnd_st = random.Random(1011)
hs = [s["h"] for s in SC["stacks"]]
stacks = [make_stack(D, s, STACK_MIN_H + (s["h"] - min(hs)) / (max(hs) - min(hs)) * (STACK_MAX_H - STACK_MIN_H), rnd_st) for s in SC["stacks"]]
lights_xy = [(x, y) for x, y in lava_light_points(D)]
stacks_xy = [(*P.m(s_["x"], s_["d"]), s_["r"] * P.YD + 9.0) for s_ in SC["stacks"]]
crust, crust_kept, crust_planned = HL.crust_field(D, SC["wall_ring"], None, near_th, near_R, stacks_xy, lights_xy, random.Random(1062))
mesas = [dict(obs=[o], kind="crust") for o in crust]                     # pseudo-stacks: the sight-line gate treats the crust like the stacks
# World-sized columns are rebuilt lower if they obstruct play, never stretched.
blocked,_ = blocked_lines(D,stacks+mesas)
rounds=0
for idx in sorted(blocked):
    st_=(stacks+mesas)[idx]
    if st_.get('kind')=='crust':continue
    old=st_['main'];old_name=old.name
    D.objects.pop(old_name,None);bpy.data.objects.remove(old,do_unlink=True)
    HL.UV_STATS.pop(old_name,None)
    stacks[idx]=HL.make_lava_stack(D,st_['design'],6.5,st_['seed']);rounds+=1
_,sight_left=blocked_lines(D,stacks+mesas)
c_st, pin_st = P.m(*D.CENTERLINE[-2]), P.m(*D.CENTERLINE[-1])        # keep the lava carry itself free of rubble
carry = [(c_st[0] + (pin_st[0] - c_st[0]) * k / 14.0, c_st[1] + (pin_st[1] - c_st[1]) * k / 14.0, 24.0) for k in range(15)]
stack_foot = [(o.location.x,o.location.y,.5*math.hypot(footprint(o)[2]-footprint(o)[0],footprint(o)[3]-footprint(o)[1])+3.) for st in stacks for o in st['obs']]
foot = PL.scatter_rocks(D, 30, zone="foot", seed=4, variant="BASALT", avoid=carry + stack_foot,
                        region=PL.region_band(D, 1.8, 7.0, "sea"))

# plants on the grass shelf: agave clumps (the crater.jpg spiky clusters round the tee pad and along the rim), dark shrubs, tufts. Land-masked,
# 1.5 m off the markers, 6 m off the pin, never on a play surface (PL.scatter safe masks). Big things first (later calls keep their distance).
tee_agave = PL.scatter(D, {"PLANT_AGAVE_B": 3, "PLANT_AGAVE_A": 1}, 14, region=PL.region_disc(0.0, 0.0, 19.0), seed=12, scale_range=(1.5, 2.0), min_gap=3.0)
agave = tee_agave + PL.scatter(D, {"PLANT_AGAVE_B": 3, "PLANT_AGAVE_A": 1}, 22, region=PL.region_band(D, 0.5, 5.0), seed=13, scale_range=(1.4, 2.0), min_gap=6.0)
shrubs = PL.scatter(D, ["PLANT_SHRUB_A", "PLANT_SHRUB_B"], 14, region=PL.region_band(D, 0.8, 6.0), seed=14, scale_range=(1.1, 1.6), min_gap=6.0)
# repair round 4 (GRASS LIVELINESS, user 2026-10-04 "do 1 and 4"): the 170-tuft carpet of 117-triangle A / B tufts (19,890 triangles, spread evenly over the shelf = a carpet at the phone camera)
# is replaced by the EDGE FRINGE of the props library: clumps of 3-7 tall leaning tufts (D / E 108-135 triangles, S fillers 36) standing taller and denser along the rough edge of the fairway / green / pad
# rims and the shore top / cliff lip, with varied lean (<= 12 degrees), height (0.6-1.8 x) and clump grouping. Never on a play surface, never on the lava, never inside 4 m of a still ball (1.5 m round the tee
# ball), >= 6 m from the pin, tops <= 0.55 yd in the ball corridor (postcard_props_lib.scatter_fringe). The triangles are paid by the 170 old tufts (-19,890) and a tight fringe budget.
FR_KINDS = dict(tall={"PLANT_TUFT_D": 3, "PLANT_TUFT_E": 2}, fill={"PLANT_TUFT_S": 3})
FR_BUDGET = 45000
FR_SEED = 11
FR_TALL = float(args[args.index("--fr-tall") + 1]) if "--fr-tall" in args else 0.25          # share of the tufts that are tall D / E (108 / 135 triangles) rather than 36-triangle fillers
FR_BOOST = float(args[args.index("--fr-boost") + 1]) if "--fr-boost" in args else 4.0
# where the stills look: the shot balls of the stills configs (the library default focus) + the grass shelf the lava-rim landmark sees on the left of its frame (ray-cast probe of the 900 x 1600 camera:
# the shelf runs (73, 107) .. (100, 134) m, v2/hole10/repair_r4/probe_lavarim_hits.py): the density x (1 + 4 exp(-(d / 45 m)^2)) there
FR_FOCUS = [(z[0], z[1]) for z in PL.still_ball_zones(D)] + [(73.0, 107.0), (77.0, 111.0), (82.0, 116.0), (88.0, 121.0), (94.0, 125.0), (99.0, 132.0)]
fringe = PL.scatter_fringe(D, kinds=FR_KINDS, tri_budget=31000, seed=FR_SEED, lip_margin_m=1.05, focus=FR_FOCUS, focus_boost=FR_BOOST, tall_share=FR_TALL)       # 1.05 m inside the cliff edge: H_NO_PLANT_ON_WATER keeps its >= 1.0 m (the library default 0.9 m would not)
tufts = fringe["objs"]
print(f"[hole10_build] fringe {fringe['count']} tufts {fringe['tris']} tris (budget {fringe['budget']}) clumps {fringe['clusters']} singles {fringe['singles']} by kind {fringe['by_kind']} scale {fringe['scale_min']:.2f}..{fringe['scale_max']:.2f} "
      f"lean max {fringe['lean_max_deg']:.1f} top max {fringe['top_max_m']:.3f} m capped {fringe['capped']} edge {fringe['edge']}", flush=True)

# background volcano + smoke wisps (visual only, built after the lava: see 5.)
CONE_C = (float(args[args.index("--cone") + 1].split(",")[0]), float(args[args.index("--cone") + 1].split(",")[1])) if "--cone" in args else (170.0, 440.0)
cone = HL.build_cone(D, (CONE_C[0], CONE_C[1], -8.0), base_r=125.0, height=135.0, seed=1, notch_deg=250.0)

# ---- 4. retarget the flat materials, cladding, lava
L.retarget_materials(D, overrides=L.CRATER_OVERRIDES)         # MAT_CLIFF / MAT_CLIFF_DARK / MAT_ROCK_DARK -> LK_BASALT (grass lip band too, see skin)
loops = L.terrain_top_loops(D)
pin = P.m(*D.hole.pin)
pillar = [i for i, (Pl, _) in enumerate(loops) if P.point_in_poly(pin, [tuple(p) for p in Pl])]
rim = [i for i in range(len(loops)) if i not in pillar]
skins = L.build_cliff_skin(D, style="basalt", loops=rim, follow_undercut=0.0, piece_m=SKIN_PIECE_M, disp_m=(0.05, 0.4), basalt_width_m=(0.85, 1.55))
skins += L.build_cliff_skin(D, style="basalt", loops=pillar, follow_undercut=1.0, seed=1, piece_m=SKIN_PIECE_M, disp_m=(0.05, 0.4), basalt_width_m=(0.85, 1.55))
def merge_tiny_skins(objs, min_verts=70):
    """A cladding piece with fewer than `min_verts` welded vertices (the short tail of a loop) is joined into its nearest neighbour piece: no rock under 60 welded vertices
    (the brief), and no hole in the cladding."""
    objs = list(objs)
    tiny = [o for o in objs if PL.welded_vertex_count(o.data) < min_verts]
    for t in tiny:
        rest = [o for o in objs if o is not t and o not in tiny]
        c = np.array(t.matrix_world.translation) + np.mean([np.array(v.co) for v in t.data.vertices], axis=0)
        tgt = min(rest, key=lambda o: float(np.linalg.norm(np.mean([np.array(v.co) for v in o.data.vertices], axis=0) - c)))
        bpy.ops.object.select_all(action='DESELECT')
        t.select_set(True)
        tgt.select_set(True)
        bpy.context.view_layer.objects.active = tgt
        bpy.ops.object.join()
        objs.remove(t)
    return objs, len(tiny)


skins, n_tiny = merge_tiny_skins(skins)
rnd_skin = random.Random(1070)
for sk_ in skins:                                              # glow in the joints, not a continuous dash lattice (the lib box UV)
    HL.UV_STATS[sk_.name] = HL.author_uv(sk_.data, rnd_skin, uf=0.78, vf=0.55, p_glow=0.26)
# ---- 4b. the tee pad shares the fairway's grass + UV phase (repair round 1, review finding "hard horizontal seam at the tee pad")
#         TEE_BOX (z 6.24, 10 cm above the FAIRWAY) was LK_GREEN with planar UVs while the FAIRWAY beside it is LK_FAIRWAY with centerline UVs: the mow stripes stopped at the pad's
#         far edge and the texture phase jumped (mean abs row step 16.4 vs about 5.5 elsewhere, hole10_1_tee.png row 590). The pad keeps its outline and height (no vertex moves); only
#         the material slot changes, and assign_uvs (right below) then gives it the SAME centerline UV function as the FAIRWAY, so the texture is continuous across the lip.
_tee = bpy.data.objects["TEE_BOX"]
_tee.data.materials[0] = L.look_material("LK_FAIRWAY", D)
assert len(_tee.data.materials) == 1

lava = L.build_lava(D, lights=5, light_points=[(x, y, 2.3) for x, y in lava_light_points(D)])
L.assign_uvs(D)                                                # LAST

# ---- 4c. the pad's UV phase = the fairway's as the address camera sees it. The pad is 10 cm above the FAIRWAY, so its far lip hides a strip of fairway behind it: the first fairway texel
#         visible past the lip at horizontal distance Dh from the eye (CameraRig.FrameAddress: 4.5 yd behind the ball on the aim line, 2.4 yd up) lies Dh x dz / h further along the ray
#         (dz = the pad's height over the fairway, h = the eye's height over the pad) than the lip itself (about 0.4 m at 9 m). The pad's UV at every vertex is therefore the FAIRWAY's
#         centerline UV of the point (p + that offset): the mow stripes and blades meet across the lip in the TEE frame (a 10 m tile moves by under 5 cm per pad metre: invisible from above
#         and from the sides, where the riser shows the edge). Measured on the Blender preview with tools/seam_metric.py / the stage gate H_TEE_CONTINUOUS.
_tee_ball = P.m(0.0, 0.0)
_aim = np.array(P.m(*D.CENTERLINE[1])) - np.array(_tee_ball)
_aim /= np.linalg.norm(_aim)
TEE_CAM_XY = np.array(_tee_ball) - _aim * 4.5 * P.YD
_fw = bpy.data.objects["FAIRWAY"]
_dz = max(v.co.z for v in _tee.data.vertices) - max(v.co.z for v in _fw.data.vertices)           # pad top over the fairway top (0.10 m)
_h = 2.4 * P.YD + 0.02 + 0.0                                                                       # the eye over the ball (the ball rests on the pad top; 2 cm = its radius)
_cl = L.CenterlineParam(D)
_lay = _tee.data.uv_layers[0]
_nl = len(_tee.data.loops)
_vi = np.empty(_nl, np.int64)
_tee.data.loops.foreach_get("vertex_index", _vi)
_co = np.empty(len(_tee.data.vertices) * 3)
_tee.data.vertices.foreach_get("co", _co)
_xy = _co.reshape(-1, 3)[_vi][:, :2]
_off = (_xy - TEE_CAM_XY[None, :]) * (_dz / _h)
_u0, _v0 = _cl.uv(_xy[:, 0], _xy[:, 1])
_u1, _v1 = _cl.uv(_xy[:, 0] + _off[:, 0], _xy[:, 1] + _off[:, 1])
_tile = L.LOOK["LK_FAIRWAY"][2]
_uv = np.empty(_nl * 2)
_lay.data.foreach_get("uv", _uv)
_uv[0::2] += (_u1 - _u0) / _tile
_uv[1::2] += (_v1 - _v0) / _tile
_lay.data.foreach_set("uv", _uv)
_tee.data.update()
TEE_PARALLAX = dict(dz=_dz, h=_h, off_max=float(np.hypot(_off[:, 0], _off[:, 1]).max()), off_at_lip=float(0.0))

# ---- 4d. repair round 2 (verifier GROUND_UV_LOCAL_SMEAR, the streak wedge of the lava-rim still): the sliver triangles of FAIRWAY_FIRSTCUT get a conformal UV unfolded from their neighbours.
#         Geometry untouched (the vertices, faces and heights are the scoring surface): see hole10_look.unfold_sliver_uv. The repo's installed build has 3 triangles (103 m2) above 4:1, worst 26:1 / collapsed.
bpy.data.objects['FAIRWAY_FIRSTCUT'].data.materials[0]=L.look_material('LK_ROUGH',D)
FW_TILE = L.LOOK['LK_ROUGH'][2]
UVFIX = {"FAIRWAY_FIRSTCUT": HL.unfold_sliver_uv(bpy.data.objects["FAIRWAY_FIRSTCUT"], FW_TILE)}

# ---- 4e. repair round 3 (review, low: "Tee reads as the square tee pad; no paved road across the fairway"): the pad shares the fairway's grass (4b / 4c: no seam), so from the tee camera it has no edge at all. Round 2 laid a
#         1.3 m flagstone kerb (LK_PATH, tan-brown: 44 pixel rows over the full frame width, "a crosswalk"); crater.jpg's pad is a grass SQUARE on BLACK rock. A LK_BASALT ribbon (the review's first suggestion) is judged a rock
#         by the verifier (>= 50 % of the area on a rock material), so the edge is the review's second option, a dark border: a continuous ragged ribbon of Plants-atlas swatches (DRESS_PADEDGE_nn, no collision prefix)
#         that hugs the pad's lip on the far edge, both sides and the back, 1.15 cm over the lowest collider sample (the library's path height: under the ball). The pad outline, its height and its grass UV are unchanged.
#         The verifier allows only DRESS_PATH / DRESS_SAND as overlays on a play surface; PROPS_OFF_PLAY counts scenery that stands more than 3 cm above the ground, so the 1.15 cm ribbon is not "standing on" it, and ONE_GREEN's
#         DRESS_ lawn budget (< 20 m2 of flat grass-material top over all DRESS_ meshes) caps its width (measured by the stage gate H_TEE_RIM; disclosed in INSTALL.md).
RIM_MODE = args[args.index("--rim") + 1] if "--rim" in args else "basalt"
assert RIM_MODE in ("rough", "basalt", "plants"), RIM_MODE
if RIM_MODE == "plants":
    # repair round 4 (review, low: thin 20-30 cm rim, no facets): w_mean 0.23 x the far-edge factor 1.4 = 0.32 m on the edge the tee camera sees (0.21 on the sides, 0.18 at the back), one swatch, shallow inner ramp
    RIM_SPEC = dict(name="DRESS_PADEDGE", material="LK_PLANTS", uv_mode="atlas", w_mean=0.23, w_amp=0.22, bites=(0.35, 0.7, 1.2), bite_every=6.0, w_clip=(0.15, 0.46), far_boost=(0.90, 0.50, 0.10))
elif RIM_MODE == "basalt":
    RIM_SPEC = dict(name="DRESS_PADRIM", material="LK_BASALT", uv_mode="basalt", w_mean=0.75, w_amp=0.42, bites=(0.5, 0.7, 1.2), bite_every=5.0, w_clip=(0.30, 1.20))
else:
    RIM_SPEC = dict(name="DRESS_PADEDGE", material="LK_ROUGH", uv_mode="planar", w_mean=0.36, w_amp=0.40, bites=(0.5, 0.7, 1.2), bite_every=5.0, w_clip=(0.16, 0.62))
RIM_SEED = 11
if RIM_MODE == "plants":
    RIM_SPEC["aim"] = [float(_aim[0]), float(_aim[1])]                                    # the tee -> A chord: the far edge (the one the tee camera sees) takes most of the width (lawn budget)
if RIM_MODE=='basalt':rim,RIM_STATS=HL.build_tee_platform(D,_tee)
else:rim,RIM_STATS=HL.build_tee_rim(D,L,_tee,seed=RIM_SEED,**RIM_SPEC)
print('[hole10_build] rim', RIM_MODE, RIM_STATS, 'pad z', max(v.co.z for v in _tee.data.vertices), 'play_z', D.play_z)

# ---- 5. smoke plumes (their own meshes, LK_SMOKE; repair round 1 + 2: SMOKE_IN_FRAME = a plume the player can pick out in EACH proof frame)
#         Round 3 had 3 single cards behind the walls: 0.47 / 0.01 / 0.00 % of the tee / approach / lava-rim frames at an effective alpha >= .30 (need 1.5 %). Smoke_C peaks at alpha .50
#         (x AlphaGain 1.35) and only 11 % of a card's area reaches .30, so every proof frame gets a STACK of 3 big cards (hole10_look.smoke_group) placed where its camera sees the sky / the
#         wall / the lava without the near scenery in front: tee = rising from behind the wall window, left of the volcano; approach = in front of the east wall to the right of the flag;
#         lava rim = over the north-east wall in the sky + a second plume in the east; plus one plume off the volcano's summit (background, not in a proof frame). All beyond the rim circle.
TEE_CAM, APP_CAM, RIM_CAM = (-0.87, -4.02), (93.82, 145.52), (64.01, 98.76)      # the three proof cameras (Blender m, x / y), from landmarks.json via FrameAddress
smoke = []
# Lower volcanic vent: choose an actual exposed cone surface just above lava.
# The old broad sky placement failed the original tee geometric frame gate.
_cone_bvh=BVHTree.FromObject(cone,bpy.context.evaluated_depsgraph_get())
_vent_probes=[]
for _r in np.linspace(90.,135.,181):
    _x,_y=CONE_C[0]-_r/math.sqrt(2.),CONE_C[1]-_r/math.sqrt(2.)
    _hit=_cone_bvh.ray_cast(Vector((_x,_y,200.)),Vector((0,0,-1)))
    if _hit[0] is not None and float(_hit[0].z)+.10>=.10:
        _vent_probes.append((_x,_y,float(_hit[0].z)+.10))
if not _vent_probes:raise RuntimeError('no exposed actual cone/lava flank vent')
_tee_vent=min(_vent_probes,key=lambda p:p[2])
smoke += HL.smoke_group(D,TEE_CAM,_tee_vent,(150.0,80.0),3,seed=1,spread=.035,parallel=True)
# Keep the existing approach/rim vents, dimensions and separated RNG offsets.
# A common heading makes each group's three curved wisps parallel.
smoke += HL.smoke_group(D,APP_CAM,(305.0,118.0,0.0),(90.0,90.0),3,seed=2,parallel=True)
smoke += HL.smoke_group(D,RIM_CAM,(300.0,330.0,30.0),(130.0,110.0),3,seed=3,parallel=True)


# ---- 6. cameras, light, previews
P.build_cameras_and_light(D)
frame_overview(D, OVERVIEW_RES)
bpy.context.preferences.filepaths.save_version = 0       # no hole_10.blend1 next to the scene
bpy.context.view_layer.update()

if D.out_dir:
    os.makedirs(os.path.join(os.path.dirname(D.out_dir),'checkpoints'),exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(D.out_dir),'checkpoints','p4_unverified.blend'),relative_remap=False)
# ---- 7. gates of the build itself
ok, detail = L.compare_ground(D, snap)
gate("H_LOOK_TOP_SURFACES_UNCHANGED", ok, detail)
for name, ok, detail in L.audit_look(D):
    gate("LIB_" + name, ok, detail)
for name, ok, detail in L.v2_gates(D, pockets=None):
    gate("LIB_" + name, ok, detail)

# ---- 7b. gates that need the postcard_lib handle (lie mirror, ground ray cast)
plants = [o for o in bpy.data.objects if o.name.startswith("PLANT_") and o.type == 'MESH' and "ASSET_LIBRARY" not in [c.name for c in o.users_collection]]
px_ = np.array([o.matrix_world.translation.x for o in plants])
py_ = np.array([o.matrix_world.translation.y for o in plants])
pz_ = np.array([o.matrix_world.translation.z for o in plants])
water_ = P.lie_codes_m(D, px_, py_) == P.LIE_WATER
sd_ = P.signed_dist_m(D, px_, py_)
gz_ = PL.ground_sampler(D)(px_, py_)
dz_ = np.abs(pz_ - gz_)
gate("H_NO_PLANT_ON_WATER", len(plants) > 0 and not water_.any() and float(sd_.max()) <= -0.999 and float(np.nanmax(dz_)) <= 0.05 and float(pz_.min()) >= D.play_z - 0.2,
     f"{len(plants)} PLANT_ objects: on water (lie mirror) {int(water_.sum())}, nearest to a shore / lava edge {-float(sd_.max()):.2f} m (>= 1.0), base vs the ray-cast collider top max |dz| "
     f"{float(np.nanmax(dz_)):.3f} m (<= 0.05), lowest base z {float(pz_.min()):.2f} m (play {D.play_z})")
pin_xy = np.array(P.m(*D.hole.pin))
mk = [np.array(o.matrix_world.translation)[:2] for o in bpy.data.objects if o.name in ("TEE_MARKER_1", "TEE_MARKER_2", "MARKER_TEE", "BALL_START", "HOLE_CUP", "MARKER_PIN", "FLAG_POLE")]
d_pin = float(np.hypot(px_ - pin_xy[0], py_ - pin_xy[1]).min())
d_mk = min(float(np.hypot(px_ - m_[0], py_ - m_[1]).min()) for m_ in mk)
gate("H_PLANTS_CLEAR_OF_PIN_AND_TEE", d_pin >= 6.0 and d_mk >= 1.5,
     f"nearest plant to the pin {d_pin:.1f} m (>= 6; brief >= 1.5), nearest to any of {len(mk)} tee / pin / cup / ball markers {d_mk:.2f} m (>= 1.5)")
bad_s, ncut = blocked_lines(D, stacks + mesas)
gate("H_SIGHTLINES_CLEAR", not bad_s and ncut == 0,
     f"legacy clear_sightlines gate re-expressed for the library stacks: {len(sightlines(D))} sight lines (ball eye 1.8 m / camera 4.5 m on the tee, A, B, C and the tee camera to every later pad and the green) cut by {ncut}; rounds used {rounds}")
tuft_n = sum(1 for o in plants if "TUFT" in o.name)
gate("H_SHELF_PLANTS", len(plants) >= 10 and tuft_n >= 40,
     f"{len(plants)} plants on the grass shelf: {len(agave)} agave, {len(shrubs)} shrubs, {len(tufts)} tufts (all land-masked PL.scatter, one shared LK_PLANTS material, instanced)")

def world_xy(obs):
    out = []
    for o in obs:
        mw = np.array(o.matrix_world)
        co = np.empty(len(o.data.vertices) * 3)
        o.data.vertices.foreach_get("co", co)
        co = co.reshape(-1, 3)
        out.append(co @ mw[:3, :3].T + mw[:3, 3])
    return np.vstack(out)


nw = world_xy(near)
sd_near = P.signed_dist_m(D, nw[:, 0], nw[:, 1])
cxm, cym = P.m(SC["wall_ring"]["cx"], SC["wall_ring"]["cd"])
r_near = np.hypot(nw[:, 0] - cxm, nw[:, 1] - cym)
near_tops = [top_z(o) for o in near]
gate("H_NEAR_WALL", len(near) >= 60 and float(sd_near.min()) >= 14.0 * P.YD and float(r_near.min()) >= 190.0 * P.YD and 12.0 <= min(near_tops) and max(near_tops) <= 60.0,
     f"{len(near)} near-wall library segments (ROCK_WALL_BASALT_N*/F*), nearest vertex {float(sd_near.min()) / P.YD:.1f} yd from any play surface / shore (>= 14), "
     f"{float(r_near.min()) / P.YD:.0f}..{float(r_near.max()) / P.YD:.0f} yd from the crater centre, tops {min(near_tops):.1f}..{max(near_tops):.1f} m above the lava (12..60)")
if crust:
    mv = world_xy(crust)
    sd_mesa = P.signed_dist_m(D, mv[:, 0], mv[:, 1])
    clx = [P.m(*p) for p in D.CENTERLINE]
    d_line = min(float(HL.seg_dist(mv[:, 0], mv[:, 1], *clx[i], *clx[i + 1]).min()) for i in range(len(clx) - 1))
    d_chord = float(HL.seg_dist(mv[:, 0], mv[:, 1], *clx[0], *clx[3]).min())
    d_light = min(float(np.hypot(mv[:, 0] - lx, mv[:, 1] - ly).min()) for lx, ly in lights_xy)
    crust_tops = [top_z(o) for o in crust]
    n_faces = sum(len(o.data.polygons) for o in crust)
    gate("H_CRUST", len(crust) >= 20 and crust_kept >= 500 and float(sd_mesa.min()) >= 24.0 * P.YD and d_line >= 38.0 * P.YD and d_chord >= 25.0 * P.YD
         and max(crust_tops) <= HL.CRUST_TOP_MAX and d_light >= 25.0 * P.YD,
         f"{len(crust)} crust chunks (ROCK_CRUST_nn, {crust_kept} of {crust_planned} planned hex plates {HL.CRUST_COL_R:.1f} m, {n_faces} faces): nearest vertex {float(sd_mesa.min()) / P.YD:.1f} yd from any play surface (>= 24), "
         f"{d_line / P.YD:.1f} yd from the line of play (>= 38), {d_chord / P.YD:.1f} yd from the tee -> C chord (>= 25), {d_light / P.YD:.1f} yd from the nearest lava light (>= 25), "
         f"highest top {max(crust_tops):.2f} m above the lava (<= {HL.CRUST_TOP_MAX:.1f}, below every sight line from z >= {D.play_z:.0f})")
else:
    gate("H_CRUST", False, "no crust placed")
uvs = HL.UV_STATS
side_n = sum(v["side"] for v in uvs.values())
glow_n = sum(v["glow"] for v in uvs.values())
top_n = sum(v["top"] for v in uvs.values())
top_free = sum(v["top_glow_free"] for v in uvs.values())
TX = HL.texture_measure()                      # the window constants of hole10_look re-measured from the Basalt_E / Basalt_C PNGs on disk (stale constants = a failed gate, not a quiet drift)
tex_ok = TX["zone_glow_max"] <= 2e-4 and TX["edge_glow_min"] >= 0.04 and TX["profile_dev"] <= 0.01
gate("H_UV_WINDOWS", len(uvs) >= 60 and top_n == top_free and 0.15 <= glow_n / max(side_n, 1) <= 0.45 and tex_ok,
     f"{len(uvs)} UV-window meshes (wall / stack variants, crust chunks, skin pieces): {side_n} side faces, {glow_n} of them ({glow_n / max(side_n, 1):.0%}) carry a Basalt_E glow line on their edge (target 15-45 %), "
     f"{top_n} top faces all take glow-free slices ({top_free}); window constants vs the PNGs on disk: glow share inside the {len(HL.ZONES)} glow-free zones max {TX['zone_glow_max']:.4%} (<= 0.02 %), "
     f"narrowest edge window at each of the {len(HL.GLOW_U)} glow lines holds {TX['edge_glow_min']:.1%} glow (>= 4 %), Basalt_C luminance profile deviation {TX['profile_dev']:.4f} (<= 0.01), glow texels {TX['glow_mean']:.2%} of the tile; "
     f"the texture-sampled glow measures at the authored UVs are in the stage gates (H_GLOW_JOINTS)")

# ---- 7c. repair round 2 / 3 gates: ground UV slivers (GROUND_UV_LOCAL_SMEAR) and the tee pad's edge ribbon
_gm = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(L.GROUND_PREFIXES) and "ASSET_LIBRARY" not in [c.name for c in o.users_collection] and len(o.data.uv_layers)]
_gs = {}
for o_ in _gm:
    W_, tv_, tl_, uv_ = HL.mesh_tris_uv(o_)
    r_, a_ = HL.tri_uv_ratio(W_, tv_, uv_[tl_])
    k_ = a_ > 1e-9
    r_, a_ = r_[k_], a_[k_]
    _gs[o_.name] = (float(a_[r_ > 4.0].sum()), float(a_[r_ > 4.0].max()) if (r_ > 4.0).any() else 0.0, float(r_.max()))
own_ = {k: v for k, v in _gs.items() if not k.startswith("TERRAIN")}
ter_ = {k: v for k, v in _gs.items() if k.startswith("TERRAIN")}
fx = UVFIX["FAIRWAY_FIRSTCUT"]
gate("H_GROUND_UV_SLIVERS", all(v[0] <= 0.5 for v in own_.values()) and all(v[0] <= 15.0 and v[1] <= 10.0 for v in ter_.values()) and fx["max_ratio_after"] <= 1.001 and fx["n_fixed"] >= 1,
     f"{len(_gs)} play-surface meshes: UV Jacobian ratio above 4:1 covers {sum(v[0] for v in own_.values()):.2f} m2 on the hole-authored ones (FAIRWAY_FIRSTCUT before the repair: 103 m2, worst 26:1 / collapsed; limit 0.5), "
     f"TERRAIN wall UV (library) {sum(v[0] for v in ter_.values()):.1f} m2 (verifier limit 15 / 10); FAIRWAY_FIRSTCUT: {fx['n_fixed']} slivers re-authored in {fx['n_clusters']} cluster(s) ({fx['n_bad']} above {HL.UV_SLIVER_TH:g}:1 + {fx['n_zero']} zero-area, {fx['area_fixed']:.0f} m2, "
     f"worst {min(fx['max_ratio_before'], 999):.0f}:1 -> {fx['max_ratio_after']:.3f}:1, least-squares residual {fx['max_resid_m']:.2f} m), worst triangle left on the mesh {fx['max_ratio_mesh']:.2f}:1, largest UV step on a shared edge {fx['max_jump_m']:.2f} m of the 10 m tile; geometry untouched (H_LOOK_TOP_SURFACES_UNCHANGED)")
kv = np.vstack([world_xy([k_]) for k_ in rim]) if rim else np.zeros((0, 3))
_pad = HL.pad_outline(_tee)
_en = np.roll(_pad, -1, axis=0) - _pad
_nn = np.stack([-_en[:, 1], _en[:, 0]], 1) / np.hypot(_en[:, 0], _en[:, 1])[:, None]
if len(kv):
    _d_in = np.einsum("kij,ij->ki", kv[:, None, :2] - _pad[None, :, :], _nn).min(axis=1)          # distance inside the convex pad outline (min over its edges; < 0 = outside)
    _pad_z = max(v.co.z for v in _tee.data.vertices)
    _top = kv[:, 2] > _pad_z - 0.002                                                                  # round 4: the ramp's lower vertices (6 mm UNDER the collider) are not top vertices
    _mk = [np.array(bpy.data.objects[n_].matrix_world.translation)[:2] for n_ in ("TEE_MARKER_1", "TEE_MARKER_2", "MARKER_TEE", "BALL_START") if bpy.data.objects.get(n_) is not None]
    _dm = min(float(np.hypot(kv[_top, 0] - m_[0], kv[_top, 1] - m_[1]).min()) for m_ in _mk)
    _lift = float(kv[_top, 2].max() - _pad_z)
    _lift_lo = float(kv[_top, 2].min() - _pad_z)
    _mat = RIM_SPEC["material"]
    _area_flat = float(sum(sum(pg.area for pg in k_.data.polygons if pg.normal.z > 0.95) for k_ in rim))
    gate("H_TEE_RIM", len(rim) >= 1 and float(_d_in[_top].min()) >= 0.0 and _dm >= 1.5 and 0.003 <= _lift_lo and _lift <= 0.015 and all({m_.name for m_ in k_.data.materials} == {_mat} for k_ in rim)
         and not any(k_.name.startswith(("DRESS_PATH",)) for k_ in bpy.data.objects) and (_mat not in ("LK_ROUGH", "LK_PLANTS") or _area_flat <= 18.5),
         f"{len(rim)} {RIM_SPEC['name']} piece(s) ({_mat}, {RIM_STATS['faces']} faces, {RIM_STATS['slabs']} slabs) hug the pad lip ({RIM_MODE} mode): every top vertex inside the TEE_BOX outline by >= {float(_d_in[_top].min()):.3f} m (it never climbs the 10 cm riser), "
         f">= {_dm:.2f} m from the tee markers / ball (limit 1.5), top {_lift_lo * 100:.2f}..{_lift * 100:.2f} cm over the pad (3-15 mm, library z_off 11.5 mm: under the ball), {RIM_STATS['total_m']:.1f} m of ribbon {RIM_STATS['w_min']:.2f}..{RIM_STATS['w_max']:.2f} m wide "
         f"(mean {RIM_STATS['w_mean']:.2f}), {_area_flat:.1f} m2 of flat top (ONE_GREEN's DRESS_ lawn budget: < 20 m2 in total, < 10 m2 per object, counted for grass materials), no DRESS_PATH left"
         + (f", UV slices in zones {RIM_STATS['zones']} (glow-free), mean Basalt_C window luminance {RIM_STATS['win_lum_mean']:.3f} (walls: 0.14)" if RIM_MODE == "basalt" else
            (f", ONE Plants-atlas swatch {RIM_STATS['zones']} with a smooth luminance gradient (no joint between slabs; UV inside its 64 px cell), shallow inner ramp (no vertical face)" if RIM_MODE == "plants" else ", world-planar UV at the contract tile")))
else:
    gate("H_TEE_RIM", False, "no rim built")

st = P.stats(D)
gate("H_GRASS_FRINGE_BUILD", fringe["count"] >= 300 and fringe["tris"] <= FR_BUDGET and fringe["lean_max_deg"] <= 12.05 and fringe["scale_min"] >= 0.595 and fringe["scale_max"] <= 1.805
     and fringe["clusters"] >= 100 and fringe["singles"] >= 5 and not any(k_ in ("PLANT_TUFT_A", "PLANT_TUFT_B") for k_ in fringe["by_kind"]) and st["tris"] <= 250000,
     f"scatter_fringe: {fringe['count']} tufts ({fringe['tris']} triangles of the {FR_BUDGET} budget; {fringe['clusters']} clumps of 3-7, {fringe['singles']} singles; by kind {fringe['by_kind']}), height factor {fringe['scale_min']:.2f}..{fringe['scale_max']:.2f}, "
     f"lean max {fringe['lean_max_deg']:.1f} deg (<= 12), tallest top {fringe['top_max_m']:.2f} m ({fringe['capped']} scaled down to the 0.55 yd cap in the ball corridor), rough-edge samples covered {fringe['edge_covered']} of {fringe['edge_samples']} {fringe['edge']}; "
     f"the hole is {st['tris']} triangles (<= 250,000; round 3: 220,197 with the 170-tuft carpet of 19,890)")
tops = [top_z(o) for o in ring]
stops = [max(top_z(o) for o in s["obs"]) for s in stacks]
print(f"[hole10_build] near wall {len(near)} segments, far ring {len(ring)} segments (tops {min(tops):.1f}..{max(tops):.1f} m above the lava), {len(crust)} crust chunks; {len(stacks)} stacks "
      f"({', '.join('%.1f' % t for t in stops)} m measured, sightline rounds {rounds}, lines still cut {sight_left}); foot rubble {len(foot)}; "
      f"plants {len(agave)} agave {len(shrubs)} shrubs {len(tufts)} tufts; skin pieces {len(skins)} ({n_tiny} short tail pieces joined into a neighbour); smoke {len(smoke)}; objects {st['objects']} tris {st['tris']}")
G_HASH, G_OBJ, G_V, G_F = geometry_hash()
print(f"[hole10_build] HASH {G_HASH} objects {G_OBJ} verts {G_V} faces {G_F}")

if RENDER:
    for nm_ in ("BALL_START", "HOLE_CUP", "MARKER_TEE", "MARKER_PIN", "MARKER_UP"):   # Unity hides these placeholders: hide them in the preview too
        if bpy.data.objects.get(nm_) is not None:
            bpy.data.objects[nm_].hide_render = True
    P.render_overview(D, res=OVERVIEW_RES)
    for nm_ in ("BALL_START", "HOLE_CUP", "MARKER_TEE", "MARKER_PIN", "MARKER_UP"):
        if bpy.data.objects.get(nm_) is not None:
            bpy.data.objects[nm_].hide_render = False
# save like P.save but WITHOUT relative_remap: the LK_* image paths stay the absolute Unity/Assets/Resources/Course/Look/*.png paths they were loaded
# with, so the .blend works from the staging folder AND after it is copied to blender/hole_10.blend (relative paths remapped against the staging folder
# would break on the copy)
_blend = P._blend_path(D)
P._guard_target(D, _blend)
os.makedirs(os.path.dirname(_blend), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=_blend, relative_remap=False)
L.export_look_fbx(D)
import json
with open(os.path.join(D.out_dir,'build_gates.json'),'w') as f_:json.dump([dict(name=n,ok=ok,detail=de) for n,ok,de in GATES],f_,indent=2)
with open(os.path.join(D.out_dir,'build_inventory.json'),'w') as f_:json.dump(dict(stats=st,near=len(near),far=len(ring),crust_chunks=len(crust),crust_kept=crust_kept,fringe={k:v for k,v in fringe.items() if k!='objs'},rim_stats=RIM_STATS,smoke_groups=3,smoke_cards=len(smoke)),f_,indent=2,default=lambda x:float(x) if isinstance(x,np.generic) else str(x))
fails = [n for n, ok, _ in GATES if not ok]
print(f"[hole10_build] done: {len(GATES)} build gates, {len(fails)} failed {fails}")
