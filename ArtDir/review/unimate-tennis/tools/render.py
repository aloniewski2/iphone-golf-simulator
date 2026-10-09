import bpy,json,math,sys,os
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
BASE=Path(__file__).resolve().parent.parent
O=Path(os.environ.get('UNIMATE_OUTPUT_DIR',str(BASE)))
bpy.ops.wm.open_mainfile(filepath=str(BASE/'HeroV5_UniMate_Preview.blend'))
scene=bpy.context.scene; rig=bpy.data.objects['Hero_01_Rig']
# Preview-only scene. Preserve the source character's meshes/materials/weights.
for ob in list(bpy.data.objects):
 if ob.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(ob,do_unlink=True)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for m in meshes:m.hide_render=False;m.hide_set(False)
for a in list(bpy.data.actions):bpy.data.actions.remove(a)
rig.animation_data_clear()
for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
scene.render.engine=os.environ.get('UNIMATE_RENDER_ENGINE','BLENDER_EEVEE');scene.cycles.samples=12;scene.cycles.use_denoising=True
try:
 if scene.render.engine!='CYCLES':raise RuntimeError('EEVEE selected')
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='METAL';prefs.get_devices()
 for d in prefs.devices:d.use=d.type=='METAL'
 scene.cycles.device='GPU'
except Exception as e:print('Renderer:',scene.render.engine,e)
scene.render.resolution_x=1400;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.fps=30
scene.eevee.taa_render_samples=16
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.65,.77,.88,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.35
scene.view_settings.view_transform='AgX'
def material(n,c,rough=.75):
 m=bpy.data.materials.new(n);m.diffuse_color=(*c,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Roughness'].default_value=rough;return m
floor=material('Review Court',(.115,.30,.32));line=material('Court Lines',(.87,.92,.86));blue=material('Preview Racket Blue',(.035,.23,.75),.3);grip=material('Preview Handle',(.025,.04,.075));strings=material('Preview Strings',(.75,.86,.94))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.035));ground=bpy.context.object;ground.name='ReviewFloor';ground.data.materials.append(floor)
for y in [-.8,1.1]:
 bpy.ops.mesh.primitive_cube_add(size=1,location=(0,y,-.03));o=bpy.context.object;o.scale=(8,.025,.002);o.data.materials.append(line)
def light(n,loc,power,size,color):
 d=bpy.data.lights.new(n,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color;o=bpy.data.objects.new(n,d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.9))-o.location).to_track_quat('-Z','Y').to_euler()
light('Soft key',(-3,-4,6),700,5,(1,.91,.8));light('Soft fill',(4,-2,3),450,4,(.81,.91,1));light('Rim',(1,3,5),800,3,(1,.95,.88))
d=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',d);scene.collection.objects.link(cam);cam.location=(0,-6,2.8);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=5.6;scene.camera=cam
# Fixed prop attachment, no IK or changes to generated arm/hand trajectories.
b=rig.data.bones;h=b['Hand.R'];mid=(b['Index1.R'].head_local+b['Pinky1.R'].head_local)*.5
axis=(b['Index1.R'].head_local-b['Pinky1.R'].head_local).normalized()
local_axis=h.matrix_local.to_3x3().inverted()@axis
cradle=h.matrix_local.inverted()@(mid+Vector((.023,0,.01)))
prop=bpy.data.objects.new('Review_RacketSocket',None);scene.collection.objects.link(prop)
# Bone-parent transform includes the bone's tail offset; explicitly cancel it.
prop.parent=rig;prop.parent_type='BONE';prop.parent_bone='Hand.R';prop.matrix_parent_inverse=Matrix.Identity(4)
prop.location=cradle-Vector((0,h.length,0));prop.rotation_mode='QUATERNION';prop.rotation_quaternion=local_axis.to_track_quat('Y','Z') @ Quaternion((0,1,0),math.pi/2)
def tube(n,pts,r,mat,cyclic=False):
 d=bpy.data.curves.new(n,'CURVE');d.dimensions='3D';d.resolution_u=1;d.bevel_depth=r;d.bevel_resolution=2;s=d.splines.new('POLY');s.points.add(len(pts)-1)
 for p,v in zip(s.points,pts):p.co=(*v,1)
 s.use_cyclic_u=cyclic;o=bpy.data.objects.new(n,d);scene.collection.objects.link(o);o.data.materials.append(mat);o.parent=prop;return o
