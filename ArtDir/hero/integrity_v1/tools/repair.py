import bpy,bmesh,math,json,sys,hashlib
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(sys.argv[sys.argv.index('--')+1]);out=root/'ArtDir/hero/integrity_v1';rig=bpy.data.objects['Hero_01_Rig']
rig.animation_data_clear()
for b in rig.pose.bones:b.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
def bone_state():return {b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
before_bones=bone_state();report={'source':bpy.data.filepath,'hair':{},'legs':{},'skeleton_changed':False}
def smoothstep(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def axis(side,z):
 points=[rig.matrix_world@rig.data.bones['UpperLeg.'+side].head_local,rig.matrix_world@rig.data.bones['LowerLeg.'+side].head_local,rig.matrix_world@rig.data.bones['Foot.'+side].head_local]
 for a,b in zip(points,points[1:]):
  if min(a.z,b.z)<=z<=max(a.z,b.z):return a.lerp(b,(z-a.z)/(b.z-a.z))
 return points[-1] if z<points[-1].z else points[0]
def factor(z):
 knots=[(.18,1),(.24,.95),(.30,.87),(.37,.83),(.44,.90),(.53,.90),(.64,.94),(.70,1)]
 if z<=knots[0][0] or z>=knots[-1][0]:return 1
 for (a,x),(b,y) in zip(knots,knots[1:]):
  if z<=b:return x+(y-x)*smoothstep(a,b,z)
def change(o,co):
 p=o.matrix_world@co
 if p.z<.18 or p.z>.70 or abs(p.x)<.02:return co.copy()
 a=axis('L' if p.x>0 else 'R',p.z);f=factor(p.z)
 return o.matrix_world.inverted()@Vector((a.x+(p.x-a.x)*f,a.y+(p.y-a.y)*f,p.z))
for name in ['Body_Skin','Shorts_Default']:
 o=bpy.data.objects[name];n=0;profiles={}
 for z in [.30,.37,.44,.53]:
  p=[o.matrix_world@v.co for v in o.data.vertices if abs((o.matrix_world@v.co).z-z)<.008 and (o.matrix_world@v.co).x>0]
  if p:profiles[str(z)]={'before_width':max(v.x for v in p)-min(v.x for v in p),'before_depth':max(v.y for v in p)-min(v.y for v in p),'radial_factor':factor(z)}
 if o.data.shape_keys:
  for key in o.data.shape_keys.key_blocks:
   for v in key.data:v.co=change(o,v.co)
  # The mesh basis is kept in sync explicitly for modifier/export consumers.
  for v,k in zip(o.data.vertices,o.data.shape_keys.key_blocks[0].data):v.co=k.co
 else:
  for v in o.data.vertices:
   q=change(o,v.co);n+=int((q-v.co).length>1e-6);v.co=q
 for z,row in profiles.items():
  p=[o.matrix_world@v.co for v in o.data.vertices if abs((o.matrix_world@v.co).z-float(z))<.008 and (o.matrix_world@v.co).x>0]
  row['after_width']=max(v.x for v in p)-min(v.x for v in p);row['after_depth']=max(v.y for v in p)-min(v.y for v in p)
 report['legs'][name]=profiles
 # Correct knee influence around the real joint; keep front cap largely on thigh.
 for side in ['L','R']:
  upper=o.vertex_groups.get('UpperLeg.'+side);lower=o.vertex_groups.get('LowerLeg.'+side)
  if not upper or not lower:continue
  knee=rig.matrix_world@rig.data.bones['LowerLeg.'+side].head_local
  edited=0
  for v in o.data.vertices:
   p=o.matrix_world@v.co
   if (p.x>0)!=(side=='L') or abs(p.z-knee.z)>.075:continue
   ws={o.vertex_groups[g.group].name:g.weight for g in v.groups};total=ws.get(upper.name,0)+ws.get(lower.name,0)
   if total<.6:continue
   t=smoothstep(knee.z-.055,knee.z+.055,p.z)
   cap=smoothstep(.025,.07,knee.y-p.y)*(1-smoothstep(.01,.065,abs(p.z-knee.z)))
   t=t*(1-cap*.5)+.78*cap*.5
   upper.add([v.index],total*t,'REPLACE');lower.add([v.index],total*(1-t),'REPLACE');edited+=1
  report['legs'][name]['knee_'+side+'_weights']=edited
# Solidify and weld the old broken shells, then voxel consolidate only the hair.
# All body topology, UVs, rest matrices and armature hierarchy are retained.
for o in list(bpy.data.objects):
 if o.type!='MESH' or not o.name.startswith('Hair_') or o.name.startswith(('Hair_BandFill','Hair_Bald','Hair_Buzz','Hair_Waves')):continue
 src=o.copy();src.data=o.data.copy();bpy.context.collection.objects.link(src);src.name='_weight_source_'+o.name;src.hide_render=True
 for m in src.modifiers:m.show_viewport=False
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);open_before=sum(e.is_boundary for e in bm.edges);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 for mod in o.modifiers:mod.show_viewport=False
 bpy.context.view_layer.objects.active=o
 solid=o.modifiers.new('Closed lock thickness','SOLIDIFY');solid.thickness=.004;solid.offset=-.5;solid.use_even_offset=False;bpy.ops.object.modifier_apply(modifier=solid.name)
 rem=o.modifiers.new('Weld sculpt volumes','REMESH');rem.mode='VOXEL';rem.voxel_size=.004;rem.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=rem.name)
 smooth=o.modifiers.new('Clean stylized lock surface','SMOOTH');smooth.factor=.22;smooth.iterations=3;bpy.ops.object.modifier_apply(modifier=smooth.name)
 target=7000 if 'Long' in o.name or 'Ponytail' in o.name else 6000
 tri=sum(len(p.vertices)-2 for p in o.data.polygons)
 if tri>target:
  dec=o.modifiers.new('Mobile closed locks','DECIMATE');dec.ratio=target/tri;bpy.ops.object.modifier_apply(modifier=dec.name)
 for g in src.vertex_groups:
  if g.name not in o.vertex_groups:o.vertex_groups.new(name=g.name)
 transfer=o.modifiers.new('Restore same-skeleton hair weights','DATA_TRANSFER');transfer.object=src;transfer.use_vert_data=True;transfer.data_types_verts={'VGROUP_WEIGHTS'};transfer.vert_mapping='POLYINTERP_NEAREST';transfer.layers_vgroup_select_src='ALL';transfer.layers_vgroup_select_dst='NAME';transfer.mix_mode='REPLACE';bpy.ops.object.modifier_apply(modifier=transfer.name)
 # New closed hair surface gets a clean UV layout for the later texture pass.
 for layer in list(o.data.uv_layers):o.data.uv_layers.remove(layer)
 o.data.uv_layers.new(name='HairUV');bpy.ops.object.select_all(action='DESELECT');o.select_set(True)
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));open_after=sum(e.is_boundary for e in bm.edges);bm.to_mesh(o.data);bm.free()
 for p in o.data.polygons:p.use_smooth=True
 for mod in o.modifiers:mod.show_viewport=True
 # Ensure at most four normalized bone influences, filling any remesh interpolation misses.
 valid={g.index for g in o.vertex_groups if g.name in rig.data.bones};head=o.vertex_groups.get('Head') or o.vertex_groups.new(name='Head')
 for v in o.data.vertices:
  ws=sorted([(g.group,g.weight) for g in v.groups if g.group in valid and g.weight>1e-6],key=lambda x:-x[1])[:4];total=sum(w for _,w in ws)
  for g in list(v.groups):
   if g.group in valid:o.vertex_groups[g.group].remove([v.index])
  if total<1e-6:head.add([v.index],1,'REPLACE')
  else:
   for gi,w in ws:o.vertex_groups[gi].add([v.index],w/total,'REPLACE')
 report['hair'][o.name]={'open_before':open_before,'open_after':open_after,'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'UV':'HairUV'}
 bpy.data.objects.remove(src,do_unlink=True)
assert bone_state()==before_bones
report['skeleton_changed']=False
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Hero_Integrity_v1.blend'))
(out/'repair-report.json').write_text(json.dumps(report,indent=2));print('REPAIR_DONE')
