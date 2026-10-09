import bpy,pathlib,json
from mathutils import Vector
from mathutils.kdtree import KDTree
import math
R=pathlib.Path.cwd();O=R/'work/reference-rebuild/characters';report={}
for sex in ('Male','Female'):
 bpy.ops.wm.open_mainfile(filepath=str(O/f'{sex}_ReferenceBody.blend'));body=bpy.data.objects['Body'];me=body.data;cut=body['ReferenceNeckCutZ'];report[sex]={'flatFaces':sum(not p.use_smooth for p in me.polygons),'sharpEdges':sum(e.use_edge_sharp for e in me.edges),'neckSharpEdges':sum(e.use_edge_sharp and any(abs(me.vertices[i].co.z-cut)<.025 for i in e.vertices) for e in me.edges)}
 for p in me.polygons:p.use_smooth=True
 for e in me.edges:
  if any(abs(me.vertices[i].co.z-cut)<.030 for i in e.vertices):e.use_edge_sharp=False
 # Clear inherited custom corner normals only on this reconstructed source mesh.
 if me.has_custom_normals:me.normals_split_custom_set([(0,0,0)]*len(me.loops))
 me.update()
 kd=KDTree(len(me.vertices))
 for v in me.vertices:kd.insert(v.co,v.index)
 kd.balance();base=[v.normal.copy() for v in me.vertices];normals=base[:];changed=0
 for v in me.vertices:
  z=v.co.z;weight=math.exp(-((z-(cut+.020))/.033)**4)
  if weight<.01:continue
  avg=Vector();ws=0
  for co,idx,d in kd.find_range(v.co,.020):
   w=(1-d/.020)**2;avg+=base[idx]*w;ws+=w
  if ws>0 and avg.length>.1:
   n=base[v.index].lerp(avg.normalized(),weight).normalized();normals[v.index]=n;changed+=1
 me.normals_split_custom_set([normals[l.vertex_index] for l in me.loops]);me.update();report[sex]['neckNormalsFair']=changed
 bpy.ops.wm.save_as_mainfile(filepath=str(O/f'{sex}_ReferenceBody_Smooth.blend'))
 for ob in bpy.context.scene.objects:ob.select_set(ob.type in ('MESH','ARMATURE'))
 bpy.ops.export_scene.fbx(filepath=str(O/f'{sex}_ReferenceBody_Smooth.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=False,axis_forward='-Z',axis_up='Y')
(O/'graft-smoothing.json').write_text(json.dumps(report,indent=2));print('SMOOTH_AUDIT',report)
