"""The Hero, rebuilt clean: Adnan's toy-athlete (blond, polo, navy shorts, white sneakers) as smooth, simple,
low-poly geometry made from scratch on his skeleton, instead of his scanned body and clothes (which carry Tripo's
noise: torn hems, thin lumpy arms, malformed hands).

Everything is lofted along the skeleton's own joints (`tube`: rings of a superellipse along a smooth path, radii
that change along it, rounded ends), so limbs follow the bones and deform cleanly, and every part is one simple
closed mesh with flat colour materials by role (the game recolours the roles: skin, shirt, shorts, trim, hair).

    Blender -b --factory-startup --python blender/scripts/hero_avatar.py -- --out <dir> [--render <dir>]

Units are metres, Blender axes (the face looks down -Y, Z up), feet on z = 0, about 1.63 m to the crown.
"""
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix

SCRIPTS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPTS)

# ------------------------------------------------------------------------------------------------ the skeleton
SPINE_Y = 0.055
JOINT = dict(          # the rig's own joints (Hero_01_Rig, A-pose): right side is x -> -x
    shoulder=(0.205, 0.055, 1.165), elbow=(0.360, 0.050, 0.965), wrist=(0.458, 0.042, 0.778),
    hip=(0.112, 0.055, 0.775), knee=(0.154, 0.055, 0.440), ankle=(0.187, 0.055, 0.165), toe=(0.187, -0.105, 0.066))

# ------------------------------------------------------------------------------------------------ helpers
def _catmull(pts, per=6):
    p = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for k in range(per):
            t = k / per; t2 = t * t; t3 = t2 * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(pts[-1])
    return out


def _super(th, rx, ry, p):
    c, s = math.cos(th), math.sin(th)
    e = 2.0 / p
    return rx * math.copysign(abs(c) ** e, c), ry * math.copysign(abs(s) ** e, s)


def tube(name, pts, radii, segs=24, p=2.0, per=5, cap0=True, cap1=True, ref=Vector((0, 1, 0)), mat=0, cap_rings=4):
    """A smooth closed tube along the path `pts` (Vectors, smoothed through), with (rx, ry) radii at each point
    (eased between), a superellipse cross-section of exponent p (2 round, more boxy) and rounded ends."""
    path = _catmull([Vector(v) for v in pts], per)
    n = len(path)
    # radii eased along the path by arc fraction
    lens = [0.0]
    for a, b in zip(path, path[1:]): lens.append(lens[-1] + (b - a).length)
    tot = lens[-1] or 1.0
    ctrl = []
    acc = 0.0
    cl = [0.0]
    for a, b in zip(pts, pts[1:]): acc += (Vector(b) - Vector(a)).length; cl.append(acc)
    def radius_at(s):
        s = s * acc
        for i in range(len(cl) - 1):
            if cl[i] <= s <= cl[i + 1] + 1e-9:
                t = (s - cl[i]) / max(cl[i + 1] - cl[i], 1e-9); t = t * t * (3 - 2 * t)
                a, b = radii[i], radii[i + 1]
                return a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
        return radii[-1]
    bm = bmesh.new()
    rings = []

    def ring(center, T, rx, ry):
        T = T.normalized()
        u = T.cross(ref)
        if u.length < 1e-4: u = T.cross(Vector((1, 0, 0)))
        u.normalize(); v = T.cross(u).normalized()
        vs = []
        for j in range(segs):
            a, b = _super(2 * math.pi * j / segs, rx, ry, p)
            vs.append(bm.verts.new(center + u * a + v * b))
        return vs

    for i in range(n):
        T = (path[min(n - 1, i + 1)] - path[max(0, i - 1)])
        rx, ry = radius_at(lens[i] / tot)
        rings.append((ring(path[i], T, rx, ry), path[i], T, rx, ry))
    # rounded ends: rings that shrink like a sphere's latitudes
    def cap(at_start):
        base = rings[0] if at_start else rings[-1]
        _, c, T, rx, ry = base
        T = T.normalized() * (-1 if at_start else 1)
        out = []
        for k in range(1, cap_rings + 1):
            a = (math.pi / 2) * k / (cap_rings + 1)
            out.append((ring(c + T * (max(rx, ry) * math.sin(a)), T * (-1 if at_start else 1), rx * math.cos(a), ry * math.cos(a)), a))
        pole = bm.verts.new(c + T * max(rx, ry))
        return [r for r, _ in out], pole
    seq = [r[0] for r in rings]
    pole0 = pole1 = None
    if cap0:
        rs, pole0 = cap(True); seq = list(reversed(rs)) + seq
    if cap1:
        rs, pole1 = cap(False); seq = seq + rs
    for a, b in zip(seq, seq[1:]):
        for j in range(segs):
            bm.faces.new((a[j], a[(j + 1) % segs], b[(j + 1) % segs], b[j])).material_index = mat
    if pole0:
        for j in range(segs): bm.faces.new((seq[0][(j + 1) % segs], seq[0][j], pole0)).material_index = mat
    if pole1:
        for j in range(segs): bm.faces.new((seq[-1][j], seq[-1][(j + 1) % segs], pole1)).material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for f in me.polygons: f.use_smooth = True
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def mirror(v): return Vector((-v[0], v[1], v[2]))


