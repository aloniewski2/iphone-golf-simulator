import bpy,json,math
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01.blend'))
objs=list(bpy.data.collections['Hero_01'].objects);ms=[o for o in objs if o.type=='MESH'];rs=[o for o in objs if o.type=='ARMATURE']
r={'armatures':len(rs),'bones':len(rs[0].data.bones),'meshes':len(ms),'unweighted_vertices':0,'bad_weight_sums':0,'max_influences':0,'identity_transforms':True,'zero_area_triangles':0,'triangles':0,'sockets':{},'mesh_stats':{}}
for o in ms:
 r['identity_transforms'] &= o.location.length<1e-5 and max(abs(x) for x in o.rotation_euler)<1e-5 and max(abs(x-1) for x in o.scale)<1e-5
 o.data.calc_loop_triangles();r['triangles']+=len(o.data.loop_triangles);r['mesh_stats'][o.name]={'vertices':len(o.data.vertices),'triangles':len(o.data.loop_triangles),'uv_layers':len(o.data.uv_layers)}
 for v in o.data.vertices:
  g=[g for g in v.groups if g.weight>0];r['unweighted_vertices']+=not bool(g);r['bad_weight_sums']+=abs(sum(x.weight for x in g)-1)>1e-4;r['max_influences']=max(r['max_influences'],len(g))
 for t in o.data.loop_triangles:
  a,b,c=(o.data.vertices[i].co for i in t.vertices);r['zero_area_triangles']+=(b-a).cross(c-a).length<1e-12
r['height']=max(v.co.z for o in ms for v in o.data.vertices)-min(v.co.z for o in ms for v in o.data.vertices)
r['head_chin_z']=1.25*1.7/1.655;r['head_ratio']=(r['height']-r['head_chin_z'])/r['height']
r['actions']=len(bpy.data.actions);r['textures']={im.name:list(im.size) for im in bpy.data.images if im.type=='IMAGE' and im.users>0}
for n in ['Hat','Hand_R','Hand_L','Back','FaceExtra']:
 o=bpy.data.objects[n];r['sockets'][n]={'position':list(o.matrix_world.translation),'parent_bone':o.parent_bone}
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(P/'Hero_01.fbx'))
r['fbx']={'armatures':sum(o.type=='ARMATURE' for o in bpy.data.objects),'meshes':sum(o.type=='MESH' for o in bpy.data.objects),'all_sockets':all(n in bpy.data.objects for n in ['Hat','Hand_R','Hand_L','Back','FaceExtra']),'actions':len(bpy.data.actions),'embedded_textures':{i.name:list(i.size) for i in bpy.data.images if i.type=='IMAGE'}}
r['texture_budget_pass']=all(max(dim)<=2048 for dim in r['fbx']['embedded_textures'].values())
r['pass']=r['texture_budget_pass'] and r['armatures']==1 and r['fbx']['armatures']==1 and r['unweighted_vertices']==0 and r['bad_weight_sums']==0 and r['identity_transforms'] and r['zero_area_triangles']==0 and r['fbx']['all_sockets'] and r['actions']==0
(P/'verification.json').write_text(json.dumps(r,indent=2));print('VERIFY',json.dumps(r))
if not r['pass']:raise RuntimeError('Verification failed; inspect verification.json')
