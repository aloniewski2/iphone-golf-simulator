"""Construct real skin under the wardrobe; preserve the locked head, hands and knee loops.

Only newly authored torso/arm/pelvis surfaces are unioned. Face, hands and knee
topology are retained and stitched to that surface. The source workset is frozen
so every revision starts from the same geometry, rather than accumulating edits.
"""
import bpy,bmesh,math,json,shutil,hashlib,struct
from pathlib import Path
from mathutils import Vector,Matrix

R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';C=O/'checkpoints'
C.mkdir(exist_ok=True);checkpoint=C/'pre-clean-foundation.blend'
if not checkpoint.exists():shutil.copy2(O/'Hero_V6_Polish.blend',checkpoint)
bpy.ops.wm.open_mainfile(filepath=str(checkpoint));rig=bpy.data.objects['Hero_01_Rig']
for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
source=bpy.data.objects['Body_Skin'];report={'method':'new lofted skin, unioned trunk only, retained head/hands/knee loops'}
def face_coordinates(o):return sorted(tuple(v.co) for v in o.data.vertices if v.co.z>1.30)
face_before=face_coordinates(source)
def ss(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def active(o):
 bpy.ops.object.select_all(action='DESELECT');o.hide_set(False);o.select_set(True);bpy.context.view_layer.objects.active=o
def material(n,c,rough=.68):
 m=bpy.data.materials.get(n) or bpy.data.materials.new(n);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Roughness'].default_value=rough
 return m
skin=material('Hero_V6_FoundationSkin',(.94,.71,.54));white=material('Hero_V6_PoloWhite',(.95,.95,.93))
navy=material('Hero_V6_PoloNavy',(.012,.024,.069));orange=material('Hero_V6_PoloOrange',(.97,.145,.047))
def components(bm):
 unseen=set(bm.verts);out=[]
 while unseen:
  v=unseen.pop();part={v};stack=[v]
  while stack:
   v=stack.pop()
   for e in v.link_edges:
    w=e.other_vert(v)
    if w in unseen:unseen.remove(w);part.add(w);stack.append(w)
  out.append(part)
 return out
def retain(o,select):
 bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();ps=components(bm);keep=select(ps)
 bmesh.ops.delete(bm,geom=[v for v in bm.verts if v not in keep],context='VERTS');bm.to_mesh(o.data);bm.free()
def duplicate(o,n):
 c=o.copy();c.data=o.data.copy();bpy.context.collection.objects.link(c);c.name=n
 for m in list(c.modifiers):
  if m.type=='MASK':c.modifiers.remove(m)
 return c
def cut(o,point,normal,keep_positive,select=None):
 bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table()
 vertices=[v for v in bm.verts if select is None or select(v.co)]
 geom=set(vertices)|{e for v in vertices for e in v.link_edges}|{f for v in vertices for f in v.link_faces}
 bmesh.ops.bisect_plane(bm,geom=list(geom),dist=.000002,plane_co=point,plane_no=normal,clear_inner=keep_positive,clear_outer=not keep_positive)
 bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(o.data);bm.free()
def loops(bm):
 edges=[e for e in bm.edges if e.is_boundary];adj={}
 for e in edges:
  for v in e.verts:adj.setdefault(v,[]).append(e.other_vert(v))
 # Branched source borders are defects. Never silently make a fan over them.
 assert all(len(q)==2 for q in adj.values()),'Branched open boundary in retained skin'
 left=set(adj);out=[]
 while left:
  start=left.pop();q=[start];prev=None;cur=start
  while True:
   nxt=next(x for x in adj[cur] if x!=prev)
   if nxt==start:break
   assert nxt in left,'Boundary traversal repeated a vertex';left.remove(nxt);q.append(nxt);prev,cur=cur,nxt
  out.append(q)
 return out
def cap_extra(o,point,normal):
 bm=bmesh.new();bm.from_mesh(o.data)
 seen=set();duplicate_faces=[]
 for f in bm.faces:
  signature=frozenset(f.verts)
  if signature in seen:duplicate_faces.append(f)
  seen.add(signature)
 if duplicate_faces:bmesh.ops.delete(bm,geom=duplicate_faces,context='FACES_ONLY')
 removed=[]
 while any(len(e.link_faces)>2 for e in bm.edges):
  edge=next(e for e in bm.edges if len(e.link_faces)>2);f=min(edge.link_faces,key=lambda f:f.calc_area());removed.append(f.calc_area())
  bmesh.ops.delete(bm,geom=[f],context='FACES_ONLY')
 bmesh.ops.delete(bm,geom=[e for e in bm.edges if not e.link_faces],context='EDGES')
 report[o.name+'_overlapping_triangles_removed']=removed
 qs=loops(bm);oncut=[q for q in qs if max(abs((v.co-point).dot(normal)) for v in q)<.0001]
 assert oncut,'Missing stitch ring';main=max(oncut,key=len);faces=[]
 for q in qs:
  if q is main:continue
  # Retained tiny holes are closed with existing boundary vertices; no hand/face points move.
  f=bm.faces.new(tuple(q));f.smooth=True;f.material_index=q[0].link_faces[0].material_index;faces.append(f)
 if faces:bmesh.ops.recalc_face_normals(bm,faces=faces)
 count=len(main);bm.to_mesh(o.data);bm.free();return count

# Preserve the actual outer skin hands, rather than the old clothing-derived foundation.
head=duplicate(source,'Body_RetainedHead')
retain(head,lambda ps:set.union(*(p for p in ps if max(v.co.z for v in p)>1.30 and min(v.co.z for v in p)>1.10)))
report['removed_old_foundation_vertices_above_1_30']=len(face_before)-len(face_coordinates(head));face_before=face_coordinates(head)
cut(head,Vector((0,0,1.245)),Vector((0,0,1)),True)
report['head_stitch_vertices']=cap_extra(head,Vector((0,0,1.245)),Vector((0,0,1)))
hands=[];wrist_planes={}
for side,sign in [('L',1),('R',-1)]:
 hand=duplicate(source,'Body_RetainedHand_'+side)
 retain(hand,lambda ps:max((p for p in ps if min(v.co.z for v in p)<.70 and max(v.co.z for v in p)<1.10 and min(sign*v.co.x for v in p)>.20),key=len))
 b=rig.data.bones['LowerArm.'+side];u=(b.tail_local-b.head_local).normalized();p=b.tail_local-u*.022
 cut(hand,p,u,True);report['hand_'+side+'_stitch_vertices']=cap_extra(hand,p,u);hands.append(hand);wrist_planes[side]=(p,u)

def loft(vs,fs,centres,radii,N=32):
 """Capped radial loft. Axial rings become the clean shoulder/arm cross-sections."""
 base=len(vs);centres=[Vector(c) for c in centres]
 for j,c in enumerate(centres):
  axis=(centres[min(j+1,len(centres)-1)]-centres[max(j-1,0)]).normalized()
  ref=Vector((0,1,0)) if abs(axis.z)<.98 else Vector((1,0,0));u=(ref-axis*ref.dot(axis)).normalized();v=axis.cross(u).normalized()
  for i in range(N):
   t=math.tau*i/N;vs.append(tuple(c+u*radii[j][0]*math.cos(t)+v*radii[j][1]*math.sin(t)))
 for j in range(len(centres)-1):
  for i in range(N):a=base+j*N+i;b=base+j*N+(i+1)%N;fs.append((a,b,b+N,a+N))
 fs.append(tuple(base+i for i in reversed(range(N))));fs.append(tuple(base+(len(centres)-1)*N+i for i in range(N)))
def build_union(name,cloth=False):
 vs=[];fs=[];extra=.016 if cloth else 0
 # Torso reaches a real pelvis. Its lower edge stays above the crotch opening.
 zs=[.715,.74,.79,.85,.91,.99,1.07,1.13,1.17,1.195,1.22,1.25]
 widths=[.145,.175,.186,.171,.172,.179,.184,.185,.174,.119,.071,.070]
 depths=[.09,.114,.121,.105,.105,.110,.114,.110,.095,.079,.064,.063]
 # For a Z-axis loft the first radial axis is X, second Y.
 loft(vs,fs,[(0,.055,z) for z in zs],[(x+extra,y+extra) for x,y in zip(widths,depths)],48)
 for side,sign in [('L',1),('R',-1)]:
  a=rig.data.bones['UpperArm.'+side];b=rig.data.bones['LowerArm.'+side]
  cs=[a.head_local.lerp(a.tail_local,t) for t in [0,.14,.30,.50,.72,.88,1]]
  rs=[.074,.076,.075,.072,.067,.063,.062]
  if cloth:
   cs=[a.head_local.lerp(a.tail_local,t) for t in [0,.15,.32,.48,.64,.72]];rs=[.081,.083,.082,.081,.079,.078]
  else:
   cs += [b.head_local.lerp(b.tail_local,t) for t in [.16,.36,.56,.75,.91,1.08]];rs += [.067,.070,.067,.059,.051,.047]
  loft(vs,fs,cs,[(r+(extra*.30 if cloth else 0),r+(extra*.30 if cloth else 0)) for r in rs],32)
  if not cloth:
   leg=rig.data.bones['UpperLeg.'+side];lc=[]
   for z in [.60,.64,.68,.72,.765,.80]:
    t=(z-leg.head_local.z)/(leg.tail_local.z-leg.head_local.z);lc.append(leg.head_local.lerp(leg.tail_local,t))
   loft(vs,fs,lc,[(.112,.096)]*6,40)
 me=bpy.data.meshes.new(name+'_Lofts');me.from_pydata(vs,[],fs);me.update();ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);active(ob)
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
 rem=ob.modifiers.new('Union newly authored surfaces only','REMESH');rem.mode='VOXEL';rem.voxel_size=.012;rem.use_smooth_shade=True
 bpy.ops.object.modifier_apply(modifier=rem.name);sm=ob.modifiers.new('Soft skin surface','SMOOTH');sm.factor=.55;sm.iterations=4;bpy.ops.object.modifier_apply(modifier=sm.name)
 dec=ob.modifiers.new('Mobile foundation','DECIMATE');dec.ratio=.20;bpy.ops.object.modifier_apply(modifier=dec.name)
 return ob