props=[prop]
props.append(tube('Handle',[(0,-.09,0),(0,.13,0)],.018,grip))
props.append(tube('ThroatL',[(0,.11,0),(-.085,.29,0)],.012,blue));props.append(tube('ThroatR',[(0,.11,0),(.085,.29,0)],.012,blue))
props.append(tube('Hoop',[(.15*math.sin(i*2*math.pi/64),.43+.205*math.cos(i*2*math.pi/64),0) for i in range(64)],.015,blue,True))
for x in [-.12,-.09,-.06,-.03,0,.03,.06,.09,.12]:
 dy=.205*math.sqrt(1-(x/.15)**2);props.append(tube('String',[(x,.43-dy,0),(x,.43+dy,0)],.0015,strings))
for dy in [-.16,-.12,-.08,-.04,0,.04,.08,.12,.16]:
 dx=.15*math.sqrt(1-(dy/.205)**2);props.append(tube('String',[(-dx,.43+dy,0),(dx,.43+dy,0)],.0015,strings))
# Two simultaneous views in one shot. Copies share mesh data but use independent rigs.
all_original=[rig]+meshes+props;copies={}
for o in all_original:
 copy=o.copy();copy.data=o.data.copy() if o.type=='ARMATURE' else o.data;scene.collection.objects.link(copy);copies[o]=copy
for o,copy in copies.items():
 if o.parent in copies:copy.parent=copies[o.parent]
 for mod in copy.modifiers:
  if mod.type=='ARMATURE' and mod.object in copies:mod.object=copies[mod.object]
rootA=bpy.data.objects.new('Angle_Front',None);scene.collection.objects.link(rootA);rootA.location=(-1.08,0,0);rootA.rotation_euler.z=math.radians(-20)
rootB=bpy.data.objects.new('Angle_Side',None);scene.collection.objects.link(rootB);rootB.location=(1.08,0,0);rootB.rotation_euler.z=math.radians(72)
for ob in all_original:
 if ob.parent not in all_original:ob.parent=rootA
for ob in copies.values():
 if ob.parent not in copies.values():ob.parent=rootB
rigs=[rig,copies[rig]]
for ob in rigs:ob.animation_data_clear()
(O/'frames').mkdir(exist_ok=True)
missing=[i.filepath for i in bpy.data.images if i.source=='FILE' and not i.packed_file and not Path(bpy.path.abspath(i.filepath)).exists()]
print('MISSING_TEXTURES',missing,flush=True)
# Still mode for inspecting rest or generated sample before full rendering.
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
clips=sorted((O/'motions').glob('*.json'))
if args and args[0]=='rest':clips=[]
for clip in clips:
 data=json.loads(clip.read_text());name=clip.stem
 for r in rigs:
  r.animation_data_clear()
  for pb in r.pose.bones:pb.matrix_basis=Matrix.Identity(4);pb.rotation_mode='QUATERNION'
  for f,rotations in enumerate(data['rotation_quaternion']):
   for n,q in zip(data['names'],rotations):
    pb=r.pose.bones[n];pb.rotation_quaternion=q;pb.keyframe_insert('rotation_quaternion',frame=f+1,group=n)
   r.pose.bones['Hips'].location=data['hips_location'][f];r.pose.bones['Hips'].keyframe_insert('location',frame=f+1,group='Hips')
  r.animation_data.action.name=name+('_front' if r==rig else '_side')
  r.animation_data.action.use_fake_user=True
 scene.frame_start=1;scene.frame_end=60
 if not args:
  (O/'exports').mkdir(exist_ok=True)
  bpy.ops.object.select_all(action='DESELECT')
  for o in [rig]+meshes:o.select_set(True)
  bpy.context.view_layer.objects.active=rig
  old=rootA.matrix_world.copy();rootA.matrix_world=Matrix.Identity(4)
  bpy.ops.export_scene.fbx(filepath=str(O/'exports'/f'UniMate_V5_{name}.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
  rootA.matrix_world=old
 frames=[1,20,40,60] if args and args[0]=='stills' else range(1,61)
 for f in frames:
  target=O/'frames'/f'{name}_{f:03d}.png'
  if target.exists():continue
  scene.frame_set(f);scene.render.filepath=str(target);bpy.ops.render.render(write_still=True)
 print('CLIP_RENDERED',name,flush=True)
if not clips:
 scene.frame_set(1);scene.render.filepath=str(O/'rest_preview.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(O/'HeroV5_UniMate_Review.blend'))
print('RENDER_DONE',flush=True)
