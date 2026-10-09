import bpy,sys,os,json,math
sys.path.insert(0,os.path.dirname(__file__))
from common import *
from pathlib import Path
P=Path(__file__).resolve().parents[1];args=json.loads(sys.argv[sys.argv.index('--')+1])
bpy.ops.wm.open_mainfile(filepath=str(P/args.get('blend','Hero_RestoreMotion_v5.blend')))
rig=bpy.data.objects['Hero_01_Rig'];G=json.loads((P/'tools/grip.json').read_text());R=json.loads((P/'retarget_report.json').read_text())['clips']
r=build_racket()
out=P/'preview';out.mkdir(exist_ok=True)
for k,(clip,f) in enumerate(args['shots']):
 roll=R[clip].get('grip_roll_deg',0);attach(r,rig,'Hand.R',socket_matrix(G)@Matrix.Rotation(math.radians(roll),4,'Y'))
 rig.animation_data.action=bpy.data.actions['Hero_'+clip+'_v5'];bpy.context.scene.frame_set(f)
 curl(rig,'R',G)
 if clip in('Backhand','Ready'):curl(rig,'L',dict(G,c1=G['c1']*.9))
 else:
  for b in rig.pose.bones:
   if b.name.endswith('.L') and any(x in b.name for x in FINGERS+['Thumb']):b.rotation_quaternion=(1,0,0,0)
 bpy.context.view_layer.update()
 h=rig.matrix_world@rig.pose.bones['Hand.L'].matrix.translation
 fwd=Vector((0,-1,0))
 for j,d in enumerate(args.get('dirs',[[0.1,-0.6,0.15],[0.6,-0.2,0.1]])):
  render(out/f"L_{args['tag']}_{k}_{j}.png",h+Vector(d),h,lens=45,res=360)
