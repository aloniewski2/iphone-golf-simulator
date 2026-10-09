"""postcard_lib: the bpy library every postcard hole build script (hole08/09/10_build.py) uses.

Generalised COPY of hole07_lib.py (same helper names: chaikin, resample, catmull_rom, point_in_poly, dist_to_poly,
signed_area, ellipse, blob, fill_polygon, build_mesh, surface_object, get_material, rgb, assign, link_to, new_mesh_object,
remove_object, get_collection, get_root, render_camera ...) plus terrain-with-holes, scoring-exact play surfaces, water
bands, rock import, arch/ruin/scatter, gameplay markers, cameras, render, save, FBX export, stats.
Nothing here ever writes hole_07.*; every path is derived from __file__ (no hard coded user directories), and
save/export_fbx/render_overview REFUSE to write anything named hole_07* or for design NUMBER 7.

A build script is a short readable sequence (run with
`/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python blender/scripts/hole08_build.py`):

    import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # blender/scripts
    import postcard_lib as P
    D = P.start("hole08_design")                 # empties the scene, loads the PURE design, collections + materials
    P.build_terrain(D)                           # flat top at PLAY_Z, water ellipses cut out, banded undercut cliffs
    P.build_play_surfaces(D)                     # fairway, first cut, green, apron, tee box, bunkers (+lips)
    P.build_water(D)                             # ocean plane + shallow/foam bands on every boundary loop
    P.import_rocks(D)                            # ROCK_* / CLIFF_ROCK library from hole_07.blend (read only)
    P.build_arch(D, P.m(30, 270), 0.0)           # visual-only ruin; also build_ruin_wall, scatter_rocks
    P.scatter_rocks(D, count=14, zone="sea")
    P.build_gameplay(D); P.build_cameras_and_light(D)
    P.render_overview(D); P.save(D); P.export_fbx(D); print(P.stats(D))
    # P.build_base(D, water=None, terrain=None) runs everything from build_terrain to build_cameras_and_light except
    # the rock extras (arch/ruin/scatter), which a hole adds itself BEFORE render_overview.

COORDINATES. Design data is COURSE YARDS (x right of the tee line, d down the hole). Blender is METRES with the tee
marker at (0, 0): blender_xy = course_yards * 0.9144 (YD). +Y = down the hole, +X = right, water level z = 0, the one
play height is D.play_z (design PLAY_Z). m(x_yd, d_yd) converts a point.

API (exact signatures; * = beyond the required list)
    YD = 0.9144
    start(design_module_name, out_dir=None) -> D       name like 'hole08_design', a path to a .py, or a module object
    m(x_yd, d_yd) -> (x_m, y_m)
    build_terrain(D, undercut_m=3.0, interior_spacing=6.5, ellipse_edge_m=2.4, name=None) -> obj   (TERRAIN_<NAME>; D.loops)
    build_play_surfaces(D, cell=0.5, fairway_spacing=3.0) -> {name: obj}
    build_water(D, ocean_name='WATER_OCEAN', ocean_material='MAT_WATER', shallow_material='MAT_WATER_SHALLOW',
                foam_material='MAT_FOAM', crest=True, shallow_m=16.0, foam_m=3.4, inner_m=5.0, cell=1.0,
                min_piece_m2=10.0) -> {name: obj}
    import_rocks(D) -> D.rock_lib
    place_rock(D, kind, loc_m, scale=(1, 1, 1), rot=None) -> obj
    build_arch(D, center_m, facing_deg, span_m=9.0, height_m=9.0, broken=True) -> [objs]
    build_ruin_wall(D, p0_m, p1_m, height_m=3.5, clip_to_land=True) -> [objs]
    scatter_rocks(D, count=20, seed=0, zone='sea', dist=None, kinds=None, scale=None, z=None, min_gap=10.0,
                  allow=('OutOfBounds',), avoid=(), companion=0.4, tries=6000) -> [objs]
    build_gameplay(D)
    build_cameras_and_light(D, overview_rot_deg=(50, 0, 20), margin=1.10)
    render_overview(D, path=None, res=(1400, 1400), samples=16) -> path
    save(D) -> path ; export_fbx(D, path=None) -> path ; stats(D) -> dict
  * build_base(D, water=None, terrain=None, rocks=True) -> D
  * lie_codes_m(D, X_m, Y_m) -> int8 array (LIE_WATER, LIE_BUNKER, LIE_GREEN, LIE_TEE, LIE_FAIRWAY, LIE_ROUGH, LIE_OOB; names in
    LIE_NAMES): vectorised numpy mirror of Hole.LieAt (verified equal to postcard_check.Hole.lie_at in the smoke test)
  * signed_dist_m(D, X_m, Y_m): metres to the terrain boundary loops, + on the water side, - on land (needs build_terrain)
  * sha256_file(path); render_camera(cam_name, out_path, res, samples)
D (the handle start() returns; unknown attributes fall through to the design module: D.CENTERLINE, D.SHORE, D.HAZARDS,
D.FAIRWAY_WIDTH, D.SCENERY ...): mod, hole (postcard_check.Hole), number, name, tag ('08'), play_z, out_dir, root_name,
terrain_name, scenery (dict copy), mats {name: Material}, loops [[(x_m, y_m), ...], ...] (ordered, LAND ON THE LEFT of the travel
direction), loop_kinds ('outer' CCW | 'hole' CW), objects {name: obj}, rock_lib, rock_bbox, rng (seeded by the hole number:
rebuilding gives identical rocks), timings.

SCORING <-> MESH. Every play surface mesh is generated from lie_codes_m, so a region cannot disagree with the scoring code:
fairway = lie in {Fairway, Tee}; green = lie Green; first cut = fairway/tee/rough within SCENERY['firstcut_yd'] (3.0) of the
fairway edge; apron = land within GREEN_RADIUS + SCENERY['apron_yd'] (3.0) of the pin that is rough/out-of-bounds/green (the
fairway part is left to the FAIRWAY mesh, same colour); tee box = rounded rectangle SCENERY['tee_box_m'] (9.0, 7.0) m about the tee
marker along the first leg; sand = the exact bunker ellipse (clipped by the scoring lie only if it dips into water or past the
shore). Region boundaries are traced with marching squares on a 0.5 m grid with every boundary vertex bisected onto the true
boundary, simplified at 2 cm, then filled with a constrained Delaunay triangulation (few triangles). Fairway mowing stripes
alternate MAT_FAIRWAY / MAT_FAIRWAY_STRIPE every SCENERY['stripe_m'] (18.0) metres along the centerline, cut on exact lines.
The bunker lip is hole 7's raised rim (1.0 / 1.10 / 1.22 x the ellipse at +0.32 / +0.57 / +0.22 m); it is pulled in or
broken where it would reach water or the shore, so on a fairway/rough cell next to a bunker the TOP mesh can be the lip.
Other SCENERY keys read here: lava (truthy -> MAT_LAVA is created).

FLAT RULE. TERRAIN top faces are at exactly D.play_z; fairway +0.14, first cut +0.08, green +0.26, apron +0.18, tee box +0.24,
sand +0.30, bunker lip +0.22..+0.57 (all inside play_z .. play_z+0.6). No height function anywhere. Cliff walls are VERTICAL OR
UNDERCUT: every wall ring is inset (toward the land) relative to the ring above it by a per-vertex amount that never decreases
downward (and is capped at 40 percent of the local land width and by the corner sharpness, so thin strips and sharp tips stay
valid), rings sit at constant z, so every wall triangle has n.z <= 0 and nothing sticks out past the top outline in plan view.
The ellipse cut is a circumscribed polygon (each chord tangent to the true ellipse, at most ~4 cm outside it): no land triangle
lies inside a water ellipse. Walls are flat shaded (smooth lip normals tilt the top triangles beside the edge).

KNOWN LIMITS / FOR BUILD AGENTS. Water bands extend `inner_m` metres under the land (hidden by the undercut walls); for lava call
build_water(D, 'WATER_LAVA', 'MAT_LAVA', shallow_material=None, foam_material=None, crest=False). Raise undercut_m (6.0) for the
crater pillar. Keep arch/ruin/rocks on land yourself (build_arch warns when the centre is within 0.7 x span of water). Rock
instances share four mesh datablocks, so the stock FBX exporter prints a harmless 'material index' warning per extra instance:
export_fbx suppresses and counts them. Regions thinner than about two grid cells (1 m) are dropped by the marching squares.

BLENDER 5.2 NOTES (hit while writing this):
  * Material.use_nodes / World.use_nodes are deprecated (DeprecationWarning, removal in 6.0); node trees already exist on new
    materials/worlds, so get_material() only touches use_nodes when node_tree is None.
  * Engine id is 'BLENDER_EEVEE' (the 4.2-4.5 'BLENDER_EEVEE_NEXT' id is gone). EEVEE has no use_gtao / gtao_distance any more;
    ray tracing, fast GI and shadow settings exist. Every EEVEE attribute is set inside try/except (hasattr guard).
    view_settings.view_transform = 'Standard' still works; the 'look' enum only lists 'NONE' in this build.
  * Headless `Blender -b` renders EEVEE stills fine (about 1-5 s for an ortho still).
  * bpy.ops.export_scene.fbx is still the Python add-on (addons_core/io_scene_fbx): object_types, apply_unit_scale,
    apply_scale_options, axis_*, bake_anim, use_mesh_modifiers, add_leaf_bones, path_mode, use_selection exist as in 4.x; the
    result's GlobalSettings equal hole_07.fbx (UnitScaleFactor 100, same axes).
  * mathutils.geometry.delaunay_2d_cdt returns a 6-tuple; edge constraints that cross each other are split at the intersection
    (used for ellipses crossing the shore or each other); output_type 0 = full triangulation (the centroid test picks the region).
  * Mesh.polygons[i].use_smooth still exists (the 'sharp_face' attribute); material_index is CLAMPED to the number of slots when
    written, so build_mesh creates the material slots BEFORE it writes the per-face indices.
  * libraries.load(link=False) of objects also pulls their meshes and materials; hole_07.blend has no MAT_ROCK_DARK (unused
    there), so the palette version is used and the loaded MAT_ROCK is remapped onto the palette material.
  * NumPy 2.x warns on np.cross of 2-D vectors: the lib does its own 2-D cross products.

MAT_LAVA (only created when design SCENERY['lava'] is truthy): Base Color AND Emission Color are RAW sRGB floats (LAVA_RGB, not
linearised). Reason: Blender's FBX exporter writes the Base Color floats as they are and hole_07.fbx.meta has
useSRGBMaterialColor: 1, so Unity reads those floats as sRGB; a linearised value would come out dark brown in Unity. In a Blender
render the same floats are read as linear, so the lava looks a little lighter orange there. HoleView.Palette does not know
MAT_LAVA, so the imported colour is what Unity shows.
"""
import bpy
import bmesh
import math
import os
import sys
import time
import random
import hashlib
import importlib
import importlib.util
import uuid

import numpy as np
from mathutils import Vector, Matrix, geometry
from mathutils.kdtree import KDTree

# ----------------------------------------------------------------------------- paths (all derived from __file__)
try:
    HERE = os.path.dirname(os.path.abspath(__file__))
except NameError:  # exec() without __file__: assume the CWD is blender/scripts
    HERE = os.getcwd()
REPO = os.path.dirname(os.path.dirname(HERE))
BLENDER_DIR = os.path.join(REPO, "blender")
COURSE_DIR = os.path.join(REPO, "Unity", "Assets", "Resources", "Course")
HOLE07_BLEND = os.path.join(BLENDER_DIR, "hole_07.blend")
HOLE07_FBX = os.path.join(COURSE_DIR, "hole_07.fbx")
HOLE07_META = HOLE07_FBX + ".meta"
if HERE not in sys.path:
    sys.path.insert(0, HERE)

YD = 0.9144                                   # metres per yard
COLLECTIONS = ["COURSE", "ENVIRONMENT", "STRUCTURES", "GAMEPLAY", "LIGHTING", "REFERENCE"]
GROUND_PREFIXES = ("TERRAIN", "FAIRWAY", "GREEN", "TEE_BOX", "BUNKER", "CART_PATH")
ROCK_NAMES = ("ROCK_SMALL", "ROCK_MEDIUM", "ROCK_LARGE", "CLIFF_ROCK")
LAVA_RGB = (1.0, 0.34, 0.04)                  # RAW sRGB floats, see module docstring

# lie codes of lie_codes_m (numpy mirror of Hole.LieAt)
LIE_WATER, LIE_BUNKER, LIE_GREEN, LIE_TEE, LIE_FAIRWAY, LIE_ROUGH, LIE_OOB = range(7)
LIE_NAMES = ["Water", "Bunker", "Green", "Tee", "Fairway", "Rough", "OutOfBounds"]

# z offsets above D.play_z (hole 7 values)
Z_FAIRWAY, Z_FIRSTCUT, Z_GREEN, Z_APRON, Z_TEE, Z_SAND = 0.14, 0.08, 0.26, 0.18, 0.24, 0.30

_ST = {"D": None}


# ----------------------------------------------------------------------------- small utilities
def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def m(x_yd, d_yd):
    """Course yards (x right, d down the hole) -> Blender metres (tee marker at 0, 0)."""
    return (float(x_yd) * YD, float(d_yd) * YD)


