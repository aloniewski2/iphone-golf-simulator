import bpy,json,collections
from pathlib import Path
R=Path(__file__).resolve().parents[4]

def signature(path):
 bpy.ops.wm.open_mainfile(filepath=str(path));out={}
 for ob in bpy.data.objects:
  if ob.type!='MESH' or not ob.name.startswith(('VISITOR_MESH','BODY','HEAD','ARM_L','ARM_R')):continue
  root=ob
  while root.parent:root=root.parent
  if not root.name.startswith('FAN_'):continue
  fan=int(root.name[4:]);far='_FAR' in ob.name
  if far and fan%2==0:continue
  palette=ob.data.color_attributes.active_color;vcol={}
  if palette:
   for loop in ob.data.loops:vcol.setdefault(loop.vertex_index,tuple(round(v,6) for v in palette.data[loop.index].color))
  records=[]
  for v in ob.data.vertices:
   weights=tuple(sorted((ob.vertex_groups[g.group].name,round(g.weight,6)) for g in v.groups))
   records.append((tuple(round(c,6) for c in v.co),vcol.get(v.index),weights))
  out[(fan,ob.name)]=collections.Counter(records)
 return out
report=[]
for asset in ['TennisPromenadeHero3','TennisSeatedHero3']:
 a=signature(R/'ArtDir/tennis/audience/inputs'/('Accepted_'+asset.replace('Hero3','Hero5')+'.blend'));b=signature(R/'ArtDir/tennis/audience/staging6'/(asset+'.blend'))
 keys=set(a)|set(b);diff=[str(k) for k in keys if a.get(k)!=b.get(k)]
 report.append({'asset':asset,'scope':'All near geometry and all female far geometry: six-decimal position, palette, normalized bone-weight multiset. Male far alone is the authored shorts correction.','checked_meshes':len(keys),'changed_meshes':diff,'status':'PASS' if not diff else 'FAIL'})
(R/'proof/full-visual-overhaul/tennis/audience6/source5-unchanged-audit.json').write_text(json.dumps(report,indent=2)+'\n');print('UNCHANGED_SOURCE_AUDIT',json.dumps(report))
