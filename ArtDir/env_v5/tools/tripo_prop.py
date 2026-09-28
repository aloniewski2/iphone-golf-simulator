"""Tripo GLB -> Unity env prop: joined single mesh, feet at the origin, decimated to budget, FBX + its base-colour
texture as <name>.png (TennisEnvironmentV4.Spawn gives EnvV5_* props their own textured material).
Usage: Blender -b --python tripo_prop.py -- <glb> <out_dir> <name> <max_tris>"""
import bpy, sys, os
from mathutils import Vector, Matrix
glb, out, name, tris = sys.argv[sys.argv.index('--') + 1:][:4]; tris = int(tris)
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=glb)
ms = [o for o in bpy.data.objects if o.type == 'MESH']
for o in ms: o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4); o.parent = None
for o in [o for o in bpy.data.objects if o.type != 'MESH']: bpy.data.objects.remove(o)
bpy.ops.object.select_all(action='DESELECT')
for o in ms: o.select_set(True)
bpy.context.view_layer.objects.active = ms[0]
if len(ms) > 1: bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active; ob.name = name; ob.data.name = name
vs = [v.co for v in ob.data.vertices]
lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
# glTF import is Z-up already; feet (min z) at the origin, centred in XY on the trunk base (lowest 5%)
base = [v for v in vs if v.z < lo.z + (hi.z - lo.z) * .05]
cx = sum(v.x for v in base) / len(base); cy = sum(v.y for v in base) / len(base)
ob.data.transform(Matrix.Translation((-cx, -cy, -lo.z)))
n = sum(len(p.vertices) - 2 for p in ob.data.polygons)
if n > tris:
    d = ob.modifiers.new('d', 'DECIMATE'); d.ratio = tris / n; bpy.ops.object.modifier_apply(modifier='d')
for p in ob.data.polygons: p.use_smooth = True
img = next((nd.image for m in ob.data.materials if m and m.use_nodes for nd in m.node_tree.nodes if nd.type == 'TEX_IMAGE' and nd.image), None)
if img:
    img.filepath_raw = os.path.join(out, name + '.png'); img.file_format = 'PNG'; img.save()
bpy.ops.export_scene.fbx(filepath=os.path.join(out, name + '.fbx'), use_selection=True, object_types={'MESH'},
                         axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                         mesh_smooth_type='FACE', embed_textures=False, path_mode='STRIP')
print('PROP', name, sum(len(p.vertices) - 2 for p in ob.data.polygons), 'tris', 'tex', bool(img), 'height', round(hi.z - lo.z, 3))
