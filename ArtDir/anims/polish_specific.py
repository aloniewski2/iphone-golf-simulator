"""Measured bind-pose corrections, preserving modular topology and one Humanoid."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'specific_before/Hero_01_Mixamo_QA.blend'))
rig=bpy.data.objects['Hero_01_Rig'];rig.animation_data.action=None
for b in rig.pose.bones:b.matrix_basis.identity()
bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
meshes=[o for o in bpy.data.collections['Hero_01'].objects if o.type=='MESH'];bn={b.name for b in rig.data.bones}
def sm(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def lerp(a,b,t):return a+(b-a)*t
factor={'biceps':.865,'forearm':.84,'thigh':.635,'calf':.80,'shoe':.90}
old={o.name:[v.co.copy() for v in o.data.vertices] for o in meshes}
# Identical seam coordinates share the same weight-based deformation mask.
shared={}
for o in meshes:
 for v in o.data.vertices:
  key=tuple(round(c,5) for c in v.co)
  if key not in shared:shared[key]={o.vertex_groups[g.group].name:g.weight for g in v.groups if o.vertex_groups[g.group].name in bn}
def axis_point(co,bone):
 b=rig.data.bones[bone];h=b.head_local;t=b.tail_local;direction=(t-h).normalized();return h+direction*(co-h).dot(direction)
changed={}
for o in meshes:
 count=0
 for v in o.data.vertices:
  co=v.co.copy();x,y,z=co;side='L' if x>=0 else 'R';sgn=1 if x>=0 else -1;w=shared[tuple(round(c,5) for c in co)]
  arm=sum(w.get(n+'.'+side,0) for n in ['Shoulder','UpperArm','LowerArm','Hand']);leg=sum(w.get(n+'.'+side,0) for n in ['UpperLeg','LowerLeg','Foot','Toes'])
  if arm>.05 and z<1.26:
   f=lerp(factor['forearm'],factor['biceps'],sm(.92,1.025,z));f=lerp(.92,f,sm(.72,.80,z))
   bone='UpperArm' if z>.965 else 'LowerArm' if z>.778 else 'Hand';center=axis_point(co,bone+'.'+side)
   v.co=co.lerp(center+(co-center)*f,sm(.05,.85,arm))
  elif leg>.05 and z<.84:
   f=lerp(factor['calf'],factor['thigh'],sm(.38,.53,z));f=lerp(.90,f,sm(.20,.29,z))
   center=axis_point(co,('UpperLeg' if z>.44 else 'LowerLeg')+'.'+side)
   slim=center+(co-center)*f
   shoe=Vector((sgn*.195,-.04,0))+(co-Vector((sgn*.195,-.04,0)))*factor['shoe']
   slim=shoe.lerp(slim,sm(.16,.25,z));v.co=co.lerp(slim,sm(.05,.85,leg))
   v.co.x-=sgn*.008*(1-sm(.65,.80,z))
  if arm>.05 and z>1.10 and z<1.27:
   v.co.z-=.008*sm(.15,.24,abs(x))*sm(1.10,1.17,z)*(1-sm(1.23,1.27,z))
  if z>1.29:
   v.co.x*=1-.07*sm(1.29,1.36,z)
   if o.name=='Body_Skin' and y<.015:
    v.co.y-=.004*(1-sm(.025,.16,abs(x)))*sm(1.29,1.34,z)*(1-sm(1.42,1.49,z))
  if o.name.startswith('Body_Eye'):v.co.y+=.0015
  if o.name=='Hat_Visor':v.co.z+=.018
  if o.name=='Hair_Default':
   # Lift the existing fringe; separate five swept clumps by four shallow channels.
   v.co.z+=.022*(1-sm(-.015,.075,y))*(1-sm(1.53,1.62,z))
   v.co.z-=.018*sm(1.57,1.68,z)
   depth=0
   for center in [-.06,0,.06]:depth+=.012*math.exp(-((v.co.x-center-.14*(y+.025))/.010)**2)
   amount=sm(1.535,1.59,z)*(1-sm(.10,.18,y));radial=(v.co-Vector((0,.045,1.45))).normalized();v.co-=radial*depth*amount
  if (v.co-co).length>1e-7:count+=1
 changed[o.name]=count
# Smooth the front fringe boundary into one continuous forehead opening.
hair=bpy.data.objects['Hair_Default'];bm=bmesh.new();bm.from_mesh(hair.data);bm.verts.ensure_lookup_table()
for v in bm.verts:
 x,y,z=v.co
 if v.is_boundary and z<1.565 and y<.04 and abs(x)<.18:
  target=1.515+.012*math.cos((x+.02)/.16*math.pi);v.co.z=lerp(z,target,(1-sm(-.035,.04,y))*(1-sm(.145,.18,abs(x))))
for _ in range(5):
 updates={}
 for v in bm.verts:
  x,y,z=v.co;t=.25*(1-sm(1.53,1.60,z))*(1-sm(-.02,.07,y))
  if t and not v.is_boundary:updates[v.index]=v.co.lerp(sum((e.other_vert(v).co for e in v.link_edges),Vector())/len(v.link_edges),t)
 for i,co in updates.items():bm.verts[i].co=co
bm.to_mesh(hair.data);bm.free();hair.data.update()
# Narrow the bind skeleton with its lower-body meshes. No new animation is authored.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for b in rig.data.edit_bones:
 if b.name.startswith(('UpperLeg.','LowerLeg.','Foot.','Toes.')):
  shift=.008 if b.name.endswith('.L') else -.008;b.head.x-=shift;b.tail.x-=shift
bpy.ops.object.mode_set(mode='OBJECT')
hat=bpy.data.objects['Hat'];world=hat.matrix_world.copy();world.translation.z+=.018;hat.matrix_world=world
# Relax the small remaining upper-arm/wrist weight transition without changing sums.
for o in meshes:
 if o.name not in ['Body_Skin','Shirt_Default']:continue
 bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table()
 for iteration in range(4):
  updates={}
  for v in bm.verts:
   x,y,z=v.co;amount=.15*sm(.18,.25,abs(x))*sm(1.04,1.10,z)*(1-sm(1.19,1.23,z))
   if v.is_boundary or not amount:continue
   updates[v.index]=v.co.lerp(sum((e.other_vert(v).co for e in v.link_edges),Vector())/len(v.link_edges),amount)
  for i,co in updates.items():bm.verts[i].co=co
 bm.to_mesh(o.data);bm.free();o.data.update()
# Cuff trim belongs to the upper arm, not the chest; eliminate inner cuff folding.
shirt=bpy.data.objects['Shirt_Default'];im=bpy.data.images['Hero_01_BaseColor'];pixels=list(im.pixels);iw,ih=im.size;uv=shirt.data.uv_layers.active.data;cuffkeys=set()
for f in shirt.data.polygons:
 center=sum((old[shirt.name][i] for i in f.vertices),Vector())/len(f.vertices)
 if not (.97<center.z<1.105 and abs(center.x)>.19):continue
 tex=sum((uv[i].uv for i in f.loop_indices),Vector((0,0)))/len(f.loop_indices);ix=max(0,min(iw-1,int(tex.x*iw)));iy=max(0,min(ih-1,int(tex.y*ih)));rr,gg,bb=pixels[(iy*iw+ix)*4:(iy*iw+ix)*4+3]
 if (rr<.24 and gg<.24) or (rr>.45 and gg<.40 and bb<.20):
  cuffkeys.update(tuple(round(c,5) for c in old[shirt.name][i]) for i in f.vertices)
cuffcount=0
for o in meshes:
 if o.name not in ['Body_Skin','Shirt_Default']:continue
 for i,co in enumerate(old[o.name]):
  if tuple(round(c,5) for c in co) not in cuffkeys:continue
  for g in o.vertex_groups:
   if g.name in bn:g.remove([i])
  o.vertex_groups['UpperArm.'+('L' if co.x>0 else 'R')].add([i],1,'REPLACE');cuffcount+=1
# Rebuild geometric vertex normals locally on limbs/head, retaining UVs and topology.
for o in meshes:
 bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bm.normal_update();norm=[v.normal.copy() for v in bm.verts];bm.free();o.data.update()
 if o.name=='Hair_Default':norm=[(v.co-Vector((0,.055,1.45))).normalized() for v in o.data.vertices]
 previous=[n.vector.copy() for n in o.data.corner_normals]
 o.data.normals_split_custom_set([norm[l.vertex_index] if (o.name=='Body_Skin' or o.name in ['Hair_Default','Shirt_Default']) else previous[l.index] for l in o.data.loops])
# Keep exact original shared boundaries aligned after the local sleeve relaxation.
refs={}
for o in meshes:
 for i,co in enumerate(old[o.name]):refs.setdefault(tuple(round(c,5) for c in co),[]).append((o,i))
for entries in refs.values():
 if len({o.name for o,i in entries})<2:continue
 if any(o.name.startswith(('Hair_','Hat_','Body_Eye')) for o,i in entries):continue
 ref,idx=next(((o,i) for o,i in entries if o.name=='Body_Skin'),entries[0]);co=ref.data.vertices[idx].co.copy();w={ref.vertex_groups[g.group].name:g.weight for g in ref.data.vertices[idx].groups if ref.vertex_groups[g.group].name in bn}
 for o,i in entries:
  o.data.vertices[i].co=co
  for g in o.vertex_groups:
   if g.name in bn:g.remove([i])
  for n,a in w.items():o.vertex_groups[n].add([i],a,'REPLACE')
# Four swept tuft shells, joined into the existing Hair slot and weighted to Head.
hair=bpy.data.objects['Hair_Default'];mat=bpy.data.materials.new('Hero_01_HairTuft');mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.69,.42,.16,1);bs.inputs['Roughness'].default_value=.76
hair.data.materials.clear();hair.data.materials.append(mat)
for f in hair.data.polygons:f.material_index=0
verts=[];faces=[]
for cx in [-.09,-.03,.03,.09]:
 base=len(verts);a=Vector((cx,-.065,1.575-abs(cx)*.2));b=Vector((cx+.017,.025,1.835-abs(cx)*.6));c=Vector((cx+.026,.14,1.555-abs(cx)*.25))
 for row in range(13):
  t=row/12;point=(1-t)**2*a+2*(1-t)*t*b+t*t*c;direction=(2*(1-t)*(b-a)+2*t*(c-b)).normalized();side=Vector((1,0,0));up=side.cross(direction).normalized();radius=max(.001,math.sin(math.pi*t)**.65)
  for col in range(12):
   theta=col/12*math.tau;verts.append(point+side*(math.cos(theta)*.032*radius)+up*(math.sin(theta)*.016*radius))
 for row in range(12):
  for col in range(12):
   aidx=base+row*12+col;bidx=base+row*12+(col+1)%12;cidx=bidx+12;didx=aidx+12;faces.extend([(aidx,bidx,cidx),(aidx,cidx,didx)])
 faces.append(tuple(base+i for i in reversed(range(12))));faces.append(tuple(base+144+i for i in range(12)))
mesh=bpy.data.meshes.new('FourSweptTufts');mesh.from_pydata(verts,[],faces);mesh.materials.append(mat);tufts=bpy.data.objects.new('FourSweptTufts',mesh);bpy.context.collection.objects.link(tufts);tufts.vertex_groups.new(name='Head').add(list(range(len(verts))),1,'REPLACE')
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
for f in mesh.polygons:f.use_smooth=True
bpy.ops.object.select_all(action='DESELECT');hair.select_set(True);tufts.select_set(True);bpy.context.view_layer.objects.active=hair;bpy.ops.object.join()
# Stitch the exposed forehead/temple boundary to the raised fringe. This adds skin,
# not a hair-colored cover, and keeps the existing Body/Hair slots swappable.
body=bpy.data.objects['Body_Skin'];hair=bpy.data.objects['Hair_Default'];hairkeys={tuple(round(c,5) for c in co):i for i,co in enumerate(old['Hair_Default'])}
bodykeys={i:tuple(round(c,5) for c in co) for i,co in enumerate(old['Body_Skin'])}
bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table();bridges=[]
for e in bm.edges:
 ids=[v.index for v in e.verts]
 if not e.is_boundary or any(old['Body_Skin'][i].z<1.30 for i in ids):continue
 if all(bodykeys[i] in hairkeys for i in ids):
  aa,bb=ids;ca=body.data.vertices[aa].co.copy();cb=body.data.vertices[bb].co.copy();ha=hair.data.vertices[hairkeys[bodykeys[aa]]].co.copy();hb=hair.data.vertices[hairkeys[bodykeys[bb]]].co.copy()
  if max((ca-ha).length,(cb-hb).length)>.0001:bridges.append((ca,cb,hb,ha))
bm.free()
if bridges:
 vs=[];fs=[]
 for quad in bridges:
  i=len(vs);vs.extend(quad);fs.extend([(i,i+1,i+2),(i,i+2,i+3)])
 me=bpy.data.meshes.new('ForeheadSkinExtension');me.from_pydata(vs,[],fs);me.materials.append(bpy.data.materials['skin_CoveredFoundation']);bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bm.normal_update();bmesh.ops.reverse_faces(bm,faces=[f for f in bm.faces if f.normal.dot(f.calc_center_median()-Vector((0,.055,1.40)))<0]);bm.to_mesh(me);bm.free();ext=bpy.data.objects.new('ForeheadSkinExtension',me);bpy.context.collection.objects.link(ext)
 for name in ['Head','Default_VisibleSkin']:ext.vertex_groups.new(name=name).add(list(range(len(me.vertices))),1,'REPLACE')
 for f in me.polygons:f.use_smooth=True
 bpy.ops.object.select_all(action='DESELECT');body.select_set(True);ext.select_set(True);bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
# Bind-pose cross-section measurements, same anatomical axes before and after.
report={'requested_radial_factors_vs_working_v3':factor,'stance_shift_m_per_leg':.008,'face_width_factor':.93,'visor_raise_m':.018,'fringe_raise_max_m':.022,'hair_channels':3,'forehead_bridge_quads':len(bridges),'changed_vertices':changed,'cuff_vertices_reweighted':cuffcount,'cross_sections_m':{}}
body=bpy.data.objects['Body_Skin'];coverage=body.vertex_groups['Default_VisibleSkin']
for label,bone,frac in [('biceps','UpperArm',.70),('forearm','LowerArm',.50),('thigh','UpperLeg',.72),('calf','LowerLeg',.40)]:
 values={}
 for version in ['before','after']:
  widths=[]
  for side in ['L','R']:
   b=rig.data.bones[bone+'.'+side];axis=(b.tail_local-b.head_local).normalized();point=b.head_local.lerp(b.tail_local,frac)
   if version=='before' and bone in ['UpperLeg','LowerLeg']:point.x+=.008 if side=='L' else -.008
   cross=axis.cross(Vector((0,1,0))).normalized();samples=[]
   for v in body.data.vertices:
    if v.index>=len(old[body.name]):continue
    if not any(g.group==coverage.index and g.weight>.5 for g in v.groups):continue
    co=old[body.name][v.index] if version=='before' else v.co
    if (co.x>0)!=(side=='L') or abs((co-point).dot(axis))>.018 or (co-point).length>.18:continue
    samples.append((co-point).dot(cross))
   widths.append(max(samples)-min(samples) if samples else None)
  values[version]=widths
 report['cross_sections_m'][label]=values
report['shoe_bounds_m']={}
shoe=bpy.data.objects['Shoes_Default']
for version,coords in [('before',old[shoe.name]),('after',[v.co.copy() for v in shoe.data.vertices])]:
 per=[]
 for side in [-1,1]:
  vs=[v for v in coords if v.x*side>0 and v.z<.20];per.append({'width':max(v.x for v in vs)-min(v.x for v in vs),'length':max(v.y for v in vs)-min(v.y for v in vs),'sole_z':min(v.z for v in vs)})
 report['shoe_bounds_m'][version]=per
rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
(P/'specific-blender-report.json').write_text(json.dumps(report,indent=2))
