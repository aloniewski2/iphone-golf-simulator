import bpy,bmesh,math,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from pathlib import Path
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'v5_before/Hero_01_Mixamo_QA.blend'))
rig=bpy.data.objects['Hero_01_Rig'];rig.animation_data.action=None
for b in rig.pose.bones:b.matrix_basis.identity()
bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
def mat(name,color):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True;b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Base Color'].default_value=(*color,1);b.inputs['Roughness'].default_value=.75;return m
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def mesh(name,vs,fs,mats,mi=None):
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs)
 for m in mats:me.materials.append(m)
 for f in me.polygons:f.use_smooth=True;f.material_index=mi[f.index] if mi else 0
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free();return me
def replace(name,vs,fs,mats,mi=None):
 o=bpy.data.objects[name];o.data=mesh(name+'_V5',vs,fs,mats,mi);o.vertex_groups.clear();o.vertex_groups.new(name='Head').add(list(range(len(vs))),1,'REPLACE');return o
orange=mat('Hero_01_V5Orange',(1,.147,.047));navy=mat('Hero_01_V5Navy',(.025,.04,.095));white=mat('Hero_01_V5White',(.92,.92,.90));blonde=mat('Hero_01_V5Blonde',(.9,.55,.18))
# Shorts silhouette: preserve waistband/crotch and ease each leg opening outward.
shorts=bpy.data.objects['Shorts_Default'];oldshort=[v.co.copy() for v in shorts.data.vertices]
for v in shorts.data.vertices:
 x,y,z=v.co;sgn=1 if x>0 else -1
 amount=.10*(1-smooth(.53,.68,z))*smooth(.035,.13,abs(x))
 v.co.x=sgn*.14+(x-sgn*.14)*(1+amount)
# Surface-conforming orange pocket and side piping, transferred from original triangles.
shorts.data.update();tree=BVHTree.FromPolygons([v.co for v in shorts.data.vertices],[list(f.vertices) for f in shorts.data.polygons])
vs=[];fs=[];weights=[]
for sgn in [-1,1]:
 for path in [[(.115,.785),(.14,.768),(.17,.735),(.195,.69)],[(.205,.77),(.219,.70),(.232,.63),(.243,.555)]]:
  pts=[]
  for j in range(len(path)-1):
   for k in range(9):
    t=k/9;x=sgn*(path[j][0]*(1-t)+path[j+1][0]*t);z=path[j][1]*(1-t)+path[j+1][1]*t
    if path[0][0]>.2:
     band=[abs(v.co.x) for v in shorts.data.vertices if abs(v.co.z-z)<.014 and v.co.x*sgn>0]
     if band:x=sgn*(max(band)-.012)
    hit,n,fi,d=tree.ray_cast(Vector((x,-.5,z)),Vector((0,1,0)))
    if hit is not None:pts.append((hit+n*.0015,fi))
  if len(pts)<3:continue
  for iteration in range(5):
   pts=[pts[0]]+[(pts[j][0].lerp((pts[j-1][0]+pts[j+1][0])*.5,.65),pts[j][1]) for j in range(1,len(pts)-1)]+[pts[-1]]
  start=len(vs)
  for j,(point,fi) in enumerate(pts):
   tangent=(pts[min(j+1,len(pts)-1)][0]-pts[max(0,j-1)][0]).normalized();side=tangent.cross(Vector((0,-1,0))).normalized();up=side.cross(tangent).normalized()
   f=shorts.data.polygons[fi];near=min(f.vertices,key=lambda i:(shorts.data.vertices[i].co-point).length)
   w={};influence=[1/max(.001,(shorts.data.vertices[i].co-point).length)**2 for i in f.vertices];total=sum(influence)
   for vi,blend in zip(f.vertices,influence):
    for g in shorts.data.vertices[vi].groups:
     name=shorts.vertex_groups[g.group].name;w[name]=w.get(name,0)+g.weight*blend/total
   w=dict(sorted(w.items(),key=lambda p:p[1],reverse=True)[:4]);total=sum(w.values());w={n:a/total for n,a in w.items()}
   for q in range(8):vs.append(point+.0025*(side*math.cos(q*math.tau/8)+up*math.sin(q*math.tau/8)));weights.append(w)
  for j in range(len(pts)-1):
   for q in range(8):a=start+j*8+q;b=start+j*8+(q+1)%8;fs.append((a,b,b+8,a+8))
if vs:
 me=mesh('Shorts_Piping',vs,fs,[orange]);o=bpy.data.objects.new('Shorts_Piping',me);bpy.context.collection.objects.link(o)
 for i,w in enumerate(weights):
  for n,a in w.items():(o.vertex_groups.get(n) or o.vertex_groups.new(name=n)).add([i],a,'REPLACE')
 bpy.ops.object.select_all(action='DESELECT');shorts.select_set(True);o.select_set(True);bpy.context.view_layer.objects.active=shorts;bpy.ops.object.join()
# Thin visor: narrow headband and curved brim, fully separate from hair cap.
vs=[];fs=[];mi=[]
def quad(a,b,c,d,m):fs.append((a,b,c,d));mi.append(m)
N=96
for row in range(4):
 for j in range(N):
  a=j/N*math.tau;x=.178*math.sin(a);y=.035-.153*math.cos(a);z=1.554+.009*math.cos(a)
  # Navy lower line, hairline-safe white band; 28 mm total band.
  z += [-.004,0,.021,.025][row];vs.append((x,y,z))