upper=build_union('Body_NewFoundation')
cut(upper,Vector((0,0,1.235)),Vector((0,0,1)),False)
cut(upper,Vector((0,0,.695)),Vector((0,0,1)),True)
for side,sign in [('L',1),('R',-1)]:
 p,u=wrist_planes[side];cut(upper,p-u*.016,u,False,lambda co:sign*co.x>.30 and co.z<1.05)

def weights(p,cloth=False):
 x,y,z=p;side='L' if x>=0 else 'R';sign=1 if x>=0 else -1
 h=ss(.81,.94,z);c=ss(1.005,1.12,z);w={'Hips':1-h,'Spine':h*(1-c),'Chest':h*c}
 if z>.18 and z<.80 and abs(x)>.023:
  leg=1-ss(.70,.80,z);w={'Hips':1-leg,'UpperLeg.'+side:leg}
 elif z>1.185 and abs(x)<.115 and not cloth:
  n=ss(1.18,1.22,z);hd=ss(1.245,1.30,z);w={'Chest':1-n,'Neck':n*(1-hd),'Head':n*hd}
 if z>.78 and abs(x)>.15:
  a=rig.data.bones['UpperArm.'+side];u=(a.tail_local-a.head_local).normalized();s=(p-a.head_local).dot(u);lower=ss(a.length-.035,a.length+.055,s)
  arm=ss(.155,.245,abs(x));clav=.24*(1-ss(.005,.085,s));arms={'Shoulder.'+side:clav,'UpperArm.'+side:(1-clav)*(1-lower),'LowerArm.'+side:(1-clav)*lower}
  w={n:v*(1-arm) for n,v in w.items()};w.update({n:v*arm for n,v in arms.items()})
 return {n:v for n,v in w.items() if v>1e-6}
