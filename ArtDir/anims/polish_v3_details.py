import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
from mathutils.geometry import barycentric_transform
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'polish_v3_before/limbs_fixed.blend'))
r=bpy.data.objects['Hero_01_Rig'];r.animation_data.action=None
for b in r.pose.bones:b.matrix_basis.identity()
def sm(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
# Sharpen existing clump ridges and finger grooves, without adding/swapping a body.
for name in ['Hair_Default','Body_Skin']:
 o=bpy.data.objects[name];bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();updates={}
 for v in bm.verts:
  x,y,z=v.co
  strength=.7*sm(1.50,1.56,z) if name=='Hair_Default' else .5*sm(.37,.42,abs(x))*(1-sm(.715,.75,z))*sm(.61,.64,z)
  if strength<=0 or v.is_boundary:continue
  avg=sum((e.other_vert(v).co for e in v.link_edges),Vector())/len(v.link_edges);delta=(v.co-avg)*strength
  limit=.002 if name=='Hair_Default' else .001
  if delta.length>limit:delta.normalize();delta*=limit
  updates[v.index]=v.co+delta
 for i,co in updates.items():bm.verts[i].co=co
 bm.to_mesh(o.data);bm.free();o.data.update()
shoe=bpy.data.objects['Shoes_Default'];me=shoe.data
# Erase fragmented source orange by assigning a clean shoe-white material to that region.
white=bpy.data.materials.new('Hero_01_ShoeCleanWhite');white.use_nodes=True;bs=white.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.83,.82,.79,1);bs.inputs['Roughness'].default_value=.75
me.materials.append(white);whiteidx=len(me.materials)-1
im=bpy.data.images['Hero_01_BaseColor'];px=list(im.pixels);W,H=im.size;uv=me.uv_layers.active.data;orangefaces=set()
for f in me.polygons:
 hit=False
 for loop in f.loop_indices:
  u,v=uv[loop].uv;ix=min(W-1,max(0,int(u*W)));iy=min(H-1,max(0,int(v*H)));rr,g,b=px[4*(iy*W+ix):4*(iy*W+ix)+3]
  hit|=rr>.3 and rr>g*1.55 and g>b*1.4 and b<.32
 if hit or 'ShoeOrange' in me.materials[f.material_index].name:orangefaces.add(f.index)
# One adjacency ring catches texture bleed along patch boundaries.
verts={i for fi in orangefaces for i in me.polygons[fi].vertices}
for f in me.polygons:
 if any(i in verts for i in f.vertices):f.material_index=whiteidx
# Slightly slimmer shoe shell, fading out before the sock; shared ankle stays unchanged.
for v in me.vertices:
 x,y,z=v.co;t=.06*(1-sm(.13,.215,z));cx=.195 if x>0 else -.195
 v.co.x=cx+(x-cx)*(1-t);v.co.y=.055+(y-.055)*(1-t)
me.update()
# A single fitted orange stripe on each outer shoe side. Existing shoe slot and skeleton.
orange=next(m for m in bpy.data.materials if m.name=='Hero_01_ShoeOrange')
coords=[v.co.copy() for v in me.vertices];tree=BVHTree.FromPolygons(coords,[list(f.vertices) for f in me.polygons]);kd=KDTree(len(coords))
for i,co in enumerate(coords):kd.insert(co,i)
kd.balance();patchverts=[];faces=[];weights=[]
for side in [-1,1]:
 for outward in [-1,1]:
  base=len(patchverts)
  for row in range(3):
   for col in range(11):
    u=col/10;v=row/2;y=-.11+.075*u;z=.095+.035*u+.015*v
    start=side*(.7 if outward==1 else .05)
    hit,normal,idx,d=tree.ray_cast(Vector((start,y,z)),Vector((-side*outward,0,0)))
    if hit is None:raise RuntimeError('Shoe trim ray missed')
    patchverts.append(hit+Vector((side*outward*.005,0,0)))
    tri=me.polygons[idx].vertices;aa,bb,cc=[coords[i] for i in tri]
    bary=barycentric_transform(hit,aa,bb,cc,Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
    w={}
    for vi,factor in zip(tri,bary):
     for g in me.vertices[vi].groups:
      n=shoe.vertex_groups[g.group].name;w[n]=w.get(n,0)+max(0,factor)*g.weight
    total=sum(w.values());weights.append({n:a/total for n,a in w.items() if a>0})
  for row in range(2):
   for col in range(10):
    a=base+row*11+col
    for tri in [(a,a+1,a+12),(a,a+12,a+11)]:
     aa,bb,cc=[patchverts[i] for i in tri]
     faces.append(tuple(reversed(tri)) if (bb-aa).cross(cc-aa).x*side*outward<0 else tri)
mesh=bpy.data.meshes.new('ShoeOrangeTrim');mesh.from_pydata(patchverts,[],faces);mesh.materials.append(orange);obj=bpy.data.objects.new('ShoeOrangeTrim',mesh);bpy.context.collection.objects.link(obj)
for i,w in enumerate(weights):
 for n,a in w.items():
  g=obj.vertex_groups.get(n) or obj.vertex_groups.new(name=n);g.add([i],a,'REPLACE')
for f in mesh.polygons:f.use_smooth=True
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);shoe.select_set(True);bpy.context.view_layer.objects.active=shoe;bpy.ops.object.join()
r.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
(P/'v3-detail-report.json').write_text(json.dumps({'old_orange_faces':len(orangefaces),'shoe_scale_reduction':.06,'new_trim_triangles':len(faces),'hair_ridge_max_displacement_m':.002,'finger_detail_max_displacement_m':.001},indent=2))
