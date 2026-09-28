import bpy, sys, math
from mathutils import Vector
argv = sys.argv[sys.argv.index('--') + 1:]; GLB, OUT = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
print('BBOX', tuple(round(v, 3) for v in mn), tuple(round(v, 3) for v in mx), 'tris', sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons))
sc = bpy.context.scene; sc.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE_NEXT'
w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (.93, .93, .92, 1)
for n, e, r in (('k', 3.0, (50, 0, 35)), ('f', 1.1, (70, 0, -60)), ('r', 1.5, (120, 0, 180))):
    l = bpy.data.lights.new(n, 'SUN'); l.energy = e; o = bpy.data.objects.new(n, l); sc.collection.objects.link(o); o.rotation_euler = [math.radians(a) for a in r]
cd = bpy.data.cameras.new('c'); cam = bpy.data.objects.new('c', cd); sc.collection.objects.link(cam); sc.camera = cam
H = mx.z - mn.z; c = (mn + mx) / 2
head = Vector((c.x, c.y, mx.z - H * .16))
def shoot(name, t, yaw, pitch, dist, res):
    sc.render.resolution_x, sc.render.resolution_y = res
    r, p = math.radians(yaw), math.radians(pitch)
    # glTF import: +Y up -> Blender Z up, model faces -Y (forward) usually
    off = Vector((math.sin(r) * math.cos(p), -math.cos(r) * math.cos(p), math.sin(p))) * dist
    cam.location = t + off; cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = f'{OUT}/{name}.png'; bpy.ops.render.render(write_still=True)
for yaw, n in ((0, 'front'), (35, '34'), (90, 'side'), (180, 'back')):
    shoot('turn_' + n, c, yaw, 3, H * 2.6, (600, 1000))
for yaw, pitch, n in ((25, 5, 'hair_34'), (180, 10, 'hair_back'), (150, 55, 'hair_top'), (90, 0, 'hair_side')):
    shoot(n, head, yaw, pitch, H * .75, (800, 800))
print('GLB_RENDERED')
