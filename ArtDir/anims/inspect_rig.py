import bpy,json
from pathlib import Path
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P.parent/'hero/modular/Hero_01_Assembled.blend'))
r=next(o for o in bpy.data.objects if o.type=='ARMATURE')
print('RIG',json.dumps({b.name:{'head':list(b.head_local),'tail':list(b.tail_local)} for b in r.data.bones}))
