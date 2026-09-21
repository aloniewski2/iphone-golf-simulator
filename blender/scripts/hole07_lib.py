"""Hole 07 build library — shared helpers used by every phase script.
Coordinates: metres. +Y = north (toward green), +X = east (ocean side). Water level z=0.
"""
import bpy, bmesh, math, random
from mathutils import Vector, geometry

BLEND_PATH = "/Users/andrewloniewski/iPhoneGolfSimulator/blender/hole_07.blend"
PREVIEW_DIR = "/Users/andrewloniewski/iPhoneGolfSimulator/blender/previews"

COLLECTIONS = ["COURSE", "ENVIRONMENT", "STRUCTURES", "GAMEPLAY", "LIGHTING", "REFERENCE"]

# ---------------------------------------------------------------- scene / collections
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
    root = bpy.data.objects.get("HOLE_07_ROOT")
    if root is None:
        root = bpy.data.objects.new("HOLE_07_ROOT", None)
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

# ---------------------------------------------------------------- colours / materials
def srgb_to_linear(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def rgb(r, g, b):
    return (srgb_to_linear(r), srgb_to_linear(g), srgb_to_linear(b), 1.0)

def get_material(name, color, roughness=0.9, metallic=0.0, emission=None, alpha=1.0):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
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
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
    mat.diffuse_color = color
    return mat

def assign(ob, mat):
    ob.data.materials.clear()
    ob.data.materials.append(mat)

# ---------------------------------------------------------------- 2-D curve helpers
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
    """kidney/blob outline — coarse noisy ellipse smoothed by Chaikin."""
    return chaikin(ellipse(cx, cy, rx, ry, rot, n, wobble, seed), smooth)

# ---------------------------------------------------------------- triangulated filled polygon
def fill_polygon(outline, spacing, height_fn, z_offset=0.0, margin=None):
    """Returns (verts3d, faces) — CDT of outline with an interior grid, z from height_fn."""
    poly = [Vector((p[0], p[1])) for p in outline]
    if signed_area(poly) < 0:
        poly.reverse()
    if margin is None:
        margin = spacing * 0.55
    xs = [p.x for p in poly]; ys = [p.y for p in poly]
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

def build_mesh(ob, verts, faces, smooth=True):
    me = ob.data
    me.clear_geometry()
    me.from_pydata(verts, [], faces)
    me.validate()
    me.update()
    for p in me.polygons:
        p.use_smooth = smooth
    return me

def surface_object(name, col_name, outline, spacing, height_fn, z_offset, mat, smooth=True):
    ob = new_mesh_object(name, col_name)
    v, f = fill_polygon(outline, spacing, height_fn, z_offset)
    build_mesh(ob, v, f, smooth)
    assign(ob, mat)
    return ob

# ---------------------------------------------------------------- save / render
def save():
    import os
    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    return BLEND_PATH

def render_camera(cam_name, out_path, res=(1400, 1400), samples=16):
    import os
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    sc = bpy.context.scene
    sc.camera = bpy.data.objects[cam_name]
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.filepath = out_path
    sc.render.image_settings.file_format = 'PNG'
    if hasattr(sc, "eevee"):
        sc.eevee.taa_render_samples = samples
    bpy.ops.render.render(write_still=True)
    return out_path
