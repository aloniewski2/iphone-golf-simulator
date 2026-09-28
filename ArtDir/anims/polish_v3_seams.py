import bpy,bmesh,json
from pathlib import Path
from mathutils.kdtree import KDTree
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'polish_v3_before/Hero_01_Mixamo_QA.blend'))
body=bpy.data.objects['Body_Skin'];coords=[v.co.copy() for v in body.data.vertices];bn={b.name for b in bpy.data.objects['Hero_01_Rig'].data.bones};oldweights=[{body.vertex_groups[g.group].name:g.weight for g in v.groups if body.vertex_groups[g.group].name in bn} for v in body.data.vertices]
wardrobe={tuple(round(c,5) for c in v.co) for name in ['Shorts_Default','Shoes_Default'] for v in bpy.data.objects[name].data.vertices}
seams=[i for i,co in enumerate(coords) if .23<co.z<.60 and tuple(round(c,5) for c in co) in wardrobe]
kd=KDTree(len(seams))
for j,i in enumerate(seams):kd.insert(coords[i],i)
kd.balance()
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
o=bpy.data.objects['Body_Skin'];changed=set();oldnorm=[n.vector.copy() for n in o.data.corner_normals]
for i,v in enumerate(o.data.vertices):
 if not .23<v.co.z<.60:continue
 co,j,d=kd.find(coords[i])
 if d>=.035:continue
 t=max(0,min(1,d/.035));amount=1-t*t*(3-2*t)
 v.co=v.co.lerp(coords[i],amount);changed.add(i)
 current={o.vertex_groups[g.group].name:g.weight for g in v.groups if o.vertex_groups[g.group].name in bn};w={n:current.get(n,0)*(1-amount)+oldweights[i].get(n,0)*amount for n in current.keys()|oldweights[i].keys()};w=dict(sorted(w.items(),key=lambda p:-p[1])[:4]);total=sum(w.values())
 for g in o.vertex_groups:
  if g.name in bn:g.remove([i])
 for n,a in w.items():
  if a>0:o.vertex_groups[n].add([i],a/total,'REPLACE')
bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bm.normal_update();norm=[v.normal.copy() for v in bm.verts];bm.free();o.data.update();o.data.normals_split_custom_set([norm[l.vertex_index] if l.vertex_index in changed else oldnorm[l.index] for l in o.data.loops])
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
(P/'v3-seam-report.json').write_text(json.dumps({'locked_original_leg_seam_vertices':len(seams),'blended_near_seam_vertices':len(changed)},indent=2))
