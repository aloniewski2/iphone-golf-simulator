import bpy,sys,json,math
sys.path.insert(0,__import__('os').path.dirname(__file__))
from common import *
from pathlib import Path
P=Path(__file__).resolve().parents[1];p=json.loads(sys.argv[sys.argv.index('--')+1])
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_FingerRig.blend'))
rig=bpy.data.objects['Hero_01_Rig']
for o in bpy.data.objects:
 if o.type=='MESH' and o.name not in ['Body_Skin','Shirt_Default','Shorts_Default']:o.hide_render=True
# Put the right forearm forward so the hand is visible.
ua=rig.pose.bones['UpperArm.R'];ua.rotation_mode='XYZ';ua.rotation_euler=(math.radians(p.get('ua',-40)),0,0)
la=rig.pose.bones['LowerArm.R'];la.rotation_mode='XYZ';la.rotation_euler=(math.radians(p.get('la',-60)),0,0)
curl(rig,'R',p);r=build_racket();attach(r,rig,'Hand.R',socket_matrix(p));bpy.context.view_layer.update()
h=rig.matrix_world@rig.pose.bones['Hand.R'].tail
out=P/'grip_iter';out.mkdir(exist_ok=True);tag=p.get('tag','t')
for i,d in enumerate(p.get('views',[(0,-.45,0.05),(.40,-.2,.1),(-.35,-.25,-.05),(0.05,-.05,.45)])):
 render(out/f'{tag}_{i}.png',h+Vector(d),h,lens=50,res=480)
