import bpy,json
from pathlib import Path
R=Path(__file__).resolve().parents[4];bpy.ops.wm.open_mainfile(filepath=str(R/'ArtDir/tennis/audience/staging5/TennisPromenadeHero3.blend'))
for var in [0,1]:
 r=bpy.data.objects[f'FAN_{var}'];rig=next(o for o in r.children if o.type=='ARMATURE')
 print('RIG',var,[(b.name,list(b.head_local),list(b.tail_local)) for b in rig.data.bones])
 for ob in r.children:
  if ob.type!='MESH':continue
  mats=ob.data.color_attributes['SpectatorPalette'];colours={v.index:tuple(mats.data[p.loop_start].color) for p in ob.data.polygons for v in [ob.data.vertices[p.vertices[0]]]}
  samples=[]
  for v in ob.data.vertices:
   c=colours.get(v.index,[])
   if c and c[0]>.7 and c[1]>.7 and v.co.z<.35:
    samples.append([v.index,list(v.co),[(ob.vertex_groups[g.group].name,g.weight) for g in v.groups]])
  print('SOCK_SOURCE',ob.name,samples[::max(1,len(samples)//8)])
