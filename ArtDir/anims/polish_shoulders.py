"""Localized shoulder weights; no skeleton, topology, or wardrobe changes."""
import bpy,json
from pathlib import Path
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'polish_before/Hero_01_Mixamo_QA.blend'))
r=bpy.data.objects['Hero_01_Rig'];bn={b.name for b in r.data.bones}
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
changed={}
for o in bpy.data.collections['Hero_01'].objects:
 if o.type!='MESH' or o.name not in ['Shirt_Default','Body_Skin']:continue
 count=0
 for v in o.data.vertices:
  x,y,z=v.co;ax=abs(x);side='L' if x>0 else 'R'
  if not (.975<z<1.245 and ax>.115):continue
  old={o.vertex_groups[g.group].name:g.weight for g in v.groups if o.vertex_groups[g.group].name in bn}
  # Lower inner sleeve follows the humerus, while the cap follows the clavicle.
  lateral=smooth(.135,.275,ax+.42*(1.17-z))
  clav=.4*lateral*(1-smooth(.23,.32,ax))*smooth(1.05,1.19,z)
  w={'Chest':1-lateral,'Shoulder.'+side:clav,'UpperArm.'+side:lateral-clav}
  blend=smooth(.115,.15,ax)*smooth(.975,1.025,z)*(1-smooth(1.215,1.245,z))
  if o.name=='Shirt_Default':blend=smooth(.115,.15,ax)*smooth(.975,1.025,z)
  out={k:old.get(k,0)*(1-blend)+w.get(k,0)*blend for k in old.keys()|w.keys()}
  out=dict(sorted(out.items(),key=lambda kv:-kv[1])[:4]);total=sum(out.values())
  for g in o.vertex_groups:
   if g.name in bn:g.remove([v.index])
  for n,a in out.items():
   if a>0:o.vertex_groups[n].add([v.index],a/total,'REPLACE')
  count+=1
 changed[o.name]=count
# Slot seams must receive exactly the same weights.
from collections import defaultdict
shared=defaultdict(list)
for o in bpy.data.collections['Hero_01'].objects:
 if o.type=='MESH':
  for v in o.data.vertices:shared[tuple(round(c,5) for c in v.co)].append((o,v))
for entries in shared.values():
 if len({o.name for o,v in entries})<2:continue
 source=next(((o,v) for o,v in entries if o.name=='Shirt_Default'),entries[0]);o,v=source
 w={o.vertex_groups[g.group].name:g.weight for g in v.groups if o.vertex_groups[g.group].name in bn}
 for ob,ve in entries:
  for g in ob.vertex_groups:
   if g.name in bn:g.remove([ve.index])
  for n,a in w.items():ob.vertex_groups[n].add([ve.index],a,'REPLACE')
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
(P/'shoulder-polish.json').write_text(json.dumps(changed,indent=2))
