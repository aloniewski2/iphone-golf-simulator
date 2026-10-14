"""Blender helpers for CLOTHING V2 (headless Blender 5.2). Geometry stays in the body's MESH space (Z up, hero faces -Y, hero's right = +X).
For viewing, objects are parented under a mirror empty (scale x -1) so renders read like Unity (the Unity importer mirrored X)."""
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mesh_object(name, pos, tri, collection=None, smooth=True, normals=None):
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(pos)); me.vertices.foreach_set('co', np.asarray(pos, np.float32).ravel())
    tri = np.asarray(tri, np.int32)
    me.loops.add(tri.size); me.loops.foreach_set('vertex_index', tri.ravel())
    me.polygons.add(len(tri)); me.polygons.foreach_set('loop_start', np.arange(0, tri.size, 3, dtype=np.int32))
    me.update(calc_edges=True); me.validate(clean_customdata=False)
    if smooth: me.shade_smooth()
    if normals is not None:
        me.normals_split_custom_set_from_vertices([tuple(n) for n in np.asarray(normals, np.float64)])
    ob = bpy.data.objects.new(name, me)
    (collection or bpy.context.scene.collection).objects.link(ob)
    return ob


def bvh_from(pos, tri):
    return BVHTree.FromPolygons([tuple(p) for p in np.asarray(pos, np.float64)], [tuple(t) for t in np.asarray(tri)], all_triangles=True)


def nearest(bvh, pts):
    """closest points, face normals, face index, distance for each point (python loop over BVH: fine for ~1e5 calls)."""
    n = len(pts); cp = np.zeros((n, 3)); nn = np.zeros((n, 3)); fi = np.full(n, -1, np.int64); d = np.full(n, 1e9)
    fn = bvh.find_nearest
    for i, p in enumerate(pts):
        r = fn(Vector(p))
        if r[0] is not None:
            cp[i] = r[0]; nn[i] = r[1]; fi[i] = r[2]; d[i] = r[3]
    return cp, nn, fi, d


def vertex_normals(pos, tri):
    p = pos; t = tri
    fn = np.cross(p[t[:, 1]] - p[t[:, 0]], p[t[:, 2]] - p[t[:, 0]])
    vn = np.zeros_like(p)
    for k in range(3): np.add.at(vn, t[:, k], fn)
    return vn / (np.linalg.norm(vn, axis=1, keepdims=True) + 1e-12)


def material(name, rgb, rough=.6, sheen=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*rgb, 1)
    b.inputs['Roughness'].default_value = rough
    if sheen > 0:
        try:
            b.inputs['Sheen Weight'].default_value = sheen
        except KeyError:
            pass
    return m


def srgb(hexs):
    h = hexs.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c)


def studio(engine='BLENDER_EEVEE', res=(900, 1200), samples=32):
    sc = bpy.context.scene
    sc.render.engine = engine
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = False
    try:
        sc.eevee.taa_render_samples = samples
    except Exception:
        pass
    sc.view_settings.view_transform = 'AgX' if 'AgX' in [i.identifier for i in sc.view_settings.bl_rna.properties['view_transform'].enum_items] else 'Filmic'
    sc.view_settings.look = 'None'
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (.20, .20, .21, 1); bg.inputs['Strength'].default_value = .9
    def sun(name, rot, energy, color=(1, 1, 1)):
        L = bpy.data.lights.new(name, 'SUN'); L.energy = energy; L.color = color; L.angle = .35
        o = bpy.data.objects.new(name, L); o.rotation_euler = rot; sc.collection.objects.link(o); return o
    import math
    sun('Key', (math.radians(52), 0, math.radians(-35)), 3.2, (1, .97, .92))
    sun('Fill', (math.radians(65), 0, math.radians(120)), 1.0, (.85, .9, 1))
    sun('Back', (math.radians(110), 0, math.radians(180)), 1.2)


def camera(target, azimuth_deg, elevation_deg=4, dist=4.0, lens=70):
    """camera looking at target (mesh space) from the hero's front rotated by azimuth (0 = front, 90 = hero's left side as seen in Unity)."""
    import math
    sc = bpy.context.scene
    cam = sc.camera
    if cam is None:
        cd = bpy.data.cameras.new('Cam'); cam = bpy.data.objects.new('Cam', cd); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = lens
    a = math.radians(azimuth_deg); e = math.radians(elevation_deg)
    # hero faces -Y in mesh space; the view is mirrored in X by the root empty, so azimuth turns the same way as in Unity
    d = Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    t = Vector(target)
    cam.location = t + d * dist
    cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
    return cam


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
