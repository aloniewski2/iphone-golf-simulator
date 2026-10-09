import bpy,json,math,hashlib
from pathlib import Path
from collections import defaultdict
P=Path(__file__).resolve().parent;report={}
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'));rig=bpy.data.objects['Hero_01_Rig'];bn=set(b.name for b in rig.data.bones);meshes=[o for o in bpy.data.collections['Hero_01'].objects if o.type=='MESH'];scene=bpy.context.scene
report['armatures']=len([o for o in bpy.data.objects if o.type=='ARMATURE']);report['unweighted_vertices']=0;report['bad_weight_sums']=0;report['max_influences']=0;report['wrong_armatures']=[];report['linear_skinning']=True
seams=defaultdict(list)
for o in meshes:
 mods=[m for m in o.modifiers if m.type=='ARMATURE']
 if len(mods)!=1 or mods[0].object!=rig:report['wrong_armatures'].append(o.name)
 report['linear_skinning'] &= all(not m.use_deform_preserve_volume for m in mods)
 for v in o.data.vertices:
  w={o.vertex_groups[g.group].name:g.weight for g in v.groups if o.vertex_groups[g.group].name in bn and g.weight>0}
  report['unweighted_vertices']+=not bool(w);report['bad_weight_sums']+=abs(sum(w.values())-1)>1e-4;report['max_influences']=max(report['max_influences'],len(w))
  seams[tuple(round(float(x),5) for x in v.co)].append((o.name,v.index,w))
pairs=[vs for vs in seams.values() if len({v[0] for v in vs})>1];report['shared_seam_points']=len(pairs);report['max_seam_weight_difference']=max((max(abs(vs[0][2].get(n,0)-v[2].get(n,0)) for n in bn) for vs in pairs for v in vs[1:]),default=0)
report['sockets']={n:{'parent':bpy.data.objects[n].parent.name,'bone':bpy.data.objects[n].parent_bone} for n in ['Hat','Hand_R','Hand_L','Back','FaceExtra']}
report['motion_bounds']={}
for clip in ['Idle','Ready']:
 a=bpy.data.actions[clip];rig.animation_data.action=a;result=[]
 for f in range(int(a.frame_range[0]),int(a.frame_range[1])+1,5):
  scene.frame_set(f);dg=bpy.context.evaluated_depsgraph_get();coords=[]
  for o in meshes:
   ev=o.evaluated_get(dg);me=ev.to_mesh();coords.extend(tuple(o.matrix_world@v.co) for v in me.vertices);ev.to_mesh_clear()
  assert all(math.isfinite(x) for co in coords for x in co)
  result.append({'frame':f,'min':[min(co[i] for co in coords) for i in range(3)],'max':[max(co[i] for co in coords) for i in range(3)]})
 report['motion_bounds'][clip]=result
report['exports']={};sigs=[]
for f in [P/'Hero_01_Mixamo_Bind.fbx',P/'Idle.fbx',P/'Ready.fbx']+sorted((P/'wardrobe').glob('*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(f));rs=[o for o in bpy.data.objects if o.type=='ARMATURE'];ms=[o for o in bpy.data.objects if o.type=='MESH'];r=rs[0]
 sig=[(b.name,b.parent.name if b.parent else None,[round(x,5) for row in b.matrix_local for x in row]) for b in r.data.bones];digest=hashlib.sha256(json.dumps(sig).encode()).hexdigest();sigs.append(digest)
 report['exports'][str(f.relative_to(P))]={'armatures':len(rs),'bones':len(r.data.bones),'signature':digest,'meshes':[o.name for o in ms],'actions':[{'name':a.name,'frames':list(a.frame_range)} for a in bpy.data.actions]}
report['identical_exported_skeletons']=len(set(sigs))==1
report['passed']=report['armatures']==1 and not report['wrong_armatures'] and not report['unweighted_vertices'] and not report['bad_weight_sums'] and report['max_influences']<=4 and report['linear_skinning'] and report['identical_exported_skeletons']
(P/'verification.json').write_text(json.dumps(report,indent=2));print('VERIFY',report['passed'],'seam_diff',report['max_seam_weight_difference'])
if not report['passed']:raise RuntimeError('Verification failed')
