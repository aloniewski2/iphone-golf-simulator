import bpy,sys,os,json,math
sys.path.insert(0,os.path.dirname(__file__))
from mathutils import Vector,Quaternion,Matrix
P=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.open_mainfile(filepath=P+'/Hero_RestoreMotion_v5.blend');rig=bpy.data.objects['Hero_01_Rig']
RQ={b.name:b.matrix_local.to_quaternion() for b in rig.data.bones}
def twist_about_y(q):
 p=Vector((q.x,q.y,q.z));t=Quaternion((q.w,0,p.y,0)).normalized();a=math.degrees(2*math.atan2(t.y,t.w));return (a+180)%360-180
for act in bpy.data.actions:
 rig.animation_data.action=act;n=int(act.frame_range[1]);tw=[];fl=[]
 for f in range(1,n+1,2):
  bpy.context.scene.frame_set(f)
  L=rig.pose.bones['LowerArm.L'].matrix.to_quaternion();H=rig.pose.bones['Hand.L'].matrix.to_quaternion();U=rig.pose.bones['UpperArm.L'].matrix.to_quaternion()
  rel=(L@RQ['LowerArm.L'].inverted()@RQ['Hand.L']).inverted()@H   # hand vs neutral-on-forearm
  fa=(U@RQ['UpperArm.L'].inverted()@RQ['LowerArm.L']).inverted()@L  # forearm vs neutral-on-upperarm
  tw.append(twist_about_y(rel));fl.append(twist_about_y(fa))
 print('LEFT',act.name,'wrist twist p5/50/95 %.0f %.0f %.0f'%tuple(sorted(tw)[k] for k in (len(tw)//20,len(tw)//2,-1-len(tw)//20)),'| forearm twist %.0f %.0f %.0f'%tuple(sorted(fl)[k] for k in (len(fl)//20,len(fl)//2,-1-len(fl)//20)))
