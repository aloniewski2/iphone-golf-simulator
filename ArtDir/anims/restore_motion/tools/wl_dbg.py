import bpy,os,math
from mathutils import Vector
P=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.open_mainfile(filepath=P+'/Hero_RestoreMotion_v5.blend');rig=bpy.data.objects['Hero_01_Rig']
RQ={b.name:b.matrix_local.to_quaternion() for b in rig.data.bones}
rig.animation_data.action=bpy.data.actions['Hero_Forehand_v5']
for f in (21,41):
 bpy.context.scene.frame_set(f)
 print('WL',f,[round(x,3) for x in rig.pose.bones['LowerArm.R'].matrix.to_quaternion()],[round(x,3) for x in rig.pose.bones['Hand.R'].matrix.to_quaternion()],rig.pose.bones['Hand.R'].rotation_mode,[c.type for c in rig.pose.bones['Hand.R'].constraints])
print('ACTIONS',[a.name for a in bpy.data.actions],rig.animation_data.nla_tracks[:] if rig.animation_data else None)
for f in (21,41):
 bpy.context.scene.frame_set(f)
 L=rig.pose.bones['LowerArm.R'].matrix.to_quaternion();H=rig.pose.bones['Hand.R'].matrix.to_quaternion()
 nn=L@RQ['LowerArm.R'].inverted()@RQ['Hand.R'];y=(nn.inverted()@H)@Vector((0,1,0))
 print('WLF',f,round(math.degrees(math.atan2(y.x,y.y))),round(math.degrees(math.atan2(y.z,y.y))))
