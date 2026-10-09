"""postcard_look_lib: the POSTCARD_LOOK ground / water / wall / path / lava pass on top of the verified flat build.

Owner: role A3 of the POSTCARD_LOOK job (brief ArtDir/environments/14_POSTCARD_LOOK.txt, interfaces
work/postcard-look/LOOK_CONTRACT.md, notes work/postcard-look/LIB_GROUND.md, v2 = 2026-10-04 continue pass, section "v2 2026-10-04").
postcard_lib.py stays the verified geometry builder; everything here is a POST-PASS run after a hole's normal build (build_terrain,
build_play_surfaces, build_water ... are already done). It never moves a vertex of a TERRAIN / FAIRWAY / GREEN / TEE_BOX / BUNKER /
CART_PATH mesh: it only (a) swaps their materials to the LK_* names Unity's GolfLook knows (and, on Crater, re-materials the wall's grass
lip band to LK_BASALT: slot only), (b) writes their UV0, and (c) adds NEW non-colliding meshes (ROCK_SKIN_*, WATER_SHELF, WATER_SURF_nn,
a subdivided WATER_OCEAN / WATER_LAVA, DRESS_PATH_nn, LAVA_LIGHT_nn empties). snapshot_ground / compare_ground measure that promise.

Copy-paste recipe for a hole build script (after the existing build, BEFORE render/save/export):

    import postcard_look_lib as L
    L.look_materials(D)                         # LK_* materials (early: props libs can then use L.look_material(name))
    snap = L.snapshot_ground(D)                 # optional: proof that the play surfaces do not move
    # ... the hole's own scenery (rocks, sea stacks, arch, plants) ...
    L.retarget_materials(D)                     # MAT_* -> LK_*, MAT_FOAM objects deleted, duplicate slots merged (idempotent:
                                                #   run it again after adding more MAT_* geometry)
    #   crater: L.retarget_materials(D, overrides=L.CRATER_OVERRIDES)     (cliff / dark rock -> LK_BASALT)
    #   Split : L.assign_region_material(D, L.spine_polygon(D.SCENERY["ridge"]["spine"], 33.0), "LK_SCRUB")
    L.build_cliff_skin(D, style="strata")       # Needle / Split: dark layered rock cladding every TERRAIN wall, jagged headlands
    #   crater: rim = loops without the pin, pillar = the loop around the pin (see postcard_look_smoke.py):
    #           L.build_cliff_skin(D, "basalt", loops=rim); L.build_cliff_skin(D, "basalt", loops=pillar, follow_undercut=1.0, seed=1)
    #           (basalt = backing sheet + hex prisms with tops 6.8-8.5 cm under PLAY_Z; covers the wall down to its foot)
    L.build_sea(D)                              # ocean grid + THIN WATER_SHELF edge + sparse WATER_SURF_nn patches (needs the skin first)
    #   Needle waterfall foot: fp = L.open_shore_point(D, (x_m, y_m)); foot = (fp[0] + 3 * fp[2], fp[1] + 3 * fp[3])
    #                          L.build_sea(D, pockets=[(foot[0], foot[1], 6.0)]); L.surf_patch(D, foot, radius_m=4.0)
    #   crater: L.build_lava(D, lights=4)       # subdivided WATER_LAVA (LK_LAVA) + LAVA_LIGHT_nn empties; no ocean/shelf/surf
    #   Needle: L.build_path(D, [(-9, -6), (-8.5, 12), (-6, 28), (-3, 40)], width_m=2.6)   # course yards -> DRESS_PATH_nn
    L.assign_uvs(D)                             # UV0 in tile units on every ground + water mesh (run LAST, after regions)
    ok, detail = L.compare_ground(D, snap)      # vertex positions of every ground mesh identical within 1e-5 m
    for name, ok, detail in L.audit_look(D): print(f"GATE: {name} {'PASS' if ok else 'FAIL'} - {detail}")
    for name, ok, detail in L.v2_gates(D, pockets=L.sea_pockets(D, pockets)): print(f"GATE: {name} {'PASS' if ok else 'FAIL'} - {detail}")
    ... P.render_overview(D); P.save(D)
    L.export_look_fbx(D)                        # = postcard_lib.export_fbx + path_mode STRIP, no embedded textures, NO texture
                                                #   references at all (see export_look_fbx), colours LINEAR (WATER_SURF A/R raw)
Gate a finished hole the same way the smoke test does: blender/scripts/postcard_look_smoke.py (+ postcard_verify.py; its
MATERIALS gate only knows the old MAT_* names and fails by design on an LK_* build).

API (all metres unless a name says _yd; D is postcard_lib's design handle)
    look_materials(D=None, rebuild=False) -> {name: Material}      every LK_* of LOOK_CONTRACT section 3
    look_material(name) -> Material                                get-or-create ONE LK_* material (props agents: use this, never
                                                                   bpy.data.materials.new, so names never become 'LK_ROCK.001')
    refresh_look_textures() -> [missing png names]                 re-link the PNGs once texture agent A1 has written them
    retarget_materials(D, overrides=None, delete_foam=True, purge=False) -> dict counters   (idempotent)
    assign_region_material(D, polygon, material='LK_SCRUB', units='yd', objects=None, top_only=True) -> faces switched
    spine_polygon(spine_yd, half_width_yd) -> polygon (course yards)   buffer of a polyline (for assign_region_material)
    assign_uvs(D, objects=None) -> {object: mode counts}           UV0 per LOOK_CONTRACT section 4 (see UV RULES)
    build_cliff_skin(D, style='strata'|'basalt', loops=None, seed=0, replace=False, keep_out_water=True, jag_m=2.0, max_out_m=2.6,
                     wall_material='LK_BASALT', basalt_backing=True, **opts) -> [objects ROCK_SKIN_nn]
    wall_to_material(D, material='LK_BASALT', from_material='LK_ROUGH') -> faces   TERRAIN wall faces' slot (geometry untouched)
    build_sea(D, ocean=True, shelf=True, shelf_m=(0.5, 2.9), pockets=None, surf=True, surf_cover=0.19, surf_min_cover=0.10,
              rock_puffs=6, **opts) -> {name: obj}               WATER_OCEAN grid, THIN WATER_SHELF edge, sparse WATER_SURF_nn patches
    sea_pockets(D, pockets=None, auto=True) -> [(x, y, r)]         the shelf pockets build_sea uses (caller's + Split's channel gap)
    open_shore_point(D, near_m, free_m=40) -> (x, y, nx, ny)       shore column with open water in front (waterfall foot, reef)
    surf_patch(D, center_m, radius_m=4.0, name=None, seed=0) -> obj   one extra whitewater patch (waterfall foot; radius <= 4.2)
    build_lava(D, **opts) -> {name: obj}                           WATER_LAVA subdivided + LAVA_LIGHT_nn empties
    build_path(D, polyline, width_m=2.4, units='yd', step_m=0.15, across_m=0.15, z_off=0.0115, **opts) -> [objects DRESS_PATH_nn]
    export_look_fbx(D, path=None) -> path                          use this, NOT postcard_lib.export_fbx (texture refs / colours)
    box_uv(ob, tile=None) -> ob                                    dominant-axis box UV, each face in its material's tile
    snapshot_ground(D) -> dict ; compare_ground(D, snap) -> (ok, detail) ; audit_look(D) -> [(name, ok, detail)]
    v2_gates(D, pockets=None) -> [(id, ok, detail)]                SHELF_THIN, NO_FOAM_STRIP, PATH_UNDER_BALL, CRATER_WALL_COVERED,
                                                                   SKIN_NOT_OVER_WATER, SKIN_FACES_OUTWARD (+ skin_tortuosity)
    fbx_check(path, expect) -> [(name, ok, detail)]                re-import check (DESTROYS the open scene: run it last)
    terrain_top_loops(D) -> [(P (n,2), vertex indices)]            boundary loops of the TERRAIN top, land on the LEFT

MATERIALS. LOOK table below = LOOK_CONTRACT section 3 (texture stem, tile metres, URP smoothness S, fallback sRGB colour).
Principled BSDF: Image Texture (<stem>_C, sRGB) -> Base Color, (<stem>_N, Non-Color) -> Normal Map -> Normal, (<stem>_E, sRGB)
-> Emission Color; Roughness = 1 - S. When a PNG is missing the material gets the flat fallback colour of the right hue and the
name is listed in D.look_missing (lava / surf get a procedural stand-in so Blender previews still read). Every image reaches the
BSDF through an identity Gamma node, so the FBX carries the material NAMES (what Unity's GolfLook keys on) and NO texture
reference: the hole .meta files use materialName 0 (Unity 'By Base Texture Name'), which would otherwise rename an
imported LK_FAIRWAY to 'Fairway_C'. GolfLook loads the PNGs from Resources/Course/Look by material name.

UV RULES (UV0 only, tile units = metres / tile, LOOK_CONTRACT section 4; every object gets an integer shift so values stay
near 0 - textures repeat, so an integer shift changes nothing on screen):
    planar      LK_ROUGH / LK_SCRUB / LK_GREEN / LK_SAND / LK_PATH / water / lava faces: u = x / tile, v = y / tile (world);
                LK_ROUGH / LK_SCRUB / LK_LAVA get a smooth domain warp first (UV_WARP: |gradient| <= ~0.45, no seam) so the
                12 m / 24 m tile does not read as a grid from the overview
    centerline  LK_FAIRWAY faces: u = signed lateral offset from the design CENTERLINE (+ = right of play), v = arc length
                along it. The polyline is Chaikin-smoothed (5 rounds) and extended 120 m past both ends, sampled every
                0.25 m, so the projection is continuous across the kinks, the round tee end, the apron past the pin and the
                water-clipped pads: stripes follow the line of play, no seam inside a face.
    wall        TERRAIN faces that are not top faces: u = metres around the boundary loop (each wall column inherits the
                arc length of the top vertex it hangs from; per-face unwrap at the loop seam), v = z.
    box         LK_ROCK / LK_ROCK_WET / LK_MASONRY / LK_CLIFF* on non-TERRAIN meshes without their own UVs: per-face
                dominant-axis projection (props libs normally write their own UVs).
ROCK_SKIN builds its own UVs: u = arc / tile, v = (z - outward offset) / tile (ledges unroll instead of smearing).

DEPTH STACK (z above the sea / lava, all WATER_* inside z 0 .. 0.2 as postcard_verify's WATER_LEVEL gate wants):
    WATER_OCEAN / WATER_LAVA 0.00, WATER_SHELF 0.06, WATER_SURF_nn 0.12, DRESS_PATH 0.0115 above the LOWEST collider sample of its cell
    (never more than 1.2 cm over the ground under any vertex, never more than 1.5 cm anywhere).
    Unity: 1 m = 1.0936 yd, near plane ~0.3 yd. With Metal's reversed-Z float depth the step at 300 yd is far below 1 mm;
    even a 24-bit fixed depth buffer gives z^2 / (near * 2^24) = 274^2 / (0.27 * 16.8e6) = 0.017 m at 300 yd, so 6 cm
    separations are >= 3 depth steps everywhere a player can see them (the old build used 4 / 8 / 9 cm and never fought).

Z-FIGHTING OF THE PATH (judged: no Unity-side fix needed, remedy ready). A path 1.15 cm over the ground is only safe if the depth
    buffer resolves 1 cm at the distances where the path is visible. Unity 6 URP on Metal uses reversed-Z with a 32-bit FLOAT depth
    (D32_SFloat_S8): the resolution is ~z * 6e-8, i.e. 2e-5 m at 300 yd (900 yd far plane: 5e-5 m), and even 8 ULP of rasteriser
    interpolation error stays under 2 mm at 300 m, so 1.15 cm has a margin of > 50x everywhere. Only a 24-bit unorm fallback
    (z^2 / (near * 2^24) = 0.017 m at 300 yd) would shimmer, and only far down the course. If the Game view still shows shimmer at
    > 100 m, apply on the LK_PATH material (Unity owner, not done here): render queue Geometry+10, ZWrite Off, ZTest LEqual (it then
    draws after the ground and cannot win/lose a depth tie) or a polygon offset of -1,-1 in a custom shader. Do NOT raise the path.

WHY THE OCEAN IS A GRID. TennisWater computes linear fog PER VERTEX (ComputeFogFactor in vert, saturated there): a 4-vertex
4000 m plane has all four vertices past the fog end, so the whole sea comes out as flat fog colour. WATER_OCEAN stays ONE
object and ONE flat plane at z = 0 (world-space shader, no UV needed), but it is a graded grid: 16 m cells over the course
growing to 400 m at the 2 km rim, cells fully under the land (12 m inside the shore) dropped.
"""
import bpy
import bmesh
import math
import os
import sys
import time
import random

import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

try:
    HERE = os.path.dirname(os.path.abspath(__file__))
except NameError:
    HERE = os.getcwd()
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import postcard_lib as P  # noqa: E402

YD = P.YD
LOOK_DIR = os.path.join(P.COURSE_DIR, "Look")
GROUND_PREFIXES = P.GROUND_PREFIXES
SAFE_PREFIXES = ("ROCK_", "PLANT_", "DRESS_", "WATER_", "LAVA_")

Z_SHELF = 0.06
Z_SURF = 0.12
Z_PATH = 0.0115                 # path top over the LOWEST collider sample of its cell (was 0.035 over the highest within 0.5 m)

# name: (texture stem or None, maps, tile_m, smoothness, fallback sRGB, uv mode)
LOOK = {
    "LK_FAIRWAY": ("Fairway", "CN", 10.0, 0.10, (88, 172, 60), "centerline"),
    "LK_GREEN": ("Green", "CN", 6.0, 0.12, (124, 204, 74), "planar"),
    "LK_ROUGH": ("Rough", "CN", 12.0, 0.08, (66, 130, 46), "planar"),
    "LK_SCRUB": ("Scrub", "CN", 12.0, 0.08, (128, 124, 66), "planar"),
    "LK_SAND": ("Sand", "CN", 6.0, 0.12, (232, 212, 162), "planar"),
    "LK_CLIFF": ("Cliff", "CN", 12.0, 0.12, (82, 80, 76), "wall"),
    "LK_CLIFF_DARK": ("CliffDark", "CN", 12.0, 0.35, (52, 58, 54), "wall"),
    "LK_ROCK": ("Rock", "CN", 4.0, 0.12, (120, 114, 106), "box"),
    "LK_ROCK_WET": ("Rock", "CN", 4.0, 0.35, (86, 84, 80), "box"),
    "LK_PATH": ("Path", "CN", 5.0, 0.10, (206, 200, 184), "planar"),
    "LK_MASONRY": ("Masonry", "CN", 4.0, 0.10, (170, 162, 148), "box"),
    "LK_BASALT": ("Basalt", "CNE", 8.0, 0.15, (40, 38, 40), "wall"),
    "LK_LAVA": ("Lava", "CNE", 24.0, 0.30, (64, 28, 16), "planar"),
    "LK_PLANTS": ("Plants", "C", 0.0, 0.10, (98, 160, 72), None),
    "LK_WATER": (None, "", 8.0, 0.90, (22, 86, 172), "planar"),
    "LK_WATER_SHALLOW": (None, "", 8.0, 0.90, (40, 186, 196), "planar"),
    "LK_SURF": ("Surf", "C", 8.0, 0.0, (242, 250, 255), "planar"),
    "LK_FALL": ("Fall", "C", 0.0, 0.0, (222, 240, 250), None),
    "LK_SMOKE": ("Smoke", "C", 0.0, 0.0, (120, 110, 112), None),
}
ALPHA_MATS = ("LK_SURF", "LK_FALL", "LK_SMOKE")

RETARGET = {"MAT_ROUGH": "LK_ROUGH", "MAT_FAIRWAY": "LK_FAIRWAY", "MAT_FAIRWAY_STRIPE": "LK_FAIRWAY",
            "MAT_FIRSTCUT": "LK_ROUGH", "MAT_GREEN": "LK_GREEN", "MAT_BUNKER_LIP": "LK_GREEN", "MAT_SAND": "LK_SAND",
            "MAT_CLIFF": "LK_CLIFF", "MAT_CLIFF_DARK": "LK_CLIFF_DARK", "MAT_ROCK": "LK_ROCK", "MAT_ROCK_DARK": "LK_ROCK",
            "MAT_WATER": "LK_WATER", "MAT_WATER_SHALLOW": "LK_WATER_SHALLOW", "MAT_LAVA": "LK_LAVA"}
CRATER_OVERRIDES = {"MAT_CLIFF": "LK_BASALT", "MAT_CLIFF_DARK": "LK_BASALT", "MAT_ROCK_DARK": "LK_BASALT"}
KEEP_MATS = ("MAT_FLAG", "MAT_POLE", "MAT_CUP", "MAT_BALL")


def _log(msg):
    print(f"[postcard_look_lib] {msg}", flush=True)


def _track(D, ob):
    if D is not None:
        lst = D.__dict__.setdefault("look_objects", [])
        if ob.name not in lst:
            lst.append(ob.name)
    return ob


# ============================================================================= noise (deterministic numpy value noise)
def _hash(ix, iy, seed):
    x = (np.asarray(ix, np.int64) * 0x27D4EB2D) ^ (np.asarray(iy, np.int64) * 0x165667B1) ^ np.int64((seed * 0x9E3779B1) & 0xFFFFFFFF)
    x = x & 0xFFFFFFFF
    x = x ^ (x >> 15)
    x = (x * 0x2C1B3C6D) & 0xFFFFFFFF
    x = x ^ (x >> 12)
    x = (x * 0x297A2D39) & 0xFFFFFFFF
    x = x ^ (x >> 15)
    return (x & 0xFFFFFF).astype(np.float64) / 16777216.0


def vnoise(X, Y, scale, seed=0):
    """Smooth value noise in [0, 1) at world points (X, Y), feature size `scale` metres."""
    x = np.asarray(X, float) / scale
    y = np.asarray(Y, float) / scale
    ix, iy = np.floor(x), np.floor(y)
    fx, fy = x - ix, y - iy
    u, v = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    ix, iy = ix.astype(np.int64), iy.astype(np.int64)
    a, b = _hash(ix, iy, seed), _hash(ix + 1, iy, seed)
    c, d = _hash(ix, iy + 1, seed), _hash(ix + 1, iy + 1, seed)
    return (a + (b - a) * u) * (1 - v) + (c + (d - c) * u) * v


def fbm(X, Y, scale, seed=0, octaves=3, gain=0.5):
    tot, amp, norm = 0.0, 1.0, 0.0
    for k in range(octaves):
        tot = tot + amp * vnoise(X, Y, scale / (2 ** k), seed + 101 * k)
        norm += amp
        amp *= gain
    return tot / norm


def loop_noise(s, L, scale, seed=0, octaves=2):
    """Periodic 1-D noise along a closed loop of length L (s in metres): noise on a circle of circumference L."""
    R = L / (2 * math.pi)
    th = np.asarray(s, float) / max(R, 1e-6)
    return fbm(R * np.cos(th) + 1000.0, R * np.sin(th) - 700.0, scale, seed, octaves)


def _blocks(L, lo, hi, rnd):
    """Piecewise-constant random blocks along [0, L): (edges array, values array)."""
    edges, s = [0.0], 0.0
    while s < L:
        s += rnd.uniform(lo, hi)
        edges.append(min(s, L))
    edges = np.array(edges)
    return edges, np.array([rnd.random() for _ in range(len(edges) - 1)])


def _blocks_mixed(L, rnd, short=(1.6, 3.0), mid=(3.0, 8.0), long_=(8.0, 15.0), p_short=0.22, p_long=0.22):
    """Like _blocks but heavy-tailed block lengths (22 % short, 56 % medium, 22 % long): joints at near-even spacing read as a regular grid
    (review 2026-10-04: 'cliff strata repeat in regular columns')."""
    edges, s = [0.0], 0.0
    while s < L:
        r_ = rnd.random()
        s += rnd.uniform(*short) if r_ < p_short else (rnd.uniform(*long_) if r_ > 1.0 - p_long else rnd.uniform(*mid))
        edges.append(min(s, L))
    edges = np.array(edges)
    return edges, np.array([rnd.random() for _ in range(len(edges) - 1)])


def _block_at(s, edges, vals):
    k = np.clip(np.searchsorted(edges, s, side="right") - 1, 0, len(vals) - 1)
    return vals[k]


