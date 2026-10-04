"""The base Hero: the head with its face, and the body. Every swappable part (hair, hats, glasses, beards, tops,
bottoms, shoes: avatar_parts.py) is made against the numbers in this file, so anything fits anyone.

Head: a soft rounded block (wide cheeks, small chin) whose features are separate small meshes laid on its surface:
two glossy eyes with a glint, thick brows, a nose, an open smile (mouth, teeth, tongue), blush, ears.
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix
import avatar_kit as K
from avatar_kit import tube, blob, join, disc, ellipse_pts, Surface

# ------------------------------------------------------------------------------------------------ the numbers
C = Vector((0.0, 0.0, 0.0))                # the head is made in its own space, centred here, then placed on the body:
HEAD_POS = Vector((0.0, 0.030, 1.548))     # where its centre goes in the body's space
HS = 1.25                                  # and how much bigger than the drawing it is (the big icon head)
HX, HY, HZ = 0.198, 0.190, 0.200           # half width, half depth, half height of the skull block
EXP_TOP, EXP_BOT, EXP_H = 2.75, 3.25, 2.95 # how square the block is: crown, jaw, sides (2 = a sphere)

# the rig's joints (Hero_01_Rig, A-pose); the right side mirrors x
J = dict(shoulder=(0.205, 0.055, 1.165), elbow=(0.360, 0.050, 0.965), wrist=(0.458, 0.042, 0.778),
         hip=(0.112, 0.055, 0.775), knee=(0.154, 0.055, 0.440), ankle=(0.187, 0.055, 0.165), toe=(0.187, -0.105, 0.066))
SPINE_Y = 0.055
# the rig's finger bones (the left hand; the right mirrors x): index, middle, ring, pinky, each root -> joint -> tip
HAND_BONES = dict(
    fingers=[[(0.471, -0.023, 0.690), (0.476, -0.024, 0.658), (0.480, -0.024, 0.626)],
             [(0.476, 0.010, 0.690), (0.480, 0.010, 0.658), (0.485, 0.010, 0.626)],
             [(0.481, 0.044, 0.690), (0.485, 0.044, 0.659), (0.489, 0.043, 0.627)],
             [(0.485, 0.077, 0.691), (0.490, 0.076, 0.659), (0.494, 0.076, 0.627)]],
    thumb=[(0.430, -0.029, 0.744), (0.424, -0.050, 0.709), (0.423, -0.058, 0.668)])

# sockets (head space): where parts hang, in metres from the head's centre C (+z up, -y forward)
SOCKETS = dict(
    crown=(0.0, 0.0, HZ), brow=(0.0, -HY * 0.85, 0.045), glasses=(0.0, -HY * 0.93, -0.005),
    ear_l=(HX, 0.0, -0.012), ear_r=(-HX, 0.0, -0.012), mouth=(0.0, -HY * 0.9, -0.105), nape=(0.0, HY * 0.8, -0.10))


def _sp(c, e):
    return math.copysign(abs(c) ** e, c)


def head_mesh(name="Hero_Head", lon=36, lat=22):
    """The skull block as an object (see head_bm)."""
    me = bpy.data.meshes.new(name); bm = head_bm(lon, lat); bm.to_mesh(me); bm.free()
    return K.new_obj(name, me, "Hero_Skin")


def head_bm(lon=36, lat=22):
    """The skull block: a superellipsoid, then the cheeks filled out and the chin tucked in (in head space)."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=lon, v_segments=lat, radius=1.0)
    eh = 2.0 / EXP_H
    for v in bm.verts:
        u = v.co.copy(); l = u.length
        if l < 1e-9: continue
        u /= l
        ph = math.asin(max(-1, min(1, u.z))); th = math.atan2(u.y, u.x)
        ev = 2.0 / (EXP_TOP if ph >= 0 else EXP_BOT)
        cp, sp = math.cos(ph), math.sin(ph)
        x = HX * _sp(cp, ev) * _sp(math.cos(th), eh)
        y = HY * _sp(cp, ev) * _sp(math.sin(th), 2.0 / 2.45)       # (front to back stays rounder than side to side)
        z = HZ * _sp(sp, ev)
        t = z / HZ
        if t < 0:                                   # the jaw: narrower toward the chin, which stays flat and defined
            s = max(0.0, min(1.0, (-t - 0.22) / 0.78)); s = s * s * (3 - 2 * s)
            x *= 1 - 0.20 * s; y *= 1 - 0.09 * s
        cheek = math.exp(-((t + 0.22) / 0.34) ** 2)  # the cheekbones: a little fuller, just under the eyes
        x *= 1 + 0.030 * cheek
        if t > 0.30:                                 # the brow line narrows a touch toward the crown
            x *= 1 - 0.05 * (t - 0.30) / 0.70
        v.co = Vector((x, y, z)) + C
    return bm


