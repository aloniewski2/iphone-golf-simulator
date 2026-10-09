import bpy,math,json
from pathlib import Path
from mathutils import Vector,Matrix
R=Path(__file__).resolve().parents[4];W=R/'ArtDir/tennis/audience/staging6';P=R/'proof/full-visual-overhaul/tennis/audience6';P.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(W/'TennisPromenadeHero3.blend'))
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.device='CPU';sc.cycles.samples=24;sc.render.threads_mode='FIXED';sc.render.threads=4;sc.render.resolution_x=720;sc.render.resolution_y=720;sc.render.resolution_percentage=100
sc.world=bpy.data.worlds.new('Continuous walker source studio');sc.world.use_nodes=True;sc.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.29,.39,.50,1);sc.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.6
bpy.ops.object.light_add(type='AREA',location=(2,-4,5));bpy.context.object.data.energy=700;bpy.context.object.data.size=5
bpy.ops.object.camera_add(location=(.2,-4,1.3));cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=1.95;sc.camera=cam;sc.view_settings.view_transform='AgX'
def rotate(bone,source,target):
 h=bone.head.copy();q=source.rotation_difference(target);bone.matrix=Matrix.Translation(h)@q.to_matrix().to_4x4()@Matrix.Translation(-h)@bone.matrix

def leg(rig,side,target):
 upper=rig.pose.bones['THIGH_'+side];lower=rig.pose.bones['SHIN_'+side];foot=rig.pose.bones['FOOT_'+side]
 hip=upper.head.copy();u=(lower.head-hip).length;l=(foot.head-lower.head).length;to=target-hip;d=min(u+l-.003,max(.05,to.length));axis=to.normalized();pole=Vector((0,-1,0))-axis*Vector((0,-1,0)).dot(axis);pole.normalize();along=(u*u-l*l+d*d)/(2*d);knee=hip+axis*along+pole*math.sqrt(max(0,u*u-along*along))
 rotate(upper,lower.head-hip,knee-hip);bpy.context.view_layer.update();rotate(lower,foot.head-lower.head,hip+axis*d-lower.head);bpy.context.view_layer.update()
 mat=rig.data.bones[foot.name].matrix_local.copy();mat.translation=foot.head.copy();foot.matrix=mat;bpy.context.view_layer.update()
for variant,sex in [(0,'male'),(1,'female')]:
 root=bpy.data.objects['FAN_'+str(variant)];rig=next(o for o in root.children if o.type=='ARMATURE');origin=root.location.copy();rest={b.name:b.matrix.copy() for b in rig.pose.bones}
 for detail in ['near','far']:
  for pose in ['ready','left_step','right_step']:
   for other in bpy.data.objects:
    if other.type=='MESH':other.hide_render=('_FAR' in other.name) if detail=='near' else not ('_FAR' in other.name)
   for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
   root.location=origin.copy();bpy.context.view_layer.update()
   if pose!='ready':
    root.location.z-=.04
    for side in ['L','R']:
     neutral=rig.data.bones['FOOT_'+side].head_local.copy();swing=(side=='L')==(pose=='left_step');neutral.y+=-.23 if swing else .16;neutral.z+=.04+(.065 if swing else 0);leg(rig,side,neutral)
   target=Vector((origin.x,-.03,.84));cam.location=target+Vector((2.0,-3,.10));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.90;sc.render.filepath=str(P/f'walker6-{sex}-{detail}-{pose}.png');bpy.ops.render.render(write_still=True)
   if pose=='left_step':
    target=Vector((origin.x,-.02,.48));cam.data.ortho_scale=.85;cam.location=target+Vector((2.5,-3,.18));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(P/f'walker6-{sex}-{detail}-knees.png');bpy.ops.render.render(write_still=True)
print('CONTINUOUS_WALKER_SOURCE_PROOF_COMPLETE')
