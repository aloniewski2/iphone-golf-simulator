"""Editable equivalents of the bounded runtime tailoring field.
Original sources remain read-only; exact UVs/weights/faces are retained.
"""
import bpy,sys,pathlib,json,math,hashlib
from mathutils import Vector
R=pathlib.Path(__file__).resolve().parents[6];W=pathlib.Path(__file__).resolve().parents[1];rows=[]
for golf in [False,True]:
 for sex in ['Male','Female']:
  source=(W/f'inputs/source-templates/Golf_{sex}_KitUV.blend') if golf else (R/f'ArtDir/hero/kit/premium/source/Tennis_{sex}_SleeveFit.blend')
  bpy.ops.wm.open_mainfile(filepath=str(source))
  rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.animation_data_clear();rig.data.pose_position='REST'
  for o in list(bpy.data.objects):
   if o.type!='MESH' or o.name not in ['Kit_Top','Kit_Shoe_L','Kit_Shoe_R','Kit_Bottom']:continue
   if o.name=='Kit_Bottom':
    if golf and sex=='Female':
     mesh=o.data;p=[o.matrix_world@v.co for v in mesh.vertices];hem=min(v.z for v in p);waist=max(v.z for v in p);parent=list(range(len(p)))
     def find(i):
      while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
      return i
     for f in mesh.polygons:
      c=find(f.vertices[0])
      for j in f.vertices[1:]:parent[find(j)]=c
     minima={}
     for i,q in enumerate(p):minima[find(i)]=min(minima.get(find(i),q.z),q.z)
     hips=o.vertex_groups.get('Hips') or o.vertex_groups.new(name='Hips');changed=0
     for i,v in enumerate(mesh.vertices):
      if minima[find(i)]>hem+.004:continue
      t=max(0,min(1,(waist-p[i].z-.06)/.16));amount=t*t*(3-2*t);weights={o.vertex_groups[g.group].name:g.weight for g in v.groups};removed=0
      for name in ['LeftUpperLeg','RightUpperLeg']:
       mass=weights.get(name,0);keep=mass*(1-.32*amount);removed+=mass-keep
       if mass:o.vertex_groups[name].add([i],keep,'REPLACE')
      if removed:hips.add([i],weights.get('Hips',0)+removed,'REPLACE');changed+=1
     rows.append({'sport':'Golf','sex':sex,'piece':o.name,'changedWeightVertices':changed,'field':'32% thigh mass to pelvis on outer skirt only; inner shorts exact','geometry_UV_faces_preserved':True})
    continue
   mesh=o.data;old=[v.co.copy() for v in mesh.vertices];points=[o.matrix_world@p for p in old];changed={}
   if o.name=='Kit_Top':
    parent=list(range(len(points)))
    def find(i):
     while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
     return i
    for f in mesh.polygons:
     c=find(f.vertices[0])
     for j in f.vertices[1:]:parent[find(j)]=c
    groups={}
    for i in range(len(points)):groups.setdefault(find(i),[]).append(i)
    for ids in groups.values():
     lo=Vector([min(points[i][k] for i in ids) for k in range(3)]);hi=Vector([max(points[i][k] for i in ids) for k in range(3)])
     collar=lo.z>(1.20 if sex=='Female' else 1.26) and hi.z>(1.38 if sex=='Female' else 1.43) and .12<hi.x-lo.x<.34
     if not collar:continue
     for i in ids:
      q=points[i].copy();width=abs(q.x)
      if width>.055:q.x=math.copysign(.055+(width-.055)*.78,q.x)
      if q.y<-.065:q.y=-.065+(q.y+.065)*.80
      bottom=1.285 if sex=='Female' else 1.325;t=max(0,min(1,(q.z-bottom)/.10));q.z+=.009*(1-t*t*(3-2*t));changed[i]=o.matrix_world.inverted()@q-old[i]
    if golf:
     for i,q in enumerate(points):
      if q.z<1.055:
       t=max(0,min(1,(q.z-.985)/.07));delta=Vector((0,0,-.012*(1-t*t*(3-2*t))));changed[i]=changed.get(i,Vector())+o.matrix_world.inverted().to_3x3()@delta
   else:
    start=.100 if sex=='Female' else .112
    for i,q in enumerate(points):
     if q.z>start:q=q.copy();q.z=start+(q.z-start)*.56;changed[i]=o.matrix_world.inverted()@q-old[i]
   for i,delta in changed.items():
    if mesh.shape_keys:
     for key in mesh.shape_keys.key_blocks:key.data[i].co+=delta
    mesh.vertices[i].co+=delta
   mesh.update();rows.append({'sport':'Golf' if golf else 'Tennis','sex':sex,'piece':o.name,'changedVertices':len(changed),'maxDeltaMetres':max([v.length for v in changed.values()] or [0]),'source':str(source.relative_to(R)),'sourceSHA256':hashlib.sha256(source.read_bytes()).hexdigest(),'vertices':len(mesh.vertices),'triangles':sum(len(f.vertices)-2 for f in mesh.polygons),'UV_weights_faces_preserved':True})
  out=W/'source'/f'Premium_{sex}_{"Golf" if golf else "Tennis"}_TailoringV4.blend';bpy.ops.wm.save_as_mainfile(filepath=str(out))
(W/'tailoring-source-manifest.json').write_text(json.dumps(rows,indent=2)+'\n');print(json.dumps(rows,indent=2))
