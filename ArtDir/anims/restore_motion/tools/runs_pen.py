# Arm-vs-torso penetration for the restored run clips, same torso model as the retarget solver.
import bpy,json,math,os
from mathutils import Vector
P=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VOL=json.load(open(P+'/tools/body_volume.json'));SL=VOL['slices']
R='/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/ArtDirection/Hero01/Models/'
def shape(z):
 for a,b in zip(SL,SL[1:]):
  if a['z']<=z<=b['z']:
   t=(z-a['z'])/(b['z']-a['z']);f=lambda k:a[k]*(1-t)+b[k]*t;return f('a'),(f('ymin')+f('ymax'))/2,(f('ymax')-f('ymin'))/2
for n in ['RunForward','RunRight','RunLeft']:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=R+'Hero_Gameplay_'+n+'_v1.fbx')
 rig=[o for o in bpy.data.objects if o.type=='ARMATURE'][0];s0,e0=map(int,rig.animation_data.action.frame_range)
 rest={b.name:(b.matrix_local.copy()) for b in rig.data.bones}
 worst={'L':(0,0),'R':(0,0)};cnt={'L':0,'R':0}
 for f in range(s0,e0+1):
  bpy.context.scene.frame_set(f)
  def to_rest(p):
   for bn,zl in (('Chest',1.03),('Spine',.87),('Hips',-9)):
    M=rig.matrix_world@rig.pose.bones[bn].matrix;r=(rig.matrix_world@rest[bn])@(M.inverted()@p)
    if r.z>=zl:return r
   return r
  def pen(p,rad):
   r=to_rest(p);sh=shape(r.z)
   if not sh:return 0
   a,cy,b=sh;x=r.x;y=r.y-cy;s=math.sqrt((x/a)**2+(y/b)**2)
   if s<1e-6:return rad
   return max(0,rad+.005-(s-1)*math.sqrt(x*x+y*y)/s)
  for sd in 'LR':
   pb=rig.pose.bones;M=rig.matrix_world
   S=M@pb['UpperArm.'+sd].head;E=M@pb['LowerArm.'+sd].head;W=M@pb['Hand.'+sd].head;hd=(M.to_3x3()@(pb['Hand.'+sd].matrix.to_3x3()@Vector((0,1,0)))).normalized()
   c=sum(pen(S.lerp(E,t),VOL['UpperArm']*.85) for t in (.8,1))+sum(pen(E.lerp(W,t),VOL['LowerArm']) for t in (0,.25,.5,.75,1))+pen(W+hd*.07,VOL['Hand']*.9)
   if c>.01:cnt[sd]+=1
   if c>worst[sd][0]:worst[sd]=(c,f)
 print('RUNPEN',n,'frames>1cm L',cnt['L'],'R',cnt['R'],'worst',{k:(round(v[0],3),v[1]) for k,v in worst.items()},'of',e0-s0+1)
