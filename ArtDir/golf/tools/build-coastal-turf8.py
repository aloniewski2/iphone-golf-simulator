"""Editable clustered cut/rough turf; meter units, three bounded rendering LODs.
No course/collider geometry is included. C/N maps are periodic geometry bakes.
"""
import bpy,math,random,sys,argparse,json,hashlib
from pathlib import Path
from mathutils import Vector
p=argparse.ArgumentParser();p.add_argument('--out',required=True);p.add_argument('--render',action='store_true');args=p.parse_args(sys.argv[sys.argv.index('--')+1:]);OUT=Path(args.out);OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=12;scene.cycles.use_denoising=False;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.resolution_x=1024;scene.render.resolution_y=1024;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
asset=bpy.data.collections.new('EXPORT_CLUSTERED_TURF');scene.collection.children.link(asset);bake=bpy.data.collections.new('AUTHOR_PERIODIC_TUFT_MAP');scene.collection.children.link(bake);proof=bpy.data.collections.new('PROOF_ONLY');scene.collection.children.link(proof)
def obj(name,v,f,coll):
 m=bpy.data.meshes.new(name);m.from_pydata(v,[],f);m.update();o=bpy.data.objects.new(name,m);coll.objects.link(o);return o
def blade(v,f,roles,x,y,a,h,w,lean,shade,detail=2):
 root=Vector((x,y,0));d=Vector((math.cos(a),math.sin(a),0));s=Vector((-d.y,d.x,0));idx=len(v)
 if detail==2:
  mid=root+d*lean*.36+Vector((0,0,h*.64));tip=root+d*lean+Vector((0,0,h))
  v.extend(tuple(q) for q in [root-s*w*.5,root+Vector((0,0,w*.12)),root+s*w*.5,mid-s*w*.36,mid+Vector((0,0,w*.36)),mid+s*w*.36,tip]);f.extend(tuple(idx+i for i in t) for t in [(0,3,1),(1,3,4),(1,4,2),(2,4,5),(3,6,4),(4,6,5)]);roles.extend([(shade,t) for t in [0,0,0,.64,.64,.64,1]])
 elif detail==1:
  mid=root+d*lean*.38+Vector((0,0,h*.62));tip=root+d*lean+Vector((0,0,h));v.extend(tuple(q) for q in [root-s*w*.5,root+s*w*.5,mid-s*w*.34,mid+s*w*.34,tip]);f.extend(tuple(idx+i for i in t) for t in [(0,2,1),(1,2,3),(2,4,3)]);roles.extend([(shade,t) for t in [0,0,.62,.62,1]])
 else:
  tip=root+d*lean+Vector((0,0,h));v.extend(tuple(q) for q in [root-s*w*.5,root+s*w*.5,tip]);f.append((idx,idx+2,idx+1));roles.extend([(shade,t) for t in [0,0,1]])
def finish(o,roles,attribute='TurfShade'):
 uv=o.data.uv_layers.new(name='TurfRole');col=o.data.color_attributes.new(name=attribute,type='FLOAT_COLOR',domain='POINT')
 for i,(shade,t) in enumerate(roles):col.data[i].color=(.055*shade+t*.025,.17*shade+t*.095,.029*shade+t*.013,1)
 for loop in o.data.loops:uv.data[loop.index].uv=roles[loop.vertex_index]
 for poly in o.data.polygons:poly.use_smooth=True
# Tuft identities and their directional splay survive all LODs.
rng=random.Random(121258);tufts=[]
for j in range(16):
 for i in range(16):
  cx=(i+rng.uniform(.05,.95))/16-.5;cy=(j+rng.uniform(.05,.95))/16-.5;direction=rng.uniform(0,math.tau);shade=rng.uniform(.74,1.12);members=[]
  for k in range(3):
   a=direction+rng.uniform(-1.3,1.3);r=rng.uniform(0,.007)
   members.append((cx+math.cos(a)*r,cy+math.sin(a)*r,a,rng.uniform(.024,.044),rng.uniform(.003,.0052),rng.uniform(.022,.042),shade+rng.uniform(-.06,.06)))
  tufts.append(members)
mat=bpy.data.materials.new('MAT_CLUSTERED_CUT_TURF');mat.use_nodes=True;nt=mat.node_tree;att=nt.nodes.new('ShaderNodeAttribute');att.attribute_name='TurfShade';coord=nt.nodes.new('ShaderNodeTexCoord');separate=nt.nodes.new('ShaderNodeSeparateXYZ');nt.links.new(coord.outputs['UV'],separate.inputs[0]);mix=nt.nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.inputs[1].default_value=(.19,.36,.16,1);mix.inputs[2].default_value=(.39,.56,.245,1);nt.links.new(separate.outputs['Y'],mix.inputs[0]);nt.links.new(mix.outputs[0],nt.nodes.get('Principled BSDF').inputs['Base Color']);nt.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.85
counts={}
for name,stride,blades,detail in [('TURF_NEAR',1,3,2),('TURF_MID',2,1,1),('TURF_FAR',4,1,0)]:
 v=[];f=[];roles=[]
 for members in tufts[::stride]:
  for seed in members[:blades]:blade(v,f,roles,*seed,detail)
 o=obj(name,v,f,asset);o.data.materials.append(mat);finish(o,roles);counts[name]={'blades':len(tufts[::stride])*blades,'vertices':len(v),'triangles':len(f)}