def blob(name, center, radii, rot=(0, 0, 0), segs=24, rings=14, mat=0):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(rot[2], 3, 'Z') @ Matrix.Rotation(rot[1], 3, 'Y') @ Matrix.Rotation(rot[0], 3, 'X'), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    for f in bm.faces: f.material_index = mat; f.smooth = True
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def apply_modifiers(o):
    """The object's mesh as its modifiers make it (the modifiers go)."""
    if not o.modifiers: return
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    o.modifiers.clear(); o.data = me


def join(name, objs):
    """One mesh from many (their material slots merged by name)."""
    for o in objs: apply_modifiers(o)
    bm = bmesh.new()
    mats = []
    for o in objs:
        slot_map = []
        for m in o.data.materials:
            if m not in mats: mats.append(m)
            slot_map.append(mats.index(m))
        me = o.data.copy(); me.transform(o.matrix_world)
        b2 = bmesh.new(); b2.from_mesh(me)
        for f in b2.faces: f.material_index = slot_map[f.material_index] if f.material_index < len(slot_map) else 0
        tmp = bpy.data.meshes.new("t"); b2.to_mesh(tmp); b2.free()
        bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp); bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    for f in me.polygons: f.use_smooth = True
    out = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(out)
    for o in objs: bpy.data.objects.remove(o, do_unlink=True)
    return out


# ------------------------------------------------------------------------------------------------ materials
TOON = True     # cel shading with an ink outline (the look), not the soft glossy one


SHADOW = {   # what each material's colour is multiplied by in shadow: cool for cloth, warm for skin and hair
    "Hero_Skin": (0.80, 0.56, 0.52), "Hero_Hair": (0.78, 0.56, 0.52), "Hero_HairB": (0.78, 0.58, 0.56),
    "Hero_Shirt": (0.66, 0.70, 0.88), "Hero_Sock": (0.66, 0.70, 0.88), "Hero_Shoe": (0.66, 0.70, 0.88), "Hero_Lace": (0.66, 0.70, 0.88),
    "Hero_Shorts": (0.55, 0.60, 0.95), "Hero_Trim": (0.55, 0.60, 0.95), "Hero_Accent": (0.70, 0.45, 0.50),
    "Hero_Cream": (0.72, 0.68, 0.80), "Hero_Sole": (0.66, 0.68, 0.80), "Hero_FaceLine": (0.8, 0.8, 0.8),
}
DEFAULT_SHADOW = (0.68, 0.68, 0.85)
SUN = math.pi        # a Toon BSDF lit by a sun of strength pi shows its colour exactly


def toon_material(name, color, gloss=0.0):
    """Cel shading: one flat lit tone and one flat shadow tone (the colour times SHADOW), nothing in between but a
    hair of smoothing. The shadow is an emission (so it needs no ambient); the lit tone is the rest, from the sun."""
    mult = SHADOW.get(name, DEFAULT_SHADOW)
    shadow = tuple(c * k for c, k in zip(color, mult))
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    out = nt.nodes["Material Output"]
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Color"].default_value = (*shadow, 1); em.inputs["Strength"].default_value = 1.0
    t = nt.nodes.new("ShaderNodeBsdfToon"); t.component = 'DIFFUSE'
    t.inputs["Color"].default_value = (*[max(0.0, c - k) for c, k in zip(color, shadow)], 1)
    t.inputs["Size"].default_value = 0.72; t.inputs["Smooth"].default_value = 0.03
    add = nt.nodes.new("ShaderNodeAddShader")
    nt.links.new(em.outputs["Emission"], add.inputs[0]); nt.links.new(t.outputs["BSDF"], add.inputs[1]); nt.links.new(add.outputs["Shader"], out.inputs["Surface"])
    m.diffuse_color = (*color, 1)
    return m


def material(name, color, rough=0.6, coat=0.0):
    if TOON: return toon_material(name, color)
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1); b.inputs["Roughness"].default_value = rough
    if "Coat Weight" in b.inputs: b.inputs["Coat Weight"].default_value = coat
    m.diffuse_color = (*color, 1)
    return m


