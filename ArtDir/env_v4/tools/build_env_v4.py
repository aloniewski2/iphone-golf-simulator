# Plan 2D environment kit: chunky toy tropical trees + the beach-house landmark, all on ONE shared
# palette atlas (Unity/Assets/Resources/Tennis/EnvV4/EnvV4_Palette.png, 16 gradient cells) so the whole
# kit is one material and batches on iPhone. Run: Blender -b --factory-startup -P build_env_v4.py
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, noise

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../../Unity/Assets/Resources/Tennis/EnvV4'))
CELLS = 16
LEAF_A, LEAF_B, TRUNK, STUCCO, ROOF, TEAL, WOOD, GLASS, CORAL, PINK, YELLOW, STONE, TRIM, SAND, LAWN, WHITE = range(16)

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def mesh_obj(name, bm):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob); return ob

def paint(bm, cell, faces=None, zmin=None, zmax=None, jitter=0.0):
    """UV every loop into palette cell `cell`; v follows height (dark bottom -> light top)."""
    uv = bm.loops.layers.uv.verify()
    faces = list(bm.faces) if faces is None else faces
    zs = [v.co.z for f in faces for v in f.verts]
    lo = min(zs) if zmin is None else zmin; hi = max(zs) if zmax is None else zmax
    u = (cell + .5) / CELLS
    for f in faces:
        for l in f.loops:
            t = (l.vert.co.z - lo) / max(1e-4, hi - lo)
            if jitter: t += (noise.noise(l.vert.co * 2.3)) * jitter
            l[uv].uv = (u, .06 + .88 * min(1, max(0, t)))

def add_ico(bm, center, r, sub=2, squash=(1, 1, 1), bump=0.0, seed=0):
    ret = bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    vs = ret['verts']
    for v in vs:
        p = v.co.copy()
        if bump:
            n = noise.noise(p * (1.6 / r) + Vector((seed * 3.1, seed * 1.7, seed * .9)))
            p = p * (1 + bump * n)
        v.co = Vector((p.x * squash[0], p.y * squash[1], p.z * squash[2])) + Vector(center)
    faces = list({f for v in vs for f in v.link_faces})
    return faces

def add_tube(bm, pts, radii, seg=8):
    """Tapered tube through pts (trunks / branches)."""
    rings = []
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        a = d.orthogonal().normalized(); b = d.cross(a)
        ring = [bm.verts.new(p + (a * math.cos(2 * math.pi * k / seg) + b * math.sin(2 * math.pi * k / seg)) * radii[i]) for k in range(seg)]
        rings.append(ring)
    faces = []
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(seg):
            faces.append(bm.faces.new((r0[k], r0[(k + 1) % seg], r1[(k + 1) % seg], r1[k])))
    faces.append(bm.faces.new(list(reversed(rings[0]))))
    return faces

def add_box(bm, center, size, taper_top=None):
    ret = bmesh.ops.create_cube(bm, size=1)
    vs = ret['verts']
    for v in vs:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) + Vector(center)
    if taper_top:
        top = [v for v in vs if v.co.z > center[2]]
        for v in top:
            v.co.x = center[0] + (v.co.x - center[0]) * taper_top[0]
            v.co.y = center[1] + (v.co.y - center[1]) * taper_top[1]
    return list({f for v in vs for f in v.link_faces})

def add_cyl(bm, center, r, h, seg=12, r_top=None):
    ret = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r if r_top is None else r_top, depth=h)
    vs = ret['verts']
    for v in vs: v.co += Vector(center)
    return list({f for v in vs for f in v.link_faces})

def finish(ob, smooth=True):
    for p in ob.data.polygons: p.use_smooth = smooth
    return ob

def export(ob, name):
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False)
    print('EXPORT', name, len(ob.data.polygons), 'faces')

# ------------------------------------------------------------------ trees
def toy_broadleaf(name, seed, height=4.6, canopy=1.5, leaf=LEAF_A, flowers=None):
    random.seed(seed); bm = bmesh.new()
    # slightly curved, tapered trunk with a fork
    pts = [Vector((0, 0, 0)), Vector((.05, 0, height * .3)), Vector((.12, .04, height * .55)), Vector((.08, .1, height * .72))]
    trunk = add_tube(bm, pts, [.26, .2, .16, .12], seg=9)
    paint(bm, TRUNK, trunk, 0, height * .72)
    top = pts[-1]
    leaves = []
    lobes = [(0, 0, .35, 1.0)] + [(math.cos(a) * canopy * .62, math.sin(a) * canopy * .62, random.uniform(-.1, .25), random.uniform(.68, .82))
                                  for a in [i * 2 * math.pi / 5 + random.uniform(-.3, .3) for i in range(5)]]
    for i, (x, y, z, s) in enumerate(lobes):
        leaves += add_ico(bm, top + Vector((x, y, z * canopy + canopy * .15)), canopy * s, sub=2, squash=(1, 1, .82), bump=.13, seed=seed * 7 + i)
    cz = [v.co.z for f in leaves for v in f.verts]
    paint(bm, leaf, leaves, min(cz), max(cz), jitter=.08)
    if flowers is not None:
        fl = []
        for k in range(14):
            a = random.uniform(0, 2 * math.pi); r = canopy * random.uniform(.6, 1.2); h = random.uniform(.3, 1.1) * canopy
            fl += add_ico(bm, top + Vector((math.cos(a) * r, math.sin(a) * r, h)), .13, sub=1)
        paint(bm, flowers, fl)
    return finish(mesh_obj(name, bm))