def _smoothstep(e0, e1, x):
    t = np.clip((np.asarray(x, float) - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ============================================================================= materials
def _png(stem, suffix):
    return os.path.join(LOOK_DIR, f"{stem}_{suffix}.png")


def _image(path, colorspace):
    name = os.path.basename(path)
    img = bpy.data.images.get(name)
    if img is None:
        img = bpy.data.images.load(path, check_existing=True)
        img.name = name
    else:
        img.filepath = path
        try:
            img.reload()
        except Exception:
            pass
    try:
        img.colorspace_settings.name = colorspace
    except Exception:
        pass
    if name.endswith("_C.png"):
        img.alpha_mode = 'STRAIGHT'
    return img


def _lin(c):
    return P.rgb(*c)


def _build_material(name, missing):
    stem, maps, tile, S, fallback, _mode = LOOK[name]
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    if mat.node_tree is None:
        mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (500, 0)
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    b.name = "Principled BSDF"
    b.location = (150, 0)
    nt.links.new(b.outputs["BSDF"], out.inputs["Surface"])
    b.inputs["Base Color"].default_value = _lin(fallback)
    b.inputs["Roughness"].default_value = max(0.05, 1.0 - S)
    b.inputs["Metallic"].default_value = 0.0
    b.inputs["Specular IOR Level"].default_value = 0.35 if name.startswith("LK_WATER") else 0.2
    mat.diffuse_color = _lin(fallback)
    mat["lk_by"] = "postcard_look_lib"
    mat["lk_tile_m"] = float(tile)
    mat["lk_smoothness"] = float(S)
    y = 300
    have = {}
    for suf, cs in (("C", "sRGB"), ("N", "Non-Color"), ("E", "sRGB")):
        if stem is None or suf not in maps:
            continue
        path = _png(stem, suf)
        if not os.path.isfile(path):
            missing.append(os.path.basename(path))
            continue
        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = _image(path, cs)
        node.extension = 'EXTEND' if name in ("LK_PLANTS", "LK_FALL", "LK_SMOKE") else 'REPEAT'
        node.interpolation = 'Linear'
        node.location = (-450, y)
        node.label = f"{stem}_{suf}"
        y -= 300
        have[suf] = node
    if "C" in have:
        nt.links.new(_passthru(nt, have["C"]), b.inputs["Base Color"])
    if "N" in have:
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nm.location = (-150, -250)
        nm.inputs["Strength"].default_value = 1.0
        nt.links.new(_passthru(nt, have["N"]), nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    if name in ("LK_BASALT", "LK_LAVA"):
        strength = 2.5 if name == "LK_LAVA" else 2.0
        if "E" in have:
            nt.links.new(_passthru(nt, have["E"]), b.inputs["Emission Color"])
            b.inputs["Emission Strength"].default_value = strength
        elif name == "LK_LAVA":
            _procedural_lava(nt, b)
        else:
            b.inputs["Emission Color"].default_value = (0, 0, 0, 1)
    if name.startswith("LK_WATER"):
        _procedural_ripple(nt, b)
    if name in ALPHA_MATS:
        _alpha_setup(mat, nt, b, have.get("C"), name)
    return mat


def _passthru(nt, img_node):
    """Image -> identity Gamma node -> socket. The FBX exporter only records a texture that is linked DIRECTLY to the
    Principled BSDF / Normal Map node, so the FBX carries no texture reference: the hole FBX .meta files use
    materialName 0 (Unity 'By Base Texture Name'), which would rename an imported LK_FAIRWAY to 'Fairway_C' and hide it
    from GolfLook. Blender previews still show the texture."""
    g = nt.nodes.new("ShaderNodeGamma")
    g.inputs["Gamma"].default_value = 1.0
    g.location = (img_node.location[0] + 220, img_node.location[1])
    nt.links.new(img_node.outputs["Color"], g.inputs["Color"])
    return g.outputs["Color"]


def _procedural_lava(nt, b):
    """Blender-preview stand-in while Lava_C/_E do not exist yet: dark crust plates, glowing fissures (Voronoi edges)."""
    vor = nt.nodes.new("ShaderNodeTexVoronoi")
    vor.feature = 'DISTANCE_TO_EDGE'
    vor.inputs["Scale"].default_value = 5.0
    vor.location = (-700, -400)
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.location = (-450, -400)
    cr = ramp.color_ramp
    cr.elements[0].position = 0.0
    cr.elements[0].color = (1.0, 0.42, 0.05, 1)
    cr.elements[1].position = 0.06
    cr.elements[1].color = (0.03, 0.012, 0.008, 1)
    nt.links.new(vor.outputs["Distance"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Emission Color"])
    b.inputs["Emission Strength"].default_value = 3.0


def _procedural_ripple(nt, b):
    """Blender-preview only: a soft noise bump so a render does not show a dead flat plane (Unity uses TennisWater)."""
    tc = nt.nodes.new("ShaderNodeTexCoord")
    tc.location = (-900, -500)
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.location = (-650, -500)
    nz.inputs["Scale"].default_value = 0.35
    nz.inputs["Detail"].default_value = 6.0
    bump = nt.nodes.new("ShaderNodeBump")
    bump.location = (-350, -500)
    bump.inputs["Strength"].default_value = 0.25
    bump.inputs["Distance"].default_value = 0.2
    nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
    nt.links.new(nz.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], b.inputs["Normal"])


def _alpha_setup(mat, nt, b, cnode, name):
    try:
        mat.surface_render_method = 'BLENDED'
    except Exception:
        pass
    try:
        mat.use_backface_culling = False
    except Exception:
        pass
    b.inputs["Roughness"].default_value = 0.6
    if cnode is not None:
        tex_alpha = cnode.outputs["Alpha"]
    else:                                         # stand-in: soft noise foam
        nz = nt.nodes.new("ShaderNodeTexNoise")
        nz.inputs["Scale"].default_value = 6.0
        nz.inputs["Detail"].default_value = 8.0
        nz.location = (-700, 200)
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.location = (-450, 200)
        ramp.color_ramp.elements[0].position = 0.42
        ramp.color_ramp.elements[1].position = 0.62
        nt.links.new(nz.outputs["Fac"], ramp.inputs["Fac"])
        tex_alpha = ramp.outputs["Color"]
    if name == "LK_SURF":
        vc = nt.nodes.new("ShaderNodeVertexColor")
        vc.layer_name = "Col"
        vc.location = (-450, -650)
        mul = nt.nodes.new("ShaderNodeMath")
        mul.operation = 'MULTIPLY'
        mul.location = (-150, -500)
        nt.links.new(tex_alpha, mul.inputs[0])
        nt.links.new(vc.outputs["Alpha"], mul.inputs[1])
        boost = nt.nodes.new("ShaderNodeMath")                  # preview only: Surf_C alpha averages 0.35
        boost.operation = 'MULTIPLY'
        boost.use_clamp = True
        boost.inputs[1].default_value = 1.8
        boost.location = (0, -500)
        nt.links.new(mul.outputs["Value"], boost.inputs[0])
        nt.links.new(boost.outputs["Value"], b.inputs["Alpha"])
    else:
        nt.links.new(tex_alpha, b.inputs["Alpha"])


def look_material(name, D=None):
    """Get-or-create ONE LK_* material by its exact contract name (never makes 'LK_X.001')."""
    if name not in LOOK:
        raise KeyError(f"{name} is not a LOOK_CONTRACT material")
    mat = bpy.data.materials.get(name)
    if mat is None:
        missing = []
        mat = _build_material(name, missing)
        if D is not None:
            D.__dict__.setdefault("look_missing", [])
            D.look_missing.extend(m for m in missing if m not in D.look_missing)
    if D is not None:
        D.mats[name] = mat
    return mat


def look_materials(D=None, rebuild=False):
    """Create every LK_* material of LOOK_CONTRACT section 3 (see the module docstring). Existing ones made by somebody else
    (no 'lk_by' tag) are left alone unless rebuild=True. Missing PNGs -> flat fallback colour, listed in D.look_missing."""
    missing, out = [], {}
    for name in LOOK:
        mat = bpy.data.materials.get(name)
        if mat is None or rebuild or mat.get("lk_by") == "postcard_look_lib":
            mat = _build_material(name, missing)
        out[name] = mat
        if D is not None:
            D.mats[name] = mat
    missing = sorted(set(missing))
    if D is not None:
        D.look_missing = missing
    _log(f"look_materials: {len(out)} LK_* materials; textures {'ALL PRESENT' if not missing else 'MISSING ' + ', '.join(missing)}")
    return out


def refresh_look_textures():
    """Rebuild the node trees of the LK_* materials this lib made (picks up PNGs written since). Returns missing names."""
    missing = []
    for name in LOOK:
        mat = bpy.data.materials.get(name)
        if mat is not None and mat.get("lk_by") == "postcard_look_lib":
            _build_material(name, missing)
    return sorted(set(missing))


# ============================================================================= retarget
def _export_meshes(D):
    return [o for o in P._export_objects(D) if o.type == 'MESH']


def _dedupe_slots(me):
    mats = list(me.materials)
    uniq, remap = [], []
    for m_ in mats:
        if m_ in uniq:
            remap.append(uniq.index(m_))
        else:
            uniq.append(m_)
            remap.append(len(uniq) - 1)
    if len(uniq) == len(mats):
        return 0
    n = len(me.polygons)
    idx = np.zeros(n, np.int32)
    me.polygons.foreach_get("material_index", idx)
    idx = np.array(remap, np.int32)[np.clip(idx, 0, len(remap) - 1)]
    for k, m_ in enumerate(uniq):
        me.materials[k] = m_
    while len(me.materials) > len(uniq):
        me.materials.pop(index=len(me.materials) - 1)
    me.polygons.foreach_set("material_index", idx)
    me.update()
    return len(mats) - len(uniq)


def retarget_materials(D, overrides=None, delete_foam=True, purge=False):
    """MAT_* -> LK_* on every mesh datablock (instanced rocks included), per RETARGET (+ `overrides`, e.g.
    CRATER_OVERRIDES). Objects whose only material is MAT_FOAM (WATER_FOAM, WATER_FOAM_CREST) are deleted; MAT_FAIRWAY /
    MAT_FAIRWAY_STRIPE collapse into one LK_FAIRWAY slot (stripes live in the texture). Gameplay MAT_FLAG / POLE / CUP /
    BALL stay. Only material slots and material indices change; no vertex moves. Idempotent: run it again after adding
    geometry that still uses MAT_* names. The old MAT_* datablocks stay in D.mats with 0 users (not saved, not exported),
    so hole code that still reads D.mats['MAT_ROUGH'] keeps working; purge=True removes them. Returns counters."""
    if not any(n in bpy.data.materials for n in LOOK):
        look_materials(D)
    table = dict(RETARGET)
    table.update(overrides or {})
    cnt = dict(slots=0, merged=0, foam_objects=0, removed_mats=0)
    if delete_foam:
        for ob in list(bpy.data.objects):
            if ob.type == 'MESH' and len(ob.data.materials) and all(m_ is not None and m_.name.split(".")[0] == "MAT_FOAM"
                                                                   for m_ in ob.data.materials):
                D.objects.pop(ob.name, None)
                P.remove_object(ob.name)
                cnt["foam_objects"] += 1
    for me in bpy.data.meshes:
        changed = False
        for i, m_ in enumerate(list(me.materials)):
            if m_ is None:
                continue
            base = m_.name.split(".")[0]
            if base in table:
                me.materials[i] = look_material(table[base], D)
                cnt["slots"] += 1
                changed = True
        if changed:
            cnt["merged"] += _dedupe_slots(me)
    for ob in bpy.data.objects:                   # object-linked slots (rare)
        for sl in ob.material_slots:
            if sl.link == 'OBJECT' and sl.material is not None and sl.material.name.split(".")[0] in table:
                sl.material = look_material(table[sl.material.name.split(".")[0]], D)
                cnt["slots"] += 1
    for m_ in list(bpy.data.materials):
        if purge and m_.name.startswith("MAT_") and m_.users == 0 and m_.name.split(".")[0] not in KEEP_MATS:
            D.mats.pop(m_.name, None)
            bpy.data.materials.remove(m_)
            cnt["removed_mats"] += 1
    _log(f"retarget_materials: {cnt}")
    return cnt


# ============================================================================= mesh helpers
def _mesh_arrays(ob):
    me = ob.data
    nv, npoly, nl = len(me.vertices), len(me.polygons), len(me.loops)
    co = np.empty(nv * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mw = np.array(ob.matrix_world)
    W = co @ mw[:3, :3].T + mw[:3, 3]
    ls = np.empty(npoly, np.int64)
    lt = np.empty(npoly, np.int64)
    me.polygons.foreach_get("loop_start", ls)
    me.polygons.foreach_get("loop_total", lt)
    lv = np.empty(nl, np.int64)
    me.loops.foreach_get("vertex_index", lv)
    mi = np.empty(npoly, np.int64)
    me.polygons.foreach_get("material_index", mi)
    pn = np.empty(npoly * 3)
    me.polygons.foreach_get("normal", pn)
    pn = pn.reshape(-1, 3) @ np.linalg.inv(mw[:3, :3]) if npoly else pn.reshape(-1, 3)     # (M^-1)^T n as row vectors
    pn = pn / np.maximum(np.linalg.norm(pn, axis=1), 1e-12)[:, None] if npoly else pn
    return W, ls, lt, lv, mi, pn


def _poly_of_loop(ls, lt, nl):
    out = np.empty(nl, np.int64)
    for p, (a, n) in enumerate(zip(ls.tolist(), lt.tolist())):
        out[a:a + n] = p
    return out


def _top_mask(D, W, ls, lt, lv, pn):
    pz = D.play_z
    pol = _poly_of_loop(ls, lt, len(lv))
    off = np.abs(W[lv, 2] - pz) > 1e-3
    bad = np.zeros(len(ls), bool)
    np.logical_or.at(bad, pol, off)
    return (pn[:, 2] > 0.5) & ~bad


def _chain_loops(dir_edges, W):
    """Ordered boundary loops from directed boundary edges (a, b); pinch vertices resolved like postcard_lib."""
    out = {}
    for a, b in dir_edges:
        out.setdefault(a, []).append(b)
    used, loops = set(), []
    for a0, b0 in sorted(dir_edges):
        if (a0, b0) in used:
            continue
        loop, prev, cur = [a0], a0, b0
        used.add((a0, b0))
        guard = 0
        while cur != a0 and guard < len(dir_edges) + 5:
            loop.append(cur)
            cands = [c for c in out.get(cur, []) if (cur, c) not in used]
            if not cands:
                break
            if len(cands) > 1:
                back = W[prev, :2] - W[cur, :2]
                ba = math.atan2(back[1], back[0])
                best, bi = 1e9, 0
                for k, c in enumerate(cands):
                    o = W[c, :2] - W[cur, :2]
                    cw = (ba - math.atan2(o[1], o[0])) % (2 * math.pi)
                    if cw < best:
                        best, bi = cw, k
                nxt = cands[bi]
            else:
                nxt = cands[0]
            used.add((cur, nxt))
            prev, cur = cur, nxt
            guard += 1
        if cur == a0 and len(loop) >= 3:
            loops.append(loop)
    return loops


def terrain_top_loops(D, ob=None):
    """Boundary loops of the TERRAIN top faces traced from the MESH itself (works after hole 8's welds): list of
    (P (n,2) metres, vertex indices (n,)), land on the LEFT of the travel direction (outer loops CCW, holes CW)."""
    ob = ob or _terrain_ob(D)
    W, ls, lt, lv, mi, pn = _mesh_arrays(ob)
    top = _top_mask(D, W, ls, lt, lv, pn)
    edges = set()
    for p in np.nonzero(top)[0].tolist():
        a, n = int(ls[p]), int(lt[p])
        vs = lv[a:a + n].tolist()
        for k in range(n):
            edges.add((vs[k], vs[(k + 1) % n]))
    bnd = [(a, b) for (a, b) in edges if (b, a) not in edges]
    loops = _chain_loops(bnd, W)
    return [(W[np.array(L_)][:, :2].copy(), np.array(L_)) for L_ in loops]


# ============================================================================= centerline parametrisation
class CenterlineParam:
    """Smooth (u, v) coordinates around the design CENTERLINE: u = signed lateral metres (+ right of play), v = arc metres.
    Chaikin-smoothed (5 rounds), extended `ext_m` past both ends, sampled every `step` metres."""

    def __init__(self, D, ext_m=120.0, step=0.25, rounds=5):
        pts = [Vector(P.m(x, d)) for x, d in D.hole.center]
        sm = P.chaikin([(p.x, p.y) for p in pts], iterations=rounds, closed=False) if len(pts) > 2 else pts
        sm = [Vector((p[0], p[1])) for p in sm]
        d0 = (sm[1] - sm[0]).normalized()
        d1 = (sm[-1] - sm[-2]).normalized()
        sm = [sm[0] - d0 * ext_m] + sm + [sm[-1] + d1 * ext_m]
        dense = P.resample([(p.x, p.y) for p in sm], step, closed=False)
        S = np.array([(p.x, p.y) for p in dense])
        seg = np.diff(S, axis=0)
        L = np.hypot(seg[:, 0], seg[:, 1])
        A = np.concatenate([[0.0], np.cumsum(L)])
        tee = np.array(P.m(*D.hole.center[0]))
        k0 = int(np.argmin(np.hypot(S[:, 0] - tee[0], S[:, 1] - tee[1])))
        self.S, self.A = S, A - A[k0]               # v = 0 at the tee station
        self.T = np.vstack([seg / np.maximum(L, 1e-9)[:, None], (seg[-1] / max(L[-1], 1e-9))[None]])
        self.kd = KDTree(len(S))
        for i, (x, y) in enumerate(S.tolist()):
            self.kd.insert((x, y, 0.0), i)
        self.kd.balance()

    def uv(self, X, Y):
        X = np.asarray(X, float).ravel()
        Y = np.asarray(Y, float).ravel()
        idx = np.fromiter((self.kd.find((x, y, 0.0))[1] for x, y in zip(X.tolist(), Y.tolist())), np.int64, len(X))
        n = len(self.S)
        best_d = np.full(len(X), np.inf)
        u = np.zeros(len(X))
        v = np.zeros(len(X))
        for a_off in (-1, 0):
            a = np.clip(idx + a_off, 0, n - 2)
            Pa, Pb = self.S[a], self.S[a + 1]
            ab = Pb - Pa
            l2 = np.maximum((ab ** 2).sum(1), 1e-12)
            t = np.clip(((X - Pa[:, 0]) * ab[:, 0] + (Y - Pa[:, 1]) * ab[:, 1]) / l2, 0.0, 1.0)
            qx, qy = Pa[:, 0] + ab[:, 0] * t, Pa[:, 1] + ab[:, 1] * t
            d = np.hypot(X - qx, Y - qy)
            tx, ty = ab[:, 0] / np.sqrt(l2), ab[:, 1] / np.sqrt(l2)
            lat = (X - qx) * ty - (Y - qy) * tx          # right of travel (ty, -tx) is positive
            arc = self.A[a] + t * np.sqrt(l2)
            better = d < best_d
            best_d = np.where(better, d, best_d)
            u = np.where(better, lat, u)
            v = np.where(better, arc, v)
        return u, v


# ============================================================================= UVs
def _mat_name(m_):
    return m_.name.split(".")[0] if m_ is not None else ""


def _uv_layer(me):
    while len(me.uv_layers) > 1:
        me.uv_layers.remove(me.uv_layers[-1])
    if len(me.uv_layers) == 0:
        me.uv_layers.new(name="UVMap")
    lay = me.uv_layers[0]
    lay.name = "UVMap"
    me.uv_layers.active = lay
    return lay


def _wall_arc(D, ob, W, ls, lt, lv, pn, top):
    """Per-vertex (arc metres along its boundary loop, loop length) for TERRAIN wall vertices: each wall column inherits
    the arc of the top boundary vertex it hangs from (via its vertical edges)."""
    me = ob.data
    nv = len(W)
    s_of = np.full(nv, np.nan)
    L_of = np.full(nv, np.nan)
    for Pl, vidx in terrain_top_loops(D, ob):
        seg = np.roll(Pl, -1, axis=0) - Pl
        Ls = np.hypot(seg[:, 0], seg[:, 1])
        s = np.concatenate([[0.0], np.cumsum(Ls)[:-1]])
        s_of[vidx] = s
        L_of[vidx] = Ls.sum()
    ev = np.empty(len(me.edges) * 2, np.int64)
    me.edges.foreach_get("vertices", ev)
    ev = ev.reshape(-1, 2)
    za, zb = W[ev[:, 0], 2], W[ev[:, 1], 2]
    vert = np.abs(za - zb) > 1e-3
    up = np.where(za > zb, ev[:, 0], ev[:, 1])[vert]
    lo = np.where(za > zb, ev[:, 1], ev[:, 0])[vert]
    dxy = np.hypot(*(W[up, :2] - W[lo, :2]).T)
    order = np.lexsort((dxy, lo))
    parent = np.full(nv, -1, np.int64)
    lo_s, up_s = lo[order], up[order]
    first = np.ones(len(lo_s), bool)
    first[1:] = lo_s[1:] != lo_s[:-1]
    parent[lo_s[first]] = up_s[first]
    for v in np.argsort(-W[:, 2]).tolist():
        if np.isnan(s_of[v]) and parent[v] >= 0 and not np.isnan(s_of[parent[v]]):
            s_of[v] = s_of[parent[v]]
            L_of[v] = L_of[parent[v]]
    miss = np.isnan(s_of)
    if miss.any():                                  # fallback: nearest arc-known vertex in plan
        known = np.nonzero(~miss)[0]
        kd = KDTree(len(known))
        for i, k in enumerate(known.tolist()):
            kd.insert((W[k, 0], W[k, 1], 0.0), i)
        kd.balance()
        for v in np.nonzero(miss)[0].tolist():
            k = known[kd.find((W[v, 0], W[v, 1], 0.0))[1]]
            s_of[v], L_of[v] = s_of[k], L_of[k]
    return s_of, L_of


# planar materials whose UVs get a smooth domain warp (amp1 m, wavelength1 m, amp2 m, wavelength2 m): a 12 m / 24 m tile
# repeated over 700 m reads as a grid from the overview; a warp with |gradient| <= ~0.45 hides it with no seam
UV_WARP = {"LK_LAVA": (5.0, 110.0, 2.5, 70.0), "LK_ROUGH": (2.2, 53.0, 0.8, 29.0), "LK_SCRUB": (2.2, 53.0, 0.8, 29.0)}


def _warp(X, Y, a1, l1, a2, l2):
    t = 2 * math.pi
    xw = X + a1 * np.sin(t * Y / l1 + 1.3) + a2 * np.sin(t * (X + Y) / (l2 * 1.41) + 0.2)
    yw = Y + a1 * np.sin(t * X / (l1 * 0.91) + 0.4) + a2 * np.sin(t * (X - Y) / (l2 * 1.29) + 2.1)
    return xw, yw


def assign_uvs(D, objects=None, fill_missing=True):
    """UV0 (one layer 'UVMap') in tile units on every ground-prefix and WATER_* mesh (or `objects`, names or objects), per
    the face material (UV RULES in the module docstring). Vertex positions are never touched. fill_missing: every other
    exported mesh that carries an LK_* material but NO UV layer at all (library rocks, a hole's own sea-stack columns) gets
    an object-space box UV in its material's tile units (shared mesh data: all instances share it); meshes that already
    have UVs (props libs) are left alone. Returns {name: modes}."""
    cl = CenterlineParam(D)
    if objects is None:
        obs = [o for o in _export_meshes(D) if o.name.startswith(GROUND_PREFIXES) or
               (o.name.startswith("WATER_") and not o.name.startswith(("WATER_SURF", "WATER_FALL")))]
    else:
        obs = [bpy.data.objects[o] if isinstance(o, str) else o for o in objects]
    report = {}
    for ob in obs:
        me = ob.data
        if len(me.polygons) == 0:
            continue
        W, ls, lt, lv, mi, pn = _mesh_arrays(ob)
        pol = _poly_of_loop(ls, lt, len(lv))
        mats = [_mat_name(m_) for m_ in me.materials] or [""]
        fmode = []
        ftile = []
        is_terrain = ob.name.startswith("TERRAIN")
        top = _top_mask(D, W, ls, lt, lv, pn) if is_terrain else None
        for p in range(len(ls)):
            name = mats[min(int(mi[p]), len(mats) - 1)]
            spec = LOOK.get(name)
            tile = spec[2] if spec and spec[2] > 0 else 8.0
            mode = spec[5] if spec and spec[5] else "planar"
            if is_terrain:
                mode = "planar" if top[p] else "wall"
            elif mode == "wall":
                mode = "box"
            elif mode == "box" and pn[p, 2] > 0.9:
                mode = "planar"
            fmode.append(mode)
            ftile.append(tile)
        fmode = np.array(fmode)
        ftile = np.array(ftile)
        fname = np.array([mats[min(int(mi[p]), len(mats) - 1)] for p in range(len(ls))])
        lmode, ltile, lname = fmode[pol], ftile[pol], fname[pol]
        X, Y, Z = W[lv, 0], W[lv, 1], W[lv, 2]
        U = np.zeros(len(lv))
        V = np.zeros(len(lv))
        m = lmode == "planar"
        U[m], V[m] = X[m] / ltile[m], Y[m] / ltile[m]
        for p_mat, wp in UV_WARP.items():                 # smooth domain warp: breaks the visible tile grid, no seams
            mm = m & (lname == p_mat)
            if mm.any():
                xw, yw = _warp(X[mm], Y[mm], *wp)
                U[mm], V[mm] = xw / ltile[mm], yw / ltile[mm]
        m = lmode == "centerline"
        if m.any():
            uu, vv = cl.uv(X[m], Y[m])
            U[m], V[m] = uu / ltile[m], vv / ltile[m]
        m = lmode == "wall"
        if m.any():
            s_of, L_of = _wall_arc(D, ob, W, ls, lt, lv, pn, top)
            s = s_of[lv]
            Lp = L_of[lv]
            for p in np.unique(pol[m]).tolist():          # unwrap each face at its loop seam
                a, n = int(ls[p]), int(lt[p])
                ss = s[a:a + n]
                half = 0.5 * np.nanmax(Lp[a:a + n])
                ss = np.where(ss - ss[0] > half, ss - 2 * half, np.where(ss - ss[0] < -half, ss + 2 * half, ss))
                s[a:a + n] = ss
            U[m], V[m] = s[m] / ltile[m], Z[m] / ltile[m]
        m = lmode == "box"
        if m.any():
            n_ = pn[pol]
            ax = np.argmax(np.abs(n_), axis=1)
            bx = m & (ax == 0)
            by = m & (ax == 1)
            bz = m & (ax == 2)
            U[bx], V[bx] = Y[bx] / ltile[bx], Z[bx] / ltile[bx]
            U[by], V[by] = X[by] / ltile[by], Z[by] / ltile[by]
            U[bz], V[bz] = X[bz] / ltile[bz], Y[bz] / ltile[bz]
        for mode in ("planar", "centerline", "wall", "box"):          # integer shift per mode: values near 0
            m = lmode == mode
            if m.any():
                if mode != "centerline":
                    U[m] -= np.round(np.mean(U[m]))
                V[m] -= np.round(np.mean(V[m]))
        lay = _uv_layer(me)
        lay.data.foreach_set("uv", np.stack([U, V], 1).ravel())
        me.update()
        report[ob.name] = {k: int((fmode == k).sum()) for k in ("planar", "centerline", "wall", "box") if (fmode == k).any()}
    if fill_missing and objects is None:
        done = set()
        for ob in _export_meshes(D):
            me = ob.data
            if ob in obs or me.name in done or len(me.uv_layers) or not len(me.polygons):
                continue
            lk = [_mat_name(m_) for m_ in me.materials if m_ is not None and _mat_name(m_) in LOOK]
            if not lk:
                continue
            box_uv(ob, tile=None, object_space=True)
            done.add(me.name)
        if done:
            report["(box fill)"] = len(done)
    _log("assign_uvs: " + ", ".join(f"{k} {v}" for k, v in report.items()))
    return report


def box_uv(ob, tile=None, object_space=True):
    """Per-face dominant-axis box projection in tile units for a prop mesh (object space by default, so instances share
    it). tile None = each face uses the contract tile of its own LK_* material (4 m when it has none); a number forces one."""
    me = ob.data
    co = np.array([v.co[:] for v in me.vertices])
    if not object_space:
        mw = np.array(ob.matrix_world)
        co = co @ mw[:3, :3].T + mw[:3, 3]
    mats = [_mat_name(m_) for m_ in me.materials] or [""]
    lay = _uv_layer(me)
    uv = np.zeros((len(me.loops), 2))
    for p in me.polygons:
        t = tile if tile else (LOOK[mats[min(p.material_index, len(mats) - 1)]][2]
                               if mats[min(p.material_index, len(mats) - 1)] in LOOK else 0.0) or 4.0
        ax = int(np.argmax(np.abs(p.normal[:])))
        a, b = [(1, 2), (0, 2), (0, 1)][ax]
        for li in p.loop_indices:
            c = co[me.loops[li].vertex_index]
            uv[li] = (c[a] / t, c[b] / t)
    lay.data.foreach_set("uv", uv.ravel())
    return ob


# ============================================================================= region material
def spine_polygon(spine_yd, half_width_yd):
    """Polygon (course yards) = the polyline `spine_yd` buffered by half_width_yd (flat ends). For assign_region_material."""
    S = np.array(spine_yd, float)
    tg = np.gradient(S, axis=0)
    tg /= np.maximum(np.hypot(tg[:, 0], tg[:, 1]), 1e-9)[:, None]
    nrm = np.stack([-tg[:, 1], tg[:, 0]], 1)
    left = S + nrm * half_width_yd
    right = S - nrm * half_width_yd
    return [tuple(p) for p in np.vstack([right, left[::-1]])]


def assign_region_material(D, polygon, material="LK_SCRUB", units="yd", objects=None, top_only=True):
    """Give every (top) face of `objects` (default: the TERRAIN) whose centroid lies inside `polygon` the material
    `material` (added as a slot when missing). polygon in course yards (units='yd') or metres ('m'). Geometry untouched; the
    boundary follows the existing triangles (about 6.5 m on the terrain top). Run assign_uvs afterwards. Returns count."""
    poly = np.array(polygon, float) * (YD if units == "yd" else 1.0)
    obs = objects or [D.terrain or bpy.data.objects[D.terrain_name]]
    mat = look_material(material, D) if material in LOOK else bpy.data.materials[material]
    n = 0
    for ob in obs:
        ob = bpy.data.objects[ob] if isinstance(ob, str) else ob
        me = ob.data
        W, ls, lt, lv, mi, pn = _mesh_arrays(ob)
        pol = _poly_of_loop(ls, lt, len(lv))
        cen = np.zeros((len(ls), 2))
        np.add.at(cen, pol, W[lv, :2])
        cen /= lt[:, None]
        sel = P._inside_np([tuple(p) for p in poly], cen[:, 0], cen[:, 1])
        if top_only:
            sel &= (pn[:, 2] > 0.5) & (_top_mask(D, W, ls, lt, lv, pn) if ob.name.startswith("TERRAIN") else True)
        if not sel.any():
            continue
        if mat.name not in [m_.name for m_ in me.materials if m_]:
            me.materials.append(mat)
        k = [m_.name if m_ else "" for m_ in me.materials].index(mat.name)
        mi[sel] = k
        me.polygons.foreach_set("material_index", mi.astype(np.int32))
        me.update()
        n += int(sel.sum())
    _log(f"assign_region_material: {n} faces -> {material}")
    return n


# ============================================================================= cliff skin
def _terrain_bvh(D):
    ob = _terrain_ob(D)
    W, ls, lt, lv, mi, pn = _mesh_arrays(ob)
    polys = [lv[a:a + n].tolist() for a, n in zip(ls.tolist(), lt.tolist())]
    return BVHTree.FromPolygons([tuple(p) for p in W.tolist()], polys)


def _loop_columns(Pl, step):
    """Columns along a closed loop: every loop vertex plus evenly spaced points on each edge (spacing <= step).
    Returns positions (m,2), arc s (m,), loop length, outward unit normals (m,2) (right of travel), is_vertex (m,)."""
    n = len(Pl)
    pos, s_arr, isv, nrm = [], [], [], []
    seg = np.roll(Pl, -1, axis=0) - Pl
    Ls = np.hypot(seg[:, 0], seg[:, 1])
    tg = seg / np.maximum(Ls, 1e-9)[:, None]
    en = np.stack([tg[:, 1], -tg[:, 0]], 1)                      # outward = right of travel (land is on the left)
    acc = 0.0
    for i in range(n):
        k = max(1, int(math.ceil(Ls[i] / step)))
        for j in range(k):
            t = j / k
            pos.append(Pl[i] + seg[i] * t)
            s_arr.append(acc + Ls[i] * t)
            isv.append(j == 0)
            if j == 0:
                b = en[i - 1] + en[i]
                bl = math.hypot(*b)
                nrm.append(b / bl if bl > 0.3 else en[i])
            else:
                nrm.append(en[i])
        acc += Ls[i]
    N = np.array(nrm)
    for _ in range(2):                                           # light smoothing of the normals
        N = 0.5 * N + 0.25 * (np.roll(N, 1, axis=0) + np.roll(N, -1, axis=0))
        N /= np.maximum(np.hypot(N[:, 0], N[:, 1]), 1e-9)[:, None]
    return np.array(pos), np.array(s_arr), acc, N, np.array(isv)


def _wall_inset(bvh, T, N, Z, start=5.0, reach=16.0):
    """Distance (>= 0) from the top-edge point T along -N to the TERRAIN wall at height Z (per column x row)."""
    out = np.zeros(Z.shape)
    for i in range(Z.shape[0]):
        for r in range(Z.shape[1]):
            o = Vector((T[i, 0] + N[i, 0] * start, T[i, 1] + N[i, 1] * start, Z[i, r]))
            hit = bvh.ray_cast(o, Vector((-N[i, 0], -N[i, 1], 0.0)), reach + start)
            if hit[0] is not None:
                out[i, r] = max(0.0, hit[3] - start)
    return out


def _unfold_rows(Pc, N, R, tg):
    """Reduce offsets R (cols x rows) where the offset curve P + N r folds back (concave corners / deep insets)."""
    for _ in range(25):
        Q = Pc[:, None, :] + N[:, None, :] * R[:, :, None]
        dq = np.roll(Q, -1, axis=0) - Q
        ok = (dq * tg[:, None, :]).sum(2) > 0.05
        if ok.all():
            break
        bad = ~ok | np.roll(~ok, 1, axis=0)
        avg = 0.25 * (np.roll(R, 1, axis=0) + 2 * R + np.roll(R, -1, axis=0))
        R = np.where(bad, 0.4 * R + 0.6 * np.minimum(avg, R), R)
    return R


def _finish_mesh(ob, sharp_deg=38.0):
    me = ob.data
    me.validate()
    me.update()
    try:
        me.shade_smooth()
        me.set_sharp_from_angle(angle=math.radians(sharp_deg))
    except Exception:
        me.polygons.foreach_set("use_smooth", [False] * len(me.polygons))
    me.update()


def _new_piece(D, name, verts, faces, uvs, mats_idx, mats, col="ENVIRONMENT", sharp_deg=38.0):
    ob = P.new_mesh_object(name, col)
    P.build_mesh(ob, verts, faces, smooth=True, material_index=mats_idx, materials=mats)
    me = ob.data
    if len(me.polygons) != len(faces):
        raise RuntimeError(f"{name}: mesh validate dropped faces ({len(faces)} -> {len(me.polygons)}), UVs would misalign")
    lay = _uv_layer(me)
    fl = []
    for f, fu in zip(faces, uvs):
        fl.extend(fu)
    lay.data.foreach_set("uv", np.array(fl, float).ravel())
    _finish_mesh(ob, sharp_deg)
    D.objects[name] = ob
    return _track(D, ob)


def _next_name(D, prefix):
    k = D.__dict__.setdefault("look_counters", {}).get(prefix, 0) + 1
    while bpy.data.objects.get(f"{prefix}_{k:02d}") is not None:
        k += 1
    D.look_counters[prefix] = k
    return f"{prefix}_{k:02d}"


def wall_to_material(D, material="LK_BASALT", from_material="LK_ROUGH"):
    """Material slot change only (geometry untouched): every TERRAIN face that is not a top face (the walls, including the
    1 m grass LIP BAND at the top of each wall) that carries `from_material` gets `material`. build_cliff_skin(style='basalt')
    calls it (wall_material=), so the sliver of bare wall that can show above the basalt tops is basalt, not vertical grass.
    Run before assign_uvs. Returns the face count."""
    ob = _terrain_ob(D)
    me = ob.data
    W, ls, lt, lv, mi, pn = _mesh_arrays(ob)
    top = _top_mask(D, W, ls, lt, lv, pn)
    names = [_mat_name(m_) for m_ in me.materials]
    if from_material not in names:
        return 0
    mat = look_material(material, D)
    if mat.name not in [m_.name for m_ in me.materials if m_]:
        me.materials.append(mat)
    k_to = [m_.name if m_ else "" for m_ in me.materials].index(mat.name)
    sel = (~top) & (mi == names.index(from_material))
    mi[sel] = k_to
    me.polygons.foreach_set("material_index", mi.astype(np.int32))
    me.update()
    return int(sel.sum())


def build_cliff_skin(D, style="strata", loops=None, seed=0, step_m=None, lip_m=None, bottom_z=-1.6, disp_m=(0.2, 1.2),
                     follow_undercut=None, bands=4, wet_z=1.0, piece_m=140.0, material=None, material_low=None,
                     basalt_width_m=(1.0, 2.0), basalt_rows=2, name="ROCK_SKIN", replace=False, keep_out_water=True, max_out_m=2.6,
                     jag_m=2.0, wall_material="LK_BASALT", basalt_backing=True, basalt_segments=(1, 1, 1, 2, 2),
                     basalt_stagger=(0.30, 0.12, 0.55), strata_tuning=None):
    """Visual rock cladding ROCK_SKIN_nn over the TERRAIN walls (colliders untouched, ROCK_ prefix, no collider).

    style 'strata' (Needle / Split): a continuous jagged skin following every boundary loop: `bands` horizontal strata
        between the grass lip and bottom_z (below the waterline), each band a block-jointed face pushed out 0.2-1.2 m
        (disp_m) from the wall with its own overhang/undercut, crisp ledges between bands, chips; a capping ledge tucks
        back into the wall just below the grass lip (lip_m below PLAY_Z, default 0.15-0.45 m, so the TERRAIN grass lip
        band shows as a fringe). Faces whose centre is below wet_z get material_low (LK_CLIFF_DARK, wet), the rest
        material (LK_CLIFF). follow_undercut (default 0.5): 0 = plumb from the grass edge, 1 = hug the undercut wall.
        Repair round 1 (2026-10-04): fissure blocks are 1.6-15 m long (heavy-tailed, CV 0.56 instead of 0.29), 40 % of the (joint, band) cells
        are closed (a hairline, not a step) and the shared 'push' weight is 0.10 (was 0.40), so the open joints are staggered and no vertical
        line runs through every band; band thicknesses differ (a random partition, not 3 equal slabs). STRATA_TUNING holds the numbers,
        strata_gate (STRATA_JOINTS_STAGGERED) measures them, strata_tuning= lets a caller / the gate pass STRATA_TUNING_PREV.
    style 'basalt' (Crater): rows of hexagonal columns (basalt_width_m across) standing from bottom_z (in the lava) up to
        tops 0.07-0.09 m below PLAY_Z (basalt_stagger = (share, lo, hi): 30 % of the columns stop 12-55 cm lower, a ragged crown; the backing
        sheet fills the gap), basalt_segments = the choices for the number of fractured segments of a column ((1, 1, 1, 2, 2): mostly ONE prism
        over the whole height, was 1-3 = stacked planks), the outer row 0.05-1.2 m proud of the edge (disp_m), an
        inner row behind it for stepped tops; LK_BASALT. follow_undercut (default 0) leans the columns inward with the
        undercut wall (1.0 = a tapered bowl like crater.jpg's pillar). The visible taper of a pillar is the TERRAIN wall's
        (P.build_terrain undercut_m, a hole script parameter): the skin and its backing sheet can only follow it.
    loops: None = every TERRAIN top boundary loop, or a list of loop indices (terrain_top_loops order). Nothing is ever
    above PLAY_Z - 0.1. Pieces of <= piece_m metres of shore each (frustum culling). replace=True first deletes every
    existing `name`_nn object (re-running a build); leave it False when you call it once per loop group (crater rim +
    pillar). Seeded by the hole number + `seed`: rebuilding gives identical rock. Returns the objects."""
    t0 = time.time()
    if style != "basalt" and strata_tuning is None:                 # what strata_gate needs to re-run the loop for its negative control
        D.__dict__["strata_kwargs"] = dict(style=style, loops=loops, seed=seed, step_m=step_m, lip_m=lip_m, bottom_z=bottom_z, disp_m=disp_m,
                                           follow_undercut=follow_undercut, bands=bands, wet_z=wet_z, piece_m=piece_m, material=material,
                                           material_low=material_low, keep_out_water=keep_out_water, max_out_m=max_out_m, jag_m=jag_m)
        if replace:
            D.__dict__["strata_stats"] = []
    if replace:
        for o in [o for o in bpy.data.objects if o.name.startswith(name + "_")]:
            D.objects.pop(o.name, None)
            P.remove_object(o.name)
        D.__dict__.setdefault("look_counters", {})[name] = 0
    rnd = random.Random(D.number * 1009 + seed)
    pz = D.play_z
    mat_hi = look_material(material or ("LK_BASALT" if style == "basalt" else "LK_CLIFF"), D)
    mat_lo = look_material(material_low or ("LK_BASALT" if style == "basalt" else "LK_CLIFF_DARK"), D)
    tile = LOOK[mat_hi.name][2]
    bvh = _terrain_bvh(D)
    all_loops = terrain_top_loops(D)
    sel = range(len(all_loops)) if loops is None else loops
    ells = _water_ellipses(D) if keep_out_water else []
    if style == "basalt" and wall_material:
        n_lip = wall_to_material(D, wall_material)
        _log(f"build_cliff_skin(basalt): {n_lip} TERRAIN wall faces (grass lip band) -> {wall_material}")
    objs = []
    for li in sel:
        Pl = all_loops[li][0]
        if style == "basalt":
            if basalt_backing:
                objs += _basalt_backing(D, Pl, li, bvh, rnd, bottom_z, mat_hi, tile, piece_m, name)
            objs += _basalt_loop(D, Pl, li, bvh, rnd, bottom_z, disp_m, follow_undercut or 0.0, lip_m or (0.068, 0.085),
                                 basalt_width_m, basalt_rows, mat_hi, tile, piece_m, name, ells=ells, seg_choices=tuple(basalt_segments),
                                 stagger=basalt_stagger)
        else:
            objs += _strata_loop(D, Pl, li, bvh, rnd, step_m or 1.5, lip_m or (0.15, 0.45), bottom_z, disp_m,
                                 0.5 if follow_undercut is None else follow_undercut, bands, wet_z, mat_hi, mat_lo, tile,
                                 piece_m, name, seed, ells=ells, jag_m=jag_m, max_out_m=max_out_m, tuning=strata_tuning)
    tris = sum(len(o.data.polygons) for o in objs) * 2
    _log(f"build_cliff_skin({style}): {len(objs)} pieces on {len(list(sel))} loops, ~{tris} tris, {time.time() - t0:.1f}s")
    return objs


def _columns_at(Pl, step, extra_s):
    """Regular loop columns (_loop_columns) plus extra columns at arc positions extra_s (fissure edges): positions on the
    polyline, normals interpolated from the regular columns. Returns Pc, s, L, N sorted by s (duplicates < 2 cm dropped)."""
    Pc, s, L, N, _ = _loop_columns(Pl, step)
    ex = np.asarray(extra_s, float) % L if len(extra_s) else np.zeros(0)
    if len(ex):
        seg = np.roll(Pl, -1, axis=0) - Pl
        Ls = np.hypot(seg[:, 0], seg[:, 1])
        Sv = np.concatenate([[0.0], np.cumsum(Ls)])
        k = np.clip(np.searchsorted(Sv, ex, side="right") - 1, 0, len(Pl) - 1)
        t = (ex - Sv[k]) / np.maximum(Ls[k], 1e-9)
        Pe = Pl[k] + seg[k] * t[:, None]
        sx = np.concatenate([s - L, s, s + L])
        Nx = np.concatenate([N, N, N])
        Ne = np.stack([np.interp(ex, sx, Nx[:, 0]), np.interp(ex, sx, Nx[:, 1])], 1)
        Ne /= np.maximum(np.hypot(Ne[:, 0], Ne[:, 1]), 1e-9)[:, None]
        Pc, s, N = np.vstack([Pc, Pe]), np.concatenate([s, ex]), np.vstack([N, Ne])
        o = np.argsort(s, kind="stable")
        Pc, s, N = Pc[o], s[o], N[o]
        keep = np.ones(len(s), bool)
        keep[1:] = np.diff(s) > 0.02
        Pc, s, N = Pc[keep], s[keep], N[keep]
    return Pc, s, L, N


def _quad_uv(p4, tg, u_off, z_off, r_off, tile):
    """Near-isometric UVs (tile units) of one skin quad: the quad is unfolded in its own plane (u along the loop travel on walls and
    steps, v up the face; ledges and caps u along the travel, v toward the wall), anchored at corner 0 with u_off (plan length along
    the row, so neighbours on a row line up) and z_off (or -r_off on horizontal faces). Faces of any slant or twist keep ~1:1
    texel density (postcard_look_verify UV_DENSITY: <= 15 % of the area above 4:1)."""
    d1, d2 = p4[2] - p4[0], p4[3] - p4[1]
    n = np.cross(d1, d2)
    ln = float(np.linalg.norm(n))
    if ln < 1e-12:
        return [(u_off / tile, z_off / tile)] * 4
    n = n / ln
    nh = math.hypot(n[0], n[1])
    if nh > 0.3:
        u = np.array([-n[1], n[0], 0.0]) / nh                  # cross(up, n): the loop travel direction on a wall
        v = np.cross(n, u)
    else:
        u = np.array([tg[0], tg[1], 0.0])
        u = u - n * float(u @ n)
        u = u / max(float(np.linalg.norm(u)), 1e-9)
        v = np.cross(n, u)
    v0 = z_off if abs(v[2]) > 0.5 else -r_off
    d = p4 - p4[0]
    return [((u_off + float(dk @ u)) / tile, (v0 + float(dk @ v)) / tile) for dk in d]


def _clamp_ellipse(Pc, N, tg, R, jit_t, ells, depth_max=0.10, iters=14):
    """Pull the outward offsets R (columns x rows) back, per entry, until the final vertex (Pc + N R + tg jit_t) is at most
    depth_max inside any scoring water ellipse (R never goes below min(R, 0): the loop edge itself)."""
    def depth(Rm):
        X = Pc[:, None, 0] + N[:, None, 0] * Rm + tg[:, None, 0] * jit_t
        Y = Pc[:, None, 1] + N[:, None, 1] * Rm + tg[:, None, 1] * jit_t
        return _ell_depth(ells, X, Y)
    bad = depth(R) > depth_max
    if not bad.any():
        return R, 0
    lo = np.minimum(R, 0.0)
    hi = R.copy()
    for _ in range(iters):
        mid = 0.5 * (lo + hi)
        over = depth(mid) > depth_max
        hi = np.where(bad & over, mid, hi)
        lo = np.where(bad & ~over, mid, lo)
    return np.where(bad, lo, R), int(bad.sum())


# Strata joint layout (review 2026-10-04: "cliff strata repeat in regular columns"). STRATA_TUNING is the live one, STRATA_TUNING_PREV the layout before
# (kept ONLY as the negative control of STRATA_JOINTS_STAGGERED: strata_gate builds a loop with it and must see the regular grid).
STRATA_TUNING = dict(mixed_blocks=True, push_w=0.10, b_w=0.56, closure=0.40, partition=True)
STRATA_TUNING_PREV = dict(mixed_blocks=False, push_w=0.40, b_w=0.34, closure=0.0, partition=False)


def _strata_loop(D, Pl, li, bvh, rnd, step, lip_m, bottom_z, disp_m, follow, nb, wet_z, mat_hi, mat_lo, tile, piece_m,
                 name, seed, ells=None, jag_m=0.0, max_out_m=2.6, tuning=None):
    """Strata cladding of one loop. The shore is cut into FISSURE BLOCKS (3-9 m along the loop, a crisp vertical joint
    between two doubled columns 0.18 m apart); every block x band cell has its own outward offset (inside disp_m), its own
    ledge height step and its own overhang, all bands of a block share a 'push' (so joints run through the whole height),
    and a 22 m buttress noise swells whole stretches. Rows per band: top / mid / bottom, ledges 0.14 m tall between bands."""
    pz = D.play_z
    sd_seed = D.number * 31 + li * 7 + seed * 13
    seg = np.roll(Pl, -1, axis=0) - Pl
    L0 = float(np.hypot(seg[:, 0], seg[:, 1]).sum())
    T_ = tuning or STRATA_TUNING
    fe, fv = _blocks_mixed(L0, rnd) if T_["mixed_blocks"] else _blocks(L0, 3.0, 9.0, rnd)   # fissure blocks shared by every band (heavy-tailed lengths: no regular grid)
    nblk = len(fv)
    je, jv = _blocks(L0, T_.get('outline_block_min',2.0), T_.get('outline_block_max',4.8), rnd) if jag_m > 0 else (np.array([0.0, L0]), np.array([0.0]))   # headland / bay blocks
    njb = len(jv)
    extra = np.concatenate([fe[1:-1] - 0.09, fe[1:-1] + 0.09]) if nblk > 1 else np.zeros(0)
    if jag_m > 0 and njb > 1:                                    # a crisp step (doubled columns) at every headland edge too
        extra = np.concatenate([extra, je[1:-1] - 0.09, je[1:-1] + 0.09])
    Pc, s, L, N = _columns_at(Pl, step, extra)
    m = len(Pc)
    tg = np.stack([-N[:, 1], N[:, 0]], 1)                         # travel direction (land on the left)
    kb = np.clip(np.searchsorted(fe, s, side="right") - 1, 0, nblk - 1)   # block of each column
    cell = rnd.random                                             # per (block, band) random values
    B = np.array([[cell() for _ in range(nb)] for _ in range(nblk)])
    STEP = np.array([[cell() for _ in range(nb + 1)] for _ in range(nblk)])
    OVER = np.array([[cell() for _ in range(nb)] for _ in range(nblk)])
    for k in range(nblk):                                         # ~35 % of the ledges vanish: two bands make one taller face
        for j in range(1, nb):
            if cell() < 0.35:
                B[k, j] = B[k, j - 1]
                OVER[k, j] = 0.0
    if T_["closure"] > 0:
        for k in range(1, nblk):                                  # ~40 % of the (joint, band) cells are CLOSED: this block has the offset of its neighbour in that band, so the
            for j in range(nb):                                   # fissure is a hairline there, not a step; the open joints are staggered (no vertical line through every band)
                if cell() < T_["closure"]:
                    B[k, j] = B[k - 1, j]
                    OVER[k, j] = OVER[k - 1, j]
    LIPB = np.array([cell() for _ in range(nblk)])
    kj = np.clip(np.searchsorted(je, s, side="right") - 1, 0, njb - 1)    # headland block of each column
    JBAND = np.array([[0.7 + 0.3 * cell() for _ in range(nb)] for _ in range(njb)])
    lip = lip_m[0] + (lip_m[1] - lip_m[0]) * (0.55 * loop_noise(s, L, 9.0, sd_seed + 1) + 0.45 * LIPB[kb])
    z_top = pz - lip
    z_hi, z_lo = pz - float(np.mean(lip_m)), 0.45
    bh = (z_hi - z_lo) / max(nb - 1, 1)
    wf = np.array([rnd.uniform(0.55, 1.75) if T_["partition"] else 1.0 for _ in range(max(nb - 1, 1))])      # band thicknesses differ (not 3 equal slabs)
    cf = np.cumsum(wf) / wf.sum()
    th_j = (z_hi - z_lo) * wf / wf.sum()
    bnd = [z_top]
    for j in range(1, nb):
        nominal = z_hi - (z_hi - z_lo) * cf[j - 1]
        amp_j = (0.34 if T_["partition"] else 0.42) * (float(np.mean(th_j[max(j - 2, 0):j + 1])) if T_["partition"] else bh)
        bnd.append(nominal + amp_j * (STEP[kb, j] - 0.5) * 2 + 0.08 * bh * (loop_noise(s, L, 7.0, sd_seed + 10 + j) - 0.5) * 2)
    bnd.append(np.full(m, bottom_z))
    for j in range(1, nb + 1):                                    # keep every band >= 0.45 m thick
        bnd[j] = np.minimum(bnd[j], bnd[j - 1] - 0.45)
    lo, hi = disp_m
    span = hi - lo
    # headland strength 0..1 per block: ~45 % bays (almost flush), the rest headlands 0.65..1 (a bimodal coast, not a gentle wave)
    amp = np.where(jv > 0.45, 0.65 + 0.35 * (jv - 0.45) / 0.55, 0.12 * jv / 0.45)
    head = amp[kj] if jag_m > 0 else np.zeros(m)
    push = fv[kb]                                                 # shared by all bands of a block
    buttress = loop_noise(s, L, 22.0, sd_seed + 5)
    rows_z, rows_d = [], []
    for j in range(nb):
        u = (0.16 + 0.34 * j / max(nb - 1, 1) + T_["push_w"] * (push - 0.5) + T_["b_w"] * (B[kb, j] - 0.5) + 0.30 * (buttress - 0.5))
        d = lo + span * np.clip(u, 0.0, 1.0) + 0.10 * (loop_noise(s, L, 2.5, sd_seed + 20 + j) - 0.5)
        d += T_.get('outline_relief_m',0.0) * (loop_noise(s,L,2.1,sd_seed+720+j)-.5)*2
        d = np.clip(d, lo, hi)
        d = d + jag_m * head * (0.8 + 0.2 * j / max(nb - 1, 1)) * JBAND[kj, j]   # headlands: the lower the band the farther out (up to jag_m)
        over = T_.get('overhang_m',.24) * OVER[kb, j] * (0.4 + 0.6 * (j < nb - 1))
        under = T_.get('undercut_m',.16) * loop_noise(s, L, 4.0, sd_seed + 40 + j)
        top = bnd[j] if j == 0 else bnd[j] - 0.07
        bot = bnd[j + 1] if j == nb - 1 else bnd[j + 1] + 0.07
        mid = 0.5 * (top + bot) + 0.12 * (bot - top) * (loop_noise(s, L, 3.0, sd_seed + 60 + j) - 0.5)
        chip = 0.14 * (loop_noise(s, L, 1.4, sd_seed + 80 + j) - 0.5)
        rows_z += [top, mid, bot]
        hj = hi + jag_m
        rows_d += [np.minimum(np.clip(d + over, lo, hj + 0.25), max_out_m), np.minimum(np.clip(d + chip, lo, hj), max_out_m),
                   np.minimum(np.clip(d - under, lo * 0.8, hj), max_out_m)]
    Z = np.stack(rows_z, 1)
    Dd = np.stack(rows_d, 1)
    if nblk > 1 and not T_.get('goal15'):                          # legacy/control statistics; goal15 is measured after all clamps below
        steps_ = []
        for fpos in fe[1:-1]:
            ia = int(np.argmin(np.abs(s - (fpos - 0.09))))
            ib = int(np.argmin(np.abs(s - (fpos + 0.09))))
            steps_.append([abs(float(Dd[ib, 3 * j + 1] - Dd[ia, 3 * j + 1])) for j in range(nb)])
        D.__dict__.setdefault("strata_stats", []).append(dict(loop=li, tuning=("live" if tuning is None or T_.get('goal15') else "control"), lengths=np.diff(fe).tolist(),
                                                              steps=steps_, bands=nb))
    Z = Z + 0.05 * (np.stack([loop_noise(s, L, 2.2, sd_seed + 200 + r) for r in range(Z.shape[1])], 1) - 0.5) * 2
    Z[:, 0] = z_top
    Z = np.minimum(Z, pz - 0.1)
    inset = _wall_inset(bvh, Pc, N, Z)
    R = -follow * inset + Dd
    if T_.get('goal15'):
        # Carve the existing visual columns, without adding samples or faces.
        # Fissure-pair columns <=.30m apart share one real cutback: the carve
        # must not manufacture a full-height step across every narrow seam.
        ds = np.r_[s[0] + L - s[-1], np.diff(s)]
        group = np.cumsum(ds > .30).astype(int)
        carve_rng = np.random.default_rng([7211, sd_seed])
        depth = .20 + .65 * (np.arange(group.max() + 1) % 2)
        depth += carve_rng.uniform(-.10, .10, len(depth))
        R = R - depth[group, None]
    R = _unfold_rows(Pc, N, R, tg)
    cap_in = _wall_inset(bvh, Pc, N, Z[:, :1])[:, 0] + 0.25        # the cap tucks 0.25 m into the wall at the lip height
    Rall = np.concatenate([-cap_in[:, None], R], 1)
    Zall = np.concatenate([Z[:, :1], Z], 1)
    jit_t = 0.10 * (np.stack([loop_noise(s, L, 1.3, sd_seed + 300 + r) for r in range(Zall.shape[1])], 1) - 0.5) * 2
    gap = np.diff(np.concatenate([s, [s[0] + L]]))
    jit_t *= np.clip(np.minimum(gap, np.roll(gap, 1)) / 1.0, 0.0, 1.0)[:, None]   # doubled fissure columns stay apart
    shear = 0.32 * (loop_noise(s, L, 7.0, sd_seed + 400) - 0.5) * 2         # joints lean: along-shore shift grows with height
    jit_t = jit_t + shear[:, None] * (Zall - 0.5 * (pz + bottom_z))
    nr = Zall.shape[1]
    if ells:                                                      # no skin surface deeper than 10 cm inside a scoring water ellipse
        Rall, n_clamped = _clamp_ellipse(Pc, N, tg, Rall, jit_t, ells)
        if n_clamped:
            _log(f"strata loop {li}: {n_clamped} skin vertices pulled out of water ellipses")
    X = Pc[:, None, 0] + N[:, None, 0] * Rall + tg[:, None, 0] * jit_t
    Y = Pc[:, None, 1] + N[:, None, 1] * Rall + tg[:, None, 1] * jit_t
    if ells:                                                      # corner leftovers (tangential shear): project out along the ellipse gradient
        for _ in range(3):
            dep = _ell_depth(ells, X, Y)
            for i_, r_ in np.argwhere(dep > 0.10).tolist():
                g = _ell_inward(ells, X[i_, r_], Y[i_, r_])
                X[i_, r_] -= g[0] * (dep[i_, r_] - 0.10 + 0.01)
                Y[i_, r_] -= g[1] * (dep[i_, r_] - 0.10 + 0.01)
    if T_.get('goal15') and nblk > 1:
        # Read the FINAL real offsets, including unfold/water clamps. The
        # original strata gate consumes these geometric measurements; never
        # retain pre-carve metadata that would hide a new full-height joint.
        final_xy = np.stack([X, Y], axis=2)
        actual_r = np.sum((final_xy - Pc[:, None, :]) * N[:, None, :], axis=2)
        steps_ = []
        for fpos in fe[1:-1]:
            ia = int(np.argmin(np.abs(s - (fpos - .09))))
            ib = int(np.argmin(np.abs(s - (fpos + .09))))
            steps_.append([abs(float(actual_r[ib, 3*j + 2] - actual_r[ia, 3*j + 2])) for j in range(nb)])
        D.__dict__.setdefault('strata_stats', []).append(dict(loop=li, tuning='live',
            lengths=np.diff(fe).tolist(), steps=steps_, bands=nb, provenance='final actual cladding vertices'))
    if T_.get('goal15'):
        final_xy = np.stack([X, Y], axis=2)
        actual_r = np.sum((final_xy - Pc[:, None, :]) * N[:, None, :], axis=2)
        outer=np.argmax(actual_r,axis=1)
        outline=np.stack([X[np.arange(m),outer],Y[np.arange(m),outer]],axis=1)
        D.__dict__.setdefault('postcard_outline',[]).append(dict(loop=li,xy=outline.tolist(),sample_arc=s.tolist()))
    # u = plan length along each row (not the loop arc): a headland step does not smear the rock texture
    Uc = np.zeros((m + 1, nr))
    dXc = np.diff(np.concatenate([X, X[:1]], 0), axis=0)
    dYc = np.diff(np.concatenate([Y, Y[:1]], 0), axis=0)
    Uc[1:] = np.cumsum(np.hypot(dXc, dYc), axis=0)
    # pieces along the loop (shared boundary columns: no gaps)
    n_piece = max(1, int(round(L / piece_m)))
    cuts = [int(round(k * m / n_piece)) for k in range(n_piece)] + [m]
    objs = []
    for k in range(n_piece):
        cols = [c % m for c in range(cuts[k], cuts[k + 1] + 1)]
        ucol = [(m if (ci == len(cols) - 1 and c == 0 and len(cols) > 1) else c) for ci, c in enumerate(cols)]
        verts, faces, uvs, mi = [], [], [], []
        vid = {}
        for ci, c in enumerate(cols):
            for r in range(nr):
                vid[(ci, r)] = len(verts)
                verts.append((float(X[c, r]), float(Y[c, r]), float(Zall[c, r])))
        for ci in range(len(cols) - 1):
            c0, c1 = cols[ci], cols[ci + 1]
            for r in range(nr - 1):
                faces.append((vid[(ci, r)], vid[(ci, r + 1)], vid[(ci + 1, r + 1)], vid[(ci + 1, r)]))
                corner = [(ci, r, c0), (ci, r + 1, c0), (ci + 1, r + 1, c1), (ci + 1, r, c1)]
                p4 = np.array([[X[c, rr], Y[c, rr], Zall[c, rr]] for _, rr, c in corner])
                uvs.append(_quad_uv(p4, tg[c0], Uc[ucol[ci], r], Zall[c0, r], Rall[c0, r], tile))
                zc = 0.25 * (Zall[c0, r] + Zall[c0, r + 1] + Zall[c1, r] + Zall[c1, r + 1])
                mi.append(1 if zc < wet_z else 0)
        u0 = math.floor(min(u for fu in uvs for u, _ in fu))
        v0 = math.floor(min(v for fu in uvs for _, v in fu))
        uvs = [[(u - u0, v - v0) for u, v in fu] for fu in uvs]
        objs.append(_new_piece(D, _next_name(D, name), verts, faces, uvs, mi, [mat_hi, mat_lo]))
    return objs


def _basalt_backing(D, Pl, li, bvh, rnd, bottom_z, mat, tile, piece_m, name, offset_m=0.05, top_gap=0.0505):
    """A continuous BACKING sheet behind the basalt prisms: a vertical (undercut-following) quad strip 5 cm in front of the TERRAIN
    wall from the wall's own foot (z -6: nothing under the lava stays bare) up to PLAY_Z - top_gap (no cap, no up-facing face). The prisms cover most of it; where they leave a groove or
    a gap the eye meets this dark LK_BASALT sheet instead of the smooth TERRAIN wall (and its grass lip band). 0.75 m columns, 9
    rows, 3 cm of noise so it is not a ruler-flat plane. Returns the piece objects."""
    pz = D.play_z
    Pc, s, L, N, _ = _loop_columns(Pl, 0.75)
    m = len(Pc)
    tg = np.stack([-N[:, 1], N[:, 0]], 1)
    # rows on the wall's own ring heights (the TERRAIN wall is piecewise linear between its rings, so the sheet follows it exactly)
    Wt = _mesh_arrays(_terrain_ob(D))[0]
    z_wall = min(float(Wt[:, 2].min()), bottom_z)                # the sheet runs down to the wall's own foot (under the opaque lava)
    rings = sorted({round(float(z_), 2) for z_ in Wt[:, 2] if z_wall + 0.05 < z_ < pz - top_gap - 0.05})
    zs = np.array([z_wall] + rings + [pz - top_gap])
    Z = np.tile(zs, (m, 1))
    inset = _wall_inset(bvh, Pc, N, Z)
    inset = np.minimum(inset, np.minimum(np.roll(inset, 1, axis=0), np.roll(inset, -1, axis=0)))   # in front of the wall at the corners too
    sd_seed = D.number * 131 + li * 17
    nz = np.stack([loop_noise(s, L, 3.0, sd_seed + r) for r in range(len(zs))], 1)
    R = -inset + offset_m + 0.03 * (nz - 0.5) * 2
    R = np.minimum(R, 0.09)
    R = _unfold_rows(Pc, N, R, tg)
    X = Pc[:, None, 0] + N[:, None, 0] * R
    Y = Pc[:, None, 1] + N[:, None, 1] * R
    Uc = np.zeros((m + 1, len(zs)))
    Uc[1:] = np.cumsum(np.hypot(np.diff(np.concatenate([X, X[:1]], 0), axis=0), np.diff(np.concatenate([Y, Y[:1]], 0), axis=0)), axis=0)
    n_piece = max(1, int(round(L / piece_m)))
    cuts = [int(round(k * m / n_piece)) for k in range(n_piece)] + [m]
    objs = []
    for k in range(n_piece):
        cols = [c % m for c in range(cuts[k], cuts[k + 1] + 1)]
        ucol = [(m if (ci == len(cols) - 1 and c == 0 and len(cols) > 1) else c) for ci, c in enumerate(cols)]
        verts, faces, uvs = [], [], []
        for ci, c in enumerate(cols):
            for r in range(len(zs)):
                verts.append((float(X[c, r]), float(Y[c, r]), float(Z[c, r])))
        nr = len(zs)
        for ci in range(len(cols) - 1):
            for r in range(nr - 1):
                # rows run UP (bottom_z first), so the winding is (c0,r) (c1,r) (c1,r+1) (c0,r+1): normals OUTWARD (single-sided in Unity)
                faces.append((ci * nr + r, (ci + 1) * nr + r, (ci + 1) * nr + r + 1, ci * nr + r + 1))
                c0, c1 = cols[ci], cols[ci + 1]
                p4 = np.array([[X[c0, r], Y[c0, r], zs[r]], [X[c1, r], Y[c1, r], zs[r]],
                               [X[c1, r + 1], Y[c1, r + 1], zs[r + 1]], [X[c0, r + 1], Y[c0, r + 1], zs[r + 1]]])
                uvs.append(_quad_uv(p4, tg[c0], Uc[ucol[ci], r], zs[r], R[c0, r], tile))
        u0 = math.floor(min(u for fu in uvs for u, _ in fu))
        v0 = math.floor(min(v for fu in uvs for _, v in fu))
        uvs = [[(u - u0, v - v0) for u, v in fu] for fu in uvs]
        objs.append(_new_piece(D, _next_name(D, name), verts, faces, uvs, [0] * len(faces), [mat], sharp_deg=60.0))
    return objs


def _hex_prism(cx, cy, r, z0, z1, rot, lean=(0.0, 0.0), cap=True, tilt=(0.0, 0.0)):
    """Hexagonal prism side quads (+ top cap) from z0 to z1; `lean` moves the BOTTOM ring by (dx, dy); `tilt` = (dz/dx, dz/dy)
    slope of the top cap (a chipped, not machine-flat, top)."""
    ang = [rot + k * math.pi / 3 for k in range(6)]
    bot = [(cx + lean[0] + r * math.cos(a), cy + lean[1] + r * math.sin(a), z0) for a in ang]
    top = [(cx + r * math.cos(a), cy + r * math.sin(a), z1 + tilt[0] * r * math.cos(a) + tilt[1] * r * math.sin(a)) for a in ang]
    return bot, top


BASALT_MIN_RELIEF = 0.14          # m: the outer face of a skin prism stands at least this far in front of the TERRAIN wall (and 9 cm in front of the 5 cm backing sheet)


def _basalt_loop(D, Pl, li, bvh, rnd, bottom_z, disp_m, follow, lip_m, width_m, rows, mat, tile, piece_m, name, ells=None,
                 keep_out_m=0.10, seg_choices=(1, 1, 1, 2, 2), stagger=(0.30, 0.12, 0.55)):
    pz = D.play_z
    ells = ells or []
    Pc, s, L, N, isv = _loop_columns(Pl, 0.5)
    m = len(Pc)
    pieces, cur, cur_len, objs = [], [], 0.0, []

    def flush():
        if cur:
            verts, faces, uvs = [], [], []
            for (bot, top, u0) in cur:
                base = len(verts)
                verts += bot + top
                side = math.dist(top[0][:2], top[1][:2])
                for k in range(6):
                    k2 = (k + 1) % 6
                    faces.append((base + k, base + k2, base + 6 + k2, base + 6 + k))
                    uvs.append([(u0 + k * side / tile, bot[k][2] / tile), (u0 + (k + 1) * side / tile, bot[k2][2] / tile),
                                (u0 + (k + 1) * side / tile, top[k2][2] / tile), (u0 + k * side / tile, top[k][2] / tile)])
                faces.append(tuple(base + 6 + k for k in range(6)))
                uvs.append([(top[k][0] / tile, top[k][1] / tile) for k in range(6)])
            ush = math.floor(min(u for fu in uvs for u, _ in fu))
            vsh = math.floor(min(v for fu in uvs for _, v in fu))
            uvs = [[(u - ush, v - vsh) for u, v in fu] for fu in uvs]
            objs.append(_new_piece(D, _next_name(D, name), verts, faces, uvs, [0] * len(faces), [mat], sharp_deg=25.0))
    for row in range(rows):
        acc = rnd.uniform(0, width_m[1]) if row else 0.0
        while acc < L:
            w = rnd.uniform(*width_m)
            sc = acc + w * 0.5
            acc += w * rnd.uniform(0.86, 0.98)
            if sc >= L:
                break
            k = int(np.searchsorted(s, sc, side="right") - 1)
            k2 = (k + 1) % m
            t = (sc - s[k]) / max((s[k2] if k2 else L) - s[k], 1e-6)
            T = Pc[k] + (Pc[k2] - Pc[k]) * t
            n = N[k] + (N[k2] - N[k]) * t
            n /= max(math.hypot(*n), 1e-9)
            r = w * 0.5 * 1.10
            if row == 0:
                out = max(rnd.uniform(*disp_m), BASALT_MIN_RELIEF)   # outer face this far proud of the grass edge (never flush with the backing sheet: see BASALT_MIN_RELIEF)
                c = T + n * (out - r)
            else:
                c = T - n * (r * rnd.uniform(0.15, 0.55))
            if ells:                                            # keep-out: no cap deeper than keep_out_m inside a scoring water ellipse
                r_cap = 1.05 * r + 0.07
                for _ in range(80):
                    ang_ = np.linspace(0, 2 * math.pi, 16, endpoint=False)
                    rx, ry = c[0] + r_cap * np.cos(ang_), c[1] + r_cap * np.sin(ang_)
                    dep = _ell_depth(ells, rx, ry)
                    kmax = int(np.argmax(dep))
                    if float(dep[kmax]) <= keep_out_m:
                        break
                    g = _ell_inward(ells, rx[kmax], ry[kmax])      # move away from the ellipse, not along the (corner-averaged) normal
                    c = c - g * min(0.25, float(dep[kmax]) - keep_out_m + 0.03)
            # every top sits 5.5-9.5 cm under the play height: the original TERRAIN wall (and its grass lip band) is covered down
            # to a sliver of at most 10 cm, and no up-facing face is above PLAY_Z - 0.05
            lip = rnd.uniform(*lip_m)
            ztop = pz - lip
            if stagger and rnd.random() < stagger[0]:          # a ragged crown: ~30 % of the columns stop 12-55 cm lower (the backing sheet fills the gap)
                ztop -= rnd.uniform(stagger[1], stagger[2])
            inset_bot = _wall_inset(bvh, T[None], n[None], np.array([[max(bottom_z, -1.0)]]))[0, 0] if follow > 0 else 0.0
            if follow > 0:
                # repair round 2 (review: the pillar wall is a flat sheet, the prisms never read): the straight lean from the lip to the wall at z -1 put the prism's outer face
                # level with the wall + the 5 cm backing sheet wherever the (curved) wall bulged, so the sheet covered the hex facets. The lean is now limited so that the
                # outer face stands >= BASALT_MIN_RELIEF in front of the wall at EVERY height (8 samples): relief that catches the light, the prisms stay inside the outline
                zq = np.linspace(max(bottom_z, -1.0), ztop, 8)[:-1]
                fq = (zq - bottom_z) / max(ztop - bottom_z, 1e-6)
                Wq = _wall_inset(bvh, T[None], n[None], zq[None, :])[0]
                face_out = float((c - T) @ n) + 0.87 * r             # how far in front of the lip the outer face stands (> 0 = outwards)
                lim = min(((face_out + Wq[k] - BASALT_MIN_RELIEF) / max(1.0 - fq[k], 1e-3) for k in range(len(zq))), default=inset_bot)
                inset_bot = max(0.0, min(inset_bot, lim))
            lean_v = -n * follow * inset_bot
            nseg = rnd.choice(seg_choices)                      # mostly ONE prism over the whole height: a column, not a stack of planks
            cuts = sorted(rnd.uniform(bottom_z + 0.8, ztop - 0.6) for _ in range(nseg - 1))
            zs = [bottom_z] + cuts + [ztop]
            rot = rnd.uniform(0, math.pi / 3)
            for g in range(nseg):
                f0 = (zs[g] - bottom_z) / max(ztop - bottom_z, 1e-6)
                f1 = (zs[g + 1] - bottom_z) / max(ztop - bottom_z, 1e-6)
                rr = r * rnd.uniform(0.93, 1.05)
                jx, jy = rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05)
                c0 = (c[0] + lean_v[0] * (1 - f0) + jx, c[1] + lean_v[1] * (1 - f0) + jy)
                c1 = (c[0] + lean_v[0] * (1 - f1) + jx, c[1] + lean_v[1] * (1 - f1) + jy)
                tilt = (rnd.uniform(-0.009, 0.009), rnd.uniform(-0.009, 0.009)) if g == nseg - 1 else (0.0, 0.0)
                bot, top = _hex_prism(c1[0], c1[1], rr, zs[g] + (0.0 if g == 0 else -0.04), zs[g + 1],
                                      rot + rnd.uniform(-0.06, 0.06), lean=(c0[0] - c1[0], c0[1] - c1[1]), tilt=tilt)
                cur.append((bot, top, sc / tile + g * 0.37))
            cur_len += w
            if cur_len >= piece_m:
                flush()
                cur, cur_len = [], 0.0
    flush()
    return objs


# ============================================================================= sea: ocean grid, shelf, surf
def _graded(c0, c1, lo, hi, cell, growth=1.22, max_cell=400.0):
    xs = list(np.arange(c0, c1 + 1e-6, cell))
    if xs[-1] < c1:
        xs.append(c1)
    x, w = xs[-1], cell
    while x < hi - 1e-6:
        w = min(w * growth, max_cell)
        x = min(x + w, hi)
        xs.append(x)
    x, w, left = xs[0], cell, []
    while x > lo + 1e-6:
        w = min(w * growth, max_cell)
        x = max(x - w, lo)
        left.append(x)
    return np.array(left[::-1] + xs)


def _ocean_grid(D, name, mat, cell=16.0, half=2000.0, margin=130.0, cut_under_land_m=12.0):
    bx0, by0, bx1, by1 = P._shore_bbox_m(D)
    cx, cy = (bx0 + bx1) / 2, (by0 + by1) / 2
    xs = _graded(bx0 - margin, bx1 + margin, cx - half, cx + half, cell)
    ys = _graded(by0 - margin, by1 + margin, cy - half, cy + half, cell)
    GX, GY = np.meshgrid(xs, ys, indexing="ij")
    sd = np.full(GX.shape, 1e3)
    near = (GX > bx0 - 5) & (GX < bx1 + 5) & (GY > by0 - 5) & (GY < by1 + 5)
    if near.any():
        sd[near] = P.signed_dist_m(D, GX[near], GY[near])
    nx, ny = len(xs), len(ys)
    vid = np.arange(nx * ny).reshape(nx, ny)
    faces = []
    for i in range(nx - 1):
        for j in range(ny - 1):
            if max(sd[i, j], sd[i + 1, j], sd[i + 1, j + 1], sd[i, j + 1]) < -cut_under_land_m:
                continue
            faces.append((int(vid[i, j]), int(vid[i + 1, j]), int(vid[i + 1, j + 1]), int(vid[i, j + 1])))
    used = sorted({v for f in faces for v in f})
    remap = {v: k for k, v in enumerate(used)}
    verts = [(float(GX.ravel()[v]), float(GY.ravel()[v]), 0.0) for v in used]
    faces = [tuple(remap[v] for v in f) for f in faces]
    ob = bpy.data.objects.get(name)
    if ob is None:
        ob = P.new_mesh_object(name, "ENVIRONMENT")
    P.build_mesh(ob, verts, faces, smooth=True, materials=[mat])
    D.objects[name] = ob
    return _track(D, ob)


def _region_mesh(D, name, loops, z, mat, spacing, col="ENVIRONMENT"):
    res = P._triangulate_region(loops, spacing)
    if res is None:
        return None, None
    V, T = res
    ob = P.new_mesh_object(name, col)
    P.build_mesh(ob, [(float(x), float(y), z) for x, y in V], [tuple(int(i) for i in t) for t in T], smooth=True,
                 materials=[mat])
    D.objects[name] = ob
    return _track(D, ob), V


def _set_surf_attrs(ob, A, R):
    me = ob.data
    for nm in [a.name for a in me.color_attributes]:
        me.color_attributes.remove(me.color_attributes[nm])
    att = me.color_attributes.new(name="Col", type='FLOAT_COLOR', domain='POINT')
    col = np.stack([R, np.zeros_like(R), np.zeros_like(R), A], 1)
    att.data.foreach_set("color", col.astype(np.float32).ravel())
    try:
        me.color_attributes.active_color_name = "Col"
        me.color_attributes.render_color_index = 0
    except Exception:
        pass
    W = np.array([v.co[:] for v in me.vertices])
    lay = _uv_layer(me)
    lv = np.empty(len(me.loops), np.int64)
    me.loops.foreach_get("vertex_index", lv)
    U, V = W[lv, 0] / 8.0, W[lv, 1] / 8.0
    U -= np.round(U.mean())
    V -= np.round(V.mean())
    lay.data.foreach_set("uv", np.stack([U, V], 1).ravel())
    me.update()


def _pieces_by_cluster(loops, cluster_m):
    """Group contour loops (outer CCW + holes CW) into pieces, then pieces into clusters of about cluster_m metres."""
    outers = [L_ for L_ in loops if P.signed_area(L_) > 0]
    holes = [L_ for L_ in loops if P.signed_area(L_) <= 0]
    pieces = [[o] for o in outers]
    for h in holes:
        c = h.mean(0)
        for pc in pieces:
            if P.point_in_poly(c, pc[0]):
                pc.append(h)
                break
    groups = {}
    for pc in pieces:
        c = pc[0].mean(0)
        key = (int(math.floor(c[0] / cluster_m)), int(math.floor(c[1] / cluster_m)))
        groups.setdefault(key, []).append(pc)
    return pieces, [sum(g, []) for _, g in sorted(groups.items())]


def _shore_loop_data(D, step=0.5):
    """Per boundary loop: columns (Pc, s, L, N) every `step` m and the skin's outer face at the WATERLINE along each normal
    (ext_wl, metres out from the loop line; the TERRAIN wall when there is no skin; nan filled from the neighbours)."""
    sk, terr = _bvh_of(_skin_objs()), _bvh_of([_terrain_ob(D)])
    out = []
    for li, (Pl, _) in enumerate(terrain_top_loops(D)):
        Pc, s, Lp, N, _ = _loop_columns(Pl, step)
        ext = _outer_face(D, (sk, terr), Pc, N, 0.12)
        ext_top = ext.copy()
        for z in (0.5, 1.0, 2.0, 3.5, 5.5):                       # top-down footprint of the skin: the farthest face at any height
            ext_top = np.fmax(ext_top, _outer_face(D, (sk,), Pc, N, z))
        idx = np.arange(len(ext))
        for arr in (ext, ext_top):
            ok = np.isfinite(arr)
            arr[:] = np.interp(idx, idx[ok], arr[ok], period=len(arr)) if ok.any() else 0.5
        out.append(dict(li=li, Pc=Pc, s=s, L=Lp, N=N, ext=ext, ext_top=np.maximum(ext_top, ext)))
    return out


def _circ_smooth(a, win):
    """Circular moving average of a 1-D array over `win` samples (odd)."""
    k = max(1, int(win) | 1)
    pad = k // 2
    ap = np.concatenate([a[-pad:], a, a[:pad]]) if pad else a
    return np.convolve(ap, np.ones(k) / k, mode="valid")


def open_shore_point(D, near_m, free_m=40.0, step=1.0):
    """The shore column (x, y, nx, ny: metres, outward unit normal) nearest to `near_m` (x, y) whose outward ray has `free_m` of
    open water in front (no other land): where a waterfall foot / reef sits. Returns None when there is none."""
    best = None
    for L_ in _shore_loop_data(D, step):
        Pc, N = L_["Pc"], L_["N"]
        free = np.ones(len(Pc), bool)
        for dd in (0.25, 0.5, 0.75, 1.0):
            free &= P.signed_dist_m(D, Pc[:, 0] + N[:, 0] * free_m * dd, Pc[:, 1] + N[:, 1] * free_m * dd) > free_m * dd * 0.8
        dist = np.hypot(Pc[:, 0] - near_m[0], Pc[:, 1] - near_m[1]) + np.where(free, 0.0, 1e6)
        k = int(np.argmin(dist))
        if best is None or dist[k] < best[0]:
            best = (dist[k], Pc[k, 0], Pc[k, 1], N[k, 0], N[k, 1])
    return None if best is None or best[0] >= 1e6 else tuple(float(v) for v in best[1:])


def sea_pockets(D, pockets=None, auto=True):
    """The shelf pockets build_sea uses: the caller's [(x, y, r)] plus (auto) one per water hazard whose tag contains 'channel'
    (centre of the ellipse, radius = larger semi-axis + 6 m). Pass the same list to shelf_gate / v2_gates."""
    pk = [tuple(map(float, p_)) for p_ in (pockets or [])]
    if auto:
        for cx, cy, a, b, tag in _water_ellipses(D):
            if "channel" in str(tag).lower():
                pk.append((float(cx), float(cy), float(max(a, b) + 6.0)))
    return pk


def _surf_blob(D, name, cx, cy, ax, ay, rot, seed, strength=1.0, z=Z_SURF):
    """One ragged elliptical whitewater patch WATER_SURF_nn: semi-axes ax (along rot) x ay, outline radius 1 +- 0.2 (smooth noise),
    vertex colour A = strength * (1 - smoothstep(.35, 1, rho)) with rho the outline-relative radius, so A is exactly 0 on the whole
    rim (alpha fades out at BOTH ends and at the outer edge). R = phase noise. UV0 = world / 8 m. Returns the object."""
    n = 44
    th = np.linspace(0, 2 * math.pi, n, endpoint=False)
    rr = 1.0 + 0.2 * (2 * vnoise(np.cos(th) * 2.5 + 20 + seed, np.sin(th) * 2.5 + 20, 1.0, seed + 11) - 1.0)
    cr, sr = math.cos(rot), math.sin(rot)
    ex, ey = ax * rr * np.cos(th), ay * rr * np.sin(th)
    ring = np.stack([cx + ex * cr - ey * sr, cy + ex * sr + ey * cr], 1)
    ob, V = _region_mesh(D, name, [ring], z, look_material("LK_SURF", D), max(0.9, min(ax, ay) / 2.2))
    if ob is None:
        return None
    dx, dy = V[:, 0] - cx, V[:, 1] - cy
    u_, v_ = dx * cr + dy * sr, -dx * sr + dy * cr
    rho = np.hypot(u_ / ax, v_ / ay)
    ang = np.arctan2(v_ / ay, u_ / ax) % (2 * math.pi)
    rl = np.interp(ang, np.append(th, 2 * math.pi), np.append(rr, rr[0]))
    A = strength * (1.0 - _smoothstep(0.35, 1.0, rho / np.maximum(rl, 1e-6)))
    _set_surf_attrs(ob, np.clip(A, 0, 1), vnoise(V[:, 0], V[:, 1], 9.0, seed + 1))
    ob["surf_kind"] = "blob"                                  # this function's own output (build_sea re-runs delete it); surf_patch's stay
    return ob


def build_sea(D, ocean=True, ocean_cell_m=16.0, shelf=True, shelf_m=(0.5, 2.9), shelf_break=0.2, shelf_rag_m=0.6, pockets=None,
              pocket_m=4.2, auto_pockets=True, inner_m=4.0, surf=True, surf_cover=0.19, surf_min_cover=0.10, surf_len_m=(4.0, 7.0),
              surf_reach_m=(2.2, 3.6), surf_gap_m=12.5, rock_puffs=6, seed=0, cell=0.75, **legacy):
    """The sea around the islands (Needle, Split). Run it AFTER build_cliff_skin (it measures the skin). Deletes WATER_SHALLOW /
    WATER_FOAM / WATER_FOAM_CREST and its own earlier output, then builds

      WATER_OCEAN   one flat graded grid at z 0 (LK_WATER, see WHY THE OCEAN IS A GRID); ocean=False keeps the old plane.
      WATER_SHELF   z 0.06, LK_WATER_SHALLOW. A THIN EDGE, not a halo (split.jpg is deep blue right up to the foam; needle.jpg has
                    turquoise only in pockets at cliff feet and the waterfall): the shelf runs from inner_m under the walls out to
                    (the skin: mean of its waterline face and its top-down footprint, 5.5 m moving average) + v(s), where v(s) is the VISIBLE width shelf_m = (lo, hi) m
                    (low-frequency noise), v = 0 on shelf_break (0.2) of the shore (breaks), and a ragged +-shelf_rag_m outer edge.
                    pockets [(x, y, r)] (m): inside r of each the visible width grows to pocket_m (4.2, hard cap 6): the waterfall
                    foot (Needle: pass the same centre you give surf_patch) and, with auto_pockets, every water hazard whose tag
                    contains 'channel' (Split's channel_gap: radius = the larger ellipse semi-axis + 6 m).
                    The old shelf_m=(10, 24) halo is gone; a call with an old-style hi > 6 is clamped to 6 (and logged).
      WATER_SURF_nn z 0.12, LK_SURF: SPARSE, BROKEN whitewater, never a band: isolated ragged patches (one object each) hugging the
                    cliff foot, `surf_len_m` (4-7) long x `surf_reach_m` (2.2-3.6) out beyond the skin (the patch reaches 2 m back under the
                    overhang so the wet line is inside it), spaced so that the share of the shoreline covered is ~surf_cover (0.19 nominal
                    = 10-14 % measured, hard limit 0.25; if the measured share stays under surf_min_cover (0.10: the verifier needs >= 5 % of the
                    DESIGN shore, which is longer than the loops on Needle) it re-plans with closer patches), runs <= 10 m, gaps between runs >= surf_gap_m (12.5 nominal, limit 8), plus
                    up to rock_puffs reef-break puffs at the feet of rocks standing in the sea (3-12 m off the shore, >= 18 m from
                    any patch). Vertex colour 'Col': A = 1 - smoothstep(.35, 1, rho) (0 on the whole rim), R = phase; UV0 = world /
                    8 m. Pockets with r <= 12 reserve their centre for the caller's surf_patch (waterfall foot).
    Returns {name: obj}."""
    t0 = time.time()
    bpy.context.view_layer.update()                # objects made in this run have an identity matrix_world until the depsgraph is evaluated (rock puffs read it)
    if legacy:
        _log(f"build_sea: ignoring legacy options {sorted(legacy)} (surf is now sparse patches; the shelf is a thin edge)")
    if shelf_m[1] > 6.0:
        _log(f"build_sea: WARNING shelf_m={shelf_m} is an old-style halo width; clamped to ({min(shelf_m[0], 3.0)}, 6.0)")
        shelf_m = (min(shelf_m[0], 3.0), 6.0)
    rng_seed = D.number * 977 + seed
    rnd = random.Random(rng_seed)
    out = {}
    old = [nm for nm in ("WATER_SHALLOW", "WATER_FOAM", "WATER_FOAM_CREST", "WATER_SHELF") if bpy.data.objects.get(nm) is not None]
    old += [o.name for o in bpy.data.objects if o.name.startswith("WATER_SURF") and o.get("surf_kind") in ("shore", "shelf", "blob")]
    for nm in old:                                # the old bands, and this function's own output when it is re-run
        D.objects.pop(nm, None)
        P.remove_object(nm)
    if ocean:
        out["WATER_OCEAN"] = _ocean_grid(D, "WATER_OCEAN", look_material("LK_WATER", D), cell=ocean_cell_m)
    pk = sea_pockets(D, pockets, auto_pockets)
    loops = _shore_loop_data(D, 0.5)
    if not loops:
        return out
    # ---- per-sample visible width v(s) and outer edge (metres out from the loop line)
    allP, allE, allS = [], [], []
    for L_ in loops:
        Pc, s, Lp, N = L_["Pc"], L_["s"], L_["L"], L_["N"]
        ext_s = _circ_smooth(0.5 * (L_["ext"] + L_["ext_top"]), 11)          # 5.5 m moving average, halfway between the waterline face and the top-down footprint
        g = 0.6 * loop_noise(s, Lp, 22.0, rng_seed + 21) + 0.4 * loop_noise(s, Lp, 7.0, rng_seed + 22)
        thr = float(np.quantile(g, shelf_break))
        v = shelf_m[0] + (shelf_m[1] - shelf_m[0]) * np.clip((g - thr) / max(1.0 - thr, 1e-6), 0.0, 1.0) ** 0.8
        v = np.where(g < thr, 0.0, v)                                        # breaks: no shelf beyond the cliff
        for (px, py, pr) in pk:
            dd = np.hypot(Pc[:, 0] - px, Pc[:, 1] - py)
            bump = 1.0 - _smoothstep(0.55 * pr, pr, dd)
            v = np.maximum(v, pocket_m * bump)
        L_["edge"] = ext_s + v
        L_["v"] = v
        allP.append(Pc)
        allE.append(L_["edge"])
    kd = KDTree(sum(len(p_) for p_ in allP))
    base = 0
    for p_ in allP:
        for k_, q in enumerate(p_.tolist()):
            kd.insert((q[0], q[1], 0.0), base + k_)
        base += len(p_)
    kd.balance()
    edge_all = np.concatenate(allE)
    # ---- shelf
    if shelf:
        bx0, by0, bx1, by1 = P._shore_bbox_m(D)
        ext_r = float(np.max(edge_all)) + shelf_rag_m + 3.0
        grid = P._Grid((bx0 - ext_r, by0 - ext_r, bx1 + ext_r, by1 + ext_r), cell, pad=2)
        GX, GY = grid.X, grid.Y
        sd = P.signed_dist_m(D, GX, GY)
        F = np.full(GX.shape, -1.0)
        band = (sd > -inner_m - 1.0) & (sd < ext_r)
        ii = np.argwhere(band)
        edge_xy = np.zeros(len(ii))
        for n_, (i_, j_) in enumerate(ii.tolist()):
            near = kd.find_n((GX[i_, j_], GY[i_, j_], 0.0), 3)
            w_ = np.array([1.0 / (d_ + 0.4) for _, _, d_ in near])
            edge_xy[n_] = float(sum(w * edge_all[idx] for w, (_, idx, _) in zip(w_, near)) / w_.sum())
        gxv, gyv = GX[band], GY[band]
        rag = shelf_rag_m * (2 * vnoise(gxv, gyv, 3.2, rng_seed + 31) - 1) + 0.5 * shelf_rag_m * (2 * vnoise(gxv, gyv, 1.3, rng_seed + 32) - 1)
        F[band] = np.minimum(edge_xy + rag - sd[band], sd[band] + inner_m)
        shelf_loops = P._contours(grid, F > 0, F=F, simplify=0.05, min_area=1.0)
        ob, _ = _region_mesh(D, "WATER_SHELF", shelf_loops, Z_SHELF, look_material("LK_WATER_SHALLOW", D), 3.0)
        if ob:
            out["WATER_SHELF"] = ob
    # ---- whitewater: sparse isolated patches
    n_patch, n_rock = 0, 0
    if surf:
        cover_try = surf_cover
        for attempt in range(4):
            n_patch, n_rock = 0, 0
            made = []
            sites = []                                              # (cx, cy, ax, ay, rot, strength)
            reserve = [(px, py, pr + surf_gap_m) for (px, py, pr) in pk if pr <= 12.0]
            blobs = []                                              # (x, y, radius) of every placed patch: no two outlines overlap
            if rock_puffs:                                          # reef-break puffs at rocks standing in the sea (placed first)
                cands = []
                for o in bpy.data.objects:
                    if o.type != 'MESH' or not o.name.startswith(("ROCK_", "CLIFF_ROCK")) or o.name.startswith(("ROCK_SKIN", "ROCK_WALL")):
                        continue
                    Wv = np.array([tuple(o.matrix_world @ v.co) for v in o.data.vertices])
                    if len(Wv) == 0 or Wv[:, 2].min() > 0.8 or Wv[:, 2].max() < 1.5:
                        continue
                    c2 = Wv[:, :2].mean(0)
                    sd_c = float(P.signed_dist_m(D, np.array([c2[0]]), np.array([c2[1]]))[0])
                    if 3.0 <= sd_c <= 12.0:
                        cands.append((c2, float(np.hypot(*(Wv[:, :2] - c2).T).max()), sd_c))
                rnd.shuffle(cands)
                _log(f"build_sea: {len(cands)} rock-puff candidates (rocks standing in the sea 3-12 m off the shore)")
                for c2, rad, sd_c in cands:
                    if n_rock >= rock_puffs:
                        break
                    r_ = float(np.clip(rad + 1.4, 2.2, 3.9))
                    if any(math.hypot(c2[0] - x, c2[1] - y) < r_ + rr_ for x, y, rr_ in reserve) or \
                            any(math.hypot(c2[0] - x, c2[1] - y) < r_ + rr_ + 1.0 for x, y, rr_ in blobs):
                        continue
                    touches = sd_c - r_ <= 1.5                         # reaches the wet line: it is a run, keep the run gap round it
                    sites.append((float(c2[0]), float(c2[1]), r_, r_ * rnd.uniform(0.8, 1.0), rnd.uniform(0, math.pi), 0.9))
                    blobs.append((float(c2[0]), float(c2[1]), r_ * 1.2))
                    if touches:
                        reserve.append((float(c2[0]), float(c2[1]), r_ + surf_gap_m + 0.5 * surf_len_m[1] * 1.2))
                    n_rock += 1
            len_mean = 0.5 * (surf_len_m[0] + surf_len_m[1])
            gap_mean = len_mean * (1.0 / max(cover_try, 1e-3) - 1.0)
            gap_hi = max(surf_gap_m + 2.0, 2.0 * gap_mean - surf_gap_m)
            for L_ in loops:
                Pc, s, Lp, N, ext = L_["Pc"], L_["s"], L_["L"], L_["N"], L_["ext"]
                ext_s = _circ_smooth(ext, 5)
                first = rnd.uniform(0.0, min(gap_hi, Lp * 0.5))
                pos, plan = first, []
                while True:
                    ln = rnd.uniform(*surf_len_m)
                    if pos + ln + surf_gap_m > Lp + first:
                        break
                    plan.append((pos + ln / 2, ln))
                    pos += ln + rnd.uniform(surf_gap_m, gap_hi)
                for sc, ln in plan:
                    k = int(np.searchsorted(s, sc % Lp, side="right") - 1) % len(Pc)
                    T, n_ = Pc[k], N[k]
                    if any(math.hypot(T[0] - rx, T[1] - ry) < rr_ for rx, ry, rr_ in reserve):
                        continue
                    reach = rnd.uniform(*surf_reach_m)
                    sd_out, sd_in = max(float(ext_s[k]), 0.0) + reach, -2.0      # from under the overhang (the wet line is inside) to the open edge
                    c = T + n_ * (0.5 * (sd_out + sd_in))
                    rad_ = 1.2 * max(ln / 2, 0.5 * (sd_out - sd_in))
                    if any(math.hypot(c[0] - x, c[1] - y) < rad_ + rr_ + 1.0 for x, y, rr_ in blobs):
                        continue
                    sites.append((float(c[0]), float(c[1]), ln / 2, 0.5 * (sd_out - sd_in), math.atan2(-n_[0], n_[1]), 1.0))
                    blobs.append((float(c[0]), float(c[1]), rad_))
            for (cx, cy, ax, ay, rot, st) in sites:
                nm = _next_name(D, "WATER_SURF")
                ob = _surf_blob(D, nm, cx, cy, ax, ay, rot, rng_seed + 100 + n_patch, strength=st)
                if ob is None:
                    continue
                out[nm] = ob
                made.append(nm)
                n_patch += 1
            if surf and n_patch:                                        # drop a patch whose two touches of a notched shore are < 8 m apart
                for _ in range(4):
                    m_ = measure_surf(D)
                    if not m_.get("close"):
                        break
                    for (cx, cy, gap_) in m_["close"]:
                        near = []
                        for nm, o in out.items():
                            if nm.startswith("WATER_SURF") and o.get("surf_kind") == "blob":
                                Wv_ = _mesh_arrays(o)[0]
                                near.append((math.hypot(cx - float(Wv_[:, 0].mean()), cy - float(Wv_[:, 1].mean())), nm))
                        if near:
                            nm = min(near)[1]
                            D.objects.pop(nm, None)
                            P.remove_object(nm)
                            out.pop(nm, None)
                            n_patch -= 1
                            _log(f"build_sea: dropped {nm} (two runs {gap_} m apart at ({cx}, {cy}))")
            cov_now = measure_surf(D)["covered_m"] / max(sum(L_["L"] for L_ in loops), 1e-6)
            if cov_now >= surf_min_cover or cover_try >= 0.6 or attempt == 3:
                break
            for nm in made:                                         # too sparse for the hole's short shore: retry with closer patches
                if nm in out:
                    D.objects.pop(nm, None)
                    P.remove_object(nm)
                    out.pop(nm, None)
            _log(f"build_sea: surf covers {100 * cov_now:.1f}% of the shore (< {100 * surf_min_cover:.0f}%): retry with surf_cover {cover_try:.2f} -> {cover_try * 1.35:.2f}")
            cover_try *= 1.35
    _log(f"build_sea: {', '.join(f'{k} {len(v.data.polygons)}f' for k, v in list(out.items())[:4])} ... {len(out)} objects, "
         f"{n_patch} surf patches ({n_rock} at rocks), pockets {len(pk)} ({time.time() - t0:.1f}s)")
    return out


def surf_patch(D, center_m, radius_m=4.0, name=None, seed=0, z=Z_SURF, strength=1.0):
    """One ragged whitewater patch (LK_SURF, vertex colour A fading to 0 at the rim, R phase, UV world/8) - e.g. the foot
    of a waterfall. radius_m <= 4.2 keeps the run along the shore <= 10 m (NO_FOAM_STRIP); a larger one is logged. build_sea never
    deletes it (surf_kind 'patch'), so pass the same centre as a build_sea pocket to widen the shelf there. Returns the object
    (WATER_SURF_nn unless `name`)."""
    if radius_m > 4.4:
        _log(f"surf_patch: WARNING radius {radius_m} m makes a run of ~{2 * 0.9 * radius_m:.0f} m along the shore (NO_FOAM_STRIP: <= 10 m)")
    rnd_seed = D.number * 4243 + seed
    cx, cy = center_m
    n = 48
    ang = np.linspace(0, 2 * math.pi, n, endpoint=False)
    rr = radius_m * (0.72 + 0.5 * vnoise(np.cos(ang) * 3 + 10, np.sin(ang) * 3 + 10, 1.3, rnd_seed))
    ring = np.stack([cx + rr * np.cos(ang), cy + rr * np.sin(ang)], 1)
    nm = name or _next_name(D, "WATER_SURF")
    ob, V = _region_mesh(D, nm, [ring], z, look_material("LK_SURF", D), max(0.8, radius_m / 5))
    d = np.hypot(V[:, 0] - cx, V[:, 1] - cy)
    rl = np.interp(np.arctan2(V[:, 1] - cy, V[:, 0] - cx) % (2 * math.pi), np.append(ang, 2 * math.pi), np.append(rr, rr[0]))
    A = strength * (1.0 - _smoothstep(0.35, 1.0, d / np.maximum(rl, 1e-6)))
    _set_surf_attrs(ob, np.clip(A, 0, 1), vnoise(V[:, 0], V[:, 1], 9.0, rnd_seed + 1))
    ob["surf_kind"] = "patch"
    return ob


# ============================================================================= lava
def build_lava(D, cell_m=3.5, near_m=45.0, mid_m=130.0, extent_m=None, inner_margin_m=8.0, lights=4, light_z=2.2,
               light_points=None, name="WATER_LAVA"):
    """Replace the 1-polygon lava plane by a subdivided WATER_LAVA at z 0 (LK_LAVA, UV world / 24 m): cells of cell_m
    within near_m of the land and over every water ellipse, 2.5 x coarser to mid_m, 7 x coarser out to the extent (a disc
    reaching 40 m past SCENERY['wall_ring'] r_outer, else the shore box + 350 m). The land interior deeper than
    inner_margin_m inside the walls is left out (the undercut walls hide the edge). Also places `lights` LAVA_LIGHT_nn
    EMPTY objects (Unity puts static orange point lights there) light_z above the lava, 3-14 m off the walls, around the
    lava ellipse, inside SCENERY['crater'] rim_radius - 10 m when given, spread by farthest-point sampling starting at the
    pin side; or exactly at light_points [(x_m, y_m[, z])]. Returns {name: obj}."""
    t0 = time.time()
    sc = D.scenery
    wr = sc.get("wall_ring")
    bx0, by0, bx1, by1 = P._shore_bbox_m(D)
    if extent_m is None:
        if wr:
            cx, cy = P.m(wr["cx"], wr["cd"])
            R = wr["r_outer"] * YD + 40.0
            ext = ("disc", cx, cy, R)
        else:
            ext = ("box", bx0 - 350, by0 - 350, bx1 + 350, by1 + 350)
    else:
        ext = extent_m
    if ext[0] == "disc":
        _, cx, cy, R = ext
        x0, y0, x1, y1 = cx - R, cy - R, cx + R, cy + R
    else:
        _, x0, y0, x1, y1 = ext
    ells = [(P.m(hx, hd), w / 2 * YD, l / 2 * YD) for kind, hx, hd, w, l, tag in D.hole.hazards if kind == "water"]

    def grid_pts(cell, off):
        xs = np.arange(x0 + off * cell, x1, cell)
        ys = np.arange(y0, y1, cell * 0.866)
        GX, GY = np.meshgrid(xs, ys, indexing="ij")
        GX = GX + (np.arange(len(ys))[None, :] % 2) * cell * 0.5
        return GX.ravel(), GY.ravel()
    pts = []
    for cell, lo, hi in ((cell_m, -inner_margin_m, near_m), (cell_m * 2.5, near_m, mid_m), (cell_m * 7.0, mid_m, 1e9)):
        X, Y = grid_pts(cell, 0.37)
        if ext[0] == "disc":
            k = np.hypot(X - cx, Y - cy) <= R
            X, Y = X[k], Y[k]
        near_box = (X > bx0 - hi - 5) & (X < bx1 + hi + 5) & (Y > by0 - hi - 5) & (Y < by1 + hi + 5) if hi < 1e8 else np.ones(len(X), bool)
        s_ = np.full(len(X), 1e4)
        cand = (X > bx0 - mid_m - 10) & (X < bx1 + mid_m + 10) & (Y > by0 - mid_m - 10) & (Y < by1 + mid_m + 10)
        if cand.any():
            s_[cand] = P.signed_dist_m(D, X[cand], Y[cand])
        in_ell = np.zeros(len(X), bool)
        for (ex, ey), a, b in ells:
            in_ell |= ((X - ex) / (a + 6)) ** 2 + ((Y - ey) / (b + 6)) ** 2 <= 1
        if lo < 0:
            keep = ((s_ >= lo) & (s_ < hi)) | (in_ell & (s_ >= lo))
        else:
            keep = (s_ >= lo) & (s_ < hi) & ~in_ell & near_box
        pts.append(np.stack([X[keep], Y[keep]], 1))
    if ext[0] == "disc":
        nring = max(64, int(2 * math.pi * R / (cell_m * 7.0)))
        th = np.linspace(0, 2 * math.pi, nring, endpoint=False)
        pts.append(np.stack([cx + R * np.cos(th), cy + R * np.sin(th)], 1))
    Pp = np.vstack(pts)
    V, T = P._cdt(Pp, [])
    cen = V[T].mean(1)
    sdc = P.signed_dist_m(D, cen[:, 0], cen[:, 1])
    T = T[sdc >= -inner_margin_m]
    V, T = P._compact(V, T)
    ob = bpy.data.objects.get(name)
    if ob is None:
        ob = P.new_mesh_object(name, "ENVIRONMENT")
    P.build_mesh(ob, [(float(x), float(y), 0.0) for x, y in V], [tuple(int(i) for i in t) for t in T], smooth=True,
                 materials=[look_material("LK_LAVA", D)])
    D.objects[name] = ob
    _track(D, ob)
    assign_uvs(D, objects=[ob])
    out = {name: ob}
    # ---- LAVA_LIGHT_nn
    for o in [o for o in bpy.data.objects if o.name.startswith("LAVA_LIGHT_")]:
        bpy.data.objects.remove(o, do_unlink=True)
    if light_points is None:
        cand = V[(np.hypot(V[:, 0] - (bx0 + bx1) / 2, V[:, 1] - (by0 + by1) / 2) < 0.75 * max(bx1 - bx0, by1 - by0))]
        s_ = P.signed_dist_m(D, cand[:, 0], cand[:, 1])
        cand = cand[(s_ >= 3.0) & (s_ <= 14.0)]
        if ells:
            (ex, ey), a, b = ells[0]
            de = np.hypot((cand[:, 0] - ex) / (a + 45), (cand[:, 1] - ey) / (b + 45))
            if (de <= 1).sum() >= lights:
                cand = cand[de <= 1]
        cr = sc.get("crater")
        if cr and len(cand):                      # stay well inside SCENERY['crater'] rim_radius (postcard_look_verify limit + 20 m)
            ccx, ccy = P.m(cr["cx"], cr["cd"])
            inside = np.hypot(cand[:, 0] - ccx, cand[:, 1] - ccy) <= cr.get("rim_radius", 200.0) * YD - 10.0
            if inside.sum() >= lights:
                cand = cand[inside]
        pin = np.array(P.m(*D.hole.pin))
        chosen = [int(np.argmin(np.hypot(cand[:, 0] - pin[0], cand[:, 1] - pin[1])))] if len(cand) else []
        while len(chosen) < min(lights, len(cand)):
            dmin = np.min(np.stack([np.hypot(cand[:, 0] - cand[c, 0], cand[:, 1] - cand[c, 1]) for c in chosen]), 0)
            chosen.append(int(np.argmax(dmin)))
        light_points = [(float(cand[c, 0]), float(cand[c, 1]), light_z) for c in chosen]
    for i, p in enumerate(light_points):
        e = bpy.data.objects.new(f"LAVA_LIGHT_{i + 1:02d}", None)
        e.empty_display_type = 'SPHERE'
        e.empty_display_size = 1.5
        bpy.context.scene.collection.objects.link(e)
        P.link_to(e, "ENVIRONMENT")
        e.parent = P.get_root()
        e.location = (p[0], p[1], p[2] if len(p) > 2 else light_z)
        D.objects[e.name] = e
        _track(D, e)
        out[e.name] = e
    _log(f"build_lava: {name} {len(T)} tris ({len(V)} verts), {len(light_points)} LAVA_LIGHT empties ({time.time() - t0:.1f}s)")
    return out


# ============================================================================= stone path
def _ground_bvh(D):
    V, F = [], []
    for ob in _export_meshes(D):
        if not ob.name.startswith(GROUND_PREFIXES):
            continue
        W, ls, lt, lv, mi, pn = _mesh_arrays(ob)
        base = len(V)
        V += [tuple(p) for p in W.tolist()]
        for p in np.nonzero(pn[:, 2] > 0.5)[0].tolist():
            a, n = int(ls[p]), int(lt[p])
            F.append([base + int(v) for v in lv[a:a + n]])
    return BVHTree.FromPolygons(V, F)


def _ground_top(bvh, X, Y, z0, r=0.5):
    out = np.full(len(X), -1e9)
    for dx, dy in (((0, 0),) if r <= 0 else ((0, 0), (r, 0), (-r, 0), (0, r), (0, -r))):
        for i, (x, y) in enumerate(zip(X.tolist(), Y.tolist())):
            hit = bvh.ray_cast(Vector((x + dx, y + dy, z0)), Vector((0, 0, -1)), 50.0)
            if hit[0] is not None:
                out[i] = max(out[i], hit[0].z)
    return out


def build_path(D, polyline, width_m=2.4, units="yd", seed=0, step_m=0.15, across_m=0.15, z_off=Z_PATH, wobble=0.16,
               notch=0.22, skirt_m=0.04, clip_to_land=True, name="DRESS_PATH"):
    """Irregular flagstone path DRESS_PATH_nn (LK_PATH, non-colliding) along `polyline` (course yards, or metres with
    units='m'), Catmull-Rom smoothed. Half-widths wander +-wobble, a few bites (notch), rounded tapered ends. Pieces are clipped
    to land (>= half width + 0.3 m inside the shore).

    UNDER THE BALL (2026-10-04). The mesh is a fine lattice (rows every `step_m`, columns every ~`across_m`). The ground (the
    up-facing collider faces of TERRAIN / FAIRWAY / GREEN / TEE_BOX / BUNKER) is sampled on a lattice twice as fine; each
    vertex gets z = (LOWEST ground sampled in the up-to-four cells around it) + z_off (0.0115 m). So every vertex is at most
    z_off above the collider right below it, and, because the surface inside a cell is a blend of its corners, the path is at
    most z_off above the ground everywhere (it dips UNDER a higher neighbour instead of hiding the ball: it can sink up to one
    cell (0.15 m) into a fairway step, never rise above one). An edge skirt runs from the outline down to skirt_m under the
    lowest ground of its cell, so a border is never a gap and never more than z_off of step. UV: u = lateral / 5 m, v = arc / 5 m.
    Returns the objects."""
    rnd = random.Random(D.number * 7717 + seed)
    sc = YD if units == "yd" else 1.0
    pts = [(x * sc, y * sc) for x, y in polyline]
    cr = P.catmull_rom(pts, samples_per_seg=12, closed=False) if len(pts) > 2 else [Vector(p) for p in pts]
    dense = P.resample([(p[0], p[1]) for p in cr], step_m, closed=False)
    C = np.array([(p[0], p[1]) for p in dense])
    seg = np.diff(C, axis=0)
    Ls = np.hypot(seg[:, 0], seg[:, 1])
    s = np.concatenate([[0.0], np.cumsum(Ls)])
    tg = np.gradient(C, axis=0)
    tg /= np.maximum(np.hypot(tg[:, 0], tg[:, 1]), 1e-9)[:, None]
    rt = np.stack([tg[:, 1], -tg[:, 0]], 1)
    hw = width_m / 2
    nseed = D.number * 53 + seed
    wl = hw * (1 + wobble * 2 * (fbm(s, np.zeros_like(s), 7.0, nseed + 1) - 0.5) + 0.1 * (vnoise(s, 0 * s, 1.1, nseed + 2) - 0.5))
    wr_ = hw * (1 + wobble * 2 * (fbm(s, np.zeros_like(s), 7.0, nseed + 3) - 0.5) + 0.1 * (vnoise(s, 0 * s, 1.1, nseed + 4) - 0.5))
    for w_ in (wl, wr_):
        for _ in range(max(1, int(s[-1] / 9.0))):
            c = rnd.uniform(0, s[-1])
            ln = rnd.uniform(0.5, 1.2)
            w_ *= 1 - notch * np.exp(-((s - c) / ln) ** 2)
    taper = np.sqrt(np.clip(np.minimum(s, s[-1] - s) / 1.2, 0.12, 1.0))
    wl, wr_ = wl * taper, wr_ * taper
    ok = np.ones(len(C), bool)
    if clip_to_land:
        sd = P.signed_dist_m(D, C[:, 0], C[:, 1])
        ok = sd <= -(np.maximum(wl, wr_) + 0.3)
    runs, cur = [], []
    for i, k in enumerate(ok.tolist()):
        if k:
            cur.append(i)
        elif cur:
            runs.append(cur)
            cur = []
    if cur:
        runs.append(cur)
    runs = [r for r in runs if s[r[-1]] - s[r[0]] >= 2.0]
    bvh = _ground_bvh(D)
    mat = look_material("LK_PATH", D)
    tile = LOOK["LK_PATH"][2]
    nl = max(7, int(round(width_m / across_m)) + 1)
    nl += 1 - nl % 2                                          # odd: a column on the centre line
    fr = np.linspace(-1.0, 1.0, nl)
    down = Vector((0, 0, -1))
    objs = []
    for run in runs:
        idx = np.array(run)
        n = len(idx)
        lat = np.where(fr[None, :] < 0, fr[None, :] * wl[idx][:, None], fr[None, :] * wr_[idx][:, None])
        X = C[idx, 0][:, None] + rt[idx, 0][:, None] * lat
        Y = C[idx, 1][:, None] + rt[idx, 1][:, None] * lat
        # ground on the doubled lattice (vertices + edge midpoints + cell centres)
        Xd = np.zeros((2 * n - 1, 2 * nl - 1))
        Yd = np.zeros_like(Xd)
        for A_, Ad in ((X, Xd), (Y, Yd)):
            Ad[::2, ::2] = A_
            Ad[1::2, ::2] = 0.5 * (A_[:-1] + A_[1:])
            Ad[::2, 1::2] = 0.5 * (A_[:, :-1] + A_[:, 1:])
            Ad[1::2, 1::2] = 0.25 * (A_[:-1, :-1] + A_[1:, :-1] + A_[:-1, 1:] + A_[1:, 1:])
        Gd = np.full(Xd.shape, np.nan)
        for a in range(Xd.shape[0]):
            for b in range(Xd.shape[1]):
                hit = bvh.ray_cast(Vector((Xd[a, b], Yd[a, b], D.play_z + 3.0)), down, 50.0)
                if hit[0] is not None:
                    Gd[a, b] = hit[0].z
        Gd = np.where(np.isfinite(Gd), Gd, D.play_z)
        cmin = np.full((n - 1, nl - 1), np.inf)
        for da in range(3):
            for db in range(3):
                cmin = np.minimum(cmin, Gd[da:da + 2 * (n - 1):2, db:db + 2 * (nl - 1):2])
        # vertex (i, j) touches cells (i-1..i, j-1..j)
        cp = np.full((n + 1, nl + 1), np.inf)
        cp[1:n, 1:nl] = cmin
        zmin = np.minimum(np.minimum(cp[:-1, :-1], cp[1:, :-1]), np.minimum(cp[:-1, 1:], cp[1:, 1:]))
        Z = zmin + z_off
        Zs = zmin - skirt_m                                    # skirt foot: skirt_m under the lowest ground of the cell
        verts, faces, uvs = [], [], []
        vid = np.zeros((n, nl + 2), np.int64)
        for i in range(n):
            for k in range(nl):
                vid[i, k + 1] = len(verts)
                verts.append((float(X[i, k]), float(Y[i, k]), float(Z[i, k])))
            for side, k, col in ((-1, 0, 0), (1, nl - 1, nl + 1)):    # skirt foot just outside the outline
                vid[i, col] = len(verts)
                fx, fy = float(X[i, k] + side * rt[idx[i], 0] * 0.04), float(Y[i, k] + side * rt[idx[i], 1] * 0.04)
                hit = bvh.ray_cast(Vector((fx, fy, D.play_z + 3.0)), down, 50.0)
                gz = float(hit[0].z) if hit[0] is not None else float(zmin[i, k])
                Zs[i, k] = min(float(zmin[i, k]), gz) - skirt_m       # under the lowest ground of the cell AND of the spot outside
                verts.append((fx, fy, float(Zs[i, k])))
        lat_full = np.concatenate([lat[:, :1] - 0.04 - skirt_m, lat, lat[:, -1:] + 0.04 + skirt_m], 1)
        for i in range(n - 1):
            for k in range(nl + 1):
                f = (int(vid[i, k]), int(vid[i, k + 1]), int(vid[i + 1, k + 1]), int(vid[i + 1, k]))
                faces.append(f)
                uvs.append([(lat_full[i, k] / tile, s[idx[i]] / tile), (lat_full[i, k + 1] / tile, s[idx[i]] / tile),
                            (lat_full[i + 1, k + 1] / tile, s[idx[i + 1]] / tile), (lat_full[i + 1, k] / tile, s[idx[i + 1]] / tile)])
        for i, back in ((0, True), (n - 1, False)):               # end skirts across the whole end section
            for k in range(nl + 1):
                a, b = int(vid[i, k]), int(vid[i, k + 1])
                d = -tg[idx[i]] * 0.04 if back else tg[idx[i]] * 0.04
                va = len(verts)
                za = float(Zs[i, max(0, min(nl - 1, k - 1))])
                zb = float(Zs[i, max(0, min(nl - 1, k))])
                zz = []
                for vv, z_ in ((verts[a], za), (verts[b], zb)):
                    hit = bvh.ray_cast(Vector((vv[0] + d[0], vv[1] + d[1], D.play_z + 3.0)), down, 50.0)
                    gz = float(hit[0].z) - skirt_m if hit[0] is not None else z_
                    zz.append(min(vv[2], z_, gz))
                verts.append((verts[a][0] + d[0], verts[a][1] + d[1], zz[0]))
                verts.append((verts[b][0] + d[0], verts[b][1] + d[1], zz[1]))
                f = (a, va, va + 1, b) if back else (b, va + 1, va, a)
                faces.append(f)
                v0 = s[idx[i]] / tile
                uvs.append([(lat_full[i, k] / tile, v0), (lat_full[i, k] / tile, v0 - 0.03), (lat_full[i, k + 1] / tile, v0 - 0.03),
                            (lat_full[i, k + 1] / tile, v0)] if back else
                           [(lat_full[i, k + 1] / tile, v0), (lat_full[i, k + 1] / tile, v0 + 0.03), (lat_full[i, k] / tile, v0 + 0.03),
                            (lat_full[i, k] / tile, v0)])
        vsh = math.floor(min(v for fu in uvs for _, v in fu))
        uvs = [[(u, v - vsh) for u, v in fu] for fu in uvs]
        ob = _new_piece(D, _next_name(D, name), verts, faces, uvs, [0] * len(faces), [mat], col="STRUCTURES", sharp_deg=50.0)
        objs.append(ob)
    _log(f"build_path: {len(objs)} pieces, {sum(len(o.data.polygons) for o in objs)} faces, top {z_off * 100:.2f} cm over the lowest ground of each cell")
    return objs


# ============================================================================= export
class _ExportSceneProxy:
    def __init__(self, real, extra):
        self._real, self._extra = real, extra

    def __getattr__(self, k):
        f = getattr(self._real, k)
        if k != "fbx":
            return f
        extra = self._extra

        def fbx(*a, **kw):
            kw.update(extra)
            return f(*a, **kw)
        return fbx


class _OpsProxy:
    def __init__(self, real, extra):
        self._real, self._extra = real, extra

    def __getattr__(self, k):
        if k == "export_scene":
            return _ExportSceneProxy(self._real.export_scene, self._extra)
        return getattr(self._real, k)


class _BpyProxy:
    def __init__(self, real, extra):
        self._real = real
        self.ops = _OpsProxy(real.ops, extra)

    def __getattr__(self, k):
        return getattr(self._real, k)


def export_look_fbx(D, path=None, colors_type="LINEAR"):
    """postcard_lib.export_fbx (same object selection, axes, unit scale, .meta handling, hole 7 guard) with three extra
    exporter options: path_mode='STRIP', embed_textures=False (LOOK_CONTRACT section 4) and colors_type (LINEAR:
    WATER_SURF A / R arrive as raw data). The FBX carries NO texture reference at all (see _passthru: materialName 0 in
    the .meta would otherwise name Unity's material after the texture); any direct image link on an LK_* material made by
    another lib is cut for the duration of the export and restored. Unity's GolfLook loads the PNGs by material name.

    v2 2026-10-05 (plant sway data, work/postcard-look/LIB_PROPS.md 'v2 sway'): every PLANT_ library mesh carries the colour attribute 'Col'
    (R = sway weight, G = phase, B = 0, A = 1) that GolfArcade/GolfPlants reads from Unity's mesh.colors, so the numbers must arrive RAW:
    colors_type must stay 'LINEAR' (the default) - 'SRGB' would gamma-encode the weights (0.5 -> 0.735) and 'NONE' would drop them, so both raise
    ValueError when a mesh with a sway attribute is in the export set. Any mesh in the export set that uses LK_PLANTS but has NO colour attribute
    (hole 10's DRESS_PADEDGE ribbon, a TERRAIN patch painted with the plants swatches) gets a STATIC one (R = 0, B = 0) first: Unity gives a mesh
    without a colour channel the default colour (1, 1, 1, 1) = full sway weight on a ground ribbon. The names of the meshes touched are
    returned in the module variable LAST_EXPORT['static_sway']."""
    if colors_type not in ("LINEAR", "SRGB", "NONE"):
        raise ValueError(f"export_look_fbx: colors_type must be LINEAR, SRGB or NONE (got {colors_type!r})")
    meshes = _export_meshes(D)
    static_done = []
    seen = set()
    for ob in meshes:                               # static sway data for every non-PLANT_ LK_PLANTS mesh without a colour channel
        me = ob.data
        if me is None or me.name in seen:
            continue
        seen.add(me.name)
        if any(m_ is not None and m_.name == "LK_PLANTS" for m_ in me.materials) and len(me.color_attributes) == 0 and not me.name.startswith("PLANT_"):
            att = me.color_attributes.new(name="Col", type='FLOAT_COLOR', domain='POINT')
            col = np.zeros((len(me.vertices), 4))
            col[:, 1] = 0.5
            col[:, 3] = 1.0
            att.data.foreach_set("color", col.ravel())
            me["lk_sway"] = "static"
            static_done.append(me.name)
    if colors_type != "LINEAR":
        sway = [me.name for me in {o.data.name: o.data for o in meshes if o.data is not None}.values()
                if len(me.color_attributes) and me.get("lk_sway") is not None]
        if sway:
            raise ValueError(f"export_look_fbx(colors_type={colors_type!r}) would corrupt / drop the sway weights of {len(sway)} meshes ({sway[:3]}): use 'LINEAR'")
    LAST_EXPORT["static_sway"] = static_done
    extra = dict(path_mode='STRIP', embed_textures=False, colors_type=colors_type)
    cut = []                                      # direct image -> BSDF / Normal Map links (materials made elsewhere)
    for mat in bpy.data.materials:
        if not mat.name.startswith("LK_") or mat.node_tree is None:
            continue
        for ln in list(mat.node_tree.links):
            if ln.from_node.type == 'TEX_IMAGE' and ln.to_node.type in ('BSDF_PRINCIPLED', 'NORMAL_MAP'):
                cut.append((mat.node_tree, ln.from_socket, ln.to_socket))
                mat.node_tree.links.remove(ln)
    real = P.bpy
    P.bpy = _BpyProxy(real, extra)
    try:
        return P.export_fbx(D, path)
    finally:
        P.bpy = real
        for nt, a, b in cut:
            nt.links.new(a, b)


LAST_EXPORT = {}                                  # filled by export_look_fbx: {'static_sway': [mesh names given a static colour attribute]}


# ============================================================================= gates / audits
def snapshot_ground(D):
    """{name: world vertex array} of every TERRAIN/FAIRWAY/GREEN/TEE_BOX/BUNKER/CART_PATH mesh in the export set."""
    out = {}
    for ob in _export_meshes(D):
        if ob.name.startswith(GROUND_PREFIXES):
            out[ob.name] = _mesh_arrays(ob)[0].copy()
    return out


def compare_ground(D, snap, tol=1e-5):
    now = snapshot_ground(D)
    bad = []
    if set(now) != set(snap):
        bad.append(f"ground objects differ: added {sorted(set(now) - set(snap))}, removed {sorted(set(snap) - set(now))}")
    worst = 0.0
    nv = 0
    for k in sorted(set(now) & set(snap)):
        a, b = snap[k], now[k]
        if a.shape != b.shape:
            bad.append(f"{k} vertex count {len(a)} -> {len(b)}")
            continue
        d = float(np.abs(a - b).max()) if len(a) else 0.0
        worst = max(worst, d)
        nv += len(a)
        if d > tol:
            bad.append(f"{k} moved {d:.2e} m")
    ok = not bad
    return ok, (f"{len(now)} ground meshes, {nv} vertices, max |delta| {worst:.2e} m (tol {tol:g})" + ("" if ok else "; " + "; ".join(bad[:6])))


def _uv_stats(ob, tile_of, wall_mask=None):
    me = ob.data
    if len(me.uv_layers) != 1:
        return False, f"{ob.name}: {len(me.uv_layers)} UV layers"
    W, ls, lt, lv, mi, pn = _mesh_arrays(ob)
    uv = np.empty(len(lv) * 2)
    me.uv_layers[0].data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    if not np.isfinite(uv).all():
        return False, f"{ob.name}: NaN/inf UV"
    if np.abs(uv).max() > 1e4:
        return False, f"{ob.name}: |uv| {np.abs(uv).max():.0f}"
    mats = [_mat_name(m_) for m_ in me.materials] or [""]
    ratios, wall_r = [], []
    for p in range(len(ls)):
        a, n = int(ls[p]), int(lt[p])
        tile = tile_of(mats[min(int(mi[p]), len(mats) - 1)])
        wall = wall_mask is not None and wall_mask[p]
        for k in range(n):
            i, j = a + k, a + (k + 1) % n
            wl = float(np.linalg.norm(W[lv[i]] - W[lv[j]]))
            if wl < 0.05:
                continue
            (wall_r if wall else ratios).append(float(np.linalg.norm(uv[i] - uv[j])) * tile / wl)
    r = np.array(ratios) if ratios else np.array([1.0])
    rw = np.array(wall_r) if wall_r else np.array([1.0])
    return True, (r.min(), np.percentile(r, 99.9), r.max(), float(np.abs(uv).max()), float(rw.max()))


def audit_look(D, uv_ratio=(0.45, 2.2)):
    """GATE tuples (name, ok, detail) on the current scene: no new object with a ground prefix, no MAT_FOAM, UV sanity
    (one layer, finite, |uv| < 1e4, world-vs-UV edge ratio within uv_ratio on ground/water meshes), skin under the play
    height, water stack inside z 0..0.2, surf pieces broken + vertex colours, lava tri count."""
    res = []
    ex = P._export_objects(D)
    meshes = [o for o in ex if o.type == 'MESH']
    ground = sorted(o.name for o in meshes if o.name.startswith(GROUND_PREFIXES))
    allowed = [r"^TERRAIN_[A-Za-z0-9_]+$", r"^FAIRWAY$", r"^FAIRWAY_FIRSTCUT$", r"^GREEN$", r"^GREEN_APRON$", r"^TEE_BOX$",
               r"^BUNKER_\d\d$", r"^BUNKER_\d\d_LIP$"]
    import re
    odd = [n for n in ground if not any(re.match(p, n) for p in allowed)]
    mine = D.__dict__.get("look_objects", [])
    bad_new = [n for n in mine if n.startswith(GROUND_PREFIXES)]
    res.append(("LOOK_NO_COLLISION_PREFIX", not odd and not bad_new,
                f"{len(mine)} objects made by postcard_look_lib, none with a ground prefix; ground meshes {len(ground)} all on the "
                f"postcard_verify allow-list" + (f"; BAD {odd + bad_new}" if odd or bad_new else "")))
    foam = sorted({o.name for o in meshes for m_ in o.data.materials if m_ is not None and m_.name.startswith("MAT_FOAM")})
    foam_obj = [o.name for o in meshes if o.name.startswith("WATER_FOAM")]
    mf = bpy.data.materials.get("MAT_FOAM")
    res.append(("LOOK_NO_MAT_FOAM", not foam and not foam_obj and (mf is None or mf.users == 0),
                f"exported meshes using MAT_FOAM {foam}, WATER_FOAM* objects {foam_obj}, MAT_FOAM datablock users "
                f"{mf.users if mf else 'gone'} (0-user data is neither saved nor exported)"))
    flat_mats = sorted({_mat_name(m_) for o in meshes if o.name.startswith(GROUND_PREFIXES + ("WATER_",))
                        for m_ in o.data.materials if m_ is not None and not _mat_name(m_).startswith("LK_")})
    res.append(("LOOK_GROUND_WATER_ARE_LK", not flat_mats, f"non-LK materials on ground/water meshes: {flat_mats}"))
    tile_of = lambda n: (LOOK[n][2] if n in LOOK and LOOK[n][2] > 0 else 8.0)
    worst, probs = [], []
    for o in meshes:
        if not (o.name.startswith(GROUND_PREFIXES) or o.name.startswith(("WATER_OCEAN", "WATER_SHELF", "WATER_LAVA"))):
            continue
        wm = None
        if o.name.startswith("TERRAIN"):
            W_, ls_, lt_, lv_, mi_, pn_ = _mesh_arrays(o)
            wm = ~_top_mask(D, W_, ls_, lt_, lv_, pn_)
        ok, st = _uv_stats(o, tile_of, wm)
        if not ok:
            probs.append(st)
            continue
        lo_, p999, hi_, mx, wall_max = st
        worst.append((o.name, lo_, hi_, mx, wall_max))
        if lo_ < uv_ratio[0] or hi_ > uv_ratio[1]:
            probs.append(f"{o.name} UV/world edge ratio {lo_:.2f}..{hi_:.2f}")
        if wall_max > 8.0:
            probs.append(f"{o.name} wall UV jump: ratio {wall_max:.1f} (> 8 = a seam inside a face)")
    res.append(("LOOK_UV_SANE", not probs,
                f"{len(worst)} ground/water meshes: one UV layer, finite, max |uv| {max((w[3] for w in worst), default=0):.1f}, "
                f"UV*tile/world edge ratio on every top/flat face {min((w[1] for w in worst), default=1):.2f}..{max((w[2] for w in worst), default=1):.2f} "
                f"(limits {uv_ratio[0]}..{uv_ratio[1]}); TERRAIN wall faces (hidden by the skin; arc-of-the-top-loop u) max ratio "
                f"{max((w[4] for w in worst), default=1):.2f} (<= 8: no seam jump)" + (f"; PROBLEMS {probs[:5]}" if probs else "")))
    skins = [o for o in meshes if o.name.startswith("ROCK_SKIN")]
    if skins:
        zmax = max(float(_mesh_arrays(o)[0][:, 2].max()) for o in skins)
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in skins)
        res.append(("LOOK_SKIN_BELOW_PLAY", zmax <= D.play_z - 0.05,
                    f"{len(skins)} ROCK_SKIN pieces, {tris} tris, highest vertex {zmax:.3f} m (play {D.play_z} - 0.05)"))
    wat = [o for o in meshes if o.name.startswith("WATER_")]
    # WATER_FALL_nn (the waterfall sheet, prefix mandated by LOOK_CONTRACT section 2) is vertical by design: not part of the flat stack (LIB_BUGS hole 8 #1)
    zs = [(o.name, float(_mesh_arrays(o)[0][:, 2].min()), float(_mesh_arrays(o)[0][:, 2].max())) for o in wat
          if len(o.data.vertices) and not o.name.startswith("WATER_FALL")]
    badz = [z for z in zs if z[1] < -1e-3 or z[2] > 0.2]
    res.append(("LOOK_WATER_STACK", not badz, f"{len(zs)} WATER_* meshes inside z 0..0.2 (WATER_FALL sheets excluded: vertical by design)" + (f"; BAD {badz[:4]}" if badz else "")))
    surfs = [o for o in wat if o.name.startswith("WATER_SURF")]
    if surfs:
        okc, nparts, longest = True, 0, 0.0
        for o in surfs:
            me = o.data
            if "Col" not in me.color_attributes:
                okc = False
                continue
            bm = bmesh.new()
            bm.from_mesh(me)
            bm.verts.ensure_lookup_table()
            seen = set()
            for v in bm.verts:
                if v.index in seen:
                    continue
                stack, comp = [v], []
                seen.add(v.index)
                while stack:
                    a = stack.pop()
                    comp.append(a.co.xy.copy())
                    for e in a.link_edges:
                        b = e.other_vert(a)
                        if b.index not in seen:
                            seen.add(b.index)
                            stack.append(b)
                nparts += 1
                cc = np.array([(c.x, c.y) for c in comp])
                longest = max(longest, float(np.hypot(*(cc.max(0) - cc.min(0)))))
            bm.free()
        res.append(("LOOK_SURF_BROKEN", okc and nparts >= 8 and longest <= 14.0,
                    f"{len(surfs)} WATER_SURF objects, {nparts} separate pieces (>= 8; was >= 12 on the old near-continuous band), "
                    f"longest piece {longest:.1f} m (<= 14 bbox diagonal of one patch; was <= 140; the run along the shore is capped at 10 m by NO_FOAM_STRIP), vertex colour 'Col' on all: {okc}"))
    lava = bpy.data.objects.get("WATER_LAVA")
    if lava is not None and lava in meshes:
        me = lava.data
        me.calc_loop_triangles()
        nt = len(me.loop_triangles)
        lm = [_mat_name(m_) for m_ in me.materials]
        lights = [o.name for o in ex if o.name.startswith("LAVA_LIGHT_")]
        res.append(("LOOK_LAVA_MESH", nt >= 3000 and lm == ["LK_LAVA"] and 3 <= len(lights) <= 5,
                    f"WATER_LAVA {nt} tris (>= 3000), materials {lm}, {len(lights)} LAVA_LIGHT empties (3..5)"))
    return res


def fbx_check(path, expect_ground, expect_lk=True):
    """Re-import `path` into an EMPTY scene (destroys the open one) and measure: ground-prefix object set == expect_ground,
    every ground/water mesh carries LK_* materials and exactly one UV layer, WATER_SURF meshes carry a colour layer,
    no MAT_FOAM. Returns [(name, ok, detail)]."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    res = []
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    ground = sorted(o.name for o in meshes if o.name.startswith(GROUND_PREFIXES))
    res.append(("FBX_GROUND_SET", ground == sorted(expect_ground), f"ground meshes {len(ground)} == expected {len(expect_ground)}"
                + ("" if ground == sorted(expect_ground) else f"; got {ground} want {sorted(expect_ground)}")))
    bad_mat, bad_uv, lk_names = [], [], set()
    for o in meshes:
        names = [m_.name for m_ in o.data.materials if m_]
        lk_names |= {n for n in names if n.startswith("LK_")}
        if o.name.startswith(GROUND_PREFIXES + ("WATER_",)) and (not names or not all(n.startswith("LK_") for n in names)):
            bad_mat.append((o.name, names))
        if o.name.startswith(GROUND_PREFIXES + ("WATER_", "ROCK_SKIN", "DRESS_PATH")) and len(o.data.uv_layers) != 1:
            bad_uv.append((o.name, len(o.data.uv_layers)))
    res.append(("FBX_LK_MATERIALS", not bad_mat, f"LK materials in the file: {sorted(lk_names)}" + (f"; BAD {bad_mat[:4]}" if bad_mat else "")))
    res.append(("FBX_UV0", not bad_uv, f"every ground/water/skin/path mesh has exactly one UV layer" + (f"; BAD {bad_uv[:4]}" if bad_uv else "")))
    surfs = [o for o in meshes if o.name.startswith("WATER_SURF")]
    nocol = [o.name for o in surfs if len(o.data.color_attributes) == 0]
    amax = 0.0
    for o in surfs:
        if len(o.data.color_attributes):
            a = np.empty(len(o.data.color_attributes[0].data) * 4)
            o.data.color_attributes[0].data.foreach_get("color", a)
            amax = max(amax, float(a.reshape(-1, 4)[:, 3].max()))
    res.append(("FBX_SURF_COLOURS", (not surfs) or (not nocol and amax > 0.5),
                f"{len(surfs)} WATER_SURF meshes, all with a colour layer: {not nocol}, max alpha {amax:.2f}"))
    imgs = sorted(i.name for i in bpy.data.images)
    res.append(("FBX_NO_TEXTURE_REFS", not imgs, f"texture references in the FBX: {imgs[:6]} (Unity names materials by base "
                f"texture name with materialName 0, so the LK_* names only survive without them)"))
    foam = [m_.name for m_ in bpy.data.materials if m_.name.startswith("MAT_FOAM")]
    res.append(("FBX_NO_MAT_FOAM", not foam, f"MAT_FOAM materials in the file: {foam}"))
    return res


# ============================================================================= v2 measurements + gates (2026-10-04)
# Everything below only READS the scene (and the design handle D for loops / ellipses). Every function finds the TERRAIN
# by name, so it also works after `bpy.ops.wm.open_mainfile` on a saved look blend (see work/postcard-look/v2/ground/tools/
# measure_blend.py). Gate ids returned by v2_gates: SHELF_THIN, NO_FOAM_STRIP, PATH_UNDER_BALL, CRATER_WALL_COVERED (info
# on strata holes), SKIN_NOT_OVER_WATER, SKIN_FACES_OUTWARD.
BALL_R_M = 0.055                      # the game ball is ~0.11 m across (GolfGame.cs: 0.12 yd)


def _terrain_ob(D):
    ob = bpy.data.objects.get(D.terrain_name)
    return ob if ob is not None else D.terrain


def _bvh_of(obs, up_only=False, nz_min=0.0):
    V, F = [], []
    for o in obs:
        W, ls, lt, lv, mi, pn = _mesh_arrays(o)
        base = len(V)
        V += [tuple(p) for p in W.tolist()]
        for p in range(len(ls)):
            if up_only and pn[p, 2] <= nz_min:
                continue
            a, n = int(ls[p]), int(lt[p])
            F.append([base + int(v) for v in lv[a:a + n]])
    return BVHTree.FromPolygons(V, F) if F else None


def _water_ellipses(D):
    """[(cx, cy, a, b, tag)] metres: the scoring WATER hazard ellipses (the analytic ones LieAt uses)."""
    out = []
    for kind, hx, hd, w, l, tag in D.hole.hazards:
        if kind == "water":
            cx, cy = P.m(hx, hd)
            out.append((cx, cy, w / 2 * YD, l / 2 * YD, tag))
    return out


def _ell_depth(ells, X, Y):
    """First-order depth (m) of points inside the deepest water ellipse: > 0 inside, < 0 outside (-1e9 without ellipses)."""
    X = np.asarray(X, float)
    Y = np.asarray(Y, float)
    best = np.full(X.shape, -1e9)
    for cx, cy, a, b, _ in ells:
        dx, dy = X - cx, Y - cy
        e = (dx / a) ** 2 + (dy / b) ** 2 - 1.0
        g = 2.0 * np.sqrt((dx / a ** 2) ** 2 + (dy / b ** 2) ** 2)
        best = np.maximum(best, -e / np.maximum(g, 1e-9))
    return best


def _ell_inward(ells, x, y):
    """Unit vector (2,) at (x, y) pointing deeper into the nearest-depth water ellipse (the direction a prism must move AWAY from)."""
    best, bv = -1e18, np.array([0.0, 0.0])
    for cx, cy, a, b, _ in ells:
        dx, dy = x - cx, y - cy
        e = (dx / a) ** 2 + (dy / b) ** 2 - 1.0
        g = np.array([2 * dx / a ** 2, 2 * dy / b ** 2])
        gl = max(float(np.hypot(*g)), 1e-9)
        d = -e / gl
        if d > best:
            best, bv = d, -g / gl
    return bv


def _skin_objs():
    return [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("ROCK_SKIN")]


def _outer_face(D, trees, Pc, N, z, reach=14.0):
    """Distance (m, along the outward normal from the loop point; + = out over the water) of the outermost skin / wall surface
    at height z, per column: a horizontal ray from `reach` m out. nan: nothing hit, ray origin inside land, or the hit is
    not this shore (|offset| > 5 m)."""
    out = np.full(len(Pc), np.nan)
    org = Pc + N * reach
    free = P.signed_dist_m(D, org[:, 0], org[:, 1]) > 0.5
    for i in range(len(Pc)):
        if not free[i]:
            continue
        o = Vector((org[i, 0], org[i, 1], z))
        d = Vector((-N[i, 0], -N[i, 1], 0.0))
        best = None
        for t in trees:
            if t is None:
                continue
            h = t.ray_cast(o, d, reach + 6.0)
            if h[0] is not None and (best is None or h[3] < best):
                best = h[3]
        if best is not None and -5.0 <= reach - best <= 5.0:
            out[i] = reach - best
    return out


def _runs(mask):
    """Runs of True in a circular boolean array: [(start, length)] (a run crossing the end is merged)."""
    n = len(mask)
    if n == 0 or not mask.any():
        return []
    if mask.all():
        return [(0, n)]
    k0 = int(np.nonzero(~mask)[0][0])
    m2 = np.roll(mask, -k0)
    out, i = [], 0
    while i < n:
        if m2[i]:
            j = i
            while j < n and m2[j]:
                j += 1
            out.append(((i + k0) % n, j - i))
            i = j
        else:
            i += 1
    return out


def measure_shelf(D, step=1.0, probe=0.1, pockets=None, pocket_pad_m=0.0):
    """Visible WATER_SHELF width beyond the cliff skin along every shore loop (a sample every `step` m).
    width_wl  = outermost contiguous shelf (gaps < 0.4 m bridged) beyond the skin's outer face AT THE WATERLINE (horizontal
                ray at z 0.12), measured along the outward normal; 0 = no shelf beyond the face (a break)
    width_top = the same beyond the skin's top-down footprint (outermost face at any height 0.12 .. 5.5)
    pockets   [(x_m, y_m, r_m)]: samples inside them are reported separately (waterfall foot, Split's channel gap)
    Returns dict of arrays + the sample positions."""
    sk, terr = _bvh_of(_skin_objs()), _bvh_of([_terrain_ob(D)])
    shelf = bpy.data.objects.get("WATER_SHELF")
    sh = _bvh_of([shelf]) if shelf is not None else None
    rec = dict(w_wl=[], w_top=[], ext_wl=[], ext_max=[], x=[], y=[], loop=[], pocket=[])
    for li, (Pl, _) in enumerate(terrain_top_loops(D)):
        Pc, s, Lp, N, _ = _loop_columns(Pl, step)
        ext_wl = _outer_face(D, (sk, terr), Pc, N, 0.12)
        ext_max = ext_wl.copy()
        for z in (0.5, 1.0, 2.0, 3.5, 5.5):
            ext_max = np.fmax(ext_max, _outer_face(D, (sk,), Pc, N, z))
        for i in range(len(Pc)):
            if not np.isfinite(ext_wl[i]):
                continue
            n = N[i]
            res = []
            for base in (ext_wl[i], ext_max[i] if np.isfinite(ext_max[i]) else ext_wl[i]):
                t, started, miss, last = base - 0.8, False, 0, base
                while t < base + 40.0:
                    present = False
                    if sh is not None:
                        h = sh.ray_cast(Vector((Pc[i, 0] + n[0] * t, Pc[i, 1] + n[1] * t, 2.0)), Vector((0, 0, -1)), 3.0)
                        present = h[0] is not None
                    if present:
                        started, miss, last = True, 0, t
                    else:
                        miss += 1
                        if (not started and t > base + 0.6) or (started and miss * probe > 0.4):
                            break
                    t += probe
                res.append(max(0.0, last - base) if started else 0.0)
            rec["w_wl"].append(res[0])
            rec["w_top"].append(res[1])
            rec["ext_wl"].append(ext_wl[i])
            rec["ext_max"].append(ext_max[i])
            rec["x"].append(Pc[i, 0])
            rec["y"].append(Pc[i, 1])
            rec["loop"].append(li)
            inp = False
            for (px, py, pr) in (pockets or []):
                if math.hypot(Pc[i, 0] - px, Pc[i, 1] - py) <= pr + pocket_pad_m:
                    inp = True
            rec["pocket"].append(inp)
    return {k: np.array(v) for k, v in rec.items()}


def shelf_gate(D, pockets=None, step=1.0):
    """GATE SHELF_THIN: median <= 2.5 m and p95 <= 4 m of the shelf width beyond the skin at the waterline (every sample, breaks
    counted as 0), no sample > 4.5 m outside the pockets and none > 6 m inside them, not a uniform ring (CV >= 0.3 of the width
    over samples with a shelf). Also reports the top-down numbers and the shelf area."""
    m_ = measure_shelf(D, step=step, pockets=pockets)
    w, wt, pk = m_["w_wl"], m_["w_top"], m_["pocket"]
    if len(w) == 0:
        return "SHELF_THIN", False, "no shore samples measured"
    present = w > 0.15
    shelf = bpy.data.objects.get("WATER_SHELF")
    area = 0.0
    if shelf is not None:
        Wv, ls, lt, lv, mi, pn = _mesh_arrays(shelf)
        for p in range(len(ls)):
            a, n = int(ls[p]), int(lt[p])
            vs = Wv[lv[a:a + n]]
            for k in range(1, n - 1):
                area += 0.5 * abs((vs[k][0] - vs[0][0]) * (vs[k + 1][1] - vs[0][1]) - (vs[k + 1][0] - vs[0][0]) * (vs[k][1] - vs[0][1]))
    med, p95 = float(np.median(w)), float(np.percentile(w, 95))
    mx_out = float(w[~pk].max()) if (~pk).any() else 0.0
    mx_pk = float(w[pk].max()) if pk.any() else 0.0
    cv = float(w[present].std() / max(w[present].mean(), 1e-6)) if present.sum() > 3 else 0.0
    ok = med <= 2.5 and p95 <= 4.0 and mx_out <= 4.5 and mx_pk <= 6.0 and cv >= 0.3
    det = (f"{len(w)} shore samples (every {step:g} m, {len(set(m_['loop'].tolist()))} loops): visible shelf width beyond the skin at "
           f"the waterline median {med:.2f} m (<= 2.5), p95 {p95:.2f} m (<= 4), max {mx_out:.2f} m outside pockets (<= 4.5), "
           f"{mx_pk:.2f} m in {int(pk.sum())} pocket samples (<= 6); breaks (no shelf) {100 * (~present).mean():.0f}% of samples; "
           f"where present median {float(np.median(w[present])) if present.any() else 0:.2f} m, CV {cv:.2f} (>= 0.3); top-down "
           f"footprint width median {float(np.median(wt)):.2f} m, p95 {float(np.percentile(wt, 95)):.2f} m; WATER_SHELF area {area:.0f} m2")
    return "SHELF_THIN", ok, det


def _surf_data(D):
    """Merged BVH + per-vertex alpha of every WATER_SURF object, and its connected components."""
    V, F, A, comps = [], [], [], []
    for o in sorted((o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("WATER_SURF")), key=lambda o: o.name):
        me = o.data
        if "Col" not in me.color_attributes:
            continue
        Wv, ls, lt, lv, mi, pn = _mesh_arrays(o)
        col = np.empty(len(me.color_attributes["Col"].data) * 4)
        me.color_attributes["Col"].data.foreach_get("color", col)
        a_v = col.reshape(-1, 4)[:, 3]
        base = len(V)
        V += [tuple(p) for p in Wv.tolist()]
        A += a_v.tolist()
        faces = [lv[int(a):int(a) + int(n)].tolist() for a, n in zip(ls, lt)]
        F += [[base + v for v in f] for f in faces]
        par = list(range(len(Wv)))

        def find(x):
            while par[x] != x:
                par[x] = par[par[x]]
                x = par[x]
            return x
        for f in faces:
            r0 = find(f[0])
            for v in f[1:]:
                r1 = find(v)
                if r1 != r0:
                    par[r1] = r0
        groups = {}
        for v in set(lv.tolist()):
            groups.setdefault(find(v), []).append(v)
        for g in groups.values():
            comps.append((o.name, Wv[g], a_v[g]))
    return V, F, np.array(A), comps


def _strip_metrics(xy, a):
    """Oriented-box length / width of a vertex cloud, the width profile CV over 8 bins along the long axis, and the maximum
    alpha at the two extreme ends (outer 6 % of the long axis)."""
    c = xy - xy.mean(0)
    u, s, vt = np.linalg.svd(c, full_matrices=False)
    p, q = c @ vt[0], c @ vt[1]
    Lg, Wd = float(p.max() - p.min()), float(q.max() - q.min())
    edges = np.linspace(p.min(), p.max(), 9)
    ws = []
    for k in range(8):
        mk = (p >= edges[k]) & (p <= edges[k + 1])
        ws.append(float(q[mk].max() - q[mk].min()) if mk.sum() >= 2 else 0.0)
    ws = np.array(ws)
    cv = float(ws.std() / max(ws.mean(), 1e-9))
    end = (p <= p.min() + 0.06 * Lg) | (p >= p.max() - 0.06 * Lg)
    return Lg, Wd, cv, float(a[end].max()) if end.any() else 0.0


def measure_surf(D, step=0.5, dmax=1.0, amin=0.3):
    """Whitewater coverage along the shoreline + per-component shape numbers.
    A shore sample (every `step` m along the terrain loops) is COVERED when an up-facing WATER_SURF triangle whose mean vertex alpha
    is >= amin lies within dmax m of the point (x, y, 0.12): the same rule postcard_look_verify.surf_cover_mask uses (it samples the
    design shore, which is these loops). Returns dict: covered / total shoreline length, runs (m), gaps between consecutive runs
    (m, same loop), per-component shape numbers."""
    V, F, A, comps = _surf_data(D)
    out = dict(n_obj=len({c[0] for c in comps}), n_comp=len(comps), runs=[], gaps=[], covered_m=0.0, total_m=0.0, comp=[])
    for name, xy, a in comps:
        Lg, Wd, cv, aend = _strip_metrics(xy[:, :2], a)
        out["comp"].append(dict(obj=name, length=Lg, width=Wd, aspect=Lg / max(Wd, 1e-6), cv=cv, a_end=aend, amax=float(a.max()),
                                strip=(Lg / max(Wd, 1e-6) >= 4.0 and cv <= 0.35)))
    if not F:
        return out
    tri = [f for f in F if len(f) == 3]
    ta = np.array([np.mean([A[k] for k in f]) for f in tri])
    sb = BVHTree.FromPolygons(V, tri, all_triangles=True)
    for li, (Pl, _) in enumerate(terrain_top_loops(D)):
        Pc, s, Lp, N, _ = _loop_columns(Pl, step)
        cov = np.zeros(len(Pc), bool)
        for i in range(len(Pc)):
            r = sb.find_nearest(Vector((Pc[i, 0], Pc[i, 1], 0.12)), dmax)
            if r[0] is not None and ta[r[2]] >= amin:
                cov[i] = True
        out["total_m"] += Lp
        out["covered_m"] += float(cov.sum()) * step
        rr = _runs(cov)
        out["runs"] += [ln * step for _, ln in rr]
        if len(rr) >= 2 and not cov.all():
            rr_s = sorted(rr)
            for k in range(len(rr_s)):
                a0, l0 = rr_s[k]
                b0, _ = rr_s[(k + 1) % len(rr_s)]
                gap = ((b0 - (a0 + l0)) % len(cov)) * step
                out["gaps"].append(gap)
                if gap < 8.0:
                    out.setdefault("close", []).append((round(float(Pc[(a0 + l0) % len(Pc), 0]), 1), round(float(Pc[(a0 + l0) % len(Pc), 1]), 1), round(gap, 1)))
    return out


def surf_gate(D, step=0.5):
    """GATE NO_FOAM_STRIP: no WATER_FOAM object / MAT_FOAM material; every contiguous whitewater run <= 10 m, runs separated by
    >= 8 m, total <= 25 % of the shoreline; no component that is a near-constant-width strip (aspect >= 4 and width CV <= 0.35);
    alpha <= 0.1 at both ends of every component."""
    meshes = [o for o in P._export_objects(D) if o.type == 'MESH']
    foam = [o.name for o in meshes if o.name.startswith(("WATER_FOAM", "DRESS_SHOREBAND")) or
            any(m_ is not None and m_.name.startswith(("MAT_FOAM", "LK_FOAM")) for m_ in o.data.materials)]
    m_ = measure_surf(D, step)
    runs, gaps = m_["runs"], m_["gaps"]
    cov = m_["covered_m"] / max(m_["total_m"], 1e-6)
    strips = [c for c in m_["comp"] if c["strip"]]
    bad_end = [c for c in m_["comp"] if c["a_end"] > 0.1]
    mx_run = max(runs) if runs else 0.0
    mn_gap = min(gaps) if gaps else 99.0
    ok = (not foam and mx_run <= 10.0 and mn_gap >= 8.0 and cov <= 0.25 and not strips and not bad_end)
    close = m_.get("close", [])[:4]
    det = (f"{len(meshes)} export meshes: foam objects/materials {foam}; {m_['n_obj']} WATER_SURF objects, {m_['n_comp']} components; "
           f"coverage {100 * cov:.1f}% of {m_['total_m']:.0f} m shoreline (<= 25), {len(runs)} runs along the shore, longest "
           f"{mx_run:.1f} m (<= 10), shortest gap between runs {mn_gap:.1f} m (>= 8), strips (aspect >= 4 and width CV <= 0.35) "
           f"{len(strips)}, components with alpha > 0.1 at an end {len(bad_end)}; component aspect max "
           f"{max((c['aspect'] for c in m_['comp']), default=0):.2f}, alpha max {max((c['amax'] for c in m_['comp']), default=0):.2f}"
           + (f"; runs closer than 8 m at (x, y, gap) {close}" if close else ""))
    return "NO_FOAM_STRIP", ok, det


def _height_grid(bvh, x0, y0, nx, ny, h, mask=None, z0=12.0):
    """Top-down hit z of a BVH on a lattice (nan = miss). mask (nx, ny) bool restricts the casts."""
    Z = np.full((nx, ny), np.nan)
    d = Vector((0, 0, -1))
    idx = np.argwhere(mask) if mask is not None else np.argwhere(np.ones((nx, ny), bool))
    for i, j in idx.tolist():
        hit = bvh.ray_cast(Vector((x0 + i * h, y0 + j * h, z0)), d, 40.0)
        if hit[0] is not None:
            Z[i, j] = hit[0].z
    return Z


def measure_path(D, h=0.0275, margin=0.15):
    """DRESS_PATH vs the collider under it. (1) vertex excess = path vertex z - ground z right below it, (2) dense excess on a
    lattice of pitch h (path top minus ground top wherever the path covers), (3) ball test: a sphere of BALL_R_M resting on the
    collider (centre on the ground lattice, rest height = max over its disk of ground + chord) and the share of its volume under
    the path surface. Returns dict of numbers (None when no path)."""
    paths = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("DRESS_PATH")]
    if not paths:
        return None
    gobs = [o for o in _export_meshes(D) if o.name.startswith(GROUND_PREFIXES)]
    gb = _bvh_of(gobs, up_only=True, nz_min=0.5)
    out = dict(vert_excess_max=-1e9, vert_n=0, dense=[], hidden=[], border=[], skirt_min=1e9, area=0.0, buried_share=0.0)
    d = Vector((0, 0, -1))
    for po in paths:
        Wp, ls, lt, lv, mi, pn = _mesh_arrays(po)
        for k in range(len(Wp)):
            hit = gb.ray_cast(Vector((Wp[k, 0], Wp[k, 1], D.play_z + 3.0)), d, 20.0)
            if hit[0] is None:
                continue
            ex = Wp[k, 2] - hit[0].z
            if ex > -0.02:                                     # skirt feet (>= 2 cm under the ground) are not 'path top'
                out["vert_excess_max"] = max(out["vert_excess_max"], ex)
            out["skirt_min"] = min(out["skirt_min"], ex)
            out["vert_n"] += 1
        pb = _bvh_of([po], up_only=True, nz_min=0.0)
        x0, y0 = Wp[:, 0].min() - margin, Wp[:, 1].min() - margin
        nx = int((Wp[:, 0].max() + margin - x0) / h) + 2
        ny = int((Wp[:, 1].max() + margin - y0) / h) + 2
        # footprint mask from a coarse cast (path present within one cell) to keep the number of rays down
        Zp = _height_grid(pb, x0, y0, nx, ny, h)
        foot = np.isfinite(Zp)
        if not foot.any():
            continue
        near = foot.copy()
        for sx in (-3, -2, -1, 0, 1, 2, 3):
            for sy in (-3, -2, -1, 0, 1, 2, 3):
                near |= np.roll(np.roll(foot, sx, 0), sy, 1)
        Zg = _height_grid(gb, x0, y0, nx, ny, h, mask=near)
        both = foot & np.isfinite(Zg)
        ex = (Zp - Zg)[both]
        out["dense"].append(ex)
        out["area"] += float(foot.sum()) * h * h
        out["buried_share"] += float((ex < -0.005).sum())
        ring = foot & ~(np.roll(foot, 1, 0) & np.roll(foot, -1, 0) & np.roll(foot, 1, 1) & np.roll(foot, -1, 1))
        bm = ring & np.isfinite(Zg)
        out["border"].append((Zp - Zg)[bm])
        # ball test on a lattice of every 2nd node
        r = int(round(BALL_R_M / h))
        offs = [(a, b) for a in range(-r, r + 1) for b in range(-r, r + 1) if (a * h) ** 2 + (b * h) ** 2 <= BALL_R_M ** 2 + 1e-9]
        chord = np.array([math.sqrt(max(BALL_R_M ** 2 - (a * h) ** 2 - (b * h) ** 2, 0.0)) for a, b in offs])
        Zp2 = np.where(np.isfinite(Zp), Zp, -1e9)
        Zg2 = np.where(np.isfinite(Zg), Zg, np.nan)
        cs = np.argwhere(foot[r:nx - r:2, r:ny - r:2] & np.isfinite(Zg[r:nx - r:2, r:ny - r:2])) * 2 + r
        for ci, cj in cs.tolist():
            gz = np.array([Zg2[ci + a, cj + b] for a, b in offs])
            if not np.isfinite(gz).all():
                continue
            c_z = float(np.max(gz + chord))
            pz_ = np.array([Zp2[ci + a, cj + b] for a, b in offs])
            lo, ln = c_z - chord, 2 * chord
            hid = np.clip(pz_ - lo, 0.0, ln)
            out["hidden"].append(float(hid.sum() / ln.sum()))
    for key in ("dense", "border"):
        out[key] = np.concatenate(out[key]) if out[key] else np.zeros(0)
    out["hidden"] = np.array(out["hidden"])
    return out


def path_gate(D, h=0.0275):
    """GATE PATH_UNDER_BALL (when the hole has a DRESS_PATH): every path vertex <= 1.2 cm above the collider right below it, no
    point of the dense lattice > 1.5 cm above the ground, skirt feet >= 3 cm under the ground, a resting ball (r 0.055 m) is
    never more than 10 % hidden under the path surface (all positions)."""
    m_ = measure_path(D, h)
    if m_ is None:
        return "PATH_UNDER_BALL", True, "no DRESS_PATH on this hole (nothing to measure)"
    dn, hid, bd = m_["dense"], m_["hidden"], m_["border"]
    mx_dense = float(dn.max()) if len(dn) else 0.0
    ok = (m_["vert_excess_max"] <= 0.012 + 1e-4 and mx_dense <= 0.015 and m_["skirt_min"] <= -0.03 and
          (len(hid) > 0 and float(hid.max()) <= 0.10))
    det = (f"{m_['vert_n']} path vertices: max top above the collider right below {100 * m_['vert_excess_max']:.2f} cm (<= 1.2), "
           f"lowest skirt foot {100 * m_['skirt_min']:.1f} cm (<= -3); dense lattice {len(dn)} samples ({m_['area']:.0f} m2 at {h * 100:.2f} cm): "
           f"max {100 * mx_dense:.2f} cm above the ground (<= 1.5), median {100 * float(np.median(dn)) if len(dn) else 0:.2f} cm, "
           f"{100 * float((dn < -0.005).mean()) if len(dn) else 0:.0f}% of the path buried under the ground; border step max "
           f"{100 * float(bd.max()) if len(bd) else 0:.2f} cm; ball test {len(hid)} resting positions: max hidden "
           f"{100 * float(hid.max()) if len(hid) else 0:.1f}% (<= 10), > 5% at {int((hid > 0.05).sum())}, > 10% at {int((hid > 0.10).sum())}")
    return "PATH_UNDER_BALL", ok, det


def _first_front(bvh, o, d, maxd, skip=12):
    """First FRONT-facing hit of a ray (back faces are skipped like a single-sided Unity material): (location, normal, face index,
    distance) or None."""
    trav = 0.0
    for _ in range(skip):
        h = bvh.ray_cast(o, d, maxd - trav)
        if h[0] is None:
            return None
        if h[1].dot(d) < 0.0:
            return h[0], h[1], h[2], trav + h[3]
        o = h[0] + d * 1e-4
        trav += h[3] + 1e-4
    return None


def wall_cover_test(D, step=2.0, heights=(0.3, 1.5, 3.0, 4.5, 5.0, 5.4, 5.7), top_band=0.10, seed=5):
    """Rays at the TERRAIN wall from three viewpoints (aerial: 40 m out, 15 m up; low: 40 m out, 2 m up; oblique: 25 m out, 20 m
    along, 8 m up) aimed at wall points at `heights` and at PLAY_Z - top_band - 0.02 (just under the tolerated sliver). A ray
    counts as EXPOSED when the first FRONT-facing thing it hits (TERRAIN + ROCK_SKIN; back faces are culled like Unity's single-sided
    materials) is the TERRAIN wall within 0.3 m of the aimed point.
    Also counts the first hits on TERRAIN wall faces that carry an LK_ROUGH material (grass texture on a vertical face) at any
    height below PLAY_Z, and the sliver band (PLAY_Z - top_band .. PLAY_Z), reported apart (the user allows 3-10 cm).
    Returns dict."""
    pz = D.play_z
    terr_ob = _terrain_ob(D)
    sk = _bvh_of(_skin_objs())
    tb = _bvh_of([terr_ob])
    allb = _bvh_of([terr_ob] + _skin_objs())
    W, ls, lt, lv, mi, pn = _mesh_arrays(terr_ob)
    mats = [_mat_name(m_) for m_ in terr_ob.data.materials]
    heights = tuple(heights) + (pz - top_band - 0.02,)
    cnt = dict(total=0, exposed=0, rough_vert=0, by_z={}, by_view={"aerial": [0, 0], "low": [0, 0], "oblique": [0, 0]}, sliver_total=0,
               sliver_exposed=0, pts=[])
    for Pl, _ in terrain_top_loops(D):
        Pc, s, Lp, N, _ = _loop_columns(Pl, step)
        for i in range(len(Pc)):
            nrm = N[i]
            tg = np.array([-nrm[1], nrm[0]])
            for z in list(heights) + [pz - 0.04]:
                o = Vector((Pc[i, 0] + nrm[0] * 6.0, Pc[i, 1] + nrm[1] * 6.0, z))
                h = tb.ray_cast(o, Vector((-nrm[0], -nrm[1], 0.0)), 30.0)
                if h[0] is None or abs(h[1].z) > 0.5 and False:
                    continue
                Wp = h[0]
                sliver = z > pz - top_band
                for vn, off in (("aerial", (40, 0, 15)), ("low", (40, 0, 2.0)), ("oblique", (25, 20, 8))):
                    Vp = Vector((Wp.x + nrm[0] * off[0] + tg[0] * off[1], Wp.y + nrm[1] * off[0] + tg[1] * off[1], Wp.z + off[2]))
                    if P.signed_dist_m(D, np.array([Vp.x]), np.array([Vp.y]))[0] < 1.0:
                        continue
                    dv = Wp - Vp
                    dist = dv.length
                    hh = _first_front(allb, Vp, dv.normalized(), dist + 0.05)       # back faces are culled in Unity
                    if hh is None:
                        continue
                    ht = _first_front(tb, Vp, dv.normalized(), dist + 0.05)
                    is_wall = ht is not None and abs(ht[3] - hh[3]) < 1e-5 and (ht[0] - Wp).length < 0.3
                    if sliver:
                        cnt["sliver_total"] += 1
                        cnt["sliver_exposed"] += int(is_wall)
                        continue
                    cnt["total"] += 1
                    cnt["by_view"][vn][1] += 1
                    if is_wall:
                        cnt["exposed"] += 1
                        cnt["by_view"][vn][0] += 1
                        if len(cnt["pts"]) < 80:
                            cnt["pts"].append((vn, round(Wp.x, 1), round(Wp.y, 1), round(Wp.z, 2), round(float(s[i]), 1)))
                        zb = "z<1" if Wp.z < 1 else ("1-3" if Wp.z < 3 else ("3-5" if Wp.z < 5 else ">=5"))
                        cnt["by_z"][zb] = cnt["by_z"].get(zb, 0) + 1
                        if mats[min(int(mi[h[2]]), len(mats) - 1)] == "LK_ROUGH":
                            cnt["rough_vert"] += 1
    return cnt


def wall_cover_gate(D, step=2.0, strict=True):
    """GATE CRATER_WALL_COVERED. Exposed-wall rays <= 1 % in total, none at wall z >= 5 m below the tolerated sliver
    (PLAY_Z - 0.10 .. PLAY_Z), no first hit on a vertical LK_ROUGH (grass) wall face, no up-facing skin face above PLAY_Z - 0.05.
    strict=False only reports (strata holes: a short grass fringe above the cap ledge is wanted there)."""
    c = wall_cover_test(D, step)
    share = c["exposed"] / max(c["total"], 1)
    hi = c["by_z"].get(">=5", 0)
    sk_up = 0
    for o in _skin_objs():
        Wv, ls, lt, lv, mi, pn = _mesh_arrays(o)
        pol = _poly_of_loop(ls, lt, len(lv))
        cen = np.zeros((len(ls), 3))
        np.add.at(cen, pol, Wv[lv])
        cen /= lt[:, None]
        sk_up += int(((pn[:, 2] > 0.3) & (cen[:, 2] > D.play_z - 0.05)).sum())
    rough_share = c["rough_vert"] / max(c["total"], 1)
    ok = share <= 0.01 and hi == 0 and rough_share <= 0.01 and sk_up == 0
    det = (f"{c['total']} rays (aerial/low/oblique) at wall points z 0.3 .. {D.play_z - 0.12:.2f} m: smooth TERRAIN wall seen first "
           f"{c['exposed']} = {100 * share:.2f}% (<= 1), at z >= 5 m: {hi} (must be 0), first hit on a vertical LK_ROUGH wall face "
           f"{c['rough_vert']} ({100 * rough_share:.2f}%, <= 1); by view { {k: f'{v[0]}/{v[1]}' for k, v in c['by_view'].items()} }; by wall height "
           f"{c['by_z']}; tolerated sliver PLAY_Z - 0.10 .. PLAY_Z: {c['sliver_exposed']}/{c['sliver_total']} rays reach bare wall "
           f"(LK_BASALT there on Crater); up-facing skin faces above PLAY_Z - 0.05: {sk_up}")
    return "CRATER_WALL_COVERED", (ok if strict else True), det + ("" if strict else " (report only: strata holes keep a grass fringe above the cap ledge)")


def skin_over_water(D, cell=0.5, depth=0.15):
    """Up-facing ROCK_SKIN faces inside a WATER hazard ellipse deeper than `depth`: (a) top-down 0.5 m grid inside every ellipse,
    first hit a skin face with normal z > 0.1; (b) faces with nz > 0.3 whose centre / any vertex is deeper than `depth`.
    Returns dict."""
    ells = _water_ellipses(D)
    skins = _skin_objs()
    out = dict(cells=0, cell_area=0.0, worst=0.0, face_centre=0, face_vertex=0, max_z=-1e9, n_ell=len(ells), n_cells=0, pts=[])
    if not ells or not skins:
        return out
    sb = _bvh_of(skins)
    d = Vector((0, 0, -1))
    for cx, cy, a, b, _ in ells:
        xs = np.arange(cx - a, cx + a + cell, cell)
        ys = np.arange(cy - b, cy + b + cell, cell)
        X, Y = np.meshgrid(xs, ys, indexing='ij')
        dep = _ell_depth(ells, X, Y)
        for i, j in np.argwhere(dep > depth).tolist():
            out["n_cells"] += 1
            hit = sb.ray_cast(Vector((X[i, j], Y[i, j], D.play_z + 2.0)), d, 30.0)
            if hit[0] is not None and hit[1].z > 0.1:
                out["cells"] += 1
                out["cell_area"] += cell * cell
                out["worst"] = max(out["worst"], float(dep[i, j]))
                out["max_z"] = max(out["max_z"], hit[0].z)
                if len(out["pts"]) < 40:
                    out["pts"].append((round(float(X[i, j]), 1), round(float(Y[i, j]), 1), round(float(hit[0].z), 2), round(float(dep[i, j]), 2)))
    for o in skins:
        Wv, ls, lt, lv, mi, pn = _mesh_arrays(o)
        pol = _poly_of_loop(ls, lt, len(lv))
        cen = np.zeros((len(ls), 3))
        np.add.at(cen, pol, Wv[lv])
        cen /= lt[:, None]
        up = pn[:, 2] > 0.3
        dv = _ell_depth(ells, Wv[:, 0], Wv[:, 1])
        dc = _ell_depth(ells, cen[:, 0], cen[:, 1])
        mxv = np.full(len(ls), -1e9)
        np.maximum.at(mxv, pol, dv[lv])
        out["face_centre"] += int((up & (dc > depth)).sum())
        out["face_vertex"] += int((up & (mxv > depth)).sum())
    return out


def skin_over_water_gate(D, cell=0.5, depth=0.15):
    c = skin_over_water(D, cell, depth)
    if not c["n_ell"]:
        return "SKIN_NOT_OVER_WATER", True, "no scoring water ellipse on this hole"
    if not _skin_objs():
        return "SKIN_NOT_OVER_WATER", True, "no ROCK_SKIN on this hole"
    ok = c["cells"] == 0 and c["face_centre"] == 0 and c["face_vertex"] == 0
    return "SKIN_NOT_OVER_WATER", ok, (f"{c['n_ell']} water ellipses, {c['n_cells']} grid cells ({cell} m) deeper than {depth} m inside them: "
                                       f"{c['cells']} are covered by an up-facing skin surface ({c['cell_area']:.1f} m2, worst depth {c['worst']:.2f} m, "
                                       f"top z {c['max_z'] if c['cells'] else 0:.2f}); up-facing (nz > 0.3) skin faces deeper than {depth} m: "
                                       f"{c['face_centre']} by centre, {c['face_vertex']} by any vertex")


def skin_facing(D, step=3.0, heights=(1.0, 3.0, 5.0), reach=20.0):
    """Are the ROCK_SKIN faces FRONT-facing toward the sea (Unity draws single-sided)? Horizontal rays from `reach` m out at several
    heights toward every shore column; share of FIRST hits whose face normal opposes the ray. Returns (front, back, share)."""
    sk = _bvh_of(_skin_objs())
    front = back = 0
    if sk is None:
        return 0, 0, 1.0
    for Pl, _ in terrain_top_loops(D):
        Pc, s, Lp, N, _ = _loop_columns(Pl, step)
        org = Pc + N * reach
        free = P.signed_dist_m(D, org[:, 0], org[:, 1]) > 0.5
        for i in range(len(Pc)):
            if not free[i]:
                continue
            for z in heights:
                d = Vector((-N[i, 0], -N[i, 1], 0.0))
                h = sk.ray_cast(Vector((org[i, 0], org[i, 1], z)), d, reach + 6.0)
                if h[0] is None:
                    continue
                if h[1].dot(d) < 0:
                    front += 1
                else:
                    back += 1
    return front, back, front / max(front + back, 1)


def skin_facing_gate(D):
    f, b, sh = skin_facing(D)
    return "SKIN_FACES_OUTWARD", (f + b) > 0 and sh >= 0.99, (f"{f + b} rays from 20 m out at z 1, 3, 5 toward the shore: first hit on a ROCK_SKIN face is FRONT-facing "
                                                           f"{f} times, back-facing {b} ({100 * sh:.1f}% front, need >= 99%; the materials are single-sided in Unity)")


def skin_tortuosity(D):
    """Plan tortuosity of the strata skin the reviewer's way: mid-row polyline length / its 9-column moving average (1.000 =
    smooth). 13 rows per column (cap + 4 bands x 3). Returns (mean tortuosity, std of signed distance along the shore, share of
    adjacent-column steps > 0.2 m) or None for non-strata skins."""
    NR = 13
    rough, S_all = [], []
    for o in _skin_objs():
        Wv = _mesh_arrays(o)[0]
        if len(Wv) % NR:
            return None
        Wr = Wv.reshape(-1, NR, 3)
        Wm = Wr[:, 6, :2]
        L_raw = np.linalg.norm(np.diff(Wm, axis=0), axis=1).sum()
        k = 9
        sm = np.stack([np.convolve(Wm[:, c], np.ones(k) / k, mode="valid") for c in range(2)], 1)
        L_sm = np.linalg.norm(np.diff(sm, axis=0), axis=1).sum() * len(Wm) / max(len(sm), 1)
        rough.append(L_raw / max(L_sm, 1e-6))
    return float(np.mean(rough)) if rough else None


def strata_gate(D, control=True):
    """STRATA_JOINTS_STAGGERED (review 2026-10-04, hole 9 far cliff: 'tan vertical joint lines at near-even spacing cross every band, like masonry
    coursework'). Measured on the live build's own numbers (D.strata_stats: every fissure block length, and the along-shore offset step across every
    fissure in every band): block lengths have a coefficient of variation >= 0.40 (uniform 3-9 m = 0.29), and at most 25 % of the fissures are a
    step of >= 6 cm in EVERY band (a line straight through the whole height). NEGATIVE CONTROL: the same loops rebuilt with STRATA_TUNING_PREV (the
    previous layout) must show the grid (>= 35 % full-height lines or CV < 0.40); the control's objects are deleted again."""
    st = [e for e in D.__dict__.get("strata_stats", []) if e["tuning"] == "live"]
    if not st:
        return ("STRATA_JOINTS_STAGGERED", True, "no strata skin on this hole (basalt / no skin)")

    def metrics(entries):
        lens = np.concatenate([np.asarray(e["lengths"], float) for e in entries])
        steps = [r for e in entries for r in e["steps"]]
        cv = float(lens.std() / max(lens.mean(), 1e-9))
        full = float(np.mean([all(v > 0.06 for v in r) for r in steps])) if steps else 0.0
        opn = float(np.mean([v > 0.06 for r in steps for v in r])) if steps else 0.0
        return cv, full, opn, len(steps), len(lens)
    cv, full, opn, nj, nb_ = metrics(st)
    ok = cv >= 0.40 and full <= 0.25
    ctl = "control skipped"
    if control and D.__dict__.get("strata_kwargs"):
        kw = dict(D.__dict__["strata_kwargs"])
        kw.update(name="ROCK_SKINCTL", replace=False, strata_tuning=STRATA_TUNING_PREV)
        before = len(D.__dict__.get("strata_stats", []))
        made = build_cliff_skin(D, **kw)
        ce = D.__dict__["strata_stats"][before:]
        for o in made:
            D.objects.pop(o.name, None)
            P.remove_object(o.name)
        del D.__dict__["strata_stats"][before:]
        if "look_objects" in D.__dict__:
            D.look_objects[:] = [n for n in D.look_objects if not n.startswith("ROCK_SKINCTL")]
        if ce:
            ccv, cfull, copn, cnj, _n = metrics(ce)
            cok = ccv < 0.40 or cfull >= 0.35
            ok = ok and cok
            ctl = f"NEGATIVE CONTROL (previous layout, {cnj} fissures): block CV {ccv:.2f}, full-height lines {cfull * 100:.0f} % -> {'regular grid detected' if cok else 'BLIND'}"
    return ("STRATA_JOINTS_STAGGERED", ok,
            f"{nb_} fissure blocks (mean {np.concatenate([np.asarray(e['lengths'], float) for e in st]).mean():.1f} m, CV {cv:.2f}, need >= 0.40), {nj} fissures: "
            f"{full * 100:.0f} % are a >= 6 cm step in every band (need <= 25 %), {opn * 100:.0f} % of the (fissure, band) cells are open; {ctl}")


def v2_gates(D, pockets=None, strata=True):
    """[(id, ok, detail)] for the 2026-10-04 pass: SHELF_THIN + NO_FOAM_STRIP (sea holes), PATH_UNDER_BALL, CRATER_WALL_COVERED
    (strict on basalt holes, report only on strata holes), SKIN_NOT_OVER_WATER."""
    res = []
    if bpy.data.objects.get("WATER_SHELF") is not None or bpy.data.objects.get("WATER_OCEAN") is not None and not bpy.data.objects.get("WATER_LAVA"):
        res.append(shelf_gate(D, pockets=pockets))
        res.append(surf_gate(D))
    else:
        res.append(("SHELF_THIN", True, "no sea shelf on this hole (lava hole)"))
        res.append(surf_gate(D))
    res.append(path_gate(D))
    res.append(wall_cover_gate(D, strict=bool(bpy.data.objects.get("WATER_LAVA") is not None)))
    res.append(skin_over_water_gate(D))
    res.append(skin_facing_gate(D))
    if strata and D.__dict__.get("strata_stats"):
        res.append(strata_gate(D))
    return res


def build_postcard_cliff(D, style="needle", loops=None, seed=0, replace=False):
    """Goal15 cliff cladding from any existing terrain play-edge loop.

    needle = thin uneven bedding and overhangs; split = coarser eroded rock
    and scrub ledges; crater = world-sized columns with chipped flat tops.
    Frozen terrain/collider vertices are never modified. Every outline sample
    is at most1.25m apart, and small independent outward relief changes break
    long straight silhouette runs. Returns the visual ROCK_SKIN chunks.
    """
    if style not in ("needle", "split", "crater"):
        raise ValueError("style must be needle, split or crater")
    if style != "crater":
        tuning=dict(STRATA_TUNING,goal15=True,outline_block_min=1.1,outline_block_max=2.8,
                    outline_relief_m=.42,overhang_m=.7,undercut_m=.55)
        made=build_cliff_skin(D, style="strata", loops=loops, seed=seed,
                                step_m=1.1 if style == "needle" else 1.25,
                                bands=5 if style == "needle" else 4,
                                disp_m=(.22,1.05) if style == "needle" else (.3,1.5),
                                jag_m=2.3, max_out_m=2.6, piece_m=25,
                                material="LK_CLIFF", material_low="LK_CLIFF_DARK",
                                name="ROCK_SKIN", replace=replace,strata_tuning=tuning)
        import postcard_props_lib as PL
        vines=PL.hang_vines_on_cliff(D,spacing_m=7.0,seed=seed,skip=.45)
        PL.attach_vines(D,vines=vines,targets=made)
        D.__dict__['postcard_cliff_vines']=vines
        return made
    import postcard_props_lib as PL
    if replace:
        for o in list(bpy.data.objects):
            if o.name.startswith("ROCK_SKIN_V3"):
                D.objects.pop(o.name, None)
                P.remove_object(o.name)
    all_loops = terrain_top_loops(D)
    sel = list(range(len(all_loops))) if loops is None else list(loops)
    rnd = random.Random(D.number *1009 + seed)
    bvh = _terrain_bvh(D)
    mat = look_material("LK_BASALT",D)
    wall_to_material(D,"LK_BASALT")
    out=[]
    for li in sel:
        Pl=all_loops[li][0]
        # Dense, dark backing follows the frozen wall through the narrow cracks.
        out += _basalt_backing(D,Pl,li,bvh,rnd,-1.6,mat,8,70,"ROCK_SKIN_V3_BACK")
        Pc,s,Lp,N,_=_loop_columns(Pl,1.1)
        cols=PL.build_column_wall(D,Pc,D.play_z-.10,-1.6,closed=True,
                                   width_m=(.8,1.5),rows=1,seed=seed+li,
                                   name=f"ROCK_SKIN_V3_{li:02d}",relief_m=.24)
        for ob in cols:
            D.objects[ob.name]=ob
            _track(D,ob)
        out += cols
    return out


# P7 paired charts are opt-in on genuinely world-sized new column geometry.
# These authored intervals retain every frozen hole10 GLOW_U and ZONES entry.
BASALT_PAIRED_GLOW_U = (0.123, 0.446, 0.534, 0.718, 0.806, 0.930)
BASALT_PAIRED_ZONES = ((.009, .061), (.165, .385), (.576, .685), (.772, .796), (.854, .893))
_BASALT_CHART_IMAGES = {}


def _basalt_chart_image(suffix):
    """Decoded PNG bytes; Blender colour-management is not measurement truth."""
    import hashlib
    import golf_look_textures as T
    path = os.path.join(LOOK_DIR, 'Basalt_' + suffix + '.png')
    with open(path, 'rb') as f:
        digest = hashlib.sha256(f.read()).hexdigest()
    key = (path, digest)
    if key not in _BASALT_CHART_IMAGES:
        _BASALT_CHART_IMAGES[key] = T.read_png(path)[..., :3].astype(float) / 255.0
    return _BASALT_CHART_IMAGES[key], digest


def _basalt_chart_normal(pts):
    a, b = pts, np.roll(pts, -1, axis=0)
    n = np.array([np.sum((a[:, 1] - b[:, 1]) * (a[:, 2] + b[:, 2])),
                  np.sum((a[:, 2] - b[:, 2]) * (a[:, 0] + b[:, 0])),
                  np.sum((a[:, 0] - b[:, 0]) * (a[:, 1] + b[:, 1]))])
    return n / max(float(np.linalg.norm(n)), 1e-12)


def _basalt_plain_charts(ob, seed, uf, vf):
    """Metre charts in existing plain windows, including every actual cap/chip."""
    rng = random.Random(seed)
    me = ob.data
    uv = me.uv_layers.active or me.uv_layers.new(name='UVMap')
    C, _ = _basalt_chart_image('C')
    profile = C.mean(axis=(0, 2))
    M = np.asarray(ob.matrix_world, float)
    co = np.array([tuple(v.co) for v in me.vertices])
    co = co @ M[:3, :3].T + M[:3, 3]
    for poly in me.polygons:
        pts = co[list(poly.vertices)]
        n = _basalt_chart_normal(pts)
        if abs(n[2]) >= .8:
            su = pts[:, 0]
            dv = min(.60 / 8, 3 * uf / 8)
            v = pts[:, 1] * dv
        else:
            h = n[:2] / max(float(np.linalg.norm(n[:2])), 1e-12)
            su = pts[:, :2] @ np.array([-h[1], h[0]])
            v = pts[:, 2] * vf / 8
        width = max(float(np.ptp(su)), .05)
        du = uf / 8
        viable = [(a, b) for a, b in BASALT_PAIRED_ZONES if b - a >= width * du * 1.03]
        if not viable:
            a, b = max(BASALT_PAIRED_ZONES, key=lambda z: z[1] - z[0])
            du = .96 * (b - a) / width
            viable = [(a, b)]
        if abs(n[2]) >= .8:
            v = pts[:, 1] * min(.60 / 8, 3 * du)
        choices = []
        for _ in range(32):
            a, b = rng.choice(viable)
            u0 = rng.uniform(a, b - width * du)
            sample = u0 + width * du * (np.arange(9) + .5) / 9
            mean = float(profile[np.floor((sample % 1) * len(profile)).astype(int)].mean())
            choices.append((u0, (.14 / max(mean, .025)) ** 3))
        at = rng.random() * sum(w for _, w in choices)
        u0 = choices[-1][0]
        for value, weight in choices:
            at -= weight
            if at <= 0:
                u0 = value
                break
        phase = rng.random()
        for j, li in enumerate(poly.loop_indices):
            uv.data[li].uv = (u0 + (su[j] - su.min()) * du, v[j] + phase)
    me.update()


def author_basalt_paired_columns(ob, seed=0, uf=.38, vf=.32,
                                 hot_fraction=.80, front_dir=(0., -1.), plain_author=None):
    """UV0 only: one chart across two actual neighbouring shaft planes.

    Existing column vertices/faces/materials/scoring are never changed. The
    complete column metadata bounds the real pieces; shared polygon edges
    prove adjacency. Each fractured piece uses the column's same world-z
    phase. A deterministic whole-column subset is hot; all other faces/caps
    retain plain charts. Non-fitting narrow components stay physically plain
    and are reported; the whole-hole original gates still decide PASS/FAIL.
    plain_author may call hole10's unchanged author_uv with p_glow=0, retaining
    its exact existing weighted albedo-window selection.
    """
    import json
    if not (0 <= hot_fraction <= 1) or uf <= 0 or vf <= 0:
        raise ValueError('Invalid paired UV density/frequency')
    specs = json.loads(ob['lk_v3_columns'])
    me = ob.data
    if plain_author is None:
        _basalt_plain_charts(ob, seed, uf, vf)
    else:
        plain_author(me, random.Random(seed), uf=uf, vf=vf, p_glow=0.)
    uv = me.uv_layers.active
    M = np.asarray(ob.matrix_world, float)
    co = np.array([tuple(v.co) for v in me.vertices])
    co = co @ M[:3, :3].T + M[:3, 3]
    normals = [_basalt_chart_normal(co[list(p.vertices)]) for p in me.polygons]
    front = np.asarray(front_dir, float)
    front /= max(float(np.linalg.norm(front)), 1e-12)
    selected = set(random.Random(seed + 3527).sample(range(len(specs)), int(len(specs) * hot_fraction)))
    phases = random.Random(seed + 941)
    centers = (.113, .435, .532, .728, .825, .946)
    records, unsupported = [], []
    for ci, spec in enumerate(specs):
        phase = phases.random()
        if ci not in selected:
            continue
        a, b = spec['faces']
        if not (0 <= a < b <= len(me.polygons)):
            raise ValueError('Column polygon metadata is outside actual geometry')
        parent = {int(v) for pi in range(a, b) for v in me.polygons[pi].vertices}
        parent = {v: v for v in parent}
        def component(v):
            while parent[v] != v:
                parent[v] = parent[parent[v]]
                v = parent[v]
            return v
        for pi in range(a, b):
            vs = list(me.polygons[pi].vertices)
            for v in vs[1:]:
                parent[component(v)] = component(vs[0])
        edges = {}
        for pi in range(a, b):
            if abs(normals[pi][2]) >= .05:
                continue
            vs = list(me.polygons[pi].vertices)
            for v0, v1 in zip(vs, vs[1:] + vs[:1]):
                if abs(co[v0, 2] - co[v1, 2]) > .4:
                    edges.setdefault(tuple(sorted((v0, v1))), []).append(pi)
        pairs = []
        for edge, polys in edges.items():
            if len(polys) != 2:
                continue
            lo, hi = sorted(edge, key=lambda i: co[i, 2])
            bottom, top = co[lo], co[hi]
            faces = []
            for pi in polys:
                pts = co[list(me.polygons[pi].vertices)]
                t = np.array([-normals[pi][1], normals[pi][0]])
                t /= max(float(np.linalg.norm(t)), 1e-12)
                along = (pts[:, 2] - bottom[2]) / (top[2] - bottom[2])
                corner = bottom[:2] + along[:, None] * (top[:2] - bottom[:2])
                s_ = np.sum((pts[:, :2] - corner) * t, axis=1)
                faces.append((pi, s_))
            left = max(-float(s_.min()) for _, s_ in faces) * uf / 8
            right = max(float(s_.max()) for _, s_ in faces) * uf / 8
            if left < 1e-6 or right < 1e-6:
                continue
            eligible = [j for j, g in enumerate(BASALT_PAIRED_GLOW_U)
                        if g-left <= centers[j]-(.022 if j == 4 else .028)-.003
                        and g+right >= centers[j]+(.022 if j == 4 else .028)+.003]
            if eligible:
                pairs.append((float(((bottom + top)[:2] * .5) @ front), edge, faces, eligible))
        # A connected shaft segment has one maximum-facing real corner.
        # Different fracture pieces have disjoint vertices and remain intact.
        groups = set()
        for candidate in sorted(pairs, reverse=True, key=lambda q: q[0]):
            root = component(candidate[1][0])
            if root in groups:
                continue
            groups.add(root)
            _, edge, faces, eligible = candidate
            gi = eligible[ci % len(eligible)]
            for pi, s_ in faces:
                poly = me.polygons[pi]
                for j, li in enumerate(poly.loop_indices):
                    uv.data[li].uv = (BASALT_PAIRED_GLOW_U[gi] + s_[j] * uf / 8,
                                      phase + co[me.loops[li].vertex_index, 2] * vf / 8)
            records.append(dict(column=ci, polygons=[p for p, _ in faces], shared_edge=list(edge),
                                glow_index=gi, world_z_phase=phase))
        if not groups:
            unsupported.append(ci)
    me.update()
    E, e_sha = _basalt_chart_image('E')
    G = np.flipud(E.max(axis=2) > .2)
    rng = np.random.default_rng(1010)
    rnd = rng.random((96, 2)); q = np.sqrt(rnd[:, 0])
    bary = np.c_[1-q, q*(1-rnd[:, 1]), q*rnd[:, 1]]
    st = dict(side=0, glow=0, top=0, top_glow_free=0)
    for pi, poly in enumerate(me.polygons):
        chart = np.array([tuple(uv.data[li].uv) for li in poly.loop_indices])
        hot = total = 0
        for j in range(1, len(chart)-1):
            samples = bary @ chart[[0, j, j+1]]
            xy = np.floor((samples % 1) * np.array([G.shape[1], G.shape[0]])).astype(int)
            hot += int(G[xy[:, 1], xy[:, 0]].sum()); total += len(samples)
        share = hot / max(total, 1)
        if abs(normals[pi][2]) >= .8:
            st['top'] += 1; st['top_glow_free'] += int(share <= 2e-4)
        else:
            st['side'] += 1; st['glow'] += int(share > 0)
    st.update(paired_columns=len({r['column'] for r in records}), paired_components=len(records),
              selected_columns=len(selected), plain_unsupported_columns=unsupported,
              hot_fraction=hot_fraction, uf=uf, vf=vf, emission_sha256=e_sha,
              glow_lines_used=sorted({r['glow_index'] for r in records}), records=records)
    return st