COLORS = {   # linear, from the design's sRGB
    "Hero_Skin": (0.88, 0.46, 0.17), "Hero_Shirt": (0.88, 0.88, 0.90), "Hero_Shorts": (0.022, 0.050, 0.23),
    "Hero_Trim": (0.022, 0.050, 0.23), "Hero_Accent": (0.94, 0.20, 0.02), "Hero_Hair": (0.93, 0.52, 0.09),
    "Hero_Sock": (0.88, 0.88, 0.90), "Hero_Shoe": (0.86, 0.86, 0.88), "Hero_FaceLine": (0.10, 0.05, 0.03),
    "Hero_HairB": (0.76, 0.38, 0.05), "Hero_Lace": (0.90, 0.90, 0.92), "Hero_Cream": (0.86, 0.80, 0.66), "Hero_Sole": (0.50, 0.50, 0.55),
}


def mats(*names): return [material(n, COLORS[n], 0.55 if n == "Hero_Skin" else 0.75) for n in names]



# ------------------------------------------------------------------------------------------------ the body
def build_skin():
    """Neck, arms, hands, legs: skin. The shirt and the shorts cover the rest (the torso is not made)."""
    skin = mats("Hero_Skin")
    parts = []
    n = tube("Neck", [(0, SPINE_Y, 1.17), (0, SPINE_Y, 1.24), (0, SPINE_Y, 1.33)], [(0.066, 0.064), (0.062, 0.060), (0.060, 0.058)], segs=20)
    parts.append(n)
    S, E, W = JOINT["shoulder"], JOINT["elbow"], JOINT["wrist"]
    for side in (1, -1):
        f = (lambda v: Vector(v)) if side == 1 else mirror
        arm = tube("Arm%+d" % side, [f((S[0] - 0.035, S[1], S[2] - 0.055)), f((0.285, 0.053, 1.07)), f(E), f((0.41, 0.046, 0.87)), f(W)],
                   [(0.080, 0.078), (0.076, 0.074), (0.066, 0.064), (0.056, 0.054), (0.047, 0.044)], segs=20)
        parts.append(arm)
        # the hand: a chunky mitten with four short fingers and a thumb
        parts.append(blob("Palm%+d" % side, f((0.471, 0.040, 0.716)), (0.052, 0.036, 0.064), segs=20, rings=12))
        for i, (dy, ln) in enumerate(((-0.026, 0.060), (0.002, 0.066), (0.030, 0.060), (0.056, 0.048))):
            x0 = 0.474 + 0.004 * i
            parts.append(tube("Finger%+d_%d" % (side, i), [f((x0, 0.040 + dy, 0.694)), f((x0 + 0.003, 0.040 + dy - 0.004, 0.694 - ln * 0.5)), f((x0 + 0.006, 0.040 + dy - 0.012, 0.694 - ln))],
                              [(0.0165, 0.0165), (0.0155, 0.0155), (0.0130, 0.0130)], segs=12, per=3, cap_rings=3))
        parts.append(tube("Thumb%+d" % side, [f((0.444, 0.010, 0.748)), f((0.430, -0.016, 0.724)), f((0.424, -0.042, 0.700))],
                          [(0.024, 0.024), (0.020, 0.020), (0.016, 0.016)], segs=12, per=3, cap_rings=3))
        H, K, A = JOINT["hip"], JOINT["knee"], JOINT["ankle"]
        leg = tube("Leg%+d" % side, [f((H[0], H[1], H[2] + 0.02)), f((0.133, 0.055, 0.61)), f(K), f((0.170, 0.055, 0.30)), f(A)],
                   [(0.098, 0.096), (0.088, 0.086), (0.072, 0.072), (0.062, 0.061), (0.054, 0.053)], segs=20)
        parts.append(leg)
    for o in parts: o.data.materials.append(skin[0])
    return join("Hero_Skin", parts)


