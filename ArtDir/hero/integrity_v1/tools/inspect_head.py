import bpy,json
from collections import defaultdict
for name in ['Body_Skin','Body_EyeSphere_L','Body_EyeSphere_R','Hair_Default','Hair_Default_Free','Hair_Ponytail']:
 o=bpy.data.objects.get(name)
 if not o: continue
 sums=defaultdict(float); count=0
 for v in o.data.vertices:
  if name=='Body_Skin' and (o.matrix_world@v.co).z<1.32:continue
  count+=1
  for g in v.groups:sums[o.vertex_groups[g.group].name]+=g.weight
 print('HEAD_WEIGHT',name,'count',count,'world',list(o.matrix_world.translation),'rotation',list(o.rotation_euler),'scale',list(o.scale),'weights',dict(sorted(sums.items(),key=lambda x:-x[1])[:12]))
 print('HEAD_MODIFIERS',name,[(m.name,m.type,m.show_viewport,m.show_render) for m in o.modifiers])
rig=bpy.data.objects['Hero_01_Rig']
for name in ['Chest','Neck','Head']:
 b=rig.data.bones[name];print('BONE',name,list(b.head_local),list(b.tail_local),b.parent.name if b.parent else '')
