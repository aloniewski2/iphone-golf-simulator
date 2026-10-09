"""Bake approved hero anatomy and fitted golf kit into low-cost rigid crowd pieces.
Original sources remain read-only. Face eye geometry/material boundaries are retained.
"""
import bpy,bmesh,math,json,sys,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
R=Path(__file__).resolve().parents[3];W=R/'work/golf-art-exports';P=W/'proof';W.mkdir(parents=True,exist_ok=True);P.mkdir(exist_ok=True)
skins=[(.76,.44,.27),(.93,.70,.51),(.38,.20,.12),(.63,.38,.24),(.83,.57,.37),(.95,.75,.59)]
shirts=[(.06,.35,.42),(.91,.33,.18),(.91,.89,.78),(.19,.32,.63),(.34,.52,.18),(.66,.22,.37)]
prepared={};far_prepared={};anchors={};source_reports=[]
def group_kind(name):
 if name in ['Head','Neck']:return 'HEAD'
 if name.startswith('Left') and any(p in name for p in ['Arm','Hand','Thumb','Index','Middle','Ring','Little']):return 'ARM_L'
 if name.startswith('Right') and any(p in name for p in ['Arm','Hand','Thumb','Index','Middle','Ring','Little']):return 'ARM_R'
 return 'BODY'
def decimate(o,target):
 mesh=o.data;mesh.calc_loop_triangles();n=len(mesh.loop_triangles)
 if '_HEAD_' in o.name and o.name.startswith('Body'):
  bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('Topology-preserving head LOD','DECIMATE');mod.decimate_type='UNSUBDIV';mod.iterations=1;bpy.ops.object.modifier_apply(modifier=mod.name)
  o.data.calc_loop_triangles();return len(o.data.loop_triangles)
 if n>target:
  bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('Crowd silhouette budget','DECIMATE');mod.ratio=target/n;mod.use_collapse_triangulate=True
  bpy.ops.object.modifier_apply(modifier=mod.name)
 o.data.calc_loop_triangles();return len(o.data.loop_triangles)
def close_mesh(mesh):
 bm=bmesh.new();bm.from_mesh(mesh);edges=[e for e in bm.edges if e.is_boundary]
 if edges:bmesh.ops.holes_fill(bm,edges=edges,sides=0)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update()
