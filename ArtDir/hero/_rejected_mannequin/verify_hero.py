import bpy,json
from pathlib import Path
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01.blend'))
C=bpy.data.collections['Hero_01'];objs=list(C.objects);ms=[o for o in objs if o.type=='MESH'];rigs=[o for o in objs if o.type=='ARMATURE']
report={'armatures':len(rigs),'mesh_count':len(ms),'unweighted_vertices':0,'bad_weight_sums':0,'max_influences':0,'transforms_identity':True,'zero_area_triangles':0,'socket_positions':{},'body_bounds':[]}
for o in ms:
 report['transforms_identity'] &= max(abs(v) for v in o.location)<1e-5 and max(abs(v) for v in o.rotation_euler)<1e-5 and max(abs(v-1) for v in o.scale)<1e-5
 for v in o.data.vertices:
  gs=[g for g in v.groups if g.weight>0];report['unweighted_vertices']+=not bool(gs);report['bad_weight_sums']+=abs(sum(g.weight for g in gs)-1)>1e-4;report['max_influences']=max(report['max_influences'],len(gs))
 o.data.calc_loop_triangles()
 for t in o.data.loop_triangles:
  a,b,c=[o.data.vertices[i].co for i in t.vertices];report['zero_area_triangles']+=(b-a).cross(c-a).length<1e-10
for n in ['Hat','Hand_R','Hand_L','Back','FaceExtra']:report['socket_positions'][n]=list(bpy.data.objects[n].matrix_world.translation)
report['rest_pose']=all(p.rotation_quaternion.angle<1e-5 and p.location.length<1e-5 for r in rigs for p in r.pose.bones)
report['actions']=len(bpy.data.actions)
report['bones']=len(rigs[0].data.bones)
report['materials']=sorted({m.name for o in ms for m in o.data.materials})
report['triangles']=sum(len(o.data.loop_triangles) for o in ms)
report['height']=max(v.co.z for o in ms for v in o.data.vertices)-min(v.co.z for o in ms for v in o.data.vertices)
report['head_ratio']=(report['height']-1.313)/report['height']
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(P/'Hero_01.fbx'))
report['fbx_reimport']={'armatures':sum(o.type=='ARMATURE' for o in bpy.data.objects),'meshes':sum(o.type=='MESH' for o in bpy.data.objects),'sockets':all(n in bpy.data.objects for n in ['Hat','Hand_R','Hand_L','Back','FaceExtra']),'actions':len(bpy.data.actions),'images':[(i.name,i.size[:]) for i in bpy.data.images if i.type=='IMAGE']}
(P/'verification.json').write_text(json.dumps(report,indent=2));print('VERIFICATION',json.dumps(report))
