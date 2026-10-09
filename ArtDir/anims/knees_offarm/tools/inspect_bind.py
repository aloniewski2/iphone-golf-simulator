import bpy,json
from pathlib import Path
P=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
r=bpy.data.objects['Hero_01_Rig'];o=bpy.data.objects['Body_Skin']
print('BONES',[(b.name,list(b.head_local),list(b.tail_local)) for b in r.data.bones])
print('MESH',len(o.data.vertices),len(o.data.polygons),list(o.scale),list(o.location))
for side in ['L','R']:
 k=r.data.bones['LowerLeg.'+side].head_local
 vs=[v for v in o.data.vertices if abs(v.co.z-k.z)<.09 and abs(v.co.x-k.x)<.15]
 print('KNEE',side,'vertices',len(vs),'zlevels',len(set(round(v.co.z,3) for v in vs)), 'weights',[(list(v.co),[(o.vertex_groups[g.group].name,round(g.weight,3)) for g in v.groups]) for v in vs[::max(1,len(vs)//8)]])
print('MODIFIERS',[(x.name,x.type,getattr(x,'use_deform_preserve_volume',None)) for x in o.modifiers])
