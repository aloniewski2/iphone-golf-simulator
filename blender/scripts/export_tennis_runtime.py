"""Read-only source conversion of the approved resort and V4 tennis standards."""
import bpy, json, math, sys
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
import tennis_clips
arena=R/'Unity/Assets/Resources/Tennis';arena.mkdir(parents=True,exist_ok=True)
# The arena half is independent of the characters and rewrites assets the live tennis
# scene no longer loads, so animation work can skip it entirely.
if '--tennis-only' not in sys.argv:
 bpy.ops.wm.open_mainfile(filepath=str(R/'SportsLibrary/Arenas/Tennis/coastal-tennis-resort-v1.blend'))
 s=bpy.context.scene
 # Keep planted areas separate from the acrylic run-off (x +/-9.2, y +/-18.5).
 garden=bpy.data.collections.new('08 Grass gardens - gameplay v2');s.collection.children.link(garden)
 grass=bpy.data.materials.new('Resort lawn - living green');grass.diffuse_color=(.12,.34,.105,1)
 grass.use_nodes=True;grass.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=grass.diffuse_color
 grass.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.95
 patches=[('West tree lawn',(-25,2,.005),(15,35,.06)),('East tree lawn',(25,2,.005),(15,35,.06)),('Back tree lawn',(0,27,.005),(66,17,.06)),('Left court garden border',(-10,-5,.005),(1.3,27,.06)),('Right court garden border',(10,-5,.005),(1.3,27,.06))]
 for name,location,size in patches:
  bpy.ops.mesh.primitive_cube_add(size=1,location=location);o=bpy.context.object;o.name=name;o.scale=size
  bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
  for c in list(o.users_collection):c.objects.unlink(o)
  garden.objects.link(o);o.data.materials.append(grass)
 trees=[]
 for o in s.objects:
  if not o.name.startswith(('Stone pine trunk','Cypress trunk')):continue
  vertices=[o.matrix_world@v.co for v in o.data.vertices]
  bottom=min(v.z for v in vertices);roots=[v for v in vertices if v.z<bottom+.1]
  x=sum(v.x for v in roots)/len(roots);y=sum(v.y for v in roots)/len(roots)
  supported=any(abs(x-p[0])<=d[0]/2 and abs(y-p[1])<=d[1]/2 for _,p,d in patches)
  outside=abs(x)>9.2 or abs(y)>18.5
  assert supported and outside, f'Tree requires an outside-court lawn: {o.name} at {x},{y}'
  trees.append({'tree':o.name,'root_xy':[x,y],'on_grass':supported,'outside_runoff':outside})
 (R/'SportsLibrary/Arenas/Tennis/gameplay-landscaping-validation.json').write_text(json.dumps(trees,indent=2))
 bpy.ops.wm.save_as_mainfile(filepath=str(R/'SportsLibrary/Arenas/Tennis/coastal-tennis-resort-gameplay-v2.blend'))
 selected=[]
 for o in list(s.objects):
  if o.type not in {'MESH','CURVE'} or o.hide_render or any(c.name.startswith('07') for c in o.users_collection):continue
  o.hide_set(False);o.hide_viewport=False;selected.append(o)
 bpy.ops.object.select_all(action='DESELECT')
 for o in selected:o.select_set(True)
 bpy.context.view_layer.objects.active=selected[0]
 bpy.ops.object.convert(target='MESH')
 # Foliage instances share mesh datablocks. Join mutates the active mesh: isolate them first
 # or merging one tile also changes other tree instances and corrupts their material slots.
 for o in bpy.context.selected_objects:
  if o.type=='MESH':o.data=o.data.copy()
 # Join by spatial tile to reduce object overhead while retaining cullable pieces.
 groups={}
 for o in list(bpy.context.selected_objects):
  p=o.matrix_world.translation;k=(math.floor(p.x/24),math.floor(p.y/24))
  groups.setdefault(k,[]).append(o)
 joined=[]
 for k,objects in groups.items():
  bpy.ops.object.select_all(action='DESELECT')
  for o in objects:o.select_set(True)
  bpy.context.view_layer.objects.active=objects[0]
  if len(objects)>1:bpy.ops.object.join()
  o=bpy.context.object;o.name=f'Resort tile {k[0]} {k[1]}';joined.append(o)
 bpy.ops.object.select_all(action='DESELECT')
 for o in joined:o.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(arena/'CoastalTennisResort.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,use_mesh_modifiers=True,path_mode='AUTO')
 print('ARENA',len(joined),flush=True)
if '--arena-only' in sys.argv:sys.exit(0)
for gender in ['Male','Female']:
 bpy.ops.wm.open_mainfile(filepath=str(R/'SportsLibrary/Blender/sports-animation-studio-v4.blend'))
 s=bpy.data.scenes['03 TENNIS'];bpy.context.window.scene=s
 rig=bpy.data.objects[f'{gender}_Tennis_Rig'];delta=rig.location.copy()
 prop=next(c for c in s.collection.children if c.name.startswith('V4 PROP '+gender+' V4 racket'))
 racket=next(o for o in prop.objects if o.parent is None)
 # Export the whole authored timeline. Baking only the first 682 frames is what limited
 # the runtime to six clips out of the forty-four the library actually contains.
 # Clip ranges come from the V4 NLA strips, which carry exact frame extents. Inferring them
 # from marker gaps breaks as soon as clips are appended out of hand order, which is exactly
 # what converting the leftover V3 animations does.
 track=next((t for t in rig.animation_data.nla_tracks if t.name.startswith('V4 |')),None)
 if not track:raise SystemExit('Missing V4 NLA track; cannot derive clips')
 clips=tennis_clips.derive_from_strips([(st.name,st.frame_start,st.frame_end) for st in track.strips])
 if not clips:raise SystemExit('V4 track has no "<Clip> <RH|LH>" strips; cannot derive clips')
 end=max(s.frame_end,max(c['lastFrame'] for c in clips)+1)
 poses=[]
 for f in range(1,end+1):
  s.frame_set(f);poses.append((f,racket.matrix_world.copy()))
 racket.animation_data_clear();previous=None
 for f,m in poses:
  m.translation-=delta;racket.matrix_world=m
  if previous and racket.rotation_quaternion.dot(previous)<0:racket.rotation_quaternion.negate()
  previous=racket.rotation_quaternion.copy()
  for channel in ['location','rotation_quaternion','scale']:racket.keyframe_insert(channel,frame=f)
 rig.location-=delta
 selected=[rig]+list(bpy.data.collections[f'V4 {gender} Tennis | BODY'].objects)+list(bpy.data.collections[f'V4 {gender} Tennis | KIT 1'].objects)+list(prop.objects)
 for c in s.collection.children:c.hide_viewport=False
 for o in s.objects:o.select_set(False)
 for o in selected:
  o.hide_set(False);o.hide_viewport=False;o.hide_render=False;o.select_set(True)
  if o.type=='MESH':o.data=o.data.copy()
  if o.type=='CURVE':bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH')
 marker=bpy.data.objects.new('TennisSweetSpot',None);s.collection.objects.link(marker);marker.parent=racket;marker.location=(0,0,.4);marker.select_set(True)
 for name,position in [('TennisStringRight',(.2,0,.4)),('TennisStringUp',(0,0,.67)),('TennisStringNormal',(0,.1,.4))]:
  marker=bpy.data.objects.new(name,None);s.collection.objects.link(marker);marker.parent=racket;marker.location=position;marker.select_set(True)
 s.frame_start=1;s.frame_end=end;s.frame_set(1);s.render.fps=30
 bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(R/f'Unity/Assets/Resources/StandardCharacters/standard_{gender.lower()}_tennis.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_step=0.5,bake_anim_simplify_factor=0,path_mode='AUTO')
 print('TENNIS',gender,'frames',end,'clips',len(clips),flush=True)
 (R/'blender/tennis-clips.json').write_text(json.dumps({'timelineEnd':end,'clips':clips},indent=1))
