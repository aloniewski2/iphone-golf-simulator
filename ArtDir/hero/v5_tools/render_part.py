import bpy, sys, math
from mathutils import Vector
argv = sys.argv[sys.argv.index('--') + 1:]; GLB, PART, OUT = argv[0], argv[1], argv[2]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=GLB)
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != PART: bpy.data.objects.remove(o)
o = bpy.data.objects[PART]; sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE_NEXT'
w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (1, 0, 1, 1)
for n, e, r in (('k', 3.0, (50, 0, 35)), ('f', 1.2, (70, 0, -60)), ('r', 1.5, (120, 0, 180))):
    l = bpy.data.lights.new(n, 'SUN'); l.energy = e; ob = bpy.data.objects.new(n, l); sc.collection.objects.link(ob); ob.rotation_euler = [math.radians(a) for a in r]
cd = bpy.data.cameras.new('c'); cam = bpy.data.objects.new('c', cd); sc.collection.objects.link(cam); sc.camera = cam
bb = [o.matrix_world @ Vector(c) for c in o.bound_box]; c = sum(bb, Vector()) / 8
for n, d in (('px', (1, 0, .2)), ('nx', (-1, 0, .2)), ('py', (0, 1, .2)), ('top', (0.2, 0, 1))):
    cam.location = c + Vector(d).normalized() * 2.2; cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.resolution_x = sc.render.resolution_y = 500; sc.render.filepath = f'{OUT}_{n}.png'; bpy.ops.render.render(write_still=True)