def nose(surf, sz=1.0):
    p, n = surf.hit(0.0, C.z - 0.058)
    if p is None: return None
    o = blob("Nose", p + n * 0.004, (0.028 * sz, 0.023 * sz, 0.025 * sz), "Hero_Skin", segs=12, rings=8)
    # lean it forward and a bit down
    return o


def eye(surf, side, dx=0.078, dz=-0.010, w=0.0172, h=0.0265, depth=0.0100):
    x, z = side * dx, C.z + dz
    p, n = surf.hit(x, z)
    q = Vector((0, -1, 0)).rotation_difference(n)
    R = q.to_matrix().to_4x4()
    def put(o, off):
        o.data.transform(Matrix.Translation(Vector((0, 0, 0))))
        o.data.transform(R); o.location = p + n * off
        return o
    e = blob("Eye", (0, 0, 0), (w, depth, h), "Hero_Eye", segs=12, rings=8); put(e, -0.0035)
    g1 = blob("Glint", (0, 0, 0), (w * 0.34, depth * 0.30, w * 0.34), "Hero_EyeGlint", segs=8, rings=5)
    g1.data.transform(Matrix.Translation(Vector((side * w * 0.26 * -1, -depth * 0.92, h * 0.40)))); put(g1, -0.0035)
    g2 = blob("Glint2", (0, 0, 0), (w * 0.16, depth * 0.18, w * 0.16), "Hero_EyeGlint", segs=8, rings=5)
    g2.data.transform(Matrix.Translation(Vector((side * w * 0.30, -depth * 0.88, -h * 0.35)))); put(g2, -0.0035)
    return [e, g1, g2]


def brow(surf, side, dx=0.084, dz=0.050, length=0.056, thick=0.0125, tilt=0.10, arch=0.005):
    """A thick, rounded brow: a short tube along an arc on the forehead, a little raised off it."""
    pts = []
    for k in range(5):
        t = k / 4.0                                     # 0 inner .. 1 outer
        x = side * (dx - length / 2 + length * t)
        z = C.z + dz + (t - 0.5) * tilt * length * -1 + arch * (1 - (2 * t - 1) ** 2) - 0.002
        p, n = surf.hit(x, z)
        pts.append(p + n * (thick * 0.55))
    return tube("Brow", pts, [(thick * 0.90, thick), (thick, thick * 1.1), (thick * 1.05, thick * 1.1), (thick * 0.9, thick), (thick * 0.6, thick * 0.7)],
                "Hero_Hair", segs=8, per=1, cap_rings=2)


