import bpy,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'polish_before/shoulder_fixed.blend'))
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
o=bpy.data.objects['Body_Skin'];count=0
for v in o.data.vertices:
 x,y,z=v.co
 if abs(x)<.33 or z>.805 or z<.55:continue
 amount=.14*(1-smooth(.72,.805,z))*smooth(.33,.40,abs(x))
 anchor=Vector((.458 if x>0 else -.458,.037,.795))
 v.co=anchor+(v.co-anchor)*(1-amount);count+=1
o.data.update()
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
(P/'hand-polish.json').write_text(json.dumps({'vertices_adjusted':count,'maximum_reduction':.14,'wrist_blend_z':[.72,.805],'bones_and_sockets_unchanged':True},indent=2))
