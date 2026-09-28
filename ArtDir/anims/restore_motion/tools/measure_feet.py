# Per clip: lowest heel/toe (sink below court), and for runs the in-place speed of the planted foot.
import bpy,glob,json,math,os
from mathutils import Vector
R='/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/ArtDirection/Hero01/Models/RestoreMotion/'
out={}
for f in sorted(glob.glob(R+'Hero_*_v5.fbx')):
 name=os.path.basename(f)[5:-7]
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=f)
 rig=[o for o in bpy.data.objects if o.type=='ARMATURE'][0];s,e=map(int,rig.animation_data.action.frame_range)
 M=rig.matrix_world;rows=[]
 for fr in range(s,e+1):
  bpy.context.scene.frame_set(fr);pb=rig.pose.bones
  row={}
  for sd in 'LR':
   heel=M@pb['Foot.'+sd].head;toe=M@pb['Toes.'+sd].head;tip=M@pb['Toes.'+sd].tail
   row[sd]=(heel.copy(),toe.copy(),tip.copy())
  rows.append(row)
 # sole height: ankle joint rests .165 above ground, toe joint .055, toe tip ~.045 (bind)
 sink=min(min(r[sd][0].z-.165,r[sd][1].z-.055) for r in rows for sd in 'LR')
 info={'frames':len(rows),'min_sole_m':round(sink,3)}
 if 'Run' in name:
  vs=[];dirs=Vector((0,0,0))
  for a,b in zip(rows,rows[1:]):
   for sd in 'LR':
    if b[sd][1].z-.055<.03:   # planted
     d=(b[sd][1]-a[sd][1]);d.z=0;vs.append(d.length*30);dirs+=d
  vs.sort();info['planted_speed_mps']=round(vs[len(vs)//2],2) if vs else 0
  n=dirs.normalized();info['foot_slide_dir_blender']=[round(n.x,2),round(n.y,2)]
 out[name]=info;print('FEET',name,info,flush=True)
json.dump(out,open(os.path.dirname(os.path.abspath(__file__))+'/feet_report.json','w'),indent=1)
