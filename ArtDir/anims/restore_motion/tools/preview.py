# Render preview strips of the retargeted actions with the racket and finger grip applied.
import bpy,sys,json,os,math
sys.path.insert(0,os.path.dirname(__file__))
from common import *
from pathlib import Path
P=Path(__file__).resolve().parents[1];args=json.loads(sys.argv[sys.argv.index('--')+1])
bpy.ops.wm.open_mainfile(filepath=str(P/args.get('blend','Hero_RestoreMotion_v5.blend')))
rig=bpy.data.objects['Hero_01_Rig'];G=json.loads((P/'tools/grip.json').read_text())
for o in bpy.data.objects:
 if o.type=='MESH' and o.name in ['Studio floor']:o.hide_render=False
r=build_racket();attach(r,rig,'Hand.R',socket_matrix(G));curl(rig,'R',G)
out=P/'preview';out.mkdir(exist_ok=True)
for name in args['clips']:
 act=bpy.data.actions['Hero_'+name+'_v5'];rig.animation_data.action=act;n=int(act.frame_range[1])
 if name in ('Backhand','Ready','CelebratePoint','HitPerfect','MatchWin'):curl(rig,'L',dict(G,c1=G['c1']*.9))
 else:
  for b in rig.pose.bones:
   if b.name.endswith('.L') and any(k in b.name for k in FINGERS+['Thumb']):b.rotation_quaternion=(1,0,0,0)
 frames=args.get('frames') or [int(1+k*(n-1)/(args.get('count',10)-1)) for k in range(args.get('count',10))]
 for k,f in enumerate(frames):
  bpy.context.scene.frame_set(f)
  for cam,tag in args.get('cams',[[[1.6,-2.6,1.3],'f']]):
   render(out/f'{name}_{tag}_{k:02d}.png',Vector(cam),Vector((0,0,.85)),lens=args.get('lens',40),res=args.get('res',360))
print('PREVIEW_DONE')
