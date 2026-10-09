"""Author real pose-space knee correctives from volume-preserving deformation, exporting standard morphs."""
import bpy,math,json
from pathlib import Path
from mathutils import Matrix,Vector
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));rig=bpy.data.objects['Hero_01_Rig'];report={}
for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
def evaluated(o):
 bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();ee=o.evaluated_get(dg);m=ee.to_mesh();p=[v.co.copy() for v in m.vertices];ee.to_mesh_clear();return p
for name in ['Body_Legs','Body_Legs_Full']:
 o=bpy.data.objects[name];arm=next(m for m in o.modifiers if m.type=='ARMATURE');masks=[m for m in o.modifiers if m.type=='MASK'];state=[(m.show_viewport,m.show_render) for m in masks]
 for m in masks:m.show_viewport=m.show_render=False
 for k in o.data.shape_keys.key_blocks:k.value=0
 basis=[v.co.copy() for v in o.data.shape_keys.key_blocks[0].data];stats=[]
 for side in ['L','R']:
  for angle in [60,110]:
   pb=rig.pose.bones['LowerLeg.'+side];pb.rotation_mode='XYZ';pb.rotation_euler=(math.radians(angle),0,0)
   arm.use_deform_preserve_volume=True;target=evaluated(o);arm.use_deform_preserve_volume=False
   key=o.shape_key_add(name='Knee_'+side+'_'+str(angle),from_mix=False);key.slider_max=1
   changes=[]
   for i,(v,p) in enumerate(zip(o.data.vertices,basis)):
    if (p.x>0)!=(side=='L') or not .34<p.z<.535:continue
    mats=[]
    for g in v.groups:
     n=o.vertex_groups[g.group].name
     if n in rig.data.bones and g.weight>0:
      M=o.matrix_world.inverted()@rig.matrix_world@rig.pose.bones[n].matrix@rig.data.bones[n].matrix_local.inverted()@rig.matrix_world.inverted()@o.matrix_world;mats.append((g.weight,M))
    A=Matrix(((0,0,0,0),)*4)
    for w,M in mats:
     for r in range(4):
      for c in range(4):A[r][c]+=w*M[r][c]
    if not mats:continue
    rest=A.inverted_safe()@target[i];delta=rest-p
    # Ease correction smoothly outside the six-ring joint zone.
    f=max(0,min(1,(p.z-.34)/.025))*max(0,min(1,(.535-p.z)/.025));key.data[i].co=p+delta*f;changes.append(delta.length*f)
   key.value=1;corrected=evaluated(o);core=[i for i,p in enumerate(basis) if (p.x>0)==(side=='L') and .39<p.z<.49]
   err=max((corrected[i]-target[i]).length for i in core);stats.append({'side':side,'angle':angle,'max_rest_delta_mm':max(changes)*1000,'core_pose_error_mm':err*1000,'vertices_affected':sum(x>1e-8 for x in changes)})
   assert err<.0002,'Corrective did not reconstruct volume-preserving target'
   key.value=0;pb.matrix_basis=Matrix.Identity(4)
 for m,(v,r) in zip(masks,state):m.show_viewport=v;m.show_render=r
 report[name]=stats
arm.use_deform_preserve_volume=False
for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
(O/'knee-correctives.json').write_text(json.dumps(report,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));print('KNEE_CORRECTIVES',report)
