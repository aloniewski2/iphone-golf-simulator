"""Local rest-form, normal and weight cleanup; keeps topology and shared skeleton."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'polish_v3_before/Hero_01_Mixamo_QA.blend'))
r=bpy.data.objects['Hero_01_Rig'];bn={b.name for b in r.data.bones};o=bpy.data.objects['Body_Skin'];me=o.data
oldnorm=[n.vector.copy() for n in me.corner_normals]
def sm(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
bm=bmesh.new();bm.from_mesh(me);bm.verts.ensure_lookup_table();remaining=set(bm.verts);legs=[];arms=[]
while remaining:
 todo=[remaining.pop()];vs=[]
 while todo:
  v=todo.pop();vs.append(v)
  for e in v.link_edges:
   n=e.other_vert(v)
   if n in remaining:remaining.remove(n);todo.append(n)
 center=sum((v.co for v in vs),Vector())/len(vs)
 if 500<len(vs)<1000 and .3<center.z<.5:legs.extend(v.index for v in vs)
 if 1000<len(vs)<2000 and abs(center.x)>.35:arms.extend(v.index for v in vs)
bm.free();edited=set()
for i in legs:
 v=me.vertices[i];x,y,z=v.co;side=1 if x>0 else -1
 cx=side*(.213-.135*(z-.26));cy=.040
 rx=.102+.020*sm(.32,.56,z);ry=.116+.009*sm(.32,.56,z)
 dx=(x-cx)/rx;dy=(y-cy)/ry;length=math.hypot(dx,dy)
 target=Vector((cx+dx/max(length,1e-6)*rx,cy+dy/max(length,1e-6)*ry,z))
 amount=sm(.26,.305,z)*(1-sm(.51,.555,z))
 v.co=v.co.lerp(target,amount);edited.add(i)
 # Broad, normalized knee transition; ankle transition is retained at sock boundary.
 t=sm(.345,.535,z);w={'UpperLeg.'+('L' if side>0 else 'R'):t,'LowerLeg.'+('L' if side>0 else 'R'):1-t}
 blend=sm(.26,.31,z)*(1-sm(.52,.559,z))
 old={o.vertex_groups[g.group].name:g.weight for g in v.groups if o.vertex_groups[g.group].name in bn}
 new={n:old.get(n,0)*(1-blend)+w.get(n,0)*blend for n in old.keys()|w.keys()};new=dict(sorted(new.items(),key=lambda a:-a[1])[:4]);total=sum(new.values())
 for g in o.vertex_groups:
  if g.name in bn:g.remove([i])
 for n,a in new.items():
  if a>0:o.vertex_groups[n].add([i],a/total,'REPLACE')
# Relax only wrist/forearm surfaces. Keep finger tips and separations untouched.
bm=bmesh.new();bm.from_mesh(me);bm.verts.ensure_lookup_table()
for iteration in range(8):
 updates={}
 for i in arms:
  v=bm.verts[i];z=v.co.z
  strength=.3*sm(.705,.765,z)*(1-sm(.89,.99,z))
  if strength<=0 or v.is_boundary:continue
  avg=sum((e.other_vert(v).co for e in v.link_edges),Vector())/len(v.link_edges)
  updates[i]=v.co.lerp(avg,strength)
 for i,co in updates.items():bm.verts[i].co=co;edited.add(i)
# Restore smooth geometric normals locally instead of the old sculpt's split normals.
bm.normal_update()
newcoords={v.index:v.co.copy() for v in bm.verts};normals={v.index:v.normal.copy() for v in bm.verts};bm.free()
for i,co in newcoords.items():me.vertices[i].co=co
me.update();me.normals_split_custom_set([normals[l.vertex_index] if l.vertex_index in edited else oldnorm[l.index] for l in me.loops])
# Gently relax the polo sleeve cap, keeping collar/cuffs and slot borders fixed.
shirt=bpy.data.objects['Shirt_Default'];bm=bmesh.new();bm.from_mesh(shirt.data);bm.verts.ensure_lookup_table()
for iteration in range(5):
 updates={}
 for v in bm.verts:
  x,y,z=v.co;amount=.2*sm(.135,.20,abs(x))*(1-sm(.31,.36,abs(x)))*sm(1.07,1.13,z)*(1-sm(1.20,1.24,z))
  if amount and not v.is_boundary:updates[v.index]=v.co.lerp(sum((e.other_vert(v).co for e in v.link_edges),Vector())/len(v.link_edges),amount)
 for i,co in updates.items():bm.verts[i].co=co
bm.to_mesh(shirt.data);bm.free();shirt.data.update()
r.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
(P/'v3-limb-report.json').write_text(json.dumps({'leg_vertices':len(legs),'arm_vertices':len(arms),'local_normal_vertices':len(edited),'topology_preserved':True,'skeleton_changed':False},indent=2))