def build_shirt():
    """The polo: torso, short sleeves with navy and orange cuffs, a collar and an orange placket."""
    w, nv, og = mats("Hero_Shirt", "Hero_Trim", "Hero_Accent")
    parts = []
    # torso, hem to neck opening; boxy cross-section
    z = [0.895, 0.96, 1.05, 1.12, 1.172, 1.208, 1.230]
    rx = [0.186, 0.186, 0.184, 0.182, 0.170, 0.132, 0.092]
    ry = [0.138, 0.138, 0.130, 0.124, 0.116, 0.098, 0.078]
    torso = tube("Torso", [(0, SPINE_Y + 0.004, zz) for zz in z], list(zip(rx, ry)), segs=32, p=2.4, per=4, cap0=False, cap1=False)
    torso.data.materials.append(w); parts.append(torso)
    S, E = JOINT["shoulder"], JOINT["elbow"]
    for side in (1, -1):
        f = (lambda v: Vector(v)) if side == 1 else mirror
        pts = [f((0.150, 0.055, 1.118)), f((0.215, 0.054, 1.098)), f((0.285, 0.053, 1.062)), f((0.336, 0.0514, 0.9986))]
        sl = tube("Sleeve%+d" % side, pts, [(0.098, 0.094), (0.097, 0.093), (0.094, 0.090), (0.088, 0.084)], segs=24, per=4, cap0=False, cap1=False)
        sl.data.materials.append(w); parts.append(sl)
        cap = blob("ShoulderCap%+d" % side, f((0.172, 0.055, 1.105)), (0.098, 0.094, 0.092), segs=24, rings=14)
        cap.data.materials.append(w); parts.append(cap)
        # cuffs: a navy band and a thin orange one at the hem
        c0, c1 = Vector(pts[-1]), Vector(pts[-1]) + (Vector(pts[-1]) - Vector(pts[-2])).normalized() * 0.02
        t = (Vector(pts[-1]) - Vector(pts[-2])).normalized()
        nvb = tube("CuffN%+d" % side, [f(tuple(Vector((pts[-1][0] * side, pts[-1][1], pts[-1][2])) - t * Vector((side, 1, 1)) * 0.0)), f(tuple(Vector((pts[-1][0] * side, pts[-1][1], pts[-1][2])) + Vector((t.x * side, t.y, t.z)) * 0.026))],
                   [(0.0905, 0.0865), (0.0905, 0.0865)], segs=24, per=2, cap0=False, cap1=False)
        nvb.data.materials.append(nv); parts.append(nvb)
        ogb = tube("CuffO%+d" % side, [f(tuple(Vector((pts[-1][0] * side, pts[-1][1], pts[-1][2])) + Vector((t.x * side, t.y, t.z)) * 0.026)), f(tuple(Vector((pts[-1][0] * side, pts[-1][1], pts[-1][2])) + Vector((t.x * side, t.y, t.z)) * 0.038))],
                   [(0.0915, 0.0875), (0.0915, 0.0875)], segs=24, per=2, cap0=False, cap1=False)
        ogb.data.materials.append(og); parts.append(ogb)
    # collar: a navy band round the neck opening with two folded wings in front
    collar = tube("Collar", [(0, SPINE_Y + 0.004, 1.222), (0, SPINE_Y + 0.004, 1.262)], [(0.100, 0.086), (0.078, 0.068)], segs=32, p=2.2, per=3, cap0=False, cap1=False)
    collar.data.materials.append(nv); parts.append(collar)
    for side in (1, -1):
        wing = blob("Wing%+d" % side, (side * 0.052, SPINE_Y - 0.078, 1.225), (0.048, 0.010, 0.040), rot=(0.35, side * -0.55, side * 0.25), segs=16, rings=8)
        wing.data.materials.append(nv); parts.append(wing)
    # placket: an orange stripe down the front
    plk = tube("Placket", [(0, SPINE_Y - 0.094, 1.205), (0, SPINE_Y - 0.120, 1.10)], [(0.0055, 0.004), (0.0055, 0.004)], segs=8, per=3, cap0=True, cap1=True)
    plk.data.materials.append(og); parts.append(plk)
    cream = mats("Hero_Cream")[0]
    for z in (1.176, 1.128):                                    # two buttons on the placket
        b = blob("Button", (0, SPINE_Y - 0.118 - (1.205 - z) * 0.22, z), (0.0085, 0.004, 0.0085), segs=12, rings=8)
        b.data.materials.append(cream); parts.append(b)
    return join("Hero_Shirt", parts)


