"""Alternate complete coastal composition, derived from frozen gameplay course.
Source Y meters: identity <=8;8..60 maps to8..14;60..110 maps to14..110;identity >=110.
Near lagoon X left of -5m compresses to 42%, fading to identity from60..110m.
Terrain, bridge and shore sheets deform together. Individual scenery bounds stay rigid.
Blender -b -t4 --python SCRIPT -- --repo REPO --out WORK [--render]
"""
import bpy,math,json,hashlib,sys,argparse
from collections import defaultdict
from pathlib import Path
from mathutils import Vector
p=argparse.ArgumentParser();p.add_argument('--repo',required=True);p.add_argument('--out',required=True);p.add_argument('--render',action='store_true');args=p.parse_args(sys.argv[sys.argv.index('--')+1:]);R=Path(args.repo);OUT=Path(args.out);OUT.mkdir(parents=True,exist_ok=True);INPUT=R/'Unity/Assets/Resources/Course/hole_09.fbx';SHA=hashlib.sha256(INPUT.read_bytes()).hexdigest()
def warp_y(y):
 if y<=8:return y
 if y<60:return 8+(y-8)*6/52
 if y<110:return 14+(y-60)*96/50
 return y
def warp_x(x,y):
 f=.42 if y<=60 else .42+.58*min(1,(y-60)/50)
 return x if x>=-5 else -5+(x+5)*f

def slope(y):return 1 if y<=8 or y>=110 else 6/52 if y<60 else 96/50
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(INPUT),use_anim=False);scene=bpy.context.scene
for o in scene.objects:
 if o.type=='MESH':o.data=o.data.copy()
# Snapshot world matrices before moving any parent/markers.
matrices={o:o.matrix_world.copy() for o in scene.objects}
markers_before={o.name:list(matrices[o].translation) for o in scene.objects if o.name.startswith('MARKER_')}
terrain=[];objects=[];rigid_audit=[]

def components(mesh,V):
 # Same positional welding and connected-piece semantics as runtime ObstacleScan.
 idx={};w=[]
 for p in V:
  k=tuple(round(x*1000) for x in p)
  if k not in idx:idx[k]=len(idx)
  w.append(idx[k])
 parent=list(range(len(idx)))
 def find(i):
  while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
  return i
 for f in mesh.polygons:
  root=find(w[f.vertices[0]])
  for i in f.vertices[1:]:parent[find(w[i])]=root
 groups=defaultdict(list)
 for i,k in enumerate(w):groups[find(k)].append(i)
 return list(groups.values())

def bounds(V,ids):
 lo=Vector(tuple(min(V[i][axis] for i in ids) for axis in range(3)));hi=Vector(tuple(max(V[i][axis] for i in ids) for axis in range(3)));return lo,hi,(lo+hi)*.5

def standing_groups(mesh,V):
 pieces=components(mesh,V);pieces.sort(key=lambda ids:sum(bounds(V,ids)[1][a]-bounds(V,ids)[0][a] for a in (0,1)),reverse=True)
 groups=[];places=[]
 for ids in pieces:
  lo,hi,center=bounds(V,ids);half=((hi.x-lo.x)+(hi.y-lo.y))*.25;into=-1
  for k,(p,h) in enumerate(places):
   if math.hypot(p.x-center.x,p.y-center.y)<min(1.2,.5*max(h,half)):into=k;break
  if into<0:groups.append(list(ids));places.append((center,half))
  else:groups[into].extend(ids)
 return groups

