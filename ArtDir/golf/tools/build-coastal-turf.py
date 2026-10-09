"""Accepted Dense15 geometry source. Reuses the packed accepted turf materials/maps.
Runs outside Assets; exports the same three mesh names and UV role schema.
"""
import bpy, math, random, json, argparse, sys, hashlib
from pathlib import Path
from mathutils import Vector

p=argparse.ArgumentParser();p.add_argument('--repo',default=str(Path(__file__).resolve().parents[3]));p.add_argument('--out',required=True);p.add_argument('--render',action='store_true')
args=p.parse_args(sys.argv[sys.argv.index('--')+1:]);repo=Path(args.repo).resolve();out=Path(args.out).resolve();out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(repo/'ArtDir/golf/source/turf-history/CoastalTurf8.blend'))
scene=bpy.context.scene
asset=bpy.data.collections['EXPORT_CLUSTERED_TURF']
for collection in list(scene.collection.children):collection.hide_render=True
proof=bpy.data.collections.new('VISIBLE_BLADE_PROOF');scene.collection.children.link(proof)
def obj(name,v,f,coll):
 m=bpy.data.meshes.new(name);m.from_pydata(v,[],f);m.update();o=bpy.data.objects.new(name,m);coll.objects.link(o);return o
def material(name,color):
 m=bpy.data.materials.new(name);m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1);m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.85;return m

# A compact physically emerging fan replaces the isolated three-blade stars.
# All LODs retain corresponding tuft seeds and actual roots.
rng=random.Random(121515)
sites=[(i,j) for j in range(12) for i in range(16)];rng.shuffle(sites);tufts=[]
for i,j in sites[:192]:
 cx=(i+rng.uniform(.12,.88))/16-.5;cy=(j+rng.uniform(.12,.88))/12-.5;axis=rng.uniform(0,math.tau);shade=rng.uniform(.78,1.10);members=[]
 for k in range(12):
  a=axis+(k-5.5)*math.tau/12+rng.uniform(-.20,.20);r=rng.uniform(.003,.017)
  # A low interlocking layer and a few emerging tips leave the golf ball
  # readable; the field is not a uniformly tall opaque wall.
  height=rng.uniform(.024,.040) if k<3 else rng.uniform(.065,.085) if k==11 else rng.uniform(.045,.065)
  width=rng.uniform(.006,.010)
  bend=rng.uniform(.060,.100)
  members.append((cx+math.cos(a)*r,cy+math.sin(a)*r,a,height,width,bend,shade+rng.uniform(-.035,.035),rng.random()<.72))
 tufts.append(members)

def blade(v,f,roles,seed,detail):
 x,y,a,h,w,lean,shade,hooked=seed;root=Vector((x,y,0));d=Vector((math.cos(a),math.sin(a),0));side=Vector((-d.y,d.x,0));idx=len(v)
 mid=root+d*lean*.40+Vector((0,0,h*.86))
 tip=root+d*lean+Vector((0,0,h*(.88 if hooked else 1)))
 if detail==2:
  verts=[root-side*w*.5,root+Vector((0,0,w*.16)),root+side*w*.5,mid-side*w*.33,mid+Vector((0,0,w*.38)),mid+side*w*.33,tip]
  faces=[(0,3,1),(1,3,4),(1,4,2),(2,4,5),(3,6,4),(4,6,5)];t=[0,0,0,.68,.68,.68,1]
 elif detail==1:
  verts=[root-side*w*.5,root+side*w*.5,mid-side*w*.32,mid+side*w*.32,tip];faces=[(0,2,1),(1,2,3),(2,4,3)];t=[0,0,.68,.68,1]
 else:
  verts=[root-side*w*.5,root+side*w*.5,tip];faces=[(0,2,1)];t=[0,0,1]
 v.extend(tuple(q) for q in verts);f.extend(tuple(idx+k for k in face) for face in faces);roles.extend((shade,tip) for tip in t)
def mesh(name,detail):
 v=[];f=[];roles=[];blades=0
 selected=tufts if name!='TURF_FAR' else tufts[:128]
 for index,tuft in enumerate(selected):
  chosen=tuft if name=='TURF_NEAR' else [tuft[k] for k in ([0,4,8] if index<128 else [4,8])] if name=='TURF_MID' else [tuft[5]]
  for seed in chosen:blade(v,f,roles,seed,detail);blades+=1
 m=bpy.data.meshes.new(name+'_EmergentFans');m.from_pydata(v,[],f);m.update();uv=m.uv_layers.new(name='TurfRole');col=m.color_attributes.new(name='TurfShade',type='FLOAT_COLOR',domain='POINT')
 for i,(shade,t) in enumerate(roles):col.data[i].color=(.055*shade+t*.025,.17*shade+t*.095,.029*shade+t*.013,1)
 for loop in m.loops:uv.data[loop.index].uv=roles[loop.vertex_index]
 for face in m.polygons:face.use_smooth=True
 return m,{'blades':blades,'vertices':len(v),'triangles':len(f),'heightMaxMeters':max(q[2] for q in v)}

near=bpy.data.objects['TURF_NEAR'];old=near.data;old.use_fake_user=True
counts={};newmeshes={}
for name,detail in [('TURF_NEAR',1),('TURF_MID',1),('TURF_FAR',0)]:
 m,count=mesh(name,detail);counts[name]=count;newmeshes[name]=m
 for mat in bpy.data.objects[name].data.materials:m.materials.append(mat)