def _safe_name(s):
    return "".join(c if c.isalnum() else "_" for c in str(s).upper())


def _log(D, msg):
    t = time.time() - D.t0
    print(f"[postcard_lib {t:6.1f}s] {msg}", flush=True)


# ----------------------------------------------------------------------------- the design handle
class Design:
    """What start() returns. Attribute access falls through to the PURE design module (D.CENTERLINE, D.SHORE,
    D.HAZARDS, D.FAIRWAY_WIDTH, D.SCENERY ...). Own attributes: mod, hole (postcard_check.Hole scoring mirror),
    number, name, tag ('08'), play_z, out_dir, root_name, mats, loops, loop_kinds, objects, rock_lib, rock_bbox,
    rng, timings, t0."""

    def __init__(self, mod, pc, out_dir):
        self.mod = mod
        self.pc = pc
        self.hole = pc.Hole(mod)
        self.number = int(mod.NUMBER)
        self.name = str(mod.NAME)
        self.tag = f"{self.number:02d}"
        self.play_z = float(mod.PLAY_Z)
        self.out_dir = out_dir
        self.root_name = f"HOLE_{self.tag}_ROOT"
        self.mats = {}
        self.loops = []
        self.loop_kinds = []
        self.objects = {}
        self.rock_lib = {}
        self.rock_bbox = {}
        self.rock_count = {}
        self.rng = random.Random(self.number * 7919 + 13)
        self.timings = {}
        self.t0 = time.time()
        self.terrain = None
        self._lie_cache = None
        self._distfield = None

    def __getattr__(self, key):
        if key in ("mod", "__setstate__", "__getstate__"):
            raise AttributeError(key)
        return getattr(self.mod, key)

    @property
    def scenery(self):
        return dict(getattr(self.mod, "SCENERY", {}) or {})

    @property
    def terrain_name(self):
        return f"TERRAIN_{_safe_name(self.name)}"


# ----------------------------------------------------------------------------- scene / collections (hole07_lib names)
def get_collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def link_to(obj, col_name):
    col = get_collection(col_name)
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col.objects.link(obj)


def get_root():
    D = _ST["D"]
    name = D.root_name if D else "HOLE_ROOT"
    root = bpy.data.objects.get(name)
    if root is None:
        root = bpy.data.objects.new(name, None)
        root.empty_display_type = 'PLAIN_AXES'
        root.empty_display_size = 20
        bpy.context.scene.collection.objects.link(root)
    return root


def remove_object(name):
    o = bpy.data.objects.get(name)
    if o:
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data and isinstance(data, bpy.types.Mesh) and data.users == 0:
            bpy.data.meshes.remove(data)


def new_mesh_object(name, col_name, parent=True):
    remove_object(name)
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    link_to(ob, col_name)
    if parent:
        ob.parent = get_root()
    return ob


# ----------------------------------------------------------------------------- colours / materials
def srgb_to_linear(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def rgb(r, g, b):
    return (srgb_to_linear(r), srgb_to_linear(g), srgb_to_linear(b), 1.0)


def get_material(name, color, roughness=0.9, metallic=0.0, emission=None, alpha=1.0):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    if mat.node_tree is None:                      # 5.x creates the tree already; older Blender needs use_nodes
        mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    if bsdf is None:
        for n in nt.nodes:
            if n.type == 'BSDF_PRINCIPLED':
                bsdf = n
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.2
    if emission is not None:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = emission[0]
            bsdf.inputs["Emission Strength"].default_value = emission[1]
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
    mat.diffuse_color = color
    return mat


def assign(ob, mat):
    ob.data.materials.clear()
    ob.data.materials.append(mat)


# phase11_13_look.py PALETTE (the colours HoleView.Palette uses): name -> ((sRGB), roughness)
PALETTE = {
    "MAT_FAIRWAY": ((118, 208, 56), 0.95), "MAT_FAIRWAY_STRIPE": ((100, 192, 48), 0.95),
    "MAT_FIRSTCUT": ((84, 176, 44), 0.95), "MAT_ROUGH": ((58, 148, 38), 0.95),
    "MAT_GREEN": ((156, 228, 72), 0.9), "MAT_BUNKER_LIP": ((166, 228, 90), 0.95),
    "MAT_SAND": ((240, 218, 160), 0.95), "MAT_WATER": ((16, 70, 170), 0.12),
    "MAT_WATER_SHALLOW": ((40, 146, 222), 0.12), "MAT_FOAM": ((226, 244, 252), 0.6),
    "MAT_CLIFF": ((118, 122, 130), 0.95), "MAT_CLIFF_DARK": ((84, 90, 100), 0.95),
    "MAT_ROCK": ((138, 140, 146), 0.95), "MAT_ROCK_DARK": ((98, 102, 110), 0.95),
    "MAT_TREE_DARK": ((32, 104, 54), 0.95), "MAT_TREE_MID": ((50, 140, 62), 0.95),
    "MAT_TREE_LIGHT": ((94, 178, 70), 0.95), "MAT_PATH": ((200, 202, 204), 0.95),
    "MAT_PATH_EDGE": ((152, 156, 158), 0.95), "MAT_WOOD": ((112, 74, 46), 0.9),
    "MAT_ROOF": ((104, 84, 74), 0.9), "MAT_WALL": ((224, 208, 178), 0.9),
    "MAT_GLASS": ((150, 205, 235), 0.2), "MAT_STONE": ((196, 188, 176), 0.95),
    "MAT_FLAG": ((232, 40, 40), 0.8), "MAT_POLE": ((240, 240, 240), 0.5),
    "MAT_CUP": ((28, 28, 28), 0.9), "MAT_BALL": ((250, 250, 250), 0.4),
}


def _make_materials(D):
    for name, (col, rough) in PALETTE.items():
        mat = get_material(name, rgb(*col), roughness=rough)
        bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.35 if "WATER" in name or name == "MAT_GLASS" else 0.15
        mat.use_backface_culling = False
        D.mats[name] = mat
    for wn, strength in (("MAT_WATER", 0.10), ("MAT_WATER_SHALLOW", 0.18)):      # faint emissive lift (phase 11)
        bsdf = next(n for n in D.mats[wn].node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = bsdf.inputs["Base Color"].default_value
            bsdf.inputs["Emission Strength"].default_value = strength
    if D.scenery.get("lava"):
        raw = (LAVA_RGB[0], LAVA_RGB[1], LAVA_RGB[2], 1.0)       # NOT linearised on purpose, see module docstring
        mat = get_material("MAT_LAVA", raw, roughness=0.55, emission=(raw, 1.6))
        mat.use_backface_culling = False
        D.mats["MAT_LAVA"] = mat


# ----------------------------------------------------------------------------- 2-D curve helpers (hole07_lib copies)
def chaikin(points, iterations=2, closed=True):
    pts = [Vector(p) for p in points]
    for _ in range(iterations):
        out = []
        n = len(pts)
        rng = range(n) if closed else range(n - 1)
        for i in rng:
            p, q = pts[i], pts[(i + 1) % n]
            out.append(p * 0.75 + q * 0.25)
            out.append(p * 0.25 + q * 0.75)
        if not closed:
            out = [pts[0]] + out + [pts[-1]]
        pts = out
    return pts


def resample(points, spacing, closed=True):
    pts = [Vector(p) for p in points]
    if closed:
        pts = pts + [pts[0]]
    lengths = [(pts[i + 1] - pts[i]).length for i in range(len(pts) - 1)]
    total = sum(lengths)
    count = max(4, int(round(total / spacing)))
    step = total / count
    out = [pts[0].copy()]
    seg, acc, target = 0, 0.0, step
    while len(out) < count:
        while seg < len(lengths) and acc + lengths[seg] < target:
            acc += lengths[seg]
            seg += 1
        if seg >= len(lengths):
            break
        t = (target - acc) / lengths[seg] if lengths[seg] > 0 else 0
        out.append(pts[seg].lerp(pts[seg + 1], t))
        target += step
    if not closed:
        out.append(pts[-1].copy())
    return out


def catmull_rom(points, samples_per_seg=8, closed=False):
    pts = [Vector(p) for p in points]
    out = []
    n = len(pts)
    if closed:
        idx = lambda i: pts[i % n]
        segs = n
    else:
        idx = lambda i: pts[max(0, min(n - 1, i))]
        segs = n - 1
    for i in range(segs):
        p0, p1, p2, p3 = idx(i - 1), idx(i), idx(i + 1), idx(i + 2)
        for s in range(samples_per_seg):
            t = s / samples_per_seg
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    if not closed:
        out.append(pts[-1].copy())
    return out


def point_in_poly(pt, poly):
    x, y = pt[0], pt[1]
    inside = False
    n = len(poly)
    j = n - 1
    for i in range(n):
        xi, yi = poly[i][0], poly[i][1]
        xj, yj = poly[j][0], poly[j][1]
        if ((yi > y) != (yj > y)) and (x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi):
            inside = not inside
        j = i
    return inside


def dist_to_poly(pt, poly):
    p = Vector((pt[0], pt[1]))
    best = 1e9
    n = len(poly)
    for i in range(n):
        a = Vector((poly[i][0], poly[i][1]))
        b = Vector((poly[(i + 1) % n][0], poly[(i + 1) % n][1]))
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / (ab.length_squared + 1e-9)))
        d = (a + ab * t - p).length
        if d < best:
            best = d
    return best


def signed_area(poly):
    a = 0.0
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i][0], poly[i][1]
        x2, y2 = poly[(i + 1) % n][0], poly[(i + 1) % n][1]
        a += x1 * y2 - x2 * y1
    return a * 0.5


def ellipse(cx, cy, rx, ry, rot=0.0, n=24, wobble=0.0, seed=0):
    rnd = random.Random(seed)
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        w = 1.0 + (rnd.uniform(-wobble, wobble) if wobble else 0.0)
        x, y = math.cos(t) * rx * w, math.sin(t) * ry * w
        xr = x * math.cos(rot) - y * math.sin(rot)
        yr = x * math.sin(rot) + y * math.cos(rot)
        pts.append(Vector((cx + xr, cy + yr)))
    return pts


def blob(cx, cy, rx, ry, rot=0.0, seed=1, n=10, wobble=0.18, smooth=2):
    """kidney/blob outline: coarse noisy ellipse smoothed by Chaikin."""
    return chaikin(ellipse(cx, cy, rx, ry, rot, n, wobble, seed), smooth)


def fill_polygon(outline, spacing, height_fn, z_offset=0.0, margin=None):
    """(verts3d, faces): CDT of outline with an interior grid, z from height_fn (hole07_lib copy)."""
    poly = [Vector((p[0], p[1])) for p in outline]
    if signed_area(poly) < 0:
        poly.reverse()
    if margin is None:
        margin = spacing * 0.55
    xs = [p.x for p in poly]
    ys = [p.y for p in poly]
    verts2 = list(poly)
    x = min(xs) + spacing * 0.5
    row = 0
    while x < max(xs):
        y = min(ys) + spacing * (0.5 if row % 2 == 0 else 0.0)
        while y < max(ys):
            p = Vector((x, y))
            if point_in_poly(p, poly) and dist_to_poly(p, poly) > margin:
                verts2.append(p)
            y += spacing
        x += spacing
        row += 1
    face = list(range(len(poly)))
    v_out, e_out, f_out, _, _, _ = geometry.delaunay_2d_cdt(verts2, [], [face], 1, 1e-5, False)
    verts3 = [(v.x, v.y, height_fn(v.x, v.y) + z_offset) for v in v_out]
    return verts3, [list(f) for f in f_out]


def build_mesh(ob, verts, faces, smooth=True, material_index=None, materials=None):
    """Fill ob.data. smooth: bool or per-face list. materials: list of Material (slots are created BEFORE the per-face
    material_index is written: Blender clamps indices to the slot count). material_index: per-face list or None."""
    me = ob.data
    me.clear_geometry()
    me.from_pydata(verts, [], faces)
    me.validate()
    me.update()
    if materials is not None:
        me.materials.clear()
        for mt in materials:
            me.materials.append(mt)
    n = len(me.polygons)
    if n:
        if isinstance(smooth, (list, tuple, np.ndarray)):
            me.polygons.foreach_set("use_smooth", [bool(s) for s in smooth])
        else:
            me.polygons.foreach_set("use_smooth", [bool(smooth)] * n)
        if material_index is not None:
            me.polygons.foreach_set("material_index", [int(i) for i in material_index])
    me.update()
    return me


def surface_object(name, col_name, outline, spacing, height_fn, z_offset, mat, smooth=True):
    ob = new_mesh_object(name, col_name)
    v, f = fill_polygon(outline, spacing, height_fn, z_offset)
    build_mesh(ob, v, f, smooth)
    assign(ob, mat)
    return ob


# ----------------------------------------------------------------------------- start / purge
def _purge_data():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for coll in (bpy.data.collections, bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights,
                 bpy.data.images, bpy.data.worlds, bpy.data.curves, bpy.data.node_groups, bpy.data.textures):
        for item in list(coll):
            try:
                coll.remove(item)
            except Exception:
                pass


