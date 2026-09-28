import bpy,json,math
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(P.parent/'blockout/Hero_01_tripo.glb'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
info=[]
for o in meshes:
 bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.transform_apply(location=False,rotation=True,scale=True);o.select_set(False)
 o.data.calc_loop_triangles();info.append({'name':o.name,'vertices':len(o.data.vertices),'triangles':len(o.data.loop_triangles),'bounds':[list(o.matrix_world@Vector(c)) for c in o.bound_box],'materials':[m.name for m in o.data.materials]})
print('RAW_INFO',json.dumps(info),flush=True)
for im in bpy.data.images:
 if im.type=='IMAGE' and im.size[0]>0:
  print('IMAGE',im.name,list(im.size),flush=True)
# Normalize orientation-independent bounds, retain imported Z-up assumed by GLTF importer.
pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices];lo=Vector(tuple(min(v[i] for v in pts) for i in range(3)));hi=Vector(tuple(max(v[i] for v in pts) for i in range(3)))
scale=1.7/(hi.z-lo.z);center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
for o in meshes:
 for v in o.data.vertices:v.co=(o.matrix_world@v.co-center)*scale
 o.matrix_world.identity()
 for m in o.data.materials:
  if m and m.use_nodes:
   bs=m.node_tree.nodes.get('Principled BSDF')
   if bs:
    for inp in ['Metallic','Roughness','Normal']:
     for l in list(bs.inputs[inp].links):m.node_tree.links.remove(l)
    bs.inputs['Metallic'].default_value=0;bs.inputs['Roughness'].default_value=.68
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=900;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Review World');scene.world.color=(.22,.22,.22);scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
for pos,power,size in [((-3,-4,5),500,4),((4,-2,3),200,3),((2,3,4),400,3)]:
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.9))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=2.05;scene.camera=cam
for name,pos in [('minus_y',(0,-5,.85)),('plus_x',(5,0,.85)),('plus_y',(0,5,.85))]:
 cam.location=pos;cam.rotation_euler=(Vector((0,0,.85))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(P/('raw_'+name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'raw_inspection.blend'))
