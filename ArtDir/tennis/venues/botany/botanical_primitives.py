"""Build an editable, shared opaque botanical kit. Existing terrain files are never loaded or edited."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
rng=random.Random(20261007)
def srgb(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
def material(name,rgb,rough=.75):
 m=bpy.data.materials.new(name);m.diffuse_color=tuple(srgb(c/255) for c in rgb)+(1,);m.use_nodes=True
 b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Base Color'].default_value=m.diffuse_color;b.inputs['Roughness'].default_value=rough
 return m
mats=[material('RESORT_BARK',(131,94,58)),material('RESORT_LEAF_DARK',(35,99,47)),material('RESORT_LEAF_MID',(72,135,51)),material('RESORT_LEAF_LIGHT',(132,172,63)),material('RESORT_CORAL',(243,119,91)),material('RESORT_GOLD',(248,196,77)),material('RESORT_STONE',(146,145,125))]
verts=[];faces=[];slots=[];weights=[]
def reset():verts.clear();faces.clear();slots.clear();weights.clear()
def vertex(p,weight=0):verts.append(tuple(p));weights.append(weight);return len(verts)-1
def face(ids,slot):faces.append(tuple(ids));slots.append(slot)
def tube(a,b,r1,r2,slot=0,sides=8):
 a=Vector(a);b=Vector(b);d=(b-a).normalized();u=d.cross(Vector((1,0,0)))
 if u.length<.01:u=d.cross(Vector((0,1,0)))
 u.normalize();v=d.cross(u);start=len(verts)
 for c,r in [(a,r1),(b,r2)]:
  for j in range(sides):vertex(c+(u*math.cos(j*math.tau/sides)+v*math.sin(j*math.tau/sides))*r)
 for j in range(sides):face((start+j,start+(j+1)%sides,start+sides+(j+1)%sides,start+sides+j),slot)
 face(tuple(start+j for j in reversed(range(sides))),slot);face(tuple(start+sides+j for j in range(sides)),slot)
def leaf(root,angle,length,width,arch,slot,sections=5,phase=0):
 root=Vector(root);d=Vector((math.cos(angle),math.sin(angle),0));side=Vector((-d.y,d.x,0));start=len(verts)
 for j in range(sections+1):
  t=j/sections;c=root+d*(length*t)+Vector((0,0,arch*math.sin(t*math.pi*.8)-length*.12*t*t));w=width*math.sin(t*math.pi)**.8
  vertex(c-side*w, t*.35);vertex(c+Vector((0,0,w*.20)),t*.35);vertex(c+side*w,t*.35)
 for j in range(sections):
  q=start+j*3;face((q,q+3,q+4,q+1),slot);face((q+1,q+4,q+5,q+2),slot)
def bulb(center,scale,slot,rings=4,sides=7):
 c=Vector(center);start=len(verts)
 for k in range(rings+1):
  phi=k*math.pi/rings
  for j in range(sides):
   a=j*math.tau/sides;vertex(c+Vector((math.sin(phi)*math.cos(a)*scale[0],math.sin(phi)*math.sin(a)*scale[1],math.cos(phi)*scale[2])),.12)
 for k in range(rings):
  for j in range(sides):face((start+k*sides+j,start+k*sides+(j+1)%sides,start+(k+1)*sides+(j+1)%sides,start+(k+1)*sides+j),slot)
def mesh(name):
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
 for m in mats:me.materials.append(m)
 for p,slot in zip(me.polygons,slots):p.material_index=slot;p.use_smooth=True
 col=me.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
 for c,w in zip(col.data,weights):c.color=(w,.28,0,1)
 ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);return ob
