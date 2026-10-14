"""ACCESSORIES: shared helpers (Blender 5.2 python, numpy only).

FRAMES (read this before touching a transform)
  unity  : Unity world, left-handed, Y up, hero faces +Z, hero's LEFT = -X.
  mesh   : the body renderer's local space (what the .bin files and bindposes use). Z up, hero faces -Y, hero's LEFT = -X.
           mesh = (ux, -uz, uy); unity = (mx, mz, -my). A rotation of the Unity frame, so anatomy stays 'mirrored' when viewed in Blender.
  N      : the natural right-handed Blender frame every ACCESSORY is built and fitted in. Z up, hero faces -Y, hero's LEFT = +X
           (a camera at -Y looking +Y sees the hero's left on the right of the image, like a photo).
           N = (-mx, my, mz). Going between N and mesh flips X, so triangle winding is reversed there (`flip_tris`).
Everything the builders write is N; `to_mesh_space` / `from_mesh_space` are the only places X changes sign.
"""
import os, sys, math, json
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..'))                   # work/accessories
REPO = os.path.abspath(os.path.join(ROOT, '..', '..'))
CV2 = os.path.join(REPO, 'work', 'clothing-v2')
sys.path.insert(0, os.path.join(CV2, 'py'))
import cv2io                                                       # noqa: E402

HEROES = ['MatchHero_Male', 'MatchHero_Female', 'GolfKitHero_Male', 'GolfKitHero_Female']
BODIES = {'Male': 'MatchHero_Male', 'Female': 'MatchHero_Female'}  # the golf heroes share these bodies and bind poses (checked in README)


def sex_of(hid): return 'Female' if hid.endswith('Female') else 'Male'
def sport_of(hid): return 'Golf' if hid.startswith('Golf') else 'Tennis'


def mesh_to_N(p):
    p = np.asarray(p, np.float64).copy(); p[..., 0] *= -1; return p
N_to_mesh = mesh_to_N                                              # an X flip is its own inverse


def flip_tris(t): return np.asarray(t)[:, [0, 2, 1]]


def unity_to_N(p):
    p = np.asarray(p, np.float64); return np.stack([-p[..., 0], -p[..., 2], p[..., 1]], -1)


def N_to_unity(p):
    p = np.asarray(p, np.float64); return np.stack([-p[..., 0], p[..., 2], -p[..., 1]], -1)


def dir_unity_to_N(v): return unity_to_N(v)                        # a pure axis permutation + sign: same map for directions


def mat_unity_to_N():
    """4x4 that maps unity-world points to N points (a reflection: det -1)."""
    m = np.zeros((4, 4)); m[0, 0] = -1; m[1, 2] = -1; m[2, 1] = 1; m[3, 3] = 1; return m


class HeroData:
    """One body (sex) in N: rest bone matrices (N, L2W), bone names, body mesh (N), skin weights, mesh-space bind poses."""
    def __init__(self, sex):
        self.sex = sex
        self.h = cv2io.Hero(BODIES[sex])
        b = self.h.body
        self.bones = list(b.bones)
        self.body_pos = mesh_to_N(b.pos)
        self.body_tri = flip_tris(b.tri)
        self.body_nrm = mesh_to_N(b.nrm)
        self.bi, self.bw = b.bi, b.bw
        self.r2w = b.r2w                                           # mesh -> unity world
        self.bind = b.bind                                         # (nb,4,4) mesh -> bone local (unity units)
        rb = self.h.rest_bones                                     # (nb,4,4) bone L2W in unity world
        self.rest_L2W_unity = rb
        self.rest_pos = {n: unity_to_N(rb[i][:3, 3]) for i, n in enumerate(self.bones)}
        # axes of a bone in N (columns of the rotation part, converted)
        self.rest_axes = {n: np.stack([unity_to_N(rb[i][:3, k]) for k in range(3)], 1) for i, n in enumerate(self.bones)}

    def idx(self, name): return self.bones.index(name)

    def socket_bindpose_mesh(self, bone, local_N):
        """bindpose (mesh -> socket local) for a socket that is a child of `bone`, whose local pose (4x4, N frame, metres) is `local_N`.
        Returns a 4x4 in the layout the installer reads (unity units for the socket's own space: the socket inherits the bone's axes)."""
        raise NotImplementedError