def _load_design(name):
    if not isinstance(name, str):                       # a module object (tests build synthetic designs)
        return name
    if name.endswith(".py") or os.sep in name:
        path = name if os.path.isabs(name) else os.path.join(HERE, name)
        spec = importlib.util.spec_from_file_location(os.path.basename(path)[:-3], path)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        return mod
    mod = importlib.import_module(name)
    return importlib.reload(mod)


def start(design_module_name, out_dir=None):
    """Empty the scene, load the PURE design module (module name like 'hole08_design', a path to a .py, or a module object), import
    postcard_check.Hole as D.hole (scoring mirror), create collections COURSE/ENVIRONMENT/STRUCTURES/GAMEPLAY/LIGHTING/
    REFERENCE + hidden ASSET_LIBRARY, the root empty HOLE_NN_ROOT, metric units and every material of the contract
    (+ MAT_LAVA only when design SCENERY['lava'] is truthy). out_dir=None writes to the repo paths
    (blender/hole_NN.blend, blender/previews/hole_NN_overview.png, Unity/Assets/Resources/Course/hole_NN.fbx);
    an out_dir redirects EVERYTHING there (smoke tests)."""
    sys.dont_write_bytecode = True                      # never drop __pycache__ files next to the scripts
    pc = importlib.import_module("postcard_check")
    mod = _load_design(design_module_name)
    for need in ("NUMBER", "NAME", "PLAY_Z", "CENTERLINE", "SHORE", "HAZARDS", "FAIRWAY_WIDTH", "GREEN_RADIUS", "ROUGH_WIDTH"):
        if not hasattr(mod, need):
            raise ValueError(f"design module {design_module_name} has no {need}")
    D = Design(mod, pc, out_dir)
    _ST["D"] = D
    _purge_data()
    sc = bpy.context.scene
    sc.name = f"HOLE_{D.tag}"
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0
    for c in COLLECTIONS:
        get_collection(c)
    lib = get_collection("ASSET_LIBRARY")
    get_collection("ENVIRONMENT")
    get_root()
    try:
        lc = bpy.context.view_layer.layer_collection.children.get("ASSET_LIBRARY")
        if lc is not None:
            lc.exclude = True
        lib.hide_viewport = True
        lib.hide_render = True
    except Exception:
        pass
    _make_materials(D)
    _log(D, f"start: hole {D.number} {D.name}, play_z {D.play_z}, {len(D.mats)} materials")
    return D


# ----------------------------------------------------------------------------- numpy geometry
def _inside_np(poly, X, Y):
    """Even-odd inside test, same expression as Hole.Inside (vectorised over arrays, polygon in the same units)."""
    X = np.asarray(X, float)
    Y = np.asarray(Y, float)
    shape = X.shape
    X = X.ravel()
    Y = Y.ravel()
    inside = np.zeros(X.shape, bool)
    n = len(poly)
    j = n - 1
    with np.errstate(divide='ignore', invalid='ignore'):
        for i in range(n):
            ax, ay = poly[i]
            bx, by = poly[j]
            cond = (ay > Y) != (by > Y)
            if cond.any():
                xint = (bx - ax) * (Y - ay) / (by - ay) + ax
                inside ^= cond & (X < xint)
            j = i
    return inside.reshape(shape)


def _segments(loops):
    """loops (list of (k,2) arrays) -> A, B endpoint arrays (M,2) of all closed-loop edges."""
    As, Bs = [], []
    for L in loops:
        L = np.asarray(L, float)
        As.append(L)
        Bs.append(np.roll(L, -1, axis=0))
    return np.vstack(As), np.vstack(Bs)


def _evenodd(P, A, B, chunk=512):
    """Even-odd point-in-loops test, points P (N,2) against edge arrays A,B (M,2): odd crossing count = inside."""
    P = np.asarray(P, float)
    out = np.zeros(len(P), bool)
    if len(P) == 0 or len(A) == 0:
        return out
    ax, ay, bx, by = A[:, 0][None], A[:, 1][None], B[:, 0][None], B[:, 1][None]
    with np.errstate(divide='ignore', invalid='ignore'):
        for s in range(0, len(P), chunk):
            px = P[s:s + chunk, 0:1]
            py = P[s:s + chunk, 1:2]
            cond = (ay > py) != (by > py)
            xint = (bx - ax) * (py - ay) / (by - ay) + ax
            hits = cond & (px < xint)
            out[s:s + chunk] = (hits.sum(axis=1) & 1).astype(bool)
    return out


def _dist_pts_segs(P, A, B, chunk=256):
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


def _lie_all(D, Xm, Ym):
    """numpy mirror of Hole.LieAt on metre coordinates. Returns (codes int8, off float yards from the centerline)."""
    h = D.hole
    X = np.asarray(Xm, float) / YD
    Y = np.asarray(Ym, float) / YD
    off = np.full(X.shape, np.inf)
    cl = h.center
    for i in range(1, len(cl)):
        ax, ay = cl[i - 1]
        bx, by = cl[i]
        dx, dd = bx - ax, by - ay
        l2 = max(dx * dx + dd * dd, 0.0001)
        t = np.clip(((X - ax) * dx + (Y - ay) * dd) / l2, 0.0, 1.0)
        off = np.minimum(off, np.hypot(X - (ax + dx * t), Y - (ay + dd * t)))
    codes = np.full(X.shape, LIE_OOB, np.int8)
    codes[off <= h.fw / 2 + h.rough] = LIE_ROUGH
    codes[off <= h.fw / 2] = LIE_FAIRWAY
    codes[np.hypot(X - h.tee[0], Y - h.tee[1]) <= 4.0] = LIE_TEE
    codes[np.hypot(X - h.pin[0], Y - h.pin[1]) <= h.gr] = LIE_GREEN
    codes[~_inside_np(h.shore, X, Y)] = LIE_WATER
    for kind, hx, hd, w, l, tag in reversed(h.hazards):          # earlier hazards win, like LieAt
        ex = (X - hx) / max(w / 2, 0.001)
        ez = (Y - hd) / max(l / 2, 0.001)
        codes[ex * ex + ez * ez <= 1] = LIE_WATER if kind == "water" else LIE_BUNKER
    return codes, off


def lie_codes_m(D, X_m, Y_m):
    """Lie codes (LIE_* ints, names in LIE_NAMES) at Blender metre points, vectorised mirror of Hole.LieAt."""
    return _lie_all(D, X_m, Y_m)[0]


def _arc_pos_yd(D, Xm, Ym):
    """Arc length (yards) along the centerline of the nearest centerline point, vectorised."""
    X = np.asarray(Xm, float) / YD
    Y = np.asarray(Ym, float) / YD
    best = np.full(X.shape, np.inf)
    pos = np.zeros(X.shape)
    cl = D.hole.center
    acc = 0.0
    for i in range(1, len(cl)):
        ax, ay = cl[i - 1]
        bx, by = cl[i]
        dx, dd = bx - ax, by - ay
        L = math.hypot(dx, dd)
        l2 = max(dx * dx + dd * dd, 0.0001)
        t = np.clip(((X - ax) * dx + (Y - ay) * dd) / l2, 0.0, 1.0)
        d = np.hypot(X - (ax + dx * t), Y - (ay + dd * t))
        better = d < best
        best = np.where(better, d, best)
        pos = np.where(better, acc + t * L, pos)
        acc += L
    return pos


class _Grid:
    """Regular sample grid in metres (xs, ys corner coordinates, X/Y meshgrids indexed [i, j])."""

    def __init__(self, bbox, cell, pad=3):
        x0, y0, x1, y1 = bbox
        self.cell = float(cell)
        self.nx = int(math.ceil((x1 - x0) / cell)) + 2 * pad + 1
        self.ny = int(math.ceil((y1 - y0) / cell)) + 2 * pad + 1
        self.xs = x0 - pad * cell + np.arange(self.nx) * cell
        self.ys = y0 - pad * cell + np.arange(self.ny) * cell
        self.X, self.Y = np.meshgrid(self.xs, self.ys, indexing='ij')
        self.codes = None
        self.off = None


def _shore_bbox_m(D, extra=0.0):
    pts = np.array([m(x, d) for x, d in D.hole.shore])
    mn, mx = pts.min(0), pts.max(0)
    return (mn[0] - extra, mn[1] - extra, mx[0] + extra, mx[1] + extra)


def _lie_grid(D, cell=0.5):
    """The shared lie grid (computed once per cell size)."""
    c = D._lie_cache
    if c is not None and abs(c.cell - cell) < 1e-9:
        return c
    t = time.time()
    g = _Grid(_shore_bbox_m(D, 2.0), cell)
    g.codes, g.off = _lie_all(D, g.X, g.Y)
    D._lie_cache = g
    D.timings["lie_grid"] = time.time() - t
    return g


# ----------------------------------------------------------------------------- distance field to the terrain loops
class _DistField:
    """Distance (metres) from points to a set of closed loops: nearest densified loop vertex through a KDTree, then
    the exact distance to the two adjacent short segments."""

    def __init__(self, loops, spacing=0.6):
        pts, prv, nxt = [], [], []
        for L in loops:
            L = np.asarray(L, float)
            dense = []
            for i in range(len(L)):
                a, b = L[i], L[(i + 1) % len(L)]
                k = max(1, int(math.ceil(np.hypot(*(b - a)) / spacing)))
                for s in range(k):
                    dense.append(a + (b - a) * s / k)
            base = len(pts)
            n = len(dense)
            pts.extend(dense)
            prv.extend(base + (i - 1) % n for i in range(n))
            nxt.extend(base + (i + 1) % n for i in range(n))
        self.P = np.array(pts)
        self.prv = np.array(prv)
        self.nxt = np.array(nxt)
        self.kd = KDTree(len(self.P))
        for i, p in enumerate(self.P):
            self.kd.insert((float(p[0]), float(p[1]), 0.0), i)
        self.kd.balance()

    def dist(self, X, Y):
        X = np.asarray(X, float)
        shape = X.shape
        X = X.ravel()
        Y = np.asarray(Y, float).ravel()
        find = self.kd.find
        idx = np.fromiter((find((x, y, 0.0))[1] for x, y in zip(X.tolist(), Y.tolist())), dtype=np.int64, count=len(X))
        best = np.full(len(X), np.inf)
        for nb in (self.prv[idx], self.nxt[idx]):
            a = self.P[idx]
            b = self.P[nb]
            ab = b - a
            l2 = np.maximum((ab ** 2).sum(1), 1e-12)
            t = np.clip(((X - a[:, 0]) * ab[:, 0] + (Y - a[:, 1]) * ab[:, 1]) / l2, 0.0, 1.0)
            best = np.minimum(best, np.hypot(X - (a[:, 0] + ab[:, 0] * t), Y - (a[:, 1] + ab[:, 1] * t)))
        return best.reshape(shape)


def _get_distfield(D):
    if D._distfield is None:
        if not D.loops:
            raise RuntimeError("build_terrain(D) must run before anything that needs D.loops")
        D._distfield = _DistField([np.array(L) for L in D.loops])
    return D._distfield


def signed_dist_m(D, X, Y):
    """Distance in metres to the terrain boundary loops: positive on the water side (outside the land top), negative on
    land. Needs build_terrain(D) to have run."""
    d = _get_distfield(D).dist(X, Y)
    land = _lie_all(D, X, Y)[0] != LIE_WATER
    return np.where(land, -d, d)


# ----------------------------------------------------------------------------- CDT helpers
def _cdt(P, edges):
    """Full constrained Delaunay triangulation of points P (N,2) with constraint edges (pairs of indices).
    Crossing constraints are split at their intersection by Blender. Returns (V (n,2), T (m,3) counter-clockwise)."""
    verts = [Vector((float(x), float(y))) for x, y in P]
    vo, eo, fo, _a, _b, _c = geometry.delaunay_2d_cdt(verts, [(int(a), int(b)) for a, b in edges], [], 0, 1e-5, False)
    V = np.array([(v.x, v.y) for v in vo], float)
    T = np.array([list(f) for f in fo], dtype=np.int64).reshape(-1, 3)
    a, b, c = V[T[:, 0]], V[T[:, 1]], V[T[:, 2]]
    area = (b[:, 0] - a[:, 0]) * (c[:, 1] - a[:, 1]) - (b[:, 1] - a[:, 1]) * (c[:, 0] - a[:, 0])
    flip = area < 0
    T[flip] = T[flip][:, [0, 2, 1]]
    return V, T[np.abs(area) > 1e-9]


def _compact(V, T):
    used = np.unique(T)
    remap = -np.ones(len(V), dtype=np.int64)
    remap[used] = np.arange(len(used))
    return V[used], remap[T]