# Plain white blades on charcoal ground make silhouette and true emergence
# assessable independently of the accepted grass palette/texture.
white=material('PROOF_WHITE_PHYSICAL_BLADES',(.72,.72,.72));dark=material('PROOF_DARK_GROUND',(.025,.025,.025))
plane=obj('PROOF_GROUND',[(-7,-2,-.002),(7,-2,-.002),(7,14,-.002),(-7,14,-.002)],[(0,1,2,3)],proof);plane.data.materials.append(bpy.data.materials['PROOF_CLUSTERED_GROUND'])
for node in plane.data.materials[0].node_tree.nodes:
  if node.type=='MATH' and node.operation=='SUBTRACT':node.inputs[1].default_value=.0907707
  if node.type=='NORMAL_MAP':node.inputs['Strength'].default_value=.65
bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,radius=.06*.9144,location=(0,0,.06*.9144))
ball=bpy.context.object;ball.name='PROOF_REAL_SIZE_GOLF_BALL';
for c in list(ball.users_collection):c.objects.unlink(ball)
proof.objects.link(ball);ball.data.materials.append(white)
oldproof=old.copy()
newproof=newmeshes['TURF_NEAR'].copy()
tiles=[]
for j in range(6):
 for i in range(8):
  o=bpy.data.objects.new(f'VISIBLE_TILE_{i}_{j}',oldproof);proof.objects.link(o);o.location=(i-3.5,j-.5,0);tiles.append(o)
sun=bpy.data.lights.new('PROOF_SUN','SUN');sun.energy=2;sun.angle=.06;so=bpy.data.objects.new('PROOF_SUN',sun);proof.objects.link(so);so.rotation_euler=(.45,-.55,-.80)
world=bpy.data.worlds.new('Emergent Turf Studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.2,.24,.30,1);world.node_tree.nodes['Background'].inputs[1].default_value=.35;scene.world=world
camd=bpy.data.cameras.new('PHYSICAL_CAMERA');cam=bpy.data.objects.new('PHYSICAL_CAMERA',camd);proof.objects.link(cam);scene.camera=cam
camd.type='PERSP';camd.sensor_fit='VERTICAL';camd.sensor_height=24;camd.lens=24/(2*math.tan(math.radians(70)/2))
cam.location=(.25*.9144,-4.5*.9144,3.2*.9144);direction=Vector((0,math.cos(math.radians(12)),-math.sin(math.radians(12))));cam.rotation_euler=direction.to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=12;scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.resolution_x=900;scene.render.resolution_y=1600;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
if args.render:
 scene.render.filepath=str(out/'dense-green-before-camera.png');bpy.ops.render.render(write_still=True)
for tile in tiles:tile.data=newproof
if args.render:
 scene.render.filepath=str(out/'dense-green-candidate-camera.png');bpy.ops.render.render(write_still=True)
 cam.location=(-1.1,-1.7,.45);cam.rotation_euler=(Vector((0,.7,.03))-cam.location).to_track_quat('-Z','Y').to_euler();camd.sensor_fit='AUTO';camd.lens=45;scene.render.resolution_x=1400;scene.render.resolution_y=900;scene.render.filepath=str(out/'dense-green-source-detail.png');bpy.ops.render.render(write_still=True)

# Plain silhouette control exposes overlap independently from the green underlay.
newproof.materials.clear();newproof.materials.append(white);plane.data.materials.clear();plane.data.materials.append(dark)
if args.render:
 scene.render.filepath=str(out/'dense-blades-plain-silhouette.png');bpy.ops.render.render(write_still=True)
for name,m in newmeshes.items():bpy.data.objects[name].data=m
for o in scene.objects:o.select_set(o.name in counts)
bpy.context.view_layer.objects.active=near
bpy.ops.export_scene.fbx(filepath=str(out/'CoastalTurf.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_anim=False,mesh_smooth_type='OFF',use_mesh_modifiers=True,path_mode='STRIP',embed_textures=False)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'CoastalTurf.blend'))
cap=48*counts['TURF_NEAR']['triangles']+96*counts['TURF_MID']['triangles']+496*counts['TURF_FAR']['triangles']
assert cap==542720
report={'status':'SOURCE_REBUILT; geometry accepted by Dense15 actual capture; current material response is runtime-owned','sourceInput':'ArtDir/golf/source/turf-history/CoastalTurf8.blend','inputSha256':hashlib.sha256((repo/'ArtDir/golf/source/turf-history/CoastalTurf8.blend').read_bytes()).hexdigest(),'lods':counts,'tuftsPerSquareMeter':192,'nearBladesPerTuft':12,'nearWidthMeters':[.006,.010],'cutHeightMeters':[.024,.085],'existingRoughScale':1.8,'roughResultHeightMeters':[.0432,.153],'budget':{'nearCells':48,'midCells':96,'maximumCells':640,'maximumTriangles':cap,'submissions':3},'mapsPaletteShaders':'UNCHANGED; export mesh geometry only','proofCamera':'Reference camera2: 70deg vertical FOV, camera -4.5yards,+3.2yards and12deg downward; exact source meters converted using .9144. Plain48-tile near patch, not a Unity performance/appearance certificate.','files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in out.glob('*') if p.suffix in ('.fbx','.blend','.png')}}
(out/'candidate-manifest.json').write_text(json.dumps(report,indent=2)+'\n');print('EMERGENT_TURF='+json.dumps(report),flush=True)
