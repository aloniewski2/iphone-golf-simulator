import bpy, json, math, sys, hashlib
from pathlib import Path
from mathutils import Matrix
root=Path(sys.argv[sys.argv.index('--')+1]);out=root/'ArtDir/hero/v5_astra';rig=bpy.data.objects['Hero_01_Rig']
rig.animation_data_clear()
for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
report={'source':bpy.data.filepath,'rig':{'bones':len(rig.data.bones),'scale':list(rig.scale),'bones_data':[]},'parts':[]}
for b in rig.data.bones:report['rig']['bones_data'].append({'name':b.name,'parent':b.parent.name if b.parent else None,'head':list(rig.matrix_world@b.head_local),'tail':list(rig.matrix_world@b.tail_local)})
for o in bpy.data.objects:
 if o.type!='MESH' or o.name=='Studio floor':continue
 m=o.data;m.calc_loop_triangles();bad=0;unweighted=0;maxinf=0;badgroups=set()
 for v in m.vertices:
  weights=[g.weight for g in v.groups if o.vertex_groups[g.group].name in rig.data.bones]
  if not weights:unweighted+=1
  if weights and abs(sum(weights)-1)>.005:bad+=1
  maxinf=max(maxinf,len([w for w in weights if w>.0001]))
  for g in v.groups:
   if o.vertex_groups[g.group].name not in rig.data.bones and not o.vertex_groups[g.group].name.startswith(('_','Covered')):badgroups.add(o.vertex_groups[g.group].name)
 uv=m.uv_layers.active;uvbad=0;uvzero=0
 if uv:
  for t in m.loop_triangles:
   a,b,c=[uv.data[i].uv for i in t.loops]
   if not all(math.isfinite(x) for p in [a,b,c] for x in p):uvbad+=1
   if abs((b-a).cross(c-a))<1e-10:uvzero+=1
 # edges are a topology diagnostic; disconnected closed locks are valid.
 edges={tuple(sorted(e.vertices)):0 for e in m.edges}
 for p in m.polygons:
  for e in p.edge_keys:edges[tuple(sorted(e))]=edges.get(tuple(sorted(e)),0)+1
 mats=[]
 for material in m.materials:
  if not material:continue
  mats.append({'name':material.name,'images':[n.image.filepath for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image] if material.use_nodes else []})
 world=[o.matrix_world@v.co for v in m.vertices];profiles={}
 if o.name=='Body_Skin':
  for z in [.30,.37,.44,.53,.60]:
   p=[v for v in world if abs(v.z-z)<.008 and v.x>0]
   if p:profiles[str(z)]={'width':max(v.x for v in p)-min(v.x for v in p),'depth':max(v.y for v in p)-min(v.y for v in p)}
 report['parts'].append({'name':o.name,'vertices':len(m.vertices),'triangles':len(m.loop_triangles),'uv':uv.name if uv else None,'uv_nonfinite_triangles':uvbad,'uv_zero_area_triangles':uvzero,'unweighted':unweighted,'unnormalized':bad,'max_influences':maxinf,'nonbone_groups':sorted(badgroups),'open_edges':sum(n==1 for n in edges.values()),'nonmanifold_edges':sum(n>2 for n in edges.values()),'armatures':[x.object.name for x in o.modifiers if x.type=='ARMATURE' and x.object],'shape_keys':[k.name for k in m.shape_keys.key_blocks] if m.shape_keys else [],'materials':mats,'leg_profile':profiles})
(out/'baseline/blender-audit.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Hero_V5_AstraTex_Baseline.blend'))
print('AUDIT_DONE')
