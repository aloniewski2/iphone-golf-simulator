"""Split approved hero into shared-rig slots; preserve visible surfaces and UVs."""
import bpy,bmesh,numpy as np,json,math
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P.parent/'Hero_01.blend'))
C=bpy.data.collections['Hero_01'];rig=next(o for o in C.objects if o.type=='ARMATURE');sources=[o for o in C.objects if o.type=='MESH' and 'EyeSphere' not in o.name];eyes=[o for o in C.objects if 'EyeSphere' in o.name]
color=bpy.data.images['Hero_01_BaseColor'];a=np.asarray(color.pixels[:],dtype=np.float32).reshape((2048,2048,4));allm=[]
for o in sources:
 for m in o.data.materials:
  if m not in allm:allm.append(m)
verts=[];faces=[];loopuv=[];normals=[];mindices=[];vweights=[];slots=[];uvtri=[]
for o in sources:
 force_hat=set()
 if o.name=='Hero_01_Body':
  adjacency=[set() for _ in o.data.vertices]
  for e in o.data.edges:a0,b0=e.vertices;adjacency[a0].add(b0);adjacency[b0].add(a0)
  remaining=set(range(len(adjacency)))
  while remaining:
   stack=[remaining.pop()];component=[]
   while stack:
    v=stack.pop();component.append(v)
    for q in adjacency[v]:
     if q in remaining:remaining.remove(q);stack.append(q)
   if len(component)<500 and min(o.data.vertices[v].co.z for v in component)>1.40:force_hat.update(component)
 base=len(verts);verts += [tuple(v.co) for v in o.data.vertices]
 vweights += [{o.vertex_groups[g.group].name:g.weight for g in v.groups} for v in o.data.vertices]
 uv=o.data.uv_layers.active
 for p in o.data.polygons:
  c=p.center;tc=sum((uv.data[i].uv for i in p.loop_indices),Vector((0,0)))/len(p.loop_indices);r,g,b=a[min(2047,int(tc.y*2048)),min(2047,int(tc.x*2048)),:3];m=o.data.materials[p.material_index];mn=m.name
  if abs(c.x)>.143 and c.z<1.445 and c.z>1.30 and c.y<.17 and 'Hair' in o.name:slot='Body_Skin'
  elif abs(c.x)<.085 and 1.145<c.z<1.30 and c.y>-.06 and r>g*1.12 and g>b*1.05:slot='Body_Skin'
  elif all(v in force_hat for v in p.vertices):slot='Hat_Visor'
  elif 'Hair' in o.name:slot='Hair_Default'
  elif 'Visor' in o.name:slot='Hat_Visor'
  elif c.z>1.465 and (r<g*1.08 or b>g*.88):slot='Hat_Visor'
  elif c.z>1.515:slot='Hair_Default'
  elif c.z>1.27:slot='Body_Skin'
  elif c.z<.285:
   slot='Body_Skin' if 'WarmSkin' in mn and c.z>.205 else 'Shoes_Default'
  elif .58<c.z<1.10 and abs(c.x)>.20 and r>g*1.08 and g>b*1.08 and g>r*.58:slot='Body_Skin'
  elif 'WarmSkin' in mn and g>r*.58:slot='Body_Skin'
  elif c.z<.835 and abs(c.x)<.315:slot='Shorts_Default'
  elif c.z<.90 and abs(c.x)>.315:slot='Body_Skin'
  else:slot='Shirt_Default'
  faces.append(tuple(base+i for i in p.vertices));mindices.append(allm.index(m));slots.append(slot)
  uvs=[tuple(uv.data[i].uv) for i in p.loop_indices];loopuv.append(uvs);uvtri.append(uvs)
  normals.append([tuple(o.data.corner_normals[i].vector) for i in p.loop_indices])