def rig_mesh(ob,cloth=False):
 ob.parent=rig
 for v in ob.data.vertices:
  for n,w in weights(v.co,cloth).items():(ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n)).add([v.index],w,'REPLACE')
 arm=ob.modifiers.new('Shared original Humanoid','ARMATURE');arm.object=rig
 for p in ob.data.polygons:p.use_smooth=True
 active(ob);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.018);bpy.ops.object.mode_set(mode='OBJECT')
upper.data.materials.append(skin);rig_mesh(upper)

# Keep authored knee loops and their volume-preserving morphs. Add real bare feet.
legs=bpy.data.objects['Body_Legs_Full'];legs.hide_render=False;legs.hide_set(False);legs.name='Body_RetainedLegs'
for m in list(legs.modifiers):
 if m.type=='MASK':legs.modifiers.remove(m)
bm=bmesh.new();bm.from_mesh(legs.data);dl=bm.verts.layers.deform.active;shape_layers=list(bm.verts.layers.shape.values());newfaces=[]
for side,sign in [('L',1),('R',-1)]:
 # Delete only the flat end caps, keeping all original radial deformation rings.
 caps=[f for f in bm.faces if len(f.verts)>8 and sign*sum(v.co.x for v in f.verts)>0]
 bmesh.ops.delete(bm,geom=caps,context='FACES_ONLY')
 ring=sorted([v for v in bm.verts if sign*v.co.x>0 and abs(v.co.z-.12)<.00001],key=lambda v:math.atan2(v.co.y-.055,v.co.x-sign*.187))
 assert len(ring)==40
 prev=ring
 for z,cx,cy,rx,ry in [(.100,.200,.035,.054,.070),(.080,.216,.000,.068,.099),(.055,.226,-.017,.072,.117),(.036,.225,-.017,.070,.114)]:
  current=[]
  for i in range(40):
   t=math.tau*i/40;p=Vector((sign*cx+rx*math.cos(t),cy+ry*math.sin(t),z));v=bm.verts.new(p)
   for layer in shape_layers:v[layer]=p
   f=ss(.10,.18,z);v[dl][legs.vertex_groups['Foot.'+side].index if legs.vertex_groups.get('Foot.'+side) else legs.vertex_groups.new(name='Foot.'+side).index]=1-f
   v[dl][legs.vertex_groups['LowerLeg.'+side].index]=f;current.append(v)
  for i in range(40):newfaces.append(bm.faces.new((prev[(i+1)%40],prev[i],current[i],current[(i+1)%40])))
  prev=current
 newfaces.append(bm.faces.new(tuple(reversed(prev))))