def fitted_cloth(src_mesh,body_bvh,name,near):
 # The source is a closed thin shell. Collapsing inner and outer walls together
 # inverts tiny triangles. Reduce only its exterior, then cap the real cut loops.
 coords=[v.co.copy() for v in src_mesh.vertices];high=[];lower=[]
 for poly in src_mesh.polygons:
  pos=sum((coords[v] for v in poly.vertices),Vector())/len(poly.vertices)
  hit=body_bvh.find_nearest(pos);outer=hit and hit[0] is not None and poly.normal.dot(hit[1])>-.05
  if near and pos.z>1.19:high.append(poly)
  if outer and (not near or pos.z<1.22):lower.append(poly)
 def piece(polys,suffix):
  used=sorted({v for p in polys for v in p.vertices});remap={v:i for i,v in enumerate(used)}
  mesh=bpy.data.meshes.new(name+suffix);mesh.from_pydata([coords[v] for v in used],[],[[remap[v] for v in p.vertices] for p in polys]);mesh.update()
  for mat in src_mesh.materials:mesh.materials.append(mat)
  for dst,original in zip(mesh.polygons,polys):dst.material_index=original.material_index;dst.use_smooth=True
  return mesh
 if near:
  # Preserve shared original vertex indices across the native collar/torso join.
  # Limited planar dissolution moves no vertices; a separate collapsed lower
  # slab creates a visible seam or z-fighting even when both slabs are capped.
  polys={p.index:p for p in high+lower};mesh=piece(list(polys.values()),'_continuous_near')
  bm=bmesh.new();bm.from_mesh(mesh)
  edges=[e for e in bm.edges if e.is_manifold and all(f.calc_center_median().z<1.15 for f in e.link_faces)]
  if edges:bmesh.ops.dissolve_limit(bm,angle_limit=.09,verts=list({v for e in edges for v in e.verts}),edges=edges,use_dissolve_boundaries=False,delimit={'MATERIAL','NORMAL'})
  bm.to_mesh(mesh);bm.free();mesh.update();close_mesh(mesh);mesh.calc_loop_triangles();return mesh,len(mesh.loop_triangles)
 lower_mesh=piece(lower,'_outer');lo=bpy.data.objects.new(name+'_outer',lower_mesh);bpy.context.collection.objects.link(lo);bpy.context.view_layer.objects.active=lo
 # The raw shirt has two extremely thin shell walls. Build a sealed distant
 # envelope from its actual fitted exterior points rather than voxelizing that
 # thin wall. This retains the authored bounds and avoids holes/paper folds.
 bm=bmesh.new();bm.from_mesh(lower_mesh);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=list(bm.faces),context='FACES_ONLY')
 result=bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
 unused=[g for g in result.get('geom_unused',[]) if isinstance(g,bmesh.types.BMVert)]
 if unused:bmesh.ops.delete(bm,geom=unused,context='VERTS')
 for face in bm.faces:face.material_index=0;face.smooth=True
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 # Strip the old shell's unused wire edges before budget reduction/capping.
 # They are not part of the new hull and can create spurious filling polygons.
 used=sorted({v for f in bm.faces for v in f.verts},key=lambda v:v.index);remap={v:i for i,v in enumerate(used)}
 hull=bpy.data.meshes.new(name+'_sealed_hull');hull.from_pydata([v.co.copy() for v in used],[],[[remap[v] for v in f.verts] for f in bm.faces]);hull.update()
 for material in lower_mesh.materials:hull.materials.append(material)
 for face in hull.polygons:face.material_index=0;face.use_smooth=True
 bm.free();lo.data=hull
 lower_mesh=lo.data;lower_mesh.calc_loop_triangles();n=len(lower_mesh.loop_triangles);target=150 if 'ARM_' in name else 650
 if n>target:
  mod=lo.modifiers.new('Fitted outer shell reduction','DECIMATE');mod.ratio=target/n;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
 close_mesh(lo.data)
 verts=[v.co.copy() for v in lo.data.vertices];faces=[list(p.vertices) for p in lo.data.polygons];indices=[p.material_index for p in lo.data.polygons]
 if high:
  hi=piece(high,'_native_collar');close_mesh(hi);base=len(verts);verts.extend(v.co.copy() for v in hi.vertices);faces.extend([v+base for v in p.vertices] for p in hi.polygons);indices.extend(p.material_index for p in hi.polygons)
 mesh=bpy.data.meshes.new(name+'_closed');mesh.from_pydata(verts,[],faces);mesh.update()
 for mat in src_mesh.materials:mesh.materials.append(mat)
 for poly,index in zip(mesh.polygons,indices):poly.material_index=index;poly.use_smooth=True
 mesh.calc_loop_triangles();return mesh,len(mesh.loop_triangles)