for o in list(scene.objects):
 if o.type!='MESH':continue
 M=matrices[o];inverse=M.inverted();V=[M@v.co for v in o.data.vertices];old_bounds=[list(min(v[i] for v in V) for i in range(3)),list(max(v[i] for v in V) for i in range(3))]
 sea=o.name in ('WATER_OCEAN','WATER_WAVES','WATER_GLINTS')
 rigid=o.name.startswith(('TREE','PLANT','SHRUB','BUSH','ROCK','FLOWER','LIGHTHOUSE','TEE_MARKER','BUILD','RUIN')) and not o.name.startswith('ROCK_SKIN')
 moved=0;largest=0
 if sea:mode='unchanged sea / original water blendshapes'
 elif rigid:
  mode='rigid standing scenery groups'
  groups=[list(range(len(V)))]
  newV=[v.copy() for v in V]
  for group in groups:
   lo,hi,center=bounds(V,group);dy=warp_y(center.y)-center.y;dx=warp_x(center.x,center.y)-center.x
   for i in group:newV[i].y+=dy;newV[i].x+=dx
   nlo,nhi,nc=bounds(newV,group);error=((nhi-nlo)-(hi-lo)).length
   rigid_audit.append({'object':o.name,'vertices':len(group),'pivotBefore':list(center),'pivotAfter':list(nc),'translationY':dy,'shapeExtentErrorMeters':error})
   if error>2e-5:raise RuntimeError('Rigid scenery shape changed')
  for i,v in enumerate(o.data.vertices):
   p1=newV[i];dy=(p1-V[i]).length;moved+=dy>1e-7;largest=max(largest,dy);v.co=inverse@p1
 else:
  mode='continuous complete mesh warp'
  # Preserve authored normals under the piecewise affine deformation.
  original_normals=[n.vector.copy() for n in o.data.corner_normals]
  world_normal=M.inverted().transposed().to_3x3();local_normal=M.transposed().to_3x3()
  normals=[]
  for loop,n in zip(o.data.loops,original_normals):
   wn=world_normal@n;wn.y/=slope(V[loop.vertex_index].y);normals.append((local_normal@wn).normalized())
  for i,v in enumerate(o.data.vertices):
   p1=V[i].copy();p1.x=warp_x(p1.x,p1.y);p1.y=warp_y(p1.y);dy=abs(p1.y-V[i].y);moved+=dy>1e-7;largest=max(largest,dy);v.co=inverse@p1
  o.data.update();o.data.normals_split_custom_set(normals)
 newV=[M@v.co for v in o.data.vertices];new_bounds=[list(min(v[i] for v in newV) for i in range(3)),list(max(v[i] for v in newV) for i in range(3))]
 entry={'name':o.name,'mode':mode,'vertices':len(V),'triangles':sum(len(f.vertices)-2 for f in o.data.polygons),'movedVertices':moved,'maximumYDisplacementMeters':largest,'boundsBefore':old_bounds,'boundsAfter':new_bounds,'shapeKeys':len(o.data.shape_keys.key_blocks) if o.data.shape_keys else 0};objects.append(entry)
 if o.name.startswith('TERRAIN'):terrain.append(entry)
# Empty scenery pivots follow the same mapping; marker tee/pin/up are identity anchors.
for o in scene.objects:
 if o.type!='EMPTY' or o.children or o.name=='HOLE_09_ROOT':continue
 p0=matrices[o].translation.copy();p0.y=warp_y(p0.y);M=matrices[o].copy();M.translation=p0;o.matrix_world=M
markers_after={o.name:list(o.matrix_world.translation) for o in scene.objects if o.name.startswith('MARKER_')}
if markers_before!=markers_after:raise RuntimeError('TEE/PIN/UP moved')
# Save/export the complete native model with the exact original ground prefixes and metadata.
exported=[o for o in scene.objects if o.type in ('MESH','EMPTY')]
for o in scene.objects:o.select_set(o in exported)
bpy.context.view_layer.objects.active=bpy.data.objects.get('HOLE_09_ROOT') or exported[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'ReferenceHole09.fbx'),use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type='OFF',bake_anim=False,path_mode='STRIP',embed_textures=False)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ReferenceHole09.blend'))
report={'status':'ALTERNATE SOURCE CANDIDATE; gameplay/reference composition intentionally changed; actual runtime gates pending','input':str(INPUT.relative_to(R)),'inputSha256':SHA,'sourceInputUnchanged':hashlib.sha256(INPUT.read_bytes()).hexdigest()==SHA,'warp':{'axis':'Blender world Y in meters','anchors':[[8,8],[60,14],[110,110]],'identityOutside':True,'CSharp':'GolfCoastalComposition.DownrangeMetres and SplitAcrossMetres','across':'x>=-5 unchanged; else -5+(x+5)*factor, factor .42 throughy60, fades to1 aty110','yardsConversion':'Convert course D yards to source meters (/1.0936), warp, then multiply1.0936; tee offset0','minimumDerivative':6/52,'maximumDerivative':96/50},'markersBefore':markers_before,'markersAfter':markers_after,'markersExactlyPreserved':True,'terrain':terrain,'objects':objects,'rigidScenery':rigid_audit,'rigidFloatToleranceMeters':.00002,'rigidMaximumShapeExtentErrorMeters':max((x['shapeExtentErrorMeters'] for x in rigid_audit),default=0),'bridgeContinuity':'All original bridge vertices share same continuous monotonic map; connectivity/indices unchanged','gameplayMetadataRequired':'Loader must map Hole.Shore and Hole.Islets using same meters warp. Bunkers+TEE/PIN/distance unchanged. Ground+ObstacleScan derive from this alternate source before dressing.','resource':'Course/Resort/ReferenceHole09','loaderFlag':'VISUAL_COASTAL_REFERENCE=1','fbxSha256':hashlib.sha256((OUT/'ReferenceHole09.fbx').read_bytes()).hexdigest()}
(OUT/'reference-hole09-manifest.json').write_text(json.dumps(report,indent=2));print('REFERENCE_HOLE09',json.dumps({'terrain':terrain,'markers':markers_after,'rigidGroups':len(rigid_audit),'rigidExtentError':report['rigidMaximumShapeExtentErrorMeters']}),flush=True)