def build_shorts():
    """Navy shorts: one pelvis that widens from the waist into the hips, two legs that start inside it (their tops
    overlap in the middle, so there is a crotch), and an orange pipe down each outer seam."""
    nv, og = mats("Hero_Shorts", "Hero_Accent")
    parts = []
    zs = [0.975, 0.92, 0.85, 0.79]
    pelvis = tube("Pelvis", [(0, SPINE_Y + 0.004, z) for z in zs], [(0.172, 0.126), (0.184, 0.128), (0.204, 0.126), (0.214, 0.120)], segs=32, p=2.4, per=4, cap0=False, cap1=False)
    pelvis.data.materials.append(nv); parts.append(pelvis)
    for side in (1, -1):
        f = (lambda v: Vector(v)) if side == 1 else mirror
        leg = tube("ShortLeg%+d" % side, [f((0.104, 0.055, 0.80)), f((0.114, 0.055, 0.72)), f((0.127, 0.055, 0.64)), f((0.138, 0.055, 0.565))],
                   [(0.108, 0.113), (0.113, 0.113), (0.117, 0.116), (0.125, 0.120)], segs=24, p=2.2, per=4, cap0=False, cap1=False)
        leg.data.materials.append(nv); parts.append(leg)
        pipe = tube("Pipe%+d" % side, [f((0.181, 0.055, 0.955)), f((0.207, 0.055, 0.88)), f((0.222, 0.055, 0.80)), f((0.241, 0.055, 0.70)), f((0.262, 0.055, 0.60))],
                    [(0.004, 0.004)] * 5, segs=6, per=4, cap0=True, cap1=True)
        pipe.data.materials.append(og); parts.append(pipe)
    cream = mats("Hero_Cream")[0]
    for side in (1, -1):                                        # the drawstring: two cords and a knot
        cord = tube("Cord%+d" % side, [(side * 0.012, SPINE_Y - 0.128, 0.962), (side * 0.030, SPINE_Y - 0.132, 0.925), (side * 0.040, SPINE_Y - 0.130, 0.885)],
                    [(0.0038, 0.0038), (0.0034, 0.0034), (0.0030, 0.0030)], segs=8, per=3, cap0=True, cap1=True)
        cord.data.materials.append(cream); parts.append(cord)
    knot = blob("Knot", (0, SPINE_Y - 0.131, 0.962), (0.011, 0.007, 0.008), segs=12, rings=8); knot.data.materials.append(cream); parts.append(knot)
    return join("Hero_Shorts", parts)


def build_shoes():
    """White socks and chunky sneakers with an orange stripe. (Along a path that runs front to back, a tube's first
    radius is its height and its second its width.)"""
    sk, sh, og = mats("Hero_Sock", "Hero_Shoe", "Hero_Accent")
    parts = []
    for side in (1, -1):
        f = (lambda v: Vector(v)) if side == 1 else mirror
        sock = tube("Sock%+d" % side, [f((0.172, 0.055, 0.31)), f((0.180, 0.055, 0.225)), f((0.187, 0.055, 0.15))], [(0.066, 0.065), (0.063, 0.062), (0.062, 0.061)], segs=20, per=3, cap0=False, cap1=False)
        sock.data.materials.append(sk); parts.append(sock)
        band = tube("SockBand%+d" % side, [f((0.172, 0.055, 0.285)), f((0.173, 0.055, 0.268))], [(0.0668, 0.0658), (0.0668, 0.0658)], segs=20, per=2, cap0=False, cap1=False)
        band.data.materials.append(mats("Hero_Trim")[0]); parts.append(band)
        pts = [f((0.187, 0.112, 0.098)), f((0.187, 0.040, 0.108)), f((0.187, -0.060, 0.090)), f((0.187, -0.170, 0.078)), f((0.187, -0.262, 0.064))]
        body = tube("Shoe%+d" % side, pts, [(0.072, 0.064), (0.082, 0.072), (0.068, 0.080), (0.066, 0.080), (0.054, 0.066)], segs=24, p=2.4, per=4, ref=Vector((1, 0, 0)))
        body.data.materials.append(sh); parts.append(body)
        sole = tube("Sole%+d" % side, [f((0.187, 0.114, 0.030)), f((0.187, -0.060, 0.028)), f((0.187, -0.266, 0.030))], [(0.034, 0.066), (0.034, 0.082), (0.030, 0.066)], segs=20, p=3.0, per=4, ref=Vector((1, 0, 0)))
        sole.data.materials.append(sh); parts.append(sole)
        stripe = tube("Stripe%+d" % side, [f((0.266, -0.030, 0.108)), f((0.270, -0.090, 0.090))], [(0.014, 0.004), (0.014, 0.004)], segs=8, per=3, ref=Vector((1, 0, 0)))
        stripe.data.materials.append(og); parts.append(stripe)
        lace_m = mats("Hero_Lace")[0]; sole_m = mats("Hero_Sole")[0]
        for k, y in enumerate((-0.000, -0.034, -0.068, -0.102)):      # laces: a bar across the tongue at each eyelet pair
            lace = tube("Lace%+d_%d" % (side, k), [f((0.150, y, 0.147 - k * 0.011)), f((0.187, y - 0.004, 0.160 - k * 0.012)), f((0.224, y, 0.147 - k * 0.011))],
                        [(0.0045, 0.0045)] * 3, segs=8, per=3, cap0=True, cap1=True)
            lace.data.materials.append(lace_m); parts.append(lace)
        tab = blob("HeelTab%+d" % side, f((0.187, 0.196, 0.118)), (0.030, 0.008, 0.028), segs=14, rings=8); tab.data.materials.append(sole_m); parts.append(tab)
    return join("Hero_Shoes", parts)


