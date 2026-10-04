"""Restore working arm coverage; discard only obsolete torso neck fragments after the new head join."""
import bpy,bmesh,json
from pathlib import Path
from mathutils.kdtree import KDTree
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));o=bpy.data.objects['Body_Skin']
with bpy.data.libraries.load(str(O/'checkpoints/pre-foundation.blend')) as (a,b):b.objects=['Body_Skin']
src=b.objects[0];vg=src.vertex_groups['Default_VisibleSkin'].index;k=KDTree(len(src.data.vertices))
for v in src.data.vertices:k.insert(v.co,v.index)
k.balance();bm=bmesh.new();bm.from_mesh(o.data);dl=bm.verts.layers.deform.active;vis=o.vertex_groups['Default_VisibleSkin'].index
unseen=set(bm.verts);parts=[]
while unseen:
 v=unseen.pop();part={v};s=[v]
 while s:
  for e in s.pop().link_edges:
   for v in e.verts:
    if v in unseen:unseen.remove(v);part.add(v);s.append(v)
 parts.append(part)
head=max((p for p in parts if max(v.co.z for v in p)>1.6),key=len);restored=0;hidden=0
for v in bm.verts:
 _,ix,d=k.find(v.co)
 if d<.00001:v[dl][vis]=sum(g.weight for g in src.data.vertices[ix].groups if g.group==vg);restored+=1
 if v in head:v[dl][vis]=1
 elif v.co.z>1.14 and abs(v.co.x)<.14:v[dl][vis]=0;hidden+=1
head_edges={e for v in head for e in v.link_edges};boundary=sum(e.is_boundary for e in head_edges)
print('HEAD_BOUNDARY',boundary);assert boundary==0,'Do not remove runtime inner head until authored head is closed'
bm.to_mesh(o.data);bm.free();bpy.data.objects.remove(src,do_unlink=True)
(O/'coverage-repair.json').write_text(json.dumps({'restored_arm_coverage_vertices':restored,'obsolete_torso_neck_vertices_hidden':hidden,'authored_head_boundary_edges':boundary},indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(O/'Hero_V6_Polish.blend'))
