"""Replace the torn collar-derived neck boundary; retain every core face vertex above 1.30m."""
import bpy,bmesh,json,hashlib,struct,shutil,math
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';C=O/'checkpoints';C.mkdir(exist_ok=True)
if not (C/'pre-foundation.blend').exists():shutil.copy2(O/'Hero_V6_Polish.blend',C/'pre-foundation.blend')
bpy.ops.wm.open_mainfile(filepath=str(C/'pre-foundation.blend'));o=bpy.data.objects['Body_Skin'];rig=bpy.data.objects['Hero_01_Rig']
def core():return sorted(tuple(v.co) for v in o.data.vertices if v.co.z>1.30)
before=core();bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();dl=bm.verts.layers.deform.active;uv=bm.loops.layers.uv.active
unseen=set(bm.verts);parts=[]
while unseen:
 v=unseen.pop();p={v};s=[v]
 while s:
  for e in s.pop().link_edges:
   for v in e.verts:
    if v in unseen:unseen.remove(v);p.add(v);s.append(v)
 parts.append(p)
head=max((p for p in parts if max(v.co.z for v in p)>1.6),key=len)
geom=set(head)|{e for v in head for e in v.link_edges}|{f for v in head for f in v.link_faces}
bmesh.ops.bisect_plane(bm,geom=list(geom),dist=.000001,plane_co=(0,0,1.29),plane_no=(0,0,1),clear_inner=True,clear_outer=False)
# Clear cut-off dangling wires, including a source loose chain inside the neck.
bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.verts.ensure_lookup_table()
# Cross-section ring at 1.29m is the only head boundary left after discarding the torn cloth/neck strip.
edges=[e for e in bm.edges if e.is_boundary and all(abs(v.co.z-1.29)<.00001 for v in e.verts)]
adj={}
for e in edges:
 for v in e.verts:adj.setdefault(v,[]).append(e.other_vert(v))
assert edges and all(len(x)==2 for x in adj.values()),'Neck cut is not a simple closed loop'
remaining=set(adj);loops=[]
while remaining:
 start=min(remaining,key=lambda v:math.atan2(v.co.y-.055,v.co.x));ring=[start];prev=None;cur=start;remaining.remove(start)
 while True:
  nxt=next(v for v in adj[cur] if v!=prev)
  if nxt==start:break
  ring.append(nxt);remaining.discard(nxt);prev,cur=cur,nxt
 loops.append(ring)
print('CUTLOOPS',[(len(q),[(min(v.co[i] for v in q),max(v.co[i] for v in q)) for i in range(3)]) for q in loops])
ring=max(loops,key=len)
for other in loops:
 if other is ring:continue
 f=bm.faces.new(tuple(reversed(other)));f.smooth=True
# Restore original covered-body mask, rather than expose old hidden outfit/foundation triangles.
with bpy.data.libraries.load(str(R/'ArtDir/hero/v5/Hero_01_V5.blend'),link=False) as (a,b):b.objects=['Body_Skin']
src=b.objects[0];vg=next(g for g in src.vertex_groups if g.name=='Default_VisibleSkin');kd=KDTree(len(src.data.vertices))
for v in src.data.vertices:kd.insert(v.co,v.index)
kd.balance();vis=o.vertex_groups['Default_VisibleSkin'].index
for v in bm.verts:
 _,ix,_=kd.find(v.co);val=sum(g.weight for g in src.data.vertices[ix].groups if g.group==vg.index);v[dl][vis]=val
bpy.data.objects.remove(src,do_unlink=True)
for v in bm.verts:
 if v.co.z>1.29:v[dl][vis]=1
headgroup=o.vertex_groups['Head'].index;neckgroup=o.vertex_groups['Neck'].index
skin=bpy.data.materials['Hero_V6_Scalp'];mi=next((i for i,m in enumerate(o.data.materials) if m==skin),None)
if mi is None:o.data.materials.append(skin);mi=len(o.data.materials)-1
# Ease from the head cut to a short, anatomically closed neck. Ring 0 and core face never move.
base=[v.co.copy() for v in ring];rings=[ring]
for z,blend in [(1.283,.08),(1.273,.22),(1.257,.52),(1.238,.79),(1.214,.97),(1.183,1),(1.145,1)]:
 new=[]
 for i,p in enumerate(base):
  d=Vector((p.x,p.y-.055,0));theta=math.atan2(d.y,d.x);q=Vector((.070*math.cos(theta),.055+.063*math.sin(theta),z));pos=p.lerp(q,blend);pos.z=z
  v=bm.verts.new(pos);v[dl][vis]=1;h=max(0,min(1,(z-1.175)/.10));h=h*h*(3-2*h);v[dl][headgroup]=h;v[dl][neckgroup]=1-h
  # Keep every existing shape target at the new neck's rest coordinates.
  for layer in bm.verts.layers.shape.values():v[layer]=pos
  new.append(v)
 rings.append(new)
newfaces=[]
for a,b in zip(rings,rings[1:]):
 for i in range(len(a)):
  f=bm.faces.new((a[i],a[(i+1)%len(a)],b[(i+1)%len(a)],b[i]));f.material_index=mi;f.smooth=True;newfaces.append(f)
  for l in f.loops:l[uv].uv=(.05+.20*i/len(a),.02+.15*(l.vert.co.z-1.145)/.145)
f=bm.faces.new(tuple(reversed(rings[-1])));f.material_index=mi;f.smooth=True;newfaces.append(f)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
after=core();assert before==after,'Face core coordinates drifted'
report={'core_face_coordinates_identical':True,'core_min_height':1.30,'head_neck_cut':1.29,'ring_vertices':len(ring),'new_neck_rings':len(rings)-1,'new_neck_faces':len(newfaces),'source_coverage_restored':True,'note':'Jaw boundary below 1.30m repaired; central eyes/nose/mouth/forehead coordinates unchanged. Visual identity remains pending.'}
(O/'neck-repair.json').write_text(json.dumps(report,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));print('NECK_REPAIR',report)
