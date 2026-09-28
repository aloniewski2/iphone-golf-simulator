import bpy,json,hashlib
from pathlib import Path
from collections import Counter
P=Path(__file__).resolve().parent
report={}
def triangles(objs,evaluated=False):
 out=Counter();dg=bpy.context.evaluated_depsgraph_get()
 for o in objs:
  if o.type!='MESH':continue
  ob=o.evaluated_get(dg) if evaluated else o;me=ob.to_mesh() if evaluated else ob.data;me.calc_loop_triangles()
  for t in me.loop_triangles:
   key=tuple(sorted(tuple(round(float(c),5) for c in (o.matrix_world@me.vertices[i].co)) for i in t.vertices));out[key]+=1
  if evaluated:ob.to_mesh_clear()
 return out
bpy.ops.wm.open_mainfile(filepath=str(P.parent/'Hero_01.blend'));source=triangles(bpy.data.collections['Hero_01'].objects,True)
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Assembled.blend'));objs=list(bpy.data.collections['Hero_01'].objects);rigs=[o for o in objs if o.type=='ARMATURE'];rig=rigs[0];bn={b.name for b in rig.data.bones};assembled=triangles(objs,True)
report['source_triangles']=sum(source.values());report['default_triangles']=sum(assembled.values());report['missing_approved_triangles']=sum((source-assembled).values());report['extra_visible_triangles']=sum((assembled-source).values());report['assembled_armatures']=len(rigs);report['wrong_armature_modifiers']=[];report['bad_weight_sums']=0;report['unweighted_vertices']=0
for o in objs:
 if o.type!='MESH':continue
 mods=[m for m in o.modifiers if m.type=='ARMATURE']
 if len(mods)!=1 or mods[0].object!=rig:report['wrong_armature_modifiers'].append(o.name)
 for v in o.data.vertices:
  gs=[g.weight for g in v.groups if o.vertex_groups[g.group].name in bn and g.weight>0];report['unweighted_vertices']+=not bool(gs);report['bad_weight_sums']+=abs(sum(gs)-1)>1e-4
report['sockets']=all(n in bpy.data.objects and bpy.data.objects[n].parent==rig for n in ['Hat','Hand_R','Hand_L','Back','FaceExtra'])
report['exports']={};sigs=[]
for f in sorted(P.glob('*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(f));rs=[o for o in bpy.data.objects if o.type=='ARMATURE'];ms=[o for o in bpy.data.objects if o.type=='MESH'];sig=[]
 for b in rs[0].data.bones:sig.append((b.name,b.parent.name if b.parent else None,tuple(round(float(x),6) for row in b.matrix_local for x in row)))
 digest=hashlib.sha256(json.dumps(sig).encode()).hexdigest();sigs.append(digest)
 report['exports'][f.name]={'armatures':len(rs),'meshes':[o.name for o in ms],'bone_count':len(rs[0].data.bones),'skeleton_signature':digest,'vertices':sum(len(o.data.vertices) for o in ms),'actions':len(bpy.data.actions),'materials':sorted({m.name for o in ms for m in o.data.materials})}
report['identical_skeletons']=len(set(sigs))==1
report['full_body_has_foundation']=report['exports']['Hero_01_Body.fbx']['vertices']>report['exports']['Hero_01_Body_DefaultCoverage.fbx']['vertices']
report['passed']=report['assembled_armatures']==1 and not report['wrong_armature_modifiers'] and report['bad_weight_sums']==0 and report['unweighted_vertices']==0 and report['sockets'] and report['identical_skeletons'] and report['missing_approved_triangles']==0 and report['extra_visible_triangles']==0 and report['full_body_has_foundation']
(P/'verification.json').write_text(json.dumps(report,indent=2));print('MODULAR_VERIFY',json.dumps(report))
if not report['passed']:raise RuntimeError('Modular verification failed')