def _triangulate_region(loops, spacing, extra=None, margin=None):
    """Fill the region bounded by `loops` (outer loops CCW, holes CW; closed (k,2) arrays) with a constrained Delaunay
    triangulation: boundary vertices from the loops, interior points every `spacing`, optional extra constraint
    segments [(p, q), ...] (e.g. mowing stripes). Returns (V, T) or None."""
    loops = [np.asarray(L, float) for L in loops if len(L) >= 3]
    if not loops:
        return None
    pts, edges = [], []
    for L in loops:
        base = len(pts)
        n = len(L)
        pts.extend((float(x), float(y)) for x, y in L)
        edges.extend((base + i, base + (i + 1) % n) for i in range(n))
    for p, q in (extra or []):
        i = len(pts)
        pts.append((float(p[0]), float(p[1])))
        pts.append((float(q[0]), float(q[1])))
        edges.append((i, i + 1))
    A, B = _segments(loops)
    allp = np.vstack(loops)
    x0, y0 = allp.min(0)
    x1, y1 = allp.max(0)
    if margin is None:
        margin = 0.5 * spacing
    gx = np.arange(x0 + spacing * 0.37, x1, spacing)
    gy = np.arange(y0 + spacing * 0.61, y1, spacing)
    if len(gx) and len(gy):
        GX, GY = np.meshgrid(gx, gy, indexing='ij')
        C = np.stack([GX.ravel(), GY.ravel()], 1)
        C = C[_evenodd(C, A, B)]
        if len(C):
            C = C[_dist_pts_segs(C, A, B) >= margin]
        pts.extend((float(x), float(y)) for x, y in C)
    V, T = _cdt(np.array(pts), edges)
    cent = V[T].mean(axis=1)
    T = T[_evenodd(cent, A, B)]
    if len(T) == 0:
        return None
    return _compact(V, T)


# ----------------------------------------------------------------------------- marching squares regions
def _rdp(P, tol):
    keep = np.zeros(len(P), bool)
    keep[0] = keep[-1] = True
    stack = [(0, len(P) - 1)]
    while stack:
        i, j = stack.pop()
        if j <= i + 1:
            continue
        seg = P[j] - P[i]
        L = math.hypot(seg[0], seg[1])
        mid = P[i + 1:j]
        if L < 1e-12:
            d = np.hypot(mid[:, 0] - P[i, 0], mid[:, 1] - P[i, 1])
        else:
            d = np.abs(seg[0] * (mid[:, 1] - P[i, 1]) - seg[1] * (mid[:, 0] - P[i, 0])) / L
        k = int(d.argmax())
        if d[k] > tol:
            keep[i + 1 + k] = True
            stack.append((i, i + 1 + k))
            stack.append((i + 1 + k, j))
    return P[keep]


def _simplify_closed(P, tol):
    if len(P) <= 6 or tol <= 0:
        return P
    d = np.hypot(P[:, 0] - P[0, 0], P[:, 1] - P[0, 1])
    k = int(d.argmax())
    a = _rdp(P[:k + 1], tol)
    b = _rdp(np.vstack([P[k:], P[:1]]), tol)
    return np.vstack([a[:-1], b[:-1]])


def _bisect(pred, x0, y0, x1, y1, state0, iters=15):
    ax, ay, bx, by = x0.copy(), y0.copy(), x1.copy(), y1.copy()
    for _ in range(iters):
        mx, my = (ax + bx) * 0.5, (ay + by) * 0.5
        same = pred(mx, my) == state0
        ax = np.where(same, mx, ax)
        ay = np.where(same, my, ay)
        bx = np.where(same, bx, mx)
        by = np.where(same, by, my)
    return (ax + bx) * 0.5, (ay + by) * 0.5


_MS_TABLE = {1: [(0, 3)], 2: [(1, 0)], 3: [(1, 3)], 4: [(2, 1)], 6: [(2, 0)], 7: [(2, 3)], 8: [(3, 2)], 9: [(0, 2)],
             11: [(1, 2)], 12: [(3, 1)], 13: [(0, 1)], 14: [(3, 0)]}
# corners 0 BL, 1 BR, 2 TR, 3 TL; edges 0 bottom, 1 right, 2 top, 3 left; every segment keeps the inside on its LEFT


def _contours(grid, B, pred=None, F=None, simplify=0.02, min_area=0.02):
    """Closed contour loops of the boolean grid B (True = inside). With F (float field, inside = F > 0) the crossing
    points are linearly interpolated; otherwise each is bisected on `pred(X, Y) -> bool array` so it lies on the true
    boundary. Loops come out counter-clockwise for outer boundaries and clockwise for holes (inside on the left)."""
    xs, ys = grid.xs, grid.ys
    B = B.copy()
    B[0, :] = B[-1, :] = False
    B[:, 0] = B[:, -1] = False
    hi, hj = np.nonzero(B[:-1, :] != B[1:, :])
    vi, vj = np.nonzero(B[:, :-1] != B[:, 1:])
    if len(hi) + len(vi) == 0:
        return []
    if F is not None:
        f0, f1 = F[hi, hj], F[hi + 1, hj]
        hx, hy = xs[hi] + f0 / (f0 - f1) * grid.cell, ys[hj]
        g0, g1 = F[vi, vj], F[vi, vj + 1]
        vx, vy = xs[vi], ys[vj] + g0 / (g0 - g1) * grid.cell
    else:
        hx, hy = _bisect(pred, xs[hi], ys[hj], xs[hi + 1], ys[hj], B[hi, hj])
        vx, vy = _bisect(pred, xs[vi], ys[vj], xs[vi], ys[vj + 1], B[vi, vj])
    nh, nv = len(hi), len(vi)
    hid = np.full((grid.nx - 1, grid.ny), -1, np.int64)
    hid[hi, hj] = np.arange(nh)
    vid = np.full((grid.nx, grid.ny - 1), -1, np.int64)
    vid[vi, vj] = nh + np.arange(nv)
    b0, b1, b2, b3 = B[:-1, :-1], B[1:, :-1], B[1:, 1:], B[:-1, 1:]
    code = b0.astype(np.int8) + 2 * b1.astype(np.int8) + 4 * b2.astype(np.int8) + 8 * b3.astype(np.int8)
    E = [hid[:, :-1], vid[1:, :], hid[:, 1:], vid[:-1, :]]
    froms, tos = [], []
    for c, segs in _MS_TABLE.items():
        mk = code == c
        if mk.any():
            for fe, te in segs:
                froms.append(E[fe][mk])
                tos.append(E[te][mk])
    sad5, sad10 = code == 5, code == 10
    if sad5.any() or sad10.any():
        conn = np.zeros(code.shape, bool)
        si, sj = np.nonzero(sad5 | sad10)
        if F is not None:
            ctr = (F[:-1, :-1] + F[1:, :-1] + F[1:, 1:] + F[:-1, 1:]) * 0.25
            conn[si, sj] = ctr[si, sj] > 0
        else:
            conn[si, sj] = pred((xs[si] + xs[si + 1]) * 0.5, (ys[sj] + ys[sj + 1]) * 0.5)
        for sad, table in ((sad5, {True: [(0, 1), (2, 3)], False: [(0, 3), (2, 1)]}),
                           (sad10, {True: [(3, 0), (1, 2)], False: [(1, 0), (3, 2)]})):
            for flag, segs in table.items():
                mk = sad & (conn == flag)
                if mk.any():
                    for fe, te in segs:
                        froms.append(E[fe][mk])
                        tos.append(E[te][mk])
    if not froms:
        return []
    fr = np.concatenate(froms)
    to = np.concatenate(tos)
    PX = np.concatenate([hx, vx])
    PY = np.concatenate([hy, vy])
    nxt = [-1] * (nh + nv)
    for a, b in zip(fr.tolist(), to.tolist()):
        nxt[a] = b
    seen = [False] * (nh + nv)
    loops = []
    for s in np.unique(fr).tolist():
        if seen[s]:
            continue
        path = [s]
        seen[s] = True
        cur = nxt[s]
        while cur >= 0 and cur != s and not seen[cur]:
            path.append(cur)
            seen[cur] = True
            cur = nxt[cur]
        if cur == s and len(path) >= 3:
            P = np.stack([PX[path], PY[path]], 1)
            keep = np.ones(len(P), bool)
            keep[1:] = np.hypot(*(P[1:] - P[:-1]).T) > 1e-4
            P = P[keep]
            P = _simplify_closed(P, simplify)
            if len(P) >= 3 and abs(signed_area(P)) >= min_area:
                loops.append(P)
    return loops


def _pred_codes(D, grid, codes_set, extra=None):
    """pred(X, Y) for the scoring regions; reuses the cached lie grid arrays when called on the grid itself."""
    cs = tuple(codes_set)

    def pred(X, Y):
        if X is grid.X and grid.codes is not None:
            codes, off = grid.codes, grid.off
        else:
            codes, off = _lie_all(D, X, Y)
        r = np.zeros(codes.shape, bool)
        for c in cs:
            r |= codes == c
        if extra is not None:
            r &= extra(D, X, Y, codes, off)
        return r
    return pred


# ----------------------------------------------------------------------------- terrain
def _wall_noise(s, phases, lams):
    """Smooth 0..1 noise along a loop's arc length s (metres): two long waves plus a short facet wave."""
    return (0.5 + 0.22 * np.sin(2 * np.pi * s / lams[0] + phases[0]) + 0.18 * np.sin(2 * np.pi * s / lams[1] + phases[1])
            + 0.10 * np.sin(2 * np.pi * s / lams[2] + phases[2]))


def _inward_thickness(P, N, A, B, chunk=256):
    """Distance from each vertex P (n,2) along its unit direction N to the first boundary segment A->B (inf if none)."""
    E = B - A
    out = np.full(len(P), np.inf)
    ex, ey = E[None, :, 0], E[None, :, 1]
    with np.errstate(divide='ignore', invalid='ignore'):
        for s0 in range(0, len(P), chunk):
            px, py = P[s0:s0 + chunk, 0:1], P[s0:s0 + chunk, 1:2]
            nx, ny = N[s0:s0 + chunk, 0:1], N[s0:s0 + chunk, 1:2]
            apx, apy = A[None, :, 0] - px, A[None, :, 1] - py
            den = nx * ey - ny * ex
            t = (apx * ey - apy * ex) / den
            u = (apx * ny - apy * nx) / den
            ok = (np.abs(den) > 1e-12) & (t > 1e-3) & (u >= -1e-9) & (u <= 1 + 1e-9)
            out[s0:s0 + chunk] = np.where(ok, t, np.inf).min(axis=1)
    return out


def _trace_boundary_loops(V, T):
    """Ordered boundary loops (lists of vertex indices) of a CCW triangle set; the land is on the left of every loop."""
    directed = set()
    for a, b, c in T.tolist():
        directed.add((a, b))
        directed.add((b, c))
        directed.add((c, a))
    bnd = [(a, b) for (a, b) in directed if (b, a) not in directed]
    out = {}
    for a, b in bnd:
        out.setdefault(a, []).append(b)
    used = set()
    loops = []
    for a0, b0 in sorted(bnd):
        if (a0, b0) in used:
            continue
        loop = [a0]
        prev, cur = a0, b0
        used.add((a0, b0))
        guard = 0
        while cur != a0 and guard < len(bnd) + 5:
            loop.append(cur)
            cands = [c for c in out[cur] if (cur, c) not in used]
            if not cands:
                break
            if len(cands) > 1:                      # pinch vertex: first outgoing edge clockwise from the way back
                back = V[prev] - V[cur]
                ba = math.atan2(back[1], back[0])
                best, bi = 1e9, 0
                for k, c in enumerate(cands):
                    o = V[c] - V[cur]
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


