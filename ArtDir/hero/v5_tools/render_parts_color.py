"""All parts of a Tripo segmentation, each a flat distinct colour, 4 views in one strip; legend printed + drawn.
Usage: Blender -b --python render_parts_color.py -- <glb> <out.png>"""
import bpy, sys, math, colorsys
from mathutils import Vector
argv = sys.argv[sys.argv.index('--') + 1:]; GLB, OUT = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=GLB)
parts = sorted([o for o in bpy.data.objects if o.type == 'MESH'], key=lambda o: -len(o.data.polygons))
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'OBJECT'
for i, o in enumerate(parts):
    h = (i * 0.618) % 1; c = colorsys.hsv_to_rgb(h, .85 if i % 2 == 0 else .55, 1 if i % 3 else .7)
    o.color = (*c, 1); print('LEG', o.name, len(o.data.polygons), '#%02x%02x%02x' % tuple(int(x * 255) for x in c))
cd = bpy.data.cameras.new('c'); cd.type = 'ORTHO'; cam = bpy.data.objects.new('c', cd); sc.collection.objects.link(cam); sc.camera = cam
pts = [o.matrix_world @ Vector(b) for o in parts for b in o.bound_box]
lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)]); c = (lo + hi) / 2
cd.ortho_scale = max(hi - lo) * 1.15
sc.render.resolution_x = sc.render.resolution_y = 600
import os; base = OUT[:-4]
for n, d in (('px', (1, 0, .15)), ('nx', (-1, 0, .15)), ('py', (0, 1, .15)), ('ny', (0, -1, .15))):
    cam.location = c + Vector(d).normalized() * 3; cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = f'{base}_{n}.png'; bpy.ops.render.render(write_still=True)
print('DONE')