# ------------------------------------------------------------------------------------------------ the head
import hero_head

EGG = dict(centre=Vector((0.0, 0.03, 1.47)), a=0.158, b=0.157, c=0.176, p=2.3, q=2.3, jaw=0.12, jaw_top=1.40, jaw_span=0.10)
NOSE = dict(tip=Vector((0.001, -0.150, 1.372)), amp=0.013, sigma=6.0)
FACE_X, FACE_Z0, FACE_Z1 = 0.13, 1.29, 1.55


def skin_with_face(face_png):
    """The skin, with the painted face laid on the front of the head (projected along Y over x, z)."""
    m = bpy.data.materials.get("Hero_SkinFace") or bpy.data.materials.new("Hero_SkinFace")
    m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    out = nt.nodes["Material Output"]
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Strength"].default_value = 1.0
    b = nt.nodes.new("ShaderNodeBsdfToon"); b.component = 'DIFFUSE'
    b.inputs["Size"].default_value = 0.72; b.inputs["Smooth"].default_value = 0.03
    addsh = nt.nodes.new("ShaderNodeAddShader")
    nt.links.new(em.outputs["Emission"], addsh.inputs[0]); nt.links.new(b.outputs["BSDF"], addsh.inputs[1]); nt.links.new(addsh.outputs["Shader"], out.inputs["Surface"])
    tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(tc.outputs["Object"], sep.inputs["Vector"])
    def math_node(op, a, b_=None, val=None):
        n = nt.nodes.new("ShaderNodeMath"); n.operation = op
        nt.links.new(a, n.inputs[0])
        if b_ is not None: nt.links.new(b_, n.inputs[1])
        elif val is not None: n.inputs[1].default_value = val
        return n
    u = math_node('DIVIDE', math_node('ADD', sep.outputs["X"], val=FACE_X).outputs[0], val=2 * FACE_X)
    v = math_node('DIVIDE', math_node('SUBTRACT', sep.outputs["Z"], val=FACE_Z0).outputs[0], val=FACE_Z1 - FACE_Z0)
    comb = nt.nodes.new("ShaderNodeCombineXYZ"); nt.links.new(u.outputs[0], comb.inputs["X"]); nt.links.new(v.outputs[0], comb.inputs["Y"])
    img = nt.nodes.new("ShaderNodeTexImage"); img.image = bpy.data.images.load(face_png); img.extension = 'CLIP'
    nt.links.new(comb.outputs["Vector"], img.inputs["Vector"])
    geo = nt.nodes.new("ShaderNodeNewGeometry"); ns = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(geo.outputs["Normal"], ns.inputs["Vector"])
    front = math_node('MULTIPLY', math_node('SUBTRACT', math_node('MULTIPLY', ns.outputs["Y"], val=-1.0).outputs[0], val=0.12).outputs[0], val=3.0)
    front.use_clamp = True
    fac = math_node('MULTIPLY', img.outputs["Alpha"], front.outputs[0])
    mix = nt.nodes.new("ShaderNodeMixRGB"); mix.inputs["Color1"].default_value = (*COLORS["Hero_Skin"], 1)
    nt.links.new(img.outputs["Color"], mix.inputs["Color2"]); nt.links.new(fac.outputs[0], mix.inputs["Fac"])
    smult = nt.nodes.new("ShaderNodeVectorMath"); smult.operation = 'MULTIPLY'; smult.inputs[1].default_value = SHADOW["Hero_Skin"]
    nt.links.new(mix.outputs["Color"], smult.inputs[0]); nt.links.new(smult.outputs["Vector"], em.inputs["Color"])
    sub = nt.nodes.new("ShaderNodeVectorMath"); sub.operation = 'SUBTRACT'
    nt.links.new(mix.outputs["Color"], sub.inputs[0]); nt.links.new(smult.outputs["Vector"], sub.inputs[1]); nt.links.new(sub.outputs["Vector"], b.inputs["Color"])
    m.diffuse_color = (*COLORS["Hero_Skin"], 1)
    return m


def build_head(arm, face_png):
    hero_head.EGG.update(EGG); hero_head.NOSE.update(NOSE)
    head = hero_head.build_round_head(arm, segments=48, rings=32, features=False)
    head.data.materials.clear(); head.data.materials.append(skin_with_face(face_png))
    for f in head.data.polygons: f.material_index = 0; f.use_smooth = True
    for m in list(head.modifiers):
        if m.type == 'ARMATURE': head.modifiers.remove(m)
    head.name = "Hero_Head"
    return head