def toy_umbrella(name, seed, height=5.2, spread=2.3):
    """Flowering tropical tree: trunk forks into three limbs, each topped with a flattened leaf puff."""
    random.seed(seed); bm = bmesh.new()
    base = [Vector((0, 0, 0)), Vector((0, 0, height * .42))]
    faces = add_tube(bm, base + [Vector((0, 0, height * .5))], [.24, .18, .16], seg=9)
    leaves = []; fl = []
    for i in range(3):
        a = i * 2 * math.pi / 3 + random.uniform(-.25, .25)
        tip = Vector((math.cos(a) * spread * .55, math.sin(a) * spread * .55, height * .86))
        faces += add_tube(bm, [Vector((0, 0, height * .45)), (Vector((0, 0, height * .45)) + tip) * .5 + Vector((0, 0, .2)), tip], [.14, .1, .07], seg=7)
        leaves += add_ico(bm, tip + Vector((0, 0, .25)), spread * .62, sub=2, squash=(1, 1, .45), bump=.12, seed=seed + i)
        for k in range(5):
            b = random.uniform(0, 2 * math.pi); r = spread * random.uniform(.2, .6)
            fl += add_ico(bm, tip + Vector((math.cos(b) * r, math.sin(b) * r, .45 + random.uniform(0, .12))), .12, sub=1)
    leaves += add_ico(bm, Vector((0, 0, height * .95)), spread * .55, sub=2, squash=(1, 1, .45), bump=.12, seed=seed + 9)
    paint(bm, TRUNK, faces, 0, height)
    cz = [v.co.z for f in leaves for v in f.verts]; paint(bm, LEAF_B, leaves, min(cz), max(cz), jitter=.08)
    paint(bm, PINK, fl)
    return finish(mesh_obj(name, bm))

def toy_shrub(name, seed, flowers=None, r=.6):
    random.seed(seed); bm = bmesh.new(); leaves = []
    for i in range(4):
        a = i * 2 * math.pi / 4 + random.uniform(-.4, .4); d = r * .55 if i else 0
        leaves += add_ico(bm, Vector((math.cos(a) * d, math.sin(a) * d, r * .62)), r * random.uniform(.72, .9), sub=2, squash=(1, 1, .8), bump=.14, seed=seed + i)
    cz = [v.co.z for f in leaves for v in f.verts]; paint(bm, LEAF_A if seed % 2 else LEAF_B, leaves, min(cz) - .1, max(cz), jitter=.08)
    if flowers is not None:
        fl = []
        for k in range(9):
            a = random.uniform(0, 2 * math.pi); rr = r * random.uniform(.3, .95)
            fl += add_ico(bm, Vector((math.cos(a) * rr, math.sin(a) * rr, r * random.uniform(.9, 1.25))), .09, sub=1)
        paint(bm, flowers, fl)
    return finish(mesh_obj(name, bm))