def build_terrain(D, undercut_m=3.0, interior_spacing=6.5, ellipse_edge_m=2.4, name=None):
    """TERRAIN_<NAME>: the land top at EXACTLY D.play_z, bounded by the design SHORE polygon with every WATER hazard
    ellipse cut out (no top face inside an ellipse; an ellipse sticking out past the shore or swallowing a neck is
    handled, pads come out as separate pieces). The cut is a circumscribed polygon (each chord tangent to the
    analytic ellipse, at most about 4 cm outside it), so no land triangle ever lies inside a water ellipse. Banded rock walls hang from EVERY boundary loop (outer shore and each
    cut) down to z = -6: slot 0 MAT_ROUGH (top and a grassy lip band), slot 1 MAT_CLIFF (upper bands), slot 2
    MAT_CLIFF_DARK (the lowest bands, as hole 7). The walls are vertical or undercut by up to `undercut_m` metres toward
    the land (never outward of the top edge in plan view); no bottom cap. Stores D.loops (ordered loops in metres, land on
    the LEFT of the travel direction) and D.loop_kinds ('outer' for CCW loops, 'hole' for CW)."""
    t0 = time.time()
    pz = D.play_z
    shore = np.array([m(x, d) for x, d in D.hole.shore], float)
    ells = []
    for kind, hx, hd, w, l, tag in D.hole.hazards:
        if kind != "water":
            continue
        cx, cy = m(hx, hd)
        a, b = w / 2 * YD, l / 2 * YD
        per = math.pi * (3 * (a + b) - math.sqrt((3 * a + b) * (a + 3 * b)))
        n = max(32, int(per / ellipse_edge_m))
        t = np.linspace(0, 2 * np.pi, n, endpoint=False)
        k = 1.0 / math.cos(math.pi / n)        # circumscribed polygon: every chord is tangent to the true ellipse, so no
        ells.append(np.stack([cx + a * k * np.cos(t), cy + b * k * np.sin(t)], 1))     # top face can lie inside the water
    loops_in = [shore] + ells
    pts, edges = [], []
    for L in loops_in:
        base = len(pts)
        pts.extend((float(x), float(y)) for x, y in L)
        edges.extend((base + i, base + (i + 1) % len(L)) for i in range(len(L)))
    A, B = _segments(loops_in)
    x0, y0 = shore.min(0)
    x1, y1 = shore.max(0)
    sp = interior_spacing
    gx = np.arange(x0 + sp * 0.37, x1, sp)
    gy = np.arange(y0 + sp * 0.61, y1, sp)
    GX, GY = np.meshgrid(gx, gy, indexing='ij')
    C = np.stack([GX.ravel(), GY.ravel()], 1)
    C = C[_evenodd(C, *_segments([shore]))]
    for e in ells:
        C = C[~_evenodd(C, *_segments([e]))]
    if len(C):
        C = C[_dist_pts_segs(C, A, B) >= 0.45 * sp]
    pts.extend((float(x), float(y)) for x, y in C)
    V, T = _cdt(np.array(pts), edges)
    cent = V[T].mean(axis=1)
    keep = _evenodd(cent, *_segments([shore]))
    for e in ells:
        keep &= ~_evenodd(cent, *_segments([e]))
    T = T[keep]
    V, T = _compact(V, T)
    nv_top = len(V)
    loops_idx = _trace_boundary_loops(V, T)

    verts = [(float(x), float(y), pz) for x, y in V]
    faces = [tuple(int(i) for i in t) for t in T]
    mats = [0] * len(faces)
    smooth = [True] * len(faces)

    # rings (z): top edge, grassy lip, four bands down to the waterline, then below the water
    h_rest = pz - 1.0
    levels = [pz, pz - 1.0] + [h_rest * (1 - k / 4.0) for k in (1, 2, 3, 4)] + [-6.0]
    weights = [0.06, 0.20, 0.26, 0.24, 0.20, 0.04]            # share of the undercut reached at each ring
    band_mat = {1: 0, 2: 1, 3: 1, 4: 1, 5: 2, 6: 2}
    D.loops, D.loop_kinds = [], []
    seg_A, seg_B = _segments([V[lp] for lp in loops_idx])
    for loop in loops_idx:
        P = V[loop]
        n = len(P)
        D.loops.append([(float(x), float(y)) for x, y in P])
        D.loop_kinds.append("outer" if signed_area(P) > 0 else "hole")
        e = np.roll(P, -1, axis=0) - P
        L = np.maximum(np.hypot(e[:, 0], e[:, 1]), 1e-9)
        tg = e / L[:, None]
        nl = np.stack([-tg[:, 1], tg[:, 0]], 1)                  # left normal = into the land
        nv = nl + np.roll(nl, 1, axis=0)
        nvl = np.maximum(np.hypot(nv[:, 0], nv[:, 1]), 1e-9)
        nrm = nv / nvl[:, None]
        nrm = np.where((nvl < 1e-6)[:, None], nl, nrm)
        tp = np.roll(tg, 1, axis=0)
        dphi = np.abs(np.arctan2(tp[:, 0] * tg[:, 1] - tp[:, 1] * tg[:, 0], (tp * tg).sum(1)))
        cap = 0.8 * np.minimum(np.roll(L, 1), L) / np.maximum(dphi, 0.05)       # corner sharpness
        cap = np.minimum(cap, 0.4 * _inward_thickness(P, nrm, seg_A, seg_B))   # never more than 40% of the land width there
        capw = cap.copy()
        for sft in (1, 2, -1, -2):
            capw = np.minimum(capw, np.roll(cap, sft))
        s_arc = np.concatenate([[0.0], np.cumsum(L)[:-1]])
        cum = np.zeros(n)
        ring_idx = [list(loop)]
        for k in range(1, len(levels)):
            ph = [D.rng.uniform(0, 2 * math.pi) for _ in range(3)]
            lam = [D.rng.uniform(12, 30), D.rng.uniform(31, 60), D.rng.uniform(4.5, 8.0)]
            nz = _wall_noise(s_arc, ph, lam)
            cum = cum + undercut_m * weights[k - 1] * (0.15 + 1.7 * nz)       # increments stay >= 0: never bulges outward
            ins = np.minimum(cum, capw)
            Q = P + nrm * ins[:, None]
            base = len(verts)
            verts.extend((float(x), float(y), float(levels[k])) for x, y in Q)
            ring_idx.append(list(range(base, base + n)))
            up, lo = ring_idx[k - 1], ring_idx[k]
            for i in range(n):
                j = (i + 1) % n
                faces.append((up[i], lo[i], lo[j], up[j]))
                mats.append(band_mat[k])
                smooth.append(False)          # flat walls: smooth lip normals tilt the top triangles beside the edge (blotches)
    ob = new_mesh_object(name or D.terrain_name, "COURSE")
    me = ob.data
    build_mesh(ob, verts, faces, smooth=smooth, material_index=mats,
               materials=[D.mats[mn] for mn in ("MAT_ROUGH", "MAT_CLIFF", "MAT_CLIFF_DARK")])
    D.terrain = ob
    D.objects[ob.name] = ob
    D._distfield = None
    D.timings["terrain"] = time.time() - t0
    _log(D, f"terrain {ob.name}: {nv_top} top verts, {len(T)} top tris, {len(D.loops)} loops "
            f"({D.loop_kinds.count('outer')} outer, {D.loop_kinds.count('hole')} hole), {len(me.polygons)} faces total")
    return ob


# ----------------------------------------------------------------------------- play surfaces
def _region_object(D, name, loops, z, mat, spacing, smooth=True, extra=None, mat_fn=None, extra_mats=()):
    res = _triangulate_region(loops, spacing, extra=extra)
    if res is None:
        _log(D, f"region {name}: empty, skipped")
        return None
    V, T = res
    verts = [(float(x), float(y), z) for x, y in V]
    mi = None
    if mat_fn is not None:
        mi = mat_fn(V[T].mean(axis=1))
    ob = new_mesh_object(name, "COURSE")
    build_mesh(ob, verts, [tuple(int(i) for i in t) for t in T], smooth=smooth, material_index=mi,
               materials=[mat] + list(extra_mats))
    D.objects[name] = ob
    return ob


def _rounded_rect(cx, cy, w, d, dirv, r=1.3, arc=6):
    """Rounded rectangle polygon (CCW), width w across, depth d along dirv, centre (cx, cy)."""
    f = np.array(dirv, float)
    f /= np.hypot(*f)
    a = np.array([f[1], -f[0]])                  # right of f
    pts = []
    corners = [(+1, +1, 0.0), (-1, +1, 0.5 * np.pi), (-1, -1, np.pi), (+1, -1, 1.5 * np.pi)]
    hw, hd = w / 2 - r, d / 2 - r
    for sx, sy, a0 in corners:
        c = np.array([cx, cy]) + a * (sx * hw) + f * (sy * hd)
        for k in range(arc + 1):
            ang = a0 + (np.pi / 2) * k / arc
            # local arc direction in the (a, f) frame
            pts.append(c + a * (r * np.cos(ang)) + f * (r * np.sin(ang)))
    P = np.array(pts)
    # the (a, f) frame is right-handed only when a = f rotated -90 deg, which makes this loop clockwise: fix
    if signed_area(P) < 0:
        P = P[::-1]
    return P


def _bunker_ring_pts(hz, scale=1.0, n=None):
    """Points (n, 2) metres on the axis-aligned ellipse of a bunker hazard, scaled about its centre."""
    cx, cy = m(hz[1], hz[2])
    a0, b0 = hz[3] / 2 * YD, hz[4] / 2 * YD
    if n is None:
        per = math.pi * (3 * (a0 + b0) - math.sqrt((3 * a0 + b0) * (a0 + 3 * b0)))
        n = max(32, int(per / 1.4))
    t = np.linspace(0, 2 * np.pi, n, endpoint=False)
    return np.stack([cx + a0 * scale * np.cos(t), cy + b0 * scale * np.sin(t)], 1)


def build_play_surfaces(D, cell=0.5, fairway_spacing=3.0):
    """FAIRWAY (+ FAIRWAY_FIRSTCUT), GREEN (+ GREEN_APRON), TEE_BOX, BUNKER_nn + BUNKER_nn_LIP at the hole 7 z offsets
    above D.play_z. Every region is traced from the scoring mirror (see module docstring) and is flat. Fairway mowing
    stripes alternate MAT_FAIRWAY / MAT_FAIRWAY_STRIPE every SCENERY['stripe_m'] metres along the centerline, cut
    along exact constraint lines. Returns {name: obj}."""
    t0 = time.time()
    h = D.hole
    pz = D.play_z
    sc = D.scenery
    fc_m = float(sc.get("firstcut_yd", 3.0))
    ap_yd = float(sc.get("apron_yd", 3.0))
    stripe_m = float(sc.get("stripe_m", 18.0))
    g = _lie_grid(D, cell)
    out = {}

    # --- fairway (lie Fairway or Tee: the 4 yd tee disc sits inside the fairway round end)
    pred = _pred_codes(D, g, (LIE_FAIRWAY, LIE_TEE))
    loops = _contours(g, pred(g.X, g.Y), pred=pred)
    cl = [np.array(m(x, d)) for x, d in h.center]
    seglen = [float(np.hypot(*(cl[i] - cl[i - 1]))) for i in range(1, len(cl))]
    total = sum(seglen)
    extra = []
    s = stripe_m
    while s < total - 0.5:
        acc = 0.0
        for i, L in enumerate(seglen):
            if acc + L >= s:
                t = (s - acc) / L
                p = cl[i] + (cl[i + 1] - cl[i]) * t
                dv = (cl[i + 1] - cl[i]) / L
                nv = np.array([-dv[1], dv[0]])
                half = h.fw / 2 * YD + 0.8
                extra.append((p - nv * half, p + nv * half))
                break
            acc += L
        s += stripe_m

    def stripe_mat(C):
        pos = _arc_pos_yd(D, C[:, 0], C[:, 1]) * YD
        return (np.floor(pos / stripe_m).astype(int) % 2).tolist()

    ob = _region_object(D, "FAIRWAY", loops, pz + Z_FAIRWAY, D.mats["MAT_FAIRWAY"], fairway_spacing, extra=extra,
                        mat_fn=stripe_mat, extra_mats=(D.mats["MAT_FAIRWAY_STRIPE"],))
    if ob:
        out["FAIRWAY"] = ob

    # --- first cut: fairway/tee/rough within fw/2 + firstcut of the centerline, clipped by shore/water/bunker/green
    lim = h.fw / 2 + fc_m / YD
    pred = _pred_codes(D, g, (LIE_FAIRWAY, LIE_TEE, LIE_ROUGH), extra=lambda D_, X, Y, codes, off: off <= lim)
    ob = _region_object(D, "FAIRWAY_FIRSTCUT", _contours(g, pred(g.X, g.Y), pred=pred), pz + Z_FIRSTCUT, D.mats["MAT_FIRSTCUT"], 4.5)
    if ob:
        out["FAIRWAY_FIRSTCUT"] = ob

    # --- green and apron
    pred = _pred_codes(D, g, (LIE_GREEN,))
    ob = _region_object(D, "GREEN", _contours(g, pred(g.X, g.Y), pred=pred), pz + Z_GREEN, D.mats["MAT_GREEN"], 3.0)
    if ob:
        out["GREEN"] = ob
    pin = h.pin

    def near_pin(D_, X, Y, codes, off):
        return np.hypot(X / YD - pin[0], Y / YD - pin[1]) <= h.gr + ap_yd
    pred = _pred_codes(D, g, (LIE_GREEN, LIE_ROUGH, LIE_OOB), extra=near_pin)      # not over the fairway: it is the same colour
    ob = _region_object(D, "GREEN_APRON", _contours(g, pred(g.X, g.Y), pred=pred), pz + Z_APRON, D.mats["MAT_FAIRWAY"], 4.0)
    if ob:
        out["GREEN_APRON"] = ob

    # --- tee box: rounded rectangle about the tee marker, oriented along the first leg
    tw, td = sc.get("tee_box_m", (9.0, 7.0))
    d0 = np.array(m(*h.center[1])) - np.array(m(*h.center[0]))
    P = _rounded_rect(0.0, 0.0, tw, td, d0)
    ob = _region_object(D, "TEE_BOX", [P], pz + Z_TEE, D.mats["MAT_GREEN"], 2.5)
    if ob:
        out["TEE_BOX"] = ob

    # --- bunkers: the exact ellipse (sand; clipped by the scoring lie only if it dips into water / past the shore)
    #     + a raised lip ring (hole 7 scales 1.0 inner, 1.10 mid, 1.22 outer) pulled in / broken where it would leave the land
    bi = 0
    for hz in h.hazards:
        if hz[0] != "bunker":
            continue
        bi += 1
        cx, cy = m(hz[1], hz[2])
        a, b = hz[3] / 2 * YD, hz[4] / 2 * YD
        ring = _bunker_ring_pts(hz, 1.0)
        n = len(ring)
        gx, gy = np.meshgrid(np.arange(-a, a + 0.01, 1.5), np.arange(-b, b + 0.01, 1.5), indexing='ij')
        inb = (gx / a) ** 2 + (gy / b) ** 2 <= 1
        probe = np.vstack([ring, np.stack([cx + gx[inb], cy + gy[inb]], 1)])
        if (lie_codes_m(D, probe[:, 0], probe[:, 1]) == LIE_WATER).any():
            gb = _Grid((cx - a, cy - b, cx + a, cy + b), 0.4, pad=3)

            def pred_b(X, Y, cx=cx, cy=cy, a=a, b=b):
                return (((X - cx) / a) ** 2 + ((Y - cy) / b) ** 2 <= 1) & (lie_codes_m(D, X, Y) == LIE_BUNKER)
            sand_loops = _contours(gb, pred_b(gb.X, gb.Y), pred=pred_b)
            _log(D, f"bunker {bi}: overlaps water or the shore, sand clipped to the scoring lie")
        else:
            sand_loops = [ring]
        ob = _region_object(D, f"BUNKER_{bi:02d}", sand_loops, pz + Z_SAND, D.mats["MAT_SAND"], 2.2)
        if ob:
            out[ob.name] = ob
        cand = [1.22, 1.17, 1.12, 1.07, 1.03]
        ok = np.zeros((len(cand), n), bool)
        for k, sc_ in enumerate(cand):
            Pk = _bunker_ring_pts(hz, sc_, n)
            dirv = (Pk - np.array([cx, cy])) / np.maximum(np.hypot(Pk[:, 0] - cx, Pk[:, 1] - cy), 1e-6)[:, None]
            far = Pk + dirv * 0.9
            ok[k] = (lie_codes_m(D, Pk[:, 0], Pk[:, 1]) != LIE_WATER) & (lie_codes_m(D, far[:, 0], far[:, 1]) != LIE_WATER)
        has = ok.any(axis=0)
        s_out = np.where(has, np.array(cand)[np.where(has, ok.argmax(axis=0), 0)], 1.0)    # largest valid scale per angle
        inner_ok = lie_codes_m(D, ring[:, 0], ring[:, 1]) != LIE_WATER
        t = np.linspace(0, 2 * np.pi, n, endpoint=False)

        def rpts(sv):
            return np.stack([cx + a * sv * np.cos(t), cy + b * sv * np.sin(t)], 1)
        inner, mid, outer = rpts(1.0), rpts(1.0 + (s_out - 1.0) * 0.45), rpts(s_out)
        good = has & inner_ok
        verts = ([(x, y, pz + Z_SAND + 0.02) for x, y in inner] + [(x, y, pz + 0.57) for x, y in mid] +
                 [(x, y, pz + 0.22) for x, y in outer])
        faces = []
        for i in range(n):
            j = (i + 1) % n
            if good[i] and good[j]:
                faces.append((i, n + i, n + j, j))
                faces.append((n + i, 2 * n + i, 2 * n + j, n + j))
        if faces:
            used = sorted({v for f in faces for v in f})                  # drop the vertices of skipped segments
            remap = {v: k for k, v in enumerate(used)}
            verts = [verts[v] for v in used]
            faces = [tuple(remap[v] for v in f) for f in faces]
            lob = new_mesh_object(f"BUNKER_{bi:02d}_LIP", "COURSE")
            build_mesh(lob, verts, faces, smooth=True)
            assign(lob, D.mats["MAT_BUNKER_LIP"])
            D.objects[lob.name] = lob
            out[lob.name] = lob
    D.timings["play"] = time.time() - t0
    _log(D, "play surfaces: " + ", ".join(f"{k} {len(v.data.polygons)}f" for k, v in out.items()))
    return out