def tufts_hair(head):
    """Chunky golden hair: a thick cap over the head above the hairline, and overlapping tufts on it, swept down and
    across like the design's."""
    hair, hairB = mats("Hero_Hair", "Hero_HairB")
    bm = bmesh.new(); bm.from_mesh(head.data); bm.verts.ensure_lookup_table()
    col = bm.verts.layers.float_color.get("Col")
    keep = [f for f in bm.faces if all(v[col][0] > 0.52 for v in f.verts)]
    gone = [f for f in bm.faces if f not in keep]
    bmesh.ops.delete(bm, geom=gone, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.normal_update()
    for v in bm.verts: v.co += v.normal * 0.020
    bm.verts.ensure_lookup_table()
    for _ in range(12):
        moves = {}
        for v in bm.verts:
            nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
            if len(nb) == 2: moves[v] = ((nb[0].co + nb[1].co) / 2 - v.co) * 0.6
        for v, d in moves.items(): v.co += d
    me = bpy.data.meshes.new("HairCap"); bm.to_mesh(me); bm.free()
    cap = bpy.data.objects.new("HairCap", me); bpy.context.scene.collection.objects.link(cap)
    sol = cap.modifiers.new("sol", 'SOLIDIFY'); sol.thickness = 0.010; sol.offset = 1.0
    sub = cap.modifiers.new("sub", 'SUBSURF'); sub.levels = 1; sub.render_levels = 1
    for p in me.polygons: p.use_smooth = True
    me.materials.append(hair)
    from mathutils.bvhtree import BVHTree
    dg = bpy.context.evaluated_depsgraph_get(); ev = cap.evaluated_get(dg); em = ev.to_mesh()
    tree = BVHTree.FromPolygons([v.co.copy() for v in em.vertices], [tuple(p.vertices) for p in em.polygons])
    C = hero_head.CENTER
    objs = [cap]
    import random; rnd = random.Random(11)
    def lock(phi, el, length, half_w, sweep=0.0, thick=0.019, curl=0.012):
        a = math.radians(phi); e = math.radians(el)
        d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))).normalized()
        hit = tree.ray_cast(C + d * 0.5, -d, 0.6)
        if hit[0] is None: return
        p, n = hit[0], hit[1].normalized()
        down = -(Vector((0, 0, 1)) - n * n.z)
        if down.length < 1e-3: down = Vector((math.sin(a), -math.cos(a), 0))
        down.normalize()
        side = n.cross(down).normalized()
        axis = (down * math.cos(sweep) + side * math.sin(sweep)).normalized()
        root = p - axis * 0.004 + n * 0.002
        path = [root, root + axis * length * 0.5 + n * 0.010, root + axis * length + n * (0.004 - curl)]
        o = tube("Lock", path, [(half_w, thick), (half_w * 0.85, thick * 0.9), (half_w * 0.30, thick * 0.45)], segs=16, per=4, ref=n, cap0=True, cap1=True, cap_rings=4)
        o.data.materials.append(hairB if rnd.random() < 0.38 else hair); objs.append(o)
    for phi in range(-66, 67, 22):                                # the fringe: big locks swept across the forehead
        lock(phi + rnd.uniform(-3, 3), 40 + rnd.uniform(-2, 2), 0.088 + rnd.uniform(-0.008, 0.008), 0.036, sweep=0.55)
    for phi in range(-88, 89, 24):                                # the row behind it
        lock(phi + rnd.uniform(-4, 4), 56 + rnd.uniform(-3, 3), 0.090, 0.040, sweep=0.65)
    for k in range(5):                                            # the crown's swirl
        lock(k * 72 + 10, 80, 0.070, 0.036, sweep=1.0)
    lock(-18, 70, 0.060, 0.020, sweep=-0.4, curl=-0.030)           # a cowlick standing up off the crown, to one side
    lock(24, 62, 0.050, 0.018, sweep=0.2, curl=-0.026)
    for phi in (-98, 98):                                         # over the ears
        lock(phi, 22, 0.070, 0.034, sweep=0.0)
    for phi in range(118, 243, 26):                               # round the back
        for el in (16, 38, 60):
            lock(phi + rnd.uniform(-4, 4), el, 0.085, 0.040, sweep=0.22 * (1 if phi > 180 else -1))
    return join("Hero_Hair", objs)


# ------------------------------------------------------------------------------------------------ the scene
def build_body_parts():
    return [build_skin(), build_shirt(), build_shorts(), build_shoes()]


