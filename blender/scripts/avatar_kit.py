"""The avatar kit's shared tools: smooth low-poly geometry, flat-colour "role" materials in a soft clay finish,
snapping small pieces onto the head's surface, and the studio the mock renders are shot in.

The Hero is a kit, not a model: one skeleton, one body, one head, and every haircut, hat, pair of glasses, beard,
top, pair of shorts and pair of shoes is its own small mesh made against the same head and the same body. Nothing
is textured. A part's colours are its *material roles* (Hero_Skin, Hero_Hair, Hero_Top ...): the game tints a role,
so one mesh is every colour. See docs/avatar-kit.md.

Units are metres, Blender axes (the face looks down -Y, Z up), feet on z = 0.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree


# ------------------------------------------------------------------------------------------------ colour roles
def lin(hex_or_rgb):
    """sRGB (#rrggbb or 0-255 / 0-1 triple) to linear."""
    if isinstance(hex_or_rgb, str):
        h = hex_or_rgb.lstrip("#"); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    else:
        c = [v / 255 if max(hex_or_rgb) > 1 else v for v in hex_or_rgb]
    return tuple(((v + 0.055) / 1.055) ** 2.4 if v > 0.04045 else v / 12.92 for v in c)


# role -> (default sRGB, roughness, kind). kind: skin | hair | cloth | hard | gloss | lens | flat
ROLES = {
    "Hero_Skin":     ("#E8A074", 0.52, "skin"),
    "Hero_Ear":      ("#D9826A", 0.55, "skin"),       # the inside of the ear, the nose pad: skin, a touch pinker
    "Hero_Blush":    ("#F0806E", 0.6, "skin"),
    "Hero_Hair":     ("#E0A43A", 0.38, "hair"),
    "Hero_HairB":    ("#C07F22", 0.38, "hair"),       # the darker streak
    "Hero_Eye":      ("#1C1412", 0.12, "gloss"),
    "Hero_EyeGlint": ("#FFFFFF", 0.0, "glint"),
    "Hero_MouthIn":  ("#7A1F22", 0.35, "skin"),
    "Hero_Tongue":   ("#E0606A", 0.35, "skin"),
    "Hero_Teeth":    ("#F7F2EA", 0.3, "hard"),
    "Hero_Top":      ("#F2F3F5", 0.78, "cloth"),      # the polo
    "Hero_TopTrim":  ("#1B2F6B", 0.78, "cloth"),      # collar and cuffs
    "Hero_Accent":   ("#F0501A", 0.70, "cloth"),
    "Hero_Bottom":   ("#1B2F6B", 0.78, "cloth"),
    "Hero_Sock":     ("#F2F3F5", 0.8, "cloth"),
    "Hero_Shoe":     ("#F4F4F6", 0.45, "hard"),
    "Hero_ShoeSole": ("#A9B0C4", 0.6, "hard"),
    "Hero_Lace":     ("#C9CFE2", 0.7, "cloth"),
    "Hero_HatA":     ("#1B2F6B", 0.75, "cloth"),      # a hat's main colour
    "Hero_HatB":     ("#F0501A", 0.75, "cloth"),      # its band, bill underside, button
    "Hero_Frame":    ("#23262E", 0.3, "hard"),        # glasses frames
    "Hero_Lens":     ("#9FD3FF", 0.05, "lens"),
    "Hero_LensDark": ("#1B2230", 0.06, "gloss"),
    "Hero_Metal":    ("#D8B45A", 0.25, "hard"),
}


def role(name, color=None):
    """The material for a role (one per name), in its default colour unless `color` (sRGB) is given."""
    m = bpy.data.materials.get(name)
    if m and color is None: return m
    m = m or bpy.data.materials.new(name)
    hexc, rough, kind = ROLES.get(name, ("#CCCCCC", 0.6, "flat"))
    c = lin(color or hexc)
    m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    out = nt.nodes["Material Output"]
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(b.outputs["BSDF"], out.inputs["Surface"])
    def setp(k, v):
        if k in b.inputs: b.inputs[k].default_value = v
    setp("Base Color", (*c, 1)); setp("Roughness", rough); setp("Specular IOR Level", 0.35)
    if kind == "skin":
        setp("Subsurface Weight", 0.35); setp("Subsurface Scale", 0.028)
        setp("Subsurface Radius", (1.0, 0.35, 0.22)); setp("Roughness", rough)
    elif kind == "hair":
        setp("Specular IOR Level", 0.55); setp("Coat Weight", 0.25); setp("Coat Roughness", 0.3)
    elif kind == "cloth":
        setp("Sheen Weight", 0.35); setp("Sheen Roughness", 0.5); setp("Specular IOR Level", 0.15)
    elif kind == "hard":
        setp("Coat Weight", 0.3); setp("Coat Roughness", 0.15)
    elif kind == "gloss":
        setp("Coat Weight", 0.6); setp("Coat Roughness", 0.05); setp("Specular IOR Level", 0.6)
    elif kind == "glint":
        setp("Emission Color", (1, 1, 1, 1)); setp("Emission Strength", 4.0)
    elif kind == "lens":
        setp("Alpha", 0.30); setp("Roughness", 0.04); setp("Specular IOR Level", 1.0)
        m.blend_method = 'BLEND' if hasattr(m, "blend_method") else None
    m.diffuse_color = (*c, 1)
    return m


def recolor(name, srgb):
    """Change a role's colour (every mesh that uses it follows)."""
    role(name, srgb)


def _hex(h): h = h.lstrip("#"); return [int(h[i:i + 2], 16) for i in (0, 2, 4)]


def mix_hex(a, b, t):
    A, B_ = _hex(a), _hex(b)
    return "#%02X%02X%02X" % tuple(int(round(x + (y - x) * t)) for x, y in zip(A, B_))


def skin_tone(srgb):
    """Every skin role from one colour: the skin, the pinker ear and hollow, the blush."""
    role("Hero_Skin", srgb); role("Hero_Ear", mix_hex(srgb, "#C04A4A", 0.30)); role("Hero_Blush", mix_hex(srgb, "#EE4F62", 0.45))


def hair_tone(srgb):
    """Hair and its darker streak from one colour (the brows follow the hair)."""
    role("Hero_Hair", srgb); role("Hero_HairB", mix_hex(srgb, "#000000", 0.22))


# ------------------------------------------------------------------------------------------------ geometry
def catmull(pts, per=5):
    pts = [Vector(p) for p in pts]
    if len(pts) < 3: return pts
    p = [pts[0]] + pts + [pts[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for k in range(per):
            t = k / per; t2 = t * t; t3 = t2 * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(pts[-1])
    return out


def _super(th, rx, ry, p):
    c, s = math.cos(th), math.sin(th); e = 2.0 / p
    return rx * math.copysign(abs(c) ** e, c), ry * math.copysign(abs(s) ** e, s)


def new_obj(name, me, mat=None, parent=None):
    for f in me.polygons: f.use_smooth = True
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    if mat is not None:
        for m in (mat if isinstance(mat, (list, tuple)) else [mat]): me.materials.append(role(m) if isinstance(m, str) else m)
    return o


def tube(name, pts, radii, mat="Hero_Skin", segs=20, p=2.0, per=5, cap0=True, cap1=True, ref=Vector((0, 1, 0)), cap_rings=3, flat0=False, flat1=False):
    """A smooth closed tube along the path `pts` (smoothed through), with (rx, ry) radii at each point (eased
    between), a superellipse cross-section of exponent p (2 round, more boxy) and rounded ends. `radii` may be one
    (rx, ry) or a number for a constant tube."""
    pts = [Vector(v) for v in pts]
    if isinstance(radii, (int, float)): radii = [(radii, radii)]
    else: radii = [(r, r) if isinstance(r, (int, float)) else tuple(r) for r in radii]
    if len(radii) == 1: radii = radii * len(pts)
    path = catmull(pts, per)
    n = len(path)
    lens = [0.0]
    for a, b in zip(path, path[1:]): lens.append(lens[-1] + (b - a).length)
    tot = lens[-1] or 1.0
    acc, cl = 0.0, [0.0]
    for a, b in zip(pts, pts[1:]): acc += (b - a).length; cl.append(acc)
    def radius_at(s):
        s = s * acc
        for i in range(len(cl) - 1):
            if cl[i] <= s <= cl[i + 1] + 1e-9:
                t = (s - cl[i]) / max(cl[i + 1] - cl[i], 1e-9); t = t * t * (3 - 2 * t)
                a, b = radii[i], radii[i + 1]
                return a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
        return radii[-1]
    bm = bmesh.new()
    def ring(c, T, rx, ry):
        T = T.normalized(); u = T.cross(ref(c) if callable(ref) else ref)
        if u.length < 1e-4: u = T.cross(Vector((1, 0, 0)))
        u.normalize(); v = T.cross(u).normalized()
        out = []
        for j in range(segs):
            a, b = _super(2 * math.pi * j / segs, rx, ry, p)
            out.append(bm.verts.new(c + u * a + v * b))
        return out
    rings = []
    for i in range(n):
        T = path[min(n - 1, i + 1)] - path[max(0, i - 1)]
        rx, ry = radius_at(lens[i] / tot)
        rings.append((ring(path[i], T, rx, ry), path[i], T, rx, ry))
    def cap(start):
        _, c, T, rx, ry = rings[0] if start else rings[-1]
        T = T.normalized() * (-1 if start else 1)
        rs = []
        if (flat0 if start else flat1): return rs, bm.verts.new(c)        # closed flat across the ring, not rounded
        for k in range(1, cap_rings + 1):
            a = (math.pi / 2) * k / (cap_rings + 1)
            rs.append(ring(c + T * (max(rx, ry) * math.sin(a)), T * (-1 if start else 1), rx * math.cos(a), ry * math.cos(a)))
        return rs, bm.verts.new(c + T * max(rx, ry))
    seq = [r[0] for r in rings]; pole0 = pole1 = None
    if cap0: rs, pole0 = cap(True); seq = list(reversed(rs)) + seq
    if cap1: rs, pole1 = cap(False); seq = seq + rs
    for a, b in zip(seq, seq[1:]):
        for j in range(segs): bm.faces.new((a[j], a[(j + 1) % segs], b[(j + 1) % segs], b[j]))
    if pole0:
        for j in range(segs): bm.faces.new((seq[0][(j + 1) % segs], seq[0][j], pole0))
    if pole1:
        for j in range(segs): bm.faces.new((seq[-1][j], seq[-1][(j + 1) % segs], pole1))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return new_obj(name, me, mat)


def blob(name, center, radii, mat="Hero_Skin", rot=(0, 0, 0), segs=16, rings=10, p=2.0):
    """An ellipsoid (p > 2: a rounder-cornered box). Rotation is Euler XYZ in radians, about the centre."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    if p != 2.0:
        e = 2.0 / p
        for v in bm.verts:
            l = v.co.length
            if l > 1e-6:
                u = v.co / l
                v.co = Vector((math.copysign(abs(u.x) ** e, u.x), math.copysign(abs(u.y) ** e, u.y), math.copysign(abs(u.z) ** e, u.z)))
    bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
    R = Matrix.Rotation(rot[2], 3, 'Z') @ Matrix.Rotation(rot[1], 3, 'Y') @ Matrix.Rotation(rot[0], 3, 'X')
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=R, verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return new_obj(name, me, mat)


def apply_modifiers(o):
    if not o.modifiers: return
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    mats = list(o.data.materials)
    o.modifiers.clear(); o.data = me
    for m in mats: me.materials.append(m)


def join(name, objs, smooth=True):
    """One mesh from many (their material slots merged by name)."""
    for o in objs: apply_modifiers(o)
    bm = bmesh.new(); mats = []
    for o in objs:
        slot = []
        for m in o.data.materials:
            if m not in mats: mats.append(m)
            slot.append(mats.index(m))
        me = o.data.copy(); me.transform(o.matrix_world)
        b2 = bmesh.new(); b2.from_mesh(me)
        for f in b2.faces: f.material_index = slot[f.material_index] if f.material_index < len(slot) else 0
        tmp = bpy.data.meshes.new("t"); b2.to_mesh(tmp); b2.free()
        bm.from_mesh(tmp); bpy.data.meshes.remove(tmp); bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    for f in me.polygons: f.use_smooth = smooth
    out = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(out)
    for o in objs: bpy.data.objects.remove(o, do_unlink=True)
    return out


def mirror_x(objs, name_suffix="_L"):
    """Mirrored copies across x = 0 (normals kept outward)."""
    out = []
    for o in objs:
        me = o.data.copy(); me.transform(Matrix.Scale(-1, 4, Vector((1, 0, 0))))
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.reverse_faces(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
        n = bpy.data.objects.new(o.name + name_suffix, me); bpy.context.scene.collection.objects.link(n); out.append(n)
    return out


def flip_normals(o):
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.reverse_faces(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()


def stats(o):
    return len(o.data.vertices), sum(len(p.vertices) - 2 for p in o.data.polygons)


def lathe(name, profile, mat="Hero_HatA", segs=24, sx=1.0, sy=1.0, center=(0, 0, 0), p=2.0):
    """A surface of revolution about the z axis from a profile [(radius, z) ...] (bottom to top; a radius of 0 at an
    end closes it with a pole), stretched by (sx, sy): the shape of most hats. p > 2 squares the cross-section."""
    bm = bmesh.new(); rings = []
    for r, z in profile:
        if r < 1e-6: rings.append(bm.verts.new((0, 0, z))); continue
        rings.append([bm.verts.new((sx * r * math.copysign(abs(math.cos(2 * math.pi * j / segs)) ** (2 / p), math.cos(2 * math.pi * j / segs)),
                                    sy * r * math.copysign(abs(math.sin(2 * math.pi * j / segs)) ** (2 / p), math.sin(2 * math.pi * j / segs)), z)) for j in range(segs)])
    for a, b in zip(rings, rings[1:]):
        if isinstance(a, list) and isinstance(b, list):
            for j in range(segs): bm.faces.new((a[j], a[(j + 1) % segs], b[(j + 1) % segs], b[j]))
        elif isinstance(a, list):
            for j in range(segs): bm.faces.new((a[j], a[(j + 1) % segs], b))
        elif isinstance(b, list):
            for j in range(segs): bm.faces.new((b[(j + 1) % segs], b[j], a))
    for v in bm.verts: v.co += Vector(center)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return new_obj(name, me, mat)


def ring_tube(name, center, rx, rz, r, mat="Hero_Frame", segs=28, sides=6, p=2.0, plane="xz"):
    """A closed loop of tube radius r shaped like an ellipse (p > 2: a rounded rectangle) of radii (rx, rz) in the
    x-z plane about `center`: a spectacle rim, a hair-tie, a band."""
    bm = bmesh.new(); rows = []
    for i in range(segs):
        th = 2 * math.pi * i / segs
        ex, ez = rx * math.copysign(abs(math.cos(th)) ** (2 / p), math.cos(th)), rz * math.copysign(abs(math.sin(th)) ** (2 / p), math.sin(th))
        c = Vector((ex, 0, ez))
        tx, tz = -rx * math.sin(th), rz * math.cos(th)
        T = Vector((tx, 0, tz)).normalized()
        out = Vector((T.z, 0, -T.x)).normalized()               # the ring's outward direction in its plane
        row = []
        for j in range(sides):
            ph = 2 * math.pi * j / sides
            row.append(bm.verts.new(c + out * (r * math.cos(ph)) + Vector((0, 1, 0)) * (r * math.sin(ph))))
        rows.append(row)
    for i in range(segs):
        a, b = rows[i], rows[(i + 1) % segs]
        for j in range(sides): bm.faces.new((a[j], a[(j + 1) % sides], b[(j + 1) % sides], b[j]))
    for v in bm.verts: v.co += Vector(center)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return new_obj(name, me, mat)


def ellipse_plate(name, center, rx, rz, mat="Hero_Lens", segs=20, thick=0.002, p=2.0):
    """A thin plate in the x-z plane (a lens)."""
    return lathe_plate(name, center, rx, rz, mat, segs, thick, p)


def lathe_plate(name, center, rx, rz, mat, segs, thick, p):
    bm = bmesh.new()
    front = [], []
    for k, y in enumerate((-thick / 2, thick / 2)):
        ring = [bm.verts.new((rx * math.copysign(abs(math.cos(2 * math.pi * i / segs)) ** (2 / p), math.cos(2 * math.pi * i / segs)), y,
                              rz * math.copysign(abs(math.sin(2 * math.pi * i / segs)) ** (2 / p), math.sin(2 * math.pi * i / segs)))) for i in range(segs)]
        front[k].extend(ring)
    cf = bm.verts.new((0, -thick / 2, 0)); cb = bm.verts.new((0, thick / 2, 0))
    a, b = front
    for i in range(segs):
        j = (i + 1) % segs
        bm.faces.new((cf, a[j], a[i])); bm.faces.new((cb, b[i], b[j])); bm.faces.new((a[i], a[j], b[j], b[i]))
    for v in bm.verts: v.co += Vector(center)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return new_obj(name, me, mat)


# ------------------------------------------------------------------------------------------------ the head's surface
class Surface:
    """A mesh to lay things on: ask where a ray from the front meets it, and how it faces there."""
    def __init__(self, obj):
        dg = bpy.context.evaluated_depsgraph_get(); ev = obj.evaluated_get(dg); me = ev.to_mesh()
        self.tree = BVHTree.FromPolygons([obj.matrix_world @ v.co for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
        ev.to_mesh_clear()

    def hit(self, x, z, y_from=-1.0, d=Vector((0, 1, 0))):
        """(point, normal) where the line through (x, y_from, z) along d first meets the surface."""
        r = self.tree.ray_cast(Vector((x, y_from, z)), d, 3.0)
        if r[0] is None: return None, None
        return r[0], r[1].normalized()

    def ray(self, origin, direction, dist=3.0):
        r = self.tree.ray_cast(Vector(origin), Vector(direction).normalized(), dist)
        return (r[0], r[1].normalized()) if r[0] is not None else (None, None)

    def nearest(self, p):
        r = self.tree.find_nearest(Vector(p))
        return (r[0], r[1].normalized()) if r[0] is not None else (None, None)


def disc(name, outline, surf, lift=0.002, rings=2, mat="Hero_MouthIn", y_from=-1.0, flat_centre=None):
    """A thin patch shaped like the 2D outline [(x, z) ...] (convex or star-shaped), laid on the surface
    (projected from the front, raised by `lift`): a mouth, teeth, a tongue, a blush, an eye-white."""
    cx = sum(p[0] for p in outline) / len(outline); cz = sum(p[1] for p in outline) / len(outline)
    bm = bmesh.new()
    centre = bm.verts.new((cx, 0, cz)); ringv = [[centre]]
    for k in range(1, rings + 1):
        t = k / rings
        ringv.append([bm.verts.new((cx + (x - cx) * t, 0, cz + (z - cz) * t)) for x, z in outline])
    n = len(outline)
    for j in range(n): bm.faces.new((centre, ringv[1][(j + 1) % n], ringv[1][j]))
    for k in range(1, rings):
        for j in range(n): bm.faces.new((ringv[k][j], ringv[k][(j + 1) % n], ringv[k + 1][(j + 1) % n], ringv[k + 1][j]))
    for v in bm.verts:
        p, nrm = surf.hit(v.co.x, v.co.z, y_from)
        if p is not None: v.co = p + nrm * lift
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # the patch must face the viewer (-Y)
    if sum(f.normal.y for f in bm.faces) > 0: bmesh.ops.reverse_faces(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return new_obj(name, me, mat)


def ellipse_pts(cx, cz, rx, rz, n=20, rot=0.0):
    c, s = math.cos(rot), math.sin(rot)
    return [(cx + rx * math.cos(2 * math.pi * i / n) * c - rz * math.sin(2 * math.pi * i / n) * s,
             cz + rx * math.cos(2 * math.pi * i / n) * s + rz * math.sin(2 * math.pi * i / n) * c) for i in range(n)]


# ------------------------------------------------------------------------------------------------ the studio
def studio(rim=True, floor=False):
    """Soft clay-render lighting: a big warm key, a cool fill, a rim, an overcast world, a shadow-catcher floor."""
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 64; sc.cycles.use_denoising = True
    try: sc.cycles.denoiser = 'OPENIMAGEDENOISE'
    except Exception: pass
    sc.render.film_transparent = True
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    nt = w.node_tree; bg = nt.nodes["Background"]
    bg.inputs["Color"].default_value = (0.92, 0.94, 1.0, 1); bg.inputs["Strength"].default_value = 0.42
    def area(name, loc, target, energy, size, color):
        d = bpy.data.lights.new(name, 'AREA'); d.energy = energy; d.size = size; d.color = color
        o = bpy.data.objects.new(name, d); sc.collection.objects.link(o); o.location = loc
        o.rotation_euler = (Vector(target) - Vector(loc)).normalized().to_track_quat('-Z', 'Y').to_euler()
    area("key", (-1.6, -2.6, 2.9), (0, 0, 0.9), 150, 2.4, (1.0, 0.95, 0.88))
    area("fill", (2.4, -2.0, 1.3), (0, 0, 0.9), 45, 2.8, (0.86, 0.92, 1.0))
    if rim: area("rim", (1.4, 2.4, 2.4), (0, 0, 1.0), 110, 1.6, (1.0, 1.0, 1.0))
    if floor:                     # a shadow-catcher under the feet (the mock boards fake a softer contact shadow instead)
        fl = bpy.data.objects.new("floor", bpy.data.meshes.new("floor")); sc.collection.objects.link(fl)
        bm = bmesh.new(); bmesh.ops.create_circle(bm, cap_ends=True, radius=1.8, segments=48); bm.to_mesh(fl.data); bm.free()
        fl.is_shadow_catcher = True
        fl.data.materials.append(bpy.data.materials.new("floor_mat"))
    return sc


def camera(sc):
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    return cam


def shoot(sc, cam, path, target, dist, yaw, res, lens=70, pitch=4, samples=None):
    cam.data.lens = lens; cam.data.clip_end = 100
    y, p = math.radians(yaw), math.radians(pitch)
    d = Vector((math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p)))
    cam.location = Vector(target) + d * dist; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.resolution_x, sc.render.resolution_y = res; sc.render.filepath = path
    if samples: sc.cycles.samples = samples
    bpy.ops.render.render(write_still=True)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)