# ----------------------------------------------------------------------------- water
def _wobble(X, Y, amp, seed=0.0):
    return amp * (0.6 * np.sin(0.21 * X + 0.11 * Y + 1.3 + seed) + 0.4 * np.sin(0.37 * Y - 0.17 * X + 0.7 + seed))


def build_water(D, ocean_name='WATER_OCEAN', ocean_material='MAT_WATER', shallow_material='MAT_WATER_SHALLOW',
                foam_material='MAT_FOAM', crest=True, shallow_m=16.0, foam_m=3.4, inner_m=5.0, cell=1.0, min_piece_m2=10.0):
    """Ocean plane (4000 m, z = 0, real vertices, scale 1) named `ocean_name` with `ocean_material`, plus WATER_SHALLOW
    (z 0.04), WATER_FOAM (z 0.08) and a broken WATER_FOAM_CREST (z 0.09) bands hugging EVERY boundary loop of D.loops on
    the water side (the water ellipses too: a small gap is filled with shallow water). The bands also extend `inner_m`
    metres under the land, where the undercut cliffs hide them, so the water meets the wall base with no gap.
    For the crater call build_water(D, 'WATER_LAVA', 'MAT_LAVA', shallow_material=None, foam_material=None, crest=False)
    (blue shallow water and white foam around lava would look wrong). Returns {name: obj}."""
    t0 = time.time()
    out = {}
    bx0, by0, bx1, by1 = _shore_bbox_m(D)
    cx, cy = (bx0 + bx1) / 2, (by0 + by1) / 2
    ob = new_mesh_object(ocean_name, "ENVIRONMENT")
    hs = 2000.0
    build_mesh(ob, [(cx - hs, cy - hs, 0.0), (cx + hs, cy - hs, 0.0), (cx + hs, cy + hs, 0.0), (cx - hs, cy + hs, 0.0)],
               [(0, 1, 2, 3)], smooth=True)
    assign(ob, D.mats[ocean_material])
    out[ocean_name] = ob
    D.objects[ocean_name] = ob
    if shallow_material is None and foam_material is None:
        D.timings["water"] = time.time() - t0
        return out
    ext = shallow_m + 6.0
    grid = _Grid((bx0 - ext, by0 - ext, bx1 + ext, by1 + ext), cell, pad=2)
    sd = signed_dist_m(D, grid.X, grid.Y)

    def band(name, mat, z, F, spacing):
        loops = _contours(grid, F > 0, F=F, simplify=0.05, min_area=min_piece_m2)     # no stray foam specks
        o = _region_object(D, name, loops, z, D.mats[mat], spacing)
        if o:
            o.parent = get_root()
            link_to(o, "ENVIRONMENT")
            out[name] = o
    if shallow_material is not None:
        F = np.minimum(shallow_m + _wobble(grid.X, grid.Y, 3.0, 2.0) - sd, sd + inner_m)
        band("WATER_SHALLOW", shallow_material, 0.04, F, 7.0)
    if foam_material is not None:
        F = np.minimum(foam_m + _wobble(grid.X, grid.Y, 2.2, 5.0) - sd, sd + inner_m)
        band("WATER_FOAM", foam_material, 0.08, F, 5.0)
        if crest:
            lo, hi = shallow_m * 0.62, shallow_m * 0.62 + 1.6
            dash = (np.sin(0.23 * grid.X + 0.15 * grid.Y) + np.sin(0.09 * grid.X - 0.13 * grid.Y + 1.7)) > 0.35
            F = np.where(dash, np.minimum(sd - lo, hi - sd), -1.0)
            band("WATER_FOAM_CREST", foam_material, 0.09, F, 3.0)
    D.timings["water"] = time.time() - t0
    _log(D, "water: " + ", ".join(f"{k} {len(v.data.polygons)}f" for k, v in out.items()))
    return out


# ----------------------------------------------------------------------------- rocks
def import_rocks(D):
    """Copy ROCK_SMALL, ROCK_MEDIUM, ROCK_LARGE and CLIFF_ROCK (mesh data) from hole_07.blend with
    bpy.data.libraries.load(link=False), a read-only operation, into the hidden ASSET_LIBRARY collection; the loaded
    MAT_ROCK is remapped onto the palette material. Verifies hole_07.blend's sha256 is unchanged."""
    if not os.path.isfile(HOLE07_BLEND):
        raise FileNotFoundError(HOLE07_BLEND)
    before = sha256_file(HOLE07_BLEND)
    with bpy.data.libraries.load(HOLE07_BLEND, link=False) as (df, dt):
        dt.objects = [n for n in ROCK_NAMES if n in df.objects]
    lib = get_collection("ASSET_LIBRARY")
    for ob in dt.objects:
        if ob is None:
            continue
        lib.objects.link(ob)
        ob.parent = None
        ob.hide_render = True
        ob.hide_viewport = True
        me = ob.data
        for i, mat in enumerate(list(me.materials)):
            base = mat.name.split(".")[0] if mat else ""
            if base in D.mats and mat is not D.mats[base]:
                me.materials[i] = D.mats[base]
                if mat.users == 0:
                    bpy.data.materials.remove(mat)
        key = ob.name.split(".")[0]
        D.rock_lib[key] = ob
        co = np.array([v.co[:] for v in me.vertices])
        D.rock_bbox[key] = (Vector(co.min(0)), Vector(co.max(0)))
    for stray in [mt for mt in bpy.data.materials if mt.users == 0 and mt.name.startswith("MAT_ROCK.")]:
        bpy.data.materials.remove(stray)
    after = sha256_file(HOLE07_BLEND)
    if before != after:
        raise RuntimeError("hole_07.blend changed while importing rocks (this must never happen)")
    missing = [n for n in ROCK_NAMES if n not in D.rock_lib]
    if missing:
        raise RuntimeError(f"rocks missing from hole_07.blend: {missing}")
    _log(D, f"rocks imported: {sorted(D.rock_lib)}; hole_07.blend sha256 unchanged {after[:12]}")
    return D.rock_lib


def _rock_key(kind):
    k = str(kind).upper()
    if k.startswith("ROCK_"):
        k = k[5:]
    return "CLIFF_ROCK" if k in ("CLIFF", "CLIFF_ROCK") else f"ROCK_{k}"


def place_rock(D, kind, loc_m, scale=(1, 1, 1), rot=None):
    """Instance of a library rock (shared mesh data): ROCK_<KIND>.nnn or CLIFF_ROCK.nnn in collection ROCKS (a child of
    ENVIRONMENT), parented to the root. kind: 'SMALL', 'MEDIUM', 'LARGE', 'CLIFF' (or the full names). loc_m: (x, y, z)
    metres (a 2-tuple puts the base half a metre into the play height). rot: Euler tuple/Euler/None (random yaw + tilt,
    seeded). Names never start with a ground prefix."""
    if not D.rock_lib:
        import_rocks(D)
    key = _rock_key(kind)
    src = D.rock_lib[key]
    env = get_collection("ENVIRONMENT")
    col = bpy.data.collections.get("ROCKS")
    if col is None:
        col = bpy.data.collections.new("ROCKS")
        env.children.link(col)
    n = D.rock_count.get(key, 0)
    D.rock_count[key] = n + 1
    ob = bpy.data.objects.new(f"{key}.{n:03d}", src.data)
    col.objects.link(ob)
    ob.parent = get_root()
    if len(loc_m) == 2:
        loc_m = (loc_m[0], loc_m[1], D.play_z - 0.5)
    ob.location = tuple(loc_m)
    if isinstance(scale, (int, float)):
        scale = (scale, scale, scale)
    ob.scale = tuple(scale)
    if rot is None:
        rot = (D.rng.uniform(-0.15, 0.15), D.rng.uniform(-0.15, 0.15), D.rng.uniform(0, math.tau))
    ob.rotation_euler = tuple(rot) if not hasattr(rot, "to_matrix") else rot
    return ob


def _place_fit(D, kind, center, dims, rot_mat=None):
    """Place a rock so its scaled, rotated bounding box is `dims` big and centred on `center` (world)."""
    mn, mx = D.rock_bbox[_rock_key(kind)]
    size, ctr = mx - mn, (mn + mx) * 0.5
    scale = Vector((dims[0] / size.x, dims[1] / size.y, dims[2] / size.z))
    R = rot_mat if rot_mat is not None else Matrix.Identity(3)
    loc = Vector(center) - R @ Vector((ctr.x * scale.x, ctr.y * scale.y, ctr.z * scale.z))
    return place_rock(D, kind, loc, scale=scale, rot=R.to_euler())


def _on_land_margin(D, x, y, margin=1.5):
    """True when (x, y) metres is land (not water) with a metre margin of land around it (4 probe points)."""
    pts = np.array([(x, y), (x + margin, y), (x - margin, y), (x, y + margin), (x, y - margin)])
    return bool((lie_codes_m(D, pts[:, 0], pts[:, 1]) != LIE_WATER).all())