for o in scene.objects:o.select_set(o.name in counts)
bpy.context.view_layer.objects.active=bpy.data.objects['TURF_NEAR'];bpy.ops.export_scene.fbx(filepath=str(OUT/'CoastalTurf.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_anim=False,mesh_smooth_type='OFF',use_mesh_modifiers=True,path_mode='STRIP',embed_textures=False)
asset.hide_render=True
# Dense interlocking source tufts for the normal/albedo field. The blades are
# curved in 3D, not lying flat in a raster emission field. A local AO bake retains
# the root creases without encoding directional sunlight into the albedo.
v=[];f=[];roles=[];rng=random.Random(121259)
for j in range(32):
 for i in range(32):
  cx=(i+rng.uniform(.05,.95))/32-.5;cy=(j+rng.uniform(.05,.95))/32-.5;direction=rng.uniform(0,math.tau);shade=rng.uniform(.75,1.08)
  for k in range(12):
   a=direction+rng.uniform(-1.5,1.5);rr=rng.uniform(0,.012);x=cx+math.cos(a)*rr;y=cy+math.sin(a)*rr;h=rng.uniform(.024,.043);w=rng.uniform(.0035,.0054);lean=rng.uniform(.027,.049)
   for dx in (-1,0,1):
    for dy in (-1,0,1):
     if abs(x+dx)>.55 or abs(y+dy)>.55:continue
     blade(v,f,roles,x+dx,y+dy,a,h,w,lean,shade,2)
field=obj('AUTHOR_INTERLOCKING_CURVED_TUFTS',v,f,bake);finish(field,roles,'BladeAlbedo')
base=obj('AUTHOR_ROOT_CREASE_UNDERSTORY',[(-1,-1,-.001),(1,-1,-.001),(1,1,-.001),(-1,1,-.001)],[(0,1,2,3)],bake)
for o in [field,base]:
 m=bpy.data.materials.new(o.name+'_BAKE');m.use_nodes=True;o.data.materials.append(m);nt=m.node_tree;nt.nodes.clear();em=nt.nodes.new('ShaderNodeEmission');out=nt.nodes.new('ShaderNodeOutputMaterial');nt.links.new(em.outputs[0],out.inputs[0]);att=nt.nodes.new('ShaderNodeAttribute');att.attribute_name='BladeAlbedo';ao=nt.nodes.new('ShaderNodeAmbientOcclusion');ao.inputs['Distance'].default_value=.055;ao.samples=8;mix=nt.nodes.new('ShaderNodeMixRGB');mix.name='BAKE_AO_MULTIPLY';mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.72
 if o==field:nt.links.new(att.outputs['Color'],mix.inputs[1])
 else:mix.inputs[1].default_value=(.038,.098,.024,1)
 nt.links.new(ao.outputs['Color'],mix.inputs[2])
 nt.links.new(mix.outputs[0],em.inputs[0])
camd=bpy.data.cameras.new('MAP_CAMERA');cam=bpy.data.objects.new('MAP_CAMERA',camd);scene.collection.objects.link(cam);scene.camera=cam;cam.location=(0,0,1);cam.rotation_euler=(0,0,0);camd.type='ORTHO';camd.ortho_scale=1
world=bpy.data.worlds.new('Daylight');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.48,.65,.78,1);world.node_tree.nodes['Background'].inputs[1].default_value=.55
for channel in ('C','N'):
 for o in [field,base]:
  nt=o.data.materials[0].node_tree;em=nt.nodes.get('Emission')
  for link in list(nt.links):
   if link.to_node==em:nt.links.remove(link)
  if channel=='C':nt.links.new(nt.nodes.get('BAKE_AO_MULTIPLY').outputs[0],em.inputs[0])
  else:
   geo=nt.nodes.new('ShaderNodeNewGeometry');scale=nt.nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=.5;add=nt.nodes.new('ShaderNodeVectorMath');add.operation='ADD';add.inputs[1].default_value=(.5,.5,.5);nt.links.new(geo.outputs['Normal'],scale.inputs[0]);nt.links.new(scale.outputs[0],add.inputs[0]);nt.links.new(add.outputs[0],em.inputs[0])
 scene.view_settings.view_transform='Standard' if channel=='C' else 'Raw';scene.view_settings.look='None';scene.render.filepath=str(OUT/f'CoastalTurf_{channel}.png');bpy.ops.render.render(write_still=True)
bake.hide_render=True
tex=bpy.data.images.load(str(OUT/'CoastalTurf_C.png'));tex.pack();normal=bpy.data.images.load(str(OUT/'CoastalTurf_N.png'));normal.colorspace_settings.name='Non-Color';normal.pack()
# Source comparison proof: physically grounded tiles at the actual camera height.
groundmat=bpy.data.materials.new('PROOF_CLUSTERED_GROUND');groundmat.use_nodes=True;nt=groundmat.node_tree;coord=nt.nodes.new('ShaderNodeTexCoord');node=nt.nodes.new('ShaderNodeTexImage');node.image=tex;nt.links.new(coord.outputs['Object'],node.inputs[0]);rgb=nt.nodes.new('ShaderNodeVectorMath');rgb.operation='DOT_PRODUCT';rgb.inputs[1].default_value=(.2126,.7152,.0722);nt.links.new(node.outputs[0],rgb.inputs[0]);sub=nt.nodes.new('ShaderNodeMath');sub.operation='SUBTRACT';sub.inputs[1].default_value=.1;nt.links.new(rgb.outputs['Value'],sub.inputs[0]);scale=nt.nodes.new('ShaderNodeMath');scale.operation='MULTIPLY_ADD';scale.inputs[1].default_value=6.5;scale.inputs[2].default_value=.5;nt.links.new(sub.outputs[0],scale.inputs[0]);ramp=nt.nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.235,.38,.17,1);ramp.color_ramp.elements[1].color=(.41,.565,.255,1);nt.links.new(scale.outputs[0],ramp.inputs[0]);nt.links.new(ramp.outputs[0],nt.nodes.get('Principled BSDF').inputs['Base Color']);nn=nt.nodes.new('ShaderNodeTexImage');nn.image=normal;nt.links.new(coord.outputs['Object'],nn.inputs[0]);nmap=nt.nodes.new('ShaderNodeNormalMap');nmap.inputs['Strength'].default_value=.75;nt.links.new(nn.outputs[0],nmap.inputs[1]);nt.links.new(nmap.outputs[0],nt.nodes.get('Principled BSDF').inputs['Normal']);nt.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.83
plane=obj('PROOF_GROUND',[(-5,-3,-.002),(5,-3,-.002),(5,8,-.002),(-5,8,-.002)],[(0,1,2,3)],proof);plane.data.materials.append(groundmat)
for iy in range(11):
 for ix in range(10):
  source=bpy.data.objects['TURF_NEAR'];o=bpy.data.objects.new(f'PROOF_TUFT_{ix}_{iy}',source.data);proof.objects.link(o);o.location=(-4.5+ix,-2.5+iy,0)
  if iy==7:o.scale.z=2.1