def shoulder_envelope(mesh,rings,near):
 # A fitted overlapping shoulder hinge is derived from the real shirt armhole.
 # Its rim retains the source contour; a shallow rounded closing surface stays
 # inside the original sleeve envelope and covers the torso hole when lifted.
 vertices=[v.co.copy() for v in mesh.vertices];faces=[list(p.vertices) for p in mesh.polygons];indices=[p.material_index for p in mesh.polygons]
 for side,(rim,index) in rings.items():
  centre=sum(rim,Vector())/len(rim);sign=1 if centre.x>0 else -1
  ordered=sorted(rim,key=lambda v:math.atan2(v.z-centre.z,v.y-centre.y));n=32 if near else 16
  sampled=[ordered[int(i*len(ordered)/n)] for i in range(n)];levels=5 if near else 4;previous=None
  for level in range(levels):
   t=level/levels;scale=math.cos(t*math.pi*.5);offset=.048*math.sin(t*math.pi*.5);current=[]
   for point in sampled:
    v=centre+(point-centre)*scale;v.x+=sign*offset;current.append(len(vertices));vertices.append(v)
   if previous:
    for j in range(n):faces.append([previous[j],previous[(j+1)%n],current[(j+1)%n],current[j]]);indices.append(index)
   previous=current
  pole=len(vertices);vertices.append(centre+Vector((sign*.048,0,0)))
  for j in range(n):faces.append([previous[j],previous[(j+1)%n],pole]);indices.append(index)
 result=bpy.data.meshes.new(mesh.name+'_fitted_shoulder_overlap');result.from_pydata(vertices,[],faces);result.update()
 for material in mesh.materials:result.materials.append(material)
 for poly,index in zip(result.polygons,indices):poly.material_index=index;poly.use_smooth=True
 bm=bmesh.new();bm.from_mesh(result);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(result);bm.free();result.update();result.calc_loop_triangles();return result,len(result.loop_triangles)
def exterior_kit(src_mesh,body_bvh,name,near,target):
 coords=[v.co.copy() for v in src_mesh.vertices];polys=[]
 for p in src_mesh.polygons:
  centre=sum((coords[v] for v in p.vertices),Vector())/len(p.vertices);hit=body_bvh.find_nearest(centre)
  if hit and hit[0] is not None and p.normal.dot(hit[1])>-.05:polys.append(p)
 used=sorted({v for p in polys for v in p.vertices});remap={v:i for i,v in enumerate(used)}
 mesh=bpy.data.meshes.new(name+'_outer');mesh.from_pydata([coords[v] for v in used],[],[[remap[v] for v in p.vertices] for p in polys]);mesh.update()
 for mat in src_mesh.materials:mesh.materials.append(mat)
 for dst,p in zip(mesh.polygons,polys):dst.material_index=p.material_index;dst.use_smooth=True
 ob=bpy.data.objects.new(name+'_outer',mesh);bpy.context.collection.objects.link(ob);bpy.context.view_layer.objects.active=ob
 if near:
  bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.dissolve_limit(bm,angle_limit=.10,verts=list(bm.verts),edges=list(bm.edges),use_dissolve_boundaries=False,delimit={'MATERIAL','NORMAL'});bm.to_mesh(mesh);bm.free();mesh.update()
 if near and 'Kit_Bottom' in name:
  # Consolidate the already closed exterior garment volume, then reduce it.
  # This removes razor-thin folded cap wedges without touching body anatomy.
  close_mesh(mesh)
  mod=ob.modifiers.new('Closed fitted lower garment volume','REMESH');mod.mode='VOXEL';mod.voxel_size=.006;mod.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=mod.name)
  mod=ob.modifiers.new('Quiet cloth fold normals','SMOOTH');mod.factor=.25;mod.iterations=2;bpy.ops.object.modifier_apply(modifier=mod.name)
  mesh=ob.data
 mesh.calc_loop_triangles();n=len(mesh.loop_triangles)
 if target and n>target:
  mod=ob.modifiers.new('Closed exterior garment budget','DECIMATE');mod.ratio=target/n;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
 mesh=ob.data;close_mesh(mesh);mesh.calc_loop_triangles();return mesh,len(mesh.loop_triangles)