# ------------------------------------------------------------------ beach house (front faces -Y)
def beach_house(name):
    bm = bmesh.new(); L, D = 22.0, 11.0; g1, g2 = 3.6, 3.3
    def P(cell, faces, zmin=None, zmax=None): paint(bm, cell, faces, zmin, zmax)
    P(STONE, add_box(bm, (0, 0, .35), (L + 1.2, D + 1.2, .7)))                                   # plinth
    P(STUCCO, add_box(bm, (0, 0, .7 + g1 / 2), (L, D, g1)), 0, 7.6)                              # ground floor
    P(WOOD, add_box(bm, (0, -D / 2 - 1.3, .72 + .06), (L - .6, 2.6, .12)))                          # veranda deck
    P(STUCCO, add_box(bm, (0, -D / 2 - 1.3, .7 + g1 + .12), (L + .2, 2.9, .24)), 0, 7.6)             # terrace slab
    for i in range(7):                                                                               # portico columns
        x = -L / 2 + 1 + i * (L - 2) / 6
        P(WHITE, add_cyl(bm, (x, -D / 2 - 2.5, .7 + g1 / 2), .2, g1, seg=10, r_top=.17))
    up = .7 + g1 + .24; Lu, Du = 17.0, 8.6
    P(STUCCO, add_box(bm, (-1.5, .9, up + g2 / 2), (Lu, Du, g2)), 0, 7.6)                          # upper floor, set back
    # terrace railing
    for x in [(-L / 2 + .1) + k * .55 for k in range(int(L / .55) + 1)]:
        P(WHITE, add_box(bm, (x, -D / 2 - 2.65, up + .45), (.07, .07, .9)))
    P(WHITE, add_box(bm, (0, -D / 2 - 2.65, up + .92), (L + .1, .12, .08)))
    # windows: glass + arched head + teal shutters, ground floor front & upper front
    def window(cx, cz, w=1.3, h=2.0, y=-D / 2 - .03):
        P(GLASS, add_box(bm, (cx, y, cz), (w, .12, h)))
        ret = add_cyl(bm, (cx, y, cz + h / 2), w / 2, .12, seg=12)
        for f in ret:
            for v in f.verts: pass
        bmesh.ops.rotate(bm, verts=list({v for f in ret for v in f.verts}), cent=Vector((cx, y, cz + h / 2)), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
        P(GLASS, ret)
        for s in (-1, 1): P(TEAL, add_box(bm, (cx + s * (w / 2 + .32), y - .02, cz), (.55, .1, h + .15)))
    for i in range(6): window(-L / 2 + 2 + i * (L - 4) / 5, .7 + 1.6)
    for i in range(5): window(-1.5 - Lu / 2 + 1.8 + i * (Lu - 3.6) / 4, up + 1.45, w=1.1, h=1.6, y=.9 - Du / 2 - .03)
    # striped awnings over the upper windows
    for i in range(5):
        cx = -1.5 - Lu / 2 + 1.8 + i * (Lu - 3.6) / 4
        for k in range(6):
            f = add_box(bm, (cx - .75 + k * .3, .9 - Du / 2 - .45, up + 2.55), (.3, 1.0, .06))
            bmesh.ops.rotate(bm, verts=list({v for ff in f for v in ff.verts}), cent=Vector((cx, .9 - Du / 2 - .45, up + 2.55)), matrix=Matrix.Rotation(math.radians(-22), 3, 'X'))
            P(CORAL if k % 2 == 0 else WHITE, f)
    # hipped terracotta roofs (main + lookout tower) with eaves
    rz = up + g2
    P(ROOF, add_box(bm, (-1.5, .9, rz + 1.2), (Lu + 1.2, Du + 1.2, 2.4), taper_top=(.55, .02)), rz, rz + 2.4)
    P(TRIM, add_box(bm, (-1.5, .9, rz + .06), (Lu + 1.3, Du + 1.3, .14)))
    tx = L / 2 - 2.6
    P(STUCCO, add_box(bm, (tx, 1.5, rz + 1.6), (4.2, 4.2, 3.2)), 0, 7.6)                          # lookout tower
    for s in (-1, 1): window(tx + s * .95, rz + 1.8, w=.8, h=1.3, y=1.5 - 2.1 - .03)
    P(ROOF, add_box(bm, (tx, 1.5, rz + 3.2 + 1.1), (5.0, 5.0, 2.2), taper_top=(.02, .02)), rz + 3.2, rz + 5.4)
    P(TRIM, add_box(bm, (tx, 1.5, rz + 3.25), (5.1, 5.1, .12)))
    # ground-floor roof edge where the upper floor is set back
    P(ROOF, add_box(bm, (0, 0, up + .02), (L + .5, D + .5, .1)), up, up + .1)
    # front door
    P(WOOD, add_box(bm, (0, -D / 2 - .04, .7 + 1.25), (1.8, .14, 2.5)))
    # planters with shrubs along the veranda
    for x in (-L / 2 + .6, L / 2 - .6, -3.5, 3.5):
        P(TEAL, add_cyl(bm, (x, -D / 2 - 2.5, .7 + .3), .45, .6, seg=10, r_top=.55))
        lv = []
        for k in range(3):
            lv += add_ico(bm, (x + (k - 1) * .25, -D / 2 - 2.5, .7 + .95 + (k % 2) * .15), .38, sub=1, bump=.1, seed=k)
        paint(bm, LEAF_B, lv)
    return finish(mesh_obj(name, bm), smooth=False)

reset()
os.makedirs(OUT, exist_ok=True)
for n, fn in [('EnvV4_TreeBroadleafA', lambda n: toy_broadleaf(n, 3, height=3.3, canopy=1.9)),
              ('EnvV4_TreeBroadleafB', lambda n: toy_broadleaf(n, 11, height=3.7, canopy=2.1, leaf=LEAF_B, flowers=YELLOW)),
              ('EnvV4_TreeFlowering', lambda n: toy_umbrella(n, 5)),
              ('EnvV4_ShrubA', lambda n: toy_shrub(n, 2, PINK)),
              ('EnvV4_ShrubB', lambda n: toy_shrub(n, 7, None)),
              ('EnvV4_ShrubC', lambda n: toy_shrub(n, 9, YELLOW)),
              ('EnvV4_BeachHouse', beach_house)]:
    ob = fn(n); export(ob, n)
    ob.location.x = len(bpy.data.objects) * 30
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(os.path.join(os.path.dirname(__file__), '../EnvV4_kit.blend')))
