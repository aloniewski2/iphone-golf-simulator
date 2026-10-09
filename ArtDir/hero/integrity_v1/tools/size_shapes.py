import bpy,math,sys,json
from pathlib import Path
from mathutils import Vector
root=Path(sys.argv[sys.argv.index('--')+1]);rig=bpy.data.objects['Hero_01_Rig']
def ss(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def displacement(p,scale):
 x,y,z=p;dx=dy=0
 # Keep head, neck, hand sockets, feet, and bone locations fixed. The default (50%) is the repaired shape.
 if .20<z<.70 and abs(x)>.02:
  side='L' if x>0 else 'R';u=rig.data.bones['UpperLeg.'+side];l=rig.data.bones['LowerLeg.'+side]
  a,b=(u.head_local,l.head_local) if z>=l.head_local.z else (l.head_local,l.tail_local)
  t=max(0,min(1,(z-a.z)/(b.z-a.z)));c=a.lerp(b,t)
  w=ss(.20,.27,z)*(1-ss(.61,.70,z));dx=(x-c.x)*scale*w;dy=(y-c.y)*scale*w
 elif .70<=z<1.20:
  w=ss(.70,.80,z)*(1-ss(1.07,1.20,z))*(1-ss(.19,.28,abs(x)))
  dx=x*scale*w;dy=(y-.055)*scale*w
 return Vector((dx,dy,0))
for name in ['Body_Skin','Shirt_Default','Shorts_Default']:
 o=bpy.data.objects[name]
 if not o.data.shape_keys:o.shape_key_add(name='Basis')
 basis=o.data.shape_keys.key_blocks[0]
 for label,scale in [('BuildSlim',-.16),('BuildBroad',.16)]:
  k=o.data.shape_keys.key_blocks.get(label) or o.shape_key_add(name=label,from_mix=False)
  for i,v in enumerate(basis.data):k.data[i].co=v.co+o.matrix_world.inverted().to_3x3()@displacement(o.matrix_world@v.co,scale)
  k.value=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'ArtDir/hero/integrity_v1/Hero_Integrity_v1.blend'))
print('SIZE_SHAPES_DONE: ±16% radial build, joints/head/hands/feet unchanged')