for r in range(3):
 for j in range(N):quad(r*N+j,r*N+(j+1)%N,(r+1)*N+(j+1)%N,(r+1)*N+j,1 if r==0 else 0)
start=len(vs);M=64
for r in range(5):
 t=r/4
 for j in range(M+1):
  a=-1.38+j/M*2.76;x=(.178+.021*t)*math.sin(a);y=.035-(.153+.094*t*max(.15,math.cos(a)))*math.cos(a);z=1.554+.009*math.cos(a)+t*(.025*math.cos(a)-.009)
  vs.append((x,y,z))
for r in range(4):
 for j in range(M):a=start+r*(M+1)+j;quad(a,a+1,a+M+2,a+M+1,0)
# Navy underside, 3 mm below the white curved brim.
topstart=start;understart=len(vs)
for co in vs[topstart:topstart+5*(M+1)]:vs.append((co[0],co[1],co[2]-.003))
for r in range(4):
 for j in range(M):a=understart+r*(M+1)+j;quad(a+M+1,a+M+2,a+1,a,0)
# Thin navy rim with restrained orange line under white brim.
start=len(vs)
for r in range(3):
 for j in range(M+1):
  a=-1.38+j/M*2.76;x=.199*math.sin(a);y=.035-(.153+.094*max(.15,math.cos(a)))*math.cos(a);z=1.545+.034*math.cos(a)-[0,.006,.0075][r];vs.append((x,y,z))
for r in range(2):
 for j in range(M):a=start+r*(M+1)+j;quad(a,a+1,a+M+2,a+M+1,1 if r==0 else 2)
replace('Hat_Visor',vs,fs,[white,navy,orange],mi)
# Opaque smooth cap with 4 broad swept tufts. Continuous fringe boundary, no alpha.
vs=[];fs=[];N=96;R=20
for r in range(R+1):
 t=r/R
 for j in range(N):
  a=j/N*math.tau;front=max(0,math.cos(a));bottom=1.43+.094*front**1.5
  thetaMax=math.acos(max(-1,min(1,(bottom-1.445)/.217)));theta=thetaMax*(1-t)
  vs.append((.174*math.sin(theta)*math.sin(a),.035-.153*math.sin(theta)*math.cos(a),1.445+.217*math.cos(theta)))
for r in range(R):
 for j in range(N):a=r*N+j;b=r*N+(j+1)%N;fs.append((a,b,b+N,a+N))
# Four swept crown ribbons over the continuous cap: broad clumps, narrow channels.
for cx in [-.115,-.038,.038,.115]:
 start=len(vs);rows=32;cols=12
 for r in range(rows+1):
  t=r/rows;y=-.112+.294*t;sweep=.025*math.sin(math.pi*t)
  for j in range(cols+1):
   u=j/cols;x=cx+sweep+(u-.5)*.074
   base=max(.006,1-(x/.183)**2-((y-.035)/.166)**2)
   z=1.445+.221*math.sqrt(base)+.013*math.sin(math.pi*u)**.7*math.sin(math.pi*t)**.5
   vs.append((x,y,z))
 for r in range(rows):
  for j in range(cols):a=start+r*(cols+1)+j;fs.append((a,a+1,a+cols+2,a+cols+1))
replace('Hair_Default',vs,fs,[blonde])
# Leg-only smooth corrections: preserve join rings; broad calf belly, softly tapered ankle.
body=bpy.data.objects['Body_Skin']
# Remove obsolete hair-colored face patches underneath the replacement Hair slot.
for i,m in enumerate(body.data.materials):
 if m.name=='skin_BlondHair':body.data.materials[i]=bpy.data.materials['skin_CoveredFoundation']
foundation=next(i for i,m in enumerate(body.data.materials) if m.name=='skin_CoveredFoundation')
for f in body.data.polygons:
 c=sum((body.data.vertices[i].co for i in f.vertices),Vector())/len(f.vertices)
 if .13<abs(c.x) and 1.31<c.z<1.54:f.material_index=foundation
bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table()
for iteration in range(9):
 updates={}
 for v in bm.verts:
  x,y,z=v.co
  if not (.235<z<.64 and .07<abs(x)<.31) or v.is_boundary:continue
  fade=smooth(.235,.28,z)*(1-smooth(.60,.64,z));amount=.25*fade
  if v.link_edges:updates[v.index]=v.co.lerp(sum((e.other_vert(v).co for e in v.link_edges),Vector())/len(v.link_edges),amount)
 for i,p in updates.items():bm.verts[i].co=p
bm.to_mesh(body.data);bm.free()
# Smooth outward normals on affected legs only; all torso/arm normals retained.
body.data.update();prior=[n.vector.copy() for n in body.data.corner_normals];bm=bmesh.new();bm.from_mesh(body.data);bm.normal_update();norm=[v.normal.copy() for v in bm.verts];bm.free()
body.data.normals_split_custom_set([norm[l.vertex_index] if .235<body.data.vertices[l.vertex_index].co.z<.64 else prior[l.index] for l in body.data.loops])
report={'changed_meshes':['Shorts_Default','Hat_Visor','Hair_Default','Body_Skin (legs only)'],'piping_vertices':len(weights),'armature_bones':len(rig.data.bones),'triangles':{o.name:sum(len(f.vertices)-2 for f in o.data.polygons) for o in [shorts,bpy.data.objects['Hat_Visor'],bpy.data.objects['Hair_Default'],body]}}
(P/'v5-report.json').write_text(json.dumps(report,indent=2));rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1);bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