for f in newfaces:f.smooth=True
bmesh.ops.recalc_face_normals(bm,faces=newfaces);bm.to_mesh(legs.data);bm.free()

# Remove the failed source foundation and its now-redundant masked lower-body copy.
bpy.data.objects.remove(source,do_unlink=True);bpy.data.objects.remove(bpy.data.objects['Body_Legs'],do_unlink=True)
active(head)
for ob in [head,*hands,upper,legs]:ob.hide_set(False);ob.select_set(True)
bpy.ops.object.join();body=head;body.name='Body_Skin'
for m in list(body.modifiers):
 if m.type=='MASK':body.modifiers.remove(m)

def bridge_skin(ob):
 bm=bmesh.new();bm.from_mesh(ob.data);dl=bm.verts.layers.deform.active;uv=bm.loops.layers.uv.active;shapes=list(bm.verts.layers.shape.values());qs=loops(bm)
 print('STITCH_RING_DIAGNOSTIC',[(len(q),list(sum((v.co for v in q),Vector())/len(q)),[[min(v.co[i] for v in q),max(v.co[i] for v in q)] for i in range(3)]) for q in qs])
 assert len(qs)==10,'Expected head/trunk, two wrist, two pelvis/leg stitch pairs, got '+str(len(qs))
 unused=list(qs);pairs=[]
 def centre(q):return sum((v.co for v in q),Vector())/len(q)
 # Pair nearby boundaries; every pair is geometrically distinct from the other seams.
 while unused:
  a=unused.pop(0);ca=centre(a);b=min(unused,key=lambda q:(centre(q)-ca).length);unused.remove(b);pairs.append((a,b))
 faces=[];seams=[]
 for a,b in pairs:
  ca,cb=centre(a),centre(b);axis=(cb-ca).normalized();ref=Vector((0,1,0)) if abs(axis.z)<.9 else Vector((1,0,0));u=(ref-axis*ref.dot(axis)).normalized();v=axis.cross(u).normalized()
  angle=lambda p,c:math.atan2((p-c).dot(v),(p-c).dot(u))
  def ordered(q,c):
   area=sum((q[k].co-c).cross(q[(k+1)%len(q)].co-c).dot(axis) for k in range(len(q)))
   if area<0:q=list(reversed(q))
   start=min(range(len(q)),key=lambda k:angle(q[k].co,c));return q[start:]+q[:start]
  a=ordered(a,ca);b=ordered(b,cb);i=j=0;patch=[]
  while i<len(a) or j<len(b):
   ni=(i+1)/len(a) if i<len(a) else 2;nj=(j+1)/len(b) if j<len(b) else 2
   if abs(ni-nj)<1e-7:verts=(a[i%len(a)],a[(i+1)%len(a)],b[(j+1)%len(b)],b[j%len(b)]);i+=1;j+=1
   elif ni<nj:verts=(a[i%len(a)],a[(i+1)%len(a)],b[j%len(b)]);i+=1
   else:verts=(a[i%len(a)],b[(j+1)%len(b)],b[j%len(b)]);j+=1
   f=bm.faces.new(verts);f.smooth=True;f.material_index=next(k for k,m in enumerate(ob.data.materials) if m.name=='Hero_V6_FoundationSkin');patch.append(f)
  bmesh.ops.recalc_face_normals(bm,faces=patch);faces.extend(patch);seams.append({'a':list(ca),'b':list(cb),'ring_counts':[len(a),len(b)]})
 assert sum(e.is_boundary for e in bm.edges)==0,'Unclosed skin seam after stitching'
 assert all(len(e.link_faces)==2 for e in bm.edges),'Nonmanifold edge in complete skin'
 ps=components(bm);main=max(ps,key=len)
 # Recalculate only now: the complete skin is closed, unlike the earlier open foundation.
 bmesh.ops.recalc_face_normals(bm,faces=list({f for v in main for f in v.link_faces}))
 assert min(v.co.z for v in main)<.04 and max(v.co.z for v in main)>1.60,'Skin body is not continuous head to feet'
 report['skin_components']=[len(p) for p in ps];report['stitches']=seams;report['skin_boundary_edges']=0
 bm.to_mesh(ob.data);bm.free()
