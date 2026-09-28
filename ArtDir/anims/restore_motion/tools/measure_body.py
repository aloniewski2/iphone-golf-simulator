import bpy,json,math
from mathutils import Vector
from pathlib import Path
P=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_FingerRig.blend'));rig=bpy.data.objects['Hero_01_Rig']
armnames=('Shoulder','UpperArm','LowerArm','Hand','Index','Middle','Ring','Pinky','Thumb','Head','Neck')
slices={}
arm={'UpperArm':[],'LowerArm':[],'Hand':[]}
B=rig.data.bones
for oname in ['Body_Skin','Shirt_Default','Shorts_Default']:
 o=bpy.data.objects[oname];gi={g.index:g.name for g in o.vertex_groups}
 for v in o.data.vertices:
  ws={gi[g.group]:g.weight for g in v.groups if g.group in gi}
  armw=sum(w for n,w in ws.items() if n.startswith(armnames))
  co=o.matrix_world@v.co
  if armw<.2 and .70<co.z<1.24:
   k=round((co.z-.70)/.04);slices.setdefault(k,[]).append((co.x,co.y))
  for part in arm:
   if ws.get(part+'.R',0)>.5:
    b=B[part+'.R'];a=b.head_local;t=b.tail_local;d=t-a;u=max(0,min(1,(co-a).dot(d)/d.length_squared));arm[part].append((co-(a+d*u)).length)
out={'slices':[]}
for k in sorted(slices):
 xs=[abs(p[0]) for p in slices[k]];ys=[p[1] for p in slices[k]]
 xs.sort();ys.sort()
 out['slices'].append({'z':.70+k*.04,'a':xs[int(len(xs)*.98)-1],'ymin':ys[int(len(ys)*.02)],'ymax':ys[int(len(ys)*.98)-1]})
for part,v in arm.items():v.sort();out[part]=v[int(len(v)*.8)] if v else 0
(P/'tools/body_volume.json').write_text(json.dumps(out,indent=1));print(json.dumps(out))