def add_scene(render_dir, face_png):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm = bpy.data.objects.new("arm", bpy.data.armatures.new("a")); bpy.context.scene.collection.objects.link(arm)
    parts = build_body_parts()
    head = build_head(arm, face_png); hair = tufts_hair(head)
    return parts + [head, hair]


def outline(o, thickness=0.0042):
    """An ink line round the silhouette: a slightly larger copy with its normals flipped that shows only where its
    far side peeks out behind the part (the near side is see-through)."""
    me = o.data.copy()
    bm = bmesh.new(); bm.from_mesh(me); bm.normal_update()
    for v in bm.verts: v.co += v.normal * thickness
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    for f in bm.faces: f.material_index = 0
    bm.to_mesh(me); bm.free()
    me.materials.clear()
    m = bpy.data.materials.get("Hero_Ink") or bpy.data.materials.new("Hero_Ink")
    m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    out = nt.nodes["Material Output"]
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Color"].default_value = (0.06, 0.03, 0.05, 1); em.inputs["Strength"].default_value = 1.0
    tr = nt.nodes.new("ShaderNodeBsdfTransparent"); geo = nt.nodes.new("ShaderNodeNewGeometry"); mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(geo.outputs["Backfacing"], mix.inputs["Fac"]); nt.links.new(em.outputs["Emission"], mix.inputs[1]); nt.links.new(tr.outputs["BSDF"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
    me.materials.append(m)
    ob = bpy.data.objects.new(o.name + "_ink", me); bpy.context.scene.collection.objects.link(ob)
    # the ink is seen by the camera only: it must not shade the part it surrounds, nor bounce light
    ob.visible_shadow = False; ob.visible_diffuse = False; ob.visible_glossy = False; ob.visible_transmission = False; ob.visible_volume_scatter = False
    return ob


def render(out_dir, face_png):
    parts = add_scene(out_dir, face_png)
    for o in parts: print("PART", o.name, len(o.data.vertices), "verts", sum(len(p.vertices) - 2 for p in o.data.polygons), "tris")
    if TOON:
        for o in parts: outline(o, 0.0030 if o.name == "Hero_Hair" else 0.0042)
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.use_denoising = True
    scene.render.film_transparent = True
    scene.view_settings.view_transform = 'Standard'; scene.view_settings.exposure = 0.0
    world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True
    bg = world.node_tree.nodes["Background"]; bg.inputs["Color"].default_value = (0.9, 0.93, 0.98, 1); bg.inputs["Strength"].default_value = 0.7
    def light(name, loc, energy, size, color=(1, 1, 1)):
        d = bpy.data.lights.new(name, 'AREA'); d.energy = energy; d.size = size; d.color = color
        o = bpy.data.objects.new(name, d); scene.collection.objects.link(o); o.location = loc
        o.rotation_euler = (Vector((0, 0, 0.9)) - Vector(loc)).normalized().to_track_quat('-Z', 'Y').to_euler()
    if TOON:
        def sun(name, direction, energy, color):
            d = bpy.data.lights.new(name, 'SUN'); d.energy = energy; d.angle = math.radians(4); d.color = color
            o = bpy.data.objects.new(name, d); scene.collection.objects.link(o)
            o.rotation_euler = Vector(direction).normalized().to_track_quat('-Z', 'Y').to_euler()
        sun("key", (0.50, 0.72, -0.62), SUN, (1.0, 1.0, 1.0))
        bg.inputs["Strength"].default_value = 0.0
    else:
        light("key", (-1.2, -2.0, 2.6), 140, 1.6, (1.0, 0.96, 0.9)); light("fill", (1.8, -1.6, 1.4), 50, 1.8, (0.85, 0.92, 1.0)); light("rim", (0.8, 2.0, 2.2), 110, 1.2)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam
    def shoot(name, target, dist, yaw, res, lens=70, pitch=3):
        cam.data.lens = lens; y = math.radians(yaw); p = math.radians(pitch)
        d = Vector((math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p)))
        cam.location = Vector(target) + d * dist; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        scene.render.resolution_x, scene.render.resolution_y = res
        scene.render.filepath = f"{out_dir}/{name}.png"; bpy.ops.render.render(write_still=True)
    for name, yaw in (("t_front", 0), ("t_34", -30), ("t_side", 80), ("t_back", 180)):
        shoot(name, (0, 0, 0.84), 4.8, yaw, (760, 1160), lens=60)
    shoot("head34", (0, 0, 1.44), 1.6, -28, (900, 900))
    shoot("front", (0, 0, 1.44), 1.6, 0, (800, 800))
    shoot("side", (0, 0, 1.44), 1.6, 78, (800, 800))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    arg = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d
    render(arg("--render"), arg("--face"))