def build_arch(D, center_m, facing_deg, span_m=9.0, height_m=9.0, broken=True):
    """Visual-only ruined arch made of instanced library rocks: two pillars (three stacked rocks each), a round arch ring
    of elongated rocks on top (one piece missing when `broken`), rubble at the base. facing_deg: direction you look THROUGH
    the opening, clockwise from +Y (0 = down the hole, 90 = +X); the overview camera reads it best when the opening faces
    roughly along +-Y. center_m: (x, y) metres on the play height. Warns when the centre is not on land. Returns objects."""
    if not D.rock_lib:
        import_rocks(D)
    cx, cy = center_m[0], center_m[1]
    if not _on_land_margin(D, cx, cy, span_m * 0.7):
        _log(D, f"WARNING build_arch: centre {cx:.1f},{cy:.1f} is within {span_m * 0.7:.1f} m of water")
    phi = math.radians(facing_deg)
    f = Vector((math.sin(phi), math.cos(phi), 0.0))
    a = Vector((math.cos(phi), -math.sin(phi), 0.0))
    Rz = Matrix.Rotation(-phi, 3, 'Z')
    base_z = D.play_z - 0.25
    rnd = D.rng
    thick = max(1.6, span_m * 0.24)
    depth = max(2.4, span_m * 0.30)
    R = span_m / 2.0
    pillar_h = max(2.5, height_m - R - thick / 2.0)         # where the arch ring's centre line starts
    objs = []
    pw = span_m * 0.34
    top = pillar_h + thick * 0.35                           # pillars run up into the ring: no daylight between them
    hseg = top / 2.3                                        # three rocks, each overlapping the next by 35 percent
    for side in (-1, +1):
        lx = side * R
        for k, wk in enumerate((1.15, 1.0, 0.86)):
            zc = base_z + hseg * 0.5 + hseg * 0.65 * k
            off_a = rnd.uniform(-0.10, 0.10) * pw
            off_f = rnd.uniform(-0.08, 0.08) * depth
            c = Vector((cx, cy, 0)) + a * (lx + off_a) + f * off_f + Vector((0, 0, zc))
            R_ = Rz @ Matrix.Rotation(rnd.uniform(-0.06, 0.06), 3, 'Y') @ Matrix.Rotation(rnd.uniform(-0.2, 0.2), 3, 'Z')
            objs.append(_place_fit(D, "LARGE" if k == 0 else "MEDIUM", c, (pw * wk, depth * wk, hseg * 1.15), R_))
    zc = base_z + pillar_h
    n_arch = 7
    for i in range(n_arch):
        th = math.pi * (i + 0.5) / n_arch
        if broken and i == n_arch - 1:
            continue
        px, pz_ = R * math.cos(th), R * math.sin(th)
        seg = 2 * R * math.sin(math.pi / (2 * n_arch)) * 1.75
        alpha = th + math.pi / 2
        c = Vector((cx, cy, 0)) + a * px + Vector((0, 0, zc + pz_))
        R_ = Rz @ Matrix.Rotation(-alpha, 3, 'Y')
        objs.append(_place_fit(D, "MEDIUM", c, (seg, depth * 0.9, thick * rnd.uniform(0.95, 1.15)), R_))
    if broken:                                              # a stub where the last piece fell off
        th = math.pi * 0.93
        c = Vector((cx, cy, 0)) + a * (R * math.cos(th)) + Vector((0, 0, zc + R * math.sin(th) * 0.9))
        objs.append(_place_fit(D, "SMALL", c, (seg * 0.55, depth * 0.8, thick * 0.9), Rz @ Matrix.Rotation(-(th + math.pi / 2), 3, 'Y')))
    # rubble around the feet and a fallen block
    for _ in range(8):
        ang = rnd.uniform(0, math.tau)
        rr = R * rnd.uniform(0.35, 1.5)
        c = Vector((cx, cy, 0)) + a * (math.cos(ang) * rr) + f * (math.sin(ang) * rr * 0.55)
        s = rnd.uniform(0.7, 1.6)
        d3 = (s * rnd.uniform(1.2, 1.9), s * rnd.uniform(0.9, 1.4), s * rnd.uniform(0.7, 1.1))
        c.z = base_z + d3[2] * 0.4
        objs.append(_place_fit(D, rnd.choice(("SMALL", "SMALL", "MEDIUM")), c, d3, Matrix.Rotation(rnd.uniform(0, math.tau), 3, 'Z')))
    return objs


def build_ruin_wall(D, p0_m, p1_m, height_m=3.5, clip_to_land=True):
    """A broken wall of instanced rocks from p0 to p1 (metres): a row of block-shaped rocks with gaps, two courses in
    places, rubble at the ends. Rocks whose centre is not on land (1 m margin) are skipped when clip_to_land."""
    if not D.rock_lib:
        import_rocks(D)
    p0, p1 = Vector((p0_m[0], p0_m[1], 0)), Vector((p1_m[0], p1_m[1], 0))
    L = (p1 - p0).length
    if L < 1.0:
        return []
    d = (p1 - p0) / L
    yaw = math.atan2(d.y, d.x)
    Rz = Matrix.Rotation(yaw, 3, 'Z')
    rnd = D.rng
    base_z = D.play_z - 0.25
    objs = []
    step = 2.7
    n = max(2, int(L / step))
    for i in range(n):
        if rnd.random() < 0.16:
            continue
        s = (i + 0.5) / n
        c = p0 + d * (L * s)
        if clip_to_land and not _on_land_margin(D, c.x, c.y, 1.0):
            continue
        h = height_m * rnd.uniform(0.45, 1.0)
        length = L / n * rnd.uniform(1.05, 1.3)
        thick = rnd.uniform(1.1, 1.6)
        c.z = base_z + h / 2
        R_ = Rz @ Matrix.Rotation(rnd.uniform(-0.1, 0.1), 3, 'Z') @ Matrix.Rotation(rnd.uniform(-0.05, 0.05), 3, 'Y')
        objs.append(_place_fit(D, "MEDIUM", c, (length, thick, h), R_))
        if h > height_m * 0.7 and rnd.random() < 0.5:
            c2 = Vector((c.x, c.y, base_z + h + 0.5))
            objs.append(_place_fit(D, "SMALL", c2, (length * 0.7, thick * 0.9, 1.0), Rz))
    for end in (p0, p1):
        for _ in range(3):
            q = end + Vector((rnd.uniform(-2.0, 2.0), rnd.uniform(-2.0, 2.0), 0))
            if clip_to_land and not _on_land_margin(D, q.x, q.y, 1.0):
                continue
            sz = rnd.uniform(0.7, 1.4)
            q.z = base_z + sz * 0.35
            objs.append(_place_fit(D, "SMALL", q, (sz * 1.4, sz, sz * 0.8), Matrix.Rotation(rnd.uniform(0, math.tau), 3, 'Z')))
    return objs


def scatter_rocks(D, count=20, seed=0, zone="sea", dist=None, kinds=None, scale=None, z=None, min_gap=10.0,
                  allow=("OutOfBounds",), avoid=(), companion=0.4, tries=6000):
    """Seeded, deterministic rock scatter. zone: 'sea' (sea stacks in the water 8-28 m from the land, z -4..-1),
    'foot' (boulders at the cliff foot, 2.5-9 m, z -3..-0.5), 'rim' (outcrops on the land top 2-12 m inside the shore, only on
    lie names in `allow`, z play_z-0.3). dist=(lo, hi) metres from the terrain boundary loops; kinds: names for place_rock;
    scale: (lo, hi) uniform scale range (default per zone: sea 0.4-0.85, foot 0.5-1.0, rim 0.7-1.2; CLIFF_ROCK is 17 m wide at 1.0);
    min_gap: minimum spacing between scattered rocks; avoid: [(x_m, y_m, radius_m), ...] keep-out discs; companion: chance
    of a second boulder next to a sea stack. Needs build_terrain first. Returns the objects."""
    if not D.rock_lib:
        import_rocks(D)
    rnd = random.Random(seed * 1000003 + D.number)
    defaults = {"sea": ((8.0, 28.0), ("CLIFF", "LARGE"), (-4.0, -1.0), (0.4, 0.85)),
                "foot": ((2.5, 9.0), ("LARGE", "MEDIUM", "SMALL"), (-3.0, -0.5), (0.5, 1.0)),
                "rim": ((2.0, 12.0), ("SMALL", "MEDIUM", "LARGE"), None, (0.7, 1.2))}
    d_lo_hi, k_def, z_def, s_def = defaults[zone]
    dist = dist or d_lo_hi
    kinds = kinds or k_def
    z = z or z_def
    scale = scale or s_def
    bx0, by0, bx1, by1 = _shore_bbox_m(D, dist[1] + 4)
    N = tries
    X = np.array([rnd.uniform(bx0, bx1) for _ in range(N)])
    Y = np.array([rnd.uniform(by0, by1) for _ in range(N)])
    sd = signed_dist_m(D, X, Y)
    codes = lie_codes_m(D, X, Y)
    if zone in ("sea", "foot"):
        ok = (sd >= dist[0]) & (sd <= dist[1])
    else:
        ok = (-sd >= dist[0]) & (-sd <= dist[1]) & np.isin(codes, [LIE_NAMES.index(a) for a in allow])
    placed, objs = [], []
    for i in np.nonzero(ok)[0]:
        if len(placed) >= count:
            break
        p = (float(X[i]), float(Y[i]))
        if any(math.hypot(p[0] - q[0], p[1] - q[1]) < min_gap for q in placed):
            continue
        if any(math.hypot(p[0] - ax, p[1] - ay) < ar for ax, ay, ar in avoid):
            continue
        k = rnd.choice(kinds)
        s = rnd.uniform(*scale)
        zz = rnd.uniform(*z) if z is not None else D.play_z - 0.3 * s
        objs.append(place_rock(D, k, (p[0], p[1], zz), (s, s * rnd.uniform(0.8, 1.1), s * rnd.uniform(0.7, 1.1)),
                               (rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15), rnd.uniform(0, math.tau))))
        placed.append(p)
        if zone == "sea" and rnd.random() < companion:
            ang = rnd.uniform(0, math.tau)
            q = (p[0] + math.cos(ang) * rnd.uniform(5, 9), p[1] + math.sin(ang) * rnd.uniform(5, 9))
            if signed_dist_m(D, np.array([q[0]]), np.array([q[1]]))[0] > 2.0:
                s2 = rnd.uniform(0.6, 1.0)
                objs.append(place_rock(D, "LARGE", (q[0], q[1], rnd.uniform(-1.5, 0.3)), (s2, s2, s2)))
    return objs


# ----------------------------------------------------------------------------- gameplay markers (phase9_10 builders)
def _box(bm, cx, cy, cz, sx, sy, sz, mat):
    r = bmesh.ops.create_cube(bm, size=1.0)
    verts = r["verts"]
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=verts)
    bmesh.ops.translate(bm, vec=(cx, cy, cz), verts=verts)
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mat
        f.smooth = False
    return verts


def _cylinder(bm, r, h, segs, mat, z0=0.0):
    ring0 = [bm.verts.new((math.cos(a) * r, math.sin(a) * r, z0)) for a in [math.tau * i / segs for i in range(segs)]]
    ring1 = [bm.verts.new((math.cos(a) * r, math.sin(a) * r, z0 + h)) for a in [math.tau * i / segs for i in range(segs)]]
    faces = [bm.faces.new((ring0[i], ring0[(i + 1) % segs], ring1[(i + 1) % segs], ring1[i])) for i in range(segs)]
    faces.append(bm.faces.new(list(reversed(ring0))))
    faces.append(bm.faces.new(ring1))
    for f in faces:
        f.material_index = mat
        f.smooth = False


def _build_object(D, name, col, builder, mats, loc=(0, 0, 0), rot_z=0.0):
    ob = new_mesh_object(name, col)
    bm = bmesh.new()
    builder(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for mt in mats:
        ob.data.materials.append(mt)
    bm.to_mesh(ob.data)
    bm.free()
    ob.location = loc
    ob.rotation_euler = (0, 0, rot_z)
    D.objects[name] = ob
    return ob


def build_gameplay(D):
    """HOLE_CUP, FLAG_POLE, FLAG, TEE_MARKER_1/2, BALL_START and the empties MARKER_TEE, MARKER_PIN, MARKER_UP exactly as
    phase9_10: MARKER_TEE at Blender (0, 0, play_z + 0.24), MARKER_PIN at the pin in metres (z play_z + 0.26), MARKER_UP
    50 m straight above MARKER_TEE. Unity hides FLAG, FLAG_POLE, HOLE_CUP, BALL_START and the three markers."""
    pz = D.play_z
    px, py = m(*D.hole.pin)
    pin_z = pz + Z_GREEN
    tz = pz + Z_TEE
    M = D.mats

    _build_object(D, "HOLE_CUP", "GAMEPLAY", lambda bm: _cylinder(bm, 0.7, 0.05, 12, 0, z0=0.0), [M["MAT_CUP"]],
                  loc=(px, py, pin_z + 0.01))

    def flag(bm):
        _cylinder(bm, 0.16, 10.0, 6, 0)
    _build_object(D, "FLAG_POLE", "GAMEPLAY", flag, [M["MAT_POLE"]], loc=(px, py, pin_z), rot_z=math.radians(-30))
    remove_object("FLAG")
    fl = new_mesh_object("FLAG", "GAMEPLAY")
    bm = bmesh.new()
    a_ = bm.verts.new((0.16, 0, 10.0))
    b_ = bm.verts.new((0.16, 0, 7.6))
    c_ = bm.verts.new((4.6, 0, 8.8))
    bm.faces.new((a_, b_, c_))
    bm.to_mesh(fl.data)
    bm.free()
    fl.data.materials.append(M["MAT_FLAG"])
    fl.location = (px, py, pin_z)
    fl.rotation_euler = (0, 0, math.radians(-30))
    D.objects["FLAG"] = fl

    d0 = Vector(m(*D.hole.center[1])) - Vector(m(*D.hole.center[0]))
    d0.normalize()
    across = Vector((d0.y, -d0.x))                     # right of the play direction
    yaw = math.atan2(d0.y, d0.x) - math.pi / 2
    for i, dx in enumerate((-3.4, 3.4)):
        _build_object(D, f"TEE_MARKER_{i + 1}", "GAMEPLAY", lambda bm: _box(bm, 0, 0, 0.45, 0.9, 0.9, 0.9, 0),
                      [M["MAT_POLE"]], loc=(across.x * dx, across.y * dx, tz), rot_z=yaw)
    remove_object("BALL_START")
    ball = new_mesh_object("BALL_START", "GAMEPLAY")
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.35)
    for f in bm.faces:
        f.smooth = True
    bm.to_mesh(ball.data)
    bm.free()
    ball.data.materials.append(M["MAT_BALL"])
    ball.location = (0.0, 0.0, tz + 0.35)
    D.objects["BALL_START"] = ball
    root = get_root()
    for name, loc in (("MARKER_TEE", (0.0, 0.0, tz)), ("MARKER_PIN", (px, py, pin_z)), ("MARKER_UP", (0.0, 0.0, tz + 50.0))):
        remove_object(name)
        e = bpy.data.objects.new(name, None)
        e.empty_display_type = 'SPHERE'
        e.empty_display_size = 2
        bpy.context.scene.collection.objects.link(e)
        link_to(e, "GAMEPLAY")
        e.parent = root
        e.location = loc
        D.objects[name] = e
    _log(D, f"gameplay: tee marker (0, 0, {tz:.2f}), pin ({px:.2f}, {py:.2f}, {pin_z:.2f})")