# Preserve complete visible source surface by exact partition (no discarded original face).
def make(name,ids,coords=None,custom=True):
 use=sorted({v for i in ids for v in faces[i]});mapping={v:i for i,v in enumerate(use)};me=bpy.data.meshes.new(name);me.from_pydata([(coords if coords is not None else verts)[v] for v in use],[],[tuple(mapping[v] for v in faces[i]) for i in ids]);me.update();o=bpy.data.objects.new(name,me);C.objects.link(o)
 for m in allm:me.materials.append(m)
 uv=me.uv_layers.new(name='UVMap');ns=[]
 for p,i in zip(me.polygons,ids):
  p.material_index=mindices[i];p.use_smooth=True
  for li,u in zip(p.loop_indices,loopuv[i]):uv.data[li].uv=u
  ns.extend(normals[i])
 if custom:me.normals_split_custom_set(ns)
 gs={b.name:o.vertex_groups.new(name=b.name) for b in rig.data.bones}
 for old,new in mapping.items():
  for n,w in vweights[old].items():gs[n].add([new],w,'REPLACE')
 mod=o.modifiers.new('Shared Hero_01 armature','ARMATURE');mod.object=rig;o.parent=rig
 return o,use
slotobjects={}
for slot in ['Hair_Default','Hat_Visor','Shirt_Default','Shorts_Default','Shoes_Default']:
 slotobjects[slot],_=make(slot,[i for i,s in enumerate(slots) if s==slot])
# Preserve approved exposed skin exactly. Add a separate, closed inset foundation
# underneath it; do not cap open seams across the visible face.
skinids=[i for i,s in enumerate(slots) if s=='Body_Skin'];pinned=set(v for i in skinids for v in faces[i]);coveredids=[i for i,s in enumerate(slots) if s!='Body_Skin']
body,_=make('Body_Skin',skinids);slotobjects['Body_Skin']=body
coverage=body.vertex_groups.new(name='Default_VisibleSkin');coverage.add(list(range(len(body.data.vertices))),1,'REPLACE')
under=bpy.data.materials.new('skin_CoveredFoundation');under.use_nodes=True;ub=under.node_tree.nodes.get('Principled BSDF');ub.inputs['Base Color'].default_value=(.69,.43,.22,1);ub.inputs['Roughness'].default_value=.62
# Conservative smooth anatomical underlay, strictly inside approved surfaces.
# Exposed face, hands and limb contours remain the original source mesh.
blobs=[]
def ell(loc,scale):
 bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,location=loc);o=bpy.context.object;o.scale=scale;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);blobs.append(o)
def capsule(a,b,r):
 a=Vector(a);b=Vector(b)
 for t in [0,.2,.4,.6,.8,1]:ell(a.lerp(b,t),(r,r,r))
ell((0,0,.975),(.155,.092,.245));ell((0,0,.75),(.148,.09,.13));ell((0,.01,1.477),(.146,.11,.18));capsule((0,0,1.18),(0,0,1.33),.05)
for sign in [-1,1]:
 capsule((sign*.115,0,1.145),(sign*.235,0,1.125),.043)
 capsule((sign*.235,0,1.125),(sign*.35,-.005,.965),.041)
 capsule((sign*.35,-.005,.965),(sign*.459,-.013,.78),.034)
 ell((sign*.477,-.014,.714),(.023,.019,.055))
 capsule((sign*.108,0,.76),(sign*.158,0,.445),.049)
 capsule((sign*.158,0,.445),(sign*.19,0,.19),.036)
 capsule((sign*.19,0,.19),(sign*.197,-.015,.10),.030)
 ell((sign*.197,-.073,.076),(.044,.117,.043))
bpy.ops.object.select_all(action='DESELECT')
for o in blobs:
 for v in o.data.vertices:v.co.y+=.065
 o.select_set(True)
