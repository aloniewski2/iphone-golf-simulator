import bpy,json,sys,os,math
sys.path.insert(0,os.path.dirname(__file__))
from common import *
from pathlib import Path
P=Path(__file__).resolve().parents[1];G=json.loads((P/'tools/grip.json').read_text());R=json.loads((P/'retarget_report.json').read_text())['clips']
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_RestoreMotion_v5.blend'));rig=bpy.data.objects['Hero_01_Rig']
for n,c in R.items():
 if not c['contact_s']:continue
 roll=c.get('grip_roll_deg',0);S=socket_matrix(G)@Matrix.Rotation(math.radians(roll),4,'Y')
 rig.animation_data.action=bpy.data.actions['Hero_'+n+'_v5'];cf=round(c['contact_s']*30)+1;row=[]
 for f in range(cf-3,cf+4):
  bpy.context.scene.frame_set(f);H=rig.matrix_world@rig.pose.bones['Hand.R'].matrix;Rk=H@S
  row.append(abs(Rk.to_3x3().col[2].normalized().dot(Vector((0,-1,0)))))
 print('FACE',n,' '.join('%.2f'%x for x in row),' contact=%.2f'%row[3])