# ----------------------------------------------------------------------------- cameras, light, render
def _make_camera(D, name, col="GAMEPLAY"):
    remove_object(name)
    cam = bpy.data.cameras.new(name)
    ob = bpy.data.objects.new(name, cam)
    bpy.context.scene.collection.objects.link(ob)
    link_to(ob, col)
    ob.parent = get_root()
    cam.clip_start = 0.5
    cam.clip_end = 6000
    return ob


def _aim(cam, loc, target, lens=None):
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    if lens:
        cam.data.lens = lens


def _sun(D, name, energy, rot_deg, angle_deg, shadow=True, color=(1, 1, 1)):
    remove_object(name)
    data = bpy.data.lights.new(name, 'SUN')
    data.energy = energy
    data.angle = math.radians(angle_deg)
    try:
        data.use_shadow = shadow
    except Exception:
        pass
    data.color = color
    ob = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(ob)
    link_to(ob, "LIGHTING")
    ob.parent = get_root()
    ob.location = (0, 0, 300)
    ob.rotation_euler = [math.radians(a) for a in rot_deg]
    return ob


def build_cameras_and_light(D, overview_rot_deg=(50.0, 0.0, 20.0), margin=1.10):
    """CAM_HOLE_OVERVIEW (orthographic, elevated 40 deg, yawed like hole 7, framed on the shore bounding box incl. the
    cliffs), CAM_TEE_VIEW, CAM_FAIRWAY_VIEW, CAM_GREEN_VIEW, SUN_KEY, SUN_FILL, the sky world colour and the EEVEE settings
    of phase11_13 (every EEVEE attribute guarded: Blender 5 renamed/removed several)."""
    sc = bpy.context.scene
    pz = D.play_z
    # lights as phase 12
    _sun(D, "SUN_KEY", 3.6, (50, 0, 28), 3.0, True, (1.0, 0.97, 0.9))
    _sun(D, "SUN_FILL", 0.9, (60, 0, 222), 25.0, False, (0.82, 0.9, 1.0))
    world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
    sc.world = world
    if world.node_tree is None:
        world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = rgb(160, 210, 248)
    bg.inputs[1].default_value = 0.9
    sc.render.engine = 'BLENDER_EEVEE'
    try:
        sc.view_settings.view_transform = 'Standard'
        sc.view_settings.look = 'None'
    except Exception:
        pass
    sc.view_settings.exposure = 0.0
    sc.view_settings.gamma = 1.0
    sc.render.film_transparent = False
    ev = sc.eevee
    for attr, val in (("taa_render_samples", 32), ("use_shadows", True), ("use_gtao", True), ("gtao_distance", 6.0),
                      ("use_raytracing", False), ("shadow_ray_count", 2), ("shadow_step_count", 4),
                      ("use_fast_gi", True), ("fast_gi_distance", 8.0)):
        if hasattr(ev, attr):
            try:
                setattr(ev, attr, val)
            except Exception:
                pass

    # overview: frame the bounding box (top at play_z, cliff base at z -6) in camera space
    cam = _make_camera(D, "CAM_HOLE_OVERVIEW")
    cam.data.type = 'ORTHO'
    rot = Matrix.Rotation(math.radians(overview_rot_deg[2]), 3, 'Z') @ Matrix.Rotation(math.radians(overview_rot_deg[0]), 3, 'X')
    cam.rotation_euler = rot.to_euler()
    right, up, view = rot @ Vector((1, 0, 0)), rot @ Vector((0, 1, 0)), rot @ Vector((0, 0, -1))
    corners = [Vector((x, y, z)) for x, y in (m(a, b) for a, b in D.hole.shore) for z in (pz + 10.0, -6.0)]
    rs = [c.dot(right) for c in corners]
    us = [c.dot(up) for c in corners]
    cr, cu = (min(rs) + max(rs)) / 2, (min(us) + max(us)) / 2
    cam.data.ortho_scale = max(max(rs) - min(rs), max(us) - min(us)) * margin
    cam.location = right * cr + up * cu - view * 1500.0
    cam.data.clip_end = 6000
    # tee / fairway / green views (hole 7 style, in plan along the centerline legs)
    c0 = Vector(m(*D.hole.center[0]))
    c1 = Vector(m(*D.hole.center[1]))
    d0 = (c1 - c0).normalized()
    cn = Vector(m(*D.hole.center[-1]))
    cp = Vector(m(*D.hole.center[-2]))
    dn = (cn - cp).normalized()
    cam = _make_camera(D, "CAM_TEE_VIEW")
    _aim(cam, (c0.x - d0.x * 24, c0.y - d0.y * 24, pz + 5.5), (c0.x + d0.x * 40, c0.y + d0.y * 40, pz + 4.0), lens=26)
    mid = Vector(m(*D.hole.center[len(D.hole.center) // 2]))
    cam = _make_camera(D, "CAM_FAIRWAY_VIEW")
    _aim(cam, (mid.x - 30, mid.y - 40, pz + 14), (mid.x, mid.y + 40, pz + 4), lens=30)
    cam = _make_camera(D, "CAM_GREEN_VIEW")
    _aim(cam, (cn.x - dn.x * 55, cn.y - dn.y * 55, pz + 8), (cn.x, cn.y, pz + 1.5), lens=30)
    sc.camera = bpy.data.objects["CAM_HOLE_OVERVIEW"]


def _preview_path(D):
    if D.out_dir:
        return os.path.join(D.out_dir, f"hole_{D.tag}_overview.png")
    return os.path.join(BLENDER_DIR, "previews", f"hole_{D.tag}_overview.png")


def _guard_target(D, path):
    if D.number == 7 or os.path.basename(path).startswith("hole_07"):
        raise RuntimeError(f"refusing to write {path}: hole 07 files are never touched")


def render_camera(cam_name, out_path, res=(1400, 1400), samples=16):
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    sc = bpy.context.scene
    sc.camera = bpy.data.objects[cam_name]
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.filepath = out_path
    sc.render.image_settings.file_format = 'PNG'
    if hasattr(sc, "eevee"):
        try:
            sc.eevee.taa_render_samples = samples
        except Exception:
            pass
    bpy.ops.render.render(write_still=True)
    return out_path


def render_overview(D, path=None, res=(1400, 1400), samples=16):
    """One EEVEE still of CAM_HOLE_OVERVIEW (NOT a flyover) to blender/previews/hole_NN_overview.png (or out_dir)."""
    t0 = time.time()
    path = path or _preview_path(D)
    _guard_target(D, path)
    render_camera("CAM_HOLE_OVERVIEW", path, res, samples)
    D.timings["render"] = time.time() - t0
    _log(D, f"overview rendered {path} ({D.timings['render']:.1f}s)")
    return path


# ----------------------------------------------------------------------------- save / export / stats
def _blend_path(D):
    return os.path.join(D.out_dir if D.out_dir else BLENDER_DIR, f"hole_{D.tag}.blend")


def _fbx_path(D):
    return os.path.join(D.out_dir if D.out_dir else COURSE_DIR, f"hole_{D.tag}.fbx")


def save(D):
    """Save the scene to blender/hole_NN.blend (or out_dir/hole_NN.blend)."""
    path = _blend_path(D)
    _guard_target(D, path)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=path)
    return path


def _export_objects(D):
    """Root empty + every MESH/EMPTY in COURSE, ENVIRONMENT (incl. ROCKS), STRUCTURES, GAMEPLAY; never ASSET_LIBRARY."""
    out = []
    seen = set()

    def walk(col):
        for ob in col.objects:
            if ob.name not in seen and ob.type in ('MESH', 'EMPTY'):
                seen.add(ob.name)
                out.append(ob)
        for ch in col.children:
            walk(ch)
    for cn in ("COURSE", "ENVIRONMENT", "STRUCTURES", "GAMEPLAY"):
        c = bpy.data.collections.get(cn)
        if c:
            walk(c)
    root = bpy.data.objects.get(D.root_name)
    if root and root.name not in seen:
        out.append(root)
    return out


def export_fbx(D, path=None):
    """Export hole_NN.fbx (Unity/Assets/Resources/Course/ unless out_dir/path): only COURSE/ENVIRONMENT/STRUCTURES/GAMEPLAY
    objects and the root (no cameras, lights, library originals). Settings: object_types {'MESH','EMPTY'}, axis_forward '-Z',
    axis_up 'Y', apply_unit_scale True, apply_scale_options 'FBX_SCALE_ALL', bake_anim False, use_mesh_modifiers True,
    add_leaf_bones False, path_mode 'AUTO', use_selection True (GlobalSettings equal to hole_07.fbx: UnitScaleFactor 100).
    Also writes hole_NN.fbx.meta (copy of hole_07.fbx.meta, fresh random guid) ONLY when that .meta does not exist."""
    t0 = time.time()
    path = path or _fbx_path(D)
    _guard_target(D, path)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    vl = bpy.context.view_layer
    for ob in vl.objects:
        ob.select_set(False)
    chosen = _export_objects(D)
    for ob in chosen:
        ob.select_set(True)
    vl.objects.active = bpy.data.objects.get(D.root_name)
    import contextlib
    import io
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'}, axis_forward='-Z',
                                 axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_anim=False,
                                 use_mesh_modifiers=True, add_leaf_bones=False, path_mode='AUTO')
    noise = 0
    for ln in buf.getvalue().splitlines():
        if "Cannot register a valid material index" in ln:    # the exporter says this for every extra instance of a shared rock mesh
            noise += 1
        elif ln.strip():
            print(ln)
    if noise:
        print(f"(fbx exporter: {noise} harmless 'material index' warnings for instanced rocks sharing a mesh, suppressed)")
    meta = path + ".meta"
    if not os.path.exists(meta):
        with open(HOLE07_META) as f:
            txt = f.read()
        lines = []
        for ln in txt.splitlines():
            lines.append(f"guid: {uuid.uuid4().hex}" if ln.startswith("guid:") else ln)
        with open(meta, "w") as f:
            f.write("\n".join(lines) + "\n")
    D.timings["fbx"] = time.time() - t0
    _log(D, f"fbx exported {path} ({os.path.getsize(path) // 1024} KB, {len(chosen)} objects)")
    return path


def stats(D):
    """Object and triangle counts of what export_fbx would write: {'objects', 'meshes', 'empties', 'tris', 'ground_tris',
    'by_object': {name: (verts, polys, tris)}, 'timings': {...}}."""
    by, tris, gt = {}, 0, 0
    objs = _export_objects(D)
    for ob in objs:
        if ob.type != 'MESH':
            continue
        me = ob.data
        me.calc_loop_triangles()
        t = len(me.loop_triangles)
        by[ob.name] = (len(me.vertices), len(me.polygons), t)
        tris += t
        if ob.name.startswith(GROUND_PREFIXES):
            gt += t
    return {"objects": len(objs), "meshes": len(by), "empties": len(objs) - len(by), "tris": tris, "ground_tris": gt,
            "by_object": by, "timings": dict(D.timings)}


def build_base(D, water=None, terrain=None, rocks=True):
    """Convenience: build_terrain, build_play_surfaces, build_water (water = kwargs dict), import_rocks, build_gameplay,
    build_cameras_and_light. Hole scripts then add their rocks/arch/ruin and call render_overview/save/export_fbx."""
    build_terrain(D, **(terrain or {}))
    build_play_surfaces(D)
    build_water(D, **(water or {}))
    if rocks:
        import_rocks(D)
    build_gameplay(D)
    build_cameras_and_light(D)
    return D
