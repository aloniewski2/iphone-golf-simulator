# Grip pose + socket as coordinates in a geometric hand frame (W across knuckles index->pinky, F along middle finger, N palm-ward).
# Frame is rebuilt from bone positions in Unity, so FBX axis/handedness conventions cannot corrupt it.
import bpy,sys,os,json
sys.path.insert(0,os.path.dirname(__file__))
from common import *
from pathlib import Path
P=Path(__file__).resolve().parents[1];G=json.loads((P/'tools/grip.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_FingerRig.blend'))
rig=bpy.data.objects['Hero_01_Rig'];B=rig.data.bones
def frame(s):
 h=lambda n:B[n+'.'+s].head_local
 F=(h('Middle2')-h('Middle1')).normalized();W=h('Pinky1')-h('Index1');W=(W-F*W.dot(F)).normalized()
 N=W.cross(F)
 if N.dot(h('Thumb1')-h('Index1'))<0:N=-N
 return W,F,N
def coords(v,fr):return [v.dot(fr[0]),v.dot(fr[1]),v.dot(fr[2])]
out={}
GL=dict(G,c1=G['c1']*.9)
for s,g in [('R',G),('L',GL)]:
 fr=frame(s);curl(rig,s,g);bpy.context.view_layer.update()
 bones=[]
 for pb in rig.pose.bones:
  if not pb.name.endswith('.'+s) or not any(pb.name.startswith(k) for k in FINGERS+['Thumb']):continue
  m=pb.matrix.to_3x3();r=pb.bone.matrix_local.to_3x3()
  bones.append({'bone':pb.name,'dir':coords(m.col[1],fr),'sec':coords(m.col[2],fr),'restDir':coords(r.col[1],fr),'restSec':coords(r.col[2],fr)})
 out['grip'+s]=bones
fr=frame('R');S=socket_matrix(G);H=B['Hand.R'].matrix_local
W=H@S  # racket in armature space (rest hand)
out['socket']={'origin':coords(W.translation-B['Hand.R'].head_local,fr),'y':coords(W.to_3x3().col[1],fr),'z':coords(W.to_3x3().col[2],fr)}
out['grip_params']=G
(P/'grip_pose.json').write_text(json.dumps(out,indent=1));print('GRIP_EXPORTED',len(out['gripR']),len(out['gripL']))
