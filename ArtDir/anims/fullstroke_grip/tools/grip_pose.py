import bpy,json,math
from mathutils import Vector,Matrix,Quaternion
from pathlib import Path
P=Path(__file__).resolve().parents[1];bpy.ops.wm.open_mainfile(filepath=str(P.parent/'knees_offarm/Hero_01_Knees_QA.blend'));r=bpy.data.objects['Hero_01_Rig'];o=bpy.data.objects['Body_Skin'];r.animation_data_clear()
for b in r.pose.bones:b.matrix_basis.identity()
bpy.context.view_layer.objects.active=o
for m in list(o.modifiers):
 if m.type=='MASK':bpy.ops.object.modifier_apply(modifier=m.name)
o.shape_key_add(name='Basis');C=Matrix(((-1,0,0),(0,0,1),(0,-1,0)));data=json.loads((P.parent/'anim_repair/gameplay_pose_bake.json').read_text());report={}
for s in ['R','L']:
 b=r.data.bones['Hand.'+s];inv=b.matrix_local.inverted();key=o.shape_key_add(name='Grip_'+s);idx=o.vertex_groups['Hand.'+s].index
 for v in o.data.vertices:
  w=next((g.weight for g in v.groups if g.group==idx),0)
  if w<.1:continue
  p=inv@v.co;old=p.copy();theta=math.radians(60 if s=='R' else 0);axis=Vector((0,math.sin(theta),-math.cos(theta)));cradle=Vector((-.060,.092,-.020))
  # Continuous phalange curl; no discontinuous finger/thumbnail region boundaries.
  if p.y>.085:
   angle=min(2.5,(p.y-.085)/.030);radius=max(.008,.060+p.x)
   p.x=-.060+radius*math.cos(angle);p.y=.085+radius*math.sin(angle)
  key.data[v.index].co=b.matrix_local@(old.lerp(p,min(1,w*1.2)))
 ru=data['restRotations'][data['bones'].index('Hand.'+s)];qu=Quaternion((ru['w'],ru['x'],ru['y'],ru['z']));L=qu.inverted().to_matrix()@C@b.matrix_local.to_3x3();center=L@Vector((-.060,.092,-.020))
 report[s]={'center':list(center),'posed_handle_axis':list(axis),'localConversion':[list(row) for row in L]}
# Export default zero shape to retain run geometry.
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);r.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(P/'Hero_01_GripBody.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=False,axis_forward='-Z',axis_up='Y')
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_GripPose.blend'));(P/'grip_pose.json').write_text(json.dumps(report,indent=2));print(report)
