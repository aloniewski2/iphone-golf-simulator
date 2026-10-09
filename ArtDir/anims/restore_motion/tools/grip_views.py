import bpy,sys,json,math
sys.path.insert(0,__import__('os').path.dirname(__file__))
from common import *
from pathlib import Path
P=Path(__file__).resolve().parents[1];p=json.loads(sys.argv[sys.argv.index('--')+1])
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_FingerRig.blend'))
rig=bpy.data.objects['Hero_01_Rig']
for o in bpy.data.objects:
 if o.type=='MESH' and o.name!='Body_Skin':o.hide_render=True
curl(rig,'R',p);r=build_racket();attach(r,rig,'Hand.R',socket_matrix(p));bpy.context.view_layer.update()
M=rig.matrix_world@rig.data.bones['Hand.R'].matrix_local
c=M@Vector((0.02,.09,-.02))
out=P/'grip_iter';tag=p.get('tag','g')
for i,ax in enumerate(p.get('axes',[(1,0,0),(-1,0,0),(0,0,-1),(0,0,1),(.6,.3,-.6),(.5,-.2,.8)])):
 d=(M.to_3x3()@Vector(ax)).normalized()*p.get('dist',.42)
 render(out/f"{tag}_{i}.png",c+d,c,lens=p.get("lens",60),res=p.get("res",400))
