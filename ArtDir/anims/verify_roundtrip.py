import bpy,json
from pathlib import Path
from mathutils.kdtree import KDTree
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'));scene=bpy.context.scene;rig=bpy.data.objects['Hero_01_Rig'];rig.animation_data.action=bpy.data.actions['Ready'];scene.frame_set(38);dg=bpy.context.evaluated_depsgraph_get();expected={}
for o in bpy.data.collections['Hero_01'].objects:
 if o.type=='MESH':
  ev=o.evaluated_get(dg);me=ev.to_mesh();expected[o.name]=[tuple(o.matrix_world@v.co) for v in me.vertices];ev.to_mesh_clear()
for o in list(bpy.data.collections['Hero_01'].objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(P/'Hero_01_Mixamo_Bind.fbx'));r=next(o for o in bpy.data.objects if o.type=='ARMATURE');before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(P/'Ready.fbx'));added=set(bpy.data.objects)-before;sr=next(o for o in added if o.type=='ARMATURE');a=sr.animation_data.action;r.animation_data_create();r.animation_data.action=a;r.animation_data.action_slot=a.slots[0]
for o in added:bpy.data.objects.remove(o,do_unlink=True)
scene.frame_set(39);dg=bpy.context.evaluated_depsgraph_get();result={}
for name,coords in expected.items():
 o=bpy.data.objects[name];ev=o.evaluated_get(dg);me=ev.to_mesh();kd=KDTree(len(coords))
 for i,v in enumerate(coords):kd.insert(v,i)
 kd.balance();ds=[kd.find(o.matrix_world@v.co)[2] for v in me.vertices];ev.to_mesh_clear();result[name]={'max_error_m':max(ds),'mean_error_m':sum(ds)/len(ds)}
scene.render.resolution_percentage=50;scene.cycles.samples=16;scene.render.filepath=str(P/'roundtrip_Ready.png');bpy.ops.render.render(write_still=True)
report={'passed':all(v['max_error_m']<.001 for v in result.values()),'Ready_frame':38,'reimported_frame':39,'meshes':result};(P/'roundtrip.json').write_text(json.dumps(report,indent=2));print('ROUNDTRIP',json.dumps(report))