sun=bpy.data.lights.new('PROOF_SUN','SUN');sun.energy=2.8;sun.angle=.08;so=bpy.data.objects.new('PROOF_SUN',sun);proof.objects.link(so);so.rotation_euler=(.48,-.65,-.72)
camd.type='PERSP';camd.lens=35;cam.location=(-.35,-4.2,3.4);cam.rotation_euler=(Vector((0,2.6,0))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.cycles.samples=20;scene.cycles.use_denoising=True;scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
if args.render:scene.render.filepath=str(OUT/'clustered-turf-camera-height.png');bpy.ops.render.render(write_still=True)
# Source close-up confirms the actual curved folded lamina and fuller rough edge.
cam.location=(-1.5,-2.2,.52);cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.resolution_x=1400;scene.render.resolution_y=900
if args.render:scene.render.filepath=str(OUT/'clustered-turf-source-detail.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CoastalTurf.blend'))
report={'status':'SOURCE CANDIDATE; actual camera grass gate pending','tileSizeMeters':1,'lods':counts,'grassHeightMeters':[.024,.044],'roughHeightScale':2.1,'tuftSpreadMeters':[.05,.09],'atlasTileMeters':1,'textureResolution':1024,'atlasDescription':'periodic geometry bake of1024 overlapping12-blade directional tufts; curved lamina normals and nondirectional root AO','geometryCap':{'nearCells':48,'midCells':96,'maximumCells':640,'maximumTriangles':48*counts['TURF_NEAR']['triangles']+96*counts['TURF_MID']['triangles']+(640-48-96)*counts['TURF_FAR']['triangles'],'castsShadows':False,'expectedDrawSubmissions':3},'files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in OUT.glob('*') if p.suffix in ('.png','.fbx','.blend')}}
(OUT/'coastal-turf-manifest.json').write_text(json.dumps(report,indent=2));print('CLUSTERED_TURF',json.dumps(report),flush=True)