for sex in ['Male','Female']:
 source=R/f'ArtDir/golf/source/gallery3-inputs/{sex}GolfMaster.blend';original_source=source
 bpy.ops.wm.open_mainfile(filepath=str(source));rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.animation_data_clear();rig.data.pose_position='POSE'
 for bone in rig.pose.bones:bone.rotation_mode='QUATERNION';bone.rotation_quaternion=(1,0,0,0);bone.location=(0,0,0);bone.scale=(1,1,1)
 # Use real armature deformation for garment and anatomy together, then remove rig.
 # Male's authored A-pose is wider than the female's. Drop upper arms coherently.
 bpy.context.view_layer.update()
 for side in ['Left','Right']:
  b=rig.pose.bones[side+'UpperArm'];a=math.radians(30 if sex=='Male' else 5)*(1 if side=='Left' else -1);head=b.head.copy()
  b.matrix=Matrix.Translation(head)@Matrix.Rotation(a,4,'Y')@Matrix.Translation(-head)@b.matrix
 # Coverage masks were authored for the full hero costume and suppress bare
 # limbs. Crowd includes real skin under clothes; fitted garments occlude it.
 for o in bpy.data.objects:
  if o.type=='MESH' and o.name.startswith('Body'):
   for mod in o.modifiers:
    if mod.type=='NODES':mod.show_viewport=False;mod.show_render=False
 bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
 anchor={}
 for name in ['Head','Neck','LeftUpperArm','RightUpperArm','LeftUpperLeg','LeftLowerLeg','LeftFoot','RightUpperLeg','RightLowerLeg','RightFoot']:
  anchor[name]=list(rig.matrix_world@rig.pose.bones[name].head)
 inputs=[o for o in bpy.data.objects if o.type=='MESH' and (o.name.startswith(('Body','Face','Kit_')) and not o.name.endswith('_High'))]
 if sex=='Female':inputs += [o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Hair_F')]
 points=[]
 for o in inputs:
  ev=o.evaluated_get(deps);m=ev.to_mesh();points.extend(o.matrix_world@v.co for v in m.vertices);ev.to_mesh_clear()
 floor=min(p.z for p in points);anchor['floor']=floor
 for name in list(anchor):
  if name!='floor':anchor[name][2]-=floor
 anchors[sex]=anchor
 baked=[];far_baked=[];scalp=None
 if True:
  body=next(o for o in inputs if o.name.startswith('Body'));ev=body.evaluated_get(deps);bm=ev.to_mesh()
  scalp=BVHTree.FromPolygons([body.matrix_world@v.co-Vector((0,0,floor)) for v in bm.vertices],[list(p.vertices) for p in bm.polygons]);ev.to_mesh_clear()
 def evaluated_bounds(prefix):
  points=[]
  for item in inputs:
   if not item.name.startswith(prefix):continue
   ee=item.evaluated_get(deps);mm=ee.to_mesh();points.extend(item.matrix_world@v.co-Vector((0,0,floor)) for v in mm.vertices);ee.to_mesh_clear()
  return (min(v.z for v in points),max(v.z for v in points)) if points else (0,0)
 bottom_floor,bottom_top=evaluated_bounds('Kit_Bottom');sock_floor,sock_top=evaluated_bounds('Kit_Sock')

 for src in inputs:
  ev=src.evaluated_get(deps);m=ev.to_mesh();coords=[src.matrix_world@v.co-Vector((0,0,floor)) for v in m.vertices];names=[g.name for g in src.vertex_groups]
  roles=[mat.name if mat else 'Skin' for mat in m.materials]
  if src.name.startswith('Hair') and scalp:
   # Fit the unused legacy bob as a separate NPC haircut. A smooth radial
   # envelope preserves each strand's thickness and avoids coincident faces
   # from projecting inner and outer hair shells to the same BVH surface.
   centre=Vector((0,.005,1.555))
   for v,p in enumerate(coords):
    amount=max(0,min(1,(p.z-1.47)/.10));amount=amount*amount*(3-2*amount)
    coords[v]=centre+(p-centre)*(1+.16*amount)
  # Weight-based anatomical ownership prevents arbitrary cuts across the face.
  def kind(v):
   if src.name.startswith(('Face','Hair')):return 'HEAD'
   totals={'BODY':0,'HEAD':0,'ARM_L':0,'ARM_R':0}
   for g in v.groups:
    if g.group<len(names):totals[group_kind(names[g.group])]+=g.weight
   part=max(totals,key=totals.get)
   # Preserve the entire actual head and central neck independently of soft
   # clavicle weights. Lateral upper-chest skin belongs to the fitted torso.
   if src.name.startswith('Body'):
    point=coords[v.index];head_floor=1.445 if sex=='Male' else 1.425
    neck_split=anchor['Neck'][2]-(.02 if sex=='Male' else .01)
    if point.z>head_floor or (point.z>neck_split and abs(point.x)<.095):return 'HEAD'
    # Lower neck stays with the torso: rotating upper-chest skin through the
    # fitted placket caused small patches during the actual cheering nod.
    if part=='HEAD':return 'BODY'
   if src.name=='Kit_Top' and part=='HEAD':return 'BODY'
   return part
  per_vertex=[kind(v) for v in m.vertices];pieces={}
  # Exact rendered kit bounds cover skin independently of the legacy coverage
  # node (which assumed different lower garments). Never put clavicle skin inHEAD.
  for poly in m.polygons:
   counts={k:0 for k in ['BODY','HEAD','ARM_L','ARM_R']}
   for v in poly.vertices:counts[per_vertex[v]]+=1
   part=max(counts,key=counts.get)
   if src.name.startswith('Body'):
    centre=sum((coords[v] for v in poly.vertices),Vector())/len(poly.vertices)
    # Classify the whole polygon first: first-vertex ownership can remove
    # facial polygons whose soft skin weights cross the neck/head boundary.
    if centre.z>(1.445 if sex=='Male' else 1.425):part='HEAD'
    neck_split=anchor['Neck'][2]-(.02 if sex=='Male' else .01)
    neck_junction=(1.31<centre.z<neck_split+.025 and abs(centre.x)<.095 and -.105<centre.y<.10)
    if part=='BODY' and not neck_junction and (centre.z>bottom_floor+.02 or centre.z<sock_top-.018):continue
    if part.startswith('ARM'):
     shoulder=Vector(anchor['LeftUpperArm' if part=='ARM_L' else 'RightUpperArm'])
     if (centre-shoulder).length<.17:continue
   pieces.setdefault(part,[]).append(poly)
  for part,polys in pieces.items():
   used=sorted(set(v for p in polys for v in p.vertices));lookup={v:i for i,v in enumerate(used)}
   mesh=bpy.data.meshes.new(src.name+'_'+part+'_baked');mesh.from_pydata([coords[v] for v in used],[],[tuple(lookup[v] for v in p.vertices) for p in polys]);mesh.update()
   for mat in m.materials:mesh.materials.append(mat)
   for dst,poly in zip(mesh.polygons,polys):dst.material_index=poly.material_index;dst.use_smooth=True
   joint_rings={}
   # Cap the actual garment/anatomical shoulder cut loops before reduction.
   # Their geometry comes from the fitted shirt, preserving its silhouette.
   # No primitive shoulder puffs or hero rig is introduced.
   if src.name=='Kit_Top' or (src.name.startswith('Body') and part.startswith('ARM')):
    bm=bmesh.new();bm.from_mesh(mesh);remaining={e for e in bm.edges if e.is_boundary}
    while remaining:
     seed=next(iter(remaining));stack=[seed];component=set()
     while stack:
      edge=stack.pop()
      if edge in component:continue
      component.add(edge)
      for v in edge.verts:stack.extend(e for e in v.link_edges if e in remaining and e not in component)
     remaining-=component;vs={v for e in component for v in e.verts};centre=sum((v.co for v in vs),Vector())/max(1,len(vs))
     if src.name=='Kit_Top' and part=='BODY' and len(component)>30:
      side='Left' if centre.x>0 else 'Right';span=max(v.co.z for v in vs)-min(v.co.z for v in vs)
      if side not in joint_rings or span>joint_rings[side][2]:
       side_faces=[f for e in component for f in e.link_faces];index=side_faces[0].material_index if side_faces else 0
       joint_rings[side]=([v.co.copy() for v in vs],index,span)
     if min((centre-Vector(anchor[name])).length for name in ['LeftUpperArm','RightUpperArm'])<.16 and len(component)>=3:
      sides=[f for e in component for f in e.link_faces];index=max(set(f.material_index for f in sides),key=lambda i:sum(f.material_index==i for f in sides)) if sides else 0
      for f in bmesh.ops.holes_fill(bm,edges=list(component),sides=0).get('faces',[]):f.material_index=index;f.smooth=True
    bm.to_mesh(mesh);bm.free();mesh.update()
   ob=bpy.data.objects.new(src.name+'_'+part+'_baked',mesh);bpy.context.collection.objects.link(ob)
   if src.name.startswith('Face'):budget=10000
   elif src.name.startswith('Hair'):budget=10000
   elif src.name.startswith('Body'):budget={'HEAD':200000,'ARM_L':600,'ARM_R':600,'BODY':1500}[part]
   elif src.name=='Kit_Top':budget={'BODY':2000,'ARM_L':350,'ARM_R':350,'HEAD':100}[part]
   elif src.name=='Kit_Bottom':budget=650
   elif src.name.startswith('Kit_Shoe'):budget=220
   elif src.name.startswith('Kit_Sock'):budget=90
   else:budget=100
   original_cloth=ob.data.copy() if src.name.startswith('Kit_') else None
   if src.name=='Kit_Top':ob.data,count=fitted_cloth(original_cloth,scalp,ob.name,True)
   elif src.name.startswith('Kit_'):ob.data,count=exterior_kit(original_cloth,scalp,ob.name,True,1800 if src.name=='Kit_Bottom' else (800 if src.name.startswith('Kit_Shoe') else 240))
   else:count=decimate(ob,budget)
   if src.name=='Kit_Top' and part=='BODY':ob.data,count=shoulder_envelope(ob.data,{k:(v[0],v[1]) for k,v in joint_rings.items()},True)
   # Snapshot material roles and all decimated polygon data, before next file load.
   def snapshot(ob,count):return {'source':src.name,'part':part,'vertices':[list(v.co) for v in ob.data.vertices],'faces':[list(p.vertices) for p in ob.data.polygons],'roles':[roles[p.material_index] for p in ob.data.polygons],'triangles':count}
   baked.append(snapshot(ob,count))
   far=ob.copy();far.data=ob.data.copy();bpy.context.collection.objects.link(far);bpy.context.view_layer.objects.active=far
   if src.name=='Kit_Top':
    far.data,unused_count=fitted_cloth(original_cloth,scalp,far.name,False)
    if part=='BODY':far.data,unused_count=shoulder_envelope(far.data,{k:(v[0],v[1]) for k,v in joint_rings.items()},False)
   elif src.name.startswith('Kit_'):
    far.data,unused_count=exterior_kit(original_cloth,scalp,far.name,False,250 if src.name=='Kit_Bottom' else (100 if src.name.startswith('Kit_Shoe') else 60))
   elif src.name.startswith('Body') and part=='HEAD':
    # Reduce broad skin only; the separate painted details are subsequently
    # fitted to this surface rather than allowed to intersect its simplified skin.
    mod=far.modifiers.new('Distant anatomical envelope','DECIMATE');mod.ratio=2000/count;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
   elif src.name.startswith('Face'):
    mod=far.modifiers.new('Distant facial topology','DECIMATE');mod.decimate_type='UNSUBDIV';mod.iterations=3;bpy.ops.object.modifier_apply(modifier=mod.name)
   elif src.name.startswith('Hair'):
    mod=far.modifiers.new('Distant hair silhouette','DECIMATE');mod.ratio=.09;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
   else:
    mod=far.modifiers.new('Distant fitted silhouette','DECIMATE');mod.ratio=.28;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
   far.data.calc_loop_triangles();far_baked.append(snapshot(far,len(far.data.loop_triangles)))
  ev.to_mesh_clear()
 # Keep the near eye surfaces/material boundaries exactly. The unsubdivider
 # may merge iris-ring roles into radial stars, which is not acceptable even
 # for far spectators. Only the other thin detail surfaces are simplified.
 for p in far_baked:
  if not p['source'].startswith('Face'):continue
  native=next(q for q in baked if q['source']==p['source'] and q['part']==p['part'])
  eye_roles={'Face_Sclera','Face_Pupil','Face_Iris','Face_IrisIn','Face_Limbal','Face_Catch'}
  faces=[];roles=[]
  for face,role in zip(p['faces'],p['roles']):
   if role not in eye_roles:faces.append(face);roles.append(role)
  # Move the small painted masks together to retain their authored depth.
  # Per-vertex projection collapses their front/back sides and causes z-fighting.
  vertices=[list(Vector(v)+Vector((0,-.006,0))) for v in p['vertices']]
  base=len(vertices);vertices.extend(native['vertices'])
  for face,role in zip(native['faces'],native['roles']):
   if role in eye_roles:faces.append([v+base for v in face]);roles.append(role)
  used=sorted({v for f in faces for v in f});remap={v:i for i,v in enumerate(used)}
  p['vertices']=[vertices[v] for v in used];p['faces']=[[remap[v] for v in f] for f in faces];p['roles']=roles;p['triangles']=sum(len(f)-2 for f in faces)
 prepared[sex]=baked;far_prepared[sex]=far_baked;source_reports.append({'sex':sex,'source':str(original_source.relative_to(R)),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'preserved_source':str(source.relative_to(R)),'floor_shift':-floor,'anchors':anchor,'baked_counts':[(p['source'],p['part'],p['triangles']) for p in baked],'triangles':sum(p['triangles'] for p in baked),'far_triangles':sum(p['triangles'] for p in far_baked),'far_counts':[(p['source'],p['part'],p['triangles']) for p in far_baked]})
bpy.ops.wm.read_factory_settings(use_empty=True)
mat=bpy.data.materials.new('HERO_CROWD_VERTEX_PALETTE');mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.62;vcol=mat.node_tree.nodes.new('ShaderNodeVertexColor');vcol.layer_name='SpectatorPalette';mat.node_tree.links.new(vcol.outputs['Color'],bs.inputs['Base Color'])
def mix(a,b,t):return tuple(a[j]*(1-t)+b[j]*t for j in range(3))
def colour(role,i):
 skin=skins[i];hair=(.055,.039,.028)
 if role.startswith(('Blockout','Base_Grey','Skin')):return skin,0
 if role.startswith('Hair'):return hair,.25
 if role in ['Kit_Shirt','Kit_Top']:return shirts[i],1
 if role=='Kit_ShirtTrim':return mix(shirts[i],(.88,.88,.85),.27),1
 if role in ['Kit_Shorts','Kit_ShortsBand']:return ((.055,.10,.16) if i%2==0 else (.17,.23,.29)),1
 if role in ['Kit_Shoe','Kit_Sock']:return (.84,.86,.82),1
 if role=='Kit_Sole':return (.15,.18,.20),1
 if role=='Face_Sclera':return (.80,.79,.75),.5
 if role in ['Face_Iris','Face_IrisIn']:return (.15,.085,.041),.5
 if role in ['Face_Pupil','Face_Limbal']:return (.012,.015,.016),.5
 if role=='Face_Catch':return (.96,.98,.98),.5
 if role=='Face_Brow':return hair,0
 if role=='Face_BrowSoft':return mix(skin,hair,.40),0
 if role in ['Face_Lip','Face_LipUp']:return (skin[0]*.91,skin[1]*.79,skin[2]*.81),0
 if role in ['Face_LidLine','Face_LidLower','Face_LidCrease','Face_Seam','Face_Nostril']:return mix(skin,(.13,.064,.037),.38),0
 return skin,0
manifest=[]
def make_piece(i,part,items,pivot):
 verts=[];faces=[];colors=[]
 for p in items:
  if p['part']!=part:continue
  base=len(verts);verts.extend(p['vertices']);faces.extend(tuple(v+base for v in face) for face in p['faces']);colors.extend(colour(role,i) for role in p['roles'])
 mesh=bpy.data.meshes.new(part);mesh.from_pydata([Vector(v)-pivot for v in verts],[],faces);mesh.update();mesh.materials.append(mat)
 attr=mesh.color_attributes.new(name='SpectatorPalette',type='FLOAT_COLOR',domain='CORNER')
 for poly,(color,kind) in zip(mesh.polygons,colors):
  poly.use_smooth=True
  for li in poly.loop_indices:attr.data[li].color=(*color,kind)
 mesh.calc_loop_triangles();return mesh,len(mesh.loop_triangles)
for i in range(6):
 sex='Female' if i%2 else 'Male';root=bpy.data.objects.new('FAN_'+str(i),None);bpy.context.collection.objects.link(root);root.location=(i*2.1,0,0);anchor=anchors[sex];count=0;far_count=0
 for part in ['BODY','HEAD','ARM_L','ARM_R']:
  pivot=Vector((0,0,0)) if part=='BODY' else Vector(anchor['Neck'] if part=='HEAD' else anchor['LeftUpperArm'] if part=='ARM_L' else anchor['RightUpperArm'])
  mesh,n=make_piece(i,part,prepared[sex],pivot);count+=n;ob=bpy.data.objects.new(part,mesh);bpy.context.collection.objects.link(ob);ob.parent=root;ob.location=pivot
  mesh,n=make_piece(i,part,far_prepared[sex],pivot);far_count+=n;far=bpy.data.objects.new(part+'_FAR',mesh);bpy.context.collection.objects.link(far);far.parent=ob;far.location=(0,0,0);far.hide_render=True
 manifest.append({'variant':i,'sex':sex,'triangles':count,'far_triangles':far_count,'anchors':anchor,'source':'Approved hero body/Face +actual Golf_Master fitted kit; topology-preserving head and rigid evaluation. Separate NPC-fitted legacy bob.','skin':skins[i]})
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(W/'GolfGalleryHero4.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
bpy.ops.wm.save_as_mainfile(filepath=str(W/'GolfGalleryHero4.blend'))
(P/'gallery-hero4-source-manifest.json').write_text(json.dumps({'sources':source_reports,'variants':manifest,'notes':['Original hero sources untouched; all53 bones removed from exported crowd.','Painted face geometry retained with role vertex colours; actual close view required.','Alpha records surface role:0skin,.25hair,.5eye,1cloth. Current shared shader remains opaque.']},indent=2)+'\n')
if '--render-proof' in sys.argv:
 sc=bpy.context.scene;sc.render.engine='BLENDER_EEVEE';sc.render.resolution_x=1536;sc.render.resolution_y=768;sc.render.resolution_percentage=100
 sc.world=bpy.data.worlds.new('Gallery hero studio');sc.world.use_nodes=True;sc.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.28,.36,.46,1);sc.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45
 bpy.ops.object.light_add(type='AREA',location=(4,-4,5));bpy.context.object.data.energy=1300;bpy.context.object.data.size=8
 bpy.ops.object.camera_add(location=(5,-12,2.8));cam=bpy.context.object;cam.rotation_euler=(Vector((5,-.05,.90))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=13;sc.camera=cam;sc.view_settings.view_transform='AgX';sc.render.filepath=str(P/'gallery-hero4-source.png');bpy.ops.render.render(write_still=True)
 for name,x in [('male',0),('female',2.1)]:
  cam.data.ortho_scale=.43;cam.location=(x+.20,-3,1.57);cam.rotation_euler=(Vector((x,-.02,1.565))-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.resolution_x=720;sc.render.resolution_y=720;sc.render.filepath=str(P/f'gallery-hero4-{name}-face.png');bpy.ops.render.render(write_still=True)
print('GALLERY_HERO4_STAGED',json.dumps([(x['variant'],x['triangles'],x['far_triangles']) for x in manifest]))
