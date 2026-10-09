import bpy,math,json,numpy as np
from mathutils import Vector,Matrix
from pathlib import Path
P=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(P.parent/'blockout/Hero_01_tripo.glb'))
o=next(o for o in bpy.context.scene.objects if o.type=='MESH');bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
vs=[v.co for v in o.data.vertices];lo=Vector(tuple(min(v[i] for v in vs) for i in range(3)));hi=Vector(tuple(max(v[i] for v in vs) for i in range(3)));center=Vector(((hi.x+lo.x)/2,(hi.y+lo.y)/2,lo.z));R=Matrix.Rotation(-math.pi/2,4,'Z')
for v in o.data.vertices:v.co=R@((v.co-center)*(1.7/(hi.z-lo.z)))
o.data.update();bpy.context.view_layer.update()
im=next(im for im in bpy.data.images if im.name.startswith('Color'));a=np.array(im.pixels[:]).reshape((im.size[1],im.size[0],4))
uv=o.data.uv_layers.active
samples=[]
for poly in o.data.polygons:
 c=poly.center
 if c.z>1.32 and c.z<1.51 and c.y<-.1 and abs(c.x)<.17:
  u=sum((uv.data[i].uv for i in poly.loop_indices),Vector((0,0)))/len(poly.loop_indices);col=a[min(im.size[1]-1,int(u.y*im.size[1])),min(im.size[0]-1,int(u.x*im.size[0])),:3]
  if max(col)<.25:samples.append([*c,*col])
print('DARK_EYE_BOUNDS')
for sign in [-1,1]:
 s=np.array([x for x in samples if x[0]*sign>0]);print(sign,'min',s[:,:3].min(0),'max',s[:,:3].max(0),'mean',s[:,:3].mean(0))
for x,z in [(.08,1.4),(.09,1.4),(.09,1.39),(.085,1.42),(.09,1.43),(.09,1.41)]:
 hit,loc,norm,idx=o.ray_cast(Vector((x,-1,z)),Vector((0,1,0)));print('RAY',x,z,hit,list(loc),list(norm))