bridge_skin(body)

# Size affects body and clothes radially, preserving joint positions, face and hands.
def build_shapes(ob):
 if not ob.data.shape_keys:ob.shape_key_add(name='Basis')
 basis=ob.data.shape_keys.key_blocks[0]
 for name,amt in [('BuildSlim',-.12),('BuildBroad',.12)]:
  key=ob.data.shape_keys.key_blocks.get(name) or ob.shape_key_add(name=name)
  for v,kv in zip(basis.data,key.data):
   p=v.co.copy();x,y,z=p
   if .23<z<.80:
    side='L' if x>0 else 'R';b=rig.data.bones['UpperLeg.'+side] if z>.44 else rig.data.bones['LowerLeg.'+side];t=max(0,min(1,(z-b.head_local.z)/(b.tail_local.z-b.head_local.z)));c=b.head_local.lerp(b.tail_local,t);f=amt*ss(.23,.28,z)*(1-ss(.72,.80,z));p.x+=(x-c.x)*f;p.y+=(y-c.y)*f
   elif .80<=z<1.16:
    f=amt*ss(.78,.85,z)*(1-ss(1.07,1.16,z))*(1-ss(.19,.28,abs(x)));p.x+=x*f;p.y+=(y-.055)*f
   kv.co=p
build_shapes(body)