def srgb(hexs):
    h = hexs.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c)


# ------------------------------------------------------------------ Blender side
def bpy_mods():
    import bpy, bmesh
    from mathutils import Vector, Matrix
    return bpy, bmesh, Vector, Matrix


def reset():
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mesh_object(name, pos, tri, collection=None, smooth=True):
    import bpy
    me = bpy.data.meshes.new(name)
    pos = np.asarray(pos, np.float32); tri = np.asarray(tri, np.int32)
    me.vertices.add(len(pos)); me.vertices.foreach_set('co', pos.ravel())
    me.loops.add(tri.size); me.loops.foreach_set('vertex_index', tri.ravel())
    me.polygons.add(len(tri)); me.polygons.foreach_set('loop_start', np.arange(0, tri.size, 3, dtype=np.int32))
    me.update(calc_edges=True); me.validate(clean_customdata=False)
    if smooth: me.shade_smooth()
    ob = bpy.data.objects.new(name, me)
    (collection or bpy.context.scene.collection).objects.link(ob)
    return ob


def material(name, rgb, rough=.6, sheen=0.0, metallic=0.0, clearcoat=0.0):
    import bpy
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*rgb, 1)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metallic
    for k, v in (('Sheen Weight', sheen), ('Coat Weight', clearcoat)):
        if v > 0:
            try: b.inputs[k].default_value = v
            except KeyError: pass
    return m


def studio(res=(1000, 1000), samples=48, bg=(.30, .30, .32), strength=0.55):
    import bpy
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = res
    try: sc.eevee.taa_render_samples = samples
    except Exception: pass
    items = [i.identifier for i in sc.view_settings.bl_rna.properties['view_transform'].enum_items]
    sc.view_settings.view_transform = 'Standard' if 'Standard' in items else 'Filmic'
    sc.view_settings.look = 'None'
    for o in list(sc.objects):
        if o.type == 'LIGHT': bpy.data.objects.remove(o)
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    g = w.node_tree.nodes['Background']; g.inputs['Color'].default_value = (*bg, 1); g.inputs['Strength'].default_value = strength

    def sun(name, rot, energy, color=(1, 1, 1)):
        L = bpy.data.lights.new(name, 'SUN'); L.energy = energy; L.color = color; L.angle = .35
        o = bpy.data.objects.new(name, L); o.rotation_euler = rot; sc.collection.objects.link(o); return o
    # hero faces -Y: key from the front-left-above, fill from the right, rim from behind
    sun('Key', (math.radians(55), 0, math.radians(35)), 4.6, (1, .97, .93))
    sun('Fill', (math.radians(70), 0, math.radians(-125)), 1.1, (.88, .92, 1))
    sun('Rim', (math.radians(115), 0, math.radians(180)), 1.4)


def camera(target, azimuth_deg=0.0, elevation_deg=8.0, dist=1.0, lens=70, ortho=None):
    """azimuth 0 = from the hero's front (camera at -Y), +90 = from the hero's LEFT side (camera at +X), 180 = back, -90 = right side."""
    import bpy
    from mathutils import Vector
    sc = bpy.context.scene
    cam = sc.camera
    if cam is None:
        cd = bpy.data.cameras.new('Cam'); cam = bpy.data.objects.new('Cam', cd); sc.collection.objects.link(cam); sc.camera = cam
    cd = cam.data
    if ortho: cd.type = 'ORTHO'; cd.ortho_scale = ortho
    else: cd.type = 'PERSP'; cd.lens = lens
    a, e = math.radians(azimuth_deg), math.radians(elevation_deg)
    d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    t = Vector(target)
    cam.location = t + d * dist
    cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
    return cam


def render(path):
    import bpy
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def load_body(hd, name='Body', rgb=None, rough=.55):
    """the hero's bald body in N, with a skin material."""
    ob = mesh_object(name, hd.body_pos, hd.body_tri)
    ob.data.materials.append(material('Skin', rgb or srgb('B7744A' if hd.sex == 'Male' else 'D49A74'), rough))
    return ob


def args_after_dashes():
    return sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
