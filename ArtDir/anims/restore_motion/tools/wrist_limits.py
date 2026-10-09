import bpy,os,math
from mathutils import Vector,Quaternion
P=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.open_mainfile(filepath=P+'/Hero_RestoreMotion_v5.blend');rig=bpy.data.objects['Hero_01_Rig']
RQ={b.name:b.matrix_local.to_quaternion() for b in rig.data.bones}
for act in bpy.data.actions:
 rig.animation_data.action=act;n=int(act.frame_range[1]);row={}
 for s in 'LR':
  over=0;fl=[];dv=[];tw=[]
  for f in range(1,n+1):
   bpy.context.scene.frame_set(f)
   L=rig.pose.bones['LowerArm.'+s].matrix.to_quaternion();H=rig.pose.bones['Hand.'+s].matrix.to_quaternion();U=rig.pose.bones['UpperArm.'+s].matrix.to_quaternion()
   nn=L@RQ['LowerArm.'+s].inverted()@RQ['Hand.'+s];y=(nn.inverted()@H)@Vector((0,1,0))
   a=math.degrees(math.atan2(y.x,y.y));d=math.degrees(math.atan2(y.z,y.y));fl.append(a);dv.append(d)
   fa=(U@RQ['UpperArm.'+s].inverted()@RQ['LowerArm.'+s]).inverted()@L;p=Vector((fa.x,fa.y,fa.z));t=math.degrees(2*math.atan2(p.y,fa.w));t=(t+180)%360-180;tw.append(t)
   if abs(a)>80 or abs(d)>40 or abs(t)>90:over+=1
  row[s]='over-limit frames %d/%d  flex[%.0f,%.0f] dev[%.0f,%.0f] forearmTwist[%.0f,%.0f]'%(over,n,min(fl),max(fl),min(dv),max(dv),min(tw),max(tw))
 print('LIM',act.name,'| L:',row['L'],'| R:',row['R'])