# Rebuild the garment with actual collar/sleeve boundaries, no skin triangles in Shirt.
shirt=build_union('Shirt_New',True)
cut(shirt,Vector((0,0,.826)),Vector((0,0,1)),True)
cut(shirt,Vector((0,0,1.223)),Vector((0,0,1)),False)
for side,sign in [('L',1),('R',-1)]:
 a=rig.data.bones['UpperArm.'+side];u=(a.tail_local-a.head_local).normalized();p=a.head_local+u*.145
 cut(shirt,p,u,False,lambda co:sign*co.x>.185 and co.z>.99)
active(shirt);solid=shirt.modifiers.new('Fabric thickness','SOLIDIFY');solid.thickness=.003;solid.offset=-1;bpy.ops.object.modifier_apply(modifier=solid.name)
shirt.data.materials.append(white);rig_mesh(shirt,True);build_shapes(shirt)
bpy.data.objects.remove(bpy.data.objects['Shirt_Default'],do_unlink=True);shirt.name='Shirt_Default'

def ribbon(name,rows,mat):
 vs=[tuple(p) for row in rows for p in row];N=len(rows[0]);fs=[]
 for j in range(len(rows)-1):
  for i in range(N-1):a=j*N+i;fs.append((a,a+1,a+1+N,a+N))
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update();ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);me.materials.append(mat)
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
 active(ob);s=ob.modifiers.new('Soft rounded fabric edge','SOLIDIFY');s.thickness=.0025;bpy.ops.object.modifier_apply(modifier=s.name);rig_mesh(ob,True);build_shapes(ob);return ob
angles=[math.radians(-50+i/48*280) for i in range(49)];rows=[]
for t in [0,.2,.65,1]:
 row=[]
 for a in angles:
  front=max(0,-math.sin(a));rx=.078+t*.030;ry=.072+t*.022;z=1.226-t*.042-t*front*.020
  row.append(Vector((rx*math.cos(a),.055+ry*math.sin(a),z)))
 rows.append(row)
ribbon('Shirt_Collar',rows,navy)
trim=[]
for t in [.90,1.0]:
 trim.append([Vector(((.078+t*.030)*math.cos(a),.055+(.072+t*.022)*math.sin(a),1.226-t*.042-t*max(0,-math.sin(a))*.020+.0008)) for a in angles])
ribbon('Shirt_CollarOrange',trim,orange)
for side,sign in [('L',1),('R',-1)]:
 a=rig.data.bones['UpperArm.'+side];u=(a.tail_local-a.head_local).normalized();v=Vector((0,1,0));w=u.cross(v).normalized();origin=a.head_local+u*.145
 for label,lo,hi,mat in [('Navy',-.018,.001,navy),('Orange',-.023,-.017,orange)]:
  rows=[]
  for s in [lo,(lo+hi)/2,hi]:
   rows.append([origin+u*s+(v*math.cos(math.tau*i/40)+w*math.sin(math.tau*i/40))*.084 for i in range(41)])
  ribbon('Shirt_Cuff'+label+'_'+side,rows,mat)
rows=[]
for x in [-.0035,.0035]:rows.append([Vector((x,-.077-(1.185-z)*.26,z)) for z in [1.185,1.16,1.13,1.103]])
ribbon('Shirt_PlacketOrange',rows,orange)

report['face_coordinates_unchanged']=face_coordinates(body)==face_before
assert report['face_coordinates_unchanged'],'Stop: face geometry drift'
report['triangles']={o.name:sum(len(p.vertices)-2 for p in o.data.polygons) for o in bpy.data.objects if o.type=='MESH' and o.name.startswith(('Body_','Shirt_'))}
report['body_key_names']=[k.name for k in body.data.shape_keys.key_blocks]
report['bone_rest_preserved']=True
(O/'clean-foundation.json').write_text(json.dumps(report,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));print('CLEAN_FOUNDATION_DONE',json.dumps(report))
