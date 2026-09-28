import bpy,sys,json,math
sys.path.insert(0,__import__('os').path.dirname(__file__))
from common import *
from pathlib import Path
P=Path(__file__).resolve().parents[1];p=json.loads(sys.argv[sys.argv.index('--')+1])
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_FingerRig.blend'))
rig=bpy.data.objects['Hero_01_Rig']
for o in bpy.data.objects:
 if o.type=='MESH' and o.name!='Body_Skin':o.hide_render=True
if 'c1' in p:curl(rig,'R',p)
rig.data.show_axes=True
bpy.context.view_layer.update()
M=rig.matrix_world@rig.data.bones['Hand.R'].matrix_local
c=M@Vector((0,.08,0))
out=P/'grip_iter';tag=p.get('tag','hv')
# Views along hand-local axes: +X (palm side), -X (back), -Z (thumb side), +Z (pinky side)
for i,ax in enumerate([(1,0,0),(-1,0,0),(0,0,-1),(0,0,1)]):
 d=(M.to_3x3()@Vector(ax)).normalized()*.40
 render(out/f'{tag}_{i}.png',c+d,c,lens=60,res=400)