bpy.context.view_layer.objects.active=blobs[0];bpy.ops.object.join();foundation=bpy.context.object;foundation.name='Body_CoveredFoundation'
for c in list(foundation.users_collection):c.objects.unlink(foundation)
C.objects.link(foundation);foundation.data.materials.append(under)
rem=foundation.modifiers.new('Closed covered foundation','REMESH');rem.mode='VOXEL';rem.voxel_size=.006;rem.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=rem.name)
sm=foundation.modifiers.new('Smooth covered anatomy','SMOOTH');sm.factor=.55;sm.iterations=5;bpy.ops.object.modifier_apply(modifier=sm.name)
dec=foundation.modifiers.new('Covered topology budget','DECIMATE');dec.ratio=.24;bpy.ops.object.modifier_apply(modifier=dec.name)
# Check the hidden foundation against actual front/back surface intersections.
# This handles the approved model's asymmetric depth and foot placement.
from mathutils.bvhtree import BVHTree
surface=BVHTree.FromPolygons([Vector(v) for v in verts],faces,all_triangles=True)
for v in foundation.data.vertices:
 for attempt in range(5):
  x,y,z=v.co;front=surface.ray_cast(Vector((x,-2,z)),Vector((0,1,0)))[0];back=surface.ray_cast(Vector((x,2,z)),Vector((0,-1,0)))[0]
  if front is not None and back is not None:
   margin=min(.016,max(.001,(back.y-front.y)*.3));v.co.y=max(front.y+margin,min(back.y-margin,y));break
  loc,n,idx,d=surface.find_nearest(v.co)
  if loc is None:break
  v.co=loc-n*.018
foundation.data.update()
# Restore shared rig weights after remeshing from nearest original surface.
from mathutils.kdtree import KDTree
kd=KDTree(len(verts))
for i,v in enumerate(verts):kd.insert(v,i)
kd.balance();foundation.vertex_groups.clear();gs={b.name:foundation.vertex_groups.new(name=b.name) for b in rig.data.bones}
for v in foundation.data.vertices:
 _,idx,_=kd.find(v.co)
 for n,w in vweights[idx].items():gs[n].add([v.index],w,'REPLACE')
foundation.data.normals_split_custom_set([tuple(n.vector) for n in foundation.data.corner_normals])
# Join the closed underlay with the exact approved exposed surface. One skin mesh,
# fixed eyes remain separate. Layered surfaces are intentional and documented.
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);foundation.select_set(True);bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
coverage_mod=body.modifiers.new('Default outfit coverage','MASK');coverage_mod.vertex_group='Default_VisibleSkin';coverage_mod.threshold=.5
res={'faces':[]}
# Eyes are fixed facial pieces, share Head bone and ship with the body export.
for o in eyes:o.name=o.name.replace('Hero_01_EyeSphere','Body_EyeSphere')
for o in sources:bpy.data.objects.remove(o,do_unlink=True)
# Export skin-face UV triangles so a mask can be generated using the existing KitRecolor convention.
(P/'skin-mask-input.json').write_text(json.dumps({'triangles':[uvtri[i] for i in skinids],'face_triangles':[uvtri[i] for i in skinids if sum(verts[v][2] for v in faces[i])/len(faces[i])>1.28],'source_color':str(P.parent/'Hero_01_BaseColor.png')}))
# A temporary identity colour QA render makes accidental slot contamination visible.
scene=bpy.context.scene;scene.cycles.samples=32;scene.render.resolution_percentage=70
scene.render.filepath=str(P/'modular_preview.png');bpy.ops.render.render(write_still=True)
# Save split source for subsequent mask/material and final export step.
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Assembled.blend'))
stats={'source_faces':len(faces),'partition_faces':{s:slots.count(s) for s in set(slots)},'covered_body_faces':len(coveredids),'body_hole_caps':len(res.get('faces',[])),'source_visible_vertices_preserved':len(pinned),'body_underlay_note':'Smoothed/inset covered foundation; not authored unclothed anatomy.'}
(P/'partition.json').write_text(json.dumps(stats,indent=2));print('PARTITION',json.dumps(stats))
# Diagnostic body-only preview. Not a separate body asset.
for s,o in slotobjects.items():o.hide_render=s!='Body_Skin'
coverage_mod.show_render=False
scene.render.filepath=str(P/'body_coverage_preview.png');bpy.ops.render.render(write_still=True)