def mouth(surf, style="smile", w=0.112, zc=None):
    """The open smile: a dark inside with a row of teeth along the top and a tongue at the bottom."""
    zc = C.z - 0.108 if zc is None else zc
    def outline(w, depth, top_dip, inset=1.0, top_shift=0.0, n=18):
        pts = []
        for i in range(n + 1):                          # upper edge, left to right
            x = -w / 2 + w * i / n; s = (2 * x / w)
            pts.append((x * inset, zc + 0.004 * s * s - top_dip * (1 - s * s) + top_shift))
        for i in range(1, n):                           # lower edge, right to left
            x = w / 2 - w * i / n; s = (2 * x / w)
            pts.append((x * inset, zc + 0.004 - depth * (1 - s * s) ** 0.75 + top_shift))
        return pts
    if style == "grin":    depth = 0.052
    elif style == "smile": depth = 0.040
    elif style == "soft":  depth = 0.020
    else: depth = 0.040
    out = []
    inner = outline(w, depth, 0.004)
    out.append(disc("Mouth", inner, surf, lift=0.0020, rings=2, mat="Hero_MouthIn"))
    # tongue: a low rounded shape at the bottom of the mouth
    tz = zc + 0.004 - depth * 0.78
    out.append(disc("Tongue", ellipse_pts(0.0, tz, w * 0.26, depth * 0.24, 14), surf, lift=0.0030, rings=2, mat="Hero_Tongue"))
    # teeth: the top edge, a strip
    tp = []
    n = 14
    for i in range(n + 1):
        x = -w * 0.40 + w * 0.80 * i / n; s = (2 * x / w)
        tp.append((x, zc + 0.004 * s * s - 0.004 * (1 - s * s) - 0.0005))
    for i in range(n, -1, -1):
        x = -w * 0.40 + w * 0.80 * i / n; s = (2 * x / w)
        tp.append((x, zc + 0.004 * s * s - 0.004 * (1 - s * s) - 0.0125 + 0.004 * s * s))
    out.append(disc("Teeth", tp, surf, lift=0.0030, rings=1, mat="Hero_Teeth"))
    return out


def blush(surf, side, dx=0.128, dz=-0.062):
    o = disc("Blush", ellipse_pts(side * dx, C.z + dz, 0.034, 0.022, 18, rot=side * -0.12), surf, lift=0.0012, rings=3, mat="Hero_Blush")
    # fade: alpha from the centre out, as a vertex colour (read by the material)
    me = o.data
    col = me.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
    cx = sum(v.co.x for v in me.vertices) / len(me.vertices); cz = sum(v.co.z for v in me.vertices) / len(me.vertices)
    rmax = max(math.hypot(v.co.x - cx, v.co.z - cz) for v in me.vertices)
    for v in me.vertices:
        a = max(0.0, 1.0 - math.hypot(v.co.x - cx, v.co.z - cz) / rmax) ** 1.2
        col.data[v.index].color = (1, 1, 1, a * 0.75)
    m = o.data.materials[0]
    nt = m.node_tree; b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    ca = nt.nodes.new("ShaderNodeVertexColor"); ca.layer_name = "Col"
    nt.links.new(ca.outputs["Alpha"], b.inputs["Alpha"])
    return o


def ear(side, w=0.018, d=0.042, h=0.056, flare=0.42, dz=-0.012):
    """An ear: a flattened ellipsoid, flared out at the back, with a pinker hollow on its outer face."""
    x = side * (HX * 0.985)
    o = blob("Ear", (0, 0, 0), (w, d, h), "Hero_Skin", segs=12, rings=8)
    inner = blob("EarIn", (side * w * 0.55, -d * 0.05, 0), (w * 0.55, d * 0.60, h * 0.64), "Hero_Ear", segs=10, rings=6)
    outs = []
    for part in (o, inner):
        part.data.transform(Matrix.Rotation(side * flare, 4, 'Z'))
        part.location = Vector((x + side * (w * 0.55), C.y + 0.004, C.z + dz))
        outs.append(part)
    return outs


def build_face(head, expression="smile"):
    """Everything on the face. Returns the list of small meshes (the caller joins or keeps them apart)."""
    surf = Surface(head)
    parts = []
    parts += ear(1) + ear(-1)
    n = nose(surf)
    if n: parts.append(n)
    for s in (1, -1):
        parts += eye(surf, s)
        parts.append(brow(surf, s))
        parts.append(blush(surf, s))
    parts += mouth(surf, expression)
    return parts


# ------------------------------------------------------------------------------------------------ the body
def mir(v): return Vector((-v[0], v[1], v[2]))


def body_skin():
    """Neck, torso, arms, mitten hands and legs as one skin mesh. A top or a pair of shorts is drawn over it, a few
    millimetres clear, so nothing needs to be cut away when the outfit changes."""
    parts = []
    parts.append(tube("Neck", [(0, SPINE_Y - 0.004, 1.17), (0, SPINE_Y - 0.014, 1.26), (0, 0.02, 1.35)], [(0.078, 0.074), (0.074, 0.070), (0.072, 0.068)], "Hero_Skin", segs=12, per=2))
    z = [0.78, 0.90, 1.02, 1.13, 1.20, 1.25, 1.275]
    rx = [0.150, 0.162, 0.170, 0.178, 0.165, 0.118, 0.078]      # (clear of the clothes by a centimetre, so the skin never shows through)
    ry = [0.108, 0.116, 0.118, 0.116, 0.108, 0.090, 0.072]
    parts.append(tube("Torso", [(0, SPINE_Y + 0.004, zz) for zz in z], list(zip(rx, ry)), "Hero_Skin", segs=24, p=2.4, per=2, cap0=False, cap1=True))
    S, E, W = Vector(J["shoulder"]), Vector(J["elbow"]), Vector(J["wrist"])
    H, Kn, A = Vector(J["hip"]), Vector(J["knee"]), Vector(J["ankle"])
    for side in (1, -1):
        f = (lambda v: Vector(v)) if side == 1 else mir
        parts.append(tube("Arm", [f((S.x - 0.03, S.y, S.z - 0.05)), f((0.285, 0.053, 1.07)), f(E), f((0.41, 0.046, 0.87)), f(W)],
                          [(0.088, 0.086), (0.084, 0.082), (0.072, 0.070), (0.062, 0.060), (0.052, 0.050)], "Hero_Skin", segs=12, per=2))
        # the hand: a soft mitt, four short fat fingers and a thumb, each finger laid along the rig's own finger
        # bones (HAND_BONES), so the golf grip's curl wraps them
        parts.append(blob("Palm", f((0.474, 0.026, 0.712)), (0.046, 0.060, 0.058), "Hero_Skin", segs=12, rings=8))
        for pts in HAND_BONES["fingers"]:
            parts.append(tube("Finger", [f(p) for p in pts], [(0.0185, 0.0185), (0.0170, 0.0170), (0.0140, 0.0140)], "Hero_Skin", segs=6, per=1, cap_rings=1))
        parts.append(tube("Thumb", [f(p) for p in HAND_BONES["thumb"]], [(0.027, 0.027), (0.023, 0.023), (0.018, 0.018)], "Hero_Skin", segs=8, per=1, cap_rings=2))
        parts.append(tube("Leg", [f((H.x, H.y, H.z + 0.03)), f((0.133, 0.055, 0.61)), f(Kn), f((0.170, 0.055, 0.30)), f(A)],
                          [(0.104, 0.102), (0.094, 0.092), (0.078, 0.078), (0.066, 0.065), (0.058, 0.057)], "Hero_Skin", segs=12, per=2))
    return join("Hero_BodySkin", parts)


def place_head(objs):
    """Put head-space meshes (the head, the face, hair, hats, glasses ...) onto the body: scale by HS about the head's
    centre and move it to HEAD_POS. Everything made against the head is authored unscaled, so it fits any HS."""
    M = Matrix.Translation(HEAD_POS) @ Matrix.Scale(HS, 4)
    for o in objs:
        o.data.transform(o.matrix_basis); o.matrix_basis = Matrix.Identity(4)
        o.data.transform(M)
    bpy.context.view_layer.update()             # (so matrix_world is the identity too when the parts are joined)
    return objs
